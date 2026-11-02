import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/screens/order_details_content.dart';
import 'package:tailoring_system/screens/sales_screen.dart';
import 'package:tailoring_system/models/order_models.dart';

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

  testWidgets('SavedEdit without changes performs no API write', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      expect(state.saveState, SalesSaveState.savedEdit);
      await state.saveOrder();
      await tester.pumpAndSettle();
      expect(backend.editWrites, isEmpty);
      expect(backend.requests.where((request) => request.startsWith('POST /orders')), isEmpty);
      expect(backend.orders, isEmpty);
      expect(state.saveState, SalesSaveState.savedLocked);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('safe-edit add payload preserves selected fabric metadata', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await tester.tap(find.byKey(const ValueKey('sales-add-piece')));
      await tester.pumpAndSettle();
      state.pieceTypes[1] = 'ثوب';
      state.fabricCodeControllers[1].text = 'FA0105';
      state.fabricTypeControllers[1].text = 'الصحراء';
      state.fabricColorControllers[1].text = 'كحلي';
      state.catalogNumberControllers[1].text = 'CAT-ADD-1';
      state.measurementValuesByType['piece_1'] = {'الطول': '20'};

      final saving = state.saveOrder();
      await tester.pumpAndSettle();
      await saving;

      final added = backend.editWrites.single['addedPieces'].single;
      expect(added['productTypeId'], 10);
      expect(added['fabricCode'], 'FA0105');
      expect(added['fabricType'], 'الصحراء');
      expect(added['fabricColor'], 'كحلي');
      expect(added['catalogNumber'], 'CAT-ADD-1');
      expect(added['measurements'], {'الطول': '20'});
      expect(backend.orders, isEmpty);
      expect(backend.requests.where((request) => request == 'POST /orders'), isEmpty);
    }, backend: backend, editOrderId: 81);
  });

  test('order item reads full cost from reserved pricing metadata', () {
    final item = OrderItem.fromJson({
      'orderItemId': 1271,
      'orderId': 1579,
      'pieceType': 'قميص',
      'quantity': 1,
      'fabricCode': 'FA0105',
      'fabricType': 'الصحراء',
      'fabricColor': 'كحلي',
      'request1': null,
      'request2': null,
      'specialRequest': null,
      'notes1': null,
      'notes2': null,
      'trackingCode': 'TRK-000060',
      'pieceStatus': 'New',
      'createdDate': '2026-11-01T20:41:42Z',
      'measurementSnapshot': jsonEncode({
        'الطول': '20',
        '_fullCost': 2292.22,
      }),
    });
    expect(item.fullCost, 2292.22);
    expect(item.measurements, {'الطول': '20'});
  });

  testWidgets('adding and cancelling pieces sends typed operations and no DELETE', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await tester.ensureVisible(find.byKey(const ValueKey('sales-add-piece')));
      await tester.tap(find.byKey(const ValueKey('sales-add-piece')));
      await tester.pumpAndSettle();
      state.pieceTypes[1] = 'ثوب';
      state.fabricCodeControllers[1].text = 'FAB';
      state.measurementValuesByType['piece_1'] = {'الطول': '20'};
      await tester.ensureVisible(find.byKey(const ValueKey('sales-remove-piece-0')));
      await tester.tap(find.byKey(const ValueKey('sales-remove-piece-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('حذف'));
      await tester.pumpAndSettle();
      final saving = state.saveOrder();
      await tester.pumpAndSettle();
      await saving;
      final request = backend.editWrites.single;
      expect(request['addedPieces'], hasLength(1));
      expect(request['addedPieces'].single['addOperationId'], isNotEmpty);
      expect(request['addedPieces'].single['productTypeId'], 10);
      expect(request['cancelledPieces'], [
        {'pieceId': 901, 'reason': 'إلغاء القطعة من شاشة تعديل الطلب'}
      ]);
      expect(backend.requests.where((entry) => entry.startsWith('DELETE ')), isEmpty);
      expect(backend.orders, isEmpty);
      expect(state.savedOrderId, 81);
      expect(state.savedOrderNumber, 'ORD-81');
      expect(state.savedCustomerId, 1);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('double submit during edit sends one apply request', (tester) async {
    final response = Completer<http.Response>();
    final backend = SalesBackend()..editResponse = response;
    await withSalesScreen(tester, (state) async {
      state.deliveryDateController.text = '2027-02-02';
      final saving = state.saveOrder();
      expect(state.saveState, SalesSaveState.updating);
      await state.saveOrder();
      await tester.runAsync(() async => await Future<void>.delayed(Duration.zero));
      expect(backend.editWrites, hasLength(1));
      response.complete(jsonResponse(editSnapshot(version: 'version-2')));
      await tester.pumpAndSettle();
      await saving;
      expect(backend.editWrites, hasLength(1));
      expect(backend.orders, isEmpty);
      expect(state.saveState, SalesSaveState.savedLocked);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('HTTP 409 keeps edit open and offers snapshot reload', (tester) async {
    final backend = SalesBackend()..editStatusCode = 409;
    await withSalesScreen(tester, (state) async {
      state.deliveryDateController.text = '2027-02-02';
      final saving = state.saveOrder();
      await tester.pumpAndSettle();
      await saving;
      expect(state.saveState, SalesSaveState.savedEdit);
      expect(find.text('تعارض إصدار الطلب. أعد تحميل اللقطة قبل التعديل.'), findsOneWidget);
      expect(find.text('إعادة تحميل'), findsOneWidget);
      expect(backend.orders, isEmpty);
    }, backend: backend, editOrderId: 81);
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

  testWidgets('saved edit mode preserves the existing order without creating duplicates', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      expect(state.saveState, SalesSaveState.savedEdit);
      expect(state.savedOrderId, 81);
      expect(state.savedOrderNumber, 'ORD-81');
      expect(state.currentCustomerId, 1);
      expect(backend.requests, contains('GET /orders/81/tailoring-edit'));
      state.deliveryDateController.text = '2027-02-02';
      state.notes1Controllers[0].text = 'طلب معدل';
      final saving = state.saveOrder();
      expect(state.saveState, SalesSaveState.updating);
      await tester.pumpAndSettle();
      await saving;
      expect(backend.editWrites, hasLength(1));
      expect(backend.editWrites.single['expectedVersion'], 'version-1');
      expect(backend.editWrites.single['editOperationId'], isNotEmpty);
      expect(backend.requests, contains('POST /orders/81/tailoring-edit'));
      expect(backend.orders, isEmpty);
      expect(backend.editWrites.single.keys, containsAll([
        'editOperationId',
        'expectedVersion',
        'details',
        'changedPieces',
        'addedPieces',
        'cancelledPieces',
      ]));
      expect(backend.editWrites.single['changedPieces'], [
        {
          'pieceId': 901,
          'patch': {'request1': 'طلب معدل'},
        }
      ]);
      expect(state.saveState, SalesSaveState.savedLocked);
      expect(state.savedOrderId, 81);
      expect(state.savedOrderNumber, 'ORD-81');
      expect(state.savedCustomerId, 1);
      expect(find.text('تم تحديث الطلب ORD-81 بنجاح.'), findsOneWidget);
      expect(backend.requests.where((request) => request == 'POST /orders'), isEmpty);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('removing an unsaved draft piece is local only', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await tester.tap(find.byKey(const ValueKey('sales-add-piece')));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const ValueKey('sales-remove-piece-1')));
      await tester.tap(find.byKey(const ValueKey('sales-remove-piece-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('حذف'));
      await tester.pumpAndSettle();
      await state.saveOrder();
      await tester.pumpAndSettle();
      expect(backend.editWrites, isEmpty);
      expect(backend.requests.where((request) => request.startsWith('POST /orders')), isEmpty);
      expect(backend.requests.where((request) => request.startsWith('DELETE ')), isEmpty);
      expect(state.piecesCount, 1);
      expect(state.saveState, SalesSaveState.savedLocked);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('customer and measurements follow snapshot edit capabilities', (tester) async {
    final backend = SalesBackend()
      ..editCanEditDetails = false
      ..editCanEditMeasurements = false
      ..editCanCancel = false;
    await withSalesScreen(tester, (state) async {
      await state.searchCustomerByTerm('B');
      expect(state.currentCustomerId, 1);
        final phoneField = tester.widgetList<TextField>(find.byType(TextField))
          .firstWhere((field) => field.controller == state.phoneController);
      expect(phoneField.readOnly, isTrue);
      final measurementButton = tester.widget<FilledButton>(
          find.byKey(const ValueKey('sales-edit-measurements-0')));
      expect(measurementButton.onPressed, isNull);
      final cancelButton = tester.widget<IconButton>(
          find.byKey(const ValueKey('sales-remove-piece-0')));
      expect(cancelButton.onPressed, isNull);
      final requestField = tester.widgetList<TextField>(find.byType(TextField))
          .firstWhere((field) => field.controller == state.notes1Controllers[0]);
      expect(requestField.readOnly, isTrue);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('retry reuses the same EditOperationId and exact payload', (tester) async {
    final backend = SalesBackend()..failNextEdit = true;
    await withSalesScreen(tester, (state) async {
      state.deliveryDateController.text = '2027-02-02';
      await state.saveOrder();
      expect(state.saveState, SalesSaveState.savedEdit);
      expect(backend.editWrites, hasLength(1));
      final firstPayload = Map<String, dynamic>.from(backend.editWrites.single);
      await state.saveOrder();
      expect(backend.editWrites, hasLength(2));
      expect(backend.editWrites[1], firstPayload);
      expect(backend.editWrites[1]['editOperationId'], firstPayload['editOperationId']);
      expect(state.saveState, SalesSaveState.savedLocked);
      expect(backend.orders, isEmpty);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('reopening an edited order loads SavedEdit and never creates it', (tester) async {
    final backend = SalesBackend();
    await withSalesScreen(tester, (state) async {
      await state.saveOrder();
      expect(state.saveState, SalesSaveState.savedLocked);
      final reopened = await reopen(tester, editOrderId: 81);
      expect(reopened.saveState, SalesSaveState.savedEdit);
      expect(reopened.savedOrderId, 81);
      expect(reopened.savedOrderNumber, 'ORD-81');
      await reopened.saveOrder();
      expect(backend.requests.where((request) => request == 'GET /orders/81/tailoring-edit'), hasLength(2));
      expect(backend.orders, isEmpty);
      expect(backend.editWrites, isEmpty);
    }, backend: backend, editOrderId: 81);
  });

  testWidgets('order details edit button opens the official SavedEdit route', (tester) async {
    final backend = SalesBackend();
    tester.view.physicalSize = const Size(1600, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final client = MockClient(backend.respond);
    await http.runWithClient(() async {
      await tester.pumpWidget(
          const MaterialApp(home: OrderDetailsContent(orderId: 81)));
      await tester.pumpAndSettle();
      expect(find.text('تعديل الطلب'), findsOneWidget);
      await tester.tap(find.text('تعديل الطلب'));
      await tester.pumpAndSettle();
      expect(find.byType(SalesScreen), findsOneWidget);
      final dynamic state = tester.state(find.byType(SalesScreen));
      expect(state.saveState, SalesSaveState.savedEdit);
      expect(state.savedOrderId, 81);
      expect(state.savedOrderNumber, 'ORD-81');
      expect(backend.requests, contains('GET /orders/81/tailoring-edit'));
      expect(backend.orders, isEmpty);
      expect(backend.requests.where((request) => request == 'POST /orders'), isEmpty);
      await tester.pumpWidget(const SizedBox.shrink());
    }, () => client);
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

Future<dynamic> reopen(WidgetTester tester, {int? editOrderId}) async {
  await tester.pumpWidget(const SizedBox.shrink());
  await tester.runAsync(() async => await Future<void>.delayed(Duration.zero));
  await tester.pumpWidget(MaterialApp(home: SalesScreen(editOrderId: editOrderId)));
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
  int editStatusCode = 200;
  Completer<http.Response>? editResponse;
  bool failNextEdit = false;
  bool editCanEditDetails = true;
  bool editCanEditMeasurements = true;
  bool editCanCancel = true;
  final editWrites = <Map<String, dynamic>>[];

  Future<http.Response> respond(http.Request request) async {
    final path = request.url.path;
    requests.add('${request.method} $path');
    if (path == '/orders/81' && request.method == 'GET') {
      return jsonResponse({
        'orderId': 81,
        'orderNumber': 'ORD-81',
        'customerId': 1,
        'orderDate': '2027-01-01T00:00:00Z',
        'deliveryDate': '2027-01-10T00:00:00Z',
        'totalAmount': 36.0,
        'discountAmount': 0.0,
        'paidAmount': 5.0,
        'remainingAmount': 31.0,
        'urgencyStatus': 'Normal',
        'orderStatus': 'New',
        'notes': null,
        'createdDate': '2027-01-01T00:00:00Z',
        'updatedDate': null,
        'cancellationReason': null,
        'cancelledAt': null,
        'cancelledBy': null,
        'saleCategory': 'TailoringOrder',
        'revenueRecognized': false,
        'revenueRecognizedAt': null,
        'revenueReversalCreated': false,
        'revenueReversalCreatedAt': null,
      });
    }
    if (path == '/customers/1' && request.method == 'GET') {
      return jsonResponse({
        'customerId': 1,
        'customerCode': 'A',
        'customerName': 'Customer A',
        'phoneNumber': '111',
      });
    }
    if (path == '/orders/81/items' ||
        path == '/orders/81/pieces' ||
        path == '/orders/81/fabrics' ||
        path == '/orders/81/payments' ||
        path == '/finance/transactions' ||
        path == '/customers/1/ledger') {
      return jsonResponse(const []);
    }
    if (path == '/orders/81/tailoring-edit') {
      if (request.method == 'GET') {
        return jsonResponse(editSnapshot(
          canEditDetails: editCanEditDetails,
          canEditMeasurements: editCanEditMeasurements,
          canCancel: editCanCancel,
        ));
      }
      editWrites.add(jsonDecode(request.body) as Map<String, dynamic>);
      if (failNextEdit) {
        failNextEdit = false;
        throw http.ClientException('Response lost');
      }
      final delayed = editResponse;
      editResponse = null;
      if (delayed != null) return delayed.future;
      if (editStatusCode != 200) {
        return jsonResponse({'message': 'Version conflict'}, editStatusCode);
      }
      return jsonResponse(editSnapshot(version: 'version-2'));
    }
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

Map<String, dynamic> editSnapshot({
  String version = 'version-1',
  bool canEditDetails = true,
  bool canEditMeasurements = true,
  bool canCancel = true,
}) => {
  'order': {
    'orderId': 81,
    'orderNumber': 'ORD-81',
    'customerId': 1,
    'customerName': 'Customer A',
    'phoneNumber': '111',
    'deliveryDate': '2027-01-01T00:00:00Z',
    'totalAmount': 36.0,
    'discountAmount': 0.0,
    'paidAmount': 5.0,
    'remainingAmount': 31.0,
  },
  'version': version,
  'canEdit': true,
  'canAddPiece': true,
  'pieces': [
    {
      'pieceId': 901,
      'orderItemId': 801,
      'productTypeId': 10,
      'pieceType': 'ثوب',
      'pieceStatus': 'New',
      'fabricCode': 'FAB',
      'fabricType': 'قطن',
      'fabricColor': 'أبيض',
      'catalogNumber': null,
      'request1': '',
      'request2': '',
      'specialRequest': '',
      'notes1': '',
      'notes2': '',
      'measurements': {'الطول': '20'},
      'canEditDetails': canEditDetails,
      'canEditMeasurements': canEditMeasurements,
      'canCancel': canCancel,
    },
  ],
  'addedPieceIds': <String, int>{},
};

Future<void> withSalesScreen(
  WidgetTester tester,
  Future<void> Function(dynamic state) body,
  {SalesBackend? backend, int? editOrderId}
) async {
  tester.view.physicalSize = const Size(1600, 1200);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final client = MockClient((backend ?? SalesBackend()).respond);
  await http.runWithClient(() async {
    await tester.pumpWidget(MaterialApp(home: SalesScreen(editOrderId: editOrderId)));
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