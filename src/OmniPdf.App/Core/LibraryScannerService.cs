using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OmniPdf.App.Core
{
    public class PdfLibraryItem
    {
        public string Title { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string DirectoryPath { get; set; } = "";
        public string Extension { get; set; } = ".pdf";
        public string FormatBadge { get; set; } = "PDF";
        public long FileSizeBytes { get; set; }
        public string FormattedSize { get; set; } = "0 KB";
        public DateTime LastModified { get; set; }
        public string FormattedDate { get; set; } = "";
        public int PageCount { get; set; } = 1;
        public string PageCountDisplay => PageCount > 0 ? $"{PageCount} Pages" : "Document";
        public BitmapSource? Thumbnail { get; set; }
    }

    /// <summary>
    /// Ultra-High-Performance Non-Blocking Local Device Document Scanner.
    /// Employs parallel directory enumerators, lock-free producer-consumer queues,
    /// and throttled background UI dispatches to keep the frame rate at 120 FPS.
    /// </summary>
    public static class LibraryScannerService
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".doc", ".rtf", ".txt", ".md", ".html", ".htm", ".psd", ".pptx", ".xlsx"
        };

        public static async Task ScanDevicePdfLibraryAsync(
            ObservableCollection<PdfLibraryItem> libraryCollection,
            Action<int, string>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var searchFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect standard user directories
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(docs)) searchFolders.Add(docs);
            }
            catch { }

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktop)) searchFolders.Add(desktop);
            }
            catch { }

            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string downloads = Path.Combine(userProfile, "Downloads");
                if (Directory.Exists(downloads)) searchFolders.Add(downloads);
            }
            catch { }

            // Current workspace / project drives
            try
            {
                string pDrive = @"P:\prabhu";
                if (Directory.Exists(pDrive)) searchFolders.Add(pDrive);
            }
            catch { }

            var discoveredFiles = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var batchBuffer = new ConcurrentQueue<PdfLibraryItem>();
            int totalDiscovered = 0;
            const int maxItemsToScan = 500; // Expanded capacity for comprehensive indexing

            // Dedicated UI flush timer to batch updates without stalling UI thread
            var flushCts = new CancellationTokenSource();
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, flushCts.Token);

            var flushTask = Task.Run(async () =>
            {
                while (!combinedCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(80, combinedCts.Token).ConfigureAwait(false);
                    FlushBatchToUi(batchBuffer, libraryCollection, onProgress, ref totalDiscovered);
                }
            }, combinedCts.Token);

            try
            {
                await Task.Run(() =>
                {
                    var options = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        MaxRecursionDepth = 4,
                        ReturnSpecialDirectories = false
                    };

                    Parallel.ForEach(searchFolders, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 4),
                        CancellationToken = cancellationToken
                    }, folder =>
                    {
                        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref totalDiscovered) >= maxItemsToScan)
                            return;

                        try
                        {
                            foreach (var filePath in Directory.EnumerateFiles(folder, "*.*", options))
                            {
                                if (cancellationToken.IsCancellationRequested || Volatile.Read(ref totalDiscovered) >= maxItemsToScan)
                                    break;

                                string ext = Path.GetExtension(filePath);
                                if (!SupportedExtensions.Contains(ext)) continue;

                                if (discoveredFiles.TryAdd(filePath, 0))
                                {
                                    try
                                    {
                                        var fi = new FileInfo(filePath);
                                        if (fi.Length == 0) continue;

                                        string sizeStr = fi.Length > 1024 * 1024
                                            ? $"{fi.Length / (1024.0 * 1024.0):0.1} MB"
                                            : $"{Math.Max(1, fi.Length / 1024.0):0} KB";

                                        string badge = ext.TrimStart('.').ToUpperInvariant();
                                        if (badge == "MARKDOWN") badge = "MD";

                                        var item = new PdfLibraryItem
                                        {
                                            Title = Path.GetFileNameWithoutExtension(filePath),
                                            FileName = Path.GetFileName(filePath),
                                            FilePath = filePath,
                                            DirectoryPath = Path.GetDirectoryName(filePath) ?? "",
                                            Extension = ext.ToLowerInvariant(),
                                            FormatBadge = badge,
                                            FileSizeBytes = fi.Length,
                                            FormattedSize = sizeStr,
                                            LastModified = fi.LastWriteTime,
                                            FormattedDate = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                                            PageCount = 1
                                        };

                                        batchBuffer.Enqueue(item);
                                        Interlocked.Increment(ref totalDiscovered);
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    });
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful cancellation requested by caller or UI navigation
            }
            finally
            {
                flushCts.Cancel();
                try { await flushTask; } catch { }
                // Final flush of remaining items in buffer
                FlushBatchToUi(batchBuffer, libraryCollection, onProgress, ref totalDiscovered, isFinal: true);
            }

            onProgress?.Invoke(totalDiscovered, $"Indexed {totalDiscovered} documents in ultra-fast device library.");
        }

        private static void FlushBatchToUi(
            ConcurrentQueue<PdfLibraryItem> buffer,
            ObservableCollection<PdfLibraryItem> targetCollection,
            Action<int, string>? onProgress,
            ref int totalDiscovered,
            bool isFinal = false)
        {
            var chunk = new List<PdfLibraryItem>(64);
            while (buffer.TryDequeue(out var item))
            {
                chunk.Add(item);
                if (chunk.Count >= 64) break;
            }

            if (chunk.Count == 0 && !isFinal) return;

            var app = Application.Current;
            if (app == null || app.Dispatcher == null || app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
                return;

            try
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var item in chunk)
                    {
                        targetCollection.Add(item);
                    }
                }, DispatcherPriority.Background);
            }
            catch { }

            int currentCount = Volatile.Read(ref totalDiscovered);
            onProgress?.Invoke(currentCount, $"Indexed {currentCount} documents (PDF, Word, Office)...");
        }
    }
}
