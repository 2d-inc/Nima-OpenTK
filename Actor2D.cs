using System;
using System.IO;
using System.Collections.Generic;
using Nima.OpenGL;
using OpenTK;

namespace Nima
{
	public class ActorImageRenderData
	{
		ActorImage m_ActorImage;
		VertexBuffer m_VertexBuffer;
		IndexBuffer m_IndexBuffer;
		Texture m_Texture;

		public ActorImageRenderData(ActorImageRenderData baseRenderData)
		{
			// A shared render data where the texture and index buffer are shared but the vertex buffer is unique.
			// Occurs with meshes that deform their vertex buffer in animations.
			m_ActorImage = baseRenderData.m_ActorImage;
			m_VertexBuffer = new VertexBuffer();
			m_VertexBuffer.SetData(m_ActorImage.Vertices);
			m_IndexBuffer = baseRenderData.m_IndexBuffer;
			m_Texture = baseRenderData.m_Texture;
		}

		public ActorImageRenderData(Texture texture, ActorImage imageNode)
		{
			m_ActorImage = imageNode;
			m_IndexBuffer = new IndexBuffer();
			m_IndexBuffer.SetData(imageNode.Triangles);

			// Do we have a static vertex buffer?
			if(!imageNode.DoesAnimationVertexDeform)
			{
				m_VertexBuffer = new VertexBuffer();
				m_VertexBuffer.SetData(imageNode.Vertices);
			}

			m_Texture = texture;
		}

		public VertexBuffer VtxBuffer
		{
			get
			{
				return m_VertexBuffer;
			}
		}

		public IndexBuffer IdxBuffer
		{
			get
			{
				return m_IndexBuffer;
			}
		}

		public Texture Tex
		{
			get
			{
				return m_Texture;
			}
		}
	}

	public class ActorImage2D
	{
		ActorImage m_ImageNode;
		ActorImageRenderData m_RenderData;
		public ActorImage2D(ActorImageRenderData renderData, ActorImage imageNode)
		{
			m_ImageNode = imageNode;
			m_RenderData = renderData;
		}

		public ActorImage Node
		{
			get
			{
				return m_ImageNode;
			}
		}

		public void Advance(float seconds)
		{
			if(m_ImageNode.DoesAnimationVertexDeform && m_ImageNode.IsVertexDeformDirty)
			{
				m_RenderData.VtxBuffer.SetData(m_ImageNode.AnimationDeformedVertices);
				m_ImageNode.IsVertexDeformDirty = false;
			}
		}

		public void Draw(float[] viewTransform, Renderer2D renderer)
		{
			if(m_RenderData == null)
			{
				return;
			}
			switch(m_ImageNode.BlendMode)
			{
				case Nima.BlendModes.Normal:
					renderer.BlendMode = Renderer2D.BlendModes.Transparent;
					break;
				case Nima.BlendModes.Additive:
					renderer.BlendMode = Renderer2D.BlendModes.Additive;
					break;
				case Nima.BlendModes.Multiply:
					renderer.BlendMode = Renderer2D.BlendModes.Multiply;
					break;
				case Nima.BlendModes.Screen:
					renderer.BlendMode = Renderer2D.BlendModes.Screen;
					break;
			}

			if(m_ImageNode.IsSkinned)
			{
				renderer.DrawTexturedSkin(viewTransform, m_ImageNode.WorldTransform, m_RenderData.VtxBuffer, m_RenderData.IdxBuffer, m_ImageNode.BoneInfluenceMatrices, m_ImageNode.RenderOpacity, Color.White, m_RenderData.Tex);
			}
			else
			{
				renderer.DrawTextured(viewTransform, m_ImageNode.WorldTransform, m_RenderData.VtxBuffer, m_RenderData.IdxBuffer, m_ImageNode.RenderOpacity, Color.White, m_RenderData.Tex);
			}
		}
	}

	public class Actor2D
	{
		Actor m_Actor;
		ActorImageRenderData[] m_RenderData;
		private Actor2D(string baseFileName, Actor actor)
		{
			m_Actor = actor;
			Texture[] textures = new Texture[actor.TexturesUsed];

			for(int i = 0; i < actor.TexturesUsed; i++)
			{
				string atlasFilename;
				if(actor.TexturesUsed == 1)
				{
					atlasFilename = baseFileName + ".png";
				}
				else
				{
					atlasFilename = baseFileName + i + ".png";
				}
				textures[i] = new Texture(atlasFilename, true);
			}
			m_RenderData = new ActorImageRenderData[actor.ImageNodeCount];
			int idx = 0;
			foreach(ActorImage img in actor.ImageNodes)
			{
				if(img.TriangleCount > 0)
				{
					m_RenderData[idx] = new ActorImageRenderData(textures[img.TextureIndex], img);
				}
				idx++;
			}
		}

		public static Actor2D Load(string filename)
		{
			using (FileStream stream = File.OpenRead(filename))
			{
				int idx = filename.LastIndexOf(".nima");
				string baseFileName = filename.Substring(0, idx);
				return new Actor2D(baseFileName, Actor.LoadFrom(stream));
			}
		}

		public Actor BaseData
		{
			get
			{
				return m_Actor;
			}
		}

		public ActorImageRenderData[] RenderData
		{
			get
			{
				return m_RenderData;
			}
		}
	}

	public class ActorInstance2D : ActorInstance
	{
		private Actor2D m_Actor2D;

		ActorImage2D[] m_DrawNodes;
		public class DrawNodeComprarer : IComparer<ActorImage2D>
		{
			public int Compare(ActorImage2D x, ActorImage2D y)  
			{
				return x.Node.DrawOrder.CompareTo(y.Node.DrawOrder);
			}
		}
		static DrawNodeComprarer m_DrawNodeComparer = new DrawNodeComprarer();

		public ActorInstance2D(Actor2D actor2D) : base(actor2D.BaseData)
		{
			m_Actor2D = actor2D;
			m_DrawNodes = new ActorImage2D[ImageNodeCount];
			int idx = 0;

			// Each draw node uses the renderdata from the base actor (this allows sharing of vertex and index buffers when possible).
			foreach(ActorImage img in ImageNodes)
			{
				ActorImageRenderData renderData = null;
				if(img.TriangleCount > 0)
				{
					renderData = actor2D.RenderData[idx];
					if(img.DoesAnimationVertexDeform)
					{
						renderData = new ActorImageRenderData(renderData);
					}
				}
				m_DrawNodes[idx] = new ActorImage2D(renderData, img);
				idx++;
			}
		}

		public override void Advance(float seconds)
		{
			base.Advance(seconds);
			Array.Sort<ActorImage2D>(m_DrawNodes, m_DrawNodeComparer);
			foreach(ActorImage2D img in m_DrawNodes)
			{
				img.Advance(seconds);
			}
		}

		public void Draw(float[] viewTransform, Renderer2D renderer)
		{
			int nodeCount = m_DrawNodes.Length;
			for(int i = 0; i < nodeCount; i++)
			{
				ActorImage2D actorImage = m_DrawNodes[i];
				actorImage.Draw(viewTransform, renderer);
			}
		}
	}
}