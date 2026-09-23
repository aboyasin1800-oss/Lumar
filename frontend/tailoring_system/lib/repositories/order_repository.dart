import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/order_models.dart';

class OrderRepository {
  OrderRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final http.Client _client;

  http.Client get client => _client;

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

  Future<OrderDetails> collectCustomerPayment(
    int orderId,
    double amount,
    String referenceNumber, {
    String? paymentMethod,
    String? notes,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/orders/$orderId/collect'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'amount': amount,
        'paymentMethod': paymentMethod ?? 'Cash',
        'referenceNumber': referenceNumber,
        'notes': notes ?? 'تحصيل من شاشة التسوية',
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException('/orders/$orderId/collect', response.statusCode);
    }
    return OrderDetails.fromJson(jsonDecode(response.body) as OrderJson);
  }

  Future<OrderDetails> settleCustomerBalance(
    int orderId,
    double amount,
    double discountAmount,
    String referenceNumber, {
    String? paymentMethod,
    String? notes,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/orders/$orderId/settle'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'amount': amount,
        'discountAmount': discountAmount,
        'paymentMethod': paymentMethod ?? 'Cash',
        'referenceNumber': referenceNumber,
        'notes': notes ?? 'تسوية من شاشة التسليم',
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException('/orders/$orderId/settle', response.statusCode);
    }
    return OrderDetails.fromJson(jsonDecode(response.body) as OrderJson);
  }

  Future<OrderDetails> recognizeDeliveryRevenue(int orderId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/orders/$orderId/delivery/revenue-recognize'),
      headers: {'Content-Type': 'application/json'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(
          '/orders/$orderId/delivery/revenue-recognize', response.statusCode);
    }
    return OrderDetails.fromJson(jsonDecode(response.body) as OrderJson);
  }

  Future<OrderDetails> deliverOrder(int orderId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/orders/$orderId/delivery/confirm'),
      headers: {'Content-Type': 'application/json'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(
          '/orders/$orderId/delivery/confirm', response.statusCode);
    }
    return OrderDetails.fromJson(jsonDecode(response.body) as OrderJson);
  }

  Future<OrderDetails> waiveRemainingBalance(int orderId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/orders/$orderId/delivery/balance-waiver'),
      headers: {'Content-Type': 'application/json'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(
          '/orders/$orderId/delivery/balance-waiver', response.statusCode);
    }
    return OrderDetails.fromJson(jsonDecode(response.body) as OrderJson);
  }

  Future<OrderDetails> cancelOrder(
    int orderId, {
    String? reason,
    String? cancelledBy,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/orders/$orderId/cancel'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'reason': reason ?? 'إلغاء الطلب من شاشة التفاصيل',
        'cancelledBy': cancelledBy ?? 'FlutterApp',
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException('/orders/$orderId/cancel', response.statusCode);
    }
    return OrderDetails.fromJson(jsonDecode(response.body) as OrderJson);
  }

  Future<ProductionStageRoute> getPieceRoute(int pieceId) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/production/pieces/route?pieceId=$pieceId'),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(
          '/production/pieces/route?pieceId=$pieceId', response.statusCode);
    }
    return ProductionStageRoute.fromJson(
      jsonDecode(response.body) as OrderJson,
    );
  }

  Future<ProductionStageAdvanceResult> advancePieceStage({
    required int pieceId,
    required String pieceType,
    required String requestedStage,
    String? trackingCode,
    String? scannerCode,
    String? employeeCode,
    String? operationReference,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/production/pieces/advance'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'pieceId': pieceId,
        'pieceType': pieceType,
        'trackingCode': trackingCode,
        'requestedStage': requestedStage,
        'scannerCode': scannerCode,
        'employeeCode': employeeCode,
        'operationReference':
            operationReference ?? 'Manual order detail transition',
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(
          '/production/pieces/advance', response.statusCode);
    }
    return ProductionStageAdvanceResult.fromJson(
      jsonDecode(response.body) as OrderJson,
    );
  }

  Future<CancelledPieceDisposition> savePieceDisposition(
    int pieceId,
    String decision, {
    String? reason,
    String? decidedBy,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/cancelled-piece-dispositions'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'pieceId': pieceId,
        'decision': decision,
        'reason': reason ?? 'إدارة القرار من شاشة تفاصيل الطلب',
        'decidedBy': decidedBy ?? 'FlutterApp',
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException(
          '/cancelled-piece-dispositions', response.statusCode);
    }
    return CancelledPieceDisposition.fromJson(
        jsonDecode(response.body) as OrderJson);
  }

  Future<CancelledPieceDisposition> executePieceDisposition(int pieceId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/cancelled-piece-dispositions/$pieceId/execute'),
      headers: {'Content-Type': 'application/json'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw OrderApiException('/cancelled-piece-dispositions/$pieceId/execute',
          response.statusCode);
    }
    return CancelledPieceDisposition.fromJson(
        jsonDecode(response.body) as OrderJson);
  }
}

class OrderApiException implements Exception {
  const OrderApiException(this.path, this.statusCode);
  final String path;
  final int statusCode;
}
