#include "../include/pdf_tile_rasterizer.h"
#include <algorithm>
#include <iostream>

namespace OmniPdf {

TileRasterizerCache::TileRasterizerCache(size_t maxMemoryBytes)
    : maxMemoryBytes_(maxMemoryBytes), currentMemoryBytes_(0) {
}

TileRasterizerCache::~TileRasterizerCache() {
    Clear();
}

bool TileRasterizerCache::GetTile(const TileKey& key, std::vector<uint8_t>& outPixels) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = lookupTable_.find(key);
    if (it == lookupTable_.end()) {
        return false;
    }

    // Splice to front of LRU list (most recently used)
    lruList_.splice(lruList_.begin(), lruList_, it->second);
    outPixels = it->second->bgraPixels;
    return true;
}

void TileRasterizerCache::PutTile(const TileKey& key, std::vector<uint8_t>&& pixels) {
    std::lock_guard<std::mutex> lock(mutex_);

    // If key already exists, update and promote
    auto it = lookupTable_.find(key);
    if (it != lookupTable_.end()) {
        currentMemoryBytes_ -= it->second->byteSize;
        it->second->bgraPixels = std::move(pixels);
        it->second->byteSize = it->second->bgraPixels.size();
        currentMemoryBytes_ += it->second->byteSize;
        lruList_.splice(lruList_.begin(), lruList_, it->second);
        EvictIfNeeded();
        return;
    }

    size_t tileBytes = pixels.size();
    CachedTile item;
    item.key = key;
    item.bgraPixels = std::move(pixels);
    item.byteSize = tileBytes;

    lruList_.push_front(std::move(item));
    lookupTable_[key] = lruList_.begin();
    currentMemoryBytes_ += tileBytes;

    EvictIfNeeded();
}

void TileRasterizerCache::EvictIfNeeded() {
    while (currentMemoryBytes_ > maxMemoryBytes_ && !lruList_.empty()) {
        // Evict least recently used (back of list)
        auto& backItem = lruList_.back();
        currentMemoryBytes_ -= backItem.byteSize;
        lookupTable_.erase(backItem.key);
        lruList_.pop_back();
    }
}

void TileRasterizerCache::InvalidateDocument(void* docHandle) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = lruList_.begin();
    while (it != lruList_.end()) {
        if (it->key.documentHandle == docHandle) {
            currentMemoryBytes_ -= it->byteSize;
            lookupTable_.erase(it->key);
            it = lruList_.erase(it);
        } else {
            ++it;
        }
    }
}

void TileRasterizerCache::InvalidatePage(void* docHandle, int pageIndex) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = lruList_.begin();
    while (it != lruList_.end()) {
        if (it->key.documentHandle == docHandle && it->key.pageIndex == pageIndex) {
            currentMemoryBytes_ -= it->byteSize;
            lookupTable_.erase(it->key);
            it = lruList_.erase(it);
        } else {
            ++it;
        }
    }
}

void TileRasterizerCache::Clear() {
    std::lock_guard<std::mutex> lock(mutex_);
    lruList_.clear();
    lookupTable_.clear();
    currentMemoryBytes_ = 0;
}

size_t TileRasterizerCache::GetCurrentMemoryUsage() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return currentMemoryBytes_;
}

} // namespace OmniPdf
