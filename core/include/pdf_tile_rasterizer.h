#ifndef OMNI_PDF_TILE_RASTERIZER_H
#define OMNI_PDF_TILE_RASTERIZER_H

#include <stdint.h>
#include <stddef.h>
#include <vector>
#include <string>
#include <unordered_map>
#include <list>
#include <mutex>
#include <memory>

namespace OmniPdf {

struct TileKey {
    void* documentHandle;
    int pageIndex;
    int tileX;
    int tileY;
    int tileW;
    int tileH;
    int zoomPercent; // zoom * 100 for integer key

    bool operator==(const TileKey& other) const {
        return documentHandle == other.documentHandle &&
               pageIndex == other.pageIndex &&
               tileX == other.tileX &&
               tileY == other.tileY &&
               tileW == other.tileW &&
               tileH == other.tileH &&
               zoomPercent == other.zoomPercent;
    }
};

struct TileKeyHash {
    size_t operator()(const TileKey& k) const {
        size_t h1 = reinterpret_cast<size_t>(k.documentHandle);
        size_t h2 = std::hash<int>()(k.pageIndex);
        size_t h3 = std::hash<int>()(k.tileX) ^ (std::hash<int>()(k.tileY) << 1);
        size_t h4 = std::hash<int>()(k.zoomPercent);
        return h1 ^ (h2 << 1) ^ (h3 << 2) ^ (h4 << 3);
    }
};

struct CachedTile {
    TileKey key;
    std::vector<uint8_t> bgraPixels;
    size_t byteSize;
};

class TileRasterizerCache {
public:
    static constexpr size_t DEFAULT_MAX_MEMORY = 64 * 1024 * 1024; // 64MB LRU Cap

    explicit TileRasterizerCache(size_t maxMemoryBytes = DEFAULT_MAX_MEMORY);
    ~TileRasterizerCache();

    bool GetTile(const TileKey& key, std::vector<uint8_t>& outPixels);
    void PutTile(const TileKey& key, std::vector<uint8_t>&& pixels);
    void InvalidateDocument(void* docHandle);
    void InvalidatePage(void* docHandle, int pageIndex);
    void Clear();
    size_t GetCurrentMemoryUsage() const;

private:
    void EvictIfNeeded();

    mutable std::mutex mutex_;
    size_t maxMemoryBytes_;
    size_t currentMemoryBytes_;
    std::list<CachedTile> lruList_;
    std::unordered_map<TileKey, std::list<CachedTile>::iterator, TileKeyHash> lookupTable_;
};

} // namespace OmniPdf

#endif // OMNI_PDF_TILE_RASTERIZER_H
