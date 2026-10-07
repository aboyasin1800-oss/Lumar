import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/employee_models.dart';
import '../services/auth_state.dart';

class EmployeeRepository {
  EmployeeRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final http.Client _client;

  Map<String, String> _headers({Map<String, String>? extra}) {
    final headers = <String, String>{if (extra != null) ...extra, 'Accept': 'application/json'};
    final token = AuthState.instance.token;
    if (token != null && token.trim().isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  Future<List<T>> _list<T>(String path, T Function(Map<String, dynamic>) fromJson) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'), headers: _headers());
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      return <T>[];
    }
    return decoded
        .whereType<Map<String, dynamic>>()
        .map(fromJson)
        .toList();
  }

  Future<Map<String, dynamic>> _object(String path) async {
    final response = await _client.get(Uri.parse('$_baseUrl$path'), headers: _headers());
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      return <String, dynamic>{};
    }
    return decoded;
  }

  Future<EmployeeDetails> _postJson(String path, Map<String, dynamic> body) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl$path'),
      headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
      body: jsonEncode(body),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return EmployeeDetails(
      id: json['employeeId'] as int,
      code: json['employeeCode'] as String,
      name: json['employeeName'] as String,
      fullName: json['fullName'] as String,
      jobTitle: json['jobTitle'] as String?,
      scannerCode: json['scannerCode'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      baseSalary: (json['baseSalary'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
      isActive: json['isActive'] as bool?,
      salaryType: json['salaryType'] as String?,
      fixedSalary: (json['fixedSalary'] as num?)?.toDouble(),
      nationalId: json['nationalId'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      address: json['address'] as String?,
      hireDate: DateTime.parse(json['hireDate'] as String),
      terminationDate: json['terminationDate'] == null
          ? null
          : DateTime.parse(json['terminationDate'] as String),
      status: json['status'] as String,
      departmentId: json['departmentId'] as int,
      basicSalary: (json['basicSalary'] as num).toDouble(),
      pieceWageRate: (json['pieceWageRate'] as num).toDouble(),
      overtimeHourlyRate: (json['overtimeHourlyRate'] as num).toDouble(),
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
  }

  Future<EmployeeDetails> _putJson(String path, Map<String, dynamic> body) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl$path'),
      headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
      body: jsonEncode(body),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(path, response.statusCode, _extractMessage(response));
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return EmployeeDetails(
      id: json['employeeId'] as int,
      code: json['employeeCode'] as String,
      name: json['employeeName'] as String,
      fullName: json['fullName'] as String,
      jobTitle: json['jobTitle'] as String?,
      scannerCode: json['scannerCode'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      baseSalary: (json['baseSalary'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
      isActive: json['isActive'] as bool?,
      salaryType: json['salaryType'] as String?,
      fixedSalary: (json['fixedSalary'] as num?)?.toDouble(),
      nationalId: json['nationalId'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      address: json['address'] as String?,
      hireDate: DateTime.parse(json['hireDate'] as String),
      terminationDate: json['terminationDate'] == null
          ? null
          : DateTime.parse(json['terminationDate'] as String),
      status: json['status'] as String,
      departmentId: json['departmentId'] as int,
      basicSalary: (json['basicSalary'] as num).toDouble(),
      pieceWageRate: (json['pieceWageRate'] as num).toDouble(),
      overtimeHourlyRate: (json['overtimeHourlyRate'] as num).toDouble(),
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
  }

  Future<List<EmployeeSummary>> getEmployees() =>
      _list('/employees', (json) => EmployeeSummary(
            id: json['employeeId'] as int,
            code: json['employeeCode'] as String,
            name: json['employeeName'] as String,
            jobTitle: json['jobTitle'] as String?,
            phoneNumber: json['phoneNumber'] as String?,
            isActive: json['isActive'] as bool?,
            status: json['status'] as String,
            departmentId: json['departmentId'] as int,
          ));

  Future<EmployeeDetails> getEmployee(int employeeId) async {
    final json = await _object('/employees/$employeeId');
    return EmployeeDetails(
      id: json['employeeId'] as int,
      code: json['employeeCode'] as String,
      name: json['employeeName'] as String,
      fullName: json['fullName'] as String,
      jobTitle: json['jobTitle'] as String?,
      scannerCode: json['scannerCode'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      baseSalary: (json['baseSalary'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
      isActive: json['isActive'] as bool?,
      salaryType: json['salaryType'] as String?,
      fixedSalary: (json['fixedSalary'] as num?)?.toDouble(),
      nationalId: json['nationalId'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      address: json['address'] as String?,
      hireDate: DateTime.parse(json['hireDate'] as String),
      terminationDate: json['terminationDate'] == null
          ? null
          : DateTime.parse(json['terminationDate'] as String),
      status: json['status'] as String,
      departmentId: json['departmentId'] as int,
      basicSalary: (json['basicSalary'] as num).toDouble(),
      pieceWageRate: (json['pieceWageRate'] as num).toDouble(),
      overtimeHourlyRate: (json['overtimeHourlyRate'] as num).toDouble(),
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
  }

  Future<List<EmployeeDepartment>> getDepartments() =>
      _list('/employees/departments', (json) => EmployeeDepartment(
            id: json['departmentId'] as int,
            code: json['departmentCode'] as String,
            name: json['departmentName'] as String,
            description: json['description'] as String?,
            isActive: json['isActive'] as bool,
          ));

  Future<List<String>> getProductionRouteOptions() async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/settings/production-routes/options'),
      headers: _headers(),
    );
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
      _list('/employees/$employeeId/documents', (json) => EmployeeDocument(
            id: json['employeeDocumentId'] as int,
            employeeId: json['employeeId'] as int,
            type: json['documentType'] as String,
            number: json['documentNumber'] as String?,
            issueDate: json['issueDate'] == null
                ? null
                : DateTime.parse(json['issueDate'] as String),
            expiryDate: json['expiryDate'] == null
                ? null
                : DateTime.parse(json['expiryDate'] as String),
            filePath: json['filePath'] as String?,
            notes: json['notes'] as String?,
            createdAt: DateTime.parse(json['createdAt'] as String),
          ));

  Future<void> ensureCurrentEmployeeAccess() async {
    final token = AuthState.instance.token;
    if (token == null || token.trim().isEmpty) {
      throw const EmployeeApiException('/auth/me', 401, 'لم يتم تسجيل الدخول.');
    }
  }

  Future<List<EmployeeContractTemplate>> getContractTemplates() =>
      _list('/employees/contract-templates', (json) => EmployeeContractTemplate(
            id: json['contractTemplateId'] as int,
            templateName: json['templateName'] as String,
            contractType: json['contractType'] as String,
            isActive: json['isActive'] as bool,
            templateText: json['templateText'] as String,
            createdAt: DateTime.parse(json['createdAt'] as String),
            updatedAt: json['updatedAt'] == null
                ? null
                : DateTime.parse(json['updatedAt'] as String),
          ));

  Future<EmployeeContract> getEmployeeContract(int employeeId) async {
    final json = await _object('/employees/$employeeId/contract');
    return EmployeeContract(
      id: json['employeeContractId'] as int,
      employeeId: json['employeeId'] as int,
      contractTemplateId: json['contractTemplateId'] as int?,
      number: json['contractNumber'] as String?,
      type: json['contractType'] as String?,
      status: json['contractStatus'] as String?,
      startDate: json['contractStartDate'] == null
          ? null
          : DateTime.parse(json['contractStartDate'] as String),
      endDate: json['contractEndDate'] == null
          ? null
          : DateTime.parse(json['contractEndDate'] as String),
      signedDate: json['contractSignedDate'] == null
          ? null
          : DateTime.parse(json['contractSignedDate'] as String),
      notes: json['contractNotes'] as String?,
      filePath: json['contractFilePath'] as String?,
      text: json['contractText'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
  }

  Future<EmployeeContract> generateEmployeeContract(int employeeId) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/employees/$employeeId/contract/generate'),
      headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/$employeeId/contract/generate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return EmployeeContract(
      id: json['employeeContractId'] as int,
      employeeId: json['employeeId'] as int,
      contractTemplateId: json['contractTemplateId'] as int?,
      number: json['contractNumber'] as String?,
      type: json['contractType'] as String?,
      status: json['contractStatus'] as String?,
      startDate: json['contractStartDate'] == null
          ? null
          : DateTime.parse(json['contractStartDate'] as String),
      endDate: json['contractEndDate'] == null
          ? null
          : DateTime.parse(json['contractEndDate'] as String),
      signedDate: json['contractSignedDate'] == null
          ? null
          : DateTime.parse(json['contractSignedDate'] as String),
      notes: json['contractNotes'] as String?,
      filePath: json['contractFilePath'] as String?,
      text: json['contractText'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
  }

  Future<List<EmployeeAttendanceRecord>> getAttendance(int employeeId) => _list(
      '/employees/$employeeId/attendance',
      (json) => EmployeeAttendanceRecord(
        id: json['employeeAttendanceId'] as int,
        employeeId: json['employeeId'] as int,
        attendanceDate: DateTime.parse(json['attendanceDate'] as String),
        checkInTime: json['checkInTime'] == null
            ? null
            : DateTime.parse(json['checkInTime'] as String),
        checkOutTime: json['checkOutTime'] == null
            ? null
            : DateTime.parse(json['checkOutTime'] as String),
        workedHours: (json['workedHours'] as num).toDouble(),
        overtimeHours: (json['overtimeHours'] as num).toDouble(),
        isAbsent: json['isAbsent'] as bool,
        absenceReason: json['absenceReason'] as String?,
        notes: json['notes'] as String?,
      ));

  Future<List<EmployeeLeaveRequest>> getLeaveRequests(int employeeId) => _list(
      '/employees/$employeeId/leave-requests',
      (json) => EmployeeLeaveRequest(
        id: json['leaveRequestId'] as int,
        employeeId: json['employeeId'] as int,
        type: json['leaveType'] as String,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
        requestedDays: (json['requestedDays'] as num).toDouble(),
        status: json['status'] as String,
        reason: json['reason'] as String?,
        approvedBy: json['approvedBy'] as String?,
        approvedAt: json['approvedAt'] == null
            ? null
            : DateTime.parse(json['approvedAt'] as String),
        createdAt: DateTime.parse(json['createdAt'] as String),
      ));

  Future<EmployeeDetails> createEmployee(EmployeeWritePayload payload) =>
      _postJson('/employees', payload.toJson());

  Future<EmployeeDetails> updateEmployee(int employeeId, EmployeeWritePayload payload) =>
      _putJson('/employees/$employeeId', payload.toJson());

  Future<EmployeeDetails> activateEmployee(int employeeId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/employees/$employeeId/activate'),
      headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/$employeeId/activate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return EmployeeDetails(
      id: json['employeeId'] as int,
      code: json['employeeCode'] as String,
      name: json['employeeName'] as String,
      fullName: json['fullName'] as String,
      jobTitle: json['jobTitle'] as String?,
      scannerCode: json['scannerCode'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      baseSalary: (json['baseSalary'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
      isActive: json['isActive'] as bool?,
      salaryType: json['salaryType'] as String?,
      fixedSalary: (json['fixedSalary'] as num?)?.toDouble(),
      nationalId: json['nationalId'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      address: json['address'] as String?,
      hireDate: DateTime.parse(json['hireDate'] as String),
      terminationDate: json['terminationDate'] == null
          ? null
          : DateTime.parse(json['terminationDate'] as String),
      status: json['status'] as String,
      departmentId: json['departmentId'] as int,
      basicSalary: (json['basicSalary'] as num).toDouble(),
      pieceWageRate: (json['pieceWageRate'] as num).toDouble(),
      overtimeHourlyRate: (json['overtimeHourlyRate'] as num).toDouble(),
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
  }

  Future<EmployeeDetails> deactivateEmployee(int employeeId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/employees/$employeeId/deactivate'),
      headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/$employeeId/deactivate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return EmployeeDetails(
      id: json['employeeId'] as int,
      code: json['employeeCode'] as String,
      name: json['employeeName'] as String,
      fullName: json['fullName'] as String,
      jobTitle: json['jobTitle'] as String?,
      scannerCode: json['scannerCode'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      baseSalary: (json['baseSalary'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
      isActive: json['isActive'] as bool?,
      salaryType: json['salaryType'] as String?,
      fixedSalary: (json['fixedSalary'] as num?)?.toDouble(),
      nationalId: json['nationalId'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      address: json['address'] as String?,
      hireDate: DateTime.parse(json['hireDate'] as String),
      terminationDate: json['terminationDate'] == null
          ? null
          : DateTime.parse(json['terminationDate'] as String),
      status: json['status'] as String,
      departmentId: json['departmentId'] as int,
      basicSalary: (json['basicSalary'] as num).toDouble(),
      pieceWageRate: (json['pieceWageRate'] as num).toDouble(),
      overtimeHourlyRate: (json['overtimeHourlyRate'] as num).toDouble(),
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: json['updatedAt'] == null
          ? null
          : DateTime.parse(json['updatedAt'] as String),
    );
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
