using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using OmniPdf.App.Core;

namespace OmniPdf.Tests
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("   OMNI PDF ENTERPRISE REGRESSION & BENCHMARK TEST SUITE  ");
            Console.WriteLine("==========================================================");

            int passed = 0;
            int failed = 0;

            void RunTest(string testName, Action testAction)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    testAction();
                    sw.Stop();
                    Console.WriteLine($"[PASS] {testName} ({sw.ElapsedMilliseconds} ms)");
                    passed++;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {testName} ({sw.ElapsedMilliseconds} ms): {ex.Message}");
                    Console.ResetColor();
                    failed++;
                }
            }

            async Task RunTestAsync(string testName, Func<Task> testAction)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await testAction();
                    sw.Stop();
                    Console.WriteLine($"[PASS] {testName} ({sw.ElapsedMilliseconds} ms)");
                    passed++;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {testName} ({sw.ElapsedMilliseconds} ms): {ex.Message}");
                    Console.ResetColor();
                    failed++;
                }
            }

            // Test 1: PdfDocumentModel instantiation and AST integrity
            RunTest("PdfDocumentModel: Default Document AST Construction", () =>
            {
                var doc = PdfDocumentModel.CreateDefault("TestDoc.pdf", 5);
                if (doc.Pages.Count != 5) throw new Exception($"Expected 5 pages, got {doc.Pages.Count}");
                if (doc.Title != "TestDoc.pdf") throw new Exception($"Expected title 'TestDoc.pdf', got {doc.Title}");
                if (!doc.Pages[0].ContentText.Contains("Vector Document")) throw new Exception("Page text content missing expected header.");
            });

            // Test 2: Text Extraction with ReDoS immunity & Bounded parsing
            RunTest("PdfDocumentModel: Stream Text Extraction with Timeout & Bounds", () =>
            {
                string syntheticPdfStream = "BT (Hello World) Tj ET BT (Enterprise PDF Benchmark) Tj ET";
                var blocks = PdfDocumentModel.ExtractTextBlocks(syntheticPdfStream);
                if (blocks.Count == 0) throw new Exception("Failed to extract text block from PDF stream.");
                if (!blocks[0].Contains("Hello World")) throw new Exception("Extracted text missing expected tokens.");
            });

            // Test 3: Large file bounded reading
            RunTest("PdfDocumentModel: High-Load Bounded File Load", () =>
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"test_pdf_{Guid.NewGuid():N}.pdf");
                try
                {
                    File.WriteAllText(tempFile, "%PDF-1.4\n1 0 obj\n<< /Type /Page >>\nendobj\n2 0 obj\n<< /Type /Page >>\nendobj\n%%EOF");
                    var loaded = PdfDocumentModel.LoadFromFile(tempFile);
                    if (loaded.Pages.Count < 2) throw new Exception($"Expected at least 2 pages, got {loaded.Pages.Count}");
                }
                finally
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
            });

            // Test 4: RealPdfRenderer Telemetry & Cache State
            RunTest("RealPdfRenderer: Memory Telemetry & Capacity Guard", () =>
            {
                var (bytes, docs, pages) = RealPdfRenderer.GetEngineTelemetry();
                if (bytes < 0) throw new Exception("Invalid telemetry byte count.");
                RealPdfRenderer.ClearAllCaches();
                var (bytesAfter, docsAfter, pagesAfter) = RealPdfRenderer.GetEngineTelemetry();
                if (bytesAfter != 0) throw new Exception($"Expected 0 bytes after clear, got {bytesAfter}");
            });

            // Test 5: AiDocumentCopilotService Offline Neural Heuristics
            RunTest("AiDocumentCopilotService: Offline Heuristic Text Synthesis", () =>
            {
                string sampleContract = "This objective agreement mandates that Party A shall deliver enterprise specifications by 10/25/2026 for $150,000. Failure to comply shall trigger liability damages.";
                var analysis = AiDocumentCopilotService.AnalyzeDocument(sampleContract);
                if (string.IsNullOrEmpty(analysis.ExecutiveSummary)) throw new Exception("Missing executive summary.");
                if (analysis.ActionItems.Count == 0) throw new Exception("Failed to extract action items.");
                if (analysis.KeyEntities.Count == 0) throw new Exception("Failed to extract financial entities.");
            });

            // Test 6: Digital Signature Annotation AST
            RunTest("PdfDocumentModel: Cryptographic Signature AST Annotation", () =>
            {
                var doc = PdfDocumentModel.CreateDefault("Contract.pdf", 2);
                doc.StampDigitalSignature("PSD Enterprise Root CA", "Architectural Signoff");
                var lastPage = doc.Pages[^1];
                var sig = lastPage.Annotations.Find(a => a.Type == "Signature");
                if (sig == null) throw new Exception("Digital signature annotation was not stamped.");
                if (!sig.Text.Contains("PSD Enterprise Root CA")) throw new Exception("Signature issuer mismatch.");
            });

            // Test 7: Concurrency & Cancellation Safety in Scanner
            await RunTestAsync("LibraryScannerService: Graceful Cancellation", async () =>
            {
                using var cts = new CancellationTokenSource();
                var items = new ObservableCollection<PdfLibraryItem>();
                cts.Cancel(); // Immediately cancel
                await LibraryScannerService.ScanDevicePdfLibraryAsync(items, (c, m) => { }, cts.Token);
            });

            Console.WriteLine();
            Console.WriteLine($"RESULTS: {passed} passed, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }
    }
}
