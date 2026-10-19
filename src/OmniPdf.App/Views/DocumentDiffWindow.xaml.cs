using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class DocumentDiffWindow : Window
    {
        public DocumentDiffWindow(string? initialDocA = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(initialDocA) && File.Exists(initialDocA))
            {
                TxtDocAPath.Text = initialDocA;
                LoadContentA(initialDocA);
            }
        }

        private void BtnBrowseDocA_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF & Office Files|*.pdf;*.docx;*.rtf;*.txt;*.md|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                TxtDocAPath.Text = dlg.FileName;
                LoadContentA(dlg.FileName);
            }
        }

        private void BtnBrowseDocB_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF & Office Files|*.pdf;*.docx;*.rtf;*.txt;*.md|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                TxtDocBPath.Text = dlg.FileName;
                LoadContentB(dlg.FileName);
            }
        }

        private void LoadContentA(string path)
        {
            try
            {
                var doc = PdfDocumentModel.LoadFromFile(path);
                var sb = new StringBuilder();
                for (int i = 0; i < doc.Pages.Count; i++)
                {
                    sb.AppendLine($"--- PAGE {i + 1} ---");
                    sb.AppendLine(doc.Pages[i].ContentText);
                    sb.AppendLine();
                }
                TxtContentA.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                TxtContentA.Text = $"Error loading document A: {ex.Message}";
            }
        }

        private void LoadContentB(string path)
        {
            try
            {
                var doc = PdfDocumentModel.LoadFromFile(path);
                var sb = new StringBuilder();
                for (int i = 0; i < doc.Pages.Count; i++)
                {
                    sb.AppendLine($"--- PAGE {i + 1} ---");
                    sb.AppendLine(doc.Pages[i].ContentText);
                    sb.AppendLine();
                }
                TxtContentB.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                TxtContentB.Text = $"Error loading document B: {ex.Message}";
            }
        }

        private void BtnRunCompare_Click(object sender, RoutedEventArgs e)
        {
            string pathA = TxtDocAPath.Text.Trim();
            string pathB = TxtDocBPath.Text.Trim();

            if (!File.Exists(pathA) || !File.Exists(pathB))
            {
                MessageBox.Show("Please select two valid documents to compare.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var diffResult = PdfDiffService.CompareDocuments(pathA, pathB);

            TxtDiffScore.Text = $"Similarity Score: {diffResult.SimilarityPercentage}%";
            TxtAdditions.Text = $"Additions: {diffResult.AdditionsCount}";
            TxtDeletions.Text = $"Deletions: {diffResult.DeletionsCount}";

            // Highlight additions in doc B
            var sbB = new StringBuilder();
            sbB.AppendLine($"=== COMPARISON SUMMARY ===");
            sbB.AppendLine($"Similarity: {diffResult.SimilarityPercentage}% | Additions: {diffResult.AdditionsCount} | Deletions: {diffResult.DeletionsCount}\n");

            foreach (var diff in diffResult.Differences)
            {
                string marker = diff.Type == DiffType.Added ? "[+ ADDED]" : "[- REMOVED]";
                sbB.AppendLine($"{marker} Page {diff.PageNumber}: {diff.Text}");
            }

            TxtContentB.Text = sbB.ToString();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
