import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/customer_creation_models.dart';

class CustomerCreationException implements Exception {
  const CustomerCreationException(this.message);

  final String message;

  @override
  String toString() => message;
}

class CustomerCreationRepository {
  CustomerCreationRepository({http.Client? client})
      : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<List<CustomerReferralCandidate>> searchReferrers(String term) async {
    final normalizedTerm = term.trim();
    if (normalizedTerm.isEmpty) return const [];
    final response = await _client.get(
      Uri.parse('$_baseUrl/customers/referral-search').replace(
        queryParameters: {'term': normalizedTerm},
      ),
    );
    final decoded = await _decodeResponse(response);
    if (decoded is! List) return const [];
    return decoded
        .whereType<Map>()
        .map((item) => CustomerReferralCandidate.fromJson(
              Map<String, dynamic>.from(item),
            ))
        .toList();
  }

  Future<CustomerCreationResult> create({
    required String customerName,
    required String phoneNumber,
    String? address,
    String? notes,
    int? referrerCustomerId,
    String? relationshipType,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/customers/with-referral'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'customerName': customerName.trim(),
        'phoneNumber': phoneNumber.trim(),
        'address': _nullableText(address),
        'notes': _nullableText(notes),
        'referrerCustomerId': referrerCustomerId,
        'relationshipType': _nullableText(relationshipType),
      }),
    );
    final decoded = await _decodeResponse(response);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw CustomerCreationException(_errorMessage(decoded, response.body));
    }
    if (decoded is! Map) {
      throw const CustomerCreationException('استجابة إنشاء العميل غير صالحة.');
    }
    return CustomerCreationResult.fromJson(Map<String, dynamic>.from(decoded));
  }

  Future<dynamic> _decodeResponse(http.Response response) async {
    if (response.body.trim().isEmpty) return null;
    try {
      return jsonDecode(response.body);
    } catch (_) {
      if (response.statusCode < 200 || response.statusCode >= 300) {
        return response.body;
      }
      throw const CustomerCreationException('استجابة الخادم غير صالحة.');
    }
  }

  String _errorMessage(dynamic decoded, String fallback) {
    if (decoded is Map && decoded['message'] is String) {
      return decoded['message'] as String;
    }
    if (decoded is String && decoded.trim().isNotEmpty) return decoded;
    return fallback.trim().isEmpty ? 'تعذر حفظ العميل.' : fallback;
  }

  String? _nullableText(String? value) {
    final normalized = value?.trim();
    return normalized == null || normalized.isEmpty ? null : normalized;
  }
}
