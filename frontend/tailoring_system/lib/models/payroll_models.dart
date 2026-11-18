typedef PayrollJson = Map<String, dynamic>;

DateTime _date(PayrollJson json, String key) => DateTime.parse(json[key] as String);
DateTime? _nullableDate(PayrollJson json, String key) => json[key] == null ? null : DateTime.parse(json[key] as String);
double _amount(PayrollJson json, String key) => (json[key] as num).toDouble();
double? _nullableAmount(PayrollJson json, String key) => (json[key] as num?)?.toDouble();

class PayrollPeriod {
	const PayrollPeriod({required this.id, required this.code, required this.startDate, required this.endDate, required this.status, required this.notes, required this.generatedAt, required this.approvedAt, required this.createdAt});
	factory PayrollPeriod.fromJson(PayrollJson json) => PayrollPeriod(id: json['payrollPeriodId'] as int, code: json['periodCode'] as String, startDate: _date(json, 'startDate'), endDate: _date(json, 'endDate'), status: json['status'] as String, notes: json['notes'] as String?, generatedAt: _nullableDate(json, 'generatedAt'), approvedAt: _nullableDate(json, 'approvedAt'), createdAt: _date(json, 'createdAt'));
	final int id;
	final String code;
	final DateTime startDate;
	final DateTime endDate;
	final String status;
	final String? notes;
	final DateTime? generatedAt;
	final DateTime? approvedAt;
	final DateTime createdAt;
}

class PayrollRecord {
	const PayrollRecord({required this.id, required this.periodId, required this.employeeId, required this.basicSalaryAmount, required this.pieceWageAmount, required this.attendanceAdjustmentAmount, required this.overtimeAmount, required this.grossAmount, required this.deductionsAmount, required this.netAmount, required this.status, required this.notes, required this.createdAt});
	factory PayrollRecord.fromJson(PayrollJson json) => PayrollRecord(id: json['payrollRecordId'] as int, periodId: json['payrollPeriodId'] as int, employeeId: json['employeeId'] as int, basicSalaryAmount: _amount(json, 'basicSalaryAmount'), pieceWageAmount: _amount(json, 'pieceWageAmount'), attendanceAdjustmentAmount: _amount(json, 'attendanceAdjustmentAmount'), overtimeAmount: _amount(json, 'overtimeAmount'), grossAmount: _amount(json, 'grossAmount'), deductionsAmount: _amount(json, 'deductionsAmount'), netAmount: _amount(json, 'netAmount'), status: json['status'] as String, notes: json['notes'] as String?, createdAt: _date(json, 'createdAt'));
	final int id;
	final int periodId;
	final int employeeId;
	final double basicSalaryAmount;
	final double pieceWageAmount;
	final double attendanceAdjustmentAmount;
	final double overtimeAmount;
	final double grossAmount;
	final double deductionsAmount;
	final double netAmount;
	final String status;
	final String? notes;
	final DateTime createdAt;
}

class PayrollItem {
	const PayrollItem({required this.id, required this.recordId, required this.type, required this.name, required this.quantity, required this.rate, required this.amount, required this.notes});
	factory PayrollItem.fromJson(PayrollJson json) => PayrollItem(id: json['payrollItemId'] as int, recordId: json['payrollRecordId'] as int, type: json['itemType'] as String, name: json['itemName'] as String, quantity: _amount(json, 'quantity'), rate: _amount(json, 'rate'), amount: _amount(json, 'amount'), notes: json['notes'] as String?);
	final int id;
	final int recordId;
	final String type;
	final String name;
	final double quantity;
	final double rate;
	final double amount;
	final String? notes;
}

class PayrollEmployee {
	const PayrollEmployee({required this.id, required this.code, required this.name, required this.jobTitle, required this.phoneNumber, required this.isActive, required this.status, required this.departmentId});
	factory PayrollEmployee.fromJson(PayrollJson json) => PayrollEmployee(id: json['employeeId'] as int, code: json['employeeCode'] as String, name: json['employeeName'] as String, jobTitle: json['jobTitle'] as String?, phoneNumber: json['phoneNumber'] as String?, isActive: json['isActive'] as bool?, status: json['status'] as String, departmentId: json['departmentId'] as int);
	final int id;
	final String code;
	final String name;
	final String? jobTitle;
	final String? phoneNumber;
	final bool? isActive;
	final String status;
	final int departmentId;
}

class PayrollDepartment {
	const PayrollDepartment({required this.id, required this.code, required this.name, required this.description, required this.isActive});
	factory PayrollDepartment.fromJson(PayrollJson json) => PayrollDepartment(id: json['departmentId'] as int, code: json['departmentCode'] as String, name: json['departmentName'] as String, description: json['description'] as String?, isActive: json['isActive'] as bool);
	final int id;
	final String code;
	final String name;
	final String? description;
	final bool isActive;
}

class EmployeeDraw {
	const EmployeeDraw({required this.id, required this.employeeCode, required this.drawDate, required this.amount, required this.notes});
	factory EmployeeDraw.fromJson(PayrollJson json) => EmployeeDraw(id: json['drawId'] as int, employeeCode: json['employeeCode'] as String?, drawDate: _nullableDate(json, 'drawDate'), amount: _nullableAmount(json, 'amount'), notes: json['notes'] as String?);
	final int id;
	final String? employeeCode;
	final DateTime? drawDate;
	final double? amount;
	final String? notes;
}

class EmployeeAttendance {
	const EmployeeAttendance({required this.id, required this.employeeId, required this.attendanceDate, required this.checkInTime, required this.checkOutTime, required this.workedHours, required this.overtimeHours, required this.isAbsent, required this.absenceReason, required this.notes});
	factory EmployeeAttendance.fromJson(PayrollJson json) => EmployeeAttendance(id: json['employeeAttendanceId'] as int, employeeId: json['employeeId'] as int, attendanceDate: _date(json, 'attendanceDate'), checkInTime: _nullableDate(json, 'checkInTime'), checkOutTime: _nullableDate(json, 'checkOutTime'), workedHours: _amount(json, 'workedHours'), overtimeHours: _amount(json, 'overtimeHours'), isAbsent: json['isAbsent'] as bool, absenceReason: json['absenceReason'] as String?, notes: json['notes'] as String?);
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

class PieceWageRecord {
	const PieceWageRecord({required this.id, required this.orderId, required this.pieceId, required this.employeeId, required this.employeeCode, required this.pieceType, required this.stage, required this.quantity, required this.wageRate, required this.totalWage, required this.periodId, required this.payrollRecordId, required this.status, required this.notes, required this.createdAt});
	factory PieceWageRecord.fromJson(PayrollJson json) => PieceWageRecord(id: json['pieceWageRecordId'] as int, orderId: json['orderId'] as int, pieceId: json['pieceId'] as int, employeeId: json['employeeId'] as int?, employeeCode: json['employeeCode'] as String?, pieceType: json['pieceType'] as String, stage: json['stage'] as String, quantity: _amount(json, 'quantity'), wageRate: _amount(json, 'wageRate'), totalWage: _amount(json, 'totalWage'), periodId: json['payrollPeriodId'] as int?, payrollRecordId: json['payrollRecordId'] as int?, status: json['status'] as String, notes: json['notes'] as String?, createdAt: _date(json, 'createdAt'));
	final int id;
	final int orderId;
	final int pieceId;
	final int? employeeId;
	final String? employeeCode;
	final String pieceType;
	final String stage;
	final double quantity;
	final double wageRate;
	final double totalWage;
	final int? periodId;
	final int? payrollRecordId;
	final String status;
	final String? notes;
	final DateTime createdAt;
}

class PieceWageRate {
	const PieceWageRate({required this.id, required this.pieceType, required this.stage, required this.wageRate, required this.isActive, required this.notes, required this.createdAt, required this.updatedAt});
	factory PieceWageRate.fromJson(PayrollJson json) => PieceWageRate(
		id: json['pieceWageRateId'] as int,
		pieceType: json['pieceType'] as String,
		stage: json['stage'] as String,
		wageRate: _amount(json, 'wageRate'),
		isActive: (json['isActive'] as bool?) ?? false,
		notes: json['notes'] as String?,
		createdAt: _date(json, 'createdAt'),
		updatedAt: _nullableDate(json, 'updatedAt'),
	);
	final int id;
	final String pieceType;
	final String stage;
	final double wageRate;
	final bool isActive;
	final String? notes;
	final DateTime createdAt;
	final DateTime? updatedAt;
}

class PayrollGenerationResult {
	const PayrollGenerationResult({required this.payrollPeriodId, required this.periodCode, required this.employeeCount, required this.totalGrossAmount, required this.totalNetAmount, required this.generatedAt, required this.payrollRecordIds});
	factory PayrollGenerationResult.fromJson(PayrollJson json) => PayrollGenerationResult(
		payrollPeriodId: json['payrollPeriodId'] as int,
		periodCode: json['periodCode'] as String,
		employeeCount: json['employeeCount'] as int,
		totalGrossAmount: _amount(json, 'totalGrossAmount'),
		totalNetAmount: _amount(json, 'totalNetAmount'),
		generatedAt: _date(json, 'generatedAt'),
		payrollRecordIds: (json['payrollRecordIds'] as List? ?? const []).map((item) => (item as num).toInt()).toList(),
	);
	final int payrollPeriodId;
	final String periodCode;
	final int employeeCount;
	final double totalGrossAmount;
	final double totalNetAmount;
	final DateTime generatedAt;
	final List<int> payrollRecordIds;
}

class PayrollSettlement {
	const PayrollSettlement({required this.settlementId, required this.drawId, required this.employeeCode, required this.settlementDate, required this.amount, required this.notes, required this.journalEntryId});
	factory PayrollSettlement.fromJson(PayrollJson json) => PayrollSettlement(
		settlementId: json['settlementId'] as int,
		drawId: json['drawId'] as int,
		employeeCode: json['employeeCode'] as String?,
		settlementDate: _date(json, 'settlementDate'),
		amount: _amount(json, 'amount'),
		notes: json['notes'] as String?,
		journalEntryId: json['journalEntryId'] as int?,
	);
	final int settlementId;
	final int drawId;
	final String? employeeCode;
	final DateTime settlementDate;
	final double amount;
	final String? notes;
	final int? journalEntryId;
}

class EmployeeLedgerEntry {
  const EmployeeLedgerEntry({
    required this.employeeLedgerEntryId,
    required this.employeeId,
    required this.entryType,
    required this.amount,
    required this.balanceEffect,
    required this.effectiveDate,
    required this.sourceType,
    required this.sourceId,
    required this.referenceNumber,
    required this.status,
    required this.createdBy,
    required this.occurredAt,
  });

  factory EmployeeLedgerEntry.fromJson(PayrollJson json) => EmployeeLedgerEntry(
        employeeLedgerEntryId: (json['employeeLedgerEntryId'] as num?)?.toInt() ?? 0,
        employeeId: (json['employeeId'] as num?)?.toInt() ?? 0,
        entryType: (json['entryType'] ?? json['EntryType'] ?? '').toString(),
        amount: _nullableAmount(json, 'amount') ?? 0,
        balanceEffect: _nullableAmount(json, 'balanceEffect') ?? 0,
        effectiveDate: _nullableDate(json, 'effectiveDate') ?? _nullableDate(json, 'effectiveAt') ?? DateTime.now(),
        sourceType: (json['sourceType'] ?? json['SourceType'] ?? '').toString(),
        sourceId: (json['sourceId'] as num?)?.toInt() ?? 0,
        referenceNumber: (json['referenceNumber'] ?? json['ReferenceNumber'] ?? '').toString(),
        status: (json['status'] ?? json['Status'] ?? '').toString(),
        createdBy: (json['createdBy'] ?? json['CreatedBy'])?.toString(),
        occurredAt: _nullableDate(json, 'occurredAt') ?? _nullableDate(json, 'createdAt') ?? DateTime.now(),
      );

  final int employeeLedgerEntryId;
  final int employeeId;
  final String entryType;
  final double amount;
  final double balanceEffect;
  final DateTime effectiveDate;
  final String sourceType;
  final int sourceId;
  final String referenceNumber;
  final String status;
  final String? createdBy;
  final DateTime occurredAt;
}

class EmployeeFinancialActionResult {
  const EmployeeFinancialActionResult({
    required this.transactionId,
    required this.accountingEventId,
    required this.amount,
    required this.actionDate,
    required this.actionType,
    required this.referenceNumber,
    required this.balanceBefore,
    required this.balanceAfter,
    required this.isExisting,
  });

  factory EmployeeFinancialActionResult.fromJson(PayrollJson json) => EmployeeFinancialActionResult(
        transactionId: (json['transactionId'] as num?)?.toInt() ?? 0,
        accountingEventId: (json['accountingEventId'] as num?)?.toInt() ?? 0,
        amount: _nullableAmount(json, 'amount') ?? 0,
        actionDate: _nullableDate(json, 'actionDate') ?? _nullableDate(json, 'date') ?? DateTime.now(),
        actionType: (json['actionType'] ?? json['ActionType'] ?? '').toString(),
        referenceNumber: (json['referenceNumber'] ?? json['ReferenceNumber'] ?? '').toString(),
        balanceBefore: _nullableAmount(json, 'balanceBefore') ?? 0,
        balanceAfter: _nullableAmount(json, 'balanceAfter') ?? 0,
        isExisting: json['isExisting'] as bool? ?? false,
      );

  final int transactionId;
  final int accountingEventId;
  final double amount;
  final DateTime actionDate;
  final String actionType;
  final String referenceNumber;
  final double balanceBefore;
  final double balanceAfter;
  final bool isExisting;
}

class EmployeeSelfFinancialSummary {
  const EmployeeSelfFinancialSummary({
    required this.employeeId,
    required this.payrollPeriodId,
    required this.basicSalaryAmount,
    required this.pieceWageAmount,
    required this.overtimeAmount,
    required this.grossAmount,
    required this.deductionsAmount,
    required this.netAmount,
    required this.status,
    required this.createdAt,
  });

  factory EmployeeSelfFinancialSummary.fromJson(PayrollJson json) => EmployeeSelfFinancialSummary(
        employeeId: json['employeeId'] as int,
        payrollPeriodId: json['payrollPeriodId'] as int?,
        basicSalaryAmount: _amount(json, 'basicSalaryAmount'),
        pieceWageAmount: _amount(json, 'pieceWageAmount'),
        overtimeAmount: _amount(json, 'overtimeAmount'),
        grossAmount: _amount(json, 'grossAmount'),
        deductionsAmount: _amount(json, 'deductionsAmount'),
        netAmount: _amount(json, 'netAmount'),
        status: json['status'] as String?,
        createdAt: _nullableDate(json, 'createdAt'),
      );

  final int employeeId;
  final int? payrollPeriodId;
  final double basicSalaryAmount;
  final double pieceWageAmount;
  final double overtimeAmount;
  final double grossAmount;
  final double deductionsAmount;
  final double netAmount;
  final String? status;
  final DateTime? createdAt;
}

class EmployeeMonthlyStatement {
  const EmployeeMonthlyStatement({
    required this.payrollPeriodId,
    required this.periodCode,
    required this.periodStart,
    required this.periodEnd,
    required this.periodStatus,
    required this.basicSalaryAmount,
    required this.pieceWageAmount,
    required this.overtimeAmount,
    required this.grossAmount,
    required this.deductionsAmount,
    required this.drawAmount,
    required this.settlementsAmount,
    required this.netAmount,
    required this.generatedAt,
    required this.approvedAt,
  });

  factory EmployeeMonthlyStatement.fromJson(PayrollJson json) => EmployeeMonthlyStatement(
        payrollPeriodId: json['payrollPeriodId'] as int,
        periodCode: json['periodCode'] as String,
        periodStart: _date(json, 'periodStart'),
        periodEnd: _date(json, 'periodEnd'),
        periodStatus: json['periodStatus'] as String,
        basicSalaryAmount: _nullableAmount(json, 'basicSalaryAmount') ?? 0,
        pieceWageAmount: _nullableAmount(json, 'pieceWageAmount') ?? 0,
        overtimeAmount: _nullableAmount(json, 'overtimeAmount') ?? 0,
        grossAmount: _nullableAmount(json, 'grossAmount') ?? 0,
        deductionsAmount: _nullableAmount(json, 'deductionsAmount') ?? 0,
        drawAmount: _nullableAmount(json, 'drawAmount') ?? 0,
        settlementsAmount: _nullableAmount(json, 'settlementsAmount') ?? 0,
        netAmount: _nullableAmount(json, 'netAmount') ?? 0,
        generatedAt: _nullableDate(json, 'generatedAt'),
        approvedAt: _nullableDate(json, 'approvedAt'),
      );

  final int payrollPeriodId;
  final String periodCode;
  final DateTime periodStart;
  final DateTime periodEnd;
  final String periodStatus;
  final double basicSalaryAmount;
  final double pieceWageAmount;
  final double overtimeAmount;
  final double grossAmount;
  final double deductionsAmount;
  final double drawAmount;
  final double settlementsAmount;
  final double netAmount;
  final DateTime? generatedAt;
  final DateTime? approvedAt;
}

class PayrollOverview {
	const PayrollOverview({required this.periods, required this.records, required this.employees, required this.departments, required this.pieceWages, required this.itemsByRecord, required this.drawsByEmployee});
	final List<PayrollPeriod> periods;
	final List<PayrollRecord> records;
	final List<PayrollEmployee> employees;
	final List<PayrollDepartment> departments;
	final List<PieceWageRecord> pieceWages;
	final Map<int, List<PayrollItem>> itemsByRecord;
	final Map<int, List<EmployeeDraw>> drawsByEmployee;
}

class EmployeePayrollDetailsData {
	const EmployeePayrollDetailsData({required this.items, required this.draws, required this.attendance, required this.pieceWages});
	final List<PayrollItem> items;
	final List<EmployeeDraw> draws;
	final List<EmployeeAttendance> attendance;
	final List<PieceWageRecord> pieceWages;
}