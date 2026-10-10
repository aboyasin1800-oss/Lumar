import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/employee_models.dart';
import 'package:tailoring_system/screens/employee_form_screen.dart';

void main() {
  group('employee form hire date contract', () {
    test('displayed hire date format parses to a valid DateTime', () {
      final parsed = parseHireDateFromDisplay('2026/10/10');
      expect(parsed, isNotNull);
      expect(parsed, equals(DateTime(2026, 10, 10)));
    });

    test('payload includes a non-empty ISO hire date value', () {
      final value = parseHireDateFromDisplay('2026/10/10');
      final payload = EmployeeWritePayload(
        fullName: 'Test Employee',
        departmentId: 24,
        basicSalary: 60000,
        hireDate: value,
        status: 'Active',
        salaryType: 'BasicSalary',
      );

      final json = payload.toJson();
      expect(json['hireDate'], isA<String>());
      expect((json['hireDate'] as String).isNotEmpty, isTrue);
      expect((json['hireDate'] as String).contains('2026-10-10'), isTrue);
    });

    test('null is rejected before save', () {
      final parsed = parseHireDateFromDisplay('');
      expect(parsed, isNull);
    });

    test('basic salary payload keeps ISO hire date', () {
      final value = parseHireDateFromDisplay('2026/10/10');
      final payload = EmployeeWritePayload(
        fullName: 'لؤي محمد علي الفقيه',
        departmentId: 24,
        basicSalary: 60000,
        hireDate: value,
        status: 'Active',
        salaryType: 'BasicSalary',
      );

      expect(payload.toJson()['hireDate'], '2026-10-10T00:00:00.000');
    });

    test('piece wage payload keeps ISO hire date', () {
      final value = parseHireDateFromDisplay('2026/10/10');
      final payload = EmployeeWritePayload(
        fullName: 'عبدالقادر امين علي ياسين',
        departmentId: 25,
        basicSalary: 0,
        hireDate: value,
        status: 'Active',
        salaryType: 'PieceWage',
        pieceRates: const [
          EmployeePieceRateRow(pieceType: 'كوت', stage: 'خياطة', rateText: '200'),
        ],
      );

      expect(payload.toJson()['hireDate'], '2026-10-10T00:00:00.000');
    });
  });
}
