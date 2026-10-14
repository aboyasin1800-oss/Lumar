import 'dart:convert';

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

  static String _errorDetailText(Object error) {
    if (error is AppError && error.userFriendlyMessage.trim().isNotEmpty) {
      return error.userFriendlyMessage.trim();
    }

    final raw = error.toString();
    final trimmed = raw
        .replaceFirst(RegExp(r'^(Exception|Error):\s*', caseSensitive: false), '')
        .trim();

    if (trimmed.isEmpty || trimmed.toLowerCase() == 'null') {
      return 'تعذر إتمام العملية. يرجى مراجعة البيانات المدخلة أو إعادة المحاولة.';
    }

    if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
      try {
        final decoded = __decodeJsonError(trimmed);
        if (decoded != null && decoded.isNotEmpty) {
          return decoded;
        }
      } catch (_) {
        // Ignore malformed JSON and continue to a safe Arabic fallback.
      }
      return 'حدث خطأ في الاستجابة من الخادم. يرجى مراجعة البيانات ثم إعادة المحاولة.';
    }

    if (trimmed.contains('userFriendlyMessage')) {
      final decoded = __decodeJsonError(trimmed);
      if (decoded != null && decoded.isNotEmpty) {
        return decoded;
      }
    }

    return trimmed;
  }

  static String? __decodeJsonError(String raw) {
    final parsed = raw.replaceFirst(
      RegExp(r'^(Exception|Error):\s*', caseSensitive: false),
      '',
    );
    final jsonStart = parsed.indexOf('{');
    final jsonEnd = parsed.lastIndexOf('}');
    if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart) {
      return null;
    }

    final candidate = parsed.substring(jsonStart, jsonEnd + 1);
    try {
      final decoded = __parseJsonMap(candidate);
      final value = decoded['userFriendlyMessage'];
      if (value is String && value.trim().isNotEmpty && value.toLowerCase() != 'null') {
        return value.trim();
      }
    } catch (_) {
      // Ignore malformed JSON payloads.
    }

    return null;
  }

  static Map<String, dynamic> __parseJsonMap(String jsonText) {
    final decoded = jsonDecode(jsonText);
    if (decoded is Map<String, dynamic>) {
      return decoded;
    }
    if (decoded is Map) {
      return Map<String, dynamic>.from(decoded);
    }
    throw const FormatException('Not a JSON object');
  }

  static Future<void> showErrorDialog(BuildContext context, Object error) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final surfaceColor = isDark ? UiPalette.darkSurface : UiPalette.lightCard;
    final textColor = UiPalette.adaptiveTextColor(surfaceColor);
    final detail = _errorDetailText(error);
    final appError = error is AppError
        ? error
        : AppError(
            errorCode: 'GEN-001',
            userFriendlyMessage: detail,
          );

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