import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/core/theme/app_breakpoints.dart';
import 'package:tailoring_system/core/theme/app_dimensions.dart';
import 'package:tailoring_system/core/theme/app_icons.dart';
import 'package:tailoring_system/core/theme/app_spacing.dart';
import 'package:tailoring_system/core/theme/app_theme.dart';
import 'package:tailoring_system/core/theme/app_typography.dart';
import 'package:tailoring_system/core/ui_palette.dart';

void main() {
  group('design system core', () {
    test('theme builds correctly for light and dark modes', () {
      final lightTheme = AppTheme.light();
      final darkTheme = AppTheme.dark();

      expect(lightTheme.useMaterial3, isTrue);
      expect(darkTheme.useMaterial3, isTrue);
      expect(lightTheme.colorScheme.primary, UiPalette.primaryBlue);
      expect(darkTheme.colorScheme.primary, UiPalette.primaryBlue);
      expect(lightTheme.cardTheme.color, isNotNull);
      expect(darkTheme.cardTheme.color, isNotNull);
    });

    test('typography provides Arabic-safe styles with fallback', () {
      final display = AppTypography.display;
      final headline = AppTypography.headline;
      final numeric = AppTypography.numeric;

      expect(display.fontSize, greaterThan(0));
      expect(headline.fontFamilyFallback, contains('Tahoma'));
      expect(numeric.fontFeatures, isNotEmpty);
    });

    test('breakpoints are valid and ordered', () {
      expect(AppBreakpoints.mobile, lessThan(AppBreakpoints.tablet));
      expect(AppBreakpoints.tablet, lessThan(AppBreakpoints.desktop));
      expect(AppBreakpoints.desktop, lessThan(AppBreakpoints.largeDesktop));
    });

    test('spacing values are consistent and widget helpers work', () {
      expect(AppSpacing.xxs, equals(4.0));
      expect(AppSpacing.xs, equals(8.0));
      expect(AppSpacing.sm, equals(12.0));
      expect(AppSpacing.md, equals(16.0));
      expect(AppSpacing.lg, equals(24.0));
      expect(AppSpacing.xl, equals(32.0));
      expect(AppSpacing.vertical(12), isA<SizedBox>());
      expect(AppSpacing.horizontal(12), isA<SizedBox>());
    });

    test('dimension constants remain stable for ERP components', () {
      expect(AppDimensions.fieldHeight, greaterThan(0));
      expect(AppDimensions.buttonHeight, greaterThan(0));
      expect(AppDimensions.cardRadius, greaterThan(0));
      expect(AppDimensions.iconMedium, greaterThan(0));
    });

    test('icon wrapper supports fallback and SVG path safety', () {
      final icon = AppIcons.icon(Icons.info_outline, size: 18);
      final missingSvg = AppIcons.svg('missing_icon', size: 18, color: UiPalette.primaryBlue);

      expect(icon, isA<Icon>());
      expect(missingSvg, isA<Widget>());
    });
  });
}
