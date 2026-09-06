import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../models/finance_models.dart';
import '../../providers/finance_provider.dart';
import '../../repositories/finance_repository.dart';

final _money = NumberFormat('#,##0.00');
final _date = DateFormat('yyyy/MM/dd');
String _text(String? value) => value == null || value.trim().isEmpty ? '-' : value;

class FinancialScreen extends StatefulWidget {
	const FinancialScreen({super.key});
	@override State<FinancialScreen> createState() => _FinancialScreenState();
}

class _FinancialScreenState extends State<FinancialScreen> with SingleTickerProviderStateMixin {
	late final FinanceProvider provider;
	late final TabController tabs;
	@override void initState() { super.initState(); tabs = TabController(length: 7, vsync: this); provider = FinanceProvider()..loadOverview(); }
	@override void dispose() { tabs.dispose(); provider.dispose(); super.dispose(); }

	@override Widget build(BuildContext context) => AnimatedBuilder(
		animation: provider,
		builder: (context, _) => Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			Row(children: [Expanded(child: Text('الإدارة المالية', style: Theme.of(context).textTheme.headlineSmall)), IconButton(tooltip: 'تحديث البيانات', onPressed: provider.state == FinanceLoadState.loading ? null : provider.loadOverview, icon: const Icon(Icons.refresh))]),
			const SizedBox(height: 12),
			TabBar(controller: tabs, isScrollable: true, tabAlignment: TabAlignment.start, tabs: const [
				Tab(icon: Icon(Icons.query_stats_outlined), text: 'الإحصاء'),
				Tab(icon: Icon(Icons.assessment_outlined), text: 'القوائم والتقارير'),
				Tab(icon: Icon(Icons.swap_horiz_outlined), text: 'المعاملات'),
				Tab(icon: Icon(Icons.person_search_outlined), text: 'أستاذ العميل'),
				Tab(icon: Icon(Icons.account_balance_wallet_outlined), text: 'النقدية'),
				Tab(icon: Icon(Icons.menu_book_outlined), text: 'القيود'),
				Tab(icon: Icon(Icons.account_tree_outlined), text: 'الأستاذ'),
			]),
			const SizedBox(height: 8),
			Expanded(child: TabBarView(controller: tabs, children: [
				_FinanceStatisticsTab(provider: provider),
				const _FinancialReportsTab(),
				const FinancialTransactionsScreen(embedded: true),
				const CustomerLedgerScreen(embedded: true),
				const CashAccountsScreen(embedded: true),
				const JournalEntriesScreen(embedded: true),
				const LedgerAccountsScreen(embedded: true),
			])),
		]),
	);
}

class _FinanceStatisticsTab extends StatelessWidget {
	const _FinanceStatisticsTab({required this.provider});
	final FinanceProvider provider;
	@override Widget build(BuildContext context) {
		if (provider.state == FinanceLoadState.loading || provider.state == FinanceLoadState.idle) return const Center(child: CircularProgressIndicator());
		if (provider.state == FinanceLoadState.error) return _ErrorPanel(onRetry: provider.loadOverview);
		final metrics = [
			('الحسابات', '${provider.ledgerAccounts.length}', Icons.account_tree_outlined),
			('القيود', '${provider.journalEntries.length}', Icons.menu_book_outlined),
			('المعاملات', '${provider.transactions.length}', Icons.swap_horiz_outlined),
			('الحسابات النقدية', '${provider.cashAccounts.length}', Icons.account_balance_wallet_outlined),
		];
		return ListView(children: [
			Text('نظرة عامة', style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 6),
			const Text('مؤشرات عددية من الخدمات الحالية دون إعادة احتساب أي قيمة مالية.'),
			const SizedBox(height: 16),
			Wrap(spacing: 12, runSpacing: 12, children: metrics.map((metric) => _FinanceMetricCard(label: metric.$1, value: metric.$2, icon: metric.$3)).toList()),
			const SizedBox(height: 18),
			const _StatusCard('الإحصاءات المالية التفصيلية', 'هيكل أولي', 'لا توجد خدمة إحصاءات مالية مستقلة في النظام الحالي، لذلك لم تُنشأ إجماليات أو مؤشرات محاسبية جديدة.'),
		]);
	}
}

class _FinanceMetricCard extends StatelessWidget {
	const _FinanceMetricCard({required this.label, required this.value, required this.icon});
	final String label;
	final String value;
	final IconData icon;
	@override Widget build(BuildContext context) => SizedBox(
		width: 210,
		height: 92,
		child: Card(child: Padding(padding: const EdgeInsets.all(14), child: Row(children: [
			Icon(icon, size: 30),
			const SizedBox(width: 12),
			Expanded(child: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label), Text(value, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold))])),
		]))),
	);
}

class _FinancialReportsTab extends StatelessWidget {
	const _FinancialReportsTab();
	@override Widget build(BuildContext context) => ListView(children: [
		Text('القوائم المالية', style: Theme.of(context).textTheme.titleLarge),
		const SizedBox(height: 8),
		const _UnavailableReport('الأرباح والخسائر'),
		const _UnavailableReport('الميزانية'),
		const _UnavailableReport('التدفقات النقدية'),
		const SizedBox(height: 20),
		Text('التقارير المتقدمة', style: Theme.of(context).textTheme.titleLarge),
		const SizedBox(height: 8),
		const _UnavailableReport('الملخص المالي'),
		const _UnavailableReport('تقييم المخزون'),
		const _UnavailableReport('تكلفة الإنتاج'),
		const _UnavailableReport('الذمم الدائنة'),
		const _UnavailableReport('ملخص الرواتب'),
		const _UnavailableReport('الملخص التنفيذي'),
	]);
}

class _UnavailableReport extends StatelessWidget {
	const _UnavailableReport(this.title);
	final String title;
	@override Widget build(BuildContext context) => ListTile(
		leading: const Icon(Icons.description_outlined),
		title: Text(title),
		subtitle: const Text('لا توجد خدمة تقرير فعلية لهذا التقرير في النظام الحالي.'),
		trailing: const Chip(label: Text('غير متاح حالياً')),
	);
}

class FinancialTransactionsScreen extends StatefulWidget {
	const FinancialTransactionsScreen({this.embedded = false, super.key});
	final bool embedded;
	@override State<FinancialTransactionsScreen> createState() => _FinancialTransactionsScreenState();
}

class _FinancialTransactionsScreenState extends State<FinancialTransactionsScreen> {
	final repository = FinanceRepository(); final search = TextEditingController();
	late Future<List<FinancialTransaction>> future; String? type; DateTime? from; DateTime? to;
	@override void initState() { super.initState(); future = repository.getTransactions(); }
	@override void dispose() { search.dispose(); super.dispose(); }
	void reload() => setState(() => future = repository.getTransactions());
	@override Widget build(BuildContext context) => _Page(title: 'الحركات المالية', onRefresh: reload, embedded: widget.embedded, child: FutureBuilder<List<FinancialTransaction>>(future: future, builder: (context, snapshot) {
		if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
		if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
		final all = snapshot.data!; final types = all.map((item) => item.transactionType).toSet().toList()..sort();
		final query = search.text.trim().toLowerCase(); final items = all.where((item) => (type == null || item.transactionType == type) && (from == null || !item.createdAt.isBefore(from!)) && (to == null || item.createdAt.isBefore(to!.add(const Duration(days: 1)))) && (query.isEmpty || item.referenceNumber.toLowerCase().contains(query) || item.transactionType.toLowerCase().contains(query) || (item.description?.toLowerCase().contains(query) ?? false))).toList();
		return Column(children: [Wrap(spacing: 10, runSpacing: 10, children: [SizedBox(width: 260, child: TextField(controller: search, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.search), labelText: 'بحث بالمرجع أو الوصف', border: OutlineInputBorder()))), SizedBox(width: 210, child: DropdownButtonFormField<String>(initialValue: type, decoration: const InputDecoration(labelText: 'نوع الحركة', border: OutlineInputBorder()), items: [const DropdownMenuItem(value: null, child: Text('كل الأنواع')), ...types.map((value) => DropdownMenuItem(value: value, child: Text(value)))], onChanged: (value) => setState(() => type = value))), _DateFilter(label: 'من تاريخ', value: from, onChanged: (value) => setState(() => from = value)), _DateFilter(label: 'إلى تاريخ', value: to, onChanged: (value) => setState(() => to = value))]), const SizedBox(height: 12), Expanded(child: _Table(columns: const ['المرجع', 'النوع', 'المبلغ', 'التاريخ', 'الوصف'], rows: items.map((item) => [item.referenceNumber, item.transactionType, _money.format(item.amount), _date.format(item.createdAt), _text(item.description)]).toList(), empty: 'لا توجد حركات مطابقة.'))]);
	}));
}

class JournalEntriesScreen extends StatefulWidget {
	const JournalEntriesScreen({this.embedded = false, super.key});
	final bool embedded;
	@override State<JournalEntriesScreen> createState() => _JournalEntriesScreenState();
}
class _JournalEntriesScreenState extends State<JournalEntriesScreen> {
	final repository = FinanceRepository(); late Future<List<_JournalEntrySummary>> future;
	@override void initState() { super.initState(); future = load(); }
	Future<List<_JournalEntrySummary>> load() async {
		final entries = await repository.getJournalEntries();
		return Future.wait(entries.map((entry) async => _JournalEntrySummary(entry, await repository.getJournalEntryLines(entry.id))));
	}
	void reload() => setState(() => future = load());
	@override Widget build(BuildContext context) => _Page(
		title: 'القيود اليومية',
		onRefresh: reload,
		embedded: widget.embedded,
		child: FutureBuilder<List<_JournalEntrySummary>>(
			future: future,
			builder: (context, snapshot) {
				if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
				if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
				final items = snapshot.data!;
				return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
					Row(children: [
						Tooltip(message: 'إنشاء القيود غير مدعوم لأن خدمة المالية الحالية للقراءة فقط.', child: FilledButton.icon(onPressed: null, icon: const Icon(Icons.add), label: const Text('قيد جديد'))),
						const SizedBox(width: 12),
						const Expanded(child: Text('اضغط على أي قيد لعرض تفاصيله وأسطره.')),
					]),
					const SizedBox(height: 10),
					Expanded(child: _Table(
						columns: const ['المرجع', 'التاريخ', 'الوصف', 'إجمالي المدين', 'إجمالي الدائن'],
						rows: items.map((item) => [item.entry.referenceNumber, _date.format(item.entry.entryDate), _text(item.entry.description), _money.format(item.debit), _money.format(item.credit)]).toList(),
						empty: 'لا توجد قيود مسجلة.',
						onRowTap: (index) => AppNavigation.push(context, (_) => JournalEntryDetailsScreen(entryId: items[index].entry.id)),
					)),
				]);
			},
		),
	);
}

class _JournalEntrySummary {
	const _JournalEntrySummary(this.entry, this.lines);
	final JournalEntry entry;
	final List<JournalEntryLine> lines;
	double get debit => lines.fold(0, (sum, line) => sum + line.debitAmount);
	double get credit => lines.fold(0, (sum, line) => sum + line.creditAmount);
}

class JournalEntryDetailsScreen extends StatefulWidget {
	const JournalEntryDetailsScreen({required this.entryId, super.key}); final int entryId;
	@override State<JournalEntryDetailsScreen> createState() => _JournalEntryDetailsScreenState();
}
class _JournalEntryDetailsScreenState extends State<JournalEntryDetailsScreen> {
	final repository = FinanceRepository(); late Future<(JournalEntry, List<JournalEntryLine>, List<LedgerAccount>)> future;
	@override void initState() { super.initState(); future = load(); }
	Future<(JournalEntry, List<JournalEntryLine>, List<LedgerAccount>)> load() async { final result = await Future.wait([repository.getJournalEntry(widget.entryId), repository.getJournalEntryLines(widget.entryId), repository.getLedgerAccounts()]); return (result[0] as JournalEntry, result[1] as List<JournalEntryLine>, result[2] as List<LedgerAccount>); }
	void reload() => setState(() => future = load());
	@override Widget build(BuildContext context) => _Page(title: 'تفاصيل القيد', onRefresh: reload, child: FutureBuilder(future: future, builder: (context, snapshot) {
		if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator()); if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
		final (entry, lines, accounts) = snapshot.data!; final names = {for (final account in accounts) account.id: account.accountName}; final debit = lines.fold<double>(0, (sum, line) => sum + line.debitAmount); final credit = lines.fold<double>(0, (sum, line) => sum + line.creditAmount); final balanced = (debit - credit).abs() < 0.005;
		return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [Card(child: Padding(padding: const EdgeInsets.all(16), child: Wrap(spacing: 28, runSpacing: 8, children: [Text('المرجع: ${entry.referenceNumber}'), Text('تاريخ القيد: ${_date.format(entry.entryDate)}'), Text('إجمالي المدين: ${_money.format(debit)}'), Text('إجمالي الدائن: ${_money.format(credit)}'), Chip(avatar: Icon(balanced ? Icons.check_circle : Icons.warning_amber, color: balanced ? Colors.green : Colors.orange), label: Text(balanced ? 'القيد متوازن' : 'القيد غير متوازن'))]))), const SizedBox(height: 10), Expanded(child: _Table(columns: const ['الحساب', 'المدين', 'الدائن', 'الوصف'], rows: lines.map((line) => [names[line.ledgerAccountId] ?? 'حساب ${line.ledgerAccountId}', _money.format(line.debitAmount), _money.format(line.creditAmount), _text(line.description)]).toList(), empty: 'لا توجد أسطر لهذا القيد.'))]);
	}));
}

class LedgerAccountsScreen extends StatelessWidget {
	const LedgerAccountsScreen({this.embedded = false, super.key});
	final bool embedded;
	@override Widget build(BuildContext context) => _SimpleListPage<LedgerAccount>(title: 'دليل الحسابات', embedded: embedded, load: FinanceRepository().getLedgerAccounts, columns: const ['رمز الحساب', 'اسم الحساب', 'نوع الحساب', 'الحالة'], row: (item) => [item.accountCode, item.accountName, item.accountType, item.isActive ? 'نشط' : 'غير نشط'], toolbar: const _LedgerActions());
}
class CashAccountsScreen extends StatelessWidget {
	const CashAccountsScreen({this.embedded = false, super.key});
	final bool embedded;
	@override Widget build(BuildContext context) => _SimpleListPage<CashAccount>(title: 'الحسابات النقدية', embedded: embedded, load: FinanceRepository().getCashAccounts, columns: const ['اسم الحساب', 'الرصيد الحالي', 'الحالة'], row: (item) => [item.accountName, _money.format(item.currentBalance), item.isActive ? 'نشط' : 'غير نشط']);
}

class CustomerLedgerScreen extends StatelessWidget {
	const CustomerLedgerScreen({this.embedded = false, super.key});
	final bool embedded;
	@override Widget build(BuildContext context) => _LedgerLookup<CustomerLedgerEntry>(title: 'كشف حساب العميل', partyLabel: 'رقم العميل', embedded: embedded, load: FinanceRepository().getCustomerLedger, row: (item) => [item.referenceNumber, _money.format(item.debitAmount), _money.format(item.creditAmount), _money.format(item.balanceAfterTransaction), _date.format(item.createdAt)]);
}
class SupplierLedgerScreen extends StatelessWidget {
	const SupplierLedgerScreen({super.key});
	@override Widget build(BuildContext context) => _LedgerLookup<SupplierLedgerEntry>(title: 'كشف حساب المورد', partyLabel: 'رقم المورد', load: FinanceRepository().getSupplierLedger, row: (item) => [item.referenceNumber, _money.format(item.debitAmount), _money.format(item.creditAmount), _money.format(item.balanceAfterTransaction), _date.format(item.createdAt)]);
}

class _LedgerActions extends StatelessWidget {
	const _LedgerActions();
	static const reason = 'إضافة الحسابات وتعديلها غير مدعومتين لأن خدمة المالية الحالية للقراءة فقط.';
	@override Widget build(BuildContext context) => Row(children: [
		Tooltip(message: reason, child: FilledButton.icon(onPressed: null, icon: const Icon(Icons.add), label: const Text('إضافة حساب'))),
		const SizedBox(width: 8),
		Tooltip(message: reason, child: OutlinedButton.icon(onPressed: null, icon: const Icon(Icons.edit_outlined), label: const Text('تعديل حساب'))),
		const SizedBox(width: 12),
		const Expanded(child: Text('التحديث متاح من زر التحديث أعلى القائمة.')),
	]);
}

class SupplierInvoicesScreen extends StatelessWidget {
	const SupplierInvoicesScreen({super.key});
	@override Widget build(BuildContext context) => _SimpleListPage<SupplierInvoice>(title: 'فواتير الموردين', load: FinanceRepository().getSupplierInvoices, columns: const ['المعرف', 'رقم الفاتورة', 'المورد', 'أمر الشراء', 'التاريخ', 'الاستحقاق', 'الإجمالي', 'المدفوع', 'الحالة', 'ملاحظات', 'تاريخ الإنشاء'], row: (item) => ['${item.id}', item.invoiceNumber, '${item.supplierId}', '${item.purchaseOrderId}', _date.format(item.invoiceDate), _date.format(item.dueDate), _money.format(item.totalAmount), _money.format(item.amountPaid), item.status, _text(item.notes), _date.format(item.createdAt)]);
}
class SupplierPaymentsScreen extends StatelessWidget {
	const SupplierPaymentsScreen({super.key});
	@override Widget build(BuildContext context) => _SimpleListPage<SupplierPayment>(title: 'دفعات الموردين', load: FinanceRepository().getSupplierPayments, columns: const ['المعرف', 'رقم الدفعة', 'المورد', 'التاريخ', 'المبلغ', 'الطريقة', 'المرجع', 'ملاحظات', 'تاريخ الإنشاء', 'القيد'], row: (item) => ['${item.id}', item.paymentNumber, '${item.supplierId}', _date.format(item.paymentDate), _money.format(item.amount), _text(item.paymentMethod), _text(item.referenceNumber), _text(item.notes), _date.format(item.createdAt), item.journalEntryId?.toString() ?? '-']);
}
class OperatingExpensesScreen extends StatelessWidget {
	const OperatingExpensesScreen({super.key});
	Future<List<FinancialTransaction>> load() async => (await FinanceRepository().getTransactions()).where((item) => item.transactionType == 'OperatingExpense').toList();
	@override Widget build(BuildContext context) => _SimpleListPage<FinancialTransaction>(title: 'المصروفات التشغيلية', load: load, columns: const ['المرجع', 'المبلغ', 'الوصف', 'التاريخ'], row: (item) => [item.referenceNumber, _money.format(item.amount), _text(item.description), _date.format(item.createdAt)]);
}

class FinancialReconciliationScreen extends StatelessWidget {
	const FinancialReconciliationScreen({super.key});
	@override Widget build(BuildContext context) => _SimpleListPage<FinancialReconciliation>(title: 'المطابقة والتدقيق', load: () async => [await FinanceRepository().getReconciliation()], columns: const ['الحركات', 'القيود', 'المتوازنة', 'غير المتوازنة', 'المراجع المشتركة', 'حركات بلا قيد', 'قيود بلا حركة', 'أسطر يتيمة'], row: (item) => ['${item.financialTransactions}', '${item.journalEntries}', '${item.balancedJournalEntries}', '${item.unbalancedJournalEntries}', '${item.sharedReferences}', '${item.financialReferencesWithoutJournal}', '${item.journalReferencesWithoutTransaction}', '${item.orphanJournalLines}']);
}

class FinancialStatementsScreen extends StatelessWidget {
	const FinancialStatementsScreen({super.key});
	@override Widget build(BuildContext context) => _Page(title: 'القوائم المالية', child: ListView(children: const [
		_StatusCard('الإيرادات', 'تغطية جزئية', 'Orders وInvoice_Header وFinancialTransactions مصادر متداخلة، ولا يوجد مصدر واحد مكتمل.'),
		_StatusCard('تكلفة المبيعات', 'تغطية جزئية', 'FinancialTransactions يسجل تكاليف محددة، ولا يغطي دفتر الأستاذ كامل التاريخ.'),
		_StatusCard('المصروفات', 'تغطية جزئية', 'OperatingExpense موجود ضمن الحركات المالية، لكن التغطية المحاسبية التاريخية غير مكتملة.'),
		_StatusCard('النقدية', 'رصيد حالي فقط', 'CashAccounts يعرض الرصيد المخزن حاليًا دون سجل كامل قابل لإعادة بناء التدفق.'),
		_StatusCard('الذمم المدينة', 'تغطية جزئية', 'CustomerLedgerEntries يحفظ الرصيد التاريخي بدلالاته الأصلية ولا يجوز إعادة احتسابه.'),
		_StatusCard('الذمم الدائنة', 'تغطية جزئية', 'SupplierLedgerEntries وSupplierInvoices لا يمثلان تاريخًا كاملاً موحدًا.'),
		_StatusCard('المخزون', 'رصيد تشغيلي', 'InventoryItems وReadyMadeInventoryProducts يوفران كميات وتكاليف متفاوتة الاكتمال.'),
		_StatusCard('الالتزامات الأخرى', 'غير مكتملة', 'تصنيف الحسابات الحالي لا يثبت تغطية جميع الالتزامات التاريخية.'),
		Divider(height: 28),
		_StatusCard('ميزان المراجعة', 'غير متاح بدقة', 'القيود متوازنة، لكنها لا تغطي جميع المراجع المالية.'),
		_StatusCard('الأرباح والخسائر', 'البيانات غير مكتملة', 'عرض رقم نهائي سيجمع مصادر متداخلة أو يهمل حركات تاريخية.'),
		_StatusCard('الميزانية العمومية', 'البيانات غير مكتملة', 'تصنيف AccountType غير موحد وتغطية القيود جزئية.'),
		_StatusCard('التدفقات النقدية', 'البيانات غير مكتملة', 'لا يوجد مصدر تاريخي واحد مكتمل للحركات النقدية.'),
	]));
}

class _StatusCard extends StatelessWidget {
	const _StatusCard(this.title, this.status, this.reason); final String title; final String status; final String reason;
	@override Widget build(BuildContext context) => Card(child: ListTile(leading: const Icon(Icons.info_outline), title: Text(title), subtitle: Text(reason), trailing: Text(status, style: const TextStyle(fontWeight: FontWeight.bold))));
}

class _LedgerLookup<T> extends StatefulWidget {
	const _LedgerLookup({required this.title, required this.partyLabel, required this.load, required this.row, this.embedded = false});
	final String title; final String partyLabel; final Future<List<T>> Function(int) load; final List<String> Function(T) row; final bool embedded;
	@override State<_LedgerLookup<T>> createState() => _LedgerLookupState<T>();
}
class _LedgerLookupState<T> extends State<_LedgerLookup<T>> {
	final controller = TextEditingController(); final filter = TextEditingController(); Future<List<T>>? future;
	@override void dispose() { controller.dispose(); filter.dispose(); super.dispose(); }
	void search() { final id = int.tryParse(controller.text.trim()); if (id != null && id > 0) setState(() => future = widget.load(id)); }
	@override Widget build(BuildContext context) => _Page(title: widget.title, embedded: widget.embedded, child: Column(children: [Wrap(spacing: 10, runSpacing: 10, children: [SizedBox(width: 240, child: TextField(controller: controller, keyboardType: TextInputType.number, onSubmitted: (_) => search(), decoration: InputDecoration(labelText: widget.partyLabel, border: const OutlineInputBorder()))), FilledButton.icon(onPressed: search, icon: const Icon(Icons.search), label: const Text('عرض الكشف')), SizedBox(width: 260, child: TextField(controller: filter, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.filter_alt_outlined), labelText: 'تصفية بالمرجع أو القيمة', border: OutlineInputBorder()))) ]), const SizedBox(height: 12), Expanded(child: future == null ? const Center(child: Text('أدخل الرقم لعرض كشف الحساب.')) : FutureBuilder<List<T>>(future: future, builder: (context, snapshot) { if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator()); if (snapshot.hasError) return _ErrorPanel(onRetry: search); final query = filter.text.trim().toLowerCase(); final rows = snapshot.data!.map(widget.row).where((row) => query.isEmpty || row.any((value) => value.toLowerCase().contains(query))).toList(); return _Table(columns: const ['المرجع', 'مدين', 'دائن', 'الرصيد', 'التاريخ'], rows: rows, empty: 'لا توجد حركات مطابقة لهذا الحساب.'); }))]));
}

class _SimpleListPage<T> extends StatefulWidget {
	const _SimpleListPage({required this.title, required this.load, required this.columns, required this.row, this.embedded = false, this.toolbar});
	final String title; final Future<List<T>> Function() load; final List<String> columns; final List<String> Function(T) row; final bool embedded; final Widget? toolbar;
	@override State<_SimpleListPage<T>> createState() => _SimpleListPageState<T>();
}
class _SimpleListPageState<T> extends State<_SimpleListPage<T>> {
	late Future<List<T>> future;
	@override void initState() { super.initState(); future = widget.load(); }
	void reload() => setState(() => future = widget.load());
	@override Widget build(BuildContext context) => _Page(title: widget.title, onRefresh: reload, embedded: widget.embedded, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [if (widget.toolbar != null) ...[widget.toolbar!, const SizedBox(height: 10)], Expanded(child: FutureBuilder<List<T>>(future: future, builder: (context, snapshot) { if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator()); if (snapshot.hasError) return _ErrorPanel(onRetry: reload); return _Table(columns: widget.columns, rows: snapshot.data!.map(widget.row).toList(), empty: 'لا توجد بيانات متاحة.'); }))]));
}

class _Page extends StatelessWidget {
	const _Page({required this.title, required this.child, this.onRefresh, this.embedded = false}); final String title; final Widget child; final VoidCallback? onRefresh; final bool embedded;
	@override Widget build(BuildContext context) {
		final content = SafeArea(child: Padding(padding: const EdgeInsets.all(16), child: child));
		if (embedded) return content;
		return Scaffold(appBar: Navigator.of(context).canPop() ? AppBar(title: Text(title), actions: [if (onRefresh != null) IconButton(tooltip: 'تحديث', onPressed: onRefresh, icon: const Icon(Icons.refresh))]) : null, body: content);
	}
}
class _Table extends StatelessWidget {
	const _Table({required this.columns, required this.rows, required this.empty, this.onRowTap}); final List<String> columns; final List<List<String>> rows; final String empty; final ValueChanged<int>? onRowTap;
	@override Widget build(BuildContext context) {
		if (rows.isEmpty) return Center(child: Text(empty));
		return Card(
			clipBehavior: Clip.antiAlias,
			child: Scrollbar(
				child: SingleChildScrollView(
					scrollDirection: Axis.horizontal,
					child: SingleChildScrollView(
						child: DataTable(
							columns: columns.map((value) => DataColumn(label: Text(value, style: const TextStyle(fontWeight: FontWeight.bold)))).toList(),
							rows: rows.asMap().entries.map((entry) => DataRow(onSelectChanged: onRowTap == null ? null : (_) => onRowTap!(entry.key), cells: entry.value.map((value) => DataCell(SelectableText(value))).toList())).toList(),
						),
					),
				),
			),
		);
	}
}
class _DateFilter extends StatelessWidget {
	const _DateFilter({required this.label, required this.value, required this.onChanged}); final String label; final DateTime? value; final ValueChanged<DateTime?> onChanged;
	@override Widget build(BuildContext context) => OutlinedButton.icon(icon: const Icon(Icons.calendar_month_outlined), label: Text(value == null ? label : _date.format(value!)), onPressed: () async { final selected = await showDatePicker(context: context, firstDate: DateTime(2000), lastDate: DateTime(2100), initialDate: value ?? DateTime.now()); if (selected != null) onChanged(selected); });
}
class _ErrorPanel extends StatelessWidget {
	const _ErrorPanel({required this.onRetry}); final VoidCallback onRetry;
	@override Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.cloud_off_outlined, size: 42), const SizedBox(height: 10), const Text('تعذر تحميل البيانات المالية.'), const SizedBox(height: 10), FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة'))]));
}