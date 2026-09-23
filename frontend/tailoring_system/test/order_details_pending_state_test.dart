import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/order_details_content.dart';

void main() {
  testWidgets('order cancel action does not pass async callback into setState',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: OrderDetailsContent(orderId: 1),
        ),
      ),
    );

    final buttonFinder = find.text('إلغاء الطلب');
    expect(buttonFinder, findsOneWidget);

    final button = tester.widget<FilledButton>(buttonFinder);
    expect(button.onPressed, isNotNull);
  });

  test('cancel path keeps async work outside setState', () {
    final source = File('lib/screens/order_details_content.dart').readAsStringSync();

    expect(source, contains('Future<void> _handleCancelOrder() async'));
    expect(source, contains('await repository.cancelOrder'));
    expect(source, isNot(contains('setState(() async')));
    expect(source, isNot(contains('setState(() => future = Future.value(refreshed));')));
  });
}
