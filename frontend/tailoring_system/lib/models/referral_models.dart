typedef ReferralJson = Map<String, dynamic>;

class ReferralCustomerIdentity {
  const ReferralCustomerIdentity({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.phoneNumber,
  });

  factory ReferralCustomerIdentity.fromJson(ReferralJson json) =>
      ReferralCustomerIdentity(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final String? phoneNumber;
}

DateTime _parseDate(dynamic value) {
  if (value == null) throw const FormatException('Date is null');
  return DateTime.parse(value.toString());
}

DateTime? _parseNullableDate(dynamic value) =>
    value == null ? null : DateTime.parse(value.toString());

double _numAsDouble(dynamic value, {double fallback = 0}) {
  if (value == null) return fallback;
  if (value is num) return value.toDouble();
  return double.tryParse(value.toString()) ?? fallback;
}

class ReferralAccount {
  const ReferralAccount({
    required this.customerId,
    required this.referralCode,
    required this.referralCodeIsActive,
    required this.totalReferrals,
    required this.successfulReferrals,
    required this.totalRewardsAmount,
    required this.totalRewardPoints,
    required this.createdAt,
    required this.updatedAt,
  });

  factory ReferralAccount.fromJson(ReferralJson json) => ReferralAccount(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        referralCode: json['referralCode'] as String?,
        referralCodeIsActive: json['referralCodeIsActive'] as bool? ?? false,
        totalReferrals: (json['totalReferrals'] as num?)?.toInt() ?? 0,
        successfulReferrals:
            (json['successfulReferrals'] as num?)?.toInt() ?? 0,
        totalRewardsAmount: _numAsDouble(json['totalRewardsAmount']),
        totalRewardPoints: _numAsDouble(json['totalRewardPoints']),
        createdAt: _parseDate(json['createdAt']),
        updatedAt: _parseDate(json['updatedAt']),
      );

  final int customerId;
  final String? referralCode;
  final bool referralCodeIsActive;
  final int totalReferrals;
  final int successfulReferrals;
  final double totalRewardsAmount;
  final double totalRewardPoints;
  final DateTime createdAt;
  final DateTime updatedAt;
}

class ReferralCustomerDetails {
  const ReferralCustomerDetails({
    required this.currentPoints,
    required this.totalReferralPoints,
    required this.totalReferralRewardsAmount,
    required this.referralAccountBalance,
    required this.totalFinancialBalance,
    required this.currentDebt,
    required this.latestLedgerBalance,
  });

  final double currentPoints;
  final double totalReferralPoints;
  final double totalReferralRewardsAmount;
  final double referralAccountBalance;
  final double totalFinancialBalance;
  final double currentDebt;
  final double latestLedgerBalance;
}

class ReferralCode {
  const ReferralCode({
    required this.referralCodeId,
    required this.customerId,
    required this.code,
    required this.isActive,
    required this.createdAt,
    required this.lastUsedAt,
  });

  factory ReferralCode.fromJson(ReferralJson json) => ReferralCode(
        referralCodeId: (json['referralCodeId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        code: json['code'] as String? ?? '',
        isActive: json['isActive'] as bool? ?? false,
        createdAt: _parseDate(json['createdAt']),
        lastUsedAt: _parseNullableDate(json['lastUsedAt']),
      );

  final int referralCodeId;
  final int customerId;
  final String code;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? lastUsedAt;
}

class ReferralTransaction {
  const ReferralTransaction({
    required this.referralTransactionId,
    required this.referrerCustomerId,
    required this.referredCustomerId,
    required this.referralCodeId,
    required this.orderId,
    required this.referralRewardId,
    required this.transactionType,
    required this.fixedRewardAmount,
    required this.loyaltyPoints,
    required this.notes,
    required this.createdAt,
  });

  factory ReferralTransaction.fromJson(ReferralJson json) =>
      ReferralTransaction(
        referralTransactionId:
            (json['referralTransactionId'] as num?)?.toInt() ?? 0,
        referrerCustomerId: (json['referrerCustomerId'] as num?)?.toInt() ?? 0,
        referredCustomerId: (json['referredCustomerId'] as num?)?.toInt(),
        referralCodeId: (json['referralCodeId'] as num?)?.toInt(),
        orderId: (json['orderId'] as num?)?.toInt(),
        referralRewardId: (json['referralRewardId'] as num?)?.toInt(),
        transactionType: json['transactionType'] as String? ?? 'UNKNOWN',
        fixedRewardAmount: _numAsDouble(json['fixedRewardAmount']),
        loyaltyPoints: _numAsDouble(json['loyaltyPoints']),
        notes: json['notes'] as String?,
        createdAt: _parseDate(json['createdAt']),
      );

  final int referralTransactionId;
  final int referrerCustomerId;
  final int? referredCustomerId;
  final int? referralCodeId;
  final int? orderId;
  final int? referralRewardId;
  final String transactionType;
  final double fixedRewardAmount;
  final double loyaltyPoints;
  final String? notes;
  final DateTime createdAt;
}

class ReferralRewardsData {
  const ReferralRewardsData({
    required this.grantedCount,
    required this.reversalCount,
    required this.totalGrantedPoints,
    required this.beneficiaryCount,
    required this.topBeneficiaries,
    required this.events,
  });

  factory ReferralRewardsData.fromJson(ReferralJson json) =>
      ReferralRewardsData(
        grantedCount: (json['grantedCount'] as num?)?.toInt() ?? 0,
        reversalCount: (json['reversalCount'] as num?)?.toInt() ?? 0,
        totalGrantedPoints: _numAsDouble(json['totalGrantedPoints']),
        beneficiaryCount: (json['beneficiaryCount'] as num?)?.toInt() ?? 0,
        topBeneficiaries: _mapList(
          json['topBeneficiaries'],
          ReferralRewardBeneficiary.fromJson,
        ),
        events: _mapList(json['events'], ReferralRewardEvent.fromJson),
      );

  final int grantedCount;
  final int reversalCount;
  final double totalGrantedPoints;
  final int beneficiaryCount;
  final List<ReferralRewardBeneficiary> topBeneficiaries;
  final List<ReferralRewardEvent> events;
}

class ReferralRewardBeneficiary {
  const ReferralRewardBeneficiary({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.totalPoints,
    required this.eventCount,
  });

  factory ReferralRewardBeneficiary.fromJson(ReferralJson json) =>
      ReferralRewardBeneficiary(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        totalPoints: _numAsDouble(json['totalPoints']),
        eventCount: (json['eventCount'] as num?)?.toInt() ?? 0,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final double totalPoints;
  final int eventCount;
}

class ReferralRewardEvent {
  const ReferralRewardEvent({
    required this.transactionId,
    required this.beneficiaryCustomerId,
    required this.beneficiaryCode,
    required this.beneficiaryName,
    required this.referredCustomerId,
    required this.referredCustomerName,
    required this.transactionType,
    required this.fixedRewardAmount,
    required this.loyaltyPoints,
    required this.orderId,
    required this.notes,
    required this.createdAt,
  });

  factory ReferralRewardEvent.fromJson(ReferralJson json) =>
      ReferralRewardEvent(
        transactionId: (json['transactionId'] as num?)?.toInt() ?? 0,
        beneficiaryCustomerId:
            (json['beneficiaryCustomerId'] as num?)?.toInt() ?? 0,
        beneficiaryCode: json['beneficiaryCode'] as String?,
        beneficiaryName: json['beneficiaryName'] as String?,
        referredCustomerId: (json['referredCustomerId'] as num?)?.toInt(),
        referredCustomerName: json['referredCustomerName'] as String?,
        transactionType: json['transactionType'] as String? ?? '',
        fixedRewardAmount: _numAsDouble(json['fixedRewardAmount']),
        loyaltyPoints: _numAsDouble(json['loyaltyPoints']),
        orderId: (json['orderId'] as num?)?.toInt(),
        notes: json['notes'] as String?,
        createdAt: _parseDate(json['createdAt']),
      );

  final int transactionId;
  final int beneficiaryCustomerId;
  final String? beneficiaryCode;
  final String? beneficiaryName;
  final int? referredCustomerId;
  final String? referredCustomerName;
  final String transactionType;
  final double fixedRewardAmount;
  final double loyaltyPoints;
  final int? orderId;
  final String? notes;
  final DateTime createdAt;
}

class ReferralDashboard {
  const ReferralDashboard({
    required this.totalRegistrations,
    required this.participatingCustomers,
    required this.rewardsGranted,
    required this.rewardReversals,
    required this.totalReferralPoints,
    required this.topReferrers,
    required this.topCodes,
    required this.recentEvents,
    required this.topReceivers,
    required this.treeSummary,
  });

  factory ReferralDashboard.fromJson(ReferralJson json) => ReferralDashboard(
        totalRegistrations: (json['totalRegistrations'] as num?)?.toInt() ?? 0,
        participatingCustomers:
            (json['participatingCustomers'] as num?)?.toInt() ?? 0,
        rewardsGranted: (json['rewardsGranted'] as num?)?.toInt() ?? 0,
        rewardReversals: (json['rewardReversals'] as num?)?.toInt() ?? 0,
        totalReferralPoints: _numAsDouble(json['totalReferralPoints']),
        topReferrers: _mapList(
          json['topReferrers'],
          ReferralDashboardReferrer.fromJson,
        ),
        topCodes: _mapList(json['topCodes'], ReferralDashboardCode.fromJson),
        recentEvents: _mapList(
          json['recentEvents'],
          ReferralDashboardEvent.fromJson,
        ),
        topReceivers: _mapList(
          json['topReceivers'],
          ReferralDashboardReceiver.fromJson,
        ),
        treeSummary: ReferralDashboardTreeSummary.fromJson(
          Map<String, dynamic>.from(json['treeSummary'] as Map),
        ),
      );

  final int totalRegistrations;
  final int participatingCustomers;
  final int rewardsGranted;
  final int rewardReversals;
  final double totalReferralPoints;
  final List<ReferralDashboardReferrer> topReferrers;
  final List<ReferralDashboardCode> topCodes;
  final List<ReferralDashboardEvent> recentEvents;
  final List<ReferralDashboardReceiver> topReceivers;
  final ReferralDashboardTreeSummary treeSummary;
}

List<T> _mapList<T>(dynamic value, T Function(ReferralJson json) fromJson) {
  if (value is! List) return const [];
  return value
      .map((item) => fromJson(Map<String, dynamic>.from(item as Map)))
      .toList();
}

class ReferralDashboardReferrer {
  const ReferralDashboardReferrer(
      {required this.customerId,
      required this.customerCode,
      required this.customerName,
      required this.referralCount});
  factory ReferralDashboardReferrer.fromJson(ReferralJson json) =>
      ReferralDashboardReferrer(
          customerId: (json['customerId'] as num?)?.toInt() ?? 0,
          customerCode: json['customerCode'] as String?,
          customerName: json['customerName'] as String?,
          referralCount: (json['referralCount'] as num?)?.toInt() ?? 0);
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int referralCount;
}

class ReferralDashboardCode {
  const ReferralDashboardCode(
      {required this.referralCodeId,
      required this.code,
      required this.customerId,
      required this.customerName,
      required this.usageCount});
  factory ReferralDashboardCode.fromJson(ReferralJson json) =>
      ReferralDashboardCode(
          referralCodeId: (json['referralCodeId'] as num?)?.toInt() ?? 0,
          code: json['code'] as String? ?? '',
          customerId: (json['customerId'] as num?)?.toInt() ?? 0,
          customerName: json['customerName'] as String?,
          usageCount: (json['usageCount'] as num?)?.toInt() ?? 0);
  final int referralCodeId;
  final String code;
  final int customerId;
  final String? customerName;
  final int usageCount;
}

class ReferralDashboardEvent {
  const ReferralDashboardEvent(
      {required this.transactionId,
      required this.transactionType,
      required this.referrerCustomerId,
      required this.referrerName,
      required this.referredCustomerId,
      required this.referredName,
      required this.referralCode,
      required this.fixedRewardAmount,
      required this.loyaltyPoints,
      required this.createdAt});
  factory ReferralDashboardEvent.fromJson(ReferralJson json) =>
      ReferralDashboardEvent(
          transactionId: (json['transactionId'] as num?)?.toInt() ?? 0,
          transactionType: json['transactionType'] as String? ?? '',
          referrerCustomerId:
              (json['referrerCustomerId'] as num?)?.toInt() ?? 0,
          referrerName: json['referrerName'] as String?,
          referredCustomerId: (json['referredCustomerId'] as num?)?.toInt(),
          referredName: json['referredName'] as String?,
          referralCode: json['referralCode'] as String?,
          fixedRewardAmount: _numAsDouble(json['fixedRewardAmount']),
          loyaltyPoints: _numAsDouble(json['loyaltyPoints']),
          createdAt: _parseDate(json['createdAt']));
  final int transactionId;
  final String transactionType;
  final int referrerCustomerId;
  final String? referrerName;
  final int? referredCustomerId;
  final String? referredName;
  final String? referralCode;
  final double fixedRewardAmount;
  final double loyaltyPoints;
  final DateTime createdAt;
}

class ReferralDashboardReceiver {
  const ReferralDashboardReceiver(
      {required this.customerId,
      required this.customerCode,
      required this.customerName,
      required this.referralCount});
  factory ReferralDashboardReceiver.fromJson(ReferralJson json) =>
      ReferralDashboardReceiver(
          customerId: (json['customerId'] as num?)?.toInt() ?? 0,
          customerCode: json['customerCode'] as String?,
          customerName: json['customerName'] as String?,
          referralCount: (json['referralCount'] as num?)?.toInt() ?? 0);
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int referralCount;
}

class ReferralDashboardTreeSummary {
  const ReferralDashboardTreeSummary(
      {required this.rootCount,
      required this.maxDepth,
      required this.maxDirectReferrals});
  factory ReferralDashboardTreeSummary.fromJson(ReferralJson json) =>
      ReferralDashboardTreeSummary(
          rootCount: (json['rootCount'] as num?)?.toInt() ?? 0,
          maxDepth: (json['maxDepth'] as num?)?.toInt() ?? 0,
          maxDirectReferrals:
              (json['maxDirectReferrals'] as num?)?.toInt() ?? 0);
  final int rootCount;
  final int maxDepth;
  final int maxDirectReferrals;
}

class ReferralDashboardSearchResult {
  const ReferralDashboardSearchResult(
      {required this.customerId,
      required this.customerCode,
      required this.customerName,
      required this.referralCode,
      required this.resultType});
  factory ReferralDashboardSearchResult.fromJson(ReferralJson json) =>
      ReferralDashboardSearchResult(
          customerId: (json['customerId'] as num?)?.toInt() ?? 0,
          customerCode: json['customerCode'] as String?,
          customerName: json['customerName'] as String?,
          referralCode: json['referralCode'] as String?,
          resultType: json['resultType'] as String? ?? 'customer');
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final String? referralCode;
  final String resultType;
}

class ReferralAnalyticsData {
  const ReferralAnalyticsData({
    required this.totalRegistrations,
    required this.totalReferredCustomers,
    required this.rewardsGranted,
    required this.totalReferralPoints,
    required this.averageReferralsPerCustomer,
    required this.topReferrers,
    required this.topCodes,
    required this.topRewardCustomers,
    required this.quality,
    required this.activity,
    required this.tree,
  });

  factory ReferralAnalyticsData.fromJson(ReferralJson json) =>
      ReferralAnalyticsData(
        totalRegistrations: (json['totalRegistrations'] as num?)?.toInt() ?? 0,
        totalReferredCustomers:
            (json['totalReferredCustomers'] as num?)?.toInt() ?? 0,
        rewardsGranted: (json['rewardsGranted'] as num?)?.toInt() ?? 0,
        totalReferralPoints: _numAsDouble(json['totalReferralPoints']),
        averageReferralsPerCustomer:
            _numAsDouble(json['averageReferralsPerCustomer']),
        topReferrers: _mapList(
          json['topReferrers'],
          ReferralAnalyticsReferrer.fromJson,
        ),
        topCodes: _mapList(
          json['topCodes'],
          ReferralAnalyticsCode.fromJson,
        ),
        topRewardCustomers: _mapList(
          json['topRewardCustomers'],
          ReferralAnalyticsRewardCustomer.fromJson,
        ),
        quality: ReferralAnalyticsQuality.fromJson(
          Map<String, dynamic>.from(json['quality'] as Map),
        ),
        activity: ReferralAnalyticsActivity.fromJson(
          Map<String, dynamic>.from(json['activity'] as Map),
        ),
        tree: ReferralAnalyticsTree.fromJson(
          Map<String, dynamic>.from(json['tree'] as Map),
        ),
      );

  final int totalRegistrations;
  final int totalReferredCustomers;
  final int rewardsGranted;
  final double totalReferralPoints;
  final double averageReferralsPerCustomer;
  final List<ReferralAnalyticsReferrer> topReferrers;
  final List<ReferralAnalyticsCode> topCodes;
  final List<ReferralAnalyticsRewardCustomer> topRewardCustomers;
  final ReferralAnalyticsQuality quality;
  final ReferralAnalyticsActivity activity;
  final ReferralAnalyticsTree tree;
}

class ReferralAnalyticsReferrer {
  const ReferralAnalyticsReferrer({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.referralCount,
  });

  factory ReferralAnalyticsReferrer.fromJson(ReferralJson json) =>
      ReferralAnalyticsReferrer(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        referralCount: (json['referralCount'] as num?)?.toInt() ?? 0,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int referralCount;
}

class ReferralAnalyticsCode {
  const ReferralAnalyticsCode({
    required this.referralCodeId,
    required this.code,
    required this.customerId,
    required this.customerName,
    required this.usageCount,
  });

  factory ReferralAnalyticsCode.fromJson(ReferralJson json) =>
      ReferralAnalyticsCode(
        referralCodeId: (json['referralCodeId'] as num?)?.toInt() ?? 0,
        code: json['code'] as String? ?? '',
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerName: json['customerName'] as String?,
        usageCount: (json['usageCount'] as num?)?.toInt() ?? 0,
      );

  final int referralCodeId;
  final String code;
  final int customerId;
  final String? customerName;
  final int usageCount;
}

class ReferralAnalyticsRewardCustomer {
  const ReferralAnalyticsRewardCustomer({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.totalPoints,
    required this.rewardCount,
  });

  factory ReferralAnalyticsRewardCustomer.fromJson(ReferralJson json) =>
      ReferralAnalyticsRewardCustomer(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        totalPoints: _numAsDouble(json['totalPoints']),
        rewardCount: (json['rewardCount'] as num?)?.toInt() ?? 0,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final double totalPoints;
  final int rewardCount;
}

class ReferralAnalyticsQuality {
  const ReferralAnalyticsQuality({
    required this.averageUsagePerCode,
    required this.referredCustomersRate,
    required this.rewardsToRegistrationsRate,
    required this.reversalsToRewardsRate,
    required this.rewardReversals,
  });

  factory ReferralAnalyticsQuality.fromJson(ReferralJson json) =>
      ReferralAnalyticsQuality(
        averageUsagePerCode: _numAsDouble(json['averageUsagePerCode']),
        referredCustomersRate: _numAsDouble(json['referredCustomersRate']),
        rewardsToRegistrationsRate:
            _numAsDouble(json['rewardsToRegistrationsRate']),
        reversalsToRewardsRate: _numAsDouble(json['reversalsToRewardsRate']),
        rewardReversals: (json['rewardReversals'] as num?)?.toInt() ?? 0,
      );

  final double averageUsagePerCode;
  final double referredCustomersRate;
  final double rewardsToRegistrationsRate;
  final double reversalsToRewardsRate;
  final int rewardReversals;
}

class ReferralAnalyticsActivity {
  const ReferralAnalyticsActivity({
    required this.todayRegistrations,
    required this.thisWeekRegistrations,
    required this.thisMonthRegistrations,
    required this.dailyRegistrations,
  });

  factory ReferralAnalyticsActivity.fromJson(ReferralJson json) =>
      ReferralAnalyticsActivity(
        todayRegistrations: (json['todayRegistrations'] as num?)?.toInt() ?? 0,
        thisWeekRegistrations:
            (json['thisWeekRegistrations'] as num?)?.toInt() ?? 0,
        thisMonthRegistrations:
            (json['thisMonthRegistrations'] as num?)?.toInt() ?? 0,
        dailyRegistrations: _mapList(
          json['dailyRegistrations'],
          ReferralAnalyticsPeriod.fromJson,
        ),
      );

  final int todayRegistrations;
  final int thisWeekRegistrations;
  final int thisMonthRegistrations;
  final List<ReferralAnalyticsPeriod> dailyRegistrations;
}

class ReferralAnalyticsPeriod {
  const ReferralAnalyticsPeriod({
    required this.period,
    required this.registrationCount,
  });

  factory ReferralAnalyticsPeriod.fromJson(ReferralJson json) =>
      ReferralAnalyticsPeriod(
        period: _parseDate(json['period']),
        registrationCount: (json['registrationCount'] as num?)?.toInt() ?? 0,
      );

  final DateTime period;
  final int registrationCount;
}

class ReferralAnalyticsTree {
  const ReferralAnalyticsTree({
    required this.rootCount,
    required this.maxDepth,
    required this.largestNetworkSize,
    required this.largestNetworkCustomerId,
    required this.largestNetworkCustomerName,
  });

  factory ReferralAnalyticsTree.fromJson(ReferralJson json) =>
      ReferralAnalyticsTree(
        rootCount: (json['rootCount'] as num?)?.toInt() ?? 0,
        maxDepth: (json['maxDepth'] as num?)?.toInt() ?? 0,
        largestNetworkSize: (json['largestNetworkSize'] as num?)?.toInt() ?? 0,
        largestNetworkCustomerId:
            (json['largestNetworkCustomerId'] as num?)?.toInt(),
        largestNetworkCustomerName:
            json['largestNetworkCustomerName'] as String?,
      );

  final int rootCount;
  final int maxDepth;
  final int largestNetworkSize;
  final int? largestNetworkCustomerId;
  final String? largestNetworkCustomerName;
}

class ReferralTreeNode {
  const ReferralTreeNode({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.parentCustomerId,
    required this.level,
    required this.children,
    required this.directChildrenCount,
    required this.totalDescendantsCount,
    required this.maxDepth,
    required this.referralCode,
    required this.isActive,
  });

  factory ReferralTreeNode.fromJson(ReferralJson json) => ReferralTreeNode(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        parentCustomerId: (json['parentCustomerId'] as num?)?.toInt(),
        level: (json['level'] as num?)?.toInt() ?? 0,
        children: ((json['children'] as List?) ?? const [])
            .map((e) => ReferralTreeNode.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
        directChildrenCount:
            (json['directChildrenCount'] as num?)?.toInt() ?? 0,
        totalDescendantsCount:
            (json['totalDescendantsCount'] as num?)?.toInt() ?? 0,
        maxDepth: (json['maxDepth'] as num?)?.toInt() ?? 0,
        referralCode: json['referralCode'] as String?,
        isActive: json['isActive'] as bool? ?? true,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int? parentCustomerId;
  final int level;
  final List<ReferralTreeNode> children;
  final int directChildrenCount;
  final int totalDescendantsCount;
  final int maxDepth;
  final String? referralCode;
  final bool isActive;
}

class ReferralRoot {
  const ReferralRoot({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.directReferralsCount,
    required this.totalDescendantsCount,
    required this.maxDepth,
  });

  factory ReferralRoot.fromJson(ReferralJson json) => ReferralRoot(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        directReferralsCount:
            (json['directReferralsCount'] as num?)?.toInt() ?? 0,
        totalDescendantsCount:
            (json['totalDescendantsCount'] as num?)?.toInt() ?? 0,
        maxDepth: (json['maxDepth'] as num?)?.toInt() ?? 0,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int directReferralsCount;
  final int totalDescendantsCount;
  final int maxDepth;
}

class ReferralTree {
  const ReferralTree({
    required this.rootCustomerId,
    required this.rootCustomerCode,
    required this.rootCustomerName,
    required this.directReferralsCount,
    required this.totalDescendantsCount,
    required this.maxDepth,
    required this.children,
  });

  factory ReferralTree.fromJson(ReferralJson json) => ReferralTree(
        rootCustomerId: (json['rootCustomerId'] as num?)?.toInt() ?? 0,
        rootCustomerCode: json['rootCustomerCode'] as String?,
        rootCustomerName: json['rootCustomerName'] as String?,
        directReferralsCount:
            (json['directReferralsCount'] as num?)?.toInt() ?? 0,
        totalDescendantsCount:
            (json['totalDescendantsCount'] as num?)?.toInt() ?? 0,
        maxDepth: (json['maxDepth'] as num?)?.toInt() ?? 0,
        children: ((json['children'] as List?) ?? const [])
            .map((e) => ReferralTreeNode.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
      );

  final int rootCustomerId;
  final String? rootCustomerCode;
  final String? rootCustomerName;
  final int directReferralsCount;
  final int totalDescendantsCount;
  final int maxDepth;
  final List<ReferralTreeNode> children;
}

class ReferralReward {
  const ReferralReward({
    required this.referralRewardId,
    required this.rewardCode,
    required this.rewardName,
    required this.rewardType,
    required this.fixedAmount,
    required this.loyaltyPoints,
    required this.bonusMultiplier,
    required this.minSuccessfulReferrals,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  factory ReferralReward.fromJson(ReferralJson json) => ReferralReward(
        referralRewardId: (json['referralRewardId'] as num?)?.toInt() ?? 0,
        rewardCode: json['rewardCode'] as String? ?? '',
        rewardName: json['rewardName'] as String? ?? '',
        rewardType: json['rewardType'] as String? ?? '',
        fixedAmount: _numAsDouble(json['fixedAmount']),
        loyaltyPoints: _numAsDouble(json['loyaltyPoints']),
        bonusMultiplier: _numAsDouble(json['bonusMultiplier']),
        minSuccessfulReferrals:
            (json['minSuccessfulReferrals'] as num?)?.toInt() ?? 0,
        isActive: json['isActive'] as bool? ?? true,
        createdAt: _parseDate(json['createdAt']),
        updatedAt: _parseDate(json['updatedAt']),
      );

  final int referralRewardId;
  final String rewardCode;
  final String rewardName;
  final String rewardType;
  final double fixedAmount;
  final double loyaltyPoints;
  final double bonusMultiplier;
  final int minSuccessfulReferrals;
  final bool isActive;
  final DateTime createdAt;
  final DateTime updatedAt;
}
