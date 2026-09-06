typedef CustomerJson = Map<String, dynamic>;

DateTime _date(CustomerJson json, String key) =>
    DateTime.parse(json[key] as String);
DateTime? _nullableDate(CustomerJson json, String key) =>
    json[key] == null ? null : DateTime.parse(json[key] as String);
double _amount(CustomerJson json, String key) => (json[key] as num).toDouble();
double? _nullableAmount(CustomerJson json, String key) =>
    (json[key] as num?)?.toDouble();

class CustomerDetails {
  const CustomerDetails(
      {required this.id,
      required this.code,
      required this.name,
      required this.phoneNumber,
      required this.parentCustomerCode,
      required this.parentCustomerId,
      required this.parentCustomerName,
      required this.address,
      required this.notes,
      required this.isActive,
      required this.totalPoints,
      required this.totalPieces,
      required this.totalDebts,
      required this.relationshipType});

  factory CustomerDetails.fromJson(CustomerJson json) => CustomerDetails(
        id: json['customerId'] as int,
        code: json['customerCode'] as String?,
        name: json['customerName'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        parentCustomerCode: json['parentCustomerCode'] as String?,
        parentCustomerId: json['parentCustomerId'] as int?,
        parentCustomerName: json['parentCustomerName'] as String?,
        address: json['address'] as String?,
        notes: json['notes'] as String?,
        isActive: json['isActive'] as bool,
        totalPoints: _nullableAmount(json, 'totalPoints'),
        totalPieces: json['totalPieces'] as int?,
        totalDebts: _nullableAmount(json, 'totalDebts'),
        relationshipType: json['relationshipType'] as String?,
      );

  final int id;
  final String? code;
  final String? name;
  final String? phoneNumber;
  final String? parentCustomerCode;
  final int? parentCustomerId;
  final String? parentCustomerName;
  final String? address;
  final String? notes;
  final bool isActive;
  final double? totalPoints;
  final int? totalPieces;
  final double? totalDebts;
  final String? relationshipType;
}

class CustomerLoyalty {
  const CustomerLoyalty(
      {required this.currentPoints,
      required this.lifetimeEarnedPoints,
      required this.lifetimeRedeemedPoints,
      required this.pendingExpirePoints,
      required this.vipLevelCode,
      required this.vipLevelDisplayName,
      required this.createdAt,
      required this.lastActivityAt});

  factory CustomerLoyalty.fromJson(CustomerJson json) => CustomerLoyalty(
        currentPoints: _amount(json, 'currentPoints'),
        lifetimeEarnedPoints: _amount(json, 'lifetimeEarnedPoints'),
        lifetimeRedeemedPoints: _amount(json, 'lifetimeRedeemedPoints'),
        pendingExpirePoints: _amount(json, 'pendingExpirePoints'),
        vipLevelCode: json['vipLevelCode'] as String?,
        vipLevelDisplayName: json['vipLevelDisplayName'] as String?,
        createdAt: _date(json, 'createdAt'),
        lastActivityAt: _nullableDate(json, 'lastActivityAt'),
      );

  final double currentPoints;
  final double lifetimeEarnedPoints;
  final double lifetimeRedeemedPoints;
  final double pendingExpirePoints;
  final String? vipLevelCode;
  final String? vipLevelDisplayName;
  final DateTime createdAt;
  final DateTime? lastActivityAt;
}

class CustomerReferral {
  const CustomerReferral(
      {required this.referralCode,
      required this.referralCodeIsActive,
      required this.totalReferrals,
      required this.successfulReferrals,
      required this.totalRewardsAmount,
      required this.totalRewardPoints});

  factory CustomerReferral.fromJson(CustomerJson json) => CustomerReferral(
        referralCode: json['referralCode'] as String?,
        referralCodeIsActive: json['referralCodeIsActive'] as bool?,
        totalReferrals: json['totalReferrals'] as int,
        successfulReferrals: json['successfulReferrals'] as int,
        totalRewardsAmount: _amount(json, 'totalRewardsAmount'),
        totalRewardPoints: _amount(json, 'totalRewardPoints'),
      );

  final String? referralCode;
  final bool? referralCodeIsActive;
  final int totalReferrals;
  final int successfulReferrals;
  final double totalRewardsAmount;
  final double totalRewardPoints;
}

class CustomerLedgerRecord {
  const CustomerLedgerRecord(
      {required this.id,
      required this.referenceNumber,
      required this.debitAmount,
      required this.creditAmount,
      required this.balanceAfterTransaction,
      required this.createdAt});

  factory CustomerLedgerRecord.fromJson(CustomerJson json) =>
      CustomerLedgerRecord(
        id: json['customerLedgerEntryId'] as int,
        referenceNumber: json['referenceNumber'] as String,
        debitAmount: _amount(json, 'debitAmount'),
        creditAmount: _amount(json, 'creditAmount'),
        balanceAfterTransaction: _amount(json, 'balanceAfterTransaction'),
        createdAt: _date(json, 'createdAt'),
      );

  final int id;
  final String referenceNumber;
  final double debitAmount;
  final double creditAmount;
  final double balanceAfterTransaction;
  final DateTime createdAt;
}

class CustomerOrder {
  const CustomerOrder(
      {required this.id,
      required this.number,
      required this.customerId,
      required this.orderDate,
      required this.totalAmount,
      required this.discountAmount,
      required this.paidAmount,
      required this.remainingAmount,
      required this.urgencyStatus,
      required this.status,
      required this.notes,
      required this.cancellationReason,
      required this.cancelledAt,
      required this.saleCategory});

  factory CustomerOrder.fromJson(CustomerJson json) => CustomerOrder(
        id: json['orderId'] as int,
        number: json['orderNumber'] as String,
        customerId: json['customerId'] as int,
        orderDate: _date(json, 'orderDate'),
        totalAmount: _amount(json, 'totalAmount'),
        discountAmount: _amount(json, 'discountAmount'),
        paidAmount: _amount(json, 'paidAmount'),
        remainingAmount: _amount(json, 'remainingAmount'),
        urgencyStatus: json['urgencyStatus'] as String,
        status: json['orderStatus'] as String,
        notes: json['notes'] as String?,
        cancellationReason: json['cancellationReason'] as String?,
        cancelledAt: _nullableDate(json, 'cancelledAt'),
        saleCategory: json['saleCategory'] as String,
      );

  final int id;
  final String number;
  final int customerId;
  final DateTime orderDate;
  final double totalAmount;
  final double discountAmount;
  final double paidAmount;
  final double remainingAmount;
  final String urgencyStatus;
  final String status;
  final String? notes;
  final String? cancellationReason;
  final DateTime? cancelledAt;
  final String saleCategory;
}

class CustomerOrderPayment {
  const CustomerOrderPayment(
      {required this.id,
      required this.orderId,
      required this.paymentDate,
      required this.amount,
      required this.paymentMethod,
      required this.referenceNumber,
      required this.notes,
      required this.paymentKind});

  factory CustomerOrderPayment.fromJson(CustomerJson json) =>
      CustomerOrderPayment(
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

class CustomerFinancialTransaction {
  const CustomerFinancialTransaction(
      {required this.referenceNumber,
      required this.transactionType,
      required this.amount,
      required this.description,
      required this.createdAt});

  factory CustomerFinancialTransaction.fromJson(CustomerJson json) =>
      CustomerFinancialTransaction(
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

class CustomerDetailsData {
  const CustomerDetailsData(
      {required this.customer,
      required this.loyalty,
      required this.referral,
      required this.ledger,
      required this.orders,
      required this.payments,
      required this.financialTransactions});
  final CustomerDetails customer;
  final CustomerLoyalty? loyalty;
  final CustomerReferral? referral;
  final List<CustomerLedgerRecord> ledger;
  final List<CustomerOrder> orders;
  final List<CustomerOrderPayment> payments;
  final List<CustomerFinancialTransaction> financialTransactions;
}
