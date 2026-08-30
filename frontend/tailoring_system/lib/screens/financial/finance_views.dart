import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

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

class _FinancialScreenState extends State<FinancialScreen> {
	late final FinanceProvider provider;
	@override void initState() { super.initState(); provider = FinanceProvider()..loadOverview(); }
	@override void dispose() { provider.dispose(); super.dispose(); }
	void _open(Widget screen) => Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));

	@override Widget build(BuildContext context) => AnimatedBuilder(
		animation: provider,
		builder: (context, _) => Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			Row(children: [Expanded(child: Text('الإدارة المالية', style: Theme.of(context).textTheme.headlineSmall)), IconButton(tooltip: 'تحديث البيانات', onPressed: provider.state == FinanceLoadState.loading ? null : provider.loadOverview, icon: const Icon(Icons.refresh))]),
			const SizedBox(height: 12),
			if (provider.state == FinanceLoadState.loading) const LinearProgressIndicator(),
			if (provider.state == FinanceLoadState.error) _ErrorPanel(onRetry: provider.loadOverview),
			if (provider.state == FinanceLoadState.empty) const Padding(padding: EdgeInsets.all(24), child: Center(child: Text('لا توجد بيانات مالية متاحة.'))),
			const SizedBox(height: 12),
			Expanded(child: GridView.count(crossAxisCount: MediaQuery.sizeOf(context).width < 850 ? 2 : 3, childAspectRatio: 2.35, mainAxisSpacing: 12, crossAxisSpacing: 12, children: [
				_FinanceTile('الحركات المالية', 'سجل الحركات المسجلة', Icons.swap_horiz_outlined, () => _open(const FinancialTransactionsScreen())),
				_FinanceTile('دليل الحسابات', 'الحسابات وتصنيفاتها', Icons.account_tree_outlined, () => _open(const LedgerAccountsScreen())),
				_FinanceTile('القيود اليومية', 'القيود وتفاصيل التوازن', Icons.menu_book_outlined, () => _open(const JournalEntriesScreen())),
				_FinanceTile('الحسابات النقدية', 'الأرصدة النقدية الحالية', Icons.account_balance_wallet_outlined, () => _open(const CashAccountsScreen())),
				_FinanceTile('كشف حساب العميل', 'الحركات والأرصدة المسجلة', Icons.person_search_outlined, () => _open(const CustomerLedgerScreen())),
				_FinanceTile('كشف حساب المورد', 'حركات المورد دون إعادة احتساب', Icons.local_shipping_outlined, () => _open(const SupplierLedgerScreen())),
				_FinanceTile('فواتير الموردين', 'الفواتير وحالات السداد', Icons.receipt_long_outlined, () => _open(const SupplierInvoicesScreen())),
				_FinanceTile('دفعات الموردين', 'الدفعات ومراجعها', Icons.payments_outlined, () => _open(const SupplierPaymentsScreen())),
				_FinanceTile('المصروفات التشغيلية', 'الحركات المصنفة كمصروف تشغيلي', Icons.money_off_outlined, () => _open(const OperatingExpensesScreen())),
				_FinanceTile('القوائم المالية', 'حالة مصادر القوائم', Icons.assessment_outlined, () => _open(const FinancialStatementsScreen())),
				_FinanceTile('المطابقة والتدقيق', 'تغطية الحركات بالقيود', Icons.rule_outlined, () => _open(const FinancialReconciliationScreen())),
			]),),
		]),
	);
}

class _FinanceTile extends StatelessWidget {
	const _FinanceTile(this.title, this.subtitle, this.icon, this.onTap);
	final String title; final String subtitle; final IconData icon; final VoidCallback onTap;
	@override Widget build(BuildContext context) => Card(clipBehavior: Clip.antiAlias, child: InkWell(onTap: onTap, child: Padding(padding: const EdgeInsets.all(14), child: Row(children: [Icon(icon, size: 30), const SizedBox(width: 12), Expanded(child: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [Text(title, style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 3), Text(subtitle, maxLines: 2, overflow: TextOverflow.ellipsis)])), const Icon(Icons.chevron_left)]))));
}

class FinancialTransactionsScreen extends StatefulWidget {
	const FinancialTransactionsScreen({super.key});
	@override State<FinancialTransactionsScreen> createState() => _FinancialTransactionsScreenState();
}

class _FinancialTransactionsScreenState extends State<FinancialTransactionsScreen> {
	final repository = FinanceRepository(); final search = TextEditingController();
	late Future<List<FinancialTransaction>> future; String? type; DateTime? from; DateTime? to;
	@override void initState() { super.initState(); future = repository.getTransactions(); }
	@override void dispose() { search.dispose(); super.dispose(); }
	void reload() => setState(() => future = repository.getTransactions());
	@override Widget build(BuildContext context) => _Page(title: 'الحركات المالية', onRefresh: reload, child: FutureBuilder<List<FinancialTransaction>>(future: future, builder: (context, snapshot) {
		if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
		if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
		final all = snapshot.data!; final types = all.map((item) => item.transactionType).toSet().toList()..sort();
		final query = search.text.trim().toLowerCase(); final items = all.where((item) => (type == null || item.transactionType == type) && (from == null || !item.createdAt.isBefore(from!)) && (to == null || item.createdAt.isBefore(to!.add(const Duration(days: 1)))) && (query.isEmpty || item.referenceNumber.toLowerCase().contains(query) || item.transactionType.toLowerCase().contains(query) || (item.description?.toLowerCase().contains(query) ?? false))).toList();
		return Column(children: [Wrap(spacing: 10, runSpacing: 10, children: [SizedBox(width: 260, child: TextField(controller: search, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.search), labelText: 'بحث بالمرجع أو الوصف', border: OutlineInputBorder()))), SizedBox(width: 210, child: DropdownButtonFormField<String>(initialValue: type, decoration: const InputDecoration(labelText: 'نوع الحركة', border: OutlineInputBorder()), items: [const DropdownMenuItem(value: null, child: Text('كل الأنواع')), ...types.map((value) => DropdownMenuItem(value: value, child: Text(value)))], onChanged: (value) => setState(() => type = value))), _DateFilter(label: 'من تاريخ', value: from, onChanged: (value) => setState(() => from = value)), _DateFilter(label: 'إلى تاريخ', value: to, onChanged: (value) => setState(() => to = value))]), const SizedBox(height: 12), Expanded(child: _Table(columns: const ['رقم الحركة', 'المرجع', 'النوع', 'المبلغ', 'الوصف', 'التاريخ'], rows: items.map((item) => ['${item.id}', item.referenceNumber, item.transactionType, _money.format(item.amount), _text(item.description), _date.format(item.createdAt)]).toList(), empty: 'لا توجد حركات مطابقة.'))]);
	}));
}

class JournalEntriesScreen extends StatefulWidget {
	const JournalEntriesScreen({super.key});
	@override State<JournalEntriesScreen> createState() => _JournalEntriesScreenState();
}
class _JournalEntriesScreenState extends State<JournalEntriesScreen> {
	final repository = FinanceRepository(); late Future<List<JournalEntry>> future;
	@override void initState() { super.initState(); future = repository.getJournalEntries(); }
	void reload() => setState(() => future = repository.getJournalEntries());
	@override Widget build(BuildContext context) => _Page(
		title: 'القيود اليومية',
		onRefresh: reload,
		child: FutureBuilder<List<JournalEntry>>(
			future: future,
			builder: (context, snapshot) {
				if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
				if (snapshot.hasError) return _ErrorPanel(onRetry: reload);
				final items = snapshot.data!;
				return ListView.separated(
					itemCount: items.length,
					separatorBuilder: (_, __) => const SizedBox(height: 8),
					itemBuilder: (context, index) {
						final item = items[index];
						return Card(child: ListTile(
							leading: CircleAvatar(child: Text('${item.id}')),
							title: Text(item.referenceNumber),
							subtitle: Text('${_text(item.description)}  •  ${_date.format(item.entryDate)}'),
							trailing: const Icon(Icons.chevron_left),
							onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => JournalEntryDetailsScreen(entryId: item.id))),
						));
					},
				);
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
		final (entry, lines, accounts) = snapshot.data!; final names = {for (final account in accounts) account.id: account.accountName}; final debit = lines.fold<double>(0, (sum, line) => sum + line.debitAmount); final credit = lines.fold<double>(0, (sum, line) => sum + line.creditAmount); final balanced = (debit - credit).abs() < 0.005;
		return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [Card(child: Padding(padding: const EdgeInsets.all(16), child: Wrap(spacing: 28, runSpacing: 8, children: [Text('المرجع: ${entry.referenceNumber}'), Text('تاريخ القيد: ${_date.format(entry.entryDate)}'), Text('إجمالي المدين: ${_money.format(debit)}'), Text('إجمالي الدائن: ${_money.format(credit)}'), Chip(avatar: Icon(balanced ? Icons.check_circle : Icons.warning_amber, color: balanced ? Colors.green : Colors.orange), label: Text(balanced ? 'القيد متوازن' : 'القيد غير متوازن'))]))), const SizedBox(height: 10), Expanded(child: _Table(columns: const ['الحساب', 'المدين', 'الدائن', 'الوصف'], rows: lines.map((line) => [names[line.ledgerAccountId] ?? 'حساب ${line.ledgerAccountId}', _money.format(line.debitAmount), _money.format(line.creditAmount), _text(line.description)]).toList(), empty: 'لا توجد أسطر لهذا القيد.'))]);
	}));
}

class LedgerAccountsScreen extends StatelessWidget {
	const LedgerAccountsScreen({super.key});
	@override Widget build(BuildContext context) => _SimpleListPage<LedgerAccount>(title: 'دليل الحسابات', load: FinanceRepository().getLedgerAccounts, columns: const ['الكود', 'اسم الحساب', 'النوع', 'الحالة'], row: (item) => [item.accountCode, item.accountName, item.accountType, item.isActive ? 'نشط' : 'غير نشط']);
}
class CashAccountsScreen extends StatelessWidget {
	const CashAccountsScreen({super.key});
	@override Widget build(BuildContext context) => _SimpleListPage<CashAccount>(title: 'الحسابات النقدية', load: FinanceRepository().getCashAccounts, columns: const ['رقم الحساب', 'اسم الحساب', 'الرصيد الحالي', 'الحالة', 'تاريخ الإنشاء'], row: (item) => ['${item.id}', item.accountName, _money.format(item.currentBalance), item.isActive ? 'نشط' : 'غير نشط', _date.format(item.createdAt)]);
}

class CustomerLedgerScreen extends StatelessWidget {
	const CustomerLedgerScreen({super.key});
	@override Widget build(BuildContext context) => _LedgerLookup<CustomerLedgerEntry>(title: 'كشف حساب العميل', partyLabel: 'رقم العميل', load: FinanceRepository().getCustomerLedger, row: (item) => [item.referenceNumber, item.debitAmount > 0 ? 'مدين' : 'دائن', _money.format(item.debitAmount), _money.format(item.creditAmount), _money.format(item.balanceAfterTransaction), _date.format(item.createdAt)]);
}
class SupplierLedgerScreen extends StatelessWidget {
	const SupplierLedgerScreen({super.key});
	@override Widget build(BuildContext context) => _LedgerLookup<SupplierLedgerEntry>(title: 'كشف حساب المورد', partyLabel: 'رقم المورد', load: FinanceRepository().getSupplierLedger, row: (item) => [item.referenceNumber, item.debitAmount > 0 ? 'مدين' : 'دائن', _money.format(item.debitAmount), _money.format(item.creditAmount), _money.format(item.balanceAfterTransaction), _date.format(item.createdAt)]);
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
	const _LedgerLookup({required this.title, required this.partyLabel, required this.load, required this.row});
	final String title; final String partyLabel; final Future<List<T>> Function(int) load; final List<String> Function(T) row;
	@override State<_LedgerLookup<T>> createState() => _LedgerLookupState<T>();
}
class _LedgerLookupState<T> extends State<_LedgerLookup<T>> {
	final controller = TextEditingController(); final filter = TextEditingController(); Future<List<T>>? future;
	@override void dispose() { controller.dispose(); filter.dispose(); super.dispose(); }
	void search() { final id = int.tryParse(controller.text.trim()); if (id != null && id > 0) setState(() => future = widget.load(id)); }
	@override Widget build(BuildContext context) => _Page(title: widget.title, child: Column(children: [Wrap(spacing: 10, runSpacing: 10, children: [SizedBox(width: 240, child: TextField(controller: controller, keyboardType: TextInputType.number, onSubmitted: (_) => search(), decoration: InputDecoration(labelText: widget.partyLabel, border: const OutlineInputBorder()))), FilledButton.icon(onPressed: search, icon: const Icon(Icons.search), label: const Text('عرض الكشف')), SizedBox(width: 260, child: TextField(controller: filter, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.filter_alt_outlined), labelText: 'تصفية بالمرجع أو القيمة', border: OutlineInputBorder()))) ]), const SizedBox(height: 12), Expanded(child: future == null ? const Center(child: Text('أدخل الرقم لعرض كشف الحساب.')) : FutureBuilder<List<T>>(future: future, builder: (context, snapshot) { if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator()); if (snapshot.hasError) return _ErrorPanel(onRetry: search); final query = filter.text.trim().toLowerCase(); final rows = snapshot.data!.map(widget.row).where((row) => query.isEmpty || row.any((value) => value.toLowerCase().contains(query))).toList(); return _Table(columns: const ['المرجع', 'نوع الحركة', 'المدين', 'الدائن', 'الرصيد بعد الحركة', 'التاريخ'], rows: rows, empty: 'لا توجد حركات مطابقة لهذا الحساب.'); }))]));
}

class _SimpleListPage<T> extends StatefulWidget {
	const _SimpleListPage({required this.title, required this.load, required this.columns, required this.row});
	final String title; final Future<List<T>> Function() load; final List<String> columns; final List<String> Function(T) row;
	@override State<_SimpleListPage<T>> createState() => _SimpleListPageState<T>();
}
class _SimpleListPageState<T> extends State<_SimpleListPage<T>> {
	late Future<List<T>> future;
	@override void initState() { super.initState(); future = widget.load(); }
	void reload() => setState(() => future = widget.load());
	@override Widget build(BuildContext context) => _Page(title: widget.title, onRefresh: reload, child: FutureBuilder<List<T>>(future: future, builder: (context, snapshot) { if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator()); if (snapshot.hasError) return _ErrorPanel(onRetry: reload); return _Table(columns: widget.columns, rows: snapshot.data!.map(widget.row).toList(), empty: 'لا توجد بيانات متاحة.'); }));
}

class _Page extends StatelessWidget {
	const _Page({required this.title, required this.child, this.onRefresh}); final String title; final Widget child; final VoidCallback? onRefresh;
	@override Widget build(BuildContext context) => Scaffold(appBar: Navigator.of(context).canPop() ? AppBar(title: Text(title), actions: [if (onRefresh != null) IconButton(tooltip: 'تحديث', onPressed: onRefresh, icon: const Icon(Icons.refresh))]) : null, body: SafeArea(child: Padding(padding: const EdgeInsets.all(16), child: child)));
}
class _Table extends StatelessWidget {
	const _Table({required this.columns, required this.rows, required this.empty}); final List<String> columns; final List<List<String>> rows; final String empty;
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
							rows: rows.map((row) => DataRow(cells: row.map((value) => DataCell(SelectableText(value))).toList())).toList(),
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