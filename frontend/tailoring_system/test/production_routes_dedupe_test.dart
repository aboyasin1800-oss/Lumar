import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/screens/settings/production_routes_screen.dart';

void main() {
  test('official product types are deduplicated and route config is merged once', () {
    final official = [
      {'productTypeId': 1, 'nameAr': 'قميص', 'code': 'SHIRT'},
      {'productTypeId': 2, 'nameAr': 'قميص', 'code': 'SHIRT'},
      {'productTypeId': 3, 'nameAr': 'ثوب', 'code': 'THOBE'},
      {'productTypeId': 4, 'nameAr': 'ثوب قطري', 'code': 'THOBE_QATARI'},
      {'productTypeId': 5, 'nameAr': 'بنطلون', 'code': 'PANTS'},
    ];

    final routes = [
      {'productTypeId': 1, 'stages': ['Printing', 'Cutting']},
      {'productTypeId': 1, 'stages': ['Printing', 'Sewing']},
      {'productTypeId': 2, 'stages': ['Printing', 'Sewing']},
      {'productTypeId': 5, 'stages': ['Printing', 'Ironing']},
    ];

    final result = buildUniqueProductionRouteItems(
      officialProductTypes: official,
      routeEntries: routes,
    );

    expect(result.length, 5);
    expect(result.map((e) => e['nameAr']).toList(), containsAll(['قميص', 'ثوب', 'ثوب قطري', 'بنطلون']));
    expect(result.where((e) => e['nameAr'] == 'قميص').length, 2);
    expect(result.where((e) => e['nameAr'] == 'ثوب').length, 1);
    expect(result.where((e) => e['nameAr'] == 'بنطلون').length, 1);
    expect(result.map((e) => e['productTypeId']).toSet(), {1, 2, 3, 4, 5});
  });
}
