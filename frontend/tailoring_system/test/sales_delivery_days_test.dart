import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/sales_screen.dart';

void main() {
  test('adds delivery days to order creation date', () {
    final base = DateTime(2026, 1, 10);

    expect(calculateDeliveryDateFromDays(7, base: base),
        DateTime(2026, 1, 17));
    expect(calculateDeliveryDateFromDays(10, base: base),
        DateTime(2026, 1, 20));
    expect(calculateDeliveryDateFromDays(14, base: base),
        DateTime(2026, 1, 24));
  });
}
