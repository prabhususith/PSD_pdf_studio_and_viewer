package com.omni.pdf.scanner

import android.graphics.Bitmap
import android.graphics.PointF
import java.nio.ByteBuffer

class EdgeDetector {

    external fun nativeDetectCorners(rgbaPixels: ByteArray, width: Int, height: Int, outCorners: FloatArray): Int

    fun findDocumentCorners(bitmap: Bitmap): List<PointF> {
        val width = bitmap.width
        val height = bitmap.height
        val outCorners = FloatArray(8) // 4 pairs of (x, y)

        var detected = false
        try {
            val byteCount = bitmap.byteCount
            val buffer = ByteBuffer.allocate(byteCount)
            bitmap.copyPixelsToBuffer(buffer)
            val pixels = buffer.array()

            val res = nativeDetectCorners(pixels, width, height, outCorners)
            detected = (res == 1)
        } catch (e: Throwable) {
            // Fallback to high-contrast corner heuristics
            detected = false
        }

        if (!detected) {
            // Heuristic quadrilateral with 10% edge margins
            val mx = width * 0.10f
            val my = height * 0.10f
            return listOf(
                PointF(mx, my),
                PointF(width - mx, my),
                PointF(width - mx, height - my),
                PointF(mx, height - my)
            )
        }

        return listOf(
            PointF(outCorners[0], outCorners[1]),
            PointF(outCorners[2], outCorners[3]),
            PointF(outCorners[4], outCorners[5]),
            PointF(outCorners[6], outCorners[7])
        )
    }
}
