typedef JsonMap = Map<String, dynamic>;

DateTime _readDate(JsonMap json, String key) {
  final value = json[key];
  if (value == null || value == '') return DateTime.fromMillisecondsSinceEpoch(0);
  return DateTime.parse(value.toString());
}

double _readDouble(JsonMap json, String key) {
  final value = json[key];
  if (value == null) return 0.0;
  return (value as num).toDouble();
}

class SupplierMobileProfile {
  const SupplierMobileProfile({
    required this.supplierId,
    required this.supplierCode,
    required this.supplierName,
    required this.phone,
    required this.email,
    required this.address,
    required this.isActive,
    required this.createdAt,
    this.updatedAt,
  });

  factory SupplierMobileProfile.fromJson(JsonMap json) => SupplierMobileProfile(
        supplierId: json['supplierId'] as int? ?? 0,
        supplierCode: json['supplierCode'] as String? ?? '',
        supplierName: json['supplierName'] as String? ?? '',
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        address: json['address'] as String?,
        isActive: json['isActive'] as bool? ?? true,
        createdAt: _readDate(json, 'createdAt'),
        updatedAt: json['updatedAt'] == null ? null : _readDate(json, 'updatedAt'),
      );

  final int supplierId;
  final String supplierCode;
  final String supplierName;
  final String? phone;
  final String? email;
  final String? address;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;
}

class SupplierHomeSummary {
  const SupplierHomeSummary({
    required this.currentBalance,
    required this.invoiceCount,
    required this.paymentCount,
    required this.goodsReceiptCount,
    required this.pendingAcknowledgements,
    required this.openDisputes,
  });

  factory SupplierHomeSummary.fromJson(JsonMap json) => SupplierHomeSummary(
        currentBalance: _readDouble(json, 'currentBalance'),
        invoiceCount: json['invoiceCount'] as int? ?? 0,
        paymentCount: json['paymentCount'] as int? ?? 0,
        goodsReceiptCount: json['goodsReceiptCount'] as int? ?? 0,
        pendingAcknowledgements: json['pendingAcknowledgements'] as int? ?? 0,
        openDisputes: json['openDisputes'] as int? ?? 0,
      );

  final double currentBalance;
  final int invoiceCount;
  final int paymentCount;
  final int goodsReceiptCount;
  final int pendingAcknowledgements;
  final int openDisputes;
}

class SupplierLedgerEntry {
  const SupplierLedgerEntry({
    required this.id,
    required this.supplierId,
    required this.referenceNumber,
    required this.debitAmount,
    required this.creditAmount,
    required this.balanceAfterTransaction,
    required this.createdAt,
  });

  factory SupplierLedgerEntry.fromJson(JsonMap json) => SupplierLedgerEntry(
        id: json['supplierLedgerEntryId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        referenceNumber: json['referenceNumber'] as String? ?? '',
        debitAmount: _readDouble(json, 'debitAmount'),
        creditAmount: _readDouble(json, 'creditAmount'),
        balanceAfterTransaction: _readDouble(json, 'balanceAfterTransaction'),
        createdAt: _readDate(json, 'createdAt'),
      );

  final int id;
  final int supplierId;
  final String referenceNumber;
  final double debitAmount;
  final double creditAmount;
  final double balanceAfterTransaction;
  final DateTime createdAt;
}

class SupplierInvoice {
  const SupplierInvoice({
    required this.id,
    required this.supplierId,
    required this.invoiceNumber,
    required this.invoiceDate,
    required this.dueDate,
    required this.totalAmount,
    required this.amountPaid,
    required this.status,
    required this.notes,
    required this.createdAt,
  });

  factory SupplierInvoice.fromJson(JsonMap json) => SupplierInvoice(
        id: json['supplierInvoiceId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        invoiceNumber: json['invoiceNumber'] as String? ?? '',
        invoiceDate: _readDate(json, 'invoiceDate'),
        dueDate: _readDate(json, 'dueDate'),
        totalAmount: _readDouble(json, 'totalAmount'),
        amountPaid: _readDouble(json, 'amountPaid'),
        status: json['status'] as String? ?? '',
        notes: json['notes'] as String?,
        createdAt: _readDate(json, 'createdAt'),
      );

  final int id;
  final int supplierId;
  final String invoiceNumber;
  final DateTime invoiceDate;
  final DateTime dueDate;
  final double totalAmount;
  final double amountPaid;
  final String status;
  final String? notes;
  final DateTime createdAt;
}

class SupplierPayment {
  const SupplierPayment({
    required this.id,
    required this.supplierId,
    required this.paymentNumber,
    required this.paymentDate,
    required this.amount,
    required this.paymentMethod,
    required this.referenceNumber,
    required this.notes,
    required this.createdAt,
    required this.journalEntryId,
  });

  factory SupplierPayment.fromJson(JsonMap json) => SupplierPayment(
        id: json['supplierPaymentId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        paymentNumber: json['paymentNumber'] as String? ?? '',
        paymentDate: _readDate(json, 'paymentDate'),
        amount: _readDouble(json, 'amount'),
        paymentMethod: json['paymentMethod'] as String?,
        referenceNumber: json['referenceNumber'] as String?,
        notes: json['notes'] as String?,
        createdAt: _readDate(json, 'createdAt'),
        journalEntryId: json['journalEntryId'] as int?,
      );

  final int id;
  final int supplierId;
  final String paymentNumber;
  final DateTime paymentDate;
  final double amount;
  final String? paymentMethod;
  final String? referenceNumber;
  final String? notes;
  final DateTime createdAt;
  final int? journalEntryId;
}

class SupplierPaymentResponseStatus {
  const SupplierPaymentResponseStatus({
    required this.supplierPaymentId,
    required this.supplierId,
    required this.paymentNumber,
    required this.acknowledgementStatus,
    required this.disputeStatus,
    required this.disputedAmount,
    required this.acknowledgedAt,
    required this.resolvedAt,
    required this.createdAt,
  });

  factory SupplierPaymentResponseStatus.fromJson(JsonMap json) => SupplierPaymentResponseStatus(
        supplierPaymentId: json['supplierPaymentId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        paymentNumber: json['paymentNumber'] as String? ?? '',
        acknowledgementStatus: json['acknowledgementStatus'] as String? ?? '',
        disputeStatus: json['disputeStatus'] as String? ?? '',
        disputedAmount: json['disputedAmount'] == null ? null : _readDouble(json, 'disputedAmount'),
        acknowledgedAt: json['acknowledgedAt'] == null ? null : _readDate(json, 'acknowledgedAt'),
        resolvedAt: json['resolvedAt'] == null ? null : _readDate(json, 'resolvedAt'),
        createdAt: _readDate(json, 'createdAt'),
      );

  final int supplierPaymentId;
  final int supplierId;
  final String paymentNumber;
  final String acknowledgementStatus;
  final String disputeStatus;
  final double? disputedAmount;
  final DateTime? acknowledgedAt;
  final DateTime? resolvedAt;
  final DateTime createdAt;
}

class SupplierMessage {
  const SupplierMessage({
    required this.supplierMessageId,
    required this.supplierId,
    required this.mobileAccountId,
    required this.messageType,
    required this.subject,
    required this.body,
    required this.channel,
    required this.relatedEntityType,
    required this.relatedEntityId,
    required this.deliveryStatus,
    required this.isRead,
    required this.readAtUtc,
    required this.createdAtUtc,
    required this.sentAtUtc,
    required this.idempotencyKey,
  });

  factory SupplierMessage.fromJson(JsonMap json) => SupplierMessage(
        supplierMessageId: json['supplierMessageId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        mobileAccountId: json['mobileAccountId'] as int? ?? 0,
        messageType: json['messageType'] as String? ?? '',
        subject: json['subject'] as String? ?? '',
        body: json['body'] as String? ?? '',
        channel: json['channel'] as String? ?? '',
        relatedEntityType: json['relatedEntityType'] as String?,
        relatedEntityId: json['relatedEntityId'] as int?,
        deliveryStatus: json['deliveryStatus'] as String? ?? '',
        isRead: json['isRead'] as bool? ?? false,
        readAtUtc: json['readAtUtc'] == null ? null : _readDate(json, 'readAtUtc'),
        createdAtUtc: _readDate(json, 'createdAtUtc'),
        sentAtUtc: json['sentAtUtc'] == null ? null : _readDate(json, 'sentAtUtc'),
        idempotencyKey: json['idempotencyKey'] as String? ?? '',
      );

  final int supplierMessageId;
  final int supplierId;
  final int mobileAccountId;
  final String messageType;
  final String subject;
  final String body;
  final String channel;
  final String? relatedEntityType;
  final int? relatedEntityId;
  final String deliveryStatus;
  final bool isRead;
  final DateTime? readAtUtc;
  final DateTime createdAtUtc;
  final DateTime? sentAtUtc;
  final String idempotencyKey;
}

class SupplierNotification {
  const SupplierNotification({
    required this.supplierNotificationId,
    required this.supplierId,
    required this.mobileAccountId,
    required this.notificationType,
    required this.title,
    required this.body,
    required this.isRead,
    required this.readAtUtc,
    required this.createdAtUtc,
    required this.expiresAtUtc,
    required this.referenceType,
    required this.referenceId,
  });

  factory SupplierNotification.fromJson(JsonMap json) => SupplierNotification(
        supplierNotificationId: json['supplierNotificationId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        mobileAccountId: json['mobileAccountId'] as int? ?? 0,
        notificationType: json['notificationType'] as String? ?? '',
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
        isRead: json['isRead'] as bool? ?? false,
        readAtUtc: json['readAtUtc'] == null ? null : _readDate(json, 'readAtUtc'),
        createdAtUtc: _readDate(json, 'createdAtUtc'),
        expiresAtUtc: json['expiresAtUtc'] == null ? null : _readDate(json, 'expiresAtUtc'),
        referenceType: json['referenceType'] as String?,
        referenceId: json['referenceId'] as int?,
      );

  final int supplierNotificationId;
  final int supplierId;
  final int mobileAccountId;
  final String notificationType;
  final String title;
  final String body;
  final bool isRead;
  final DateTime? readAtUtc;
  final DateTime createdAtUtc;
  final DateTime? expiresAtUtc;
  final String? referenceType;
  final int? referenceId;
}

class SupplierAnnouncement {
  const SupplierAnnouncement({
    required this.supplierAnnouncementId,
    required this.supplierId,
    required this.title,
    required this.body,
    required this.isActive,
    required this.startsAtUtc,
    required this.endsAtUtc,
    required this.createdAtUtc,
  });

  factory SupplierAnnouncement.fromJson(JsonMap json) => SupplierAnnouncement(
        supplierAnnouncementId: json['supplierAnnouncementId'] as int? ?? 0,
        supplierId: json['supplierId'] as int? ?? 0,
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
        isActive: json['isActive'] as bool? ?? true,
        startsAtUtc: _readDate(json, 'startsAtUtc'),
        endsAtUtc: _readDate(json, 'endsAtUtc'),
        createdAtUtc: _readDate(json, 'createdAtUtc'),
      );

  final int supplierAnnouncementId;
  final int supplierId;
  final String title;
  final String body;
  final bool isActive;
  final DateTime startsAtUtc;
  final DateTime endsAtUtc;
  final DateTime createdAtUtc;
}
