import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

class InventoryScreen extends StatefulWidget {
	const InventoryScreen({super.key});
	@override
	State<InventoryScreen> createState() => _InventoryScreenState();
}

class _InventoryScreenState extends State<InventoryScreen> with SingleTickerProviderStateMixin {
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5092');
	late final TabController _tabs;
	late Future<InventoryData> _data;

	@override
	void initState() {
		super.initState();
		_tabs = TabController(length: 3, vsync: this);
		_data = _load();
	}

	@override
	void dispose() {
		_tabs.dispose();
		super.dispose();
	}

	Future<InventoryData> _load() async {
		final responses = await Future.wait([
			http.get(Uri.parse('$_baseUrl/inventory/items')),
			http.get(Uri.parse('$_baseUrl/inventory/readymade')),
			http.get(Uri.parse('$_baseUrl/inventory/imported')),
		]);
		if (responses.any((response) => response.statusCode < 200 || response.statusCode >= 300)) throw Exception();
		List<Map<String, dynamic>> decode(http.Response response) => (jsonDecode(response.body) as List).cast<Map<String, dynamic>>();
		return InventoryData(
			fabrics: decode(responses[0]).map(FabricItem.fromJson).toList(),
			readyMade: decode(responses[1]).map(ReadyMadeItem.fromJson).toList(),
			imported: decode(responses[2]).map(ImportedItem.fromJson).toList(),
		);
	}

	Future<void> _refresh() async {
		setState(() => _data = _load());
		await _data;
	}

	@override
	Widget build(BuildContext context) => FutureBuilder<InventoryData>(
		future: _data,
		builder: (context, snapshot) {
			if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
			if (snapshot.hasError) return _LoadError(onRetry: _refresh);
			final data = snapshot.data!;
			return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
				Row(children: [
					Expanded(child: Text('نظرة عامة على المخزون', style: Theme.of(context).textTheme.headlineSmall)),
					IconButton(tooltip: 'تحديث البيانات', onPressed: _refresh, icon: const Icon(Icons.refresh)),
				]),
				const SizedBox(height: 14),
				_Statistics(data: data),
				const SizedBox(height: 16),
				TabBar(controller: _tabs, isScrollable: true, tabs: const [
					Tab(icon: Icon(Icons.texture_outlined), text: 'مخزن الأقمشة'),
					Tab(icon: Icon(Icons.checkroom_outlined), text: 'منتجاتنا الجاهزة'),
					Tab(icon: Icon(Icons.public_outlined), text: 'المنتجات المستوردة'),
				]),
				const SizedBox(height: 10),
				Expanded(child: TabBarView(controller: _tabs, children: [
					InventoryTable(
						columns: const ['الكود', 'اسم الصنف', 'الفئة', 'الرصيد الحالي', 'المتاح', 'المحجوز'],
						rows: data.fabrics.map((item) => [item.code, item.name, item.category, quantity(item.current), quantity(item.available), quantity(item.reserved)]).toList(),
						emptyMessage: 'لا توجد أصناف أقمشة.', onRefresh: _refresh,
					),
					InventoryTable(
						columns: const ['المنتج', 'الحالة', 'التكلفة', 'سعر البيع المقترح'],
						rows: data.readyMade.map((item) => [item.product, statusLabel(item.status), money(item.cost), money(item.price)]).toList(),
						emptyMessage: 'لا توجد منتجات جاهزة من إنتاجنا.', onRefresh: _refresh,
					),
					InventoryTable(
						columns: const ['المنتج', 'الكمية', 'سعر الشراء', 'سعر البيع'],
						rows: data.imported.map((item) => [item.product, '${quantity(item.quantity)} ${item.unit}', money(item.purchasePrice), money(item.sellingPrice)]).toList(),
						emptyMessage: 'لا توجد منتجات مستوردة.', onRefresh: _refresh,
					),
				])),
			]);
		},
	);
}

class _Statistics extends StatelessWidget {
	const _Statistics({required this.data});
	final InventoryData data;
	@override
	Widget build(BuildContext context) => LayoutBuilder(builder: (context, constraints) {
		final width = constraints.maxWidth < 720 ? constraints.maxWidth : (constraints.maxWidth - 24) / 3;
		return Wrap(spacing: 12, runSpacing: 12, children: [
			StatCard(width: width, icon: Icons.texture_outlined, title: 'الأقمشة', value: '${data.fabrics.length} صنف', detail: 'إجمالي الرصيد ${quantity(data.fabricBalance)}'),
			StatCard(width: width, icon: Icons.checkroom_outlined, title: 'منتجاتنا', value: '${data.readyMade.length} منتج', detail: '${data.readyMade.where((item) => item.status == 'AvailableForSale').length} متاح للبيع'),
			StatCard(width: width, icon: Icons.public_outlined, title: 'المستورد', value: '${data.imported.length} منتج', detail: 'إجمالي الكمية ${quantity(data.importedQuantity)}'),
		]);
	});
}

class StatCard extends StatelessWidget {
	const StatCard({required this.width, required this.icon, required this.title, required this.value, required this.detail, super.key});
	final double width;
	final IconData icon;
	final String title;
	final String value;
	final String detail;
	@override
	Widget build(BuildContext context) => SizedBox(width: width, height: 96, child: Card(child: Padding(
		padding: const EdgeInsets.all(14),
		child: Row(children: [
			CircleAvatar(backgroundColor: Theme.of(context).colorScheme.secondaryContainer, child: Icon(icon)),
			const SizedBox(width: 12),
			Expanded(child: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [
				Text(title, style: Theme.of(context).textTheme.labelLarge),
				Text(value, style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)),
				Text(detail, style: Theme.of(context).textTheme.bodySmall),
			])),
		]),
	)));
}

class InventoryTable extends StatelessWidget {
	const InventoryTable({required this.columns, required this.rows, required this.emptyMessage, required this.onRefresh, super.key});
	final List<String> columns;
	final List<List<String>> rows;
	final String emptyMessage;
	final Future<void> Function() onRefresh;
	@override
	Widget build(BuildContext context) => RefreshIndicator(
		onRefresh: onRefresh,
		child: rows.isEmpty
				? ListView(children: [const SizedBox(height: 150), Center(child: Text(emptyMessage))])
				: ListView(children: [Card(clipBehavior: Clip.antiAlias, child: SingleChildScrollView(
					scrollDirection: Axis.horizontal,
					child: DataTable(
						columnSpacing: 36,
						columns: columns.map((column) => DataColumn(label: Text(column, style: const TextStyle(fontWeight: FontWeight.bold)))).toList(),
						rows: rows.map((row) => DataRow(cells: row.map((value) => DataCell(Text(value))).toList())).toList(),
					),
				))]),
	);
}

class InventoryData {
	const InventoryData({required this.fabrics, required this.readyMade, required this.imported});
	final List<FabricItem> fabrics;
	final List<ReadyMadeItem> readyMade;
	final List<ImportedItem> imported;
	double get fabricBalance => fabrics.fold(0, (sum, item) => sum + item.current);
	double get importedQuantity => imported.fold(0, (sum, item) => sum + item.quantity);
}

class FabricItem {
	const FabricItem(this.code, this.name, this.category, this.current, this.available, this.reserved);
	factory FabricItem.fromJson(Map<String, dynamic> json) => FabricItem(
		json['itemCode']?.toString() ?? '-', json['itemName']?.toString() ?? '-', json['category']?.toString() ?? '-',
		(json['currentQuantity'] as num?)?.toDouble() ?? 0, (json['availableQuantity'] as num?)?.toDouble() ?? 0, (json['reservedQuantity'] as num?)?.toDouble() ?? 0,
	);
	final String code;
	final String name;
	final String category;
	final double current;
	final double available;
	final double reserved;
}

class ReadyMadeItem {
	const ReadyMadeItem(this.product, this.status, this.cost, this.price);
	factory ReadyMadeItem.fromJson(Map<String, dynamic> json) => ReadyMadeItem(
		json['productionName']?.toString() ?? json['pieceType']?.toString() ?? '-', json['status']?.toString() ?? '-',
		(json['actualCost'] as num?)?.toDouble(), (json['suggestedSellingPrice'] as num?)?.toDouble(),
	);
	final String product;
	final String status;
	final double? cost;
	final double? price;
}

class ImportedItem {
	const ImportedItem(this.product, this.quantity, this.unit, this.purchasePrice, this.sellingPrice);
	factory ImportedItem.fromJson(Map<String, dynamic> json) => ImportedItem(
		json['productName']?.toString() ?? '-', (json['quantity'] as num?)?.toDouble() ?? 0, json['unit']?.toString() ?? '',
		(json['purchasePrice'] as num?)?.toDouble() ?? 0, (json['sellingPrice'] as num?)?.toDouble() ?? 0,
	);
	final String product;
	final double quantity;
	final String unit;
	final double purchasePrice;
	final double sellingPrice;
}

class _LoadError extends StatelessWidget {
	const _LoadError({required this.onRetry});
	final Future<void> Function() onRetry;
	@override
	Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
		const Icon(Icons.cloud_off_outlined, size: 42), const SizedBox(height: 12), const Text('تعذر تحميل بيانات المخزون.'), const SizedBox(height: 12),
		FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة')),
	]));
}

final numberFormat = NumberFormat('#,##0.##');
final moneyFormat = NumberFormat('#,##0.00');
String quantity(double value) => numberFormat.format(value);
String money(double? value) => value == null ? '-' : moneyFormat.format(value);
String statusLabel(String status) => switch (status) { 'AvailableForSale' => 'متاح للبيع', 'Sold' => 'مباع', _ => status };