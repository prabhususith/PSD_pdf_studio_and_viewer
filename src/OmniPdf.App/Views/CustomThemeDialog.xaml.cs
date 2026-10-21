using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class CustomThemeDialog : Window
    {
        private TextBox? _lastFocusedField;
        private ThemeDefinition _workingTheme;

        // Curated 35 rich palette swatches across Dark, Light, Primary, Accent, Neon, Sepia
        private static readonly string[] SwatchColors = new[]
        {
            "#0F0F12", "#18181B", "#27272A", "#3F3F46", "#71717A", "#A1A1AA", "#D4D4D8", "#F4F4F5", "#FFFFFF",
            "#000000", "#0B0C10", "#1E222A", "#061A14", "#120B1C", "#14120B", "#180A0C", "#0A1128", "#121214",
            "#0284C7", "#0369A1", "#38BDF8", "#2563EB", "#1D4ED8", "#4F46E5", "#6366F1", "#7C3AED", "#9333EA",
            "#10B981", "#059669", "#34D399", "#F59E0B", "#D97706", "#EA580C", "#EF4444", "#DC2626", "#F43F5E",
            "#00F0FF", "#00FF66", "#FF007F", "#8D6E63", "#CA8A04", "#0D9488", "#E11D48", "#BE185D", "#475569"
        };

        public CustomThemeDialog()
        {
            InitializeComponent();
            _workingTheme = ThemeManager.CustomTheme ?? ThemeManager.AllThemes[0];
            _lastFocusedField = TxtWinBg;

            PopulatePresetDropdown();
            PopulateSwatches();
            LoadThemeIntoControls(_workingTheme);
            UpdatePreview();
        }

        private void PopulatePresetDropdown()
        {
            CmbPresetStarter.Items.Clear();
            foreach (var t in ThemeManager.AllThemes)
            {
                CmbPresetStarter.Items.Add(new ComboBoxItem
                {
                    Content = $"[{t.Category}] {t.Name}",
                    Tag = t
                });
            }
        }

        private void PopulateSwatches()
        {
            WrapSwatches.Children.Clear();
            foreach (var hex in SwatchColors)
            {
                var color = ThemeDefinition.Hex(hex);
                var btn = new Button
                {
                    Style = (Style)FindResource("PaletteChip"),
                    Background = new SolidColorBrush(color),
                    ToolTip = hex,
                    Tag = hex
                };
                btn.Click += Swatch_Click;
                WrapSwatches.Children.Add(btn);
            }
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                var target = _lastFocusedField ?? TxtWinBg;
                target.Text = hex;
            }
        }

        private void LoadThemeIntoControls(ThemeDefinition theme)
        {
            TxtWinBg.Text = ThemeManager.ColorToHex(theme.WindowBg);
            TxtWinFg.Text = ThemeManager.ColorToHex(theme.WindowFg);
            TxtHeaderBg.Text = ThemeManager.ColorToHex(theme.HeaderBg);
            TxtAccentDef.Text = ThemeManager.ColorToHex(theme.AccentDef);
            TxtCanvasBg.Text = ThemeManager.ColorToHex(theme.CanvasBg);
            TxtStatusBg.Text = ThemeManager.ColorToHex(theme.StatusBg);
        }

        private void ColorField_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _lastFocusedField = tb;
            }
        }

        private void ColorField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdatePreview();
            }
        }

        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                TextBox? target = tag switch
                {
                    "WinBg" => TxtWinBg,
                    "WinFg" => TxtWinFg,
                    "HeaderBg" => TxtHeaderBg,
                    "AccentDef" => TxtAccentDef,
                    "CanvasBg" => TxtCanvasBg,
                    "StatusBg" => TxtStatusBg,
                    _ => null
                };

                if (target != null)
                {
                    _lastFocusedField = target;
                    // Cycle through a few complementary shades or open color dialog if needed
                    var currentColor = ThemeDefinition.Hex(target.Text);
                    // Lighten or cycle hue
                    byte r = (byte)((currentColor.R + 40) % 256);
                    byte g = (byte)((currentColor.G + 50) % 256);
                    byte b = (byte)((currentColor.B + 60) % 256);
                    target.Text = ThemeManager.ColorToHex(Color.FromRgb(r, g, b));
                }
            }
        }

        private void CmbPresetStarter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbPresetStarter.SelectedItem is ComboBoxItem item && item.Tag is ThemeDefinition preset)
            {
                LoadThemeIntoControls(preset);
                TxtThemeNameDisplay.Text = $"Based on {preset.Name}";
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            try
            {
                var winBg = ThemeDefinition.Hex(TxtWinBg.Text);
                var winFg = ThemeDefinition.Hex(TxtWinFg.Text);
                var headerBg = ThemeDefinition.Hex(TxtHeaderBg.Text);
                var accent = ThemeDefinition.Hex(TxtAccentDef.Text);
                var canvasBg = ThemeDefinition.Hex(TxtCanvasBg.Text);
                var statusBg = ThemeDefinition.Hex(TxtStatusBg.Text);

                // Update indicators
                ChipWinBg.Background = new SolidColorBrush(winBg);
                ChipWinFg.Background = new SolidColorBrush(winFg);
                ChipHeaderBg.Background = new SolidColorBrush(headerBg);
                ChipAccentDef.Background = new SolidColorBrush(accent);
                ChipCanvasBg.Background = new SolidColorBrush(canvasBg);
                ChipStatusBg.Background = new SolidColorBrush(statusBg);

                // Update Preview mock window
                PrevWindow.Background = new SolidColorBrush(winBg);
                PrevWindow.BorderBrush = new SolidColorBrush(Color.FromRgb((byte)Math.Clamp(winBg.R + 25, 0, 255), (byte)Math.Clamp(winBg.G + 25, 0, 255), (byte)Math.Clamp(winBg.B + 25, 0, 255)));

                PrevHeader.Background = new SolidColorBrush(headerBg);
                PrevHeader.BorderBrush = new SolidColorBrush(Color.FromRgb((byte)Math.Clamp(headerBg.R + 25, 0, 255), (byte)Math.Clamp(headerBg.G + 25, 0, 255), (byte)Math.Clamp(headerBg.B + 25, 0, 255)));
                PrevTitleText.Foreground = new SolidColorBrush(winFg);
                PrevLogoBadge.Background = new SolidColorBrush(accent);

                PrevRibbon.Background = new SolidColorBrush(Color.FromRgb((byte)Math.Clamp(headerBg.R + 10, 0, 255), (byte)Math.Clamp(headerBg.G + 10, 0, 255), (byte)Math.Clamp(headerBg.B + 10, 0, 255)));
                PrevRibbon.BorderBrush = new SolidColorBrush(Color.FromRgb((byte)Math.Clamp(headerBg.R + 30, 0, 255), (byte)Math.Clamp(headerBg.G + 30, 0, 255), (byte)Math.Clamp(headerBg.B + 30, 0, 255)));

                PrevBtnAccent.Background = new SolidColorBrush(accent);
                PrevBtnOutline.BorderBrush = new SolidColorBrush(accent);
                PrevBtnOutlineText.Foreground = new SolidColorBrush(accent);
                PrevBtnTool.BorderBrush = new SolidColorBrush(Color.FromRgb((byte)Math.Clamp(winBg.R + 40, 0, 255), (byte)Math.Clamp(winBg.G + 40, 0, 255), (byte)Math.Clamp(winBg.B + 40, 0, 255)));
                PrevBtnToolText.Foreground = new SolidColorBrush(winFg);

                PrevCanvas.Background = new SolidColorBrush(canvasBg);
                PrevStatusBar.Background = new SolidColorBrush(statusBg);
                PrevStatusText.Foreground = new SolidColorBrush(Colors.White);
                PrevStatusRight.Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            }
            catch { }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var defaultDark = ThemeManager.AllThemes[0];
            LoadThemeIntoControls(defaultDark);
            TxtThemeNameDisplay.Text = "Reset to Fluent Dark";
            UpdatePreview();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnApplyAndSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var winBg = ThemeDefinition.Hex(TxtWinBg.Text);
                var winFg = ThemeDefinition.Hex(TxtWinFg.Text);
                var headerBg = ThemeDefinition.Hex(TxtHeaderBg.Text);
                var accent = ThemeDefinition.Hex(TxtAccentDef.Text);
                var canvasBg = ThemeDefinition.Hex(TxtCanvasBg.Text);
                var statusBg = ThemeDefinition.Hex(TxtStatusBg.Text);

                var custom = ThemeDefinition.Create(
                    "custom", "Custom Theme", "Custom",
                    ThemeManager.ColorToHex(winBg),
                    ThemeManager.ColorToHex(winFg),
                    ThemeManager.ColorToHex(headerBg),
                    ThemeManager.ColorToHex(winFg),
                    ThemeManager.ColorToHex(headerBg),
                    ThemeManager.ColorToHex(winFg),
                    ThemeManager.ColorToHex(Color.FromRgb((byte)Math.Clamp(headerBg.R + 15, 0, 255), (byte)Math.Clamp(headerBg.G + 15, 0, 255), (byte)Math.Clamp(headerBg.B + 15, 0, 255))),
                    ThemeManager.ColorToHex(winFg),
                    ThemeManager.ColorToHex(Color.FromRgb((byte)Math.Clamp(headerBg.R + 35, 0, 255), (byte)Math.Clamp(headerBg.G + 35, 0, 255), (byte)Math.Clamp(headerBg.B + 35, 0, 255))),
                    ThemeManager.ColorToHex(headerBg),
                    ThemeManager.ColorToHex(canvasBg),
                    ThemeManager.ColorToHex(headerBg),
                    ThemeManager.ColorToHex(Color.FromRgb((byte)Math.Clamp(headerBg.R + 25, 0, 255), (byte)Math.Clamp(headerBg.G + 25, 0, 255), (byte)Math.Clamp(headerBg.B + 25, 0, 255))),
                    ThemeManager.ColorToHex(Color.FromRgb((byte)Math.Clamp(headerBg.R + 45, 0, 255), (byte)Math.Clamp(headerBg.G + 45, 0, 255), (byte)Math.Clamp(headerBg.B + 45, 0, 255))),
                    ThemeManager.ColorToHex(accent),
                    ThemeManager.ColorToHex(Color.FromRgb((byte)Math.Clamp(accent.R - 20, 0, 255), (byte)Math.Clamp(accent.G - 20, 0, 255), (byte)Math.Clamp(accent.B - 20, 0, 255))),
                    ThemeManager.ColorToHex(statusBg),
                    "#FFFFFF"
                );

                ThemeManager.SaveCustomTheme(custom);
                ThemeManager.ApplyTheme(custom, Owner?.Resources);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply custom theme: {ex.Message}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
