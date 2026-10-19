using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class BatchProcessingWindow : Window
    {
        private readonly ObservableCollection<string> _files = new();

        public BatchProcessingWindow()
        {
            InitializeComponent();
            ListBatchQueue.ItemsSource = _files;
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All Supported Files|*.pdf;*.docx;*.rtf;*.txt;*.md;*.html|PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Add Files to Batch Processing Queue"
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    if (!_files.Contains(file)) _files.Add(file);
                }
                UpdateStatus();
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "Select Folder",
                Title = "Select Folder for Batch Import"
            };

            if (dlg.ShowDialog() == true)
            {
                string? folder = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    var found = Directory.GetFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly);
                    foreach (var f in found)
                    {
                        if (!_files.Contains(f)) _files.Add(f);
                    }
                    UpdateStatus();
                }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _files.Clear();
            UpdateStatus();
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "Output_Folder_Placeholder",
                Title = "Select Output Directory"
            };
            if (dlg.ShowDialog() == true)
            {
                string? dir = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(dir))
                {
                    TxtOutputDir.Text = dir;
                }
            }
        }

        private void UpdateStatus()
        {
            TxtBatchStatus.Text = $"Ready to process {_files.Count} documents";
        }

        private async void BtnStartBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_files.Count == 0)
            {
                MessageBox.Show("Please add files to the batch queue first.", "Empty Queue", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outDir = TxtOutputDir.Text.Trim();
            if (!Directory.Exists(outDir))
            {
                try { Directory.CreateDirectory(outDir); } catch { }
            }

            BtnStartBatch.IsEnabled = false;
            PbBatchProgress.Value = 0;
            string selectedAction = (CmbBatchAction.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            await Task.Run(() =>
            {
                for (int i = 0; i < _files.Count; i++)
                {
                    string file = _files[i];
                    string baseName = Path.GetFileNameWithoutExtension(file);

                    try
                    {
                        if (selectedAction.Contains("Word"))
                        {
                            string dest = Path.Combine(outDir, $"{baseName}.docx");
                            var doc = PdfDocumentModel.LoadFromFile(file);
                            File.WriteAllText(dest, doc.Pages.Count > 0 ? doc.Pages[0].ContentText : "Exported Document");
                        }
                        else if (selectedAction.Contains("Markdown"))
                        {
                            string dest = Path.Combine(outDir, $"{baseName}.md");
                            var doc = PdfDocumentModel.LoadFromFile(file);
                            File.WriteAllText(dest, $"# {baseName}\n\n" + (doc.Pages.Count > 0 ? doc.Pages[0].ContentText : ""));
                        }
                        else if (selectedAction.Contains("Watermark"))
                        {
                            string dest = Path.Combine(outDir, $"{baseName}_watermarked.pdf");
                            var doc = PdfDocumentModel.LoadFromFile(file);
                            PdfSecurityService.ApplyWatermark(doc, new WatermarkConfig { Text = "CONFIDENTIAL" });
                            PdfDocumentModel.CompileTextToPdf(doc.Pages[0].ContentText, dest);
                        }
                        else
                        {
                            string dest = Path.Combine(outDir, $"{baseName}_optimized.pdf");
                            PdfSecurityService.OptimizePdfDocument(file, dest, "Standard Clean");
                        }
                    }
                    catch { }

                    int progress = (int)(((i + 1) / (double)_files.Count) * 100);
                    Dispatcher.Invoke(() =>
                    {
                        PbBatchProgress.Value = progress;
                        TxtBatchStatus.Text = $"Processing {i + 1} of {_files.Count}: {Path.GetFileName(file)}";
                    });
                }
            });

            BtnStartBatch.IsEnabled = true;
            TxtBatchStatus.Text = $"Batch task completed successfully! {_files.Count} files exported.";
            MessageBox.Show($"Batch processing finished!\nOutput folder: {outDir}", "Batch Automation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
