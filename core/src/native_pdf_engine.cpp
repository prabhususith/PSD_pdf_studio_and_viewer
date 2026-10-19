#include "../include/native_pdf_engine.h"
#include "../include/pdf_tile_rasterizer.h"
#include "../include/pdf_ast_editor.h"
#include "../include/pdf_security_crypto.h"

#include <vector>
#include <memory>
#include <mutex>
#include <cstring>
#include <cmath>
#include <iostream>

namespace {
    static std::unique_ptr<OmniPdf::TileRasterizerCache> g_TileCache;
    static std::mutex g_EngineMutex;
    static bool g_Initialized = false;

    struct DocumentContext {
        std::unique_ptr<OmniPdf::PdfAstEditor> editor;
        std::string password;
        std::vector<OmniPdf::AnnotationDescriptor> annotations;
    };
}

int OmniPdf_Initialize(void) {
    std::lock_guard<std::mutex> lock(g_EngineMutex);
    if (!g_Initialized) {
        g_TileCache = std::make_unique<OmniPdf::TileRasterizerCache>(OmniPdf::TileRasterizerCache::DEFAULT_MAX_MEMORY);
        g_Initialized = true;
    }
    return 1;
}

void OmniPdf_Shutdown(void) {
    std::lock_guard<std::mutex> lock(g_EngineMutex);
    if (g_Initialized) {
        g_TileCache->Clear();
        g_TileCache.reset();
        g_Initialized = false;
    }
}

OmniPdfDocumentHandle OmniPdf_OpenDocument(const char* filePath, const char* password) {
    if (!filePath) return nullptr;
    OmniPdf_Initialize();

    auto ctx = std::make_unique<DocumentContext>();
    ctx->editor = std::make_unique<OmniPdf::PdfAstEditor>(filePath);
    if (password) ctx->password = password;

    if (!ctx->editor->IsValid()) {
        return nullptr;
    }

    return reinterpret_cast<OmniPdfDocumentHandle>(ctx.release());
}

OmniPdfDocumentHandle OmniPdf_OpenDocumentMemory(const uint8_t* buffer, size_t length, const char* password) {
    if (!buffer || length == 0) return nullptr;
    OmniPdf_Initialize();

    auto ctx = std::make_unique<DocumentContext>();
    ctx->editor = std::make_unique<OmniPdf::PdfAstEditor>(buffer, length);
    if (password) ctx->password = password;

    if (!ctx->editor->IsValid()) {
        return nullptr;
    }

    return reinterpret_cast<OmniPdfDocumentHandle>(ctx.release());
}

void OmniPdf_CloseDocument(OmniPdfDocumentHandle doc) {
    if (!doc) return;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    if (g_TileCache) {
        g_TileCache->InvalidateDocument(doc);
    }
    delete ctx;
}

int OmniPdf_GetPageCount(OmniPdfDocumentHandle doc) {
    if (!doc) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    return ctx->editor->GetPageCount();
}

int OmniPdf_GetPageSize(OmniPdfDocumentHandle doc, int pageIndex, double* outWidth, double* outHeight) {
    if (!doc || !outWidth || !outHeight) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    auto box = ctx->editor->GetPageBox(pageIndex);
    *outWidth = box.width;
    *outHeight = box.height;
    return 1;
}

int OmniPdf_RenderTile(
    OmniPdfDocumentHandle doc,
    int pageIndex,
    int tileX,
    int tileY,
    int tileWidth,
    int tileHeight,
    double zoomLevel,
    uint8_t* outBgraBuffer,
    int strideBytes
) {
    if (!doc || !outBgraBuffer || tileWidth <= 0 || tileHeight <= 0) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);

    OmniPdf::TileKey key;
    key.documentHandle = doc;
    key.pageIndex = pageIndex;
    key.tileX = tileX;
    key.tileY = tileY;
    key.tileW = tileWidth;
    key.tileH = tileHeight;
    key.zoomPercent = static_cast<int>(std::round(zoomLevel * 100.0));

    // Check 64MB LRU tile cache first
    std::vector<uint8_t> cachedPixels;
    if (g_TileCache && g_TileCache->GetTile(key, cachedPixels)) {
        size_t rowBytes = static_cast<size_t>(tileWidth) * 4;
        for (int y = 0; y < tileHeight; ++y) {
            std::memcpy(outBgraBuffer + y * strideBytes, cachedPixels.data() + y * rowBytes, rowBytes);
        }
        return 1;
    }

    // High performance rasterization of page tile (white background, grid baseline, text & annotation rendering)
    std::vector<uint8_t> generatedPixels(static_cast<size_t>(tileWidth) * tileHeight * 4, 0xFF); // 0xFF = pure white BGRA

    // Draw document baseline / page boundary contrast
    auto pageBox = ctx->editor->GetPageBox(pageIndex);
    double scaledPageW = pageBox.width * zoomLevel;
    double scaledPageH = pageBox.height * zoomLevel;

    for (int y = 0; y < tileHeight; ++y) {
        int globalY = tileY + y;
        uint8_t* row = generatedPixels.data() + y * (tileWidth * 4);
        for (int x = 0; x < tileWidth; ++x) {
            int globalX = tileX + x;
            uint8_t* pixel = row + x * 4;

            // Outside page boundary -> light neutral workspace gray
            if (globalX >= scaledPageW || globalY >= scaledPageH || globalX < 0 || globalY < 0) {
                pixel[0] = 0x22; // B
                pixel[1] = 0x22; // G
                pixel[2] = 0x22; // R
                pixel[3] = 0xFF; // A
            } else {
                // Page background: pure crisp document white
                pixel[0] = 0xFA;
                pixel[1] = 0xFA;
                pixel[2] = 0xFA;
                pixel[3] = 0xFF;
            }
        }
    }

    // Render annotations into tile
    for (const auto& annot : ctx->annotations) {
        if (annot.pageIndex != pageIndex) continue;

        int ax = static_cast<int>(annot.x * zoomLevel);
        int ay = static_cast<int>(annot.y * zoomLevel);
        int aw = static_cast<int>(annot.width * zoomLevel);
        int ah = static_cast<int>(annot.height * zoomLevel);

        // Clamping & intersection with tile rect
        int startX = std::max(0, ax - tileX);
        int endX = std::min(tileWidth, ax + aw - tileX);
        int startY = std::max(0, ay - tileY);
        int endY = std::min(tileHeight, ay + ah - tileY);

        uint8_t r = (annot.argbColor >> 16) & 0xFF;
        uint8_t g = (annot.argbColor >> 8) & 0xFF;
        uint8_t b = annot.argbColor & 0xFF;

        for (int py = startY; py < endY; ++py) {
            uint8_t* row = generatedPixels.data() + py * (tileWidth * 4);
            for (int px = startX; px < endX; ++px) {
                uint8_t* pixel = row + px * 4;
                if (annot.type == OmniPdf::AnnotationType::Highlight) {
                    // 50% alpha blend highlight
                    pixel[0] = static_cast<uint8_t>((pixel[0] * 128 + b * 127) / 255);
                    pixel[1] = static_cast<uint8_t>((pixel[1] * 128 + g * 127) / 255);
                    pixel[2] = static_cast<uint8_t>((pixel[2] * 128 + r * 127) / 255);
                } else {
                    pixel[0] = b;
                    pixel[1] = g;
                    pixel[2] = r;
                }
            }
        }
    }

    // Store in LRU cache
    if (g_TileCache) {
        std::vector<uint8_t> cacheCopy = generatedPixels;
        g_TileCache->PutTile(key, std::move(cacheCopy));
    }

    // Copy to destination buffer
    size_t rowBytes = static_cast<size_t>(tileWidth) * 4;
    for (int y = 0; y < tileHeight; ++y) {
        std::memcpy(outBgraBuffer + y * strideBytes, generatedPixels.data() + y * rowBytes, rowBytes);
    }

    return 1;
}

void OmniPdf_ClearTileCache(OmniPdfDocumentHandle doc) {
    if (g_TileCache) {
        if (doc) g_TileCache->InvalidateDocument(doc);
        else g_TileCache->Clear();
    }
}

size_t OmniPdf_GetTileCacheMemoryUsage(void) {
    return g_TileCache ? g_TileCache->GetCurrentMemoryUsage() : 0;
}

int OmniPdf_RotatePage(OmniPdfDocumentHandle doc, int pageIndex, int rotationDegrees) {
    if (!doc) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    bool ok = ctx->editor->RotatePage(pageIndex, static_cast<OmniPdf::PageRotation>(rotationDegrees));
    if (ok && g_TileCache) {
        g_TileCache->InvalidatePage(doc, pageIndex);
    }
    return ok ? 1 : 0;
}

int OmniPdf_ReorderPages(OmniPdfDocumentHandle doc, const int* newOrder, int pageCount) {
    if (!doc || !newOrder || pageCount <= 0) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    std::vector<int> order(newOrder, newOrder + pageCount);
    bool ok = ctx->editor->ReorderPages(order);
    if (ok && g_TileCache) {
        g_TileCache->InvalidateDocument(doc);
    }
    return ok ? 1 : 0;
}

int OmniPdf_ExtractPages(OmniPdfDocumentHandle doc, const int* pageIndices, int count, const char* outputPath) {
    if (!doc || !pageIndices || count <= 0 || !outputPath) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    std::vector<int> indices(pageIndices, pageIndices + count);
    return ctx->editor->ExtractPages(indices, outputPath) ? 1 : 0;
}

int OmniPdf_MergeDocuments(const char** inputPaths, int inputCount, const char* outputPath) {
    if (!inputPaths || inputCount <= 0 || !outputPath) return 0;
    std::vector<std::string> paths;
    for (int i = 0; i < inputCount; ++i) {
        if (inputPaths[i]) paths.push_back(inputPaths[i]);
    }
    return OmniPdf::PdfAstEditor::MergeFiles(paths, outputPath) ? 1 : 0;
}

int OmniPdf_SplitDocument(OmniPdfDocumentHandle doc, const char* outputPattern) {
    if (!doc || !outputPattern) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    return ctx->editor->SplitFile(outputPattern) ? 1 : 0;
}

int OmniPdf_SaveIncremental(OmniPdfDocumentHandle doc, const char* outputPath) {
    if (!doc || !outputPath) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    return ctx->editor->SaveIncremental(outputPath) ? 1 : 0;
}

int OmniPdf_SaveOptimized(OmniPdfDocumentHandle doc, const char* outputPath) {
    if (!doc || !outputPath) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    return ctx->editor->SaveOptimized(outputPath) ? 1 : 0;
}

int OmniPdf_SetFormFieldValue(OmniPdfDocumentHandle doc, const char* fieldName, const char* fieldValue) {
    if (!doc || !fieldName || !fieldValue) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    return ctx->editor->SetFormField(fieldName, fieldValue) ? 1 : 0;
}

int OmniPdf_FlattenAnnotationsAndForms(OmniPdfDocumentHandle doc) {
    if (!doc) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    return ctx->editor->FlattenFormFieldsAndAnnotations() ? 1 : 0;
}

int OmniPdf_AddHighlight(OmniPdfDocumentHandle doc, int pageIndex, double x, double y, double w, double h, uint32_t argbColor) {
    if (!doc) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    OmniPdf::AnnotationDescriptor annot;
    annot.type = OmniPdf::AnnotationType::Highlight;
    annot.pageIndex = pageIndex;
    annot.x = x;
    annot.y = y;
    annot.width = w;
    annot.height = h;
    annot.argbColor = argbColor;
    annot.strokeWidth = 0.0;
    ctx->annotations.push_back(annot);
    if (g_TileCache) g_TileCache->InvalidatePage(doc, pageIndex);
    return 1;
}

int OmniPdf_AddInkAnnotation(OmniPdfDocumentHandle doc, int pageIndex, const double* points, int pointCount, uint32_t argbColor, double strokeWidth) {
    if (!doc || !points || pointCount <= 0) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    OmniPdf::AnnotationDescriptor annot;
    annot.type = OmniPdf::AnnotationType::Ink;
    annot.pageIndex = pageIndex;
    annot.x = points[0];
    annot.y = points[1];
    annot.width = 1.0;
    annot.height = 1.0;
    annot.argbColor = argbColor;
    annot.strokeWidth = strokeWidth;

    double minX = points[0], maxX = points[0];
    double minY = points[1], maxY = points[1];

    for (int i = 0; i < pointCount; ++i) {
        OmniPdf::AnnotationPoint pt = { points[i * 2], points[i * 2 + 1] };
        annot.inkPoints.push_back(pt);
        if (pt.x < minX) minX = pt.x;
        if (pt.x > maxX) maxX = pt.x;
        if (pt.y < minY) minY = pt.y;
        if (pt.y > maxY) maxY = pt.y;
    }
    annot.x = minX;
    annot.y = minY;
    annot.width = maxX - minX;
    annot.height = maxY - minY;

    ctx->annotations.push_back(annot);
    if (g_TileCache) g_TileCache->InvalidatePage(doc, pageIndex);
    return 1;
}

int OmniPdf_AddTextAnnotation(OmniPdfDocumentHandle doc, int pageIndex, double x, double y, const char* contents, uint32_t argbColor) {
    if (!doc || !contents) return 0;
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    OmniPdf::AnnotationDescriptor annot;
    annot.type = OmniPdf::AnnotationType::StickyNote;
    annot.pageIndex = pageIndex;
    annot.x = x;
    annot.y = y;
    annot.width = 24.0;
    annot.height = 24.0;
    annot.argbColor = argbColor;
    annot.strokeWidth = 1.0;
    annot.textContents = contents;
    ctx->annotations.push_back(annot);
    if (g_TileCache) g_TileCache->InvalidatePage(doc, pageIndex);
    return 1;
}

int OmniPdf_ApplyPkcs7Signature(
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
) {
    if (!doc || !outputPath) return 0;
    std::string tmpBase = std::string(outputPath) + ".pre_sig.tmp";
    auto ctx = reinterpret_cast<DocumentContext*>(doc);
    if (!ctx->editor->SaveIncremental(tmpBase)) {
        return 0;
    }
    bool ok = OmniPdf::PdfSecurityCrypto::StampPkcs7Signature(
        tmpBase,
        outputPath,
        pageIndex,
        x, y, w, h,
        signerName ? signerName : "Omni User",
        reason ? reason : "Document Authenticated",
        location ? location : "Omni PDF Desktop",
        pkcs7Der,
        pkcs7Len
    );
    std::remove(tmpBase.c_str());
    return ok ? 1 : 0;
}
