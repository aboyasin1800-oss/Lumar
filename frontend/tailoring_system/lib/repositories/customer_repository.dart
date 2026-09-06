import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/customer_models.dart';

class CustomerRepository {
  CustomerRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final http.Client _client;

  Future<List<T>> _list<T>(
      String path, T Function(CustomerJson) fromJson) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw CustomerApiException(path, response.statusCode);
    }
    return (jsonDecode(response.body) as List)
        .cast<CustomerJson>()
        .map(fromJson)
        .toList();
  }

  Future<CustomerJson> _object(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw CustomerApiException(path, response.statusCode);
    }
    return jsonDecode(response.body) as CustomerJson;
  }

  Future<CustomerJson?> _optionalObject(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode == 404) {
      return null;
    }
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw CustomerApiException(path, response.statusCode);
    }
    return jsonDecode(response.body) as CustomerJson;
  }

  Future<CustomerDetailsData> getDetails(int customerId) async {
    final initial = await Future.wait([
      _object('/customers/$customerId'),
      _optionalObject('/customers/$customerId/loyalty'),
      _optionalObject('/customers/$customerId/referrals'),
      _list('/customers/$customerId/ledger', CustomerLedgerRecord.fromJson),
      _list<CustomerJson>('/orders', (json) => json),
      _list('/finance/transactions', CustomerFinancialTransaction.fromJson),
    ]);

    final orderIds = (initial[4] as List<CustomerJson>)
        .where((json) => json['customerId'] == customerId)
        .map((json) => json['orderId'] as int)
        .toList();
    final orders = await Future.wait(orderIds.map((orderId) async =>
        CustomerOrder.fromJson(await _object('/orders/$orderId'))));
    final paymentLists = await Future.wait(orderIds.map((orderId) =>
        _list('/orders/$orderId/payments', CustomerOrderPayment.fromJson)));
    final payments = paymentLists.expand((items) => items).toList()
      ..sort((left, right) => right.paymentDate.compareTo(left.paymentDate));
    final orderNumbers = orders.map((order) => order.number).toSet();
    final financialTransactions =
        (initial[5] as List<CustomerFinancialTransaction>).where((transaction) {
      return orderNumbers.any((number) =>
          transaction.referenceNumber == number ||
          transaction.referenceNumber.startsWith('$number:'));
    }).toList();

    final loyaltyJson = initial[1] as CustomerJson?;
    final referralJson = initial[2] as CustomerJson?;
    return CustomerDetailsData(
      customer: CustomerDetails.fromJson(initial[0] as CustomerJson),
      loyalty:
          loyaltyJson == null ? null : CustomerLoyalty.fromJson(loyaltyJson),
      referral:
          referralJson == null ? null : CustomerReferral.fromJson(referralJson),
      ledger: initial[3] as List<CustomerLedgerRecord>,
      orders: orders
        ..sort((left, right) => right.orderDate.compareTo(left.orderDate)),
      payments: payments,
      financialTransactions: financialTransactions
        ..sort((left, right) => right.createdAt.compareTo(left.createdAt)),
    );
  }
}

class CustomerApiException implements Exception {
  const CustomerApiException(this.path, this.statusCode);
  final String path;
  final int statusCode;
}
