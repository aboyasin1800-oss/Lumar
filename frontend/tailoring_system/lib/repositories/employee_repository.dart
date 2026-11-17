import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/employee_models.dart';
import '../services/auth_state.dart';

class EmployeeRepository {
  EmployeeRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final http.Client _client;

  static String _canonicalStage(String value) {
    final text = value.trim();
    if (text.isEmpty) return '';
    switch (text.toLowerCase()) {
      case 'cutting':
        return 'Cutting';
      case 'sewing':
        return 'Sewing';
      default:
        return '';
    }
  }

  static List<dynamic> _coerceList(dynamic value) {
    if (value is List) return value;
    if (value is Map) {
      final typed = Map<String, dynamic>.from(value as Map);
      final candidates = <String>['routes', 'data', 'items', 'result', 'results', 'options', 'rates'];
      for (final key in candidates) {
        final candidate = typed[key];
        if (candidate is List) return candidate;
      }
      for (final entry in typed.entries) {
        final candidate = entry.value;
        if (candidate is List && candidate.isNotEmpty) {
          return candidate;
        }
      }
    }
    return const <dynamic>[];
  }

  static Map<String, String> _extractProductTypeIdentity(Map<String, dynamic> item) {
    final productTypeId = item['productTypeId'] ?? item['ProductTypeId'] ?? item['productTypeID'];
    final code = item['productTypeCode'] ?? item['ProductTypeCode'] ?? item['code'] ?? item['Code'] ?? '';
    final displayName = item['productTypeNameAr'] ??
        item['ProductTypeNameAr'] ??
        item['nameAr'] ??
        item['NameAr'] ??
        item['name'] ??
        item['Name'] ??
        code;

    final textId = productTypeId is num ? productTypeId.toInt() : int.tryParse(productTypeId?.toString() ?? '');
    return {
      'productTypeId': textId == null || textId <= 0 ? '' : textId.toString(),
      'code': code.toString().trim(),
      'displayName': displayName.toString().trim(),
    };
  }

  Future<List<PieceWageProductTypeOption>> getPieceWageProductTypes() async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/settings/production-routes'),
      headers: _headers(),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/settings/production-routes',
        response.statusCode,
        _extractMessage(response),
      );
    }

    final decoded = jsonDecode(response.body);
    final routeEntries = _coerceList(decoded);
    final uniqueTypes = <int, PieceWageProductTypeOption>{};

    for (final rawEntry in routeEntries) {
      if (rawEntry is! Map<String, dynamic>) {
        if (rawEntry is Map) {
          final typed = Map<String, dynamic>.from(rawEntry as Map);
          final identity = _extractProductTypeIdentity(typed);
          final productTypeId = int.tryParse(identity['productTypeId'] ?? '');
          if (productTypeId == null || productTypeId <= 0) continue;
          final stages = typed['stages'] is List
              ? (typed['stages'] as List)
              : const <dynamic>[];
          final allowedStages = stages
              .map((entry) => _canonicalStage(entry.toString()))
              .where((entry) => entry.isNotEmpty)
              .toSet()
              .toList();
          uniqueTypes[productTypeId] = PieceWageProductTypeOption(
            productTypeId: productTypeId,
            code: identity['code'] ?? '',
            displayName: identity['displayName'] ?? identity['code'] ?? '',
            allowedStages: allowedStages,
          );
        }
        continue;
      }

      final identity = _extractProductTypeIdentity(rawEntry);
      final productTypeId = int.tryParse(identity['productTypeId'] ?? '');
      if (productTypeId == null || productTypeId <= 0) continue;
      final stages = rawEntry['stages'] is List ? rawEntry['stages'] as List : const <dynamic>[];
      final allowedStages = stages
          .map((entry) => _canonicalStage(entry.toString()))
          .where((entry) => entry.isNotEmpty)
          .toSet()
          .toList();

      uniqueTypes[productTypeId] = PieceWageProductTypeOption(
        productTypeId: productTypeId,
        code: identity['code'] ?? '',
        displayName: identity['displayName'] ?? identity['code'] ?? '',
        allowedStages: allowedStages,
      );
    }

    final ordered = uniqueTypes.values.toList();
    ordered.sort((a, b) => a.productTypeId.compareTo(b.productTypeId));
    return ordered;
  }

  Future<Map<String, Map<String, double>>> getOfficialPieceWageRates() async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/payroll/piece-wage-rates'),
      headers: _headers(),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      return const <String, Map<String, double>>{};
    }

    final decoded = jsonDecode(response.body);
    final rateItems = _coerceList(decoded);

    final rates = <String, Map<String, double>>{};
    for (final item in rateItems) {
      if (item is! Map<String, dynamic>) continue;
      final pieceType = (item['pieceType'] ?? item['PieceType'] ?? '').toString().trim();
      final stage = _canonicalStage((item['stage'] ?? item['Stage'] ?? '').toString());
      final rawRate = item['wageRate'] ?? item['WageRate'] ?? 0;
      final parsedRate = double.tryParse(rawRate.toString()) ?? 0.0;
      if (pieceType.isEmpty || stage.isEmpty) continue;
      rates.putIfAbsent(pieceType, () => <String, double>{});
      rates[pieceType]![stage] = parsedRate;
    }

    return rates;
  }

  Future<Map<int, Map<String, double>>> getOfficialPieceWageRatesByProductTypeId() async {
    final productTypes = await getPieceWageProductTypes();
    final ratesByPieceType = await getOfficialPieceWageRates();
    final result = <int, Map<String, double>>{};

    for (final productType in productTypes) {
      final matched = <String, double>{};
      final keys = <String>{
        productType.code,
        productType.displayName,
        productType.code.toLowerCase(),
        productType.displayName.toLowerCase(),
      };

      for (final entry in ratesByPieceType.entries) {
        final pieceTypeName = entry.key.trim();
        if (keys.any((key) => key == pieceTypeName || key == pieceTypeName.toLowerCase())) {
          matched.addAll(entry.value);
        }
      }

      if (matched.isNotEmpty) {
        result[productType.productTypeId] = matched;
      }
    }

    return result;
  }

  Map<String, String> _headers({Map<String, String>? extra}) {
    final headers = <String, String>{
      if (extra != null) ...extra,
      'Accept': 'application/json'
    };
    final token = AuthState.instance.token;
    if (token != null && token.trim().isNotEmpty) {
      headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  Future<List<T>> _list<T>(
      String path, T Function(Map<String, dynamic>) fromJson) async {
    final response =
        await _client.get(Uri.parse('$_baseUrl$path'), headers: _headers());
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
          path, response.statusCode, _extractMessage(response));
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      return <T>[];
    }
    return decoded.whereType<Map<String, dynamic>>().map(fromJson).toList();
  }

  Future<Map<String, dynamic>> _object(String path) async {
    final response =
        await _client.get(Uri.parse('$_baseUrl$path'), headers: _headers());
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
          path, response.statusCode, _extractMessage(response));
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      return <String, dynamic>{};
    }
    return decoded;
  }

  Future<EmployeeDetails> _postJson(
      String path, Map<String, dynamic> body) async {
    final jsonBody = jsonEncode(body);

    final response = await _client.post(
      Uri.parse('$_baseUrl$path'),
      headers:
          _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
      body: jsonBody,
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
          path, response.statusCode, _extractMessage(response));
    }

    final decoded = jsonDecode(response.body);
    return EmployeeDetails.fromJson(decoded as Map<String, dynamic>);
  }

  Future<EmployeeDetails> _putJson(
      String path, Map<String, dynamic> body) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl$path'),
      headers:
          _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
      body: jsonEncode(body),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
          path, response.statusCode, _extractMessage(response));
    }
    return EmployeeDetails.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<List<EmployeeSummary>> getEmployees() => _list(
      '/employees',
      (json) => EmployeeSummary(
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
    final payload = json['data'] is Map<String, dynamic>
        ? Map<String, dynamic>.from(json['data'])
        : json;
    if (payload['employeeId'] == null && payload['id'] == null) {
      throw EmployeeApiException('/employees/$employeeId', 500,
          'استجابة الموظف غير متوافقة مع العقد المتوقع.');
    }
    return EmployeeDetails.fromJson(payload);
  }

  Future<List<EmployeeDepartment>> getDepartments() async {
    final response = await _client.get(
      Uri.parse('$_baseUrl/employees/departments'),
      headers: _headers(),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw EmployeeApiException(
        '/employees/departments',
        response.statusCode,
        _extractMessage(response),
      );
    }

    final decoded = jsonDecode(response.body);
    List<dynamic> items;
    if (decoded is List) {
      items = decoded;
    } else if (decoded is Map<String, dynamic>) {
      final maybe = decoded['data'] ??
          decoded['items'] ??
          decoded['departments'] ??
          decoded['result'] ??
          decoded['results'];
      items = maybe is List ? maybe : const <dynamic>[];
    } else {
      items = const <dynamic>[];
    }

    return items
        .whereType<Map<String, dynamic>>()
        .map(
          (json) => EmployeeDepartment(
            id: json['departmentId'] as int,
            code: json['departmentCode'] as String,
            name: json['departmentName'] as String,
            description: json['description'] as String?,
            isActive: json['isActive'] as bool,
          ),
        )
        .toList();
  }

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
    final candidates = <dynamic>[];

    if (decoded is List) {
      candidates.addAll(decoded);
    } else if (decoded is Map<String, dynamic>) {
      candidates.addAll([
        decoded['data'],
        decoded['items'],
        decoded['result'],
        decoded['results'],
        decoded['routes'],
        decoded['options'],
      ]);
    }

    final values = <String>[];
    for (final candidate in candidates) {
      if (candidate is String) {
        final trimmed = candidate.trim();
        if (trimmed.isNotEmpty) values.add(trimmed);
        continue;
      }

      if (candidate is Map<String, dynamic>) {
        final extracted = <String>[];
        for (final key in [
          'productTypeNameAr',
          'ProductTypeNameAr',
          'nameAr',
          'NameAr',
          'pieceType',
          'PieceType',
          'productTypeCode',
          'ProductTypeCode',
          'name',
          'Name',
          'code',
          'Code',
        ]) {
          final value = candidate[key];
          if (value is String && value.trim().isNotEmpty) {
            extracted.add(value.trim());
          }
        }
        values.addAll(extracted);
        continue;
      }

      if (candidate is List) {
        for (final item in candidate) {
          if (item is String) {
            final trimmed = item.trim();
            if (trimmed.isNotEmpty) values.add(trimmed);
          } else if (item is Map<String, dynamic>) {
            final extracted = <String>[];
            for (final key in [
              'productTypeNameAr',
              'ProductTypeNameAr',
              'nameAr',
              'NameAr',
              'pieceType',
              'PieceType',
              'productTypeCode',
              'ProductTypeCode',
              'name',
              'Name',
              'code',
              'Code',
            ]) {
              final value = item[key];
              if (value is String && value.trim().isNotEmpty) extracted.add(value.trim());
            }
            values.addAll(extracted);
          }
        }
      }
    }

    final seen = <String>{};
    return values.where((value) => seen.add(value)).toList();
  }

  Future<List<EmployeeDocument>> getDocuments(int employeeId) => _list(
      '/employees/$employeeId/documents',
      (json) => EmployeeDocument(
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

  Future<List<EmployeeContractTemplate>> getContractTemplates() => _list(
      '/employees/contract-templates',
      (json) => EmployeeContractTemplate(
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
      headers:
          _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
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

  Future<EmployeeDetails> updateEmployee(
          int employeeId, EmployeeWritePayload payload) =>
      _putJson('/employees/$employeeId', payload.toJson());

  Future<EmployeeDetails> activateEmployee(int employeeId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/employees/$employeeId/activate'),
      headers:
          _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
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
      headers:
          _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
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

    final rows = <EmployeeOverviewRow>[];
    for (final employee in employees) {
      EmployeeDetails details;
      try {
        details = await getEmployee(employee.id);
      } catch (_) {
        details = EmployeeDetails(
          id: employee.id,
          code: employee.code,
          name: employee.name,
          fullName: employee.name,
          jobTitle: employee.jobTitle,
          scannerCode: null,
          phoneNumber: employee.phoneNumber,
          baseSalary: 0,
          notes: null,
          isActive: employee.isActive ?? true,
          salaryType: null,
          fixedSalary: 0,
          nationalId: null,
          phone: employee.phoneNumber,
          email: null,
          address: null,
          hireDate: DateTime.now(),
          terminationDate: null,
          status: employee.status,
          departmentId: employee.departmentId,
          basicSalary: 0,
          pieceWageRate: 0,
          overtimeHourlyRate: 0,
          createdAt: DateTime.now(),
          updatedAt: null,
        );
      }

      rows.add(
        EmployeeOverviewRow(
          summary: employee,
          details: details,
          department: departments[employee.departmentId],
        ),
      );
    }

    return rows;
  }

  Future<EmployeeDetailsData> getDetails(int employeeId) async {
    final results = await Future.wait([
      getEmployee(employeeId),
      getDepartments(),
      getDocuments(employeeId).catchError((_) => const <EmployeeDocument>[]),
      getAttendance(employeeId).catchError((_) => const <EmployeeAttendanceRecord>[]),
      getLeaveRequests(employeeId).catchError((_) => const <EmployeeLeaveRequest>[]),
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
  final defaultMessage = switch (response.statusCode) {
    400 => 'بيانات الموظف غير صحيحة.',
    401 => 'انتهت الجلسة أو غير مسموح لك بالوصول.',
    403 => 'ليس لديك صلاحية للوصول إلى هذا القسم.',
    409 => 'رقم الموظف موجود بالفعل.',
    404 => 'الموظف المطلوب غير موجود.',
    _ => 'تعذر تنفيذ العملية المطلوبة.',
  };

  try {
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) {
      final message = decoded['message'] ?? decoded['error'];
      if (message is String && message.isNotEmpty && response.statusCode < 400) return message;
    }
    if (decoded is String && decoded.isNotEmpty && response.statusCode < 400) return decoded;
  } catch (_) {}

  return defaultMessage;
}

class EmployeeApiException implements Exception {
  const EmployeeApiException(this.path, this.statusCode, this.message);
  final String path;
  final int statusCode;
  final String message;

  @override
  String toString() =>
      'EmployeeApiException(path: $path, statusCode: $statusCode, message: $message)';
}
