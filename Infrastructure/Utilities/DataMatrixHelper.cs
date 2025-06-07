using ZXing;
using ZXing.Common;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Infrastructure.Utilities
{

	public static class DataMatrixHelper
	{
		public static byte[] GenerateDataMatrix(string content)
		{
			var writer = new MultiFormatWriter();
			BitMatrix matrix = writer.encode(content, BarcodeFormat.DATA_MATRIX, 300, 300);

			using Bitmap bitmap = new Bitmap(matrix.Width, matrix.Height);
			for (int x = 0; x < matrix.Width; x++)
			{
				for (int y = 0; y < matrix.Height; y++)
				{
					bitmap.SetPixel(x, y, matrix[x, y] ? Color.Black : Color.White);
				}
			}

			using MemoryStream ms = new MemoryStream();
			bitmap.Save(ms, ImageFormat.Png);
			return ms.ToArray();
		}
	}
}

