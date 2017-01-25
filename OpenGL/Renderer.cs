using System;
using System.IO;
using OpenTK.Graphics;
using OpenTK.Graphics.ES20;
using System.Collections.Generic;

namespace Nima.OpenGL
{
    public class ShaderLoadException : System.Exception
    {
        public ShaderLoadException() : base() { }
        public ShaderLoadException(string message) : base(message) { }
        public ShaderLoadException(string message, System.Exception inner) : base(message, inner) { }
    }

    public class Renderer
    {
        protected class Shader
        {
            private int m_Id;
            private string m_Filename;
            public Shader(string filename, Dictionary<string, string> includedShaders)
            {
                m_Filename = filename;
                ShaderType type = ShaderType.VertexShader;
                int index = filename.LastIndexOf(".");
                if (index != -1)
                {
                    string extension = filename.Substring(index + 1);
                    if (extension == "fs")
                    {
                        type = ShaderType.FragmentShader;
                    }
                }
                m_Id = GL.CreateShader(type);

                string contents = null;
                if (includedShaders != null)
                {
                    includedShaders.TryGetValue(filename, out contents);

                }
                if (contents == null)
                {
                    using (FileStream stream = File.OpenRead(filename))
                    {
                        using(StreamReader reader = new StreamReader(stream))
                        {
                            contents = reader.ReadToEnd();
                        }
                    }
                }

                if (contents == null)
                {
                    throw new ShaderLoadException(string.Format("Couln't find source for shader {0}).", filename));
                }

                GL.ShaderSource(m_Id, contents);
                GL.CompileShader(m_Id);

                int[] p = { 0 };
                GL.GetShader(m_Id, ShaderParameter.CompileStatus, p);
                if (p[0] == (int)All.False)
                {
                    string infoLog = GL.GetShaderInfoLog(m_Id);
                    throw new ShaderLoadException(infoLog);
                }
            }

            ~Shader()
            {
                GL.DeleteShader(m_Id);
            }

            public int Id
            {
                get
                {
                    return m_Id;
                }
            }

            public string Filename
            {
                get
                {
                    return m_Filename;
                }
            }
        }

        protected class ShaderAttribute
        {
            private string m_Name;
            private int m_Position;
            private int m_Size;
            private int m_Stride;
            private int m_StrideInBytes;
            private int m_Offset;


            public ShaderAttribute(string name, int size, int stride, int offset)
            {
                m_Name = name;
                m_Position = -1;
                m_Size = size;
                m_Stride = stride;
                m_StrideInBytes = stride * sizeof(float);
                m_Offset = offset;
            }
            public ShaderAttribute(ShaderAttribute attribute, int position)
            {
                m_Name = attribute.m_Name;
                m_Position = position;
                m_Size = attribute.m_Size;
                m_Stride = attribute.m_Stride;
                m_StrideInBytes = attribute.m_StrideInBytes;
                m_Offset = attribute.m_Offset;
            }

            public string Name
            {
                get
                {
                    return m_Name;
                }
            }


            public int Position
            {
                get
                {
                    return m_Position;
                }
            }
            public int Size
            {
                get
                {
                    return m_Size;
                }
            }
            public int Stride
            {
                get
                {
                    return m_Stride;
                }
            }

            public int StrideInBytes
            {
                get
                {
                    return m_StrideInBytes;
                }
            }
            public int Offset
            {
                get
                {
                    return m_Offset;
                }
            }
        }

        protected class ShaderProgram
        {
            private Shader m_VertexShader;
            private Shader m_FragmentShader;
            private ShaderAttribute[] m_Attributes;
            private ShaderAttribute[] m_SecondaryAttributes;
            private int[] m_Uniforms;

            private int m_Id;

            public ShaderProgram(Shader vs, Shader fs, ShaderAttribute[] attributes, string[] uniforms) : this(vs, fs, attributes, null, uniforms)
            {
                
            }

            public ShaderProgram(Shader vs, Shader fs, ShaderAttribute[] attributes, ShaderAttribute[] secondaryAttributes, string[] uniforms)
            {
                m_VertexShader = vs;
                m_FragmentShader = fs;
                m_Id = GL.CreateProgram();
                GL.AttachShader(m_Id, m_VertexShader.Id);
                GL.AttachShader(m_Id, m_FragmentShader.Id);
                GL.LinkProgram(m_Id);
                GL.UseProgram(m_Id);

                m_Attributes = new ShaderAttribute[attributes.Length];
                if (secondaryAttributes != null)
                {
                    m_SecondaryAttributes = new ShaderAttribute[secondaryAttributes.Length];
                }
                m_Uniforms = new int[uniforms.Length];

                int idx = 0;
                foreach (ShaderAttribute attribute in attributes)
                {
                    int location = GL.GetAttribLocation(m_Id, attribute.Name);
                    if (location == -1)
                    {
                        throw new ShaderLoadException(string.Format("Couln't find attribute {0} ({1} | {2}).", attribute.Name, vs.Filename, fs.Filename));
                    }
                    m_Attributes[idx] = new ShaderAttribute(attribute, location);
                    idx++;
                }

                if (secondaryAttributes != null)
                {
                    idx = 0;
                    foreach (ShaderAttribute attribute in secondaryAttributes)
                    {
                        int location = GL.GetAttribLocation(m_Id, attribute.Name);
                        if (location == -1)
                        {
                            throw new ShaderLoadException(string.Format("Couln't find secondary attribute {0} ({1} | {2}).", attribute.Name, vs.Filename, fs.Filename));
                        }
                        m_SecondaryAttributes[idx] = new ShaderAttribute(attribute, location);
                        idx++;
                    }
                }

                idx = 0;
                foreach (string u in uniforms)
                {
                    int location = GL.GetUniformLocation(m_Id, u);
                    if (location == -1)
                    {
                        //throw new ShaderLoadException(string.Format("Couln't find uniform {0} ({1} | {2}).", u, vs.Filename, fs.Filename));
                        Console.WriteLine(string.Format("Couln't find uniform {0} ({1} | {2}).", u, vs.Filename, fs.Filename));
                    }
                    m_Uniforms[idx++] = location;
                }
            }

            ~ShaderProgram()
            {
                if (m_Id != -1)
                {
                    if (m_VertexShader != null)
                    {
                        GL.DetachShader(m_Id, m_VertexShader.Id);
                    }
                    if (m_FragmentShader != null)
                    {
                        GL.DetachShader(m_Id, m_FragmentShader.Id);
                    }
                    GL.DeleteProgram(m_Id);
                }
            }

            public int Id
            {
                get
                {
                    return m_Id;
                }
            }

            public ShaderAttribute[] Attributes
            {
                get
                {
                    return m_Attributes;
                }
            }

            public ShaderAttribute[] SecondaryAttributes
            {
                get
                {
                    return m_SecondaryAttributes;
                }
            }

            public int[] Uniforms
            {
                get
                {
                    return m_Uniforms;
                }
            }
        }
        Dictionary<string, Shader> m_Shaders;

        protected virtual Dictionary<string, string> GetIncludedShaders()
        {
            return null;
        }

        protected ShaderProgram InitProgram(string vsFilename, string fsFilename, ShaderAttribute[] attributes, ShaderAttribute[] secondaryAttributes, string[] uniforms)
        {
            Shader vs = null;
            Shader fs = null;
            if (!m_Shaders.TryGetValue(vsFilename, out vs))
            {
                vs = new Shader(vsFilename, GetIncludedShaders());
                m_Shaders.Add(vsFilename, vs);
            }
            if (!m_Shaders.TryGetValue(fsFilename, out fs))
            {
                fs = new Shader(fsFilename, GetIncludedShaders());
                m_Shaders.Add(fsFilename, fs);
            }

            return new ShaderProgram(vs, fs, attributes, secondaryAttributes, uniforms);
        }

        protected ShaderProgram InitProgram(string vsFilename, string fsFilename, ShaderAttribute[] attributes, string[] uniforms)
        {
            return InitProgram(vsFilename, fsFilename, attributes, null, uniforms);
        }

        protected void Bind(ShaderProgram shader, VertexBuffer buffer)
        {
            Bind(shader, buffer, null);
            /*
            int[] boundBuffer = {0};
            GL.GetInteger(GetPName.ArrayBufferBinding, boundBuffer);
            int[] boundShader = {0};
            GL.GetInteger(GetPName.ArrayBufferBinding, boundShader);

            if(boundShader[0] == shader.Id && boundBuffer[0] == buffer.Id)
            {
                return;
            }

            if(boundShader[0] > 0)
            {
                int[] attributes = { 0 };
                GL.GetProgram(shader.Id, GetProgramParameterName.ActiveAttributes, attributes);
                int l = attributes[0];
                if(l > 0)
                {
                    for(int i = 1; i < l; i++)
                    {
                        GL.DisableVertexAttribArray(i);
                    }
                }
            }

            if(shader == null)
            {
                GL.UseProgram(0);
                return;
            }

            GL.UseProgram(shader.Id);

            GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Id);
            ShaderAttribute[] atts = shader.Attributes;
            foreach(ShaderAttribute a in atts)
            {
                GL.EnableVertexAttribArray(a.Position);
                GL.VertexAttribPointer(a.Position, a.Size, VertexAttribPointerType.Float, false, a.StrideInBytes, a.Offset);
            }*/
        }

        protected void Bind(ShaderProgram shader, VertexBuffer buffer, VertexBuffer secondaryBuffer)
        {
            int[] boundBuffer = {0};
            GL.GetInteger(GetPName.ArrayBufferBinding, boundBuffer);
            int[] boundShader = {0};
            GL.GetInteger(GetPName.ArrayBufferBinding, boundShader);

            if (boundShader[0] == shader.Id && boundBuffer[0] == buffer.Id)
            {
                return;
            }

            if (boundShader[0] > 0)
            {
                int[] attributes = { 0 };
                GL.GetProgram(shader.Id, GetProgramParameterName.ActiveAttributes, attributes);
                int l = attributes[0];
                if (l > 0)
                {
                    for (int i = 1; i < l; i++)
                    {
                        GL.DisableVertexAttribArray(i);
                    }
                }
            }

            if (shader == null)
            {
                GL.UseProgram(0);
                return;
            }

            GL.UseProgram(shader.Id);

            GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Id);
            ShaderAttribute[] atts = shader.Attributes;
            foreach (ShaderAttribute a in atts)
            {
                GL.EnableVertexAttribArray(a.Position);
                GL.VertexAttribPointer(a.Position, a.Size, VertexAttribPointerType.Float, false, a.StrideInBytes, a.Offset);
            }

            if (secondaryBuffer != null)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, secondaryBuffer.Id);
                atts = shader.SecondaryAttributes;
                foreach (ShaderAttribute a in atts)
                {
                    GL.EnableVertexAttribArray(a.Position);
                    GL.VertexAttribPointer(a.Position, a.Size, VertexAttribPointerType.Float, false, a.StrideInBytes, a.Offset);
                }
            }
        }

        public Renderer()
        {
            m_Shaders = new Dictionary<string, Shader>();
        }

    }
}