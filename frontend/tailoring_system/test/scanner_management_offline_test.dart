import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:tailoring_system/screens/settings/scanner_management_screen.dart';

void main() {
  test('ScannerRepository maps backend disconnect to a safe ScannerApiException', () async {
    final repository = ScannerRepository(
      client: _FailingClient(
        const SocketException('Connection refused'),
      ),
    );

    await expectLater(
      repository.getScanners(),
      throwsA(isA<ScannerApiException>()),
    );

    try {
      await repository.getScanners();
      fail('Expected a ScannerApiException');
    } on ScannerApiException catch (error) {
      expect(error.message, contains('تعذر الاتصال بالخادم'));
    }
  });
}

class _FailingClient extends http.BaseClient {
  _FailingClient(this.exception);

  final Object exception;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    throw exception;
  }
}
