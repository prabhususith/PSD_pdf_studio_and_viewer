using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        private FileSystemWatcher? _cameraRollWatcher;

        public PdfDocumentModel Document => _document;

        public CameraScannerWindow(PdfDocumentModel? document = null)
        {
            InitializeComponent();
            _document = document ?? new PdfDocumentModel("Scanned_Document.pdf");

            InitCameraRollWatcher();
            GenerateSampleDocument("Invoice");
        }

        protected override void OnClosed(EventArgs e)
        {
            _cameraRollWatcher?.Dispose();
            base.OnClosed(e);
        }

        private void InitCameraRollWatcher()
        {
            try
            {
                string cameraRoll = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Camera Roll"
                );

                if (!Directory.Exists(cameraRoll))
                {
                    Directory.CreateDirectory(cameraRoll);
                }

                _cameraRollWatcher = new FileSystemWatcher(cameraRoll)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                _cameraRollWatcher.Created += (s, args) =>
                {
                    // Delay slightly to ensure Windows Camera has completed writing file bytes
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                LoadImageFromFile(args.FullPath);
                                TxtCameraStatus.Text = $"Photo Captured: {Path.GetFileName(args.FullPath)}";
                                StatusDot.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                                Activate();
                                Focus();
                            }
                            catch { }
                        });
                    });
                };
            }
            catch { }
        }

        private async void BtnCaptureCamera_Click(object sender, RoutedEventArgs e)
        {
            TxtCameraStatus.Text = "Capturing from Camera...";
            StatusDot.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));

            // Attempt 1: Direct Windows WinRT MediaCapture
            bool directCaptured = await TryDirectMediaCaptureAsync();
            if (directCaptured)
            {
                TxtCameraStatus.Text = "Hardware WebCam Frame Captured!";
                StatusDot.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                return;
            }

            // Attempt 2: Launch Windows Camera Companion app with Camera Roll Watcher
            try
            {
                TxtCameraStatus.Text = "Windows Camera Active: Snap photo to import automatically";
                Process.Start(new ProcessStartInfo("microsoft.windows.camera:") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No integrated camera detected directly. You can use 'Import File' or 'Paste Clipboard' to scan any document photo.\n\nDetails: " + ex.Message,
                    "Camera Scanner Notice",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                TxtCameraStatus.Text = "Camera Standby (Import / Paste Ready)";
                StatusDot.Background = new SolidColorBrush(Color.FromRgb(113, 113, 122));
            }
        }

        private async Task<bool> TryDirectMediaCaptureAsync()
        {
            try
            {
                var capture = new Windows.Media.Capture.MediaCapture();
                await capture.InitializeAsync();

                using var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                await capture.CapturePhotoToStreamAsync(
                    Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(),
                    memStream);

                memStream.Seek(0);
                using var netStream = memStream.AsStreamForRead();

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = netStream;
                bi.EndInit();
                bi.Freeze();

                SetNewScannedBitmap(bi);
                return true;
            }
            catch
            {
                // Machine may have no direct webcam permission or desktop camera driver
                return false;
            }
        }

        private void BtnPasteClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                var img = Clipboard.GetImage();
                if (img != null)
                {
                    SetNewScannedBitmap(img);
                    TxtCameraStatus.Text = "Pasted Photo from Clipboard";
                    StatusDot.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    return;
                }
            }

            MessageBox.Show(
                "No image currently found in Windows clipboard.\n\nTip: You can use Win + Shift + S to take a screenshot and click Paste Clipboard here.",
                "Clipboard Empty",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Scanned Document Photo or Blueprint",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadImageFromFile(dlg.FileName);
            }
        }

        private void LoadImageFromFile(string path)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();

                SetNewScannedBitmap(bi);
                TxtCameraStatus.Text = $"Imported: {Path.GetFileName(path)}";
                StatusDot.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetNewScannedBitmap(BitmapSource bitmap)
        {
            _originalBitmap = bitmap;
            _currentBitmap = bitmap;
            ImgPreview.Source = _currentBitmap;

            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
            TxtResolution.Text = $"Resolution: {w}x{h} (Off-heap processed)";

            var detected = ImageProcessingService.DetectDocumentCorners(w, h);
            for (int i = 0; i < 4; i++) _corners[i] = detected[i];

            OverlayCanvas.Visibility = Visibility.Visible;
            UpdateHandlesAndPolygon();
        }

        private void CmbSampleDoc_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbSampleDoc.SelectedItem is ComboBoxItem item)
            {
                string text = item.Content.ToString() ?? "";
                if (text.Contains("Invoice")) GenerateSampleDocument("Invoice");
                else if (text.Contains("Contract")) GenerateSampleDocument("Contract");
                else if (text.Contains("Receipt")) GenerateSampleDocument("Receipt");
                else if (text.Contains("Blueprint")) GenerateSampleDocument("Blueprint");
            }
        }

        private void GenerateSampleDocument(string type)
        {
            int w = 700, h = 920;
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            byte[] pixels = new byte[w * h * 4];

            byte paperB = 245, paperG = 248, paperR = 250;
            if (type == "Blueprint") { paperB = 180; paperG = 120; paperR = 30; }
            else if (type == "Receipt") { paperB = 230; paperG = 240; paperR = 245; }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    // Background dark wood desk
                    byte b = 30, g = 25, r = 20;

                    // Document skewed quad region
                    if (x > 60 + (y / 20) && x < w - 60 - (y / 25) && y > 70 && y < h - 70)
                    {
                        b = paperB; g = paperG; r = paperR;
                        // Simulated printed lines
                        if (y % 28 < 3 && x > 100 && x < w - 100 && y > 180 && y < h - 140)
                        {
                            b = (byte)(paperB > 100 ? 50 : 220);
                            g = (byte)(paperG > 100 ? 50 : 220);
                            r = (byte)(paperR > 100 ? 50 : 220);
                        }
                    }

                    pixels[i] = b;
                    pixels[i + 1] = g;
                    pixels[i + 2] = r;
                    pixels[i + 3] = 255;
                }
            }

            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            wb.Freeze();

            _originalBitmap = wb;
            _currentBitmap = wb;
            ImgPreview.Source = _currentBitmap;

            // Skewed boundary corners
            _corners[0] = new Point(65, 75);
            _corners[1] = new Point(w - 65, 75);
            _corners[2] = new Point(w - 100, h - 75);
            _corners[3] = new Point(100, h - 75);

            OverlayCanvas.Visibility = Visibility.Visible;
            UpdateHandlesAndPolygon();
        }

        private void UpdateHandlesAndPolygon()
        {
            QuadPolygon.Points.Clear();
            for (int i = 0; i < 4; i++) QuadPolygon.Points.Add(_corners[i]);

            Canvas.SetLeft(HandleTL, _corners[0].X - 11);
            Canvas.SetTop(HandleTL, _corners[0].Y - 11);

            Canvas.SetLeft(HandleTR, _corners[1].X - 11);
            Canvas.SetTop(HandleTR, _corners[1].Y - 11);

            Canvas.SetLeft(HandleBR, _corners[2].X - 11);
            Canvas.SetTop(HandleBR, _corners[2].Y - 11);

            Canvas.SetLeft(HandleBL, _corners[3].X - 11);
            Canvas.SetTop(HandleBL, _corners[3].Y - 11);
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
            TxtCameraStatus.Text = "Perspective Warped to Orthogonal Vector Dimensions (612x792 pt)";
            StatusDot.Background = new SolidColorBrush(Color.FromRgb(168, 85, 247));
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

            _document.Pages.Add(new PdfPageModel
            {
                PageNumber = _document.Pages.Count + 1,
                ContentText = "Scanned Page (Captured with Camera & Perspective Homography Warp)",
                RenderedImage = _currentBitmap
            });
            _document.IsModified = true;

            MessageBox.Show(
                "Document page successfully captured, warped, and inserted into active document workspace!",
                "Page Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

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
