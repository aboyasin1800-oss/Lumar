import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/supplier_mobile_models.dart';
import '../services/auth_state.dart';

class SupplierMobileRepository {
  SupplierMobileRepository({http.Client? client, AuthState? auth})
      : _client = client ?? http.Client(),
        _auth = auth;

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;
  final AuthState? _auth;

  Map<String, String> _headers() {
    final headers = <String, String>{'Accept': 'application/json'};
    final token = _auth?.token;
    if (token != null && token.isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  Future<T> _getJson<T>(String path, T Function(Map<String, dynamic>) parser) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl$path'),
      headers: _headers(),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException(path, response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const SupplierMobileApiException('unexpected JSON payload', 0, '');
    }
    return parser(decoded);
  }

  Future<List<T>> _getJsonList<T>(String path, T Function(Map<String, dynamic>) parser) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl$path'),
      headers: _headers(),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException(path, response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      return <T>[];
    }
    return decoded
        .whereType<Map>()
        .map((item) => parser(Map<String, dynamic>.from(item)))
        .toList();
  }

  Future<SupplierMobileProfile> getProfile() => _getJson('/mobile/supplier/profile', SupplierMobileProfile.fromJson);

  Future<SupplierHomeSummary> getHomeSummary() => _getJson('/mobile/supplier/home', SupplierHomeSummary.fromJson);

  Future<List<SupplierLedgerEntry>> getLedger() => _getJsonList('/mobile/supplier/ledger', SupplierLedgerEntry.fromJson);

  Future<List<SupplierInvoice>> getInvoices() => _getJsonList('/mobile/supplier/invoices', SupplierInvoice.fromJson);

  Future<SupplierInvoice?> getInvoice(int invoiceId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/mobile/supplier/invoices/$invoiceId'),
      headers: _headers(),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException('/mobile/supplier/invoices/$invoiceId', response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) return SupplierInvoice.fromJson(decoded);
    return null;
  }

  Future<List<SupplierPayment>> getPayments() => _getJsonList('/mobile/supplier/payments', SupplierPayment.fromJson);

  Future<SupplierPayment?> getPayment(int paymentId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/mobile/supplier/payments/$paymentId'),
      headers: _headers(),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException('/mobile/supplier/payments/$paymentId', response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) return SupplierPayment.fromJson(decoded);
    return null;
  }

  Future<SupplierPaymentResponseStatus?> getPaymentResponse(int paymentId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/mobile/supplier/payments/$paymentId/response'),
      headers: _headers(),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException('/mobile/supplier/payments/$paymentId/response', response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) return SupplierPaymentResponseStatus.fromJson(decoded);
    return null;
  }

  Future<List<SupplierMessage>> getMessages({int page = 1, int pageSize = 20}) =>
      _getJsonList('/mobile/supplier/messages?page=$page&pageSize=$pageSize', SupplierMessage.fromJson);

  Future<SupplierMessage?> getMessage(int messageId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/mobile/supplier/messages/$messageId'),
      headers: _headers(),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException('/mobile/supplier/messages/$messageId', response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) return SupplierMessage.fromJson(decoded);
    return null;
  }

  Future<List<SupplierNotification>> getNotifications({int page = 1, int pageSize = 20}) =>
      _getJsonList('/mobile/supplier/notifications?page=$page&pageSize=$pageSize', SupplierNotification.fromJson);

  Future<SupplierNotification?> getNotification(int notificationId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/mobile/supplier/notifications/$notificationId'),
      headers: _headers(),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException('/mobile/supplier/notifications/$notificationId', response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) return SupplierNotification.fromJson(decoded);
    return null;
  }

  Future<List<SupplierAnnouncement>> getAnnouncements({int page = 1, int pageSize = 20}) =>
      _getJsonList('/mobile/supplier/announcements?page=$page&pageSize=$pageSize', SupplierAnnouncement.fromJson);

  Future<SupplierAnnouncement?> getAnnouncement(int announcementId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/mobile/supplier/announcements/$announcementId'),
      headers: _headers(),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierMobileApiException('/mobile/supplier/announcements/$announcementId', response.statusCode, response.body);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) return SupplierAnnouncement.fromJson(decoded);
    return null;
  }
}

class SupplierMobileApiException implements Exception {
  const SupplierMobileApiException(this.path, this.statusCode, this.responseBody);

  final String path;
  final int statusCode;
  final String responseBody;

  @override
  String toString() => 'SupplierMobileApiException(path: $path, statusCode: $statusCode, responseBody: $responseBody)';
}


