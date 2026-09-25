import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../core/finance_ui_text.dart';
import '../../models/finance_models.dart';
import '../../providers/finance_provider.dart';
import '../../repositories/finance_repository.dart';
import '../customers_screen.dart';

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
		final dashboard = provider.dashboard;
		if (dashboard == null) return _ErrorPanel(onRetry: provider.loadOverview);
		final metrics = [
			('إجمالي الإيرادات', _money.format(dashboard.revenue), Icons.trending_up_outlined),
			('إجمالي التحصيلات', _money.format(dashboard.collections), Icons.payments_outlined),
			('الذمم المدينة', _money.format(dashboard.receivables), Icons.person_search_outlined),
			('النقدية الحالية', _money.format(dashboard.cashBalance), Icons.account_balance_wallet_outlined),
			('القيود', '${dashboard.journalEntries}', Icons.menu_book_outlined),
			('الحركات المالية', '${dashboard.financialTransactions}', Icons.swap_horiz_outlined),
			('العملاء النشطون مالياً', '${dashboard.financialCustomers}', Icons.people_outline),
			('إيراد اليوم', _money.format(dashboard.dailyRevenue), Icons.today_outlined),
			('إيراد الشهر', _money.format(dashboard.monthlyRevenue), Icons.calendar_month_outlined),
		];
		return ListView(children: [
			Text('نظرة عامة', style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 6),
			const Text('مؤشرات محسوبة من سجلات المالية والعملاء الحالية.'),
			const SizedBox(height: 16),
			Wrap(spacing: 12, runSpacing: 12, children: metrics.map((metric) => _FinanceMetricCard(label: metric.$1, value: metric.$2, icon: metric.$3)).toList()),
			const SizedBox(height: 18),
			_SectionHeading(title: 'أعلى العملاء مديونية', description: 'حسب آخر رصيد رسمي في أستاذ العميل.'),
			...dashboard.topDebtors.map((item) => _CashListRow(title: '${item.customerCode} - ${item.customerName}', value: _money.format(item.amount))),
			const SizedBox(height: 12),
			_SectionHeading(title: 'أعلى العملاء تحصيلاً', description: 'حسب الدفعات المسجلة غير المستردة.'),
			...dashboard.topCollections.map((item) => _CashListRow(title: '${item.customerCode} - ${item.customerName}', value: _money.format(item.amount))),
			const SizedBox(height: 12),
			_SectionHeading(title: 'آخر النشاطات المالية', description: 'أحدث الحركات المسجلة.'),
			...dashboard.recentActivities.map((item) => _CashListRow(title: FinanceUiText.transactionType(item.transactionType), subtitle: '${item.referenceNumber} - ${_date.format(item.createdAt)}', value: _money.format(item.amount))),
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
	@override Widget build(BuildContext context) => DefaultTabController(
		length: 2,
		child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			Text('القوائم والتقارير', style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 8),
			const TabBar(tabs: [
				Tab(text: 'القوائم المالية'),
				Tab(text: 'التقارير المتقدمة'),
			]),
			const SizedBox(height: 8),
			const Expanded(child: TabBarView(children: [
				_FinancialStatementsTabs(),
				_AdvancedReportsTab(),
			])),
		]),
	);
}

class _FinancialStatementsTabs extends StatefulWidget {
	const _FinancialStatementsTabs();
	@override State<_FinancialStatementsTabs> createState() => _FinancialStatementsTabsState();
}

class _FinancialStatementsTabsState extends State<_FinancialStatementsTabs> {
	final repository = FinanceRepository();
	late Future<FinancialStatements> future;

	@override
	void initState() {
		super.initState();
		future = repository.getFinancialStatements();
	}

	void reload() => setState(() => future = repository.getFinancialStatements());

	@override
	Widget build(BuildContext context) => FutureBuilder<FinancialStatements>(
		future: future,
		builder: (context, snapshot) {
			if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
			if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
			final statements = snapshot.data!;
			return DefaultTabController(
				length: 3,
				child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
					Row(children: [
						const Expanded(child: Text('القوائم المالية من البيانات الفعلية')),
						IconButton(tooltip: 'تحديث القوائم', onPressed: reload, icon: const Icon(Icons.refresh)),
					]),
					const TabBar(tabs: [
						Tab(text: 'الأرباح والخسائر'),
						Tab(text: 'الميزانية'),
						Tab(text: 'التدفقات النقدية'),
					]),
					const SizedBox(height: 8),
					Expanded(child: TabBarView(children: [
						_FinancialValueList(title: 'الأرباح والخسائر', values: [
							('الإيرادات', statements.profitLoss.revenue),
							('تكلفة البضاعة المباعة', statements.profitLoss.costOfGoodsSold),
							('إجمالي الربح', statements.profitLoss.grossProfit),
							('المصروفات', statements.profitLoss.expenses),
							('صافي الربح', statements.profitLoss.netProfit),
						]),
						_FinancialValueList(title: 'الميزانية العمومية', values: [
							('الأصول', statements.balanceSheet.assets),
							('الالتزامات', statements.balanceSheet.liabilities),
							('الحسابات المدينة', statements.balanceSheet.accountsReceivable),
							('قيمة المخزون', statements.balanceSheet.inventoryValue),
							('حقوق الملكية', statements.balanceSheet.equity),
						]),
						_CashFlowStatement(cashFlow: statements.cashFlow),
					])),
				]),
			);
		},
	);
}

class _FinancialValueList extends StatelessWidget {
	const _FinancialValueList({required this.title, required this.values});
	final String title;
	final List<(String, double)> values;

	@override
	Widget build(BuildContext context) => ListView(
		children: [
			Text(title, style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 8),
			...values.map((entry) => Card(
				child: ListTile(
					title: Text(entry.$1),
					trailing: Text(_money.format(entry.$2), style: Theme.of(context).textTheme.titleMedium),
				),
			)),
		],
	);
}

class _AdvancedReportsTab extends StatelessWidget {
	const _AdvancedReportsTab();
	@override Widget build(BuildContext context) => ListView(children: const [
		_UnavailableReport(title: 'الملخص المالي', message: 'لا توجد خدمة تقرير فعلية لهذا التقرير في النظام الحالي.'),
		_UnavailableReport(title: 'تقييم المخزون', message: 'لا توجد خدمة تقرير فعلية لهذا التقرير في النظام الحالي.'),
		_UnavailableReport(title: 'تكلفة الإنتاج', message: 'لا توجد خدمة تقرير فعلية لهذا التقرير في النظام الحالي.'),
		_UnavailableReport(title: 'الذمم الدائنة', message: 'لا توجد خدمة تقرير فعلية لهذا التقرير في النظام الحالي.'),
		_UnavailableReport(title: 'ملخص الرواتب', message: 'لا توجد خدمة تقرير فعلية لهذا التقرير في النظام الحالي.'),
	]);
}

class _UnavailableReport extends StatelessWidget {
	const _UnavailableReport({required this.title, required this.message});
	final String title;
	final String message;
	@override Widget build(BuildContext context) => ListTile(
		leading: const Icon(Icons.description_outlined),
		title: Text(title),
		subtitle: Text(message),
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
		return Column(children: [Wrap(spacing: 10, runSpacing: 10, children: [SizedBox(width: 260, child: TextField(controller: search, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.search), labelText: 'بحث بالمرجع أو الوصف', border: OutlineInputBorder()))), SizedBox(width: 210, child: DropdownButtonFormField<String>(initialValue: type, decoration: const InputDecoration(labelText: 'نوع الحركة', border: OutlineInputBorder()), items: [const DropdownMenuItem(value: null, child: Text('كل الأنواع')), ...types.map((value) => DropdownMenuItem(value: value, child: Text(FinanceUiText.transactionType(value))))], onChanged: (value) => setState(() => type = value))), _DateFilter(label: 'من تاريخ', value: from, onChanged: (value) => setState(() => from = value)), _DateFilter(label: 'إلى تاريخ', value: to, onChanged: (value) => setState(() => to = value))]), const SizedBox(height: 12), Expanded(child: _TransactionList(items: items, empty: 'لا توجد حركات مطابقة.'))]);
	}));
}

class JournalEntriesScreen extends StatefulWidget {
	const JournalEntriesScreen({this.embedded = false, super.key});
	final bool embedded;
	@override State<JournalEntriesScreen> createState() => _JournalEntriesScreenState();
}
class _JournalEntriesScreenState extends State<JournalEntriesScreen> {
	final repository = FinanceRepository(); late Future<List<JournalEntry>> future;
	@override void initState() { super.initState(); future = load(); }
	Future<List<JournalEntry>> load() => repository.getJournalEntries();
	void reload() => setState(() => future = load());
	@override Widget build(BuildContext context) => _Page(
		title: 'القيود اليومية',
		onRefresh: reload,
		embedded: widget.embedded,
		child: FutureBuilder<List<JournalEntry>>(
			future: future,
			builder: (context, snapshot) {
				if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
				if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
				final items = snapshot.data!;
				return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
					Row(children: [
						Tooltip(message: 'إنشاء القيود غير مدعوم لأن خدمة المالية الحالية للقراءة فقط.', child: FilledButton.icon(onPressed: null, icon: const Icon(Icons.add), label: const Text('إنشاء قيد'))),
						const SizedBox(width: 12),
						const Expanded(child: Text('اضغط على أي قيد لعرض تفاصيله وأسطره.')),
					]),
					const SizedBox(height: 10),
					Expanded(child: _Table(
						columns: const ['المرجع', 'التاريخ', 'الوصف', 'إجمالي المدين', 'إجمالي الدائن'],
						rows: items.map((item) => [item.referenceNumber, _date.format(item.entryDate), FinanceUiText.description(item.description), _money.format(item.totalDebit), _money.format(item.totalCredit)]).toList(),
						empty: 'لا توجد قيود مسجلة.',
						onRowTap: (index) => AppNavigation.push(context, (_) => JournalEntryDetailsScreen(entryId: items[index].id)),
					)),
				]);
			},
		),
	);
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
		final (entry, lines, accounts) = snapshot.data!; final names = {for (final account in accounts) account.id: FinanceUiText.accountName(account.accountCode, account.accountName)}; final debit = lines.fold<double>(0, (sum, line) => sum + line.debitAmount); final credit = lines.fold<double>(0, (sum, line) => sum + line.creditAmount); final balanced = (debit - credit).abs() < 0.005; final statusColor = balanced ? Theme.of(context).colorScheme.primary : Theme.of(context).colorScheme.error;
		return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [Card(child: Padding(padding: const EdgeInsets.all(16), child: Wrap(spacing: 28, runSpacing: 8, children: [Text('المرجع: ${entry.referenceNumber}'), Text('تاريخ القيد: ${_date.format(entry.entryDate)}'), Text('إجمالي المدين: ${_money.format(debit)}'), Text('إجمالي الدائن: ${_money.format(credit)}'), Chip(avatar: Icon(balanced ? Icons.check_circle : Icons.warning_amber, color: statusColor), label: Text(balanced ? 'القيد متوازن' : 'القيد غير متوازن'))]))), const SizedBox(height: 10), Expanded(child: _Table(columns: const ['الحساب', 'المدين', 'الدائن', 'الوصف'], rows: lines.map((line) => [names[line.ledgerAccountId] ?? 'حساب ${line.ledgerAccountId}', _money.format(line.debitAmount), _money.format(line.creditAmount), FinanceUiText.description(line.description)]).toList(), empty: 'لا توجد أسطر لهذا القيد.'))]);
	}));
}

class LedgerAccountsScreen extends StatelessWidget {
	const LedgerAccountsScreen({this.embedded = false, super.key});
	final bool embedded;
	@override Widget build(BuildContext context) => _SimpleListPage<LedgerAccount>(title: 'دليل الحسابات', embedded: embedded, load: FinanceRepository().getLedgerAccounts, columns: const ['رمز الحساب', 'اسم الحساب', 'نوع الحساب', 'الحالة'], row: (item) => [item.accountCode, FinanceUiText.accountName(item.accountCode, item.accountName), FinanceUiText.accountType(item.accountType), item.isActive ? 'نشط' : 'غير نشط'], toolbar: const _LedgerActions());
}
class CashAccountsScreen extends StatefulWidget {
	const CashAccountsScreen({this.embedded = false, super.key});
	final bool embedded;
	@override State<CashAccountsScreen> createState() => _CashAccountsScreenState();
}

class _CashAccountsScreenState extends State<CashAccountsScreen> {
	late Future<(List<CashAccount>, List<FinancialTransaction>, CashReconciliation)> future;
	final repository = FinanceRepository();
	@override void initState() { super.initState(); future = load(); }
	Future<(List<CashAccount>, List<FinancialTransaction>, CashReconciliation)> load() async {
		final result = await Future.wait([repository.getCashAccounts(), repository.getTransactions(), repository.getCashReconciliation()]);
		return (result[0] as List<CashAccount>, result[1] as List<FinancialTransaction>, result[2] as CashReconciliation);
	}
	void reload() => setState(() => future = load());
	@override Widget build(BuildContext context) => _Page(title: 'النقدية', onRefresh: reload, embedded: widget.embedded, child: FutureBuilder<(List<CashAccount>, List<FinancialTransaction>, CashReconciliation)>(future: future, builder: (context, snapshot) {
		if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
		if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
		final (accounts, transactions, reconciliation) = snapshot.data!;
		return DefaultTabController(length: 3, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			const TabBar(tabs: [Tab(text: 'الصندوق'), Tab(text: 'الحسابات النقدية'), Tab(text: 'الحركات والتسويات')]),
			const SizedBox(height: 8),
			Expanded(child: TabBarView(children: [
				_CashSummary(accounts: accounts, reconciliation: reconciliation),
				_CashAccountsList(accounts: accounts),
				_TransactionList(items: transactions, empty: 'لا توجد حركات نقدية متاحة.'),
			])),
		]));
	}));
}

class _CashSummary extends StatelessWidget {
	const _CashSummary({required this.accounts, required this.reconciliation});
	final List<CashAccount> accounts;
	final CashReconciliation reconciliation;
	@override Widget build(BuildContext context) {
		final total = accounts.fold<double>(0, (sum, account) => sum + account.currentBalance);
		return ListView(padding: const EdgeInsets.only(bottom: 12), children: [
			_SectionHeading(title: 'الصندوق', description: 'ملخص الأرصدة الحالية كما يوردها النظام.'),
			_CashValueRow(label: 'إجمالي الأرصدة النقدية', value: _money.format(total)),
			_CashValueRow(label: 'الحسابات النشطة', value: '${accounts.where((account) => account.isActive).length}'),
			_CashValueRow(label: 'إجمالي الحسابات', value: '${accounts.length}'),
			_CashValueRow(label: 'رصيد دفتر الأستاذ العام', value: _money.format(reconciliation.generalLedgerCashBalance)),
			_CashValueRow(label: 'فرق المطابقة النقدية', value: _money.format(reconciliation.difference)),
			if (!reconciliation.isReconciled) Card(color: Theme.of(context).colorScheme.errorContainer, child: const ListTile(leading: Icon(Icons.warning_amber_outlined), title: Text('يوجد فرق بين الحسابات النقدية ودفتر الأستاذ العام'), subtitle: Text('البيانات النقدية تحتاج إلى مراجعة قبل اعتمادها.'))),
		]);
	}
}

class _CashAccountsList extends StatelessWidget {
	const _CashAccountsList({required this.accounts});
	final List<CashAccount> accounts;
	@override Widget build(BuildContext context) => ListView(padding: const EdgeInsets.only(bottom: 12), children: [
		_SectionHeading(title: 'الحسابات النقدية', description: 'الأرصدة الحالية والحالة التشغيلية للحسابات المسجلة.'),
		...accounts.map((account) => _CashListRow(title: FinanceUiText.label(account.accountName, 'حساب نقدي'), subtitle: account.isActive ? 'نشط' : 'غير نشط', value: _money.format(account.currentBalance))),
	]);
}

class _SectionHeading extends StatelessWidget {
	const _SectionHeading({required this.title, required this.description});
	final String title;
	final String description;
	@override Widget build(BuildContext context) => Padding(
		padding: const EdgeInsetsDirectional.only(bottom: 10),
		child: Column(
			crossAxisAlignment: CrossAxisAlignment.stretch,
			children: [
				Text(title, style: Theme.of(context).textTheme.titleLarge),
				const SizedBox(height: 4),
				Text(description, style: Theme.of(context).textTheme.bodySmall),
			],
		),
	);
}

class _CashValueRow extends StatelessWidget {
	const _CashValueRow({required this.label, required this.value});
	final String label;
	final String value;
	@override Widget build(BuildContext context) => _CashListRow(title: label, value: value);
}

class _CashListRow extends StatelessWidget {
	const _CashListRow({required this.title, required this.value, this.subtitle});
	final String title;
	final String value;
	final String? subtitle;
	@override Widget build(BuildContext context) => Card(child: ListTile(title: Text(title), subtitle: subtitle == null ? null : Text(subtitle!), trailing: Text(value, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold))));
}

class _TransactionList extends StatelessWidget {
	const _TransactionList({required this.items, required this.empty});
	final List<FinancialTransaction> items;
	final String empty;
	@override Widget build(BuildContext context) {
		if (items.isEmpty) return Center(child: Text(empty));
		return ListView.separated(
			padding: const EdgeInsets.only(bottom: 12),
			itemCount: items.length,
			separatorBuilder: (_, __) => const SizedBox(height: 4),
			itemBuilder: (context, index) {
				final item = items[index];
				return Card(child: ListTile(
					leading: const Icon(Icons.swap_horiz_outlined),
					title: Text(item.referenceNumber, maxLines: 1, overflow: TextOverflow.ellipsis),
						subtitle: Text('${FinanceUiText.transactionType(item.transactionType)} • ${_date.format(item.createdAt)}${item.description == null ? '' : ' • ${FinanceUiText.description(item.description)}'}', maxLines: 2, overflow: TextOverflow.ellipsis),
					trailing: Text(_money.format(item.amount), style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold), textAlign: TextAlign.end),
				));
			},
		);
	}
}

class CustomerLedgerScreen extends StatefulWidget {
	const CustomerLedgerScreen({this.embedded = false, super.key});
	final bool embedded;
	@override State<CustomerLedgerScreen> createState() => _CustomerLedgerScreenState();
}

class _CustomerLedgerScreenState extends State<CustomerLedgerScreen> {
	final customerApi = CustomerApi();
	final repository = FinanceRepository();
	final search = TextEditingController();
	final filter = TextEditingController();
	Timer? searchTimer;
	List<Map<String, dynamic>> suggestions = const [];
	Map<String, dynamic>? selectedCustomer;
	Future<List<CustomerLedgerEntry>>? future;
	Object? searchError;
	bool searching = false;
	int searchVersion = 0;

	@override void dispose() {
		searchTimer?.cancel();
		customerApi.cancelSearch();
		search.dispose();
		filter.dispose();
		super.dispose();
	}

	void onSearchChanged(String value) {
		searchTimer?.cancel();
		final version = ++searchVersion;
		final term = value.trim();
		setState(() {
			suggestions = const [];
			searchError = null;
			searching = term.length >= 2;
			if (term.isEmpty) {
				selectedCustomer = null;
				future = null;
			}
		});
		if (term.length < 2) return;
		searchTimer = Timer(const Duration(milliseconds: 250), () async {
			try {
				final results = await customerApi.search(term);
				if (!mounted || version != searchVersion) return;
				setState(() {
					suggestions = results.take(8).toList();
					searching = false;
				});
			} catch (error) {
				if (!mounted || version != searchVersion) return;
				setState(() {
					searchError = error;
					searching = false;
				});
			}
		});
	}

	void selectCustomer(Map<String, dynamic> customer) {
		searchTimer?.cancel();
		searchVersion++;
		final customerId = int.tryParse('${customer['customerId']}');
		if (customerId == null || customerId <= 0) return;
		final name = customer['customerName']?.toString().trim();
		final code = customer['customerCode']?.toString().trim();
		search.text = (name == null || name.isEmpty) ? (code ?? '') : name;
		setState(() {
			selectedCustomer = customer;
			suggestions = const [];
			searchError = null;
			searching = false;
			future = repository.getCustomerLedger(customerId);
		});
	}

	void clearSelection() {
		searchTimer?.cancel();
		searchVersion++;
		search.clear();
		setState(() {
			selectedCustomer = null;
			suggestions = const [];
			future = null;
			searchError = null;
		});
	}

	void reload() {
		final customerId = int.tryParse('${selectedCustomer?['customerId']}');
		if (customerId != null && customerId > 0) {
			setState(() => future = repository.getCustomerLedger(customerId));
		}
	}

	@override Widget build(BuildContext context) => _Page(title: 'أستاذ العميل', onRefresh: reload, embedded: widget.embedded, child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
		Text('اختيار العميل', style: Theme.of(context).textTheme.titleLarge),
		const SizedBox(height: 8),
		TextField(controller: search, onChanged: onSearchChanged, onSubmitted: onSearchChanged, textInputAction: TextInputAction.search, decoration: InputDecoration(labelText: 'ابحث بكود العميل أو الاسم أو رقم الهاتف', prefixIcon: const Icon(Icons.person_search_outlined), suffixIcon: searching ? const Padding(padding: EdgeInsets.all(12), child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))) : search.text.isEmpty ? null : IconButton(tooltip: 'مسح اختيار العميل', onPressed: clearSelection, icon: const Icon(Icons.clear)))) ,
		if (searchError != null) Padding(padding: const EdgeInsets.only(top: 8), child: Text('تعذر البحث عن العميل. حاول مرة أخرى.', style: TextStyle(color: Theme.of(context).colorScheme.error))),
		if (suggestions.isNotEmpty) ...[
			const SizedBox(height: 6),
			_CustomerSuggestionList(items: suggestions, onSelected: selectCustomer),
		],
		if (selectedCustomer != null) ...[
			const SizedBox(height: 12),
			_SelectedCustomerPanel(customer: selectedCustomer!, onClear: clearSelection),
		],
		const SizedBox(height: 12),
		Expanded(child: future == null ? const Center(child: Text('ابحث عن العميل ثم اختره لعرض الأستاذ.')) : FutureBuilder<List<CustomerLedgerEntry>>(future: future, builder: (context, snapshot) {
			if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
			if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
			final query = filter.text.trim().toLowerCase();
			final rows = (snapshot.data ?? const <CustomerLedgerEntry>[]).where((item) => query.isEmpty || item.referenceNumber.toLowerCase().contains(query) || _money.format(item.balanceAfterTransaction).contains(query)).toList();
			final debit = rows.fold<double>(0, (sum, item) => sum + item.debitAmount);
			final credit = rows.fold<double>(0, (sum, item) => sum + item.creditAmount);
			final balance = rows.isEmpty ? 0.0 : rows.first.balanceAfterTransaction;
			return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [Wrap(spacing: 16, runSpacing: 8, children: [Text('إجمالي المدين: ${_money.format(debit)}'), Text('إجمالي الدائن: ${_money.format(credit)}'), Text('الرصيد الحالي: ${_money.format(balance)}'), Text('عدد العمليات: ${rows.length}')]), const SizedBox(height: 8), TextField(controller: filter, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.filter_alt_outlined), labelText: 'تصفية الأستاذ بالمرجع أو الرصيد')), const SizedBox(height: 8), Expanded(child: _Table(columns: const ['المرجع', 'مدين', 'دائن', 'الرصيد', 'التاريخ'], rows: rows.map((item) => [item.referenceNumber, _money.format(item.debitAmount), _money.format(item.creditAmount), _money.format(item.balanceAfterTransaction), _date.format(item.createdAt)]).toList(), empty: 'لا توجد حركات مطابقة لهذا العميل.'))]);
		}) ),
	]));
}

class _CustomerSuggestionList extends StatelessWidget {
	const _CustomerSuggestionList({required this.items, required this.onSelected});
	final List<Map<String, dynamic>> items;
	final ValueChanged<Map<String, dynamic>> onSelected;
	@override Widget build(BuildContext context) => Card(child: ConstrainedBox(constraints: const BoxConstraints(maxHeight: 240), child: ListView.separated(shrinkWrap: true, itemCount: items.length, separatorBuilder: (_, __) => const Divider(height: 1), itemBuilder: (context, index) {
		final customer = items[index];
		final code = customer['customerCode']?.toString() ?? '-';
		final name = customer['customerName']?.toString() ?? 'عميل';
		final phone = customer['phoneNumber']?.toString() ?? '';
		return ListTile(dense: true, leading: const Icon(Icons.person_outline), title: Text('$code  $name'), subtitle: phone.isEmpty ? null : Text(phone), onTap: () => onSelected(customer));
	})));
}

class _SelectedCustomerPanel extends StatelessWidget {
	const _SelectedCustomerPanel({required this.customer, required this.onClear});
	final Map<String, dynamic> customer;
	final VoidCallback onClear;
	@override Widget build(BuildContext context) {
		final code = customer['customerCode']?.toString() ?? '-';
		final name = customer['customerName']?.toString() ?? 'عميل';
		final phone = customer['phoneNumber']?.toString() ?? '-';
		return Card(child: ListTile(leading: const Icon(Icons.account_circle_outlined), title: Text(name), subtitle: Text('الكود: $code  |  الهاتف: $phone'), trailing: IconButton(tooltip: 'مسح الاختيار', onPressed: onClear, icon: const Icon(Icons.clear))));
	}
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
	@override Widget build(BuildContext context) => _Page(title: 'القوائم المالية', child: const _FinancialStatementsTabs());
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

class _CashFlowStatement extends StatelessWidget {
	const _CashFlowStatement({required this.cashFlow});
	final FinancialCashFlow cashFlow;
	@override Widget build(BuildContext context) => ListView(children: [
		if (!cashFlow.isAccountingComplete) Card(color: Theme.of(context).colorScheme.errorContainer, child: const ListTile(leading: Icon(Icons.warning_amber_outlined), title: Text('التدفق النقدي غير مكتمل محاسبياً'), subtitle: Text('يوجد فرق مثبت بين الحسابات النقدية ودفتر الأستاذ العام.'))),
		_FinancialValueList(title: 'التدفقات النقدية', values: [
			('تحصيلات العملاء', cashFlow.customerCollections),
			('عربون العملاء', cashFlow.customerAdvances),
			('المبالغ المستردة', cashFlow.refunds),
			('صافي حركة النقد', cashFlow.netCashMovement),
			('صافي مركز النقد', cashFlow.netCashPosition),
			('رصيد النقدية في الأستاذ العام', cashFlow.generalLedgerCashBalance),
			('فرق المطابقة النقدية', cashFlow.cashDifference),
		]),
	]);
}