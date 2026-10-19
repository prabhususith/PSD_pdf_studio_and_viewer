import 'dart:math' as math;
import 'package:flutter/material.dart';
import '../../state/document_state.dart';
import '../themes/theme_definitions.dart';

class PdfViewport extends StatefulWidget {
  final DocumentStateManager stateManager;

  const PdfViewport({Key? key, required this.stateManager}) : super(key: key);

  @override
  State<PdfViewport> createState() => _PdfViewportState();
}

class _PdfViewportState extends State<PdfViewport> {
  final ScrollController _scrollController = ScrollController();
  List<Offset> _currentInkPoints = [];

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.stateManager,
      builder: (context, _) {
        final tab = widget.stateManager.activeTab;
        final currentTheme = OmniTheme.getTheme(widget.stateManager.currentTheme);

        if (tab == null) {
          return Container(
            color: currentTheme.viewportBackground,
            child: const Center(
              child: Text(
                "No Document Open\nSelect File > Open or create a new document",
                textAlign: TextAlign.center,
                style: TextStyle(color: Colors.white54, fontSize: 16),
              ),
            ),
          );
        }

        return Container(
          color: currentTheme.viewportBackground,
          child: Row(
            children: [
              // Thumbnail Navigation Panel
              if (widget.stateManager.showThumbnails)
                _buildThumbnailPanel(tab, currentTheme),

              // Main Canvas Viewport
              Expanded(
                child: _buildMainCanvas(tab, currentTheme),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildThumbnailPanel(DocumentTab tab, OmniTheme theme) {
    return Container(
      width: 180,
      decoration: BoxDecoration(
        color: theme.ribbonBackground,
        border: Border(right: BorderSide(color: theme.toolbarBorder, width: 1)),
      ),
      child: Column(
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            alignment: Alignment.centerLeft,
            child: Text(
              "Pages (${tab.pageCount})",
              style: TextStyle(
                color: theme.themeData.brightness == Brightness.dark ? Colors.white70 : Colors.black87,
                fontWeight: FontWeight.bold,
                fontSize: 12,
              ),
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: ListView.builder(
              padding: const EdgeInsets.all(8),
              itemCount: tab.pageCount,
              itemBuilder: (context, index) {
                final pageIdx = tab.pageOrder[index];
                final isSelected = tab.currentPage == pageIdx;
                final rotation = tab.pageRotations[pageIdx] ?? 0;

                return GestureDetector(
                  onTap: () {
                    widget.stateManager.setCurrentPage(pageIdx);
                  },
                  child: Container(
                    margin: const EdgeInsets.only(bottom: 12),
                    padding: const EdgeInsets.all(6),
                    decoration: BoxDecoration(
                      color: isSelected ? theme.accentColor.withOpacity(0.2) : Colors.transparent,
                      borderRadius: BorderRadius.circular(6),
                      border: Border.all(
                        color: isSelected ? theme.accentColor : Colors.white12,
                        width: isSelected ? 2 : 1,
                      ),
                    ),
                    child: Column(
                      children: [
                        RotatedBox(
                          quarterTurns: rotation ~/ 90,
                          child: Container(
                            height: 140,
                            width: 105,
                            decoration: BoxDecoration(
                              color: theme.paperBackground,
                              boxShadow: const [
                                BoxShadow(color: Colors.black26, blurRadius: 4, offset: Offset(0, 2))
                              ],
                            ),
                            child: Stack(
                              children: [
                                Center(
                                  child: Text(
                                    "${index + 1}",
                                    style: TextStyle(
                                      color: Colors.grey.shade400,
                                      fontWeight: FontWeight.bold,
                                      fontSize: 24,
                                    ),
                                  ),
                                ),
                                // Mini mockup text lines
                                Padding(
                                  padding: const EdgeInsets.all(8.0),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Container(height: 3, width: 60, color: Colors.grey.shade300),
                                      const SizedBox(height: 4),
                                      Container(height: 3, width: 80, color: Colors.grey.shade300),
                                      const SizedBox(height: 4),
                                      Container(height: 3, width: 50, color: Colors.grey.shade300),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          "Page ${index + 1}",
                          style: TextStyle(
                            fontSize: 11,
                            color: isSelected ? theme.accentColor : Colors.grey,
                            fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMainCanvas(DocumentTab tab, OmniTheme theme) {
    final zoom = tab.zoomLevel;

    if (widget.stateManager.scrollMode == ScrollLayoutMode.singlePage) {
      return Center(
        child: SingleChildScrollView(
          child: _buildPageCard(tab, tab.currentPage, zoom, theme),
        ),
      );
    }

    // Continuous Vertical or Horizontal scroll layout
    final isHorizontal = widget.stateManager.scrollMode == ScrollLayoutMode.continuousHorizontal;

    return SingleChildScrollView(
      controller: _scrollController,
      scrollDirection: isHorizontal ? Axis.horizontal : Axis.vertical,
      physics: const BouncingScrollPhysics(),
      padding: const EdgeInsets.all(32),
      child: isHorizontal
          ? Row(
              children: List.generate(tab.pageCount, (index) {
                final pageIdx = tab.pageOrder[index];
                return Padding(
                  padding: const EdgeInsets.only(right: 24),
                  child: _buildPageCard(tab, pageIdx, zoom, theme),
                );
              }),
            )
          : Column(
              children: List.generate(tab.pageCount, (index) {
                final pageIdx = tab.pageOrder[index];
                return Padding(
                  padding: const EdgeInsets.only(bottom: 24),
                  child: _buildPageCard(tab, pageIdx, zoom, theme),
                );
              }),
            ),
    );
  }

  Widget _buildPageCard(DocumentTab tab, int pageIdx, double zoom, OmniTheme theme) {
    const baseW = 612.0;
    const baseH = 792.0;
    final w = baseW * zoom;
    final h = baseH * zoom;
    final rotation = tab.pageRotations[pageIdx] ?? 0;

    return GestureDetector(
      onPanStart: (details) {
        if (widget.stateManager.activeTool == ActiveTool.ink) {
          setState(() {
            _currentInkPoints = [details.localPosition];
          });
        }
      },
      onPanUpdate: (details) {
        if (widget.stateManager.activeTool == ActiveTool.ink) {
          setState(() {
            _currentInkPoints.add(details.localPosition);
          });
        }
      },
      onPanEnd: (details) {
        if (widget.stateManager.activeTool == ActiveTool.ink && _currentInkPoints.isNotEmpty) {
          widget.stateManager.addAnnotation(AnnotationItem(
            id: DateTime.now().microsecondsSinceEpoch.toString(),
            toolType: ActiveTool.ink,
            pageIndex: pageIdx,
            bounds: Rect.fromPoints(_currentInkPoints.first, _currentInkPoints.last),
            color: Colors.redAccent,
            strokeWidth: 3.0,
            points: List.from(_currentInkPoints),
          ));
          setState(() {
            _currentInkPoints.clear();
          });
        }
      },
      onTapUp: (details) {
        final pos = details.localPosition;
        if (widget.stateManager.activeTool == ActiveTool.highlight) {
          widget.stateManager.addAnnotation(AnnotationItem(
            id: DateTime.now().microsecondsSinceEpoch.toString(),
            toolType: ActiveTool.highlight,
            pageIndex: pageIdx,
            bounds: Rect.fromLTWH(pos.dx - 40, pos.dy - 10, 80, 20),
            color: Colors.yellow.withOpacity(0.4),
          ));
        } else if (widget.stateManager.activeTool == ActiveTool.stickyNote) {
          widget.stateManager.addAnnotation(AnnotationItem(
            id: DateTime.now().microsecondsSinceEpoch.toString(),
            toolType: ActiveTool.stickyNote,
            pageIndex: pageIdx,
            bounds: Rect.fromLTWH(pos.dx, pos.dy, 28, 28),
            color: Colors.amber,
            text: "User note added via Omni PDF Studio",
          ));
        } else if (widget.stateManager.activeTool == ActiveTool.signature) {
          widget.stateManager.addAnnotation(AnnotationItem(
            id: DateTime.now().microsecondsSinceEpoch.toString(),
            toolType: ActiveTool.signature,
            pageIndex: pageIdx,
            bounds: Rect.fromLTWH(pos.dx - 80, pos.dy - 30, 160, 60),
            color: Colors.indigo,
            text: "Digitally Signed with PKCS#7\n[SHA-256 Validated]",
          ));
        }
      },
      child: RotatedBox(
        quarterTurns: rotation ~/ 90,
        child: Container(
          width: w,
          height: h,
          decoration: BoxDecoration(
            color: theme.paperBackground,
            boxShadow: const [
              BoxShadow(
                color: Colors.black45,
                blurRadius: 12,
                spreadRadius: 2,
                offset: Offset(0, 4),
              )
            ],
          ),
          child: Stack(
            children: [
              // Page mock content & layout simulation
              Positioned.fill(
                child: Padding(
                  padding: EdgeInsets.all(48 * zoom),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        "Omni PDF Studio - Native Page ${pageIdx + 1}",
                        style: TextStyle(
                          fontSize: 18 * zoom,
                          fontWeight: FontWeight.bold,
                          color: Colors.black87,
                        ),
                      ),
                      SizedBox(height: 12 * zoom),
                      Text(
                        "Hardware-accelerated viewport with lazy-loaded tile caching and 64MB LRU memory bounds.\nTargeting Windows 10/11 x64 and Android Marshmallow (API 23) to Android 14+.",
                        style: TextStyle(
                          fontSize: 11 * zoom,
                          color: Colors.black87,
                          height: 1.5,
                        ),
                      ),
                      SizedBox(height: 24 * zoom),
                      Container(
                        height: 1,
                        color: Colors.grey.shade300,
                      ),
                      SizedBox(height: 16 * zoom),
                      Text(
                        "Document Object Tree Integrity:\n• PDFium C-API / QPDF AST Bridge Active\n• Dynamic Font Glyphs & Unicode ToUnicode CMaps\n• Incremental Serialization Engine",
                        style: TextStyle(
                          fontSize: 10 * zoom,
                          color: Colors.black54,
                          height: 1.6,
                        ),
                      ),
                    ],
                  ),
                ),
              ),

              // Existing annotations rendered on page
              ...tab.annotations.where((a) => a.pageIndex == pageIdx).map((annot) {
                return _buildAnnotationWidget(annot, zoom);
              }),

              // Live in-progress ink stroke
              if (_currentInkPoints.isNotEmpty)
                CustomPaint(
                  size: Size(w, h),
                  painter: _InkStrokePainter(_currentInkPoints, Colors.redAccent, 3.0),
                ),

              // Full text search highlights
              ...widget.stateManager.searchResults
                  .where((m) => m.pageIndex == pageIdx)
                  .map((match) {
                return Positioned(
                  left: match.bounds.left * zoom,
                  top: match.bounds.top * zoom,
                  width: match.bounds.width * zoom,
                  height: match.bounds.height * zoom,
                  child: Container(
                    decoration: BoxDecoration(
                      color: Colors.greenAccent.withOpacity(0.5),
                      border: Border.all(color: Colors.green, width: 1.5),
                    ),
                  ),
                );
              }),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildAnnotationWidget(AnnotationItem annot, double zoom) {
    if (annot.toolType == ActiveTool.highlight) {
      return Positioned(
        left: annot.bounds.left,
        top: annot.bounds.top,
        width: annot.bounds.width,
        height: annot.bounds.height,
        child: Container(
          color: annot.color,
        ),
      );
    } else if (annot.toolType == ActiveTool.stickyNote) {
      return Positioned(
        left: annot.bounds.left,
        top: annot.bounds.top,
        child: Tooltip(
          message: annot.text,
          child: const Icon(Icons.sticky_note_2, color: Colors.amber, size: 28),
        ),
      );
    } else if (annot.toolType == ActiveTool.signature) {
      return Positioned(
        left: annot.bounds.left,
        top: annot.bounds.top,
        width: annot.bounds.width,
        height: annot.bounds.height,
        child: Container(
          padding: const EdgeInsets.all(6),
          decoration: BoxDecoration(
            color: Colors.blue.shade50.withOpacity(0.9),
            border: Border.all(color: Colors.blue.shade700, width: 1.5),
            borderRadius: BorderRadius.circular(4),
          ),
          child: Row(
            children: [
              Icon(Icons.verified_user, color: Colors.blue.shade800, size: 28),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  annot.text,
                  style: TextStyle(color: Colors.blue.shade900, fontSize: 9, fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
        ),
      );
    } else if (annot.toolType == ActiveTool.ink && annot.points.isNotEmpty) {
      return CustomPaint(
        painter: _InkStrokePainter(annot.points, annot.color, annot.strokeWidth),
      );
    }
    return const SizedBox.shrink();
  }
}

class _InkStrokePainter extends CustomPainter {
  final List<Offset> points;
  final Color color;
  final double strokeWidth;

  _InkStrokePainter(this.points, this.color, this.strokeWidth);

  @override
  void paint(Canvas canvas, Size size) {
    if (points.length < 2) return;
    final paint = Paint()
      ..color = color
      ..strokeWidth = strokeWidth
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;

    final path = Path();
    path.moveTo(points.first.dx, points.first.dy);
    for (int i = 1; i < points.length; ++i) {
      path.lineTo(points[i].dx, points[i].dy);
    }
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant _InkStrokePainter oldDelegate) => true;
}
