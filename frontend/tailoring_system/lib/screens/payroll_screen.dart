import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../models/payroll_models.dart';
import '../repositories/payroll_repository.dart';
import 'payroll_details_screen.dart';

final _money = NumberFormat('#,##0.00');
final _date = DateFormat('yyyy/MM/dd');

String _text(String? value) => value == null || value.trim().isEmpty ? '-' : value;
String _status(String value) => switch (value.toLowerCase()) {
	'new' || 'draft' => 'جديد',
	'generated' || 'calculated' => 'تم التوليد',
	'underreview' || 'under_review' || 'review' => 'قيد المراجعة',
	'paid' => 'تم الدفع',
	'closed' => 'مغلق',
	_ => value,
};

class PayrollScreen extends StatefulWidget {
	const PayrollScreen({super.key});
	@override State<PayrollScreen> createState() => _PayrollScreenState();
}

class _PayrollScreenState extends State<PayrollScreen> {
	final repository = PayrollRepository();
	final search = TextEditingController();
	late Future<PayrollOverview> future;
	int? selectedPeriodId;
	String? statusFilter;

	@override void initState() {
		super.initState();
		future = load();
	}

	@override void dispose() {
		search.dispose();
		super.dispose();
	}

	bool _processing = false;

	Future<PayrollOverview> load() async {
		final data = await repository.getOverview();
		if (data.periods.isEmpty) {
			selectedPeriodId = null;
			return data;
		}
		if (!data.periods.any((period) => period.id == selectedPeriodId)) {
			selectedPeriodId = data.periods.firstWhere(
				(period) => data.records.any((record) => record.periodId == period.id),
				orElse: () => data.periods.first,
			).id;
		}
		return data;
	}

	void reload() => setState(() => future = load());

	Future<void> _generateCurrentPeriod(PayrollPeriod period) async {
		if (_processing) return;
		setState(() => _processing = true);
		try {
			final result = await repository.generatePayroll(startDate: period.startDate, endDate: period.endDate, periodCode: period.code, notes: 'تمت الإضافة من شاشة الرواتب');
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم توليد ${result.employeeCount} سجل راتب للفترة ${result.periodCode}')));
			reload();
		} catch (error) {
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تعذر توليد الرواتب: $error')));
		} finally {
			if (mounted) setState(() => _processing = false);
		}
	}

	Future<void> _approveCurrentPeriod(PayrollPeriod period) async {
		if (_processing) return;
		setState(() => _processing = true);
		try {
			final result = await repository.approvePayroll(period.id, approvedBy: 'FlutterApp', notes: 'تمت الموافقة من شاشة الرواتب');
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم اعتماد الفترة ${result.code}')));
			reload();
		} catch (error) {
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تعذر اعتماد الفترة: $error')));
		} finally {
			if (mounted) setState(() => _processing = false);
		}
	}

	Future<void> _payCurrentRecord(List<PayrollRecord> records) async {
		if (_processing || records.isEmpty) {
			if (records.isEmpty) {
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('لا توجد سجلات راتب يمكن دفعها في هذه الفترة.')));
			}
			return;
		}
		final record = records.first;
		setState(() => _processing = true);
		try {
			final result = await repository.payPayroll(record.id, paymentMethod: 'Cash', referenceNumber: 'PAY-${record.id}-${DateTime.now().millisecondsSinceEpoch}', notes: 'دفع من شاشة الرواتب');
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم دفع سجل الراتب رقم ${result.id} بنجاح')));
			reload();
		} catch (error) {
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تعذر دفع الراتب: $error')));
		} finally {
			if (mounted) setState(() => _processing = false);
		}
	}

	bool _insidePeriod(DateTime? value, PayrollPeriod period) => value != null && !value.isBefore(period.startDate) && value.isBefore(period.endDate.add(const Duration(days: 1)));

	@override Widget build(BuildContext context) => FutureBuilder<PayrollOverview>(
		future: future,
		builder: (context, snapshot) {
			if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
			if (snapshot.hasError) return _PayrollError(onRetry: reload);
			final data = snapshot.data!;
			if (data.periods.isEmpty) return _EmptyPayroll(onRefresh: reload);
			final period = data.periods.firstWhere((item) => item.id == selectedPeriodId, orElse: () => data.periods.first);
			final periodRecords = data.records.where((record) => record.periodId == period.id).toList();
			final employees = {for (final employee in data.employees) employee.id: employee};
			final departments = {for (final department in data.departments) department.id: department};
			final allRows = periodRecords.map((record) {
				final employee = employees[record.employeeId];
				final items = data.itemsByRecord[record.id] ?? const [];
				final allowances = items.where((item) => item.type.toLowerCase() == 'allowance').fold<double>(0, (sum, item) => sum + item.amount);
				final draws = (data.drawsByEmployee[record.employeeId] ?? const []).where((draw) => _insidePeriod(draw.drawDate, period)).fold<double>(0, (sum, draw) => sum + (draw.amount ?? 0));
				return _PayrollStatementRow(record: record, employee: employee, department: employee == null ? null : departments[employee.departmentId], allowances: allowances, periodDraws: draws);
			}).toList();
			final statuses = allRows.map((row) => row.record.status).toSet().toList()..sort();
			if (statusFilter != null && !statuses.contains(statusFilter)) statusFilter = null;
			final query = search.text.trim().toLowerCase();
			final rows = allRows.where((row) {
				final employee = row.employee;
				return (statusFilter == null || row.record.status == statusFilter) && (query.isEmpty || '${row.record.employeeId}'.contains(query) || (employee?.code.toLowerCase().contains(query) ?? false) || (employee?.name.toLowerCase().contains(query) ?? false) || (employee?.jobTitle?.toLowerCase().contains(query) ?? false) || (row.department?.name.toLowerCase().contains(query) ?? false));
			}).toList();
			return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
				Row(children: [
					Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('الرواتب', style: Theme.of(context).textTheme.headlineSmall), const SizedBox(height: 3), const Text('عرض تشغيلي للبيانات المخزنة في النظام الحالي دون إعادة احتساب الرواتب.')])),
					IconButton(tooltip: 'تحديث البيانات', onPressed: reload, icon: const Icon(Icons.refresh)),
				]),
				const SizedBox(height: 12),
				Row(children: [Text('فترات الرواتب', style: Theme.of(context).textTheme.titleMedium), const SizedBox(width: 10), Text('${data.periods.length} فترة', style: Theme.of(context).textTheme.bodySmall)]),
				const SizedBox(height: 8),
				SizedBox(
					height: 118,
					child: ListView.separated(
						scrollDirection: Axis.horizontal,
						itemCount: data.periods.length,
						separatorBuilder: (_, __) => const SizedBox(width: 10),
						itemBuilder: (context, index) {
							final item = data.periods[index];
							return _PeriodCard(period: item, selected: item.id == period.id, recordCount: data.records.where((record) => record.periodId == item.id).length, onSelect: () => setState(() {
								selectedPeriodId = item.id;
								statusFilter = null;
							}));
						},
					),
				),
				const SizedBox(height: 10),
				_PeriodSummary(period: period),
				const SizedBox(height: 10),
				_PayrollActions(
					period: period,
					record: periodRecords.isEmpty ? null : periodRecords.first,
					busy: _processing,
					onGenerate: () => _generateCurrentPeriod(period),
					onApprove: () => _approveCurrentPeriod(period),
					onPay: () => _payCurrentRecord(periodRecords),
				),
				const SizedBox(height: 12),
				Row(children: [Text('كشف الموظفين', style: Theme.of(context).textTheme.titleMedium), const SizedBox(width: 10), Text('${allRows.length} سجل', style: Theme.of(context).textTheme.bodySmall)]),
				const SizedBox(height: 8),
				Wrap(spacing: 10, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.center, children: [
					SizedBox(width: 300, child: TextField(controller: search, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.search), labelText: 'بحث بالرقم أو الاسم أو الوظيفة أو القسم', border: OutlineInputBorder(), isDense: true))),
					SizedBox(width: 210, child: DropdownButtonFormField<String>(initialValue: statusFilter, decoration: const InputDecoration(labelText: 'حالة الراتب', border: OutlineInputBorder(), isDense: true), items: [const DropdownMenuItem(value: null, child: Text('كل الحالات')), ...statuses.map((value) => DropdownMenuItem(value: value, child: Text(_status(value))))], onChanged: (value) => setState(() => statusFilter = value))),
					const _StatementNotice(),
				]),
				const SizedBox(height: 8),
				Expanded(child: _PayrollStatementTable(rows: rows, period: period)),
			]);
		},
	);
}

class _PeriodCard extends StatelessWidget {
	const _PeriodCard({required this.period, required this.selected, required this.recordCount, required this.onSelect});
	final PayrollPeriod period;
	final bool selected;
	final int recordCount;
	final VoidCallback onSelect;
	@override Widget build(BuildContext context) {
		final scheme = Theme.of(context).colorScheme;
		return SizedBox(width: 270, child: Card(
			color: selected ? scheme.primaryContainer : null,
			clipBehavior: Clip.antiAlias,
			child: InkWell(onTap: onSelect, child: Padding(padding: const EdgeInsets.all(12), child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
				Row(children: [Expanded(child: Text(period.code, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold))), _StatusChip(value: period.status)]),
				const SizedBox(height: 8),
				Text('${_date.format(period.startDate)} - ${_date.format(period.endDate)}'),
				const Spacer(),
				Text('$recordCount سجل راتب', style: Theme.of(context).textTheme.bodySmall),
			]))),
		));
	}
}

class _PeriodSummary extends StatelessWidget {
	const _PeriodSummary({required this.period});
	final PayrollPeriod period;
	@override Widget build(BuildContext context) => Container(
		padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
		decoration: BoxDecoration(color: Theme.of(context).colorScheme.surfaceContainerHighest, borderRadius: BorderRadius.circular(8)),
		child: Wrap(spacing: 26, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.center, children: [
			_Info('اسم الفترة', period.code),
			_Info('تاريخ البداية', _date.format(period.startDate)),
			_Info('تاريخ النهاية', _date.format(period.endDate)),
			Row(mainAxisSize: MainAxisSize.min, children: [const Text('حالة الفترة: '), _StatusChip(value: period.status)]),
			_Info('تاريخ التوليد', period.generatedAt == null ? '-' : _date.format(period.generatedAt!)),
			_Info('تاريخ الاعتماد', period.approvedAt == null ? '-' : _date.format(period.approvedAt!)),
		]),
	);
}

class _Info extends StatelessWidget {
	const _Info(this.label, this.value);
	final String label;
	final String value;
	@override Widget build(BuildContext context) => Text('$label: $value');
}

class _StatusChip extends StatelessWidget {
	const _StatusChip({required this.value});
	final String value;
	@override Widget build(BuildContext context) {
		final normalized = value.toLowerCase();
		final color = switch (normalized) {
			'paid' => Colors.green,
			'generated' || 'calculated' => Colors.blue,
			'underreview' || 'under_review' || 'review' => Colors.orange,
			'closed' => Colors.grey,
			_ => Theme.of(context).colorScheme.secondary,
		};
		return Chip(label: Text(_status(value)), side: BorderSide(color: color), visualDensity: VisualDensity.compact);
	}
}

class _PayrollActions extends StatelessWidget {
	const _PayrollActions({required this.period, required this.record, required this.busy, required this.onGenerate, required this.onApprove, required this.onPay});
	final PayrollPeriod period;
	final PayrollRecord? record;
	final bool busy;
	final Future<void> Function() onGenerate;
	final Future<void> Function() onApprove;
	final Future<void> Function() onPay;

	@override Widget build(BuildContext context) {
		final periodStatus = period.status.toLowerCase();
		final isApproved = periodStatus == 'approved';
		final isPaid = record != null && record!.status.toLowerCase() == 'paid';
		return Wrap(spacing: 8, runSpacing: 8, children: [
			_ActionButton(label: 'توليد الرواتب', icon: Icons.playlist_add_check, busy: busy, onPressed: onGenerate, enabled: !busy),
			_ActionButton(label: 'مراجعة', icon: Icons.fact_check_outlined, busy: busy, onPressed: onApprove, enabled: !busy && !isApproved),
			_ActionButton(label: 'دفع', icon: Icons.payments_outlined, busy: busy, onPressed: onPay, enabled: !busy && !isPaid && record != null),
		]);
	}
}

class _ActionButton extends StatelessWidget {
	const _ActionButton({required this.label, required this.icon, required this.busy, required this.onPressed, required this.enabled});
	final String label;
	final IconData icon;
	final bool busy;
	final Future<void> Function() onPressed;
	final bool enabled;

	@override Widget build(BuildContext context) => Tooltip(
		message: enabled ? 'تنفيذ العملية' : 'الإجراء غير متاح في الوقت الحالي',
		child: FilledButton.icon(
			onPressed: enabled ? () => onPressed() : null,
			icon: busy ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)) : Icon(icon),
			label: Text(label),
		),
	);
}

class _StatementNotice extends StatelessWidget {
	const _StatementNotice();
	@override Widget build(BuildContext context) => ConstrainedBox(
		constraints: const BoxConstraints(maxWidth: 500),
		child: const Row(mainAxisSize: MainAxisSize.min, children: [Icon(Icons.info_outline, size: 18), SizedBox(width: 6), Flexible(child: Text('الصافي وأجر القطعة من سجل الراتب. السلف للعرض حسب تاريخها ولا يعاد احتساب الصافي.'))]),
	);
}

class _PayrollStatementRow {
	const _PayrollStatementRow({required this.record, required this.employee, required this.department, required this.allowances, required this.periodDraws});
	final PayrollRecord record;
	final PayrollEmployee? employee;
	final PayrollDepartment? department;
	final double allowances;
	final double periodDraws;
}

class _PayrollStatementTable extends StatelessWidget {
	const _PayrollStatementTable({required this.rows, required this.period});
	final List<_PayrollStatementRow> rows;
	final PayrollPeriod period;
	@override Widget build(BuildContext context) {
		if (rows.isEmpty) return const Center(child: Text('لا توجد سجلات رواتب مطابقة لهذه الفترة.'));
		return Card(clipBehavior: Clip.antiAlias, child: Scrollbar(child: SingleChildScrollView(scrollDirection: Axis.horizontal, child: SingleChildScrollView(child: DataTable(
			showCheckboxColumn: false,
			columns: const [
				DataColumn(label: Text('رقم الموظف')),
				DataColumn(label: Text('الاسم')),
				DataColumn(label: Text('الوظيفة')),
				DataColumn(label: Text('القسم')),
				DataColumn(label: Text('الراتب الأساسي')),
				DataColumn(label: Text('البدلات')),
				DataColumn(label: Text('الخصومات')),
				DataColumn(label: Text('السلف')),
				DataColumn(label: Text('الإنتاج / القطع')),
				DataColumn(label: Text('صافي الراتب')),
				DataColumn(label: Text('طريقة الدفع')),
				DataColumn(label: Text('الحالة')),
			],
			rows: rows.map((row) {
				final employee = row.employee;
				return DataRow(
					onSelectChanged: employee == null ? null : (_) => AppNavigation.push(context, (_) => PayrollDetailsScreen(period: period, record: row.record, employee: employee, department: row.department)),
					cells: [
						DataCell(Text(employee?.code ?? '${row.record.employeeId}')),
						DataCell(Text(employee?.name ?? 'بيانات الموظف غير متاحة')),
						DataCell(Text(_text(employee?.jobTitle))),
						DataCell(Text(row.department?.name ?? '-')),
						DataCell(Text(_money.format(row.record.basicSalaryAmount))),
						DataCell(Text(_money.format(row.allowances))),
						DataCell(Text(_money.format(row.record.deductionsAmount))),
						DataCell(Text(_money.format(row.periodDraws))),
						DataCell(Text(_money.format(row.record.pieceWageAmount))),
						DataCell(Text(_money.format(row.record.netAmount), style: const TextStyle(fontWeight: FontWeight.bold))),
						const DataCell(Text('غير متاح')),
						DataCell(Text(_status(row.record.status))),
					],
				);
			}).toList(),
		)))));
	}
}

class _PayrollError extends StatelessWidget {
	const _PayrollError({required this.onRetry});
	final VoidCallback onRetry;
	@override Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
		const Icon(Icons.cloud_off_outlined, size: 42),
		const SizedBox(height: 10),
		const Text('تعذر تحميل بيانات الرواتب.'),
		const SizedBox(height: 10),
		FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة')),
	]));
}

class _EmptyPayroll extends StatelessWidget {
	const _EmptyPayroll({required this.onRefresh});
	final VoidCallback onRefresh;
	@override Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
		const Icon(Icons.event_busy_outlined, size: 42),
		const SizedBox(height: 10),
		const Text('لا توجد فترات رواتب مسجلة.'),
		const SizedBox(height: 10),
		OutlinedButton.icon(onPressed: onRefresh, icon: const Icon(Icons.refresh), label: const Text('تحديث')),
	]));
}