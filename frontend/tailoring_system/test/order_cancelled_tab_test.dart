import 'package:flutter_test/flutter_test.dart';

import 'package:tailoring_system/screens/orders_screen.dart';

void main() {
  test('cancelled tab includes only cancelled orders', () {
    final orders = [
      const OrderSummary(
        orderId: 151,
        orderNumber: 'ORD-000129',
        customerId: 18,
        orderDate: null,
        deliveryDate: null,
        totalAmount: 7700,
        paidAmount: 5000,
        remainingAmount: 2000,
        status: 'Cancelled',
        urgencyStatus: 'Normal',
      ),
      const OrderSummary(
        orderId: 152,
        orderNumber: 'ORD-000130',
        customerId: 37,
        orderDate: null,
        deliveryDate: null,
        totalAmount: 2350,
        paidAmount: 1350,
        remainingAmount: 1000,
        status: 'Cancelled',
        urgencyStatus: 'Normal',
      ),
      const OrderSummary(
        orderId: 153,
        orderNumber: 'ORD-000131',
        customerId: 41,
        orderDate: null,
        deliveryDate: null,
        totalAmount: 5000,
        paidAmount: 5000,
        remainingAmount: 0,
        status: 'Delivered',
        urgencyStatus: 'Normal',
      ),
    ];

    expect(
        OrdersScreen.orderTabs.any((tab) => tab.status == 'Cancelled'), isTrue);
    final cancelledOrders =
        OrdersScreen.filterOrdersForTab(orders, 'Cancelled');
    expect(cancelledOrders.map((order) => order.orderNumber),
        ['ORD-000129', 'ORD-000130']);
    expect(
        OrdersScreen.filterOrdersForTab(orders, 'Delivered').single.orderNumber,
        'ORD-000131');
  });

  test('delivered tab sorts newest delivery date first and then newest order id', () {
    final orders = [
      OrderSummary(
        orderId: 101,
        orderNumber: 'ORD-000101',
        customerId: 1,
        orderDate: null,
        deliveryDate: DateTime(2024, 9, 1),
        totalAmount: 500,
        paidAmount: 500,
        remainingAmount: 0,
        status: 'Delivered',
        urgencyStatus: 'Normal',
      ),
      OrderSummary(
        orderId: 105,
        orderNumber: 'ORD-000105',
        customerId: 2,
        orderDate: null,
        deliveryDate: DateTime(2024, 9, 10),
        totalAmount: 600,
        paidAmount: 600,
        remainingAmount: 0,
        status: 'Delivered',
        urgencyStatus: 'Normal',
      ),
      OrderSummary(
        orderId: 99,
        orderNumber: 'ORD-000099',
        customerId: 3,
        orderDate: null,
        deliveryDate: DateTime(2024, 9, 5),
        totalAmount: 700,
        paidAmount: 700,
        remainingAmount: 0,
        status: 'Delivered',
        urgencyStatus: 'Normal',
      ),
    ];

    final delivered = OrdersScreen.filterOrdersForTab(orders, 'Delivered');
    expect(
      delivered.map((order) => order.orderNumber),
      ['ORD-000105', 'ORD-000099', 'ORD-000101'],
    );
  });
}
