using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace OmniPdf.App.Core
{
    public static class RealPdfRenderer
    {
        public static async Task<(int PageCount, double Width, double Height)> GetPdfInfoAsync(string pdfPath)
        {
            if (!File.Exists(pdfPath)) return (0, 612, 792);
            try
            {
                using var fs = File.OpenRead(pdfPath);
                using var ras = fs.AsRandomAccessStream();
                var pdfDoc = await PdfDocument.LoadFromStreamAsync(ras);
                int count = (int)pdfDoc.PageCount;
                if (count == 0) return (0, 612, 792);
                using var p = pdfDoc.GetPage(0);
                return (count, p.Size.Width, p.Size.Height);
            }
            catch
            {
                return (0, 612, 792);
            }
        }

        public static async Task<BitmapImage?> RenderPdfPageAsync(string pdfPath, int pageIndex, double targetWidth = 1400)
        {
            if (!File.Exists(pdfPath)) return null;

            try
            {
                using var fs = File.OpenRead(pdfPath);
                using var ras = fs.AsRandomAccessStream();
                var pdfDoc = await PdfDocument.LoadFromStreamAsync(ras);

                if (pageIndex < 0 || pageIndex >= pdfDoc.PageCount) return null;

                using var page = pdfDoc.GetPage((uint)pageIndex);
                using var stream = new InMemoryRandomAccessStream();

                var options = new PdfPageRenderOptions
                {
                    DestinationWidth = (uint)Math.Max(100, targetWidth)
                };

                await page.RenderToStreamAsync(stream, options);

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream.AsStreamForRead();
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }
    }
}
