#include "../include/text_stream_extractor.h"
#include <fstream>
#include <sstream>
#include <vector>
#include <string>
#include <cstring>
#include <cstdlib>
#include <iostream>

namespace {

// Helper to escape PDF strings
std::string EscapePdfString(const std::string& input) {
    std::string out;
    out.reserve(input.size() * 2);
    for (char c : input) {
        if (c == '(' || c == ')' || c == '\\') {
            out.push_back('\\');
        }
        out.push_back(c);
    }
    return out;
}

// Simple stream text extractor
std::string ExtractTextFromStream(const std::string& stream) {
    std::string extracted;
    size_t pos = 0;
    while ((pos = stream.find("BT", pos)) != std::string::npos) {
        size_t endBt = stream.find("ET", pos);
        if (endBt == std::string::npos) break;

        std::string block = stream.substr(pos, endBt - pos);
        size_t tjPos = 0;
        while ((tjPos = block.find('(', tjPos)) != std::string::npos) {
            size_t closeParen = block.find(')', tjPos);
            if (closeParen == std::string::npos) break;

            std::string textPart = block.substr(tjPos + 1, closeParen - tjPos - 1);
            extracted += textPart;
            extracted += " ";
            tjPos = closeParen + 1;
        }
        extracted += "\n";
        pos = endBt + 2;
    }
    return extracted;
}

} // namespace

char* Omni_ExtractTextFromPdf(const char* pdfPath, int pageIndex) {
    if (!pdfPath) return nullptr;
    std::ifstream in(pdfPath, std::ios::binary);
    if (!in.is_open()) return nullptr;

    std::string buffer((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
    std::string text = ExtractTextFromStream(buffer);

    if (text.empty()) {
        text = "No extractable text stream located on page.";
    }

    char* result = static_cast<char*>(std::malloc(text.size() + 1));
    if (result) {
        std::memcpy(result, text.c_str(), text.size() + 1);
    }
    return result;
}

void Omni_FreeExtractedText(char* textPtr) {
    if (textPtr) {
        std::free(textPtr);
    }
}

int Omni_ExportPdfToMarkdown(const char* pdfPath, const char* outMarkdownPath) {
    if (!pdfPath || !outMarkdownPath) return 0;
    char* raw = Omni_ExtractTextFromPdf(pdfPath, 0);
    if (!raw) return 0;

    std::string rawStr(raw);
    Omni_FreeExtractedText(raw);

    std::ofstream out(outMarkdownPath);
    if (!out.is_open()) return 0;

    out << "# Document Transcript\n\n";
    std::istringstream stream(rawStr);
    std::string line;
    while (std::getline(stream, line)) {
        if (!line.empty()) {
            out << line << "\n\n";
        }
    }
    return 1;
}

int Omni_EmbedSearchableOcrLayer(
    const char* inputPdfOrImagePath,
    const OmniTextSpan* spans,
    int spanCount,
    int imageWidth,
    int imageHeight,
    const char* outputPdfPath
) {
    if (!outputPdfPath || imageWidth <= 0 || imageHeight <= 0) return 0;

    std::ofstream out(outputPdfPath, std::ios::binary);
    if (!out.is_open()) return 0;

    out << "%PDF-1.7\r\n%\xE2\xE3\xCF\xD3\r\n";
    out << "1 0 obj\r\n<< /Type /Catalog /Pages 2 0 R >>\r\nendobj\r\n";
    out << "2 0 obj\r\n<< /Type /Pages /Count 1 /Kids [ 3 0 R ] >>\r\nendobj\r\n";

    // Standard letter or image aspect ratio points
    double ptWidth = 612.0;
    double ptHeight = (static_cast<double>(imageHeight) / imageWidth) * ptWidth;

    out << "3 0 obj\r\n<< /Type /Page /Parent 2 0 R /MediaBox [ 0 0 " 
        << ptWidth << " " << ptHeight << " ] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\r\nendobj\r\n";

    // Text rendering mode 3: Neither fill nor stroke text (Invisible OCR Layer for selection/search)
    std::stringstream stream;
    stream << "BT\n/F1 12 Tf\n3 Tr\n"; // 3 Tr = Invisible text
    if (spans && spanCount > 0) {
        for (int i = 0; i < spanCount; ++i) {
            const auto& s = spans[i];
            if (s.textUtf8) {
                stream << "1 0 0 1 " << s.x << " " << s.y << " Tm\n";
                stream << "(" << EscapePdfString(s.textUtf8) << ") Tj\n";
            }
        }
    }
    stream << "ET\n";

    std::string contentStr = stream.str();
    out << "4 0 obj\r\n<< /Length " << contentStr.size() << " >>\r\nstream\r\n"
        << contentStr << "\r\nendstream\r\nendobj\r\n";

    // Standard Type1 Helvetica Font definition
    out << "5 0 obj\r\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\r\nendobj\r\n";

    out << "xref\r\n0 6\r\n";
    out << "0000000000 65535 f \r\n";
    for (int i = 1; i < 6; ++i) {
        char buf[32];
        snprintf(buf, sizeof(buf), "%010ld 00000 n \r\n", static_cast<long>(15 + i * 80));
        out << buf;
    }
    out << "trailer\r\n<< /Size 6 /Root 1 0 R >>\r\nstartxref\r\n"
        << (15 + 6 * 80) << "\r\n%%EOF\r\n";

    return 1;
}

int Omni_CompileTextToPdf(
    const char* utf8Text,
    const char* outputPdfPath,
    double fontSize,
    double lineHeight,
    double marginPoints,
    const char* watermarkText
) {
    if (!utf8Text || !outputPdfPath) return 0;
    if (fontSize <= 0) fontSize = 11.0;
    if (lineHeight <= 0) lineHeight = fontSize * 1.4;
    if (marginPoints <= 0) marginPoints = 54.0; // 0.75 in

    const double pageWidth = 612.0;  // Standard Letter
    const double pageHeight = 792.0;
    const double usableWidth = pageWidth - 2 * marginPoints;
    const double usableHeight = pageHeight - 2 * marginPoints;

    std::vector<std::string> lines;
    std::istringstream textStream(utf8Text);
    std::string rawLine;
    while (std::getline(textStream, rawLine)) {
        // Line wrap estimation: ~80 chars per line for standard width
        size_t maxChars = static_cast<size_t>(usableWidth / (fontSize * 0.52));
        if (maxChars == 0) maxChars = 60;

        if (rawLine.size() <= maxChars) {
            lines.push_back(rawLine);
        } else {
            size_t start = 0;
            while (start < rawLine.size()) {
                size_t len = std::min(maxChars, rawLine.size() - start);
                // Try to break at whitespace
                if (start + len < rawLine.size()) {
                    size_t space = rawLine.rfind(' ', start + len);
                    if (space != std::string::npos && space > start) {
                        len = space - start;
                    }
                }
                lines.push_back(rawLine.substr(start, len));
                start += len;
                while (start < rawLine.size() && rawLine[start] == ' ') start++;
            }
        }
    }

    int linesPerPage = static_cast<int>(usableHeight / lineHeight);
    if (linesPerPage <= 0) linesPerPage = 40;

    int totalPages = static_cast<int>((lines.size() + linesPerPage - 1) / linesPerPage);
    if (totalPages == 0) totalPages = 1;

    std::ofstream out(outputPdfPath, std::ios::binary);
    if (!out.is_open()) return 0;

    out << "%PDF-1.7\r\n%\xE2\xE3\xCF\xD3\r\n";
    out << "1 0 obj\r\n<< /Type /Catalog /Pages 2 0 R >>\r\nendobj\r\n";

    // Pages object
    out << "2 0 obj\r\n<< /Type /Pages /Count " << totalPages << " /Kids [ ";
    for (int i = 0; i < totalPages; ++i) {
        out << (4 + i * 2) << " 0 R ";
    }
    out << "] >>\r\nendobj\r\n";

    // Standard Font Object
    out << "3 0 obj\r\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\r\nendobj\r\n";

    int currentObj = 4;
    for (int p = 0; p < totalPages; ++p) {
        int pageObjId = currentObj++;
        int contentObjId = currentObj++;

        out << pageObjId << " 0 obj\r\n<< /Type /Page /Parent 2 0 R /MediaBox [ 0 0 "
            << pageWidth << " " << pageHeight << " ] /Resources << /Font << /F1 3 0 R >> >> /Contents "
            << contentObjId << " 0 R >>\r\nendobj\r\n";

        std::stringstream contentStream;

        // Watermark if requested
        if (watermarkText && watermarkText[0] != '\0') {
            contentStream << "q\n0.85 0.85 0.85 rg\nBT\n/F1 48 Tf\n";
            contentStream << "1 0 0 1 150 400 Tm\n";
            contentStream << "(" << EscapePdfString(watermarkText) << ") Tj\nET\nQ\n";
        }

        // Page content
        contentStream << "BT\n/F1 " << fontSize << " Tf\n0 0 0 rg\n";
        double yPos = pageHeight - marginPoints - fontSize;

        int startLine = p * linesPerPage;
        int endLine = std::min(static_cast<int>(lines.size()), startLine + linesPerPage);

        for (int l = startLine; l < endLine; ++l) {
            contentStream << "1 0 0 1 " << marginPoints << " " << yPos << " Tm\n";
            contentStream << "(" << EscapePdfString(lines[l]) << ") Tj\n";
            yPos -= lineHeight;
        }

        // Pagination stamp at bottom center
        contentStream << "/F1 9 Tf\n0.4 0.4 0.4 rg\n";
        char pageStamp[64];
        snprintf(pageStamp, sizeof(pageStamp), "Page %d of %d", p + 1, totalPages);
        contentStream << "1 0 0 1 " << (pageWidth / 2 - 25) << " 25 Tm\n";
        contentStream << "(" << pageStamp << ") Tj\n";

        contentStream << "ET\n";

        std::string cStr = contentStream.str();
        out << contentObjId << " 0 obj\r\n<< /Length " << cStr.size() << " >>\r\nstream\r\n"
            << cStr << "\r\nendstream\r\nendobj\r\n";
    }

    out << "xref\r\n0 " << currentObj << "\r\n";
    out << "0000000000 65535 f \r\n";
    for (int i = 1; i < currentObj; ++i) {
        char buf[32];
        snprintf(buf, sizeof(buf), "%010ld 00000 n \r\n", static_cast<long>(20 + i * 90));
        out << buf;
    }
    out << "trailer\r\n<< /Size " << currentObj << " /Root 1 0 R >>\r\nstartxref\r\n"
        << (20 + currentObj * 90) << "\r\n%%EOF\r\n";

    return 1;
}
