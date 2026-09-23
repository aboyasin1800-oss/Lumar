import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/referral_models.dart';

double _referralNumber(Object? value) {
  if (value is num) return value.toDouble();
  return double.tryParse(value?.toString() ?? '') ?? 0;
}

class ReferralRepository {
  ReferralRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<ReferralDashboard> getDashboard() => _getJson(
        '/api/referrals/dashboard',
        (json) => ReferralDashboard.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<ReferralDashboardSearchResult>> searchDashboard(String query) =>
      _getList(
        '/api/referrals/dashboard/search?query=${Uri.encodeQueryComponent(query)}',
        (json) => ReferralDashboardSearchResult.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<ReferralAnalyticsData> getReferralAnalytics({
    String? search,
    DateTime? from,
    DateTime? to,
  }) {
    final query = <String, String>{
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
      if (from != null) 'from': from.toIso8601String(),
      if (to != null) 'to': to.toIso8601String(),
    };
    final uri = Uri.parse('$_baseUrl/api/referrals/analytics')
        .replace(queryParameters: query);
    return _getUriJson(
      uri,
      (json) => ReferralAnalyticsData.fromJson(
        Map<String, dynamic>.from(json),
      ),
    );
  }

  Future<ReferralRewardsData> getReferralRewardsScreen({
    String? transactionType,
    DateTime? from,
    DateTime? to,
    int? customerId,
    String? search,
  }) {
    final query = <String, String>{
      if (transactionType != null) 'transactionType': transactionType,
      if (from != null) 'from': from.toIso8601String(),
      if (to != null) 'to': to.toIso8601String(),
      if (customerId != null) 'customerId': customerId.toString(),
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    };
    final uri = Uri.parse('$_baseUrl/api/referrals/rewards-screen')
        .replace(queryParameters: query);
    return _getUriJson(
      uri,
      (json) => ReferralRewardsData.fromJson(
        Map<String, dynamic>.from(json),
      ),
    );
  }

  Future<T> _getJson<T>(
    String path,
    T Function(dynamic json) fromJson,
  ) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('طلب فشل: $path (${response.statusCode})');
    }
    return fromJson(jsonDecode(response.body));
  }

  Future<T> _getUriJson<T>(
    Uri uri,
    T Function(dynamic json) fromJson,
  ) async {
    final response = await _client.get(uri);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('طلب فشل: ${uri.path} (${response.statusCode})');
    }
    return fromJson(jsonDecode(response.body));
  }

  Future<List<T>> _getList<T>(
    String path,
    T Function(dynamic json) fromJson,
  ) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('طلب فشل: $path (${response.statusCode})');
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) return const [];
    return decoded.map((e) => fromJson(e)).toList();
  }

  Future<ReferralAccount> getAccount(int customerId) => _getJson(
        '/api/referrals/customers/$customerId/account',
        (json) => ReferralAccount.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<ReferralCustomerDetails> getCustomerDetails(int customerId) async {
    final results = await Future.wait<dynamic>([
      _getOptionalJson('/customers/$customerId/loyalty'),
      _getOptionalJson('/api/referrals/customers/$customerId/account'),
      getTransactions(customerId),
      _getList<Map<String, dynamic>>(
        '/customers/$customerId/ledger',
        (json) => Map<String, dynamic>.from(json),
      ),
    ]);

    final loyalty = results[0] as Map<String, dynamic>?;
    final account = results[1] as Map<String, dynamic>?;
    final transactions = results[2] as List<ReferralTransaction>;
    final ledger = results[3] as List<Map<String, dynamic>>;

    final latestLedgerBalance = ledger.isEmpty
      ? 0.0
      : _referralNumber(ledger.first['balanceAfterTransaction']);
    final totalFinancialBalance = ledger.fold<double>(
      0,
      (balance, entry) =>
          balance +
          _referralNumber(entry['debitAmount']) -
          _referralNumber(entry['creditAmount']),
    );
    final referralAccountBalance = transactions.fold<double>(0, (balance, item) {
      if (item.referrerCustomerId != customerId) return balance;
      return balance +
          (item.transactionType == 'RewardReversal'
              ? -item.fixedRewardAmount
              : item.transactionType == 'RewardGranted'
                  ? item.fixedRewardAmount
                  : 0);
    });

    return ReferralCustomerDetails(
        currentPoints: _referralNumber(loyalty?['currentPoints']),
        totalReferralPoints: _referralNumber(account?['totalRewardPoints']),
      totalReferralRewardsAmount:
          _referralNumber(account?['totalRewardsAmount']),
      referralAccountBalance: referralAccountBalance,
      totalFinancialBalance: totalFinancialBalance,
      currentDebt: latestLedgerBalance > 0 ? latestLedgerBalance : 0,
      latestLedgerBalance: latestLedgerBalance,
    );
  }

  Future<List<ReferralCode>> getCodes(int customerId) => _getList(
        '/api/referrals/customers/$customerId/codes',
        (json) => ReferralCode.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<Map<String, dynamic>?> _getOptionalJson(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode == 404) return null;
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('طلب فشل: $path (${response.statusCode})');
    }
    return Map<String, dynamic>.from(jsonDecode(response.body) as Map);
  }

  Future<List<ReferralTransaction>> getTransactions(int customerId) => _getList(
        '/api/referrals/customers/$customerId/transactions',
        (json) => ReferralTransaction.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<ReferralTree> getTree(int customerId) => _getJson(
        '/api/referrals/customers/$customerId/tree',
        (json) => ReferralTree.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<ReferralRoot>> getRoots() => _getList(
        '/api/referrals/roots',
        (json) => ReferralRoot.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<ReferralCustomerIdentity>> getAllCustomerIdentities() => _getList(
        '/customers',
        (json) => ReferralCustomerIdentity.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<List<ReferralCustomerIdentity>> searchCustomers(String term) =>
      _getList(
        '/customers/search?term=${Uri.encodeQueryComponent(term.trim())}',
        (json) => ReferralCustomerIdentity.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<List<ReferralCode>> getAllReferralCodes() async {
    final customerIds = await _getReferralCustomerIds();
    final codes = await Future.wait(customerIds.map(getCodes));
    final unique = <int, ReferralCode>{};
    for (final customerCodes in codes) {
      for (final code in customerCodes) {
        unique[code.referralCodeId] = code;
      }
    }
    return unique.values.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  Future<Set<int>> _getReferralCustomerIds() async {
    final roots = await getRoots();
    final customerIds = <int>{for (final root in roots) root.customerId};
    final trees = await Future.wait(
      roots.map((root) => getTree(root.customerId)),
    );
    for (final tree in trees) {
      void collect(ReferralTreeNode node) {
        customerIds.add(node.customerId);
        for (final child in node.children) {
          collect(child);
        }
      }

      for (final child in tree.children) {
        collect(child);
      }
    }
    return customerIds;
  }

  Future<List<ReferralTransaction>> getAllTransactions() async {
    final customerIds = await _getReferralCustomerIds();
    final transactions = await Future.wait(
      customerIds.map(getTransactions),
    );
    final unique = <int, ReferralTransaction>{};
    for (final customerTransactions in transactions) {
      for (final transaction in customerTransactions) {
        unique[transaction.referralTransactionId] = transaction;
      }
    }
    final result = unique.values.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    return result;
  }

  Future<List<ReferralReward>> getReferralRewards() => _getList(
        '/api/loyalty-management/referral-rewards',
        (json) => ReferralReward.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<ReferralCode> ensureCode(int customerId,
      {String? preferredCode}) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/referrals/codes/ensure'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'customerId': customerId,
        'preferredCode': preferredCode ?? '',
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر إنشاء كود الإحالة (${response.statusCode})');
    }
    return ReferralCode.fromJson(jsonDecode(response.body));
  }

  Future<Map<String, dynamic>> registerReferral({
    required int referredCustomerId,
    required String referralCode,
    String? notes,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/referrals/register'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'referredCustomerId': referredCustomerId,
        'referralCode': referralCode,
        'notes': notes,
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تسجيل الإحالة (${response.statusCode})');
    }
    return jsonDecode(response.body) as Map<String, dynamic>;
  }
}
