typedef JsonMap = Map<String, dynamic>;

DateTime _date(JsonMap json, String key) => DateTime.parse(json[key] as String);
double _amount(JsonMap json, String key) => (json[key] as num).toDouble();

class FinancialTransaction {
	const FinancialTransaction({required this.id, required this.referenceNumber, required this.transactionType, required this.amount, required this.description, required this.createdAt});
	factory FinancialTransaction.fromJson(JsonMap json) => FinancialTransaction(id: json['financialTransactionId'] as int, referenceNumber: json['referenceNumber'] as String, transactionType: json['transactionType'] as String, amount: _amount(json, 'amount'), description: json['description'] as String?, createdAt: _date(json, 'createdAt'));
	final int id;
	final String referenceNumber;
	final String transactionType;
	final double amount;
	final String? description;
	final DateTime createdAt;
}

class JournalEntry {
	const JournalEntry({required this.id, required this.referenceNumber, required this.description, required this.entryDate, required this.createdAt});
	factory JournalEntry.fromJson(JsonMap json) => JournalEntry(id: json['journalEntryId'] as int, referenceNumber: json['referenceNumber'] as String, description: json['description'] as String?, entryDate: _date(json, 'entryDate'), createdAt: _date(json, 'createdAt'));
	final int id;
	final String referenceNumber;
	final String? description;
	final DateTime entryDate;
	final DateTime createdAt;
}

class JournalEntryLine {
	const JournalEntryLine({required this.id, required this.journalEntryId, required this.ledgerAccountId, required this.debitAmount, required this.creditAmount, required this.description});
	factory JournalEntryLine.fromJson(JsonMap json) => JournalEntryLine(id: json['journalEntryLineId'] as int, journalEntryId: json['journalEntryId'] as int, ledgerAccountId: json['ledgerAccountId'] as int, debitAmount: _amount(json, 'debitAmount'), creditAmount: _amount(json, 'creditAmount'), description: json['description'] as String?);
	final int id;
	final int journalEntryId;
	final int ledgerAccountId;
	final double debitAmount;
	final double creditAmount;
	final String? description;
}

class LedgerAccount {
	const LedgerAccount({required this.id, required this.accountCode, required this.accountName, required this.accountType, required this.isActive, required this.createdAt, required this.updatedAt});
	factory LedgerAccount.fromJson(JsonMap json) => LedgerAccount(id: json['ledgerAccountId'] as int, accountCode: json['accountCode'] as String, accountName: json['accountName'] as String, accountType: json['accountType'] as String, isActive: json['isActive'] as bool, createdAt: _date(json, 'createdAt'), updatedAt: json['updatedAt'] == null ? null : _date(json, 'updatedAt'));
	final int id;
	final String accountCode;
	final String accountName;
	final String accountType;
	final bool isActive;
	final DateTime createdAt;
	final DateTime? updatedAt;
}

class CashAccount {
	const CashAccount({required this.id, required this.accountName, required this.currentBalance, required this.isActive, required this.createdAt});
	factory CashAccount.fromJson(JsonMap json) => CashAccount(id: json['cashAccountId'] as int, accountName: json['accountName'] as String, currentBalance: _amount(json, 'currentBalance'), isActive: json['isActive'] as bool, createdAt: _date(json, 'createdAt'));
	final int id;
	final String accountName;
	final double currentBalance;
	final bool isActive;
	final DateTime createdAt;
}

class CustomerLedgerEntry {
	const CustomerLedgerEntry({required this.id, required this.customerId, required this.referenceNumber, required this.debitAmount, required this.creditAmount, required this.balanceAfterTransaction, required this.createdAt});
	factory CustomerLedgerEntry.fromJson(JsonMap json) => CustomerLedgerEntry(id: json['customerLedgerEntryId'] as int, customerId: json['customerId'] as int, referenceNumber: json['referenceNumber'] as String, debitAmount: _amount(json, 'debitAmount'), creditAmount: _amount(json, 'creditAmount'), balanceAfterTransaction: _amount(json, 'balanceAfterTransaction'), createdAt: _date(json, 'createdAt'));
	final int id;
	final int customerId;
	final String referenceNumber;
	final double debitAmount;
	final double creditAmount;
	final double balanceAfterTransaction;
	final DateTime createdAt;
}

class SupplierLedgerEntry {
	const SupplierLedgerEntry({required this.id, required this.supplierId, required this.referenceNumber, required this.debitAmount, required this.creditAmount, required this.balanceAfterTransaction, required this.createdAt});
	factory SupplierLedgerEntry.fromJson(JsonMap json) => SupplierLedgerEntry(id: json['supplierLedgerEntryId'] as int, supplierId: json['supplierId'] as int, referenceNumber: json['referenceNumber'] as String, debitAmount: _amount(json, 'debitAmount'), creditAmount: _amount(json, 'creditAmount'), balanceAfterTransaction: _amount(json, 'balanceAfterTransaction'), createdAt: _date(json, 'createdAt'));
	final int id;
	final int supplierId;
	final String referenceNumber;
	final double debitAmount;
	final double creditAmount;
	final double balanceAfterTransaction;
	final DateTime createdAt;
}

class SupplierPayment {
	const SupplierPayment({required this.id, required this.supplierId, required this.paymentNumber, required this.paymentDate, required this.amount, required this.paymentMethod, required this.referenceNumber, required this.notes, required this.createdAt, required this.journalEntryId});
	factory SupplierPayment.fromJson(JsonMap json) => SupplierPayment(id: json['supplierPaymentId'] as int, supplierId: json['supplierId'] as int, paymentNumber: json['paymentNumber'] as String, paymentDate: _date(json, 'paymentDate'), amount: _amount(json, 'amount'), paymentMethod: json['paymentMethod'] as String?, referenceNumber: json['referenceNumber'] as String?, notes: json['notes'] as String?, createdAt: _date(json, 'createdAt'), journalEntryId: json['journalEntryId'] as int?);
	final int id;
	final int supplierId;
	final String paymentNumber;
	final DateTime paymentDate;
	final double amount;
	final String? paymentMethod;
	final String? referenceNumber;
	final String? notes;
	final DateTime createdAt;
	final int? journalEntryId;
}

class SupplierInvoice {
	const SupplierInvoice({required this.id, required this.supplierId, required this.purchaseOrderId, required this.invoiceNumber, required this.invoiceDate, required this.dueDate, required this.totalAmount, required this.amountPaid, required this.status, required this.notes, required this.createdAt});
	factory SupplierInvoice.fromJson(JsonMap json) => SupplierInvoice(id: json['supplierInvoiceId'] as int, supplierId: json['supplierId'] as int, purchaseOrderId: json['purchaseOrderId'] as int, invoiceNumber: json['invoiceNumber'] as String, invoiceDate: _date(json, 'invoiceDate'), dueDate: _date(json, 'dueDate'), totalAmount: _amount(json, 'totalAmount'), amountPaid: _amount(json, 'amountPaid'), status: json['status'] as String, notes: json['notes'] as String?, createdAt: _date(json, 'createdAt'));
	final int id;
	final int supplierId;
	final int purchaseOrderId;
	final String invoiceNumber;
	final DateTime invoiceDate;
	final DateTime dueDate;
	final double totalAmount;
	final double amountPaid;
	final String status;
	final String? notes;
	final DateTime createdAt;
}

class FinancialReconciliation {
	const FinancialReconciliation({required this.financialTransactions, required this.journalEntries, required this.balancedJournalEntries, required this.unbalancedJournalEntries, required this.sharedReferences, required this.financialReferencesWithoutJournal, required this.journalReferencesWithoutTransaction, required this.orphanJournalLines});
	factory FinancialReconciliation.fromJson(JsonMap json) => FinancialReconciliation(financialTransactions: json['financialTransactions'] as int, journalEntries: json['journalEntries'] as int, balancedJournalEntries: json['balancedJournalEntries'] as int, unbalancedJournalEntries: json['unbalancedJournalEntries'] as int, sharedReferences: json['sharedReferences'] as int, financialReferencesWithoutJournal: json['financialReferencesWithoutJournal'] as int, journalReferencesWithoutTransaction: json['journalReferencesWithoutTransaction'] as int, orphanJournalLines: json['orphanJournalLines'] as int);
	final int financialTransactions;
	final int journalEntries;
	final int balancedJournalEntries;
	final int unbalancedJournalEntries;
	final int sharedReferences;
	final int financialReferencesWithoutJournal;
	final int journalReferencesWithoutTransaction;
	final int orphanJournalLines;
}

class FinancialStatements {
	const FinancialStatements({required this.balanceSheet, required this.cashFlow, required this.profitLoss});

	factory FinancialStatements.fromJson(JsonMap json) => FinancialStatements(
				balanceSheet: FinancialBalanceSheet.fromJson(json['balanceSheet'] as JsonMap),
				cashFlow: FinancialCashFlow.fromJson(json['cashFlow'] as JsonMap),
				profitLoss: FinancialProfitLoss.fromJson(json['profitLoss'] as JsonMap),
			);

	final FinancialBalanceSheet balanceSheet;
	final FinancialCashFlow cashFlow;
	final FinancialProfitLoss profitLoss;
}

class FinancialBalanceSheet {
	const FinancialBalanceSheet({required this.assets, required this.liabilities, required this.accountsReceivable, required this.accountsPayable, required this.inventoryValue, required this.equity});
	factory FinancialBalanceSheet.fromJson(JsonMap json) => FinancialBalanceSheet(
				assets: _amount(json, 'assets'),
				liabilities: _amount(json, 'liabilities'),
				accountsReceivable: _amount(json, 'accountsReceivable'),
				accountsPayable: _amount(json, 'accountsPayable'),
				inventoryValue: _amount(json, 'inventoryValue'),
				equity: _amount(json, 'equity'),
			);
	final double assets;
	final double liabilities;
	final double accountsReceivable;
	final double accountsPayable;
	final double inventoryValue;
	final double equity;
}

class FinancialCashFlow {
	const FinancialCashFlow({required this.cashInflows, required this.supplierPayments, required this.refunds, required this.netCashPosition, required this.netCashMovement});
	factory FinancialCashFlow.fromJson(JsonMap json) => FinancialCashFlow(
				cashInflows: _amount(json, 'cashInflows'),
				supplierPayments: _amount(json, 'supplierPayments'),
				refunds: _amount(json, 'refunds'),
				netCashPosition: _amount(json, 'netCashPosition'),
				netCashMovement: _amount(json, 'netCashMovement'),
			);
	final double cashInflows;
	final double supplierPayments;
	final double refunds;
	final double netCashPosition;
	final double netCashMovement;
}

class FinancialProfitLoss {
	const FinancialProfitLoss({required this.revenue, required this.expenses, required this.costOfGoodsSold, required this.grossProfit, required this.netProfit});
	factory FinancialProfitLoss.fromJson(JsonMap json) => FinancialProfitLoss(
				revenue: _amount(json, 'revenue'),
				expenses: _amount(json, 'expenses'),
				costOfGoodsSold: _amount(json, 'costOfGoodsSold'),
				grossProfit: _amount(json, 'grossProfit'),
				netProfit: _amount(json, 'netProfit'),
			);
	final double revenue;
	final double expenses;
	final double costOfGoodsSold;
	final double grossProfit;
	final double netProfit;
}