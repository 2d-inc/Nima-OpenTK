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

		public override ActorComponent MakeInstance(Actor resetActor)
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
	}
}