import 'dart:convert';

import 'package:http/http.dart' as http;

class SupplierRepository {
  SupplierRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<List<SupplierListItem>> getSuppliers() async {
    final response = await _client.get(Uri.parse('$_baseUrl/suppliers'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierApiException('/suppliers', response.statusCode);
    }

    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((json) => SupplierListItem.fromJson(
            Map<String, dynamic>.from(json as Map<dynamic, dynamic>)))
        .toList();
  }

  Future<List<SupplierInvoiceItem>> getInvoices() async {
    final response = await _client.get(Uri.parse('$_baseUrl/finance/supplier-invoices'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierApiException('/finance/supplier-invoices', response.statusCode);
    }

    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((json) => SupplierInvoiceItem.fromJson(
            Map<String, dynamic>.from(json as Map<dynamic, dynamic>)))
        .toList();
  }

  Future<List<SupplierPaymentItem>> getPayments() async {
    final response = await _client.get(Uri.parse('$_baseUrl/finance/supplier-payments'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw SupplierApiException('/finance/supplier-payments', response.statusCode);
    }

    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((json) => SupplierPaymentItem.fromJson(
            Map<String, dynamic>.from(json as Map<dynamic, dynamic>)))
        .toList();
  }
}

class SupplierListItem {
  const SupplierListItem({
    required this.id,
    required this.code,
    required this.name,
    required this.phone,
    required this.email,
    required this.isActive,
  });

  factory SupplierListItem.fromJson(Map<String, dynamic> json) => SupplierListItem(
        id: (json['supplierId'] as num).toInt(),
        code: (json['supplierCode'] ?? '').toString(),
        name: (json['supplierName'] ?? '').toString(),
        phone: json['phone']?.toString(),
        email: json['email']?.toString(),
        isActive: json['isActive'] as bool? ?? true,
      );

  final int id;
  final String code;
  final String name;
  final String? phone;
  final String? email;
  final bool isActive;
}

class SupplierInvoiceItem {
  const SupplierInvoiceItem({
    required this.id,
    required this.supplierId,
    required this.invoiceNumber,
    required this.invoiceDate,
    required this.totalAmount,
    required this.amountPaid,
    required this.status,
    required this.notes,
  });

  factory SupplierInvoiceItem.fromJson(Map<String, dynamic> json) => SupplierInvoiceItem(
        id: (json['supplierInvoiceId'] as num).toInt(),
        supplierId: (json['supplierId'] as num).toInt(),
        invoiceNumber: (json['invoiceNumber'] ?? '').toString(),
        invoiceDate: DateTime.parse(json['invoiceDate'] as String),
        totalAmount: ((json['totalAmount'] ?? 0) as num).toDouble(),
        amountPaid: ((json['amountPaid'] ?? 0) as num).toDouble(),
        status: (json['status'] ?? '').toString(),
        notes: json['notes']?.toString(),
      );

  final int id;
  final int supplierId;
  final String invoiceNumber;
  final DateTime invoiceDate;
  final double totalAmount;
  final double amountPaid;
  final String status;
  final String? notes;
}

class SupplierPaymentItem {
  const SupplierPaymentItem({
    required this.id,
    required this.supplierId,
    required this.paymentNumber,
    required this.paymentDate,
    required this.amount,
    required this.paymentMethod,
    required this.referenceNumber,
    required this.notes,
    required this.finalBalance,
  });

  factory SupplierPaymentItem.fromJson(Map<String, dynamic> json) => SupplierPaymentItem(
        id: (json['supplierPaymentId'] as num).toInt(),
        supplierId: (json['supplierId'] as num).toInt(),
        paymentNumber: (json['paymentNumber'] ?? '').toString(),
        paymentDate: DateTime.parse(json['paymentDate'] as String),
        amount: ((json['amount'] ?? 0) as num).toDouble(),
        paymentMethod: json['paymentMethod']?.toString(),
        referenceNumber: json['referenceNumber']?.toString(),
        notes: json['notes']?.toString(),
        finalBalance: ((json['amount'] ?? 0) as num).toDouble(),
      );

  final int id;
  final int supplierId;
  final String paymentNumber;
  final DateTime paymentDate;
  final double amount;
  final String? paymentMethod;
  final String? referenceNumber;
  final String? notes;
  final double finalBalance;
}

class SupplierApiException implements Exception {
  const SupplierApiException(this.path, this.statusCode);

  final String path;
  final int statusCode;

  @override
  String toString() => 'SupplierApiException(path: $path, statusCode: $statusCode)';
}
