import 'package:flutter/foundation.dart';

import '../models/finance_models.dart';
import '../repositories/finance_repository.dart';

enum FinanceLoadState { idle, loading, ready, empty, error }

class FinanceProvider extends ChangeNotifier {
	FinanceProvider({FinanceRepository? repository}) : repository = repository ?? FinanceRepository();
	final FinanceRepository repository;
	FinanceLoadState state = FinanceLoadState.idle;
	Object? error;
	List<FinancialTransaction> transactions = const [];
	List<JournalEntry> journalEntries = const [];
	List<LedgerAccount> ledgerAccounts = const [];
	List<CashAccount> cashAccounts = const [];
	List<SupplierPayment> supplierPayments = const [];
	List<SupplierInvoice> supplierInvoices = const [];

	Future<void> loadOverview() async {
		state = FinanceLoadState.loading;
		error = null;
		notifyListeners();
		try {
			final results = await Future.wait([repository.getTransactions(), repository.getJournalEntries(), repository.getLedgerAccounts(), repository.getCashAccounts(), repository.getSupplierPayments(), repository.getSupplierInvoices()]);
			transactions = results[0] as List<FinancialTransaction>;
			journalEntries = results[1] as List<JournalEntry>;
			ledgerAccounts = results[2] as List<LedgerAccount>;
			cashAccounts = results[3] as List<CashAccount>;
			supplierPayments = results[4] as List<SupplierPayment>;
			supplierInvoices = results[5] as List<SupplierInvoice>;
			state = results.every((items) => (items as List).isEmpty) ? FinanceLoadState.empty : FinanceLoadState.ready;
		} catch (caught) {
			error = caught;
			state = FinanceLoadState.error;
		}
		notifyListeners();
	}

	List<FinancialTransaction> filterTransactions({String? type, String query = '', DateTime? from, DateTime? to}) => transactions.where((item) {
		final normalized = query.trim().toLowerCase();
		return (type == null || item.transactionType == type) && (from == null || !item.createdAt.isBefore(from)) && (to == null || item.createdAt.isBefore(to.add(const Duration(days: 1)))) && (normalized.isEmpty || item.referenceNumber.toLowerCase().contains(normalized) || item.transactionType.toLowerCase().contains(normalized) || (item.description?.toLowerCase().contains(normalized) ?? false));
	}).toList();

	List<FinancialTransaction> get operatingExpenses => transactions.where((item) => item.transactionType == 'OperatingExpense').toList();
}