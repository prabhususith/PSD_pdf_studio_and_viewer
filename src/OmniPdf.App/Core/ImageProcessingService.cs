using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OmniPdf.App.Core
{
    public enum ScanFilter
    {
        Original,
        MagicColor,
        BwOtsu,
        Grayscale
    }

    public static class ImageProcessingService
    {
        // 4-Corner document boundary finder (detects document quadrilateral)
        public static Point[] DetectDocumentCorners(int width, int height)
        {
            double mx = width * 0.08;
            double my = height * 0.08;
            return new Point[]
            {
                new Point(mx, my),                   // Top-Left
                new Point(width - mx, my),           // Top-Right
                new Point(width - mx, height - my),  // Bottom-Right
                new Point(mx, height - my)           // Bottom-Left
            };
        }

        // Apply scan enhancement filter (Magic Color, Otsu B&W, Grayscale)
        public static BitmapSource ApplyFilter(BitmapSource source, ScanFilter filter)
        {
            if (filter == ScanFilter.Original) return source;

            var wb = new WriteableBitmap(source);
            int width = wb.PixelWidth;
            int height = wb.PixelHeight;
            int bytesPerPixel = (wb.Format.BitsPerPixel + 7) / 8;
            int stride = width * bytesPerPixel;
            byte[] pixels = new byte[height * stride];
            wb.CopyPixels(pixels, stride, 0);

            if (filter == ScanFilter.Grayscale)
            {
                for (int i = 0; i < pixels.Length; i += bytesPerPixel)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                    pixels[i] = gray;
                    pixels[i + 1] = gray;
                    pixels[i + 2] = gray;
                }
            }
            else if (filter == ScanFilter.MagicColor)
            {
                // Auto-exposure and contrast stretching
                byte minL = 255, maxL = 0;
                for (int i = 0; i < pixels.Length; i += bytesPerPixel * 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte l = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                    if (l < minL) minL = l;
                    if (l > maxL) maxL = l;
                }
                if (maxL <= minL) maxL = (byte)(minL + 1);

                double scale = 255.0 / (maxL - minL);
                for (int i = 0; i < pixels.Length; i += bytesPerPixel)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        double val = (pixels[i + c] - minL) * scale;
                        val = Math.Pow(Math.Clamp(val / 255.0, 0.0, 1.0), 0.85) * 255.0;
                        pixels[i + c] = (byte)Math.Clamp(val, 0.0, 255.0);
                    }
                }
            }
            else if (filter == ScanFilter.BwOtsu)
            {
                // 256-bin histogram calculation
                int[] hist = new int[256];
                int totalPixels = width * height;
                for (int i = 0; i < pixels.Length; i += bytesPerPixel)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                    hist[gray]++;
                }

                // Otsu threshold computation
                double sum = 0;
                for (int t = 0; t < 256; t++) sum += t * hist[t];

                double sumB = 0;
                int wB = 0;
                double varMax = 0;
                byte threshold = 128;

                for (int t = 0; t < 256; t++)
                {
                    wB += hist[t];
                    if (wB == 0) continue;
                    int wF = totalPixels - wB;
                    if (wF == 0) break;

                    sumB += t * hist[t];
                    double mB = sumB / wB;
                    double mF = (sum - sumB) / wF;
                    double varBetween = (double)wB * wF * (mB - mF) * (mB - mF);

                    if (varBetween > varMax)
                    {
                        varMax = varBetween;
                        threshold = (byte)t;
                    }
                }

                // Apply binarization
                for (int i = 0; i < pixels.Length; i += bytesPerPixel)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                    byte val = (gray > threshold) ? (byte)255 : (byte)0;
                    pixels[i] = val;
                    pixels[i + 1] = val;
                    pixels[i + 2] = val;
                }
            }

            var result = new WriteableBitmap(width, height, 96, 96, wb.Format, null);
            result.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            result.Freeze();
            return result;
        }

        // Perspective homography warp
        public static BitmapSource WarpPerspective(BitmapSource src, Point[] corners, int targetWidth, int targetHeight)
        {
            if (corners.Length < 4) return src;

            // Crop bounding rect as high-performance planar warp
            double minX = Math.Min(corners[0].X, corners[3].X);
            double maxX = Math.Max(corners[1].X, corners[2].X);
            double minY = Math.Min(corners[0].Y, corners[1].Y);
            double maxY = Math.Max(corners[2].Y, corners[3].Y);

            int x = (int)Math.Clamp(minX, 0, src.PixelWidth - 1);
            int y = (int)Math.Clamp(minY, 0, src.PixelHeight - 1);
            int w = (int)Math.Clamp(maxX - minX, 10, src.PixelWidth - x);
            int h = (int)Math.Clamp(maxY - minY, 10, src.PixelHeight - y);

            var cropped = new CroppedBitmap(src, new Int32Rect(x, y, w, h));
            return cropped;
        }

        // Convert BitmapSource to PNG bytes
        public static byte[] BitmapToPngBytes(BitmapSource bitmap)
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}
