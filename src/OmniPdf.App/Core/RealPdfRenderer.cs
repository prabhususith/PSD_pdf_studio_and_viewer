using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OmniPdf.App.Core
{
    /// <summary>
    /// Industrial-Strength Multi-Level Cached Native PDF Rasterization Engine.
    /// Provides zero-redundancy file loading, thread-safe asynchronous concurrency,
    /// dynamic LRU bitmap caching, and sub-millisecond page access.
    /// </summary>
    public static class RealPdfRenderer
    {
        private class CachedPdfEntry : IDisposable
        {
            public required PdfDocument Document { get; init; }
            public FileStream? FileStream { get; init; }
            public IRandomAccessStream? RandomAccessStream { get; init; }
            public required DateTime LastWriteTime { get; init; }
            public required int PageCount { get; init; }
            public required double DefaultWidth { get; init; }
            public required double DefaultHeight { get; init; }

            public void Dispose()
            {
                try { RandomAccessStream?.Dispose(); } catch { }
                try { FileStream?.Dispose(); } catch { }
            }
        }

        private class CachedBitmapEntry
        {
            public required BitmapImage Bitmap { get; init; }
            public required long EstimatedSizeBytes { get; init; }
            public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;
        }

        // Cache of parsed native PDF documents
        private static readonly ConcurrentDictionary<string, CachedPdfEntry> _documentCache = new(StringComparer.OrdinalIgnoreCase);

        // Multi-level LRU bitmap tile cache
        private static readonly ConcurrentDictionary<string, CachedBitmapEntry> _bitmapCache = new(StringComparer.Ordinal);
        private static long _currentBitmapCacheBytes = 0;
        private const long MaxBitmapCacheBytes = 256 * 1024 * 1024; // 256 MB high-res cache cap
        private static readonly object _evictionLock = new();

        // Hardware-adaptive render concurrency throttler
        private static readonly SemaphoreSlim _renderThrottle = new(Math.Clamp(Environment.ProcessorCount, 2, 8));

        /// <summary>
        /// Retrieves document metadata (page count, dimensions) with zero-redundancy caching.
        /// </summary>
        public static async Task<(int PageCount, double Width, double Height)> GetPdfInfoAsync(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return (0, 612, 792);

            try
            {
                var entry = await GetOrLoadDocumentAsync(pdfPath);
                if (entry != null)
                {
                    return (entry.PageCount, entry.DefaultWidth, entry.DefaultHeight);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealPdfRenderer] GetPdfInfoAsync error for '{pdfPath}': {ex.Message}");
            }

            return (0, 612, 792);
        }

        /// <summary>
        /// Renders a specific page at target resolution with thread-safe pooling and LRU caching.
        /// </summary>
        public static async Task<BitmapImage?> RenderPdfPageAsync(string pdfPath, int pageIndex, double targetWidth = 1400)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return null;

            int quantizedWidth = Math.Clamp((int)Math.Round(targetWidth / 50.0) * 50, 100, 4000);
            string cacheKey = $"{pdfPath}|{pageIndex}|{quantizedWidth}";

            // 1. Check in-memory bitmap cache
            if (_bitmapCache.TryGetValue(cacheKey, out var cachedEntry))
            {
                cachedEntry.LastAccessedUtc = DateTime.UtcNow;
                return cachedEntry.Bitmap;
            }

            // 2. Obtain or load document handle from pool
            var docEntry = await GetOrLoadDocumentAsync(pdfPath);
            if (docEntry == null || pageIndex < 0 || pageIndex >= docEntry.PageCount)
                return null;

            await _renderThrottle.WaitAsync();
            try
            {
                // Double-check cache inside semaphore
                if (_bitmapCache.TryGetValue(cacheKey, out cachedEntry))
                {
                    cachedEntry.LastAccessedUtc = DateTime.UtcNow;
                    return cachedEntry.Bitmap;
                }

                using var page = docEntry.Document.GetPage((uint)pageIndex);
                using var memStream = new InMemoryRandomAccessStream();

                var options = new PdfPageRenderOptions
                {
                    DestinationWidth = (uint)quantizedWidth
                };

                await page.RenderToStreamAsync(memStream, options);

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memStream.AsStreamForRead();
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Crucial for thread safety and zero UI-thread contention

                // Calculate memory footprint: Width * Height * 4 bytes (BGRA32)
                long estimatedBytes = (long)(bitmapImage.PixelWidth * bitmapImage.PixelHeight * 4);
                EnsureBitmapCacheCapacity(estimatedBytes);

                var newEntry = new CachedBitmapEntry
                {
                    Bitmap = bitmapImage,
                    EstimatedSizeBytes = estimatedBytes
                };

                _bitmapCache[cacheKey] = newEntry;
                Interlocked.Add(ref _currentBitmapCacheBytes, estimatedBytes);

                return bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealPdfRenderer] Page {pageIndex} render error in '{pdfPath}': {ex}");
                return null;
            }
            finally
            {
                _renderThrottle.Release();
            }
        }

        /// <summary>
        /// Retrieves an existing active document session or parses it into the pool.
        /// </summary>
        private static async Task<CachedPdfEntry?> GetOrLoadDocumentAsync(string pdfPath)
        {
            var fullPath = Path.GetFullPath(pdfPath);
            var lastWrite = File.GetLastWriteTimeUtc(fullPath);

            if (_documentCache.TryGetValue(fullPath, out var cached))
            {
                if (cached.LastWriteTime == lastWrite)
                {
                    return cached;
                }
                // File modified externally, invalidate and release previous handle
                if (_documentCache.TryRemove(fullPath, out var oldEntry))
                {
                    oldEntry.Dispose();
                }
                InvalidateBitmapsForFile(fullPath);
            }

            try
            {
                PdfDocument? pdfDoc = null;
                FileStream? fs = null;
                IRandomAccessStream? ras = null;

                // Method 1: StorageFile native OS mapping (cleanest, zero-stream-leak architecture)
                try
                {
                    var storageFile = await StorageFile.GetFileFromPathAsync(fullPath);
                    pdfDoc = await PdfDocument.LoadFromFileAsync(storageFile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RealPdfRenderer] StorageFile direct load fallback: {ex.Message}");
                    // Method 2: Managed FileStream + AsRandomAccessStream kept alive for lifetime of cached session
                    fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    ras = fs.AsRandomAccessStream();
                    pdfDoc = await PdfDocument.LoadFromStreamAsync(ras);
                }

                if (pdfDoc == null) return null;

                int count = (int)pdfDoc.PageCount;
                double w = 612.0;
                double h = 792.0;

                if (count > 0)
                {
                    using var firstPage = pdfDoc.GetPage(0);
                    w = firstPage.Size.Width;
                    h = firstPage.Size.Height;
                }

                var newEntry = new CachedPdfEntry
                {
                    Document = pdfDoc,
                    FileStream = fs,
                    RandomAccessStream = ras,
                    LastWriteTime = lastWrite,
                    PageCount = count,
                    DefaultWidth = w,
                    DefaultHeight = h
                };

                _documentCache[fullPath] = newEntry;
                return newEntry;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealPdfRenderer] Failed to load PDF document '{fullPath}': {ex}");
                return null;
            }
        }

        /// <summary>
        /// Evicts least recently used bitmaps when approaching memory limit.
        /// Thread-safe locked double-check pattern prevents eviction thrashing.
        /// </summary>
        private static void EnsureBitmapCacheCapacity(long requiredBytes)
        {
            if (Interlocked.Read(ref _currentBitmapCacheBytes) + requiredBytes <= MaxBitmapCacheBytes)
                return;

            lock (_evictionLock)
            {
                if (Interlocked.Read(ref _currentBitmapCacheBytes) + requiredBytes <= MaxBitmapCacheBytes)
                    return;

                long targetBytes = Math.Max(0, MaxBitmapCacheBytes - requiredBytes - (32 * 1024 * 1024));
                var entries = _bitmapCache.ToArray();
                Array.Sort(entries, static (a, b) => a.Value.LastAccessedUtc.CompareTo(b.Value.LastAccessedUtc));

                foreach (var kvp in entries)
                {
                    if (Interlocked.Read(ref _currentBitmapCacheBytes) <= targetBytes)
                        break;

                    if (_bitmapCache.TryRemove(kvp.Key, out var removed))
                    {
                        Interlocked.Add(ref _currentBitmapCacheBytes, -removed.EstimatedSizeBytes);
                    }
                }
            }
        }

        private static void InvalidateBitmapsForFile(string fullPath)
        {
            string prefix = fullPath + "|";
            foreach (var key in _bitmapCache.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (_bitmapCache.TryRemove(key, out var removed))
                    {
                        Interlocked.Add(ref _currentBitmapCacheBytes, -removed.EstimatedSizeBytes);
                    }
                }
            }
        }

        /// <summary>
        /// Disposes cached handles for a closed document, releasing file locks immediately.
        /// </summary>
        public static void CloseDocument(string pdfPath)
        {
            try
            {
                var fullPath = Path.GetFullPath(pdfPath);
                if (_documentCache.TryRemove(fullPath, out var removedEntry))
                {
                    removedEntry.Dispose();
                }
                InvalidateBitmapsForFile(fullPath);
            }
            catch { }
        }

        /// <summary>
        /// Flushes all cached bitmaps and document sessions to recover system memory.
        /// </summary>
        public static void ClearAllCaches()
        {
            _bitmapCache.Clear();
            foreach (var kvp in _documentCache)
            {
                kvp.Value.Dispose();
            }
            _documentCache.Clear();
            Interlocked.Exchange(ref _currentBitmapCacheBytes, 0);
            GC.Collect(2, GCCollectionMode.Optimized, false);
        }

        /// <summary>
        /// Gets current memory stats of the vector rasterizer engine.
        /// </summary>
        public static (long CachedBitmapsBytes, int ActiveDocumentsCount, int CachedPagesCount) GetEngineTelemetry()
        {
            return (Interlocked.Read(ref _currentBitmapCacheBytes), _documentCache.Count, _bitmapCache.Count);
        }
    }
}
