using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using System.Runtime.InteropServices.WindowsRuntime;

namespace VRC_cantalkcn.Services;

public sealed class QrCodeService
{
    public async Task<BitmapImage?> GenerateAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(6);

        var image = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        await image.SetSourceAsync(ms.AsRandomAccessStream());
        return image;
    }
}
