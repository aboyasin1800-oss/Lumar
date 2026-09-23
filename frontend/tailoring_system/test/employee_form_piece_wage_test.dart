import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/models/employee_models.dart';
import 'package:tailoring_system/repositories/employee_repository.dart';
import 'package:tailoring_system/screens/employee_details_screen.dart';
import 'package:tailoring_system/screens/employee_form_screen.dart';

class _FakeEmployeeRepository extends EmployeeRepository {
  @override
  Future<List<EmployeeDepartment>> getDepartments() async => const [
    EmployeeDepartment(
      id: 1,
      code: 'D-01',
      name: 'الخياطة',
      description: 'قسم الخياطة',
      isActive: true,
    ),
  ];

  @override
  Future<List<String>> getProductionRouteOptions() async => const [
    'بنطلون',
    'قميص',
    'فستان',
  ];
}

class _FakeEmployeeDetailsRepository extends EmployeeRepository {
  @override
  Future<EmployeeDetails> getEmployee(int employeeId) async => EmployeeDetails(
        id: 8,
        code: 'MO-0033',
        name: 'عبد القادر امين',
        fullName: 'عبد القادر امين',
        jobTitle: 'خياط',
        scannerCode: 'SC-1001',
        phoneNumber: '0500000000',
        baseSalary: 0,
        notes: null,
        isActive: true,
        salaryType: 'PieceWage',
        fixedSalary: null,
        nationalId: '1234567890',
        phone: '0500000000',
        email: null,
        address: null,
        hireDate: DateTime(2026, 9, 1),
        terminationDate: null,
        status: 'Active',
        departmentId: 11,
        basicSalary: 0,
        pieceWageRate: 12.5,
        overtimeHourlyRate: 0,
        createdAt: DateTime(2026, 9, 1),
        updatedAt: DateTime(2026, 9, 1),
      );

  @override
  Future<EmployeeDetailsData> getDetails(int employeeId) async => EmployeeDetailsData(
        employee: EmployeeDetails(
          id: 8,
          code: 'MO-0033',
          name: 'عبد القادر امين',
          fullName: 'عبد القادر امين',
          jobTitle: 'خياط',
          scannerCode: 'SC-1001',
          phoneNumber: '0500000000',
          baseSalary: 0,
          notes: null,
          isActive: true,
          salaryType: 'PieceWage',
          fixedSalary: null,
          nationalId: '1234567890',
          phone: '0500000000',
          email: null,
          address: null,
          hireDate: DateTime(2026, 9, 1),
          terminationDate: null,
          status: 'Active',
          departmentId: 11,
          basicSalary: 0,
          pieceWageRate: 12.5,
          overtimeHourlyRate: 0,
          createdAt: DateTime(2026, 9, 1),
          updatedAt: DateTime(2026, 9, 1),
        ),
        department: const EmployeeDepartment(
          id: 11,
          code: 'D-11',
          name: 'قسم الخياطة',
          description: null,
          isActive: true,
        ),
        documents: const [],
        attendance: const [],
        leaveRequests: const [],
      );

  @override
  Future<EmployeeContract> getEmployeeContract(int employeeId) async => EmployeeContract(
        id: 1,
        employeeId: 8,
        contractTemplateId: 2,
        number: 'CNT-8-20260914',
        type: 'Standard',
        status: 'Active',
        startDate: DateTime(2026, 9, 1),
        endDate: DateTime(2026, 12, 31),
        signedDate: DateTime(2026, 9, 14),
        notes: null,
        filePath: null,
        text: 'عقد عمل تجريبي للموظف عبد القادر امين برقم MO-0033 في قسم قسم الخياطة.',
        createdAt: DateTime(2026, 9, 14),
        updatedAt: DateTime(2026, 9, 14),
      );
}

void main() {
  test('generateEmployeeContract uses POST to the generation endpoint', () async {
    final client = MockClient((request) async {
      expect(request.method, 'POST');
      expect(request.url.path, '/employees/9/contract/generate');

      return http.Response(
        jsonEncode({
          'employeeContractId': 101,
          'employeeId': 9,
          'contractTemplateId': 2,
          'contractNumber': 'CNT-9-20260914',
          'contractType': 'Standard',
          'contractStatus': 'Active',
          'contractStartDate': '2026-09-14T00:00:00.000',
          'contractEndDate': '2026-12-31T00:00:00.000',
          'contractSignedDate': '2026-09-14T00:00:00.000',
          'contractNotes': null,
          'contractFilePath': null,
          'contractText': 'Generated contract text for employee 9',
          'createdAt': '2026-09-14T00:00:00.000',
          'updatedAt': '2026-09-14T00:00:00.000',
        }),
        200,
      );
    });

    final repository = EmployeeRepository(client: client);
    final contract = await repository.generateEmployeeContract(9);

    expect(contract.id, 101);
    expect(contract.number, 'CNT-9-20260914');
    expect(contract.text, 'Generated contract text for employee 9');
  });

  testWidgets('piece wage mode shows piece rows and contract details are available', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeFormScreen(
          repository: _FakeEmployeeRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('employee_salary_type')));
    await tester.pumpAndSettle();

    expect(find.text('نوع القطعة'), findsNothing);

    await tester.tap(find.text('أجر قطعة').last);
    await tester.pumpAndSettle();

    expect(find.text('نوع القطعة'), findsWidgets);
    expect(find.text('السعر'), findsWidgets);
    expect(find.text('إضافة سطر'), findsOneWidget);

    expect(find.text('بيانات العقد'), findsOneWidget);
    expect(find.text('رقم العقد'), findsOneWidget);
    expect(find.text('حالة العقد'), findsOneWidget);
  });

  testWidgets('employee details screen loads contract data from repository and shows print action', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeDetailsScreen(
          employeeId: 8,
          repository: _FakeEmployeeDetailsRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    await tester.tap(find.text('عقد العمل'));
    await tester.pumpAndSettle();

    expect(find.text('CNT-8-20260914'), findsOneWidget);
    expect(find.text('Standard'), findsOneWidget);
    expect(find.text('Active'), findsOneWidget);
    expect(find.textContaining('عبد القادر امين'), findsWidgets);
    expect(find.text('طباعة العقد'), findsOneWidget);
  });
}
