using System;
using System.Windows;
using System.Windows.Controls;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class WatermarkAndHeaderWindow : Window
    {
        private readonly PdfDocumentModel _document;

        public WatermarkAndHeaderWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document;
            UpdatePreview();
        }

        private void Settings_Changed(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LblOpacity != null)
            {
                LblOpacity.Text = $"OPACITY: {(int)e.NewValue}%";
            }
            UpdatePreview();
        }

        private void SliderAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LblAngle != null)
            {
                LblAngle.Text = $"ROTATION ANGLE: {(int)e.NewValue}°";
            }
            UpdatePreview();
        }

        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string text)
            {
                TxtWatermarkText.Text = text;
            }
        }

        private void UpdatePreview()
        {
            if (TxtPreviewWatermark == null || RotatePreview == null) return;
            TxtPreviewWatermark.Text = TxtWatermarkText?.Text ?? "CONFIDENTIAL";
            TxtPreviewWatermark.Opacity = (SliderOpacity?.Value ?? 25.0) / 100.0;
            RotatePreview.Angle = SliderAngle?.Value ?? -45.0;
        }

        private void BtnApplyWatermark_Click(object sender, RoutedEventArgs e)
        {
            var config = new WatermarkConfig
            {
                Text = TxtWatermarkText.Text.Trim(),
                Opacity = SliderOpacity.Value / 100.0,
                RotationAngle = SliderAngle.Value
            };

            PdfSecurityService.ApplyWatermark(_document, config);
            MessageBox.Show($"Watermark '{config.Text}' stamped across all {_document.Pages.Count} pages.", "Watermark Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void BtnApplyHeaderFooter_Click(object sender, RoutedEventArgs e)
        {
            var config = new HeaderFooterConfig
            {
                HeaderLeft = TxtHLeft.Text,
                HeaderCenter = TxtHCenter.Text,
                HeaderRight = TxtHRight.Text,
                FooterLeft = TxtFLeft.Text,
                FooterCenter = TxtFCenter.Text,
                FooterRight = TxtFRight.Text
            };

            PdfSecurityService.ApplyHeaderAndFooter(_document, config);
            MessageBox.Show($"Headers and footers stamped across all {_document.Pages.Count} pages.", "Header & Footer Stamped", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
