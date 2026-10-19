package com.omni.pdf.converters

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import java.io.File
import java.io.FileOutputStream

object TextToPdfConverter {

    external fun nativeCompileTextToPdf(
        utf8Text: String,
        outputPath: String,
        fontSize: Double,
        lineHeight: Double,
        marginPoints: Double,
        watermarkText: String
    ): Int

    fun convert(
        text: String,
        outputPath: String,
        fontSize: Double = 12.0,
        watermark: String = ""
    ): Boolean {
        // Try native high-performance engine first
        try {
            val res = nativeCompileTextToPdf(
                text,
                outputPath,
                fontSize,
                fontSize * 1.4,
                54.0, // 0.75 in margin
                watermark
            )
            if (res == 1) return true
        } catch (e: Throwable) {
            // Fallback to Android PdfDocument
        }

        // Android SDK fallback
        val doc = PdfDocument()
        val paint = Paint().apply {
            textSize = fontSize.toFloat()
            color = Color.BLACK
            isAntiAlias = true
        }

        val watermarkPaint = Paint().apply {
            textSize = 48f
            color = Color.argb(40, 0, 0, 0)
            isAntiAlias = true
        }

        val pageWidth = 612 // Letter points
        val pageHeight = 792
        val margin = 54f
        val usableWidth = pageWidth - (margin * 2)

        val lines = mutableListOf<String>()
        val paragraphs = text.split("\n")
        for (p in paragraphs) {
            if (p.isEmpty()) {
                lines.add("")
                continue
            }
            var currentLine = ""
            val words = p.split(" ")
            for (word in words) {
                val testLine = if (currentLine.isEmpty()) word else "$currentLine $word"
                val measured = paint.measureText(testLine)
                if (measured <= usableWidth) {
                    currentLine = testLine
                } else {
                    if (currentLine.isNotEmpty()) lines.add(currentLine)
                    currentLine = word
                }
            }
            if (currentLine.isNotEmpty()) lines.add(currentLine)
        }

        val lineHeightPx = fontSize.toFloat() * 1.4f
        val linesPerPage = ((pageHeight - (margin * 2)) / lineHeightPx).toInt()
        val totalPages = kotlin.math.max(1, (lines.size + linesPerPage - 1) / linesPerPage)

        for (pageIdx in 0 until totalPages) {
            val pageInfo = PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageIdx + 1).create()
            val page = doc.startPage(pageInfo)
            val canvas: Canvas = page.canvas

            if (watermark.isNotEmpty()) {
                canvas.drawText(watermark, 120f, 400f, watermarkPaint)
            }

            val startLine = pageIdx * linesPerPage
            val endLine = kotlin.math.min(lines.size, startLine + linesPerPage)
            var y = margin + fontSize.toFloat()

            for (i in startLine until endLine) {
                canvas.drawText(lines[i], margin, y, paint)
                y += lineHeightPx
            }

            // Pagination footer
            val footerPaint = Paint().apply {
                textSize = 9f
                color = Color.GRAY
                isAntiAlias = true
            }
            canvas.drawText("Page ${pageIdx + 1} of $totalPages", (pageWidth / 2f) - 30f, pageHeight - 20f, footerPaint)

            doc.finishPage(page)
        }

        return try {
            val file = File(outputPath)
            file.parentFile?.mkdirs()
            FileOutputStream(file).use { outStream ->
                doc.writeTo(outStream)
            }
            doc.close()
            true
        } catch (e: Exception) {
            doc.close()
            false
        }
    }
}
