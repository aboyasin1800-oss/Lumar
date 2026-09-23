class CustomerReferralCandidate {
  const CustomerReferralCandidate({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.phoneNumber,
    required this.referralCode,
  });

  factory CustomerReferralCandidate.fromJson(Map<String, dynamic> json) =>
      CustomerReferralCandidate(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        referralCode: json['referralCode'] as String?,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final String? phoneNumber;
  final String? referralCode;
}

class CustomerCreationResult {
  const CustomerCreationResult({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.phoneNumber,
    required this.address,
    required this.notes,
    required this.relationshipType,
    required this.totalPoints,
    required this.totalDebts,
    required this.referralCount,
    required this.referralRewardsAmount,
    required this.referralRewardPoints,
    required this.registrationTransactionId,
    required this.referrerCustomerId,
    required this.referrerCustomerCode,
    required this.referrerCustomerName,
    required this.referrerPhoneNumber,
    required this.referrerReferralCode,
  });

  factory CustomerCreationResult.fromJson(Map<String, dynamic> json) =>
      CustomerCreationResult(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String? ?? '',
        customerName: json['customerName'] as String? ?? '',
        phoneNumber: json['phoneNumber'] as String? ?? '',
        address: json['address'] as String?,
        notes: json['notes'] as String?,
        relationshipType: json['relationshipType'] as String?,
        totalPoints: _asDouble(json['totalPoints']),
        totalDebts: _asDouble(json['totalDebts']),
        referralCount: (json['referralCount'] as num?)?.toInt() ?? 0,
        referralRewardsAmount: _asDouble(json['referralRewardsAmount']),
        referralRewardPoints: _asDouble(json['referralRewardPoints']),
        registrationTransactionId:
            (json['registrationTransactionId'] as num?)?.toInt(),
        referrerCustomerId: (json['referrerCustomerId'] as num?)?.toInt(),
        referrerCustomerCode: json['referrerCustomerCode'] as String?,
        referrerCustomerName: json['referrerCustomerName'] as String?,
        referrerPhoneNumber: json['referrerPhoneNumber'] as String?,
        referrerReferralCode: json['referrerReferralCode'] as String?,
      );

  final int customerId;
  final String customerCode;
  final String customerName;
  final String phoneNumber;
  final String? address;
  final String? notes;
  final String? relationshipType;
  final double totalPoints;
  final double? totalDebts;
  final int referralCount;
  final double referralRewardsAmount;
  final double referralRewardPoints;
  final int? registrationTransactionId;
  final int? referrerCustomerId;
  final String? referrerCustomerCode;
  final String? referrerCustomerName;
  final String? referrerPhoneNumber;
  final String? referrerReferralCode;
}

double _asDouble(dynamic value) {
  if (value == null) return 0;
  if (value is num) return value.toDouble();
  return double.tryParse(value.toString()) ?? 0;
}
