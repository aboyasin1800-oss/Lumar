class SupplierPurchasingSupplier {
  const SupplierPurchasingSupplier(
      {required this.id,
      required this.name,
      required this.code,
      this.phone,
      this.email});
  factory SupplierPurchasingSupplier.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingSupplier(
          id: (json['supplierId'] as num).toInt(),
          name: (json['supplierName'] ?? '').toString(),
          code: (json['supplierCode'] ?? '').toString(),
          phone: json['phone']?.toString(),
          email: json['email']?.toString());
  final int id;
  final String name;
  final String code;
  final String? phone;
  final String? email;
}

class SupplierPurchasingCashAccount {
  const SupplierPurchasingCashAccount(
      {required this.id,
      required this.name,
      required this.balance,
      required this.isActive,
      required this.currency});
  factory SupplierPurchasingCashAccount.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingCashAccount(
          id: (json['cashAccountId'] as num).toInt(),
          name: (json['accountName'] ?? '').toString(),
          balance: ((json['derivedBalance'] ?? 0) as num).toDouble(),
          isActive: json['isActive'] == true,
          currency: (json['currencyCode'] ?? 'YER').toString());
  final int id;
  final String name;
  final double balance;
  final bool isActive;
  final String currency;
}

class SupplierPurchasingInventoryItem {
  const SupplierPurchasingInventoryItem(
      {required this.id,
      required this.code,
      required this.name,
      required this.category,
    this.fabricCategory,
      required this.unit,
      required this.isActive});
  factory SupplierPurchasingInventoryItem.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingInventoryItem(
          id: (json['inventoryItemId'] as num).toInt(),
          code: (json['itemCode'] ?? '').toString(),
          name: (json['itemName'] ?? '').toString(),
          category: (json['category'] ?? '').toString(),
          fabricCategory: json['fabricCategory']?.toString(),
          unit: (json['unit'] ?? '').toString(),
          isActive: json['isActive'] == true);
  final int id;
  final String code;
  final String name;
  final String category;
    final String? fabricCategory;
  final String unit;
  final bool isActive;
}

class SupplierPurchasingWarehouse {
  const SupplierPurchasingWarehouse(
      {required this.id, required this.code, required this.name});
  factory SupplierPurchasingWarehouse.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingWarehouse(
          id: (json['warehouseId'] as num).toInt(),
          code: (json['warehouseCode'] ?? '').toString(),
          name: (json['warehouseName'] ?? '').toString());
  final int id;
  final String code;
  final String name;
}

class SupplierPurchasingInvoice {
  const SupplierPurchasingInvoice(
      {required this.id,
      required this.supplierId,
      required this.purchaseOrderId,
      required this.number,
      required this.date,
      required this.dueDate,
      required this.total,
      required this.paid,
      required this.status,
      this.notes});
  factory SupplierPurchasingInvoice.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingInvoice(
          id: (json['supplierInvoiceId'] as num).toInt(),
          supplierId: (json['supplierId'] as num).toInt(),
          purchaseOrderId: (json['purchaseOrderId'] as num?)?.toInt(),
          number: (json['invoiceNumber'] ?? '').toString(),
          date: DateTime.parse(json['invoiceDate'] as String),
          dueDate: DateTime.parse(json['dueDate'] as String),
          total: ((json['totalAmount'] ?? 0) as num).toDouble(),
          paid: ((json['amountPaid'] ?? 0) as num).toDouble(),
          status: (json['status'] ?? '').toString(),
          notes: json['notes']?.toString());
  final int id;
  final int supplierId;
  final int? purchaseOrderId;
  final String number;
  final DateTime date;
  final DateTime dueDate;
  final double total;
  final double paid;
  final String status;
  final String? notes;
  double get outstanding => total - paid;
}

class SupplierPurchasingInvoiceLine {
    const SupplierPurchasingInvoiceLine(
            {required this.id,
            required this.invoiceId,
            this.inventoryItemId,
            required this.itemCode,
            required this.itemName,
            required this.itemType,
            this.supplierItemCode,
            required this.quantity,
            required this.unitCost,
            required this.total,
            required this.status,
            this.rollCount});
    factory SupplierPurchasingInvoiceLine.fromJson(Map<String, dynamic> json) =>
            SupplierPurchasingInvoiceLine(
                    id: (json['supplierInvoiceLineId'] as num).toInt(),
                    invoiceId: (json['supplierInvoiceId'] as num).toInt(),
                    inventoryItemId: (json['inventoryItemId'] as num?)?.toInt(),
                    itemCode: json['itemCode']?.toString(),
                    itemName: (json['itemName'] ?? '').toString(),
                    itemType: (json['itemType'] ?? 'Legacy').toString(),
                    supplierItemCode: json['supplierItemCode']?.toString(),
                    quantity: ((json['quantity'] ?? 0) as num).toDouble(),
                    unitCost: ((json['unitCost'] ?? 0) as num).toDouble(),
                    total: ((json['lineTotal'] ?? 0) as num).toDouble(),
                    rollCount: (json['rollCount'] as num?)?.toInt(),
                    status: (json['status'] ?? '').toString());
    final int id;
    final int invoiceId;
    final int? inventoryItemId;
    final String? itemCode;
    final String itemName;
    final String itemType;
    final String? supplierItemCode;
    final double quantity;
    final double unitCost;
    final double total;
    final int? rollCount;
    final String status;
}

class SupplierPurchasingPayment {
  const SupplierPurchasingPayment(
      {required this.id,
      required this.supplierId,
      required this.number,
      required this.date,
      required this.amount,
      this.method,
      this.reference,
      this.notes});
  factory SupplierPurchasingPayment.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingPayment(
          id: (json['supplierPaymentId'] as num).toInt(),
          supplierId: (json['supplierId'] as num).toInt(),
          number: (json['paymentNumber'] ?? '').toString(),
          date: DateTime.parse(json['paymentDate'] as String),
          amount: ((json['amount'] ?? 0) as num).toDouble(),
          method: json['paymentMethod']?.toString(),
          reference: json['referenceNumber']?.toString(),
          notes: json['notes']?.toString());
  final int id;
  final int supplierId;
  final String number;
  final DateTime date;
  final double amount;
  final String? method;
  final String? reference;
  final String? notes;
}

class SupplierPurchasingAllocation {
  const SupplierPurchasingAllocation(
      {required this.id,
      required this.paymentId,
      required this.invoiceId,
      required this.amount,
      required this.date});
  factory SupplierPurchasingAllocation.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingAllocation(
          id: (json['supplierPaymentAllocationId'] as num).toInt(),
          paymentId: (json['supplierPaymentId'] as num).toInt(),
          invoiceId: (json['supplierInvoiceId'] as num).toInt(),
          amount: ((json['allocatedAmount'] ?? 0) as num).toDouble(),
          date: DateTime.parse(json['allocationDate'] as String));
  final int id;
  final int paymentId;
  final int invoiceId;
  final double amount;
  final DateTime date;
}

class SupplierPurchasingOrder {
  const SupplierPurchasingOrder(
      {required this.id,
      required this.number,
      required this.supplierId,
      required this.date,
      required this.total,
      required this.status});
  factory SupplierPurchasingOrder.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingOrder(
          id: (json['purchaseOrderId'] as num).toInt(),
          number: (json['purchaseOrderNumber'] ?? '').toString(),
          supplierId: (json['supplierId'] as num).toInt(),
          date: DateTime.parse(json['orderDate'] as String),
          total: ((json['totalAmount'] ?? 0) as num).toDouble(),
          status: (json['status'] ?? '').toString());
  final int id;
  final String number;
  final int supplierId;
  final DateTime date;
  final double total;
  final String status;
}

class SupplierPurchasingReceipt {
  const SupplierPurchasingReceipt(
      {required this.id,
      required this.supplierId,
      required this.purchaseOrderId,
      required this.number,
      required this.date,
      this.notes});
  factory SupplierPurchasingReceipt.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingReceipt(
          id: (json['goodsReceiptId'] as num).toInt(),
          supplierId: (json['supplierId'] as num).toInt(),
          purchaseOrderId: (json['purchaseOrderId'] as num?)?.toInt(),
          number: (json['receiptNumber'] ?? '').toString(),
          date: DateTime.parse(json['receiptDate'] as String),
          notes: json['notes']?.toString());
  final int id;
  final int supplierId;
  final int? purchaseOrderId;
  final String number;
  final DateTime date;
  final String? notes;
}

class SupplierPurchasingReceiptItem {
  const SupplierPurchasingReceiptItem(
      {required this.id,
      required this.itemName,
      required this.quantity,
      required this.unitCost});
  factory SupplierPurchasingReceiptItem.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingReceiptItem(
          id: (json['goodsReceiptItemId'] as num).toInt(),
          itemName: (json['itemName'] ?? '').toString(),
          quantity: ((json['receivedQuantity'] ?? 0) as num).toDouble(),
          unitCost: ((json['unitCost'] ?? 0) as num).toDouble());
  final int id;
  final String itemName;
  final double quantity;
  final double unitCost;
}

class SupplierPurchasingReceiptMatching {
  const SupplierPurchasingReceiptMatching(
      {required this.receipt, required this.items, required this.differences});
  factory SupplierPurchasingReceiptMatching.fromJson(
          Map<String, dynamic> json) =>
      SupplierPurchasingReceiptMatching(
          receipt: SupplierPurchasingReceipt.fromJson(
              Map<String, dynamic>.from(json['receipt'] as Map)),
          items: (json['items'] as List<dynamic>)
              .map((item) => SupplierPurchasingReceiptItem.fromJson(
                  Map<String, dynamic>.from(item as Map)))
              .toList(),
          differences: ((json['differences'] as List<dynamic>?) ?? const [])
              .map((item) => SupplierPurchasingDifference.fromJson(
                  Map<String, dynamic>.from(item as Map)))
              .toList());
  final SupplierPurchasingReceipt receipt;
  final List<SupplierPurchasingReceiptItem> items;
  final List<SupplierPurchasingDifference> differences;
}

class SupplierPurchasingDifference {
  const SupplierPurchasingDifference(
      {required this.type,
      required this.receiptItemId,
      this.expectedQuantity,
      this.actualQuantity,
      this.expectedUnitCost,
      this.actualUnitCost});
  factory SupplierPurchasingDifference.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingDifference(
          type: (json['differenceType'] ?? '').toString(),
          receiptItemId: (json['goodsReceiptItemId'] as num).toInt(),
          expectedQuantity: (json['expectedQuantity'] as num?)?.toDouble(),
          actualQuantity: (json['actualQuantity'] as num?)?.toDouble(),
          expectedUnitCost: (json['expectedUnitCost'] as num?)?.toDouble(),
          actualUnitCost: (json['actualUnitCost'] as num?)?.toDouble());
  final String type;
  final int receiptItemId;
  final double? expectedQuantity;
  final double? actualQuantity;
  final double? expectedUnitCost;
  final double? actualUnitCost;
}
