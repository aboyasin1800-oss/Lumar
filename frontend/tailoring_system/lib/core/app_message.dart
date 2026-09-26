import 'package:flutter/material.dart';

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
}