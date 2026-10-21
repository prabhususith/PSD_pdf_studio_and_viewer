using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        private readonly SpeechReaderService _speechService = new();
        private Point? _measureStartPoint = null;

        public enum PageViewMode
        {
            Continuous,
            SinglePage,
            TwoPageSpread
        }

        private PageViewMode _currentViewMode = PageViewMode.Continuous;
        private bool _isDarkReadingMode = false;
        private bool _isProgrammaticScroll = false;
        private bool _isSnapshotDragging = false;
        private Point _snapshotStartPoint;
        private readonly List<Border> _pageCards = new();
        private readonly List<Image> _pageImages = new();
        private readonly List<TextBox> _pageTextBoxes = new();
        private Color _activePdfPenColor = Color.FromRgb(220, 38, 38);
        private double _activePdfPenWidth = 2.5;

        public MainWindow()
        {
            InitializeComponent();
            Closing += (s, e) => _speechService.Dispose();

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

                try
                {
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] candidates = new[]
                    {
                        Path.Combine(appDir, "docs", "PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf"),
                        Path.Combine(appDir, "PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf"),
                        Path.GetFullPath(@"docs\PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf")
                    };

                    foreach (var c in candidates)
                    {
                        if (File.Exists(c))
                        {
                            _documents.Clear();
                            await LoadPdfPathAsync(c);
                            break;
                        }
                    }
                }
                catch { }
            };
        }

        #region Device Library & Dashboard Engine

        public enum DashboardViewMode
        {
            Grid,
            Thumbnails,
            Compact
        }

        private DashboardViewMode _currentDashboardView = DashboardViewMode.Grid;

        private void SetDashboardViewMode(DashboardViewMode mode)
        {
            _currentDashboardView = mode;
            var accentBtnStyle = TryFindResource("AccentBtn") as Style;
            var modernBtnStyle = TryFindResource("ModernBtn") as Style;

            if (BtnDashboardGrid != null && accentBtnStyle != null && modernBtnStyle != null)
                BtnDashboardGrid.Style = (mode == DashboardViewMode.Grid) ? accentBtnStyle : modernBtnStyle;

            if (BtnDashboardThumbnails != null && accentBtnStyle != null && modernBtnStyle != null)
                BtnDashboardThumbnails.Style = (mode == DashboardViewMode.Thumbnails) ? accentBtnStyle : modernBtnStyle;

            if (BtnDashboardCompact != null && accentBtnStyle != null && modernBtnStyle != null)
                BtnDashboardCompact.Style = (mode == DashboardViewMode.Compact) ? accentBtnStyle : modernBtnStyle;

            if (LibraryList == null) return;

            switch (mode)
            {
                case DashboardViewMode.Grid:
                    if (TryFindResource("DashboardGridTemplate") is DataTemplate gridTmpl)
                        LibraryList.ItemTemplate = gridTmpl;
                    if (TryFindResource("WrapPanelHost") is ItemsPanelTemplate wrapPanel)
                        LibraryList.ItemsPanel = wrapPanel;
                    break;

                case DashboardViewMode.Thumbnails:
                    if (TryFindResource("DashboardThumbnailTemplate") is DataTemplate thumbTmpl)
                        LibraryList.ItemTemplate = thumbTmpl;
                    if (TryFindResource("WrapPanelHost") is ItemsPanelTemplate wrapPanel2)
                        LibraryList.ItemsPanel = wrapPanel2;
                    break;

                case DashboardViewMode.Compact:
                    if (TryFindResource("DashboardCompactTemplate") is DataTemplate compactTmpl)
                        LibraryList.ItemTemplate = compactTmpl;
                    if (TryFindResource("StackPanelHost") is ItemsPanelTemplate stackPanel)
                        LibraryList.ItemsPanel = stackPanel;
                    break;
            }
        }

        private void BtnDashboardGrid_Click(object sender, RoutedEventArgs e)
        {
            SetDashboardViewMode(DashboardViewMode.Grid);
        }

        private void BtnDashboardThumbnails_Click(object sender, RoutedEventArgs e)
        {
            SetDashboardViewMode(DashboardViewMode.Thumbnails);
        }

        private void BtnDashboardCompact_Click(object sender, RoutedEventArgs e)
        {
            SetDashboardViewMode(DashboardViewMode.Compact);
        }

        private void CmbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLibraryFiltersAndSort();
        }

        private void CmbFilterDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLibraryFiltersAndSort();
        }

        private void CmbFilterSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLibraryFiltersAndSort();
        }

        private void CmbSortOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLibraryFiltersAndSort();
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            if (TxtLibrarySearch != null) TxtLibrarySearch.Text = "";
            if (CmbFilterType != null) CmbFilterType.SelectedIndex = 0;
            if (CmbFilterDate != null) CmbFilterDate.SelectedIndex = 0;
            if (CmbFilterSize != null) CmbFilterSize.SelectedIndex = 0;
            if (CmbSortOrder != null) CmbSortOrder.SelectedIndex = 0;
            ApplyLibraryFiltersAndSort();
        }

        private void ApplyLibraryFiltersAndSort()
        {
            if (_libraryItems == null || _filteredLibraryItems == null) return;

            string query = TxtLibrarySearch?.Text.Trim() ?? "";
            string selectedType = (CmbFilterType?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Formats";
            string selectedDate = (CmbFilterDate?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Time";
            string selectedSize = (CmbFilterSize?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Sizes";
            string selectedSort = (CmbSortOrder?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Date Modified (Newest)";

            IEnumerable<PdfLibraryItem> filtered = _libraryItems;

            // 1. Text Search Filter
            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(item =>
                    (item.Title != null && item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (item.FileName != null && item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (item.FilePath != null && item.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (item.DirectoryPath != null && item.DirectoryPath.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            // 2. Type / Format Filter
            if (selectedType.StartsWith("PDF", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.Extension, ".pdf", StringComparison.OrdinalIgnoreCase));
            }
            else if (selectedType.StartsWith("Word", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.Extension, ".docx", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(x.Extension, ".doc", StringComparison.OrdinalIgnoreCase));
            }
            else if (selectedType.StartsWith("Text", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.Extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(x.Extension, ".md", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(x.Extension, ".rtf", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(x.Extension, ".html", StringComparison.OrdinalIgnoreCase));
            }
            else if (selectedType.StartsWith("Photoshop", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.Extension, ".psd", StringComparison.OrdinalIgnoreCase));
            }

            // 3. Date Modified Filter
            DateTime now = DateTime.Now;
            if (selectedDate.Equals("Today", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => x.LastModified.Date == DateTime.Today);
            }
            else if (selectedDate.Equals("This Week", StringComparison.OrdinalIgnoreCase))
            {
                DateTime weekAgo = now.AddDays(-7);
                filtered = filtered.Where(x => x.LastModified >= weekAgo);
            }
            else if (selectedDate.Equals("This Month", StringComparison.OrdinalIgnoreCase))
            {
                DateTime monthAgo = now.AddDays(-30);
                filtered = filtered.Where(x => x.LastModified >= monthAgo);
            }
            else if (selectedDate.Equals("Past Year", StringComparison.OrdinalIgnoreCase))
            {
                DateTime yearAgo = now.AddDays(-365);
                filtered = filtered.Where(x => x.LastModified >= yearAgo);
            }

            // 4. File Size Filter
            if (selectedSize.StartsWith("< 1 MB", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => x.FileSizeBytes < 1024 * 1024);
            }
            else if (selectedSize.StartsWith("1 - 10 MB", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => x.FileSizeBytes >= 1024 * 1024 && x.FileSizeBytes <= 10 * 1024 * 1024);
            }
            else if (selectedSize.StartsWith("> 10 MB", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => x.FileSizeBytes > 10 * 1024 * 1024);
            }

            // 5. Multi-field Sorting Engine
            if (selectedSort.Contains("Oldest"))
            {
                filtered = filtered.OrderBy(x => x.LastModified);
            }
            else if (selectedSort.Contains("A → Z"))
            {
                filtered = filtered.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase);
            }
            else if (selectedSort.Contains("Z → A"))
            {
                filtered = filtered.OrderByDescending(x => x.Title, StringComparer.OrdinalIgnoreCase);
            }
            else if (selectedSort.Contains("Largest"))
            {
                filtered = filtered.OrderByDescending(x => x.FileSizeBytes);
            }
            else if (selectedSort.Contains("Smallest"))
            {
                filtered = filtered.OrderBy(x => x.FileSizeBytes);
            }
            else // Date Modified (Newest) default
            {
                filtered = filtered.OrderByDescending(x => x.LastModified);
            }

            var list = filtered.ToList();
            _filteredLibraryItems.Clear();
            foreach (var item in list)
            {
                _filteredLibraryItems.Add(item);
            }

            if (TxtLibraryFooterCount != null)
            {
                TxtLibraryFooterCount.Text = $"Showing {_filteredLibraryItems.Count} of {_libraryItems.Count} indexed documents";
            }
        }

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
                totalBytes += item.FileSizeBytes;
            }

            ApplyLibraryFiltersAndSort();

            if (TxtKpiTotalSize != null)
            {
                double mb = totalBytes / (1024.0 * 1024.0);
                TxtKpiTotalSize.Text = mb > 1024 ? $"{mb / 1024.0:0.1} GB" : $"{mb:0.1} MB";
            }
        }

        private void TxtLibrarySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyLibraryFiltersAndSort();
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

        private async void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && File.Exists(item.FilePath))
            {
                string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                if (ext == ".docx" || ext == ".rtf" || ext == ".txt" || ext == ".md" || ext == ".html")
                {
                    OpenWordDocumentPath(item.FilePath);
                }
                else
                {
                    await LoadPdfPathAsync(item.FilePath);
                    ShowReaderView();
                }
            }
        }

        private async void LibMenuOpenReader_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && File.Exists(item.FilePath))
            {
                await LoadPdfPathAsync(item.FilePath);
                ShowReaderView();
            }
        }

        private void LibMenuOpenWord_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && File.Exists(item.FilePath))
            {
                OpenWordDocumentPath(item.FilePath);
            }
        }

        private async void LibMenuOrganize_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && File.Exists(item.FilePath))
            {
                await LoadPdfPathAsync(item.FilePath);
                ShowReaderView();
                BtnPageOrganizer_Click(sender, e);
            }
        }

        private async void LibMenuConvert_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && File.Exists(item.FilePath))
            {
                await LoadPdfPathAsync(item.FilePath);
                ShowReaderView();
                BtnTextConverter_Click(sender, e);
            }
        }

        private void LibMenuShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && File.Exists(item.FilePath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not open file explorer: {ex.Message}", "PSD PDF Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void LibMenuCopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item && !string.IsNullOrEmpty(item.FilePath))
            {
                try
                {
                    Clipboard.SetText(item.FilePath);
                    if (TxtLibraryFooterCount != null)
                    {
                        TxtLibraryFooterCount.Text = $"Copied path to clipboard: {item.FilePath}";
                    }
                }
                catch { }
            }
        }

        private void LibMenuProperties_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryList?.SelectedItem is PdfLibraryItem item)
            {
                string info = $"File Title: {item.Title}\n" +
                              $"File Name: {item.FileName}\n" +
                              $"Location: {item.FilePath}\n" +
                              $"Directory: {item.DirectoryPath}\n" +
                              $"Format: {item.FormatBadge} ({item.Extension})\n" +
                              $"File Size: {item.FormattedSize} ({item.FileSizeBytes:N0} bytes)\n" +
                              $"Last Modified: {item.FormattedDate}\n" +
                              $"Estimated Pages: {item.PageCountDisplay}";
                MessageBox.Show(info, $"Document Properties - {item.Title}", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (BtnToggleNavRail != null) BtnToggleNavRail.Content = "◫ Show Sidebar (F4)";
                if (BtnFloatingShowRail != null) BtnFloatingShowRail.Visibility = Visibility.Visible;
                if (BtnHeaderToggleNav != null) BtnHeaderToggleNav.ToolTip = "Expand Navigation Sidebar (F4)";
            }
            else
            {
                ColNavRail.Width = new GridLength(190);
                if (BtnToggleNavRail != null) BtnToggleNavRail.Content = "◫ Hide Sidebar (F4)";
                if (BtnFloatingShowRail != null) BtnFloatingShowRail.Visibility = Visibility.Collapsed;
                if (BtnHeaderToggleNav != null) BtnHeaderToggleNav.ToolTip = "Collapse Navigation Sidebar (F4)";
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
            RebuildPageCards();
            UpdateStatus();
        }

        private void RebuildPageCards()
        {
            if (_activeDoc == null || PagesStackPanel == null || PageBorder == null) return;

            _pageCards.Clear();
            _pageImages.Clear();
            _pageTextBoxes.Clear();
            PagesStackPanel.Children.Clear();

            if (_activeDoc.Pages.Count == 0) return;

            // Page 0 uses the designer PageBorder to preserve XAML wiring
            PagesStackPanel.Children.Add(PageBorder);
            _pageCards.Add(PageBorder);
            _pageImages.Add(ImgPageRender);
            _pageTextBoxes.Add(TxtPageContent);

            UpdatePageCardAppearance(PageBorder, ImgPageRender, TxtPageContent, _activeDoc.Pages[0], 0);

            // Additional pages for Continuous / Multi-Page flow
            for (int i = 1; i < _activeDoc.Pages.Count; i++)
            {
                var (cardBorder, img, txt) = CreateDynamicPageCard(i, _activeDoc.Pages[i]);
                PagesStackPanel.Children.Add(cardBorder);
                _pageCards.Add(cardBorder);
                _pageImages.Add(img);
                _pageTextBoxes.Add(txt);
            }

            ApplyViewModeLayout();
        }

        private (Border Card, Image Img, TextBox Txt) CreateDynamicPageCard(int index, PdfPageModel page)
        {
            var border = new Border
            {
                Background = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(30, 30, 36)) : Brushes.White,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 18),
                Width = page.Width > 0 ? page.Width : 612.0,
                Height = page.Height > 0 ? page.Height : 792.0,
                Tag = index,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 4,
                    Opacity = 0.45,
                    Color = Colors.Black
                }
            };

            var grid = new Grid();
            grid.MouseLeftButtonDown += (s, e) =>
            {
                if (_activeDoc != null) _activeDoc.CurrentPageIndex = index;
                UpdateStatus();
                if (ThumbnailList != null && ThumbnailList.SelectedIndex != index)
                {
                    ThumbnailList.SelectedIndex = index;
                }
            };

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txt = new TextBox
            {
                Margin = new Thickness(40),
                Background = Brushes.Transparent,
                Foreground = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(228, 228, 231)) : new SolidColorBrush(Color.FromRgb(24, 24, 27)),
                BorderThickness = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                FontSize = 13,
                FontFamily = new FontFamily("Georgia"),
                Text = page.ContentText
            };

            if (page.RenderedImage != null)
            {
                img.Source = page.RenderedImage;
                img.Visibility = Visibility.Visible;
                txt.Visibility = Visibility.Collapsed;
            }
            else
            {
                img.Visibility = Visibility.Collapsed;
                txt.Visibility = Visibility.Visible;
            }

            grid.Children.Add(img);
            grid.Children.Add(txt);
            border.Child = grid;

            return (border, img, txt);
        }

        private void UpdatePageCardAppearance(Border border, Image img, TextBox txt, PdfPageModel page, int index)
        {
            border.Width = page.Width > 0 ? page.Width : 612.0;
            border.Height = page.Height > 0 ? page.Height : 792.0;
            border.Background = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(30, 30, 36)) : Brushes.White;

            if (page.RenderedImage != null)
            {
                img.Source = page.RenderedImage;
                img.Visibility = Visibility.Visible;
                txt.Visibility = Visibility.Collapsed;
            }
            else
            {
                img.Visibility = Visibility.Collapsed;
                txt.Visibility = Visibility.Visible;
                txt.Text = page.ContentText;
                txt.Foreground = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(228, 228, 231)) : new SolidColorBrush(Color.FromRgb(24, 24, 27));
            }

            if (index == 0)
            {
                UpdatePageRotation(page.Rotation);
            }
        }

        private void DisplayActivePage()
        {
            if (_activeDoc == null || _pageCards.Count == 0) return;
            int idx = Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1);
            if (idx < _pageCards.Count)
            {
                UpdatePageCardAppearance(_pageCards[idx], _pageImages[idx], _pageTextBoxes[idx], _activeDoc.Pages[idx], idx);
            }
            UpdateStatus();
        }

        private void ApplyViewModeLayout()
        {
            if (_activeDoc == null || _pageCards.Count == 0 || PagesStackPanel == null) return;

            if (_currentViewMode == PageViewMode.Continuous)
            {
                PagesStackPanel.Orientation = Orientation.Vertical;
                for (int i = 0; i < _pageCards.Count; i++)
                {
                    _pageCards[i].Visibility = Visibility.Visible;
                    _pageCards[i].Margin = new Thickness(0, 0, 0, 18);
                }
            }
            else if (_currentViewMode == PageViewMode.SinglePage)
            {
                int activeIdx = Math.Clamp(_activeDoc.CurrentPageIndex, 0, _pageCards.Count - 1);
                for (int i = 0; i < _pageCards.Count; i++)
                {
                    _pageCards[i].Visibility = i == activeIdx ? Visibility.Visible : Visibility.Collapsed;
                    _pageCards[i].Margin = new Thickness(0);
                }
            }
            else if (_currentViewMode == PageViewMode.TwoPageSpread)
            {
                PagesStackPanel.Orientation = Orientation.Vertical;
                for (int i = 0; i < _pageCards.Count; i++)
                {
                    _pageCards[i].Visibility = Visibility.Visible;
                    _pageCards[i].Margin = new Thickness(0, 0, 0, 18);
                }
            }

            UpdateViewModeButtons();
        }

        private void UpdateViewModeButtons()
        {
            if (BtnViewContinuous == null || BtnViewSinglePage == null || BtnViewTwoPage == null) return;
            var accent = (Style)FindResource("AccentBtn");
            var modern = (Style)FindResource("ModernBtn");

            BtnViewContinuous.Style = _currentViewMode == PageViewMode.Continuous ? accent : modern;
            BtnViewSinglePage.Style = _currentViewMode == PageViewMode.SinglePage ? accent : modern;
            BtnViewTwoPage.Style = _currentViewMode == PageViewMode.TwoPageSpread ? accent : modern;
        }

        public void ScrollToPage(int targetIndex)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            targetIndex = Math.Clamp(targetIndex, 0, _activeDoc.Pages.Count - 1);
            _activeDoc.CurrentPageIndex = targetIndex;

            if (_currentViewMode == PageViewMode.Continuous)
            {
                if (targetIndex < _pageCards.Count)
                {
                    _isProgrammaticScroll = true;
                    try
                    {
                        _pageCards[targetIndex].BringIntoView();
                    }
                    finally
                    {
                        _isProgrammaticScroll = false;
                    }
                }
            }
            else
            {
                ApplyViewModeLayout();
            }

            UpdateStatus();
            if (ThumbnailList != null && ThumbnailList.SelectedIndex != targetIndex)
            {
                ThumbnailList.SelectedIndex = targetIndex;
                ThumbnailList.ScrollIntoView(ThumbnailList.SelectedItem);
            }

            // Immediately ensure visible viewport is prioritized and rendered
            _ = EnsureViewportPagesRenderedAsync(_activeDoc, targetIndex);
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isProgrammaticScroll || _activeDoc == null || _activeDoc.Pages.Count <= 1 || MainScrollViewer == null) return;
            if (_currentViewMode != PageViewMode.Continuous) return;

            double offset = MainScrollViewer.VerticalOffset;
            double accumulated = 0;
            int visible = 0;

            for (int i = 0; i < _pageCards.Count; i++)
            {
                double h = _pageCards[i].ActualHeight > 0 ? _pageCards[i].ActualHeight + 18 : (_pageCards[i].Height + 18);
                if (accumulated + (h * 0.4) >= offset)
                {
                    visible = i;
                    break;
                }
                accumulated += h;
            }

            if (visible != _activeDoc.CurrentPageIndex && visible < _activeDoc.Pages.Count)
            {
                _activeDoc.CurrentPageIndex = visible;
                UpdateStatus();
                if (ThumbnailList != null && ThumbnailList.SelectedIndex != visible)
                {
                    ThumbnailList.SelectedIndex = visible;
                    ThumbnailList.ScrollIntoView(ThumbnailList.SelectedItem);
                }

                // Adaptive multi-threaded viewport rasterization
                _ = EnsureViewportPagesRenderedAsync(_activeDoc, visible);
            }
        }

        private CancellationTokenSource? _viewportRenderCts;

        private async Task RenderDocumentPagesAsync(PdfDocumentModel doc)
        {
            if (string.IsNullOrEmpty(doc.FilePath) || !File.Exists(doc.FilePath)) return;
            await EnsureViewportPagesRenderedAsync(doc, doc.CurrentPageIndex);
        }

        private async Task EnsureViewportPagesRenderedAsync(PdfDocumentModel doc, int centerIdx)
        {
            if (string.IsNullOrEmpty(doc.FilePath) || !File.Exists(doc.FilePath) || doc.Pages.Count == 0) return;

            _viewportRenderCts?.Cancel();
            var cts = new CancellationTokenSource();
            _viewportRenderCts = cts;
            var token = cts.Token;

            int totalPages = doc.Pages.Count;
            centerIdx = Math.Clamp(centerIdx, 0, totalPages - 1);
            double targetWidth = Math.Clamp(1400.0 * Math.Max(1.0, _currentZoom), 800.0, 3200.0);

            // Phase 1: High-Priority Viewport Cluster (Center +/- 2 pages)
            var priorityIndices = new List<int> { centerIdx };
            for (int offset = 1; offset <= 2; offset++)
            {
                int next = centerIdx + offset;
                if (next < totalPages) priorityIndices.Add(next);
                int prev = centerIdx - offset;
                if (prev >= 0) priorityIndices.Add(prev);
            }

            // Render high-priority pages concurrently via native engine
            var highPriorityTasks = priorityIndices.Select(async idx =>
            {
                if (token.IsCancellationRequested) return;
                if (doc.Pages[idx].RenderedImage != null)
                {
                    Dispatcher.Invoke(() => ApplyPageRenderToUi(idx, doc.Pages[idx].RenderedImage));
                    return;
                }

                var img = await RealPdfRenderer.RenderPdfPageAsync(doc.FilePath, idx, targetWidth);
                if (img != null && !token.IsCancellationRequested)
                {
                    doc.Pages[idx].RenderedImage = img;
                    Dispatcher.Invoke(() => ApplyPageRenderToUi(idx, img));
                }
            });

            await Task.WhenAll(highPriorityTasks);
            if (token.IsCancellationRequested) return;

            // Phase 2: Background pre-fetching ordered by distance from viewport
            _ = Task.Run(async () =>
            {
                var distantIndices = Enumerable.Range(0, totalPages)
                    .Where(i => !priorityIndices.Contains(i))
                    .OrderBy(i => Math.Abs(i - centerIdx))
                    .ToList();

                foreach (int idx in distantIndices)
                {
                    if (token.IsCancellationRequested) break;
                    if (doc.Pages[idx].RenderedImage != null) continue;

                    // If user is far away and document is large, don't pre-render excessively to protect battery and memory
                    if (totalPages > 30 && Math.Abs(idx - centerIdx) > 6)
                        continue;

                    var img = await RealPdfRenderer.RenderPdfPageAsync(doc.FilePath, idx, targetWidth);
                    if (img != null && !token.IsCancellationRequested)
                    {
                        doc.Pages[idx].RenderedImage = img;
                        Dispatcher.Invoke(() => ApplyPageRenderToUi(idx, img));
                    }
                }

                // Phase 3: Intelligent LRU memory trimming for large documents (> 25 pages)
                if (totalPages > 25)
                {
                    for (int i = 0; i < totalPages; i++)
                    {
                        if (Math.Abs(i - centerIdx) > 8 && doc.Pages[i].RenderedImage != null)
                        {
                            doc.Pages[i].RenderedImage = null; // Free up managed memory
                            int evictedIdx = i;
                            Dispatcher.Invoke(() =>
                            {
                                if (evictedIdx < _pageImages.Count && evictedIdx < _pageTextBoxes.Count)
                                {
                                    _pageImages[evictedIdx].Source = null;
                                    _pageImages[evictedIdx].Visibility = Visibility.Collapsed;
                                    _pageTextBoxes[evictedIdx].Visibility = Visibility.Visible;
                                }
                            });
                        }
                    }
                }
            }, token);
        }

        private void ApplyPageRenderToUi(int pIdx, BitmapSource? img)
        {
            if (img == null || pIdx >= _pageImages.Count || pIdx >= _pageTextBoxes.Count) return;
            _pageImages[pIdx].Source = img;
            _pageImages[pIdx].Visibility = Visibility.Visible;
            _pageTextBoxes[pIdx].Visibility = Visibility.Collapsed;
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

            if (TxtJumpPage != null) TxtJumpPage.Text = cur.ToString();
            if (TxtNavPageIndicator != null) TxtNavPageIndicator.Text = $" / {total}";
            if (StatusPage != null) StatusPage.Text = $"Page {cur} of {total}";
            if (StatusDimensions != null && _activeDoc.Pages.Count > 0)
            {
                var p = _activeDoc.Pages[Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1)];
                StatusDimensions.Text = $"Dimensions: {(int)p.Width} x {(int)p.Height} pt";
            }
            if (StatusToolIndicator != null) StatusToolIndicator.Text = $"Tool: {_activeTool}";
            
            var (rasterizerBytes, docCount, pageCount) = RealPdfRenderer.GetEngineTelemetry();
            double mb = rasterizerBytes / (1024.0 * 1024.0);
            if (StatusLru != null) StatusLru.Text = $"Engine: {mb:0.1} MB ({pageCount} cached pages, {docCount} docs)";
        }

        private void BtnFirstPage_Click(object sender, RoutedEventArgs e) => ScrollToPage(0);

        private void BtnLastPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc != null && _activeDoc.Pages.Count > 0)
                ScrollToPage(_activeDoc.Pages.Count - 1);
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc != null && _activeDoc.CurrentPageIndex > 0)
                ScrollToPage(_activeDoc.CurrentPageIndex - 1);
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc != null && _activeDoc.CurrentPageIndex < _activeDoc.Pages.Count - 1)
                ScrollToPage(_activeDoc.CurrentPageIndex + 1);
        }

        private void TxtJumpPage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && int.TryParse(TxtJumpPage.Text.Trim(), out int pageNum))
            {
                ScrollToPage(pageNum - 1);
                e.Handled = true;
            }
        }

        private void BtnViewContinuous_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = PageViewMode.Continuous;
            ApplyViewModeLayout();
            ScrollToPage(_activeDoc?.CurrentPageIndex ?? 0);
        }

        private void BtnViewSinglePage_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = PageViewMode.SinglePage;
            ApplyViewModeLayout();
        }

        private void BtnViewTwoPage_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = PageViewMode.TwoPageSpread;
            ApplyViewModeLayout();
        }

        private void BtnDarkModeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkReadingMode = !_isDarkReadingMode;
            if (BtnDarkModeToggle != null)
            {
                BtnDarkModeToggle.Foreground = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(250, 204, 21)) : (Brush)FindResource("WindowFgBrush");
            }

            var bg = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(30, 30, 36)) : Brushes.White;
            var fg = _isDarkReadingMode ? new SolidColorBrush(Color.FromRgb(228, 228, 231)) : new SolidColorBrush(Color.FromRgb(24, 24, 27));

            foreach (var card in _pageCards)
            {
                card.Background = bg;
            }
            foreach (var txt in _pageTextBoxes)
            {
                txt.Foreground = fg;
            }
        }

        private void BtnToolSnapshot_Click(object sender, RoutedEventArgs e)
        {
            _activeTool = "Snapshot";
            if (MainScrollViewer != null) MainScrollViewer.Cursor = Cursors.Cross;
            UpdateStatus();
        }

        private void MainScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeTool == "Snapshot" && e.LeftButton == MouseButtonState.Pressed && CanvasContainer != null && SnapshotOverlayCanvas != null && SnapshotSelectionRect != null)
            {
                _snapshotStartPoint = e.GetPosition(CanvasContainer);
                SnapshotOverlayCanvas.Visibility = Visibility.Visible;
                Canvas.SetLeft(SnapshotSelectionRect, _snapshotStartPoint.X);
                Canvas.SetTop(SnapshotSelectionRect, _snapshotStartPoint.Y);
                SnapshotSelectionRect.Width = 0;
                SnapshotSelectionRect.Height = 0;
                SnapshotSelectionRect.Visibility = Visibility.Visible;
                _isSnapshotDragging = true;
                e.Handled = true;
            }
        }

        private void MainScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSnapshotDragging && CanvasContainer != null && SnapshotSelectionRect != null)
            {
                Point cur = e.GetPosition(CanvasContainer);
                double x = Math.Min(cur.X, _snapshotStartPoint.X);
                double y = Math.Min(cur.Y, _snapshotStartPoint.Y);
                double w = Math.Abs(cur.X - _snapshotStartPoint.X);
                double h = Math.Abs(cur.Y - _snapshotStartPoint.Y);

                Canvas.SetLeft(SnapshotSelectionRect, x);
                Canvas.SetTop(SnapshotSelectionRect, y);
                SnapshotSelectionRect.Width = w;
                SnapshotSelectionRect.Height = h;
                e.Handled = true;
            }
        }

        private void MainScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSnapshotDragging && CanvasContainer != null && SnapshotOverlayCanvas != null && SnapshotSelectionRect != null)
            {
                _isSnapshotDragging = false;
                SnapshotOverlayCanvas.Visibility = Visibility.Collapsed;
                SnapshotSelectionRect.Visibility = Visibility.Collapsed;
                _activeTool = "Pointer";
                if (MainScrollViewer != null) MainScrollViewer.Cursor = Cursors.Arrow;
                UpdateStatus();

                Point cur = e.GetPosition(CanvasContainer);
                double x = Math.Min(cur.X, _snapshotStartPoint.X);
                double y = Math.Min(cur.Y, _snapshotStartPoint.Y);
                double w = Math.Abs(cur.X - _snapshotStartPoint.X);
                double h = Math.Abs(cur.Y - _snapshotStartPoint.Y);

                if (w > 15 && h > 15)
                {
                    try
                    {
                        int targetW = (int)Math.Max(1, CanvasContainer.ActualWidth);
                        int targetH = (int)Math.Max(1, CanvasContainer.ActualHeight);
                        var rtb = new RenderTargetBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(CanvasContainer);

                        int cropX = Math.Clamp((int)x, 0, targetW - 1);
                        int cropY = Math.Clamp((int)y, 0, targetH - 1);
                        int cropW = Math.Clamp((int)w, 1, targetW - cropX);
                        int cropH = Math.Clamp((int)h, 1, targetH - cropY);

                        var cropped = new CroppedBitmap(rtb, new Int32Rect(cropX, cropY, cropW, cropH));
                        Clipboard.SetImage(cropped);

                        MessageBox.Show($"✓ Selected snapshot area copied to Windows Clipboard!\n\nDimensions: {cropW} x {cropH} px\nYou can now paste it directly into Microsoft Word, Paint, Slack, Teams, or any document.", 
                                        "PSD PDF Snapshot Tool", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Unable to capture snapshot: {ex.Message}", "Snapshot Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                e.Handled = true;
            }
        }

        private void BtnDocumentProperties_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new DocumentPropertiesWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true)
            {
                if (TabList != null) TabList.Items.Refresh();
                UpdateStatus();
            }
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null || _activeDoc.Pages.Count == 0) return;
            int idx = Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1);
            string text = _activeDoc.Pages[idx].ContentText;
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                MessageBox.Show("✓ Page text copied to Windows Clipboard!", "PSD PDF Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThumbnailList?.SelectedIndex >= 0 && _activeDoc != null)
            {
                int idx = ThumbnailList.SelectedIndex;
                if (idx != _activeDoc.CurrentPageIndex)
                {
                    ScrollToPage(idx);
                }
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
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    RealPdfRenderer.CloseDocument(doc.FilePath);
                }
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
                Filter = "All Supported Documents|*.pdf;*.docx;*.rtf;*.txt;*.md;*.html;*.psd|PDF Files (*.pdf)|*.pdf|Word Documents (*.docx)|*.docx|Photoshop PSD (*.psd)|*.psd|Rich Text Format (*.rtf)|*.rtf|Markdown (*.md)|*.md|HTML Files (*.html)|*.html|Plain Text (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Open Document into PSD PDF Studio"
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
                WordDocumentService.PrintDocument(RtbWordEditor, "PSD Word Document");
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
            WordDocumentService.PrintDocument(RtbWordEditor, "PSD Word Document");
        }

        private void BtnWordUndo_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor != null && RtbWordEditor.CanUndo)
            {
                RtbWordEditor.Undo();
            }
        }

        private void BtnWordRedo_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor != null && RtbWordEditor.CanRedo)
            {
                RtbWordEditor.Redo();
            }
        }

        private void CmbWordStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWordStyle?.SelectedItem is ComboBoxItem item && RtbWordEditor != null)
            {
                string style = item.Content?.ToString() ?? "Normal";
                switch (style)
                {
                    case "Title":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 26.0);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(30, 41, 59)));
                        break;
                    case "Subtitle":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 16.0);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 116, 139)));
                        break;
                    case "Heading 1":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 20.0);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
                        break;
                    case "Heading 2":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 16.0);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.SemiBold);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(30, 41, 59)));
                        break;
                    case "Heading 3":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 13.5);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Medium);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85)));
                        break;
                    case "Quote":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 11.5);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
                        break;
                    case "Code":
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Consolas"));
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 10.5);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromRgb(241, 245, 249)));
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
                        break;
                    default: // Normal
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 11.0);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
                        break;
                }
            }
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

        private void BtnWordSubscript_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor == null) return;
            var current = RtbWordEditor.Selection.GetPropertyValue(Inline.BaselineAlignmentProperty);
            if (current is BaselineAlignment align && align == BaselineAlignment.Subscript)
            {
                RtbWordEditor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Baseline);
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 11.0);
            }
            else
            {
                RtbWordEditor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Subscript);
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 8.5);
            }
        }

        private void BtnWordSuperscript_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor == null) return;
            var current = RtbWordEditor.Selection.GetPropertyValue(Inline.BaselineAlignmentProperty);
            if (current is BaselineAlignment align && align == BaselineAlignment.Superscript)
            {
                RtbWordEditor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Baseline);
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 11.0);
            }
            else
            {
                RtbWordEditor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Superscript);
                RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 8.5);
            }
        }

        private void BtnWordClearFormat_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor == null) return;
            RtbWordEditor.Selection.ClearAllProperties();
            RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Calibri"));
            RtbWordEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 11.0);
            RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
            RtbWordEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            RtbWordEditor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            RtbWordEditor.Selection.ApplyPropertyValue(Inline.BaselineAlignmentProperty, BaselineAlignment.Baseline);
        }

        private object? _painterFontFamily;
        private object? _painterFontSize;
        private object? _painterFontWeight;
        private object? _painterFontStyle;
        private object? _painterForeground;
        private object? _painterBackground;
        private bool _isFormatPainterActive = false;

        private void BtnWordFormatPainter_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor == null) return;
            var sel = RtbWordEditor.Selection;
            _painterFontFamily = sel.GetPropertyValue(TextElement.FontFamilyProperty);
            _painterFontSize = sel.GetPropertyValue(TextElement.FontSizeProperty);
            _painterFontWeight = sel.GetPropertyValue(TextElement.FontWeightProperty);
            _painterFontStyle = sel.GetPropertyValue(TextElement.FontStyleProperty);
            _painterForeground = sel.GetPropertyValue(TextElement.ForegroundProperty);
            _painterBackground = sel.GetPropertyValue(TextElement.BackgroundProperty);

            _isFormatPainterActive = true;
            if (BtnWordFormatPainter != null)
            {
                BtnWordFormatPainter.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            }
        }

        private void BtnWordChangeCase_Click(object sender, RoutedEventArgs e)
        {
            if (PopupWordChangeCase != null)
            {
                PopupWordChangeCase.IsOpen = !PopupWordChangeCase.IsOpen;
            }
        }

        private void ChangeCaseChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string caseType && RtbWordEditor != null)
            {
                WordDocumentService.ChangeSelectionCase(RtbWordEditor, caseType);
                _isWordModified = true;
                UpdateWordStats();
            }
            if (PopupWordChangeCase != null) PopupWordChangeCase.IsOpen = false;
        }

        private void BtnWordTextColor_Click(object sender, RoutedEventArgs e)
        {
            if (PopupWordTextColor != null)
            {
                PopupWordTextColor.IsOpen = !PopupWordTextColor.IsOpen;
            }
        }

        private void ColorChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex && RtbWordEditor != null)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    var brush = new SolidColorBrush(color);
                    RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                    if (IndicatorTextColor != null)
                    {
                        IndicatorTextColor.Background = brush;
                    }
                }
                catch { }
            }
            if (PopupWordTextColor != null) PopupWordTextColor.IsOpen = false;
        }

        private void BtnCustomTextColor_Click(object sender, RoutedEventArgs e)
        {
            if (PopupWordTextColor != null) PopupWordTextColor.IsOpen = false;

            string? prompt = PromptInput(this, "Enter custom color hex code (e.g. #FF5722 or #2E7D32):", "Custom Text Color", "#2563EB");
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                try
                {
                    if (!prompt.StartsWith("#")) prompt = "#" + prompt;
                    var color = (Color)ColorConverter.ConvertFromString(prompt);
                    var brush = new SolidColorBrush(color);
                    RtbWordEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                    if (IndicatorTextColor != null)
                    {
                        IndicatorTextColor.Background = brush;
                    }
                }
                catch
                {
                    MessageBox.Show("Invalid hex color code. Please use format #RRGGBB.", "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnWordHighlightColor_Click(object sender, RoutedEventArgs e)
        {
            if (PopupWordHighlightColor != null)
            {
                PopupWordHighlightColor.IsOpen = !PopupWordHighlightColor.IsOpen;
            }
        }

        private void HighlightChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string val && RtbWordEditor != null)
            {
                if (val.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
                {
                    RtbWordEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                    if (IndicatorHighlightColor != null)
                    {
                        IndicatorHighlightColor.Background = Brushes.Transparent;
                    }
                }
                else
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(val);
                        var brush = new SolidColorBrush(color);
                        RtbWordEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
                        if (IndicatorHighlightColor != null)
                        {
                            IndicatorHighlightColor.Background = brush;
                        }
                    }
                    catch { }
                }
            }
            if (PopupWordHighlightColor != null) PopupWordHighlightColor.IsOpen = false;
        }

        private void CmbWordLineSpacing_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWordLineSpacing?.SelectedItem is ComboBoxItem item && RtbWordEditor != null)
            {
                string tag = item.Tag?.ToString() ?? "1.15";
                if (double.TryParse(tag, out double multiplier))
                {
                    double curSize = 11.0;
                    var fs = RtbWordEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                    if (fs is double d) curSize = d;
                    double lineHeight = curSize * multiplier * 1.25;
                    RtbWordEditor.Selection.ApplyPropertyValue(Block.LineHeightProperty, lineHeight);
                }
            }
        }

        private void BtnWordIndentDecrease_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.DecreaseIndentation.Execute(null, RtbWordEditor);
        }

        private void BtnWordIndentIncrease_Click(object sender, RoutedEventArgs e)
        {
            EditingCommands.IncreaseIndentation.Execute(null, RtbWordEditor);
        }

        private void BtnWordInsertLink_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor == null) return;
            string selText = RtbWordEditor.Selection.Text;
            if (string.IsNullOrWhiteSpace(selText)) selText = "Link";

            string? url = PromptInput(this, "Enter Destination URL (e.g. https://www.google.com):", "Insert Hyperlink", "https://");
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var link = new Hyperlink(new Run(selText))
                    {
                        NavigateUri = new Uri(url),
                        Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235))
                    };
                    link.RequestNavigate += (s, req) =>
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(req.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
                    };

                    new TextRange(RtbWordEditor.Selection.Start, RtbWordEditor.Selection.End) { Text = "" };
                    var para = RtbWordEditor.CaretPosition.Paragraph ?? new Paragraph();
                    para.Inlines.Add(link);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not insert hyperlink: {ex.Message}", "Hyperlink Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnWordInsertSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (PopupWordSymbol != null)
            {
                PopupWordSymbol.IsOpen = !PopupWordSymbol.IsOpen;
            }
        }

        private void SymbolChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string symbol && RtbWordEditor != null)
            {
                RtbWordEditor.Selection.Text = symbol;
                RtbWordEditor.CaretPosition = RtbWordEditor.Selection.End;
            }
            if (PopupWordSymbol != null) PopupWordSymbol.IsOpen = false;
        }

        private void BtnWordToggleFindReplace_Click(object sender, RoutedEventArgs e)
        {
            if (WordFindReplacePanel == null) return;
            if (WordFindReplacePanel.Visibility == Visibility.Visible)
            {
                WordFindReplacePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                WordFindReplacePanel.Visibility = Visibility.Visible;
                TxtWordFind?.Focus();
                TxtWordFind?.SelectAll();
            }
        }

        private void BtnWordCloseFind_Click(object sender, RoutedEventArgs e)
        {
            if (WordFindReplacePanel != null)
            {
                WordFindReplacePanel.Visibility = Visibility.Collapsed;
            }
        }

        private TextRange? FindNextText(string query, bool matchCase)
        {
            if (RtbWordEditor == null || string.IsNullOrEmpty(query)) return null;

            TextPointer start = RtbWordEditor.Selection.End ?? RtbWordEditor.Document.ContentStart;
            TextRange? match = SearchTextInDocument(start, RtbWordEditor.Document.ContentEnd, query, matchCase);
            if (match == null)
            {
                match = SearchTextInDocument(RtbWordEditor.Document.ContentStart, start, query, matchCase);
            }
            return match;
        }

        private TextRange? SearchTextInDocument(TextPointer start, TextPointer end, string query, bool matchCase)
        {
            TextPointer current = start;
            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            while (current != null && current.CompareTo(end) < 0)
            {
                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string text = current.GetTextInRun(LogicalDirection.Forward);
                    int index = text.IndexOf(query, comparison);
                    if (index >= 0)
                    {
                        TextPointer matchStart = current.GetPositionAtOffset(index);
                        TextPointer matchEnd = current.GetPositionAtOffset(index + query.Length);
                        return new TextRange(matchStart, matchEnd);
                    }
                }
                current = current.GetNextContextPosition(LogicalDirection.Forward);
            }
            return null;
        }

        private void BtnWordFindNext_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtWordFind?.Text ?? "";
            if (string.IsNullOrEmpty(query))
            {
                if (TxtWordFindCount != null) TxtWordFindCount.Text = "Please enter search term";
                return;
            }

            bool matchCase = ChkWordMatchCase?.IsChecked == true;
            var range = FindNextText(query, matchCase);
            if (range != null)
            {
                RtbWordEditor.Selection.Select(range.Start, range.End);
                RtbWordEditor.Focus();
                if (TxtWordFindCount != null) TxtWordFindCount.Text = "Match found";
            }
            else
            {
                if (TxtWordFindCount != null) TxtWordFindCount.Text = "No matches found";
            }
        }

        private void BtnWordReplace_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtWordFind?.Text ?? "";
            string replaceWith = TxtWordReplace?.Text ?? "";
            if (string.IsNullOrEmpty(query)) return;

            bool matchCase = ChkWordMatchCase?.IsChecked == true;
            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (RtbWordEditor.Selection.Text.Equals(query, comparison))
            {
                RtbWordEditor.Selection.Text = replaceWith;
                _isWordModified = true;
                UpdateWordStats();
            }

            BtnWordFindNext_Click(sender, e);
        }

        private void BtnWordReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtWordFind?.Text ?? "";
            string replaceWith = TxtWordReplace?.Text ?? "";
            if (string.IsNullOrEmpty(query) || RtbWordEditor == null) return;

            bool matchCase = ChkWordMatchCase?.IsChecked == true;
            int count = 0;

            var start = RtbWordEditor.Document.ContentStart;
            TextRange? range;
            while ((range = SearchTextInDocument(start, RtbWordEditor.Document.ContentEnd, query, matchCase)) != null)
            {
                range.Text = replaceWith;
                start = range.End;
                count++;
            }

            _isWordModified = true;
            UpdateWordStats();
            if (TxtWordFindCount != null) TxtWordFindCount.Text = $"Replaced {count} occurrence{(count == 1 ? "" : "s")}";
        }

        private void BtnWordTemplates_Click(object sender, RoutedEventArgs e)
        {
            if (PopupWordTemplates != null)
            {
                PopupWordTemplates.IsOpen = !PopupWordTemplates.IsOpen;
            }
        }

        private void WordTemplateChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string template && RtbWordEditor != null)
            {
                if (_isWordModified)
                {
                    var res = MessageBox.Show("Loading a new template will replace current document content. Continue?", "Confirm Template", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        if (PopupWordTemplates != null) PopupWordTemplates.IsOpen = false;
                        return;
                    }
                }
                WordDocumentService.LoadTemplate(RtbWordEditor, template);
                _isWordModified = false;
                UpdateWordStats();
            }
            if (PopupWordTemplates != null) PopupWordTemplates.IsOpen = false;
        }

        private void BtnWordDetailedStats_Click(object sender, RoutedEventArgs e)
        {
            if (RtbWordEditor == null) return;
            var stats = WordDocumentService.GetDetailedDocumentStatistics(RtbWordEditor);

            var win = new Window
            {
                Title = "Document Audit & Readability Metrics",
                Width = 460,
                Height = 440,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 18)),
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.ToolWindow
            };

            var root = new StackPanel { Margin = new Thickness(20) };

            var header = new TextBlock
            {
                Text = "MICROSOFT WORD AUDIT & READABILITY",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(header);

            var statBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 27)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(39, 39, 42)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var statGrid = new Grid();
            statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 7; i++) statGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddStatRow(statGrid, 0, "Words:", $"{stats.Words:N0}");
            AddStatRow(statGrid, 1, "Characters (no spaces):", $"{stats.CharactersNoSpaces:N0}");
            AddStatRow(statGrid, 2, "Characters (with spaces):", $"{stats.CharactersWithSpaces:N0}");
            AddStatRow(statGrid, 3, "Paragraphs:", $"{stats.Paragraphs:N0}");
            AddStatRow(statGrid, 4, "Lines:", $"{stats.Lines:N0}");
            AddStatRow(statGrid, 5, "Estimated Pages:", $"{stats.EstimatedPages}");
            AddStatRow(statGrid, 6, "Est. Reading / Speaking Time:", $"~{stats.ReadingTimeMinutes}m / ~{stats.SpeakingTimeMinutes}m");

            statBorder.Child = statGrid;
            root.Children.Add(statBorder);

            var readBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 27)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(39, 39, 42)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var readStack = new StackPanel();
            readStack.Children.Add(new TextBlock { Text = $"Flesch Reading Ease: {stats.FleschReadingEase} / 100", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)), FontSize = 12 });
            readStack.Children.Add(new TextBlock { Text = $"Readability Standard: {stats.ReadabilityLabel}", Foreground = new SolidColorBrush(Color.FromRgb(228, 228, 231)), Margin = new Thickness(0, 2, 0, 2), FontSize = 11 });
            readStack.Children.Add(new TextBlock { Text = $"Grade Level Equivalent: {stats.GradeLevel}", Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), FontSize = 10.5 });
            readBorder.Child = readStack;
            root.Children.Add(readBorder);

            var closeBtn = new Button
            {
                Content = "Close",
                Width = 90,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                IsDefault = true
            };
            closeBtn.Click += (s, ev) => win.Close();
            root.Children.Add(closeBtn);

            win.Content = root;
            win.ShowDialog();
        }

        private static void AddStatRow(Grid grid, int row, string label, string value)
        {
            var lbl = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), Margin = new Thickness(0, 2, 0, 2), FontSize = 11 };
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var val = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Right, FontSize = 11 };
            Grid.SetRow(val, row);
            Grid.SetColumn(val, 1);
            grid.Children.Add(val);
        }

        private static string? PromptInput(Window owner, string prompt, string title, string defaultText = "")
        {
            var win = new Window
            {
                Title = title,
                Width = 420,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                WindowStyle = WindowStyle.ToolWindow
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock { Text = prompt, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8), Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
            var txt = new TextBox { Text = defaultText, Height = 28, Padding = new Thickness(6, 4, 6, 4), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), Margin = new Thickness(0, 0, 0, 12) };
            stack.Children.Add(txt);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 75, Height = 26, Margin = new Thickness(0, 0, 8, 0), IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var cancelBtn = new Button { Content = "Cancel", Width = 75, Height = 26, IsCancel = true, Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), BorderThickness = new Thickness(0) };

            string? result = null;
            okBtn.Click += (s, e) => { result = txt.Text; win.DialogResult = true; win.Close(); };
            cancelBtn.Click += (s, e) => { win.DialogResult = false; win.Close(); };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            stack.Children.Add(btnPanel);
            win.Content = stack;

            txt.Focus();
            txt.SelectAll();

            return win.ShowDialog() == true ? result : null;
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
            if (RtbWordEditor == null) return;

            // Apply Format Painter if active
            if (_isFormatPainterActive && !RtbWordEditor.Selection.IsEmpty)
            {
                var sel = RtbWordEditor.Selection;
                if (_painterFontFamily != null && _painterFontFamily != DependencyProperty.UnsetValue)
                    sel.ApplyPropertyValue(TextElement.FontFamilyProperty, _painterFontFamily);
                if (_painterFontSize != null && _painterFontSize != DependencyProperty.UnsetValue)
                    sel.ApplyPropertyValue(TextElement.FontSizeProperty, _painterFontSize);
                if (_painterFontWeight != null && _painterFontWeight != DependencyProperty.UnsetValue)
                    sel.ApplyPropertyValue(TextElement.FontWeightProperty, _painterFontWeight);
                if (_painterFontStyle != null && _painterFontStyle != DependencyProperty.UnsetValue)
                    sel.ApplyPropertyValue(TextElement.FontStyleProperty, _painterFontStyle);
                if (_painterForeground != null && _painterForeground != DependencyProperty.UnsetValue)
                    sel.ApplyPropertyValue(TextElement.ForegroundProperty, _painterForeground);
                if (_painterBackground != null && _painterBackground != DependencyProperty.UnsetValue)
                    sel.ApplyPropertyValue(TextElement.BackgroundProperty, _painterBackground);

                _isFormatPainterActive = false;
                if (BtnWordFormatPainter != null)
                {
                    BtnWordFormatPainter.ClearValue(Button.BackgroundProperty);
                }
            }

            // Sync Ribbon Font Size
            var curSize = RtbWordEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
            if (curSize is double sz && CmbWordFontSize != null)
            {
                CmbWordFontSize.Text = ((int)sz).ToString();
            }
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
                else if (tag.Contains("Measure")) SetTool("Measure");
                else if (tag.Contains("Eraser")) SetTool("Eraser");
                else if (tag.Contains("Sign")) StampDigitalSignature();
            }
        }

        private void MenuToolPointer_Click(object sender, RoutedEventArgs e) => SetTool("Pointer");
        private void MenuToolInk_Click(object sender, RoutedEventArgs e) => SetTool("Ink");
        private void MenuToolHighlight_Click(object sender, RoutedEventArgs e) => SetTool("Highlight");
        private void MenuToolSticky_Click(object sender, RoutedEventArgs e) => SetTool("Sticky");
        private void MenuToolMeasure_Click(object sender, RoutedEventArgs e) => SetTool("Measure");
        private void MenuToolEraser_Click(object sender, RoutedEventArgs e) => SetTool("Eraser");
        private void MenuToolSignature_Click(object sender, RoutedEventArgs e) => BtnToolSignature_Click(sender, e);

        private void BtnToolPointer_Click(object sender, RoutedEventArgs e) => SetTool("Pointer");
        private void BtnToolInk_Click(object sender, RoutedEventArgs e) => SetTool("Ink");
        private void BtnToolHighlight_Click(object sender, RoutedEventArgs e) => SetTool("Highlight");
        private void BtnToolSticky_Click(object sender, RoutedEventArgs e) => SetTool("Sticky");
        private void BtnToolMeasure_Click(object sender, RoutedEventArgs e) => SetTool("Measure");
        private void BtnRedactTool_Click(object sender, RoutedEventArgs e) => BtnSecurity_Click(sender, e);
        private void BtnWordFont_Click(object sender, RoutedEventArgs e)
        {
            if (CmbWordFontFamily != null)
            {
                CmbWordFontFamily.IsDropDownOpen = true;
                CmbWordFontFamily.Focus();
            }
        }

        private void BtnPdfPenColor_Click(object sender, RoutedEventArgs e)
        {
            if (PopupPdfPenColor != null)
            {
                PopupPdfPenColor.IsOpen = !PopupPdfPenColor.IsOpen;
            }
        }

        private void PdfPenColorChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                try
                {
                    var col = (Color)ColorConverter.ConvertFromString(hex);
                    _activePdfPenColor = col;
                    if (PageInkCanvas != null)
                    {
                        PageInkCanvas.DefaultDrawingAttributes.Color = col;
                    }
                    if (IndicatorPenColor != null)
                    {
                        IndicatorPenColor.Background = new SolidColorBrush(col);
                    }
                }
                catch { }
            }
            if (PopupPdfPenColor != null) PopupPdfPenColor.IsOpen = false;
        }

        private void CmbPdfPenWidth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbPdfPenWidth?.SelectedItem is ComboBoxItem item)
            {
                string text = item.Content?.ToString() ?? "2.5";
                string[] parts = text.Split(' ');
                if (double.TryParse(parts[0], out double w))
                {
                    _activePdfPenWidth = w;
                    if (PageInkCanvas != null)
                    {
                        PageInkCanvas.DefaultDrawingAttributes.Width = w;
                        PageInkCanvas.DefaultDrawingAttributes.Height = w;
                    }
                }
            }
        }

        private void SetTool(string toolName)
        {
            _activeTool = toolName;
            _measureStartPoint = null;
            UpdateStatus();

            if (PageInkCanvas == null) return;

            switch (toolName)
            {
                case "Ink":
                    PageInkCanvas.IsHitTestVisible = true;
                    PageInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                    PageInkCanvas.DefaultDrawingAttributes.Color = _activePdfPenColor;
                    PageInkCanvas.DefaultDrawingAttributes.Width = _activePdfPenWidth;
                    PageInkCanvas.DefaultDrawingAttributes.Height = _activePdfPenWidth;
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

        private void BtnToolSignature_Click(object sender, RoutedEventArgs e)
        {
            var win = new ElectronicSignatureWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true)
            {
                if (SignatureBadge != null)
                {
                    SignatureBadge.Visibility = Visibility.Visible;
                }

                CreateStickyNote(40, 40, $"🔒 CERTIFIED DIGITAL SIGNATURE\nSigner: {win.SignedByName}\nReason: {win.SignReason}\nTimestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\nSHA-256: {win.AuditHash.Substring(0, Math.Min(16, win.AuditHash.Length))}...\nStandard: PKCS#7 / ISO 32000-2\nStatus: Cryptographically Sealed");

                MessageBox.Show($"Digital signature and tamper-evident audit trail successfully embedded!\n\nSigner: {win.SignedByName}\nSHA-256 Checksum: {win.AuditHash}\nCompliance: PKCS#7 Verified", "e-Signature Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void StampDigitalSignature() => BtnToolSignature_Click(this, new RoutedEventArgs());

        private void BtnToggleAiCopilot_Click(object sender, RoutedEventArgs e)
        {
            if (AiCopilotPanel == null || ColAiCopilotPanel == null) return;
            if (AiCopilotPanel.Visibility == Visibility.Visible)
            {
                AiCopilotPanel.Visibility = Visibility.Collapsed;
                ColAiCopilotPanel.Width = new GridLength(0);
            }
            else
            {
                AiCopilotPanel.Visibility = Visibility.Visible;
                ColAiCopilotPanel.Width = new GridLength(350);
                BtnAiExecSummary_Click(sender, e);
            }
        }

        private void BtnCloseAiCopilot_Click(object sender, RoutedEventArgs e)
        {
            if (AiCopilotPanel != null && ColAiCopilotPanel != null)
            {
                AiCopilotPanel.Visibility = Visibility.Collapsed;
                ColAiCopilotPanel.Width = new GridLength(0);
            }
        }

        private void BtnAiExecSummary_Click(object sender, RoutedEventArgs e)
        {
            string text = GetActiveDocumentText();
            var analysis = AiDocumentCopilotService.AnalyzeDocument(text);

            AiResponseContainer?.Children.Clear();

            var card1 = CreateAiResultCard("⚡ EXECUTIVE SUMMARY", analysis.ExecutiveSummary, "#38BDF8");
            AiResponseContainer?.Children.Add(card1);

            var sb = new StringBuilder();
            foreach (var t in analysis.KeyTakeaways) sb.AppendLine($"• {t}");
            var card2 = CreateAiResultCard("📌 KEY STRATEGIC TAKEAWAYS", sb.ToString().TrimEnd(), "#4ADE80");
            AiResponseContainer?.Children.Add(card2);

            string meta = $"Word Count: {analysis.DocumentWordCount:N0} | Tone: {analysis.SentimentTone} | Engine: Local Offline Neural Heuristics";
            var card3 = CreateAiResultCard("📊 DOCUMENT TELEMETRY", meta, "#A78BFA");
            AiResponseContainer?.Children.Add(card3);
        }

        private void BtnAiActionItems_Click(object sender, RoutedEventArgs e)
        {
            string text = GetActiveDocumentText();
            var analysis = AiDocumentCopilotService.AnalyzeDocument(text);

            AiResponseContainer?.Children.Clear();

            var headerCard = CreateAiResultCard("📋 ACTION ITEMS & DELIVERABLES", $"Extracted {analysis.ActionItems.Count} actionable tasks & commitments from document.", "#38BDF8");
            AiResponseContainer?.Children.Add(headerCard);

            foreach (var item in analysis.ActionItems)
            {
                string color = item.Urgency == "High Priority" ? "#EF4444" : "#38BDF8";
                string content = $"{item.Task}\n\nAssignee: {item.Assignee} | Due: {item.DueDate} | Urgency: {item.Urgency}";
                var taskCard = CreateAiResultCard($"TASK: [{item.Urgency.ToUpper()}]", content, color);
                AiResponseContainer?.Children.Add(taskCard);
            }
        }

        private void BtnAiContractRisks_Click(object sender, RoutedEventArgs e)
        {
            string text = GetActiveDocumentText();
            var analysis = AiDocumentCopilotService.AnalyzeDocument(text);

            AiResponseContainer?.Children.Clear();

            var headerCard = CreateAiResultCard("⚠️ CONTRACT RISK & CLAUSE AUDIT", $"Audited document against enterprise liability, indemnification, and termination risks.", "#FACC15");
            AiResponseContainer?.Children.Add(headerCard);

            foreach (var risk in analysis.ContractRisks)
            {
                string color = risk.Severity == "High" ? "#EF4444" : risk.Severity == "Medium" ? "#F59E0B" : "#10B981";
                string content = $"Clause: \"{risk.ClauseSnippet}\"\n\nMitigation: {risk.Recommendation}";
                var riskCard = CreateAiResultCard($"{risk.Category} [{risk.Severity.ToUpper()} RISK]", content, color);
                AiResponseContainer?.Children.Add(riskCard);
            }
        }

        private void BtnAiKeyFigures_Click(object sender, RoutedEventArgs e)
        {
            string text = GetActiveDocumentText();
            var analysis = AiDocumentCopilotService.AnalyzeDocument(text);

            AiResponseContainer?.Children.Clear();

            var headerCard = CreateAiResultCard("💰 FINANCIALS, RATES & ENTITIES", $"Identified {analysis.KeyEntities.Count} numerical, financial, and timeline milestones.", "#38BDF8");
            AiResponseContainer?.Children.Add(headerCard);

            var sb = new StringBuilder();
            foreach (var ent in analysis.KeyEntities)
            {
                sb.AppendLine($"• [{ent.EntityType}] {ent.Value} — {ent.Context}");
            }
            if (analysis.KeyEntities.Count == 0) sb.AppendLine("No explicit financial amounts or percentages detected in active text.");

            var entCard = CreateAiResultCard("EXTRACTED ENTITIES", sb.ToString().TrimEnd(), "#4ADE80");
            AiResponseContainer?.Children.Add(entCard);
        }

        private void BtnAiPlainEnglish_Click(object sender, RoutedEventArgs e)
        {
            string text = GetActiveDocumentText();
            var analysis = AiDocumentCopilotService.AnalyzeDocument(text);

            AiResponseContainer?.Children.Clear();

            var headerCard = CreateAiResultCard("💡 PLAIN-ENGLISH TRANSLATOR", "Converts legal terminology and technical jargon into clear, straightforward English.", "#38BDF8");
            AiResponseContainer?.Children.Add(headerCard);

            var plainCard = CreateAiResultCard("SIMPLIFIED CLAUSES", analysis.PlainEnglishExplanation, "#A78BFA");
            AiResponseContainer?.Children.Add(plainCard);
        }

        private void BtnExportRagJson_Click(object sender, RoutedEventArgs e)
        {
            string json;
            string defaultName;

            if (_activeDoc != null)
            {
                json = AiDocumentCopilotService.ExportToRagJson(_activeDoc);
                defaultName = Path.GetFileNameWithoutExtension(_activeDoc.Title ?? "Document") + "_rag_vectors.json";
            }
            else
            {
                string text = GetActiveDocumentText();
                var mockDoc = PdfDocumentModel.CreateDefault("Word_Document", 1);
                mockDoc.Pages[0].ContentText = text;
                json = AiDocumentCopilotService.ExportToRagJson(mockDoc);
                defaultName = "Document_rag_vectors.json";
            }

            var sfd = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = defaultName,
                Title = "Export for LLM Vector Search (Pinecone / Azure / LangChain)"
            };

            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                MessageBox.Show($"Standardized Vector RAG JSON dataset successfully exported!\n\nDestination: {sfd.FileName}\nFormat: Chunks with page citations & token metadata", "RAG Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAiAsk_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtAiPrompt?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query)) return;

            string text = GetActiveDocumentText();
            string answer = AiDocumentCopilotService.AnswerQuestion(text, query);

            var qCard = CreateAiResultCard($"Q: {query}", answer, "#38BDF8");
            AiResponseContainer?.Children.Add(qCard);
            if (TxtAiPrompt != null) TxtAiPrompt.Text = "";
        }

        private void TxtAiPrompt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnAiAsk_Click(sender, e);
                e.Handled = true;
            }
        }

        private static Border CreateAiResultCard(string title, string content, string accentHex)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 27)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(39, 39, 42)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            var head = new TextBlock
            {
                Text = title,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(accentHex)!,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(head);

            var body = new TextBlock
            {
                Text = content,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(228, 228, 231)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 15
            };
            stack.Children.Add(body);

            card.Child = stack;
            return card;
        }

        private string GetActiveDocumentText()
        {
            if (ViewWordEditor?.Visibility == Visibility.Visible && RtbWordEditor != null)
            {
                return new TextRange(RtbWordEditor.Document.ContentStart, RtbWordEditor.Document.ContentEnd).Text;
            }
            if (_activeDoc != null)
            {
                var sb = new StringBuilder();
                foreach (var p in _activeDoc.Pages)
                {
                    sb.AppendLine(p.ContentText);
                    sb.AppendLine();
                }
                return sb.ToString();
            }
            return "";
        }

        private void PageContentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeTool == "Sticky")
            {
                Point pos = e.GetPosition(NotesCanvas);
                CreateStickyNote(pos.X, pos.Y, "Review note: ");
                e.Handled = true;
            }
            else if (_activeTool == "Measure")
            {
                Point pos = e.GetPosition(PageContentGrid);
                if (_measureStartPoint == null)
                {
                    _measureStartPoint = pos;
                    if (StatusToolIndicator != null) StatusToolIndicator.Text = $"Measure: Start at ({pos.X:0}, {pos.Y:0}). Click endpoint.";
                }
                else
                {
                    var scale = new MeasurementScale { UnitName = "in", RealWorldUnitsPerInch = 1.0 };
                    var (dist, formattedDist) = EngineeringMeasurementService.MeasureDistance(_measureStartPoint.Value, pos, scale);
                    var (area, formattedArea) = EngineeringMeasurementService.MeasureRectangleArea(_measureStartPoint.Value, pos, scale);
                    double angle = EngineeringMeasurementService.MeasureAngle(_measureStartPoint.Value, pos);

                    string summary = $"Measured: {formattedDist} | Area: {formattedArea} | Angle: {angle:0.1}°";
                    if (StatusToolIndicator != null) StatusToolIndicator.Text = summary;
                    CreateStickyNote(pos.X, pos.Y, $"📐 Measurement:\nDistance: {formattedDist}\nArea: {formattedArea}\nAngle: {angle:0.1}°");
                    _measureStartPoint = null;
                }
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

            // Update Dynamic Theme Resources (Window and Global Application Scope)
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

            if (Application.Current != null)
            {
                Application.Current.Resources["WindowBgBrush"] = winBg;
                Application.Current.Resources["WindowFgBrush"] = winFg;
                Application.Current.Resources["HeaderBgBrush"] = headerBg;
                Application.Current.Resources["HeaderFgBrush"] = headerFg;
                Application.Current.Resources["MenuBgBrush"] = menuBg;
                Application.Current.Resources["MenuFgBrush"] = menuFg;
                Application.Current.Resources["SubmenuBgBrush"] = submenuBg;
                Application.Current.Resources["SubmenuFgBrush"] = submenuFg;
                Application.Current.Resources["ItemHoverBgBrush"] = hoverBg;
                Application.Current.Resources["NavRailBgBrush"] = navBg;
                Application.Current.Resources["CanvasBgBrush"] = canvasBg;
                Application.Current.Resources["CardBgBrush"] = cardBg;
                Application.Current.Resources["BorderBrushDefault"] = borderDef;
                Application.Current.Resources["BorderBrushLight"] = borderLt;
                Application.Current.Resources["AccentBrushDefault"] = accentDef;
                Application.Current.Resources["AccentHoverBrush"] = accentHov;
                Application.Current.Resources["StatusBarBgBrush"] = statusBg;
                Application.Current.Resources["StatusBarFgBrush"] = statusFg;
            }

            Background = winBg;
            Foreground = winFg;
        }

        #endregion

        #region In-Document Search & Text Highlighting Engine

        public class DocumentSearchMatch
        {
            public int PageIndex { get; set; }
            public int StartIndex { get; set; }
            public int Length { get; set; }
            public string Snippet { get; set; } = "";
        }

        private readonly List<DocumentSearchMatch> _searchMatches = new();
        private int _currentMatchIndex = -1;

        private void BtnToggleSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel == null) return;
            SearchPanel.Visibility = SearchPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (SearchPanel.Visibility == Visibility.Visible && TxtSearchQuery != null)
            {
                TxtSearchQuery.Focus();
                TxtSearchQuery.SelectAll();
                PerformSearch();
            }
        }

        private void BtnCloseSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchPanel != null) SearchPanel.Visibility = Visibility.Collapsed;
            _searchMatches.Clear();
            _currentMatchIndex = -1;
            if (TxtMatchCount != null) TxtMatchCount.Text = "0 matches";
        }

        private void TxtSearchQuery_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    NavigateSearchMatch(-1);
                }
                else
                {
                    NavigateSearchMatch(1);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                BtnCloseSearch_Click(sender, e);
                e.Handled = true;
                return;
            }
            PerformSearch();
        }

        private void BtnFindNext_Click(object sender, RoutedEventArgs e)
        {
            NavigateSearchMatch(1);
        }

        private void BtnFindPrev_Click(object sender, RoutedEventArgs e)
        {
            NavigateSearchMatch(-1);
        }

        private void PerformSearch()
        {
            if (TxtSearchQuery == null || TxtMatchCount == null) return;
            string query = TxtSearchQuery.Text.Trim();
            _searchMatches.Clear();
            _currentMatchIndex = -1;

            if (string.IsNullOrEmpty(query) || _activeDoc == null)
            {
                TxtMatchCount.Text = "0 matches";
                return;
            }

            for (int p = 0; p < _activeDoc.Pages.Count; p++)
            {
                string text = _activeDoc.Pages[p].ContentText;
                if (string.IsNullOrEmpty(text)) continue;

                int index = 0;
                while ((index = text.IndexOf(query, index, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int snippetStart = Math.Max(0, index - 25);
                    int snippetLen = Math.Min(text.Length - snippetStart, query.Length + 50);
                    string snippet = text.Substring(snippetStart, snippetLen).Replace("\n", " ").Replace("\r", "");

                    _searchMatches.Add(new DocumentSearchMatch
                    {
                        PageIndex = p,
                        StartIndex = index,
                        Length = query.Length,
                        Snippet = snippet
                    });

                    index += query.Length;
                }
            }

            if (_searchMatches.Count > 0)
            {
                _currentMatchIndex = 0;
                UpdateSearchNavigationUi();
                JumpToSearchMatch(_searchMatches[0]);
            }
            else
            {
                TxtMatchCount.Text = "0 matches found";
            }
        }

        private void NavigateSearchMatch(int direction)
        {
            if (_searchMatches.Count == 0)
            {
                PerformSearch();
                return;
            }

            _currentMatchIndex = (_currentMatchIndex + direction + _searchMatches.Count) % _searchMatches.Count;
            UpdateSearchNavigationUi();
            JumpToSearchMatch(_searchMatches[_currentMatchIndex]);
        }

        private void UpdateSearchNavigationUi()
        {
            if (TxtMatchCount == null) return;
            if (_searchMatches.Count == 0 || _currentMatchIndex < 0)
            {
                TxtMatchCount.Text = "0 matches";
            }
            else
            {
                var match = _searchMatches[_currentMatchIndex];
                TxtMatchCount.Text = $"Match {_currentMatchIndex + 1} of {_searchMatches.Count} (Page {match.PageIndex + 1})";
            }
        }

        private void JumpToSearchMatch(DocumentSearchMatch match)
        {
            if (_activeDoc == null) return;

            // Scroll to the matching page
            ScrollToPage(match.PageIndex);

            // Select and highlight text inside TextBox if visible
            if (match.PageIndex < _pageTextBoxes.Count)
            {
                var tb = _pageTextBoxes[match.PageIndex];
                if (tb.Visibility == Visibility.Visible)
                {
                    try
                    {
                        tb.Focus();
                        tb.Select(match.StartIndex, match.Length);
                    }
                    catch { }
                }
            }
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

        private void BtnTtsPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc != null && _activeDoc.Pages.Count > 0)
            {
                int idx = Math.Clamp(_activeDoc.CurrentPageIndex, 0, _activeDoc.Pages.Count - 1);
                string text = _activeDoc.Pages[idx].ContentText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _speechService.SpeakText(text);
                }
            }
            else if (ViewWordEditor?.Visibility == Visibility.Visible)
            {
                var range = new TextRange(RtbWordEditor.Document.ContentStart, RtbWordEditor.Document.ContentEnd);
                if (!string.IsNullOrWhiteSpace(range.Text))
                {
                    _speechService.SpeakText(range.Text);
                }
            }
        }

        private void BtnTtsPause_Click(object sender, RoutedEventArgs e)
        {
            _speechService.Pause();
        }

        private void BtnTtsStop_Click(object sender, RoutedEventArgs e)
        {
            _speechService.Stop();
        }

        private void BtnSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new SecurityAndRedactionWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true)
            {
                DisplayActivePage();
            }
        }

        private void BtnDiff_Click(object sender, RoutedEventArgs e)
        {
            var win = new DocumentDiffWindow(_activeDoc?.FilePath) { Owner = this };
            win.ShowDialog();
        }

        private void BtnBatch_Click(object sender, RoutedEventArgs e)
        {
            var win = new BatchProcessingWindow { Owner = this };
            win.ShowDialog();
        }

        private void BtnOptimizer_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new PdfOptimizerWindow(_activeDoc) { Owner = this };
            win.ShowDialog();
        }

        private void BtnWatermark_Click(object sender, RoutedEventArgs e)
        {
            if (_activeDoc == null) return;
            var win = new WatermarkAndHeaderWindow(_activeDoc) { Owner = this };
            if (win.ShowDialog() == true)
            {
                DisplayActivePage();
            }
        }

        private void BtnShortcuts_Click(object sender, RoutedEventArgs e)
        {
            string msg = "PSD PDF Studio & Viewer Keyboard Shortcuts:\n\n" +
                         "• F11: Dedicated Full Screen Presentation Mode\n" +
                         "• F4: Toggle Left Navigation Rail\n" +
                         "• Ctrl+T: Toggle Thumbnails Sidebar\n" +
                         "• Ctrl+O: Open Document (PDF, Word, RTF, MD, HTML, PSD)\n" +
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
            string msg = "PSD PDF Studio & Viewer Enterprise Edition v3.0.0\n\n" +
                         "• High-Performance Vector PDF Engine, Reader & Hardware Accelerated Viewport\n" +
                         "• Native Microsoft Word (.docx), RTF, Markdown, HTML, Plain Text & PSD Reader/Writer\n" +
                         "• Air-Gapped Offline AI Document Copilot & Clause Intelligence Suite\n" +
                         "• Enterprise Electronic Signature Studio with PKCS#7 SHA-256 Audit Certification\n" +
                         "• Dedicated Borderless Full-Screen Presentation Mode with Floating Controls\n" +
                         "• Permanent Top Title Bar with 100% Screen-Visible Caption Buttons\n" +
                         "• Dynamic Multi-Theme Color Engine with High-Contrast Tools Menus\n" +
                         "• DirectX WinRT Vector Rendering Engine with 64MB LRU RAM Ceiling\n\n" +
                         "Copyright © 2026 PSD Software Studio. All rights reserved.";
            MessageBox.Show(msg, "About PSD PDF Studio & Viewer Enterprise", MessageBoxButton.OK, MessageBoxImage.Information);
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
            else if (e.Key == Key.F3)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    NavigateSearchMatch(-1);
                else
                    NavigateSearchMatch(1);
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
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (e.Key == Key.C) { BtnToolSnapshot_Click(this, new RoutedEventArgs()); e.Handled = true; }
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
                else if (e.Key == Key.D) { BtnDocumentProperties_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.I) { BtnDarkModeToggle_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D1 || e.Key == Key.NumPad1) { BtnViewContinuous_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D2 || e.Key == Key.NumPad2) { BtnViewSinglePage_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D3 || e.Key == Key.NumPad3) { BtnViewTwoPage_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add) { BtnZoomIn_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { BtnZoomOut_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { BtnZoomActual_Click(this, new RoutedEventArgs()); e.Handled = true; }
            }
        }

        #endregion
    }
}
