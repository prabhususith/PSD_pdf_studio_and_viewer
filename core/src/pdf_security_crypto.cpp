#include "../include/pdf_security_crypto.h"
#include <fstream>
#include <sstream>
#include <iomanip>
#include <cstring>
#include <cmath>

namespace OmniPdf {

// Standard FIPS 180-4 SHA-256 implementation (zero external dependencies)
namespace {
    static inline uint32_t rotr(uint32_t x, uint32_t n) {
        return (x >> n) | (x << (32 - n));
    }
    static inline uint32_t ch(uint32_t x, uint32_t y, uint32_t z) {
        return (x & y) ^ (~x & z);
    }
    static inline uint32_t maj(uint32_t x, uint32_t y, uint32_t z) {
        return (x & y) ^ (x & z) ^ (y & z);
    }
    static inline uint32_t sigma0(uint32_t x) {
        return rotr(x, 2) ^ rotr(x, 13) ^ rotr(x, 22);
    }
    static inline uint32_t sigma1(uint32_t x) {
        return rotr(x, 6) ^ rotr(x, 11) ^ rotr(x, 25);
    }
    static inline uint32_t gamma0(uint32_t x) {
        return rotr(x, 7) ^ rotr(x, 18) ^ (x >> 3);
    }
    static inline uint32_t gamma1(uint32_t x) {
        return rotr(x, 17) ^ rotr(x, 19) ^ (x >> 10);
    }

    static const uint32_t K256[64] = {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    };

    void sha256_process(const uint8_t* data, size_t len, uint8_t hash[32]) {
        uint32_t state[8] = {
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
            0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
        };

        std::vector<uint8_t> padded(data, data + len);
        padded.push_back(0x80);
        while ((padded.size() % 64) != 56) {
            padded.push_back(0x00);
        }
        uint64_t bit_len = static_cast<uint64_t>(len) * 8;
        for (int i = 7; i >= 0; --i) {
            padded.push_back(static_cast<uint8_t>((bit_len >> (i * 8)) & 0xff));
        }

        for (size_t chunk = 0; chunk < padded.size(); chunk += 64) {
            uint32_t w[64];
            for (int i = 0; i < 16; ++i) {
                w[i] = (static_cast<uint32_t>(padded[chunk + i * 4]) << 24) |
                       (static_cast<uint32_t>(padded[chunk + i * 4 + 1]) << 16) |
                       (static_cast<uint32_t>(padded[chunk + i * 4 + 2]) << 8) |
                       (static_cast<uint32_t>(padded[chunk + i * 4 + 3]));
            }
            for (int i = 16; i < 64; ++i) {
                w[i] = gamma1(w[i - 2]) + w[i - 7] + gamma0(w[i - 15]) + w[i - 16];
            }

            uint32_t a = state[0], b = state[1], c = state[2], d = state[3];
            uint32_t e = state[4], f = state[5], g = state[6], h = state[7];

            for (int i = 0; i < 64; ++i) {
                uint32_t t1 = h + sigma1(e) + ch(e, f, g) + K256[i] + w[i];
                uint32_t t2 = sigma0(a) + maj(a, b, c);
                h = g;
                g = f;
                f = e;
                e = d + t1;
                d = c;
                c = b;
                b = a;
                a = t1 + t2;
            }

            state[0] += a; state[1] += b; state[2] += c; state[3] += d;
            state[4] += e; state[5] += f; state[6] += g; state[7] += h;
        }

        for (int i = 0; i < 8; ++i) {
            hash[i * 4]     = static_cast<uint8_t>((state[i] >> 24) & 0xff);
            hash[i * 4 + 1] = static_cast<uint8_t>((state[i] >> 16) & 0xff);
            hash[i * 4 + 2] = static_cast<uint8_t>((state[i] >> 8) & 0xff);
            hash[i * 4 + 3] = static_cast<uint8_t>(state[i] & 0xff);
        }
    }
}

PdfSecurityCrypto::PdfSecurityCrypto() {}
PdfSecurityCrypto::~PdfSecurityCrypto() {}

void PdfSecurityCrypto::ComputeSha256Digest(const uint8_t* data, size_t length, uint8_t outHash[32]) {
    sha256_process(data, length, outHash);
}

std::string PdfSecurityCrypto::ComputeSha256Hex(const uint8_t* data, size_t length) {
    uint8_t hash[32];
    ComputeSha256Digest(data, length, hash);
    std::stringstream ss;
    ss << std::hex << std::setfill('0');
    for (int i = 0; i < 32; ++i) {
        ss << std::setw(2) << static_cast<int>(hash[i]);
    }
    return ss.str();
}

std::string PdfSecurityCrypto::SerializeAnnotationDict(int annotId, const AnnotationDescriptor& annot) {
    std::stringstream ss;
    ss << annotId << " 0 obj\r\n<< ";

    // Convert ARGB to RGB float
    float r = ((annot.argbColor >> 16) & 0xFF) / 255.0f;
    float g = ((annot.argbColor >> 8) & 0xFF) / 255.0f;
    float b = (annot.argbColor & 0xFF) / 255.0f;

    switch (annot.type) {
        case AnnotationType::Highlight:
            ss << "/Type /Annot /Subtype /Highlight /Rect [" 
               << annot.x << " " << annot.y << " " << (annot.x + annot.width) << " " << (annot.y + annot.height) << "] "
               << "/C [" << r << " " << g << " " << b << "] /QuadPoints ["
               << annot.x << " " << (annot.y + annot.height) << " "
               << (annot.x + annot.width) << " " << (annot.y + annot.height) << " "
               << annot.x << " " << annot.y << " "
               << (annot.x + annot.width) << " " << annot.y << "] ";
            break;

        case AnnotationType::Underline:
            ss << "/Type /Annot /Subtype /Underline /Rect ["
               << annot.x << " " << annot.y << " " << (annot.x + annot.width) << " " << (annot.y + annot.height) << "] "
               << "/C [" << r << " " << g << " " << b << "] ";
            break;

        case AnnotationType::StrikeOut:
            ss << "/Type /Annot /Subtype /StrikeOut /Rect ["
               << annot.x << " " << annot.y << " " << (annot.x + annot.width) << " " << (annot.y + annot.height) << "] "
               << "/C [" << r << " " << g << " " << b << "] ";
            break;

        case AnnotationType::Ink: {
            ss << "/Type /Annot /Subtype /Ink /Rect ["
               << annot.x << " " << annot.y << " " << (annot.x + annot.width) << " " << (annot.y + annot.height) << "] "
               << "/C [" << r << " " << g << " " << b << "] /BS << /W " << annot.strokeWidth << " >> /InkList [ [ ";
            for (const auto& pt : annot.inkPoints) {
                ss << pt.x << " " << pt.y << " ";
            }
            ss << "] ] ";
            break;
        }

        case AnnotationType::StickyNote:
            ss << "/Type /Annot /Subtype /Text /Rect ["
               << annot.x << " " << annot.y << " " << (annot.x + 24.0) << " " << (annot.y + 24.0) << "] "
               << "/Contents (" << annot.textContents << ") /C [" << r << " " << g << " " << b << "] ";
            break;

        default:
            break;
    }

    ss << ">>\r\nendobj\r\n";
    return ss.str();
}

bool PdfSecurityCrypto::StampPkcs7Signature(
    const std::string& inputPdfPath,
    const std::string& outputPdfPath,
    int pageIndex,
    double x, double y, double width, double height,
    const std::string& signerName,
    const std::string& reason,
    const std::string& location,
    const uint8_t* pkcs7Der,
    size_t pkcs7Len
) {
    std::ifstream in(inputPdfPath, std::ios::binary);
    if (!in.is_open()) return false;

    std::vector<uint8_t> buffer((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
    in.close();

    // Standard PKCS#7 signature container placeholder (padded to 8192 bytes hex = 4096 bytes DER)
    const size_t SIGNATURE_HEX_LEN = 8192;
    std::string hexSig(SIGNATURE_HEX_LEN, '0');
    if (pkcs7Der && pkcs7Len > 0) {
        std::stringstream ss;
        ss << std::hex << std::setfill('0');
        for (size_t i = 0; i < pkcs7Len && i < (SIGNATURE_HEX_LEN / 2); ++i) {
            ss << std::setw(2) << static_cast<int>(pkcs7Der[i]);
        }
        std::string converted = ss.str();
        for (size_t i = 0; i < converted.size(); ++i) {
            hexSig[i] = converted[i];
        }
    }

    // Append signature dictionary object
    std::stringstream sigObj;
    sigObj << "\r\n999 0 obj\r\n<<\r\n"
           << "/Type /Sig\r\n"
           << "/Filter /Adobe.PPKLite\r\n"
           << "/SubFilter /adbe.pkcs7.detached\r\n"
           << "/Name (" << signerName << ")\r\n"
           << "/Reason (" << reason << ")\r\n"
           << "/Location (" << location << ")\r\n"
           << "/M (D:20261019120000+00'00')\r\n"
           << "/ByteRange [ 0 0000000000 0000000000 0000000000 ]\r\n"
           << "/Contents <" << hexSig << ">\r\n"
           << ">>\r\nendobj\r\n";

    std::string sigObjStr = sigObj.str();
    size_t origSize = buffer.size();
    buffer.insert(buffer.end(), sigObjStr.begin(), sigObjStr.end());

    // Update ByteRange offsets in buffer
    std::string fullStr(reinterpret_cast<const char*>(buffer.data()), buffer.size());
    size_t contentsPos = fullStr.rfind("/Contents <");
    size_t byteRangePos = fullStr.rfind("/ByteRange [");
    if (contentsPos != std::string::npos && byteRangePos != std::string::npos) {
        size_t offset1 = 0;
        size_t len1 = contentsPos + 11; // up to '<'
        size_t offset2 = len1 + SIGNATURE_HEX_LEN + 1; // after '>'
        size_t len2 = buffer.size() - offset2;

        char byteRangeBuf[64];
        snprintf(byteRangeBuf, sizeof(byteRangeBuf), "/ByteRange [ %010zu %010zu %010zu %010zu ]",
                 offset1, len1, offset2, len2);
        std::memcpy(buffer.data() + byteRangePos, byteRangeBuf, std::strlen(byteRangeBuf));
    }

    std::ofstream out(outputPdfPath, std::ios::binary);
    if (!out.is_open()) return false;
    out.write(reinterpret_cast<const char*>(buffer.data()), buffer.size());
    return true;
}

} // namespace OmniPdf
