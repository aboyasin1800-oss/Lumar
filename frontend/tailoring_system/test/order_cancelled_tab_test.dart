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
}
