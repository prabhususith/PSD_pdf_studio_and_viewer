using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace OmniPdf.App.Core
{
    public class PdfLibraryItem
    {
        public string Title { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string DirectoryPath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public string FormattedSize { get; set; } = "0 KB";
        public DateTime LastModified { get; set; }
        public string FormattedDate { get; set; } = "";
        public int PageCount { get; set; } = 1;
        public string PageCountDisplay => PageCount > 0 ? $"{PageCount} Pages" : "Document";
        public BitmapSource? Thumbnail { get; set; }
    }

    public static class LibraryScannerService
    {
        public static async Task ScanDevicePdfLibraryAsync(
            ObservableCollection<PdfLibraryItem> libraryCollection,
            Action<int, string>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var searchFolders = new List<string>();

            // Collect standard user directories
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(docs) && !searchFolders.Contains(docs)) searchFolders.Add(docs);
            }
            catch { }

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktop) && !searchFolders.Contains(desktop)) searchFolders.Add(desktop);
            }
            catch { }

            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string downloads = Path.Combine(userProfile, "Downloads");
                if (Directory.Exists(downloads) && !searchFolders.Contains(downloads)) searchFolders.Add(downloads);
            }
            catch { }

            // Current workspace / root drive search targets
            try
            {
                string pDrive = @"P:\prabhu";
                if (Directory.Exists(pDrive) && !searchFolders.Contains(pDrive)) searchFolders.Add(pDrive);
            }
            catch { }

            var discoveredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                int count = 0;
                foreach (var folder in searchFolders)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (!Directory.Exists(folder)) continue;

                    onProgress?.Invoke(count, $"Scanning {Path.GetFileName(folder)}...");

                    try
                    {
                        var options = new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                            MaxRecursionDepth = 4,
                            ReturnSpecialDirectories = false
                        };

                        foreach (var filePath in Directory.EnumerateFiles(folder, "*.pdf", options))
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            if (discoveredFiles.Add(filePath))
                            {
                                try
                                {
                                    var fi = new FileInfo(filePath);
                                    if (fi.Length == 0) continue;

                                    string sizeStr;
                                    if (fi.Length > 1024 * 1024)
                                        sizeStr = $"{fi.Length / (1024.0 * 1024.0):0.1} MB";
                                    else
                                        sizeStr = $"{Math.Max(1, fi.Length / 1024.0):0} KB";

                                    var item = new PdfLibraryItem
                                    {
                                        Title = Path.GetFileNameWithoutExtension(filePath),
                                        FileName = Path.GetFileName(filePath),
                                        FilePath = filePath,
                                        DirectoryPath = Path.GetDirectoryName(filePath) ?? "",
                                        FileSizeBytes = fi.Length,
                                        FormattedSize = sizeStr,
                                        LastModified = fi.LastWriteTime,
                                        FormattedDate = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                                        PageCount = 1
                                    };

                                    count++;
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        libraryCollection.Add(item);
                                    });

                                    if (count % 5 == 0)
                                    {
                                        onProgress?.Invoke(count, $"Found {count} PDF documents...");
                                    }

                                    // Cap automatic scan to 250 items to keep UI lighting fast
                                    if (count >= 250) break;
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                onProgress?.Invoke(count, $"Scan completed. Found {count} PDFs on device.");
            }, cancellationToken);
        }
    }
}
