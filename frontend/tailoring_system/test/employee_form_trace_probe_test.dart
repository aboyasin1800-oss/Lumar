import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:tailoring_system/models/employee_models.dart';
import 'package:tailoring_system/repositories/employee_repository.dart';
import 'package:tailoring_system/screens/employee_form_screen.dart';

void main() {
  testWidgets('employee form save emits real request/response trace without creating a DB row', (tester) async {
    final client = MockClient((request) async {
      switch (request.url.path) {
        case '/employees/departments':
          return http.Response(
            jsonEncode([
              {
                'departmentId': 1,
                'departmentCode': 'D-01',
                'departmentName': 'الخياطة',
                'description': null,
                'isActive': true,
                'createdAt': '2024-01-01T00:00:00.000',
                'updatedAt': null,
              },
            ]),
            200,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        case '/settings/production-routes/options':
          return http.Response(jsonEncode(['بنطلون', 'قميص']), 200,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/settings/production-routes':
          return http.Response(jsonEncode({'routes': []}), 200,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case '/employees':
          expect(request.method, 'POST');
          final body = jsonDecode(request.body);
          expect(body['fullName'], 'تجربة تتبع شاشة الموظف');
          expect(body['salaryType'], 'BasicSalary');
          return http.Response(
            jsonEncode({
              'employeeId': 999,
              'employeeCode': 'MO-TRACE-01',
              'employeeName': 'تجربة تتبع شاشة الموظف',
              'jobTitle': null,
              'scannerCode': null,
              'phoneNumber': '0500000000',
              'baseSalary': 0,
              'notes': null,
              'isActive': true,
              'salaryType': 'BasicSalary',
              'fixedSalary': null,
              'nationalId': null,
              'phone': null,
              'email': null,
              'address': null,
              'hireDate': '2026-10-11T00:00:00.000',
              'terminationDate': null,
              'status': 'Active',
              'departmentId': 1,
              'basicSalary': 5000,
              'pieceWageRate': 0,
              'overtimeHourlyRate': 0,
              'createdAt': '2026-10-11T00:00:00.000',
              'updatedAt': '2026-10-11T00:00:00.000',
              'contractNumber': null,
              'contractType': null,
              'contractStatus': null,
              'contractStartDate': null,
              'contractEndDate': null,
              'contractSignedDate': null,
              'contractNotes': null,
              'contractFilePath': null,
              'contractTemplateId': null,
              'contractText': null,
            }),
            201,
            headers: {'Content-Type': 'application/json; charset=utf-8'},
          );
        default:
          return http.Response('not found', 404);
      }
    });

    await tester.pumpWidget(
      MaterialApp(
        home: EmployeeFormScreen(
          repository: EmployeeRepository(client: client),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextFormField).at(1), 'تجربة تتبع شاشة الموظف');
    await tester.enterText(find.byType(TextFormField).at(2), '5000');
    await tester.tap(find.text('حفظ'));
    await tester.pump();
    await tester.pumpAndSettle();

    expect(find.text('تمت إضافة الموظف بنجاح.'), findsOneWidget);
  });
}
