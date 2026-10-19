#ifndef OMNI_IMAGE_PERSPECTIVE_WARP_H
#define OMNI_IMAGE_PERSPECTIVE_WARP_H

#include <stdint.h>
#include <stddef.h>
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

struct OmniPoint2D {
    float x;
    float y;
};

enum OmniScanFilter {
    OMNI_FILTER_ORIGINAL = 0,
    OMNI_FILTER_MAGIC_COLOR = 1,
    OMNI_FILTER_BW_OTSU = 2,
    OMNI_FILTER_GRAYSCALE = 3
};

// 4-corner boundary detection on RGBA image
OMNI_API int Omni_DetectDocumentCorners(
    const uint8_t* rgbaPixels,
    int width,
    int height,
    OmniPoint2D outCorners[4] // Top-Left, Top-Right, Bottom-Right, Bottom-Left
);

// Perspective transform warp from 4 source corners into a flattened w x h image
OMNI_API int Omni_WarpPerspective(
    const uint8_t* srcRgba,
    int srcWidth,
    int srcHeight,
    const OmniPoint2D srcCorners[4],
    int dstWidth,
    int dstHeight,
    uint8_t* outDstRgba
);

// Image enhancement filters
OMNI_API int Omni_ApplyScanFilter(
    uint8_t* inOutRgba,
    int width,
    int height,
    int filterMode
);

// Fast Otsu thresholding for 1-bit / 8-bit text binarization
OMNI_API int Omni_BinarizeOtsu(
    const uint8_t* srcRgba,
    int width,
    int height,
    uint8_t* outMonochrome,
    int outStride
);

#ifdef __cplusplus
}
#endif

#endif // OMNI_IMAGE_PERSPECTIVE_WARP_H
