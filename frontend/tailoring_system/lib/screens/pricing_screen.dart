import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

class PricingScreen extends StatefulWidget {
	const PricingScreen({super.key});

	@override
	State<PricingScreen> createState() => _PricingScreenState();
}

class _PricingScreenState extends State<PricingScreen> {
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');
	final search = TextEditingController();
	late Future<List<_PieceCostSetting>> settings;

	@override
	void initState() {
		super.initState();
		settings = _load();
	}

	@override
	void dispose() {
		search.dispose();
		super.dispose();
	}

	Future<List<_PieceCostSetting>> _load() async {
		final response = await http.get(Uri.parse('$_baseUrl/piece-cost-settings'));
		if (response.statusCode < 200 || response.statusCode >= 300) throw Exception('HTTP ${response.statusCode}');
		return (jsonDecode(response.body) as List).cast<Map<String, dynamic>>().map(_PieceCostSetting.fromJson).toList();
	}

	Future<void> _refresh() async {
		setState(() => settings = _load());
		await settings;
	}

	Future<_PieceCostSetting> _save(_PieceCostSetting item, Map<String, dynamic> values) async {
		final response = await http.put(
			Uri.parse('$_baseUrl/piece-cost-settings/${item.productTypeId}'),
			headers: const {'Content-Type': 'application/json'},
			body: jsonEncode(values),
		);
		if (response.statusCode < 200 || response.statusCode >= 300) throw Exception('HTTP ${response.statusCode}');
		return _PieceCostSetting.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
	}

	Future<void> _edit(_PieceCostSetting item) async {
		final saved = await showDialog<_PieceCostSetting>(
			context: context,
			barrierDismissible: false,
			builder: (_) => _PieceCostDialog(item: item, onSave: (values) => _save(item, values)),
		);

		if (saved == null || !mounted) return;
		setState(() => settings = settings.then((items) => items.map((current) => current.productTypeId == saved.productTypeId ? saved : current).toList()));
		ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم حفظ تكاليف ${saved.pieceName}.')));
	}

	@override
	Widget build(BuildContext context) => FutureBuilder<List<_PieceCostSetting>>(
		future: settings,
		builder: (context, snapshot) {
			if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
			if (snapshot.hasError) return _LoadError(onRetry: _refresh);
			final allItems = snapshot.data!;
			final query = search.text.trim().toLowerCase();
			final items = allItems.where((item) => query.isEmpty || item.searchText.contains(query)).toList();
			final configuredCount = allItems.where((item) => item.isConfigured).length;
			return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
				Row(children: [
					Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
						Text('إعدادات تكاليف القطع', style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold)),
						const SizedBox(height: 4),
						Text('المصدر الرسمي للتكاليف التشغيلية المستخدمة في التسعير التلقائي.', style: Theme.of(context).textTheme.bodyMedium),
					])),
					IconButton(tooltip: 'تحديث البيانات', onPressed: _refresh, icon: const Icon(Icons.refresh)),
				]),
				const SizedBox(height: 18),
				Wrap(spacing: 12, runSpacing: 12, children: [
					_Summary(width: 210, icon: Icons.checkroom_outlined, label: 'أنواع القطع', value: '${allItems.length}'),
					_Summary(width: 210, icon: Icons.tune_outlined, label: 'تم إعداد تكلفتها', value: '$configuredCount'),
					_Summary(width: 210, icon: Icons.pending_actions_outlined, label: 'بانتظار الإعداد', value: '${allItems.length - configuredCount}'),
				]),
				const SizedBox(height: 16),
				TextField(
					controller: search,
					onChanged: (_) => setState(() {}),
					decoration: const InputDecoration(labelText: 'بحث باسم القطعة أو الكود', prefixIcon: Icon(Icons.search), border: OutlineInputBorder(), isDense: true),
				),
				const SizedBox(height: 12),
				Expanded(child: items.isEmpty ? const Center(child: Text('لا توجد أنواع قطع مطابقة.')) : LayoutBuilder(builder: (context, constraints) {
					if (constraints.maxWidth < 950) return ListView.separated(itemCount: items.length, separatorBuilder: (_, __) => const SizedBox(height: 8), itemBuilder: (context, index) => _PieceCostTile(item: items[index], onEdit: () => _edit(items[index])));
					return _PieceCostTable(items: items, onEdit: _edit);
				})),
			]);
		},
	);
}

class _PieceCostDialog extends StatefulWidget {
	const _PieceCostDialog({required this.item, required this.onSave});
	final _PieceCostSetting item;
	final Future<_PieceCostSetting> Function(Map<String, dynamic> values) onSave;

	@override
	State<_PieceCostDialog> createState() => _PieceCostDialogState();
}

class _PieceCostDialogState extends State<_PieceCostDialog> {
	final formKey = GlobalKey<FormState>();
	late final TextEditingController sewing;
	late final TextEditingController consumables;
	late final TextEditingController ironing;
	late final TextEditingController fixed;
	late final TextEditingController notes;
	bool saving = false;
	String? error;

	@override
	void initState() {
		super.initState();
		sewing = TextEditingController(text: _editableNumber(widget.item.sewingCost));
		consumables = TextEditingController(text: _editableNumber(widget.item.consumablesCost));
		ironing = TextEditingController(text: _editableNumber(widget.item.ironingAndPackagingCost));
		fixed = TextEditingController(text: _editableNumber(widget.item.fixedOperatingCost));
		notes = TextEditingController(text: widget.item.notes ?? '');
	}

	@override
	void dispose() {
		sewing.dispose();
		consumables.dispose();
		ironing.dispose();
		fixed.dispose();
		notes.dispose();
		super.dispose();
	}

	double _value(TextEditingController controller) => double.tryParse(controller.text.trim()) ?? 0;
	double get total => _value(sewing) + _value(consumables) + _value(ironing) + _value(fixed);

	Future<void> _submit() async {
		if (!(formKey.currentState?.validate() ?? false)) return;
		setState(() { saving = true; error = null; });
		try {
			final result = await widget.onSave({
				'sewingCost': _value(sewing),
				'consumablesCost': _value(consumables),
				'ironingAndPackagingCost': _value(ironing),
				'fixedOperatingCost': _value(fixed),
				'notes': notes.text.trim().isEmpty ? null : notes.text.trim(),
			});
			if (mounted) Navigator.pop(context, result);
		} catch (_) {
			if (mounted) setState(() { saving = false; error = 'تعذر حفظ إعدادات التكلفة.'; });
		}
	}

	@override
	Widget build(BuildContext context) => AlertDialog(
		title: Text('${widget.item.isConfigured ? 'تعديل' : 'إضافة'} تكاليف ${widget.item.pieceName}'),
		content: SizedBox(
			width: 580,
			child: Form(
				key: formKey,
				child: SingleChildScrollView(
					child: Column(mainAxisSize: MainAxisSize.min, children: [
						Row(children: [
							Expanded(child: _CostField(controller: sewing, label: 'تكلفة الخياطة', onChanged: () => setState(() {}), onSubmitted: _submit)),
							const SizedBox(width: 12),
							Expanded(child: _CostField(controller: consumables, label: 'الأدوات والمستهلكات', onChanged: () => setState(() {}), onSubmitted: _submit)),
						]),
						const SizedBox(height: 12),
						Row(children: [
							Expanded(child: _CostField(controller: ironing, label: 'الكي والتغليف', onChanged: () => setState(() {}), onSubmitted: _submit)),
							const SizedBox(width: 12),
							Expanded(child: _CostField(controller: fixed, label: 'التشغيل الثابت', onChanged: () => setState(() {}), onSubmitted: _submit)),
						]),
						const SizedBox(height: 14),
						Container(
							width: double.infinity,
							padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
							decoration: BoxDecoration(color: Theme.of(context).colorScheme.secondaryContainer, borderRadius: BorderRadius.circular(6)),
							child: Row(children: [
								const Expanded(child: Text('إجمالي التكاليف التشغيلية', style: TextStyle(fontWeight: FontWeight.bold))),
								Text(_money(total), style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)),
							]),
						),
						const SizedBox(height: 14),
						TextFormField(controller: notes, maxLength: 500, maxLines: 3, decoration: const InputDecoration(labelText: 'ملاحظات اختيارية', border: OutlineInputBorder(), alignLabelWithHint: true)),
						if (error != null) Align(alignment: Alignment.centerRight, child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
					]),
				),
			),
		),
		actions: [
			TextButton(onPressed: saving ? null : () => Navigator.pop(context), child: const Text('إلغاء')),
			FilledButton.icon(
				onPressed: saving ? null : _submit,
				icon: saving ? const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.save_outlined),
				label: const Text('حفظ'),
			),
		],
	);
}

class _CostField extends StatelessWidget {
	const _CostField({required this.controller, required this.label, required this.onChanged, required this.onSubmitted});
	final TextEditingController controller;
	final String label;
	final VoidCallback onChanged;
	final Future<void> Function() onSubmitted;

	@override
	Widget build(BuildContext context) => TextFormField(
		controller: controller,
		keyboardType: const TextInputType.numberWithOptions(decimal: true),
		textInputAction: TextInputAction.next,
		onChanged: (_) => onChanged(),
		onFieldSubmitted: (_) => onSubmitted(),
		decoration: InputDecoration(labelText: label, border: const OutlineInputBorder(), prefixText: 'ر.س '),
		validator: (value) {
			final number = double.tryParse(value?.trim() ?? '');
			if (number == null) return 'أدخل قيمة رقمية';
			if (number < 0) return 'لا يمكن أن تكون سالبة';
			return null;
		},
	);
}

class _PieceCostTable extends StatelessWidget {
	const _PieceCostTable({required this.items, required this.onEdit});
	final List<_PieceCostSetting> items;
	final ValueChanged<_PieceCostSetting> onEdit;

	@override
	Widget build(BuildContext context) => SingleChildScrollView(
		child: SizedBox(
			width: double.infinity,
			child: DataTable(
				showBottomBorder: true,
				columns: const [
					DataColumn(label: Text('نوع القطعة')),
					DataColumn(label: Text('الخياطة'), numeric: true),
					DataColumn(label: Text('الأدوات'), numeric: true),
					DataColumn(label: Text('الكي والتغليف'), numeric: true),
					DataColumn(label: Text('التشغيل الثابت'), numeric: true),
					DataColumn(label: Text('الإجمالي'), numeric: true),
					DataColumn(label: Text('الإجراء')),
				],
				rows: items.map((item) => DataRow(cells: [
					DataCell(Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [Text(item.pieceName, style: const TextStyle(fontWeight: FontWeight.bold)), Text(item.pieceCode, style: Theme.of(context).textTheme.bodySmall)])),
					DataCell(Text(_money(item.sewingCost))),
					DataCell(Text(_money(item.consumablesCost))),
					DataCell(Text(_money(item.ironingAndPackagingCost))),
					DataCell(Text(_money(item.fixedOperatingCost))),
					DataCell(Text(_money(item.totalOperationalCost), style: const TextStyle(fontWeight: FontWeight.bold))),
					DataCell(item.isConfigured
						? IconButton(tooltip: 'تعديل التكاليف', onPressed: () => onEdit(item), icon: const Icon(Icons.edit_outlined))
						: FilledButton.icon(onPressed: () => onEdit(item), icon: const Icon(Icons.add, size: 18), label: const Text('إضافة'))),
				])).toList(),
			),
		),
	);
}

class _PieceCostTile extends StatelessWidget {
	const _PieceCostTile({required this.item, required this.onEdit});
	final _PieceCostSetting item;
	final VoidCallback onEdit;

	@override
	Widget build(BuildContext context) => Container(
		padding: const EdgeInsets.all(12),
		decoration: BoxDecoration(border: Border.all(color: Theme.of(context).dividerColor), borderRadius: BorderRadius.circular(8)),
		child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			Row(children: [
				Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(item.pieceName, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)), Text(item.pieceCode)])),
				IconButton(tooltip: item.isConfigured ? 'تعديل التكاليف' : 'إضافة التكاليف', onPressed: onEdit, icon: Icon(item.isConfigured ? Icons.edit_outlined : Icons.add_circle_outline)),
			]),
			const Divider(),
			Wrap(spacing: 18, runSpacing: 8, children: [
				_Value(label: 'الخياطة', value: item.sewingCost),
				_Value(label: 'الأدوات', value: item.consumablesCost),
				_Value(label: 'الكي والتغليف', value: item.ironingAndPackagingCost),
				_Value(label: 'التشغيل الثابت', value: item.fixedOperatingCost),
				_Value(label: 'الإجمالي', value: item.totalOperationalCost, emphasized: true),
			]),
		]),
	);
}

class _Value extends StatelessWidget {
	const _Value({required this.label, required this.value, this.emphasized = false});
	final String label;
	final double value;
	final bool emphasized;
	@override
	Widget build(BuildContext context) => SizedBox(width: 120, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label, style: Theme.of(context).textTheme.bodySmall), Text(_money(value), style: TextStyle(fontWeight: emphasized ? FontWeight.bold : FontWeight.normal))]));
}

class _Summary extends StatelessWidget {
	const _Summary({required this.width, required this.icon, required this.label, required this.value});
	final double width;
	final IconData icon;
	final String label;
	final String value;
	@override
	Widget build(BuildContext context) => SizedBox(width: width, child: Container(
		padding: const EdgeInsets.all(14),
		decoration: BoxDecoration(color: Theme.of(context).colorScheme.surfaceContainerHighest, borderRadius: BorderRadius.circular(8)),
		child: Row(children: [Icon(icon), const SizedBox(width: 12), Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label, style: Theme.of(context).textTheme.bodySmall), Text(value, style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold))]))]),
	));
}

class _LoadError extends StatelessWidget {
	const _LoadError({required this.onRetry});
	final Future<void> Function() onRetry;
	@override
	Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
		const Icon(Icons.cloud_off_outlined, size: 44),
		const SizedBox(height: 10),
		const Text('تعذر تحميل إعدادات تكاليف القطع.'),
		const SizedBox(height: 10),
		FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة')),
	]));
}

class _PieceCostSetting {
	const _PieceCostSetting({required this.productTypeId, required this.pieceCode, required this.pieceName, required this.sewingCost, required this.consumablesCost, required this.ironingAndPackagingCost, required this.fixedOperatingCost, required this.notes, required this.totalOperationalCost, required this.isConfigured});
	factory _PieceCostSetting.fromJson(Map<String, dynamic> json) => _PieceCostSetting(
		productTypeId: json['productTypeId'] as int,
		pieceCode: json['pieceCode']?.toString() ?? '-',
		pieceName: json['pieceName']?.toString() ?? '-',
		sewingCost: (json['sewingCost'] as num?)?.toDouble() ?? 0,
		consumablesCost: (json['consumablesCost'] as num?)?.toDouble() ?? 0,
		ironingAndPackagingCost: (json['ironingAndPackagingCost'] as num?)?.toDouble() ?? 0,
		fixedOperatingCost: (json['fixedOperatingCost'] as num?)?.toDouble() ?? 0,
		notes: json['notes']?.toString(),
		totalOperationalCost: (json['totalOperationalCost'] as num?)?.toDouble() ?? 0,
		isConfigured: json['isConfigured'] == true,
	);
	final int productTypeId;
	final String pieceCode;
	final String pieceName;
	final double sewingCost;
	final double consumablesCost;
	final double ironingAndPackagingCost;
	final double fixedOperatingCost;
	final String? notes;
	final double totalOperationalCost;
	final bool isConfigured;
	String get searchText => '$pieceCode $pieceName'.toLowerCase();
}

final _numberFormat = NumberFormat('#,##0.##');
String _money(double value) => '${_numberFormat.format(value)} ر.س';
String _editableNumber(double value) => value == value.truncateToDouble() ? value.toInt().toString() : value.toString();