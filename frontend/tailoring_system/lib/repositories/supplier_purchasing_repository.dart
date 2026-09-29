import '../core/authenticated_api_client.dart';
import '../models/supplier_purchasing_models.dart';

class SupplierPurchasingRepository {
  SupplierPurchasingRepository(this._api);
  final AuthenticatedApiClient _api;

  Future<List<SupplierPurchasingSupplier>> getSuppliers() async =>
      (await _api.getList('/suppliers'))
          .map((json) => SupplierPurchasingSupplier.fromJson(
              Map<String, dynamic>.from(json as Map)))
          .toList();
  Future<List<SupplierPurchasingInvoice>> getInvoices() async =>
      (await _api.getList('/purchasing/invoices'))
          .map((json) => SupplierPurchasingInvoice.fromJson(
              Map<String, dynamic>.from(json as Map)))
          .toList();
  Future<List<SupplierPurchasingPayment>> getPayments() async =>
      (await _api.getList('/purchasing/payments'))
          .map((json) => SupplierPurchasingPayment.fromJson(
              Map<String, dynamic>.from(json as Map)))
          .toList();
  Future<List<SupplierPurchasingOrder>> getOrders() async =>
      (await _api.getList('/purchasing/orders'))
          .map((json) => SupplierPurchasingOrder.fromJson(
              Map<String, dynamic>.from(json as Map)))
          .toList();
  Future<List<SupplierPurchasingReceipt>> getReceipts() async =>
      (await _api.getList('/purchasing/receipts'))
          .map((json) => SupplierPurchasingReceipt.fromJson(
              Map<String, dynamic>.from(json as Map)))
          .toList();
  Future<SupplierPurchasingReceiptMatching> getReceiptMatching(int id) async =>
      SupplierPurchasingReceiptMatching.fromJson(
          await _api.getObject('/inventory/operations/receipts/$id/matching'));

  Future<void> createInvoice(
      {required int supplierId,
      required String invoiceNumber,
      required DateTime invoiceDate,
      required DateTime dueDate,
      required double amount,
      int? purchaseOrderId}) async {
    await _api.postObject('/purchasing/operations/invoices', {
      'supplierId': supplierId,
      'invoiceNumber': invoiceNumber,
      'invoiceDate': _date(invoiceDate),
      'dueDate': _date(dueDate),
      'amount': amount,
      'sourceOperationId': _operationId(),
      'purchaseOrderId': purchaseOrderId
    });
  }

  Future<void> createPayment(
      {required int supplierId,
      required double amount,
      required DateTime paymentDate,
      required int cashAccountId,
      required int paymentKind,
      required String paymentMethod,
      required String referenceNumber,
      int? supplierInvoiceId}) async {
    await _api.postObject('/purchasing/operations/payments', {
      'supplierId': supplierId,
      'supplierInvoiceId': supplierInvoiceId,
      'amount': amount,
      'paymentDate': _date(paymentDate),
      'cashAccountId': cashAccountId,
      'paymentKind': paymentKind,
      'paymentMethod': paymentMethod,
      'referenceNumber': referenceNumber,
      'sourceOperationId': _operationId()
    });
  }

  Future<void> createAllocation(
      {required int paymentId,
      required int invoiceId,
      required double amount,
      required String referenceNumber}) async {
    await _api.postObject('/purchasing/operations/allocations', {
      'supplierPaymentId': paymentId,
      'supplierInvoiceId': invoiceId,
      'amount': amount,
      'allocationDate': _date(DateTime.now()),
      'referenceNumber': referenceNumber,
      'sourceOperationId': _operationId()
    });
  }

  Future<void> reverseFinancial(
      {required String documentType,
      required int documentId,
      required String reason}) async {
    await _api.postObject('/purchasing/operations/reversals', {
      'documentType': documentType,
      'documentId': documentId,
      'reason': reason,
      'sourceOperationId': _operationId()
    });
  }

  Future<void> createReceipt(
      {required int supplierId,
      int? purchaseOrderId,
      required int warehouseId,
      required String receiptNumber,
      required List<Map<String, dynamic>> items}) async {
    await _api.postObject('/inventory/operations/receipts', {
      'supplierId': supplierId,
      'purchaseOrderId': purchaseOrderId,
      'warehouseId': warehouseId,
      'receiptNumber': receiptNumber,
      'receiptDate': DateTime.now().toUtc().toIso8601String(),
      'sourceOperationId': _operationId(),
      'items': items
    });
  }

  Future<void> reverseReceipt(
      {required int receiptId, required String reason}) async {
    await _api.postObject('/inventory/operations/receipts/reversals', {
      'goodsReceiptId': receiptId,
      'reason': reason,
      'sourceOperationId': _operationId()
    });
  }

  String _date(DateTime value) => value.toIso8601String().split('T').first;
  String _operationId() {
    final now = DateTime.now().toUtc().microsecondsSinceEpoch.toRadixString(16);
    final suffix = DateTime.now()
        .microsecondsSinceEpoch
        .toRadixString(16)
        .padLeft(12, '0')
        .substring(0, 12);
    return '${now.padLeft(32, '0').substring(0, 8)}-0000-4000-8000-$suffix';
  }
}
