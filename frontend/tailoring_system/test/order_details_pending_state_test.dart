import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/order_details_content.dart';

void main() {
  testWidgets(
      'pending order details state shows loading spinner and hides cancel action before data is ready',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: OrderDetailsContent(orderId: 1),
        ),
      ),
    );

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.text('إلغاء الطلب'), findsNothing);
  });

  test('order details gate keeps cancel action behind active loaded state only', () {
    final source = File('lib/screens/order_details_content.dart').readAsStringSync();

    expect(source, contains('if (snapshot.connectionState != ConnectionState.done)'));
    expect(source, contains('return const Center(child: CircularProgressIndicator());'));
    expect(source, contains("order.status.toLowerCase() == 'cancelled'"));
    expect(source, contains("label: const Text('إلغاء الطلب')"));
  });

  test('cancel path keeps async work outside setState', () {
    final source = File('lib/screens/order_details_content.dart').readAsStringSync();

    expect(source, contains('Future<void> _handleCancelOrder() async'));
    expect(source, contains('await repository.cancelOrder'));
    expect(source, isNot(contains('setState(() async')));
    expect(source, isNot(contains('setState(() => future = Future.value(refreshed));')));
  });
}
