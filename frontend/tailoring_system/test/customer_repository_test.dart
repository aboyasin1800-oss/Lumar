import 'dart:convert';
import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/repositories/customer_creation_repository.dart';
import 'package:tailoring_system/repositories/customer_repository.dart';
import 'package:tailoring_system/screens/customers_screen.dart';

void main() {
  test('online creation leaves identity generation to the backend', () async {
    final client = MockClient((request) async {
      expect(request.method, 'POST');
      expect(request.url.path, '/customers/with-referral');
      final payload = jsonDecode(request.body) as Map<String, dynamic>;
      expect(payload.containsKey('customerCode'), isFalse);
      expect(payload.containsKey('globalCustomerUid'), isFalse);
      expect(payload['customerName'], 'Customer Test Name');
      return http.Response(jsonEncode({
        'customerId': 42,
        'customerCode': 'C-A7K3M-9Q8X2-RD4F',
        'customerName': 'Customer Test Name',
        'phoneNumber': '775123456',
      }), 201);
    });
    final created = await CustomerCreationRepository(client: client).create(
      customerName: 'Customer Test Name',
      phoneNumber: '775123456',
    );
    expect(created.customerId, 42);
    expect(created.customerCode, 'C-A7K3M-9Q8X2-RD4F');
    client.close();
  });

  for (final brightness in Brightness.values) {
    for (final code in ['C10149', 'C-A7K3M-9Q8X2-RD4F']) {
      testWidgets('customer code search and edit $brightness $code', (tester) async {
        final http = _CustomerHttpOverrides(code);
        HttpOverrides.global = http;
        addTearDown(() => HttpOverrides.global = null);
        await tester.pumpWidget(MaterialApp(
          theme: ThemeData(brightness: brightness),
          home: const Scaffold(body: CustomerPage()),
        ));
        await tester.pumpAndSettle();
        await tester.enterText(find.byType(TextField).first, code);
        await tester.pump(const Duration(milliseconds: 300));
        await tester.pumpAndSettle();
        expect(http.searchTerms, contains(code));
        expect(find.byTooltip('تعديل العميل'), findsOneWidget);
        await tester.tap(find.byTooltip('تعديل العميل'));
        await tester.pumpAndSettle();
        final codeField = find.byWidgetPredicate((widget) =>
            widget is TextField && widget.decoration?.labelText == 'رمز العميل');
        expect(tester.widget<TextField>(codeField).readOnly, code.length == 18);
        final nameField = find.byWidgetPredicate((widget) =>
            widget is TextField && widget.decoration?.labelText == 'اسم العميل');
        await tester.enterText(nameField, 'Updated Customer Name');
        await tester.tap(find.text('حفظ'));
        await tester.pumpAndSettle();
        expect(http.saved?['customerCode'], code);
        expect(http.saved?['customerName'], 'Updated Customer Name');
        expect(tester.takeException(), isNull);
      });
    }
  }

  test('getDetails tolerates partial backend failures', () async {
    final client = MockClient((request) {
      final path = request.url.path;
      switch (path) {
        case '/customers/42':
          return Future.value(http.Response(
            jsonEncode({
              'customerId': 42,
              'customerCode': 'C-42',
              'customerName': 'Ali Test',
              'phoneNumber': '0500000000',
              'parentCustomerCode': null,
              'parentCustomerId': null,
              'parentCustomerName': null,
              'address': 'Address',
              'notes': null,
              'isActive': true,
              'totalPoints': 12.5,
              'totalPieces': 2,
              'totalDebts': 0,
              'relationshipType': 'VIP',
            }),
            200,
          ));
        case '/customers/42/loyalty':
          return Future.value(http.Response('server error', 500));
        case '/customers/42/referrals':
          return Future.value(http.Response('not found', 404));
        case '/customers/42/ledger':
          return Future.value(http.Response('[]', 200));
        case '/orders':
          return Future.value(http.Response('[]', 200));
        case '/finance/transactions':
          return Future.value(http.Response('[]', 200));
        default:
          return Future.value(http.Response('not found', 404));
      }
    });

    final repository = CustomerRepository(client: client);

    final data = await repository.getDetails(42);

    expect(data.customer.id, 42);
    expect(data.customer.name, 'Ali Test');
    expect(data.loyalty, isNull);
    expect(data.referral, isNull);
    expect(data.orders, isEmpty);
    expect(data.payments, isEmpty);
    expect(data.ledger, isEmpty);
  });
}

class _CustomerHttpOverrides extends HttpOverrides {
  _CustomerHttpOverrides(this.code);
  final String code;
  final searchTerms = <String>[];
  Map<String, dynamic>? saved;

  Map<String, dynamic> get customer => {
        'customerId': 42,
        'customerCode': code,
        'customerName': 'Customer Test Name',
        'phoneNumber': '775123456',
        'isActive': true,
      };

  @override
  HttpClient createHttpClient(SecurityContext? context) => _CustomerClient(this);
}

class _CustomerClient implements HttpClient {
  _CustomerClient(this.owner);
  final _CustomerHttpOverrides owner;

  @override
  void close({bool force = false}) {}

  @override
  Future<HttpClientRequest> getUrl(Uri url) => openUrl('GET', url);

  @override
  Future<HttpClientRequest> openUrl(String method, Uri url) async =>
      _CustomerRequest(owner, method, url);

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _CustomerRequest implements HttpClientRequest {
  _CustomerRequest(this.owner, this.method, this.url);
  final _CustomerHttpOverrides owner;
  @override
  final String method;
  final Uri url;
  String body = '';

  @override
  final HttpHeaders headers = _CustomerHeaders();

  @override
  void write(Object? object) => body += object.toString();

  @override
  Future<HttpClientResponse> close() async {
    if (method == 'PUT') {
      owner.saved = jsonDecode(body) as Map<String, dynamic>;
    }
    if (url.path.endsWith('/search')) {
      owner.searchTerms.add(url.queryParameters['term']!);
    }
    final result = url.path.endsWith('/42')
        ? owner.customer
        : [owner.customer];
    return _CustomerResponse(jsonEncode(result));
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _CustomerHeaders implements HttpHeaders {
  @override
  set contentType(ContentType? value) {}

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _CustomerResponse extends Stream<List<int>> implements HttpClientResponse {
  _CustomerResponse(this.body);
  final String body;

  @override
  int get statusCode => 200;

  @override
  StreamSubscription<List<int>> listen(void Function(List<int>)? onData,
          {Function? onError, void Function()? onDone, bool? cancelOnError}) =>
      Stream.value(utf8.encode(body)).listen(onData,
          onError: onError, onDone: onDone, cancelOnError: cancelOnError);

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
