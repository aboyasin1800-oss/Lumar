import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/orders_screen.dart';

class _TestHttpOverrides extends HttpOverrides {
  @override
  HttpClient createHttpClient(SecurityContext? context) => _FakeHttpClient();
}

class _FakeHttpClient implements HttpClient {
  @override
  void close({bool force = false}) {}

  @override
  Future<HttpClientRequest> getUrl(Uri url) async {
    final body = switch (url.path) {
      '/orders' => jsonEncode([
          {
            'orderId': 1,
            'orderNumber': 'ORD-1001',
            'customerId': 1,
            'customerCode': 'C-1',
            'customerName': 'أحمد',
            'phoneNumber': '0500000001',
            'orderDate': '2026-01-01T00:00:00Z',
            'deliveryDate': '2026-01-03T00:00:00Z',
            'totalAmount': 100.0,
            'paidAmount': 100.0,
            'remainingAmount': 0.0,
            'orderStatus': 'ReadyForDelivery',
            'urgencyStatus': 'Normal',
          },
          {
            'orderId': 2,
            'orderNumber': 'ORD-1002',
            'customerId': 2,
            'customerCode': 'C-2',
            'customerName': 'سارة',
            'phoneNumber': '0500000002',
            'orderDate': '2026-01-02T00:00:00Z',
            'deliveryDate': '2026-01-04T00:00:00Z',
            'totalAmount': 200.0,
            'paidAmount': 200.0,
            'remainingAmount': 0.0,
            'orderStatus': 'Delivered',
            'urgencyStatus': 'Normal',
          },
        ]),
      '/customers/search' => jsonEncode(List.generate(40, (index) {
          final id = index + 10;
          return {
            'customerId': id,
            'customerCode': 'C-$id',
            'customerName': 'عميل $id',
            'phoneNumber': '05000000${id.toString().padLeft(2, '0')}',
          };
        })),
      _ => jsonEncode({}),
    };

    return _FakeHttpClientRequest(body, statusCode: 200);
  }

  @override
dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _FakeHttpClientRequest implements HttpClientRequest {
  _FakeHttpClientRequest(this._body, {int statusCode = 200}) : _statusCode = statusCode;

  final String _body;
  final int _statusCode;

  @override
  Future<HttpClientResponse> close() async => _FakeHttpClientResponse(_body,
      statusCode: _statusCode);

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _FakeHttpClientResponse implements HttpClientResponse {
  _FakeHttpClientResponse(this._body, {int statusCode = 200}) : _statusCode = statusCode;

  final String _body;
  final int _statusCode;

  @override
  int get statusCode => _statusCode;

  Stream<List<int>> get stream => Stream.value(utf8.encode(_body));

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  setUp(() {
    HttpOverrides.global = _TestHttpOverrides();
  });

  tearDown(() {
    HttpOverrides.global = null;
  });

  testWidgets('orders screen renders with fake HTTP responses', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: OrdersScreen(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('الطلبات'), findsOneWidget);
    expect(find.byType(TabBar), findsOneWidget);
    expect(find.byType(TextField), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
