import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/core/authenticated_api_client.dart';
import 'package:tailoring_system/screens/messages_screen.dart';
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

  test('MessagesRepository adds the bearer token when reading customer messages', () async {
    final auth = _FakeAuth();
    final repository = MessagesRepository(
      auth: auth,
      client: MockClient((request) {
        expect(request.url.path, '/messages/customer-messages');
        expect(request.headers['authorization'], 'Bearer demo-session-token');
        return Future.value(http.Response.bytes(
          utf8.encode(jsonEncode([
            {'id': 1, 'title': 'مرحبا', 'body': 'نص', 'channel': 'LocalWhatsApp', 'eventType': 'ManualCampaign', 'deliveryStatus': 'Sent', 'createdAtUtc': DateTime.now().toUtc().toIso8601String()}
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
