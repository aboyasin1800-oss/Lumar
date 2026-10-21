import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/repositories/customer_repository.dart';

void main() {
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
