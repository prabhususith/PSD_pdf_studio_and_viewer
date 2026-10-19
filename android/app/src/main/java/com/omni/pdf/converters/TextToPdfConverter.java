package com.omni.pdf.converters;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.pdf.PdfDocument;
import java.io.File;
import java.io.FileOutputStream;

public class TextToPdfConverter {
    public static boolean convertTextToPdf(String text, String outputPath, double fontSize, String watermark) {
        PdfDocument document = new PdfDocument();
        PdfDocument.PageInfo pageInfo = new PdfDocument.PageInfo.Builder(612, 792, 1).create();
        PdfDocument.Page page = document.startPage(pageInfo);

        Canvas canvas = page.getCanvas();
        Paint paint = new Paint();
        paint.setColor(Color.BLACK);
        paint.setTextSize((float)fontSize);

        canvas.drawText(text, 54, 100, paint);
        document.finishPage(page);

        try {
            File file = new File(outputPath);
            file.getParentFile().mkdirs();
            FileOutputStream fos = new FileOutputStream(file);
            document.writeTo(fos);
            document.close();
            fos.close();
            return true;
        } catch (Exception e) {
            document.close();
            return false;
        }
    }
}
