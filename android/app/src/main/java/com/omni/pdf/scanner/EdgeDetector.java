package com.omni.pdf.scanner;

import android.graphics.PointF;
import java.util.ArrayList;
import java.util.List;

public class EdgeDetector {
    public List<PointF> findDocumentCorners(int width, int height) {
        float mx = width * 0.08f;
        float my = height * 0.08f;
        List<PointF> corners = new ArrayList<>();
        corners.add(new PointF(mx, my));
        corners.add(new PointF(width - mx, my));
        corners.add(new PointF(width - mx, height - my));
        corners.add(new PointF(mx, height - my));
        return corners;
    }
}
