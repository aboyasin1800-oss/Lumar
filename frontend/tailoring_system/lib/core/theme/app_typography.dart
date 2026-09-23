import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../ui_palette.dart';

class AppTypography {
  static const List<String> arabicFallback = ['Tahoma', 'Arial', 'sans-serif'];

  static TextStyle _base({
    required double size,
    required FontWeight weight,
    double? height,
    Color color = UiPalette.textMain,
  }) {
    return GoogleFonts.cairo(
      textStyle: TextStyle(
        color: color,
        fontSize: size,
        fontWeight: weight,
        height: height,
        fontFamily: 'Tahoma',
        fontFamilyFallback: arabicFallback,
      ),
    );
  }

  static TextStyle get display =>
      _base(size: 28, weight: FontWeight.w700, height: 1.2);

  static TextStyle get headline =>
      _base(size: 22, weight: FontWeight.w700, height: 1.3);

  static TextStyle get title =>
      _base(size: 18, weight: FontWeight.w600, height: 1.35);

  static TextStyle get body =>
      _base(size: 14, weight: FontWeight.w500, height: 1.5);

  static TextStyle get label => _base(
        size: 12,
        weight: FontWeight.w600,
        height: 1.4,
        color: UiPalette.textSoft,
      );

  static TextStyle get caption => _base(
        size: 11,
        weight: FontWeight.w500,
        height: 1.5,
        color: UiPalette.textSoft,
      );

  static TextStyle get numeric =>
      _base(size: 14, weight: FontWeight.w700, height: 1.3).copyWith(
        fontFeatures: const [FontFeature.tabularFigures()],
      );
}
