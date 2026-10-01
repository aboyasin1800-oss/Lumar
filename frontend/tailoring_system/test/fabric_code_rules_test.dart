import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/pending_receipt_storage.dart';
import 'package:tailoring_system/screens/inventory/bulk_fabric_entry_screen.dart';

void main() {
  test('fabric code generation honors the active prefix and highest number', () {
    final generated = FabricCodeUtils.nextFabricCode(
      ['FA0027', 'FAB0018', 'FA0100'],
      prefix: 'FAB',
    );

    expect(generated, 'FAB0019');
  });

  test('catalog numbers also honor the configured prefix and keep the sequence', () {
    final generated = FabricCodeUtils.nextCatalogNumber(
      ['CAT0008', 'CAT0011', 'FAB0010'],
      prefix: 'FAB',
    );

    expect(generated, 'FAB0011');
  });

  test('yard price display removes unnecessary decimal zeros only', () {
    expect(FabricCodeUtils.formatYardPriceDisplay(1000), '1000');
    expect(FabricCodeUtils.formatYardPriceDisplay(1000.5), '1000.5');
    expect(FabricCodeUtils.formatYardPriceDisplay(1000.75), '1000.75');
    expect(FabricCodeUtils.formatYardPriceDisplay(1000.50), '1000.5');
  });

  test('pending receipt storage keeps exactly one alert per receipt line and ignores fully stored items', () {
    final pending = [
      PendingReceiptStorage(
        goodsReceiptItemId: 31,
        goodsReceiptId: 10,
        supplierId: 1,
        supplierName: 'المورد أ',
        receiptNumber: '10001',
        receiptDate: DateTime(2026, 9, 30),
        itemType: 'Fabric',
        itemDescription: 'قماش أبيض',
        receivedQuantity: 25,
        storedQuantity: 25,
        remainingQuantity: 0,
        unit: 'ياردة',
        unitCost: 50,
      ),
      PendingReceiptStorage(
        goodsReceiptItemId: 32,
        goodsReceiptId: 10,
        supplierId: 1,
        supplierName: 'المورد أ',
        receiptNumber: '10001',
        receiptDate: DateTime(2026, 9, 30),
        itemType: 'Fabric',
        itemDescription: 'قماش أبيض',
        receivedQuantity: 20,
        storedQuantity: 0,
        remainingQuantity: 20,
        unit: 'ياردة',
        unitCost: 52,
      ),
      PendingReceiptStorage(
        goodsReceiptItemId: 32,
        goodsReceiptId: 10,
        supplierId: 1,
        supplierName: 'المورد أ',
        receiptNumber: '10001',
        receiptDate: DateTime(2026, 9, 30, 1),
        itemType: 'Fabric',
        itemDescription: 'قماش أبيض',
        receivedQuantity: 20,
        storedQuantity: 0,
        remainingQuantity: 20,
        unit: 'ياردة',
        unitCost: 52,
      ),
    ];

    final normalized = PendingReceiptStorage.normalizePendingReceipts(pending);

    expect(normalized.length, 1);
    expect(normalized.single.goodsReceiptItemId, 32);
    expect(normalized.single.remainingQuantity, 20);
  });
}
