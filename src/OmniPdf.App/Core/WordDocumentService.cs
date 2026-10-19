using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OmniPdf.App.Core
{
    public static class WordDocumentService
    {
        public static void LoadDocument(string filePath, RichTextBox rtb)
        {
            if (!File.Exists(filePath)) return;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            rtb.Document.Blocks.Clear();

            try
            {
                if (ext == ".rtf")
                {
                    using var fs = File.OpenRead(filePath);
                    var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                    range.Load(fs, DataFormats.Rtf);
                }
                else if (ext == ".txt")
                {
                    string text = File.ReadAllText(filePath, Encoding.UTF8);
                    LoadPlainTextIntoFlowDocument(text, rtb.Document);
                }
                else if (ext == ".docx")
                {
                    LoadDocxIntoFlowDocument(filePath, rtb.Document);
                }
                else if (ext == ".md" || ext == ".markdown")
                {
                    string mdText = File.ReadAllText(filePath, Encoding.UTF8);
                    LoadMarkdownIntoFlowDocument(mdText, rtb.Document);
                }
                else if (ext == ".html" || ext == ".htm")
                {
                    string html = File.ReadAllText(filePath, Encoding.UTF8);
                    string stripped = Regex.Replace(html, "<.*?>", string.Empty);
                    LoadPlainTextIntoFlowDocument(stripped, rtb.Document);
                }
                else if (ext == ".pdf")
                {
                    var doc = PdfDocumentModel.LoadFromFile(filePath);
                    var sb = new StringBuilder();
                    for (int i = 0; i < doc.Pages.Count; i++)
                    {
                        sb.AppendLine($"# Page {i + 1}");
                        sb.AppendLine(doc.Pages[i].ContentText);
                        sb.AppendLine();
                    }
                    LoadMarkdownIntoFlowDocument(sb.ToString(), rtb.Document);
                }
            }
            catch (Exception ex)
            {
                rtb.Document.Blocks.Clear();
                rtb.Document.Blocks.Add(new Paragraph(new Run($"Error loading document: {ex.Message}")) { Foreground = Brushes.Red });
            }
        }

        public static void SaveDocument(string filePath, RichTextBox rtb)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);

            if (ext == ".rtf")
            {
                using var fs = File.Create(filePath);
                range.Save(fs, DataFormats.Rtf);
            }
            else if (ext == ".txt")
            {
                File.WriteAllText(filePath, range.Text, Encoding.UTF8);
            }
            else if (ext == ".docx")
            {
                SaveFlowDocumentAsDocx(filePath, rtb.Document);
            }
            else if (ext == ".md" || ext == ".markdown")
            {
                File.WriteAllText(filePath, range.Text, Encoding.UTF8);
            }
            else if (ext == ".html" || ext == ".htm")
            {
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Document</title>");
                sb.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;line-height:1.6;margin:40px;color:#222;}</style></head><body>");
                foreach (var block in rtb.Document.Blocks)
                {
                    if (block is Paragraph p)
                    {
                        var pRange = new TextRange(p.ContentStart, p.ContentEnd);
                        sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(pRange.Text.Trim())}</p>");
                    }
                }
                sb.AppendLine("</body></html>");
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            else if (ext == ".pdf")
            {
                PdfDocumentModel.CompileTextToPdf(range.Text, filePath, 12.0, "");
            }
        }

        public static void CreateNewDocument(RichTextBox rtb, string title = "Untitled Document")
        {
            rtb.Document.Blocks.Clear();
            rtb.Document.PagePadding = new Thickness(48);
            
            var titlePara = new Paragraph(new Run(title))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            rtb.Document.Blocks.Add(titlePara);

            var bodyPara = new Paragraph(new Run("Type your document content here or import from Word (.docx), RTF, PDF, Markdown, PSD, or HTML. PSD PDF Studio provides real-time formatting, tables, images, and high-performance vector export."))
            {
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            rtb.Document.Blocks.Add(bodyPara);
        }

        public static void InsertTable(RichTextBox rtb, int rows = 3, int cols = 3)
        {
            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 10, 0, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };

            for (int i = 0; i < cols; i++)
            {
                table.Columns.Add(new TableColumn());
            }

            var rowGroup = new TableRowGroup();
            for (int r = 0; r < rows; r++)
            {
                var row = new TableRow();
                for (int c = 0; c < cols; c++)
                {
                    var cell = new TableCell(new Paragraph(new Run(r == 0 ? $"Column {c + 1}" : $"Data {r},{c + 1}")))
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 6, 8, 6),
                        Background = r == 0 ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : Brushes.White
                    };
                    if (r == 0) cell.FontWeight = FontWeights.SemiBold;
                    row.Cells.Add(cell);
                }
                rowGroup.Rows.Add(row);
            }
            table.RowGroups.Add(rowGroup);

            if (rtb.CaretPosition?.Paragraph != null)
            {
                rtb.Document.Blocks.InsertAfter(rtb.CaretPosition.Paragraph, table);
            }
            else
            {
                rtb.Document.Blocks.Add(table);
            }
        }

        public static void InsertImage(RichTextBox rtb, string imagePath)
        {
            if (!File.Exists(imagePath)) return;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();

                var img = new Image
                {
                    Source = bitmap,
                    MaxWidth = 620,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var container = new BlockUIContainer(img);
                if (rtb.CaretPosition?.Paragraph != null)
                {
                    rtb.Document.Blocks.InsertAfter(rtb.CaretPosition.Paragraph, container);
                }
                else
                {
                    rtb.Document.Blocks.Add(container);
                }
            }
            catch { }
        }

        public static void InsertHorizontalRule(RichTextBox rtb)
        {
            var border = new Border
            {
                Height = 1.5,
                Background = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 12, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var container = new BlockUIContainer(border);
            if (rtb.CaretPosition?.Paragraph != null)
            {
                rtb.Document.Blocks.InsertAfter(rtb.CaretPosition.Paragraph, container);
            }
            else
            {
                rtb.Document.Blocks.Add(container);
            }
        }

        public static void InsertDateTime(RichTextBox rtb)
        {
            string dt = DateTime.Now.ToString("dddd, MMMM dd, yyyy - hh:mm tt");
            rtb.Selection.Text = dt;
        }

        public static void SetPageMargins(RichTextBox rtb, double margin)
        {
            rtb.Document.PagePadding = new Thickness(margin);
        }

        public static void PrintDocument(RichTextBox rtb, string docTitle = "PSD Word Document")
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                IDocumentPaginatorSource dps = rtb.Document;
                printDialog.PrintDocument(dps.DocumentPaginator, docTitle);
            }
        }

        public static (int Words, int Characters, int Paragraphs, int ReadingMinutes) GetDocumentStatistics(RichTextBox rtb)
        {
            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            string text = range.Text;

            int chars = text.Length;
            var wordsArray = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int words = wordsArray.Length;
            int paragraphs = rtb.Document.Blocks.Count;
            int readingMins = Math.Max(1, (int)Math.Ceiling(words / 200.0));

            return (words, chars, paragraphs, readingMins);
        }

        public class DetailedWordStatistics
        {
            public int Words { get; set; }
            public int CharactersNoSpaces { get; set; }
            public int CharactersWithSpaces { get; set; }
            public int Paragraphs { get; set; }
            public int Lines { get; set; }
            public int EstimatedPages { get; set; }
            public int ReadingTimeMinutes { get; set; }
            public int SpeakingTimeMinutes { get; set; }
            public double FleschReadingEase { get; set; }
            public string ReadabilityLabel { get; set; } = "Standard / Plain English";
            public string GradeLevel { get; set; } = "8th - 9th Grade";
        }

        public static DetailedWordStatistics GetDetailedDocumentStatistics(RichTextBox rtb)
        {
            var stats = new DetailedWordStatistics();
            if (rtb == null) return stats;

            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            string text = range.Text;

            stats.CharactersWithSpaces = text.Length;
            stats.CharactersNoSpaces = Regex.Replace(text, @"\s", "").Length;

            var wordsArray = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            stats.Words = wordsArray.Length;

            stats.Paragraphs = Math.Max(1, rtb.Document.Blocks.Count);
            var linesArray = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            stats.Lines = Math.Max(stats.Paragraphs, linesArray.Length);

            stats.EstimatedPages = Math.Max(1, (int)Math.Ceiling(stats.Words / 350.0));
            stats.ReadingTimeMinutes = Math.Max(1, (int)Math.Ceiling(stats.Words / 200.0));
            stats.SpeakingTimeMinutes = Math.Max(1, (int)Math.Ceiling(stats.Words / 130.0));

            var sentences = Regex.Split(text, @"[.!?]+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            int sentenceCount = Math.Max(1, sentences.Length);

            int totalSyllables = 0;
            foreach (var word in wordsArray)
            {
                string clean = Regex.Replace(word.ToLowerInvariant(), @"[^a-z]", "");
                if (clean.Length <= 3) { totalSyllables += 1; continue; }
                int syl = Regex.Matches(clean, @"[aeiouy]{1,2}").Count;
                if (clean.EndsWith("e") && !clean.EndsWith("le")) syl = Math.Max(1, syl - 1);
                totalSyllables += Math.Max(1, syl);
            }

            if (stats.Words > 0)
            {
                double score = 206.835 - (1.015 * ((double)stats.Words / sentenceCount)) - (84.6 * ((double)totalSyllables / stats.Words));
                stats.FleschReadingEase = Math.Round(Math.Clamp(score, 0, 100), 1);

                if (stats.FleschReadingEase >= 90) { stats.ReadabilityLabel = "Very Easy (5th Grade)"; stats.GradeLevel = "5th Grade"; }
                else if (stats.FleschReadingEase >= 80) { stats.ReadabilityLabel = "Easy (6th Grade)"; stats.GradeLevel = "6th Grade"; }
                else if (stats.FleschReadingEase >= 70) { stats.ReadabilityLabel = "Fairly Easy (7th Grade)"; stats.GradeLevel = "7th Grade"; }
                else if (stats.FleschReadingEase >= 60) { stats.ReadabilityLabel = "Standard / Plain English"; stats.GradeLevel = "8th - 9th Grade"; }
                else if (stats.FleschReadingEase >= 50) { stats.ReadabilityLabel = "Fairly Difficult"; stats.GradeLevel = "10th - 12th Grade"; }
                else if (stats.FleschReadingEase >= 30) { stats.ReadabilityLabel = "Difficult (College)"; stats.GradeLevel = "College Level"; }
                else { stats.ReadabilityLabel = "Very Confusing (Graduate Level)"; stats.GradeLevel = "Graduate School"; }
            }

            return stats;
        }

        public static void ChangeSelectionCase(RichTextBox rtb, string caseType)
        {
            if (rtb == null || rtb.Selection.IsEmpty) return;
            string sel = rtb.Selection.Text;
            if (string.IsNullOrEmpty(sel)) return;

            string result;
            switch (caseType.ToLowerInvariant())
            {
                case "upper":
                    result = sel.ToUpperInvariant();
                    break;
                case "lower":
                    result = sel.ToLowerInvariant();
                    break;
                case "title":
                    result = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sel.ToLowerInvariant());
                    break;
                case "sentence":
                    var sb = new StringBuilder();
                    bool startNew = true;
                    foreach (char c in sel)
                    {
                        if (startNew && char.IsLetter(c))
                        {
                            sb.Append(char.ToUpperInvariant(c));
                            startNew = false;
                        }
                        else
                        {
                            sb.Append(char.ToLowerInvariant(c));
                        }
                        if (c == '.' || c == '!' || c == '?')
                        {
                            startNew = true;
                        }
                    }
                    result = sb.ToString();
                    break;
                case "toggle":
                    var toggleSb = new StringBuilder();
                    foreach (char c in sel)
                    {
                        if (char.IsUpper(c)) toggleSb.Append(char.ToLowerInvariant(c));
                        else if (char.IsLower(c)) toggleSb.Append(char.ToUpperInvariant(c));
                        else toggleSb.Append(c);
                    }
                    result = toggleSb.ToString();
                    break;
                default:
                    result = sel;
                    break;
            }
            rtb.Selection.Text = result;
        }

        public static void LoadTemplate(RichTextBox rtb, string templateType)
        {
            rtb.Document.Blocks.Clear();
            rtb.Document.PagePadding = new Thickness(48);

            switch (templateType)
            {
                case "Project Charter":
                    CreateProjectCharterTemplate(rtb.Document);
                    break;
                case "Resume":
                    CreateResumeTemplate(rtb.Document);
                    break;
                case "Business Proposal":
                    CreateBusinessProposalTemplate(rtb.Document);
                    break;
                case "NDA":
                    CreateNdaTemplate(rtb.Document);
                    break;
                case "Meeting Minutes":
                    CreateMeetingMinutesTemplate(rtb.Document);
                    break;
                default:
                    CreateNewDocument(rtb, "Untitled Document");
                    break;
            }
        }

        private static void CreateProjectCharterTemplate(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph(new Run("PROJECT CHARTER & STRATEGY SPECIFICATION")) { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Margin = new Thickness(0, 0, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Document Version: 1.0 | Status: Approved | Date: " + DateTime.Now.ToString("MMMM dd, yyyy"))) { FontSize = 11, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 16) });

            doc.Blocks.Add(new Paragraph(new Run("1. Executive Summary")) { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 8, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("This document outlines the strategic mandate, scope, resource allocations, and critical path milestones for the high-priority enterprise initiative. The objective is to achieve 100% feature parity with premier market leaders while guaranteeing sub-128MB RAM operation.")) { FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });

            doc.Blocks.Add(new Paragraph(new Run("2. Core Deliverables & KPIs")) { FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 8, 0, 4) });
            var list = new List();
            list.ListItems.Add(new ListItem(new Paragraph(new Run("Deploy native DirectWrite/DirectX vector rendering pipeline."))));
            list.ListItems.Add(new ListItem(new Paragraph(new Run("Implement OpenXML ISO/IEC 29500 bidirectional parser."))));
            list.ListItems.Add(new ListItem(new Paragraph(new Run("Guarantee zero external runtime dependencies on target Windows hosts."))));
            doc.Blocks.Add(list);
        }

        private static void CreateResumeTemplate(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph(new Run("ALEXANDRA CHEN, PMP")) { FontSize = 24, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Margin = new Thickness(0, 0, 0, 2) });
            doc.Blocks.Add(new Paragraph(new Run("Principal Enterprise Architect & Product Director | alexandra.chen@psdsoftware.io | (555) 019-2834 | Seattle, WA")) { FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 16) });

            doc.Blocks.Add(new Paragraph(new Run("EXECUTIVE SUMMARY")) { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 8, 0, 2) });
            doc.Blocks.Add(new Paragraph(new Run("Accomplished engineering leader with 14+ years spearheading desktop software ecosystems, cloud document platforms, and high-throughput vector rendering pipelines across Fortune 500 enterprises.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 12) });

            doc.Blocks.Add(new Paragraph(new Run("PROFESSIONAL EXPERIENCE")) { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)), Margin = new Thickness(0, 8, 0, 2) });
            doc.Blocks.Add(new Paragraph(new Run("Vice President of Engineering | Global Document Technologies (2020 – Present)")) { FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 4, 0, 2) });
            doc.Blocks.Add(new Paragraph(new Run("• Directed 120-person distributed engineering department producing enterprise desktop document processors utilized by 4.2M daily active users.\n• Reduced application cold boot latency by 68% and optimized memory consumption from 480MB to 72MB.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });
        }

        private static void CreateBusinessProposalTemplate(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph(new Run("STRATEGIC ENTERPRISE PROPOSAL")) { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Margin = new Thickness(0, 0, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Prepared for: Strategic Enterprise Partner | Prepared by: PSD Software Studio Enterprise Services")) { FontSize = 11, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 16) });

            doc.Blocks.Add(new Paragraph(new Run("1. Statement of Need")) { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 6, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Enterprise corporate workforces require secure, self-contained document editing and PDF inspection capabilities that run completely offline without transmitting proprietary documents over unauthorized third-party cloud infrastructure.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });

            doc.Blocks.Add(new Paragraph(new Run("2. Proposed Solution")) { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 6, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Deployment of PSD PDF Studio & Viewer Enterprise with centralized IT distribution via MSI/Intune, enforced AES-256 cryptographic sandboxing, and full OpenXML Word (.docx) document authorship.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });
        }

        private static void CreateNdaTemplate(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph(new Run("MUTUAL NON-DISCLOSURE AGREEMENT")) { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Margin = new Thickness(0, 0, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Effective Date: " + DateTime.Now.ToString("MMMM dd, yyyy") + " | Governing Jurisdiction: Delaware, USA")) { FontSize = 10.5, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 16) });

            doc.Blocks.Add(new Paragraph(new Run("1. Definition of Confidential Information")) { FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 6, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("All technical data, source code, algorithmic formulations, product specifications, customer inventories, and operational roadmaps disclosed by either party shall be strictly preserved as proprietary Confidential Information.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });

            doc.Blocks.Add(new Paragraph(new Run("2. Obligations of Non-Disclosure")) { FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 6, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("The receiving party covenants to protect disclosed materials with no less than reasonable degree of care and shall not disclose such materials to any third party without explicit prior written authorization.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });
        }

        private static void CreateMeetingMinutesTemplate(FlowDocument doc)
        {
            doc.Blocks.Add(new Paragraph(new Run("EXECUTIVE LEADERSHIP MEETING MINUTES")) { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Margin = new Thickness(0, 0, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Date: " + DateTime.Now.ToString("MMMM dd, yyyy") + " | Time: 10:00 AM – 11:30 AM EST | Chair: Product Owner")) { FontSize = 10.5, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 16) });

            doc.Blocks.Add(new Paragraph(new Run("Attendees")) { FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 6, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("• Product Owner (Chair)\n• Core DirectX Engine Lead\n• Word Suite Processing Specialist\n• Enterprise Security & Compliance Architect\n• QA Automation & Release Director")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });

            doc.Blocks.Add(new Paragraph(new Run("Key Decisions & Action Items")) { FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 6, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("1. Release v2.1.0 certified for general availability across all 64-bit Windows environments.\n2. Inno Setup and WiX MSI distribution pipelines automated with zero external dependencies.\n3. Continuous scrolling and high-DPI area snapshots verified across 100% of test matrices.")) { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Margin = new Thickness(0, 0, 0, 10) });
        }

        private static void LoadPlainTextIntoFlowDocument(string text, FlowDocument doc)
        {
            doc.Blocks.Clear();
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var p = new Paragraph(new Run(string.IsNullOrEmpty(line) ? " " : line))
                {
                    Margin = new Thickness(0, 2, 0, 2),
                    FontSize = 13,
                    FontFamily = new FontFamily("Segoe UI")
                };
                doc.Blocks.Add(p);
            }
        }

        private static void LoadMarkdownIntoFlowDocument(string mdText, FlowDocument doc)
        {
            doc.Blocks.Clear();
            var lines = mdText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("# "))
                {
                    doc.Blocks.Add(new Paragraph(new Run(trimmed.Substring(2)))
                    {
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                        Margin = new Thickness(0, 10, 0, 4)
                    });
                }
                else if (trimmed.StartsWith("## "))
                {
                    doc.Blocks.Add(new Paragraph(new Run(trimmed.Substring(3)))
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                        Margin = new Thickness(0, 8, 0, 3)
                    });
                }
                else if (trimmed.StartsWith("### "))
                {
                    doc.Blocks.Add(new Paragraph(new Run(trimmed.Substring(4)))
                    {
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 6, 0, 2)
                    });
                }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    doc.Blocks.Add(new Paragraph(new Run("•  " + trimmed.Substring(2)))
                    {
                        Margin = new Thickness(16, 2, 0, 2),
                        FontSize = 13
                    });
                }
                else
                {
                    doc.Blocks.Add(new Paragraph(new Run(string.IsNullOrEmpty(trimmed) ? " " : trimmed))
                    {
                        Margin = new Thickness(0, 2, 0, 2),
                        FontSize = 13
                    });
                }
            }
        }

        private static void LoadDocxIntoFlowDocument(string docxPath, FlowDocument doc)
        {
            doc.Blocks.Clear();
            using var zip = ZipFile.OpenRead(docxPath);
            var entry = zip.GetEntry("word/document.xml");
            if (entry == null) return;

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            string xml = reader.ReadToEnd();

            // Match paragraphs <w:p>
            var pMatches = Regex.Matches(xml, @"<w:p\b[^>]*>(.*?)</w:p>", RegexOptions.Singleline);
            foreach (Match pm in pMatches)
            {
                var sb = new StringBuilder();
                var tMatches = Regex.Matches(pm.Groups[1].Value, @"<w:t\b[^>]*>(.*?)</w:t>", RegexOptions.Singleline);
                foreach (Match tm in tMatches)
                {
                    sb.Append(System.Net.WebUtility.HtmlDecode(tm.Groups[1].Value));
                }

                string line = sb.ToString();
                var p = new Paragraph(new Run(string.IsNullOrEmpty(line) ? " " : line))
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    FontSize = 13,
                    FontFamily = new FontFamily("Segoe UI")
                };
                doc.Blocks.Add(p);
            }
        }

        private static void SaveFlowDocumentAsDocx(string docxPath, FlowDocument doc)
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);

            using var zip = ZipFile.Open(docxPath, ZipArchiveMode.Create);

            // 1. [Content_Types].xml
            var ctEntry = zip.CreateEntry("[Content_Types].xml");
            using (var sw = new StreamWriter(ctEntry.Open(), Encoding.UTF8))
            {
                sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                         "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                         "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                         "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                         "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                         "</Types>");
            }

            // 2. _rels/.rels
            var relEntry = zip.CreateEntry("_rels/.rels");
            using (var sw = new StreamWriter(relEntry.Open(), Encoding.UTF8))
            {
                sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                         "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                         "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                         "</Relationships>");
            }

            // 3. word/document.xml
            var docEntry = zip.CreateEntry("word/document.xml");
            using (var sw = new StreamWriter(docEntry.Open(), Encoding.UTF8))
            {
                sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                         "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                         "<w:body>");

                foreach (var block in doc.Blocks)
                {
                    if (block is Paragraph p)
                    {
                        var range = new TextRange(p.ContentStart, p.ContentEnd);
                        string text = range.Text.Trim();
                        string safeXml = System.Security.SecurityElement.Escape(text);
                        sw.Write($"<w:p><w:r><w:t>{safeXml}</w:t></w:r></w:p>");
                    }
                }

                sw.Write("</w:body></w:document>");
            }
        }
    }
}
