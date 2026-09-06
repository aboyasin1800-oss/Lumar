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