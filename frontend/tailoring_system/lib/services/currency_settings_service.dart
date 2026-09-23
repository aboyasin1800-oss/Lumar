import 'dart:convert';

import 'package:http/http.dart' as http;

class CurrencyOption {
  const CurrencyOption({
    required this.name,
    required this.code,
    required this.symbol,
  });

  final String name;
  final String code;
  final String symbol;
}

const supportedCurrencyOptions = <CurrencyOption>[
  CurrencyOption(name: 'الريال اليمني', code: 'YER', symbol: 'ر.ي.'),
  CurrencyOption(name: 'الريال السعودي', code: 'SAR', symbol: 'ر.س'),
  CurrencyOption(name: 'الدولار الأمريكي', code: 'USD', symbol: r'$'),
  CurrencyOption(name: 'الجنيه المصري', code: 'EGP', symbol: 'ج.م'),
  CurrencyOption(name: 'الريال القطري', code: 'QAR', symbol: 'ر.ق'),
  CurrencyOption(name: 'الريال العماني', code: 'OMR', symbol: 'ر.ع'),
  CurrencyOption(name: 'الدرهم الإماراتي', code: 'AED', symbol: 'د.إ'),
  CurrencyOption(name: 'الدينار الكويتي', code: 'KWD', symbol: 'د.ك'),
  CurrencyOption(name: 'الدينار العراقي', code: 'IQD', symbol: 'د.ع'),
  CurrencyOption(name: 'الجنيه السوداني', code: 'SDG', symbol: 'ج.س'),
];

class CurrencySettings {
  const CurrencySettings({
    required this.currencyName,
    required this.preferredCurrency,
  });

  final String currencyName;
  final String preferredCurrency;

  String get normalizedCode => preferredCurrency.trim().toUpperCase();

  String get displayName {
    return option?.name ??
        (currencyName.trim().isEmpty ? 'الريال اليمني' : currencyName.trim());
  }

  String get displayCode => normalizedCode.isEmpty ? 'YER' : normalizedCode;

  CurrencyOption? get option {
    for (final item in supportedCurrencyOptions) {
      if (item.code == displayCode) return item;
    }
    return null;
  }

  String get displaySymbol => option?.symbol ?? displayCode;

  static CurrencySettings fromValues({
    required String currencyName,
    required String preferredCurrency,
  }) =>
      CurrencySettings(
        currencyName: currencyName,
        preferredCurrency: preferredCurrency,
      );
}

class CurrencySettingsService {
  CurrencySettingsService({http.Client? client, String? baseUrl})
      : _client = client ?? http.Client(),
        _baseUrl = baseUrl ??
            const String.fromEnvironment(
              'LUMAR_API_URL',
              defaultValue: 'http://127.0.0.1:5093',
            );

  final http.Client _client;
  final String _baseUrl;

  Future<CurrencySettings> load() async {
    final values = await Future.wait([
      _read('Currency'),
      _read('PreferredCurrency'),
    ]);
    return CurrencySettings.fromValues(
      currencyName: values[0],
      preferredCurrency: values[1],
    );
  }

  Future<String> _read(String key) async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/settings/by-key/${Uri.encodeComponent(key)}'),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر قراءة إعداد العملة: ${response.statusCode}');
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return json['settingValue']?.toString() ?? '';
  }

  Future<CurrencySettings> update({
    required String currencyName,
    required String preferredCurrency,
  }) async {
    final option = supportedCurrencyOptions.firstWhere(
      (item) => item.code == preferredCurrency.trim().toUpperCase(),
      orElse: () => throw ArgumentError('العملة المحددة غير مدعومة للعرض.'),
    );
    if (currencyName.trim() != option.name &&
        !(option.code == 'YER' && currencyName.trim() == 'ريال')) {
      throw ArgumentError('اسم العملة لا يطابق الكود المحدد.');
    }
    final response = await _client.put(
      Uri.parse('$_baseUrl/settings/currency'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'currencyName': currencyName,
        'preferredCurrency': preferredCurrency,
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر حفظ إعدادات العملة: ${response.statusCode}');
    }
    return load();
  }

  void dispose() => _client.close();
}
