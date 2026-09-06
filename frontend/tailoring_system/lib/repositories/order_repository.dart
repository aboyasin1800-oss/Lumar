import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/order_models.dart';

class OrderRepository {
  OrderRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final http.Client _client;

  Future<List<T>> _list<T>(String path, T Function(OrderJson) fromJson) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(path, response.statusCode);
    }
    return (jsonDecode(response.body) as List)
        .cast<OrderJson>()
        .map(fromJson)
        .toList();
  }

  Future<OrderJson> _object(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(path, response.statusCode);
    }
    return jsonDecode(response.body) as OrderJson;
  }

  Future<OrderDetailsData> getDetails(int orderId) async {
    final order = OrderDetails.fromJson(await _object('/orders/$orderId'));
    final results = await Future.wait([
      _object('/customers/${order.customerId}'),
      _list('/orders/$orderId/items', OrderItem.fromJson),
      _list('/orders/$orderId/pieces', OrderPiece.fromJson),
      _list('/orders/$orderId/fabrics', OrderFabric.fromJson),
      _list('/orders/$orderId/payments', OrderPayment.fromJson),
      _list('/finance/transactions', OrderFinancialTransaction.fromJson),
      _list('/customers/${order.customerId}/ledger', OrderLedgerEntry.fromJson),
    ]);
    final pieces = results[2] as List<OrderPiece>;
    final trackingLists = await Future.wait(pieces.map((piece) => _list(
        '/production/pieces/${piece.id}/tracking',
        OrderTrackingEvent.fromJson)));
    final tracking = trackingLists.expand((items) => items).toList()
      ..sort((left, right) => left.eventTime.compareTo(right.eventTime));
    bool belongsToOrder(String reference) =>
        reference == order.number || reference.startsWith('${order.number}:');
    return OrderDetailsData(
      order: order,
      customer: OrderCustomer.fromJson(results[0] as OrderJson),
      items: results[1] as List<OrderItem>,
      pieces: pieces,
      fabrics: results[3] as List<OrderFabric>,
      payments: results[4] as List<OrderPayment>,
      trackingEvents: tracking,
      financialTransactions: (results[5] as List<OrderFinancialTransaction>)
          .where((item) => belongsToOrder(item.referenceNumber))
          .toList(),
      ledgerEntries: (results[6] as List<OrderLedgerEntry>)
          .where((item) => belongsToOrder(item.referenceNumber))
          .toList(),
    );
  }
}

class OrderApiException implements Exception {
  const OrderApiException(this.path, this.statusCode);
  final String path;
  final int statusCode;
}
