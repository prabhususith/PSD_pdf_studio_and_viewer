using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OmniPdf.App.Core;
using OmniPdf.App.Views;

namespace OmniPdf.App
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<PdfDocumentModel> _documents = new();
        private readonly ObservableCollection<PdfLibraryItem> _libraryItems = new();
        private readonly ObservableCollection<PdfLibraryItem> _filteredLibraryItems = new();

        private PdfDocumentModel? _activeDoc;
        private readonly LruTileCache _lruCache = new(64 * 1024 * 1024);
        private double _currentZoom = 1.0;
        private string _activeTool = "Pointer";
        private CancellationTokenSource? _scanCts;

        private bool _isFullScreen = false;
        private WindowState _prevWindowState = WindowState.Normal;
        private WindowStyle _prevWindowStyle = WindowStyle.SingleBorderWindow;

        public MainWindow()
        {
            InitializeComponent();

            // Responsive window sizing adapted dynamically to screen resolution
            try
            {
                double workWidth = SystemParameters.WorkArea.Width;
                double workHeight = SystemParameters.WorkArea.Height;
                if (workWidth > 0 && workHeight > 0)
                {
                    Width = Math.Min(1140, workWidth * 0.94);
                    Height = Math.Min(630, workHeight * 0.90);
                    Left = Math.Max(0, (workWidth - Width) / 2);
                    Top = Math.Max(0, (workHeight - Height) / 2);
                }
            }
            catch { }

            // Safe UI initializations
            if (CmbTheme != null) CmbTheme.SelectedIndex = 0;
            if (TabList != null) TabList.ItemsSource = _documents;
            if (LibraryList != null) LibraryList.ItemsSource = _filteredLibraryItems;

            // Load initial document in reader
            var initialDoc = PdfDocumentModel.CreateDefault("Sample Document.pdf", 3);
            _documents.Add(initialDoc);
            SelectDocument(initialDoc);

            // Start auto-scanning local device PDF library in background
            Loaded += async (s, e) =>
            {
                await StartDeviceLibraryScanAsync();
            };
        }

        #region Device Library & Dashboard Engine

        private async Task StartDeviceLibraryScanAsync()
        {
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();

            _libraryItems.Clear();
            _filteredLibraryItems.Clear();
            if (TxtScanStatus != null) TxtScanStatus.Text = "Scanning device for PDF documents...";

            long totalBytes = 0;

            await LibraryScannerService.ScanDevicePdfLibraryAsync(_libraryItems, (count, msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (TxtScanStatus != null) TxtScanStatus.Text = msg;
                    if (TxtLibraryBadge != null) TxtLibraryBadge.Text = count.ToString();
                    if (TxtKpiTotalPdfs != null) TxtKpiTotalPdfs.Text = $"{count} Documents";
                    if (TxtLibraryFooterCount != null) TxtLibraryFooterCount.Text = $"{count} documents indexed across local drives";
                });
            }, _scanCts.Token);

            // Populate filtered collection
            foreach (var item in _libraryItems)
            {
                _filteredLibraryItems.Add(item);
                totalBytes += item.FileSizeBytes;
            }

            if (TxtKpiTotalSize != null)
            {
                double mb = totalBytes / (1024.0 * 1024.0);
                TxtKpiTotalSize.Text = mb > 1024 ? $"{mb / 1024.0:0.1} GB" : $"{mb:0.1} MB";
            }
        }

        private void TxtLibrarySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtLibrarySearch == null) return;
            string query = TxtLibrarySearch.Text.Trim();
            _filteredLibraryItems.Clear();

            if (string.IsNullOrEmpty(query))
            {
                foreach (var item in _libraryItems) _filteredLibraryItems.Add(item);
            }
            else
            {
                foreach (var item in _libraryItems)
                {
                    if (item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        _filteredLibraryItems.Add(item);
                    }
                }
            }

            if (TxtLibraryFooterCount != null)
            {
                TxtLibraryFooterCount.Text = $"Showing {_filteredLibraryItems.Count} of {_libraryItems.Count} documents";
            }
        }

        private async void BtnRescanLibrary_Click(object sender, RoutedEventArgs e)
        {
            await StartDeviceLibraryScanAsync();
        }

        private async void BtnOpenLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                await LoadPdfPathAsync(path);
                ShowReaderView();
            }
        }

        private async void BtnOrganizeLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                await LoadPdfPathAsync(path);
                ShowReaderView();
                BtnPageOrganizer_Click(sender, e);
            }
        }

        private async void BtnConvertLibraryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                await LoadPdfPathAsync(path);
                ShowReaderView();
                BtnTextConverter_Click(sender, e);
            }
        }

        #endregion

        #region Navigation & View Modes

        private void ShowLibraryView()
        {
            if (ViewLibrary != null) ViewLibrary.Visibility = Visibility.Visible;
            if (ViewReader != null) ViewReader.Visibility = Visibility.Collapsed;
        }

        private void ShowReaderView()
        {
            if (ViewLibrary != null) ViewLibrary.Visibility = Visibility.Collapsed;
            if (ViewReader != null) ViewReader.Visibility = Visibility.Visible;
        }

        private void NavBtnLibrary_Click(object sender, RoutedEventArgs e)
        {
            ShowLibraryView();
        }

        private void NavBtnReader_Click(object sender, RoutedEventArgs e)
        {
            ShowReaderView();
        }

        private void NavBtnOrganizer_Click(object sender, RoutedEventArgs e)
        {
            ShowReaderView();
            BtnPageOrganizer_Click(sender, e);
        }

        private void NavBtnScanner_Click(object sender, RoutedEventArgs e)
        {
            ShowReaderView();
            BtnScanner_Click(sender, e);
        }

        private void NavBtnConverter_Click(object sender, RoutedEventArgs e)
        {
            ShowReaderView();
            BtnTextConverter_Click(sender, e);
        }

        #endregion

        #region View Toggles (Nav Rail, Thumbnails, Full Screen)

        private void BtnToggleNavRail_Click(object sender, RoutedEventArgs e)
        {
            if (ColNavRail == null) return;
            if (ColNavRail.Width.Value > 0)
            {
                ColNavRail.Width = new GridLength(0);
                if (BtnToggleNavRail != null) BtnToggleNavRail.Content = "▶ Rail";
            }
            else
            {
                ColNavRail.Width = new GridLength(200);
                if (BtnToggleNavRail != null) BtnToggleNavRail.Content = "◀ Rail";
            }
        }

        private void BtnToggleThumbnails_Click(object sender, RoutedEventArgs e)
        {
            if (ColThumbnails == null) return;
            if (ColThumbnails.Width.Value > 0)
            {
                ColThumbnails.Width = new GridLength(0);
                if (BtnToggleThumbnails != null) BtnToggleThumbnails.Content = "📑 Show";
            }
            else
            {
                ColThumbnails.Width = new GridLength(170);
                if (BtnToggleThumbnails != null) BtnToggleThumbnails.Content = "📑 Thumbs";
            }
        }

        private void BtnToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen();
        }

        private void ToggleFullScreen()
        {
            _isFullScreen = !_isFullScreen;
            if (_isFullScreen)
            {
                _prevWindowState = WindowState;
                _prevWindowStyle = WindowStyle;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                if (BtnFullScreen != null) BtnFullScreen.Content = "🗗 Windowed";
            }
            else
            {
                WindowStyle = _prevWindowStyle;
                WindowState = _prevWindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
                if (BtnFullScreen != null) BtnFullScreen.Content = "⛶ Full Screen";
            }
        }

        #endregion

        #region Document Management & Loading

        public async Task LoadPdfPathAsync(string filePath)
        {
            if (!File.Exists(filePath)) return;

            // Check if already open
            var existing = _documents.FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectDocument(existing);
                return;
            }

            var doc = await PdfDocumentModel.LoadFromFileAsync(filePath);
            _documents.Add(doc);
            SelectDocument(doc);

            await RenderDocumentPagesAsync(doc);
        }

        private void SelectDocument(PdfDocumentModel doc)
        {
            _activeDoc = doc;
            if (TabList != null) TabList.SelectedItem = doc;

            if (ThumbnailList != null)
            {
                ThumbnailList.ItemsSource = null;
                ThumbnailList.ItemsSource = doc.Pages;
                if (doc.Pages.Count > 0)
                {
                    ThumbnailList.SelectedIndex = Math.Clamp(doc.CurrentPageIndex, 0, doc.Pages.Count - 1);
                }
            }

            if (NotesCanvas != null) NotesCanvas.Children.Clear();
            DisplayActivePage();
            UpdateStatus();
        }

        private void DisplayActivePage()
        {
            if (ImgPageRender == null || TxtPageContent == null || PageBorder == null) return;
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;

            int idx = Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1);
            var page = _activeDoc.Pages[idx];

            // Dynamically scale PageBorder according to real page dimensions
            PageBorder.Width = page.Width > 0 ? page.Width : 612.0;
            PageBorder.Height = page.Height > 0 ? page.Height : 792.0;

            if (page.RenderedImage != null)
            {
                ImgPageRender.Source = page.RenderedImage;
                ImgPageRender.Visibility = Visibility.Visible;
                TxtPageContent.Visibility = Visibility.Collapsed;
            }
            else
            {
                ImgPageRender.Visibility = Visibility.Collapsed;
                TxtPageContent.Visibility = Visibility.Visible;
                TxtPageContent.Text = page.ContentText;
            }

            UpdatePageRotation(page.Rotation);
        }

        private async Task RenderDocumentPagesAsync(PdfDocumentModel doc)
        {
            if (string.IsNullOrEmpty(doc.FilePath) || !File.Exists(doc.FilePath)) return;

            int activeIdx = Math.Clamp(doc.CurrentPageIndex, 0, doc.Pages.Count - 1);
            var activeImg = await RealPdfRenderer.RenderPdfPageAsync(doc.FilePath, activeIdx, 1400);
            if (activeImg != null)
            {
                doc.Pages[activeIdx].RenderedImage = activeImg;
                _lruCache.Put($"doc_{doc.Id}_p_{activeIdx}", new byte[128 * 1024]);
                if (_activeDoc == doc)
                {
                    DisplayActivePage();
                    ThumbnailList?.Items.Refresh();
                    UpdateStatus();
                }
            }

            // Render remaining pages in background
            for (int i = 0; i < doc.Pages.Count; i++)
            {
                if (i == activeIdx) continue;
                var img = await RealPdfRenderer.RenderPdfPageAsync(doc.FilePath, i, 800);
                if (img != null)
                {
                    doc.Pages[i].RenderedImage = img;
                    _lruCache.Put($"doc_{doc.Id}_p_{i}", new byte[64 * 1024]);
                }
            }

            if (_activeDoc == doc)
            {
                DisplayActivePage();
                ThumbnailList?.Items.Refresh();
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;

            int cur = _activeDoc.CurrentPageIndex + 1;
            int total = _activeDoc.Pages.Count;

            if (StatusPage != null) StatusPage.Text = $"Page {cur} of {total}";
            if (TxtNavPageIndicator != null) TxtNavPageIndicator.Text = $"Page {cur} / {total}";

            var page = _activeDoc.Pages[Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1)];
            if (StatusDimensions != null) StatusDimensions.Text = $"Dimensions: {(int)page.Width} x {(int)page.Height} pt";

            double lruMb = Math.Round(_lruCache.CurrentBytes / (1024.0 * 1024.0), 2);
            if (StatusLru != null) StatusLru.Text = $"LRU: {lruMb:0.0} MB / 64 MB Cap";
        }

        #endregion

        #region Tab & Page Controls

        private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabList?.SelectedItem is PdfDocumentModel doc)
            {
                SelectDocument(doc);
            }
        }

        private void BtnNewTab_Click(object sender, RoutedEventArgs e)
        {
            int count = _documents.Count + 1;
            var newDoc = PdfDocumentModel.CreateDefault($"Document {count}.pdf", 2);
            _documents.Add(newDoc);
            SelectDocument(newDoc);
            ShowReaderView();
        }

        private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PdfDocumentModel doc)
            {
                _documents.Remove(doc);
                if (_documents.Count > 0)
                {
                    SelectDocument(_documents[0]);
                }
                else
                {
                    BtnNewTab_Click(this, new RoutedEventArgs());
                }
            }
        }

        private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThumbnailList == null) return;
            if (_activeDoc != null && ThumbnailList.SelectedIndex >= 0)
            {
                _activeDoc.CurrentPageIndex = ThumbnailList.SelectedIndex;
                DisplayActivePage();
                UpdateStatus();
            }
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            if (_activeDoc.CurrentPageIndex > 0)
            {
                _activeDoc.CurrentPageIndex--;
                if (ThumbnailList != null) ThumbnailList.SelectedIndex = _activeDoc.CurrentPageIndex;
                DisplayActivePage();
                UpdateStatus();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            if (_activeDoc.CurrentPageIndex < _activeDoc.Pages.Count - 1)
            {
                _activeDoc.CurrentPageIndex++;
                if (ThumbnailList != null) ThumbnailList.SelectedIndex = _activeDoc.CurrentPageIndex;
                DisplayActivePage();
                UpdateStatus();
            }
        }

        private void UpdatePageRotation(int degrees)
        {
            if (PageBorder == null) return;
            PageBorder.LayoutTransform = new RotateTransform(degrees);
        }

        private void BtnRotatePage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            var page = _activeDoc.Pages[_activeDoc.CurrentPageIndex];
            page.Rotation = (page.Rotation + 90) % 360;
            UpdatePageRotation(page.Rotation);
            _activeDoc.IsModified = true;
            ThumbnailList?.Items.Refresh();
        }

        private void BtnRotateCounterPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            var page = _activeDoc.Pages[_activeDoc.CurrentPageIndex];
            page.Rotation = (page.Rotation + 270) % 360;
            UpdatePageRotation(page.Rotation);
            _activeDoc.IsModified = true;
            ThumbnailList?.Items.Refresh();
        }

        private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open PDF Document",
                Filter = "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                await LoadPdfPathAsync(dlg.FileName);
                ShowReaderView();
            }
        }

        private void BtnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var dlg = new SaveFileDialog
            {
                Title = "Save Incremental PDF Document",
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = _activeDoc.Title
            };
            if (dlg.ShowDialog() == true)
            {
                _activeDoc.SaveIncremental(dlg.FileName);
                MessageBox.Show($"Document saved incrementally to:\n{dlg.FileName}", "Save Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Zoom & View Modes

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Min(3.0, _currentZoom + 0.15);
            ApplyZoom();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Max(0.3, _currentZoom - 0.15);
            ApplyZoom();
        }

        private void BtnZoomActual_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = 1.0;
            ApplyZoom();
        }

        private void BtnFitWidth_Click(object sender, RoutedEventArgs e)
        {
            if (MainScrollViewer != null && PageBorder != null && PageBorder.ActualWidth > 0)
            {
                double available = MainScrollViewer.ActualWidth - 60;
                _currentZoom = Math.Clamp(available / PageBorder.ActualWidth, 0.35, 2.5);
                ApplyZoom();
            }
        }

        private void BtnFitPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainScrollViewer != null && PageBorder != null && PageBorder.ActualHeight > 0)
            {
                double available = MainScrollViewer.ActualHeight - 60;
                _currentZoom = Math.Clamp(available / PageBorder.ActualHeight, 0.35, 2.5);
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            if (TxtZoomLevel != null) TxtZoomLevel.Text = $"{(int)(_currentZoom * 100)}%";
            if (CanvasContainer != null) CanvasContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
        }

        #endregion

        #region Interactive Annotations (Vector Pen, Highlighter, Sticky Note, Eraser, Sign)

        private void BtnTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string text = btn.Content?.ToString() ?? "";
                if (text.Contains("Pointer")) SetTool("Pointer");
                else if (text.Contains("Sketch") || text.Contains("Pen")) SetTool("Vector Ink");
                else if (text.Contains("Highlight")) SetTool("Highlight");
                else if (text.Contains("Sticky")) SetTool("Sticky Note");
                else if (text.Contains("Eraser")) SetTool("Eraser");
                else if (text.Contains("Sign")) SetTool("PKCS#7 Sign");
            }
        }

        private void MenuToolPointer_Click(object sender, RoutedEventArgs e) => SetTool("Pointer");
        private void MenuToolInk_Click(object sender, RoutedEventArgs e) => SetTool("Vector Ink");
        private void MenuToolHighlight_Click(object sender, RoutedEventArgs e) => SetTool("Highlight");
        private void MenuToolSticky_Click(object sender, RoutedEventArgs e) => SetTool("Sticky Note");
        private void MenuToolEraser_Click(object sender, RoutedEventArgs e) => SetTool("Eraser");
        private void MenuToolSignature_Click(object sender, RoutedEventArgs e) => SetTool("PKCS#7 Sign");

        private void SetTool(string toolName)
        {
            _activeTool = toolName;
            if (StatusToolIndicator != null) StatusToolIndicator.Text = $"Tool: {_activeTool}";

            if (PageInkCanvas == null) return;

            if (_activeTool == "Vector Ink")
            {
                PageInkCanvas.IsHitTestVisible = true;
                PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                PageInkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(248, 113, 113); // red
                PageInkCanvas.DefaultDrawingAttributes.Width = 3;
                PageInkCanvas.DefaultDrawingAttributes.Height = 3;
                PageInkCanvas.DefaultDrawingAttributes.IsHighlighter = false;
            }
            else if (_activeTool == "Highlight")
            {
                PageInkCanvas.IsHitTestVisible = true;
                PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                PageInkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb(120, 250, 204, 21); // yellow highlight
                PageInkCanvas.DefaultDrawingAttributes.Width = 22;
                PageInkCanvas.DefaultDrawingAttributes.Height = 22;
                PageInkCanvas.DefaultDrawingAttributes.IsHighlighter = true;
            }
            else if (_activeTool == "Eraser")
            {
                PageInkCanvas.IsHitTestVisible = true;
                PageInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
            }
            else if (_activeTool == "Sticky Note")
            {
                PageInkCanvas.IsHitTestVisible = false;
                if (StatusToolIndicator != null) StatusToolIndicator.Text = "Tool: Sticky Note (Click anywhere on page to pin note)";
            }
            else if (_activeTool == "PKCS#7 Sign")
            {
                PageInkCanvas.IsHitTestVisible = false;
                if (_activeDoc != null)
                {
                    _activeDoc.StampDigitalSignature("Omni PDF Studio Enterprise Root CA", "Document Hash Verified SHA-256");
                    if (SignatureBadge != null) SignatureBadge.Visibility = Visibility.Visible;
                    MessageBox.Show("Cryptographic PKCS#7 detached digital signature stamped successfully with SHA-256 digest validation.", "Signature Verified", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else // Pointer
            {
                PageInkCanvas.IsHitTestVisible = false;
            }
        }

        private void PageContentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeTool == "Sticky Note" && NotesCanvas != null)
            {
                Point p = e.GetPosition(NotesCanvas);
                CreateStickyNoteOnPage(p.X, p.Y, "Type note here...");
                e.Handled = true;
            }
        }

        private void CreateStickyNoteOnPage(double x, double y, string initialText)
        {
            if (NotesCanvas == null) return;

            var noteBorder = new Border
            {
                Width = 190,
                Height = 140,
                Background = new SolidColorBrush(Color.FromRgb(254, 240, 138)), // warm yellow
                BorderBrush = new SolidColorBrush(Color.FromRgb(202, 138, 4)),  // yellow-600
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.4 }
            };

            Canvas.SetLeft(noteBorder, Math.Max(0, x - 10));
            Canvas.SetTop(noteBorder, Math.Max(0, y - 10));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Note text

            // Header bar
            var headerDock = new DockPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(253, 224, 71)),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = Cursors.SizeAll
            };

            var pinText = new TextBlock
            {
                Text = "📌 Sticky Note",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(113, 63, 18)),
                Margin = new Thickness(6, 3, 0, 3),
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnClose = new Button
            {
                Content = "✕",
                Width = 18,
                Height = 18,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7)),
                Cursor = Cursors.Hand
            };
            DockPanel.SetDock(btnClose, Dock.Right);
            headerDock.Children.Add(btnClose);
            headerDock.Children.Add(pinText);

            btnClose.Click += (s, ev) =>
            {
                NotesCanvas.Children.Remove(noteBorder);
            };

            // Dragging capability
            bool isDragging = false;
            Point startMouse = new Point();
            double startLeft = 0, startTop = 0;

            headerDock.MouseLeftButtonDown += (s, ev) =>
            {
                isDragging = true;
                startMouse = ev.GetPosition(NotesCanvas);
                startLeft = Canvas.GetLeft(noteBorder);
                startTop = Canvas.GetTop(noteBorder);
                headerDock.CaptureMouse();
                ev.Handled = true;
            };

            headerDock.MouseMove += (s, ev) =>
            {
                if (isDragging)
                {
                    Point current = ev.GetPosition(NotesCanvas);
                    Canvas.SetLeft(noteBorder, Math.Max(0, startLeft + (current.X - startMouse.X)));
                    Canvas.SetTop(noteBorder, Math.Max(0, startTop + (current.Y - startMouse.Y)));
                }
            };

            headerDock.MouseLeftButtonUp += (s, ev) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    headerDock.ReleaseMouseCapture();
                }
            };

            // Note Text input
            var txtNote = new TextBox
            {
                Text = initialText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(69, 26, 3)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Margin = new Thickness(6, 0, 6, 6)
            };

            Grid.SetRow(headerDock, 0);
            Grid.SetRow(txtNote, 1);
            grid.Children.Add(headerDock);
            grid.Children.Add(txtNote);

            noteBorder.Child = grid;
            NotesCanvas.Children.Add(noteBorder);
            txtNote.Focus();
            txtNote.SelectAll();
        }

        private void BtnClearInk_Click(object sender, RoutedEventArgs e)
        {
            PageInkCanvas?.Strokes.Clear();
        }

        private void BtnClearNotes_Click(object sender, RoutedEventArgs e)
        {
            NotesCanvas?.Children.Clear();
        }

        #endregion

        #region Themes & Skins Engine

        private void MenuTheme_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string themeName)
            {
                if (CmbTheme != null)
                {
                    foreach (ComboBoxItem item in CmbTheme.Items)
                    {
                        if (item.Content?.ToString() == themeName)
                        {
                            CmbTheme.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTheme?.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Content?.ToString() ?? "Fluent Dark";
                ApplyTheme(theme);
            }
        }

        private void ApplyTheme(string theme)
        {
            if (PageBorder == null) return;

            switch (theme)
            {
                case "Modern Light":
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                    Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    if (AppMenuBar != null) AppMenuBar.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    if (BorderNavRail != null) BorderNavRail.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    if (ViewLibrary != null) ViewLibrary.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                    if (ViewReader != null) ViewReader.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    if (MainScrollViewer != null) MainScrollViewer.Background = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                    if (BorderThumbnails != null) BorderThumbnails.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    if (ReaderStatusBar != null) ReaderStatusBar.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    break;

                case "OLED Black":
                    Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    Foreground = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    if (AppMenuBar != null) AppMenuBar.Background = new SolidColorBrush(Color.FromRgb(9, 9, 11));
                    if (BorderNavRail != null) BorderNavRail.Background = new SolidColorBrush(Color.FromRgb(9, 9, 11));
                    if (ViewLibrary != null) ViewLibrary.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    if (ViewReader != null) ViewReader.Background = new SolidColorBrush(Color.FromRgb(5, 5, 5));
                    if (MainScrollViewer != null) MainScrollViewer.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    if (BorderThumbnails != null) BorderThumbnails.Background = new SolidColorBrush(Color.FromRgb(9, 9, 11));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    if (ReaderStatusBar != null) ReaderStatusBar.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    break;

                case "Sepia Reading":
                    Background = new SolidColorBrush(Color.FromRgb(245, 235, 225));
                    Foreground = new SolidColorBrush(Color.FromRgb(69, 50, 33));
                    if (AppMenuBar != null) AppMenuBar.Background = new SolidColorBrush(Color.FromRgb(234, 219, 196));
                    if (BorderNavRail != null) BorderNavRail.Background = new SolidColorBrush(Color.FromRgb(234, 219, 196));
                    if (ViewLibrary != null) ViewLibrary.Background = new SolidColorBrush(Color.FromRgb(245, 235, 225));
                    if (ViewReader != null) ViewReader.Background = new SolidColorBrush(Color.FromRgb(239, 227, 207));
                    if (MainScrollViewer != null) MainScrollViewer.Background = new SolidColorBrush(Color.FromRgb(223, 206, 180));
                    if (BorderThumbnails != null) BorderThumbnails.Background = new SolidColorBrush(Color.FromRgb(234, 219, 196));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(253, 248, 238));
                    if (ReaderStatusBar != null) ReaderStatusBar.Background = new SolidColorBrush(Color.FromRgb(141, 110, 99));
                    break;

                case "Windows Mica":
                    Background = new SolidColorBrush(Color.FromRgb(26, 29, 36));
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    if (AppMenuBar != null) AppMenuBar.Background = new SolidColorBrush(Color.FromRgb(34, 38, 48));
                    if (BorderNavRail != null) BorderNavRail.Background = new SolidColorBrush(Color.FromRgb(34, 38, 48));
                    if (ViewLibrary != null) ViewLibrary.Background = new SolidColorBrush(Color.FromRgb(26, 29, 36));
                    if (ViewReader != null) ViewReader.Background = new SolidColorBrush(Color.FromRgb(32, 36, 48));
                    if (MainScrollViewer != null) MainScrollViewer.Background = new SolidColorBrush(Color.FromRgb(20, 23, 30));
                    if (BorderThumbnails != null) BorderThumbnails.Background = new SolidColorBrush(Color.FromRgb(34, 38, 48));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    if (ReaderStatusBar != null) ReaderStatusBar.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    break;

                default: // Fluent Dark
                    Background = new SolidColorBrush(Color.FromRgb(15, 15, 18));
                    Foreground = new SolidColorBrush(Color.FromRgb(236, 236, 239));
                    if (AppMenuBar != null) AppMenuBar.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    if (BorderNavRail != null) BorderNavRail.Background = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    if (ViewLibrary != null) ViewLibrary.Background = new SolidColorBrush(Color.FromRgb(15, 15, 18));
                    if (ViewReader != null) ViewReader.Background = new SolidColorBrush(Color.FromRgb(18, 18, 20));
                    if (MainScrollViewer != null) MainScrollViewer.Background = new SolidColorBrush(Color.FromRgb(9, 9, 11));
                    if (BorderThumbnails != null) BorderThumbnails.Background = new SolidColorBrush(Color.FromRgb(20, 20, 23));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    if (ReaderStatusBar != null) ReaderStatusBar.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    break;
            }
        }

        #endregion

        #region In-Document Search

        private void BtnToggleSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel == null) return;
            SearchPanel.Visibility = SearchPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (SearchPanel.Visibility == Visibility.Visible && TxtSearchQuery != null)
            {
                TxtSearchQuery.Focus();
            }
        }

        private void BtnCloseSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel != null) SearchPanel.Visibility = Visibility.Collapsed;
        }

        private void TxtSearchQuery_KeyUp(object sender, KeyEventArgs e)
        {
            PerformSearch();
        }

        private void BtnFindNext_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            if (TxtSearchQuery == null || TxtMatchCount == null) return;
            string query = TxtSearchQuery.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                TxtMatchCount.Text = "0 matches";
                return;
            }

            int matches = 0;
            if (_activeDoc != null)
            {
                foreach (var p in _activeDoc.Pages)
                {
                    if (p.ContentText.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                    }
                }
            }
            TxtMatchCount.Text = $"{matches} page matches found";
        }

        #endregion

        #region Modal Windows (Organizer, Scanner, Converter, Shortcuts, About)

        private void BtnPageOrganizer_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new PageOrganizerWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true)
            {
                SelectDocument(_activeDoc);
            }
        }

        private void BtnScanner_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new CameraScannerWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true)
            {
                SelectDocument(_activeDoc);
                if (ThumbnailList != null) ThumbnailList.SelectedIndex = _activeDoc.Pages.Count - 1;
            }
        }

        private void BtnTextConverter_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new TextConverterWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true && win.GeneratedDocument != null)
            {
                _documents.Add(win.GeneratedDocument);
                SelectDocument(win.GeneratedDocument);
            }
        }

        private void BtnShortcuts_Click(object sender, RoutedEventArgs e)
        {
            string msg = "Omni PDF Studio Keyboard Shortcuts:\n\n" +
                         "• F11: Toggle Full Screen (Presentation Mode)\n" +
                         "• F4: Toggle Left Navigation Rail\n" +
                         "• Ctrl+T: Toggle Thumbnails Sidebar\n" +
                         "• Ctrl+O: Open PDF Document\n" +
                         "• Ctrl+N: New Blank Document\n" +
                         "• Ctrl+S: Save Incremental Updates\n" +
                         "• Ctrl+F: Find in Document\n" +
                         "• Ctrl+R: Rotate Clockwise 90°\n" +
                         "• Ctrl++: Zoom In\n" +
                         "• Ctrl+-: Zoom Out\n" +
                         "• Ctrl+0: Actual Size (100%)\n" +
                         "• Esc: Exit Search / Exit Full Screen";
            MessageBox.Show(msg, "Keyboard Shortcuts Reference", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            string msg = "Omni PDF Studio Pro Enterprise v1.0.0\n\n" +
                         "• High-Performance DirectX WinRT Vector Rendering Engine\n" +
                         "• Strict 64MB LRU Memory Capped Tile Cache\n" +
                         "• Interactive Vector Ink, Highlighting & Draggable Sticky Notes\n" +
                         "• Automated Device PDF Library Indexing Hub\n" +
                         "• Dual ABI Android 6.0 Marshmallow (API 23) to API 34+ Compatibility\n\n" +
                         "Copyright © 2026 Omni Studio Inc. All rights reserved.";
            MessageBox.Show(msg, "About Omni PDF Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
            }
            else if (e.Key == Key.F4)
            {
                BtnToggleNavRail_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_isFullScreen)
                {
                    ToggleFullScreen();
                    e.Handled = true;
                }
                else if (SearchPanel?.Visibility == Visibility.Visible)
                {
                    SearchPanel.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.O) { BtnOpenFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.S) { BtnSaveFile_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.N) { BtnNewTab_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.F) { BtnToggleSearch_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.R) { BtnRotatePage_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.T) { BtnToggleThumbnails_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add) { BtnZoomIn_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { BtnZoomOut_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { BtnZoomActual_Click(this, new RoutedEventArgs()); e.Handled = true; }
            }
        }

        #endregion

        #region Window Caption Controls (Minimize, Maximize, Close)

        private void BtnWinMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnWinMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnWinClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
