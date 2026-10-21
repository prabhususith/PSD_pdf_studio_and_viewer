using System;
using System.Windows;
using System.Windows.Controls;

namespace OmniPdf.App.Views
{
    public class AppSettingsState
    {
        public bool HoverPreviewEnabled { get; set; } = true;
        public int HoverPreviewDelayMs { get; set; } = 300;
        public int DefaultDashboardView { get; set; } = 0; // 0 = Grid, 1 = Thumbnails, 2 = Compact
        public bool TouchpadPinchZoomEnabled { get; set; } = true;
        public bool HorizontalScrollEnabled { get; set; } = true;
        public double ZoomSensitivityStep { get; set; } = 0.08;
        public bool AutoScanLibrary { get; set; } = true;
        public string DefaultWatermark { get; set; } = "PSD PDF Studio Enterprise";
    }

    public partial class SettingsWindow : Window
    {
        public AppSettingsState Settings { get; private set; }

        public SettingsWindow(AppSettingsState currentSettings)
        {
            InitializeComponent();
            Settings = currentSettings ?? new AppSettingsState();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            ChkEnableHoverPreview.IsChecked = Settings.HoverPreviewEnabled;

            if (Settings.HoverPreviewDelayMs <= 150) CmbHoverDelay.SelectedIndex = 0;
            else if (Settings.HoverPreviewDelayMs >= 500) CmbHoverDelay.SelectedIndex = 2;
            else CmbHoverDelay.SelectedIndex = 1;

            CmbDefaultViewMode.SelectedIndex = Math.Clamp(Settings.DefaultDashboardView, 0, 2);

            ChkTouchpadPinchZoom.IsChecked = Settings.TouchpadPinchZoomEnabled;
            ChkHorizontalScroll.IsChecked = Settings.HorizontalScrollEnabled;

            if (Settings.ZoomSensitivityStep <= 0.05) CmbZoomSensitivity.SelectedIndex = 0;
            else if (Settings.ZoomSensitivityStep >= 0.15) CmbZoomSensitivity.SelectedIndex = 2;
            else CmbZoomSensitivity.SelectedIndex = 1;

            ChkAutoScanLibrary.IsChecked = Settings.AutoScanLibrary;
            TxtDefaultWatermark.Text = Settings.DefaultWatermark;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.HoverPreviewEnabled = ChkEnableHoverPreview.IsChecked == true;

            int delayIdx = CmbHoverDelay.SelectedIndex;
            Settings.HoverPreviewDelayMs = delayIdx == 0 ? 150 : (delayIdx == 2 ? 500 : 300);

            Settings.DefaultDashboardView = CmbDefaultViewMode.SelectedIndex;
            Settings.TouchpadPinchZoomEnabled = ChkTouchpadPinchZoom.IsChecked == true;
            Settings.HorizontalScrollEnabled = ChkHorizontalScroll.IsChecked == true;

            int zoomIdx = CmbZoomSensitivity.SelectedIndex;
            Settings.ZoomSensitivityStep = zoomIdx == 0 ? 0.05 : (zoomIdx == 2 ? 0.15 : 0.08);

            Settings.AutoScanLibrary = ChkAutoScanLibrary.IsChecked == true;
            Settings.DefaultWatermark = TxtDefaultWatermark.Text.Trim();

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
