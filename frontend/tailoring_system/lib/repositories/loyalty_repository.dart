import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import '../models/loyalty_models.dart';

class LoyaltyRepository {
  LoyaltyRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<LoyaltyDashboardData> getDashboard() => _getJson(
        '/api/loyalty/dashboard',
        (json) => LoyaltyDashboardData.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<LoyaltyTransactionsScreenData> getTransactionsScreen({
    String? search,
    String? transactionType,
    DateTime? from,
    DateTime? to,
    int? customerId,
  }) {
    final query = <String, String>{};
    if (search?.trim().isNotEmpty == true) query['search'] = search!.trim();
    if (transactionType?.trim().isNotEmpty == true) {
      query['transactionType'] = transactionType!.trim();
    }
    if (from != null) query['from'] = _dateQueryValue(from);
    if (to != null) query['to'] = _dateQueryValue(to);
    if (customerId != null) query['customerId'] = '$customerId';
    final uri = Uri.parse('$_baseUrl/api/loyalty/transactions-screen')
        .replace(queryParameters: query);
    return _getJsonUri(
      uri,
      (json) => LoyaltyTransactionsScreenData.fromJson(
        Map<String, dynamic>.from(json),
      ),
    );
  }

  Future<LoyaltyRewardsScreenData> getRewardsScreen({
    String? search,
    String? rewardType,
    DateTime? from,
    DateTime? to,
    int? customerId,
  }) {
    final query = <String, String>{};
    if (search?.trim().isNotEmpty == true) query['search'] = search!.trim();
    if (rewardType?.trim().isNotEmpty == true) {
      query['rewardType'] = rewardType!.trim();
    }
    if (from != null) query['from'] = _dateQueryValue(from);
    if (to != null) query['to'] = _dateQueryValue(to);
    if (customerId != null) query['customerId'] = '$customerId';
    final uri = Uri.parse('$_baseUrl/api/loyalty/rewards-screen')
        .replace(queryParameters: query);
    return _getJsonUri(
      uri,
      (json) => LoyaltyRewardsScreenData.fromJson(
        Map<String, dynamic>.from(json),
      ),
    );
  }

  Future<List<LoyaltyCustomerSearchResult>> searchCustomers(String term) {
    final normalizedTerm = term.trim();
    if (normalizedTerm.isEmpty) return Future.value(const []);
    return _getList(
      '/customers/search?term=${Uri.encodeQueryComponent(normalizedTerm)}',
      (json) => LoyaltyCustomerSearchResult.fromJson(
        Map<String, dynamic>.from(json),
      ),
    );
  }

  Future<List<Map<String, dynamic>>> getOrders() => _getList(
        '/orders',
        (json) => Map<String, dynamic>.from(json as Map),
      );

  Future<T> _getJson<T>(String path, T Function(dynamic json) fromJson) async {
    return _getJsonUri(Uri.parse('$_baseUrl$path'), fromJson);
  }

  Future<T> _getJsonUri<T>(Uri uri, T Function(dynamic json) fromJson) async {
    debugPrint('[Loyalty HTTP] GET $uri');
    final response = await _client.get(uri);
    debugPrint(
        '[Loyalty HTTP] GET $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: GET $uri [${response.statusCode}]: ${response.body}');
    }
    return fromJson(jsonDecode(response.body));
  }

  Future<List<T>> _getList<T>(
      String path, T Function(dynamic json) fromJson) async {
    final uri = Uri.parse('$_baseUrl$path');
    debugPrint('[Loyalty HTTP] GET $uri');
    final response = await _client.get(uri);
    debugPrint(
        '[Loyalty HTTP] GET $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: GET $uri [${response.statusCode}]: ${response.body}');
    }
    try {
      final decoded = jsonDecode(response.body);
      debugPrint('[Loyalty HTTP] GET $uri JSON type=${decoded.runtimeType}');
      if (decoded is! List) {
        debugPrint('[Loyalty HTTP] GET $uri decoded non-list; returning []');
        return const [];
      }
      debugPrint('[Loyalty HTTP] GET $uri decoded count=${decoded.length}');
      final items = <T>[];
      for (var index = 0; index < decoded.length; index++) {
        try {
          final item = fromJson(decoded[index]);
          items.add(item);
          if (path == '/api/piece-points/product-settings' &&
              item is ProductLoyaltyPointSetting &&
              item.productTypeId == 7) {
            debugPrint(
                '[Loyalty Model] ProductTypeId=7 parsed: settingId=${item.settingId}, points=${item.points}, isActive=${item.isSettingActive}, hasSetting=${item.isConfigured}');
          }
        } catch (error, stackTrace) {
          debugPrint(
              '[Loyalty Model] GET $uri item[$index] parse failed: $error');
          debugPrintStack(stackTrace: stackTrace);
          rethrow;
        }
      }
      debugPrint('[Loyalty Model] GET $uri parsed count=${items.length}');
      return items;
    } catch (error, stackTrace) {
      debugPrint('[Loyalty Model] GET $uri decode/list failed: $error');
      debugPrintStack(stackTrace: stackTrace);
      rethrow;
    }
  }

  Future<LoyaltyAccount> getAccount(int customerId) => _getJson(
        '/api/loyalty/customers/$customerId/account',
        (json) => LoyaltyAccount.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<LoyaltyBalance> getBalance(int customerId) => _getJson(
        '/api/loyalty/customers/$customerId/balance',
        (json) => LoyaltyBalance.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<LoyaltyTransaction>> getTransactions(int customerId) => _getList(
        '/api/loyalty/customers/$customerId/transactions',
        (json) => LoyaltyTransaction.fromJson(Map<String, dynamic>.from(json)),
      );

  String _dateQueryValue(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-'
      '${value.month.toString().padLeft(2, '0')}-'
      '${value.day.toString().padLeft(2, '0')}';

  Future<LoyaltyAccount> ensureAccount(int customerId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/loyalty/customers/$customerId/ensure'),
      headers: {'Content-Type': 'application/json'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر إنشاء حساب الولاء (${response.statusCode})');
    }
    return LoyaltyAccount.fromJson(jsonDecode(response.body));
  }

  Future<List<LoyaltyProgramSettings>> getProgramSettings() => _getList(
        '/api/loyalty-management/program-settings',
        (json) =>
            LoyaltyProgramSettings.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<LoyaltyProgramSettings> updateProgramSettings({
    required int id,
    required bool isEnabled,
    required double pointsPerPiece,
    required double pointMonetaryValue,
    required DateTime effectiveFromUtc,
    bool allowRedemption = true,
    double minimumRedemptionPoints = 0,
    double maximumRedemptionPoints = 0,
    bool loyaltyFreezeEnabled = false,
    int gracePeriodDays = 180,
    int warningPeriodDays = 30,
    bool manualReactivationEnabled = true,
    bool purchaseReactivationEnabled = true,
  }) async {
    final uri =
        Uri.parse('$_baseUrl/api/loyalty-management/program-settings/$id');
    final body = jsonEncode({
      'isEnabled': isEnabled,
      'pointsPerPiece': pointsPerPiece,
      'pointMonetaryValue': pointMonetaryValue,
      'effectiveFromUtc': effectiveFromUtc.toUtc().toIso8601String(),
      'allowRedemption': allowRedemption,
      'minimumRedemptionPoints': minimumRedemptionPoints,
      'maximumRedemptionPoints': maximumRedemptionPoints,
      'loyaltyFreezeEnabled': loyaltyFreezeEnabled,
      'gracePeriodDays': gracePeriodDays,
      'warningPeriodDays': warningPeriodDays,
      'manualReactivationEnabled': manualReactivationEnabled,
      'purchaseReactivationEnabled': purchaseReactivationEnabled,
    });
    debugPrint('[Loyalty HTTP] PUT $uri body=$body');
    final response = await _client.put(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: body,
    );
    debugPrint(
        '[Loyalty HTTP] PUT $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: PUT $uri [${response.statusCode}]: ${response.body}');
    }
    return LoyaltyProgramSettings.fromJson(
        Map<String, dynamic>.from(jsonDecode(response.body) as Map));
  }

  Future<LoyaltyPiecePointSetting> updatePiecePointSetting({
    required int id,
    required String pieceCode,
    required String pieceName,
    required double points,
    required bool isActive,
  }) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/api/loyalty-management/piece-point-settings/$id'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'pieceCode': pieceCode.trim(),
        'pieceName': pieceName.trim(),
        'points': points,
        'isActive': isActive,
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر حفظ إعداد نقاط القطعة');
    }
    return LoyaltyPiecePointSetting.fromJson(jsonDecode(response.body));
  }

  Future<List<LoyaltyPiecePointSetting>> getPiecePointSettings() => _getList(
        '/api/loyalty-management/piece-point-settings',
        (json) =>
            LoyaltyPiecePointSetting.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<ProductLoyaltyPointSetting>> getProductLoyaltyPointSettings() =>
      _getList(
        '/api/piece-points/product-settings',
        (json) => ProductLoyaltyPointSetting.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<double> evaluateOfficialProductPoints({
    required int productTypeId,
  }) async {
    final uri = Uri.parse('$_baseUrl/api/piece-points/evaluate-official');
    final body = jsonEncode({
      'productTypeId': productTypeId,
      'quantity': 1,
      'source': 'sales-expected-points-preview',
    });
    debugPrint('[Loyalty HTTP] POST $uri body=$body');
    final response = await _client.post(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: body,
    );
    debugPrint(
        '[Loyalty HTTP] POST $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: POST $uri [${response.statusCode}]: ${response.body}');
    }
    final decoded = Map<String, dynamic>.from(jsonDecode(response.body));
    final value = decoded['basePoints'];
    return value is num ? value.toDouble() : double.tryParse('$value') ?? 0;
  }

  Future<({double basePoints, List<double> referralLevels})>
      evaluateOfficialReferralLevels({
    int? productTypeId,
    int? importedReadyMadeProductId,
  }) async {
    if ((productTypeId == null) == (importedReadyMadeProductId == null)) {
      throw ArgumentError(
        'يجب تحديد مصدر رسمي واحد لقراءة مستويات الإحالة.',
      );
    }
    final uri = Uri.parse('$_baseUrl/api/piece-points/evaluate-official');
    final body = jsonEncode({
      if (productTypeId != null) 'productTypeId': productTypeId,
      if (importedReadyMadeProductId != null)
        'importedReadyMadeProductId': importedReadyMadeProductId,
      'quantity': 1,
      'source': 'loyalty-rules-reference',
    });
    debugPrint('[Loyalty HTTP] POST $uri body=$body');
    final response = await _client.post(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: body,
    );
    debugPrint(
        '[Loyalty HTTP] POST $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: POST $uri [${response.statusCode}]: ${response.body}');
    }
    final decoded = Map<String, dynamic>.from(jsonDecode(response.body));
    final baseValue = decoded['basePoints'];
    final basePoints = baseValue is num
        ? baseValue.toDouble()
        : double.tryParse('$baseValue') ?? 0;
    final levels = decoded['referralLevels'] is List
        ? (decoded['referralLevels'] as List)
            .whereType<num>()
            .map((value) => value.toDouble())
            .toList()
        : <double>[];
    return (basePoints: basePoints, referralLevels: levels);
  }

  Future<ProductLoyaltyPointSetting> updateProductLoyaltyPointSetting({
    required int productTypeId,
    required double points,
    required bool isActive,
  }) async {
    final uri =
        Uri.parse('$_baseUrl/api/piece-points/product-settings/$productTypeId');
    final body = jsonEncode({'points': points, 'isActive': isActive});
    debugPrint('[Loyalty HTTP] PUT $uri body=$body');
    final response = await _client.put(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: body,
    );
    debugPrint(
        '[Loyalty HTTP] PUT $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: PUT $uri [${response.statusCode}]: ${response.body}');
    }
    return ProductLoyaltyPointSetting.fromJson(
        Map<String, dynamic>.from(jsonDecode(response.body)));
  }

  Future<List<ProductLoyaltyPointSetting>>
      getReadyMadeProductLoyaltyPointSettings() => _getList(
            '/api/piece-points/ready-made-product-settings',
            (json) => ProductLoyaltyPointSetting.fromJson(
              Map<String, dynamic>.from(json),
            ),
          );

  Future<ProductLoyaltyPointSetting>
      updateReadyMadeProductLoyaltyPointSetting({
    required int productTypeId,
    required double points,
    required bool isActive,
  }) async {
    final uri = Uri.parse(
        '$_baseUrl/api/piece-points/ready-made-product-settings/$productTypeId');
    final body = jsonEncode({'points': points, 'isActive': isActive});
    debugPrint('[Loyalty HTTP] PUT $uri body=$body');
    final response = await _client.put(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: body,
    );
    debugPrint(
        '[Loyalty HTTP] PUT $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: PUT $uri [${response.statusCode}]: ${response.body}');
    }
    return ProductLoyaltyPointSetting.fromJson(
        Map<String, dynamic>.from(jsonDecode(response.body)));
  }

  Future<List<ImportedProductLoyaltyPointSetting>>
      getImportedProductLoyaltyPointSettings() => _getList(
            '/api/piece-points/imported-product-settings',
            (json) => ImportedProductLoyaltyPointSetting.fromJson(
              Map<String, dynamic>.from(json),
            ),
          );

  Future<ImportedProductLoyaltyPointSetting>
      updateImportedProductLoyaltyPointSetting({
    required int importedReadyMadeProductId,
    required double points,
    required bool isActive,
  }) async {
    final uri = Uri.parse(
        '$_baseUrl/api/piece-points/imported-product-settings/$importedReadyMadeProductId');
    final body = jsonEncode({'points': points, 'isActive': isActive});
    debugPrint('[Loyalty HTTP] PUT $uri body=$body');
    final response = await _client.put(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: body,
    );
    debugPrint(
        '[Loyalty HTTP] PUT $uri -> ${response.statusCode} ${response.body}');
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'طلب فشل: PUT $uri [${response.statusCode}]: ${response.body}');
    }
    return ImportedProductLoyaltyPointSetting.fromJson(
        Map<String, dynamic>.from(jsonDecode(response.body)));
  }

  Future<List<VipLevel>> getVipLevels() => _getList(
        '/api/loyalty-management/vip-levels',
        (json) => VipLevel.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<VipEvaluationCriteria>> getVipEvaluationCriteria() => _getList(
        '/api/vip-levels/criteria',
        (json) => VipEvaluationCriteria.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<VipEvaluation> getVipEvaluation(int customerId) => _getJson(
        '/api/vip-levels/customers/$customerId/evaluation',
        (json) => VipEvaluation.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<VipCustomerListItem>> getVipCustomers() => _getList(
        '/api/vip-levels/customers',
        (json) => VipCustomerListItem.fromJson(
          Map<String, dynamic>.from(json),
        ),
      );

  Future<List<LoyaltyRule>> getRules() => _getList(
        '/api/loyalty-management/loyalty-rules',
        (json) => LoyaltyRule.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<List<LoyaltyRedemptionRecord>> getRedemptionHistory(int customerId) =>
      _getList(
        '/api/loyalty-redemptions/customers/$customerId/history',
        (json) =>
            LoyaltyRedemptionRecord.fromJson(Map<String, dynamic>.from(json)),
      );

  Future<LoyaltyRedemptionHistoryData> getRedemptionsHistory({
    String? search,
    String? transactionType,
    DateTime? from,
    DateTime? to,
    int? customerId,
  }) {
    final query = <String, String>{};
    if (search?.trim().isNotEmpty == true) query['search'] = search!.trim();
    if (transactionType?.trim().isNotEmpty == true) {
      query['transactionType'] = transactionType!.trim();
    }
    if (from != null) query['from'] = _dateQueryValue(from);
    if (to != null) query['to'] = _dateQueryValue(to);
    if (customerId != null) query['customerId'] = '$customerId';
    final uri = Uri.parse('$_baseUrl/api/loyalty-redemptions/history')
        .replace(queryParameters: query);
    return _getJsonUri(
      uri,
      (json) => LoyaltyRedemptionHistoryData.fromJson(
        Map<String, dynamic>.from(json),
      ),
    );
  }

  Future<LoyaltyRedemptionPreview> previewRedemption({
    required int customerId,
    required int orderId,
    required double pointsRedeemed,
    required double pointMonetaryValue,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/loyalty-redemptions/preview'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'customerId': customerId,
        'orderId': orderId,
        'pointsRedeemed': pointsRedeemed,
        'pointMonetaryValue': pointMonetaryValue,
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر معاينة الاسترداد (${response.statusCode})');
    }
    return LoyaltyRedemptionPreview.fromJson(jsonDecode(response.body));
  }

  Future<LoyaltyRedemptionResult> applyRedemption({
    required int customerId,
    required int orderId,
    required double pointsRedeemed,
    required double pointMonetaryValue,
  }) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/loyalty-redemptions/apply'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'customerId': customerId,
        'orderId': orderId,
        'pointsRedeemed': pointsRedeemed,
        'pointMonetaryValue': pointMonetaryValue,
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تطبيق الاسترداد (${response.statusCode})');
    }
    return LoyaltyRedemptionResult.fromJson(jsonDecode(response.body));
  }

  Future<Map<String, dynamic>> reverseRedemption(int redemptionId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/api/loyalty-redemptions/reverse'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'redemptionId': redemptionId}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر التراجع عن الاسترداد (${response.statusCode})');
    }
    return jsonDecode(response.body) as Map<String, dynamic>;
  }
}
