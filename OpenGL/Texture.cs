using System;
using System.IO;
using OpenTK.Graphics.ES20;
using Hjg.Pngcs;

namespace Nima.OpenGL
{
	public class Texture
	{
		private int m_Id;
		public Texture(string filename, bool multiplyAlpha = false)
		{
			using (FileStream stream = File.OpenRead(filename))
			{
				PngReader reader = new PngReader(stream);
				byte[] data = new byte[reader.ImgInfo.Rows*reader.ImgInfo.Cols*4];
				int widx = 0;
				for (int row = 0; row < reader.ImgInfo.Rows; row++) 
				{
					ImageLine line = reader.ReadRowByte(row);
					int idx = 0;
					for(int col = 0; col < reader.ImgInfo.Cols; col++)
					{
						byte R = line.ScanlineB[idx++];
						byte G = line.ScanlineB[idx++];
						byte B = line.ScanlineB[idx++];
						byte A = line.ScanlineB[idx++];
						if(multiplyAlpha)
						{
							float alpha = A/255.0f;
							R = (byte)Math.Round(R * alpha);
							G = (byte)Math.Round(G * alpha);
							B = (byte)Math.Round(B * alpha);
						}
						data[widx++] = R;
						data[widx++] = G;
						data[widx++] = B;
						data[widx++] = A;
					}
					//Console.WriteLine("ELEMENTS PER ROW " + line.ElementsPerRow + " " + line.ScanlineB.Length); // should be 4 * width
				}
				// Console.WriteLine("DECODED IT " + reader.ImgInfo.Cols + " " + reader.ImgInfo.Rows);

				m_Id = GL.GenTexture();
				GL.BindTexture(TextureTarget.Texture2D, m_Id);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
				GL.TexImage2D<byte>(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgba, reader.ImgInfo.Cols, reader.ImgInfo.Rows, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
				GL.BindTexture(TextureTarget.Texture2D, 0);
			}
		}

		~Texture()
		{
			GL.DeleteTexture(m_Id);
		}

		public int Id
		{
			get
			{
				return m_Id;
			}
		}
	}
}