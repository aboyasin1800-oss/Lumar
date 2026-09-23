import 'package:intl/intl.dart';

import '../services/currency_settings_service.dart';

class CurrencyFormatter {
  CurrencyFormatter({CurrencySettings? settings})
      : _settings = settings ??
            CurrencySettings.fromValues(
              currencyName: 'ريال',
              preferredCurrency: 'YER',
            );

  final CurrencySettings _settings;
  final NumberFormat _amountFormat = NumberFormat('#,##0.00');

  String amount(num value, {bool includeSymbol = true}) {
    final formatted = _amountFormat.format(value);
    return includeSymbol ? '$formatted ${_settings.displaySymbol}' : formatted;
  }

  String amountWithName(num value) =>
      '${_amountFormat.format(value)} ${_settings.displayName}';

  String get code => _settings.displayCode;
  String get symbol => _settings.displaySymbol;
  String get name => _settings.displayName;
}
