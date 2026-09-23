import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../models/payroll_models.dart';
import '../repositories/payroll_repository.dart';

final _payrollMoney = NumberFormat('#,##0.00');
final _payrollDate = DateFormat('yyyy/MM/dd');

String _payrollText(String? value) => value == null || value.trim().isEmpty ? '-' : value;
String _recordStatus(String value) => switch (value.toLowerCase()) {
	'new' || 'draft' => 'جديد',
	'generated' || 'calculated' => 'تم التوليد',
	'underreview' || 'under_review' || 'review' => 'قيد المراجعة',
	'paid' => 'تم الدفع',
	'closed' => 'مغلق',
	_ => value,
};

class PayrollDetailsScreen extends StatefulWidget {
	const PayrollDetailsScreen({required this.period, required this.record, required this.employee, required this.department, super.key});
	final PayrollPeriod period;
	final PayrollRecord record;
	final PayrollEmployee employee;
	final PayrollDepartment? department;

	@override State<PayrollDetailsScreen> createState() => _PayrollDetailsScreenState();
}

class _PayrollDetailsScreenState extends State<PayrollDetailsScreen> {
	final repository = PayrollRepository();
	late Future<EmployeePayrollDetailsData> future;
	bool _processing = false;

	@override void initState() {
		super.initState();
		future = repository.getPayrollDetails(widget.record.id, widget.employee.id);
	}

	void reload() => setState(() => future = repository.getPayrollDetails(widget.record.id, widget.employee.id));

	Future<void> _payCurrentRecord() async {
		if (_processing || widget.record.status.toLowerCase() == 'paid') return;
		setState(() => _processing = true);
		try {
			await repository.payPayroll(widget.record.id, paymentMethod: 'Cash', referenceNumber: 'PAY-${widget.record.id}-${DateTime.now().millisecondsSinceEpoch}', notes: 'دفع من شاشة تفاصيل الرواتب');
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم دفع الراتب الخاص بالموظف ${widget.employee.name} بنجاح')));
			reload();
		} catch (error) {
			if (!mounted) return;
			ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تعذر دفع الراتب: $error')));
		} finally {
			if (mounted) setState(() => _processing = false);
		}
	}

	bool _insidePeriod(DateTime? value) => value != null && !value.isBefore(widget.period.startDate) && value.isBefore(widget.period.endDate.add(const Duration(days: 1)));

	@override Widget build(BuildContext context) => Scaffold(
		appBar: AppBar(
			title: Text('تفاصيل راتب ${widget.employee.name}'),
			actions: [IconButton(tooltip: 'تحديث', onPressed: reload, icon: const Icon(Icons.refresh))],
		),
		body: SafeArea(child: FutureBuilder<EmployeePayrollDetailsData>(
			future: future,
			builder: (context, snapshot) {
				if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
				if (snapshot.hasError) return _PayrollDetailsError(onRetry: reload);
				final data = snapshot.data!;
				final allowances = data.items.where((item) => item.type.toLowerCase() == 'allowance').toList();
				final deductions = data.items.where((item) => item.type.toLowerCase() == 'deduction').toList();
				final periodDraws = data.draws.where((draw) => _insidePeriod(draw.drawDate)).toList();
				final attendance = data.attendance.where((item) => _insidePeriod(item.attendanceDate)).toList();
				final employeeWages = data.pieceWages.where((item) => item.employeeId == widget.employee.id || item.employeeCode == widget.employee.code).toList();
				final linkedWages = employeeWages.where((item) => item.payrollRecordId == widget.record.id || item.periodId == widget.period.id).toList();
				final allowanceAmount = allowances.fold<double>(0, (sum, item) => sum + item.amount);
				final periodDrawAmount = periodDraws.fold<double>(0, (sum, item) => sum + (item.amount ?? 0));
				return ListView(padding: const EdgeInsets.all(16), children: [
					_PayrollSection(
						title: 'بيانات الموظف والفترة',
						icon: Icons.badge_outlined,
						child: Wrap(spacing: 28, runSpacing: 12, children: [
							_InfoValue('رقم الموظف', widget.employee.code),
							_InfoValue('الاسم', widget.employee.name),
							_InfoValue('الوظيفة', _payrollText(widget.employee.jobTitle)),
							_InfoValue('القسم', widget.department?.name ?? '-'),
							_InfoValue('الفترة', widget.period.code),
							_InfoValue('حالة الراتب', _recordStatus(widget.record.status)),
						]),
					),
					const SizedBox(height: 12),
					Wrap(spacing: 10, runSpacing: 10, children: [
						_AmountTile('الراتب الأساسي', widget.record.basicSalaryAmount, Icons.payments_outlined),
						_AmountTile('البدلات المسجلة', allowanceAmount, Icons.add_circle_outline),
						_AmountTile('الخصومات', widget.record.deductionsAmount, Icons.remove_circle_outline),
						_AmountTile('سلف مؤرخة بالفترة', periodDrawAmount, Icons.account_balance_wallet_outlined),
						_AmountTile('أجر القطعة المخزن', widget.record.pieceWageAmount, Icons.precision_manufacturing_outlined),
						_AmountTile('تعديل الحضور', widget.record.attendanceAdjustmentAmount, Icons.event_available_outlined),
						_AmountTile('العمل الإضافي', widget.record.overtimeAmount, Icons.more_time_outlined),
						_AmountTile('صافي الراتب المخزن', widget.record.netAmount, Icons.price_check_outlined, emphasized: true),
					]),
					const SizedBox(height: 12),
					_PayrollSection(
						title: 'جميع بنود الراتب',
						icon: Icons.receipt_long_outlined,
						child: _PayrollDataTable(
							columns: const ['النوع', 'البند', 'الكمية', 'السعر', 'المبلغ', 'ملاحظات'],
							rows: data.items.map((item) => [item.type, item.name, _payrollMoney.format(item.quantity), _payrollMoney.format(item.rate), _payrollMoney.format(item.amount), _payrollText(item.notes)]).toList(),
							empty: 'لا توجد بنود راتب مسجلة.',
						),
					),
					const SizedBox(height: 12),
					_PayrollSection(
						title: 'البدلات والخصومات',
						icon: Icons.tune_outlined,
						child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
							Text(allowances.isEmpty ? 'لا توجد بنود بدلات مسجلة في PayrollItems لهذا الراتب.' : '${allowances.length} بنود بدلات مسجلة.'),
							const SizedBox(height: 6),
							Text(deductions.isEmpty ? 'لا توجد بنود خصومات مسجلة في PayrollItems لهذا الراتب.' : '${deductions.length} بنود خصومات مسجلة.'),
						]),
					),
					const SizedBox(height: 12),
					_PayrollSection(
						title: 'السلف',
						icon: Icons.account_balance_wallet_outlined,
						action: OutlinedButton.icon(
							onPressed: data.draws.isEmpty ? null : () => AppNavigation.push(context, (_) => EmployeeDrawsScreen(employee: widget.employee, draws: data.draws)),
							icon: const Icon(Icons.open_in_new),
							label: const Text('فتح شاشة السلف'),
						),
						child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
							_PayrollDataTable(
								columns: const ['رقم السلفة', 'التاريخ', 'المبلغ', 'ملاحظات'],
								rows: periodDraws.map((draw) => ['${draw.id}', draw.drawDate == null ? '-' : _payrollDate.format(draw.drawDate!), draw.amount == null ? '-' : _payrollMoney.format(draw.amount), _payrollText(draw.notes)]).toList(),
								empty: 'لا توجد سلف مؤرخة داخل هذه الفترة.',
							),
							const SizedBox(height: 8),
							const _UnavailableNotice('الرصيد المتبقي من السلف غير متاح لأن خدمة تسويات السلف غير مكشوفة للقراءة حالياً.'),
						]),
					),
					const SizedBox(height: 12),
					_PayrollSection(
						title: 'الحضور والغياب',
						icon: Icons.calendar_month_outlined,
						child: _PayrollDataTable(
							columns: const ['التاريخ', 'ساعات العمل', 'الإضافي', 'الحالة', 'سبب الغياب'],
							rows: attendance.map((item) => [_payrollDate.format(item.attendanceDate), _payrollMoney.format(item.workedHours), _payrollMoney.format(item.overtimeHours), item.isAbsent ? 'غياب' : 'حضور', _payrollText(item.absenceReason)]).toList(),
							empty: 'لا توجد بيانات حضور مسجلة داخل هذه الفترة.',
						),
					),
					const SizedBox(height: 12),
					_PayrollSection(
						title: 'الإنتاج وأجور القطعة',
						icon: Icons.precision_manufacturing_outlined,
						child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
							_PayrollDataTable(
								columns: const ['القطعة', 'النوع', 'المرحلة', 'الكمية', 'الأجر', 'الحالة'],
								rows: linkedWages.map((item) => ['${item.pieceId}', item.pieceType, item.stage, _payrollMoney.format(item.quantity), _payrollMoney.format(item.totalWage), item.status]).toList(),
								empty: 'لا توجد سجلات أجور قطعة مرتبطة بهذا الراتب أو الفترة.',
							),
							if (linkedWages.isEmpty && employeeWages.isNotEmpty) ...[
								const SizedBox(height: 8),
								Text('توجد ${employeeWages.length} سجلات أجور قطعة للموظف، لكنها غير مرتبطة بسجل الراتب أو الفترة الحالية.'),
							],
						]),
					),
					const SizedBox(height: 12),
					_PayrollSection(
						title: 'الدفع',
						icon: Icons.credit_card_outlined,
						child: Row(children: [
							Expanded(child: _UnavailableNotice(widget.record.status.toLowerCase() == 'paid' ? 'تم تسجيل الدفع لهذا الراتب سابقاً.' : 'يمكن دفع هذا الراتب مباشرة من الواجهة بعد التحقق من بيانات الموظف والفترة.')),
							const SizedBox(width: 12),
							FilledButton.icon(
								onPressed: widget.record.status.toLowerCase() == 'paid' || _processing ? null : _payCurrentRecord,
								icon: _processing ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.payments_outlined),
								label: Text(widget.record.status.toLowerCase() == 'paid' ? 'تم الدفع' : 'دفع'),
							),
						]),
					),
					const SizedBox(height: 12),
					Card(color: Theme.of(context).colorScheme.primaryContainer, child: Padding(padding: const EdgeInsets.all(18), child: Row(children: [
						const Icon(Icons.verified_outlined, size: 32),
						const SizedBox(width: 14),
						Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('صافي الراتب النهائي', style: Theme.of(context).textTheme.titleMedium), const Text('القيمة المخزنة في سجل الراتب؛ لم تُعد الشاشة احتسابها.') ])),
						Text(_payrollMoney.format(widget.record.netAmount), style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold)),
					]))),
				]);
			},
		)),
	);
}

class EmployeeDrawsScreen extends StatelessWidget {
	const EmployeeDrawsScreen({required this.employee, required this.draws, super.key});
	final PayrollEmployee employee;
	final List<EmployeeDraw> draws;
	@override Widget build(BuildContext context) => Scaffold(
		appBar: AppBar(title: Text('سلف ${employee.name}')),
		body: SafeArea(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			Text('${employee.code}  •  ${employee.name}', style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 12),
			const _UnavailableNotice('الرصيد المتبقي غير معروض لأن تسويات السلف لا تتوفر عبر خدمة القراءة الحالية.'),
			const SizedBox(height: 12),
			Expanded(child: _PayrollDataTable(
				columns: const ['رقم السلفة', 'التاريخ', 'المبلغ', 'ملاحظات'],
				rows: draws.map((draw) => ['${draw.id}', draw.drawDate == null ? '-' : _payrollDate.format(draw.drawDate!), draw.amount == null ? '-' : _payrollMoney.format(draw.amount), _payrollText(draw.notes)]).toList(),
				empty: 'لا توجد سلف مسجلة لهذا الموظف.',
			)),
		]))),
	);
}

class _PayrollSection extends StatelessWidget {
	const _PayrollSection({required this.title, required this.icon, required this.child, this.action});
	final String title;
	final IconData icon;
	final Widget child;
	final Widget? action;
	@override Widget build(BuildContext context) => Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
		Row(children: [Icon(icon), const SizedBox(width: 8), Expanded(child: Text(title, style: Theme.of(context).textTheme.titleMedium)), if (action != null) action!]),
		const SizedBox(height: 12),
		child,
	])));
}

class _InfoValue extends StatelessWidget {
	const _InfoValue(this.label, this.value);
	final String label;
	final String value;
	@override Widget build(BuildContext context) => SizedBox(width: 210, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label, style: Theme.of(context).textTheme.labelMedium), const SizedBox(height: 3), Text(value, style: Theme.of(context).textTheme.titleSmall)]));
}

class _AmountTile extends StatelessWidget {
	const _AmountTile(this.label, this.amount, this.icon, {this.emphasized = false});
	final String label;
	final double amount;
	final IconData icon;
	final bool emphasized;
	@override Widget build(BuildContext context) => Container(
		width: 220,
		height: 86,
		padding: const EdgeInsets.all(12),
		decoration: BoxDecoration(color: emphasized ? Theme.of(context).colorScheme.primaryContainer : Theme.of(context).colorScheme.surfaceContainerHighest, borderRadius: BorderRadius.circular(8)),
		child: Row(children: [Icon(icon), const SizedBox(width: 10), Expanded(child: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label, maxLines: 2, overflow: TextOverflow.ellipsis), Text(_payrollMoney.format(amount), style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold))]))]),
	);
}

class _PayrollDataTable extends StatelessWidget {
	const _PayrollDataTable({required this.columns, required this.rows, required this.empty});
	final List<String> columns;
	final List<List<String>> rows;
	final String empty;
	@override Widget build(BuildContext context) {
		if (rows.isEmpty) return Padding(padding: const EdgeInsets.symmetric(vertical: 12), child: Text(empty));
		return SingleChildScrollView(scrollDirection: Axis.horizontal, child: DataTable(
			columns: columns.map((column) => DataColumn(label: Text(column, style: const TextStyle(fontWeight: FontWeight.bold)))).toList(),
			rows: rows.map((row) => DataRow(cells: row.map((value) => DataCell(SelectableText(value))).toList())).toList(),
		));
	}
}

class _UnavailableNotice extends StatelessWidget {
	const _UnavailableNotice(this.message);
	final String message;
	@override Widget build(BuildContext context) => Row(crossAxisAlignment: CrossAxisAlignment.start, children: [Icon(Icons.info_outline, color: Theme.of(context).colorScheme.tertiary), const SizedBox(width: 8), Expanded(child: Text(message))]);
}

class _PayrollDetailsError extends StatelessWidget {
	const _PayrollDetailsError({required this.onRetry});
	final VoidCallback onRetry;
	@override Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
		const Icon(Icons.cloud_off_outlined, size: 42),
		const SizedBox(height: 10),
		const Text('تعذر تحميل تفاصيل الراتب.'),
		const SizedBox(height: 10),
		FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة')),
	]));
}