import 'dart:convert';

import 'package:http/http.dart' as http;

class AppError implements Exception {
  const AppError({
    required this.errorCode,
    required this.userFriendlyMessage,
  });

  static const _allowedCodes = {
    'DB-001',
    'DLV-001',
    'GEN-001',
    'NET-001',
    'PAY-001',
    'REV-001',
  };

  factory AppError.fromResponse(http.Response response) {
    try {
      final payload = jsonDecode(response.body);
      if (payload is Map<String, dynamic>) {
        final errorCode = payload['errorCode'];
        final message = payload['userFriendlyMessage'];
        if (errorCode is String &&
            message is String &&
            _allowedCodes.contains(errorCode) &&
            message.trim().isNotEmpty) {
          return AppError(
            errorCode: errorCode,
            userFriendlyMessage: message.trim(),
          );
        }
      }
    } on FormatException {
      // Older endpoints can return non-JSON errors; never render their body.
    }

    return const AppError(
      errorCode: 'GEN-001',
      userFriendlyMessage: 'تعذر إتمام العملية. يرجى إعادة المحاولة.',
    );
  }

  final String errorCode;
  final String userFriendlyMessage;
}