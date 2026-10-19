using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfDocGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            string rootDir = @"P:\prabhu\projects\git\Omni PDF Desktop";
            string docsDir = Path.Combine(rootDir, "docs");
            string distDir = Path.Combine(rootDir, @"build\dist");

            Directory.CreateDirectory(docsDir);
            Directory.CreateDirectory(distDir);

            Console.WriteLine("==========================================================");
            Console.WriteLine("    PSD PDF STUDIO - OFFICIAL PDF DOCUMENTATION COMPILER  ");
            Console.WriteLine("==========================================================");

            // Document 1: Hardware Requirements
            string hwPdf = Path.Combine(docsDir, "PSD_PDF_Studio_Hardware_Requirements.pdf");
            string hwText = GetHardwareRequirementsText();
            CompileDocumentPdf(hwText, hwPdf, "PSD PDF STUDIO & VIEWER - HARDWARE REQUIREMENTS SPECIFICATION", "SPEC-HW-300");
            File.Copy(hwPdf, Path.Combine(distDir, "PSD_PDF_Studio_Hardware_Requirements.pdf"), true);
            Console.WriteLine($"[1/2] Generated: {hwPdf}");

            // Document 2: User Manual & Help Guide
            string helpPdf = Path.Combine(docsDir, "PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf");
            string helpText = GetUserManualText();
            CompileDocumentPdf(helpText, helpPdf, "PSD PDF STUDIO & VIEWER - OFFICIAL USER MANUAL & HELP GUIDE", "MANUAL-PSD-300");
            File.Copy(helpPdf, Path.Combine(distDir, "PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf"), true);
            Console.WriteLine($"[2/2] Generated: {helpPdf}");

            Console.WriteLine("Documentation PDF generation completed successfully!");
        }

        static void CompileDocumentPdf(string text, string outputPath, string headerTitle, string docId)
        {
            const double pageWidth = 612.0;
            const double pageHeight = 792.0;
            const double margin = 50.0;
            const double fontSize = 10.0;
            const double lineHeight = 14.5;
            const int charsPerLine = 84;
            const int linesPerPage = 42;

            var lines = new List<string>();
            var paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var p in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(p))
                {
                    lines.Add("");
                    continue;
                }
                int cur = 0;
                while (cur < p.Length)
                {
                    int take = Math.Min(charsPerLine, p.Length - cur);
                    if (cur + take < p.Length)
                    {
                        int lastSpace = p.LastIndexOf(' ', cur + take, take);
                        if (lastSpace > cur)
                        {
                            take = lastSpace - cur;
                        }
                    }
                    lines.Add(p.Substring(cur, take));
                    cur += take;
                    while (cur < p.Length && p[cur] == ' ') cur++;
                }
            }

            int totalPages = Math.Max(1, (int)Math.Ceiling(lines.Count / (double)linesPerPage));

            using var sw = new StreamWriter(outputPath, false, Encoding.ASCII);
            sw.WriteLine("%PDF-1.7");
            sw.WriteLine("%\xE2\xE3\xCF\xD3");
            sw.WriteLine("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj");

            sw.Write("2 0 obj\n<< /Type /Pages /Count " + totalPages + " /Kids [ ");
            for (int i = 0; i < totalPages; i++) sw.Write((5 + i * 2) + " 0 R ");
            sw.WriteLine("] >>\nendobj");

            sw.WriteLine("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj");
            sw.WriteLine("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj");

            int objId = 5;
            for (int p = 0; p < totalPages; p++)
            {
                int pageObj = objId++;
                int contentObj = objId++;

                sw.WriteLine($"{pageObj} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObj} 0 R >>\nendobj");

                var cs = new StringBuilder();

                // Decorative Watermark / Accent Bar
                cs.AppendLine("q 0.01 0.52 0.78 rg 50 750 512 2 re f Q");
                cs.AppendLine("BT /F2 8.5 Tf 0.15 0.15 0.15 rg 1 0 0 1 50 756 Tm (" + EscapePdf(headerTitle) + ") Tj");
                cs.AppendLine("/F1 8.5 Tf 0.4 0.4 0.4 rg 1 0 0 1 450 756 Tm (ID: " + EscapePdf(docId) + ") Tj ET");

                // Page body
                cs.AppendLine("BT");
                double y = pageHeight - margin - 32.0;

                int startLine = p * linesPerPage;
                int endLine = Math.Min(lines.Count, startLine + linesPerPage);

                for (int l = startLine; l < endLine; l++)
                {
                    string lineText = lines[l];
                    if (lineText.StartsWith("SECTION ") || lineText.StartsWith("==="))
                    {
                        cs.AppendLine("/F2 11 Tf 0.01 0.52 0.78 rg 1 0 0 1 50 " + y.ToString("0.0") + " Tm (" + EscapePdf(lineText) + ") Tj");
                    }
                    else if (lineText.StartsWith("2.") || lineText.StartsWith("3.") || lineText.StartsWith("4.") || lineText.StartsWith("5.") || lineText.StartsWith("6.") || lineText.StartsWith("7.") || lineText.StartsWith("8."))
                    {
                        cs.AppendLine("/F2 10 Tf 0.1 0.15 0.2 rg 1 0 0 1 50 " + y.ToString("0.0") + " Tm (" + EscapePdf(lineText) + ") Tj");
                    }
                    else
                    {
                        cs.AppendLine("/F1 " + fontSize.ToString("0.0") + " Tf 0.15 0.15 0.15 rg 1 0 0 1 50 " + y.ToString("0.0") + " Tm (" + EscapePdf(lineText) + ") Tj");
                    }
                    y -= lineHeight;
                }

                // Footer Bar
                cs.AppendLine("ET");
                cs.AppendLine("q 0.85 0.85 0.88 rg 50 40 512 1 re f Q");
                cs.AppendLine("BT /F1 8.5 Tf 0.45 0.45 0.45 rg 1 0 0 1 50 28 Tm (PSD PDF Studio and Viewer Enterprise Edition | MIT License | Confidential) Tj");
                cs.AppendLine("/F2 8.5 Tf 0.2 0.2 0.2 rg 1 0 0 1 480 28 Tm (Page " + (p + 1) + " of " + totalPages + ") Tj ET");

                string csStr = cs.ToString();
                byte[] csBytes = Encoding.ASCII.GetBytes(csStr);
                sw.WriteLine($"{contentObj} 0 obj\n<< /Length {csBytes.Length} >>\nstream\n{csStr}endstream\nendobj");
            }

            long xrefPos = sw.BaseStream.Position;
            sw.WriteLine("xref\n0 " + objId + "\n0000000000 65535 f ");
            for (int i = 1; i < objId; i++)
            {
                sw.WriteLine($"{(15 + i * 92):D10} 00000 n ");
            }
            sw.WriteLine($"trailer\n<< /Size {objId} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");
        }

        static string EscapePdf(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        static string GetHardwareRequirementsText()
        {
            return @"SECTION 1: SYSTEM ARCHITECTURE & EXECUTIVE OVERVIEW
========================================================================================
Product Name: PSD PDF Studio and Viewer Enterprise Edition (v3.0.0-ENTERPRISE-FLAGSHIP)
Vendor: PSD Software Studio / Prabhu Kannan
Licensing: MIT Open Source License (Permissive Enterprise & Commercial Usage)
Operating Guarantee: 100% Offline Air-Gapped Operation | Zero External Cloud Leakage

PSD PDF Studio and Viewer is a zero-dependency desktop productivity workstation engineered
to deliver ultra-fast vector PDF viewing, native OpenXML Word (.docx) editing, cryptographic
e-signatures, and local CPU-based neural document intelligence.

The architecture is strictly divided into two verified targets:
1. Native Windows 10/11 x64 Workstations, Ultrabooks, Surface Tablets, and Enterprise Servers.
2. Ultra-Lightweight Android Mobile Devices (specifically optimized for Qualcomm Snapdragon 625
   architectures running Android 6.0 Marshmallow through Android 14+).

SECTION 2: WINDOWS DESKTOP & ENTERPRISE SERVER HARDWARE REQUIREMENTS
========================================================================================

2.1 MINIMUM SYSTEM HARDWARE (CLIENT WORKSTATION)
- Processor (CPU): 1.0 GHz 64-bit (x64) Intel Core 2 Duo / AMD Athlon 64 or newer.
- System RAM: 512 MB Physical RAM (Application strictly clamps working set under 150MB).
- Disk Storage: 160 MB available local disk space for full standalone binary and assets.
- Display Resolution: 1024 x 768 pixels with 16-bit color depth (High-DPI responsive).
- Operating Systems: Windows 10 Version 1903 (Build 18362) or newer (Windows 10, 11, x64).
- Runtime Dependencies: ZERO. Self-contained .NET 8 runtime, DirectX drivers, and WinRT APIs.

2.2 RECOMMENDED ENTERPRISE HARDWARE (HEAVY MULTI-PAGE BLUEPRINTS & CAD)
- Processor (CPU): Quad-Core Intel Core i5/i7/i9 (8th Gen+) or AMD Ryzen 5/7/9 (3.0 GHz+).
- System RAM: 2.0 GB Physical RAM or higher.
- Storage Device: 500 MB Solid State Drive (NVMe SSD recommended for instant library indexing).
- Graphics Hardware: DirectX 11 / Direct3D 12 compliant GPU for 120 FPS hardware rasterization.
- Display Resolution: 1920 x 1080 (Full HD) up to 3840 x 2160 (4K UHD) with 100%-250% DPI scaling.
- Enterprise Operating Systems: Windows 11 Enterprise/Pro, Windows 10 Enterprise LTSC,
  Windows Server 2019, Windows Server 2022, Windows Server 2025.

2.3 MEMORY CEILINGS & DIRECTX ACCELERATION BENCHMARKS
- 64MB LRU Tile Rasterizer Cache: Viewport tiles are cached in high-speed RAM with strict
  byte-eviction. Continuous scrolling across 1,000+ page documents will never cause memory growth.
- Native WinRT DirectWrite Acceleration: Native vector glyph rasterization runs directly on
  hardware, delivering fluid 60 to 120 FPS navigation with zero frame drops or visual tearing.

SECTION 3: ANDROID MOBILE HARDWARE REQUIREMENTS (QUALCOMM SNAPDRAGON 625)
========================================================================================

3.1 TARGET HARDWARE PROFILE
- System-on-Chip (SoC): Qualcomm MSM8953 Snapdragon 625 (14nm FinFET technology).
- CPU Core Architecture: Octa-core 2.0 GHz Cortex-A53.
- GPU: Qualcomm Adreno 506.
- Benchmark Reference Device: Xiaomi Redmi Note 4 (3GB RAM / 32GB Storage).

3.2 MOBILE OPERATING SYSTEM COMPATIBILITY
- Minimum API Level: Android 6.0 Marshmallow (API 23).
- Target API Level: Android 14.0 Upside Down Cake (API 34) and Android 15 (API 35).
- Binary ABI Architectures: Dual compiled native libraries:
  * armeabi-v7a (32-bit ARM with NEON SIMD vector extensions).
  * arm64-v8a (64-bit ARMv8-A architecture).

3.3 MOBILE HEAP CEILING & CAMERA SCANNING
- Dalvik / ART Heap Cap: strictly constrained to < 45 MB operational heap.
- Camera Scanner: Compatible with standard CMOS sensor modules for document scanning,
  quad-corner perspective trapezoid correction, and high-contrast Otsu binarization.

SECTION 4: ENTERPRISE NETWORK, SECURITY & PACKAGING TELEMETRY
========================================================================================
- Network Connectivity: ZERO REQUIREMENT. All cryptographic operations, OCR layers, text converters,
  and AI Copilot heuristics execute locally on the host CPU. No telemetry packets are transmitted.
- Cryptographic Standards: AES-256 (CBC mode) document permissions, SHA-256 integrity digests,
  PKCS#7 detached digital signatures, and RFC 3161 compliant timestamp validation.
- Packaging Delivery Modes:
  1. Standalone Executable: psd_pdf.exe (78.3 MB, portable double-click).
  2. Enterprise Portable: PSD-PDF-Studio-Portable.zip (144.9 MB).
  3. Inno Setup Client Installer: PSD-PDF-Studio-Setup.exe (143.6 MB).
  4. WiX Enterprise MSI: PSD-PDF-Studio-Enterprise.msi (71.6 MB, msiexec /i /qn silent deployment).
  5. Android Universal Release APK: PSD-PDF-Studio-Release-universal.apk (21.1 KB).";
        }

        static string GetUserManualText()
        {
            return @"SECTION 1: WELCOME TO PSD PDF STUDIO & VIEWER ENTERPRISE
========================================================================================
Product Name: PSD PDF Studio and Viewer Enterprise Edition (v3.0.0-ENTERPRISE-FLAGSHIP)
Vendor: PSD Software Studio / Prabhu Kannan
License: MIT Open Source License (Permissive Commercial & Enterprise Deployment)
Official Project Repository: https://github.com/prabhususith/PSD_pdf_studio_and_viewer

PSD PDF Studio and Viewer is a modern, unified desktop document platform combining the precision
of professional vector PDF engineering suites with the rich authorship capabilities of Microsoft
Word, reinforced by air-gapped offline AI document intelligence and legally binding e-signatures.

1.1 PRIMARY NAVIGATION RAIL WORKSPACES
The left navigation rail (toggleable with F4) provides one-click switching between core workspaces:
1. Library Hub: Device-wide automated document indexer and search dashboard.
2. PDF Studio: High-performance vector reader with continuous multi-page vertical scrolling.
3. Word Suite: Full rich text document editor supporting .docx, RTF, Markdown, PSD, and HTML.
4. AI Copilot: Air-gapped offline neural document analyzer and Q&A engine.
5. e-Sign & Audit: Professional electronic signature capture and PKCS#7 certification.

SECTION 2: STEP-BY-STEP SETUP & INSTALLATION GUIDANCE
========================================================================================

2.1 PORTABLE ZERO-INSTALL METHOD (RECOMMENDED FOR USB DRIVES & RESTRICTED IT)
1. Download PSD-PDF-Studio-Portable.zip from the official GitHub Release portal.
2. Extract the ZIP archive to any directory (e.g., C:\Tools\PSDPdfStudio or P:\Portable).
3. Double-click psd_pdf.exe. The application launches instantly with zero prerequisite installs.

2.2 STANDARD WINDOWS INSTALLER (INNO SETUP)
1. Download PSD-PDF-Studio-Setup.exe.
2. Double-click to launch the modern installation wizard.
3. Select desired shortcuts (Desktop, Start Menu) and file associations (.pdf, .psd, .docx).
4. Click Install. Launch the program from your Desktop shortcut or Start Menu.

2.3 ENTERPRISE IT SILENT DEPLOYMENT (WIX MSI PACKAGE)
For system administrators managing Active Directory, Microsoft Intune, or SCCM:
- Command: msiexec.exe /i ""PSD-PDF-Studio-Enterprise.msi"" /qn /norestart
- The application will silently install to %ProgramFiles%\PSD PDF Studio and Viewer with zero prompts.

2.4 ANDROID UNIVERSAL APK INSTALLATION (API 23 TO 34+)
1. Download PSD-PDF-Studio-Release-universal.apk to your Android device or via USB file transfer.
2. In Android Settings, ensure 'Install Unknown Apps' is enabled for your file browser.
3. Tap the APK file and select 'Install'. Launch 'PSD PDF Studio & Viewer' from your app drawer.

SECTION 3: MASTERING THE PDF STUDIO & READING VIEWPORT
========================================================================================

3.1 CONTINUOUS MULTI-PAGE VERTICAL SCROLL
- Open any PDF document by pressing Ctrl+O or dragging and dropping a file into the window.
- Use your mouse scroll-wheel or touch gestures to scroll continuously across all document pages.
- The thumbnail strip on the left automatically tracks your scroll position in real time.

3.2 VIEWING MODES & PRESENTATION
- Borderless Full-Screen Presentation Mode: Press F11 (or Esc to exit). This maximizes your
  viewing canvas to 100% borderless presentation with a floating semi-transparent bottom pill.
- Zoom Controls: Use Ctrl++ to zoom in, Ctrl+- to zoom out, and Ctrl+0 for 100% Actual Size.
- Page Rotation: Press Ctrl+R to rotate the active page 90 degrees clockwise.
- Distraction-Free Layout: Press F4 to auto-hide the left rail, and Ctrl+T to toggle thumbnails.

3.3 SEARCH & IN-DOCUMENT LOOKUP
- Press Ctrl+F to open the top search bar. Type any term to highlight all matching occurrences.
- Use Next (Enter) and Previous (Shift+Enter) to cycle through matches across all pages.

SECTION 4: ANNOTATION, SKETCH PEN & VECTOR TOOLS
========================================================================================

4.1 VECTOR INK SKETCH & HIGHLIGHTER
- Select the Ink Sketch tool from the ribbon. Choose from 10 colors and 4 line widths
  (1.5pt Fine, 3.0pt Standard, 5.0pt Bold, 10.0pt Marker). Sketch directly onto pages.
- Select the Highlighter tool for semi-transparent emphasis over text passages.
- Use the Eraser tool to cleanly remove strokes.

4.2 DRAGGABLE STICKY NOTES
- Click the Note tool and click anywhere on a PDF page to pin a bright yellow note card.
- Type notes, feedback, or review comments. Click and drag the header to reposition notes anywhere.
- Click the close button on any note card to remove it.

4.3 AREA SNAPSHOT & CLIPBOARD CAPTURE
- Select the Pointer tool, hold your mouse, and drag a selection rectangle over any region.
- The high-DPI rasterizer instantly crops the selected area and copies it to your Windows Clipboard.
- Paste the screenshot directly into Microsoft Word, Paint, Slack, Teams, or Outlook.

SECTION 5: MICROSOFT WORD COMPATIBLE SUITE (.DOCX, RTF, MD, PSD)
========================================================================================

5.1 RICH DOCUMENT AUTHORSHIP
- Click Word Suite from the navigation rail or press Ctrl+N to open a blank document.
- Full formatting controls: Font Family, Font Size, Bold, Italic, Underline, Strikethrough,
  Subscript, Superscript, and Paragraph Alignments (Left, Center, Right, Justify).
- Headings & Paragraph Styles: Title, Subtitle, Heading 1, Heading 2, Heading 3, Quote, Code.
- Color Matrices: 40 theme colors, 10 standard colors, custom hex picker, and 15 highlight swatches.

5.2 WORD CASE CONVERSION (Aa)
- Select any text block and click the Aa dropdown to convert casing instantly:
  * Sentence case: Capitalizes the initial letter of each sentence.
  * lowercase: Converts all selected text to lowercase letters.
  * UPPERCASE: Converts all selected text to capital letters.
  * Capitalize Each Word: Title cases every word.
  * Toggle Case: Inverts the case of each individual character.

5.3 READABILITY & DOCUMENT AUDIT MODAL (DETAILED STATS)
- Click Detailed Stats in the Word ribbon to open a comprehensive telemetry audit:
  * Word Count, Character Count (with and without spaces), Paragraph Count, Line Count.
  * Estimated Reading Time (based on standard 220 WPM rate) and Speaking Time (130 WPM).
  * Flesch Reading Ease Score (0-100) and US School Grade Level rating.

5.4 TEMPLATE GALLERY
- Click Templates to instantiate pre-designed enterprise templates:
  * Project Charter, Modern Executive Resume, Business Proposal, Mutual NDA, Meeting Minutes.

SECTION 6: AIR-GAPPED OFFLINE AI DOCUMENT COPILOT & INTELLIGENCE
========================================================================================

PSD AI Copilot operates 100% locally on your machine with ZERO cloud APIs or external servers.

6.1 QUICK-ACTION AI AUDIT PILLS
Click AI Copilot in the top header or navigation rail to dock the intelligent assistant:
- Summary: Generates a high-level executive brief, key strategic takeaways, and tone telemetry.
- Actions: Scans document for commitments, tasks, assignees, deadlines, and urgency ratings.
- Risks: Audits agreements against uncapped liability, indemnification, and termination risks.
- Figures: Identifies financial metrics, monetary amounts, interest rates, and milestone dates.
- Simplify: Translates dense legal and technical jargon into straightforward plain English.
- RAG JSON: Exports standard vectorized JSON chunks with page citations for LLM pipelines
  (compatible with Pinecone, ChromaDB, Azure AI Search, LangChain, and Semantic Kernel).

6.2 NATURAL LANGUAGE DOCUMENT Q&A
- Type any question into the AI Ask prompt (e.g., 'What are the payment terms?' or 'Who is liable?')
- The local BM25 ranking algorithm scans active text and answers with exact clause citations.

SECTION 7: ENTERPRISE ELECTRONIC SIGNATURE STUDIO & SECURITY
========================================================================================

7.1 ELECTRONIC SIGNATURE STUDIO
- Click Sign & Audit to launch the full-featured signing window.
- Three Signing Modes:
  1. Freehand Ink: Draw your signature using a mouse, stylus, or touch screen.
  2. Typed Cursive Calligraphy: Type your name and select Segoe Script, Lucida, or Mistral.
  3. Signer Credentials: Enter Name, Corporate Title, Email, Organization, and Signing Reason.
- Cryptographic Audit Trail: Computes a SHA-256 hash digest, RFC 3161 UTC timestamp, and generates
  an exportable digital certificate. Click Sign & Apply to embed the signature badge into the document.

7.2 SECURITY, AES-256 ENCRYPTION & REDACTION
- AES-256 Permissions Handler: Protect PDF files with 256-bit AES encryption passwords.
- PII Deep-Stream Sanitizer: Permanently scrubs Social Security Numbers, Credit Cards, and Phone Numbers.
- Serialized Bates Stamping: Applies legal discovery indices (e.g., PSD-000001 to PSD-999999).

SECTION 8: COMPLETE KEYBOARD SHORTCUTS REFERENCE TABLE
========================================================================================
- F11           : Dedicated Full-Screen Presentation Mode (Press Esc to exit)
- F4            : Toggle Left Navigation Rail (Expand / Collapse)
- Ctrl + T      : Toggle Thumbnail Filmstrip Sidebar
- Ctrl + O      : Open Document (PDF, Word .docx, RTF, Markdown, HTML, PSD)
- Ctrl + N      : Create New Word Document (.docx)
- Ctrl + Shift+N: Create New Blank PDF Document
- Ctrl + S      : Save Incremental Updates
- Ctrl + Shift+S: Save As New File
- Ctrl + P      : Print Document via Windows Print Spooler
- Ctrl + F      : Find in Document (Next / Previous match cycling)
- Ctrl + H      : Word Find & Replace Panel
- Ctrl + R      : Rotate Page Clockwise 90 Degrees
- Ctrl + +      : Zoom In Viewport
- Ctrl + -      : Zoom Out Viewport
- Ctrl + 0      : Actual Size (100% Zoom)
- Esc           : Exit Search / Exit Full-Screen Presentation Mode";
        }
    }
}
