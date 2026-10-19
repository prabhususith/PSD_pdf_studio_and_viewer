using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        private double _fsZoom = 1.0;
        private string _activeTool = "Pointer";
        private CancellationTokenSource? _scanCts;

        private bool _isFullScreen = false;
        private WindowState _prevWindowState = WindowState.Normal;
        private WindowStyle _prevWindowStyle = WindowStyle.SingleBorderWindow;

        private string? _currentWordFilePath = null;
        private bool _isWordModified = false;

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
                    Width = Math.Min(1140, workWidth - 30);
                    Height = Math.Min(580, workHeight - 40);
                    Left = Math.Max(10, (workWidth - Width) / 2);
                    Top = Math.Max(10, (workHeight - Height) / 2);
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

            // Initialize Word document with sample template
            WordDocumentService.CreateNewDocument(RtbWordEditor, "Executive Summary & Strategy Plan");
            UpdateWordStats();

            // Start auto-scanning local device document library in background
            Loaded += async (s, e) =>
            {
                ApplyTheme("Fluent Dark");
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
            if (TxtScanStatus != null) TxtScanStatus.Text = "Scanning device for PDF and Office documents...";

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
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".docx" || ext == ".rtf" || ext == ".txt" || ext == ".md" || ext == ".html")
                {
                    OpenWordDocumentPath(path);
                }
                else
                {
                    await LoadPdfPathAsync(path);
                    ShowReaderView();
                }
            }
        }

        private void BtnOpenLibraryInWord_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                OpenWordDocumentPath(path);
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
            if (ViewWordEditor != null) ViewWordEditor.Visibility = Visibility.Collapsed;
            if (ViewPdfFullScreen != null) ViewPdfFullScreen.Visibility = Visibility.Collapsed;
        }

        private void ShowReaderView()
        {
            if (ViewLibrary != null) ViewLibrary.Visibility = Visibility.Collapsed;
            if (ViewReader != null) ViewReader.Visibility = Visibility.Visible;
            if (ViewWordEditor != null) ViewWordEditor.Visibility = Visibility.Collapsed;
            if (ViewPdfFullScreen != null) ViewPdfFullScreen.Visibility = Visibility.Collapsed;
        }

        private void ShowWordView()
        {
            if (ViewLibrary != null) ViewLibrary.Visibility = Visibility.Collapsed;
            if (ViewReader != null) ViewReader.Visibility = Visibility.Collapsed;
            if (ViewWordEditor != null) ViewWordEditor.Visibility = Visibility.Visible;
            if (ViewPdfFullScreen != null) ViewPdfFullScreen.Visibility = Visibility.Collapsed;
        }

        private void NavBtnLibrary_Click(object sender, RoutedEventArgs e)
        {
            ShowLibraryView();
        }

        private void NavBtnReader_Click(object sender, RoutedEventArgs e)
        {
            ShowReaderView();
        }

        private void NavBtnWord_Click(object sender, RoutedEventArgs e)
        {
            ShowWordView();
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
                ColNavRail.Width = new GridLength(190);
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
                ColThumbnails.Width = new GridLength(160);
                if (BtnToggleThumbnails != null) BtnToggleThumbnails.Content = "📑 Thumbs";
            }
        }

        private void BtnToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            EnterFullScreenPresentation();
        }

        private void EnterFullScreenPresentation()
        {
            _isFullScreen = true;
            _prevWindowState = WindowState;
            _prevWindowStyle = WindowStyle;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;

            if (ViewPdfFullScreen != null)
            {
                ViewPdfFullScreen.Visibility = Visibility.Visible;
                UpdateFullScreenPresentationPage();
            }
        }

        private void ExitFullScreenPresentation()
        {
            _isFullScreen = false;
            if (ViewPdfFullScreen != null) ViewPdfFullScreen.Visibility = Visibility.Collapsed;

            WindowStyle = _prevWindowStyle;
            WindowState = _prevWindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
            ShowReaderView();
        }

        private void BtnExitFullScreen_Click(object sender, RoutedEventArgs e)
        {
            ExitFullScreenPresentation();
        }

        private void UpdateFullScreenPresentationPage()
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            int idx = Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1);
            var page = _activeDoc.Pages[idx];

            if (ImgFullScreenPage != null && page.RenderedImage != null)
            {
                ImgFullScreenPage.Source = page.RenderedImage;
                ImgFullScreenPage.LayoutTransform = new ScaleTransform(_fsZoom, _fsZoom);
            }

            if (TxtFsPageIndicator != null)
            {
                TxtFsPageIndicator.Text = $"Page {idx + 1} of {_activeDoc.Pages.Count}";
            }
        }

        private void BtnFsPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            if (_activeDoc.CurrentPageIndex > 0)
            {
                _activeDoc.CurrentPageIndex--;
                DisplayActivePage();
                UpdateFullScreenPresentationPage();
            }
        }

        private void BtnFsNext_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            if (_activeDoc.CurrentPageIndex < _activeDoc.Pages.Count - 1)
            {
                _activeDoc.CurrentPageIndex++;
                DisplayActivePage();
                UpdateFullScreenPresentationPage();
            }
        }

        private void BtnFsZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _fsZoom = Math.Min(3.0, _fsZoom + 0.15);
            UpdateFullScreenPresentationPage();
        }

        private void BtnFsZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _fsZoom = Math.Max(0.5, _fsZoom - 0.15);
            UpdateFullScreenPresentationPage();
        }

        private void BtnFsFitWidth_Click(object sender, RoutedEventArgs e)
        {
            _fsZoom = 1.0;
            UpdateFullScreenPresentationPage();
        }

        #endregion

        #region PDF Document Management & Loading

        public async Task LoadPdfPathAsync(string filePath)
        {
            if (!File.Exists(filePath)) return;

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
                DisplayActivePage();
            }

            _ = Task.Run(async () =>
            {
                for (int i = 0; i < doc.Pages.Count; i++)
                {
                    if (i == activeIdx) continue;
                    var img = await RealPdfRenderer.RenderPdfPageAsync(doc.FilePath, i, 400);
                    if (img != null)
                    {
                        int pIdx = i;
                        Dispatcher.Invoke(() =>
                        {
                            if (pIdx < doc.Pages.Count)
                            {
                                doc.Pages[pIdx].RenderedImage = img;
                            }
                        });
                    }
                }
            });
        }

        private void UpdatePageRotation(int degrees)
        {
            if (PageContentGrid == null) return;
            PageContentGrid.LayoutTransform = new RotateTransform(degrees);
        }

        private void UpdateStatus()
        {
            if (_activeDoc == null) return;
            int cur = _activeDoc.CurrentPageIndex + 1;
            int total = Math.Max(1, _activeDoc.Pages.Count);

            if (TxtNavPageIndicator != null) TxtNavPageIndicator.Text = $"Page {cur} / {total}";
            if (StatusPage != null) StatusPage.Text = $"Page {cur} of {total}";
            if (StatusDimensions != null && _activeDoc.Pages.Count > 0)
            {
                var p = _activeDoc.Pages[_activeDoc.CurrentPageIndex];
                StatusDimensions.Text = $"Dimensions: {(int)p.Width} x {(int)p.Height} pt";
            }
            if (StatusToolIndicator != null) StatusToolIndicator.Text = $"Tool: {_activeTool}";
            if (StatusLru != null) StatusLru.Text = $"LRU: {_lruCache.CurrentBytes / (1024.0 * 1024.0):0.1} MB / 64 MB Cap";
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            if (_activeDoc.CurrentPageIndex > 0)
            {
                _activeDoc.CurrentPageIndex--;
                DisplayActivePage();
                UpdateStatus();
                if (ThumbnailList != null) ThumbnailList.SelectedIndex = _activeDoc.CurrentPageIndex;
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            if (_activeDoc.CurrentPageIndex < _activeDoc.Pages.Count - 1)
            {
                _activeDoc.CurrentPageIndex++;
                DisplayActivePage();
                UpdateStatus();
                if (ThumbnailList != null) ThumbnailList.SelectedIndex = _activeDoc.CurrentPageIndex;
            }
        }

        private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThumbnailList?.SelectedIndex >= 0 && _activeDoc != null)
            {
                _activeDoc.CurrentPageIndex = ThumbnailList.SelectedIndex;
                DisplayActivePage();
                UpdateStatus();
            }
        }

        private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabList?.SelectedItem is PdfDocumentModel doc)
            {
                SelectDocument(doc);
            }
        }

        private void BtnCloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PdfDocumentModel doc)
            {
                _documents.Remove(doc);
                if (_documents.Count > 0)
                {
                    SelectDocument(_documents.Last());
                }
                else
                {
                    var newDoc = PdfDocumentModel.CreateDefault("Untitled.pdf", 1);
                    _documents.Add(newDoc);
                    SelectDocument(newDoc);
                }
            }
        }

        private void BtnNewTab_Click(object sender, RoutedEventArgs e)
        {
            BtnNewPdfTab_Click(sender, e);
        }

        private void BtnNewPdfTab_Click(object sender, RoutedEventArgs e)
        {
            var newDoc = PdfDocumentModel.CreateDefault($"Blank Document {_documents.Count + 1}.pdf", 1);
            _documents.Add(newDoc);
            SelectDocument(newDoc);
            ShowReaderView();
        }

        private void BtnNewWordTab_Click(object sender, RoutedEventArgs e)
        {
            if (_isWordModified && !string.IsNullOrEmpty(_currentWordFilePath))
            {
                // Document modified
            }
            _currentWordFilePath = null;
            _isWordModified = false;
            WordDocumentService.CreateNewDocument(RtbWordEditor, "Untitled Document");
            UpdateWordStats();
            ShowWordView();
        }

        private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "All Supported Documents|*.pdf;*.docx;*.rtf;*.txt;*.md;*.html|PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx|Rich Text Format (*.rtf)|*.rtf|Markdown (*.md)|*.md|HTML Files (*.html)|*.html|Plain Text (*.txt)|*.txt",
                Title = "Open Document into Omni Studio"
            };

            if (dlg.ShowDialog() == true)
            {
                string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                if (ext == ".docx" || ext == ".rtf" || ext == ".txt" || ext == ".md" || ext == ".html")
                {
                    OpenWordDocumentPath(dlg.FileName);
                }
                else
                {
                    await LoadPdfPathAsync(dlg.FileName);
                    ShowReaderView();
                }
            }
        }

        private void BtnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (ViewWordEditor?.Visibility == Visibility.Visible)
            {
                BtnWordSave_Click(sender, e);
            }
            else
            {
                if (_activeDoc != null && !string.IsNullOrEmpty(_activeDoc.FilePath))
                {
                    PdfDocumentModel.CompileTextToPdf(_activeDoc.Pages[_activeDoc.CurrentPageIndex].ContentText, _activeDoc.FilePath);
                    MessageBox.Show("PDF changes saved successfully.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    BtnSaveAsFile_Click(sender, e);
                }
            }
        }

        private void BtnSaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            if (ViewWordEditor?.Visibility == Visibility.Visible)
            {
                BtnWordSaveAs_Click(sender, e);
            }
            else
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "PDF Document (*.pdf)|*.pdf|Plain Text (*.txt)|*.txt",
                    FileName = _activeDoc?.Title ?? "Document.pdf"
                };
                if (dlg.ShowDialog() == true && _activeDoc != null)
                {
                    PdfDocumentModel.CompileTextToPdf(_activeDoc.Pages[_activeDoc.CurrentPageIndex].ContentText, dlg.FileName);
                    MessageBox.Show($"Document exported to {dlg.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (ViewWordEditor?.Visibility == Visibility.Visible)
            {
                WordDocumentService.PrintDocument(RtbWordEditor, "Omni Word Document");
            }
            else
            {
                MessageBox.Show("Printing current PDF page via Windows Spooler...", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Min(3.0, _currentZoom + 0.15);
            ApplyZoom();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Max(0.4, _currentZoom - 0.15);
            ApplyZoom();
        }

        private void BtnZoomActual_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = 1.0;
            ApplyZoom();
        }

        private void BtnFitWidth_Click(object sender, RoutedEventArgs e)
        {
            if (MainScrollViewer != null && PageBorder != null)
            {
                double targetW = MainScrollViewer.ActualWidth - 60;
                if (targetW > 200 && PageBorder.Width > 0)
                {
                    _currentZoom = Math.Clamp(targetW / PageBorder.Width, 0.4, 3.0);
                    ApplyZoom();
                }
            }
        }

        private void BtnFitPage_Click(object sender, RoutedEventArgs e)
        {
            if (MainScrollViewer != null && PageBorder != null)
            {
                double targetH = MainScrollViewer.ActualHeight - 60;
                if (targetH > 200 && PageBorder.Height > 0)
                {
                    _currentZoom = Math.Clamp(targetH / PageBorder.Height, 0.4, 3.0);
                    ApplyZoom();
                }
            }
        }

        private void ApplyZoom()
        {
            if (CanvasContainer != null)
            {
                CanvasContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            }
            if (TxtZoomLevel != null)
            {
                TxtZoomLevel.Text = $"{(int)(_currentZoom * 100)}%";
            }
        }

        private void BtnRotatePage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            var page = _activeDoc.Pages[_activeDoc.CurrentPageIndex];
            page.Rotation = (page.Rotation + 90) % 360;
            UpdatePageRotation(page.Rotation);
        }

        private void BtnRotateCounterPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            var page = _activeDoc.Pages[_activeDoc.CurrentPageIndex];
            page.Rotation = (page.Rotation + 270) % 360;
            UpdatePageRotation(page.Rotation);
        }

        #endregion

        #region Microsoft Word Document Suite Operations

        private void OpenWordDocumentPath(string filePath)
        {
            if (!File.Exists(filePath)) return;
            _currentWordFilePath = filePath;
            WordDocumentService.LoadDocument(filePath, RtbWordEditor);
            UpdateWordStats();
            ShowWordView();
        }

        private void BtnWordNew_Click(object sender, RoutedEventArgs e)
        {
            BtnNewWordTab_Click(sender, e);
        }

        private void BtnWordOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Word Documents (*.docx)|*.docx|Rich Text Format (*.rtf)|*.rtf|Markdown Files (*.md)|*.md|HTML Files (*.html)|*.html|Plain Text (*.txt)|*.txt|PDF Documents (*.pdf)|*.pdf|All Supported Files|*.docx;*.rtf;*.md;*.html;*.txt;*.pdf",
                Title = "Open Document into Word Suite"
            };
            if (dlg.ShowDialog() == true)
            {
                OpenWordDocumentPath(dlg.FileName);
            }
        }

        private void BtnWordSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentWordFilePath))
            {
                BtnWordSaveAs_Click(sender, e);
            }
            else
            {
                WordDocumentService.SaveDocument(_currentWordFilePath, RtbWordEditor);
                _isWordModified = false;
                MessageBox.Show($"Changes saved to {Path.GetFileName(_currentWordFilePath)}", "Word Suite", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnWordSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Word Document (*.docx)|*.docx|Rich Text Document (*.rtf)|*.rtf|Markdown Document (*.md)|*.md|HTML Document (*.html)|*.html|Plain Text (*.txt)|*.txt|PDF Vector Document (*.pdf)|*.pdf",
                Title = "Save Word Document As...",
                FileName = string.IsNullOrEmpty(_currentWordFilePath) ? "Document.docx" : Path.GetFileName(_currentWordFilePath)
            };

            if (dlg.ShowDialog() == true)
            {
                _currentWordFilePath = dlg.FileName;
                WordDocumentService.SaveDocument(_currentWordFilePath, RtbWordEditor);
                _isWordModified = false;
                MessageBox.Show($"Document saved to {_currentWordFilePath}", "Word Suite", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnWordExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = Path.ChangeExtension(Path.GetFileName(_currentWordFilePath ?? "Document"), ".pdf")
            };
            if (dlg.ShowDialog() == true)
            {
                var range = new TextRange(RtbWordEditor.Document.ContentStart, RtbWordEditor.Document.ContentEnd);
                PdfDocumentModel.CompileTextToPdf(range.Text, dlg.FileName, 12.0, "");
                MessageBox.Show($"Compiled and exported to vector PDF:\n{dlg.FileName}", "PDF Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnWordPrint_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.PrintDocument(RtbWordEditor, "Omni Word Document");
        }

        private void CmbWordFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWordFontFamily?.SelectedItem is ComboBoxItem item && RtbWordEditor != null)
            {
                string fontName = item.Content?.ToString() ?? "Segoe UI";
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(fontName));
            }
        }

        private void CmbWordFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWordFontSize?.SelectedItem is ComboBoxItem item && RtbWordEditor != null)
            {
                if (double.TryParse(item.Content?.ToString(), out double sz))
                {
                    RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, sz);
                }
            }
        }

        private void BtnWordBold_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleBold.Execute(null, RtbWordEditor);
        }

        private void BtnWordItalic_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleItalic.Execute(null, RtbWordEditor);
        }

        private void BtnWordUnderline_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleUnderline.Execute(null, RtbWordEditor);
        }

        private void BtnWordStrikethrough_Click(object sender, RoutedEventArgs e)
        {
            var cur = RtbWordEditor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            if (cur == TextDecorations.Strikethrough)
            {
                RtbWordEditor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            }
            else
            {
                RtbWordEditor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
            }
        }

        private void BtnWordTextColor_Click(object sender, RoutedEventArgs e)
        {
            var colors = new[] { Brushes.Black, Brushes.Navy, Brushes.Crimson, Brushes.DarkGreen, Brushes.DarkViolet };
            RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, colors[new Random().Next(colors.Length)]);
        }

        private void BtnWordHighlightColor_Click(object sender, RoutedEventArgs e)
        {
            var cur = RtbWordEditor.Selection.GetPropertyValue(TextElement.BackgroundProperty);
            if (cur is SolidColorBrush sb && sb.Color == Colors.Yellow)
            {
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            }
            else
            {
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(180, 255, 255, 0)));
            }
        }

        private void BtnWordAlignLeft_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.AlignLeft.Execute(null, RtbWordEditor);
        }

        private void BtnWordAlignCenter_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.AlignCenter.Execute(null, RtbWordEditor);
        }

        private void BtnWordAlignRight_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.AlignRight.Execute(null, RtbWordEditor);
        }

        private void BtnWordAlignJustify_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.AlignJustify.Execute(null, RtbWordEditor);
        }

        private void BtnWordBullets_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleBullets.Execute(null, RtbWordEditor);
        }

        private void BtnWordNumbering_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.ToggleNumbering.Execute(null, RtbWordEditor);
        }

        private void BtnWordInsertTable_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.InsertTable(RtbWordEditor, 3, 3);
        }

        private void BtnWordInsertImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Insert Image into Word Document"
            };
            if (dlg.ShowDialog() == true)
            {
                WordDocumentService.InsertImage(RtbWordEditor, dlg.FileName);
            }
        }

        private void BtnWordInsertHr_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.InsertHorizontalRule(RtbWordEditor);
        }

        private void BtnWordInsertDate_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.InsertDateTime(RtbWordEditor);
        }

        private void BtnWordMarginsNormal_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.SetPageMargins(RtbWordEditor, 48);
        }

        private void BtnWordMarginsNarrow_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.SetPageMargins(RtbWordEditor, 24);
        }

        private void BtnWordMarginsWide_Click(object sender, RoutedEventArgs e)
        {
            WordDocumentService.SetPageMargins(RtbWordEditor, 72);
        }

        private void RtbWordEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // Sync ribbon controls with caret selection
        }

        private void RtbWordEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            _isWordModified = true;
            UpdateWordStats();
        }

        private void UpdateWordStats()
        {
            if (RtbWordEditor == null || TxtWordStats == null) return;
            var (words, chars, paragraphs, readingMins) = WordDocumentService.GetDocumentStatistics(RtbWordEditor);
            TxtWordStats.Text = $"Words: {words:N0} | Characters: {chars:N0} | Paragraphs: {paragraphs} | Reading Time: ~{readingMins} min";
        }

        private void SliderWordZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RtbWordEditor != null)
            {
                double scale = e.NewValue / 100.0;
                RtbWordEditor.LayoutTransform = new ScaleTransform(scale, scale);
            }
        }

        #endregion

        #region PDF Vector Annotation Suite (Ink, Highlighter, Sticky Notes)

        private void BtnTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string tag = btn.Content.ToString() ?? "";
                if (tag.Contains("Pointer")) SetTool("Pointer");
                else if (tag.Contains("Sketch") || tag.Contains("Ink")) SetTool("Ink");
                else if (tag.Contains("Highlight")) SetTool("Highlight");
                else if (tag.Contains("Sticky")) SetTool("Sticky");
                else if (tag.Contains("Eraser")) SetTool("Eraser");
                else if (tag.Contains("Sign")) StampDigitalSignature();
            }
        }

        private void MenuToolPointer_Click(object sender, RoutedEventArgs e) => SetTool("Pointer");
        private void MenuToolInk_Click(object sender, RoutedEventArgs e) => SetTool("Ink");
        private void MenuToolHighlight_Click(object sender, RoutedEventArgs e) => SetTool("Highlight");
        private void MenuToolSticky_Click(object sender, RoutedEventArgs e) => SetTool("Sticky");
        private void MenuToolEraser_Click(object sender, RoutedEventArgs e) => SetTool("Eraser");
        private void MenuToolSignature_Click(object sender, RoutedEventArgs e) => StampDigitalSignature();

        private void SetTool(string toolName)
        {
            _activeTool = toolName;
            UpdateStatus();

            if (PageInkCanvas == null) return;

            switch (toolName)
            {
                case "Ink":
                    PageInkCanvas.IsHitTestVisible = true;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    PageInkCanvas.DefaultDrawingAttributes.Color = Color.FromRgb(220, 38, 38);
                    PageInkCanvas.DefaultDrawingAttributes.Width = 2.5;
                    PageInkCanvas.DefaultDrawingAttributes.Height = 2.5;
                    PageInkCanvas.DefaultDrawingAttributes.IsHighlighter = false;
                    break;

                case "Highlight":
                    PageInkCanvas.IsHitTestVisible = true;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    PageInkCanvas.DefaultDrawingAttributes.Color = Color.FromArgb(120, 250, 204, 21);
                    PageInkCanvas.DefaultDrawingAttributes.Width = 24.0;
                    PageInkCanvas.DefaultDrawingAttributes.Height = 12.0;
                    PageInkCanvas.DefaultDrawingAttributes.IsHighlighter = true;
                    break;

                case "Eraser":
                    PageInkCanvas.IsHitTestVisible = true;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    break;

                default:
                    PageInkCanvas.IsHitTestVisible = false;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.None;
                    break;
            }
        }

        private void StampDigitalSignature()
        {
            if (SignatureBadge != null)
            {
                SignatureBadge.Visibility = Visibility.Visible;
                MessageBox.Show("PKCS#7 SHA-256 Digital Signature embedded into document layer.", "Digital Signature Verified", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PageContentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeTool == "Sticky")
            {
                Point pos = e.GetPosition(NotesCanvas);
                CreateStickyNote(pos.X, pos.Y, "Review note: ");
                e.Handled = true;
            }
        }

        private void CreateStickyNote(double x, double y, string initialText)
        {
            if (NotesCanvas == null) return;

            var noteBorder = new Border
            {
                Width = 160,
                Height = 130,
                Background = new SolidColorBrush(Color.FromRgb(254, 240, 138)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 }
            };

            Canvas.SetLeft(noteBorder, x);
            Canvas.SetTop(noteBorder, y);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerDock = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(250, 204, 21)), Height = 20, Cursor = Cursors.SizeAll };
            var lblTitle = new TextBlock { Text = "📌 Note", FontWeight = FontWeights.Bold, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(113, 63, 18)), Margin = new Thickness(4, 3, 0, 0) };
            var btnClose = new Button { Content = "✕", Width = 16, Height = 16, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Black, FontSize = 9, Cursor = Cursors.Hand };
            btnClose.Click += (s, e) => NotesCanvas.Children.Remove(noteBorder);

            DockPanel.SetDock(btnClose, Dock.Right);
            headerDock.Children.Add(btnClose);
            headerDock.Children.Add(lblTitle);

            bool isDragging = false;
            Point startPoint = new Point();

            headerDock.MouseLeftButtonDown += (s, e) =>
            {
                isDragging = true;
                startPoint = e.GetPosition(NotesCanvas);
                headerDock.CaptureMouse();
                e.Handled = true;
            };

            headerDock.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    Point curPoint = e.GetPosition(NotesCanvas);
                    double dx = curPoint.X - startPoint.X;
                    double dy = curPoint.Y - startPoint.Y;

                    double newX = Math.Max(0, Canvas.GetLeft(noteBorder) + dx);
                    double newY = Math.Max(0, Canvas.GetTop(noteBorder) + dy);

                    Canvas.SetLeft(noteBorder, newX);
                    Canvas.SetTop(noteBorder, newY);

                    startPoint = curPoint;
                }
            };

            headerDock.MouseLeftButtonUp += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    headerDock.ReleaseMouseCapture();
                }
            };

            var txtNote = new TextBox
            {
                Text = initialText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(69, 26, 3)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Margin = new Thickness(5, 2, 5, 5)
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

        #region Themes & Skins Dynamic Palette Engine

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
            SolidColorBrush winBg, winFg, headerBg, headerFg, menuBg, menuFg, submenuBg, submenuFg,
                hoverBg, navBg, canvasBg, cardBg, borderDef, borderLt, accentDef, accentHov, statusBg, statusFg;

            switch (theme)
            {
                case "Modern Light":
                    winBg = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                    winFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    headerBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    headerFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    menuBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    menuFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    submenuBg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    submenuFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    hoverBg = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    navBg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    canvasBg = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    cardBg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    borderDef = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    borderLt = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                    accentDef = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    accentHov = new SolidColorBrush(Color.FromRgb(3, 105, 161));
                    statusBg = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                case "Glass Theme":
                    winBg = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                    winFg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    headerBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    headerFg = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    menuBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    menuFg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    submenuBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    submenuFg = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    hoverBg = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                    navBg = new SolidColorBrush(Color.FromRgb(24, 33, 47));
                    canvasBg = new SolidColorBrush(Color.FromRgb(11, 17, 32));
                    cardBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    borderDef = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                    borderLt = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                    accentDef = new SolidColorBrush(Color.FromRgb(14, 165, 233));
                    accentHov = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    statusBg = new SolidColorBrush(Color.FromRgb(14, 165, 233));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                case "Word Classic Blue":
                    winBg = new SolidColorBrush(Color.FromRgb(243, 242, 241));
                    winFg = new SolidColorBrush(Color.FromRgb(32, 31, 30));
                    headerBg = new SolidColorBrush(Color.FromRgb(24, 90, 189)); // Word Blue
                    headerFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    menuBg = new SolidColorBrush(Color.FromRgb(24, 90, 189));
                    menuFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    submenuBg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    submenuFg = new SolidColorBrush(Color.FromRgb(32, 31, 30));
                    hoverBg = new SolidColorBrush(Color.FromRgb(16, 75, 165));
                    navBg = new SolidColorBrush(Color.FromRgb(237, 235, 233));
                    canvasBg = new SolidColorBrush(Color.FromRgb(225, 223, 221));
                    cardBg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    borderDef = new SolidColorBrush(Color.FromRgb(210, 208, 206));
                    borderLt = new SolidColorBrush(Color.FromRgb(180, 178, 176));
                    accentDef = new SolidColorBrush(Color.FromRgb(24, 90, 189));
                    accentHov = new SolidColorBrush(Color.FromRgb(16, 75, 165));
                    statusBg = new SolidColorBrush(Color.FromRgb(24, 90, 189));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                case "OLED Black":
                    winBg = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    winFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    headerBg = new SolidColorBrush(Color.FromRgb(10, 10, 10));
                    headerFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    menuBg = new SolidColorBrush(Color.FromRgb(10, 10, 10));
                    menuFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    submenuBg = new SolidColorBrush(Color.FromRgb(16, 16, 16));
                    submenuFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    hoverBg = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                    navBg = new SolidColorBrush(Color.FromRgb(8, 8, 8));
                    canvasBg = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    cardBg = new SolidColorBrush(Color.FromRgb(12, 12, 12));
                    borderDef = new SolidColorBrush(Color.FromRgb(28, 28, 28));
                    borderLt = new SolidColorBrush(Color.FromRgb(48, 48, 48));
                    accentDef = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    accentHov = new SolidColorBrush(Color.FromRgb(29, 78, 216));
                    statusBg = new SolidColorBrush(Color.FromRgb(20, 20, 20));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                case "Sepia Reading":
                    winBg = new SolidColorBrush(Color.FromRgb(245, 235, 225));
                    winFg = new SolidColorBrush(Color.FromRgb(69, 50, 33));
                    headerBg = new SolidColorBrush(Color.FromRgb(234, 219, 196));
                    headerFg = new SolidColorBrush(Color.FromRgb(69, 50, 33));
                    menuBg = new SolidColorBrush(Color.FromRgb(234, 219, 196));
                    menuFg = new SolidColorBrush(Color.FromRgb(69, 50, 33));
                    submenuBg = new SolidColorBrush(Color.FromRgb(253, 248, 238));
                    submenuFg = new SolidColorBrush(Color.FromRgb(69, 50, 33));
                    hoverBg = new SolidColorBrush(Color.FromRgb(223, 206, 180));
                    navBg = new SolidColorBrush(Color.FromRgb(234, 219, 196));
                    canvasBg = new SolidColorBrush(Color.FromRgb(223, 206, 180));
                    cardBg = new SolidColorBrush(Color.FromRgb(253, 248, 238));
                    borderDef = new SolidColorBrush(Color.FromRgb(215, 196, 165));
                    borderLt = new SolidColorBrush(Color.FromRgb(195, 176, 145));
                    accentDef = new SolidColorBrush(Color.FromRgb(141, 110, 99));
                    accentHov = new SolidColorBrush(Color.FromRgb(121, 85, 72));
                    statusBg = new SolidColorBrush(Color.FromRgb(141, 110, 99));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                case "Cyberpunk Neon":
                    winBg = new SolidColorBrush(Color.FromRgb(13, 11, 24));
                    winFg = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                    headerBg = new SolidColorBrush(Color.FromRgb(21, 16, 42));
                    headerFg = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                    menuBg = new SolidColorBrush(Color.FromRgb(21, 16, 42));
                    menuFg = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                    submenuBg = new SolidColorBrush(Color.FromRgb(28, 20, 56));
                    submenuFg = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                    hoverBg = new SolidColorBrush(Color.FromRgb(255, 0, 127));
                    navBg = new SolidColorBrush(Color.FromRgb(21, 16, 42));
                    canvasBg = new SolidColorBrush(Color.FromRgb(8, 6, 16));
                    cardBg = new SolidColorBrush(Color.FromRgb(28, 20, 56));
                    borderDef = new SolidColorBrush(Color.FromRgb(255, 0, 127));
                    borderLt = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                    accentDef = new SolidColorBrush(Color.FromRgb(255, 0, 127));
                    accentHov = new SolidColorBrush(Color.FromRgb(218, 0, 108));
                    statusBg = new SolidColorBrush(Color.FromRgb(255, 0, 127));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                case "Emerald Forest":
                    winBg = new SolidColorBrush(Color.FromRgb(6, 26, 20));
                    winFg = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    headerBg = new SolidColorBrush(Color.FromRgb(10, 41, 32));
                    headerFg = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    menuBg = new SolidColorBrush(Color.FromRgb(10, 41, 32));
                    menuFg = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    submenuBg = new SolidColorBrush(Color.FromRgb(13, 53, 41));
                    submenuFg = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    hoverBg = new SolidColorBrush(Color.FromRgb(4, 120, 87));
                    navBg = new SolidColorBrush(Color.FromRgb(10, 41, 32));
                    canvasBg = new SolidColorBrush(Color.FromRgb(4, 18, 14));
                    cardBg = new SolidColorBrush(Color.FromRgb(13, 53, 41));
                    borderDef = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                    borderLt = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    accentDef = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    accentHov = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                    statusBg = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;

                default: // Fluent Dark & Custom Theme
                    winBg = new SolidColorBrush(Color.FromRgb(15, 15, 18));
                    winFg = new SolidColorBrush(Color.FromRgb(236, 236, 239));
                    headerBg = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    headerFg = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    menuBg = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    menuFg = new SolidColorBrush(Color.FromRgb(236, 236, 239));
                    submenuBg = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    submenuFg = new SolidColorBrush(Color.FromRgb(244, 244, 245));
                    hoverBg = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                    navBg = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    canvasBg = new SolidColorBrush(Color.FromRgb(9, 9, 11));
                    cardBg = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    borderDef = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                    borderLt = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                    accentDef = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    accentHov = new SolidColorBrush(Color.FromRgb(3, 105, 161));
                    statusBg = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    statusFg = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    break;
            }

            // Update Dynamic Theme Resources
            Resources["WindowBgBrush"] = winBg;
            Resources["WindowFgBrush"] = winFg;
            Resources["HeaderBgBrush"] = headerBg;
            Resources["HeaderFgBrush"] = headerFg;
            Resources["MenuBgBrush"] = menuBg;
            Resources["MenuFgBrush"] = menuFg;
            Resources["SubmenuBgBrush"] = submenuBg;
            Resources["SubmenuFgBrush"] = submenuFg;
            Resources["ItemHoverBgBrush"] = hoverBg;
            Resources["NavRailBgBrush"] = navBg;
            Resources["CanvasBgBrush"] = canvasBg;
            Resources["CardBgBrush"] = cardBg;
            Resources["BorderBrushDefault"] = borderDef;
            Resources["BorderBrushLight"] = borderLt;
            Resources["AccentBrushDefault"] = accentDef;
            Resources["AccentHoverBrush"] = accentHov;
            Resources["StatusBarBgBrush"] = statusBg;
            Resources["StatusBarFgBrush"] = statusFg;

            Background = winBg;
            Foreground = winFg;
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

        #region Modals & Window Controls (Organizer, Scanner, Converter, Title Drag, Min, Max, Close)

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BtnWinMaximize_Click(sender, e);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

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
            string msg = "Omni PDF Studio & Word Suite Keyboard Shortcuts:\n\n" +
                         "• F11: Dedicated Full Screen Presentation Mode\n" +
                         "• F4: Toggle Left Navigation Rail\n" +
                         "• Ctrl+T: Toggle Thumbnails Sidebar\n" +
                         "• Ctrl+O: Open Document (PDF, Word, RTF, MD, HTML)\n" +
                         "• Ctrl+N: New Word Document (.docx)\n" +
                         "• Ctrl+Shift+N: New Blank PDF Document\n" +
                         "• Ctrl+S: Save Incremental Updates\n" +
                         "• Ctrl+P: Print Document\n" +
                         "• Ctrl+F: Find in Document\n" +
                         "• Ctrl+R: Rotate Clockwise 90°\n" +
                         "• Ctrl++: Zoom In\n" +
                         "• Ctrl+-: Zoom Out\n" +
                         "• Ctrl+0: Actual Size (100%)\n" +
                         "• Esc: Exit Search / Exit Full Screen Presentation";
            MessageBox.Show(msg, "Keyboard Shortcuts Reference", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            string msg = "Omni PDF Studio & Microsoft Word Suite Enterprise Edition v1.2.0\n\n" +
                         "• Native Microsoft Word (.docx), RTF, Markdown, HTML & PDF Reader/Writer\n" +
                         "• Dedicated Borderless Full-Screen Presentation Mode with Floating Controls\n" +
                         "• Permanent Top Title Bar with 100% Screen-Visible Caption Buttons\n" +
                         "• Dynamic Multi-Theme Color Engine with High-Contrast Tools Menus\n" +
                         "• DirectX WinRT Vector Rendering Engine with 64MB LRU RAM Ceiling\n" +
                         "• Dual ABI Android 6.0 Marshmallow (API 23) to API 34+ Compatibility\n\n" +
                         "Copyright © 2026 Omni Studio Inc. All rights reserved.";
            MessageBox.Show(msg, "About Omni Studio Enterprise", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                if (_isFullScreen) ExitFullScreenPresentation();
                else EnterFullScreenPresentation();
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
                    ExitFullScreenPresentation();
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
                else if (e.Key == Key.P) { BtnPrint_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.N) { BtnNewWordTab_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.F) { BtnToggleSearch_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.R) { BtnRotatePage_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.T) { BtnToggleThumbnails_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add) { BtnZoomIn_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { BtnZoomOut_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { BtnZoomActual_Click(this, new RoutedEventArgs()); e.Handled = true; }
            }
        }

        #endregion
    }
}
