import 'package:flutter/material.dart';

import 'app_error.dart';
import 'ui_palette.dart';

enum AppMessageType { success, warning, error, info }

class AppMessage {
  static void show(
    BuildContext context,
    String message, {
    required AppMessageType type,
  }) {
    final color = switch (type) {
      AppMessageType.success => const Color(0xFF167C4B),
      AppMessageType.warning => const Color(0xFFB85C00),
      AppMessageType.error => const Color(0xFFC62828),
      AppMessageType.info => const Color(0xFF1565C0),
    };
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message, style: const TextStyle(color: Colors.white)),
          backgroundColor: color,
          duration: const Duration(seconds: 5),
          behavior: SnackBarBehavior.floating,
        ),
      );
  }

  static Future<void> showErrorDialog(BuildContext context, Object error) {
    final appError = error is AppError
        ? error
        : const AppError(
            errorCode: 'GEN-001',
            userFriendlyMessage: 'تعذر إتمام العملية. يرجى إعادة المحاولة.',
          );
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final surfaceColor = isDark ? UiPalette.darkSurface : UiPalette.lightCard;
    final textColor = UiPalette.adaptiveTextColor(surfaceColor);

    return showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        backgroundColor: surfaceColor,
        title: Text('تعذر إتمام العملية', style: TextStyle(color: textColor)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(appError.userFriendlyMessage,
                style: TextStyle(color: textColor)),
            const SizedBox(height: 12),
            Text(
              'رمز المتابعة: ${appError.errorCode}',
              style: TextStyle(color: textColor.withValues(alpha: 0.72)),
            ),
          ],
        ),
        actions: [
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: UiPalette.primary,
              foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
            ),
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('حسنًا'),
          ),
        ],
      ),
    );
  }
}