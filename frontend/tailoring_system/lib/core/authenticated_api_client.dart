import 'dart:convert';

import 'package:http/http.dart' as http;

import '../services/auth_state.dart';

class AuthenticatedApiClient {
  AuthenticatedApiClient(
      {required this.auth, http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ??
            const String.fromEnvironment('LUMAR_API_URL',
                defaultValue: 'http://127.0.0.1:5093');

  final AuthState auth;
  final http.Client _client;
  final String _baseUrl;
  String? _es7OperationalGrant;
  DateTime? _es7OperationalGrantExpiresAt;

  Future<DateTime> activateEs7OperationalTestMode() async {
    final response = await _send('POST', '/purchasing/operations/test-mode/activate', body: const <String, dynamic>{});
    final result = _decodeObject(response, '/purchasing/operations/test-mode/activate');
    final grant = result['grantToken'] as String?;
    final expiresAt = DateTime.tryParse(result['expiresAtUtc']?.toString() ?? '');
    if (grant == null || grant.trim().isEmpty || expiresAt == null) {
      throw const ApiClientException('/purchasing/operations/test-mode/activate', 0, 'استجابة تفعيل وضع التنفيذ غير صحيحة.');
    }
    _es7OperationalGrant = grant;
    _es7OperationalGrantExpiresAt = expiresAt.toUtc();
    return _es7OperationalGrantExpiresAt!;
  }

  Future<Map<String, dynamic>> getObject(String path) async {
    final response = await _send('GET', path);
    return _decodeObject(response, path);
  }

  Future<List<dynamic>> getList(String path) async {
    final response = await _send('GET', path);
    final body = _decode(response.body, path);
    if (body is! List<dynamic>) {
      throw ApiClientException(
          path, response.statusCode, 'استجابة القائمة غير صحيحة.');
    }
    return body;
  }

  Future<Map<String, dynamic>> postObject(
      String path, Map<String, dynamic> body) async {
    final response = await _send('POST', path, body: body);
    return _decodeObject(response, path);
  }

  Future<void> postEmpty(String path, Map<String, dynamic> body) async {
    final response = await _send('POST', path, body: body);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      _throwApiError(response, path);
    }
  }

  Future<http.Response> _send(String method, String path,
      {Map<String, dynamic>? body}) async {
    final headers = <String, String>{'Accept': 'application/json'};
    final token = auth.token;
    if (token != null && token.trim().isNotEmpty) {
      headers['Authorization'] = 'Bearer ${token.trim()}';
    }
    if (_hasActiveEs7OperationalGrant) {
      headers['X-ES7-Operational-Grant'] = _es7OperationalGrant!;
    }
    if (body != null) headers['Content-Type'] = 'application/json';
    final request = http.Request(method, Uri.parse('$_baseUrl$path'))
      ..headers.addAll(headers)
      ..body = body == null ? '' : jsonEncode(body);
    try {
      final streamed = await _client.send(request);
      final response = await http.Response.fromStream(streamed);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        _throwApiError(response, path);
      }
      return response;
    } on http.ClientException catch (error) {
      throw ApiClientException(
          path, 0, 'تعذر الاتصال بالخادم: ${error.message}');
    }
  }

  bool get _hasActiveEs7OperationalGrant {
    final expiresAt = _es7OperationalGrantExpiresAt;
    if (_es7OperationalGrant == null || expiresAt == null || !expiresAt.isAfter(DateTime.now().toUtc())) {
      _es7OperationalGrant = null;
      _es7OperationalGrantExpiresAt = null;
      return false;
    }
    return true;
  }

  Map<String, dynamic> _decodeObject(http.Response response, String path) {
    final body = _decode(response.body, path);
    if (body is! Map<String, dynamic>) {
      throw ApiClientException(
          path, response.statusCode, 'استجابة الخادم غير صحيحة.');
    }
    return body;
  }

  dynamic _decode(String text, String path) {
    if (text.trim().isEmpty) return <String, dynamic>{};
    try {
      return jsonDecode(text);
    } catch (_) {
      throw ApiClientException(path, 0, 'تعذر قراءة استجابة الخادم.');
    }
  }

  void _throwApiError(http.Response response, String path) {
    String message = 'تعذر تنفيذ العملية.';
    try {
      final json = jsonDecode(response.body) as Map<String, dynamic>;
      message = (json['message'] ?? json['title'] ?? message).toString();
    } catch (_) {}
    throw ApiClientException(path, response.statusCode,
        _arabicMessage(response.statusCode, message));
  }

  String _arabicMessage(int statusCode, String message) {
    if (statusCode == 401) return 'انتهت الجلسة. سجل الدخول من جديد.';
    if (statusCode == 403) return 'ليس لديك صلاحية لتنفيذ هذه العملية.';
    if (statusCode == 404) return 'العنصر المطلوب غير موجود.';
    if (statusCode >= 500) return 'حدث خطأ في الخادم. أعد المحاولة لاحقًا.';
    return message;
  }
}

class ApiClientException implements Exception {
  const ApiClientException(this.path, this.statusCode, this.message);

  final String path;
  final int statusCode;
  final String message;

  @override
  String toString() => message;
}


