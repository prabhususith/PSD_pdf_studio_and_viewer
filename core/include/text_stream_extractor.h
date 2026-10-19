#ifndef OMNI_TEXT_STREAM_EXTRACTOR_H
#define OMNI_TEXT_STREAM_EXTRACTOR_H

#include <stdint.h>
#include <stddef.h>
#include <string>
#include <vector>

#ifdef _WIN32
  #ifdef OMNI_PDF_EXPORTS
    #define OMNI_API __declspec(dllexport)
  #else
    #define OMNI_API __declspec(dllimport)
  #endif
#else
  #define OMNI_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

struct OmniTextSpan {
    double x;
    double y;
    double width;
    double height;
    const char* textUtf8;
};

// Extract plain UTF-8 text from PDF file or memory
OMNI_API char* Omni_ExtractTextFromPdf(const char* pdfPath, int pageIndex);
OMNI_API void Omni_FreeExtractedText(char* textPtr);

// Export to Markdown preserving paragraph structure and headings
OMNI_API int Omni_ExportPdfToMarkdown(const char* pdfPath, const char* outMarkdownPath);

// Generate searchable PDF by embedding invisible text spans over scanned image
OMNI_API int Omni_EmbedSearchableOcrLayer(
    const char* inputPdfOrImagePath,
    const OmniTextSpan* spans,
    int spanCount,
    int imageWidth,
    int imageHeight,
    const char* outputPdfPath
);

// Dynamic Text-to-PDF compilation engine
OMNI_API int Omni_CompileTextToPdf(
    const char* utf8Text,
    const char* outputPdfPath,
    double fontSize,
    double lineHeight,
    double marginPoints,
    const char* watermarkText
);

#ifdef __cplusplus
}
#endif

#endif // OMNI_TEXT_STREAM_EXTRACTOR_H
