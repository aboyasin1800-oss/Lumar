import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/screens/sales_screen.dart';

void main() {
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  testWidgets('changed phone blocks save before validating pieces', (tester) async {
    await withSalesScreen(tester, (state) async {
      await tester.runAsync(() async => await state.searchCustomerByTerm('A'));
      state.phoneController.text = '222';
      await state.saveOrder();
      await tester.pump();
      expect(find.text('تحقق من اختيار العميل وبياناته قبل حفظ الطلب.'),
          findsOneWidget);
    });
  });

  testWidgets('switching a populated customer draft requires confirmation',
      (tester) async {
    await withSalesScreen(tester, (state) async {
        await tester.runAsync(() async => await state.searchCustomerByTerm('A'));
      state.notes1Controllers.first.text = 'original note';
        late Future<dynamic> selection;
        await tester.runAsync(() async {
          selection = state.searchCustomerByTerm('B');
          await Future<void>.delayed(Duration.zero);
        });
      await tester.pumpAndSettle();
      expect(find.text('تغيير العميل'), findsOneWidget);
      expect(state.currentCustomerId, 1);
      await tester.tap(find.text('إلغاء'));
      await tester.pumpAndSettle();
      await tester.runAsync(() async => await selection);
      expect(state.currentCustomerId, 1);
      expect(state.phoneController.text, '111');
      expect(state.notes1Controllers.first.text, 'original note');
    });
  });

  testWidgets('empty draft switches customer and replaces official identity', (tester) async {
    await withSalesScreen(tester, (state) async {
      await select(tester, state, 'A');
      await select(tester, state, 'B');
      expect(state.currentCustomerId, 2);
      expect(state.phoneController.text, '222');
      expect(state.customerCodeController.text, 'B');
      expect(find.byType(AlertDialog), findsNothing);
    });
  });

  testWidgets('confirmed switch clears all dependent data and loads B measurements', (tester) async {
    await withSalesScreen(tester, (state) async {
      await prepareOrder(tester, state);
      state.notes1Controllers.first.text = 'A note';
      state.catalogNumberControllers.first.text = 'A catalog';
      state.paidAmountController.text = '5';
      state.discountAmountController.text = '3';
      state.deliveryDateController.text = '2027-01-01';
      await confirmSwitch(tester, state);
      expect(state.currentCustomerId, 2);
      expect(state.pieceTypes, ['']);
      expect(state.notes1Controllers.first.text, isEmpty);
      expect(state.catalogNumberControllers.first.text, isEmpty);
      expect(state.fabricCodeControllers.first.text, isEmpty);
      expect(state.paidAmountController.text, '0.00');
      expect(state.discountAmountController.text, '0.00');
      expect(state.deliveryDateController.text, isEmpty);
      state.pieceTypes[0] = 'ثوب';
      await tester.runAsync(() async => await state.loadMeasurementPreview());
      expect(state.measurementValuesByType['piece_0']['الطول'], '20');
    });
  });

  testWidgets('late A measurements cannot overwrite B', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await select(tester, state, 'A');
      state.pieceTypes[0] = 'ثوب';
      final delayed = Completer<http.Response>();
      backend.nextMeasurement = delayed;
      late Future<dynamic> oldLoad;
      await tester.runAsync(() async {
        oldLoad = state.loadMeasurementPreview();
        await Future<void>.delayed(Duration.zero);
      });
      await confirmSwitch(tester, state);
      state.pieceTypes[0] = 'ثوب';
      await tester.runAsync(() async => await state.loadMeasurementPreview());
      await tester.runAsync(() async {
        delayed.complete(jsonResponse(measurements('99')));
        await oldLoad;
      });
      expect(state.currentCustomerId, 2);
      expect(state.measurementValuesByType['piece_0']['الطول'], '20');
    }, backend: backend);
  });

  testWidgets('late search cannot replace the newer selected customer', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      final delayed = Completer<http.Response>();
      backend.nextSearch = delayed;
      late Future<dynamic> oldSearch;
      await tester.runAsync(() async {
        oldSearch = state.searchCustomerByTerm('A');
        await Future<void>.delayed(Duration.zero);
      });
      await select(tester, state, 'B');
      await tester.runAsync(() async {
        delayed.complete(jsonResponse([customer(false)]));
        await oldSearch;
      });
      expect(state.currentCustomerId, 2);
      expect(state.phoneController.text, '222');
    }, backend: backend);
  });

  testWidgets('double click during cash lookup sends only one order', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await prepareOrder(tester, state);
      state.paidAmountController.text = '5';
      final cash = Completer<http.Response>();
      backend.cashLookup = cash;
      final Future<void> saving = state.saveOrder();
      expect(state.saveState, SalesSaveState.saving);
      await state.saveOrder();
      await state.searchCustomerByTerm('B');
      expect(state.currentCustomerId, 1);
      cash.complete(jsonResponse({'cashAccountId': 7}));
      await tester.pumpAndSettle(const Duration(milliseconds: 100),
          EnginePhase.sendSemanticsUpdate, const Duration(seconds: 5));
      await saving;
      expect(backend.orders, hasLength(1));
      expect(state.saveState, SalesSaveState.savedLocked);
    }, backend: backend);
  }, timeout: const Timeout(Duration(seconds: 20)));

  testWidgets('success retains order identity and second save sends zero requests', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await prepareOrder(tester, state);
      await save(tester, state);
      expect(state.savedOrderId, 81);
      expect(state.savedOrderNumber, 'ORD-81');
      expect(state.savedCustomerId, 1);
      expect(state.savedRequestReference, backend.orders.single['requestReference']);
      expect(state.saveState, SalesSaveState.savedLocked);
      await state.saveOrder();
      await tester.pumpAndSettle();
      expect(backend.orders, hasLength(1));
      expect(find.textContaining('الطلب ORD-81 محفوظ ومقفل'), findsOneWidget);
      expect(find.textContaining('هذا الطلب محفوظ بالفعل برقم ORD-81'), findsOneWidget);
    }, backend: backend);
  });

  testWidgets('saved order blocks customer and piece editing and new order resets it', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await prepareOrder(tester, state);
      await save(tester, state);
      await tester.pumpAndSettle();
      await state.searchCustomerByTerm('B');
      expect(state.currentCustomerId, 1);
      final fields = tester.widget<AbsorbPointer>(find.byKey(const ValueKey('sales-draft-fields')));
      expect(fields.absorbing, isTrue);
      final add = tester.widget<FilledButton>(find.byKey(const ValueKey('sales-add-piece')));
      expect(add.onPressed, isNull);
      await tester.tap(find.text('طلب جديد').first);
      await tester.pumpAndSettle();
      expect(state.saveState, SalesSaveState.draft);
      expect(state.savedOrderId, isNull);
      expect(state.savedOrderNumber, isNull);
      expect(state.savedCustomerId, isNull);
      expect(state.savedRequestReference, isNull);
      expect(state.currentCustomerId, 0);
      expect(state.pieceTypes, ['']);
        expect(state.measurementValuesByType.values.every(
          (Map<String, String> values) => values.isEmpty), isTrue);
      expect(state.phoneController.text, isEmpty);
      expect(state.totalAmountController.text, '0.00');
      expect(backend.orders, hasLength(1));
    }, backend: backend);
  });

  testWidgets('reopening saved screen starts clean and cannot create it again', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await prepareOrder(tester, state);
      await save(tester, state);
      final reopened = await reopen(tester);
      expect(reopened.currentCustomerId, 0);
      expect(reopened.pieceTypes, ['']);
      expect(reopened.saveState, SalesSaveState.draft);
      await reopened.saveOrder();
      expect(backend.orders, hasLength(1));
    }, backend: backend);
  });

  for (final lostResponse in [false, true]) {
    testWidgets('retry retains persisted reference and exact payload lostResponse=$lostResponse', (tester) async {
      final backend = SalesBackend()..failNextOrder = true..loseResponse = lostResponse;
      await withSalesScreen(tester, (state) async {
        await prepareOrder(tester, state);
        await save(tester, state);
        expect(state.saveState, SalesSaveState.draft);
        final stored = await tester.runAsync(() => const FlutterSecureStorage().read(key: sessionKey));
        expect(jsonDecode(stored!)['requestReference'], backend.orders.single['requestReference']);
        final reopened = await reopen(tester);
        await save(tester, reopened);
        expect(backend.orders, hasLength(2));
        expect(backend.orders[1], backend.orders[0]);
        expect(reopened.saveState, SalesSaveState.savedLocked);
      }, backend: backend);
    });
  }

  testWidgets('explicit new order uses a different reference', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await prepareOrder(tester, state);
      await save(tester, state);
      await tester.pumpAndSettle();
      await tester.tap(find.text('طلب جديد').first);
      await tester.pumpAndSettle();
      await prepareOrder(tester, state);
      await save(tester, state);
      expect(backend.orders, hasLength(2));
      expect(backend.orders[1]['requestReference'], isNot(backend.orders[0]['requestReference']));
    }, backend: backend);
  });

  testWidgets('deleting piece rebinds measurements catalog and all parallel controllers', (tester) async {
    await withSalesScreen(tester, (state) async {
      await select(tester, state, 'A');
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const ValueKey('sales-add-piece')));
      await tester.tap(find.byKey(const ValueKey('sales-add-piece')));
      await tester.pumpAndSettle();
      state.measurementValuesByType['piece_0'] = <String, String>{'الطول': '10'};
      state.measurementValuesByType['piece_1'] = <String, String>{'الطول': '20'};
      state.catalogNumberControllers[0].text = 'first';
      state.catalogNumberControllers[1].text = 'second';
      state.availableInchesControllers[1].text = '200';
      state.notes1Controllers[1].text = 'second note';
      await tester.ensureVisible(find.byKey(const ValueKey('sales-remove-piece-0')));
      await tester.tap(find.byKey(const ValueKey('sales-remove-piece-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('حذف'));
      await tester.pumpAndSettle();
      expect(state.piecesCount, 1);
      expect(state.measurementValuesByType, {'piece_0': {'الطول': '20'}});
      expect(state.catalogNumberControllers.single.text, 'second');
      expect(state.availableInchesControllers.single.text, '200');
      expect(state.notes1Controllers.single.text, 'second note');
    });
  });
}

const sessionKey = 'lumar_sales_active_session_v1';

Future<void> save(WidgetTester tester, dynamic state) async {
  final Future<void> saving = state.saveOrder();
  await tester.pumpAndSettle(const Duration(milliseconds: 100),
      EnginePhase.sendSemanticsUpdate, const Duration(seconds: 5));
  await saving;
}

Future<void> select(WidgetTester tester, dynamic state, String term) async {
  await tester.runAsync(() async => await state.searchCustomerByTerm(term));
  await tester.pumpAndSettle();
}

Future<void> prepareOrder(WidgetTester tester, dynamic state) async {
  await select(tester, state, 'A');
  state.pieceTypes[0] = 'ثوب';
  state.fabricCodeControllers[0].text = 'FAB';
  await tester.runAsync(() async => await state.loadMeasurementPreview());
  await tester.pumpAndSettle();
}

Future<void> confirmSwitch(WidgetTester tester, dynamic state) async {
  late Future<dynamic> selection;
  await tester.runAsync(() async {
    selection = state.searchCustomerByTerm('B');
    await Future<void>.delayed(Duration.zero);
  });
  await tester.pumpAndSettle();
  expect(find.text('تغيير العميل'), findsOneWidget);
  await tester.tap(find.text('متابعة'));
  await tester.pumpAndSettle();
  await tester.runAsync(() async => await selection);
}

Future<dynamic> reopen(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox.shrink());
  await tester.runAsync(() async => await Future<void>.delayed(Duration.zero));
  await tester.pumpWidget(const MaterialApp(home: SalesScreen()));
  await tester.pumpAndSettle();
  return tester.state(find.byType(SalesScreen));
}

Map<String, dynamic> customer(bool second) => {
  'customerId': second ? 2 : 1,
  'customerCode': second ? 'B' : 'A',
  'customerName': second ? 'Customer B' : 'Customer A',
  'phoneNumber': second ? '222' : '111',
};

List<Map<String, dynamic>> measurements(String value) => [
  {'pieceType': 'ثوب', 'measurementName': 'الطول', 'measurementValue': value},
];

http.Response jsonResponse(Object data, [int status = 200]) =>
  http.Response.bytes(utf8.encode(jsonEncode(data)), status,
        headers: {'content-type': 'application/json; charset=utf-8'});

class SalesBackend {
  final orders = <Map<String, dynamic>>[];
  final requests = <String>[];
  Completer<http.Response>? nextMeasurement;
  Completer<http.Response>? nextSearch;
  Completer<http.Response>? cashLookup;
  bool failNextOrder = false;
  bool loseResponse = false;

  Future<http.Response> respond(http.Request request) async {
    final path = request.url.path;
    requests.add('${request.method} $path');
    if (path == '/orders') {
      orders.add(jsonDecode(request.body) as Map<String, dynamic>);
      if (failNextOrder) {
        failNextOrder = false;
        if (loseResponse) throw http.ClientException('Response lost');
        return jsonResponse({'message': 'تعذر الحفظ'}, 503);
      }
      return jsonResponse({'orderId': 81, 'orderNumber': 'ORD-81'}, 201);
    }
    if (path == '/customers/search') {
      final delayed = nextSearch;
      nextSearch = null;
      if (delayed != null) return delayed.future;
      return jsonResponse([customer(request.url.queryParameters['term'] == 'B')]);
    }
    if (path.endsWith('/measurements')) {
      final delayed = nextMeasurement;
      nextMeasurement = null;
      if (delayed != null) return delayed.future;
      return jsonResponse(measurements(path.contains('/2/') ? '20' : '10'));
    }
    if (path == '/consumption-rules/evaluate') {
      return jsonResponse({'value': 36, 'unit': 'Inch'});
    }
    if (path == '/pricing-engine/calculate') {
      return jsonResponse({'isReady': true, 'inchPrice': 1, 'finalPriceTotal': 36});
    }
    if (path == '/settings/general-cash-account') {
      return cashLookup?.future ?? Future.value(jsonResponse({'cashAccountId': 7}));
    }
    if (path == '/finance/cash-accounts') {
      return jsonResponse([{'cashAccountId': 7, 'accountName': 'صندوق',
        'isActive': true, 'isReceiptEnabled': true, 'currencyCode': 'YER',
        'derivedBalance': 0, 'historicalSnapshotBalance': 0,
        'createdAt': '2026-01-01T00:00:00Z'}]);
    }
    return jsonResponse({
      'schemaVersion': 1, 'version': 'test',
      'productTypes': [{'productTypeId': 10, 'code': 'THOBE', 'nameAr': 'ثوب', 'isActive': true}],
      'measurementFields': [{'productTypeId': 10, 'code': 'length', 'nameAr': 'الطول'}],
    });
  }
}

Future<void> withSalesScreen(
  WidgetTester tester,
  Future<void> Function(dynamic state) body,
  {SalesBackend? backend}
) async {
  tester.view.physicalSize = const Size(1600, 1200);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final client = MockClient((backend ?? SalesBackend()).respond);
  await http.runWithClient(() async {
    await tester.pumpWidget(const MaterialApp(home: SalesScreen()));
    await tester.pumpAndSettle();
    final dynamic state = tester.state(find.byType(SalesScreen));
    try {
      await body(state);
    } finally {
      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pumpAndSettle();
    }
  }, () => client);
}