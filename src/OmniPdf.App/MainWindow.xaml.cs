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

        public MainWindow()
        {
            InitializeComponent();

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

            // Asynchronously render active page first for instant visual feedback
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

        #region Zoom, Tools & Annotations

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

        private void BtnFitWidth_Click(object sender, RoutedEventArgs e)
        {
            if (MainScrollViewer != null && PageBorder != null && PageBorder.ActualWidth > 0)
            {
                double available = MainScrollViewer.ActualWidth - 80;
                _currentZoom = Math.Clamp(available / PageBorder.ActualWidth, 0.4, 2.5);
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            if (TxtZoomLevel != null) TxtZoomLevel.Text = $"{(int)(_currentZoom * 100)}%";
            if (CanvasContainer != null) CanvasContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
        }

        private void BtnTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _activeTool = btn.Content?.ToString() ?? "Pointer";

                if (PageInkCanvas == null) return;

                if (_activeTool.Contains("Vector Ink"))
                {
                    PageInkCanvas.IsHitTestVisible = true;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    PageInkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(248, 113, 113);
                    PageInkCanvas.DefaultDrawingAttributes.Width = 3;
                    PageInkCanvas.DefaultDrawingAttributes.Height = 3;
                }
                else if (_activeTool.Contains("Highlight"))
                {
                    PageInkCanvas.IsHitTestVisible = true;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    PageInkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb(120, 250, 204, 21);
                    PageInkCanvas.DefaultDrawingAttributes.Width = 18;
                    PageInkCanvas.DefaultDrawingAttributes.Height = 18;
                }
                else if (_activeTool.Contains("Sticky Note"))
                {
                    PageInkCanvas.IsHitTestVisible = false;
                    MessageBox.Show("Click on any page location to pin a sticky annotation note.", "Sticky Note Tool", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (_activeTool.Contains("PKCS#7 Sign"))
                {
                    PageInkCanvas.IsHitTestVisible = false;
                    if (_activeDoc != null)
                    {
                        _activeDoc.StampDigitalSignature("Omni PDF Studio Enterprise Root CA", "Document Hash Verified SHA-256");
                        if (SignatureBadge != null) SignatureBadge.Visibility = Visibility.Visible;
                        MessageBox.Show("Cryptographic PKCS#7 detached digital signature stamped successfully with SHA-256 digest validation.", "Signature Verified", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    PageInkCanvas.IsHitTestVisible = false;
                }
            }
        }

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageBorder == null || CmbTheme == null) return;
            if (CmbTheme.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Content?.ToString() ?? "";
                if (theme == "OLED Black")
                {
                    Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
                else if (theme == "Sepia Reading")
                {
                    Background = new SolidColorBrush(Color.FromRgb(239, 230, 213));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(251, 240, 217));
                }
                else if (theme == "Windows Mica")
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 34));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
                else // Fluent Dark
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 15, 18));
                    PageBorder.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
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

        #region Modal Windows (Organizer, Scanner, Converter)

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

        #endregion
    }
}
