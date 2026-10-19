using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class SecurityAndRedactionWindow : Window
    {
        private readonly PdfDocumentModel _document;

        public SecurityAndRedactionWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document;
        }

        private void BtnApplyEncryption_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_document.FilePath) || !File.Exists(_document.FilePath))
            {
                MessageBox.Show("Please save the active document before applying encryption.", "Save Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Encrypted PDF (*.pdf)|*.pdf",
                FileName = Path.ChangeExtension(Path.GetFileName(_document.FilePath), "_protected.pdf"),
                Title = "Save Encrypted Document"
            };

            if (dlg.ShowDialog() == true)
            {
                var perms = new DocumentPermissions
                {
                    CanPrint = ChkAllowPrint.IsChecked == true,
                    CanCopy = ChkAllowCopy.IsChecked == true,
                    CanModify = ChkAllowModify.IsChecked == true,
                    CanAnnotate = ChkAllowAnnotate.IsChecked == true
                };

                PdfSecurityService.ProtectDocument(_document.FilePath, dlg.FileName, TxtUserPass.Password, TxtOwnerPass.Password, perms);
                TxtSecurityStatus.Text = $"Encrypted and protected: {Path.GetFileName(dlg.FileName)}";
                MessageBox.Show($"Document successfully secured with AES-256 standard encryption:\n{dlg.FileName}", "Security Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnApplyRedaction_Click(object sender, RoutedEventArgs e)
        {
            bool sanitize = ChkSanitizeMetadata.IsChecked == true;
            PdfSecurityService.RedactAndSanitize(_document, new(), sanitize);

            TxtSecurityStatus.Text = "Sensitive PII redacted and metadata sanitized.";
            MessageBox.Show("Redaction complete!\nAll sensitive numbers (SSN, credit cards) and emails scrubbed from content streams.", "Redaction Scrubbed", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void BtnApplyBates_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtBatesStart.Text, out int startNum)) startNum = 1;
            if (!int.TryParse(TxtBatesDigits.Text, out int digits)) digits = 6;

            var config = new BatesNumberConfig
            {
                Prefix = TxtBatesPrefix.Text,
                StartNumber = startNum,
                NumberOfDigits = digits,
                Position = CmbBatesPosition.Text
            };

            PdfSecurityService.ApplyBatesNumbering(_document, config);
            TxtSecurityStatus.Text = $"Bates numbering stamped from {config.Prefix}{startNum.ToString().PadLeft(digits, '0')}.";
            MessageBox.Show($"Bates numbering stamped across all {_document.Pages.Count} pages.", "Bates Stamping Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
