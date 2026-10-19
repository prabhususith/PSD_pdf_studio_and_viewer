import 'package:flutter/material.dart';

enum AppThemeMode {
  fluentDark,
  oledBlack,
  sepiaReading,
  windowsMica,
}

class OmniTheme {
  final String name;
  final ThemeData themeData;
  final Color viewportBackground;
  final Color paperBackground;
  final Color ribbonBackground;
  final Color toolbarBorder;
  final Color accentColor;

  OmniTheme({
    required this.name,
    required this.themeData,
    required this.viewportBackground,
    required this.paperBackground,
    required this.ribbonBackground,
    required this.toolbarBorder,
    required this.accentColor,
  });

  static OmniTheme fluentDark = OmniTheme(
    name: "Fluent Dark",
    viewportBackground: const Color(0xFF1E1E1E),
    paperBackground: const Color(0xFFFAFAFA),
    ribbonBackground: const Color(0xFF252526),
    toolbarBorder: const Color(0xFF383838),
    accentColor: const Color(0xFF0078D4),
    themeData: ThemeData(
      brightness: Brightness.dark,
      scaffoldBackgroundColor: const Color(0xFF1E1E1E),
      primaryColor: const Color(0xFF0078D4),
      cardColor: const Color(0xFF252526),
      dividerColor: const Color(0xFF383838),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFF252526),
        elevation: 0,
      ),
      colorScheme: const ColorScheme.dark(
        primary: Color(0xFF0078D4),
        secondary: Color(0xFF60CDFF),
        surface: Color(0xFF252526),
        background: Color(0xFF1E1E1E),
      ),
    ),
  );

  static OmniTheme oledBlack = OmniTheme(
    name: "OLED Black",
    viewportBackground: const Color(0xFF000000),
    paperBackground: const Color(0xFFFFFFFF),
    ribbonBackground: const Color(0xFF0A0A0A),
    toolbarBorder: const Color(0xFF222222),
    accentColor: const Color(0xFF00E5FF),
    themeData: ThemeData(
      brightness: Brightness.dark,
      scaffoldBackgroundColor: const Color(0xFF000000),
      primaryColor: const Color(0xFF00E5FF),
      cardColor: const Color(0xFF0A0A0A),
      dividerColor: const Color(0xFF222222),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFF0A0A0A),
        elevation: 0,
      ),
      colorScheme: const ColorScheme.dark(
        primary: Color(0xFF00E5FF),
        secondary: Color(0xFF00B0FF),
        surface: Color(0xFF0A0A0A),
        background: Color(0xFF000000),
      ),
    ),
  );

  static OmniTheme sepiaReading = OmniTheme(
    name: "Sepia Reading",
    viewportBackground: const Color(0xFFEFE6D5),
    paperBackground: const Color(0xFFFBF0D9),
    ribbonBackground: const Color(0xFFE4DAC7),
    toolbarBorder: const Color(0xFFD6C8AF),
    accentColor: const Color(0xFF8D6E63),
    themeData: ThemeData(
      brightness: Brightness.light,
      scaffoldBackgroundColor: const Color(0xFFEFE6D5),
      primaryColor: const Color(0xFF8D6E63),
      cardColor: const Color(0xFFE4DAC7),
      dividerColor: const Color(0xFFD6C8AF),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFFE4DAC7),
        elevation: 0,
        foregroundColor: Color(0xFF3E2723),
      ),
      colorScheme: const ColorScheme.light(
        primary: Color(0xFF8D6E63),
        secondary: Color(0xFF6D4C41),
        surface: Color(0xFFE4DAC7),
        background: Color(0xFFEFE6D5),
      ),
    ),
  );

  static OmniTheme windowsMica = OmniTheme(
    name: "Windows Mica",
    viewportBackground: const Color(0xFF2C2C2E),
    paperBackground: const Color(0xFFFFFFFF),
    ribbonBackground: const Color(0xFF3A3A3C),
    toolbarBorder: const Color(0xFF48484A),
    accentColor: const Color(0xFF4CC2FF),
    themeData: ThemeData(
      brightness: Brightness.dark,
      scaffoldBackgroundColor: const Color(0xFF2C2C2E),
      primaryColor: const Color(0xFF4CC2FF),
      cardColor: const Color(0xFF3A3A3C),
      dividerColor: const Color(0xFF48484A),
      appBarTheme: const AppBarTheme(
        backgroundColor: Color(0xFF3A3A3C),
        elevation: 0,
      ),
      colorScheme: const ColorScheme.dark(
        primary: Color(0xFF4CC2FF),
        secondary: Color(0xFF0078D4),
        surface: Color(0xFF3A3A3C),
        background: Color(0xFF2C2C2E),
      ),
    ),
  );

  static OmniTheme getTheme(AppThemeMode mode) {
    switch (mode) {
      case AppThemeMode.fluentDark:
        return fluentDark;
      case AppThemeMode.oledBlack:
        return oledBlack;
      case AppThemeMode.sepiaReading:
        return sepiaReading;
      case AppThemeMode.windowsMica:
        return windowsMica;
    }
  }
}
