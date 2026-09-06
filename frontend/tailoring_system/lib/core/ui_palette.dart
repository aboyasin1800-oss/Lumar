import 'package:flutter/material.dart';

class UiPalette {
  static const Color screenBackground = Color(0xFF070B10);
  static const Color surfaceCard = Color(0xFF121A22);
  static const Color softBlue = Color(0xFF172A3A);
  static const Color primaryBlue = Color.fromARGB(255, 78, 201, 176);
  static const Color primaryDark = Color.fromARGB(255, 46, 162, 138);
  static const Color purpleAccent = Color.fromARGB(255, 78, 201, 176);
  static const Color textMain = Color(0xFFEAF2FF);
  static const Color textSoft = Color(0xFFB7C7DA);
  static const Color borderSoft = Color(0xFF2A3A4E);

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
