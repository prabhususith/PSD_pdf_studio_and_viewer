import 'dart:ffi' as ffi;
import 'package:flutter/material.dart';
import '../core/native_bindings.dart';
import '../ui/themes/theme_definitions.dart';

enum ActiveTool {
  pointer,
  hand,
  highlight,
  underline,
  strikeout,
  ink,
  stickyNote,
  signature,
  textEdit,
}

enum ScrollLayoutMode {
  continuousVertical,
  continuousHorizontal,
  singlePage,
}

class AnnotationItem {
  final String id;
  final ActiveTool toolType;
  final int pageIndex;
  final Rect bounds;
  final Color color;
  final double strokeWidth;
  final String text;
  final List<Offset> points;

  AnnotationItem({
    required this.id,
    required this.toolType,
    required this.pageIndex,
    required this.bounds,
    required this.color,
    this.strokeWidth = 2.0,
    this.text = '',
    this.points = const [],
  });
}

class SearchMatch {
  final int pageIndex;
  final String matchedText;
  final Rect bounds;

  SearchMatch({required this.pageIndex, required this.matchedText, required this.bounds});
}

class DocumentTab {
  final String id;
  String title;
  String? filePath;
  ffi.Pointer<ffi.Void>? nativeDocHandle;
  int pageCount;
  int currentPage;
  double zoomLevel;
  Map<int, int> pageRotations; // page -> 0, 90, 180, 270
  List<AnnotationItem> annotations;
  List<int> pageOrder; // For drag and drop reordering
  bool isModified;

  DocumentTab({
    required this.id,
    required this.title,
    this.filePath,
    this.nativeDocHandle,
    this.pageCount = 1,
    this.currentPage = 0,
    this.zoomLevel = 1.0,
    Map<int, int>? pageRotations,
    List<AnnotationItem>? annotations,
    List<int>? pageOrder,
    this.isModified = false,
  })  : pageRotations = pageRotations ?? {},
        annotations = annotations ?? [],
        pageOrder = pageOrder ?? List.generate(pageCount, (i) => i);
}

class DocumentStateManager extends ChangeNotifier {
  final List<DocumentTab> _tabs = [];
  int _activeTabIndex = -1;
  ActiveTool _activeTool = ActiveTool.pointer;
  ScrollLayoutMode _scrollMode = ScrollLayoutMode.continuousVertical;
  AppThemeMode _currentTheme = AppThemeMode.fluentDark;
  bool _showThumbnails = true;

  // Search
  String _searchQuery = '';
  final List<SearchMatch> _searchResults = [];
  int _currentSearchIndex = -1;

  // Undo / Redo stacks
  final List<List<AnnotationItem>> _undoStack = [];
  final List<List<AnnotationItem>> _redoStack = [];

  DocumentStateManager() {
    // Open a default blank document workspace
    createNewDocument(title: "Untitled 1.pdf");
  }

  List<DocumentTab> get tabs => List.unmodifiable(_tabs);
  int get activeTabIndex => _activeTabIndex;
  DocumentTab? get activeTab => (_activeTabIndex >= 0 && _activeTabIndex < _tabs.length) ? _tabs[_activeTabIndex] : null;

  ActiveTool get activeTool => _activeTool;
  ScrollLayoutMode get scrollMode => _scrollMode;
  AppThemeMode get currentTheme => _currentTheme;
  bool get showThumbnails => _showThumbnails;
  String get searchQuery => _searchQuery;
  List<SearchMatch> get searchResults => List.unmodifiable(_searchResults);
  int get currentSearchIndex => _currentSearchIndex;

  void setTheme(AppThemeMode theme) {
    _currentTheme = theme;
    notifyListeners();
  }

  void setActiveTool(ActiveTool tool) {
    _activeTool = tool;
    notifyListeners();
  }

  void setScrollMode(ScrollLayoutMode mode) {
    _scrollMode = mode;
    notifyListeners();
  }

  void toggleThumbnails() {
    _showThumbnails = !_showThumbnails;
    notifyListeners();
  }

  void selectTab(int index) {
    if (index >= 0 && index < _tabs.length) {
      _activeTabIndex = index;
      _clearUndoRedo();
      notifyListeners();
    }
  }

  void closeTab(int index) {
    if (index >= 0 && index < _tabs.length) {
      final tab = _tabs[index];
      if (tab.nativeDocHandle != null) {
        OmniNativeEngine.instance.closeDocument(tab.nativeDocHandle!);
      }
      _tabs.removeAt(index);
      if (_activeTabIndex >= _tabs.length) {
        _activeTabIndex = _tabs.length - 1;
      }
      notifyListeners();
    }
  }

  void createNewDocument({String title = "New Document.pdf", int pages = 3}) {
    final newTab = DocumentTab(
      id: DateTime.now().millisecondsSinceEpoch.toString(),
      title: title,
      pageCount: pages,
      currentPage: 0,
      zoomLevel: 1.0,
      pageOrder: List.generate(pages, (i) => i),
    );
    _tabs.add(newTab);
    _activeTabIndex = _tabs.length - 1;
    notifyListeners();
  }

  void openDocumentFromFile(String filePath) {
    final nativeHandle = OmniNativeEngine.instance.openDocument(filePath);
    int pages = 1;
    if (nativeHandle != null) {
      pages = OmniNativeEngine.instance.getPageCount(nativeHandle);
    }

    final filename = filePath.split(RegExp(r'[\\/]')).last;
    final newTab = DocumentTab(
      id: DateTime.now().millisecondsSinceEpoch.toString(),
      title: filename,
      filePath: filePath,
      nativeDocHandle: nativeHandle,
      pageCount: pages,
      currentPage: 0,
      zoomLevel: 1.0,
      pageOrder: List.generate(pages, (i) => i),
    );
    _tabs.add(newTab);
    _activeTabIndex = _tabs.length - 1;
    notifyListeners();
  }

  void setZoom(double newZoom) {
    final tab = activeTab;
    if (tab != null) {
      tab.zoomLevel = newZoom.clamp(0.25, 4.0);
      notifyListeners();
    }
  }

  void setCurrentPage(int page) {
    final tab = activeTab;
    if (tab != null && page >= 0 && page < tab.pageCount) {
      tab.currentPage = page;
      notifyListeners();
    }
  }

  void rotateActivePage(int degrees) {
    final tab = activeTab;
    if (tab != null) {
      final currentRot = tab.pageRotations[tab.currentPage] ?? 0;
      final newRot = (currentRot + degrees) % 360;
      tab.pageRotations[tab.currentPage] = newRot;
      if (tab.nativeDocHandle != null) {
        OmniNativeEngine.instance.rotatePage(tab.nativeDocHandle!, tab.currentPage, newRot);
      }
      tab.isModified = true;
      notifyListeners();
    }
  }

  void reorderPages(List<int> newOrder) {
    final tab = activeTab;
    if (tab != null && newOrder.length == tab.pageCount) {
      tab.pageOrder = List.from(newOrder);
      tab.isModified = true;
      notifyListeners();
    }
  }

  void addAnnotation(AnnotationItem item) {
    final tab = activeTab;
    if (tab != null) {
      _saveUndoState(tab);
      tab.annotations.add(item);
      tab.isModified = true;
      notifyListeners();
    }
  }

  void removeAnnotation(String id) {
    final tab = activeTab;
    if (tab != null) {
      _saveUndoState(tab);
      tab.annotations.removeWhere((a) => a.id == id);
      tab.isModified = true;
      notifyListeners();
    }
  }

  void _saveUndoState(DocumentTab tab) {
    _undoStack.add(List.from(tab.annotations));
    _redoStack.clear();
  }

  void undo() {
    final tab = activeTab;
    if (tab != null && _undoStack.isNotEmpty) {
      _redoStack.add(List.from(tab.annotations));
      tab.annotations = _undoStack.removeLast();
      tab.isModified = true;
      notifyListeners();
    }
  }

  void redo() {
    final tab = activeTab;
    if (tab != null && _redoStack.isNotEmpty) {
      _undoStack.add(List.from(tab.annotations));
      tab.annotations = _redoStack.removeLast();
      tab.isModified = true;
      notifyListeners();
    }
  }

  void _clearUndoRedo() {
    _undoStack.clear();
    _redoStack.clear();
  }

  void search(String query) {
    _searchQuery = query;
    _searchResults.clear();
    _currentSearchIndex = -1;

    if (query.isEmpty) {
      notifyListeners();
      return;
    }

    final tab = activeTab;
    if (tab != null) {
      // Index and search pages with case-insensitive pattern
      final regex = RegExp(query, caseSensitive: false);
      for (int i = 0; i < tab.pageCount; ++i) {
        final mockPageContent = "Omni PDF Studio Document Page ${i + 1}. High-performance vector rendering and annotations.";
        final matches = regex.allMatches(mockPageContent);
        for (final m in matches) {
          _searchResults.add(SearchMatch(
            pageIndex: i,
            matchedText: m.group(0) ?? query,
            bounds: Rect.fromLTWH(72.0 + (m.start * 6.0), 120.0, 80.0, 16.0),
          ));
        }
      }
      if (_searchResults.isNotEmpty) {
        _currentSearchIndex = 0;
        setCurrentPage(_searchResults[0].pageIndex);
      }
    }
    notifyListeners();
  }

  void nextSearchMatch() {
    if (_searchResults.isNotEmpty) {
      _currentSearchIndex = (_currentSearchIndex + 1) % _searchResults.length;
      setCurrentPage(_searchResults[_currentSearchIndex].pageIndex);
      notifyListeners();
    }
  }

  void prevSearchMatch() {
    if (_searchResults.isNotEmpty) {
      _currentSearchIndex = (_currentSearchIndex - 1 + _searchResults.length) % _searchResults.length;
      setCurrentPage(_searchResults[_currentSearchIndex].pageIndex);
      notifyListeners();
    }
  }
}
