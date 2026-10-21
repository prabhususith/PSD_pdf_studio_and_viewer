package com.omni.pdf;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.ColorMatrix;
import android.graphics.ColorMatrixColorFilter;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PointF;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.View;
import java.util.ArrayList;
import java.util.List;

/**
 * High-Performance Hardware-Accelerated Vector PDF Page Canvas.
 * Supports smooth pinch-to-zoom, pan gesture navigation, live stylus/pen sketch overlay,
 * night mode inverted rendering, and double-buffering.
 */
public class PdfPageView extends View {

    public static class InkStroke {
        public Path path;
        public int color;
        public float width;

        public InkStroke(Path path, int color, float width) {
            this.path = path;
            this.color = color;
            this.width = width;
        }
    }

    private Bitmap mBitmap;
    private final Matrix mMatrix = new Matrix();
    private final Matrix mInverseMatrix = new Matrix();
    private final Paint mBitmapPaint = new Paint(Paint.FILTER_BITMAP_FLAG);
    private final Paint mNightPaint = new Paint(Paint.FILTER_BITMAP_FLAG);
    private final Paint mInkPaint = new Paint(Paint.ANTI_ALIAS_FLAG);

    private final ScaleGestureDetector mScaleDetector;
    private float mScaleFactor = 1.0f;
    private float mFocusX = 0f;
    private float mFocusY = 0f;

    private float mLastTouchX;
    private float mLastTouchY;
    private static final int INVALID_POINTER_ID = -1;
    private int mActivePointerId = INVALID_POINTER_ID;

    private boolean mNightMode = false;
    private boolean mInkMode = false;
    private int mCurrentColor = 0xFFDC2626; // Modern Red default
    private float mCurrentStrokeWidth = 6.0f;

    private final List<InkStroke> mStrokes = new ArrayList<>();
    private Path mCurrentPath;

    public PdfPageView(Context context) {
        super(context);

        mScaleDetector = new ScaleGestureDetector(context, new ScaleListener());

        // Setup Inverted Night Mode ColorMatrix Filter
        ColorMatrix cm = new ColorMatrix(new float[]{
            -1.0f,  0.0f,  0.0f, 0.0f, 255.0f,
             0.0f, -1.0f,  0.0f, 0.0f, 255.0f,
             0.0f,  0.0f, -1.0f, 0.0f, 255.0f,
             0.0f,  0.0f,  0.0f, 1.0f,   0.0f
        });
        mNightPaint.setColorFilter(new ColorMatrixColorFilter(cm));

        mInkPaint.setStyle(Paint.Style.STROKE);
        mInkPaint.setStrokeJoin(Paint.Join.ROUND);
        mInkPaint.setStrokeCap(Paint.Cap.ROUND);
    }

    public void setPageBitmap(Bitmap bitmap) {
        if (mBitmap != null && mBitmap != bitmap && !mBitmap.isRecycled()) {
            mBitmap.recycle();
        }
        mBitmap = bitmap;
        resetZoomAndPan();
        mStrokes.clear();
        invalidate();
    }

    public void recycleBitmap() {
        if (mBitmap != null && !mBitmap.isRecycled()) {
            mBitmap.recycle();
            mBitmap = null;
            invalidate();
        }
    }

    @Override
    protected void onDetachedFromWindow() {
        super.onDetachedFromWindow();
        recycleBitmap();
    }

    public Bitmap getPageBitmap() {
        return mBitmap;
    }

    public void setNightMode(boolean enabled) {
        mNightMode = enabled;
        invalidate();
    }

    public boolean isNightMode() {
        return mNightMode;
    }

    public void setInkMode(boolean enabled) {
        mInkMode = enabled;
    }

    public boolean isInkMode() {
        return mInkMode;
    }

    public void setInkColor(int color) {
        mCurrentColor = color;
    }

    public int getInkColor() {
        return mCurrentColor;
    }

    public void setInkWidth(float width) {
        mCurrentStrokeWidth = width;
    }

    public void undoLastStroke() {
        if (!mStrokes.isEmpty()) {
            mStrokes.remove(mStrokes.size() - 1);
            invalidate();
        }
    }

    public void clearAllStrokes() {
        mStrokes.clear();
        invalidate();
    }

    public void zoomIn() {
        mScaleFactor = Math.min(mScaleFactor * 1.25f, 6.0f);
        mMatrix.postScale(1.25f, 1.25f, getWidth() / 2f, getHeight() / 2f);
        invalidate();
    }

    public void zoomOut() {
        mScaleFactor = Math.max(mScaleFactor / 1.25f, 0.4f);
        mMatrix.postScale(1.0f / 1.25f, 1.0f / 1.25f, getWidth() / 2f, getHeight() / 2f);
        invalidate();
    }

    public void resetZoomAndPan() {
        mScaleFactor = 1.0f;
        mMatrix.reset();
        if (mBitmap != null && getWidth() > 0 && getHeight() > 0) {
            float scaleX = (float) getWidth() / mBitmap.getWidth();
            float scaleY = (float) getHeight() / mBitmap.getHeight();
            float scale = Math.min(scaleX, scaleY);
            if (scale > 0) {
                float dx = (getWidth() - mBitmap.getWidth() * scale) / 2f;
                float dy = (getHeight() - mBitmap.getHeight() * scale) / 2f;
                mMatrix.setScale(scale, scale);
                mMatrix.postTranslate(dx, dy);
                mScaleFactor = scale;
            }
        }
        invalidate();
    }

    @Override
    protected void onSizeChanged(int w, int h, int oldw, int oldh) {
        super.onSizeChanged(w, h, oldw, oldh);
        if (mBitmap != null && mScaleFactor == 1.0f) {
            resetZoomAndPan();
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent ev) {
        if (mInkMode) {
            // Live Freehand Inking Overlay Mode
            float x = ev.getX();
            float y = ev.getY();

            switch (ev.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    mCurrentPath = new Path();
                    mCurrentPath.moveTo(x, y);
                    mStrokes.add(new InkStroke(mCurrentPath, mCurrentColor, mCurrentStrokeWidth));
                    invalidate();
                    return true;

                case MotionEvent.ACTION_MOVE:
                    if (mCurrentPath != null) {
                        int historySize = ev.getHistorySize();
                        for (int h = 0; h < historySize; h++) {
                            mCurrentPath.lineTo(ev.getHistoricalX(h), ev.getHistoricalY(h));
                        }
                        mCurrentPath.lineTo(x, y);
                        invalidate();
                    }
                    return true;

                case MotionEvent.ACTION_UP:
                case MotionEvent.ACTION_CANCEL:
                    mCurrentPath = null;
                    invalidate();
                    return true;
            }
            return super.onTouchEvent(ev);
        }

        // Vector Canvas Pan & Scale Navigation Mode
        mScaleDetector.onTouchEvent(ev);

        final int action = ev.getActionMasked();
        switch (action) {
            case MotionEvent.ACTION_DOWN: {
                final int pointerIndex = ev.getActionIndex();
                final float x = ev.getX(pointerIndex);
                final float y = ev.getY(pointerIndex);

                mLastTouchX = x;
                mLastTouchY = y;
                mActivePointerId = ev.getPointerId(0);
                break;
            }

            case MotionEvent.ACTION_MOVE: {
                final int pointerIndex = ev.findPointerIndex(mActivePointerId);
                if (pointerIndex != -1) {
                    final float x = ev.getX(pointerIndex);
                    final float y = ev.getY(pointerIndex);

                    if (!mScaleDetector.isInProgress()) {
                        final float dx = x - mLastTouchX;
                        final float dy = y - mLastTouchY;
                        mMatrix.postTranslate(dx, dy);
                        invalidate();
                    }

                    mLastTouchX = x;
                    mLastTouchY = y;
                }
                break;
            }

            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL: {
                mActivePointerId = INVALID_POINTER_ID;
                break;
            }

            case MotionEvent.ACTION_POINTER_UP: {
                final int pointerIndex = ev.getActionIndex();
                final int pointerId = ev.getPointerId(pointerIndex);
                if (pointerId == mActivePointerId) {
                    final int newPointerIndex = pointerIndex == 0 ? 1 : 0;
                    mLastTouchX = ev.getX(newPointerIndex);
                    mLastTouchY = ev.getY(newPointerIndex);
                    mActivePointerId = ev.getPointerId(newPointerIndex);
                }
                break;
            }
        }

        return true;
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);

        // Draw Canvas Backdrop
        canvas.drawColor(mNightMode ? 0xFF0D0D11 : 0xFF18181B);

        if (mBitmap != null && !mBitmap.isRecycled()) {
            Paint activePaint = mNightMode ? mNightPaint : mBitmapPaint;
            canvas.drawBitmap(mBitmap, mMatrix, activePaint);
        }

        // Draw Inking / Markup Overlay on Top
        for (InkStroke stroke : mStrokes) {
            mInkPaint.setColor(stroke.color);
            mInkPaint.setStrokeWidth(stroke.width);
            canvas.drawPath(stroke.path, mInkPaint);
        }
    }

    private class ScaleListener extends ScaleGestureDetector.SimpleOnScaleGestureListener {
        @Override
        public boolean onScale(ScaleGestureDetector detector) {
            float scale = detector.getScaleFactor();
            float newScale = mScaleFactor * scale;
            if (newScale >= 0.4f && newScale <= 6.0f) {
                mScaleFactor = newScale;
                mMatrix.postScale(scale, scale, detector.getFocusX(), detector.getFocusY());
                invalidate();
            }
            return true;
        }
    }
}
