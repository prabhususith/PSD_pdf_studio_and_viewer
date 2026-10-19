import 'package:flutter/material.dart';
import '../../state/document_state.dart';
import '../themes/theme_definitions.dart';

class AnnotationToolbar extends StatelessWidget {
  final DocumentStateManager stateManager;
  final VoidCallback onOpenScanner;
  final VoidCallback onOpenConverter;
  final VoidCallback onOpenPageOrganizer;

  const AnnotationToolbar({
    Key? key,
    required this.stateManager,
    required this.onOpenScanner,
    required this.onOpenConverter,
    required this.onOpenPageOrganizer,
  }) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: stateManager,
      builder: (context, _) {
        final currentTheme = OmniTheme.getTheme(stateManager.currentTheme);
        final tab = stateManager.activeTab;

        return Container(
          height: 48,
          decoration: BoxDecoration(
            color: currentTheme.ribbonBackground,
            border: Border(bottom: BorderSide(color: currentTheme.toolbarBorder, width: 1)),
          ),
          child: Row(
            children: [
              // Sidebar toggle
              IconButton(
                icon: Icon(
                  stateManager.showThumbnails ? Icons.view_sidebar : Icons.view_sidebar_outlined,
                  size: 20,
                  color: stateManager.showThumbnails ? currentTheme.accentColor : Colors.grey,
                ),
                tooltip: "Toggle Thumbnail Sidebar",
                onPressed: stateManager.toggleThumbnails,
              ),

              const VerticalDivider(width: 1, indent: 8, endIndent: 8),

              // Selection / Hand tools
              _buildToolButton(
                icon: Icons.navigation_outlined,
                tool: ActiveTool.pointer,
                tooltip: "Selection Tool",
                theme: currentTheme,
              ),
              _buildToolButton(
                icon: Icons.pan_tool_outlined,
                tool: ActiveTool.hand,
                tooltip: "Hand / Pan Tool",
                theme: currentTheme,
              ),

              const VerticalDivider(width: 1, indent: 8, endIndent: 8),

              // Annotations
              _buildToolButton(
                icon: Icons.highlight_outlined,
                tool: ActiveTool.highlight,
                tooltip: "Text Highlight",
                theme: currentTheme,
              ),
              _buildToolButton(
                icon: Icons.format_underlined,
                tool: ActiveTool.underline,
                tooltip: "Underline Text",
                theme: currentTheme,
              ),
              _buildToolButton(
                icon: Icons.format_strikethrough,
                tool: ActiveTool.strikeout,
                tooltip: "Strikethrough",
                theme: currentTheme,
              ),
              _buildToolButton(
                icon: Icons.draw_outlined,
                tool: ActiveTool.ink,
                tooltip: "Freehand Vector Ink",
                theme: currentTheme,
              ),
              _buildToolButton(
                icon: Icons.sticky_note_2_outlined,
                tool: ActiveTool.stickyNote,
                tooltip: "Sticky Note",
                theme: currentTheme,
              ),
              _buildToolButton(
                icon: Icons.verified_user_outlined,
                tool: ActiveTool.signature,
                tooltip: "PKCS#7 Digital Signature",
                theme: currentTheme,
              ),

              const VerticalDivider(width: 1, indent: 8, endIndent: 8),

              // Page Matrix Organizer
              IconButton(
                icon: const Icon(Icons.grid_view, size: 20, color: Colors.grey),
                tooltip: "Page Matrix Organizer (Rotate/Split/Merge/Reorder)",
                onPressed: onOpenPageOrganizer,
              ),

              // Camera Scanner
              IconButton(
                icon: const Icon(Icons.document_scanner, size: 20, color: Colors.grey),
                tooltip: "Camera Document Scanner & OCR",
                onPressed: onOpenScanner,
              ),

              // Text Converter
              IconButton(
                icon: const Icon(Icons.transform, size: 20, color: Colors.grey),
                tooltip: "Dual-Direction Text Converter (PDF <-> Text)",
                onPressed: onOpenConverter,
              ),

              const Spacer(),

              // Undo / Redo
              IconButton(
                icon: const Icon(Icons.undo, size: 20, color: Colors.grey),
                tooltip: "Undo",
                onPressed: stateManager.undo,
              ),
              IconButton(
                icon: const Icon(Icons.redo, size: 20, color: Colors.grey),
                tooltip: "Redo",
                onPressed: stateManager.redo,
              ),

              const VerticalDivider(width: 1, indent: 8, endIndent: 8),

              // Zoom controls
              IconButton(
                icon: const Icon(Icons.remove, size: 18, color: Colors.grey),
                tooltip: "Zoom Out",
                onPressed: () {
                  if (tab != null) stateManager.setZoom(tab.zoomLevel - 0.15);
                },
              ),
              Text(
                tab != null ? "${(tab.zoomLevel * 100).toInt()}%" : "100%",
                style: const TextStyle(fontSize: 12, color: Colors.grey),
              ),
              IconButton(
                icon: const Icon(Icons.add, size: 18, color: Colors.grey),
                tooltip: "Zoom In",
                onPressed: () {
                  if (tab != null) stateManager.setZoom(tab.zoomLevel + 0.15);
                },
              ),

              const VerticalDivider(width: 1, indent: 8, endIndent: 8),

              // Theme Selector
              PopupMenuButton<AppThemeMode>(
                icon: const Icon(Icons.palette_outlined, size: 20, color: Colors.grey),
                tooltip: "Switch Theme",
                initialValue: stateManager.currentTheme,
                onSelected: stateManager.setTheme,
                itemBuilder: (context) => [
                  const PopupMenuItem(
                    value: AppThemeMode.fluentDark,
                    child: Text("Fluent Dark"),
                  ),
                  const PopupMenuItem(
                    value: AppThemeMode.oledBlack,
                    child: Text("OLED Black"),
                  ),
                  const PopupMenuItem(
                    value: AppThemeMode.sepiaReading,
                    child: Text("Sepia Reading"),
                  ),
                  const PopupMenuItem(
                    value: AppThemeMode.windowsMica,
                    child: Text("Windows 11 Mica"),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildToolButton({
    required IconData icon,
    required ActiveTool tool,
    required String tooltip,
    required OmniTheme theme,
  }) {
    final isSelected = stateManager.activeTool == tool;

    return IconButton(
      icon: Icon(
        icon,
        size: 20,
        color: isSelected ? theme.accentColor : Colors.grey,
      ),
      tooltip: tooltip,
      onPressed: () => stateManager.setActiveTool(tool),
    );
  }
}
