import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/payroll_models.dart';

class PayrollRepository {
	PayrollRepository({http.Client? client}) : _client = client ?? http.Client();
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');
	final http.Client _client;

	http.Client get client => _client;

	Future<List<T>> _list<T>(String path, T Function(PayrollJson) fromJson) async {
		final response = await _client.get(Uri.parse('$_baseUrl$path'));
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException(path, response.statusCode);
		return (jsonDecode(response.body) as List).cast<PayrollJson>().map(fromJson).toList();
	}

	Future<List<PayrollPeriod>> getPeriods() => _list('/payroll/periods', PayrollPeriod.fromJson);
	Future<List<PayrollRecord>> getRecords() => _list('/payroll/records', PayrollRecord.fromJson);
	Future<List<PayrollItem>> getItems(int recordId) => _list('/payroll/records/$recordId/items', PayrollItem.fromJson);
	Future<List<PayrollEmployee>> getEmployees() => _list('/employees', PayrollEmployee.fromJson);
	Future<List<PayrollDepartment>> getDepartments() => _list('/employees/departments', PayrollDepartment.fromJson);
	Future<List<EmployeeAttendance>> getAttendance(int employeeId) => _list('/employees/$employeeId/attendance', EmployeeAttendance.fromJson);
	Future<List<EmployeeDraw>> getDraws(int employeeId) => _list('/employees/$employeeId/draws', EmployeeDraw.fromJson);
	Future<List<PieceWageRecord>> getPieceWages() => _list('/payroll/piece-wages', PieceWageRecord.fromJson);
	Future<List<PieceWageRate>> getPieceWageRates() => _list('/payroll/piece-wage-rates', PieceWageRate.fromJson);
	Future<PieceWageRate> createPieceWageRate({required String pieceType, required String stage, required double wageRate, bool isActive = true, String? notes}) async {
		final response = await _client.post(
			Uri.parse('$_baseUrl/payroll/piece-wage-rates'),
			headers: {'Content-Type': 'application/json'},
			body: jsonEncode({
				'pieceType': pieceType,
				'stage': stage,
				'wageRate': wageRate,
				'isActive': isActive,
				'notes': notes,
			}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/piece-wage-rates', response.statusCode);
		return PieceWageRate.fromJson(jsonDecode(response.body) as PayrollJson);
	}
	Future<PieceWageRate> updatePieceWageRate(int id, {required String pieceType, required String stage, required double wageRate, required bool isActive, String? notes}) async {
		final response = await _client.put(
			Uri.parse('$_baseUrl/payroll/piece-wage-rates/$id'),
			headers: {'Content-Type': 'application/json'},
			body: jsonEncode({'pieceType': pieceType, 'stage': stage, 'wageRate': wageRate, 'isActive': isActive, 'notes': notes}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/piece-wage-rates/$id', response.statusCode);
		return PieceWageRate.fromJson(jsonDecode(response.body) as PayrollJson);
	}
	Future<void> deletePieceWageRate(int id) async {
		final response = await _client.delete(Uri.parse('$_baseUrl/payroll/piece-wage-rates/$id'));
		if (response.statusCode != 204) throw PayrollApiException('/payroll/piece-wage-rates/$id', response.statusCode);
	}
	Future<List<PayrollSettlement>> getSettlements(int employeeId) => _list('/payroll/employees/$employeeId/settlements', PayrollSettlement.fromJson);

	Future<PayrollGenerationResult> generatePayroll({required DateTime startDate, required DateTime endDate, String? periodCode, String? notes}) async {
		final response = await _client.post(
			Uri.parse('$_baseUrl/payroll/generate'),
			headers: {'Content-Type': 'application/json'},
			body: jsonEncode({
				'startDate': startDate.toUtc().toIso8601String(),
				'endDate': endDate.toUtc().toIso8601String(),
				'periodCode': periodCode,
				'notes': notes,
			}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/generate', response.statusCode);
		return PayrollGenerationResult.fromJson(jsonDecode(response.body) as PayrollJson);
	}

	Future<PayrollPeriod> approvePayroll(int periodId, {String? approvedBy, String? notes}) async {
		final response = await _client.post(
			Uri.parse('$_baseUrl/payroll/periods/$periodId/approve'),
			headers: {'Content-Type': 'application/json'},
			body: jsonEncode({'approvedBy': approvedBy ?? 'FlutterApp', 'notes': notes}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/periods/$periodId/approve', response.statusCode);
		return PayrollPeriod.fromJson(jsonDecode(response.body) as PayrollJson);
	}

	Future<PayrollRecord> payPayroll(int recordId, {required String paymentMethod, String? referenceNumber, String? notes, double? overrideAmount}) async {
		final response = await _client.post(
			Uri.parse('$_baseUrl/payroll/records/$recordId/pay'),
			headers: {'Content-Type': 'application/json'},
			body: jsonEncode({
				'paymentMethod': paymentMethod,
				'referenceNumber': referenceNumber,
				'notes': notes ?? 'دفعة رواتب من شاشة Flutter',
				'overrideAmount': overrideAmount,
			}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/records/$recordId/pay', response.statusCode);
		return PayrollRecord.fromJson(jsonDecode(response.body) as PayrollJson);
	}

	Future<PayrollOverview> getOverview() async {
		final results = await Future.wait([getPeriods(), getRecords(), getEmployees(), getDepartments(), getPieceWages()]);
		final records = results[1] as List<PayrollRecord>;
		final employees = results[2] as List<PayrollEmployee>;
		final itemLists = await Future.wait(records.map((record) => getItems(record.id)));
		final drawLists = await Future.wait(employees.map((employee) => getDraws(employee.id)));
		return PayrollOverview(
			periods: results[0] as List<PayrollPeriod>,
			records: records,
			employees: employees,
			departments: results[3] as List<PayrollDepartment>,
			pieceWages: results[4] as List<PieceWageRecord>,
			itemsByRecord: {for (var index = 0; index < records.length; index++) records[index].id: itemLists[index]},
			drawsByEmployee: {for (var index = 0; index < employees.length; index++) employees[index].id: drawLists[index]},
		);
	}

	Future<EmployeePayrollDetailsData> getEmployeeDetails(int employeeId) async {
		final results = await Future.wait([getDraws(employeeId), getAttendance(employeeId), getPieceWages()]);
		return EmployeePayrollDetailsData(
			items: const [],
			draws: results[0] as List<EmployeeDraw>,
			attendance: results[1] as List<EmployeeAttendance>,
			pieceWages: results[2] as List<PieceWageRecord>,
		);
	}

	Future<EmployeePayrollDetailsData> getPayrollDetails(int recordId, int employeeId) async {
		final results = await Future.wait([getItems(recordId), getDraws(employeeId), getAttendance(employeeId), getPieceWages()]);
		return EmployeePayrollDetailsData(
			items: results[0] as List<PayrollItem>,
			draws: results[1] as List<EmployeeDraw>,
			attendance: results[2] as List<EmployeeAttendance>,
			pieceWages: results[3] as List<PieceWageRecord>,
		);
	}
}

class PayrollApiException implements Exception {
	const PayrollApiException(this.path, this.statusCode);
	final String path;
	final int statusCode;
}