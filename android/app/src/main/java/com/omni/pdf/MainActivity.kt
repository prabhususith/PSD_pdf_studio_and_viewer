package com.omni.pdf

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import com.omni.pdf.scanner.CameraScanActivity
import com.omni.pdf.converters.TextToPdfConverter
import com.omni.pdf.converters.PdfToTextExtractor

class MainActivity : FlutterActivity() {
    private val SCANNER_CHANNEL = "com.omni.pdf/scanner"
    private val CONVERTER_CHANNEL = "com.omni.pdf/converter"
    private val REQUEST_SCAN_CODE = 1001
    private var pendingScanResult: MethodChannel.Result? = null

    companion object {
        init {
            try {
                System.loadLibrary("pdfengine")
            } catch (e: UnsatisfiedLinkError) {
                // Library might be bundled or statically linked
            }
        }
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, SCANNER_CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "startCameraScan" -> {
                    pendingScanResult = result
                    val intent = Intent(this, CameraScanActivity::class.java)
                    startActivityForResult(intent, REQUEST_SCAN_CODE)
                }
                else -> result.notImplemented()
            }
        }

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CONVERTER_CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "convertTextToPdf" -> {
                    val text = call.argument<String>("text") ?: ""
                    val outputPath = call.argument<String>("outputPath") ?: ""
                    val fontSize = call.argument<Double>("fontSize") ?: 12.0
                    val watermark = call.argument<String>("watermark") ?: ""
                    
                    val success = TextToPdfConverter.convert(text, outputPath, fontSize, watermark)
                    result.success(success)
                }
                "extractPdfText" -> {
                    val pdfPath = call.argument<String>("pdfPath") ?: ""
                    val text = PdfToTextExtractor.extractText(pdfPath)
                    result.success(text)
                }
                "exportToMarkdown" -> {
                    val pdfPath = call.argument<String>("pdfPath") ?: ""
                    val mdPath = call.argument<String>("mdPath") ?: ""
                    val success = PdfToTextExtractor.exportToMarkdown(pdfPath, mdPath)
                    result.success(success)
                }
                else -> result.notImplemented()
            }
        }
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == REQUEST_SCAN_CODE) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                val scannedPath = data.getStringExtra("SCANNED_IMAGE_PATH")
                pendingScanResult?.success(scannedPath)
            } else {
                pendingScanResult?.error("SCAN_CANCELLED", "Scan was cancelled by user", null)
            }
            pendingScanResult = null
        }
    }
}
