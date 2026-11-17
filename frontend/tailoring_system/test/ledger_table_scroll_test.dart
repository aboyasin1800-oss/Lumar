import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/financial/finance_views.dart';

void main() {
  testWidgets('LedgerTable supports vertical and horizontal scrolling',
      (tester) async {
    final rows = List.generate(
      40,
      (index) => [
        'A${index + 1}',
        'الحساب ${index + 1}',
        index.isEven ? 'Asset' : 'Revenue',
        index.isEven ? 'نشط' : 'غير نشط',
      ],
    );

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Center(
            child: SizedBox(
              width: 500,
              height: 260,
              child: LedgerTable(
                key: const ValueKey('ledger-table-test'),
                columns: const [
                  'رمز الحساب',
                  'اسم الحساب',
                  'نوع الحساب',
                  'الحالة',
                ],
                rows: rows,
                empty: 'لا توجد حسابات.',
              ),
            ),
          ),
        ),
      ),
    );

    final scrollViews = tester
        .widgetList<SingleChildScrollView>(find.byType(SingleChildScrollView))
        .toList();

    expect(scrollViews.length, 2);
    expect(scrollViews.first.scrollDirection, Axis.vertical);
    expect(scrollViews.first.controller, isNotNull);
    expect(scrollViews.last.scrollDirection, Axis.horizontal);
    expect(scrollViews.last.controller, isNotNull);

    final verticalScrollTarget = find.byType(SingleChildScrollView).first;
    await tester.drag(verticalScrollTarget, const Offset(0, -250));
    await tester.pump();

    final verticalController =
        tester.widget<SingleChildScrollView>(verticalScrollTarget).controller!;
    expect(verticalController.position.pixels, greaterThan(0));
  });
}
