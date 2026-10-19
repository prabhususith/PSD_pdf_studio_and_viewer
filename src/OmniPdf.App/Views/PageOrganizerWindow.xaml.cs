using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using OmniPdf.App.Core;

namespace OmniPdf.App.Views
{
    public partial class PageOrganizerWindow : Window
    {
        private readonly PdfDocumentModel _document;
        private readonly ObservableCollection<PdfPageModel> _workingPages = new();

        public PageOrganizerWindow(PdfDocumentModel document)
        {
            InitializeComponent();
            _document = document;

            foreach (var p in _document.Pages)
            {
                _workingPages.Add(new PdfPageModel
                {
                    PageNumber = p.PageNumber,
                    Rotation = p.Rotation,
                    Width = p.Width,
                    Height = p.Height,
                    ContentText = p.ContentText,
                    Annotations = p.Annotations
                });
            }

            PagesListBox.ItemsSource = _workingPages;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            TxtSummary.Text = $"Total Pages: {_workingPages.Count}";
        }

        private void BtnRotateRight_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is PdfPageModel page)
            {
                page.Rotation = (page.Rotation + 90) % 360;
                PagesListBox.Items.Refresh();
            }
        }

        private void BtnRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is PdfPageModel page)
            {
                page.Rotation = (page.Rotation + 270) % 360;
                PagesListBox.Items.Refresh();
            }
        }

        private void BtnMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            int idx = PagesListBox.SelectedIndex;
            if (idx > 0)
            {
                var item = _workingPages[idx];
                _workingPages.RemoveAt(idx);
                _workingPages.Insert(idx - 1, item);
                PagesListBox.SelectedIndex = idx - 1;
                RenumberPages();
            }
        }

        private void BtnMoveRight_Click(object sender, RoutedEventArgs e)
        {
            int idx = PagesListBox.SelectedIndex;
            if (idx >= 0 && idx < _workingPages.Count - 1)
            {
                var item = _workingPages[idx];
                _workingPages.RemoveAt(idx);
                _workingPages.Insert(idx + 1, item);
                PagesListBox.SelectedIndex = idx + 1;
                RenumberPages();
            }
        }

        private void BtnDeletePage_Click(object sender, RoutedEventArgs e)
        {
            if (_workingPages.Count <= 1)
            {
                MessageBox.Show("Cannot delete the only page in the document.", "Operation Prevented", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int idx = PagesListBox.SelectedIndex;
            if (idx >= 0)
            {
                _workingPages.RemoveAt(idx);
                RenumberPages();
                UpdateSummary();
            }
        }

        private void RenumberPages()
        {
            for (int i = 0; i < _workingPages.Count; i++)
            {
                _workingPages[i].PageNumber = i + 1;
            }
            PagesListBox.Items.Refresh();
        }

        private void BtnSplit_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Select Destination Folder & Base Filename for Split Pages",
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"{Path.GetFileNameWithoutExtension(_document.Title)}_Page_1.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                string dir = Path.GetDirectoryName(dlg.FileName)!;
                string baseName = Path.GetFileNameWithoutExtension(dlg.FileName).Replace("_Page_1", "");
                for (int i = 0; i < _workingPages.Count; i++)
                {
                    string outPath = Path.Combine(dir, $"{baseName}_Page_{i + 1}.pdf");
                    var singlePageDoc = new PdfDocumentModel { Title = $"{baseName}_Page_{i + 1}.pdf" };
                    singlePageDoc.Pages.Add(_workingPages[i]);
                    singlePageDoc.SaveIncremental(outPath);
                }
                MessageBox.Show($"Successfully split document into {_workingPages.Count} files in:\n{dir}", "Split Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnMerge_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select PDF Document to Merge",
                Filter = "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    var appendDoc = PdfDocumentModel.LoadFromFile(file);
                    foreach (var p in appendDoc.Pages)
                    {
                        _workingPages.Add(new PdfPageModel
                        {
                            PageNumber = _workingPages.Count + 1,
                            Rotation = p.Rotation,
                            Width = p.Width,
                            Height = p.Height,
                            ContentText = p.ContentText
                        });
                    }
                }
                RenumberPages();
                UpdateSummary();
                MessageBox.Show($"Successfully merged pages into document. Total pages: {_workingPages.Count}", "Merge Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExtract_Click(object sender, RoutedEventArgs e)
        {
            if (PagesListBox.SelectedItem is PdfPageModel page)
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Extract Selected Page",
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    FileName = $"Extracted_Page_{page.PageNumber}.pdf"
                };
                if (dlg.ShowDialog() == true)
                {
                    var extractDoc = new PdfDocumentModel { Title = Path.GetFileName(dlg.FileName) };
                    extractDoc.Pages.Add(page);
                    extractDoc.SaveIncremental(dlg.FileName);
                    MessageBox.Show($"Page {page.PageNumber} extracted successfully to:\n{dlg.FileName}", "Extraction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a page card to extract.", "Select Page", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            _document.Pages.Clear();
            foreach (var p in _workingPages)
            {
                _document.Pages.Add(p);
            }
            _document.IsModified = true;
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
