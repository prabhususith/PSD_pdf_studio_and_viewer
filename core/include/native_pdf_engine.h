#ifndef OMNI_PDF_NATIVE_ENGINE_H
#define OMNI_PDF_NATIVE_ENGINE_H

#include <stdint.h>
#include <stddef.h>

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

typedef void* OmniPdfDocumentHandle;
typedef void* OmniPdfPageHandle;

// Engine Lifecycle
OMNI_API int OmniPdf_Initialize(void);
OMNI_API void OmniPdf_Shutdown(void);

// Document Lifecycle
OMNI_API OmniPdfDocumentHandle OmniPdf_OpenDocument(const char* filePath, const char* password);
OMNI_API OmniPdfDocumentHandle OmniPdf_OpenDocumentMemory(const uint8_t* buffer, size_t length, const char* password);
OMNI_API void OmniPdf_CloseDocument(OmniPdfDocumentHandle doc);
OMNI_API int OmniPdf_GetPageCount(OmniPdfDocumentHandle doc);
OMNI_API int OmniPdf_GetPageSize(OmniPdfDocumentHandle doc, int pageIndex, double* outWidth, double* outHeight);

// Rendering & Tile System (Backed by 64MB LRU cache)
OMNI_API int OmniPdf_RenderTile(
    OmniPdfDocumentHandle doc,
    int pageIndex,
    int tileX,
    int tileY,
    int tileWidth,
    int tileHeight,
    double zoomLevel,
    uint8_t* outBgraBuffer,
    int strideBytes
);

OMNI_API void OmniPdf_ClearTileCache(OmniPdfDocumentHandle doc);
OMNI_API size_t OmniPdf_GetTileCacheMemoryUsage(void);

// Page Matrix Operations
OMNI_API int OmniPdf_RotatePage(OmniPdfDocumentHandle doc, int pageIndex, int rotationDegrees);
OMNI_API int OmniPdf_ReorderPages(OmniPdfDocumentHandle doc, const int* newOrder, int pageCount);
OMNI_API int OmniPdf_ExtractPages(OmniPdfDocumentHandle doc, const int* pageIndices, int count, const char* outputPath);
OMNI_API int OmniPdf_MergeDocuments(const char** inputPaths, int inputCount, const char* outputPath);
OMNI_API int OmniPdf_SplitDocument(OmniPdfDocumentHandle doc, const char* outputPattern);

// Incremental Saving Engine
OMNI_API int OmniPdf_SaveIncremental(OmniPdfDocumentHandle doc, const char* outputPath);
OMNI_API int OmniPdf_SaveOptimized(OmniPdfDocumentHandle doc, const char* outputPath);

// Form Fields & Flattening
OMNI_API int OmniPdf_SetFormFieldValue(OmniPdfDocumentHandle doc, const char* fieldName, const char* fieldValue);
OMNI_API int OmniPdf_FlattenAnnotationsAndForms(OmniPdfDocumentHandle doc);

// Annotation Stamping
OMNI_API int OmniPdf_AddHighlight(OmniPdfDocumentHandle doc, int pageIndex, double x, double y, double w, double h, uint32_t argbColor);
OMNI_API int OmniPdf_AddInkAnnotation(OmniPdfDocumentHandle doc, int pageIndex, const double* points, int pointCount, uint32_t argbColor, double strokeWidth);
OMNI_API int OmniPdf_AddTextAnnotation(OmniPdfDocumentHandle doc, int pageIndex, double x, double y, const char* contents, uint32_t argbColor);

// Digital Signature (PKCS#7 detached)
OMNI_API int OmniPdf_ApplyPkcs7Signature(
    OmniPdfDocumentHandle doc,
    int pageIndex,
    double x,
    double y,
    double w,
    double h,
    const char* signerName,
    const char* reason,
    const char* location,
    const uint8_t* pkcs7Der,
    size_t pkcs7Len,
    const char* outputPath
);

#ifdef __cplusplus
}
#endif

#endif // OMNI_PDF_NATIVE_ENGINE_H
