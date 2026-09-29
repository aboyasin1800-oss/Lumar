class SupplierPurchasingSupplier {
  const SupplierPurchasingSupplier(
      {required this.id, required this.name, required this.code, this.phone});
  factory SupplierPurchasingSupplier.fromJson(Map<String, dynamic> json) =>
      SupplierPurchasingSupplier(
          id: (json['supplierId'] as num).toInt(),
          name: (json['supplierName'] ?? '').toString(),
          code: (json['supplierCode'] ?? '').toString(),
          phone: json['phone']?.toString());
  final int id;
  final String name;
  final String code;
  final String? phone;
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
      {required this.receipt, required this.items});
  factory SupplierPurchasingReceiptMatching.fromJson(
          Map<String, dynamic> json) =>
      SupplierPurchasingReceiptMatching(
          receipt: SupplierPurchasingReceipt.fromJson(
              Map<String, dynamic>.from(json['receipt'] as Map)),
          items: (json['items'] as List<dynamic>)
              .map((item) => SupplierPurchasingReceiptItem.fromJson(
                  Map<String, dynamic>.from(item as Map)))
              .toList());
  final SupplierPurchasingReceipt receipt;
  final List<SupplierPurchasingReceiptItem> items;
}
