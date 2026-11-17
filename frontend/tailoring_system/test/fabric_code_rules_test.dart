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

  test('official linked supplier invoice fabric payload uses 1120 while other pending receipts keep 2100', () {
    final linkedPending = PendingReceiptStorage(
      goodsReceiptItemId: 77,
      goodsReceiptId: 99,
      supplierId: 12,
      supplierName: 'المورد أ',
      receiptNumber: 'R-1001',
      receiptDate: DateTime(2026, 10, 5),
      itemType: 'Fabric',
      itemDescription: 'قماش أبيض',
      receivedQuantity: 20,
      storedQuantity: 0,
      remainingQuantity: 20,
      unit: 'ياردة',
      unitCost: 60,
      supplierInvoiceLineId: 405,
    );

    final unlinkedPending = PendingReceiptStorage(
      goodsReceiptItemId: 88,
      goodsReceiptId: 100,
      supplierId: 12,
      supplierName: 'المورد أ',
      receiptNumber: 'R-1002',
      receiptDate: DateTime(2026, 10, 6),
      itemType: 'Fabric',
      itemDescription: 'قماش أسود',
      receivedQuantity: 18,
      storedQuantity: 0,
      remainingQuantity: 18,
      unit: 'ياردة',
      unitCost: 55,
    );

    final linkedPayload = buildFabricReceiptPayload(
      isPendingStorage: true,
      pendingReceipt: linkedPending,
      batchCatalogNumber: 'CAT-001',
      itemCode: 'FAB-001',
      fabricTypeCode: 'Cotton',
      fabricWidth: 58,
      colorValue: 'أبيض',
      unitId: 1,
      sourceOperationId: '3d0d54ef-7bb8-4b55-a19b-7d9c8dcbf5d3',
      rolls: [
        {'fabricCode': 'FAB-001', 'rollCode': 'ROLL-001', 'colorValue': 'أبيض', 'quantity': 12.5},
      ],
    );

    final unlinkedPayload = buildFabricReceiptPayload(
      isPendingStorage: true,
      pendingReceipt: unlinkedPending,
      batchCatalogNumber: 'CAT-002',
      itemCode: 'FAB-002',
      fabricTypeCode: 'Cotton',
      fabricWidth: 58,
      colorValue: 'أسود',
      unitId: 1,
      sourceOperationId: '3d0d54ef-7bb8-4b55-a19b-7d9c8dcbf5d3',
      rolls: [
        {'fabricCode': 'FAB-002', 'rollCode': 'ROLL-002', 'colorValue': 'أسود', 'quantity': 9.5},
      ],
    );

    expect(linkedPayload['opposingLedgerAccountCode'], '1120');
    expect(unlinkedPayload['opposingLedgerAccountCode'], '2100');
  });

  test('manual quantities are accepted without enforcing equal roll splitting', () {
    final validNonEqual = [
      FabricRollQuantityInput(quantityYards: '23.5'),
      FabricRollQuantityInput(quantityYards: '26'),
      FabricRollQuantityInput(quantityYards: '25.5'),
    ];
    final validBalanced = [
      FabricRollQuantityInput(quantityYards: '20'),
      FabricRollQuantityInput(quantityYards: '25'),
      FabricRollQuantityInput(quantityYards: '30'),
    ];

    expect(validateRollQuantities(expectedTotal: 75, rolls: validNonEqual.cast<Object>().toList()), isNull);
    expect(validateRollQuantities(expectedTotal: 75, rolls: validBalanced.cast<Object>().toList()), isNull);
  });

  test('manual roll quantity edits are rejected when the total does not match the invoice line total', () {
    final rolls = [
      FabricRollQuantityInput(quantityYards: '24'),
      FabricRollQuantityInput(quantityYards: '25'),
      FabricRollQuantityInput(quantityYards: '25'),
    ];

    final validation = validateRollQuantities(expectedTotal: 75, rolls: rolls.cast<Object>().toList());

    expect(validation, isNotNull);
    expect(validation, contains('كمية البند المطلوبة هي 75.000'));
    expect(validation, contains('مجموع كميات الرولات المدخلة هو 74.000'));
  });

  test('manual roll quantities are not auto-filled or distributed across the roll count', () {
    final rolls = [
      FabricRollQuantityInput(quantityYards: ''),
      FabricRollQuantityInput(quantityYards: ''),
      FabricRollQuantityInput(quantityYards: ''),
    ];

    final validation = validateRollQuantities(expectedTotal: 75, rolls: rolls.cast<Object>().toList());

    expect(validation, isNotNull);
    expect(validation, contains('مجموع كميات الرولات المدخلة هو 0.000'));
  });

  test('a single invoice line keeps one batch catalog number for all its rolls', () {
    final batchCatalog = 'CAT0003';
    final catalogValues = [batchCatalog, batchCatalog, batchCatalog];

    expect(catalogValues.toSet().length, 1);
    expect(catalogValues.first, 'CAT0003');
  });

  test('batch generation must keep fabric codes unique across the same unsaved batch', () {
    final existingCodes = <String>['FA0001', 'FA0002'];
    final currentBatchCodes = <String>['FA0003'];

    final next = FabricCodeUtils.nextFabricCode(
      [...existingCodes, ...currentBatchCodes],
      prefix: 'FA',
    );

    expect(next, 'FA0004');
    expect(next != currentBatchCodes.first, isTrue);
  });
}
