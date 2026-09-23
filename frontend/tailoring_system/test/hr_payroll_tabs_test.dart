import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:tailoring_system/models/finance_models.dart';
import 'package:tailoring_system/models/payroll_models.dart';
import 'package:tailoring_system/repositories/payroll_repository.dart';
import 'package:tailoring_system/screens/employee_draws_screen.dart';
import 'package:tailoring_system/screens/factory_monitor_screen.dart';
import 'package:tailoring_system/screens/payroll/employee_production_screen.dart';
import 'package:tailoring_system/screens/payroll/piece_wage_screen.dart';
import 'package:tailoring_system/screens/production_screen.dart';

class _FakePayrollClient extends http.BaseClient {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final path = request.url.path;
    final payload = switch (path) {
      '/payroll/piece-wages' => jsonEncode([
            {
              'pieceWageRecordId': 1,
              'orderId': 10,
              'orderItemId': 11,
              'pieceId': 100,
              'trackingEventId': 200,
              'employeeId': 7,
              'employeeCode': 'EMP-7',
              'pieceType': 'بنطلون',
              'stage': 'خياطة',
              'quantity': 5,
              'wageRate': 12.5,
              'totalWage': 62.5,
              'payrollPeriodId': 1,
              'payrollRecordId': 1,
              'status': 'PendingPayroll',
              'notes': 'مراجعة',
              'createdAt': '2025-02-01T00:00:00Z',
            }
          ]),
      '/payroll/piece-wage-rates' => jsonEncode([
            {
              'pieceWageRateId': 1,
              'pieceType': 'بنطلون',
              'stage': 'خياطة',
              'wageRate': 12.5,
              'isActive': true,
              'notes': 'معدل عام',
              'createdAt': '2025-01-01T00:00:00Z',
              'updatedAt': null,
            }
          ]),
      '/employees' => jsonEncode([
            {
              'employeeId': 7,
              'employeeCode': 'EMP-7',
              'employeeName': 'أحمد محمد',
              'jobTitle': 'خياط',
              'phoneNumber': '0500000000',
              'isActive': true,
              'status': 'Active',
              'departmentId': 1,
            }
          ]),
      '/employees/7/draws' => jsonEncode([
            {
              'drawId': 30,
              'employeeCode': 'EMP-7',
              'drawDate': '2025-02-02T00:00:00Z',
              'amount': 25,
              'notes': 'سلفة شهرية',
            }
          ]),
      '/payroll/employees/7/settlements' => jsonEncode([
            {
              'settlementId': 1,
              'drawId': 30,
              'employeeCode': 'EMP-7',
              'settlementDate': '2025-02-03T00:00:00Z',
              'amount': 10,
              'notes': 'تسوية أولية',
              'journalEntryId': 99,
            }
          ]),
      '/orders/10' => jsonEncode({
            'orderId': 10,
            'orderNumber': 'ORD-1010',
            'customerId': 2,
            'orderDate': '2025-02-01T00:00:00Z',
            'deliveryDate': '2025-02-10T00:00:00Z',
            'totalAmount': 500,
            'discountAmount': 0,
            'paidAmount': 0,
            'remainingAmount': 500,
            'urgencyStatus': 'normal',
            'orderStatus': 'active',
            'notes': null,
            'createdDate': '2025-02-01T00:00:00Z',
            'updatedDate': null,
            'cancellationReason': null,
            'cancelledAt': null,
            'cancelledBy': null,
            'saleCategory': 'tailoring',
            'revenueRecognized': false,
            'revenueRecognizedAt': null,
            'revenueReversalCreated': false,
            'revenueReversalCreatedAt': null,
          }),
      '/orders/10/items' => jsonEncode([
            {
              'orderItemId': 11,
              'orderId': 10,
              'pieceType': 'بنطلون',
              'quantity': 1,
              'fabricCode': 'FAB-001',
              'fabricType': 'قماش',
              'fabricColor': 'أسود',
              'request1': null,
              'request2': null,
              'specialRequest': null,
              'notes1': null,
              'notes2': null,
              'trackingCode': 'PT-1001',
              'pieceStatus': 'Sewing',
              'createdDate': '2025-02-01T00:00:00Z',
            }
          ]),
      '/orders/10/pieces' => jsonEncode([
            {
              'pieceId': 100,
              'orderItemId': 11,
              'trackingCode': 'PT-1001',
              'pieceStatus': 'Sewing',
              'pieceNumber': 1,
              'createdDate': '2025-02-01T00:00:00Z',
            }
          ]),
      '/customers/2' => jsonEncode({
            'customerId': 2,
            'customerCode': 'C-002',
            'customerName': 'مؤسسة النور',
            'phoneNumber': '0500000002',
            'status': 'Active',
            'createdAt': '2025-01-01T00:00:00Z',
          }),
      '/production/pieces/100' => jsonEncode({
            'pieceId': 100,
            'orderItemId': 11,
            'trackingCode': 'PT-1001',
            'pieceStatus': 'Sewing',
            'pieceNumber': 1,
            'createdDate': '2025-02-01T00:00:00Z',
            'pieceType': 'بنطلون',
          }),
      _ => jsonEncode([]),
    };

    return http.StreamedResponse(
      Stream.value(Uint8List.fromList(utf8.encode(payload))),
      200,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );
  }
}

class _FakeProductionApi extends ProductionApi {
  int loadCount = 0;

  @override
  Future<ProductionData> load() async {
    loadCount += 1;
    return ProductionData(
      dashboard: {
        'totalPieces': 2,
        'inProductionPieces': 1,
        'readyPieces': 1,
        'deliveredPieces': 0,
        'activeStages': 3,
        'readyForSaleProducts': 0,
        'delayedPieces': 0,
      },
      pieces: const [],
      readyOrders: const [],
      readyInventory: const [],
      wages: const [],
      scanners: const [],
      scans: const [],
      deliveries: const [],
    );
  }
}

void main() {
  testWidgets('employee draw ledger renders current balance and operating expense split', (tester) async {
    const employee = PayrollEmployee(
      id: 7,
      code: 'EMP-7',
      name: 'أحمد محمد',
      jobTitle: 'خياط',
      phoneNumber: '0500000000',
      isActive: true,
      status: 'Active',
      departmentId: 1,
    );

    final draws = [
      const EmployeeDraw(
        id: 30,
        employeeCode: 'EMP-7',
        drawDate: null,
        amount: 350,
        notes: 'سلفة أولية',
      ),
    ];
    final settlements = [
      PayrollSettlement(
        settlementId: 1,
        drawId: 30,
        employeeCode: 'EMP-7',
        settlementDate: DateTime(2025, 2, 3),
        amount: 200,
        notes: 'تسوية',
        journalEntryId: 99,
      ),
    ];
    final operatingExpenses = [
      FinancialTransaction(
        id: 1,
        referenceNumber: 'EXP-001',
        transactionType: 'OperatingExpense',
        amount: 400,
        description: 'مصاريف محل',
        createdAt: DateTime(2025, 2, 3),
      ),
    ];

    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeDrawsScreen(
          employee: employee,
          draws: draws,
          settlements: settlements,
          pieceWageTotal: 1200,
          operatingExpenses: operatingExpenses,
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('دفتر السلف والتسويات'), findsOneWidget);
    expect(find.text('الرصيد الجاري'), findsOneWidget);
    expect(find.text('150.00'), findsOneWidget);
    expect(find.text('المصروفات'), findsOneWidget);
    expect(find.text('400.00'), findsOneWidget);
  });

  testWidgets('yasin ledger scenario keeps employee balance separate from operating expenses', (tester) async {
    const employee = PayrollEmployee(
      id: 7,
      code: 'EMP-7',
      name: 'أحمد محمد',
      jobTitle: 'خياط',
      phoneNumber: '0500000000',
      isActive: true,
      status: 'Active',
      departmentId: 1,
    );

    const draw = EmployeeDraw(
      id: 30,
      employeeCode: 'EMP-7',
      drawDate: null,
      amount: 1000,
      notes: 'سلفة 1000',
    );
    final settlements = [
      PayrollSettlement(
        settlementId: 1,
        drawId: 30,
        employeeCode: 'EMP-7',
        settlementDate: DateTime(2026, 9, 13),
        amount: 300,
        notes: 'تسوية 300',
        journalEntryId: 99,
      ),
    ];
    final operatingExpenses = [
      FinancialTransaction(
        id: 1,
        referenceNumber: 'EXP-1000',
        transactionType: 'OperatingExpense',
        amount: 1000,
        description: 'مصروف محل 1000',
        createdAt: DateTime(2026, 9, 13),
      ),
    ];

    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeDrawsScreen(
          employee: employee,
          draws: const [draw],
          settlements: settlements,
          operatingExpenses: operatingExpenses,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('الرصيد الجاري'), findsOneWidget);
    expect(find.text('1,000.00'), findsWidgets);
    expect(find.text('700.00'), findsOneWidget);
    expect(find.text('المصروفات'), findsOneWidget);
    expect(find.textContaining('1,000'), findsWidgets);
  });

  testWidgets('production screen refreshes automatically on polling timer', (tester) async {
    final api = _FakeProductionApi();

    await tester.pumpWidget(
      MaterialApp(
        home: ProductionScreen(
          api: api,
          refreshInterval: const Duration(milliseconds: 80),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(api.loadCount, 1);

    await Future<void>.delayed(const Duration(milliseconds: 140));
    await tester.pump();

    expect(api.loadCount, greaterThanOrEqualTo(2));
  });

  testWidgets('piece wage screen renders live tab content', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: PieceWageScreen(
          repository: PayrollRepository(client: _FakePayrollClient()),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('أجور القطعة'), findsWidgets);
    expect(find.text('أسعار القطعة'), findsWidgets);
    expect(find.text('EMP-7'), findsOneWidget);
    expect(find.text('ORD-1010'), findsOneWidget);
    expect(find.text('PT-1001'), findsOneWidget);
  });

  testWidgets('employee production ledger shows net due and contextual data', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeProductionScreen(
          repository: PayrollRepository(client: _FakePayrollClient()),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('كشف حساب الإنتاج'), findsWidgets);
    expect(find.text('ORD-1010'), findsOneWidget);
    expect(find.text('PT-1001'), findsOneWidget);
    expect(find.textContaining('27.50'), findsWidgets);
  });

  testWidgets('factory monitor auto-scrolls long columns', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: FactoryMonitorScreen(
          dataSource: () async => {
            'totalAtRisk': 0,
            'totalStalled': 12,
            'totalBlocked': 0,
            'totalReadyForDelivery': 0,
            'overallProgressPercent': 42.5,
            'lastUpdatedAt': '2026-09-13T00:00:00Z',
            'atRiskOrders': <Map<String, dynamic>>[],
            'stalledOrders': List.generate(12, (index) => {
                  'orderId': 1000 + index,
                  'orderNumber': 'ORD-TEST-$index',
                  'customerName': 'عميل $index',
                  'customerCode': 'C-$index',
                  'progressPercent': 25,
                  'daysRemaining': 2,
                  'reason': 'يوجد تقدم لكنه بطيء ومؤخر نسبياً.',
                  'delayedPieceCode': 'TRK-$index',
                  'delayedCurrentStage': 'Cutting',
                  'delayedNextStage': 'Sewing',
                }),
            'blockedOrders': <Map<String, dynamic>>[],
            'readyForDeliveryOrders': <Map<String, dynamic>>[],
          },
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('قص'), findsWidgets);
    expect(find.text('خياطة'), findsWidgets);
    expect(find.text('جاهز'), findsWidgets);

    final listView = tester.widget<ListView>(find.byType(ListView).last);
    final controller = listView.controller!;

    expect(controller.position.maxScrollExtent, greaterThan(0));
    expect(controller.offset, greaterThanOrEqualTo(0));
    expect(find.text('قص'), findsWidgets);
    expect(find.text('خياطة'), findsWidgets);
  });
}
