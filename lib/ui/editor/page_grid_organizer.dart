import 'package:flutter/material.dart';
import '../../state/document_state.dart';
import '../themes/theme_definitions.dart';

class PageGridOrganizerModal extends StatefulWidget {
  final DocumentStateManager stateManager;

  const PageGridOrganizerModal({Key? key, required this.stateManager}) : super(key: key);

  @override
  State<PageGridOrganizerModal> createState() => _PageGridOrganizerModalState();
}

class _PageGridOrganizerModalState extends State<PageGridOrganizerModal> {
  late List<int> _workingOrder;
  final Set<int> _selectedPages = {};

  @override
  void initState() {
    super.initState();
    final tab = widget.stateManager.activeTab;
    _workingOrder = tab != null ? List.from(tab.pageOrder) : [];
  }

  @override
  Widget build(BuildContext context) {
    final tab = widget.stateManager.activeTab;
    final currentTheme = OmniTheme.getTheme(widget.stateManager.currentTheme);

    if (tab == null) {
      return const AlertDialog(title: Text("No document open"));
    }

    return Dialog(
      backgroundColor: currentTheme.ribbonBackground,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      insetPadding: const EdgeInsets.all(32),
      child: Container(
        width: 800,
        height: 600,
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            // Modal Header
            Row(
              children: [
                const Icon(Icons.grid_view, color: Colors.blueAccent),
                const SizedBox(width: 10),
                Text(
                  "Page Matrix Organizer - ${tab.title}",
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const Spacer(),
                IconButton(
                  icon: const Icon(Icons.close),
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
            const Divider(),

            // Action Ribbon for Matrix Operations
            Row(
              children: [
                ElevatedButton.icon(
                  icon: const Icon(Icons.rotate_right, size: 18),
                  label: const Text("Rotate 90°"),
                  style: ElevatedButton.styleFrom(backgroundColor: currentTheme.accentColor),
                  onPressed: () {
                    for (int p in _selectedPages) {
                      widget.stateManager.rotateActivePage(90);
                    }
                    setState(() {});
                  },
                ),
                const SizedBox(width: 8),
                ElevatedButton.icon(
                  icon: const Icon(Icons.call_split, size: 18),
                  label: const Text("Split Document"),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.teal),
                  onPressed: () {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text("Splitting document into individual single-page files...")),
                    );
                  },
                ),
                const SizedBox(width: 8),
                ElevatedButton.icon(
                  icon: const Icon(Icons.merge, size: 18),
                  label: const Text("Merge Files"),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.deepPurple),
                  onPressed: () {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text("Select second PDF to append...")),
                    );
                  },
                ),
                const SizedBox(width: 8),
                ElevatedButton.icon(
                  icon: const Icon(Icons.file_download, size: 18),
                  label: const Text("Extract Selected"),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.indigo),
                  onPressed: _selectedPages.isEmpty
                      ? null
                      : () {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(content: Text("Extracted ${_selectedPages.length} pages to new document")),
                          );
                        },
                ),
                const Spacer(),
                Text(
                  "${_selectedPages.length} pages selected",
                  style: const TextStyle(fontSize: 12, color: Colors.grey),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Drag-and-drop Reorderable Page Grid
            Expanded(
              child: Container(
                decoration: BoxDecoration(
                  color: currentTheme.viewportBackground,
                  borderRadius: BorderRadius.circular(8),
                ),
                padding: const EdgeInsets.all(16),
                child: ReorderableListView(
                  scrollDirection: Axis.vertical,
                  onReorder: (oldIndex, newIndex) {
                    setState(() {
                      if (newIndex > oldIndex) newIndex -= 1;
                      final item = _workingOrder.removeAt(oldIndex);
                      _workingOrder.insert(newIndex, item);
                    });
                    widget.stateManager.reorderPages(_workingOrder);
                  },
                  children: [
                    Wrap(
                      key: const ValueKey("wrap_matrix"),
                      spacing: 16,
                      runSpacing: 16,
                      children: List.generate(_workingOrder.length, (index) {
                        final pageIdx = _workingOrder[index];
                        final isSelected = _selectedPages.contains(pageIdx);
                        final rotation = tab.pageRotations[pageIdx] ?? 0;

                        return GestureDetector(
                          key: ValueKey("page_card_$pageIdx"),
                          onTap: () {
                            setState(() {
                              if (isSelected) {
                                _selectedPages.remove(pageIdx);
                              } else {
                                _selectedPages.add(pageIdx);
                              }
                            });
                          },
                          child: Container(
                            width: 130,
                            padding: const EdgeInsets.all(8),
                            decoration: BoxDecoration(
                              color: isSelected ? currentTheme.accentColor.withOpacity(0.3) : currentTheme.ribbonBackground,
                              border: Border.all(
                                color: isSelected ? currentTheme.accentColor : currentTheme.toolbarBorder,
                                width: isSelected ? 2 : 1,
                              ),
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Column(
                              children: [
                                RotatedBox(
                                  quarterTurns: rotation ~/ 90,
                                  child: Container(
                                    height: 140,
                                    width: 105,
                                    color: currentTheme.paperBackground,
                                    child: Center(
                                      child: Text(
                                        "${pageIdx + 1}",
                                        style: TextStyle(
                                          fontSize: 28,
                                          fontWeight: FontWeight.bold,
                                          color: Colors.grey.shade400,
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(height: 8),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: [
                                    Text(
                                      "Page ${index + 1}",
                                      style: TextStyle(
                                        fontSize: 11,
                                        fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                                      ),
                                    ),
                                    Icon(
                                      Icons.drag_indicator,
                                      size: 16,
                                      color: Colors.grey.shade500,
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        );
                      }),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),

            // Modal footer
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                OutlinedButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text("Done"),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
