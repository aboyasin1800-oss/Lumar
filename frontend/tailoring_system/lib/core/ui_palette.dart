import 'package:flutter/material.dart';

class UiPalette {
  static const Color darkBackground = Color(0xFF0D1117);
  static const Color darkSurface = Color(0xFF1D1F21);
  static const Color darkSurfaceAlt = Color(0xFF24272B);
  static const Color primary = Color(0xFF4EC9B0);
  static const Color primaryBorder = Color(0xFF3A9684);
  static const Color darkText = Color(0xFFECF0F5);

  static const Color lightBackground = Color(0xFFF7F4EE);
  static const Color lightCard = Color(0xFFFFFCF7);
  static const Color lightCardAlt = Color(0xFFFAF7F2);
  static const Color lightField = Color(0xFFFFFDF9);
  static const Color lightText = Color(0xFF2B2F36);
  static const Color lightSecondaryText = Color(0xFF6D747E);

  static const Color screenBackground = darkBackground;
  static const Color surfaceCard = darkSurface;
  static const Color softBlue = darkSurfaceAlt;
  static const Color primaryBlue = primary;
  static const Color primaryDark = primaryBorder;
  static const Color purpleAccent = primary;
  static const Color textMain = darkText;
  static const Color textSoft = Color(0xFF9AA6B2);
  static const Color borderSoft = Color(0xFF2B3138);

  static Color adaptiveTextColor(Color backgroundColor) {
    final brightness = ThemeData.estimateBrightnessForColor(backgroundColor);
    return brightness == Brightness.dark ? Colors.white : Colors.black;
  }

  static TextStyle adaptiveTextStyle(
    BuildContext context, {
    required Color backgroundColor,
    double fontSize = 14,
    FontWeight fontWeight = FontWeight.w500,
    TextDecoration? decoration,
  }) {
    final color = adaptiveTextColor(backgroundColor);
    return Theme.of(context).textTheme.bodyMedium?.copyWith(
          color: color,
          fontSize: fontSize,
          fontWeight: fontWeight,
          decoration: decoration,
        ) ??
        TextStyle(
          color: color,
          fontSize: fontSize,
          fontWeight: fontWeight,
          decoration: decoration,
        );
  }
}
