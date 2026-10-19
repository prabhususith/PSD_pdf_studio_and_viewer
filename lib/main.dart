import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'state/document_state.dart';
import 'ui/themes/theme_definitions.dart';
import 'ui/viewer/pdf_viewport.dart';
import 'ui/tools/annotation_toolbar.dart';
import 'ui/editor/page_grid_organizer.dart';
import 'ui/scanner/document_scanner_view.dart';
import 'ui/scanner/filter_selection_sheet.dart';
import 'ui/scanner/text_converter_dialog.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const OmniPdfApp());
}

class OmniPdfApp extends StatefulWidget {
  const OmniPdfApp({Key? key}) : super(key: key);

  @override
  State<OmniPdfApp> createState() => _OmniPdfAppState();
}

class _OmniPdfAppState extends State<OmniPdfApp> {
  final DocumentStateManager _stateManager = DocumentStateManager();

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _stateManager,
      builder: (context, _) {
        final currentTheme = OmniTheme.getTheme(_stateManager.currentTheme);

        return MaterialApp(
          title: 'Omni PDF Studio',
          debugShowCheckedModeBanner: false,
          theme: currentTheme.themeData,
          home: OmniPdfMainScreen(stateManager: _stateManager),
        );
      },
    );
  }
}

class OmniPdfMainScreen extends StatefulWidget {
  final DocumentStateManager stateManager;

  const OmniPdfMainScreen({Key? key, required this.stateManager}) : super(key: key);

  @override
  State<OmniPdfMainScreen> createState() => _OmniPdfMainScreenState();
}

class _OmniPdfMainScreenState extends State<OmniPdfMainScreen> {
  final TextEditingController _searchController = TextEditingController();
  bool _isSearchOpen = false;

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _openScanner() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => FilterSelectionSheet(
        onFilterApplied: (filter, quality) {
          widget.stateManager.createNewDocument(title: "Scanned Document.pdf", pages: 1);
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text("Document scanned and added with filter applied.")),
          );
        },
      ),
    );
  }

  void _openConverter() {
    showDialog(
      context: context,
      builder: (context) => TextConverterDialog(stateManager: widget.stateManager),
    );
  }

  void _openPageOrganizer() {
    showDialog(
      context: context,
      builder: (context) => PageGridOrganizerModal(stateManager: widget.stateManager),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = OmniTheme.getTheme(widget.stateManager.currentTheme);
    final activeTab = widget.stateManager.activeTab;

    return Scaffold(
      backgroundColor: theme.viewportBackground,
      body: SafeArea(
        child: Column(
          children: [
            // Top Window Bar / Ribbon & Multi-Tab Switcher
            _buildTopTabBar(theme),

            // Main Annotation Toolbar
            AnnotationToolbar(
              stateManager: widget.stateManager,
              onOpenScanner: _openScanner,
              onOpenConverter: _openConverter,
              onOpenPageOrganizer: _openPageOrganizer,
            ),

            // In-page search bar if toggled
            if (_isSearchOpen) _buildSearchBar(theme),

            // Core Viewport Area
            Expanded(
              child: PdfViewport(stateManager: widget.stateManager),
            ),

            // Status Bar at Bottom
            _buildStatusBar(theme, activeTab),
          ],
        ),
      ),
    );
  }

  Widget _buildTopTabBar(OmniTheme theme) {
    return Container(
      height: 40,
      decoration: BoxDecoration(
        color: theme.ribbonBackground,
        border: Border(bottom: BorderSide(color: theme.toolbarBorder, width: 1)),
      ),
      child: Row(
        children: [
          // App Logo / Title
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 12),
            child: Row(
              children: [
                Icon(Icons.picture_as_pdf, color: Colors.redAccent, size: 20),
                SizedBox(width: 8),
                Text(
                  "Omni PDF",
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                ),
              ],
            ),
          ),
          const VerticalDivider(width: 1),

          // Multi-tab document headers
          Expanded(
            child: ListView.builder(
              scrollDirection: Axis.horizontal,
              itemCount: widget.stateManager.tabs.length,
              itemBuilder: (context, index) {
                final tab = widget.stateManager.tabs[index];
                final isSelected = widget.stateManager.activeTabIndex == index;

                return GestureDetector(
                  onTap: () => widget.stateManager.selectTab(index),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    margin: const EdgeInsets.only(top: 4, right: 2),
                    decoration: BoxDecoration(
                      color: isSelected ? theme.viewportBackground : Colors.transparent,
                      borderRadius: const BorderRadius.vertical(top: Radius.circular(6)),
                      border: Border(
                        top: BorderSide(color: isSelected ? theme.accentColor : Colors.transparent, width: 2),
                        left: BorderSide(color: isSelected ? theme.toolbarBorder : Colors.transparent, width: 1),
                        right: BorderSide(color: isSelected ? theme.toolbarBorder : Colors.transparent, width: 1),
                      ),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          tab.title,
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                            color: isSelected ? Colors.white : Colors.grey,
                          ),
                        ),
                        if (tab.isModified)
                          Container(
                            margin: const EdgeInsets.only(left: 4),
                            width: 6,
                            height: 6,
                            decoration: const BoxDecoration(shape: BoxShape.circle, color: Colors.blueAccent),
                          ),
                        const SizedBox(width: 8),
                        GestureDetector(
                          onTap: () => widget.stateManager.closeTab(index),
                          child: const Icon(Icons.close, size: 14, color: Colors.grey),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),

          // Add Document Button
          IconButton(
            icon: const Icon(Icons.add, size: 18),
            tooltip: "New Document",
            onPressed: () {
              final count = widget.stateManager.tabs.length + 1;
              widget.stateManager.createNewDocument(title: "Untitled $count.pdf");
            },
          ),

          // Search Toggle Button
          IconButton(
            icon: Icon(Icons.search, size: 18, color: _isSearchOpen ? theme.accentColor : Colors.grey),
            tooltip: "Find Text (Ctrl+F)",
            onPressed: () {
              setState(() {
                _isSearchOpen = !_isSearchOpen;
              });
            },
          ),
        ],
      ),
    );
  }

  Widget _buildSearchBar(OmniTheme theme) {
    return Container(
      height: 42,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        color: theme.ribbonBackground,
        border: Border(bottom: BorderSide(color: theme.toolbarBorder, width: 1)),
      ),
      child: Row(
        children: [
          const Icon(Icons.search, size: 18, color: Colors.grey),
          const SizedBox(width: 8),
          Expanded(
            child: TextField(
              controller: _searchController,
              style: const TextStyle(fontSize: 12),
              decoration: const InputDecoration(
                hintText: "Search in document...",
                border: InputBorder.none,
                isDense: true,
              ),
              onChanged: (val) {
                widget.stateManager.search(val);
              },
            ),
          ),
          if (widget.stateManager.searchResults.isNotEmpty)
            Text(
              "${widget.stateManager.currentSearchIndex + 1} of ${widget.stateManager.searchResults.length}",
              style: const TextStyle(fontSize: 11, color: Colors.grey),
            ),
          IconButton(
            icon: const Icon(Icons.keyboard_arrow_up, size: 18),
            onPressed: widget.stateManager.prevSearchMatch,
          ),
          IconButton(
            icon: const Icon(Icons.keyboard_arrow_down, size: 18),
            onPressed: widget.stateManager.nextSearchMatch,
          ),
          IconButton(
            icon: const Icon(Icons.close, size: 18),
            onPressed: () {
              setState(() {
                _isSearchOpen = false;
                widget.stateManager.search('');
              });
            },
          ),
        ],
      ),
    );
  }

  Widget _buildStatusBar(OmniTheme theme, DocumentTab? tab) {
    return Container(
      height: 26,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: theme.ribbonBackground,
        border: Border(top: BorderSide(color: theme.toolbarBorder, width: 1)),
      ),
      child: Row(
        children: [
          Text(
            tab != null ? "Page ${tab.currentPage + 1} of ${tab.pageCount}" : "Ready",
            style: const TextStyle(fontSize: 11, color: Colors.grey),
          ),
          const SizedBox(width: 16),
          const Icon(Icons.memory, size: 14, color: Colors.greenAccent),
          const SizedBox(width: 4),
          const Text(
            "LRU Cache: < 64MB Cap",
            style: TextStyle(fontSize: 11, color: Colors.grey),
          ),
          const SizedBox(width: 16),
          const Icon(Icons.lock_outline, size: 14, color: Colors.blueAccent),
          const SizedBox(width: 4),
          const Text(
            "PKCS#7 Crypto Ready",
            style: TextStyle(fontSize: 11, color: Colors.grey),
          ),
          const Spacer(),
          // Layout switchers
          GestureDetector(
            onTap: () => widget.stateManager.setScrollMode(ScrollLayoutMode.continuousVertical),
            child: Icon(
              Icons.swap_vert,
              size: 16,
              color: widget.stateManager.scrollMode == ScrollLayoutMode.continuousVertical
                  ? theme.accentColor
                  : Colors.grey,
            ),
          ),
          const SizedBox(width: 8),
          GestureDetector(
            onTap: () => widget.stateManager.setScrollMode(ScrollLayoutMode.continuousHorizontal),
            child: Icon(
              Icons.swap_horiz,
              size: 16,
              color: widget.stateManager.scrollMode == ScrollLayoutMode.continuousHorizontal
                  ? theme.accentColor
                  : Colors.grey,
            ),
          ),
          const SizedBox(width: 8),
          GestureDetector(
            onTap: () => widget.stateManager.setScrollMode(ScrollLayoutMode.singlePage),
            child: Icon(
              Icons.crop_portrait,
              size: 16,
              color: widget.stateManager.scrollMode == ScrollLayoutMode.singlePage
                  ? theme.accentColor
                  : Colors.grey,
            ),
          ),
        ],
      ),
    );
  }
}
