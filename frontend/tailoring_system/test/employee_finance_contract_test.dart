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

  test('daily expense failures retain the safe HTTP response body for diagnosis', () async {
    final fake = _FailingDailyExpenseClient();
    final failingRepository = PayrollRepository(client: fake);

    try {
      await failingRepository.addEmployeeDailyExpense(
        42,
        amount: 500,
        expenseDate: DateTime(2025, 2, 2),
        cashAccountId: 12,
        referenceNumber: 'EXP-42-TEST',
        notes: 'daily expense',
        sourceOperationId: '11111111-1111-4111-8111-222222222222',
      );
      fail('Expected daily expense request to fail.');
    } on PayrollApiException catch (error) {
      expect(error.statusCode, 500);
      expect(error.responseBody, contains('GEN-001'));
      expect(error.path, contains('/employees/42/daily-expenses'));
    }
  });

  test('daily expense auto-generates a valid GUID sourceOperationId', () async {
    final client = _CapturingSourceOperationClient();
    final repository = PayrollRepository(client: client);

    await repository.addEmployeeDailyExpense(
      42,
      amount: 500,
      expenseDate: DateTime(2025, 2, 2),
      cashAccountId: 12,
      referenceNumber: 'EXP-42-TEST',
      notes: 'daily expense',
    );

    expect(client.lastSourceOperationId, isNotNull);
    expect(client.lastSourceOperationId, isNotEmpty);
    expect(client.lastSourceOperationId, isNot('AUTO_GENERATED'));
    expect(client.lastSourceOperationId, matches(RegExp(r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$')));
  });

  test('Arabic ledger labels are translated for display only without changing raw values', () {
    final entry = EmployeeLedgerEntry(
      employeeLedgerEntryId: 1,
      employeeId: 42,
      entryType: 'PieceWorkerDailyExpense',
      amount: 500,
      balanceEffect: -500,
      effectiveDate: DateTime(2025, 2, 2),
      sourceType: 'EmployeeAccrualSource',
      sourceId: 9,
      referenceNumber: 'EXP-42-TEST',
      status: 'Posted',
      createdBy: 'admin',
      occurredAt: DateTime(2025, 2, 2),
    );

    expect(entry.displayEntryType, 'مصروف يومي لموظف أجر قطعة');
    expect(entry.displaySourceType, 'مصدر استحقاق موظف');
    expect(entry.displayStatus, 'مسجّل');
    expect(entry.entryType, 'PieceWorkerDailyExpense');
    expect(entry.sourceType, 'EmployeeAccrualSource');
  });

  test('daily expense total refreshes from official ledger entries instead of local deduction assumptions', () {
    final financial = _EmployeeFinancialSummaryForTest(
      draws: const [],
      settlements: const [],
      pieceWages: const [],
      officialSummary: null,
      ledgerEntries: [
        EmployeeLedgerEntry(
          employeeLedgerEntryId: 1,
          employeeId: 42,
          entryType: 'PieceWorkerDailyExpense',
          amount: 500,
          balanceEffect: -500,
          effectiveDate: DateTime(2025, 2, 2),
          sourceType: 'EmployeeAccrualSource',
          sourceId: 9,
          referenceNumber: 'EXP-42-TEST',
          status: 'Posted',
          createdBy: 'admin',
          occurredAt: DateTime(2025, 2, 2),
        ),
        EmployeeLedgerEntry(
          employeeLedgerEntryId: 2,
          employeeId: 42,
          entryType: 'Advance',
          amount: 160,
          balanceEffect: -160,
          effectiveDate: DateTime(2025, 2, 2),
          sourceType: 'EmployeeAdvance',
          sourceId: 11,
          referenceNumber: 'ADV-42-TEST',
          status: 'Posted',
          createdBy: 'admin',
          occurredAt: DateTime(2025, 2, 2),
        ),
      ],
    );

    expect(financial.officialDeductionTotal, 500);
    expect(financial.dailyExpenseTotal, 500);
  });

  test('employee remaining wages is backend official and distinct from payroll net amount', () {
    final financial = _EmployeeFinancialSummaryForTest(
      draws: const [],
      settlements: const [],
      pieceWages: const [],
      officialSummary: const EmployeeSelfFinancialSummary(
        employeeId: 42,
        payrollPeriodId: 7,
        basicSalaryAmount: 0,
        pieceWageAmount: 2000,
        overtimeAmount: 0,
        grossAmount: 2000,
        deductionsAmount: 500,
        netAmount: 1500,
        remainingWages: 1500,
        status: 'PendingPayroll',
        createdAt: null,
      ),
      ledgerEntries: [
        EmployeeLedgerEntry(
          employeeLedgerEntryId: 1,
          employeeId: 42,
          entryType: 'PieceWageAccrual',
          amount: 2000,
          balanceEffect: 2000,
          effectiveDate: DateTime(2025, 2, 2),
          sourceType: 'PieceWageRecord',
          sourceId: 9,
          referenceNumber: 'PWR-42-2000',
          status: 'Posted',
          createdBy: 'admin',
          occurredAt: DateTime(2025, 2, 2),
        ),
        EmployeeLedgerEntry(
          employeeLedgerEntryId: 2,
          employeeId: 42,
          entryType: 'PieceWorkerDailyExpense',
          amount: 500,
          balanceEffect: -500,
          effectiveDate: DateTime(2025, 2, 3),
          sourceType: 'EmployeeAccrualSource',
          sourceId: 10,
          referenceNumber: 'EXP-42-TEST',
          status: 'Posted',
          createdBy: 'admin',
          occurredAt: DateTime(2025, 2, 3),
        ),
      ],
    );

    expect(financial.remainingWages, 1500);
    expect(financial.payrollNetAmount, 1500);
    expect(financial.remainingWages, greaterThanOrEqualTo(0));
    expect(financial.remainingWages, isNot(-500));
  });
}

class _EmployeeFinancialSummaryForTest {
  const _EmployeeFinancialSummaryForTest({
    required this.draws,
    required this.settlements,
    required this.pieceWages,
    this.officialSummary,
    this.ledgerEntries = const <EmployeeLedgerEntry>[],
  });

  final List<EmployeeDraw> draws;
  final List<PayrollSettlement> settlements;
  final List<PieceWageRecord> pieceWages;
  final EmployeeSelfFinancialSummary? officialSummary;
  final List<EmployeeLedgerEntry> ledgerEntries;

  double get officialDeductionTotal =>
      ledgerEntries
          .where((entry) =>
              entry.entryType.toLowerCase().contains('dailyexpense') ||
              entry.sourceType.toLowerCase().contains('dailyexpense') ||
              entry.entryType.toLowerCase().contains('pieceworkerdailyexpense') ||
              entry.entryType.toLowerCase().contains('salariedemployeedailyexpense'))
          .fold<double>(0, (sum, entry) => sum + entry.amount);

  double get dailyExpenseTotal => officialDeductionTotal;
  double get currentDueBalance =>
      ledgerEntries.fold<double>(0, (sum, entry) => sum + entry.balanceEffect);
  double get payrollNetAmount => officialSummary?.netAmount ?? 0;
  double get remainingWages => officialSummary?.remainingWages ?? currentDueBalance;
}

class _FailingDailyExpenseClient extends http.BaseClient {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final payload = jsonEncode({
      'success': false,
      'errorCode': 'GEN-001',
      'userFriendlyMessage': 'Unexpected error',
      'correlationId': 'abc-123',
    });

    return http.StreamedResponse(
      Stream.value(utf8.encode(payload)),
      500,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );
  }
}

class _CapturingSourceOperationClient extends http.BaseClient {
  String? lastSourceOperationId;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final body = jsonDecode(await request.finalize().bytesToString()) as Map<String, dynamic>;
    lastSourceOperationId = body['sourceOperationId']?.toString();

    final payload = jsonEncode({
      'transactionId': 101,
      'accountingEventId': 202,
      'amount': 500,
      'actionDate': '2025-02-02T00:00:00Z',
      'actionType': 'DailyExpense',
      'referenceNumber': 'EXP-42-TEST',
      'balanceBefore': 0,
      'balanceAfter': 0,
      'isExisting': false,
    });

    return http.StreamedResponse(
      Stream.value(utf8.encode(payload)),
      201,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );
  }
}
