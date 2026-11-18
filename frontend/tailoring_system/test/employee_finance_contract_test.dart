import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:tailoring_system/models/payroll_models.dart';
import 'package:tailoring_system/repositories/payroll_repository.dart';

class _FakeEmployeeFinanceClient extends http.BaseClient {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final path = request.url.path;
    final method = request.method;

    if (path == '/employees/42/ledger' && method == 'GET') {
      final payload = jsonEncode([
        {
          'employeeLedgerEntryId': 99,
          'employeeId': 42,
          'entryType': 'Advance',
          'amount': 250,
          'balanceEffect': -250,
          'effectiveDate': '2025-02-01T00:00:00Z',
          'sourceType': 'EmployeeAdvance',
          'sourceId': 17,
          'referenceNumber': 'ADV-42-20250201',
          'status': 'Posted',
          'createdBy': 'admin',
          'occurredAt': '2025-02-01T00:00:00Z',
        }
      ]);
      return http.StreamedResponse(
        Stream.value(utf8.encode(payload)),
        200,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    if (path == '/employees/42/advances' && method == 'POST') {
      final body = jsonDecode(await request.finalize().bytesToString());
      expect(body['employeeId'], 42);
      expect(body['amount'], 150.0);
      expect(body['referenceNumber'], 'ADV-42-TEST');
      final payload = jsonEncode({
        'transactionId': 77,
        'accountingEventId': 88,
        'amount': 150,
        'actionDate': '2025-02-02T00:00:00Z',
        'actionType': 'Advance',
        'referenceNumber': 'ADV-42-TEST',
        'balanceBefore': 500,
        'balanceAfter': 350,
        'isExisting': false,
      });
      return http.StreamedResponse(
        Stream.value(utf8.encode(payload)),
        201,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }

    return http.StreamedResponse(
      Stream.value(utf8.encode('[]')),
      404,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );
  }
}

void main() {
  test('employee ledger and advance endpoint contract is parsed correctly', () async {
    final repository = PayrollRepository(client: _FakeEmployeeFinanceClient());

    final ledger = await repository.getEmployeeLedger(42);
    expect(ledger, hasLength(1));
    expect(ledger.first.entryType, 'Advance');
    expect(ledger.first.amount, 250);

    final result = await repository.addEmployeeAdvance(
      42,
      amount: 150,
      advanceDate: DateTime(2025, 2, 2),
      cashAccountId: 12,
      referenceNumber: 'ADV-42-TEST',
      sourceOperationId: '11111111-1111-4111-8111-111111111111',
    );

    expect(result.actionType, 'Advance');
    expect(result.amount, 150);
    expect(result.referenceNumber, 'ADV-42-TEST');
  });
}
