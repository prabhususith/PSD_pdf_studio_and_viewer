using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OmniPdf.App.Core
{
    public static class RealPdfRenderer
    {
        public static async Task<BitmapImage?> RenderPdfPageAsync(string pdfPath, int pageIndex, double targetWidth = 1200)
        {
            if (!File.Exists(pdfPath)) return null;

            try
            {
                var storageFile = await StorageFile.GetFileFromPathAsync(pdfPath);
                var pdfDoc = await PdfDocument.LoadFromFileAsync(storageFile);

                if (pageIndex < 0 || pageIndex >= pdfDoc.PageCount) return null;

                using var page = pdfDoc.GetPage((uint)pageIndex);
                using var stream = new InMemoryRandomAccessStream();

                var options = new PdfPageRenderOptions
                {
                    DestinationWidth = (uint)targetWidth
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
