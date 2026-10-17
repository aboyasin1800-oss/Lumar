import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/models/finance_models.dart';
import 'package:tailoring_system/repositories/finance_repository.dart';
import 'package:tailoring_system/widgets/cash_account_picker.dart';

CashAccount _account({
  required int id,
  bool active = true,
  bool receivesCash = true,
  bool disbursesCash = true,
}) => CashAccount(
  id: id,
  accountName: 'الحساب $id',
  derivedBalance: 0,
  historicalSnapshotBalance: 0,
  isActive: active,
  isReceiptEnabled: receivesCash,
  isDisbursementEnabled: disbursesCash,
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

  test('eligible disbursing accounts exclude inactive and non-dispursing accounts', () {
    final accounts = eligibleCashAccounts([
      _account(id: 1),
      _account(id: 2, active: false),
      _account(id: 3, disbursesCash: false),
    ], direction: CashAccountDirection.disbursing);

    expect(accounts.map((account) => account.id), [1]);
  });

  testWidgets('never auto-selects the first eligible account', (tester) async {
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
    expect(selectedId, isNull);
    expect(find.text('اختر الحساب النقدي'), findsOneWidget);
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

  test('resolveGeneralCashAccount returns the configured setting only', () async {
    final repository = FinanceRepository(
      client: MockClient((request) {
        if (request.url.path == '/settings/general-cash-account') {
          return Future.value(http.Response(jsonEncode({'cashAccountId': 42}), 200));
        }
        if (request.url.path == '/finance/cash-accounts') {
          return Future.value(http.Response(
            jsonEncode([
              {'cashAccountId': 7, 'accountName': 'Other', 'derivedBalance': 0, 'historicalSnapshotBalance': 0, 'isActive': true, 'isReceiptEnabled': true, 'isDisbursementEnabled': true, 'currencyCode': 'YER', 'createdAt': '2026-09-26T00:00:00Z'},
              {'cashAccountId': 42, 'accountName': 'General', 'derivedBalance': 0, 'historicalSnapshotBalance': 0, 'isActive': true, 'isReceiptEnabled': true, 'isDisbursementEnabled': true, 'currencyCode': 'YER', 'createdAt': '2026-09-26T00:00:00Z'},
            ]),
            200,
          ));
        }
        return Future.value(http.Response('not found', 404));
      }),
    );

    final account = await repository.resolveGeneralCashAccount();
    expect(account, isNotNull);
    expect(account!.id, 42);
  });

  test('resolveGeneralCashAccount returns null for missing setting', () async {
    final repository = FinanceRepository(
      client: MockClient((request) {
        if (request.url.path == '/settings/general-cash-account') {
          return Future.value(http.Response(jsonEncode({}), 200));
        }
        return Future.value(http.Response('not found', 404));
      }),
    );

    final account = await repository.resolveGeneralCashAccount();
    expect(account, isNull);
  });

  test('resolveGeneralCashAccount returns null for invalid configured account', () async {
    final repository = FinanceRepository(
      client: MockClient((request) {
        if (request.url.path == '/settings/general-cash-account') {
          return Future.value(http.Response(jsonEncode({'cashAccountId': 42}), 200));
        }
        if (request.url.path == '/finance/cash-accounts') {
          return Future.value(http.Response(
            jsonEncode([
              {'cashAccountId': 42, 'accountName': 'General', 'derivedBalance': 0, 'historicalSnapshotBalance': 0, 'isActive': false, 'isReceiptEnabled': false, 'isDisbursementEnabled': false, 'currencyCode': 'YER', 'createdAt': '2026-09-26T00:00:00Z'},
            ]),
            200,
          ));
        }
        return Future.value(http.Response('not found', 404));
      }),
    );

    final account = await repository.resolveGeneralCashAccount();
    expect(account, isNull);
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