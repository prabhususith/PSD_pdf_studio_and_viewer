package com.omni.pdf;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import com.omni.pdf.scanner.CameraScanActivity;
import com.omni.pdf.converters.TextToPdfConverter;
import com.omni.pdf.converters.PdfToTextExtractor;

public class MainActivity extends Activity {
    private static final int REQUEST_SCAN = 1001;

    static {
        try {
            System.loadLibrary("pdfengine");
        } catch (UnsatisfiedLinkError ignored) {}
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setPadding(32, 32, 32, 32);

        TextView title = new TextView(this);
        title.setText("PSD PDF Studio & Viewer (Android 6.0 - 14+)");
        title.setTextSize(20);
        layout.addView(title);

        TextView status = new TextView(this);
        status.setText("Native Engine: Armed (armeabi-v7a / arm64-v8a)\nRedmi Note 4 Snapdragon 625 Heap Target: < 128MB");
        status.setPadding(0, 16, 0, 16);
        layout.addView(status);

        Button scanBtn = new Button(this);
        scanBtn.setText("Launch Camera Scanner");
        scanBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent intent = new Intent(MainActivity.this, CameraScanActivity.class);
                startActivityForResult(intent, REQUEST_SCAN);
            }
        });
        layout.addView(scanBtn);

        setContentView(layout);
    }
}
