#ifndef OMNI_PDF_SECURITY_CRYPTO_H
#define OMNI_PDF_SECURITY_CRYPTO_H

#include <stdint.h>
#include <stddef.h>
#include <string>
#include <vector>

namespace OmniPdf {

struct AnnotationPoint {
    double x;
    double y;
};

enum class AnnotationType {
    Highlight,
    Underline,
    StrikeOut,
    Ink,
    StickyNote,
    DigitalSignature
};

struct AnnotationDescriptor {
    AnnotationType type;
    int pageIndex;
    double x, y, width, height;
    uint32_t argbColor;
    double strokeWidth;
    std::string textContents;
    std::vector<AnnotationPoint> inkPoints;
};

class PdfSecurityCrypto {
public:
    PdfSecurityCrypto();
    ~PdfSecurityCrypto();

    // Annotation serialization
    static std::string SerializeAnnotationDict(int annotId, const AnnotationDescriptor& annot);

    // Cryptographic PKCS#7 Digital Signature Stamping
    static bool StampPkcs7Signature(
        const std::string& inputPdfPath,
        const std::string& outputPdfPath,
        int pageIndex,
        double x, double y, double width, double height,
        const std::string& signerName,
        const std::string& reason,
        const std::string& location,
        const uint8_t* pkcs7Der,
        size_t pkcs7Len
    );

    // SHA-256 computation over byte ranges
    static std::string ComputeSha256Hex(const uint8_t* data, size_t length);
    static void ComputeSha256Digest(const uint8_t* data, size_t length, uint8_t outHash[32]);
};

} // namespace OmniPdf

#endif // OMNI_PDF_SECURITY_CRYPTO_H
