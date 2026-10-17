import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/core/authenticated_api_client.dart';
import 'package:tailoring_system/services/auth_state.dart';

void main() {
  test('AuthenticatedApiClient adds the Bearer token when saving an order', () async {
    final server = await HttpServer.bind('127.0.0.1', 0);
    addTearDown(() async => server.close(force: true));

    final baseUrl = 'http://${server.address.host}:${server.port}';
    final auth = AuthState(baseUrl: baseUrl);

    server.listen((request) async {
      if (request.uri.path == '/auth/login') {
        final body = await utf8.decoder.bind(request).join();
        expect(jsonDecode(body)['username'], 'admin');

        request.response.statusCode = HttpStatus.ok;
        request.response.headers.contentType = ContentType.json;
        request.response.write(jsonEncode({
          'token': 'demo-session-token',
          'user': {
            'userId': 1,
            'username': 'admin',
            'fullName': 'Admin',
            'isActive': true,
            'accountType': 'Employee',
            'employeeId': 7,
          },
        }));
        await request.response.close();
        return;
      }

      if (request.uri.path == '/orders') {
        expect(request.headers.value(HttpHeaders.authorizationHeader),
            'Bearer demo-session-token');

        final body = await utf8.decoder.bind(request).join();
        final payload = jsonDecode(body) as Map<String, dynamic>;
        expect(payload['requestReference'], 'ORD-TEST-001');
        expect(payload['cashAccountId'], 42);

        request.response.statusCode = HttpStatus.created;
        request.response.headers.contentType = ContentType.json;
        request.response.write(jsonEncode({
          'orderId': 77,
          'orderNumber': 'ORD-77',
        }));
        await request.response.close();
        return;
      }

      request.response.statusCode = HttpStatus.notFound;
      await request.response.close();
    });

    final loginError = await auth.login('admin', '1234', true);
    expect(loginError, isNull);
    expect(auth.token, 'demo-session-token');

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
      baseUrl: baseUrl,
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
}
