import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/employee_models.dart';

class EmployeeRepository {
  EmployeeRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final http.Client _client;

  Future<List<T>> _list<T>(
      String path, T Function(EmployeeJson) fromJson) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode);
    }
    return (jsonDecode(response.body) as List)
        .cast<EmployeeJson>()
        .map(fromJson)
        .toList();
  }

  Future<EmployeeJson> _object(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode);
    }
    return jsonDecode(response.body) as EmployeeJson;
  }

  Future<List<EmployeeSummary>> getEmployees() =>
      _list('/employees', EmployeeSummary.fromJson);
  Future<EmployeeDetails> getEmployee(int employeeId) async =>
      EmployeeDetails.fromJson(await _object('/employees/$employeeId'));
  Future<List<EmployeeDepartment>> getDepartments() =>
      _list('/employees/departments', EmployeeDepartment.fromJson);
  Future<List<EmployeeDocument>> getDocuments(int employeeId) =>
      _list('/employees/$employeeId/documents', EmployeeDocument.fromJson);
  Future<List<EmployeeAttendanceRecord>> getAttendance(int employeeId) => _list(
      '/employees/$employeeId/attendance', EmployeeAttendanceRecord.fromJson);
  Future<List<EmployeeLeaveRequest>> getLeaveRequests(int employeeId) => _list(
      '/employees/$employeeId/leave-requests', EmployeeLeaveRequest.fromJson);

  Future<List<EmployeeOverviewRow>> getOverview() async {
    final results = await Future.wait([getEmployees(), getDepartments()]);
    final employees = results[0] as List<EmployeeSummary>;
    final departments = {
      for (final department in results[1] as List<EmployeeDepartment>)
        department.id: department
    };
    final details = await Future.wait(
        employees.map((employee) => getEmployee(employee.id)));
    return [
      for (var index = 0; index < employees.length; index++)
        EmployeeOverviewRow(
            summary: employees[index],
            details: details[index],
            department: departments[employees[index].departmentId]),
    ];
  }

  Future<EmployeeDetailsData> getDetails(int employeeId) async {
    final results = await Future.wait([
      getEmployee(employeeId),
      getDepartments(),
      getDocuments(employeeId),
      getAttendance(employeeId),
      getLeaveRequests(employeeId)
    ]);
    final employee = results[0] as EmployeeDetails;
    final departments = {
      for (final department in results[1] as List<EmployeeDepartment>)
        department.id: department
    };
    return EmployeeDetailsData(
      employee: employee,
      department: departments[employee.departmentId],
      documents: results[2] as List<EmployeeDocument>,
      attendance: results[3] as List<EmployeeAttendanceRecord>,
      leaveRequests: results[4] as List<EmployeeLeaveRequest>,
    );
  }
}

class EmployeeApiException implements Exception {
  const EmployeeApiException(this.path, this.statusCode);
  final String path;
  final int statusCode;
}
