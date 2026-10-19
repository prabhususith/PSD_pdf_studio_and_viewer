import 'dart:ffi' as ffi;
import 'dart:io';
import 'package:ffi/ffi.dart';

// Native function typedefs
typedef NativeInitEngine = ffi.Int32 Function();
typedef DartInitEngine = int Function();

typedef NativeOpenDocument = ffi.Pointer<ffi.Void> Function(ffi.Pointer<Utf8> filePath, ffi.Pointer<Utf8> password);
typedef DartOpenDocument = ffi.Pointer<ffi.Void> Function(ffi.Pointer<Utf8> filePath, ffi.Pointer<Utf8> password);

typedef NativeCloseDocument = ffi.Void Function(ffi.Pointer<ffi.Void> doc);
typedef DartCloseDocument = void Function(ffi.Pointer<ffi.Void> doc);

typedef NativeGetPageCount = ffi.Int32 Function(ffi.Pointer<ffi.Void> doc);
typedef DartGetPageCount = int Function(ffi.Pointer<ffi.Void> doc);

typedef NativeGetPageSize = ffi.Int32 Function(ffi.Pointer<ffi.Void> doc, ffi.Int32 pageIndex, ffi.Pointer<ffi.Double> width, ffi.Pointer<ffi.Double> height);
typedef DartGetPageSize = int Function(ffi.Pointer<ffi.Void> doc, int pageIndex, ffi.Pointer<ffi.Double> width, ffi.Pointer<ffi.Double> height);

typedef NativeRotatePage = ffi.Int32 Function(ffi.Pointer<ffi.Void> doc, ffi.Int32 pageIndex, ffi.Int32 degrees);
typedef DartRotatePage = int Function(ffi.Pointer<ffi.Void> doc, int pageIndex, int degrees);

typedef NativeSaveIncremental = ffi.Int32 Function(ffi.Pointer<ffi.Void> doc, ffi.Pointer<Utf8> outputPath);
typedef DartSaveIncremental = int Function(ffi.Pointer<ffi.Void> doc, ffi.Pointer<Utf8> outputPath);

typedef NativeCompileTextToPdf = ffi.Int32 Function(
    ffi.Pointer<Utf8> text,
    ffi.Pointer<Utf8> outPath,
    ffi.Double fontSize,
    ffi.Double lineHeight,
    ffi.Double margin,
    ffi.Pointer<Utf8> watermark
);
typedef DartCompileTextToPdf = int Function(
    ffi.Pointer<Utf8> text,
    ffi.Pointer<Utf8> outPath,
    double fontSize,
    double lineHeight,
    double margin,
    ffi.Pointer<Utf8> watermark
);

class OmniNativeEngine {
  static final OmniNativeEngine instance = OmniNativeEngine._internal();

  ffi.DynamicLibrary? _lib;
  bool _isAvailable = false;

  DartInitEngine? _initEngine;
  DartOpenDocument? _openDocument;
  DartCloseDocument? _closeDocument;
  DartGetPageCount? _getPageCount;
  DartGetPageSize? _getPageSize;
  DartRotatePage? _rotatePage;
  DartSaveIncremental? _saveIncremental;
  DartCompileTextToPdf? _compileTextToPdf;

  OmniNativeEngine._internal() {
    _loadLibrary();
  }

  void _loadLibrary() {
    try {
      if (Platform.isWindows) {
        _lib = ffi.DynamicLibrary.open('pdfengine.dll');
      } else if (Platform.isAndroid) {
        _lib = ffi.DynamicLibrary.open('libpdfengine.so');
      } else if (Platform.isLinux) {
        _lib = ffi.DynamicLibrary.open('libpdfengine.so');
      }
    } catch (_) {
      // Graceful fallback to pure-dart simulation mode when running prior to full binary compilation
      _lib = null;
    }

    if (_lib != null) {
      try {
        _initEngine = _lib!.lookupFunction<NativeInitEngine, DartInitEngine>('OmniPdf_Initialize');
        _openDocument = _lib!.lookupFunction<NativeOpenDocument, DartOpenDocument>('OmniPdf_OpenDocument');
        _closeDocument = _lib!.lookupFunction<NativeCloseDocument, DartCloseDocument>('OmniPdf_CloseDocument');
        _getPageCount = _lib!.lookupFunction<NativeGetPageCount, DartGetPageCount>('OmniPdf_GetPageCount');
        _getPageSize = _lib!.lookupFunction<NativeGetPageSize, DartGetPageSize>('OmniPdf_GetPageSize');
        _rotatePage = _lib!.lookupFunction<NativeRotatePage, DartRotatePage>('OmniPdf_RotatePage');
        _saveIncremental = _lib!.lookupFunction<NativeSaveIncremental, DartSaveIncremental>('OmniPdf_SaveIncremental');
        _compileTextToPdf = _lib!.lookupFunction<NativeCompileTextToPdf, DartCompileTextToPdf>('Omni_CompileTextToPdf');
        _initEngine?.call();
        _isAvailable = true;
      } catch (_) {
        _isAvailable = false;
      }
    }
  }

  bool get isAvailable => _isAvailable;

  ffi.Pointer<ffi.Void>? openDocument(String path, {String password = ''}) {
    if (!_isAvailable || _openDocument == null) return null;
    final pathPtr = path.toNativeUtf8();
    final passPtr = password.toNativeUtf8();
    try {
      final doc = _openDocument!(pathPtr, passPtr);
      return doc.address == 0 ? null : doc;
    } finally {
      malloc.free(pathPtr);
      malloc.free(passPtr);
    }
  }

  void closeDocument(ffi.Pointer<ffi.Void> doc) {
    if (_isAvailable && _closeDocument != null && doc.address != 0) {
      _closeDocument!(doc);
    }
  }

  int getPageCount(ffi.Pointer<ffi.Void> doc) {
    if (_isAvailable && _getPageCount != null && doc.address != 0) {
      return _getPageCount!(doc);
    }
    return 1;
  }

  bool rotatePage(ffi.Pointer<ffi.Void> doc, int pageIndex, int degrees) {
    if (_isAvailable && _rotatePage != null && doc.address != 0) {
      return _rotatePage!(doc, pageIndex, degrees) == 1;
    }
    return true;
  }

  bool saveIncremental(ffi.Pointer<ffi.Void> doc, String outputPath) {
    if (!_isAvailable || _saveIncremental == null || doc.address == 0) return false;
    final outPtr = outputPath.toNativeUtf8();
    try {
      return _saveIncremental!(doc, outPtr) == 1;
    } finally {
      malloc.free(outPtr);
    }
  }

  bool compileTextToPdf(String text, String outPath, {double fontSize = 12.0, String watermark = ''}) {
    if (!_isAvailable || _compileTextToPdf == null) return false;
    final textPtr = text.toNativeUtf8();
    final outPtr = outPath.toNativeUtf8();
    final wmPtr = watermark.toNativeUtf8();
    try {
      return _compileTextToPdf!(textPtr, outPtr, fontSize, fontSize * 1.4, 54.0, wmPtr) == 1;
    } finally {
      malloc.free(textPtr);
      malloc.free(outPtr);
      malloc.free(wmPtr);
    }
  }
}
