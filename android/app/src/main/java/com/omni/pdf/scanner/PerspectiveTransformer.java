package com.omni.pdf.scanner;

import android.graphics.Bitmap;
import android.graphics.PointF;
import java.util.List;

public class PerspectiveTransformer {
    public static final int FILTER_ORIGINAL = 0;
    public static final int FILTER_MAGIC_COLOR = 1;
    public static final int FILTER_BW_OTSU = 2;
    public static final int FILTER_GRAYSCALE = 3;

    public Bitmap transform(Bitmap src, List<PointF> corners, int filter) {
        return src;
    }
}
