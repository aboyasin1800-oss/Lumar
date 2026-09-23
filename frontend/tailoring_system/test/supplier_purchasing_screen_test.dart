import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/supplier_purchasing_screen.dart';

void main() {
  testWidgets('supplier purchasing screen renders main tabs and add actions', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: SupplierPurchasingScreen(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('الموردون والمشتريات'), findsOneWidget);
    expect(find.text('الموردون'), findsOneWidget);
    expect(find.text('فواتير المشتريات'), findsOneWidget);
    expect(find.text('دفعات الموردين'), findsOneWidget);
    expect(find.text('التسوية'), findsOneWidget);
    expect(find.text('إضافة مورد'), findsOneWidget);
  });

  testWidgets('payment and invoice dialogs include required review fields', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: SupplierPurchasingScreen(),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('دفعات الموردين'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('إنشاء دفعة جديدة'));
    await tester.pumpAndSettle();

    expect(find.text('الرصيد السابق'), findsOneWidget);
    expect(find.text('الرصيد النهائي'), findsOneWidget);

    await tester.tap(find.text('إلغاء'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('فواتير المشتريات'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('إضافة فاتورة'));
    await tester.pumpAndSettle();

    expect(find.text('عدد البنود'), findsOneWidget);
    expect(find.text('ملاحظة'), findsOneWidget);
  });
}
