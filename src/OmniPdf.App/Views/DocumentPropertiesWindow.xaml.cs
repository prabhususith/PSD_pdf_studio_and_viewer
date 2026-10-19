using System;
using System.IO;
using System.Windows;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class DocumentPropertiesWindow : Window
    {
        private readonly PdfDocumentModel _document;

        public DocumentPropertiesWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document ?? throw new ArgumentNullException(nameof(document));
            PopulateProperties();
        }

        private void PopulateProperties()
        {
            TxtFileName.Text = _document.Title ?? "Untitled.pdf";

            if (!string.IsNullOrEmpty(_document.FilePath) && File.Exists(_document.FilePath))
            {
                TxtFilePath.Text = _document.FilePath;
                var fi = new FileInfo(_document.FilePath);
                double mb = fi.Length / (1024.0 * 1024.0);
                TxtFileSize.Text = mb >= 1.0 ? $"{mb:0.2} MB ({fi.Length:N0} bytes)" : $"{fi.Length / 1024.0:0.1} KB ({fi.Length:N0} bytes)";
            }
            else
            {
                TxtFilePath.Text = "InMemory / Unsaved Document";
                TxtFileSize.Text = "Unsaved draft";
            }

            int count = Math.Max(1, _document.Pages.Count);
            TxtPageCount.Text = $"{count} {(count == 1 ? "Page" : "Pages")}";

            if (_document.Pages.Count > 0)
            {
                var p = _document.Pages[0];
                double wIn = p.Width / 72.0;
                double hIn = p.Height / 72.0;
                string paper = Math.Abs(p.Width - 612) < 5 && Math.Abs(p.Height - 792) < 5 ? "Letter" :
                               Math.Abs(p.Width - 595) < 5 && Math.Abs(p.Height - 842) < 5 ? "A4" : "Custom";
                TxtDimensions.Text = $"{(int)p.Width} x {(int)p.Height} pt ({wIn:0.1} x {hIn:0.1} in, {paper})";
            }

            TxtMetaTitle.Text = _document.Title ?? "";
            TxtMetaAuthor.Text = _document.Author ?? "";
            TxtMetaSubject.Text = _document.Subject ?? "";
            TxtMetaKeywords.Text = _document.Keywords ?? "";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            _document.Title = TxtMetaTitle.Text.Trim();
            _document.Author = TxtMetaAuthor.Text.Trim();
            _document.Subject = TxtMetaSubject.Text.Trim();
            _document.Keywords = TxtMetaKeywords.Text.Trim();
            _document.IsModified = true;

            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
