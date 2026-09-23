import 'dart:convert';

import 'package:flutter/material.dart';

import '../core/ui_palette.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' hide TextDirection;

import '../core/app_navigation.dart';

class ReadyMadeProductionScreen extends StatefulWidget {
	const ReadyMadeProductionScreen({super.key});

	@override
	State<ReadyMadeProductionScreen> createState() => _ReadyMadeProductionScreenState();
}

class _ReadyMadeProductionScreenState extends State<ReadyMadeProductionScreen> with SingleTickerProviderStateMixin {
	final _api = _ReadyMadeProductionApi();
	late final TabController _tabs;
	late Future<_ReadyMadeProductionData> _future;

	@override
	void initState() {
		super.initState();
		_tabs = TabController(length: 5, vsync: this);
		_future = _api.load();
	}

	@override
	void dispose() {
		_tabs.dispose();
		super.dispose();
	}

	void _reload() => setState(() => _future = _api.load());

	@override
	Widget build(BuildContext context) => Directionality(
		textDirection: TextDirection.rtl,
		child: Container(
			color: UiPalette.screenBackground,
			child: FutureBuilder<_ReadyMadeProductionData>(
				future: _future,
				builder: (context, snapshot) {
					if (snapshot.connectionState != ConnectionState.done) {
						return const Center(child: CircularProgressIndicator());
					}
					if (snapshot.hasError) return _LoadError(onRetry: _reload);
					final data = snapshot.data!;
					return Column(
						crossAxisAlignment: CrossAxisAlignment.stretch,
						children: [
							Padding(
								padding: const EdgeInsets.fromLTRB(10, 12, 10, 4),
								child: Row(children: [
									Expanded(
										child: Text(
											'الإنتاج الجاهز من منتجاتنا',
											style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.screenBackground, fontSize: 24, fontWeight: FontWeight.w800),
										),
									),
									FilledButton.icon(
										onPressed: () => AppNavigation.push(context, (_) => const ReadyMadeOrderCreateScreen()),
										icon: const Icon(Icons.add_circle_outline),
										label: const Text('إنشاء أمر جديد'),
										style: FilledButton.styleFrom(
											backgroundColor: const Color.fromARGB(255, 3, 252, 206),
											foregroundColor: const Color.fromARGB(255, 0, 5, 13),
										),
									),
									const SizedBox(width: 8),
									IconButton(
										tooltip: 'تحديث البيانات',
										onPressed: _reload,
										icon: const Icon(Icons.refresh),
										color: const Color.fromARGB(255, 34, 54, 80),
									),
								]),
							),
							const SizedBox(height: 12),
							_Statistics(data: data),
							const SizedBox(height: 14),
							Container(
								decoration: BoxDecoration(
									color: UiPalette.surfaceCard,
									borderRadius: BorderRadius.circular(14),
									border: Border.all(color: UiPalette.borderSoft),
								),
								child: TabBar(
									controller: _tabs,
									isScrollable: true,
									indicatorColor: const Color.fromARGB(255, 3, 252, 206),
									labelColor: const Color.fromARGB(255, 3, 252, 206),
									unselectedLabelColor: UiPalette.textSoft,
									tabs: const [
										Tab(icon: Icon(Icons.assignment_outlined), text: 'أوامر الإنتاج'),
										Tab(icon: Icon(Icons.view_list_outlined), text: 'بنود الإنتاج'),
										Tab(icon: Icon(Icons.qr_code_2_outlined), text: 'القطع المنتجة'),
										Tab(icon: Icon(Icons.storefront_outlined), text: 'المنتجات الجاهزة للبيع'),
										Tab(icon: Icon(Icons.route_outlined), text: 'تتبع الإنتاج'),
									],
								),
							),
							const SizedBox(height: 8),
							Expanded(
								child: TabBarView(
									controller: _tabs,
									children: [
										_OrdersTab(data: data),
										_ItemsTab(data: data),
										_PiecesTab(data: data),
										_ProductsTab(data: data),
										_TrackingTab(data: data),
									],
								),
							),
						],
					);
				},
			),
		),
	);
}

class _Statistics extends StatelessWidget {
	const _Statistics({required this.data});
	final _ReadyMadeProductionData data;

	@override
	Widget build(BuildContext context) {
		final values = [
			('إجمالي أوامر الإنتاج', data.orders.length, Icons.assignment_outlined),
			('إجمالي القطع المنتجة', data.pieces.length, Icons.qr_code_2_outlined),
			('منتجات جاهزة للبيع', data.products.where((item) => item.status == 'AvailableForSale').length, Icons.storefront_outlined),
			('منتجات تم بيعها', data.products.where((item) => item.status == 'Sold').length, Icons.shopping_bag_outlined),
			('منتجات قيد التصنيع', data.pieces.where((item) => item.status != 'ReadyForSale').length, Icons.precision_manufacturing_outlined),
		];
		return SizedBox(
			height: 94,
			child: ListView.separated(
				scrollDirection: Axis.horizontal,
				itemCount: values.length,
				separatorBuilder: (_, __) => const SizedBox(width: 10),
				itemBuilder: (context, index) {
					final value = values[index];
					return SizedBox(
						width: 190,
						child: Card(
							child: Padding(
								padding: const EdgeInsets.all(12),
								child: Row(children: [
									Icon(value.$3),
									const SizedBox(width: 10),
									Expanded(child: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.start, children: [
										Text(value.$1, maxLines: 1, overflow: TextOverflow.ellipsis),
										Text('${value.$2}', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)),
									])),
								]),
							),
						),
					);
				},
			),
		);
	}
}

class _OrdersTab extends StatelessWidget {
	const _OrdersTab({required this.data});
	final _ReadyMadeProductionData data;

	@override
	Widget build(BuildContext context) => _TableFrame(
		child: DataTable(
			showCheckboxColumn: false,
			columns: const [
				DataColumn(label: Text('رقم الأمر')),
				DataColumn(label: Text('الحالة')),
				DataColumn(label: Text('تاريخ الإنشاء')),
				DataColumn(label: Text('عدد البنود')),
				DataColumn(label: Text('عدد القطع')),
				DataColumn(label: Text('المنتجات الناتجة')),
			],
			rows: data.orders.map((order) {
				final items = data.items.where((item) => item.orderId == order.id).toList();
				final itemIds = items.map((item) => item.id).toSet();
				final pieces = data.pieces.where((piece) => itemIds.contains(piece.itemId)).toList();
				final pieceIds = pieces.map((piece) => piece.id).toSet();
				final products = data.products.where((product) => pieceIds.contains(product.pieceId)).length;
				return DataRow(
					onSelectChanged: (_) => AppNavigation.push(context, (_) => _ReadyMadeOrderDetailsScreen(order: order, data: data)),
					cells: [
						DataCell(Text(order.number)),
						DataCell(_StatusLabel(order.status)),
						DataCell(Text(_date(order.createdAt))),
						DataCell(Text('${items.length}')),
						DataCell(Text('${pieces.length}')),
						DataCell(Text('$products')),
					],
				);
			}).toList(),
		),
	);
}

class _ItemsTab extends StatelessWidget {
	const _ItemsTab({required this.data});
	final _ReadyMadeProductionData data;

	@override
	Widget build(BuildContext context) => ListView.separated(
		itemCount: data.items.length,
		separatorBuilder: (_, __) => const SizedBox(height: 7),
		itemBuilder: (context, index) {
			final item = data.items[index];
			final order = data.orderById(item.orderId);
			final cardColor = UiPalette.surfaceCard;
			return Container(
				decoration: BoxDecoration(
					color: cardColor,
					borderRadius: BorderRadius.circular(14),
					border: Border.all(color: UiPalette.borderSoft),
				),
				padding: const EdgeInsets.all(4),
				child: ListTile(
					leading: const CircleAvatar(backgroundColor: UiPalette.primaryBlue, child: Icon(Icons.checkroom_outlined, color: UiPalette.textMain)),
					title: Text(item.name, style: UiPalette.adaptiveTextStyle(context, backgroundColor: cardColor, fontSize: 16, fontWeight: FontWeight.w700)),
					subtitle: Text('الكمية المطلوبة: ${item.quantity}  •  الحالة: ${_status(item.status)}\nاللون: ${item.color ?? 'غير مسجل'}  •  القماش: ${item.fabricType ?? 'غير مسجل'}\nالملاحظات: ${item.notes ?? 'لا توجد ملاحظات متاحة'}', style: UiPalette.adaptiveTextStyle(context, backgroundColor: cardColor, fontSize: 12, fontWeight: FontWeight.w500)),
					isThreeLine: true,
					trailing: Container(
						padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
						decoration: BoxDecoration(
							color: UiPalette.softBlue,
							borderRadius: BorderRadius.circular(10),
						),
						child: Text(order?.number ?? 'غير محدد', style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.softBlue, fontSize: 12, fontWeight: FontWeight.w700)),
					),
				),
			);
		},
	);
}

class _PiecesTab extends StatelessWidget {
	const _PiecesTab({required this.data});
	final _ReadyMadeProductionData data;

	@override
	Widget build(BuildContext context) => ListView.separated(
		itemCount: data.pieces.length,
		separatorBuilder: (_, __) => const SizedBox(height: 7),
		itemBuilder: (context, index) {
			final piece = data.pieces[index];
			final product = data.productForPiece(piece.id);
			final cardColor = UiPalette.surfaceCard;
			return Container(
				decoration: BoxDecoration(
					color: cardColor,
					borderRadius: BorderRadius.circular(14),
					border: Border.all(color: UiPalette.borderSoft),
				),
				padding: const EdgeInsets.all(4),
				child: ListTile(
					leading: CircleAvatar(backgroundColor: UiPalette.primaryBlue, child: Text('${piece.number}', style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.primaryBlue, fontSize: 14, fontWeight: FontWeight.w800))),
					title: Text(piece.trackingCode, style: UiPalette.adaptiveTextStyle(context, backgroundColor: cardColor, fontSize: 16, fontWeight: FontWeight.w700)),
					subtitle: Text('الحالة الحالية: ${_status(piece.status)}  •  مراحل التتبع: ${piece.events.length}\n${product == null ? 'لم تدخل المخزون' : 'موجودة في المخزون'}', style: UiPalette.adaptiveTextStyle(context, backgroundColor: cardColor, fontSize: 12, fontWeight: FontWeight.w500)),
					trailing: const Icon(Icons.chevron_left, color: UiPalette.primaryBlue),
					onTap: () => AppNavigation.push(context, (_) => _ReadyMadePieceDetailsScreen(piece: piece, product: product)),
				),
			);
		},
	);
}

class _ProductsTab extends StatelessWidget {
	const _ProductsTab({required this.data});
	final _ReadyMadeProductionData data;

	@override
	Widget build(BuildContext context) => ListView.separated(
		itemCount: data.products.length,
		separatorBuilder: (_, __) => const SizedBox(height: 7),
		itemBuilder: (context, index) {
			final product = data.products[index];
			final cardColor = UiPalette.surfaceCard;
			return Container(
				decoration: BoxDecoration(
					color: cardColor,
					borderRadius: BorderRadius.circular(14),
					border: Border.all(color: UiPalette.borderSoft),
				),
				padding: const EdgeInsets.all(4),
				child: ListTile(
					leading: Icon(product.active ? Icons.inventory_2_outlined : Icons.inventory_outlined, color: UiPalette.primaryBlue),
					title: Text(product.trackingCode, style: UiPalette.adaptiveTextStyle(context, backgroundColor: cardColor, fontSize: 16, fontWeight: FontWeight.w700)),
					subtitle: Text('أمر الإنتاج: ${product.orderNumber}  •  الحالة: ${_status(product.status)}\nسعر التكلفة: ${_money(product.cost)}  •  سعر البيع: ${_money(product.price)}', style: UiPalette.adaptiveTextStyle(context, backgroundColor: cardColor, fontSize: 12, fontWeight: FontWeight.w500)),
					trailing: const Icon(Icons.chevron_left, color: UiPalette.primaryBlue),
					onTap: () => AppNavigation.push(context, (_) => _ReadyMadeProductDetailsScreen(product: product)),
				),
			);
		},
	);
}

class _TrackingTab extends StatefulWidget {
	const _TrackingTab({required this.data});
	final _ReadyMadeProductionData data;

	@override
	State<_TrackingTab> createState() => _TrackingTabState();
}

class _TrackingTabState extends State<_TrackingTab> {
	int? selectedId;

	@override
	Widget build(BuildContext context) {
		final selected = widget.data.pieces.where((piece) => piece.id == selectedId).firstOrNull ?? (widget.data.pieces.isEmpty ? null : widget.data.pieces.first);
		return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
			DropdownButtonFormField<int>(
				initialValue: selected?.id,
				decoration: const InputDecoration(labelText: 'اختر القطعة', prefixIcon: Icon(Icons.qr_code_2_outlined), border: OutlineInputBorder()),
				items: widget.data.pieces.map((piece) => DropdownMenuItem(value: piece.id, child: Text('${piece.trackingCode} - ${_status(piece.status)}'))).toList(),
				onChanged: (value) => setState(() => selectedId = value),
			),
			const SizedBox(height: 12),
			Expanded(child: selected == null ? const Center(child: Text('لا توجد قطع منتجة.')) : _Timeline(events: selected.events)),
		]);
	}
}

class _ReadyMadeOrderDetailsScreen extends StatelessWidget {
	const _ReadyMadeOrderDetailsScreen({required this.order, required this.data});
	final _ReadyMadeOrder order;
	final _ReadyMadeProductionData data;

	@override
	Widget build(BuildContext context) {
		final items = data.items.where((item) => item.orderId == order.id).toList();
		final itemIds = items.map((item) => item.id).toSet();
		final pieces = data.pieces.where((piece) => itemIds.contains(piece.itemId)).toList();
		final pieceIds = pieces.map((piece) => piece.id).toSet();
		final products = data.products.where((product) => pieceIds.contains(product.pieceId)).toList();
		return _DetailsScaffold(
			title: 'تفاصيل أمر الإنتاج',
			children: [
				_DetailsWrap(children: [
					_Detail('رقم الأمر', order.number),
					_Detail('اسم الإنتاج', order.name),
					_Detail('حالة الأمر', _status(order.status)),
					_Detail('تاريخ الإنشاء', _date(order.createdAt)),
					_Detail('عدد البنود', '${items.length}'),
					_Detail('عدد القطع', '${pieces.length}'),
					_Detail('عدد المنتجات الناتجة', '${products.length}'),
					_Detail('إجمالي التكلفة', _money(order.totalCost)),
					_Detail('سعر البيع المقترح', _money(order.suggestedPrice)),
				]),
				const SizedBox(height: 20),
				Text('بنود الأمر', style: Theme.of(context).textTheme.titleLarge),
				const SizedBox(height: 8),
				...items.map((item) => Card(child: ListTile(title: Text(item.name), subtitle: Text('الكمية: ${item.quantity}  •  الحالة: ${_status(item.status)}')))),
			],
		);
	}
}

class _ReadyMadePieceDetailsScreen extends StatelessWidget {
	const _ReadyMadePieceDetailsScreen({required this.piece, required this.product});
	final _ReadyMadePiece piece;
	final _ReadyMadeProduct? product;

	@override
	Widget build(BuildContext context) => _DetailsScaffold(
		title: 'تفاصيل القطعة المنتجة',
		children: [
			_DetailsWrap(children: [
				_Detail('رقم التتبع', piece.trackingCode),
				_Detail('الحالة', _status(piece.status)),
				_Detail('تاريخ الإنشاء', _date(piece.createdAt)),
				_Detail('عدد أحداث التتبع', '${piece.events.length}'),
				_Detail('دخول المخزون', product == null ? 'لم تدخل المخزون' : 'دخلت المخزون'),
				_Detail('الجاهزية للبيع', piece.status == 'ReadyForSale' ? 'جاهزة للبيع' : 'لم تصبح جاهزة للبيع'),
			]),
			const SizedBox(height: 20),
			Text('مسار التتبع', style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 8),
			_Timeline(events: piece.events, shrinkWrap: true),
		],
	);
}

class _ReadyMadeProductDetailsScreen extends StatelessWidget {
	const _ReadyMadeProductDetailsScreen({required this.product});
	final _ReadyMadeProduct product;

	@override
	Widget build(BuildContext context) => _DetailsScaffold(
		title: 'تفاصيل المنتج الجاهز',
		children: [
			_DetailsWrap(children: [
				_Detail('رقم المنتج', '${product.id}'),
				_Detail('رقم التتبع', product.trackingCode),
				_Detail('رقم أمر الإنتاج', product.orderNumber),
				_Detail('اسم المنتج', product.name),
				_Detail('الحالة', _status(product.status)),
				_Detail('مصدر المنتج', _source(product.source)),
				_Detail('حالة النشاط', product.active ? 'نشط' : 'غير نشط'),
				_Detail('تكلفة المنتج', _money(product.cost)),
				_Detail('سعر البيع', _money(product.price)),
				_Detail('تاريخ الجاهزية للبيع', _date(product.readyAt)),
			]),
		],
	);
}

class _Timeline extends StatelessWidget {
	const _Timeline({required this.events, this.shrinkWrap = false});
	final List<_TrackingEvent> events;
	final bool shrinkWrap;

	@override
	Widget build(BuildContext context) {
		if (events.isEmpty) return const Center(child: Text('لم تبدأ مراحل تتبع هذه القطعة بعد.'));
		return ListView.builder(
			shrinkWrap: shrinkWrap,
			physics: shrinkWrap ? const NeverScrollableScrollPhysics() : null,
			itemCount: events.length,
			itemBuilder: (context, index) {
				final event = events[index];
				final isLast = index == events.length - 1;
				return IntrinsicHeight(
					child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
						SizedBox(width: 34, child: Column(children: [
							const Icon(Icons.check_circle, color: Colors.green),
							if (!isLast) Expanded(child: Container(width: 2, color: Theme.of(context).colorScheme.outlineVariant)),
						])),
						const SizedBox(width: 8),
						Expanded(child: Padding(
							padding: const EdgeInsets.only(bottom: 16),
							child: Card(child: ListTile(
								title: Text(_stage(event.stage)),
								subtitle: Text('الحالة: ${_status(event.status)}\nالتاريخ: ${_dateTime(event.time)}${event.employeeCode == null ? '' : '\nرمز الموظف: ${event.employeeCode}'}'),
							)),
						)),
					]),
				);
			},
		);
	}
}

class _TableFrame extends StatelessWidget {
	const _TableFrame({required this.child});
	final Widget child;
	@override Widget build(BuildContext context) => SingleChildScrollView(child: SingleChildScrollView(scrollDirection: Axis.horizontal, child: child));
}

class _StatusLabel extends StatelessWidget {
	const _StatusLabel(this.value);
	final String value;
	@override
	Widget build(BuildContext context) {
		final color = _statusColor(value);
		return Container(
			padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
			decoration: BoxDecoration(
				color: color,
				borderRadius: BorderRadius.circular(999),
				border: Border.all(color: UiPalette.borderSoft),
			),
			child: Text(_status(value), style: UiPalette.adaptiveTextStyle(context, backgroundColor: color, fontSize: 12, fontWeight: FontWeight.w700)),
		);
	}
}

class _DetailsScaffold extends StatelessWidget {
	const _DetailsScaffold({required this.title, required this.children});
	final String title;
	final List<Widget> children;
	@override
	Widget build(BuildContext context) => Directionality(
		textDirection: TextDirection.rtl,
		child: Scaffold(
			backgroundColor: UiPalette.screenBackground,
			appBar: AppBar(
				title: Text(title, style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.surfaceCard, fontSize: 18, fontWeight: FontWeight.w700)),
				backgroundColor: UiPalette.surfaceCard,
				foregroundColor: UiPalette.textMain,
				iconTheme: const IconThemeData(color: UiPalette.textMain),
			),
			body: Container(
				color: UiPalette.screenBackground,
				padding: const EdgeInsets.all(20),
				child: ListView(children: children),
			),
		),
	);
}

class _DetailsWrap extends StatelessWidget {
	const _DetailsWrap({required this.children});
	final List<Widget> children;
	@override
	Widget build(BuildContext context) => Container(
		decoration: BoxDecoration(
			color: UiPalette.surfaceCard,
			borderRadius: BorderRadius.circular(16),
			border: Border.all(color: UiPalette.borderSoft),
		),
		padding: const EdgeInsets.all(16),
		child: Wrap(spacing: 20, runSpacing: 16, children: children),
	);
}

class _Detail extends StatelessWidget {
	const _Detail(this.label, this.value);
	final String label;
	final String value;
	@override
	Widget build(BuildContext context) => SizedBox(
		width: 210,
		child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
			Text(label, style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.surfaceCard, fontSize: 12, fontWeight: FontWeight.w600)),
			const SizedBox(height: 3),
			SelectableText(value, style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.surfaceCard, fontSize: 15, fontWeight: FontWeight.w700)),
		]),
	);
}

class ReadyMadeOrderCreateScreen extends StatefulWidget {
	const ReadyMadeOrderCreateScreen({super.key});

	@override
	State<ReadyMadeOrderCreateScreen> createState() => _ReadyMadeOrderCreateScreenState();
}

class _ReadyMadeOrderCreateScreenState extends State<ReadyMadeOrderCreateScreen> {
	static const _baseUrl = String.fromEnvironment(
		'LUMAR_API_URL',
		defaultValue: 'http://127.0.0.1:5093',
	);

	final _formKey = GlobalKey<FormState>();
	final _nameController = TextEditingController();
	final _totalCostController = TextEditingController(text: '0');
	final _profitController = TextEditingController(text: '12');
	final _priceController = TextEditingController(text: '0');
	final _notesController = TextEditingController();
	final List<_ReadyMadeDraftItem> _items = [
		_ReadyMadeDraftItem(),
	];
	bool _saving = false;
	List<_PieceCostSetting> _pieceCostSettings = const [];
	Map<String, _FabricLookup> _fabrics = const {};

	@override
	void initState() {
		super.initState();
		_loadLookups();
	}

	@override
	void dispose() {
		for (final item in _items) {
			item.dispose();
		}
		_nameController.dispose();
		_totalCostController.dispose();
		_profitController.dispose();
		_priceController.dispose();
		_notesController.dispose();
		super.dispose();
	}

	Future<void> _loadLookups() async {
		try {
			final settingsResponse = await http.get(Uri.parse('$_baseUrl/piece-cost-settings'));
			final settings = settingsResponse.statusCode >= 200 && settingsResponse.statusCode < 300
				? (jsonDecode(settingsResponse.body) as List?)?.cast<Map<String, dynamic>>().map(_PieceCostSetting.fromJson).toList() ?? const <_PieceCostSetting>[]
				: const <_PieceCostSetting>[];

			final fabricsResponse = await http.get(Uri.parse('$_baseUrl/inventory/fabrics'));
			final fabrics = <String, _FabricLookup>{};
			if (fabricsResponse.statusCode >= 200 && fabricsResponse.statusCode < 300) {
				for (final row in (jsonDecode(fabricsResponse.body) as List).cast<Map<String, dynamic>>()) {
					final fabric = _FabricLookup.fromJson(row);
					if (fabric.code.isNotEmpty) {
						fabrics[fabric.code.toUpperCase()] = fabric;
					}
				}
			}
			if (!mounted) return;
			setState(() {
				_pieceCostSettings = settings;
				_fabrics = fabrics;
				for (final item in _items) {
					if (item.selectedPieceType.isEmpty && settings.isNotEmpty) {
						item.selectedPieceType = settings.first.name;
						item.selectedProductTypeId = settings.first.productTypeId;
						item.pieceTypeController.text = settings.first.name;
					}
					item.loadMeasurementFields();
					item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
				}
				_updateAutoPrice();
			});
		} catch (_) {
			if (!mounted) return;
			setState(() {
				_pieceCostSettings = const [];
				_fabrics = const {};
			});
		}
	}

	void _addItem() => setState(() {
		final item = _ReadyMadeDraftItem();
		if (_pieceCostSettings.isNotEmpty) {
			item.selectedPieceType = _pieceCostSettings.first.name;
			item.selectedProductTypeId = _pieceCostSettings.first.productTypeId;
			item.pieceTypeController.text = _pieceCostSettings.first.name;
		}
		item.loadMeasurementFields();
		item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
		_items.add(item);
		_updateAutoPrice();
	});

	void _removeItem(int index) {
		if (_items.length <= 1) return;
		setState(() {
			_items[index].dispose();
			_items.removeAt(index);
			_updateAutoPrice();
		});
	}

	void _syncFabricData(_ReadyMadeDraftItem item) {
		final code = item.fabricCodeController.text.trim();
		if (code.isEmpty) {
			item.fabricTypeController.clear();
			item.fabricColorController.clear();
			item.catalogController.clear();
			item.yardPriceController.clear();
			item.inchPriceController.clear();
			item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
			_updateAutoPrice();
			return;
		}
		final fabric = _fabrics[code.toUpperCase()];
		if (fabric == null) {
			item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
			_updateAutoPrice();
			return;
		}
		item.fabricTypeController.text = fabric.type;
		item.fabricColorController.text = fabric.color;
		item.catalogController.text = fabric.catalog;
		item.yardPriceController.text = _formatNumber(fabric.unitPrice);
		item.inchPriceController.text = _formatNumber(fabric.unitPrice / 36.0);
		item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
		_updateAutoPrice();
	}

	void _updateAutoPrice() {
		final totalCost = _items.fold<double>(0, (sum, item) => sum + item.lineTotalValue);
		_totalCostController.text = _formatNumber(totalCost);
		final profit = double.tryParse(_profitController.text.replaceAll(',', '.')) ?? 0;
		_priceController.text = _formatNumber(totalCost * (1 + (profit / 100)));
		for (final item in _items) {
			item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
		}
	}

	Future<void> _save() async {
		if (!_formKey.currentState!.validate()) return;
		if (_items.isEmpty) {
			_scaffoldMessage('يجب إضافة بند واحد على الأقل.');
			return;
		}
		for (var index = 0; index < _items.length; index++) {
			final item = _items[index];
			if (item.pieceTypeController.text.trim().isEmpty) {
				_scaffoldMessage('ادخل نوع القطعة في البند ${index + 1}.');
				return;
			}
			if (item.selectedProductTypeId <= 0) {
				_scaffoldMessage('نوع المنتج الرسمي مطلوب في البند ${index + 1}.');
				return;
			}
			if (int.tryParse(item.quantityController.text.trim()) == null || int.parse(item.quantityController.text.trim()) <= 0) {
				_scaffoldMessage('أدخل كمية صحيحة في البند ${index + 1}.');
				return;
			}
		}
		setState(() => _saving = true);
		try {
			final payload = {
				'productionOrderNumber': '',
				'productionName': _nameController.text.trim(),
				'totalCost': double.tryParse(_totalCostController.text.replaceAll(',', '.')) ?? 0,
				'profitPercentage': double.tryParse(_profitController.text.replaceAll(',', '.')) ?? 0,
				'suggestedSellingPrice': double.tryParse(_priceController.text.replaceAll(',', '.')) ?? 0,
				'notes': _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
				'items': _items.map((item) {
					item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
					return {
						'pieceType': item.selectedPieceType.trim(),
						'quantity': int.parse(item.quantityController.text.trim()),
						'productTypeId': item.selectedProductTypeId,
						'fabricCode': item.fabricCodeController.text.trim().isEmpty ? null : item.fabricCodeController.text.trim(),
						'fabricType': item.fabricTypeController.text.trim().isEmpty ? null : item.fabricTypeController.text.trim(),
						'fabricColor': item.fabricColorController.text.trim().isEmpty ? null : item.fabricColorController.text.trim(),
						'catalogNumber': item.catalogController.text.trim().isEmpty ? null : item.catalogController.text.trim(),
						'fabricCost': item.fabricCostValue,
						'pieceCost': item.pieceCostValue,
						'lineTotal': item.lineTotalValue,
						'measurementSnapshot': item.measurementsForPayload.isEmpty ? null : item.measurementsForPayload,
						'notes': item.notesController.text.trim().isEmpty ? null : item.notesController.text.trim(),
					};
				}).toList(),
			};
			final response = await http.post(
				Uri.parse('$_baseUrl/production/readymade-orders'),
				headers: {'Content-Type': 'application/json'},
				body: jsonEncode(payload),
			);
			if (response.statusCode < 200 || response.statusCode >= 300) {
				throw Exception('خطأ ${response.statusCode}: ${response.body}');
			}
			if (mounted) {
				ScaffoldMessenger.of(context).showSnackBar(
					SnackBar(content: Text('تم إنشاء أمر الإنتاج بنجاح.')),
				);
				Navigator.of(context).pop();
			}
		} catch (error) {
			debugPrint('Create ready-made order failed: $error');
			_scaffoldMessage('تعذر إنشاء أمر الإنتاج. تحقق من البيانات وحاول مرة أخرى.');
		} finally {
			if (mounted) setState(() => _saving = false);
		}
	}

	void _scaffoldMessage(String message) {
		if (!mounted) return;
		ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
	}

	Widget _buildOrderSummaryCard() {
		final cardColor = UiPalette.surfaceCard;
		return Container(
			padding: const EdgeInsets.all(18),
			decoration: BoxDecoration(
				gradient: LinearGradient(
					begin: Alignment.topCenter,
					end: Alignment.bottomCenter,
					colors: [UiPalette.surfaceCard, const Color.fromARGB(255, 0, 7, 14).withValues(alpha: 0.32)],
				),
				borderRadius: BorderRadius.circular(22),
				boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.18), blurRadius: 18, offset: const Offset(0, 8))],
				border: Border.all(color: UiPalette.borderSoft.withValues(alpha: 0.55)),
			),
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.stretch,
				children: [
					Text(
						'ملخص الأمر',
						style: UiPalette.adaptiveTextStyle(
							context,
							backgroundColor: cardColor,
							fontSize: 20,
							fontWeight: FontWeight.w800,
						),
					),
					const SizedBox(height: 16),
					_paddingField(
						controller: _nameController,
						label: 'اسم الإنتاج',
						validator: (value) => (value == null || value.trim().isEmpty) ? 'اسم الإنتاج مطلوب' : null,
					),
					const SizedBox(height: 12),
					_paddingField(controller: _totalCostController, label: 'إجمالي التكلفة', readOnly: true),
					const SizedBox(height: 12),
					_paddingField(
						controller: _profitController,
						label: 'نسبة الربح %',
						onChanged: (_) => _updateAutoPrice(),
					),
					const SizedBox(height: 12),
					_paddingField(controller: _priceController, label: 'سعر البيع المقترح', readOnly: true),
					const SizedBox(height: 12),
					_paddingField(controller: _notesController, label: 'ملاحظات', maxLines: 3),
				],
			),
		);
	}

	Widget _buildItemCard(_ReadyMadeDraftItem item, int index) {
		final row1 = [
			_dropdownField(
				label: 'نوع القطعة',
				value: item.selectedPieceType.isEmpty ? null : item.selectedPieceType,
				items: _pieceCostSettings.map((option) => DropdownMenuItem<String>(value: option.name, child: Text(option.name))).toList(),
				onChanged: (value) {
					if (value == null) return;
					final option = _pieceCostSettings.firstWhere((entry) => entry.name == value);
					setState(() {
						item.selectedPieceType = value;
						item.selectedProductTypeId = option.productTypeId;
						item.pieceTypeController.text = value;
						item.loadMeasurementFields();
						item.refreshCalculatedValues(_pieceCostSettings, _fabrics);
					});
					_updateAutoPrice();
				},
				validator: (value) => value == null || value.trim().isEmpty ? 'نوع القطعة مطلوب' : null,
			),
			_paddingField(controller: item.quantityController, label: 'الكمية', keyboardType: TextInputType.number),
			_paddingField(controller: item.fabricCodeController, label: 'كود القماش', onChanged: (_) => _syncFabricData(item)),
			_paddingField(controller: item.fabricTypeController, label: 'نوع القماش', readOnly: true),
			_paddingField(controller: item.fabricColorController, label: 'لون القماش', readOnly: true),
			_paddingField(controller: item.catalogController, label: 'رقم الكتالوج', readOnly: true),
		];

		final row2 = [
			_paddingField(controller: item.yardPriceController, label: 'سعر الياردة', readOnly: true),
			_paddingField(controller: item.inchPriceController, label: 'سعر البوصة', readOnly: true),
			_paddingField(controller: item.consumedQuantityController, label: 'الكمية المستهلكة', readOnly: true),
			_paddingField(controller: item.request1Controller, label: 'طلب رقم 1'),
			_paddingField(controller: item.request2Controller, label: 'طلب رقم 2'),
			_paddingField(controller: item.specialRequestController, label: 'طلب خاص'),
		];

		final measurementFields = item.measurementFieldNames.isNotEmpty
			? item.measurementFieldNames.map((fieldName) {
					final controller = item.measurementControllers.putIfAbsent(fieldName, () => TextEditingController());
					return _paddingField(controller: controller, label: fieldName);
				}).toList()
			: <Widget>[];

		return Container(
			margin: const EdgeInsets.only(bottom: 18),
			padding: const EdgeInsets.all(18),
			decoration: BoxDecoration(
				gradient: LinearGradient(
					begin: Alignment.topCenter,
					end: Alignment.bottomCenter,
					colors: [const Color.fromARGB(255, 18, 18, 20), const Color.fromARGB(255, 33, 33, 36)],
				),
				borderRadius: BorderRadius.circular(24),
				boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.24), blurRadius: 18, offset: const Offset(0, 9))],
				border: Border.all(color: const Color.fromARGB(255, 80, 80, 82).withValues(alpha: 0.7)),
			),
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.stretch,
				children: [
					Row(
						children: [
							Expanded(
								child: Text(
									'البند ${index + 1}',
									style: UiPalette.adaptiveTextStyle(
										context,
										backgroundColor: const Color.fromARGB(255, 18, 18, 20),
										fontSize: 18,
										fontWeight: FontWeight.w800,
									),
								),
							),
							if (_items.length > 1)
								TextButton.icon(
									onPressed: () => _removeItem(index),
									icon: const Icon(Icons.delete_outline_rounded),
									label: const Text('حذف البند'),
								),
						],
					),
					const SizedBox(height: 16),
					_buildFieldGrid(row1),
					const SizedBox(height: 14),
					_buildFieldGrid(row2),
					if (measurementFields.isNotEmpty) ...[
						const SizedBox(height: 14),
						_buildFieldGrid(measurementFields),
					],
				],
			),
		);
	}

	Widget _buildFieldGrid(List<Widget> fields) {
		if (fields.isEmpty) return const SizedBox.shrink();
		return LayoutBuilder(
			builder: (context, constraints) {
				final spacing = 12.0;
				final usableWidth = constraints.maxWidth - (spacing * (fields.length - 1));
				final itemWidth = usableWidth / fields.length;
				return Wrap(
					runSpacing: 12,
					spacing: spacing,
					alignment: WrapAlignment.start,
					children: fields
						.map((field) => SizedBox(width: itemWidth.clamp(110.0, constraints.maxWidth), child: field))
						.toList(),
				);
			},
		);
	}

	Widget _paddingField({
		required TextEditingController controller,
		required String label,
		bool readOnly = false,
		TextInputType keyboardType = TextInputType.text,
		int maxLines = 1,
		void Function(String)? onChanged,
		String? Function(String?)? validator,
	}) {
		return Container(
			padding: const EdgeInsets.fromLTRB(10, 8, 10, 6),
			decoration: BoxDecoration(
				color: readOnly ? const Color.fromARGB(255, 17, 17, 19).withValues(alpha: 0.55) : const Color.fromARGB(255, 12, 12, 14).withValues(alpha: 0.90),
				borderRadius: BorderRadius.circular(14),
				border: Border.all(color: readOnly ? const Color.fromARGB(255, 84, 84, 88).withValues(alpha: 0.5) : const Color.fromARGB(255, 78, 78, 82).withValues(alpha: 0.35)),
				boxShadow: [BoxShadow(color: const Color.fromARGB(255, 0, 0, 0).withValues(alpha: readOnly ? 0.12 : 0.20), blurRadius: readOnly ? 8 : 12, offset: const Offset(0, 5))],
			),
			child: TextFormField(
				controller: controller,
				readOnly: readOnly,
				keyboardType: keyboardType,
				maxLines: maxLines,
				onChanged: onChanged,
				validator: validator,
				decoration: InputDecoration(
					labelText: label,
					labelStyle: UiPalette.adaptiveTextStyle(
						context,
						backgroundColor: const Color.fromARGB(255, 12, 12, 14),
						fontSize: 12,
						fontWeight: FontWeight.w600,
					),
					border: InputBorder.none,
					enabledBorder: InputBorder.none,
					focusedBorder: InputBorder.none,
					contentPadding: EdgeInsets.zero,
				),
				style: UiPalette.adaptiveTextStyle(
					context,
					backgroundColor: const Color.fromARGB(255, 12, 12, 14),
					fontSize: 14,
					fontWeight: FontWeight.w600,
				),
			),
		);
	}

	Widget _dropdownField({
		required String label,
		required String? value,
		required List<DropdownMenuItem<String>> items,
		required void Function(String?) onChanged,
		String? Function(String?)? validator,
	}) {
		return Container(
			padding: const EdgeInsets.fromLTRB(10, 8, 10, 6),
			decoration: BoxDecoration(
				color: UiPalette.surfaceCard.withValues(alpha: 0.80),
				borderRadius: BorderRadius.circular(14),
				border: Border.all(color: UiPalette.primaryBlue.withValues(alpha: 0.35)),
				boxShadow: const [BoxShadow(color: Color.fromARGB(25, 0, 0, 0), blurRadius: 12, offset: Offset(0, 5))],
			),
			child: DropdownButtonFormField<String>(
				initialValue: value,
				isExpanded: true,
				decoration: InputDecoration(
					labelText: label,
					labelStyle: UiPalette.adaptiveTextStyle(
						context,
						backgroundColor: UiPalette.surfaceCard,
						fontSize: 12,
						fontWeight: FontWeight.w600,
					),
					border: InputBorder.none,
					enabledBorder: InputBorder.none,
					focusedBorder: InputBorder.none,
					contentPadding: EdgeInsets.zero,
				),
				items: items,
				onChanged: onChanged,
				validator: validator,
				style: UiPalette.adaptiveTextStyle(
					context,
					backgroundColor: UiPalette.surfaceCard,
					fontSize: 14,
					fontWeight: FontWeight.w600,
				),
			),
		);
	}

	@override
	Widget build(BuildContext context) {
		return Directionality(
			textDirection: TextDirection.rtl,
			child: Scaffold(
				backgroundColor: UiPalette.screenBackground,
				appBar: AppBar(
					title: Text(
						'إنشاء أمر إنتاج جاهز',
						style: UiPalette.adaptiveTextStyle(
							context,
							backgroundColor: UiPalette.surfaceCard,
							fontSize: 18,
							fontWeight: FontWeight.w700,
						),
					),
					backgroundColor: UiPalette.surfaceCard,
					foregroundColor: UiPalette.textMain,
					iconTheme: const IconThemeData(color: UiPalette.textMain),
				),
				body: SafeArea(
					child: Form(
						key: _formKey,
						child: LayoutBuilder(
							builder: (context, constraints) {
								final isWide = constraints.maxWidth >= 980;
								final itemList = [
									..._items.asMap().entries.map((entry) => _buildItemCard(entry.value, entry.key)),
									const SizedBox(height: 12),
									SizedBox(
										height: 50,
										child: FilledButton.icon(
											onPressed: _addItem,
											icon: const Icon(Icons.add_circle_outline_rounded),
											label: const Text('إضافة بند جديد'),
											style: FilledButton.styleFrom(
												backgroundColor: const Color.fromARGB(255, 18, 247, 216),
												foregroundColor: const Color.fromARGB(255, 0, 2, 5),
											),
										),
									),
									const SizedBox(height: 18),
									SizedBox(
										height: 48,
										child: FilledButton.icon(
											onPressed: _saving ? null : _save,
											icon: _saving
												? const SizedBox(
														width: 18,
														height: 18,
														child: CircularProgressIndicator(strokeWidth: 2),
													)
												: const Icon(Icons.save_alt_rounded),
											label: Text(_saving ? 'جاري الحفظ...' : 'حفظ الأمر'),
											style: FilledButton.styleFrom(
												backgroundColor: const Color.fromARGB(255, 17, 251, 231),
												foregroundColor: const Color.fromARGB(255, 0, 3, 8),
											),
										),
									),
								];

								if (isWide) {
									return Padding(
										padding: const EdgeInsets.all(16),
										child: Row(
											crossAxisAlignment: CrossAxisAlignment.start,
											children: [
												Expanded(
													flex: 3,
													child: ListView(
														padding: EdgeInsets.zero,
														children: itemList,
													),
												),
												const SizedBox(width: 18),
												Expanded(flex: 1, child: _buildOrderSummaryCard()),
											],
										),
									);
								}

								return Padding(
									padding: const EdgeInsets.all(16),
									child: Column(
										children: [
											Expanded(
												child: ListView(
													padding: EdgeInsets.zero,
													children: itemList,
												),
											),
											const SizedBox(height: 18),
											_buildOrderSummaryCard(),
										],
									),
								);
							},
						),
					),
				),
			),
		);
	}
}

class _PieceCostSetting {
	const _PieceCostSetting({required this.productTypeId, required this.name, required this.totalOperationalCost});

	factory _PieceCostSetting.fromJson(Map<String, dynamic> json) {
		final amount = ((json['totalOperationalCost'] ?? json['totalCost'] ?? json['total'] ?? 0) as num?)?.toDouble() ?? 0;
		final fallback = (((json['sewingCost'] as num?)?.toDouble() ?? 0) +
			((json['consumablesCost'] as num?)?.toDouble() ?? 0) +
			((json['ironingAndPackagingCost'] as num?)?.toDouble() ?? 0) +
			((json['fixedOperatingCost'] as num?)?.toDouble() ?? 0));
		return _PieceCostSetting(
			productTypeId: (json['productTypeId'] as num?)?.toInt() ?? 0,
			name: (json['pieceName'] ?? json['nameAr'] ?? json['name'] ?? 'غير محدد').toString(),
			totalOperationalCost: amount > 0 ? amount : fallback,
		);
	}

	final int productTypeId;
	final String name;
	final double totalOperationalCost;
}

class _FabricLookup {
	const _FabricLookup({required this.code, required this.type, required this.color, required this.catalog, required this.unitPrice});

	factory _FabricLookup.fromJson(Map<String, dynamic> json) => _FabricLookup(
		code: (json['fabricCode'] ?? json['inventoryFabricCode'] ?? json['itemCode'] ?? '').toString(),
		type: (json['fabricName'] ?? json['inventoryFabricName'] ?? json['fabricType'] ?? json['name'] ?? '').toString(),
		color: (json['color'] ?? json['fabricColor'] ?? '').toString(),
		catalog: (json['catalogNumber'] ?? json['catalog'] ?? json['barcode'] ?? '').toString(),
		unitPrice: ((json['pricePerYard'] ?? json['yardPrice'] ?? json['fabricPrice'] ?? json['unitPrice'] ?? 0) as num?)?.toDouble() ?? 0,
	);

	final String code;
	final String type;
	final String color;
	final String catalog;
	final double unitPrice;
}

class _ReadyMadeDraftItem {
	_ReadyMadeDraftItem() {
		quantityController.text = '1';
		fabricCostController.text = '0';
		pieceCostController.text = '0';
		lineTotalController.text = '0';
		consumedQuantityController.text = '0';
		yardPriceController.text = '0';
		inchPriceController.text = '0';
	}

	final pieceTypeController = TextEditingController();
	final quantityController = TextEditingController(text: '1');
	final fabricCodeController = TextEditingController();
	final fabricTypeController = TextEditingController();
	final fabricColorController = TextEditingController();
	final catalogController = TextEditingController();
	final measurementController = TextEditingController();
	final fabricCostController = TextEditingController(text: '0');
	final pieceCostController = TextEditingController(text: '0');
	final lineTotalController = TextEditingController(text: '0');
	final notesController = TextEditingController();
	final yardPriceController = TextEditingController(text: '0');
	final inchPriceController = TextEditingController(text: '0');
	final consumedQuantityController = TextEditingController(text: '0');
	final request1Controller = TextEditingController();
	final request2Controller = TextEditingController();
	final specialRequestController = TextEditingController();
	final measurementControllers = <String, TextEditingController>{};
	String selectedPieceType = '';
	int selectedProductTypeId = 0;
	double fabricCostValue = 0;
	double pieceCostValue = 0;
	double lineTotalValue = 0;

	List<String> get measurementFieldNames {
		final type = selectedPieceType.toLowerCase();
		if (type.contains('كوت')) return const ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن', 'فتحة اليد', 'وسع المرفق'];
		if (type.contains('ثوب') || type.contains('فستان')) return const ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن', 'الرقبة', 'طول الكبك', 'عرض الكبك', 'وسع المرفق', 'فتحة اسفل الثوب'];
		if (type.contains('قميص') || type.contains('بلايز') || type.contains('بلوز')) return const ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن', 'الرقبة', 'طول الكبك', 'عرض الكبك', 'وسع المرفق'];
		if (type.contains('يلق')) return const ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'];
		if (type.contains('بنطلون')) return const ['الطول', 'الحزام', 'الارداف', 'الفخذ', 'الركبة', 'الفتحة', 'عرض الحزام'];
		if (type.contains('مريلة')) return const ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'];
		if (type.contains('بالطو')) return const ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'];
		if (type.contains('جاكيت')) return const ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'];
		if (type.contains('سروال')) return const ['الطول', 'الحزام', 'الارداف'];
		if (type.contains('فنيلة')) return const ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن', 'الرقبة'];
		if (type.contains('مقطب')) return const ['الطول', 'العرض'];
		if (type.contains('طاقية')) return const ['دوران الراس', 'ارتفاع الحزام'];
		return const ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'];
	}

	Map<String, dynamic> get measurementsForPayload {
		final payload = <String, dynamic>{};
		for (final fieldName in measurementFieldNames) {
			final controller = measurementControllers[fieldName];
			final value = controller?.text.trim();
			if (value != null && value.isNotEmpty) {
				payload[fieldName] = value;
			}
		}
		return payload;
	}

	void loadMeasurementFields() {
		final names = measurementFieldNames;
		final current = measurementControllers.keys.toSet();
		for (final name in current.difference(names.toSet())) {
			measurementControllers.remove(name)?.dispose();
		}
		for (final name in names) {
			measurementControllers.putIfAbsent(name, () => TextEditingController());
		}
	}

	void refreshCalculatedValues(List<_PieceCostSetting> pieceOptions, Map<String, _FabricLookup> fabrics) {
		final quantity = int.tryParse(quantityController.text.trim()) ?? 1;
		final selectedOption = pieceOptions.where((option) => option.name == selectedPieceType).isNotEmpty ? pieceOptions.firstWhere((option) => option.name == selectedPieceType) : null;
		final fabric = fabrics[fabricCodeController.text.trim().toUpperCase()];
		final consumption = _estimatedConsumption(selectedPieceType, quantity);
		final fabricUnitPrice = fabric?.unitPrice ?? 0;
		fabricCostValue = consumption * fabricUnitPrice;
		pieceCostValue = (selectedOption?.totalOperationalCost ?? 0) * quantity;
		lineTotalValue = fabricCostValue + pieceCostValue;
		fabricCostController.text = _formatNumber(fabricCostValue);
		pieceCostController.text = _formatNumber(pieceCostValue);
		lineTotalController.text = _formatNumber(lineTotalValue);
		consumedQuantityController.text = _formatNumber(consumption);
		yardPriceController.text = _formatNumber(fabricUnitPrice);
		inchPriceController.text = _formatNumber(fabricUnitPrice / 36.0);
	}

	void dispose() {
		pieceTypeController.dispose();
		quantityController.dispose();
		fabricCodeController.dispose();
		fabricTypeController.dispose();
		fabricColorController.dispose();
		catalogController.dispose();
		measurementController.dispose();
		fabricCostController.dispose();
		pieceCostController.dispose();
		lineTotalController.dispose();
		notesController.dispose();
		yardPriceController.dispose();
		inchPriceController.dispose();
		consumedQuantityController.dispose();
		request1Controller.dispose();
		request2Controller.dispose();
		specialRequestController.dispose();
		for (final controller in measurementControllers.values) {
			controller.dispose();
		}
		measurementControllers.clear();
	}
}

String _formatNumber(double value) => NumberFormat('#,##0.##').format(value);

double _estimatedConsumption(String pieceType, int quantity) {
	final type = pieceType.toLowerCase();
	final base = type.contains('قميص') || type.contains('ثوب') || type.contains('فستان')
		? 1.5
		: type.contains('بنطلون') || type.contains('سروال') || type.contains('شورت')
			? 1.8
			: type.contains('جاكيت') || type.contains('بالطو') || type.contains('سترة')
				? 2.2
				: 1.0;
	return base * quantity;
}

class _LoadError extends StatelessWidget {
	const _LoadError({required this.onRetry});
	final VoidCallback onRetry;
	@override Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.cloud_off_outlined, size: 44), const SizedBox(height: 10), const Text('تعذر تحميل بيانات الإنتاج الجاهز.'), const SizedBox(height: 10), FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة'))]));
}

class _ReadyMadeProductionData {
	const _ReadyMadeProductionData({required this.orders, required this.items, required this.pieces, required this.products});
	final List<_ReadyMadeOrder> orders;
	final List<_ReadyMadeItem> items;
	final List<_ReadyMadePiece> pieces;
	final List<_ReadyMadeProduct> products;
	_ReadyMadeOrder? orderById(int id) => orders.where((order) => order.id == id).firstOrNull;
	_ReadyMadeProduct? productForPiece(int id) => products.where((product) => product.pieceId == id).firstOrNull;
}

class _ReadyMadeOrder {
	const _ReadyMadeOrder({required this.id, required this.number, required this.name, required this.totalCost, required this.suggestedPrice, required this.status, required this.createdAt});
	factory _ReadyMadeOrder.fromJson(Map<String, dynamic> json) => _ReadyMadeOrder(id: json['readyMadeProductionOrderId'] as int, number: json['productionOrderNumber'] as String, name: json['productionName'] as String, totalCost: _decimal(json['totalCost']), suggestedPrice: _decimal(json['suggestedSellingPrice']), status: json['status'] as String, createdAt: DateTime.parse(json['createdAt'] as String));
	final int id;
	final String number;
	final String name;
	final double totalCost;
	final double suggestedPrice;
	final String status;
	final DateTime createdAt;
}

class _ReadyMadeItem {
	const _ReadyMadeItem({required this.id, required this.orderId, required this.name, required this.quantity, required this.status, this.color, this.fabricType, this.fabricCode, this.catalogNumber, this.notes});
	factory _ReadyMadeItem.fromJson(Map<String, dynamic> json) => _ReadyMadeItem(id: json['readyMadeProductionOrderItemId'] as int, orderId: json['readyMadeProductionOrderId'] as int, name: json['pieceType'] as String, quantity: json['quantity'] as int, status: json['pieceStatus'] as String, color: json['fabricColor'] as String?, fabricType: json['fabricType'] as String?, fabricCode: json['fabricCode'] as String?, catalogNumber: json['catalogNumber'] as String?, notes: _combinedNotes(json));
	final int id;
	final int orderId;
	final String name;
	final int quantity;
	final String status;
	final String? color;
	final String? fabricType;
	final String? fabricCode;
	final String? catalogNumber;
	final String? notes;
}

class _ReadyMadePiece {
	const _ReadyMadePiece({required this.id, required this.itemId, required this.number, required this.trackingCode, required this.status, required this.createdAt, required this.events});
	factory _ReadyMadePiece.fromJson(Map<String, dynamic> json, List<_TrackingEvent> events) => _ReadyMadePiece(id: json['readyMadeProductionOrderPieceInstanceId'] as int, itemId: json['readyMadeProductionOrderItemId'] as int, number: json['pieceNumber'] as int, trackingCode: json['trackingCode'] as String, status: json['pieceStatus'] as String, createdAt: DateTime.parse(json['createdAt'] as String), events: events);
	final int id;
	final int itemId;
	final int number;
	final String trackingCode;
	final String status;
	final DateTime createdAt;
	final List<_TrackingEvent> events;
}

class _ReadyMadeProduct {
	const _ReadyMadeProduct({required this.id, required this.orderId, required this.itemId, required this.pieceId, required this.orderNumber, required this.name, required this.trackingCode, required this.cost, required this.price, required this.status, required this.source, required this.active, required this.readyAt});
	factory _ReadyMadeProduct.fromJson(Map<String, dynamic> json) => _ReadyMadeProduct(id: json['readyMadeInventoryProductId'] as int, orderId: json['readyMadeProductionOrderId'] as int, itemId: json['readyMadeProductionOrderItemId'] as int, pieceId: json['readyMadeProductionOrderPieceInstanceId'] as int, orderNumber: json['productionOrderNumber'] as String, name: json['productionName'] as String, trackingCode: json['trackingCode'] as String, cost: _nullableDecimal(json['actualCost']), price: _nullableDecimal(json['suggestedSellingPrice']), status: json['status'] as String, source: json['source'] as String, active: json['isActive'] as bool, readyAt: DateTime.parse(json['readyForSaleAt'] as String));
	final int id;
	final int orderId;
	final int itemId;
	final int pieceId;
	final String orderNumber;
	final String name;
	final String trackingCode;
	final double? cost;
	final double? price;
	final String status;
	final String source;
	final bool active;
	final DateTime readyAt;
}

class _TrackingEvent {
	const _TrackingEvent({required this.stage, required this.status, required this.time, this.employeeCode});
	factory _TrackingEvent.fromJson(Map<String, dynamic> json) => _TrackingEvent(stage: json['stage'] as String, status: json['status'] as String, time: DateTime.parse(json['eventTime'] as String), employeeCode: json['employeeCode'] as String?);
	final String stage;
	final String status;
	final DateTime time;
	final String? employeeCode;
}

class _ReadyMadeProductionApi {
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');

	Future<_ReadyMadeProductionData> load() async {
		final root = await Future.wait([_list('/production/readymade-orders'), _list('/inventory/readymade')]);
		final orders = root[0].map(_ReadyMadeOrder.fromJson).toList();
		final products = root[1].map(_ReadyMadeProduct.fromJson).toList();
		final itemGroups = await Future.wait(orders.map((order) => _list('/production/readymade-orders/${order.id}/items')));
		final items = itemGroups.expand((group) => group).map(_ReadyMadeItem.fromJson).toList();
		final pieceGroups = await Future.wait(items.map((item) => _list('/production/readymade-order-items/${item.id}/pieces')));
		final pieceJson = pieceGroups.expand((group) => group).toList();
		final eventGroups = await Future.wait(pieceJson.map((piece) => _list('/production/readymade-pieces/${piece['readyMadeProductionOrderPieceInstanceId']}/tracking')));
		final pieces = <_ReadyMadePiece>[];
		for (var index = 0; index < pieceJson.length; index++) {
			pieces.add(_ReadyMadePiece.fromJson(pieceJson[index], eventGroups[index].map(_TrackingEvent.fromJson).toList()));
		}
		return _ReadyMadeProductionData(orders: orders, items: items, pieces: pieces, products: products);
	}

	Future<List<Map<String, dynamic>>> _list(String path) async {
		final response = await http.get(Uri.parse('$_baseUrl$path'));
		if (response.statusCode < 200 || response.statusCode >= 300) throw Exception('تعذر تحميل البيانات');
		return (jsonDecode(response.body) as List).cast<Map<String, dynamic>>();
	}
}

String _status(String value) => switch (value) {
	'Draft' => 'مسودة',
	'Completed' => 'مكتمل',
	'New' => 'جديدة',
	'Printing' => 'الطباعة',
	'Cutting' => 'القص',
	'Sewing' => 'الخياطة',
	'Buttons' => 'الأزرار',
	'Ironing' => 'الكي',
	'Quality' => 'فحص الجودة',
	'Assembly' => 'التجميع',
	'Ready' || 'ReadyForSale' => 'جاهزة للبيع',
	'AvailableForSale' => 'متاحة للبيع',
	'Sold' => 'تم بيعها',
	'InProduction' => 'قيد التصنيع',
	_ => 'حالة أخرى',
};

String _stage(String value) => switch (value) {
	'Printing' => 'الطباعة',
	'Cutting' => 'القص',
	'Sewing' => 'الخياطة',
	'Buttons' => 'الأزرار',
	'Ironing' => 'الكي',
	'Quality' => 'فحص الجودة',
	'Assembly' => 'التجميع',
	'Ready' => 'جاهز للبيع',
	_ => 'مرحلة أخرى',
};

Color _statusColor(String value) => switch (value) {
	'ReadyForSale' || 'AvailableForSale' => const Color(0xFF1F8F5F),
	'New' || 'Draft' => const Color(0xFF3B82F6),
	'Cutting' || 'Sewing' || 'Quality' || 'Assembly' => const Color(0xFF4EC9B0),
	'Sold' => const Color(0xFFB45309),
	_ => const Color(0xFF475569),
};

String _source(String value) => value == 'OurProduction' ? 'إنتاجنا' : 'مصدر آخر';
String _date(DateTime value) => DateFormat('yyyy/MM/dd').format(value);
String _dateTime(DateTime value) => DateFormat('yyyy/MM/dd HH:mm').format(value);
String _money(double? value) => value == null ? 'غير مسجل' : NumberFormat('#,##0.##').format(value);
double _decimal(Object? value) => (value as num).toDouble();
double? _nullableDecimal(Object? value) => value == null ? null : (value as num).toDouble();
String? _combinedNotes(Map<String, dynamic> json) {
	final values = [json['notes'], json['notes1'], json['notes2']].whereType<String>().where((value) => value.trim().isNotEmpty).toList();
	return values.isEmpty ? null : values.join(' - ');
}