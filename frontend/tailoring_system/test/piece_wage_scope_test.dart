import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/models/employee_models.dart';
import 'package:tailoring_system/repositories/employee_repository.dart';
import 'package:tailoring_system/screens/employee_form_screen.dart';

http.Response _utf8JsonResponse(dynamic payload) {
  final bytes = Uint8List.fromList(utf8.encode(jsonEncode(payload)));
  return http.Response.bytes(bytes, 200,
      headers: {'content-type': 'application/json; charset=utf-8'});
}

class _FakePieceWageRepository extends EmployeeRepository {
  _FakePieceWageRepository() : super(client: MockClient((request) async {
          switch (request.url.path) {
            case '/employees/departments':
              return _utf8JsonResponse([
                {
                  'departmentId': 7,
                  'departmentCode': 'D-07',
                  'departmentName': 'Sewing',
                  'description': null,
                  'isActive': true,
                },
              ]);
            case '/settings/production-routes':
              return _utf8JsonResponse([
                {
                  'productTypeId': 11,
                  'productTypeCode': 'PANTS',
                  'productTypeNameAr': 'بنطلون',
                  'stages': ['Cutting', 'Sewing', 'Packing'],
                },
                {
                  'productTypeId': 12,
                  'productTypeCode': 'SHIRT',
                  'productTypeNameAr': 'قميص',
                  'stages': ['Cutting', 'Sewing'],
                },
                {
                  'productTypeId': 13,
                  'productTypeCode': 'FABRIC',
                  'productTypeNameAr': 'قماش',
                  'stages': ['Cutting', 'Printing', 'Finishing'],
                },
              ]);
            case '/payroll/piece-wage-rates':
              return _utf8JsonResponse([
                {'pieceType': 'بنطلون', 'stage': 'Cutting', 'wageRate': 18.5},
                {'pieceType': 'بنطلون', 'stage': 'Sewing', 'wageRate': 22.0},
                {'pieceType': 'قميص', 'stage': 'Cutting', 'wageRate': 17.25},
                {'pieceType': 'قميص', 'stage': 'Sewing', 'wageRate': 21.5},
              ]);
            default:
              throw StateError('Unexpected request: ${request.url.path}');
          }
        }));
}

void main() {
  test('PieceWage_loads_product_types_and_keeps_productTypeId', () async {
    final repository = _FakePieceWageRepository();
    final options = await repository.getPieceWageProductTypes();

    expect(options.map((item) => item.productTypeId).toList(), [11, 12, 13]);
    expect(options.first.displayName, 'بنطلون');
    expect(options.first.code, 'PANTS');
  });

  test('PieceWage_filters_allowed_stages_to_cutting_and_sewing_only', () async {
    final repository = _FakePieceWageRepository();
    final options = await repository.getPieceWageProductTypes();

    final pants = options.firstWhere((entry) => entry.productTypeId == 11);
    final shirt = options.firstWhere((entry) => entry.productTypeId == 12);

    expect(pants.allowedStages, ['Cutting', 'Sewing']);
    expect(shirt.allowedStages, ['Cutting', 'Sewing']);
    expect(pants.allowedStages, isNot(contains('Packing')));
    expect(shirt.allowedStages, isNot(contains('Finishing')));
  });

  test('PieceWage_rates_are_mapped_by_product_type_id', () async {
    final repository = _FakePieceWageRepository();
    final rates = await repository.getOfficialPieceWageRatesByProductTypeId();

    expect(rates[11]!['Cutting'], 18.5);
    expect(rates[11]!['Sewing'], 22.0);
    expect(rates[12]!['Cutting'], 17.25);
    expect(rates[12]!['Sewing'], 21.5);
  });

  test('PieceWage_unwraps_real_backend_routes_object_to_product_type_list', () async {
    final client = MockClient((request) async {
      switch (request.url.path) {
        case '/settings/production-routes':
          return _utf8JsonResponse({
            'routes': [
              {
                'productTypeId': 11,
                'productTypeCode': 'OVERCOAT',
                'productTypeNameAr': 'بالطو',
                'stages': ['Printing', 'Cutting', 'Sewing'],
              },
              {
                'productTypeId': 7,
                'productTypeCode': 'PANTS',
                'productTypeNameAr': 'بنطلون',
                'stages': ['Cutting', 'Sewing'],
              },
            ],
          });
        case '/payroll/piece-wage-rates':
          return _utf8JsonResponse([
            {'pieceType': 'بالطو', 'stage': 'Cutting', 'wageRate': 120.0},
            {'pieceType': 'بالطو', 'stage': 'Sewing', 'wageRate': 500.0},
            {'pieceType': 'بنطلون', 'stage': 'Cutting', 'wageRate': 140.0},
            {'pieceType': 'بنطلون', 'stage': 'Sewing', 'wageRate': 550.0},
          ]);
        default:
          throw StateError('Unexpected request: ${request.url.path}');
      }
    });

    final repository = EmployeeRepository(client: client);
    final options = await repository.getPieceWageProductTypes();
    final rates = await repository.getOfficialPieceWageRatesByProductTypeId();

    expect(options.map((item) => item.productTypeId).toList(), [7, 11]);
    expect(rates[7]!.containsKey('Cutting'), isTrue);
    expect(rates[11]!.containsKey('Sewing'), isTrue);
  });

  testWidgets('PieceWage_screen_loads_piece_rows_and_product_type_dropdown', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeFormScreen(repository: _FakePieceWageRepository()),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('employee_salary_type')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('أجر قطعة').last);
    await tester.pumpAndSettle();

    expect(find.text('أسعار القطع'), findsOneWidget);
    expect(find.text('نوع القطعة'), findsWidgets);
    expect(find.text('المرحلة'), findsWidgets);
    expect(find.text('السعر العام'), findsWidgets);
    expect(find.text('إضافي'), findsWidgets);
  });

  test('PieceWage_ignores_missing_rate_for_one_type_without_dropping_list', () async {
    final repository = _FakePieceWageRepository();
    final rates = await repository.getOfficialPieceWageRatesByProductTypeId();

    expect(rates.containsKey(11), isTrue);
    expect(rates.containsKey(12), isTrue);
    expect(rates.containsKey(13), isFalse);
  });

  test('PieceWage_accepts_positive_and_zero_additional_amounts', () {
    const payload = EmployeeWritePayload(
      fullName: 'عبد الله أحمد',
      departmentId: 1,
      basicSalary: 0,
      hireDate: null,
      status: 'Active',
      salaryType: 'PieceWage',
      pieceRates: [
        EmployeePieceRateRow(
          productTypeId: 11,
          pieceType: 'بنطلون',
          stage: 'Cutting',
          rateText: '0',
        ),
        EmployeePieceRateRow(
          productTypeId: 12,
          pieceType: 'قميص',
          stage: 'Sewing',
          rateText: '3.25',
        ),
      ],
    );

    final json = payload.toJson();
    expect((json['pieceRates'] as List).first['rate'], 0.0);
    expect((json['pieceRates'] as List).last['rate'], 3.25);
  });

  test('PieceWage_rejects_negative_additional_amounts', () {
    const row = EmployeePieceRateRow(
      productTypeId: 11,
      pieceType: 'بنطلون',
      stage: 'Cutting',
      rateText: '-1.5',
    );

    expect(row.parsedRate, isNegative);
    final invalid = row.parsedRate != null && row.parsedRate! < 0;
    expect(invalid, isTrue);
  });

  test('EmployeeDepartment_403_uses_explicit_permission_message_not_connection_error', () async {
    final client = MockClient((request) async => http.Response(
      jsonEncode({'message': 'Employee accounts can only access their own data.'}),
      403,
      headers: {'content-type': 'application/json; charset=utf-8'},
    ));

    final repository = EmployeeRepository(client: client);
    try {
      await repository.getDepartments();
      fail('Expected a 403 exception');
    } on EmployeeApiException catch (error) {
      expect(error.statusCode, 403);
      expect(error.message, contains('ليس لديك صلاحية'));
    }
  });

  test('PieceWage_keeps_product_type_id_in_saved_payload', () {
    const row = EmployeePieceRateRow(
      productTypeId: 12,
      pieceType: 'قميص',
      stage: 'Sewing',
      rateText: '2.5',
    );

    final json = row.toJson();
    expect(json['productTypeId'], 12);
    expect(json['pieceType'], 'قميص');
    expect(json['stage'], 'Sewing');
  });
}
