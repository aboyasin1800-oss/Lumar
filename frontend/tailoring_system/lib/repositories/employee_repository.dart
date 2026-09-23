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
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    return (jsonDecode(response.body) as List)
        .cast<EmployeeJson>()
        .map(fromJson)
        .toList();
  }

  Future<EmployeeJson> _object(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    return jsonDecode(response.body) as EmployeeJson;
  }

  Future<EmployeeDetails> _postJson(String path, Map<String, dynamic> body) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl$path'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
      body: jsonEncode(body),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    return EmployeeDetails.fromJson(jsonDecode(response.body) as EmployeeJson);
  }

  Future<EmployeeDetails> _putJson(String path, Map<String, dynamic> body) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl$path'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
      body: jsonEncode(body),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    return EmployeeDetails.fromJson(jsonDecode(response.body) as EmployeeJson);
  }

  Future<List<EmployeeSummary>> getEmployees() =>
      _list('/employees', EmployeeSummary.fromJson);

  Future<EmployeeDetails> getEmployee(int employeeId) async =>
      EmployeeDetails.fromJson(await _object('/employees/$employeeId'));

  Future<List<EmployeeDepartment>> getDepartments() =>
      _list('/employees/departments', EmployeeDepartment.fromJson);

  Future<List<String>> getProductionRouteOptions() async {
    final response = await _client.get(Uri.parse('$_baseUrl/settings/production-routes/options'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/settings/production-routes/options',
        response.statusCode,
        _extractMessage(response),
      );
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      return const <String>[];
    }
    return decoded
        .whereType<String>()
        .map((value) => value.trim())
        .where((value) => value.isNotEmpty)
        .toList();
  }

  Future<List<EmployeeDocument>> getDocuments(int employeeId) =>
      _list('/employees/$employeeId/documents', EmployeeDocument.fromJson);

  Future<List<EmployeeContractTemplate>> getContractTemplates() =>
      _list('/employees/contract-templates', EmployeeContractTemplate.fromJson);

  Future<EmployeeContract> getEmployeeContract(int employeeId) async =>
      EmployeeContract.fromJson(await _object('/employees/$employeeId/contract'));

  Future<EmployeeContract> generateEmployeeContract(int employeeId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/employees/$employeeId/contract/generate'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/$employeeId/contract/generate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return EmployeeContract.fromJson(jsonDecode(response.body) as EmployeeJson);
  }

  Future<List<EmployeeAttendanceRecord>> getAttendance(int employeeId) => _list(
      '/employees/$employeeId/attendance', EmployeeAttendanceRecord.fromJson);

  Future<List<EmployeeLeaveRequest>> getLeaveRequests(int employeeId) => _list(
      '/employees/$employeeId/leave-requests', EmployeeLeaveRequest.fromJson);

  Future<EmployeeDetails> createEmployee(EmployeeWritePayload payload) =>
      _postJson('/employees', payload.toJson());

  Future<EmployeeDetails> updateEmployee(int employeeId, EmployeeWritePayload payload) =>
      _putJson('/employees/$employeeId', payload.toJson());

  Future<EmployeeDetails> activateEmployee(int employeeId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/employees/$employeeId/activate'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/$employeeId/activate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return EmployeeDetails.fromJson(jsonDecode(response.body) as EmployeeJson);
  }

  Future<EmployeeDetails> deactivateEmployee(int employeeId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/employees/$employeeId/deactivate'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/$employeeId/deactivate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return EmployeeDetails.fromJson(jsonDecode(response.body) as EmployeeJson);
  }

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

String _extractMessage(http.Response response) {
  try {
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) {
      final message = decoded['message'] ?? decoded['error'];
      if (message is String && message.isNotEmpty) return message;
    }
    if (decoded is String && decoded.isNotEmpty) return decoded;
  } catch (_) {}

  return switch (response.statusCode) {
    400 => 'بيانات الموظف غير صحيحة.',
    409 => 'رقم الموظف موجود بالفعل.',
    404 => 'الموظف المطلوب غير موجود.',
    _ => 'تعذر تنفيذ العملية المطلوبة.',
  };
}

class EmployeeApiException implements Exception {
  const EmployeeApiException(this.path, this.statusCode, this.message);
  final String path;
  final int statusCode;
  final String message;

  @override
  String toString() => 'EmployeeApiException(path: $path, statusCode: $statusCode, message: $message)';
}
