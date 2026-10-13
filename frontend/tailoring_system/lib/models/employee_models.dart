typedef EmployeeJson = Map<String, dynamic>;

class EmployeePieceRateRow {
  const EmployeePieceRateRow({
    this.pieceType = '',
    this.stage = '',
    this.rateText = '',
  });

  final String pieceType;
  final String stage;
  final String rateText;

  EmployeePieceRateRow copyWith(
          {String? pieceType, String? stage, String? rateText}) =>
      EmployeePieceRateRow(
        pieceType: pieceType ?? this.pieceType,
        stage: stage ?? this.stage,
        rateText: rateText ?? this.rateText,
      );

  double? get parsedRate => double.tryParse(rateText.trim());

  Map<String, dynamic> toJson() {
    final rateValue = rateText.trim();
    return {
      'pieceType': pieceType.trim(),
      'stage': stage.trim(),
      'rate': rateValue.isEmpty ? 0.0 : (double.tryParse(rateValue) ?? 0.0),
    };
  }
}

class EmployeeWritePayload {
  const EmployeeWritePayload({
    this.employeeCode = '',
    required this.fullName,
    required this.departmentId,
    required this.basicSalary,
    this.phoneNumber,
    this.hireDate,
    this.status = 'Active',
    this.salaryType = 'BasicSalary',
    this.pieceRates = const <EmployeePieceRateRow>[],
  });

  final String employeeCode;
  final String fullName;
  final int departmentId;
  final double basicSalary;
  final String? phoneNumber;
  final DateTime? hireDate;
  final String status;
  final String salaryType;
  final List<EmployeePieceRateRow> pieceRates;

  Map<String, dynamic> toJson() => {
        'employeeCode':
            employeeCode.trim().isEmpty ? null : employeeCode.trim(),
        'fullName': fullName.trim(),
        'departmentId': departmentId,
        'basicSalary': basicSalary,
        'phoneNumber': phoneNumber == null || phoneNumber!.trim().isEmpty
            ? null
            : phoneNumber!.trim(),
        'hireDate': hireDate?.toIso8601String(),
        'status': status,
        'salaryType': salaryType,
        'pieceRates': pieceRates.map((row) => row.toJson()).toList(),
      };
}

String _stringValue(EmployeeJson json, String key, {String fallback = ''}) {
  final value = json[key];
  if (value == null) return fallback;
  final text = value.toString().trim();
  return text.isEmpty ? fallback : text;
}

String _firstNonEmpty(EmployeeJson json, List<String> keys,
    {String fallback = ''}) {
  for (final key in keys) {
    final text = _stringValue(json, key);
    if (text.isNotEmpty) return text;
  }
  return fallback;
}

DateTime _date(EmployeeJson json, String key, {DateTime? fallback}) {
  final raw = json[key];
  if (raw == null || raw.toString().trim().isEmpty) {
    return fallback ?? DateTime.fromMillisecondsSinceEpoch(0);
  }
  return DateTime.tryParse(raw.toString()) ??
      (fallback ?? DateTime.fromMillisecondsSinceEpoch(0));
}

DateTime? _nullableDate(EmployeeJson json, String key) {
  final raw = json[key];
  if (raw == null || raw.toString().trim().isEmpty) return null;
  return DateTime.tryParse(raw.toString());
}

double _amount(EmployeeJson json, String key, {double fallback = 0}) {
  final raw = json[key];
  if (raw == null) return fallback;
  if (raw is num) return raw.toDouble();
  return double.tryParse(raw.toString()) ?? fallback;
}

double? _nullableAmount(EmployeeJson json, String key) {
  final raw = json[key];
  if (raw == null) return null;
  if (raw is num) return raw.toDouble();
  return double.tryParse(raw.toString());
}

int _intValue(EmployeeJson json, String key, {int fallback = 0}) {
  final raw = json[key];
  if (raw == null) return fallback;
  if (raw is int) return raw;
  if (raw is num) return raw.toInt();
  return int.tryParse(raw.toString()) ?? fallback;
}

class EmployeeSummary {
  const EmployeeSummary(
      {required this.id,
      required this.code,
      required this.name,
      required this.jobTitle,
      required this.phoneNumber,
      required this.isActive,
      required this.status,
      required this.departmentId});

  factory EmployeeSummary.fromJson(EmployeeJson json) => EmployeeSummary(
        id: _intValue(json, 'employeeId', fallback: _intValue(json, 'id')),
        code:
            _firstNonEmpty(json, ['employeeCode', 'code'], fallback: 'UNKNOWN'),
        name: _firstNonEmpty(json, ['employeeName', 'name', 'fullName'],
            fallback: 'UNKNOWN'),
        jobTitle: _stringValue(json, 'jobTitle').isEmpty
            ? null
            : _stringValue(json, 'jobTitle'),
        phoneNumber: _stringValue(json, 'phoneNumber').isEmpty
            ? null
            : _stringValue(json, 'phoneNumber'),
        isActive: json['isActive'] is bool ? json['isActive'] as bool : null,
        status: _firstNonEmpty(json, ['status'], fallback: 'Active'),
        departmentId: _intValue(json, 'departmentId',
            fallback: _intValue(json, 'department_id')),
      );

  final int id;
  final String code;
  final String name;
  final String? jobTitle;
  final String? phoneNumber;
  final bool? isActive;
  final String status;
  final int departmentId;
}

class EmployeeDetails {
  const EmployeeDetails(
      {required this.id,
      required this.code,
      required this.name,
      required this.fullName,
      required this.jobTitle,
      required this.scannerCode,
      required this.phoneNumber,
      required this.baseSalary,
      required this.notes,
      required this.isActive,
      required this.salaryType,
      required this.fixedSalary,
      required this.nationalId,
      required this.phone,
      required this.email,
      required this.address,
      required this.hireDate,
      required this.terminationDate,
      required this.status,
      required this.departmentId,
      required this.basicSalary,
      required this.pieceWageRate,
      required this.overtimeHourlyRate,
      required this.createdAt,
      required this.updatedAt,
      this.pieceRates = const <EmployeePieceRateRow>[]});

  static List<EmployeePieceRateRow> _parsePieceRates(dynamic raw) {
    final values = raw is List ? raw : const <dynamic>[];
    return values
        .whereType<Map<String, dynamic>>()
        .map(
          (entry) => EmployeePieceRateRow(
            pieceType:
                (entry['pieceType'] ?? entry['piece_type'] ?? '').toString(),
            stage: (entry['stage'] ?? entry['stageName'] ?? '').toString(),
            rateText:
                (entry['rate'] ?? entry['wageRate'] ?? entry['pieceRate'] ?? '')
                    .toString(),
          ),
        )
        .toList();
  }

  factory EmployeeDetails.fromJson(EmployeeJson json) => EmployeeDetails(
        id: _intValue(json, 'employeeId', fallback: _intValue(json, 'id')),
        code:
            _firstNonEmpty(json, ['employeeCode', 'code'], fallback: 'UNKNOWN'),
        name: _firstNonEmpty(json, ['employeeName', 'name', 'fullName'],
            fallback: 'UNKNOWN'),
        fullName: _firstNonEmpty(json, ['fullName', 'employeeName', 'name'],
            fallback: _firstNonEmpty(json, ['employeeName', 'name'],
                fallback: 'UNKNOWN')),
        jobTitle: _stringValue(json, 'jobTitle').isEmpty
            ? null
            : _stringValue(json, 'jobTitle'),
        scannerCode: _stringValue(json, 'scannerCode').isEmpty
            ? null
            : _stringValue(json, 'scannerCode'),
        phoneNumber: _stringValue(json, 'phoneNumber').isEmpty
            ? null
            : _stringValue(json, 'phoneNumber'),
        baseSalary: _nullableAmount(json, 'baseSalary'),
        notes: _stringValue(json, 'notes').isEmpty
            ? null
            : _stringValue(json, 'notes'),
        isActive: json['isActive'] is bool ? json['isActive'] as bool : null,
        salaryType: _stringValue(json, 'salaryType').isEmpty
            ? null
            : _stringValue(json, 'salaryType'),
        fixedSalary: _nullableAmount(json, 'fixedSalary'),
        nationalId: _stringValue(json, 'nationalId').isEmpty
            ? null
            : _stringValue(json, 'nationalId'),
        phone: _stringValue(json, 'phone').isEmpty
            ? null
            : _stringValue(json, 'phone'),
        email: _stringValue(json, 'email').isEmpty
            ? null
            : _stringValue(json, 'email'),
        address: _stringValue(json, 'address').isEmpty
            ? null
            : _stringValue(json, 'address'),
        hireDate: _date(json, 'hireDate',
            fallback: DateTime.fromMillisecondsSinceEpoch(0)),
        terminationDate: _nullableDate(json, 'terminationDate'),
        status: _firstNonEmpty(json, ['status'], fallback: 'Active'),
        departmentId: _intValue(json, 'departmentId',
            fallback: _intValue(json, 'department_id')),
        basicSalary: _amount(json, 'basicSalary', fallback: 0),
        pieceWageRate: _amount(json, 'pieceWageRate', fallback: 0),
        overtimeHourlyRate: _amount(json, 'overtimeHourlyRate', fallback: 0),
        createdAt: _date(json, 'createdAt',
            fallback: DateTime.fromMillisecondsSinceEpoch(0)),
        updatedAt: _nullableDate(json, 'updatedAt'),
        pieceRates: _parsePieceRates(json['pieceRates']),
      );

  final int id;
  final String code;
  final String name;
  final String fullName;
  final String? jobTitle;
  final String? scannerCode;
  final String? phoneNumber;
  final double? baseSalary;
  final String? notes;
  final bool? isActive;
  final String? salaryType;
  final double? fixedSalary;
  final String? nationalId;
  final String? phone;
  final String? email;
  final String? address;
  final DateTime hireDate;
  final DateTime? terminationDate;
  final String status;
  final int departmentId;
  final double basicSalary;
  final double pieceWageRate;
  final double overtimeHourlyRate;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final List<EmployeePieceRateRow> pieceRates;
}

class EmployeeDepartment {
  const EmployeeDepartment(
      {required this.id,
      required this.code,
      required this.name,
      required this.description,
      required this.isActive});

  factory EmployeeDepartment.fromJson(EmployeeJson json) => EmployeeDepartment(
        id: json['departmentId'] as int,
        code: json['departmentCode'] as String,
        name: json['departmentName'] as String,
        description: json['description'] as String?,
        isActive: json['isActive'] as bool,
      );

  final int id;
  final String code;
  final String name;
  final String? description;
  final bool isActive;
}

class EmployeeDocument {
  const EmployeeDocument(
      {required this.id,
      required this.employeeId,
      required this.type,
      required this.number,
      required this.issueDate,
      required this.expiryDate,
      required this.filePath,
      required this.notes,
      required this.createdAt});

  factory EmployeeDocument.fromJson(EmployeeJson json) => EmployeeDocument(
        id: json['employeeDocumentId'] as int,
        employeeId: json['employeeId'] as int,
        type: json['documentType'] as String,
        number: json['documentNumber'] as String?,
        issueDate: _nullableDate(json, 'issueDate'),
        expiryDate: _nullableDate(json, 'expiryDate'),
        filePath: json['filePath'] as String?,
        notes: json['notes'] as String?,
        createdAt: _date(json, 'createdAt'),
      );

  final int id;
  final int employeeId;
  final String type;
  final String? number;
  final DateTime? issueDate;
  final DateTime? expiryDate;
  final String? filePath;
  final String? notes;
  final DateTime createdAt;
}

class EmployeeAttendanceRecord {
  const EmployeeAttendanceRecord(
      {required this.id,
      required this.employeeId,
      required this.attendanceDate,
      required this.checkInTime,
      required this.checkOutTime,
      required this.workedHours,
      required this.overtimeHours,
      required this.isAbsent,
      required this.absenceReason,
      required this.notes});

  factory EmployeeAttendanceRecord.fromJson(EmployeeJson json) =>
      EmployeeAttendanceRecord(
        id: json['employeeAttendanceId'] as int,
        employeeId: json['employeeId'] as int,
        attendanceDate: _date(json, 'attendanceDate'),
        checkInTime: _nullableDate(json, 'checkInTime'),
        checkOutTime: _nullableDate(json, 'checkOutTime'),
        workedHours: _amount(json, 'workedHours'),
        overtimeHours: _amount(json, 'overtimeHours'),
        isAbsent: json['isAbsent'] as bool,
        absenceReason: json['absenceReason'] as String?,
        notes: json['notes'] as String?,
      );

  final int id;
  final int employeeId;
  final DateTime attendanceDate;
  final DateTime? checkInTime;
  final DateTime? checkOutTime;
  final double workedHours;
  final double overtimeHours;
  final bool isAbsent;
  final String? absenceReason;
  final String? notes;
}

class EmployeeContractTemplate {
  const EmployeeContractTemplate({
    required this.id,
    required this.templateName,
    required this.contractType,
    required this.isActive,
    required this.templateText,
    required this.createdAt,
    required this.updatedAt,
  });

  factory EmployeeContractTemplate.fromJson(EmployeeJson json) =>
      EmployeeContractTemplate(
        id: json['contractTemplateId'] as int,
        templateName: json['templateName'] as String,
        contractType: json['contractType'] as String,
        isActive: json['isActive'] as bool,
        templateText: json['templateText'] as String,
        createdAt: _date(json, 'createdAt'),
        updatedAt: _nullableDate(json, 'updatedAt'),
      );

  final int id;
  final String templateName;
  final String contractType;
  final bool isActive;
  final String templateText;
  final DateTime createdAt;
  final DateTime? updatedAt;
}

class EmployeeContract {
  const EmployeeContract({
    required this.id,
    required this.employeeId,
    required this.contractTemplateId,
    required this.number,
    required this.type,
    required this.status,
    required this.startDate,
    required this.endDate,
    required this.signedDate,
    required this.notes,
    required this.filePath,
    required this.text,
    required this.createdAt,
    required this.updatedAt,
  });

  factory EmployeeContract.fromJson(EmployeeJson json) => EmployeeContract(
        id: json['employeeContractId'] as int,
        employeeId: json['employeeId'] as int,
        contractTemplateId: json['contractTemplateId'] as int?,
        number: json['contractNumber'] as String?,
        type: json['contractType'] as String?,
        status: json['contractStatus'] as String?,
        startDate: _nullableDate(json, 'contractStartDate'),
        endDate: _nullableDate(json, 'contractEndDate'),
        signedDate: _nullableDate(json, 'contractSignedDate'),
        notes: json['contractNotes'] as String?,
        filePath: json['contractFilePath'] as String?,
        text: json['contractText'] as String?,
        createdAt: _date(json, 'createdAt'),
        updatedAt: _nullableDate(json, 'updatedAt'),
      );

  final int id;
  final int employeeId;
  final int? contractTemplateId;
  final String? number;
  final String? type;
  final String? status;
  final DateTime? startDate;
  final DateTime? endDate;
  final DateTime? signedDate;
  final String? notes;
  final String? filePath;
  final String? text;
  final DateTime createdAt;
  final DateTime? updatedAt;
}

class EmployeeLeaveRequest {
  const EmployeeLeaveRequest(
      {required this.id,
      required this.employeeId,
      required this.type,
      required this.startDate,
      required this.endDate,
      required this.requestedDays,
      required this.status,
      required this.reason,
      required this.approvedBy,
      required this.approvedAt,
      required this.createdAt});

  factory EmployeeLeaveRequest.fromJson(EmployeeJson json) =>
      EmployeeLeaveRequest(
        id: json['leaveRequestId'] as int,
        employeeId: json['employeeId'] as int,
        type: json['leaveType'] as String,
        startDate: _date(json, 'startDate'),
        endDate: _date(json, 'endDate'),
        requestedDays: _amount(json, 'requestedDays'),
        status: json['status'] as String,
        reason: json['reason'] as String?,
        approvedBy: json['approvedBy'] as String?,
        approvedAt: _nullableDate(json, 'approvedAt'),
        createdAt: _date(json, 'createdAt'),
      );

  final int id;
  final int employeeId;
  final String type;
  final DateTime startDate;
  final DateTime endDate;
  final double requestedDays;
  final String status;
  final String? reason;
  final String? approvedBy;
  final DateTime? approvedAt;
  final DateTime createdAt;
}

class EmployeeOverviewRow {
  const EmployeeOverviewRow(
      {required this.summary, required this.details, required this.department});
  final EmployeeSummary summary;
  final EmployeeDetails details;
  final EmployeeDepartment? department;
}

class EmployeeDetailsData {
  const EmployeeDetailsData(
      {required this.employee,
      required this.department,
      required this.documents,
      required this.attendance,
      required this.leaveRequests});
  final EmployeeDetails employee;
  final EmployeeDepartment? department;
  final List<EmployeeDocument> documents;
  final List<EmployeeAttendanceRecord> attendance;
  final List<EmployeeLeaveRequest> leaveRequests;
}
