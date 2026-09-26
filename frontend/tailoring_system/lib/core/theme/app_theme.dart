import 'package:flex_color_scheme/flex_color_scheme.dart';
import 'package:flutter/material.dart';

import '../ui_palette.dart';
import 'app_dimensions.dart';
import 'app_typography.dart';

class AppTheme {
  static ThemeData light({double scale = 1.0}) {
    final base = FlexThemeData.light(
      scheme: FlexScheme.material,
      surfaceMode: FlexSurfaceMode.levelSurfacesLowScaffold,
      blendLevel: 8,
      appBarStyle: FlexAppBarStyle.primary,
      useMaterial3: true,
      subThemesData: const FlexSubThemesData(
        defaultRadius: 14,
        inputDecoratorRadius: 12,
      ),
      scaffoldBackground: UiPalette.lightBackground,
      appBarBackground: UiPalette.lightCard,
    );

    return base.copyWith(
      visualDensity: VisualDensity.standard,
      colorScheme: ColorScheme.fromSeed(
        seedColor: UiPalette.primary,
        brightness: Brightness.light,
      ).copyWith(
        primary: UiPalette.primary,
        onPrimary: UiPalette.lightText,
        secondary: UiPalette.primary,
        onSecondary: UiPalette.lightText,
        surface: UiPalette.lightBackground,
        onSurface: UiPalette.lightText,
        surfaceContainerHighest: UiPalette.lightCardAlt,
        outline: UiPalette.primaryBorder,
      ),
      scaffoldBackgroundColor: UiPalette.lightBackground,
      cardTheme: CardThemeData(
        color: UiPalette.lightCard,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: const EdgeInsets.all(4),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppDimensions.cardRadius),
          side: const BorderSide(color: UiPalette.primaryBorder),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: UiPalette.lightField,
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide: const BorderSide(color: UiPalette.primaryBorder),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide: const BorderSide(color: UiPalette.primaryBorder),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide:
              const BorderSide(color: UiPalette.primaryBorder, width: 1.5),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide: const BorderSide(color: Colors.redAccent),
        ),
        hintStyle:
            AppTypography.body.copyWith(color: UiPalette.lightSecondaryText),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: UiPalette.primary,
          foregroundColor: UiPalette.lightText,
          minimumSize: const Size(0, AppDimensions.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          ),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: UiPalette.lightCardAlt,
          foregroundColor: UiPalette.lightText,
          minimumSize: const Size(0, AppDimensions.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: UiPalette.lightText,
          side: const BorderSide(color: UiPalette.primaryBorder),
          minimumSize: const Size(0, AppDimensions.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          ),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: UiPalette.primary,
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: UiPalette.lightCard,
        titleTextStyle:
            AppTypography.title.copyWith(color: UiPalette.lightText),
        contentTextStyle:
            AppTypography.body.copyWith(color: UiPalette.lightSecondaryText),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppDimensions.cardRadius),
        ),
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(
          color: UiPalette.lightCardAlt,
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          border: Border.all(color: UiPalette.primaryBorder),
        ),
        textStyle: AppTypography.caption.copyWith(color: UiPalette.lightText),
      ),
      snackBarTheme: const SnackBarThemeData(
        backgroundColor: Color(0xFF1565C0),
        contentTextStyle: TextStyle(color: Colors.white),
        behavior: SnackBarBehavior.floating,
      ),
      dividerTheme: const DividerThemeData(
        color: UiPalette.primaryBorder,
        thickness: 1,
      ),
      scrollbarTheme: ScrollbarThemeData(
        thumbColor: WidgetStatePropertyAll(
          UiPalette.primary.withValues(alpha: 0.7),
        ),
        trackColor: const WidgetStatePropertyAll(UiPalette.lightCardAlt),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: UiPalette.lightCard,
        foregroundColor: UiPalette.lightText,
        centerTitle: false,
      ),
      textTheme: ThemeData.light().textTheme.apply(
            bodyColor: UiPalette.lightText,
            displayColor: UiPalette.lightText,
          ),
      iconTheme: const IconThemeData(color: UiPalette.primary),
    );
  }

  static ThemeData dark({double scale = 1.0}) {
    final base = FlexThemeData.dark(
      scheme: FlexScheme.material,
      surfaceMode: FlexSurfaceMode.levelSurfacesLowScaffold,
      blendLevel: 8,
      appBarStyle: FlexAppBarStyle.primary,
      useMaterial3: true,
      subThemesData: const FlexSubThemesData(
        defaultRadius: 14,
        inputDecoratorRadius: 12,
      ),
      scaffoldBackground: UiPalette.darkBackground,
      appBarBackground: UiPalette.darkSurfaceAlt,
    );

    return base.copyWith(
      visualDensity: VisualDensity.standard,
      colorScheme: ColorScheme.fromSeed(
        seedColor: UiPalette.primary,
        brightness: Brightness.dark,
      ).copyWith(
        primary: UiPalette.primary,
        onPrimary: UiPalette.darkText,
        secondary: UiPalette.primary,
        onSecondary: UiPalette.darkText,
        surface: UiPalette.darkBackground,
        onSurface: UiPalette.darkText,
        surfaceContainerHighest: UiPalette.darkSurfaceAlt,
        outline: UiPalette.primaryBorder,
      ),
      scaffoldBackgroundColor: UiPalette.darkBackground,
      cardTheme: CardThemeData(
        color: UiPalette.darkSurface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: const EdgeInsets.all(4),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppDimensions.cardRadius),
          side: const BorderSide(color: UiPalette.primaryBorder),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: UiPalette.darkSurfaceAlt,
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide: const BorderSide(color: UiPalette.primaryBorder),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide: const BorderSide(color: UiPalette.primaryBorder),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide:
              const BorderSide(color: UiPalette.primaryBorder, width: 1.5),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          borderSide: const BorderSide(color: Colors.redAccent),
        ),
        hintStyle: AppTypography.body.copyWith(color: UiPalette.textSoft),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: UiPalette.primary,
          foregroundColor: UiPalette.darkText,
          minimumSize: const Size(0, AppDimensions.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          ),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: UiPalette.darkSurfaceAlt,
          foregroundColor: UiPalette.darkText,
          minimumSize: const Size(0, AppDimensions.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: UiPalette.darkText,
          side: const BorderSide(color: UiPalette.primaryBorder),
          minimumSize: const Size(0, AppDimensions.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          ),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: UiPalette.primary,
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: UiPalette.darkSurface,
        titleTextStyle: AppTypography.title.copyWith(color: UiPalette.darkText),
        contentTextStyle:
            AppTypography.body.copyWith(color: UiPalette.textSoft),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppDimensions.cardRadius),
        ),
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(
          color: UiPalette.darkSurfaceAlt,
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          border: Border.all(color: UiPalette.primaryBorder),
        ),
        textStyle: AppTypography.caption.copyWith(color: UiPalette.darkText),
      ),
      snackBarTheme: const SnackBarThemeData(
        backgroundColor: Color(0xFF1565C0),
        contentTextStyle: TextStyle(color: Colors.white),
        behavior: SnackBarBehavior.floating,
      ),
      dividerTheme: const DividerThemeData(
        color: UiPalette.primaryBorder,
        thickness: 1,
      ),
      scrollbarTheme: ScrollbarThemeData(
        thumbColor: WidgetStatePropertyAll(
          UiPalette.primary.withValues(alpha: 0.7),
        ),
        trackColor: const WidgetStatePropertyAll(UiPalette.darkSurfaceAlt),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: UiPalette.darkSurfaceAlt,
        foregroundColor: UiPalette.darkText,
        centerTitle: false,
      ),
      textTheme: ThemeData.dark().textTheme.apply(
            bodyColor: UiPalette.darkText,
            displayColor: UiPalette.darkText,
          ),
      iconTheme: const IconThemeData(color: UiPalette.primary),
    );
  }
}
