import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:uuid/uuid.dart';

import '../models/payroll_models.dart';
import '../services/auth_state.dart';

class PayrollRepository {
	PayrollRepository({http.Client? client}) : _client = client ?? http.Client();
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');
	final http.Client _client;

	String _newSourceOperationId() {
		return const Uuid().v4();
	}

	http.Client get client => _client;

	Map<String, String> _headers({Map<String, String>? extra}) {
		final headers = <String, String>{
			if (extra != null) ...extra,
			'Accept': 'application/json',
		};
		final token = AuthState.instance.token;
		if (token != null && token.trim().isNotEmpty) {
			headers['Authorization'] = 'Bearer ${token.trim()}';
		}
		return headers;
	}

	Future<List<T>> _list<T>(String path, T Function(PayrollJson) fromJson) async {
		final response = await _client.get(Uri.parse('$_baseUrl$path'), headers: _headers());
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
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
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
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
			body: jsonEncode({'pieceType': pieceType, 'stage': stage, 'wageRate': wageRate, 'isActive': isActive, 'notes': notes}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/piece-wage-rates/$id', response.statusCode);
		return PieceWageRate.fromJson(jsonDecode(response.body) as PayrollJson);
	}
	Future<void> deletePieceWageRate(int id) async {
		final response = await _client.delete(Uri.parse('$_baseUrl/payroll/piece-wage-rates/$id'), headers: _headers());
		if (response.statusCode != 204) throw PayrollApiException('/payroll/piece-wage-rates/$id', response.statusCode);
	}
	Future<List<PayrollSettlement>> getSettlements(int employeeId) => _list('/payroll/employees/$employeeId/settlements', PayrollSettlement.fromJson);
	Future<List<EmployeeDraw>> getMyDraws() => _list('/employees/me/draws', EmployeeDraw.fromJson);
	Future<List<PayrollSettlement>> getMySettlements() => _list('/employees/me/settlements', PayrollSettlement.fromJson);
	Future<List<PieceWageRecord>> getMyPieceWages() => _list('/employees/me/piece-wages', PieceWageRecord.fromJson);
	Future<EmployeeSelfFinancialSummary> getEmployeeFinancialSummary(int employeeId, {int? periodId}) async {
		final query = periodId == null ? '' : '?periodId=$periodId';
		final response = await _client.get(Uri.parse('$_baseUrl/employees/$employeeId/financial-summary$query'), headers: _headers());
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/$employeeId/financial-summary', response.statusCode);
		final decoded = jsonDecode(response.body);
		return EmployeeSelfFinancialSummary.fromJson(decoded as PayrollJson);
	}
	Future<EmployeeMonthlyStatement> getEmployeeMonthlyStatement(int employeeId, int periodId) async {
		final response = await _client.get(Uri.parse('$_baseUrl/employees/$employeeId/monthly-statement?periodId=$periodId'), headers: _headers());
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/$employeeId/monthly-statement', response.statusCode);
		final decoded = jsonDecode(response.body);
		return EmployeeMonthlyStatement.fromJson(decoded as PayrollJson);
	}
	Future<List<EmployeeLedgerEntry>> getEmployeeLedger(int employeeId) async {
		final response = await _client.get(Uri.parse('$_baseUrl/employees/$employeeId/ledger'), headers: _headers());
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/$employeeId/ledger', response.statusCode);
		final decoded = jsonDecode(response.body);
		if (decoded is! List) return const <EmployeeLedgerEntry>[];
		return decoded.whereType<Map<String, dynamic>>().map(EmployeeLedgerEntry.fromJson).toList();
	}
	Future<EmployeeFinancialActionResult> addEmployeeAdvance(int employeeId, {
		required double amount,
		required DateTime advanceDate,
		required int cashAccountId,
		String? referenceNumber,
		String? notes,
		String? sourceOperationId,
	}) async {
		final body = <String, dynamic>{
			'employeeId': employeeId,
			'amount': amount,
			'advanceDate': advanceDate.toIso8601String().substring(0, 10),
			'cashAccountId': cashAccountId,
			'referenceNumber': referenceNumber ?? 'ADV-$employeeId-${DateTime.now().millisecondsSinceEpoch}',
			'notes': notes ?? 'إضافة سلفة من شاشة الموظف',
			'sourceOperationId': sourceOperationId ?? _newSourceOperationId(),
			'currencyCode': 'YER',
		};
		final response = await _client.post(
			Uri.parse('$_baseUrl/employees/$employeeId/advances'),
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
			body: jsonEncode(body),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/$employeeId/advances', response.statusCode);
		final decoded = jsonDecode(response.body);
		return EmployeeFinancialActionResult.fromJson(decoded as PayrollJson);
	}
	Future<EmployeeFinancialActionResult> addEmployeeDailyExpense(int employeeId, {
		required double amount,
		required DateTime expenseDate,
		required int cashAccountId,
		String? referenceNumber,
		String? notes,
		String? sourceOperationId,
	}) async {
		final baseUrl = _baseUrl;
		final finalUri = Uri.parse('$baseUrl/employees/$employeeId/daily-expenses');
		final hasReference = (referenceNumber != null && referenceNumber.trim().isNotEmpty);
		final hasSourceOperationId = (sourceOperationId != null && sourceOperationId.trim().isNotEmpty);
		debugPrint('FINANCE_ACTION = DAILY_EXPENSE');
		debugPrint('EMPLOYEE_ID = $employeeId');
		debugPrint('API_BASE_URL = $baseUrl');
		debugPrint('REQUEST_PATH = /employees/$employeeId/daily-expenses');
		debugPrint('CASH_ACCOUNT_ID = $cashAccountId');
		debugPrint('AMOUNT = $amount');
		debugPrint('REFERENCE_PRESENT = ${hasReference ? 'YES' : 'NO'}');
		debugPrint('SOURCE_OPERATION_ID_PRESENT = ${hasSourceOperationId ? 'YES' : 'NO'}');

		final body = <String, dynamic>{
			'employeeId': employeeId,
			'amount': amount,
			'expenseDate': expenseDate.toIso8601String().substring(0, 10),
			'cashAccountId': cashAccountId,
			'referenceNumber': referenceNumber ?? 'EXP-$employeeId-${DateTime.now().millisecondsSinceEpoch}',
			'notes': notes ?? 'إضافة مصروف يومي من شاشة الموظف',
			'sourceOperationId': sourceOperationId ?? _newSourceOperationId(),
			'currencyCode': 'YER',
		};
		final safeRequestBody = <String, dynamic>{
			'employeeId': employeeId,
			'amount': amount,
			'expenseDate': body['expenseDate'],
			'cashAccountId': cashAccountId,
			'referenceNumber': hasReference ? referenceNumber : 'PRESENTED',
			'notes': notes ?? 'SET',
			'sourceOperationId': body['sourceOperationId'] ?? 'AUTO_GENERATED',
			'currencyCode': 'YER',
		};
		debugPrint('HTTP_POST_START');
		debugPrint('FINAL_URI = $finalUri');
		debugPrint('SAFE_REQUEST_BODY = ${jsonEncode(safeRequestBody)}');

		try {
			final response = await _client.post(
				finalUri,
				headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
				body: jsonEncode(body),
			);
			debugPrint('HTTP_STATUS = ${response.statusCode}');
			debugPrint('HTTP_RESPONSE_RECEIVED');
			debugPrint('RESPONSE_BODY = ${response.body}');
			if (response.statusCode < 200 || response.statusCode >= 300) {
				throw PayrollApiException('/employees/$employeeId/daily-expenses', response.statusCode, response.body);
			}
			final decoded = jsonDecode(response.body);
			return EmployeeFinancialActionResult.fromJson(decoded as PayrollJson);
		} on Exception catch (error, stackTrace) {
			debugPrint('HTTP_EXCEPTION_TYPE = ${error.runtimeType}');
			debugPrint('HTTP_EXCEPTION_MESSAGE = ${error.toString()}');
			debugPrint('HTTP_EXCEPTION_STACK = $stackTrace');
			rethrow;
		}
	}
	Future<EmployeeFinancialActionResult> payEmployee(int employeeId, {
		required double amount,
		required DateTime paymentDate,
		required DateTime asOfDate,
		required int cashAccountId,
		required String paymentMethod,
		String? referenceNumber,
		String? notes,
		String? sourceOperationId,
	}) async {
		final body = <String, dynamic>{
			'employeeId': employeeId,
			'amount': amount,
			'paymentDate': paymentDate.toIso8601String().substring(0, 10),
			'asOfDate': asOfDate.toIso8601String().substring(0, 10),
			'cashAccountId': cashAccountId,
			'paymentMethod': paymentMethod,
			'referenceNumber': referenceNumber ?? 'PAY-$employeeId-${DateTime.now().millisecondsSinceEpoch}',
			'notes': notes ?? 'دفع من شاشة الموظف',
			'sourceOperationId': sourceOperationId ?? _newSourceOperationId(),
			'currencyCode': 'YER',
		};
		final response = await _client.post(
			Uri.parse('$_baseUrl/employees/$employeeId/payments'),
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
			body: jsonEncode(body),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/$employeeId/payments', response.statusCode);
		final decoded = jsonDecode(response.body);
		return EmployeeFinancialActionResult.fromJson(decoded as PayrollJson);
	}
	Future<EmployeeSelfFinancialSummary> getMyFinancialSummary({int? periodId}) async {
		final query = periodId == null ? '' : '?periodId=$periodId';
		final response = await _client.get(Uri.parse('$_baseUrl/employees/me/financial-summary$query'), headers: _headers());
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/me/financial-summary', response.statusCode);
		final decoded = jsonDecode(response.body);
		return EmployeeSelfFinancialSummary.fromJson(decoded as PayrollJson);
	}
	Future<EmployeeMonthlyStatement> getMyMonthlyStatement(int periodId) async {
		final response = await _client.get(Uri.parse('$_baseUrl/employees/me/monthly-statement?periodId=$periodId'), headers: _headers());
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/employees/me/monthly-statement', response.statusCode);
		final decoded = jsonDecode(response.body);
		return EmployeeMonthlyStatement.fromJson(decoded as PayrollJson);
	}

	Future<PayrollGenerationResult> generatePayroll({required DateTime startDate, required DateTime endDate, String? periodCode, String? notes}) async {
		final response = await _client.post(
			Uri.parse('$_baseUrl/payroll/generate'),
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
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
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
			body: jsonEncode({'approvedBy': approvedBy ?? 'FlutterApp', 'notes': notes}),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw PayrollApiException('/payroll/periods/$periodId/approve', response.statusCode);
		return PayrollPeriod.fromJson(jsonDecode(response.body) as PayrollJson);
	}

	Future<PayrollRecord> payPayroll(int recordId, {required String paymentMethod, String? referenceNumber, String? notes, double? overrideAmount}) async {
		final response = await _client.post(
			Uri.parse('$_baseUrl/payroll/records/$recordId/pay'),
			headers: _headers(extra: {'Content-Type': 'application/json; charset=utf-8'}),
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

		final itemLists = <List<PayrollItem>>[];
		for (final record in records) {
			try {
				itemLists.add(await getItems(record.id));
			} catch (_) {
				itemLists.add(const <PayrollItem>[]);
			}
		}

		final drawLists = <List<EmployeeDraw>>[];
		for (final employee in employees) {
			try {
				drawLists.add(await getDraws(employee.id));
			} catch (_) {
				drawLists.add(const <EmployeeDraw>[]);
			}
		}

		return PayrollOverview(
			periods: results[0] as List<PayrollPeriod>,
			records: records,
			employees: employees,
			departments: results[3] as List<PayrollDepartment>,
			pieceWages: results[4] as List<PieceWageRecord>,
			itemsByRecord: {
				for (var index = 0; index < records.length; index++)
					records[index].id: itemLists[index],
			},
			drawsByEmployee: {
				for (var index = 0; index < employees.length; index++)
					employees[index].id: drawLists[index],
			},
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
	const PayrollApiException(this.path, this.statusCode, [this.responseBody = '']);
	final String path;
	final int statusCode;
	final String responseBody;

	@override
	String toString() => 'PayrollApiException(path: $path, statusCode: $statusCode, responseBody: $responseBody)';
}