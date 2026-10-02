class PendingReceiptStorage {
  const PendingReceiptStorage({
    required this.goodsReceiptItemId,
    required this.goodsReceiptId,
    required this.supplierId,
    required this.supplierName,
    required this.receiptNumber,
    required this.receiptDate,
    required this.itemType,
    required this.itemDescription,
    required this.receivedQuantity,
    required this.storedQuantity,
    required this.remainingQuantity,
    required this.unit,
    required this.unitCost,
    this.rollCount,
    this.supplierInvoiceLineId,
    this.productType,
    this.unitCode,
    this.itemCount,
    this.receivedItemCount,
  });

  final int goodsReceiptItemId;
  final int goodsReceiptId;
  final int supplierId;
  final String supplierName;
  final String receiptNumber;
  final DateTime receiptDate;
  final String itemType;
  final String itemDescription;
  final double receivedQuantity;
  final double storedQuantity;
  final double remainingQuantity;
  final String unit;
  final double unitCost;
  final int? rollCount;
  final int? supplierInvoiceLineId;
  final String? productType;
  final String? unitCode;
  final double? itemCount;
  final double? receivedItemCount;

  factory PendingReceiptStorage.fromJson(Map<String, dynamic> json) {
    double number(String key) => (json[key] as num?)?.toDouble() ?? 0;
    return PendingReceiptStorage(
      goodsReceiptItemId: (json['goodsReceiptItemId'] as num).toInt(),
      goodsReceiptId: (json['goodsReceiptId'] as num).toInt(),
      supplierId: (json['supplierId'] as num).toInt(),
      supplierName: json['supplierName']?.toString() ?? 'غير محدد',
      receiptNumber: json['receiptNumber']?.toString() ?? '',
      receiptDate: DateTime.tryParse(json['receiptDate']?.toString() ?? '') ?? DateTime.now(),
      itemType: json['itemType']?.toString() ?? '',
      itemDescription: json['itemDescription']?.toString() ?? '',
      receivedQuantity: number('receivedQuantity'),
      storedQuantity: number('storedQuantity'),
      remainingQuantity: number('remainingQuantity'),
      unit: json['unit']?.toString() ?? 'قطعة',
      unitCost: number('unitCost'),
      rollCount: (json['rollCount'] as num?)?.toInt(),
      supplierInvoiceLineId: (json['supplierInvoiceLineId'] as num?)?.toInt(),
      productType: json['productType']?.toString(),
      unitCode: json['unitCode']?.toString(),
      itemCount: (json['itemCount'] as num?)?.toDouble(),
      receivedItemCount: (json['receivedItemCount'] as num?)?.toDouble(),
    );
  }

  static List<PendingReceiptStorage> normalizePendingReceipts(Iterable<PendingReceiptStorage> rows) {
    final byItemId = <int, PendingReceiptStorage>{};

    for (final row in rows) {
      if (row.goodsReceiptItemId <= 0 || row.remainingQuantity <= 0) {
        continue;
      }

      final existing = byItemId[row.goodsReceiptItemId];
      if (existing == null || row.receiptDate.isAfter(existing.receiptDate) || row.remainingQuantity > existing.remainingQuantity) {
        byItemId[row.goodsReceiptItemId] = row;
      }
    }

    final normalized = byItemId.values.toList()
      ..sort((a, b) {
        final dateCompare = a.receiptDate.compareTo(b.receiptDate);
        if (dateCompare != 0) return dateCompare;
        return a.goodsReceiptItemId.compareTo(b.goodsReceiptItemId);
      });

    return normalized;
  }

  String get arabicItemType => switch (itemType) {
        'Fabric' => 'قماش',
        'ImportedProduct' => 'منتج مستورد',
        'UsedTool' => 'أداة',
        _ => 'صنف',
      };
}
