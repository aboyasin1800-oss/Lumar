import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/order_models.dart';
import 'package:tailoring_system/repositories/order_repository.dart';
import 'package:tailoring_system/screens/delivery/order_delivery_screen.dart';

class _FakeOrderRepository extends OrderRepository {
  _FakeOrderRepository(this.data);

  final OrderDetailsData data;
  int deliveryCalls = 0;
  int waiverCalls = 0;
  int settlementCalls = 0;
  double? lastSettlementAmount;
  double? lastSettlementDiscount;

  @override
  Future<OrderDetailsData> getDetails(int orderId) async => data;

  @override
  Future<OrderDetails> collectCustomerPayment(
    int orderId,
    double amount,
    String referenceNumber, {
    String? paymentMethod,
    String? notes,
  }) async {
    final updatedOrder = OrderDetails(
      id: data.order.id,
      number: data.order.number,
      customerId: data.order.customerId,
      orderDate: data.order.orderDate,
      deliveryDate: data.order.deliveryDate,
      totalAmount: data.order.totalAmount,
      discountAmount: data.order.discountAmount,
      paidAmount: data.order.paidAmount + amount,
      remainingAmount: data.order.remainingAmount - amount,
      urgencyStatus: data.order.urgencyStatus,
      status: data.order.status,
      notes: data.order.notes,
      createdDate: data.order.createdDate,
      updatedDate: data.order.updatedDate,
      cancellationReason: data.order.cancellationReason,
      cancelledAt: data.order.cancelledAt,
      cancelledBy: data.order.cancelledBy,
      saleCategory: data.order.saleCategory,
      revenueRecognized: data.order.revenueRecognized,
      revenueRecognizedAt: data.order.revenueRecognizedAt,
      revenueReversalCreated: data.order.revenueReversalCreated,
      revenueReversalCreatedAt: data.order.revenueReversalCreatedAt,
    );

    return updatedOrder;
  }

  @override
  Future<OrderDetails> settleCustomerBalance(
    int orderId,
    double amount,
    double discountAmount,
    String referenceNumber, {
    String? paymentMethod,
    String? notes,
  }) async {
    settlementCalls++;
    lastSettlementAmount = amount;
    lastSettlementDiscount = discountAmount;
    return data.order;
  }

  @override
  Future<OrderDetails> deliverOrder(int orderId) async {
    deliveryCalls++;
    return data.order;
  }

  @override
  Future<OrderDetails> waiveRemainingBalance(int orderId) async {
    waiverCalls++;
    return data.order;
  }
}

void main() {
  testWidgets('delivery screen renders settlement summary and validates amount',
      (WidgetTester tester) async {
    final order = OrderDetails(
      id: 42,
      number: 'ORD-1001',
      customerId: 7,
      orderDate: DateTime(2026, 9, 1),
      deliveryDate: DateTime(2026, 9, 2),
      totalAmount: 1500,
      discountAmount: 100,
      paidAmount: 500,
      remainingAmount: 900,
      urgencyStatus: 'Normal',
      status: 'Delivered',
      notes: 'طلب تجريبي',
      createdDate: DateTime(2026, 9, 1),
      updatedDate: null,
      cancellationReason: null,
      cancelledAt: null,
      cancelledBy: null,
      saleCategory: 'TailoringOrder',
      revenueRecognized: true,
      revenueRecognizedAt: DateTime(2026, 9, 2),
      revenueReversalCreated: false,
      revenueReversalCreatedAt: null,
    );

    final repository = _FakeOrderRepository(
      OrderDetailsData(
        order: order,
        customer: const OrderCustomer(
          id: 7,
          code: 'C-7',
          name: 'عميل تجريبي',
          phoneNumber: '0500000000',
        ),
        items: const [],
        pieces: const [],
        fabrics: const [],
        payments: const [],
        trackingEvents: const [],
        financialTransactions: const [],
        ledgerEntries: const [],
      ),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: OrderDeliveryScreen(orderId: 42, repository: repository),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('رقم الطلب'), findsOneWidget);
    expect(find.text('ORD-1001'), findsOneWidget);
    expect(find.text('المبلغ المتبقي'), findsOneWidget);
    expect(find.text('المبلغ الذي سيُحصّل'), findsWidgets);
    expect(find.text('900.00'), findsWidgets);
    expect(find.text('تحصيل كامل المتبقي'), findsOneWidget);
    expect(find.text('تحصيل جزئي'), findsOneWidget);
    expect(find.text('تبرع بالرصيد'), findsOneWidget);

    await tester.ensureVisible(find.text('تحصيل كامل المتبقي'));
    await tester.tap(find.text('تحصيل كامل المتبقي'));
    final discountField = find.byType(TextField).at(1);
    await tester.ensureVisible(discountField);
    await tester.enterText(discountField, '100');
    await tester.ensureVisible(find.text('تأكيد التحصيل'));
    await tester.tap(find.text('تأكيد التحصيل'));
    await tester.pumpAndSettle();
    expect(repository.settlementCalls, 1);
    expect(repository.lastSettlementAmount, 800);
    expect(repository.lastSettlementDiscount, 100);

    await tester.ensureVisible(find.text('تبرع بالرصيد'));
    await tester.tap(find.text('تبرع بالرصيد'));
    await tester.pumpAndSettle();
    expect(find.text('تأكيد التبرع'), findsOneWidget);
    await tester.tap(find.text('تأكيد التبرع'));
    await tester.pumpAndSettle();
    expect(repository.waiverCalls, 1);
  });

  testWidgets('ready-for-delivery screen confirms delivery',
      (WidgetTester tester) async {
    final order = OrderDetails(
      id: 77,
      number: 'ORD-2002',
      customerId: 12,
      orderDate: DateTime(2026, 9, 5),
      deliveryDate: DateTime(2026, 9, 6),
      totalAmount: 1800,
      discountAmount: 50,
      paidAmount: 1100,
      remainingAmount: 650,
      urgencyStatus: 'Normal',
      status: 'ReadyForDelivery',
      notes: 'طلب جاهز للتسليم',
      createdDate: DateTime(2026, 9, 5),
      updatedDate: null,
      cancellationReason: null,
      cancelledAt: null,
      cancelledBy: null,
      saleCategory: 'TailoringOrder',
      revenueRecognized: false,
      revenueRecognizedAt: null,
      revenueReversalCreated: false,
      revenueReversalCreatedAt: null,
    );

    final repository = _FakeOrderRepository(
      OrderDetailsData(
        order: order,
        customer: const OrderCustomer(
          id: 12,
          code: 'C-12',
          name: 'عميل جاهز',
          phoneNumber: '0555555555',
        ),
        items: const [],
        pieces: const [],
        fabrics: const [],
        payments: const [],
        trackingEvents: const [],
        financialTransactions: const [],
        ledgerEntries: const [],
      ),
    );

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Builder(
            builder: (context) {
              return Card(
                child: ListTile(
                  onTap: () => showDialog(
                    context: context,
                    builder: (_) => Dialog.fullscreen(
                      child: OrderDeliveryScreen(
                        orderId: 77,
                        repository: repository,
                      ),
                    ),
                  ),
                  title: const Text('الطلب ORD-2002'),
                ),
              );
            },
          ),
        ),
      ),
    );

    await tester.tap(find.byType(ListTile));
    await tester.pumpAndSettle();

    expect(find.byType(Dialog), findsOneWidget);
    expect(find.byType(OrderDeliveryScreen), findsOneWidget);
    expect(find.text('تأكيد التسليم'), findsOneWidget);

    await tester.tap(find.text('تأكيد التسليم'));
    await tester.pumpAndSettle();
    expect(find.byType(AlertDialog), findsOneWidget);
    await tester.tap(find.text('تأكيد التسليم').last);
    await tester.pumpAndSettle();
    expect(repository.deliveryCalls, 1);
  });
}
