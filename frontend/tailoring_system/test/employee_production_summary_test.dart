import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/employee_models.dart';
import 'package:tailoring_system/models/payroll_models.dart';
import 'package:tailoring_system/screens/employee_details_screen.dart';

void main() {
  group('employee production summary', () {
    test('counts only matching piece type and stage from the saved assignment rows', () {
      final employee = EmployeeDetails(
        id: 7,
        code: 'E-7',
        name: 'أحمد',
        fullName: 'أحمد',
        jobTitle: 'خياط',
        scannerCode: null,
        phoneNumber: null,
        baseSalary: null,
        notes: null,
        isActive: true,
        salaryType: 'PieceWage',
        fixedSalary: null,
        nationalId: null,
        phone: null,
        email: null,
        address: null,
        hireDate: DateTime(2024, 1, 1),
        terminationDate: null,
        status: 'Active',
        departmentId: 1,
        basicSalary: 0,
        pieceWageRate: 0,
        overtimeHourlyRate: 0,
        createdAt: DateTime(2024, 1, 1),
        updatedAt: null,
        pieceRates: const [
          EmployeePieceRateRow(productTypeId: 10, pieceType: 'قميص', stage: 'قطع', rateText: '5'),
          EmployeePieceRateRow(productTypeId: 11, pieceType: 'كوت', stage: 'خياطة', rateText: '6'),
        ],
      );

      final wages = [
        PieceWageRecord(
          id: 1,
          orderId: 1,
          pieceId: 100,
          employeeId: 7,
          employeeCode: 'E-7',
          pieceType: 'قميص',
          stage: 'قطع',
          quantity: 1,
          wageRate: 5,
          totalWage: 5,
          periodId: null,
          payrollRecordId: null,
          status: 'PendingPayroll',
          notes: null,
          createdAt: DateTime(2024, 2, 1),
        ),
        PieceWageRecord(
          id: 2,
          orderId: 1,
          pieceId: 100,
          employeeId: 7,
          employeeCode: 'E-7',
          pieceType: 'قميص',
          stage: 'قطع',
          quantity: 1,
          wageRate: 5,
          totalWage: 5,
          periodId: null,
          payrollRecordId: null,
          status: 'PendingPayroll',
          notes: null,
          createdAt: DateTime(2024, 2, 2),
        ),
        PieceWageRecord(
          id: 3,
          orderId: 2,
          pieceId: 200,
          employeeId: 7,
          employeeCode: 'E-7',
          pieceType: 'قميص',
          stage: 'خياطة',
          quantity: 1,
          wageRate: 6,
          totalWage: 6,
          periodId: null,
          payrollRecordId: null,
          status: 'PendingPayroll',
          notes: null,
          createdAt: DateTime(2024, 2, 3),
        ),
        PieceWageRecord(
          id: 4,
          orderId: 3,
          pieceId: 300,
          employeeId: 7,
          employeeCode: 'E-7',
          pieceType: 'كوت',
          stage: 'قطع',
          quantity: 1,
          wageRate: 6,
          totalWage: 6,
          periodId: null,
          payrollRecordId: null,
          status: 'PendingPayroll',
          notes: null,
          createdAt: DateTime(2024, 2, 4),
        ),
        PieceWageRecord(
          id: 5,
          orderId: 4,
          pieceId: 400,
          employeeId: 9,
          employeeCode: 'E-9',
          pieceType: 'قميص',
          stage: 'قطع',
          quantity: 1,
          wageRate: 5,
          totalWage: 5,
          periodId: null,
          payrollRecordId: null,
          status: 'PendingPayroll',
          notes: null,
          createdAt: DateTime(2024, 2, 5),
        ),
      ];

      final summary = summarizeEmployeeProduction(employee, wages);

      expect(summary, hasLength(2));
      expect(summary[0].pieceType, 'قميص');
      expect(summary[0].stage, 'قطع');
      expect(summary[0].count, 1);
      expect(summary[1].pieceType, 'كوت');
      expect(summary[1].stage, 'خياطة');
      expect(summary[1].count, 0);
    });

    test('returns zero for each saved assignment row with no matching production', () {
      final employee = EmployeeDetails(
        id: 8,
        code: 'E-8',
        name: 'سارة',
        fullName: 'سارة',
        jobTitle: 'قصاصة',
        scannerCode: null,
        phoneNumber: null,
        baseSalary: null,
        notes: null,
        isActive: true,
        salaryType: 'PieceWage',
        fixedSalary: null,
        nationalId: null,
        phone: null,
        email: null,
        address: null,
        hireDate: DateTime(2024, 1, 1),
        terminationDate: null,
        status: 'Active',
        departmentId: 1,
        basicSalary: 0,
        pieceWageRate: 0,
        overtimeHourlyRate: 0,
        createdAt: DateTime(2024, 1, 1),
        updatedAt: null,
        pieceRates: const [
          EmployeePieceRateRow(productTypeId: 20, pieceType: 'كوت', stage: 'قطع', rateText: '4'),
          EmployeePieceRateRow(productTypeId: 21, pieceType: 'قميص', stage: 'خياطة', rateText: '5'),
        ],
      );

      final summary = summarizeEmployeeProduction(employee, const <PieceWageRecord>[]);

      expect(summary, hasLength(2));
      expect(summary.any((entry) => entry.pieceType == 'كوت' && entry.stage == 'قطع' && entry.count == 0), isTrue);
      expect(summary.any((entry) => entry.pieceType == 'قميص' && entry.stage == 'خياطة' && entry.count == 0), isTrue);
    });
  });
}
