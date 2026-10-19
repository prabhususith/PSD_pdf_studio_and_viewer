package com.omni.pdf.scanner

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Matrix
import android.graphics.PointF
import java.nio.ByteBuffer
import kotlin.math.hypot
import kotlin.math.max
import kotlin.math.min

class PerspectiveTransformer {

    enum class FilterType(val code: Int) {
        ORIGINAL(0),
        MAGIC_COLOR(1),
        BW_OTSU(2),
        GRAYSCALE(3)
    }

    external fun nativeWarpPerspective(
        srcRgba: ByteArray,
        srcW: Int,
        srcH: Int,
        srcCorners: FloatArray,
        dstW: Int,
        dstH: Int,
        outDstRgba: ByteArray
    ): Int

    external fun nativeApplyScanFilter(
        inOutRgba: ByteArray,
        w: Int,
        h: Int,
        filterMode: Int
    ): Int

    fun transformAndFilter(
        srcBitmap: Bitmap,
        corners: List<PointF>,
        filter: FilterType
    ): Bitmap {
        if (corners.size != 4) return srcBitmap

        val p0 = corners[0]
        val p1 = corners[1]
        val p2 = corners[2]
        val p3 = corners[3]

        // Calculate planar width and height
        val wTop = hypot((p1.x - p0.x).toDouble(), (p1.y - p0.y).toDouble())
        val wBtm = hypot((p2.x - p3.x).toDouble(), (p2.y - p3.y).toDouble())
        val targetW = max(wTop, wBtm).toInt()

        val hLeft = hypot((p3.x - p0.x).toDouble(), (p3.y - p0.y).toDouble())
        val hRight = hypot((p2.x - p1.x).toDouble(), (p2.y - p1.y).toDouble())
        val targetH = max(hLeft, hRight).toInt()

        // Restrict to max 1920x1080 for Redmi Note 4 heap constraint
        val scale = min(1.0, 1920.0 / max(targetW, targetH))
        val dstW = max(100, (targetW * scale).toInt())
        val dstH = max(100, (targetH * scale).toInt())

        val srcPoints = floatArrayOf(
            p0.x, p0.y,
            p1.x, p1.y,
            p2.x, p2.y,
            p3.x, p3.y
        )
        val dstPoints = floatArrayOf(
            0f, 0f,
            dstW.toFloat(), 0f,
            dstW.toFloat(), dstH.toFloat(),
            0f, dstH.toFloat()
        )

        // First attempt native C++ homography
        try {
            val srcBuffer = ByteBuffer.allocate(srcBitmap.byteCount)
            srcBitmap.copyPixelsToBuffer(srcBuffer)
            val srcBytes = srcBuffer.array()

            val dstBytes = ByteArray(dstW * dstH * 4)
            val nativeRes = nativeWarpPerspective(
                srcBytes,
                srcBitmap.width,
                srcBitmap.height,
                srcPoints,
                dstW,
                dstH,
                dstBytes
            )

            if (nativeRes == 1) {
                if (filter != FilterType.ORIGINAL) {
                    nativeApplyScanFilter(dstBytes, dstW, dstH, filter.code)
                }
                val resultBitmap = Bitmap.createBitmap(dstW, dstH, Bitmap.Config.ARGB_8888)
                resultBitmap.copyPixelsFromBuffer(ByteBuffer.wrap(dstBytes))
                return resultBitmap
            }
        } catch (e: Throwable) {
            // Fallback to Android Matrix polyToPoly
        }

        // Android Canvas Poly-to-Poly fallback
        val matrix = Matrix()
        matrix.setPolyToPoly(srcPoints, 0, dstPoints, 0, 4)

        val output = Bitmap.createBitmap(dstW, dstH, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(output)
        canvas.drawBitmap(srcBitmap, matrix, null)

        return output
    }
}
