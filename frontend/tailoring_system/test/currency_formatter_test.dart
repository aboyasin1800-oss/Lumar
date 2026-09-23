import 'package:flutter_test/flutter_test.dart';

import 'package:tailoring_system/core/currency_formatter.dart';
import 'package:tailoring_system/services/currency_settings_service.dart';

void main() {
  test('formats Yemeni Rial for display without changing the amount', () {
    final formatter = CurrencyFormatter(
      settings: CurrencySettings.fromValues(
        currencyName: 'ريال',
        preferredCurrency: 'yer',
      ),
    );

    expect(formatter.amount(100000), '100,000.00 ر.ي.');
    expect(formatter.amountWithName(100000), '100,000.00 الريال اليمني');
    expect(formatter.code, 'YER');
  });

  test('exposes the approved display-only currency catalog', () {
    expect(supportedCurrencyOptions.map((option) => option.code), [
      'YER',
      'SAR',
      'USD',
      'EGP',
      'QAR',
      'OMR',
      'AED',
      'KWD',
      'IQD',
      'SDG',
    ]);

    final formatter = CurrencyFormatter(
      settings: CurrencySettings.fromValues(
        currencyName: 'الريال السعودي',
        preferredCurrency: 'SAR',
      ),
    );
    expect(formatter.amount(100000), '100,000.00 ر.س');
  });
}
