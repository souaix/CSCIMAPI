using QRCoder;
using QRCoder.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using System.IO;


namespace Infrastructure.Utilities
{
	public static class QRCodeHelper
	{
		public static byte[] GenerateQrBytes(string content)
		{
			using var qrGenerator = new QRCodeGenerator();
			using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
			using var qrCode = new PngByteQRCode(qrData);
			return qrCode.GetGraphic(20);
		}
	}
}
