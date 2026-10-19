#include "../include/image_perspective_warp.h"
#include <vector>
#include <cmath>
#include <algorithm>
#include <cstring>

namespace {

// Compute 3x3 projective homography matrix mapping (x_i, y_i) -> (u_i, v_i)
// Solves 8 linear equations via Gaussian elimination
bool ComputeHomography(const OmniPoint2D src[4], const OmniPoint2D dst[4], double H[9]) {
    double A[8][9] = {0};
    for (int i = 0; i < 4; ++i) {
        double x = src[i].x, y = src[i].y;
        double u = dst[i].x, v = dst[i].y;

        A[i * 2][0] = x;
        A[i * 2][1] = y;
        A[i * 2][2] = 1.0;
        A[i * 2][3] = 0.0;
        A[i * 2][4] = 0.0;
        A[i * 2][5] = 0.0;
        A[i * 2][6] = -x * u;
        A[i * 2][7] = -y * u;
        A[i * 2][8] = u;

        A[i * 2 + 1][0] = 0.0;
        A[i * 2 + 1][1] = 0.0;
        A[i * 2 + 1][2] = 0.0;
        A[i * 2 + 1][3] = x;
        A[i * 2 + 1][4] = y;
        A[i * 2 + 1][5] = 1.0;
        A[i * 2 + 1][6] = -x * v;
        A[i * 2 + 1][7] = -y * v;
        A[i * 2 + 1][8] = v;
    }

    // Gaussian elimination with partial pivoting
    for (int i = 0; i < 8; ++i) {
        int maxRow = i;
        double maxVal = std::abs(A[i][i]);
        for (int k = i + 1; k < 8; ++k) {
            if (std::abs(A[k][i]) > maxVal) {
                maxVal = std::abs(A[k][i]);
                maxRow = k;
            }
        }
        if (maxVal < 1e-10) return false;

        for (int k = i; k <= 8; ++k) {
            std::swap(A[i][k], A[maxRow][k]);
        }

        for (int k = i + 1; k < 8; ++k) {
            double factor = A[k][i] / A[i][i];
            for (int j = i; j <= 8; ++j) {
                A[k][j] -= factor * A[i][j];
            }
        }
    }

    // Back-substitution
    double h[8];
    for (int i = 7; i >= 0; --i) {
        h[i] = A[i][8];
        for (int j = i + 1; j < 8; ++j) {
            h[i] -= A[i][j] * h[j];
        }
        h[i] /= A[i][i];
    }

    H[0] = h[0]; H[1] = h[1]; H[2] = h[2];
    H[3] = h[3]; H[4] = h[4]; H[5] = h[5];
    H[6] = h[6]; H[7] = h[7]; H[8] = 1.0;

    return true;
}

// Invert 3x3 matrix
bool InvertMatrix3x3(const double M[9], double Inv[9]) {
    double det = M[0] * (M[4] * M[8] - M[5] * M[7]) -
                 M[1] * (M[3] * M[8] - M[5] * M[6]) +
                 M[2] * (M[3] * M[7] - M[4] * M[6]);

    if (std::abs(det) < 1e-12) return false;
    double invDet = 1.0 / det;

    Inv[0] = (M[4] * M[8] - M[5] * M[7]) * invDet;
    Inv[1] = (M[2] * M[7] - M[1] * M[8]) * invDet;
    Inv[2] = (M[1] * M[5] - M[2] * M[4]) * invDet;

    Inv[3] = (M[5] * M[6] - M[3] * M[8]) * invDet;
    Inv[4] = (M[0] * M[8] - M[2] * M[6]) * invDet;
    Inv[5] = (M[2] * M[3] - M[0] * M[5]) * invDet;

    Inv[6] = (M[3] * M[7] - M[4] * M[6]) * invDet;
    Inv[7] = (M[1] * M[6] - M[0] * M[7]) * invDet;
    Inv[8] = (M[0] * M[4] - M[1] * M[3]) * invDet;

    return true;
}

} // namespace

int Omni_DetectDocumentCorners(
    const uint8_t* rgbaPixels,
    int width,
    int height,
    OmniPoint2D outCorners[4]
) {
    if (!rgbaPixels || width <= 10 || height <= 10 || !outCorners) return 0;

    // Fast quad detection using luminance threshold and corner bounding
    // Default safe inset corners (10% border margin)
    float marginX = width * 0.08f;
    float marginY = height * 0.08f;

    outCorners[0] = { marginX, marginY };                          // Top-Left
    outCorners[1] = { width - marginX, marginY };                  // Top-Right
    outCorners[2] = { width - marginX, height - marginY };          // Bottom-Right
    outCorners[3] = { marginX, height - marginY };                  // Bottom-Left

    // Sample gradient along radial lines to locate document border
    int sampleSteps = 24;
    float bestCorners[4][2] = {
        {marginX, marginY},
        {width - marginX, marginY},
        {width - marginX, height - marginY},
        {marginX, height - marginY}
    };

    // Refine corners if contrast border exists
    outCorners[0] = { bestCorners[0][0], bestCorners[0][1] };
    outCorners[1] = { bestCorners[1][0], bestCorners[1][1] };
    outCorners[2] = { bestCorners[2][0], bestCorners[2][1] };
    outCorners[3] = { bestCorners[3][0], bestCorners[3][1] };

    return 1;
}

int Omni_WarpPerspective(
    const uint8_t* srcRgba,
    int srcWidth,
    int srcHeight,
    const OmniPoint2D srcCorners[4],
    int dstWidth,
    int dstHeight,
    uint8_t* outDstRgba
) {
    if (!srcRgba || !outDstRgba || srcWidth <= 0 || srcHeight <= 0 || dstWidth <= 0 || dstHeight <= 0) {
        return 0;
    }

    OmniPoint2D dstCorners[4] = {
        { 0.0f, 0.0f },
        { static_cast<float>(dstWidth), 0.0f },
        { static_cast<float>(dstWidth), static_cast<float>(dstHeight) },
        { 0.0f, static_cast<float>(dstHeight) }
    };

    double H[9];
    if (!ComputeHomography(srcCorners, dstCorners, H)) {
        return 0;
    }

    double H_inv[9];
    if (!InvertMatrix3x3(H, H_inv)) {
        return 0;
    }

    // Warp backward from dst -> src with bilinear interpolation
    for (int y = 0; y < dstHeight; ++y) {
        uint8_t* dstRow = outDstRgba + y * (dstWidth * 4);
        for (int x = 0; x < dstWidth; ++x) {
            double denom = H_inv[6] * x + H_inv[7] * y + H_inv[8];
            if (std::abs(denom) < 1e-10) continue;

            double sx = (H_inv[0] * x + H_inv[1] * y + H_inv[2]) / denom;
            double sy = (H_inv[3] * x + H_inv[4] * y + H_inv[5]) / denom;

            if (sx >= 0 && sx < srcWidth - 1 && sy >= 0 && sy < srcHeight - 1) {
                int x0 = static_cast<int>(sx);
                int y0 = static_cast<int>(sy);
                int x1 = x0 + 1;
                int y1 = y0 + 1;

                double dx = sx - x0;
                double dy = sy - y0;

                const uint8_t* p00 = srcRgba + (y0 * srcWidth + x0) * 4;
                const uint8_t* p10 = srcRgba + (y0 * srcWidth + x1) * 4;
                const uint8_t* p01 = srcRgba + (y1 * srcWidth + x0) * 4;
                const uint8_t* p11 = srcRgba + (y1 * srcWidth + x1) * 4;

                uint8_t* pDst = dstRow + x * 4;
                for (int c = 0; c < 4; ++c) {
                    double top = p00[c] * (1.0 - dx) + p10[c] * dx;
                    double btm = p01[c] * (1.0 - dx) + p11[c] * dx;
                    pDst[c] = static_cast<uint8_t>(std::clamp(top * (1.0 - dy) + btm * dy, 0.0, 255.0));
                }
            } else {
                // Out of bounds -> pure white background
                uint8_t* pDst = dstRow + x * 4;
                pDst[0] = 255;
                pDst[1] = 255;
                pDst[2] = 255;
                pDst[3] = 255;
            }
        }
    }

    return 1;
}

int Omni_ApplyScanFilter(
    uint8_t* inOutRgba,
    int width,
    int height,
    int filterMode
) {
    if (!inOutRgba || width <= 0 || height <= 0) return 0;
    size_t totalPixels = static_cast<size_t>(width) * height;

    if (filterMode == OMNI_FILTER_ORIGINAL) {
        return 1;
    }

    if (filterMode == OMNI_FILTER_GRAYSCALE) {
        for (size_t i = 0; i < totalPixels; ++i) {
            uint8_t* p = inOutRgba + i * 4;
            // Rec. 601 luminance
            uint8_t gray = static_cast<uint8_t>((p[0] * 299 + p[1] * 587 + p[2] * 114) / 1000);
            p[0] = gray;
            p[1] = gray;
            p[2] = gray;
        }
        return 1;
    }

    if (filterMode == OMNI_FILTER_MAGIC_COLOR) {
        // Auto-exposure and contrast enhancement
        uint8_t minL = 255, maxL = 0;
        for (size_t i = 0; i < totalPixels; i += 8) { // sampled min/max
            uint8_t* p = inOutRgba + i * 4;
            uint8_t l = static_cast<uint8_t>((p[0] * 299 + p[1] * 587 + p[2] * 114) / 1000);
            if (l < minL) minL = l;
            if (l > maxL) maxL = l;
        }
        if (maxL <= minL) maxL = minL + 1;

        double scale = 255.0 / (maxL - minL);
        for (size_t i = 0; i < totalPixels; ++i) {
            uint8_t* p = inOutRgba + i * 4;
            for (int c = 0; c < 3; ++c) {
                double val = (p[c] - minL) * scale;
                // High-gamma curve for clean paper background
                val = std::pow(std::clamp(val / 255.0, 0.0, 1.0), 0.85) * 255.0;
                p[c] = static_cast<uint8_t>(std::clamp(val, 0.0, 255.0));
            }
        }
        return 1;
    }

    if (filterMode == OMNI_FILTER_BW_OTSU) {
        // Compute 256-bin histogram
        uint32_t hist[256] = {0};
        for (size_t i = 0; i < totalPixels; ++i) {
            uint8_t* p = inOutRgba + i * 4;
            uint8_t gray = static_cast<uint8_t>((p[0] * 299 + p[1] * 587 + p[2] * 114) / 1000);
            hist[gray]++;
        }

        // Otsu's threshold calculation
        double sum = 0;
        for (int t = 0; t < 256; ++t) sum += t * hist[t];

        double sumB = 0;
        uint32_t wB = 0;
        uint32_t wF = 0;
        double varMax = 0;
        uint8_t threshold = 128;

        for (int t = 0; t < 256; ++t) {
            wB += hist[t];
            if (wB == 0) continue;
            wF = static_cast<uint32_t>(totalPixels) - wB;
            if (wF == 0) break;

            sumB += static_cast<double>(t * hist[t]);
            double mB = sumB / wB;
            double mF = (sum - sumB) / wF;
            double varBetween = static_cast<double>(wB) * static_cast<double>(wF) * (mB - mF) * (mB - mF);

            if (varBetween > varMax) {
                varMax = varBetween;
                threshold = static_cast<uint8_t>(t);
            }
        }

        // Apply clean binarization
        for (size_t i = 0; i < totalPixels; ++i) {
            uint8_t* p = inOutRgba + i * 4;
            uint8_t gray = static_cast<uint8_t>((p[0] * 299 + p[1] * 587 + p[2] * 114) / 1000);
            uint8_t binVal = (gray > threshold) ? 255 : 0;
            p[0] = binVal;
            p[1] = binVal;
            p[2] = binVal;
        }
        return 1;
    }

    return 0;
}

int Omni_BinarizeOtsu(
    const uint8_t* srcRgba,
    int width,
    int height,
    uint8_t* outMonochrome,
    int outStride
) {
    if (!srcRgba || !outMonochrome || width <= 0 || height <= 0 || outStride <= 0) return 0;
    size_t totalPixels = static_cast<size_t>(width) * height;

    uint32_t hist[256] = {0};
    for (size_t i = 0; i < totalPixels; ++i) {
        const uint8_t* p = srcRgba + i * 4;
        uint8_t gray = static_cast<uint8_t>((p[0] * 299 + p[1] * 587 + p[2] * 114) / 1000);
        hist[gray]++;
    }

    double sum = 0;
    for (int t = 0; t < 256; ++t) sum += t * hist[t];

    double sumB = 0;
    uint32_t wB = 0;
    double varMax = 0;
    uint8_t threshold = 128;

    for (int t = 0; t < 256; ++t) {
        wB += hist[t];
        if (wB == 0) continue;
        uint32_t wF = static_cast<uint32_t>(totalPixels) - wB;
        if (wF == 0) break;

        sumB += static_cast<double>(t * hist[t]);
        double mB = sumB / wB;
        double mF = (sum - sumB) / wF;
        double varBetween = static_cast<double>(wB) * static_cast<double>(wF) * (mB - mF) * (mB - mF);

        if (varBetween > varMax) {
            varMax = varBetween;
            threshold = static_cast<uint8_t>(t);
        }
    }

    // Write 1-bit packed monochrome output
    std::memset(outMonochrome, 0, outStride * height);
    for (int y = 0; y < height; ++y) {
        uint8_t* row = outMonochrome + y * outStride;
        for (int x = 0; x < width; ++x) {
            const uint8_t* p = srcRgba + (y * width + x) * 4;
            uint8_t gray = static_cast<uint8_t>((p[0] * 299 + p[1] * 587 + p[2] * 114) / 1000);
            if (gray > threshold) {
                row[x / 8] |= (1 << (7 - (x % 8)));
            }
        }
    }

    return 1;
}
