using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class TextConverterWindow : Window
    {
        private readonly PdfDocumentModel _document;
        public PdfDocumentModel? GeneratedDocument { get; private set; }

        public TextConverterWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document;

            // Load text from current active document
            var sb = new StringBuilder();
            for (int i = 0; i < _document.Pages.Count; i++)
            {
                sb.AppendLine($"--- PAGE {i + 1} ---");
                sb.AppendLine(_document.Pages[i].ContentText);
                sb.AppendLine();
            }
            TxtExtracted.Text = sb.ToString();
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtFontSizeVal != null)
            {
                TxtFontSizeVal.Text = $"{(int)e.NewValue} pt";
            }
        }

        private void BtnExportTxt_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Text as UTF-8 Document",
                Filter = "Text Files (*.txt)|*.txt",
                FileName = $"{Path.GetFileNameWithoutExtension(_document.Title)}_Transcript.txt"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, TxtExtracted.Text, Encoding.UTF8);
                MessageBox.Show($"Plain UTF-8 text exported successfully to:\n{dlg.FileName}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportMd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Text as Formatted Markdown",
                Filter = "Markdown Files (*.md)|*.md",
                FileName = $"{Path.GetFileNameWithoutExtension(_document.Title)}_Document.md"
            };
            if (dlg.ShowDialog() == true)
            {
                var md = new StringBuilder();
                md.AppendLine($"# {Path.GetFileNameWithoutExtension(_document.Title)}");
                md.AppendLine();
                md.AppendLine($"*Exported via PSD PDF Studio & Viewer on {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
                md.AppendLine();
                for (int i = 0; i < _document.Pages.Count; i++)
                {
                    md.AppendLine($"## Page {i + 1}");
                    md.AppendLine();
                    md.AppendLine(_document.Pages[i].ContentText);
                    md.AppendLine();
                }
                File.WriteAllText(dlg.FileName, md.ToString(), Encoding.UTF8);
                MessageBox.Show($"Markdown file exported successfully to:\n{dlg.FileName}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEmbedOcr_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Searchable OCR-Embedded PDF",
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"{Path.GetFileNameWithoutExtension(_document.Title)}_SearchableOCR.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                // Generates PDF with invisible 3 Tr text layer
                PdfDocumentModel.CompileTextToPdf(TxtExtracted.Text, dlg.FileName, 12.0, "");
                MessageBox.Show($"Searchable PDF with invisible 3 Tr OCR text layer embedded successfully at:\n{dlg.FileName}", "OCR Layer Embedded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCompileTextPdf_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtInputForPdf.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Please enter or paste text to compile into a PDF.", "Empty Content", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Save Compiled PDF Document",
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = "CompiledDocument.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                double fontSize = SliderFontSize.Value;
                string watermark = TxtWatermark.Text.Trim();
                PdfDocumentModel.CompileTextToPdf(text, dlg.FileName, fontSize, watermark);

                // Load compiled document back into app
                GeneratedDocument = PdfDocumentModel.LoadFromFile(dlg.FileName);

                MessageBox.Show($"Compiled dynamic text into standardized multi-page PDF successfully!\nSaved to: {dlg.FileName}", "Compilation Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
