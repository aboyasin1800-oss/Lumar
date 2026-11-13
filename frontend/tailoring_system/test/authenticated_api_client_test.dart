import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/core/authenticated_api_client.dart';
import 'package:tailoring_system/screens/messages_screen.dart';
import 'package:tailoring_system/screens/settings/test_data_reset_screen.dart';
import 'package:tailoring_system/services/auth_state.dart';

class _FakeAuth extends AuthState {
  _FakeAuth() : super(baseUrl: 'http://127.0.0.1');

  @override
  String? get token => 'demo-session-token';
}

void main() {
  test('AuthenticatedApiClient adds the Bearer token when saving an order', () async {
    final auth = _FakeAuth();
    final api = AuthenticatedApiClient(
      auth: auth,
      client: MockClient((request) {
        expect(request.headers['authorization'], 'Bearer demo-session-token');
        expect(request.url.path, '/orders');

        return Future.value(http.Response(
          jsonEncode({'orderId': 77, 'orderNumber': 'ORD-77'}),
          201,
        ));
      }),
      baseUrl: 'http://127.0.0.1',
    );

    final result = await api.postObject('/orders', {
      'customerId': 7,
      'totalAmount': 1000,
      'discountAmount': 0,
      'advancePayment': 100,
      'paymentMethod': 'Cash',
      'cashAccountId': 42,
      'requestReference': 'ORD-TEST-001',
      'items': [
        {'productTypeId': 4, 'quantity': 1},
      ],
    });

    expect(result['orderNumber'], 'ORD-77');
  });

  test('AuthenticatedApiClient preserves the structured reset failure payload', () async {
    final auth = _FakeAuth();
    final api = AuthenticatedApiClient(
      auth: auth,
      client: MockClient((request) {
        expect(request.headers['authorization'], 'Bearer demo-session-token');
        expect(request.url.path, '/settings/test-data-reset');

        return Future.value(http.Response(
          jsonEncode({
            'success': false,
            'message': 'تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.',
            'databaseName': 'LUMAR_ERP_ES_VALIDATION_R3',
            'details': 'Execute stage failed.',
            'verified': false,
            'failureStage': 'EXECUTE_GUARD',
            'failureCode': 'EXECUTE_STAGE_FAILED',
            'correlationId': 'abc-123',
            'timestampUtc': '2026-11-08T00:00:00Z',
          }),
          500,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ));
      }),
      baseUrl: 'http://127.0.0.1',
    );

    try {
      await api.postObject('/settings/test-data-reset', {});
      fail('Expected a failure to be thrown.');
    } on StructuredApiException catch (error) {
      expect(error.message, contains('المرحلة: EXECUTE_GUARD'));
      expect(error.message, contains('FailureCode: EXECUTE_STAGE_FAILED'));
      expect(error.message, contains('CorrelationId: abc-123'));
      expect(error.details, 'Execute stage failed.');
      expect(error.failureStage, 'EXECUTE_GUARD');
      expect(error.failureCode, 'EXECUTE_STAGE_FAILED');
      expect(error.correlationId, 'abc-123');
      expect(error.statusCode, 500);
      expect(error.responseBody, isNotNull);
    }
  });

  testWidgets('TestDataResetScreen posts through the authenticated API client', (tester) async {
    final auth = _FakeAuth();
    final api = AuthenticatedApiClient(
      auth: auth,
      client: MockClient((request) {
        expect(request.method, 'POST');
        expect(request.url.path, '/settings/test-data-reset');
        expect(request.headers['authorization'], 'Bearer demo-session-token');
        return Future.value(http.Response(
          jsonEncode({
            'success': true,
            'message': 'تم تنظيف بيانات الاختبار بنجاح.',
            'databaseName': 'LUMAR_ERP_ES_VALIDATION_R3',
            'verified': true,
            'correlationId': 'reset-001',
          }),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ));
      }),
      baseUrl: 'http://127.0.0.1',
    );

    await tester.pumpWidget(MaterialApp(
      home: TestDataResetScreen(
        apiClient: api,
        baseUrl: 'http://127.0.0.1',
      ),
    ));

    await tester.tap(find.byKey(const ValueKey('reset_primary_button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('confirm_reset_button')));
    await tester.pumpAndSettle();

    expect(find.textContaining('تم تنظيف بيانات الاختبار بنجاح.'), findsWidgets);
  });

  test('MessagesRepository adds the bearer token when reading customer messages', () async {
    final auth = _FakeAuth();
    final repository = MessagesRepository(
      auth: auth,
      client: MockClient((request) {
        expect(request.url.path, '/messages/customer-messages');
        expect(request.headers['authorization'], 'Bearer demo-session-token');
        return Future.value(http.Response.bytes(
          utf8.encode(jsonEncode([
            {'id': 1, 'title': 'مرحبا', 'body': 'نص', 'channel': 'LocalWhatsApp', 'eventType': 'ManualCampaign', 'deliveryStatus': 'Sent', 'createdAtUtc': DateTime.now().toUtc().toIso8601String()},
          ])),
          200,
          headers: {'content-type': 'application/json; charset=utf-8'},
        ));
      }),
    );

    final messages = await repository.getCustomerMessages();
    expect(messages, hasLength(1));
    expect(messages.first.title, 'مرحبا');
  });
}

