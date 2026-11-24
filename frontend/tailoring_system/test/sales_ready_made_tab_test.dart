import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/ready_made_production_screen.dart';
import 'package:tailoring_system/screens/sales_screen.dart';

void salesOrderStatisticsTests() {}

void main() {
  test('sales order statistics use total list and matching operating day', () {
    final result = calculateSalesOrderStatistics([
      {'orderDate': '2026-11-24T08:00:00Z'},
      {'orderDate': '2026-11-24T22:00:00Z'},
      {'orderDate': '2026-11-23T22:00:00Z'},
    ], DateTime(2026, 11, 24));

    expect(result.total, 3);
    expect(result.today, 2);
  });

  testWidgets(
    'sales third tab opens the embedded ready-made order form directly',
    (tester) async {
      await tester.pumpWidget(const MaterialApp(home: SalesScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('أوامر الإنتاج الجاهز'));
      await tester.pump();

      expect(find.byType(ReadyMadeOrderCreateScreen), findsOneWidget);
      expect(find.byType(ReadyMadeProductionScreen), findsNothing);
      expect(find.text('إنشاء أمر إنتاج جاهز'), findsOneWidget);
      expect(find.text('إجمالي أوامر الإنتاج'), findsNothing);
      expect(find.text('إنشاء أمر جديد'), findsNothing);
    },
  );
}
