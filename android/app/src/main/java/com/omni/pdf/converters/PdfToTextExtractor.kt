package com.omni.pdf.converters

import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream

object PdfToTextExtractor {

    external fun nativeExtractTextFromPdf(pdfPath: String, pageIndex: Int): String?
    external fun nativeExportPdfToMarkdown(pdfPath: String, outMarkdownPath: String): Int

    fun extractText(pdfPath: String, pageIndex: Int = 0): String {
        try {
            val text = nativeExtractTextFromPdf(pdfPath, pageIndex)
            if (text != null && text.isNotEmpty()) {
                return text
            }
        } catch (e: Throwable) {
            // Fallback to basic stream parse
        }

        val file = File(pdfPath)
        if (!file.exists()) return ""

        val sb = StringBuilder()
        try {
            val bytes = FileInputStream(file).use { it.readBytes() }
            val content = String(bytes, Charsets.ISO_8859_1)

            var pos = 0
            while (true) {
                val bt = content.indexOf("BT", pos)
                if (bt == -1) break
                val et = content.indexOf("ET", bt)
                if (et == -1) break

                val block = content.substring(bt, et)
                var tjPos = 0
                while (true) {
                    val pOpen = block.indexOf('(', tjPos)
                    if (pOpen == -1) break
                    val pClose = block.indexOf(')', pOpen)
                    if (pClose == -1) break

                    val text = block.substring(pOpen + 1, pClose)
                    sb.append(text).append(" ")
                    tjPos = pClose + 1
                }
                sb.append("\n")
                pos = et + 2
            }
        } catch (e: Exception) {
            sb.append("Error parsing PDF text: ${e.message}")
        }

        return if (sb.isNotEmpty()) sb.toString() else "No text stream found."
    }

    fun exportToMarkdown(pdfPath: String, outMarkdownPath: String): Boolean {
        try {
            val res = nativeExportPdfToMarkdown(pdfPath, outMarkdownPath)
            if (res == 1) return true
        } catch (e: Throwable) {
            // Fallback
        }

        val text = extractText(pdfPath)
        return try {
            val mdFile = File(outMarkdownPath)
            mdFile.parentFile?.mkdirs()
            FileOutputStream(mdFile).use { out ->
                val header = "# Document Transcript\n\nExported via Omni PDF Studio\n\n"
                out.write(header.toByteArray(Charsets.UTF_8))
                out.write(text.toByteArray(Charsets.UTF_8))
            }
            true
        } catch (e: Exception) {
            false
        }
    }
}
