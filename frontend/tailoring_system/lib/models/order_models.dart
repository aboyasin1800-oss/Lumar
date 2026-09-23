import 'dart:convert';

typedef OrderJson = Map<String, dynamic>;

DateTime _date(OrderJson json, String key) =>
    DateTime.parse(json[key] as String);
DateTime? _nullableDate(OrderJson json, String key) =>
    json[key] == null ? null : DateTime.parse(json[key] as String);
double _amount(OrderJson json, String key) => (json[key] as num).toDouble();
double? _nullableAmount(OrderJson json, String key) =>
    (json[key] as num?)?.toDouble();

Map<String, dynamic> _snapshot(String? value) {
  if (value == null || value.trim().isEmpty) return const {};
  try {
    final decoded = jsonDecode(value);
    return decoded is Map<String, dynamic> ? decoded : const {};
  } catch (_) {
    return const {};
  }
}

String? _firstString(Object? first, [Object? second, Object? third]) {
  final values = [first, second, third]
      .whereType<Object>()
      .map((value) => value.toString().trim())
      .where((value) => value.isNotEmpty)
      .toList();
  return values.isEmpty ? null : values.first;
}

class OrderDetails {
  const OrderDetails(
      {required this.id,
      required this.number,
      required this.customerId,
      required this.orderDate,
      required this.deliveryDate,
      required this.totalAmount,
      required this.discountAmount,
      required this.paidAmount,
      required this.remainingAmount,
      required this.urgencyStatus,
      required this.status,
      required this.notes,
      required this.createdDate,
      required this.updatedDate,
      required this.cancellationReason,
      required this.cancelledAt,
      required this.cancelledBy,
      required this.saleCategory,
      required this.revenueRecognized,
      this.revenueRecognizedAt,
      required this.revenueReversalCreated,
      this.revenueReversalCreatedAt});

  factory OrderDetails.fromJson(OrderJson json) => OrderDetails(
        id: json['orderId'] as int,
        number: json['orderNumber'] as String,
        customerId: json['customerId'] as int,
        orderDate: _date(json, 'orderDate'),
        deliveryDate: _nullableDate(json, 'deliveryDate'),
        totalAmount: _amount(json, 'totalAmount'),
        discountAmount: _amount(json, 'discountAmount'),
        paidAmount: _amount(json, 'paidAmount'),
        remainingAmount: _amount(json, 'remainingAmount'),
        urgencyStatus: json['urgencyStatus'] as String,
        status: json['orderStatus'] as String,
        notes: json['notes'] as String?,
        createdDate: _date(json, 'createdDate'),
        updatedDate: _nullableDate(json, 'updatedDate'),
        cancellationReason: json['cancellationReason'] as String?,
        cancelledAt: _nullableDate(json, 'cancelledAt'),
        cancelledBy: json['cancelledBy'] as String?,
        saleCategory: json['saleCategory'] as String,
        revenueRecognized: json['revenueRecognized'] as bool? ?? false,
        revenueRecognizedAt: _nullableDate(json, 'revenueRecognizedAt'),
        revenueReversalCreated: json['revenueReversalCreated'] as bool? ?? false,
        revenueReversalCreatedAt:
            _nullableDate(json, 'revenueReversalCreatedAt'),
      );

  final int id;
  final String number;
  final int customerId;
  final DateTime orderDate;
  final DateTime? deliveryDate;
  final double totalAmount;
  final double discountAmount;
  final double paidAmount;
  final double remainingAmount;
  final String urgencyStatus;
  final String status;
  final String? notes;
  final DateTime createdDate;
  final DateTime? updatedDate;
  final String? cancellationReason;
  final DateTime? cancelledAt;
  final String? cancelledBy;
  final String saleCategory;
  final bool revenueRecognized;
  final DateTime? revenueRecognizedAt;
  final bool revenueReversalCreated;
  final DateTime? revenueReversalCreatedAt;
}

class OrderItem {
  OrderItem(
      {required this.id,
      required this.orderId,
      required this.pieceType,
      required this.quantity,
      required this.fabricCode,
      required this.fabricType,
      required this.fabricColor,
      required this.request1,
      required this.request2,
      required this.specialRequest,
      required this.notes1,
      required this.notes2,
      required this.trackingCode,
      required this.pieceStatus,
      required this.createdDate,
      required Map<String, dynamic> snapshot})
      : snapshot = Map.unmodifiable(snapshot);

  factory OrderItem.fromJson(OrderJson json) {
    final snapshot = _snapshot(json['measurementSnapshot'] as String?);
    final request1 = (json['request1'] as String?) ??
        _firstString(snapshot['request1'], snapshot['Request1']);
    final request2 = (json['request2'] as String?) ??
        _firstString(snapshot['request2'], snapshot['Request2']);
    final specialRequest = (json['specialRequest'] as String?) ??
        _firstString(snapshot['specialRequest'], snapshot['SpecialRequest']);

    return OrderItem(
      id: json['orderItemId'] as int,
      orderId: json['orderId'] as int,
      pieceType: json['pieceType'] as String,
      quantity: json['quantity'] as int,
      fabricCode: json['fabricCode'] as String?,
      fabricType: json['fabricType'] as String?,
      fabricColor: json['fabricColor'] as String?,
      request1: request1,
      request2: request2,
      specialRequest: specialRequest,
      notes1: json['notes1'] as String?,
      notes2: json['notes2'] as String?,
      trackingCode: json['trackingCode'] as String?,
      pieceStatus: json['pieceStatus'] as String?,
      createdDate: _date(json, 'createdDate'),
      snapshot: snapshot,
    );
  }

  final int id;
  final int orderId;
  final String pieceType;
  final int quantity;
  final String? fabricCode;
  final String? fabricType;
  final String? fabricColor;
  final String? request1;
  final String? request2;
  final String? specialRequest;
  final String? notes1;
  final String? notes2;
  final String? trackingCode;
  final String? pieceStatus;
  final DateTime createdDate;
  final Map<String, dynamic> snapshot;

  String? get catalogNumber => [
        snapshot['_catalogNumber'],
        snapshot['CatalogNumber'],
        snapshot['catalogNumber'],
        snapshot['barcode'],
        snapshot['Barcode'],
      ]
          .whereType<Object>()
          .map((value) => value.toString())
          .firstWhere(
            (value) => value.trim().isNotEmpty,
            orElse: () => '',
          )
          .trim()
          .isEmpty
      ? null
      : [
          snapshot['_catalogNumber'],
          snapshot['CatalogNumber'],
          snapshot['catalogNumber'],
          snapshot['barcode'],
          snapshot['Barcode'],
        ]
              .whereType<Object>()
              .map((value) => value.toString())
              .firstWhere((value) => value.trim().isNotEmpty, orElse: () => '')
              .trim();
  double? get consumption =>
      double.tryParse(snapshot['_consumption']?.toString() ?? '');
  double? get fullCost => snapshot['fullCost'] is num
      ? (snapshot['fullCost'] as num).toDouble()
      : double.tryParse(snapshot['fullCost']?.toString() ?? '');
  Map<String, dynamic> get measurements =>
      Map.unmodifiable(Map.fromEntries(snapshot.entries.where((entry) =>
          !entry.key.startsWith('_') &&
          !const {
            'fullCost',
            'source',
            'productType',
            'productCode',
            'quantity',
            'unitPrice',
            'lineTotal',
            'importedReadyMadeProductId'
          }.contains(entry.key))));
  Map<String, dynamic> get storedProductionFields =>
      Map.unmodifiable(Map.fromEntries(snapshot.entries.where((entry) => const {
            'source',
            'productType',
            'productCode',
            'quantity',
            'unitPrice',
            'lineTotal',
            'fullCost'
          }.contains(entry.key))));
}

class OrderPiece {
  const OrderPiece(
      {required this.id,
      required this.orderItemId,
      required this.trackingCode,
      required this.status,
      required this.number,
      required this.createdDate});

  factory OrderPiece.fromJson(OrderJson json) => OrderPiece(
        id: json['pieceId'] as int,
        orderItemId: json['orderItemId'] as int,
        trackingCode: json['trackingCode'] as String,
        status: json['pieceStatus'] as String,
        number: json['pieceNumber'] as int,
        createdDate: _date(json, 'createdDate'),
      );

  final int id;
  final int orderItemId;
  final String trackingCode;
  final String status;
  final int number;
  final DateTime createdDate;
}

class OrderFabric {
  const OrderFabric(
      {required this.id,
      required this.orderItemId,
      required this.inventoryItemId,
      required this.fabricCode,
      required this.fabricType,
      required this.fabricColor,
      required this.quantity,
      required this.unit,
      required this.unitCost,
      required this.totalCost,
      required this.consumedQuantity,
      required this.yardPrice,
      required this.createdDate});

  factory OrderFabric.fromJson(OrderJson json) => OrderFabric(
        id: json['orderItemFabricId'] as int,
        orderItemId: json['orderItemId'] as int,
        inventoryItemId: json['inventoryItemId'] as int?,
        fabricCode: json['fabricCode'] as String?,
        fabricType: json['fabricType'] as String?,
        fabricColor: json['fabricColor'] as String?,
        quantity: _amount(json, 'quantity'),
        unit: json['unit'] as String,
        unitCost: _amount(json, 'unitCost'),
        totalCost: _amount(json, 'totalCost'),
        consumedQuantity: _amount(json, 'consumedQuantity'),
        yardPrice: _nullableAmount(json, 'yardPrice'),
        createdDate: _date(json, 'createdDate'),
      );

  final int id;
  final int orderItemId;
  final int? inventoryItemId;
  final String? fabricCode;
  final String? fabricType;
  final String? fabricColor;
  final double quantity;
  final String unit;
  final double unitCost;
  final double totalCost;
  final double consumedQuantity;
  final double? yardPrice;
  final DateTime createdDate;

  double? get consumedCost =>
      yardPrice == null ? null : consumedQuantity * yardPrice!;
}

class OrderPayment {
  const OrderPayment(
      {required this.id,
      required this.orderId,
      required this.paymentDate,
      required this.amount,
      required this.paymentMethod,
      required this.referenceNumber,
      required this.notes,
      required this.paymentKind});

  factory OrderPayment.fromJson(OrderJson json) => OrderPayment(
        id: json['paymentId'] as int,
        orderId: json['orderId'] as int,
        paymentDate: _date(json, 'paymentDate'),
        amount: _amount(json, 'amount'),
        paymentMethod: json['paymentMethod'] as String?,
        referenceNumber: json['referenceNumber'] as String?,
        notes: json['notes'] as String?,
        paymentKind: json['paymentKind'] as String,
      );

  final int id;
  final int orderId;
  final DateTime paymentDate;
  final double amount;
  final String? paymentMethod;
  final String? referenceNumber;
  final String? notes;
  final String paymentKind;
}

class OrderCustomer {
  const OrderCustomer(
      {required this.id,
      required this.code,
      required this.name,
      required this.phoneNumber});

  factory OrderCustomer.fromJson(OrderJson json) => OrderCustomer(
        id: json['customerId'] as int,
        code: json['customerCode'] as String?,
        name: json['customerName'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
      );

  final int id;
  final String? code;
  final String? name;
  final String? phoneNumber;
}

class OrderTrackingEvent {
  const OrderTrackingEvent(
      {required this.id,
      required this.orderItemId,
      required this.orderId,
      required this.trackingCode,
      required this.stage,
      required this.status,
      required this.eventTime,
      required this.employeeCode,
      required this.notes,
      required this.isReverted,
      required this.revertedAt,
      required this.pieceId});

  factory OrderTrackingEvent.fromJson(OrderJson json) => OrderTrackingEvent(
        id: json['trackingEventId'] as int,
        orderItemId: json['orderItemId'] as int?,
        orderId: json['orderId'] as int?,
        trackingCode: json['trackingCode'] as String?,
        stage: json['stage'] as String,
        status: json['status'] as String,
        eventTime: _date(json, 'eventTime'),
        employeeCode: json['employeeCode'] as String?,
        notes: json['notes'] as String?,
        isReverted: json['isReverted'] as bool,
        revertedAt: _nullableDate(json, 'revertedAt'),
        pieceId: json['pieceId'] as int?,
      );

  final int id;
  final int? orderItemId;
  final int? orderId;
  final String? trackingCode;
  final String stage;
  final String status;
  final DateTime eventTime;
  final String? employeeCode;
  final String? notes;
  final bool isReverted;
  final DateTime? revertedAt;
  final int? pieceId;
}

class OrderFinancialTransaction {
  const OrderFinancialTransaction(
      {required this.referenceNumber,
      required this.transactionType,
      required this.amount,
      required this.description,
      required this.createdAt});

  factory OrderFinancialTransaction.fromJson(OrderJson json) =>
      OrderFinancialTransaction(
        referenceNumber: json['referenceNumber'] as String,
        transactionType: json['transactionType'] as String,
        amount: _amount(json, 'amount'),
        description: json['description'] as String?,
        createdAt: _date(json, 'createdAt'),
      );

  final String referenceNumber;
  final String transactionType;
  final double amount;
  final String? description;
  final DateTime createdAt;
}

class OrderLedgerEntry {
  const OrderLedgerEntry(
      {required this.referenceNumber,
      required this.debitAmount,
      required this.creditAmount,
      required this.balanceAfterTransaction,
      required this.createdAt});

  factory OrderLedgerEntry.fromJson(OrderJson json) => OrderLedgerEntry(
        referenceNumber: json['referenceNumber'] as String,
        debitAmount: _amount(json, 'debitAmount'),
        creditAmount: _amount(json, 'creditAmount'),
        balanceAfterTransaction: _amount(json, 'balanceAfterTransaction'),
        createdAt: _date(json, 'createdAt'),
      );

  final String referenceNumber;
  final double debitAmount;
  final double creditAmount;
  final double balanceAfterTransaction;
  final DateTime createdAt;
}

class OrderDetailsData {
  const OrderDetailsData(
      {required this.order,
      required this.customer,
      required this.items,
      required this.pieces,
      required this.fabrics,
      required this.payments,
      required this.trackingEvents,
      required this.financialTransactions,
      required this.ledgerEntries});
  final OrderDetails order;
  final OrderCustomer customer;
  final List<OrderItem> items;
  final List<OrderPiece> pieces;
  final List<OrderFabric> fabrics;
  final List<OrderPayment> payments;
  final List<OrderTrackingEvent> trackingEvents;
  final List<OrderFinancialTransaction> financialTransactions;
  final List<OrderLedgerEntry> ledgerEntries;
}

class CancelledPieceDisposition {
  const CancelledPieceDisposition({
    required this.cancelledPieceDispositionId,
    required this.pieceId,
    required this.decision,
    required this.reason,
    required this.decidedBy,
    required this.decidedAt,
    required this.transferStatus,
    required this.readyMadeInventoryProductId,
    required this.transferredAt,
    required this.createdAt,
  });

  factory CancelledPieceDisposition.fromJson(OrderJson json) =>
      CancelledPieceDisposition(
        cancelledPieceDispositionId:
            json['cancelledPieceDispositionId'] as int? ?? 0,
        pieceId: json['pieceId'] as int? ?? 0,
        decision: json['decision'] as String? ?? '',
        reason: json['reason'] as String?,
        decidedBy: json['decidedBy'] as String?,
        decidedAt: _nullableDate(json, 'decidedAt') ?? DateTime.now(),
        transferStatus: json['transferStatus'] as String? ?? 'Pending',
        readyMadeInventoryProductId:
            json['readyMadeInventoryProductId'] as int?,
        transferredAt: _nullableDate(json, 'transferredAt'),
        createdAt: _nullableDate(json, 'createdAt') ?? DateTime.now(),
      );

  final int cancelledPieceDispositionId;
  final int pieceId;
  final String decision;
  final String? reason;
  final String? decidedBy;
  final DateTime decidedAt;
  final String transferStatus;
  final int? readyMadeInventoryProductId;
  final DateTime? transferredAt;
  final DateTime createdAt;
}

class ProductionStageRoute {
  const ProductionStageRoute({
    required this.pieceType,
    required this.route,
    required this.currentStage,
    required this.nextStage,
  });

  factory ProductionStageRoute.fromJson(OrderJson json) => ProductionStageRoute(
        pieceType: (json['pieceType'] ?? '').toString(),
        route: (json['route'] as List? ?? const [])
            .map((entry) => entry.toString())
            .toList(),
        currentStage: (json['currentStage'] ?? '').toString(),
        nextStage: json['nextStage']?.toString(),
      );

  final String pieceType;
  final List<String> route;
  final String currentStage;
  final String? nextStage;
}

class ProductionStageAdvanceResult {
  const ProductionStageAdvanceResult({
    required this.pieceId,
    required this.trackingCode,
    required this.previousStatus,
    required this.newStatus,
    required this.nextStage,
    required this.message,
    required this.updated,
  });

  factory ProductionStageAdvanceResult.fromJson(OrderJson json) =>
      ProductionStageAdvanceResult(
        pieceId: json['pieceId'] as int? ?? 0,
        trackingCode: json['trackingCode']?.toString(),
        previousStatus: (json['previousStatus'] ?? '').toString(),
        newStatus: (json['newStatus'] ?? '').toString(),
        nextStage: json['nextStage']?.toString(),
        message: (json['message'] ?? '').toString(),
        updated: json['updated'] as bool? ?? false,
      );

  final int? pieceId;
  final String? trackingCode;
  final String previousStatus;
  final String newStatus;
  final String? nextStage;
  final String message;
  final bool updated;
}
