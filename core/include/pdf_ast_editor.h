#ifndef OMNI_PDF_AST_EDITOR_H
#define OMNI_PDF_AST_EDITOR_H

#include <stdint.h>
#include <string>
#include <vector>
#include <memory>
#include <map>

namespace OmniPdf {

enum class PageRotation {
    Deg0 = 0,
    Deg90 = 90,
    Deg180 = 180,
    Deg270 = 270
};

struct PageRect {
    double x;
    double y;
    double width;
    double height;
};

struct ExtractedImage {
    int objectId;
    int width;
    int height;
    int bitsPerComponent;
    std::string colorSpace;
    std::string filter;
    std::vector<uint8_t> data;
};

class PdfAstEditor {
public:
    explicit PdfAstEditor(const std::string& filePath);
    explicit PdfAstEditor(const uint8_t* memory, size_t length);
    ~PdfAstEditor();

    bool IsValid() const;
    int GetPageCount() const;
    PageRect GetPageBox(int pageIndex) const;

    // Page Matrix Operations
    bool RotatePage(int pageIndex, PageRotation rotation);
    bool ReorderPages(const std::vector<int>& newOrder);
    bool ExtractPages(const std::vector<int>& pageIndices, const std::string& outputPath);
    static bool MergeFiles(const std::vector<std::string>& inputPaths, const std::string& outputPath);
    bool SplitFile(const std::string& outputPattern);

    // CRUD Objects & Streams
    bool ExtractImages(int pageIndex, std::vector<ExtractedImage>& outImages);
    bool ReplaceImage(int objectId, const uint8_t* newImageData, size_t newImageLen, int width, int height);
    bool EditTextStream(int pageIndex, const std::string& targetText, const std::string& replacementText);

    // AcroForms
    bool SetFormField(const std::string& fieldName, const std::string& value);
    bool FlattenFormFieldsAndAnnotations();

    // Incremental and Full Serialization
    bool SaveIncremental(const std::string& outputPath);
    bool SaveOptimized(const std::string& outputPath);

    // Direct buffer access for native engine
    const std::vector<uint8_t>& GetBuffer() const { return rawBuffer_; }

private:
    bool ParseStructure();
    void BuildXrefTable();

    std::vector<uint8_t> rawBuffer_;
    std::vector<uint8_t> appendBuffer_;
    std::vector<size_t> pageOffsets_;
    std::map<int, int> pageRotations_;
    std::map<std::string, std::string> formFieldValues_;
    bool isValid_;
};

} // namespace OmniPdf

#endif // OMNI_PDF_AST_EDITOR_H
