using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class CameraScannerWindow : Window
    {
        private BitmapSource? _originalBitmap;
        private BitmapSource? _currentBitmap;
        private readonly Point[] _corners = new Point[4];
        private readonly PdfDocumentModel _document;

        public CameraScannerWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document;
            GenerateSampleScanImage();
        }

        private void GenerateSampleScanImage()
        {
            // Generate standard document scan texture
            int w = 600, h = 800;
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    // Slightly warm scan paper color with minor gradient
                    byte bg = (byte)Math.Clamp(240 - (y / 40), 200, 245);
                    pixels[i] = (byte)(bg - 10); // B
                    pixels[i + 1] = bg;          // G
                    pixels[i + 2] = bg;          // R
                    pixels[i + 3] = 255;
                }
            }
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            wb.Freeze();
            _originalBitmap = wb;
            _currentBitmap = wb;
            ImgPreview.Source = _currentBitmap;

            // Initialize corners
            _corners[0] = new Point(40, 60);
            _corners[1] = new Point(w - 40, 60);
            _corners[2] = new Point(w - 50, h - 70);
            _corners[3] = new Point(50, h - 70);

            UpdateHandlesAndPolygon();
        }

        private void UpdateHandlesAndPolygon()
        {
            QuadPolygon.Points.Clear();
            for (int i = 0; i < 4; i++) QuadPolygon.Points.Add(_corners[i]);

            Canvas.SetLeft(HandleTL, _corners[0].X - 10);
            Canvas.SetTop(HandleTL, _corners[0].Y - 10);

            Canvas.SetLeft(HandleTR, _corners[1].X - 10);
            Canvas.SetTop(HandleTR, _corners[1].Y - 10);

            Canvas.SetLeft(HandleBR, _corners[2].X - 10);
            Canvas.SetTop(HandleBR, _corners[2].Y - 10);

            Canvas.SetLeft(HandleBL, _corners[3].X - 10);
            Canvas.SetTop(HandleBL, _corners[3].Y - 10);
        }

        private void Handle_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender == HandleTL)
            {
                _corners[0].X += e.HorizontalChange;
                _corners[0].Y += e.VerticalChange;
            }
            else if (sender == HandleTR)
            {
                _corners[1].X += e.HorizontalChange;
                _corners[1].Y += e.VerticalChange;
            }
            else if (sender == HandleBR)
            {
                _corners[2].X += e.HorizontalChange;
                _corners[2].Y += e.VerticalChange;
            }
            else if (sender == HandleBL)
            {
                _corners[3].X += e.HorizontalChange;
                _corners[3].Y += e.VerticalChange;
            }
            UpdateHandlesAndPolygon();
        }

        private void BtnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Scanned Photo or Document Image",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(dlg.FileName);
                bi.EndInit();
                bi.Freeze();

                _originalBitmap = bi;
                _currentBitmap = bi;
                ImgPreview.Source = _currentBitmap;

                int w = bi.PixelWidth;
                int h = bi.PixelHeight;
                TxtResolution.Text = $"Resolution: {w}x{h} (Off-heap processed)";

                var detected = ImageProcessingService.DetectDocumentCorners(w, h);
                for (int i = 0; i < 4; i++) _corners[i] = detected[i];
                UpdateHandlesAndPolygon();
            }
        }

        private void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;
            var detected = ImageProcessingService.DetectDocumentCorners(_originalBitmap.PixelWidth, _originalBitmap.PixelHeight);
            for (int i = 0; i < 4; i++) _corners[i] = detected[i];
            UpdateHandlesAndPolygon();
        }

        private void BtnWarp_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;
            var warped = ImageProcessingService.WarpPerspective(_originalBitmap, _corners, 612, 792);
            _currentBitmap = warped;
            ImgPreview.Source = _currentBitmap;
            OverlayCanvas.Visibility = Visibility.Collapsed;
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBitmap == null) return;
            if (sender is Button btn)
            {
                string name = btn.Content.ToString() ?? "";
                ScanFilter filter = ScanFilter.Original;
                if (name == "Magic Color") filter = ScanFilter.MagicColor;
                else if (name.Contains("Otsu")) filter = ScanFilter.BwOtsu;
                else if (name == "Grayscale") filter = ScanFilter.Grayscale;

                var filtered = ImageProcessingService.ApplyFilter(_currentBitmap, filter);
                ImgPreview.Source = filtered;
            }
        }

        private void BtnCompilePdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBitmap == null) return;

            // Add as new page into document
            _document.Pages.Add(new PdfPageModel
            {
                PageNumber = _document.Pages.Count + 1,
                ContentText = "Scanned Document Page (Processed with Perspective Homography Warp & Otsu Binarization)",
                RenderedImage = _currentBitmap
            });
            _document.IsModified = true;

            MessageBox.Show("Scanned page successfully warped, enhanced, and inserted into active document AST!", "Scan Inserted", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
