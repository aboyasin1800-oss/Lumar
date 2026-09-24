import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';
import 'inventory/bulk_fabric_entry_screen.dart';
import 'inventory/imported_product_entry_screen.dart';
import 'inventory/tool_entry_screen.dart';

class InventoryScreen extends StatefulWidget {
  const InventoryScreen({super.key});

  @override
  State<InventoryScreen> createState() => _InventoryScreenState();
}

class _InventoryScreenState extends State<InventoryScreen>
    with SingleTickerProviderStateMixin {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  late final TabController _tabs;
  late final TextEditingController _fabricSearchController;
  InventoryData? _data;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 4, vsync: this);
    _fabricSearchController = TextEditingController();
    _load();
  }

  @override
  void dispose() {
    _tabs.dispose();
    _fabricSearchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);

    try {
      final responses = await Future.wait([
        http.get(Uri.parse('$_baseUrl/inventory/items')),
        http.get(Uri.parse('$_baseUrl/inventory/fabrics')),
        http.get(Uri.parse('$_baseUrl/inventory/readymade')),
        http.get(Uri.parse('$_baseUrl/inventory/imported')),
        http.get(Uri.parse('$_baseUrl/suppliers')),
        http.get(Uri.parse('$_baseUrl/inventory/transactions')),
      ]);

      if (responses.any((response) => response.statusCode < 200 || response.statusCode >= 300)) {
        throw Exception();
      }

      final inventoryItems = (jsonDecode(responses[0].body) as List)
          .cast<Map<String, dynamic>>()
          .map(InventoryItemRecord.fromJson)
          .toList();

      final fabricRows = (jsonDecode(responses[1].body) as List)
          .cast<Map<String, dynamic>>();

      final readyMade = (jsonDecode(responses[2].body) as List)
          .cast<Map<String, dynamic>>()
          .map(ReadyMadeItem.fromJson)
          .toList();

      final imported = (jsonDecode(responses[3].body) as List)
          .cast<Map<String, dynamic>>()
          .map(ImportedItem.fromJson)
          .toList();

      final supplierMap = <int, String>{};
      final suppliers = (jsonDecode(responses[4].body) as List?) ?? const [];
      for (final supplier in suppliers.cast<Map<String, dynamic>>()) {
        final id = (supplier['supplierId'] as num?)?.toInt();
        final name = supplier['supplierName']?.toString();
        if (id != null && name != null && name.trim().isNotEmpty) {
          supplierMap[id] = name.trim();
        }
      }

      final supplierByInventoryItemId = <int, String>{};
      final transactions = (jsonDecode(responses[5].body) as List?) ?? const [];
      for (final transaction in transactions.cast<Map<String, dynamic>>()) {
        final itemId = (transaction['inventoryItemId'] as num?)?.toInt();
        final notes = transaction['notes']?.toString() ?? '';
        if (itemId == null || notes.trim().isEmpty) {
          continue;
        }

        final match = RegExp(r'(?:المورد|مورد)\s*:\s*([^|]+)|Supplier\s*:\s*([^|]+)').firstMatch(notes);
        final candidate = (match?.group(1) ?? match?.group(2))?.trim();
        if (candidate != null && candidate.isNotEmpty) {
          supplierByInventoryItemId[itemId] = candidate;
        }
      }

      final fabricsFromApi = fabricRows
          .map((row) => _fabricItemFromApi(row, supplierMap, supplierByInventoryItemId))
          .whereType<FabricItem>()
          .toList();

      final fabrics = _deduplicateFabrics(fabricsFromApi);

      final tools = inventoryItems
          .where((item) => _isToolCategory(item.category) || _isToolCategory(item.itemName))
          .map(ToolItem.fromInventory)
          .toList();

      setState(() {
        _data = InventoryData(
          fabrics: fabrics,
          readyMade: readyMade,
          imported: imported,
          tools: tools,
        );
        _loading = false;
      });
    } catch (_) {
      setState(() {
        _loading = false;
        _data = null;
      });
    }
  }

  List<FabricItem> _deduplicateFabrics(List<FabricItem> fabrics) {
    final seen = <String>{};
    final result = <FabricItem>[];

    for (final fabric in fabrics) {
      final key = '${fabric.sourceTable ?? 'unknown'}|${fabric.sourceId ?? ''}|${fabric.code}|${fabric.name}|${fabric.color ?? ''}|${fabric.unit ?? ''}';
      if (seen.add(key)) {
        result.add(fabric);
      }
    }

    return result;
  }

  bool _matchesFabricSearch(FabricItem item, String query) {
    if (query.isEmpty) {
      return true;
    }

    final normalizedQuery = query.toLowerCase();
    final haystack = [
      item.name,
      item.code,
      item.color ?? '',
      item.category,
      item.unit ?? '',
    ].join(' ').toLowerCase();

    return haystack.contains(normalizedQuery);
  }

  bool _isToolCategory(String category) {
    final value = category.toLowerCase();
    return value.contains('tool') ||
        value.contains('sewing') ||
        value.contains('needle') ||
        value.contains('thread') ||
        value.contains('machine') ||
        value.contains('equipment') ||
        value.contains('اداة') ||
        value.contains('أداة');
  }

  FabricItem? _fabricItemFromApi(
    Map<String, dynamic> json,
    Map<int, String> supplierMap,
    Map<int, String> supplierByInventoryItemId,
  ) {
    final code = (json['fabricCode'] ?? json['FabricCode'] ?? json['itemCode'] ?? json['ItemCode'] ?? json['inventoryFabricCode'] ?? json['InventoryFabricCode'])?.toString();
    final name = (json['inventoryFabricName'] ?? json['fabricName'] ?? json['InventoryFabricName'] ?? json['FabricName'])?.toString();
    final available = ((json['availableQuantity'] ?? json['AvailableQuantity'] ?? json['quantityYard'] ?? json['QuantityYard']) as num?)?.toDouble() ?? 0;
    final current = ((json['quantityYard'] ?? json['QuantityYard'] ?? json['availableQuantity'] ?? json['AvailableQuantity']) as num?)?.toDouble() ?? available;
    final color = (json['color'] ?? json['Color'] ?? json['fabricColor'] ?? json['FabricColor'])?.toString();
    final unit = (json['unit'] ?? json['Unit'])?.toString();
    final price = ((json['pricePerYard'] ?? json['PricePerYard'] ?? json['fabricPrice'] ?? json['FabricPrice']) as num?)?.toDouble();
    final sourceTable = (json['sourceTable'] ?? json['SourceTable'])?.toString();
    final sourceId = (json['fabricId'] ?? json['FabricID']) as num?;
    final inventoryItemId = (json['inventoryFabricCode'] ?? json['InventoryFabricCode']) as num?;
    final rawSupplierId = (json['supplierId'] ?? json['SupplierId'] ?? json['supplier_id'] ?? json['supplierID']) as num?;
    final explicitSupplierName = (json['supplierName'] ?? json['SupplierName'] ?? json['supplier'] ?? json['Supplier'])?.toString();
    final catalogNumber = (json['catalogNumber'] ?? json['CatalogNumber'] ?? json['barcode'] ?? json['Barcode'])?.toString();

    final supplierName = explicitSupplierName ??
        (rawSupplierId != null ? supplierMap[rawSupplierId.toInt()] : null) ??
        (inventoryItemId != null ? supplierByInventoryItemId[inventoryItemId.toInt()] : null);

    if (code == null && name == null) {
      return null;
    }

    return FabricItem(
      code: code ?? name ?? 'FAB-${DateTime.now().microsecondsSinceEpoch}',
      name: name ?? code ?? 'قماش',
      category: sourceTable ?? 'Fabric',
      current: current,
      available: available,
      reserved: 0,
      color: color,
      unit: unit,
      price: price,
      catalogNumber: catalogNumber,
      sourceTable: sourceTable,
      sourceId: sourceId?.toInt(),
      supplierName: supplierName,
    );
  }

  Future<void> _refresh() async => _load();

  Future<void> _confirmDelete(
    BuildContext context,
    String message,
    Future<void> Function() onDelete,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('تنبيه حذف'),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('حذف'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await onDelete();
    }
  }

  InventoryData _removeFabric(int index) {
    final current = _data!;
    final updated = [...current.fabrics];
    updated.removeAt(index);
    return InventoryData(
      fabrics: updated,
      readyMade: [...current.readyMade],
      imported: [...current.imported],
      tools: [...current.tools],
    );
  }

  InventoryData _removeReadyMade(int index) {
    final current = _data!;
    final updated = [...current.readyMade];
    updated.removeAt(index);
    return InventoryData(
      fabrics: [...current.fabrics],
      readyMade: updated,
      imported: [...current.imported],
      tools: [...current.tools],
    );
  }

  InventoryData _removeImported(int index) {
    final current = _data!;
    final updated = [...current.imported];
    updated.removeAt(index);
    return InventoryData(
      fabrics: [...current.fabrics],
      readyMade: [...current.readyMade],
      imported: updated,
      tools: [...current.tools],
    );
  }

  InventoryData _removeTool(int index) {
    final current = _data!;
    final updated = [...current.tools];
    updated.removeAt(index);
    return InventoryData(
      fabrics: [...current.fabrics],
      readyMade: [...current.readyMade],
      imported: [...current.imported],
      tools: updated,
    );
  }

  Future<void> _deleteFabricAt(int index) async {
    if (_data == null) return;
    await _confirmDelete(
      context,
      'هل أنت متأكد من حذف هذا المنتج من مخزن الأقمشة؟',
      () async {
        setState(() {
          _data = _removeFabric(index);
        });
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف المنتج من مخزن الأقمشة.')),
          );
        }
      },
    );
  }

  Future<void> _showReadyMadeDetails(ReadyMadeItem item) async {
    final statusText = statusLabel(item.status);
    final isSold = item.status == 'Sold';
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Row(
          children: [
            Expanded(child: Text('تفاصيل المنتج الجاهز')),
            if (isSold)
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(
                  color: Colors.red.shade700,
                  borderRadius: BorderRadius.circular(6),
                ),
                child: const Text('مباع', style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
              ),
          ],
        ),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _readyMadeDetail('كود التتبع', item.trackingCode),
              _readyMadeDetail('اسم القطعة', item.product),
              _readyMadeDetail('معرف نوع المنتج', item.productTypeId?.toString() ?? '-'),
              _readyMadeDetail('نوع القطعة', item.productTypeName),
              _readyMadeDetail('معرف منتج المخزون', item.inventoryProductId.toString()),
              _readyMadeDetail('كود القماش', item.fabricCode),
              _readyMadeDetail('نوع القماش', item.fabricType),
              _readyMadeDetail('لون القماش', item.fabricColor),
              _readyMadeDetail('الاستهلاك', item.consumption == null ? '-' : '${numberFormat.format(item.consumption)} بوصة'),
              _readyMadeDetail('تكلفة القماش', money(item.fabricCost)),
              _readyMadeDetail('تكلفة التشغيل', money(item.operatingCost)),
              _readyMadeDetail('التكلفة الكاملة', money(item.fullCost ?? item.actualCost)),
              _readyMadeDetail('سعر البيع المقترح', money(item.price)),
              _readyMadeDetail('التكلفة الفعلية', money(item.actualCost)),
              _readyMadeDetail('الحالة', statusText),
              _readyMadeDetail('تاريخ الإنشاء', item.createdAt == null ? '-' : DateFormat('yyyy/MM/dd').format(item.createdAt!)),
            ],
          ),
        ),
        actions: [TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('إغلاق'))],
      ),
    );
  }

  Widget _readyMadeDetail(String label, String value) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: Text(label, style: const TextStyle(fontWeight: FontWeight.w700))),
            const SizedBox(width: 12),
            Flexible(child: SelectableText(value)),
          ],
        ),
      );

  Future<void> _deleteReadyMadeAt(int index) async {
    if (_data == null) return;
    await _confirmDelete(
      context,
      'هل أنت متأكد من حذف هذا المنتج من مخزن المنتجات الجاهزة؟',
      () async {
        setState(() {
          _data = _removeReadyMade(index);
        });
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف المنتج من مخزن المنتجات الجاهزة.')),
          );
        }
      },
    );
  }

  Future<void> _deleteImportedAt(int index) async {
    if (_data == null) return;
    await _confirmDelete(
      context,
      'هل أنت متأكد من حذف هذا المنتج من مخزن المنتجات المستوردة؟',
      () async {
        setState(() {
          _data = _removeImported(index);
        });
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف المنتج من مخزن المنتجات المستوردة.')),
          );
        }
      },
    );
  }

  Future<void> _deleteToolAt(int index) async {
    if (_data == null) return;
    await _confirmDelete(
      context,
      'هل أنت متأكد من حذف هذا المنتج من مخزن الأدوات؟',
      () async {
        setState(() {
          _data = _removeTool(index);
        });
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حذف المنتج من مخزن الأدوات.')),
          );
        }
      },
    );
  }

  Future<void> _addToTab(String tabName) async {
    if (tabName == 'fabric') {
      final result = await AppNavigation.push<bool>(context, (_) => const BulkFabricEntryScreen());
      if (result == true) {
        _load();
      }
      return;
    }

    if (tabName == 'imported') {
      final result = await AppNavigation.push<bool>(context, (_) => const ImportedProductEntryScreen());
      if (result == true) {
        _load();
      }
      return;
    }

    if (tabName == 'tools') {
      final result = await AppNavigation.push<bool>(context, (_) => const ToolEntryScreen());
      if (result == true) {
        _load();
      }
      return;
    }

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('إضافة عنصر جديد في $tabName سيتم تنفيذها لاحقًا في شاشة الإدخال المناسبة.')),
      );
    }
  }

  double get _readyMadeAveragePrice {
    if (_data == null || _data!.readyMade.isEmpty) {
      return 0;
    }

    final total = _data!.readyMade
        .map((item) => item.price ?? 0.0)
        .reduce((a, b) => a + b);

    return total / _data!.readyMade.length;
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_data == null) {
      return _LoadError(onRetry: _refresh);
    }

    final data = _data!;
    final fabricSearchQuery = _fabricSearchController.text.trim();
    final visibleFabrics = data.fabrics
        .where((item) => _matchesFabricSearch(item, fabricSearchQuery))
        .toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'إدارة المخازن',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
            ),
            IconButton(
              tooltip: 'تحديث البيانات',
              onPressed: _refresh,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Container(
          height: 52,
          decoration: BoxDecoration(
            color: UiPalette.surfaceCard,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: UiPalette.borderSoft.withValues(alpha: 0.55)),
          ),
          child: TabBar(
            controller: _tabs,
            isScrollable: false,
            tabAlignment: TabAlignment.fill,
            labelColor: UiPalette.textMain,
            unselectedLabelColor: UiPalette.textSoft,
            indicatorColor: const Color.fromARGB(255, 18, 247, 216),
            dividerColor: Colors.transparent,
            indicatorSize: TabBarIndicatorSize.tab,
            indicator: const UnderlineTabIndicator(
              borderSide: BorderSide(width: 3, color: Color.fromARGB(255, 18, 247, 216)),
              insets: EdgeInsets.symmetric(horizontal: 6),
            ),
            labelStyle: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w700,
              fontSize: 16,
            ),
            unselectedLabelStyle: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w600,
              fontSize: 12,
            ),
            tabs: const [
              Tab(text: 'مخزن الأقمشة'),
              Tab(text: 'مخزن المنتجات الجاهزة'),
              Tab(text: 'مخزن المنتجات المستوردة'),
              Tab(text: 'مخزن الأدوات المستخدمة'),
            ],
          ),
        ),
        const SizedBox(height: 12),
        Expanded(
          child: TabBarView(
            controller: _tabs,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'مخزن الأقمشة',
                          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            fontSize: 22,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      FilledButton.icon(
                        onPressed: () => _addToTab('fabric'),
                        icon: const Icon(Icons.add),
                        label: const Text('إضافة قماش'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: Autocomplete<String>(
                      optionsBuilder: (TextEditingValue value) {
                        final query = value.text.trim();
                        if (query.isEmpty) {
                          return const Iterable<String>.empty();
                        }

                        final lowerQuery = query.toLowerCase();
                        final matches = <String>{};

                        for (final item in data.fabrics) {
                          final name = item.name.toLowerCase();
                          final code = item.code.toLowerCase();
                          final color = (item.color ?? '').toLowerCase();

                          if (name.contains(lowerQuery) || code.contains(lowerQuery) || color.contains(lowerQuery)) {
                            if (item.name.isNotEmpty) {
                              matches.add(item.name);
                            }
                            if (item.code.isNotEmpty) {
                              matches.add(item.code);
                            }
                          }
                        }

                        return matches
                            .where((option) => option.toLowerCase().contains(lowerQuery))
                            .take(10);
                      },
                      onSelected: (value) {
                        _fabricSearchController.text = value;
                        setState(() {});
                      },
                      fieldViewBuilder: (context, controller, focusNode, onFieldSubmitted) {
                        return TextField(
                          controller: _fabricSearchController,
                          focusNode: focusNode,
                          onChanged: (_) => setState(() {}),
                          onSubmitted: (_) => setState(() {}),
                          decoration: InputDecoration(
                            hintText: 'ابحث بالكود أو نوع القماش',
                            prefixIcon: const Icon(Icons.search),
                            border: const OutlineInputBorder(),
                            isDense: true,
                          ),
                        );
                      },
                      optionsViewBuilder: (context, onSelected, options) {
                        final items = options.toList();
                        return Align(
                          alignment: Alignment.topLeft,
                          child: Material(
                            elevation: 4,
                            borderRadius: BorderRadius.circular(12),
                            child: ConstrainedBox(
                              constraints: const BoxConstraints(maxHeight: 220, maxWidth: 440),
                              child: ListView.builder(
                                padding: EdgeInsets.zero,
                                itemCount: items.length,
                                itemBuilder: (context, index) {
                                  final option = items[index];
                                  return ListTile(
                                    dense: true,
                                    title: Text(option),
                                    onTap: () => onSelected(option),
                                  );
                                },
                              ),
                            ),
                          ),
                        );
                      },
                    ),
                  ),
                  Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: [
                      _StatMetric(icon: Icons.layers_outlined, label: 'إجمالي الأصناف', value: '${visibleFabrics.length}'),
                      _StatMetric(icon: Icons.inventory_2_outlined, label: 'الرصيد الحالي', value: quantity(visibleFabrics.fold(0.0, (sum, item) => sum + item.current))),
                      _StatMetric(icon: Icons.check_circle_outline, label: 'المتاح', value: quantity(visibleFabrics.fold(0.0, (sum, item) => sum + item.available))),
                    ].map(
                      (metric) => SizedBox(
                        width: 380,
                        child: Card(
                          child: Padding(
                            padding: const EdgeInsets.all(5),
                            child: Row(
                              children: [
                                CircleAvatar(
                                  radius: 18,
                                  backgroundColor: Theme.of(context).colorScheme.primaryContainer,
                                  child: Icon(metric.icon, color: Theme.of(context).colorScheme.onPrimaryContainer),
                                ),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        metric.label,
                                        style: Theme.of(context).textTheme.labelMedium?.copyWith(fontSize: 18),
                                      ),
                                      Text(
                                        metric.value,
                                        style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                          fontSize: 18,
                                          fontWeight: FontWeight.w800,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ).toList(),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${visibleFabrics.length} صنف',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 4),
                  Expanded(
                    child: visibleFabrics.isEmpty
                        ? const Center(child: Text('لا توجد عناصر في هذا المخزن حاليًا.'))
                        : ListView.separated(
                            itemCount: visibleFabrics.length,
                            separatorBuilder: (_, __) => const SizedBox(height: 4),
                            itemBuilder: (context, index) {
                              final item = visibleFabrics[index];
                              final yardPrice = item.price;
                              final inchPrice = item.price == null ? null : item.price! / 36;

                              return Card(
                                margin: const EdgeInsets.symmetric(vertical: 2),
                                child: Padding(
                                  padding: const EdgeInsets.all(10),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Row(
                                        children: [
                                          Expanded(
                                            child: Text(
                                              item.name,
                                              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                                fontSize: 16,
                                                fontWeight: FontWeight.w700,
                                              ),
                                            ),
                                          ),
                                          IconButton(
                                            tooltip: 'حذف المنتج',
                                            onPressed: () => _deleteFabricAt(data.fabrics.indexOf(item)),
                                            icon: const Icon(Icons.delete_outline),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: 2),
                                      LayoutBuilder(
                                        builder: (context, constraints) {
                                          final spacing = 2.0;
                                          final columns = constraints.maxWidth < 520
                                              ? 4
                                              : constraints.maxWidth < 860
                                                  ? 10
                                                  : 12;
                                          final width = (constraints.maxWidth - (spacing * (columns - 1))) / columns;
                                          final metrics = [
                                            _fabricInfoCard(context, 'كود القماش', item.code.isEmpty ? 'غير محدد' : item.code),
                                            _fabricInfoCard(context, 'رقم الكتالوج', item.catalogNumber?.isNotEmpty == true ? item.catalogNumber! : 'غير محدد'),
                                            _fabricInfoCard(context, 'اسم المورد', item.supplierName ?? 'غير محدد'),
                                            _fabricInfoCard(context, 'نوع القماش', item.name),
                                            _fabricInfoCard(context, 'لون القماش', item.color ?? 'غير محدد'),
                                            _fabricInfoCard(context, 'الكمية المدخلة بالياردة', quantity(item.current)),
                                            _fabricInfoCard(context, 'الكمية المتوفرة بالياردة ', quantity(item.available)),
                                            _fabricInfoCard(context, 'عدد البوصات المتوفرة', quantity(item.available * 36)),
                                            _fabricInfoCard(context, 'الكمية المحجوزة', quantity(item.reserved)),
                                            _fabricInfoCard(context, 'سعر الياردة', yardPrice == null ? '-' : money(yardPrice)),
                                            _fabricInfoCard(context, 'سعر البوصة', inchPrice == null ? '-' : money(inchPrice)),
                                          ];

                                          return Wrap(
                                            spacing: spacing,
                                            runSpacing: spacing,
                                            children: metrics
                                                .map((metric) => SizedBox(width: width.clamp(90.0, 150.0), child: metric))
                                                .toList(),
                                          );
                                        },
                                      ),
                                    ],
                                  ),
                                ),
                              );
                            },
                          ),
                  ),
                ],
              ),
              _InventoryTabSection(
                title: 'مخزن المنتجات الجاهزة',
                subtitle: '${data.readyMade.length} منتج',
                addLabel: 'إضافة منتج',
                onAdd: () => _addToTab('ready'),
                stats: [
                  _StatMetric(icon: Icons.checkroom_outlined, label: 'إجمالي المنتجات', value: '${data.readyMade.length}'),
                  _StatMetric(icon: Icons.attach_money_outlined, label: 'متاح للبيع', value: '${data.readyMade.where((item) => item.status == 'AvailableForSale').length}'),
                  _StatMetric(icon: Icons.sell_outlined, label: 'متوسط السعر', value: money(_readyMadeAveragePrice)),
                ],
                items: data.readyMade
                    .map(
                      (item) => InventoryListItem(
                        title: item.product,
                        subtitle: '${statusLabel(item.status)} • ${item.productTypeName}',
                        value: money(item.price),
                        status: item.status,
                        onTap: () => _showReadyMadeDetails(item),
                        trailing: IconButton(
                          tooltip: 'حذف المنتج',
                          onPressed: () => _deleteReadyMadeAt(data.readyMade.indexOf(item)),
                          icon: const Icon(Icons.delete_outline),
                        ),
                      ),
                    )
                    .toList(),
              ),
              _InventoryTabSection(
                title: 'مخزن المنتجات المستوردة',
                subtitle: '${data.imported.length} منتج',
                addLabel: 'إضافة مستورد',
                onAdd: () => _addToTab('imported'),
                stats: [
                  _StatMetric(icon: Icons.shopping_bag_outlined, label: 'إجمالي المنتجات', value: '${data.imported.length}'),
                  _StatMetric(icon: Icons.numbers_outlined, label: 'إجمالي الكمية', value: quantity(data.importedQuantity)),
                  _StatMetric(icon: Icons.paid_outlined, label: 'إجمالي المشتريات', value: money(data.imported.fold<double>(0.0, (sum, item) => sum + item.purchasePrice))),
                ],
                items: data.imported
                    .map(
                      (item) => InventoryListItem(
                        title: item.product,
                        subtitle: '${item.productType} • ${item.unit}',
                        value: '${quantity(item.quantity)} ${item.unit}',
                        trailing: IconButton(
                          tooltip: 'حذف المنتج',
                          onPressed: () => _deleteImportedAt(data.imported.indexOf(item)),
                          icon: const Icon(Icons.delete_outline),
                        ),
                      ),
                    )
                    .toList(),
              ),
              _InventoryTabSection(
                title: 'مخزن الأدوات المستخدمة',
                subtitle: '${data.tools.length} أداة',
                addLabel: 'إضافة أداة',
                onAdd: () => _addToTab('tools'),
                stats: [
                  _StatMetric(icon: Icons.build_circle_outlined, label: 'إجمالي الأدوات', value: '${data.tools.length}'),
                  _StatMetric(icon: Icons.inventory_2_outlined, label: 'الرصيد الكلي', value: quantity(data.tools.fold<double>(0.0, (sum, item) => sum + item.quantity))),
                  _StatMetric(icon: Icons.fact_check_outlined, label: 'النوع', value: data.tools.isEmpty ? '—' : data.tools.first.category),
                ],
                items: data.tools
                    .map(
                      (item) => InventoryListItem(
                        title: item.name,
                        subtitle: '${item.category} • ${item.code}',
                        value: '${quantity(item.quantity)} ${item.unit}',
                        trailing: IconButton(
                          tooltip: 'حذف المنتج',
                          onPressed: () => _deleteToolAt(data.tools.indexOf(item)),
                          icon: const Icon(Icons.delete_outline),
                        ),
                      ),
                    )
                    .toList(),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _InventoryTabSection extends StatelessWidget {
  const _InventoryTabSection({
    required this.title,
    required this.subtitle,
    required this.addLabel,
    required this.onAdd,
    required this.stats,
    required this.items,
  });

  final String title;
  final String subtitle;
  final String addLabel;
  final VoidCallback onAdd;
  final List<_StatMetric> stats;
  final List<InventoryListItem> items;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                title,
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            FilledButton.icon(
              onPressed: onAdd,
              icon: const Icon(Icons.add),
              label: Text(addLabel),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: stats
              .map(
                (metric) => SizedBox(
                  width: 220,
                  child: Card(
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Row(
                        children: [
                          CircleAvatar(
                            radius: 18,
                            backgroundColor: Theme.of(context).colorScheme.primaryContainer,
                            child: Icon(metric.icon, color: Theme.of(context).colorScheme.onPrimaryContainer),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  metric.label,
                                  style: Theme.of(context).textTheme.labelMedium?.copyWith(fontSize: 13),
                                ),
                                Text(
                                  metric.value,
                                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                                    fontSize: 18,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              )
              .toList(),
        ),
        const SizedBox(height: 12),
        Text(
          subtitle,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 8),
        Expanded(
          child: items.isEmpty
              ? const Center(child: Text('لا توجد عناصر في هذا المخزن حاليًا.'))
              : ListView.separated(
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (context, index) {
                    final item = items[index];
                    return Card(
                      child: ListTile(
                        onTap: item.onTap,
                        title: Text(
                          item.title,
                          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        subtitle: Text(item.subtitle),
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            if (item.status != null) _InventoryStatusBadge(status: item.status!),
                            Text(
                              item.value,
                              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                                fontSize: 15,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(width: 8),
                            item.trailing,
                          ],
                        ),
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}

class _StatMetric {
  const _StatMetric({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;
}

class InventoryListItem {
  const InventoryListItem({
    required this.title,
    required this.subtitle,
    required this.value,
    required this.trailing,
    this.status,
    this.onTap,
  });

  final String title;
  final String subtitle;
  final String value;
  final Widget trailing;
  final String? status;
  final VoidCallback? onTap;
}

class _InventoryStatusBadge extends StatelessWidget {
  const _InventoryStatusBadge({required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      'AvailableForSale' => ('متاح للبيع', Colors.green),
      'Reserved' => ('محجوز', Colors.amber.shade800),
      'Sold' => ('مباع', Colors.red.shade700),
      _ => (statusLabel(status), Theme.of(context).colorScheme.outline),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(5)),
      child: Text(label, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 11)),
    );
  }
}

Widget _fabricInfoCard(BuildContext context, String label, String value) => Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Stack(
          clipBehavior: Clip.none,
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.fromLTRB(8, 12, 8, 9),
              decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.surface,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: Theme.of(context).dividerColor),
              ),
              child: Text(
                value,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                  fontSize: 15,
                ),
              ),
            ),
            Positioned(
              right: 10,
              top: -7,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 4),
                color: Theme.of(context).colorScheme.surface,
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    fontSize: 10,
                  ),
                ),
              ),
            ),
          ],
          ),
      ],
    );

class InventoryData {
  const InventoryData({
    required this.fabrics,
    required this.readyMade,
    required this.imported,
    required this.tools,
  });

  final List<FabricItem> fabrics;
  final List<ReadyMadeItem> readyMade;
  final List<ImportedItem> imported;
  final List<ToolItem> tools;

  double get fabricBalance => fabrics.fold(0.0, (sum, item) => sum + item.current);
  double get importedQuantity => imported.fold(0.0, (sum, item) => sum + item.quantity);
}

class InventoryItemRecord {
  const InventoryItemRecord({
    required this.itemCode,
    required this.itemName,
    required this.category,
    required this.currentQuantity,
    required this.availableQuantity,
    required this.reservedQuantity,
    required this.unit,
  });

  factory InventoryItemRecord.fromJson(Map<String, dynamic> json) => InventoryItemRecord(
        itemCode: json['itemCode']?.toString() ?? '-',
        itemName: json['itemName']?.toString() ?? '-',
        category: json['category']?.toString() ?? '-',
        currentQuantity: (json['currentQuantity'] as num?)?.toDouble() ?? 0,
        availableQuantity: (json['availableQuantity'] as num?)?.toDouble() ?? 0,
        reservedQuantity: (json['reservedQuantity'] as num?)?.toDouble() ?? 0,
        unit: json['unit']?.toString() ?? '',
      );

  final String itemCode;
  final String itemName;
  final String category;
  final double currentQuantity;
  final double availableQuantity;
  final double reservedQuantity;
  final String unit;
}

class FabricItem {
  const FabricItem({
    required this.code,
    required this.name,
    required this.category,
    required this.current,
    required this.available,
    required this.reserved,
    this.color,
    this.unit,
    this.price,
    this.catalogNumber,
    this.sourceTable,
    this.sourceId,
    this.supplierName,
  });

  factory FabricItem.fromInventory(InventoryItemRecord item) => FabricItem(
        code: item.itemCode,
        name: item.itemName,
        category: item.category,
        current: item.currentQuantity,
        available: item.availableQuantity,
        reserved: item.reservedQuantity,
      );

  final String code;
  final String name;
  final String category;
  final double current;
  final double available;
  final double reserved;
  final String? color;
  final String? unit;
  final double? price;
  final String? catalogNumber;
  final String? sourceTable;
  final int? sourceId;
  final String? supplierName;

  double get availableInches => available * 36;
}

class ReadyMadeItem {
  const ReadyMadeItem({
    required this.inventoryProductId,
    required this.trackingCode,
    required this.productTypeId,
    required this.product,
    required this.productType,
    required this.productTypeName,
    required this.fabricCode,
    required this.fabricType,
    required this.fabricColor,
    required this.actualCost,
    required this.consumption,
    required this.fabricCost,
    required this.operatingCost,
    required this.fullCost,
    required this.status,
    required this.price,
    required this.createdAt,
  });

  factory ReadyMadeItem.fromJson(Map<String, dynamic> json) {
    Map<String, dynamic>? pricing;
    final snapshot = json['measurementSnapshot']?.toString();
    if (snapshot != null && snapshot.trim().isNotEmpty) {
      try {
        final decoded = jsonDecode(snapshot);
        if (decoded is Map && decoded['_pricing'] is Map) {
          pricing = (decoded['_pricing'] as Map).cast<String, dynamic>();
        }
      } catch (_) {}
    }
    return ReadyMadeItem(
      inventoryProductId: (json['readyMadeInventoryProductId'] as num?)?.toInt() ?? 0,
      trackingCode: json['trackingCode']?.toString() ?? '-',
      productTypeId: (json['productTypeId'] as num?)?.toInt(),
      product: json['productionName']?.toString() ?? json['pieceType']?.toString() ?? '-',
      productType: json['pieceType']?.toString() ?? '-',
      productTypeName: json['productTypeName']?.toString() ?? json['pieceType']?.toString() ?? '-',
      fabricCode: json['fabricCode']?.toString() ?? '-',
      fabricType: json['fabricType']?.toString() ?? '-',
      fabricColor: json['fabricColor']?.toString() ?? '-',
      actualCost: (json['actualCost'] as num?)?.toDouble(),
      consumption: (pricing?['consumptionPerPiece'] as num?)?.toDouble(),
      fabricCost: (pricing?['fabricCostPerPiece'] as num?)?.toDouble(),
      operatingCost: (pricing?['operationalCostPerPiece'] as num?)?.toDouble(),
      fullCost: (pricing?['fullCostPerPiece'] as num?)?.toDouble(),
      status: json['status']?.toString() ?? '-',
      price: (json['suggestedSellingPrice'] as num?)?.toDouble(),
      createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
    );
  }

  final int inventoryProductId;
  final String trackingCode;
  final int? productTypeId;
  final String product;
  final String productType;
  final String productTypeName;
  final String fabricCode;
  final String fabricType;
  final String fabricColor;
  final double? actualCost;
  final double? consumption;
  final double? fabricCost;
  final double? operatingCost;
  final double? fullCost;
  final String status;
  final double? price;
  final DateTime? createdAt;
}

class ImportedItem {
  const ImportedItem({
    required this.product,
    required this.productType,
    required this.quantity,
    required this.unit,
    required this.purchasePrice,
  });

  factory ImportedItem.fromJson(Map<String, dynamic> json) => ImportedItem(
        product: json['productName']?.toString() ?? '-',
        productType: json['productType']?.toString() ?? '-',
        quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
        unit: json['unit']?.toString() ?? '',
        purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
      );

  final String product;
  final String productType;
  final double quantity;
  final String unit;
  final double purchasePrice;
}

class ToolItem {
  const ToolItem({
    required this.code,
    required this.name,
    required this.category,
    required this.quantity,
    required this.unit,
  });

  factory ToolItem.fromInventory(InventoryItemRecord item) => ToolItem(
        code: item.itemCode,
        name: item.itemName,
        category: item.category,
        quantity: item.currentQuantity,
        unit: item.unit.isEmpty ? 'قطعة' : item.unit,
      );

  final String code;
  final String name;
  final String category;
  final double quantity;
  final String unit;
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.onRetry});

  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_outlined, size: 42),
            const SizedBox(height: 6),
            const Text('تعذر تحميل بيانات المخزون.'),
            const SizedBox(height: 6),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('إعادة المحاولة'),
            ),
          ],
        ),
      );
}

final numberFormat = NumberFormat('#,##0.##');
final moneyFormat = NumberFormat('#,##0.00');

String quantity(double value) => numberFormat.format(value);
String money(double? value) => value == null ? '-' : moneyFormat.format(value);
String statusLabel(String status) => switch (status) {
      'AvailableForSale' => 'متاح للبيع',
  'Reserved' => 'محجوز',
      'Sold' => 'مباع',
      _ => status,
    };