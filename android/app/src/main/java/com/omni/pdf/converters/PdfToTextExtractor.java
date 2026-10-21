package com.omni.pdf.converters;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Android Sovereign PDF Metadata and Text Stream Extraction Engine.
 * Extracts document streams, text chunks, and structures into Markdown transcripts.
 */
public class PdfToTextExtractor {

    public static String extractText(String pdfPath) {
        if (pdfPath == null || !new File(pdfPath).exists()) {
            return "Document unavailable or path is invalid.";
        }

        StringBuilder sb = new StringBuilder();
        try {
            File f = new File(pdfPath);
            sb.append("File: ").append(f.getName()).append("\n");
            sb.append("Size: ").append(f.length() / 1024).append(" KB\n");
            sb.append("--------------------------------------------------\n\n");

            // Read raw bytes to extract uncompressed text tokens and metadata
            byte[] buffer = new byte[(int) Math.min(f.length(), 512 * 1024)];
            try (InputStream is = new FileInputStream(f)) {
                int read = is.read(buffer);
                String content = new String(buffer, 0, Math.max(0, read), "ISO-8859-1");

                // Extract PDF Title / Creator if present
                Pattern titlePat = Pattern.compile("/Title\\s*\\(([^)]+)\\)");
                Matcher m = titlePat.matcher(content);
                if (m.find()) {
                    sb.append("Title: ").append(m.group(1)).append("\n");
                }

                Pattern authorPat = Pattern.compile("/Author\\s*\\(([^)]+)\\)");
                Matcher ma = authorPat.matcher(content);
                if (ma.find()) {
                    sb.append("Author: ").append(ma.group(1)).append("\n");
                }

                sb.append("\n[Text Stream Segments]\n");
                // Extract (text) Tj or TJ blocks
                Pattern textPat = Pattern.compile("\\(([^()]{2,})\\)\\s*T[jd]");
                Matcher mt = textPat.matcher(content);
                int count = 0;
                while (mt.find() && count < 200) {
                    String str = mt.group(1).trim();
                    if (!str.isEmpty()) {
                        sb.append(str).append(" ");
                        count++;
                    }
                }
            }
        } catch (Exception e) {
            sb.append("Error parsing text stream: ").append(e.getMessage());
        }

        return sb.toString();
    }

    public static boolean exportToMarkdown(String pdfPath, String mdPath) {
        try {
            String extracted = extractText(pdfPath);
            File file = new File(mdPath);
            if (file.getParentFile() != null) file.getParentFile().mkdirs();

            try (FileOutputStream fos = new FileOutputStream(file)) {
                fos.write(("# Document Transcript\n\nExported via PSD PDF Studio Engine\n\n" + extracted).getBytes("UTF-8"));
            }
            return true;
        } catch (Exception e) {
            return false;
        }
    }
}
