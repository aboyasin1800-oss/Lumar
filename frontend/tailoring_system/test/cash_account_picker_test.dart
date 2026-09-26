import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/finance_models.dart';
import 'package:tailoring_system/widgets/cash_account_picker.dart';

CashAccount _account({
  required int id,
  bool active = true,
  bool receivesCash = true,
}) => CashAccount(
  id: id,
  accountName: 'الحساب $id',
  derivedBalance: 0,
  historicalSnapshotBalance: 0,
  isActive: active,
  isReceiptEnabled: receivesCash,
  currencyCode: 'YER',
  createdAt: DateTime.utc(2026, 9, 26),
);

void main() {
  test('eligible receiving accounts exclude inactive and non-receiving accounts', () {
    final accounts = eligibleReceivingCashAccounts([
      _account(id: 1),
      _account(id: 2, active: false),
      _account(id: 3, receivesCash: false),
    ]);

    expect(accounts.map((account) => account.id), [1]);
  });

  testWidgets('selects the only eligible receiving account automatically',
      (tester) async {
    int? selectedId;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CashAccountPicker(
          value: selectedId,
          onChanged: (value) => selectedId = value,
          accountsFuture: Future.value([_account(id: 1)]),
        ),
      ),
    ));

    await tester.pumpAndSettle();
    expect(selectedId, 1);
  });

  testWidgets('requires manual selection when multiple accounts are eligible',
      (tester) async {
    int? selectedId;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CashAccountPicker(
          value: selectedId,
          onChanged: (value) => selectedId = value,
          accountsFuture: Future.value([_account(id: 1), _account(id: 2)]),
        ),
      ),
    ));

    await tester.pumpAndSettle();
    expect(selectedId, isNull);
    expect(find.text('اختر الحساب النقدي'), findsOneWidget);
  });

  testWidgets('blocks selection when no account is eligible', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CashAccountPicker(
          value: null,
          onChanged: (_) {},
          accountsFuture: Future.value([
            _account(id: 2, active: false),
            _account(id: 3, receivesCash: false),
          ]),
        ),
      ),
    ));

    await tester.pumpAndSettle();
    expect(
      find.text('لا يوجد حساب نقدي نشط ومؤهل لاستلام النقدية.'),
      findsOneWidget,
    );
  });
}