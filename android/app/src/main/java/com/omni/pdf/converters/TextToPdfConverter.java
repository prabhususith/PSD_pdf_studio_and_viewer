package com.omni.pdf.converters;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.pdf.PdfDocument;
import java.io.File;
import java.io.FileOutputStream;

/**
 * Enterprise Text to Vector PDF Compilation Engine for Android.
 * Handles automatic word-wrapping, multi-page pagination, running headers & footers,
 * and page counter indicators.
 */
public class TextToPdfConverter {

    public static boolean convertTextToPdf(String title, String text, String outputPath, double fontSize, String watermark) {
        if (text == null) text = "";
        if (title == null || title.trim().isEmpty()) title = "PSD Document";

        final int pageWidth = 595;  // Standard A4 width in pt
        final int pageHeight = 842; // Standard A4 height in pt
        final int marginLeft = 54;
        final int marginRight = 54;
        final int marginTop = 72;
        final int marginBottom = 72;
        final int contentWidth = pageWidth - marginLeft - marginRight;

        PdfDocument document = new PdfDocument();

        Paint titlePaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        titlePaint.setColor(0xFF0F172A);
        titlePaint.setTextSize(20f);
        titlePaint.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));

        Paint bodyPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        bodyPaint.setColor(0xFF1E293B);
        bodyPaint.setTextSize((float) fontSize);
        bodyPaint.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.NORMAL));

        Paint footerPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        footerPaint.setColor(0xFF94A3B8);
        footerPaint.setTextSize(9f);

        Paint rulePaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        rulePaint.setColor(0xFFE2E8F0);
        rulePaint.setStrokeWidth(1.2f);

        float lineHeight = (float) fontSize * 1.45f;
        String[] rawLines = text.split("\r?\n");

        int pageNumber = 1;
        PdfDocument.PageInfo pageInfo = new PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNumber).create();
        PdfDocument.Page currentPage = document.startPage(pageInfo);
        Canvas canvas = currentPage.getCanvas();

        float y = marginTop;

        // Draw Cover / Document Header on first page
        canvas.drawText(title, marginLeft, y, titlePaint);
        y += 28;
        canvas.drawLine(marginLeft, y, pageWidth - marginRight, y, rulePaint);
        y += 24;

        for (String paragraph : rawLines) {
            if (paragraph.trim().isEmpty()) {
                y += lineHeight;
                continue;
            }

            // Word-wrap paragraph
            String[] words = paragraph.split("\\s+");
            StringBuilder currentLine = new StringBuilder();

            for (String word : words) {
                String candidate = currentLine.length() == 0 ? word : currentLine + " " + word;
                float measured = bodyPaint.measureText(candidate);

                if (measured <= contentWidth) {
                    currentLine = new StringBuilder(candidate);
                } else {
                    // Check if new page needed
                    if (y + lineHeight > pageHeight - marginBottom) {
                        // Draw footer
                        drawFooter(canvas, pageWidth, pageHeight, marginBottom, pageNumber, watermark, footerPaint);
                        document.finishPage(currentPage);

                        pageNumber++;
                        pageInfo = new PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNumber).create();
                        currentPage = document.startPage(pageInfo);
                        canvas = currentPage.getCanvas();
                        y = marginTop;
                    }

                    canvas.drawText(currentLine.toString(), marginLeft, y, bodyPaint);
                    y += lineHeight;
                    currentLine = new StringBuilder(word);
                }
            }

            if (currentLine.length() > 0) {
                if (y + lineHeight > pageHeight - marginBottom) {
                    drawFooter(canvas, pageWidth, pageHeight, marginBottom, pageNumber, watermark, footerPaint);
                    document.finishPage(currentPage);

                    pageNumber++;
                    pageInfo = new PdfDocument.PageInfo.Builder(pageWidth, pageHeight, pageNumber).create();
                    currentPage = document.startPage(pageInfo);
                    canvas = currentPage.getCanvas();
                    y = marginTop;
                }

                canvas.drawText(currentLine.toString(), marginLeft, y, bodyPaint);
                y += lineHeight + 4; // slight paragraph spacing
            }
        }

        // Draw final page footer
        drawFooter(canvas, pageWidth, pageHeight, marginBottom, pageNumber, watermark, footerPaint);
        document.finishPage(currentPage);

        try {
            File outFile = new File(outputPath);
            if (outFile.getParentFile() != null) {
                outFile.getParentFile().mkdirs();
            }
            FileOutputStream fos = new FileOutputStream(outFile);
            document.writeTo(fos);
            document.close();
            fos.close();
            return true;
        } catch (Exception e) {
            try { document.close(); } catch (Exception ignored) {}
            return false;
        }
    }

    private static void drawFooter(Canvas canvas, int pageWidth, int pageHeight, int marginBottom, int pageNumber, String watermark, Paint paint) {
        float footerY = pageHeight - marginBottom + 28;
        String stamp = (watermark != null && !watermark.isEmpty()) ? watermark : "Generated with PSD PDF Studio for Android";
        canvas.drawText(stamp, 54, footerY, paint);

        String pageText = "Page " + pageNumber;
        float pageTextWidth = paint.measureText(pageText);
        canvas.drawText(pageText, pageWidth - 54 - pageTextWidth, footerY, paint);
    }
}
