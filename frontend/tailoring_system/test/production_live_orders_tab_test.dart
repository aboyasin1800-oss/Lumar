import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/production_screen.dart';

class _FakeLiveOrdersApi extends ProductionApi {
  @override
  Future<ProductionData> load() async => ProductionData(
        dashboard: {},
        pieces: [],
        orders: [
          {
            'orderId': 101,
            'orderNumber': 'ORD-1001',
            'customerName': 'مؤسسة النور',
            'phoneNumber': '0500000001',
            'orderDate': '2026-09-13T00:00:00Z',
            'deliveryDate': '2026-09-15T00:00:00Z',
            'orderStatus': 'New',
            'totalAmount': 1200.0,
            'remainingAmount': 500.0,
          },
          {
            'orderId': 102,
            'orderNumber': 'ORD-1002',
            'customerName': 'متجر الرياض',
            'phoneNumber': '0500000002',
            'orderDate': '2026-09-12T00:00:00Z',
            'deliveryDate': '2026-09-20T00:00:00Z',
            'orderStatus': 'ReadyForDelivery',
            'totalAmount': 900.0,
            'remainingAmount': 0.0,
          },
        ],
        orderItems: [
          {'orderId': 101, 'pieceType': 'قميص', 'quantity': 4},
          {'orderId': 101, 'pieceType': 'كوت', 'quantity': 2},
          {'orderId': 101, 'pieceType': 'ثوب', 'quantity': 1},
          {'orderId': 102, 'pieceType': 'قميص', 'quantity': 2},
          {'orderId': 102, 'pieceType': 'كوت', 'quantity': 1},
        ],
        readyOrders: [],
        readyInventory: [],
        wages: [],
        scanners: [],
        scans: [],
        deliveries: [],
      );
}

void main() {
  testWidgets('production screen shows live orders from API orders list', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ProductionScreen(api: _FakeLiveOrdersApi()),
        ),
      ),
    );

    await tester.pumpAndSettle();

    final richTexts = tester.widgetList<RichText>(find.byType(RichText));
    final combined = richTexts
        .map((widget) => widget.text.toPlainText())
        .join(' ');

    expect(combined, contains('ORD-1001'));
    expect(combined, contains('ORD-1002'));
    expect(combined, contains('مؤسسة النور'));
    expect(combined, contains('متجر الرياض'));
    expect(combined, contains('4 قميص'));
    expect(combined, contains('2 كوت'));
    expect(combined, contains('1 ثوب'));
    expect(combined, contains('قيد الإنتاج'));
  });

  test('first started production piece moves order to in production and keeps red warning for unstarted new pieces', () {
    final pieces = [
      {'pieceStatus': 'New'},
      {'pieceStatus': 'Printing'},
    ];

    final status = resolveProductionOrderStatusFromPieces(pieces);
    final unstarted = countUnstartedPiecesForProductionWarning(pieces);

    expect(status, 'قيد الإنتاج');
    expect(unstarted, 1);
    expect(buildProductionWarningText(unstarted), contains('هناك 1 قطع لم تدخل خط الإنتاج'));
  });

  test('all new pieces are classified as new orders only', () {
    final metrics = calculateProductionOrderMetrics([
      {'pieceStatus': 'New', 'productTypeId': 4},
      {'pieceStatus': 'OrderCreated', 'productTypeId': 4},
    ], routesByProductTypeId: {
      4: ['Printing', 'Assembly'],
    });

    expect(metrics.status, 'جديد');
    expect(metrics.notStartedPieces, 2);
    expect(metrics.startedPieces, 0);
  });

  test('one started piece in a five-piece order is in production with four unstarted', () {
    final metrics = calculateProductionOrderMetrics([
      {'pieceStatus': 'Printing', 'productTypeId': 4},
      {'pieceStatus': 'New', 'productTypeId': 4},
      {'pieceStatus': 'New', 'productTypeId': 4},
      {'pieceStatus': 'New', 'productTypeId': 4},
      {'pieceStatus': 'New', 'productTypeId': 4},
    ], routesByProductTypeId: {
      4: ['Printing', 'Assembly'],
    });

    expect(metrics.status, 'قيد الإنتاج');
    expect(metrics.startedPieces, 1);
    expect(metrics.notStartedPieces, 4);
  });

  test('all started incomplete pieces have no unstarted warning', () {
    final metrics = calculateProductionOrderMetrics([
      {'pieceStatus': 'Printing', 'productTypeId': 4},
      {'pieceStatus': 'Cutting', 'productTypeId': 4},
    ], routesByProductTypeId: {
      4: ['Printing', 'Assembly'],
    });

    expect(metrics.status, 'قيد الإنتاج');
    expect(metrics.notStartedPieces, 0);
  });

  test('a piece at the configured final stage is ready for delivery', () {
    final metrics = calculateProductionOrderMetrics([
      {'pieceStatus': 'Assembly', 'productTypeId': 4},
    ], routesByProductTypeId: {
      4: ['Printing', 'Assembly'],
    });

    expect(metrics.status, 'جاهز للتسليم');
    expect(metrics.completedPieces, 1);
  });

  test('partially completed multi-piece order remains in production', () {
    final metrics = calculateProductionOrderMetrics([
      {'pieceStatus': 'Assembly', 'productTypeId': 4},
      {'pieceStatus': 'Sewing', 'productTypeId': 4},
    ], routesByProductTypeId: {
      4: ['Printing', 'Assembly'],
    });

    expect(metrics.status, 'قيد الإنتاج');
    expect(metrics.completedPieces, 1);
  });

  test('statistics data cannot change order classification', () {
    final metrics = calculateProductionOrderMetrics([
      {'pieceStatus': 'Assembly', 'productTypeId': 4},
    ], routesByProductTypeId: {
      4: ['Printing', 'Assembly'],
    });
    final dashboard = {'totalPieces': 999, 'inProductionPieces': 999};

    expect(dashboard['totalPieces'], isNot(metrics.totalPieces));
    expect(metrics.status, 'جاهز للتسليم');
  });

  testWidgets('production screen includes total pieces tab', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ProductionScreen(api: _FakeLiveOrdersApi()),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('إجمالي عدد القطع'), findsOneWidget);
  });
}
