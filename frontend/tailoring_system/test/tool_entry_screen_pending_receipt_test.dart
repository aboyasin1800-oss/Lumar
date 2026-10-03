import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:tailoring_system/models/pending_receipt_storage.dart';
import 'package:tailoring_system/screens/inventory/tool_entry_screen.dart';

void main() {
  testWidgets('ToolEntryScreen uses official item description and product type from pending receipt storage', (WidgetTester tester) async {
    final pending = PendingReceiptStorage(
      goodsReceiptItemId: 77,
      goodsReceiptId: 12,
      supplierId: 9,
      supplierName: 'مورد اختبار',
      receiptNumber: 'RCPT-900',
      receiptDate: DateTime(2026, 10, 3),
      itemType: 'UsedTool',
      itemDescription: 'خيط ألماني',
      receivedQuantity: 12,
      storedQuantity: 0,
      remainingQuantity: 12,
      unit: 'قطعة',
      unitCost: 15,
      productType: 'خيط إنتاج',
      unitCode: 'بكرة',
      supplierInvoiceLineId: 25,
    );

    await tester.pumpWidget(MaterialApp(home: ToolEntryScreen(pendingReceipt: pending)));

    final nameField = tester.widget<TextFormField>(find.byType(TextFormField).at(0));
    final typeField = tester.widget<TextFormField>(find.byType(TextFormField).at(1));

    expect(nameField.controller?.text, 'خيط ألماني');
    expect(typeField.controller?.text, 'خيط إنتاج');
    expect(find.text('بادئة الأدوات الحالية: AT'), findsOneWidget);
  });
}
