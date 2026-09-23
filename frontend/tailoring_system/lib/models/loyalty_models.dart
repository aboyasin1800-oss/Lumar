typedef LoyaltyJson = Map<String, dynamic>;

DateTime _parseLoyaltyDate(dynamic value) {
  if (value == null) return DateTime.now().toUtc();
  return DateTime.parse(value.toString());
}

double _asDouble(dynamic value, {double fallback = 0}) {
  if (value == null) return fallback;
  if (value is num) return value.toDouble();
  return double.tryParse(value.toString()) ?? fallback;
}

double? _nullableDouble(dynamic value) =>
    value == null ? null : _asDouble(value);

class LoyaltyDashboardData {
  const LoyaltyDashboardData({
    required this.accountCount,
    required this.currentPointsTotal,
    required this.earnedPointsTotal,
    required this.redeemedPointsTotal,
    required this.reversedPointsTotal,
    required this.adjustedPointsTotal,
    required this.activeCustomerCount,
    required this.topCustomersByBalance,
    required this.topCustomersByEarnedPoints,
    required this.vipLevels,
    required this.recentActivities,
    required this.programSummary,
  });

  factory LoyaltyDashboardData.fromJson(LoyaltyJson json) =>
      LoyaltyDashboardData(
        accountCount: (json['accountCount'] as num?)?.toInt() ?? 0,
        currentPointsTotal: _asDouble(json['currentPointsTotal']),
        earnedPointsTotal: _asDouble(json['earnedPointsTotal']),
        redeemedPointsTotal: _asDouble(json['redeemedPointsTotal']),
        reversedPointsTotal: _asDouble(json['reversedPointsTotal']),
        adjustedPointsTotal: _asDouble(json['adjustedPointsTotal']),
        activeCustomerCount:
            (json['activeCustomerCount'] as num?)?.toInt() ?? 0,
        topCustomersByBalance: _loyaltyList(
          json['topCustomersByBalance'],
          LoyaltyTopCustomer.fromJson,
        ),
        topCustomersByEarnedPoints: _loyaltyList(
          json['topCustomersByEarnedPoints'],
          LoyaltyTopCustomer.fromJson,
        ),
        vipLevels: _loyaltyList(
          json['vipLevels'],
          LoyaltyVipSummary.fromJson,
        ),
        recentActivities: _loyaltyList(
          json['recentActivities'],
          LoyaltyActivity.fromJson,
        ),
        programSummary: LoyaltyProgramSummary.fromJson(
          Map<String, dynamic>.from(json['programSummary'] as Map),
        ),
      );

  final int accountCount;
  final double currentPointsTotal;
  final double earnedPointsTotal;
  final double redeemedPointsTotal;
  final double reversedPointsTotal;
  final double adjustedPointsTotal;
  final int activeCustomerCount;
  final List<LoyaltyTopCustomer> topCustomersByBalance;
  final List<LoyaltyTopCustomer> topCustomersByEarnedPoints;
  final List<LoyaltyVipSummary> vipLevels;
  final List<LoyaltyActivity> recentActivities;
  final LoyaltyProgramSummary programSummary;
}

class LoyaltyCustomerSearchResult {
  const LoyaltyCustomerSearchResult({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.phoneNumber,
  });

  factory LoyaltyCustomerSearchResult.fromJson(LoyaltyJson json) =>
      LoyaltyCustomerSearchResult(
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

List<T> _loyaltyList<T>(dynamic value, T Function(LoyaltyJson) fromJson) {
  if (value is! List) return const [];
  return value
      .map((item) => fromJson(Map<String, dynamic>.from(item as Map)))
      .toList();
}

class LoyaltyTopCustomer {
  const LoyaltyTopCustomer({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.points,
    required this.transactionCount,
  });

  factory LoyaltyTopCustomer.fromJson(LoyaltyJson json) => LoyaltyTopCustomer(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        points: _asDouble(json['points']),
        transactionCount: (json['transactionCount'] as num?)?.toInt() ?? 0,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final double points;
  final int transactionCount;
}

class LoyaltyVipSummary {
  const LoyaltyVipSummary({
    required this.vipLevelId,
    required this.displayName,
    required this.customerCount,
    required this.minimumPoints,
  });

  factory LoyaltyVipSummary.fromJson(LoyaltyJson json) => LoyaltyVipSummary(
        vipLevelId: (json['vipLevelId'] as num?)?.toInt() ?? 0,
        displayName: json['displayName'] as String?,
        customerCount: (json['customerCount'] as num?)?.toInt() ?? 0,
        minimumPoints: _asDouble(json['minimumPoints']),
      );

  final int vipLevelId;
  final String? displayName;
  final int customerCount;
  final double minimumPoints;
}

class LoyaltyActivity {
  const LoyaltyActivity({
    required this.loyaltyTransactionId,
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.transactionType,
    required this.points,
    required this.orderId,
    required this.notes,
    required this.createdAt,
  });

  factory LoyaltyActivity.fromJson(LoyaltyJson json) => LoyaltyActivity(
        loyaltyTransactionId:
            (json['loyaltyTransactionId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        transactionType: json['transactionType'] as String? ?? '',
        points: _asDouble(json['points']),
        orderId: (json['orderId'] as num?)?.toInt(),
        notes: json['notes'] as String?,
        createdAt: _parseLoyaltyDate(json['createdAt']),
      );

  final int loyaltyTransactionId;
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final String transactionType;
  final double points;
  final int? orderId;
  final String? notes;
  final DateTime createdAt;
}

class LoyaltyProgramSummary {
  const LoyaltyProgramSummary({
    required this.accountCount,
    required this.transactionCount,
    required this.redemptionCount,
    required this.currentPointsTotal,
    required this.earnCount,
    required this.redeemCount,
    required this.reversalCount,
    required this.adjustCount,
  });

  factory LoyaltyProgramSummary.fromJson(LoyaltyJson json) =>
      LoyaltyProgramSummary(
        accountCount: (json['accountCount'] as num?)?.toInt() ?? 0,
        transactionCount: (json['transactionCount'] as num?)?.toInt() ?? 0,
        redemptionCount: (json['redemptionCount'] as num?)?.toInt() ?? 0,
        currentPointsTotal: _asDouble(json['currentPointsTotal']),
        earnCount: (json['earnCount'] as num?)?.toInt() ?? 0,
        redeemCount: (json['redeemCount'] as num?)?.toInt() ?? 0,
        reversalCount: (json['reversalCount'] as num?)?.toInt() ?? 0,
        adjustCount: (json['adjustCount'] as num?)?.toInt() ?? 0,
      );

  final int accountCount;
  final int transactionCount;
  final int redemptionCount;
  final double currentPointsTotal;
  final int earnCount;
  final int redeemCount;
  final int reversalCount;
  final int adjustCount;
}

class LoyaltyAccount {
  const LoyaltyAccount({
    required this.loyaltyAccountId,
    required this.customerId,
    required this.currentPoints,
    required this.lifetimeEarnedPoints,
    required this.lifetimeRedeemedPoints,
    required this.pendingExpirePoints,
    required this.vipLevelId,
    required this.createdAt,
    required this.updatedAt,
    required this.lastActivityAt,
    this.loyaltyAccountStatus = 'Active',
    this.warningStartedAtUtc,
    this.frozenAtUtc,
    this.reactivatedAtUtc,
    this.freezeReason,
    this.lastQualifyingActivityAtUtc,
  });

  factory LoyaltyAccount.fromJson(LoyaltyJson json) => LoyaltyAccount(
        loyaltyAccountId: (json['loyaltyAccountId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        currentPoints: _asDouble(json['currentPoints']),
        lifetimeEarnedPoints: _asDouble(json['lifetimeEarnedPoints']),
        lifetimeRedeemedPoints: _asDouble(json['lifetimeRedeemedPoints']),
        pendingExpirePoints: _asDouble(json['pendingExpirePoints']),
        vipLevelId: (json['vipLevelId'] as num?)?.toInt(),
        createdAt: _parseLoyaltyDate(json['createdAt']),
        updatedAt: _parseLoyaltyDate(json['updatedAt']),
        lastActivityAt: json['lastActivityAt'] == null
            ? null
            : _parseLoyaltyDate(json['lastActivityAt']),
        loyaltyAccountStatus:
            json['loyaltyAccountStatus'] as String? ?? 'Active',
        warningStartedAtUtc: json['warningStartedAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['warningStartedAtUtc']),
        frozenAtUtc: json['frozenAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['frozenAtUtc']),
        reactivatedAtUtc: json['reactivatedAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['reactivatedAtUtc']),
        freezeReason: json['freezeReason'] as String?,
        lastQualifyingActivityAtUtc: json['lastQualifyingActivityAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['lastQualifyingActivityAtUtc']),
      );

  final int loyaltyAccountId;
  final int customerId;
  final double currentPoints;
  final double lifetimeEarnedPoints;
  final double lifetimeRedeemedPoints;
  final double pendingExpirePoints;
  final int? vipLevelId;
  final DateTime createdAt;
  final DateTime updatedAt;
  final DateTime? lastActivityAt;
  final String loyaltyAccountStatus;
  final DateTime? warningStartedAtUtc;
  final DateTime? frozenAtUtc;
  final DateTime? reactivatedAtUtc;
  final String? freezeReason;
  final DateTime? lastQualifyingActivityAtUtc;
}

class LoyaltyTransaction {
  const LoyaltyTransaction({
    required this.loyaltyTransactionId,
    required this.loyaltyAccountId,
    required this.customerId,
    required this.orderId,
    required this.rewardId,
    required this.transactionType,
    required this.points,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.source,
    required this.notes,
    required this.createdAt,
  });

  factory LoyaltyTransaction.fromJson(LoyaltyJson json) => LoyaltyTransaction(
        loyaltyTransactionId:
            (json['loyaltyTransactionId'] as num?)?.toInt() ?? 0,
        loyaltyAccountId: (json['loyaltyAccountId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        orderId: (json['orderId'] as num?)?.toInt(),
        rewardId: (json['rewardId'] as num?)?.toInt(),
        transactionType: json['transactionType'] as String? ?? '',
        points: _asDouble(json['points']),
        balanceBefore: _asDouble(json['balanceBefore']),
        balanceAfter: _asDouble(json['balanceAfter']),
        source: json['source'] as String? ?? '',
        notes: json['notes'] as String?,
        createdAt: _parseLoyaltyDate(json['createdAt']),
      );

  final int loyaltyTransactionId;
  final int loyaltyAccountId;
  final int customerId;
  final int? orderId;
  final int? rewardId;
  final String transactionType;
  final double points;
  final double balanceBefore;
  final double balanceAfter;
  final String source;
  final String? notes;
  final DateTime createdAt;
}

class LoyaltyTransactionsScreenData {
  const LoyaltyTransactionsScreenData({
    required this.summary,
    required this.transactions,
  });

  factory LoyaltyTransactionsScreenData.fromJson(LoyaltyJson json) {
    final summaryJson = Map<String, dynamic>.from(json['summary'] as Map);
    final transactionJson = json['transactions'] as List? ?? const [];
    return LoyaltyTransactionsScreenData(
      summary: LoyaltyTransactionsSummary.fromJson(summaryJson),
      transactions: transactionJson
          .map((item) => LoyaltyTransactionListItem.fromJson(
                Map<String, dynamic>.from(item as Map),
              ))
          .toList(),
    );
  }

  final LoyaltyTransactionsSummary summary;
  final List<LoyaltyTransactionListItem> transactions;
}

class LoyaltyRewardsScreenData {
  const LoyaltyRewardsScreenData({
    required this.summary,
    required this.topCustomers,
    required this.largestRewards,
    required this.rewards,
  });

  factory LoyaltyRewardsScreenData.fromJson(LoyaltyJson json) =>
      LoyaltyRewardsScreenData(
        summary: LoyaltyRewardsSummary.fromJson(
          Map<String, dynamic>.from(json['summary'] as Map),
        ),
        topCustomers: _loyaltyList(
          json['topCustomers'],
          LoyaltyRewardCustomer.fromJson,
        ),
        largestRewards: _loyaltyList(
          json['largestRewards'],
          LoyaltyRewardItem.fromJson,
        ),
        rewards: _loyaltyList(json['rewards'], LoyaltyRewardItem.fromJson),
      );

  final LoyaltyRewardsSummary summary;
  final List<LoyaltyRewardCustomer> topCustomers;
  final List<LoyaltyRewardItem> largestRewards;
  final List<LoyaltyRewardItem> rewards;
}

class LoyaltyRewardsSummary {
  const LoyaltyRewardsSummary({
    required this.rewardCount,
    required this.grantedPointsTotal,
    required this.beneficiaryCustomerCount,
    required this.averageRewardPoints,
    required this.largestRewardPoints,
  });

  factory LoyaltyRewardsSummary.fromJson(LoyaltyJson json) =>
      LoyaltyRewardsSummary(
        rewardCount: (json['rewardCount'] as num?)?.toInt() ?? 0,
        grantedPointsTotal: _asDouble(json['grantedPointsTotal']),
        beneficiaryCustomerCount:
            (json['beneficiaryCustomerCount'] as num?)?.toInt() ?? 0,
        averageRewardPoints: _asDouble(json['averageRewardPoints']),
        largestRewardPoints: _asDouble(json['largestRewardPoints']),
      );

  final int rewardCount;
  final double grantedPointsTotal;
  final int beneficiaryCustomerCount;
  final double averageRewardPoints;
  final double largestRewardPoints;
}

class LoyaltyRewardCustomer {
  const LoyaltyRewardCustomer({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.grantedPoints,
    required this.rewardCount,
  });

  factory LoyaltyRewardCustomer.fromJson(LoyaltyJson json) =>
      LoyaltyRewardCustomer(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        grantedPoints: _asDouble(json['grantedPoints']),
        rewardCount: (json['rewardCount'] as num?)?.toInt() ?? 0,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final double grantedPoints;
  final int rewardCount;
}

class LoyaltyRewardItem {
  const LoyaltyRewardItem({
    required this.loyaltyTransactionId,
    required this.loyaltyAccountId,
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.orderId,
    required this.rewardId,
    required this.points,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.source,
    required this.notes,
    required this.createdAt,
    required this.isReversed,
  });

  factory LoyaltyRewardItem.fromJson(LoyaltyJson json) => LoyaltyRewardItem(
        loyaltyTransactionId:
            (json['loyaltyTransactionId'] as num?)?.toInt() ?? 0,
        loyaltyAccountId: (json['loyaltyAccountId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        orderId: (json['orderId'] as num?)?.toInt(),
        rewardId: (json['rewardId'] as num?)?.toInt(),
        points: _asDouble(json['points']),
        balanceBefore: _asDouble(json['balanceBefore']),
        balanceAfter: _asDouble(json['balanceAfter']),
        source: json['source'] as String? ?? '',
        notes: json['notes'] as String?,
        createdAt: _parseLoyaltyDate(json['createdAt']),
        isReversed: json['isReversed'] as bool? ?? false,
      );

  final int loyaltyTransactionId;
  final int loyaltyAccountId;
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int? orderId;
  final int? rewardId;
  final double points;
  final double balanceBefore;
  final double balanceAfter;
  final String source;
  final String? notes;
  final DateTime createdAt;
  final bool isReversed;
}

class LoyaltyTransactionsSummary {
  const LoyaltyTransactionsSummary({
    required this.transactionCount,
    required this.earnedPointsTotal,
    required this.redeemedPointsTotal,
    required this.reversedPointsTotal,
    required this.adjustCount,
    required this.activeCustomerCount,
    required this.types,
  });

  factory LoyaltyTransactionsSummary.fromJson(LoyaltyJson json) =>
      LoyaltyTransactionsSummary(
        transactionCount: (json['transactionCount'] as num?)?.toInt() ?? 0,
        earnedPointsTotal: _asDouble(json['earnedPointsTotal']),
        redeemedPointsTotal: _asDouble(json['redeemedPointsTotal']),
        reversedPointsTotal: _asDouble(json['reversedPointsTotal']),
        adjustCount: (json['adjustCount'] as num?)?.toInt() ?? 0,
        activeCustomerCount:
            (json['activeCustomerCount'] as num?)?.toInt() ?? 0,
        types: _loyaltyList(
          json['types'],
          LoyaltyTransactionTypeSummary.fromJson,
        ),
      );

  final int transactionCount;
  final double earnedPointsTotal;
  final double redeemedPointsTotal;
  final double reversedPointsTotal;
  final int adjustCount;
  final int activeCustomerCount;
  final List<LoyaltyTransactionTypeSummary> types;
}

class LoyaltyTransactionTypeSummary {
  const LoyaltyTransactionTypeSummary({
    required this.transactionType,
    required this.transactionCount,
    required this.pointsTotal,
  });

  factory LoyaltyTransactionTypeSummary.fromJson(LoyaltyJson json) =>
      LoyaltyTransactionTypeSummary(
        transactionType: json['transactionType'] as String? ?? '',
        transactionCount: (json['transactionCount'] as num?)?.toInt() ?? 0,
        pointsTotal: _asDouble(json['pointsTotal']),
      );

  final String transactionType;
  final int transactionCount;
  final double pointsTotal;
}

class LoyaltyTransactionListItem {
  const LoyaltyTransactionListItem({
    required this.loyaltyTransactionId,
    required this.loyaltyAccountId,
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.orderId,
    required this.rewardId,
    required this.transactionType,
    required this.points,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.source,
    required this.notes,
    required this.createdAt,
  });

  factory LoyaltyTransactionListItem.fromJson(LoyaltyJson json) =>
      LoyaltyTransactionListItem(
        loyaltyTransactionId:
            (json['loyaltyTransactionId'] as num?)?.toInt() ?? 0,
        loyaltyAccountId: (json['loyaltyAccountId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        orderId: (json['orderId'] as num?)?.toInt(),
        rewardId: (json['rewardId'] as num?)?.toInt(),
        transactionType: json['transactionType'] as String? ?? '',
        points: _asDouble(json['points']),
        balanceBefore: _asDouble(json['balanceBefore']),
        balanceAfter: _asDouble(json['balanceAfter']),
        source: json['source'] as String? ?? '',
        notes: json['notes'] as String?,
        createdAt: _parseLoyaltyDate(json['createdAt']),
      );

  final int loyaltyTransactionId;
  final int loyaltyAccountId;
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int? orderId;
  final int? rewardId;
  final String transactionType;
  final double points;
  final double balanceBefore;
  final double balanceAfter;
  final String source;
  final String? notes;
  final DateTime createdAt;
}

class LoyaltyBalance {
  const LoyaltyBalance({
    required this.loyaltyAccountId,
    required this.customerId,
    required this.currentPoints,
    required this.lifetimeEarnedPoints,
    required this.lifetimeRedeemedPoints,
    required this.pendingExpirePoints,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.vipLevelId,
    required this.updatedAt,
    required this.lastActivityAt,
    this.loyaltyAccountStatus = 'Active',
    this.warningStartedAtUtc,
    this.frozenAtUtc,
    this.reactivatedAtUtc,
    this.freezeReason,
    this.lastQualifyingActivityAtUtc,
  });

  factory LoyaltyBalance.fromJson(LoyaltyJson json) => LoyaltyBalance(
        loyaltyAccountId: (json['loyaltyAccountId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        currentPoints: _asDouble(json['currentPoints']),
        lifetimeEarnedPoints: _asDouble(json['lifetimeEarnedPoints']),
        lifetimeRedeemedPoints: _asDouble(json['lifetimeRedeemedPoints']),
        pendingExpirePoints: _asDouble(json['pendingExpirePoints']),
        balanceBefore: _asDouble(json['balanceBefore']),
        balanceAfter: _asDouble(json['balanceAfter']),
        vipLevelId: (json['vipLevelId'] as num?)?.toInt(),
        updatedAt: _parseLoyaltyDate(json['updatedAt']),
        lastActivityAt: json['lastActivityAt'] == null
            ? null
            : _parseLoyaltyDate(json['lastActivityAt']),
        loyaltyAccountStatus:
            json['loyaltyAccountStatus'] as String? ?? 'Active',
        warningStartedAtUtc: json['warningStartedAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['warningStartedAtUtc']),
        frozenAtUtc: json['frozenAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['frozenAtUtc']),
        reactivatedAtUtc: json['reactivatedAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['reactivatedAtUtc']),
        freezeReason: json['freezeReason'] as String?,
        lastQualifyingActivityAtUtc: json['lastQualifyingActivityAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['lastQualifyingActivityAtUtc']),
      );

  final int loyaltyAccountId;
  final int customerId;
  final double currentPoints;
  final double lifetimeEarnedPoints;
  final double lifetimeRedeemedPoints;
  final double pendingExpirePoints;
  final double balanceBefore;
  final double balanceAfter;
  final int? vipLevelId;
  final DateTime updatedAt;
  final DateTime? lastActivityAt;
  final String loyaltyAccountStatus;
  final DateTime? warningStartedAtUtc;
  final DateTime? frozenAtUtc;
  final DateTime? reactivatedAtUtc;
  final String? freezeReason;
  final DateTime? lastQualifyingActivityAtUtc;
}

class LoyaltyProgramSettings {
  const LoyaltyProgramSettings({
    required this.loyaltyProgramSettingId,
    required this.isEnabled,
    required this.pointsPerPiece,
    required this.pointMonetaryValue,
    required this.effectiveFromUtc,
    required this.createdAt,
    required this.updatedAt,
    this.allowRedemption = true,
    this.minimumRedemptionPoints = 0,
    this.maximumRedemptionPoints = 0,
    this.loyaltyFreezeEnabled = false,
    this.gracePeriodDays = 180,
    this.warningPeriodDays = 30,
    this.manualReactivationEnabled = true,
    this.purchaseReactivationEnabled = true,
  });

  factory LoyaltyProgramSettings.fromJson(LoyaltyJson json) =>
      LoyaltyProgramSettings(
        loyaltyProgramSettingId:
            (json['loyaltyProgramSettingId'] as num?)?.toInt() ?? 0,
        isEnabled: json['isEnabled'] as bool? ?? false,
        pointsPerPiece: _asDouble(json['pointsPerPiece']),
        pointMonetaryValue: _asDouble(json['pointMonetaryValue']),
        effectiveFromUtc: _parseLoyaltyDate(json['effectiveFromUtc']),
        createdAt: _parseLoyaltyDate(json['createdAt']),
        updatedAt: _parseLoyaltyDate(json['updatedAt']),
        allowRedemption: json['allowRedemption'] as bool? ?? true,
        minimumRedemptionPoints: _asDouble(json['minimumRedemptionPoints']),
        maximumRedemptionPoints: _asDouble(json['maximumRedemptionPoints']),
        loyaltyFreezeEnabled: json['loyaltyFreezeEnabled'] as bool? ?? false,
        gracePeriodDays: (json['gracePeriodDays'] as num?)?.toInt() ?? 180,
        warningPeriodDays: (json['warningPeriodDays'] as num?)?.toInt() ?? 30,
        manualReactivationEnabled:
            json['manualReactivationEnabled'] as bool? ?? true,
        purchaseReactivationEnabled:
            json['purchaseReactivationEnabled'] as bool? ?? true,
      );

  final int loyaltyProgramSettingId;
  final bool isEnabled;
  final double pointsPerPiece;
  final double pointMonetaryValue;
  final DateTime effectiveFromUtc;
  final DateTime createdAt;
  final DateTime updatedAt;
  final bool allowRedemption;
  final double minimumRedemptionPoints;
  final double maximumRedemptionPoints;
  final bool loyaltyFreezeEnabled;
  final int gracePeriodDays;
  final int warningPeriodDays;
  final bool manualReactivationEnabled;
  final bool purchaseReactivationEnabled;
}

class LoyaltyPiecePointSetting {
  const LoyaltyPiecePointSetting({
    required this.loyaltyPiecePointSettingId,
    required this.pieceCode,
    required this.pieceName,
    required this.points,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  factory LoyaltyPiecePointSetting.fromJson(LoyaltyJson json) =>
      LoyaltyPiecePointSetting(
        loyaltyPiecePointSettingId:
            (json['loyaltyPiecePointSettingId'] as num?)?.toInt() ?? 0,
        pieceCode: json['pieceCode'] as String? ?? '',
        pieceName: json['pieceName'] as String? ?? '',
        points: _asDouble(json['points']),
        isActive: json['isActive'] as bool? ?? true,
        createdAt: _parseLoyaltyDate(json['createdAt']),
        updatedAt: _parseLoyaltyDate(json['updatedAt']),
      );

  final int loyaltyPiecePointSettingId;
  final String pieceCode;
  final String pieceName;
  final double points;
  final bool isActive;
  final DateTime createdAt;
  final DateTime updatedAt;
}

class ProductLoyaltyPointSetting {
  const ProductLoyaltyPointSetting({
    required this.productTypeId,
    required this.code,
    required this.nameAr,
    required this.category,
    required this.settingId,
    required this.points,
    required this.isSettingActive,
    required this.isConfigured,
  });

  factory ProductLoyaltyPointSetting.fromJson(LoyaltyJson json) =>
      ProductLoyaltyPointSetting(
        productTypeId: (json['productTypeId'] as num?)?.toInt() ?? 0,
        code: json['code'] as String? ?? '',
        nameAr: json['nameAr'] as String? ?? '',
        category: json['category'] as String?,
        settingId: ((json['settingId'] ?? json['loyaltyPiecePointSettingId'])
                as num?)
            ?.toInt(),
        points: _nullableDouble(json['points']),
        isSettingActive: json['isSettingActive'] as bool?,
        isConfigured: json['isConfigured'] as bool? ?? false,
      );

  final int productTypeId;
  final String code;
  final String nameAr;
  final String? category;
  final int? settingId;
  final double? points;
  final bool? isSettingActive;
  final bool isConfigured;
}

class ImportedProductLoyaltyPointSetting {
  const ImportedProductLoyaltyPointSetting({
    required this.importedReadyMadeProductId,
    required this.productName,
    required this.productType,
    required this.productCode,
    required this.isProductActive,
    required this.settingId,
    required this.points,
    required this.isSettingActive,
    required this.isConfigured,
  });

  factory ImportedProductLoyaltyPointSetting.fromJson(LoyaltyJson json) =>
      ImportedProductLoyaltyPointSetting(
        importedReadyMadeProductId:
            (json['importedReadyMadeProductId'] as num?)?.toInt() ?? 0,
        productName: json['productName'] as String? ?? '',
        productType: json['productType'] as String? ?? '',
        productCode: json['productCode'] as String? ?? '',
        isProductActive: json['isProductActive'] as bool? ?? false,
        settingId: (json['settingId'] as num?)?.toInt(),
        points: _nullableDouble(json['points']),
        isSettingActive: json['isSettingActive'] as bool?,
        isConfigured: json['isConfigured'] as bool? ?? false,
      );

  final int importedReadyMadeProductId;
  final String productName;
  final String productType;
  final String productCode;
  final bool isProductActive;
  final int? settingId;
  final double? points;
  final bool? isSettingActive;
  final bool isConfigured;
}

class VipLevel {
  const VipLevel({
    required this.vipLevelId,
    required this.code,
    required this.displayName,
    required this.minimumPoints,
    required this.multiplier,
    required this.priority,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  factory VipLevel.fromJson(LoyaltyJson json) => VipLevel(
        vipLevelId: (json['vipLevelId'] as num?)?.toInt() ?? 0,
        code: json['code'] as String? ?? '',
        displayName: json['displayName'] as String? ?? '',
        minimumPoints: _asDouble(json['minimumPoints']),
        multiplier: _asDouble(json['multiplier']),
        priority: (json['priority'] as num?)?.toInt() ?? 0,
        isActive: json['isActive'] as bool? ?? true,
        createdAt: _parseLoyaltyDate(json['createdAt']),
        updatedAt: _parseLoyaltyDate(json['updatedAt']),
      );

  final int vipLevelId;
  final String code;
  final String displayName;
  final double minimumPoints;
  final double multiplier;
  final int priority;
  final bool isActive;
  final DateTime createdAt;
  final DateTime updatedAt;
}

class VipEvaluationCriteria {
  const VipEvaluationCriteria({
    required this.vipLevelId,
    required this.code,
    required this.displayName,
    required this.priority,
    required this.minimumDirectReferrals,
    required this.minimumOwnOrders,
    required this.minimumNetworkOrders,
    required this.minimumNetworkSize,
    required this.minimumScore,
    required this.directReferralWeight,
    required this.ownOrderWeight,
    required this.networkOrderWeight,
    required this.networkSizeWeight,
    required this.isActive,
  });

  factory VipEvaluationCriteria.fromJson(LoyaltyJson json) =>
      VipEvaluationCriteria(
        vipLevelId: (json['vipLevelId'] as num?)?.toInt() ?? 0,
        code: json['code'] as String? ?? '',
        displayName: json['displayName'] as String? ?? '',
        priority: (json['priority'] as num?)?.toInt() ?? 0,
        minimumDirectReferrals:
            (json['minimumDirectReferrals'] as num?)?.toInt() ?? 0,
        minimumOwnOrders: (json['minimumOwnOrders'] as num?)?.toInt() ?? 0,
        minimumNetworkOrders:
            (json['minimumNetworkOrders'] as num?)?.toInt() ?? 0,
        minimumNetworkSize: (json['minimumNetworkSize'] as num?)?.toInt() ?? 0,
        minimumScore: _asDouble(json['minimumScore']),
        directReferralWeight: _asDouble(json['directReferralWeight']),
        ownOrderWeight: _asDouble(json['ownOrderWeight']),
        networkOrderWeight: _asDouble(json['networkOrderWeight']),
        networkSizeWeight: _asDouble(json['networkSizeWeight']),
        isActive: json['isActive'] as bool? ?? false,
      );

  final int vipLevelId;
  final String code;
  final String displayName;
  final int priority;
  final int minimumDirectReferrals;
  final int minimumOwnOrders;
  final int minimumNetworkOrders;
  final int minimumNetworkSize;
  final double minimumScore;
  final double directReferralWeight;
  final double ownOrderWeight;
  final double networkOrderWeight;
  final double networkSizeWeight;
  final bool isActive;
}

class VipEvaluation {
  const VipEvaluation({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.previousVipLevelId,
    required this.previousVipLevelCode,
    required this.previousVipLevelDisplayName,
    required this.evaluatedVipLevelId,
    required this.evaluatedVipLevelCode,
    required this.evaluatedVipLevelDisplayName,
    required this.directReferralCount,
    required this.level1ReferralCount,
    required this.level2ReferralCount,
    required this.level3ReferralCount,
    required this.level4ReferralCount,
    required this.networkSize,
    required this.networkMaxDepth,
    required this.ownOrderCount,
    required this.networkOrderCount,
    required this.score,
    required this.reason,
    required this.evaluatedAtUtc,
    required this.changed,
  });

  factory VipEvaluation.fromJson(LoyaltyJson json) => VipEvaluation(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        previousVipLevelId: (json['previousVipLevelId'] as num?)?.toInt(),
        previousVipLevelCode: json['previousVipLevelCode'] as String?,
        previousVipLevelDisplayName:
            json['previousVipLevelDisplayName'] as String?,
        evaluatedVipLevelId:
            (json['evaluatedVipLevelId'] as num?)?.toInt() ?? 0,
        evaluatedVipLevelCode: json['evaluatedVipLevelCode'] as String? ?? '',
        evaluatedVipLevelDisplayName:
            json['evaluatedVipLevelDisplayName'] as String? ?? '',
        directReferralCount:
            (json['directReferralCount'] as num?)?.toInt() ?? 0,
        level1ReferralCount:
            (json['level1ReferralCount'] as num?)?.toInt() ?? 0,
        level2ReferralCount:
            (json['level2ReferralCount'] as num?)?.toInt() ?? 0,
        level3ReferralCount:
            (json['level3ReferralCount'] as num?)?.toInt() ?? 0,
        level4ReferralCount:
            (json['level4ReferralCount'] as num?)?.toInt() ?? 0,
        networkSize: (json['networkSize'] as num?)?.toInt() ?? 0,
        networkMaxDepth: (json['networkMaxDepth'] as num?)?.toInt() ?? 0,
        ownOrderCount: (json['ownOrderCount'] as num?)?.toInt() ?? 0,
        networkOrderCount: (json['networkOrderCount'] as num?)?.toInt() ?? 0,
        score: _asDouble(json['score']),
        reason: json['reason'] as String? ?? '',
        evaluatedAtUtc: _parseLoyaltyDate(json['evaluatedAtUtc']),
        changed: json['changed'] as bool? ?? false,
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int? previousVipLevelId;
  final String? previousVipLevelCode;
  final String? previousVipLevelDisplayName;
  final int evaluatedVipLevelId;
  final String evaluatedVipLevelCode;
  final String evaluatedVipLevelDisplayName;
  final int directReferralCount;
  final int level1ReferralCount;
  final int level2ReferralCount;
  final int level3ReferralCount;
  final int level4ReferralCount;
  final int networkSize;
  final int networkMaxDepth;
  final int ownOrderCount;
  final int networkOrderCount;
  final double score;
  final String reason;
  final DateTime evaluatedAtUtc;
  final bool changed;
}

class VipCustomerListItem {
  const VipCustomerListItem({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.vipLevelId,
    required this.vipLevelCode,
    required this.vipLevelDisplayName,
    required this.vipLevelPriority,
    required this.score,
    required this.directReferralCount,
    required this.ownOrderCount,
    required this.networkOrderCount,
    required this.networkSize,
    required this.networkMaxDepth,
    required this.evaluatedAtUtc,
  });

  factory VipCustomerListItem.fromJson(LoyaltyJson json) => VipCustomerListItem(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        vipLevelId: (json['vipLevelId'] as num?)?.toInt() ?? 0,
        vipLevelCode: json['vipLevelCode'] as String? ?? '',
        vipLevelDisplayName: json['vipLevelDisplayName'] as String? ?? '',
        vipLevelPriority: (json['vipLevelPriority'] as num?)?.toInt() ?? 0,
        score: _asDouble(json['score']),
        directReferralCount:
            (json['directReferralCount'] as num?)?.toInt() ?? 0,
        ownOrderCount: (json['ownOrderCount'] as num?)?.toInt() ?? 0,
        networkOrderCount: (json['networkOrderCount'] as num?)?.toInt() ?? 0,
        networkSize: (json['networkSize'] as num?)?.toInt() ?? 0,
        networkMaxDepth: (json['networkMaxDepth'] as num?)?.toInt() ?? 0,
        evaluatedAtUtc: _parseLoyaltyDate(json['evaluatedAtUtc']),
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int vipLevelId;
  final String vipLevelCode;
  final String vipLevelDisplayName;
  final int vipLevelPriority;
  final double score;
  final int directReferralCount;
  final int ownOrderCount;
  final int networkOrderCount;
  final int networkSize;
  final int networkMaxDepth;
  final DateTime evaluatedAtUtc;
}

class LoyaltyRule {
  const LoyaltyRule({
    required this.loyaltyRuleId,
    required this.ruleName,
    required this.ruleType,
    required this.isActive,
    required this.pointsValue,
    required this.spendingAmount,
    required this.multiplier,
    required this.bonusPoints,
    required this.priority,
    required this.vipLevelId,
    required this.startDate,
    required this.endDate,
    required this.createdAt,
    required this.updatedAt,
  });

  factory LoyaltyRule.fromJson(LoyaltyJson json) => LoyaltyRule(
        loyaltyRuleId: (json['loyaltyRuleId'] as num?)?.toInt() ?? 0,
        ruleName: json['ruleName'] as String? ?? '',
        ruleType: json['ruleType'] as String? ?? '',
        isActive: json['isActive'] as bool? ?? true,
        pointsValue: _asDouble(json['pointsValue']),
        spendingAmount: _asDouble(json['spendingAmount']),
        multiplier: _asDouble(json['multiplier']),
        bonusPoints: _asDouble(json['bonusPoints']),
        priority: (json['priority'] as num?)?.toInt() ?? 0,
        vipLevelId: (json['vipLevelId'] as num?)?.toInt(),
        startDate: json['startDate'] == null
            ? null
            : _parseLoyaltyDate(json['startDate']),
        endDate:
            json['endDate'] == null ? null : _parseLoyaltyDate(json['endDate']),
        createdAt: _parseLoyaltyDate(json['createdAt']),
        updatedAt: _parseLoyaltyDate(json['updatedAt']),
      );

  final int loyaltyRuleId;
  final String ruleName;
  final String ruleType;
  final bool isActive;
  final double pointsValue;
  final double spendingAmount;
  final double multiplier;
  final double bonusPoints;
  final int priority;
  final int? vipLevelId;
  final DateTime? startDate;
  final DateTime? endDate;
  final DateTime createdAt;
  final DateTime updatedAt;
}

class LoyaltyRedemptionRecord {
  const LoyaltyRedemptionRecord({
    required this.loyaltyRedemptionId,
    required this.customerId,
    required this.orderId,
    required this.pointsRedeemed,
    required this.pointMonetaryValue,
    required this.creditAmount,
    required this.status,
    required this.createdAt,
    required this.reversedAtUtc,
    required this.reversalLoyaltyTransactionId,
  });

  factory LoyaltyRedemptionRecord.fromJson(LoyaltyJson json) =>
      LoyaltyRedemptionRecord(
        loyaltyRedemptionId:
            (json['loyaltyRedemptionId'] as num?)?.toInt() ?? 0,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        orderId: (json['orderId'] as num?)?.toInt() ?? 0,
        pointsRedeemed: _asDouble(json['pointsRedeemed']),
        pointMonetaryValue: _asDouble(json['pointMonetaryValue']),
        creditAmount: _asDouble(json['creditAmount']),
        status: json['status'] as String? ?? 'Applied',
        createdAt: _parseLoyaltyDate(json['createdAt']),
        reversedAtUtc: json['reversedAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['reversedAtUtc']),
        reversalLoyaltyTransactionId:
            (json['reversalLoyaltyTransactionId'] as num?)?.toInt(),
      );

  final int loyaltyRedemptionId;
  final int customerId;
  final int orderId;
  final double pointsRedeemed;
  final double pointMonetaryValue;
  final double creditAmount;
  final String status;
  final DateTime createdAt;
  final DateTime? reversedAtUtc;
  final int? reversalLoyaltyTransactionId;
}

class LoyaltyRedemptionHistoryData {
  const LoyaltyRedemptionHistoryData(
      {required this.summary, required this.items});

  factory LoyaltyRedemptionHistoryData.fromJson(LoyaltyJson json) =>
      LoyaltyRedemptionHistoryData(
        summary: LoyaltyRedemptionHistorySummary.fromJson(
          Map<String, dynamic>.from(json['summary'] as Map),
        ),
        items: _loyaltyList(
          json['items'],
          LoyaltyRedemptionHistoryItem.fromJson,
        ),
      );

  final LoyaltyRedemptionHistorySummary summary;
  final List<LoyaltyRedemptionHistoryItem> items;
}

class LoyaltyRedemptionHistorySummary {
  const LoyaltyRedemptionHistorySummary({
    required this.redemptionCount,
    required this.redeemedPointsTotal,
    required this.creditAmountTotal,
    required this.reversalCount,
    required this.customerCount,
  });

  factory LoyaltyRedemptionHistorySummary.fromJson(LoyaltyJson json) =>
      LoyaltyRedemptionHistorySummary(
        redemptionCount: (json['redemptionCount'] as num?)?.toInt() ?? 0,
        redeemedPointsTotal: _asDouble(json['redeemedPointsTotal']),
        creditAmountTotal: _asDouble(json['creditAmountTotal']),
        reversalCount: (json['reversalCount'] as num?)?.toInt() ?? 0,
        customerCount: (json['customerCount'] as num?)?.toInt() ?? 0,
      );

  final int redemptionCount;
  final double redeemedPointsTotal;
  final double creditAmountTotal;
  final int reversalCount;
  final int customerCount;
}

class LoyaltyRedemptionHistoryItem {
  const LoyaltyRedemptionHistoryItem({
    required this.loyaltyTransactionId,
    required this.transactionType,
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.orderId,
    required this.orderNumber,
    required this.points,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.source,
    required this.notes,
    required this.createdAt,
    required this.loyaltyRedemptionId,
    required this.pointsRedeemed,
    required this.pointMonetaryValue,
    required this.creditAmount,
    required this.referenceNumber,
    required this.reversedAtUtc,
    required this.reversalLoyaltyTransactionId,
    required this.customerLedgerEntryId,
    required this.financialReference,
  });

  factory LoyaltyRedemptionHistoryItem.fromJson(LoyaltyJson json) =>
      LoyaltyRedemptionHistoryItem(
        loyaltyTransactionId:
            (json['loyaltyTransactionId'] as num?)?.toInt() ?? 0,
        transactionType: json['transactionType'] as String? ?? '',
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode'] as String?,
        customerName: json['customerName'] as String?,
        orderId: (json['orderId'] as num?)?.toInt(),
        orderNumber: json['orderNumber'] as String?,
        points: _asDouble(json['points']),
        balanceBefore: _asDouble(json['balanceBefore']),
        balanceAfter: _asDouble(json['balanceAfter']),
        source: json['source'] as String? ?? '',
        notes: json['notes'] as String?,
        createdAt: _parseLoyaltyDate(json['createdAt']),
        loyaltyRedemptionId: (json['loyaltyRedemptionId'] as num?)?.toInt(),
        pointsRedeemed: _nullableDouble(json['pointsRedeemed']),
        pointMonetaryValue: _nullableDouble(json['pointMonetaryValue']),
        creditAmount: _nullableDouble(json['creditAmount']),
        referenceNumber: json['referenceNumber'] as String?,
        reversedAtUtc: json['reversedAtUtc'] == null
            ? null
            : _parseLoyaltyDate(json['reversedAtUtc']),
        reversalLoyaltyTransactionId:
            (json['reversalLoyaltyTransactionId'] as num?)?.toInt(),
        customerLedgerEntryId: (json['customerLedgerEntryId'] as num?)?.toInt(),
        financialReference: json['financialReference'] as String?,
      );

  final int loyaltyTransactionId;
  final String transactionType;
  final int customerId;
  final String? customerCode;
  final String? customerName;
  final int? orderId;
  final String? orderNumber;
  final double points;
  final double balanceBefore;
  final double balanceAfter;
  final String source;
  final String? notes;
  final DateTime createdAt;
  final int? loyaltyRedemptionId;
  final double? pointsRedeemed;
  final double? pointMonetaryValue;
  final double? creditAmount;
  final String? referenceNumber;
  final DateTime? reversedAtUtc;
  final int? reversalLoyaltyTransactionId;
  final int? customerLedgerEntryId;
  final String? financialReference;
}

class LoyaltyRedemptionPreview {
  const LoyaltyRedemptionPreview({
    required this.customerId,
    required this.orderId,
    required this.pointsRedeemed,
    required this.pointMonetaryValue,
    required this.currentPoints,
    required this.remainingPoints,
    required this.discountAmount,
    required this.isValid,
    required this.validationMessage,
  });

  factory LoyaltyRedemptionPreview.fromJson(LoyaltyJson json) =>
      LoyaltyRedemptionPreview(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        orderId: (json['orderId'] as num?)?.toInt() ?? 0,
        pointsRedeemed: _asDouble(json['pointsRedeemed']),
        pointMonetaryValue: _asDouble(json['pointMonetaryValue']),
        currentPoints: _asDouble(json['currentPoints']),
        remainingPoints:
            _asDouble(json['remainingOrderAmount'] ?? json['remainingPoints']),
        discountAmount:
            _asDouble(json['creditAmount'] ?? json['discountAmount']),
        isValid: json['isValid'] as bool? ?? true,
        validationMessage:
            (json['validationMessage'] ?? json['message'])?.toString() ?? '',
      );

  final int customerId;
  final int orderId;
  final double pointsRedeemed;
  final double pointMonetaryValue;
  final double currentPoints;
  final double remainingPoints;
  final double discountAmount;
  final bool isValid;
  final String validationMessage;
}

class LoyaltyRedemptionResult {
  const LoyaltyRedemptionResult({
    required this.redemptionId,
    required this.customerId,
    required this.orderId,
    required this.pointsRedeemed,
    required this.amount,
    required this.status,
    required this.createdAt,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.remainingOrderAmountBefore,
    required this.remainingOrderAmountAfter,
  });

  factory LoyaltyRedemptionResult.fromJson(LoyaltyJson json) =>
      LoyaltyRedemptionResult(
        redemptionId:
            ((json['redemption'] as Map?)?['loyaltyRedemptionId'] as num?)
                    ?.toInt() ??
                (json['redemptionId'] as num?)?.toInt() ??
                0,
        customerId:
            ((json['redemption'] as Map?)?['customerId'] as num?)?.toInt() ??
                (json['customerId'] as num?)?.toInt() ??
                0,
        orderId: ((json['redemption'] as Map?)?['orderId'] as num?)?.toInt() ??
            (json['orderId'] as num?)?.toInt() ??
            0,
        pointsRedeemed: _asDouble(
            (json['redemption'] as Map?)?['pointsRedeemed'] ??
                json['pointsRedeemed']),
        amount: _asDouble(json['creditAmount'] ?? json['amount']),
        status:
            ((json['redemption'] as Map?)?['status'] as String?) ?? 'Applied',
        createdAt: _parseLoyaltyDate(
            (json['redemption'] as Map?)?['createdAt'] ?? json['createdAt']),
        balanceBefore:
            _asDouble((json['redeemTransaction'] as Map?)?['balanceBefore']),
        balanceAfter: _asDouble((json['newBalanceAfterRedeem'] ??
            (json['redeemTransaction'] as Map?)?['balanceAfter'])),
        remainingOrderAmountBefore:
            _asDouble(json['remainingOrderAmountBefore']),
        remainingOrderAmountAfter: _asDouble(json['remainingOrderAmountAfter']),
      );

  final int redemptionId;
  final int customerId;
  final int orderId;
  final double pointsRedeemed;
  final double amount;
  final String status;
  final DateTime createdAt;
  final double balanceBefore;
  final double balanceAfter;
  final double remainingOrderAmountBefore;
  final double remainingOrderAmountAfter;
}
