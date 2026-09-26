import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/sales_reference_models.dart';

class SalesReferenceRepository {
  SalesReferenceRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<SalesReferenceVersion> getVersion() async {
    final response = await _client.get(Uri.parse('$_baseUrl/sales-reference/version'));
    return SalesReferenceVersion.fromJson(_decode(response, 'إصدار بيانات المبيعات المرجعية'));
  }

  Future<SalesReferenceSnapshot> getSnapshot() async {
    final response = await _client.get(Uri.parse('$_baseUrl/sales-reference/snapshot'));
    return SalesReferenceSnapshot.fromJson(_decode(response, 'بيانات المبيعات المرجعية'));
  }

  Map<String, dynamic> _decode(http.Response response, String description) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw StateError('تعذر تحميل $description.');
    }
    final value = jsonDecode(response.body);
    if (value is! Map<String, dynamic>) {
      throw const FormatException('استجابة بيانات المبيعات المرجعية غير صالحة.');
    }
    return value;
  }
}