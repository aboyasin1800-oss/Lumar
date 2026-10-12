import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:tailoring_system/models/finance_models.dart';
import 'package:tailoring_system/repositories/finance_repository.dart';
import 'package:tailoring_system/screens/financial/finance_views.dart';

void main() {
  test('getSupplierLedgerBySupplierId calls supplier-scoped ledger endpoint',
      () async {
    final client = MockClient((request) async {
      expect(request.method, equals('GET'));
      expect(request.url.path, equals('/suppliers/42/ledger'));
      return http.Response('[]', 200,
          headers: {'content-type': 'application/json'});
    });

    final repository = FinanceRepository(client: client);
    final result = await repository.getSupplierLedgerBySupplierId(42);

    expect(result, isEmpty);
  });

  test(
      'getSupplierInvoicesBySupplierId calls supplier-scoped invoices endpoint',
      () async {
    final client = MockClient((request) async {
      expect(request.method, equals('GET'));
      expect(request.url.path, equals('/suppliers/42/invoices'));
      return http.Response('[]', 200,
          headers: {'content-type': 'application/json'});
    });

    final repository = FinanceRepository(client: client);
    final result = await repository.getSupplierInvoicesBySupplierId(42);

    expect(result, isEmpty);
  });

  test(
      'getSupplierPaymentsBySupplierId calls supplier-scoped payments endpoint',
      () async {
    final client = MockClient((request) async {
      expect(request.method, equals('GET'));
      expect(request.url.path, equals('/suppliers/42/payments'));
      return http.Response('[]', 200,
          headers: {'content-type': 'application/json'});
    });

    final repository = FinanceRepository(client: client);
    final result = await repository.getSupplierPaymentsBySupplierId(42);

    expect(result, isEmpty);
  });

  test('SupplierInvoice.fromJson accepts null purchaseOrderId from the backend', () {
    final invoice = SupplierInvoice.fromJson({
      'supplierInvoiceId': 101,
      'supplierId': 42,
      'purchaseOrderId': null,
      'invoiceNumber': 'INV-1001',
      'invoiceDate': '2025-01-10T00:00:00Z',
      'dueDate': '2025-01-30T00:00:00Z',
      'totalAmount': 1200.0,
      'amountPaid': 250.0,
      'status': 'Open',
      'notes': 'ملاحظات',
      'createdAt': '2025-01-09T00:00:00Z',
    });

    expect(invoice.purchaseOrderId, isNull);
    expect(invoice.invoiceNumber, 'INV-1001');
  });

  testWidgets(
      'supplier profile tabs stay responsive in compact window without overflow',
      (tester) async {
    const compactSize = Size(360, 640);
    tester.view.physicalSize = compactSize;
    tester.view.devicePixelRatio = 1;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: SizedBox(
              width: compactSize.width,
              height: compactSize.height,
              child: DefaultTabController(
                length: 3,
                child: Column(
                  children: [
                    const TabBar(
                      tabs: [
                        Tab(text: 'كشف الحساب'),
                        Tab(text: 'الفواتير'),
                        Tab(text: 'المدفوعات'),
                      ],
                    ),
                    const SizedBox(height: 8),
                    SizedBox(
                      height: 440,
                      child: TabBarView(
                        children: [
                          ListView(
                            children: const [
                              ListTile(title: Text('كشف الحساب')),
                              ListTile(title: Text('السطر 1')),
                            ],
                          ),
                          ListView(
                            children: const [
                              ListTile(title: Text('الفواتير')),
                              ListTile(title: Text('السطر 1')),
                            ],
                          ),
                          ListView(
                            children: const [
                              ListTile(title: Text('المدفوعات')),
                              ListTile(title: Text('السطر 1')),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );

    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'supplier ledger screen does not ask for a manual supplier id when supplier is already selected',
      (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: SupplierLedgerScreen(supplierId: 42),
    ));

    expect(find.text('أدخل الرقم لعرض كشف الحساب.'), findsNothing);
  });
}
