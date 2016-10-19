using System;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using Nima.Math2D;

using System.Collections.Generic;

namespace Nima.OpenGL
{
    public class Renderer2D : Renderer
    {
        static Dictionary<string, string> m_IncludedShaders = new Dictionary<string, string>{
            {
                "Nima-OpenTK/Shaders/Textured.vs", 
                "attribute vec2 VertexPosition; attribute vec2 VertexTexCoord; uniform mat4 ProjectionMatrix; uniform mat4 WorldMatrix; uniform mat4 ViewMatrix; varying vec2 TexCoord; void main(void) {TexCoord = VertexTexCoord; vec4 pos = ViewMatrix * WorldMatrix * vec4(VertexPosition.x, VertexPosition.y, 0.0, 1.0); gl_Position = ProjectionMatrix * vec4(pos.xyz, 1.0); }"
            },
			{
                "Nima-OpenTK/Shaders/Textured.fs", 
                "#ifdef GL_ES \nprecision highp float;\n #endif\n uniform vec4 Color; uniform float Opacity; uniform sampler2D TextureSampler; varying vec2 TexCoord; void main(void) {vec4 color = texture2D(TextureSampler, TexCoord) * Color; color.a *= Opacity; gl_FragColor = color; }" 
            },
			{
                "Nima-OpenTK/Shaders/TexturedSkin.vs", 
                "attribute vec2 VertexPosition; attribute vec2 VertexTexCoord; attribute vec4 VertexBoneIndices; attribute vec4 VertexWeights; uniform mat4 ProjectionMatrix; uniform mat4 WorldMatrix; uniform mat4 ViewMatrix; uniform vec3 BoneMatrices[82]; varying vec2 TexCoord; void main(void) {TexCoord = VertexTexCoord; vec2 position = vec2(0.0, 0.0); vec4 p = WorldMatrix * vec4(VertexPosition.x, VertexPosition.y, 0.0, 1.0); float x = p[0]; float y = p[1]; for(int i = 0; i < 4; i++) {float weight = VertexWeights[i]; int matrixIndex = int(VertexBoneIndices[i])*2; vec3 m = BoneMatrices[matrixIndex]; vec3 n = BoneMatrices[matrixIndex+1]; position[0] += (m[0] * x + m[2] * y + n[1]) * weight; position[1] += (m[1] * x + n[0] * y + n[2]) * weight; } vec4 pos = ViewMatrix * vec4(position.x, position.y, 0.0, 1.0); gl_Position = ProjectionMatrix * vec4(pos.xyz, 1.0); }"
            }
        };

        protected override Dictionary<string, string> GetIncludedShaders()
        {
            return m_IncludedShaders;
        }
        
        ShaderProgram m_TexturedShader;
        ShaderProgram m_TexturedSkinShader;
        Matrix4 m_Projection;
        Matrix4 m_Transform;
        Matrix4 m_ViewTransform;

        int m_ViewportWidth;
        int m_ViewportHeight;

        public enum BlendModes
        {
            Off = 0,
            Transparent,
            Multiply,
            Screen,
            Additive
        }

        private BlendModes m_BlendMode;

        public Renderer2D()
        {
            m_Projection = new Matrix4();
            m_Transform = new Matrix4();
            m_ViewTransform = new Matrix4();

            m_TexturedShader = InitProgram("Nima-OpenTK/Shaders/Textured.vs", "Nima-OpenTK/Shaders/Textured.fs", 
                new ShaderAttribute[] {
                    new ShaderAttribute("VertexPosition", 2, 4, 0),
                    new ShaderAttribute("VertexTexCoord", 2, 4, 8)
                },
                new string[] {
                    "ProjectionMatrix",
                    "ViewMatrix",
                    "WorldMatrix",
                    "TextureSampler",
                    "Opacity",
                    "Color"
                });

            m_TexturedSkinShader = InitProgram("Nima-OpenTK/Shaders/TexturedSkin.vs", "Nima-OpenTK/Shaders/Textured.fs", 
                new ShaderAttribute[] {
                    new ShaderAttribute("VertexPosition", 2, 12, 0),
                    new ShaderAttribute("VertexTexCoord", 2, 12, 8),
                    new ShaderAttribute("VertexBoneIndices", 4, 12, 16),
                    new ShaderAttribute("VertexWeights", 4, 12, 32)
                },
                new string[] {
                    "ProjectionMatrix",
                    "ViewMatrix",
                    "WorldMatrix",
                    "TextureSampler",
                    "Opacity",
                    "Color",
                    "BoneMatrices"
                });
        }

        public BlendModes BlendMode
        {
            get
            {
                return m_BlendMode;
            }
            set
            {
                if(m_BlendMode == value)
                {
                    return;
                }
                m_BlendMode = value;
                switch(m_BlendMode)
                {
                    case BlendModes.Off:
                        GL.Disable(EnableCap.Blend);
                        
                        break;
                    case BlendModes.Transparent:
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
                        break;
                    case BlendModes.Additive:
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.One, BlendingFactorSrc.One, BlendingFactorDest.One);
                        break;
                    case BlendModes.Multiply:
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFuncSeparate(BlendingFactorSrc.DstColor, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.DstAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                        break;
                    case BlendModes.Screen:
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcColor, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
                        break;
                }
            }
        }
        public void Resize(int width, int height)
        {
            GL.Viewport(0, 0, width, height);
            m_ViewportWidth = width;
            m_ViewportHeight = height;
            Matrix4.CreateOrthographic(width, height, 0, 1, out m_Projection);
        }

        public void DrawTextured(float[] view, float[] transform, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, float opacity, Color4 color, Texture texture)
        {
            m_ViewTransform[0,0] = view[0];
            m_ViewTransform[1,0] = view[1];
            m_ViewTransform[0,1] = view[2];
            m_ViewTransform[1,1] = view[3];
            m_ViewTransform[0,3] = view[4];
            m_ViewTransform[1,3] = view[5];

            m_Transform[0,0] = transform[0];
            m_Transform[1,0] = transform[1];
            m_Transform[0,1] = transform[2];
            m_Transform[1,1] = transform[3];
            m_Transform[0,3] = transform[4];
            m_Transform[1,3] = transform[5];

            Bind(m_TexturedShader, vertexBuffer);

            int[] u = m_TexturedShader.Uniforms;
            GL.UniformMatrix4(u[0], false, ref m_Projection);
            GL.UniformMatrix4(u[1], false, ref m_ViewTransform);
            GL.UniformMatrix4(u[2], false, ref m_Transform);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture.Id);
            GL.Uniform1(u[3], 0);

            GL.Uniform1(u[4], opacity);
            GL.Uniform4(u[5], color);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBuffer.Id);
            GL.DrawElements(BeginMode.Triangles, indexBuffer.Size, DrawElementsType.UnsignedShort, 0);
        }

        public void DrawTexturedSkin(float[] view, float[] transform, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, float[] boneMatrices, float opacity, Color4 color, Texture texture)
        {
            m_ViewTransform[0,0] = view[0];
            m_ViewTransform[1,0] = view[1];
            m_ViewTransform[0,1] = view[2];
            m_ViewTransform[1,1] = view[3];
            m_ViewTransform[0,3] = view[4];
            m_ViewTransform[1,3] = view[5];

            m_Transform[0,0] = transform[0];
            m_Transform[1,0] = transform[1];
            m_Transform[0,1] = transform[2];
            m_Transform[1,1] = transform[3];
            m_Transform[0,3] = transform[4];
            m_Transform[1,3] = transform[5];

            Bind(m_TexturedSkinShader, vertexBuffer);

            int[] u = m_TexturedSkinShader.Uniforms;
            GL.UniformMatrix4(u[0], false, ref m_Projection);
            GL.UniformMatrix4(u[1], false, ref m_ViewTransform);
            GL.UniformMatrix4(u[2], false, ref m_Transform);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture.Id);
            GL.Uniform1(u[3], 0);

            GL.Uniform1(u[4], opacity);
            GL.Uniform4(u[5], color);
            GL.Uniform3(u[6], boneMatrices.Length, boneMatrices);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBuffer.Id);
            GL.DrawElements(BeginMode.Triangles, indexBuffer.Size, DrawElementsType.UnsignedShort, 0);
        }
    }
}