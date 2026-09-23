typedef EmployeeJson = Map<String, dynamic>;

class EmployeePieceRateRow {
  const EmployeePieceRateRow({
    this.pieceType = '',
    this.rateText = '',
  });

  final String pieceType;
  final String rateText;

  EmployeePieceRateRow copyWith({String? pieceType, String? rateText}) =>
      EmployeePieceRateRow(
        pieceType: pieceType ?? this.pieceType,
        rateText: rateText ?? this.rateText,
      );

  Map<String, dynamic> toJson() => {
        'pieceType': pieceType.trim(),
        'rate': rateText.trim(),
      };
}

class EmployeeWritePayload {
  const EmployeeWritePayload({
    required this.employeeCode,
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
        'employeeCode': employeeCode.trim(),
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

DateTime _date(EmployeeJson json, String key) =>
    DateTime.parse(json[key] as String);
DateTime? _nullableDate(EmployeeJson json, String key) =>
    json[key] == null ? null : DateTime.parse(json[key] as String);
double _amount(EmployeeJson json, String key) => (json[key] as num).toDouble();
double? _nullableAmount(EmployeeJson json, String key) =>
    (json[key] as num?)?.toDouble();

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
        id: json['employeeId'] as int,
        code: json['employeeCode'] as String,
        name: json['employeeName'] as String,
        jobTitle: json['jobTitle'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        isActive: json['isActive'] as bool?,
        status: json['status'] as String,
        departmentId: json['departmentId'] as int,
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
      required this.updatedAt});

  factory EmployeeDetails.fromJson(EmployeeJson json) => EmployeeDetails(
        id: json['employeeId'] as int,
        code: json['employeeCode'] as String,
        name: json['employeeName'] as String,
        fullName: json['fullName'] as String,
        jobTitle: json['jobTitle'] as String?,
        scannerCode: json['scannerCode'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        baseSalary: _nullableAmount(json, 'baseSalary'),
        notes: json['notes'] as String?,
        isActive: json['isActive'] as bool?,
        salaryType: json['salaryType'] as String?,
        fixedSalary: _nullableAmount(json, 'fixedSalary'),
        nationalId: json['nationalId'] as String?,
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        address: json['address'] as String?,
        hireDate: _date(json, 'hireDate'),
        terminationDate: _nullableDate(json, 'terminationDate'),
        status: json['status'] as String,
        departmentId: json['departmentId'] as int,
        basicSalary: _amount(json, 'basicSalary'),
        pieceWageRate: _amount(json, 'pieceWageRate'),
        overtimeHourlyRate: _amount(json, 'overtimeHourlyRate'),
        createdAt: _date(json, 'createdAt'),
        updatedAt: _nullableDate(json, 'updatedAt'),
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
