import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/models/employee_models.dart';
import 'package:tailoring_system/repositories/employee_repository.dart';
import 'package:tailoring_system/repositories/payroll_repository.dart';
import 'package:tailoring_system/screens/attendance/attendance_report_screen.dart';
import 'package:tailoring_system/screens/attendance/attendance_screen.dart';
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
  Future<EmployeeDetailsData> getDetails(int employeeId) async =>
      EmployeeDetailsData(
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
  Future<EmployeeContract> getEmployeeContract(int employeeId) async =>
      EmployeeContract(
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
        text:
            'عقد عمل تجريبي للموظف عبد القادر امين برقم MO-0033 في قسم قسم الخياطة.',
        createdAt: DateTime(2026, 9, 14),
        updatedAt: DateTime(2026, 9, 14),
      );
}

void main() {
  test('generateEmployeeContract uses POST to the generation endpoint',
      () async {
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

  testWidgets(
      'piece wage mode shows piece rows and contract details are available',
      (tester) async {
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
    expect(find.text('السعر العام'), findsWidgets);
    expect(find.text('إضافي'), findsWidgets);
    expect(find.text('إضافة سطر'), findsOneWidget);

    expect(find.text('بيانات العقد'), findsOneWidget);
    expect(find.text('رقم العقد'), findsOneWidget);
    expect(find.text('حالة العقد'), findsOneWidget);
  });

  testWidgets(
      'employee details screen loads contract data from repository and shows print action',
      (tester) async {
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

  test(
      'employee overview keeps rows even when one employee details fetch fails',
      () async {
    final client = MockClient((request) async {
      switch (request.url.path) {
        case '/employees':
          return http.Response(
            jsonEncode([
              {
                'employeeId': 1,
                'employeeCode': 'EMP-001',
                'employeeName': 'Alice',
                'jobTitle': 'Tailor',
                'phoneNumber': '0501111111',
                'isActive': true,
                'status': 'Active',
                'departmentId': 7
              },
              {
                'employeeId': 2,
                'employeeCode': 'EMP-002',
                'employeeName': 'Sara',
                'jobTitle': 'Supervisor',
                'phoneNumber': '0502222222',
                'isActive': true,
                'status': 'Active',
                'departmentId': 7
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees/departments':
          return http.Response(
            jsonEncode([
              {
                'departmentId': 7,
                'departmentCode': 'D-07',
                'departmentName': 'Sewing',
                'description': null,
                'isActive': true
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees/1':
          return http.Response(
            jsonEncode({
              'employeeId': 1,
              'employeeCode': 'EMP-001',
              'employeeName': 'Alice',
              'fullName': 'Alice',
              'jobTitle': 'Tailor',
              'scannerCode': 'SC-1',
              'phoneNumber': '0501111111',
              'baseSalary': 0,
              'notes': null,
              'isActive': true,
              'salaryType': 'BasicSalary',
              'fixedSalary': 0,
              'nationalId': '111',
              'phone': '0501111111',
              'email': null,
              'address': null,
              'hireDate': '2024-01-01T00:00:00.000',
              'terminationDate': null,
              'status': 'Active',
              'departmentId': 7,
              'basicSalary': 2000,
              'pieceWageRate': 0,
              'overtimeHourlyRate': 0,
              'createdAt': '2024-01-01T00:00:00.000',
              'updatedAt': null,
              'pieceRates': const [],
            }),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees/2':
          return http.Response('server error', 500);
        default:
          throw StateError('Unexpected request: ${request.url.path}');
      }
    });

    final repository = EmployeeRepository(client: client);
    final rows = await repository.getOverview();

    expect(rows, hasLength(2));
    expect(rows[0].summary.name, 'Alice');
    expect(rows[1].details.code, 'EMP-002');
    expect(rows[1].details.hireDate, isNotNull);
  });

  testWidgets(
      'attendance screen keeps other employees when one attendance request fails',
      (tester) async {
    final repo = _PartialAttendanceEmployeeRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: AttendanceScreen(repository: repo),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Alice'), findsOneWidget);
    expect(find.textContaining('تعذر تحميل الحضور'), findsNothing);
  });

  testWidgets(
      'attendance report keeps other employees when one attendance request fails',
      (tester) async {
    final repo = _PartialAttendanceEmployeeRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: AttendanceReportScreen(repository: repo),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Alice'), findsOneWidget);
    expect(find.textContaining('تعذر تحميل تقرير الحضور'), findsNothing);
  });

  test('payroll overview keeps rows when one employee draw request fails',
      () async {
    final client = MockClient((request) async {
      switch (request.url.path) {
        case '/payroll/periods':
          return http.Response(
            jsonEncode([
              {
                'payrollPeriodId': 10,
                'periodCode': 'P-10',
                'periodStatus': 'Generated',
                'startDate': '2026-10-01T00:00:00.000',
                'endDate': '2026-10-15T00:00:00.000',
                'generatedAt': '2026-10-01T00:00:00.000',
                'approvedAt': null,
                'notes': null
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/payroll/records':
          return http.Response(
            jsonEncode([
              {
                'payrollRecordId': 20,
                'payrollPeriodId': 10,
                'employeeId': 1,
                'status': 'Generated',
                'baseSalary': 1000,
                'netSalary': 1000,
                'grossSalary': 1000,
                'totalAllowances': 0,
                'totalDraws': 0,
                'paidAt': null,
                'createdAt': '2026-10-01T00:00:00.000'
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees':
          return http.Response(
            jsonEncode([
              {
                'employeeId': 1,
                'employeeCode': 'EMP-001',
                'employeeName': 'Alice',
                'jobTitle': 'Tailor',
                'phoneNumber': '0501111111',
                'isActive': true,
                'status': 'Active',
                'departmentId': 7
              },
              {
                'employeeId': 2,
                'employeeCode': 'EMP-002',
                'employeeName': 'Sara',
                'jobTitle': 'Supervisor',
                'phoneNumber': '0502222222',
                'isActive': true,
                'status': 'Active',
                'departmentId': 7
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees/departments':
          return http.Response(
            jsonEncode([
              {
                'departmentId': 7,
                'departmentCode': 'D-07',
                'departmentName': 'Sewing',
                'description': null,
                'isActive': true
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/payroll/piece-wages':
          return http.Response('[]', 200,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/payroll/records/20/items':
          return http.Response('[]', 200,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/employees/1/draws':
          return http.Response('[]', 200,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/employees/2/draws':
          return http.Response('server error', 500);
        default:
          throw StateError('Unexpected request: ${request.url.path}');
      }
    });

    final overview = await PayrollRepository(client: client).getOverview();

    expect(overview.employees, hasLength(2));
    expect(overview.drawsByEmployee[1], isEmpty);
    expect(overview.drawsByEmployee[2], isEmpty);
  });

  test('piece wage payload sends zero basic salary and stage in each rate row',
      () {
    const payload = EmployeeWritePayload(
      fullName: 'عبد الله أحمد',
      departmentId: 1,
      basicSalary: 0,
      hireDate: null,
      status: 'Active',
      salaryType: 'PieceWage',
      pieceRates: [
        EmployeePieceRateRow(
            pieceType: 'بنطلون', stage: 'Cutting', rateText: '15.50'),
      ],
    );

    final json = payload.toJson();
    expect(json['basicSalary'], 0);
    expect(json['pieceRates'], isA<List>());
    expect((json['pieceRates'] as List).first['stage'], 'Cutting');
    expect((json['pieceRates'] as List).first['rate'], 15.5);
  });

  test(
      'EmployeeDetails accepts a wrapped payload and falls back when fullName is null',
      () {
    final json = {
      'data': {
        'employeeId': 42,
        'employeeCode': 'MO-0042',
        'employeeName': 'سامي علي',
        'fullName': null,
        'jobTitle': 'خياط',
        'scannerCode': 'SC-42',
        'phoneNumber': '0500000042',
        'baseSalary': 0,
        'notes': null,
        'isActive': true,
        'salaryType': 'PieceWage',
        'fixedSalary': null,
        'nationalId': '1234567890',
        'phone': '0500000042',
        'email': null,
        'address': null,
        'hireDate': '2026-09-01T00:00:00.000',
        'terminationDate': null,
        'status': 'Active',
        'departmentId': 11,
        'basicSalary': 1200,
        'pieceWageRate': 18.5,
        'overtimeHourlyRate': 0,
        'createdAt': '2026-09-01T00:00:00.000',
        'updatedAt': null,
        'pieceRates': <Map<String, dynamic>>[],
      }
    };

    final employee =
        EmployeeDetails.fromJson(json['data'] as Map<String, dynamic>);

    expect(employee.id, 42);
    expect(employee.code, 'MO-0042');
    expect(employee.name, 'سامي علي');
    expect(employee.fullName, 'سامي علي');
    expect(employee.departmentId, 11);
  });

  test(
      'EmployeeRepository.getEmployee unwraps data envelopes and keeps EmployeeId as source of truth',
      () async {
    final client = MockClient((request) async {
      expect(request.method, 'GET');
      expect(request.url.path, '/employees/77');
      return http.Response(
        jsonEncode({
          'data': {
            'employeeId': 77,
            'employeeCode': 'MO-0077',
            'employeeName': 'محمد علي',
            'fullName': 'محمد علي',
            'jobTitle': 'خياط',
            'scannerCode': 'SC-77',
            'phoneNumber': '0500000077',
            'baseSalary': 0,
            'notes': null,
            'isActive': true,
            'salaryType': 'BasicSalary',
            'fixedSalary': 0,
            'nationalId': null,
            'phone': null,
            'email': null,
            'address': null,
            'hireDate': '2026-01-31T00:00:00.000',
            'terminationDate': null,
            'status': 'Active',
            'departmentId': 3,
            'basicSalary': 2000,
            'pieceWageRate': 0,
            'overtimeHourlyRate': 0,
            'createdAt': '2026-01-31T00:00:00.000',
            'updatedAt': null,
            'pieceRates': const [],
          },
        }),
        200,
        headers: {'Content-Type': 'application/json; charset=utf-8'},
      );
    });

    final repository = EmployeeRepository(client: client);
    final employee = await repository.getEmployee(77);

    expect(employee.id, 77);
    expect(employee.code, 'MO-0077');
    expect(employee.name, 'محمد علي');
  });

  test('employee details remain available when attendance and leave requests fail',
      () async {
    final client = MockClient((request) async {
      switch (request.url.path) {
        case '/employees/99':
          return http.Response(
            jsonEncode({
              'employeeId': 99,
              'employeeCode': 'MO-0099',
              'employeeName': 'Faris Hasan',
              'fullName': 'Faris Hasan',
              'jobTitle': 'Tailor',
              'scannerCode': 'SC-99',
              'phoneNumber': '0500000099',
              'baseSalary': 0,
              'notes': null,
              'isActive': true,
              'salaryType': 'BasicSalary',
              'fixedSalary': 0,
              'nationalId': null,
              'phone': null,
              'email': null,
              'address': null,
              'hireDate': '2026-02-03T00:00:00.000',
              'terminationDate': null,
              'status': 'Active',
              'departmentId': 5,
              'basicSalary': 3000,
              'pieceWageRate': 0,
              'overtimeHourlyRate': 0,
              'createdAt': '2026-02-03T00:00:00.000',
              'updatedAt': null,
              'pieceRates': const [],
            }),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees/departments':
          return http.Response(
            jsonEncode([
              {
                'departmentId': 5,
                'departmentCode': 'D-05',
                'departmentName': 'Sewing',
                'description': null,
                'isActive': true,
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/employees/99/documents':
          return http.Response('[]', 200,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/employees/99/attendance':
          return http.Response('Unauthorized', 401,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/employees/99/leave-requests':
          return http.Response('Forbidden', 403,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        default:
          throw StateError('Unexpected request: ${request.url.path}');
      }
    });

    final repository = EmployeeRepository(client: client);
    final data = await repository.getDetails(99);

    expect(data.employee.id, 99);
    expect(data.employee.name, 'Faris Hasan');
    expect(data.documents, isEmpty);
    expect(data.attendance, isEmpty);
    expect(data.leaveRequests, isEmpty);
  });
}

class _PartialAttendanceEmployeeRepository extends EmployeeRepository {
  @override
  Future<List<EmployeeSummary>> getEmployees() async => const [
        EmployeeSummary(
          id: 1,
          code: 'EMP-001',
          name: 'Alice',
          jobTitle: 'Tailor',
          phoneNumber: '0501111111',
          isActive: true,
          status: 'Active',
          departmentId: 7,
        ),
        EmployeeSummary(
          id: 2,
          code: 'EMP-002',
          name: 'Sara',
          jobTitle: 'Supervisor',
          phoneNumber: '0502222222',
          isActive: true,
          status: 'Active',
          departmentId: 7,
        ),
      ];

  @override
  Future<List<EmployeeAttendanceRecord>> getAttendance(int employeeId) async {
    if (employeeId == 2) {
      throw EmployeeApiException(
          '/employees/$employeeId/attendance', 401, 'unauthorized');
    }

    return [
      EmployeeAttendanceRecord(
        id: 101,
        employeeId: employeeId,
        attendanceDate: DateTime(2026, 10, 11),
        checkInTime: DateTime(2026, 10, 11, 8, 0),
        checkOutTime: DateTime(2026, 10, 11, 16, 0),
        workedHours: 8,
        overtimeHours: 0,
        isAbsent: false,
        absenceReason: null,
        notes: null,
      ),
    ];
  }
}
