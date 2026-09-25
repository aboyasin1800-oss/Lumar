import 'dart:convert';

import 'package:http/http.dart' as http;

import '../models/finance_models.dart';

class FinanceRepository {
	FinanceRepository({http.Client? client}) : _client = client ?? http.Client();
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');
	final http.Client _client;

	Future<List<T>> _list<T>(String path, T Function(JsonMap) fromJson) async {
		final response = await _client.get(Uri.parse('$_baseUrl$path'));
		if (response.statusCode < 200 || response.statusCode >= 300) throw FinanceApiException(response.statusCode);
		return (jsonDecode(response.body) as List).cast<JsonMap>().map(fromJson).toList();
	}

	Future<JsonMap> _object(String path) async {
		final response = await _client.get(Uri.parse('$_baseUrl$path'));
		if (response.statusCode < 200 || response.statusCode >= 300) throw FinanceApiException(response.statusCode);
		return jsonDecode(response.body) as JsonMap;
	}

	Future<List<FinancialTransaction>> getTransactions() => _list('/finance/transactions', FinancialTransaction.fromJson);
	Future<List<JournalEntry>> getJournalEntries() => _list('/finance/journal-entries', JournalEntry.fromJson);
	Future<JournalEntry> getJournalEntry(int id) async => JournalEntry.fromJson(await _object('/finance/journal-entries/$id'));
	Future<List<JournalEntryLine>> getJournalEntryLines(int id) => _list('/finance/journal-entries/$id/lines', JournalEntryLine.fromJson);
	Future<List<LedgerAccount>> getLedgerAccounts() => _list('/finance/ledger-accounts', LedgerAccount.fromJson);
	Future<List<CashAccount>> getCashAccounts() => _list('/finance/cash-accounts', CashAccount.fromJson);
	Future<List<CustomerLedgerEntry>> getCustomerLedger(int customerId) => _list('/finance/customers/$customerId/ledger', CustomerLedgerEntry.fromJson);
	Future<List<SupplierLedgerEntry>> getSupplierLedger(int supplierId) => _list('/finance/suppliers/$supplierId/ledger', SupplierLedgerEntry.fromJson);
	Future<List<SupplierPayment>> getSupplierPayments() => _list('/finance/supplier-payments', SupplierPayment.fromJson);
	Future<List<SupplierInvoice>> getSupplierInvoices() => _list('/finance/supplier-invoices', SupplierInvoice.fromJson);
	Future<FinancialReconciliation> getReconciliation() async => FinancialReconciliation.fromJson(await _object('/finance/reconciliation'));
	Future<FinancialStatements> getFinancialStatements() async => FinancialStatements.fromJson(await _object('/finance/statements'));
	Future<FinancialDashboard> getDashboard() async => FinancialDashboard.fromJson(await _object('/finance/dashboard'));
	Future<CashReconciliation> getCashReconciliation() async => CashReconciliation.fromJson(await _object('/finance/cash-reconciliation'));
}

class FinanceApiException implements Exception {
	const FinanceApiException(this.statusCode);
	final int statusCode;
}