using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class PdfOptimizerWindow : Window
    {
        private readonly PdfDocumentModel _document;

        public PdfOptimizerWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document;
            LoadInfo();
        }

        private void LoadInfo()
        {
            if (!string.IsNullOrEmpty(_document.FilePath) && File.Exists(_document.FilePath))
            {
                var fi = new FileInfo(_document.FilePath);
                double mb = fi.Length / (1024.0 * 1024.0);
                TxtOriginalSize.Text = mb > 1 ? $"{mb:0.1} MB" : $"{fi.Length / 1024.0:0.1} KB";
                TxtEstimatedSavings.Text = "~58% reduction";
            }
            else
            {
                TxtOriginalSize.Text = "InMemory Doc";
                TxtEstimatedSavings.Text = "~45% reduction";
            }
        }

        private void CmbPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtEstimatedSavings == null) return;
            int idx = CmbPreset?.SelectedIndex ?? 0;
            TxtEstimatedSavings.Text = idx switch
            {
                0 => "~58% reduction (Max)",
                1 => "~35% reduction (Balanced)",
                _ => "~15% reduction (Print)"
            };
        }

        private void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_document.FilePath) || !File.Exists(_document.FilePath))
            {
                MessageBox.Show("Please save document before running optimizer.", "Save Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Optimized PDF (*.pdf)|*.pdf",
                FileName = Path.ChangeExtension(Path.GetFileName(_document.FilePath), "_optimized.pdf"),
                Title = "Save Optimized PDF"
            };

            if (dlg.ShowDialog() == true)
            {
                string preset = (CmbPreset.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Standard Clean";
                var report = PdfSecurityService.OptimizePdfDocument(_document.FilePath, dlg.FileName, preset);

                TxtOptStatus.Text = $"Optimized: saved {report.ReductionPercentage:0.1}% space.";
                string msg = $"PDF Optimization Finished!\n\n" +
                             $"• Original Size: {report.OriginalSizeBytes / 1024.0:0.1} KB\n" +
                             $"• Optimized Size: {report.OptimizedSizeBytes / 1024.0:0.1} KB\n" +
                             $"• Size Reduction: {report.ReductionPercentage:0.1}%\n" +
                             $"• Object Streams Linearized: {report.StreamsCompressed}\n" +
                             $"• Orphaned Objects Discarded: {report.UnusedObjectsStripped}\n\n" +
                             $"Saved to: {dlg.FileName}";
                MessageBox.Show(msg, "Optimization Report", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
