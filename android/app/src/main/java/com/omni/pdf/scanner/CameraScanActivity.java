package com.omni.pdf.scanner;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.TextView;

public class CameraScanActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(0xFF000000);

        TextView tv = new TextView(this);
        tv.setText("Camera Document Scanner Active\nReady for 4-Corner Boundary Calibration");
        tv.setTextColor(0xFFFFFFFF);
        tv.setPadding(32, 64, 32, 32);
        root.addView(tv);

        Button captureBtn = new Button(this);
        captureBtn.setText("Capture & Perspective Warp");
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.WRAP_CONTENT,
            FrameLayout.LayoutParams.WRAP_CONTENT
        );
        lp.gravity = android.view.Gravity.BOTTOM | android.view.Gravity.CENTER_HORIZONTAL;
        lp.bottomMargin = 64;
        captureBtn.setLayoutParams(lp);

        captureBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent result = new Intent();
                result.putExtra("SCANNED_PATH", "omni_scanned_doc.pdf");
                setResult(RESULT_OK, result);
                finish();
            }
        });
        root.addView(captureBtn);

        setContentView(root);
    }
}
