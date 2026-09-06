import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import 'printing/work_card_screen.dart';

class ProductionScreen extends StatefulWidget {
  const ProductionScreen({super.key});
  @override
  State<ProductionScreen> createState() => _ProductionScreenState();
}

class _ProductionScreenState extends State<ProductionScreen>
    with SingleTickerProviderStateMixin {
  final api = ProductionApi();
  late final TabController tabs;
  late Future<ProductionData> future;
  @override
  void initState() {
    super.initState();
    tabs = TabController(length: 5, vsync: this);
    future = api.load();
  }

  @override
  void dispose() {
    tabs.dispose();
    super.dispose();
  }

  void reload() {
    setState(() {
      future = api.load();
    });
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<ProductionData>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done)
          return const Center(child: CircularProgressIndicator());
        if (snapshot.hasError) return ProductionError(onRetry: reload);
        final data = snapshot.data!;
        return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(children: [
                Expanded(
                    child: Text('إدارة الإنتاج',
                        style: Theme.of(context).textTheme.headlineSmall)),
                IconButton(
                    tooltip: 'تحديث',
                    onPressed: reload,
                    icon: const Icon(Icons.refresh))
              ]),
              const SizedBox(height: 12),
              ProductionStatistics(data.dashboard),
              const SizedBox(height: 14),
              TabBar(controller: tabs, isScrollable: true, tabs: const [
                Tab(icon: Icon(Icons.content_cut), text: 'القطع'),
                Tab(icon: Icon(Icons.checkroom_outlined), text: 'إنتاج الجاهز'),
                Tab(icon: Icon(Icons.payments_outlined), text: 'أجور القطعة'),
                Tab(
                    icon: Icon(Icons.qr_code_scanner_outlined),
                    text: 'أجهزة المسح'),
                Tab(icon: Icon(Icons.local_shipping_outlined), text: 'التسليم')
              ]),
              const SizedBox(height: 8),
              Expanded(
                  child: TabBarView(controller: tabs, children: [
                PiecesTab(pieces: data.pieces),
                ReadyMadeTab(
                    orders: data.readyOrders,
                    inventory: data.readyInventory,
                    api: api),
                PieceWagesTab(items: data.wages),
                ScannersTab(scanners: data.scanners, scans: data.scans),
                DeliveryTab(items: data.deliveries)
              ])),
            ]);
      });
}

class ProductionStatistics extends StatelessWidget {
  const ProductionStatistics(this.data, {super.key});
  final Map<String, dynamic> data;
  @override
  Widget build(BuildContext context) {
    final items = [
      ('إجمالي القطع', '${data['totalPieces']}', Icons.content_cut),
      (
        'قيد الإنتاج',
        '${data['inProductionPieces']}',
        Icons.precision_manufacturing_outlined
      ),
      ('جاهزة', '${data['readyPieces']}', Icons.task_alt),
      ('مسلمة', '${data['deliveredPieces']}', Icons.local_shipping_outlined),
      ('المراحل النشطة', '${data['activeStages']}', Icons.route_outlined),
      (
        'الجاهز للبيع',
        '${data['readyForSaleProducts']}',
        Icons.storefront_outlined
      ),
      ('القطع المتأخرة', '${data['delayedPieces']}', Icons.schedule_outlined)
    ];
    return SizedBox(
        height: 92,
        child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: items.length,
            separatorBuilder: (_, __) => const SizedBox(width: 10),
            itemBuilder: (context, index) {
              final item = items[index];
              return SizedBox(
                  width: 165,
                  child: Card(
                      child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Row(children: [
                            Icon(item.$3),
                            const SizedBox(width: 10),
                            Expanded(
                                child: Column(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                  Text(item.$1, maxLines: 1),
                                  Text(item.$2,
                                      style: Theme.of(context)
                                          .textTheme
                                          .titleLarge
                                          ?.copyWith(
                                              fontWeight: FontWeight.bold))
                                ]))
                          ]))));
            }));
  }
}

class PiecesTab extends StatefulWidget {
  const PiecesTab({required this.pieces, super.key});
  final List<Map<String, dynamic>> pieces;
  @override
  State<PiecesTab> createState() => _PiecesTabState();
}

class _PiecesTabState extends State<PiecesTab> {
  final search = TextEditingController();
  String filter = 'all';
  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  bool matches(Map<String, dynamic> p) {
    final status = p['pieceStatus'].toString();
    final q = search.text.trim().toLowerCase();
    final category = filter == 'all' ||
        (filter == 'ready' && status == 'Ready') ||
        (filter == 'delivered' && status == 'Delivered') ||
        (filter == 'production' &&
            !['New', 'Ready', 'Delivered'].contains(status));
    return category &&
        (q.isEmpty ||
            p['pieceNumber'].toString().contains(q) ||
            p['trackingCode'].toString().toLowerCase().contains(q));
  }

  @override
  Widget build(BuildContext context) {
    final items = widget.pieces.where(matches).toList();
    return Column(children: [
      Wrap(spacing: 10, runSpacing: 8, children: [
        SizedBox(
            width: 260,
            child: TextField(
                controller: search,
                onChanged: (_) => setState(() {}),
                decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'رقم القطعة أو كود التتبع',
                    border: OutlineInputBorder()))),
        SegmentedButton<String>(segments: const [
          ButtonSegment(value: 'all', label: Text('الكل')),
          ButtonSegment(value: 'production', label: Text('قيد الإنتاج')),
          ButtonSegment(value: 'ready', label: Text('جاهزة')),
          ButtonSegment(value: 'delivered', label: Text('مسلمة'))
        ], selected: {
          filter
        }, onSelectionChanged: (value) => setState(() => filter = value.first))
      ]),
      const SizedBox(height: 8),
      Expanded(
          child: items.isEmpty
              ? const Center(child: Text('لا توجد قطع مطابقة.'))
              : ListView.separated(
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 6),
                  itemBuilder: (context, index) {
                    final p = items[index];
                    return Card(
                        child: ListTile(
                            leading: CircleAvatar(
                                child: Text('${p['pieceNumber']}')),
                            title: Text('${p['pieceType']}'),
                            subtitle: Text(
                                '${p['trackingCode']}  •  ${p['pieceStatus']}'),
                            trailing: const Icon(Icons.chevron_left),
                            onTap: () => AppNavigation.push(
                                context,
                                (_) => ProductionPieceDetailsScreen(
                                    pieceId: p['pieceId'] as int))));
                  }))
    ]);
  }
}

class ProductionPieceDetailsScreen extends StatefulWidget {
  const ProductionPieceDetailsScreen({required this.pieceId, super.key});
  final int pieceId;
  @override
  State<ProductionPieceDetailsScreen> createState() =>
      _ProductionPieceDetailsScreenState();
}

class _ProductionPieceDetailsScreenState
    extends State<ProductionPieceDetailsScreen> {
  final api = ProductionApi();
  late Future<Map<String, dynamic>> future;
  @override
  void initState() {
    super.initState();
    future = load();
  }

  Future<Map<String, dynamic>> load() => api
      .get('/production/pieces/${widget.pieceId}/work-card')
      .then((value) => value as Map<String, dynamic>);
  void reload() {
    setState(() {
      future = load();
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
      appBar: AppBar(title: const Text('تفاصيل القطعة'), actions: [
        IconButton(
            tooltip: 'تحديث',
            onPressed: reload,
            icon: const Icon(Icons.refresh))
      ]),
      body: FutureBuilder<Map<String, dynamic>>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done)
              return const Center(child: CircularProgressIndicator());
            if (snapshot.hasError)
              return ProductionError(
                  onRetry: reload,
                  message: snapshot.error is ProductionApiException &&
                          ((snapshot.error as ProductionApiException)
                                  .statusCode ==
                              404)
                      ? 'خادم الإنتاج الحالي لا يوفر تفاصيل بطاقة القطعة. يلزم تشغيل إصدار Backend المطابق للكود الحالي.'
                      : 'تعذر تحميل تفاصيل القطعة.');
            final card = snapshot.data!;
            final measurements =
                parseMeasurements(card['measurementSnapshot'] as String?);
            final history =
                (card['trackingHistory'] as List).cast<Map<String, dynamic>>();
            final notes = [card['notes1'], card['notes2']]
                .whereType<String>()
                .where((value) => value.trim().isNotEmpty)
                .join('\n');
            return ListView(padding: const EdgeInsets.all(20), children: [
              Wrap(spacing: 10, runSpacing: 10, children: [
                DetailField('كود العميل', card['customerCode']),
                DetailField('اسم العميل', card['customerName']),
                DetailField('الهاتف', card['phoneNumber']),
                DetailField('رقم الطلب', card['orderNumber']),
                DetailField('تاريخ التسليم', formatDate(card['deliveryDate'])),
                DetailField('رقم القطعة', card['pieceNumber']),
                DetailField('كود التتبع', card['trackingCode']),
                DetailField('نوع القطعة', card['pieceType']),
                DetailField('الحالة', card['pieceStatus']),
                DetailField('نوع القماش', card['fabricType']),
                DetailField('لون القماش', card['fabricColor'])
              ]),
              const SizedBox(height: 16),
              Text('القياسات', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 8),
              MeasurementsView(values: measurements),
              const SizedBox(height: 16),
              Text('الملاحظات', style: Theme.of(context).textTheme.titleLarge),
              Text(notes.isEmpty ? '-' : notes),
              const SizedBox(height: 16),
              Text('مسار الإنتاج',
                  style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 8),
              ProductionTimeline(events: history),
              const SizedBox(height: 14),
              FilledButton.icon(
                  onPressed: () => AppNavigation.push(context,
                    (_) => WorkCardPreviewScreen(pieceId: widget.pieceId)),
                  icon: const Icon(Icons.badge_outlined),
                  label: const Text('فتح بطاقة التشغيل'))
            ]);
          }));
}

class ProductionTimeline extends StatelessWidget {
  const ProductionTimeline({required this.events, super.key});
  final List<Map<String, dynamic>> events;
  static const stages = [
    'Printing',
    'Cutting',
    'Sewing',
    'Buttons',
    'Ironing',
    'Quality',
    'Assembly',
    'Ready',
    'Delivery'
  ];
  @override
  Widget build(BuildContext context) {
    final byStage = {
      for (final event in events) event['stage'].toString(): event
    };
    return Column(children: [
      for (final stage in stages)
        if (byStage.containsKey(stage))
          ListTile(
              leading: const Icon(Icons.check_circle, color: Colors.green),
              title: Text(stage),
              subtitle: Text(
                  '${byStage[stage]!['status']}  •  ${formatDateTime(byStage[stage]!['eventTime'])}'),
              trailing:
                  Text(byStage[stage]!['trackingCode']?.toString() ?? '-'))
    ]);
  }
}

class ReadyMadeTab extends StatelessWidget {
  const ReadyMadeTab(
      {required this.orders,
      required this.inventory,
      required this.api,
      super.key});
  final List<Map<String, dynamic>> orders;
  final List<Map<String, dynamic>> inventory;
  final ProductionApi api;
  @override
  Widget build(BuildContext context) => orders.isEmpty
      ? const Center(child: Text('لا توجد أوامر إنتاج جاهز.'))
      : ListView.separated(
          itemCount: orders.length,
          separatorBuilder: (_, __) => const SizedBox(height: 6),
          itemBuilder: (context, index) {
            final order = orders[index];
            return Card(
                child: ExpansionTile(
                    title: Text(
                        '${order['productionOrderNumber']} - ${order['productionName']}'),
                    subtitle: Text('الحالة: ${order['status']}'),
                    children: [
                  FutureBuilder<List<Map<String, dynamic>>>(
                      future: api.readyItems(
                          order['readyMadeProductionOrderId'] as int),
                      builder: (context, snapshot) {
                        if (snapshot.connectionState != ConnectionState.done)
                          return const Padding(
                              padding: EdgeInsets.all(16),
                              child: CircularProgressIndicator());
                        if (snapshot.hasError)
                          return const ListTile(
                              title: Text('تعذر تحميل بنود الأمر.'));
                        return Column(
                            children: snapshot.data!
                                .map((item) => ListTile(
                                      title: Text(
                                          '${item['pieceType']}  •  الكمية ${item['quantity']}'),
                                      subtitle: Text(
                                          'القماش: ${item['fabricType'] ?? '-'}  •  الحالة: ${item['pieceStatus']}'),
                                      trailing: const Icon(Icons.chevron_left),
                                      onTap: () => AppNavigation.push(
                                          context,
                                          (_) => ReadyMadeDetailsScreen(
                                              item: item,
                                              inventory: inventory,
                                              api: api)),
                                    ))
                                .toList());
                      })
                ]));
          });
}

class ReadyMadeDetailsScreen extends StatelessWidget {
  const ReadyMadeDetailsScreen(
      {required this.item,
      required this.inventory,
      required this.api,
      super.key});
  final Map<String, dynamic> item;
  final List<Map<String, dynamic>> inventory;
  final ProductionApi api;
  @override
  Widget build(BuildContext context) => Scaffold(
      appBar: AppBar(title: Text('${item['pieceType']}')),
      body: FutureBuilder<List<Map<String, dynamic>>>(
          future:
              api.readyPieces(item['readyMadeProductionOrderItemId'] as int),
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done)
              return const Center(child: CircularProgressIndicator());
            if (snapshot.hasError)
              return const ProductionError(
                  message: 'تعذر تحميل قطع الإنتاج الجاهز.');
            final pieces = snapshot.data!;
            return ListView.separated(
                padding: const EdgeInsets.all(20),
                itemCount: pieces.length,
                separatorBuilder: (_, __) => const SizedBox(height: 8),
                itemBuilder: (context, index) {
                  final piece = pieces[index];
                  Map<String, dynamic>? stock;
                  for (final row in inventory) {
                    if (row['readyMadeProductionOrderPieceInstanceId'] ==
                        piece['readyMadeProductionOrderPieceInstanceId']) {
                      stock = row;
                      break;
                    }
                  }
                  return Card(
                      child: Padding(
                          padding: const EdgeInsets.all(14),
                          child: Wrap(spacing: 18, runSpacing: 10, children: [
                            DetailField('رقم القطعة', piece['pieceNumber']),
                            DetailField('كود التتبع', piece['trackingCode']),
                            DetailField('حالة القطعة', piece['pieceStatus']),
                            DetailField('تاريخ الإنشاء',
                                formatDate(piece['createdAt'])),
                            DetailField(
                                'التكلفة الفعلية', stock?['actualCost']),
                            DetailField('سعر البيع المقترح',
                                stock?['suggestedSellingPrice']),
                            DetailField('حالة المخزون',
                                stock?['status'] ?? 'لم تدخل المخزون')
                          ])));
                });
          }));
}

class PieceWagesTab extends StatelessWidget {
  const PieceWagesTab({required this.items, super.key});
  final List<Map<String, dynamic>> items;
  @override
  Widget build(BuildContext context) => items.isEmpty
      ? const Center(child: Text('لا توجد أجور قطع.'))
      : ListView(children: [
          DataTable(
              columns: const [
                DataColumn(label: Text('القطعة')),
                DataColumn(label: Text('المرحلة')),
                DataColumn(label: Text('العامل')),
                DataColumn(label: Text('الأجر')),
                DataColumn(label: Text('التاريخ'))
              ],
              rows: items
                  .map((entry) => DataRow(cells: [
                        DataCell(Text('${entry['pieceId']}')),
                        DataCell(Text('${entry['stage']}')),
                        DataCell(
                            Text(entry['employeeCode']?.toString() ?? '-')),
                        DataCell(Text('${entry['totalWage']}')),
                        DataCell(Text(formatDate(entry['createdAt'])))
                      ]))
                  .toList())
        ]);
}

class ScannersTab extends StatelessWidget {
  const ScannersTab({required this.scanners, required this.scans, super.key});
  final List<Map<String, dynamic>> scanners;
  final List<Map<String, dynamic>> scans;
  @override
  Widget build(BuildContext context) => ListView(children: [
        Text('أجهزة المسح', style: Theme.of(context).textTheme.titleLarge),
        ...scanners.map((scanner) => Card(
            child: ListTile(
                leading: Icon(scanner['isActive'] == true
                    ? Icons.qr_code_scanner
                    : Icons.block),
                title: Text('${scanner['scannerName']}'),
                subtitle: Text(
                    '${scanner['scannerCode']}  •  ${scanner['isActive'] == true ? 'نشط' : 'غير نشط'}')))),
        const SizedBox(height: 14),
        Text('آخر عمليات المسح', style: Theme.of(context).textTheme.titleLarge),
        ...scans.map((scan) => ListTile(
            leading: const Icon(Icons.history),
            title: Text('كود ${scan['trackingCode'] ?? '-'}'),
            subtitle: Text(formatDateTime(scan['scanTime']))))
      ]);
}

class DeliveryTab extends StatefulWidget {
  const DeliveryTab({required this.items, super.key});
  final List<Map<String, dynamic>> items;
  @override
  State<DeliveryTab> createState() => _DeliveryTabState();
}

class _DeliveryTabState extends State<DeliveryTab> {
  final search = TextEditingController();
  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final query = search.text.trim().toLowerCase();
    final rows = widget.items
        .where((entry) =>
            query.isEmpty ||
            entry['orderNumber'].toString().toLowerCase().contains(query) ||
            (entry['customerName']?.toString().toLowerCase().contains(query) ??
                false))
        .toList();
    return Column(children: [
      TextField(
          controller: search,
          onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(
              prefixIcon: Icon(Icons.search),
              labelText: 'بحث بالطلب أو العميل',
              border: OutlineInputBorder())),
      const SizedBox(height: 8),
      Expanded(
          child: ListView.separated(
              itemCount: rows.length,
              separatorBuilder: (_, __) => const SizedBox(height: 5),
              itemBuilder: (context, index) {
                final entry = rows[index];
                return Card(
                    child: ListTile(
                        title: Text(
                            '${entry['orderNumber']} - ${entry['customerName'] ?? '-'}'),
                        subtitle: Text(
                            '${entry['customerCode'] ?? '-'}  •  ${entry['phoneNumber'] ?? '-'}'),
                        trailing: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            crossAxisAlignment: CrossAxisAlignment.end,
                            children: [
                              Text('${entry['orderStatus']}'),
                              Text(formatDate(entry['deliveryDate']),
                                  style: Theme.of(context).textTheme.bodySmall)
                            ])));
              }))
    ]);
  }
}

class DetailField extends StatelessWidget {
  const DetailField(this.label, this.value, {super.key});
  final String label;
  final Object? value;
  @override
  Widget build(BuildContext context) => SizedBox(
      width: 190,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: Theme.of(context).textTheme.labelMedium),
        SelectableText(value?.toString() ?? '-',
            style: Theme.of(context).textTheme.titleSmall)
      ]));
}

class MeasurementsView extends StatelessWidget {
  const MeasurementsView({required this.values, super.key});
  final Map<String, dynamic> values;
  @override
  Widget build(BuildContext context) => values.isEmpty
      ? const Text('لا توجد قياسات محفوظة.')
      : Wrap(
          spacing: 8,
          runSpacing: 8,
          children: values.entries
              .where((entry) => !entry.key.startsWith('_'))
              .map((entry) => Chip(label: Text('${entry.key}: ${entry.value}')))
              .toList());
}

class ProductionError extends StatelessWidget {
  const ProductionError(
      {this.onRetry, this.message = 'تعذر تحميل بيانات الإنتاج.', super.key});
  final VoidCallback? onRetry;
  final String message;
  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 42),
        const SizedBox(height: 10),
        Text(message, textAlign: TextAlign.center),
        if (onRetry != null) ...[
          const SizedBox(height: 10),
          FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('إعادة المحاولة'))
        ]
      ]));
}

class ProductionData {
  const ProductionData(
      {required this.dashboard,
      required this.pieces,
      required this.readyOrders,
      required this.readyInventory,
      required this.wages,
      required this.scanners,
      required this.scans,
      required this.deliveries});
  final Map<String, dynamic> dashboard;
  final List<Map<String, dynamic>> pieces,
      readyOrders,
      readyInventory,
      wages,
      scanners,
      scans,
      deliveries;
}

class ProductionApiException implements Exception {
  const ProductionApiException(this.statusCode);
  final int statusCode;
}

class ProductionApi {
  static const baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  Future<dynamic> get(String path) async {
    final response = await http.get(Uri.parse('$baseUrl$path'));
    if (response.statusCode < 200 || response.statusCode >= 300)
      throw ProductionApiException(response.statusCode);
    return jsonDecode(response.body);
  }

  Future<List<Map<String, dynamic>>> list(String path) async =>
      (await get(path) as List).cast<Map<String, dynamic>>();
  Future<ProductionData> load() async {
    final values = await Future.wait([
      get('/production/dashboard'),
      list('/production/pieces'),
      list('/production/readymade-orders'),
      list('/inventory/readymade'),
      list('/payroll/piece-wages'),
      list('/production/scanners'),
      list('/production/live-scan'),
      list('/production/deliveries')
    ]);
    return ProductionData(
        dashboard: values[0] as Map<String, dynamic>,
        pieces: values[1] as List<Map<String, dynamic>>,
        readyOrders: values[2] as List<Map<String, dynamic>>,
        readyInventory: values[3] as List<Map<String, dynamic>>,
        wages: values[4] as List<Map<String, dynamic>>,
        scanners: values[5] as List<Map<String, dynamic>>,
        scans: values[6] as List<Map<String, dynamic>>,
        deliveries: values[7] as List<Map<String, dynamic>>);
  }

  Future<List<Map<String, dynamic>>> readyItems(int id) =>
      list('/production/readymade-orders/$id/items');
  Future<List<Map<String, dynamic>>> readyPieces(int id) =>
      list('/production/readymade-order-items/$id/pieces');
}

Map<String, dynamic> parseMeasurements(String? source) {
  if (source == null || source.trim().isEmpty) return {};
  try {
    final value = jsonDecode(source);
    return value is Map<String, dynamic> ? value : {};
  } catch (_) {
    return {};
  }
}

String formatDate(Object? value) {
  if (value == null) return '-';
  final date = DateTime.tryParse(value.toString());
  return date == null
      ? value.toString()
      : DateFormat('yyyy/MM/dd').format(date);
}

String formatDateTime(Object? value) {
  if (value == null) return '-';
  final date = DateTime.tryParse(value.toString());
  return date == null
      ? value.toString()
      : DateFormat('yyyy/MM/dd HH:mm').format(date);
}
