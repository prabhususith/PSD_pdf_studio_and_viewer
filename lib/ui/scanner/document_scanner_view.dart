import 'dart:math' as math;
import 'package:flutter/material.dart';

class DocumentScannerView extends StatefulWidget {
  final String imagePath;
  final Function(List<Offset> corners) onCornersConfirmed;

  const DocumentScannerView({
    Key? key,
    required this.imagePath,
    required this.onCornersConfirmed,
  }) : super(key: key);

  @override
  State<DocumentScannerView> createState() => _DocumentScannerViewState();
}

class _DocumentScannerViewState extends State<DocumentScannerView> {
  // 4 draggable quad corners: TL, TR, BR, BL
  List<Offset> _corners = [
    const Offset(50, 80),
    const Offset(300, 80),
    const Offset(310, 420),
    const Offset(40, 410),
  ];

  int? _activeCornerIndex;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        title: const Text("Adjust Crop Boundary"),
        backgroundColor: Colors.black,
        actions: [
          IconButton(
            icon: const Icon(Icons.check, color: Colors.greenAccent),
            tooltip: "Confirm Boundary & Warp",
            onPressed: () {
              widget.onCornersConfirmed(_corners);
              Navigator.of(context).pop();
            },
          ),
        ],
      ),
      body: LayoutBuilder(
        builder: (context, constraints) {
          final viewW = constraints.maxWidth;
          final viewH = constraints.maxHeight;

          return Stack(
            children: [
              // Placeholder simulated scanned image canvas
              Center(
                child: Container(
                  width: math.min(viewW * 0.9, 360),
                  height: math.min(viewH * 0.8, 500),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF0F0EC),
                    borderRadius: BorderRadius.circular(4),
                    boxShadow: const [
                      BoxShadow(color: Colors.white24, blurRadius: 10)
                    ],
                  ),
                  child: Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.document_scanner, size: 64, color: Colors.grey),
                        const SizedBox(height: 12),
                        Text(
                          "Document Frame\nDrag 4 corner handles to calibrate bounds",
                          textAlign: TextAlign.center,
                          style: TextStyle(color: Colors.grey.shade700, fontSize: 13),
                        ),
                      ],
                    ),
                  ),
                ),
              ),

              // Quad polygon overlay painter
              CustomPaint(
                size: Size(viewW, viewH),
                painter: _QuadBoundaryPainter(_corners),
              ),

              // Draggable handles
              ...List.generate(4, (index) {
                final pt = _corners[index];
                return Positioned(
                  left: pt.dx - 20,
                  top: pt.dy - 20,
                  child: GestureDetector(
                    onPanStart: (_) {
                      setState(() {
                        _activeCornerIndex = index;
                      });
                    },
                    onPanUpdate: (details) {
                      setState(() {
                        _corners[index] = Offset(
                          (_corners[index].dx + details.delta.dx).clamp(10, viewW - 10),
                          (_corners[index].dy + details.delta.dy).clamp(10, viewH - 10),
                        );
                      });
                    },
                    onPanEnd: (_) {
                      setState(() {
                        _activeCornerIndex = null;
                      });
                    },
                    child: Container(
                      width: 40,
                      height: 40,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: _activeCornerIndex == index ? Colors.blueAccent : Colors.white,
                        border: Border.all(color: Colors.blueAccent, width: 3),
                        boxShadow: const [
                          BoxShadow(color: Colors.black54, blurRadius: 6, offset: Offset(0, 2))
                        ],
                      ),
                      child: Center(
                        child: Text(
                          "${index + 1}",
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: _activeCornerIndex == index ? Colors.white : Colors.blueAccent,
                          ),
                        ),
                      ),
                    ),
                  ),
                );
              }),

              // Magnifier indicator when dragging
              if (_activeCornerIndex != null)
                Positioned(
                  top: 20,
                  left: 20,
                  child: Container(
                    width: 100,
                    height: 100,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: Colors.black87,
                      border: Border.all(color: Colors.blueAccent, width: 2),
                    ),
                    child: Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.search, size: 28, color: Colors.blueAccent),
                          Text(
                            "(${_corners[_activeCornerIndex!].dx.toInt()}, ${_corners[_activeCornerIndex!].dy.toInt()})",
                            style: const TextStyle(color: Colors.white, fontSize: 10),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}

class _QuadBoundaryPainter extends CustomPainter {
  final List<Offset> corners;

  _QuadBoundaryPainter(this.corners);

  @override
  void paint(Canvas canvas, Size size) {
    if (corners.length < 4) return;

    final fillPaint = Paint()
      ..color = Colors.blueAccent.withOpacity(0.18)
      ..style = PaintingStyle.fill;

    final strokePaint = Paint()
      ..color = Colors.blueAccent
      ..strokeWidth = 2.5
      ..style = PaintingStyle.stroke;

    final path = Path()
      ..moveTo(corners[0].dx, corners[0].dy)
      ..lineTo(corners[1].dx, corners[1].dy)
      ..lineTo(corners[2].dx, corners[2].dy)
      ..lineTo(corners[3].dx, corners[3].dy)
      ..close();

    canvas.drawPath(path, fillPaint);
    canvas.drawPath(path, strokePaint);
  }

  @override
  bool shouldRepaint(covariant _QuadBoundaryPainter oldDelegate) => true;
}
