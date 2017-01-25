using System;
using System.IO;
using System.Collections.Generic;
using Nima.OpenGL;
using OpenTK;

namespace Nima
{
	public class GameActorImage : ActorImage
	{
		VertexBuffer m_DeformVertexBuffer;
		VertexBuffer m_VertexBuffer;
		int m_IndexOffset;

		void Copy(GameActorImage node, Actor resetActor)
		{
			base.Copy(node, resetActor);
			m_IndexOffset = node.m_IndexOffset;
			m_VertexBuffer = node.m_VertexBuffer;
		}

		public override ActorNode MakeInstance(Actor resetActor)
		{
			GameActorImage instanceNode = new GameActorImage();
			instanceNode.Copy(this, resetActor);
			return instanceNode;
		}

		public void Render(GameActorInstance gameActorInstance, Renderer2D renderer)
		{
			if(TextureIndex < 0 || RenderOpacity <= 0.0f)
			{
				return;
			}

			switch(BlendMode)
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

			Texture texture = gameActorInstance.BaseGameActor.Textures[TextureIndex];

			IndexBuffer indexBuffer = gameActorInstance.BaseGameActor.IdxBuffer;

			if(m_DeformVertexBuffer != null && IsVertexDeformDirty)
			{
				m_DeformVertexBuffer.SetData(AnimationDeformedVertices);
				IsVertexDeformDirty = false;
			}

			if(ConnectedBoneCount > 0)
			{
				if(m_DeformVertexBuffer != null)
				{
					renderer.DrawTexturedAndDeformedSkin(WorldTransform, m_DeformVertexBuffer, m_VertexBuffer, indexBuffer, m_IndexOffset, TriangleCount*3, BoneInfluenceMatrices, RenderOpacity, Color.White, texture);
				}
				else
				{
					renderer.DrawTexturedSkin(WorldTransform, m_VertexBuffer, indexBuffer, m_IndexOffset, TriangleCount*3, BoneInfluenceMatrices, RenderOpacity, Color.White, texture);
				}
			}
			else
			{
				if(m_DeformVertexBuffer != null)
				{
					renderer.DrawTexturedAndDeformed(WorldTransform, m_DeformVertexBuffer, m_VertexBuffer, indexBuffer, m_IndexOffset, TriangleCount*3, RenderOpacity, Color.White, texture);
				}
				else
				{
					renderer.DrawTextured(WorldTransform, m_VertexBuffer, indexBuffer, m_IndexOffset, TriangleCount*3, RenderOpacity, Color.White, texture);
				}
			}
		}

		public int IndexOffset
		{
			get
			{
				return m_IndexOffset;
			}
			set
			{
				m_IndexOffset = value;
			}
		}

        public VertexBuffer DeformVertexBuffer
        {
            get
            {
                return m_DeformVertexBuffer;
            }
            set
            {
            	m_DeformVertexBuffer = value;
            }
        }

        public VertexBuffer VtxBuffer
        {
            get
            {
                return m_VertexBuffer;
            }
            set
            {
            	m_VertexBuffer = value;
            }
        }
                
	}

	public class GameActor : Actor
	{
		Texture[] m_Textures;
		IndexBuffer m_IndexBuffer;
		VertexBuffer m_SkinnedVertexBuffer;
		VertexBuffer m_VertexBuffer;
		string m_BaseFilename;

		public GameActor(string baseFilename)
		{
			m_BaseFilename = baseFilename;
		}

		protected override ActorImage makeImageNode()
		{
			return new GameActorImage();
		}

		public static GameActor Load(string filename)
		{
			using (FileStream stream = File.OpenRead(filename))
			{
				int idx = filename.LastIndexOf(".nima");

				GameActor actor = new GameActor(filename.Substring(0, idx));
				if(actor.LoadFrom(stream))
				{
					return actor;
				}
				return null;
			}
		}

		public void InitializeGraphics(Renderer2D renderer)
		{
			if(TexturesUsed > 0)
			{
				m_Textures = new Texture[TexturesUsed];

				for(int i = 0; i < TexturesUsed; i++)
				{
					string atlasFilename;
					if(TexturesUsed == 1)
					{
						atlasFilename = m_BaseFilename + ".png";
					}
					else
					{
						atlasFilename = m_BaseFilename + i + ".png";
					}
					m_Textures[i] = new Texture(atlasFilename, true);
				}
			}

			List<float> vertexData = new List<float>();
			List<float> skinnedVertexData = new List<float>();
			List<ushort> indexData = new List<ushort>();

			foreach(ActorImage actorImage in ImageNodes)
			{
				GameActorImage gameActorImage = actorImage as GameActorImage;
				if(gameActorImage != null)
				{
					// When the image has vertex deform we actually get two vertex buffers per image.
					// One that is static with all our base vertex data in there and one with the override positions.
					// TODO: optimize this to remove the positions from the base one.
					if(gameActorImage.DoesAnimationVertexDeform)
					{
						gameActorImage.VtxBuffer = new VertexBuffer();
						gameActorImage.VtxBuffer.SetData(gameActorImage.Vertices);

						gameActorImage.IndexOffset = indexData.Count;
						ushort[] tris = gameActorImage.Triangles;
						int indexCount = gameActorImage.TriangleCount * 3;
						for(int j = 0; j < indexCount; j++)
						{
							indexData.Add(tris[j]);
						}
					}
					else
					{
						// N.B. Even vertex deformed buffers get full stride. This wastes a little bit of data as each vertex deformed
						// mesh will also have their original positions stored on the GPU, but this saves quite a bit of extra branching.

						List<float> currentVertexData = gameActorImage.ConnectedBoneCount > 0 ? skinnedVertexData : vertexData;
						
						// Calculate the offset in our contiguous vertex buffer.
						ushort firstVertexIndex = (ushort)(currentVertexData.Count/gameActorImage.VertexStride);
						float[] vertices = gameActorImage.Vertices;
						int size = gameActorImage.VertexCount * gameActorImage.VertexStride;
						for(int j = 0; j < size; j++)
						{
							currentVertexData.Add(vertices[j]);
						}

						// N.B. There's an implication here that each mesh cannot have more than 65,535 vertices.
						gameActorImage.IndexOffset = indexData.Count;
						ushort[] tris = gameActorImage.Triangles;
						int indexCount = gameActorImage.TriangleCount * 3;
						for(int j = 0; j < indexCount; j++)
						{
							indexData.Add((ushort)(tris[j]+firstVertexIndex));
						}
					}
				}
			}
			// The buffers allocated here are all static as they do not change at runtime.
			if(vertexData.Count > 0)
			{
				m_VertexBuffer = new VertexBuffer();
				m_VertexBuffer.SetData(vertexData.ToArray());
			}
			if(skinnedVertexData.Count > 0)
			{
				m_SkinnedVertexBuffer = new VertexBuffer();
				m_SkinnedVertexBuffer.SetData(skinnedVertexData.ToArray());
			}
			if(indexData.Count > 0)
			{
				m_IndexBuffer = new IndexBuffer();
				m_IndexBuffer.SetData(indexData.ToArray());
			}

			// Update the vertex buffers being referenced.
			foreach(ActorImage actorImage in ImageNodes)
			{
				GameActorImage gameActorImage = actorImage as GameActorImage;
				if(gameActorImage != null)
				{
					if(!gameActorImage.DoesAnimationVertexDeform)
					{
						if(gameActorImage.ConnectedBoneCount > 0)
						{
							gameActorImage.VtxBuffer = m_SkinnedVertexBuffer;
						}
						else
						{
							gameActorImage.VtxBuffer = m_VertexBuffer;	
						}
					}
				}
			}
		}

		public GameActorInstance makeInstance()
		{
			GameActorInstance instance = new GameActorInstance(this);
			instance.Copy(this);
			return instance;
		}

		public Texture[] Textures
		{
			get
			{
				return m_Textures;
			}
		}

        public IndexBuffer IdxBuffer
        {
            get
            {
                return m_IndexBuffer;
            }
            set
            {
            	m_IndexBuffer = value;
            }
        }
	}

	public class GameActorInstance : Actor
	{
		GameActor m_GameActor;

		public GameActorInstance(GameActor gameActor)
		{
			m_GameActor = gameActor;
		}

		public void InitializeGraphics(Renderer2D renderer)
		{
			// When we initialize a character instance we go and generate the per instance graphical data necessary.
			// In this case, each image that vertex deforms via animation will get its own buffer...
			// We could potentially make this one contiguous one too, but the assumption would be that the entire buffer
			// would be changing each frame.
			foreach(ActorImage actorImage in ImageNodes)
			{
				GameActorImage gameActorImage = actorImage as GameActorImage;
				if(gameActorImage != null && actorImage.DoesAnimationVertexDeform)
				{
					gameActorImage.DeformVertexBuffer = new VertexBuffer();
					gameActorImage.DeformVertexBuffer.SetData(gameActorImage.AnimationDeformedVertices);
				}
			}
		}

		protected override void UpdateVertexDeform(ActorImage image)
		{
			GameActorImage actorImage = image as GameActorImage;
			actorImage.DeformVertexBuffer.SetData(actorImage.AnimationDeformedVertices);
		}

		public override void Advance(float elapsedSeconds)
		{
			base.Advance(elapsedSeconds);
		}

		public void Render(Renderer2D renderer)
		{
			foreach(ActorImage actorImage in ImageNodes)
			{
				GameActorImage gameActorImage = actorImage as GameActorImage;
				if(gameActorImage != null)
				{
					gameActorImage.Render(this, renderer);
				}
			}
		}

		public GameActor BaseGameActor
		{
			get
			{
				return m_GameActor;
			}
		}
	}/*

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
	}*/
}