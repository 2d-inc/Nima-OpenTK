using OpenTK.Graphics;
using OpenTK.Graphics.ES20;

namespace Nima.OpenGL
{
	public class Buffer
        {
            protected int m_Id;
            protected Buffer()
            {
                m_Id = GL.GenBuffer();
            }

            ~Buffer()
            {
                GL.DeleteBuffer(m_Id);
            }
            public int Id
            {
                get
                {
                    return m_Id;
                }
            }

        }

        public class IndexBuffer : Buffer
        {
            private int m_Size;

            public void SetData(ushort[] data)
            {
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, m_Id);
                GL.BufferData<ushort>(BufferTarget.ElementArrayBuffer, sizeof(ushort) * data.Length, data, BufferUsage.StaticDraw);
                m_Size = data.Length;
            }

            public int Size
            {
                get
                {
                    return m_Size;
                }
            }
        }

        public class VertexBuffer : Buffer
        {
            public void SetData(float[] data)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, m_Id);
                GL.BufferData<float>(BufferTarget.ArrayBuffer, sizeof(float) * data.Length, data, BufferUsage.StaticDraw);
            }
        }
}