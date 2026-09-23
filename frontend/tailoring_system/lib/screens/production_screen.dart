import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../core/measurement_snapshot.dart';
import '../core/ui_palette.dart';
import '../services/theme_state.dart';
import 'printing/work_card_screen.dart';

String buildProductionWarningText(int unstartedCount) =>
    'هناك $unstartedCount قطع لم تدخل خط الإنتاج';

int countUnstartedPiecesForProductionWarning(
    List<Map<String, dynamic>> pieces) {
  return pieces.where((piece) {
    final status = _normalizeProductionPieceStatus(
      (piece['pieceStatus'] ?? '').toString(),
    );
    return status == 'جديد';
  }).length;
}

String _normalizeProductionPieceStatus(String value) {
  final normalized = value.trim();
  if (normalized.isEmpty) return 'جديد';

  final lowered = normalized.toLowerCase();
  if (['new', 'جديد'].contains(lowered)) return 'جديد';
  if ([
    'printing',
    'cutting',
    'fabricprep',
    'sewing',
    'buttons',
    'ironing',
    'quality',
    'assembly',
    'inproduction',
    'inprogress',
    'production',
    'تمت الطباعة',
    'قيد الإنتاج',
  ].contains(lowered)) {
    return 'قيد الإنتاج';
  }
  if (['ready', 'readyfordelivery', 'جاهز', 'جاهز للتسليم'].contains(lowered)) {
    return 'جاهز للتسليم';
  }
  if (['delivered', 'تم التسليم'].contains(lowered)) return 'تم التسليم';
  return normalized;
}

const _productionStages = {
  'Printing',
  'FabricPrep',
  'Cutting',
  'Sewing',
  'Buttons',
  'Ironing',
  'Quality',
  'Assembly',
};

String _canonicalProductionStage(String value) {
  final normalized = value.trim().toLowerCase().replaceAll(' ', '');
  for (final stage in _productionStages) {
    if (stage.toLowerCase() == normalized) return stage;
  }
  return value.trim();
}

class ProductionOrderMetrics {
  const ProductionOrderMetrics({
    required this.totalPieces,
    required this.notStartedPieces,
    required this.startedPieces,
    required this.completedPieces,
  });

  final int totalPieces;
  final int notStartedPieces;
  final int startedPieces;
  final int completedPieces;

  String get status {
    if (totalPieces == 0 || startedPieces == 0) return 'جديد';
    if (completedPieces == totalPieces) return 'جاهز للتسليم';
    return 'قيد الإنتاج';
  }
}

ProductionOrderMetrics calculateProductionOrderMetrics(
  List<Map<String, dynamic>> pieces, {
  Map<int, List<String>> routesByProductTypeId = const {},
}) {
  var started = 0;
  var completed = 0;
  var notStarted = 0;
  for (final piece in pieces) {
    final stage =
        _canonicalProductionStage((piece['pieceStatus'] ?? '').toString());
    final isProductionStage = _productionStages.contains(stage);
    final isReady = [
      'Ready',
      'ReadyForDelivery',
      'Delivered',
      'جاهز',
      'جاهز للتسليم',
      'تم التسليم'
    ].any((value) => value.toLowerCase() == stage.toLowerCase());
    if (isProductionStage) started++;

    final productTypeId =
        int.tryParse((piece['productTypeId'] ?? '').toString()) ?? 0;
    final route = routesByProductTypeId[productTypeId] ?? const <String>[];
    final lastStage =
        route.isEmpty ? '' : _canonicalProductionStage(route.last);
    final isCompleted = isReady || (lastStage.isNotEmpty && stage == lastStage);
    if (isCompleted) {
      completed++;
    } else if (!isProductionStage) {
      notStarted++;
    }
  }
  return ProductionOrderMetrics(
    totalPieces: pieces.length,
    notStartedPieces: notStarted,
    startedPieces: started,
    completedPieces: completed,
  );
}

bool _hasProductionStarted(List<Map<String, dynamic>> pieces) {
  return pieces.any((piece) {
    final status = _normalizeProductionPieceStatus(
      (piece['pieceStatus'] ?? '').toString(),
    );
    return status == 'قيد الإنتاج' ||
        status == 'جاهز للتسليم' ||
        status == 'تم التسليم';
  });
}

String resolveProductionOrderStatusFromPieces(
    List<Map<String, dynamic>> pieces) {
  if (pieces.isEmpty) return 'جديد';

  final statuses = pieces
      .map((piece) => _normalizeProductionPieceStatus(
            (piece['pieceStatus'] ?? '').toString(),
          ))
      .toSet();

  if (statuses.contains('تم التسليم')) return 'تم التسليم';
  if (statuses.contains('جاهز للتسليم')) return 'جاهز للتسليم';
  if (_hasProductionStarted(pieces) &&
      countUnstartedPiecesForProductionWarning(pieces) > 0) {
    return 'قيد الإنتاج';
  }
  if (statuses.contains('قيد الإنتاج')) return 'قيد الإنتاج';
  return 'جديد';
}

class ProductionScreen extends StatefulWidget {
  const ProductionScreen({
    this.api,
    this.refreshInterval = Duration.zero,
    this.themeState,
    super.key,
  });

  final ProductionApi? api;
  final Duration refreshInterval;
  final ThemeState? themeState;

  @override
  State<ProductionScreen> createState() => _ProductionScreenState();
}

class _ProductionScreenState extends State<ProductionScreen>
    with SingleTickerProviderStateMixin {
  late final ProductionApi _api;
  late final TabController tabs;
  Timer? _timer;
  bool _isReloading = false;
  ProductionData? _lastGoodData;
  late Future<ProductionData> future;
  bool _fallbackDarkMode = false;

  ThemeMode get _effectiveThemeMode {
    return widget.themeState?.themeMode ??
        (_fallbackDarkMode ? ThemeMode.dark : ThemeMode.light);
  }

  @override
  void initState() {
    super.initState();
    _api = widget.api ?? ProductionApi();
    tabs = TabController(length: 3, vsync: this);
    future = _api.load();

    if (widget.refreshInterval > Duration.zero) {
      _timer = Timer.periodic(widget.refreshInterval, (_) => reload());
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    tabs.dispose();
    super.dispose();
  }

  void reload() {
    if (_isReloading) return;
    _isReloading = true;

    final nextFuture = _api.load();
    setState(() {
      future = nextFuture.then((data) {
        if (!mounted) return data;
        _lastGoodData = data;
        return data;
      });
    });

    nextFuture.then((data) {
      if (!mounted) return;
      setState(() {
        _lastGoodData = data;
      });
    }).catchError((Object _) {
      // Keep the last successful data visible during a silent background refresh.
    }).whenComplete(() {
      if (!mounted) return;
      setState(() => _isReloading = false);
    });
  }

  Color _cardSurface(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? const Color(0xFF1A2B37) : const Color(0xFFF2F6F9);
  }

  Color _mutedSurface(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? const Color(0xFF0E181F) : const Color(0xFFFFFFFF);
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return FutureBuilder<ProductionData>(
      future: future,
      builder: (context, snapshot) {
        final data = snapshot.data ?? _lastGoodData;

        if (snapshot.hasError && data == null) {
          return ProductionError(onRetry: reload);
        }

        if (data == null &&
            snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }

        final activeData = data ??
            ProductionData(
              dashboard: {},
              pieces: [],
              orders: [],
              orderItems: [],
              readyPieces: [],
              readyOrders: [],
              readyInventory: [],
              wages: [],
              scanners: [],
              scans: [],
              deliveries: [],
            );

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 10, 12, 0),
              child: Row(
                children: [
                  Expanded(
                    child: TabBar(
                      controller: tabs,
                      isScrollable: false,
                      labelPadding: const EdgeInsets.symmetric(horizontal: 10),
                      indicatorColor: UiPalette.primaryBlue,
                      labelColor: Theme.of(context).colorScheme.onSurface,
                      unselectedLabelColor: Theme.of(context)
                          .colorScheme
                          .onSurface
                          .withValues(alpha: 0.7),
                      indicatorWeight: 3,
                      dividerColor: Theme.of(context).dividerColor,
                      tabs: const [
                        Tab(text: 'إنتاج طلبات التفصيل'),
                        Tab(text: 'إجمالي عدد القطع'),
                        Tab(text: 'الإنتاج الجاهز للبيع'),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton(
                    tooltip: isDark ? 'الوضع الفاتح' : 'الوضع الداكن',
                    onPressed: () {
                      if (widget.themeState != null) {
                        final target =
                            widget.themeState!.themeMode == ThemeMode.dark
                                ? AppThemePreference.light
                                : AppThemePreference.dark;
                        widget.themeState!.setPreference(target);
                        return;
                      }
                      setState(() {
                        _fallbackDarkMode = !_fallbackDarkMode;
                      });
                    },
                    icon: Icon(
                      isDark
                          ? Icons.light_mode_outlined
                          : Icons.dark_mode_outlined,
                    ),
                  ),
                  IconButton(
                    tooltip: 'تحديث',
                    onPressed: reload,
                    icon: const Icon(Icons.refresh),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 10),
            Expanded(
              child: TabBarView(
                controller: tabs,
                children: [
                  _ProductionOrdersRootTab(
                    pieces: activeData.pieces,
                    orders: activeData.orders,
                    orderItems: activeData.orderItems,
                    routesByProductTypeId: activeData.routesByProductTypeId,
                    onRefresh: reload,
                  ),
                  TotalPiecesTab(
                    pieces: activeData.pieces,
                    orderItems: activeData.orderItems,
                    onRefresh: reload,
                  ),
                  ReadyMadeTab(
                    orders: activeData.readyOrders,
                    inventory: activeData.readyInventory,
                    readyPieces: activeData.readyPieces,
                    api: _api,
                  ),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}

class _ReadyMadeProductionPlaceholder extends StatelessWidget {
  const _ReadyMadeProductionPlaceholder();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Text(
        'الإنتاج الجاهز للبيع',
        style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
      ),
    );
  }
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
        height: 60,
        child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: items.length,
            separatorBuilder: (_, __) => const SizedBox(width: 0),
            itemBuilder: (context, index) {
              final item = items[index];
              return SizedBox(
                  width: 180,
                  child: Card(
                      child: Padding(
                          padding: const EdgeInsets.all(4),
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

class _ProductionOrdersRootTab extends StatefulWidget {
  const _ProductionOrdersRootTab({
    required this.pieces,
    required this.orders,
    required this.orderItems,
    required this.routesByProductTypeId,
    required this.onRefresh,
    super.key,
  });

  final List<Map<String, dynamic>> pieces;
  final List<Map<String, dynamic>> orders;
  final List<Map<String, dynamic>> orderItems;
  final Map<int, List<String>> routesByProductTypeId;
  final VoidCallback onRefresh;

  @override
  State<_ProductionOrdersRootTab> createState() =>
      _ProductionOrdersRootTabState();
}

class _ProductionOrdersRootTabState extends State<_ProductionOrdersRootTab>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final TextEditingController _searchController = TextEditingController();
  Timer? _searchTimer;
  Future<List<_ProductionSearchSuggestion>>? _suggestionsFuture;
  String _searchQuery = '';
  int _searchVersion = 0;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _searchController.dispose();
    _tabController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    final version = ++_searchVersion;
    _searchTimer?.cancel();
    final query = value.trim();
    setState(() {
      _searchQuery = query;
      _suggestionsFuture = null;
    });

    if (query.isEmpty) return;

    _searchTimer = Timer(const Duration(milliseconds: 250), () {
      if (!mounted || version != _searchVersion) return;
      setState(() {
        _suggestionsFuture = Future<List<_ProductionSearchSuggestion>>.value(
          _buildSearchSuggestions(query),
        );
      });
    });
  }

  void _selectSearchSuggestion(_ProductionSearchSuggestion suggestion) {
    _searchTimer?.cancel();
    ++_searchVersion;
    _searchController
      ..text = suggestion.orderNumber
      ..selection = TextSelection.collapsed(
        offset: suggestion.orderNumber.length,
      );
    setState(() {
      _searchQuery = suggestion.orderNumber;
      _suggestionsFuture = null;
    });
  }

  List<_ProductionSearchSuggestion> _buildSearchSuggestions(String query) {
    final normalizedQuery = query.toLowerCase();
    final orderByNumber = <String, Map<String, dynamic>>{};
    final orderById = <String, Map<String, dynamic>>{};
    final suggestions = <String, _ProductionSearchSuggestion>{};

    for (final order in widget.orders) {
      final orderNumber = (order['orderNumber'] ?? '').toString().trim();
      if (orderNumber.isEmpty) continue;
      orderByNumber[orderNumber] = order;
      final orderId = (order['orderId'] ?? order['id'] ?? '').toString().trim();
      if (orderId.isNotEmpty) orderById[orderId] = order;
      final customerName = _productionCustomerName(order).isEmpty
          ? 'غير محدد'
          : _productionCustomerName(order);
      final searchable = '$customerName $orderNumber '
              '${order['phoneNumber'] ?? order['customerPhone'] ?? ''}'
          .toLowerCase();
      if (searchable.contains(normalizedQuery)) {
        suggestions['order:$orderNumber'] = _ProductionSearchSuggestion(
          title: customerName,
          subtitle: 'رقم الطلب: $orderNumber',
          orderNumber: orderNumber,
        );
      }
    }

    for (final piece in widget.pieces) {
      final pieceOrderNumber = (piece['orderNumber'] ?? '').toString().trim();
      final pieceOrderId =
          (piece['orderId'] ?? piece['order_id'] ?? '').toString().trim();
      final order = orderByNumber[pieceOrderNumber] ?? orderById[pieceOrderId];
      final orderNumber = pieceOrderNumber.isNotEmpty
          ? pieceOrderNumber
          : (order?['orderNumber'] ?? '').toString().trim();
      if (orderNumber.isEmpty) continue;
      final pieceCustomerName = _productionCustomerName(piece);
      final customerName = pieceCustomerName.isNotEmpty
          ? pieceCustomerName
          : order == null
              ? 'غير محدد'
              : (_productionCustomerName(order).isEmpty
                  ? 'غير محدد'
                  : _productionCustomerName(order));
      final pieceCode = (piece['pieceCode'] ??
              piece['trackingCode'] ??
              piece['tracking'] ??
              piece['pieceNumber'] ??
              '')
          .toString()
          .trim();
      if (pieceCode.isEmpty) continue;
      final searchable = '$pieceCode $orderNumber $customerName'.toLowerCase();
      if (searchable.contains(normalizedQuery)) {
        suggestions['piece:$orderNumber:$pieceCode'] =
            _ProductionSearchSuggestion(
          title: pieceCode,
          subtitle: '$customerName • الطلب $orderNumber',
          orderNumber: orderNumber,
        );
      }
    }

    return suggestions.values.take(6).toList();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(10, 8, 10, 4),
          child: TextField(
            controller: _searchController,
            onChanged: _onSearchChanged,
            textInputAction: TextInputAction.search,
            textAlign: TextAlign.right,
            decoration: InputDecoration(
              labelText: 'اسم العميل أو رقم الطلب أو كود القطعة',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _searchQuery.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'مسح البحث',
                      onPressed: () {
                        _searchController.clear();
                        _onSearchChanged('');
                      },
                      icon: const Icon(Icons.clear),
                    ),
              border: const OutlineInputBorder(),
              isDense: true,
            ),
          ),
        ),
        if (_suggestionsFuture != null)
          _ProductionSearchSuggestions(
            future: _suggestionsFuture!,
            onSelected: _selectSearchSuggestion,
          ),
        TabBar(
          controller: _tabController,
          isScrollable: true,
          indicatorColor: UiPalette.primaryBlue,
          labelColor: UiPalette.textMain,
          unselectedLabelColor: UiPalette.textSoft,
          indicatorWeight: 3,
          dividerColor: UiPalette.borderSoft,
          tabs: const [
            Tab(text: 'كل الطلبات'),
            Tab(text: 'الطلبات الجديدة'),
            Tab(text: 'الطلبات قيد الإنتاج'),
            Tab(text: 'الطلبات الجاهزة للتسليم'),
          ],
        ),
        const SizedBox(height: 8),
        Expanded(
          child: TabBarView(
            controller: _tabController,
            children: [
              _OrdersListTab(
                pieces: widget.pieces,
                orders: widget.orders,
                orderItems: widget.orderItems,
                routesByProductTypeId: widget.routesByProductTypeId,
                onRefresh: widget.onRefresh,
                searchQuery: _searchQuery,
                filter: null,
              ),
              _OrdersListTab(
                pieces: widget.pieces,
                orders: widget.orders,
                orderItems: widget.orderItems,
                routesByProductTypeId: widget.routesByProductTypeId,
                onRefresh: widget.onRefresh,
                searchQuery: _searchQuery,
                filter: _OrderStateFilter.newOrders,
              ),
              _OrdersListTab(
                pieces: widget.pieces,
                orders: widget.orders,
                orderItems: widget.orderItems,
                routesByProductTypeId: widget.routesByProductTypeId,
                onRefresh: widget.onRefresh,
                searchQuery: _searchQuery,
                filter: _OrderStateFilter.inProduction,
              ),
              _OrdersListTab(
                pieces: widget.pieces,
                orders: widget.orders,
                orderItems: widget.orderItems,
                routesByProductTypeId: widget.routesByProductTypeId,
                onRefresh: widget.onRefresh,
                searchQuery: _searchQuery,
                filter: _OrderStateFilter.readyForDelivery,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

String _productionCustomerName(Map<String, dynamic> item) =>
    (item['customerName'] ?? item['customer'] ?? '').toString().trim();

class _ProductionSearchSuggestion {
  const _ProductionSearchSuggestion({
    required this.title,
    required this.subtitle,
    required this.orderNumber,
  });

  final String title;
  final String subtitle;
  final String orderNumber;
}

class _ProductionSearchSuggestions extends StatelessWidget {
  const _ProductionSearchSuggestions({
    required this.future,
    required this.onSelected,
  });

  final Future<List<_ProductionSearchSuggestion>> future;
  final ValueChanged<_ProductionSearchSuggestion> onSelected;

  @override
  Widget build(BuildContext context) =>
      FutureBuilder<List<_ProductionSearchSuggestion>>(
        future: future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Padding(
              padding: EdgeInsets.symmetric(horizontal: 10),
              child: LinearProgressIndicator(),
            );
          }
          if (snapshot.hasError) {
            return const Padding(
              padding: EdgeInsets.fromLTRB(10, 4, 10, 8),
              child: Text('تعذر تحميل اقتراحات الإنتاج.'),
            );
          }
          final results =
              snapshot.data ?? const <_ProductionSearchSuggestion>[];
          if (results.isEmpty) {
            return const Padding(
              padding: EdgeInsets.fromLTRB(10, 4, 10, 8),
              child: Text('لا يوجد طلب أو قطعة مطابقة.'),
            );
          }
          return Container(
            margin: const EdgeInsets.fromLTRB(10, 0, 10, 8),
            decoration: BoxDecoration(
              color: UiPalette.softBlue,
              border: Border.all(color: UiPalette.borderSoft),
              borderRadius: BorderRadius.circular(6),
            ),
            child: Column(
              children: results
                  .map(
                    (suggestion) => ListTile(
                      dense: true,
                      leading: const Icon(Icons.search_outlined),
                      title: Text(suggestion.title),
                      subtitle: Text(suggestion.subtitle),
                      onTap: () => onSelected(suggestion),
                    ),
                  )
                  .toList(),
            ),
          );
        },
      );
}

enum _OrderStateFilter { newOrders, inProduction, readyForDelivery }

class _OrdersListTab extends StatefulWidget {
  const _OrdersListTab({
    required this.pieces,
    required this.orders,
    required this.orderItems,
    required this.routesByProductTypeId,
    required this.onRefresh,
    required this.searchQuery,
    this.filter,
    super.key,
  });

  final List<Map<String, dynamic>> pieces;
  final List<Map<String, dynamic>> orders;
  final List<Map<String, dynamic>> orderItems;
  final Map<int, List<String>> routesByProductTypeId;
  final VoidCallback onRefresh;
  final String searchQuery;
  final _OrderStateFilter? filter;

  @override
  State<_OrdersListTab> createState() => _OrdersListTabState();
}

class _OrdersListTabState extends State<_OrdersListTab> {
  Color _cardSurface() {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? const Color(0xFF1A2B37) : const Color(0xFFF2F6F9);
  }

  List<_ProductionOrderRow> _rows() {
    final rowsFromOrders = widget.orders
        .map((order) {
          final orderNumber = (order['orderNumber'] ?? '').toString().trim();
          if (orderNumber.isEmpty) {
            return null;
          }

          final customerValue =
              (order['customerName'] ?? order['customer'] ?? '')
                  .toString()
                  .trim();
          final customer = customerValue.isEmpty ? 'غير محدد' : customerValue;
          final phone =
              (order['phoneNumber'] ?? order['customerPhone'] ?? '').toString();
          final requestDate = order['orderDate'] ?? order['createdAt'] ?? '';
          final dueDate =
              order['deliveryDate'] ?? order['expectedDeliveryDate'] ?? '';
          final orderId = (order['orderId'] ?? order['id'] ?? 0) as int? ?? 0;
          final details = widget.pieces
              .where((piece) =>
                  (piece['orderNumber'] ?? '').toString() == orderNumber ||
                  (piece['orderId'] ?? piece['order_id'] ?? -1).toString() ==
                      (order['orderId'] ?? order['id'] ?? '-1').toString())
              .toList();
          final metrics = calculateProductionOrderMetrics(
            details,
            routesByProductTypeId: widget.routesByProductTypeId,
          );
          final status = details.isEmpty
              ? _resolveOrderStatusFromOrder(order)
              : metrics.status;
          final orderItemSummary = _buildOrderItemSummary(
            widget.orderItems
                .where((item) =>
                    (item['orderId'] ?? item['order_id'] ?? -1).toString() ==
                    (order['orderId'] ?? order['id'] ?? '-1').toString())
                .toList(),
          );
          final summary = orderItemSummary.isNotEmpty
              ? orderItemSummary
              : _buildOrderSummaryFromOrder(order);

          return _ProductionOrderRow(
            orderId: orderId,
            orderNumber: orderNumber,
            customerName: customer,
            phone: phone,
            requestDate: requestDate,
            dueDate: dueDate,
            status: status,
            summary: summary,
            pieces: details,
            warningCount: metrics.notStartedPieces,
          );
        })
        .whereType<_ProductionOrderRow>()
        .toList();

    final rows = rowsFromOrders.isNotEmpty
        ? rowsFromOrders
        : widget.pieces
            .fold<Map<String, List<Map<String, dynamic>>>>({}, (map, piece) {
              final orderNumber =
                  (piece['orderNumber'] ?? '').toString().trim();
              if (orderNumber.isEmpty) {
                return map;
              }
              map
                  .putIfAbsent(orderNumber, () => <Map<String, dynamic>>[])
                  .add(piece);
              return map;
            })
            .entries
            .map((entry) {
              final orderNumber = entry.key;
              final items = entry.value;
              final first = items.first;
              final customerValue =
                  (first['customerName'] ?? first['customer'] ?? '')
                      .toString()
                      .trim();
              final customer =
                  customerValue.isEmpty ? 'غير محدد' : customerValue;
              final phone =
                  (first['phoneNumber'] ?? first['customerPhone'] ?? '')
                      .toString();
              final requestDate =
                  first['orderDate'] ?? first['createdAt'] ?? '';
              final dueDate =
                  first['deliveryDate'] ?? first['expectedDeliveryDate'] ?? '';
              final metrics = calculateProductionOrderMetrics(
                items,
                routesByProductTypeId: widget.routesByProductTypeId,
              );
              final status = metrics.status;
              final summary = _buildOrderSummary(items);
              final orderId =
                  ((first['orderId'] ?? first['order_id'] ?? 0) as int?) ?? 0;
              return _ProductionOrderRow(
                orderId: orderId,
                orderNumber: orderNumber,
                customerName: customer,
                phone: phone,
                requestDate: requestDate,
                dueDate: dueDate,
                status: status,
                summary: summary,
                pieces: items,
                warningCount: metrics.notStartedPieces,
              );
            })
            .toList();

    rows.sort(
        (a, b) => b.requestDate.toString().compareTo(a.requestDate.toString()));

    final filtered = rows.where((row) {
      if (widget.filter != null) {
        switch (widget.filter!) {
          case _OrderStateFilter.newOrders:
            if (row.status != 'جديد') return false;
            break;
          case _OrderStateFilter.inProduction:
            if (row.status != 'قيد الإنتاج') return false;
            break;
          case _OrderStateFilter.readyForDelivery:
            if (row.status != 'جاهز للتسليم' && row.status != 'تم التسليم')
              return false;
            break;
        }
      }

      final query = widget.searchQuery.trim().toLowerCase();
      if (query.isEmpty) return true;

      final pieceSearchText = row.pieces
          .expand((piece) => [
                piece['pieceCode'],
                piece['trackingCode'],
                piece['tracking'],
                piece['pieceNumber'],
              ])
          .whereType<Object>()
          .map((value) => value.toString().toLowerCase())
          .join(' ');
      return row.orderNumber.toLowerCase().contains(query) ||
          row.customerName.toLowerCase().contains(query) ||
          row.phone.toLowerCase().contains(query) ||
          pieceSearchText.contains(query);
    }).toList();

    return filtered;
  }

  @override
  Widget build(BuildContext context) {
    final rows = _rows();

    return Column(
      children: [
        Expanded(
          child: rows.isEmpty
              ? const Center(
                  child: Text('لا توجد طلبات في هذا التبويب.'),
                )
              : ListView.separated(
                  itemCount: rows.length,
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (context, index) {
                    final row = rows[index];

                    return Material(
                      color: Colors.transparent,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(12),
                        onTap: () => AppNavigation.push(
                          context,
                          (_) => ProductionOrderDetailScreen(
                            orderId: row.orderId,
                            orderNumber: row.orderNumber,
                            customerName: row.customerName,
                            phone: row.phone,
                            requestDate: row.requestDate,
                            dueDate: row.dueDate,
                            status: row.status,
                            pieces: row.pieces,
                            routesByProductTypeId: widget.routesByProductTypeId,
                            onRefreshed: () async {
                              widget.onRefresh();
                              return true;
                            },
                          ),
                        ),
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 14, vertical: 12),
                          decoration: BoxDecoration(
                            color: _cardSurface(),
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(color: UiPalette.borderSoft),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              if (row.status == 'قيد الإنتاج' &&
                                  row.warningCount > 0)
                                Container(
                                  margin: const EdgeInsets.only(bottom: 8),
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 10, vertical: 8),
                                  decoration: BoxDecoration(
                                    color: const Color(0xFFFDECEC),
                                    borderRadius: BorderRadius.circular(10),
                                    border: Border.all(
                                        color: const Color(0xFFD9534F)),
                                  ),
                                  child: Text(
                                    buildProductionWarningText(
                                        row.warningCount),
                                    style: const TextStyle(
                                      color: Color(0xFFB3261E),
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ),
                              Row(
                                children: [
                                  Expanded(
                                    child: RichText(
                                      text: TextSpan(
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyMedium,
                                        children: [
                                          TextSpan(
                                            text: '${row.orderNumber}  |  ',
                                            style: const TextStyle(
                                                fontWeight: FontWeight.bold),
                                          ),
                                          TextSpan(
                                              text: '${row.customerName}  |  '),
                                          TextSpan(text: row.summary),
                                          TextSpan(
                                              text:
                                                  '  |  ${formatDate(row.requestDate)}'),
                                        ],
                                      ),
                                    ),
                                  ),
                                  const SizedBox(width: 12),
                                  Container(
                                    padding: const EdgeInsets.symmetric(
                                        horizontal: 10, vertical: 6),
                                    decoration: BoxDecoration(
                                      color: _statusColor(row.status),
                                      borderRadius: BorderRadius.circular(20),
                                    ),
                                    child: Text(
                                      row.status,
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodySmall
                                          ?.copyWith(
                                            color: UiPalette.adaptiveTextColor(
                                                _statusColor(row.status)),
                                            fontWeight: FontWeight.bold,
                                          ),
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
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

class _ProductionOrderRow {
  const _ProductionOrderRow({
    required this.orderId,
    required this.orderNumber,
    required this.customerName,
    required this.phone,
    required this.requestDate,
    required this.dueDate,
    required this.status,
    required this.summary,
    required this.pieces,
    required this.warningCount,
  });

  final int orderId;
  final String orderNumber;
  final String customerName;
  final String phone;
  final dynamic requestDate;
  final dynamic dueDate;
  final String status;
  final String summary;
  final List<Map<String, dynamic>> pieces;
  final int warningCount;
}

String _buildOrderSummary(List<Map<String, dynamic>> items) {
  final counts = <String, int>{};
  for (final item in items) {
    final type = (item['pieceType'] ?? item['type'] ?? 'غير محدد').toString();
    counts[type] = (counts[type] ?? 0) + 1;
  }

  if (counts.isEmpty) {
    return '0 قطعة';
  }

  return counts.entries
      .map((entry) => '${entry.value} ${entry.key}')
      .join(' - ');
}

String _buildOrderItemSummary(List<Map<String, dynamic>> items) {
  if (items.isEmpty) {
    return 'لا توجد قطع';
  }

  final counts = <String, int>{};
  for (final item in items) {
    final type = (item['pieceType'] ?? item['type'] ?? 'غير محدد').toString();
    final quantity = (item['quantity'] as num?)?.toInt() ?? 1;
    counts[type] = (counts[type] ?? 0) + quantity;
  }

  if (counts.isEmpty) {
    return 'لا توجد قطع';
  }

  return counts.entries
      .map((entry) => '${entry.value} ${entry.key}')
      .join(' - ');
}

String _buildOrderSummaryFromOrder(Map<String, dynamic> order) {
  final total = (order['totalAmount'] as num?)?.toDouble() ?? 0;
  final remaining = (order['remainingAmount'] as num?)?.toDouble() ?? 0;
  return 'الإجمالي ${total.toStringAsFixed(2)} • المتبقي ${remaining.toStringAsFixed(2)}';
}

String _normalizeOrderStatusKey(String value) {
  final normalized = value.trim();
  if (normalized.isEmpty) return '';

  final lowered = normalized.toLowerCase();
  if (lowered == 'new' || lowered == 'جديد' || lowered == 'neworder') {
    return 'new';
  }
  if (lowered.contains('inproduction') ||
      lowered.contains('production') ||
      lowered == 'قيد الإنتاج') {
    return 'in_production';
  }
  if (lowered.contains('readyfordelivery') ||
      lowered == 'ready' ||
      lowered == 'جاهز للتسليم' ||
      lowered == 'جاهز') {
    return 'ready_for_delivery';
  }
  if (lowered.contains('partial') && lowered.contains('deliver')) {
    return 'partial_delivery';
  }
  if (lowered.contains('deliver') ||
      lowered == 'delivered' ||
      lowered == 'تم التسليم' ||
      lowered == 'تم التسليم بالكامل') {
    return 'delivered';
  }
  if (lowered.contains('cancel')) {
    return 'cancelled';
  }
  return lowered;
}

String _resolveOrderStatusFromOrder(Map<String, dynamic> order) {
  final status = (order['orderStatus'] ?? '').toString();
  switch (_normalizeOrderStatusKey(status)) {
    case 'new':
      return 'جديد';
    case 'in_production':
      return 'قيد الإنتاج';
    case 'ready_for_delivery':
      return 'جاهز للتسليم';
    case 'partial_delivery':
      return 'تم تسليم جزئي';
    case 'delivered':
      return 'تم التسليم';
    case 'cancelled':
      return 'ملغاة';
    default:
      return 'جديد';
  }
}

String _resolveOrderStatus(List<Map<String, dynamic>> items) {
  if (items.isEmpty) {
    return 'جديد';
  }

  final statuses = items
      .map((item) =>
          _normalizeOrderStatusKey((item['pieceStatus'] ?? '').toString()))
      .where((value) => value.isNotEmpty)
      .toSet();

  if (statuses.contains('ready_for_delivery') ||
      statuses.contains('delivered')) {
    final hasDelivered = statuses.contains('delivered');
    final hasReady = statuses.contains('ready_for_delivery');
    if (hasDelivered && !hasReady) {
      return 'تم التسليم';
    }
    return 'جاهز للتسليم';
  }

  if (statuses.any((status) => status != 'new' && status != '')) {
    return 'قيد الإنتاج';
  }

  return 'جديد';
}

String _resolveDeliveryStateForOrder(
  List<Map<String, dynamic>> pieces,
  Map<int, List<String>> routesByProductTypeId,
) {
  final totalPieces = pieces.length;
  if (totalPieces == 0) return 'غير جاهز للتسليم';

  var readyPieces = 0;
  var deliveredPieces = 0;
  for (final piece in pieces) {
    final status = _canonicalProductionStage(
      (piece['pieceStatus'] ?? '').toString(),
    );
    final normalizedStatus = _normalizeOrderStatusKey(status);
    if (normalizedStatus == 'delivered') {
      deliveredPieces++;
      continue;
    }

    final productTypeId = int.tryParse(
          (piece['productTypeId'] ?? '').toString(),
        ) ??
        0;
    final route = routesByProductTypeId[productTypeId] ?? const <String>[];
    final lastStage =
        route.isEmpty ? '' : _canonicalProductionStage(route.last);
    final isReady = normalizedStatus == 'ready_for_delivery' ||
        (lastStage.isNotEmpty && status == lastStage);
    if (isReady) readyPieces++;
  }

  if (deliveredPieces == totalPieces) {
    return 'تم التسليم بالكامل';
  }

  if (deliveredPieces > 0) {
    return 'تم التسليم جزئياً';
  }

  if (readyPieces == totalPieces) {
    return 'جاهز للتسليم الكامل';
  }

  if (readyPieces > 0) return 'جاهز للتسليم الجزئي';
  return 'غير جاهز للتسليم';
}

Color _statusColor(String status) {
  switch (status) {
    case 'جديد':
      return UiPalette.primaryBlue;
    case 'قيد الإنتاج':
      return const Color(0xFFB7791F);
    case 'جاهز للتسليم':
      return UiPalette.primaryDark;
    default:
      return Colors.grey;
  }
}

class ProductionOrderDetailScreen extends StatefulWidget {
  const ProductionOrderDetailScreen({
    required this.orderId,
    required this.orderNumber,
    required this.customerName,
    required this.phone,
    required this.requestDate,
    required this.dueDate,
    required this.status,
    required this.pieces,
    required this.routesByProductTypeId,
    this.onRefreshed,
    super.key,
  });

  final int orderId;
  final String orderNumber;
  final String customerName;
  final String phone;
  final dynamic requestDate;
  final dynamic dueDate;
  final String status;
  final List<Map<String, dynamic>> pieces;
  final Map<int, List<String>> routesByProductTypeId;
  final Future<bool> Function()? onRefreshed;

  @override
  State<ProductionOrderDetailScreen> createState() =>
      _ProductionOrderDetailScreenState();
}

class _ProductionOrderDetailData {
  const _ProductionOrderDetailData({
    required this.order,
    required this.orderItems,
    required this.pieces,
    required this.typeSummary,
    required this.lastStatusByPieceId,
  });

  final Map<String, dynamic> order;
  final List<Map<String, dynamic>> orderItems;
  final List<Map<String, dynamic>> pieces;
  final String typeSummary;
  final Map<int, String> lastStatusByPieceId;
}

class _ProductionOrderDetailScreenState
    extends State<ProductionOrderDetailScreen> {
  late Future<_ProductionOrderDetailData> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_ProductionOrderDetailData> _load() async {
    final api = ProductionApi();
    final order =
        await api.get('/orders/${widget.orderId}') as Map<String, dynamic>;
    final orderItems = await api.list('/orders/${widget.orderId}/items');
    final pieces = await api.list('/orders/${widget.orderId}/pieces');

    final typeCounts = <String, int>{};
    for (final item in orderItems) {
      final type = (item['pieceType'] ?? item['type'] ?? 'غير محدد').toString();
      final quantity = (item['quantity'] as num?)?.toInt() ?? 1;
      typeCounts[type] = (typeCounts[type] ?? 0) + quantity;
    }
    if (typeCounts.isEmpty) {
      for (final piece in pieces) {
        final type =
            (piece['pieceType'] ?? piece['type'] ?? 'غير محدد').toString();
        typeCounts[type] = (typeCounts[type] ?? 0) + 1;
      }
    }

    final statusesByPieceId = <int, String>{};
    final workCards = await Future.wait(pieces.map((piece) async {
      final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
      if (pieceId <= 0) return <int, String>{};
      try {
        final card = await api.get('/production/pieces/$pieceId/work-card');
        final history = (card is Map<String, dynamic>
                ? (card['trackingHistory'] as List? ?? const [])
                : const [])
            .cast<Map<String, dynamic>>();
        if (history.isEmpty) {
          return <int, String>{
            pieceId:
                _normalizePieceStatus((piece['pieceStatus'] ?? '').toString())
          };
        }
        final latest =
            history.where((event) => event.isNotEmpty).reduce((current, next) {
          final currentTime = DateTime.tryParse(
              (current['eventTime'] ?? current['createdAt'] ?? '').toString());
          final nextTime = DateTime.tryParse(
              (next['eventTime'] ?? next['createdAt'] ?? '').toString());
          if (currentTime == null) return next;
          if (nextTime == null) return current;
          return nextTime.isAfter(currentTime) ? next : current;
        });
        final value =
            ((latest['status'] ?? latest['stage'] ?? '').toString()).trim();
        return <int, String>{
          pieceId: _normalizePieceStatus(
              value.isEmpty ? (piece['pieceStatus'] ?? '').toString() : value)
        };
      } catch (_) {
        return <int, String>{
          pieceId:
              _normalizePieceStatus((piece['pieceStatus'] ?? '').toString())
        };
      }
    }));

    for (final item in workCards) {
      statusesByPieceId.addAll(item);
    }

    final typeSummary = typeCounts.entries
        .map((entry) => '${entry.value} ${entry.key}')
        .join(' - ');

    return _ProductionOrderDetailData(
      order: order,
      orderItems: orderItems,
      pieces: pieces,
      typeSummary: typeSummary,
      lastStatusByPieceId: statusesByPieceId,
    );
  }

  Future<bool> _reloadAfterPieceChange() async {
    final refreshedData = await _load();
    if (!mounted) return false;
    setState(() {
      _future = Future<_ProductionOrderDetailData>.value(refreshedData);
    });
    return true;
  }

  static String _normalizePieceStatus(String value) {
    final normalized = value.trim();
    if (normalized.isEmpty) return 'غير محدد';
    return switch (normalized.toLowerCase()) {
      'new' => 'جديد',
      'cutting' => 'تم القص',
      'fabricprep' => 'تم تجهيز القماش',
      'sewing' => 'تم الخياطة',
      'buttons' => 'تم تركيب الأزرار',
      'ironing' => 'تم الكي',
      'quality' => 'تمت الجودة',
      'ready' || 'readyfordelivery' => 'جاهزة للتسليم',
      'delivered' => 'تم التسليم',
      'inproduction' || 'inprogress' => 'قيد الإنتاج',
      'assembly' => 'تم التجميع',
      'printing' => 'تمت الطباعة',
      _ => normalized,
    };
  }

  String _resolvePieceTypeForPiece(
    Map<String, dynamic> piece,
    List<Map<String, dynamic>> orderItems,
  ) {
    final directType =
        (piece['pieceType'] ?? piece['type'] ?? '').toString().trim();
    if (directType.isNotEmpty && directType != '-') {
      return directType;
    }

    final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
    final orderItemId =
        (piece['orderItemId'] ?? piece['order_item_id'] ?? -1) as int? ?? -1;

    if (pieceId > 0) {
      for (final item in orderItems) {
        final itemPieceId = (item['pieceId'] ?? item['id'] ?? -1) as int? ?? -1;
        if (itemPieceId == pieceId) {
          final type =
              (item['pieceType'] ?? item['type'] ?? '').toString().trim();
          if (type.isNotEmpty && type != '-') return type;
        }
      }
    }

    if (orderItemId > 0) {
      for (final item in orderItems) {
        final itemOrderItemId =
            (item['orderItemId'] ?? item['order_item_id'] ?? -1) as int? ?? -1;
        if (itemOrderItemId == orderItemId) {
          final type =
              (item['pieceType'] ?? item['type'] ?? '').toString().trim();
          if (type.isNotEmpty && type != '-') return type;
        }
      }
    }

    return 'غير محدد';
  }

  String _pieceNumberLabel(
    Map<String, dynamic> piece,
    Map<String, int> typeTotals,
    Map<int, int> indexByPieceId,
    List<Map<String, dynamic>> orderItems,
  ) {
    final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
    final type = _resolvePieceTypeForPiece(piece, orderItems);
    final totalForType = typeTotals[type] ?? 1;
    final currentIndex = indexByPieceId[pieceId] ?? 1;
    return '$currentIndex/$totalForType';
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<_ProductionOrderDetailData>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Scaffold(
              body: Center(child: CircularProgressIndicator()));
        }
        if (snapshot.hasError) {
          return Scaffold(
            appBar: AppBar(title: const Text('تفاصيل الطلب')),
            body: ProductionError(
              onRetry: () => setState(() => _future = _load()),
              message: 'تعذر تحميل تفاصيل الطلب.',
            ),
          );
        }

        final data = snapshot.data ??
            _ProductionOrderDetailData(
              order: {},
              orderItems: const [],
              pieces: const [],
              typeSummary: 'لا توجد قطع',
              lastStatusByPieceId: const {},
            );

        final order = data.order;
        final pieces = data.pieces;
        final typeSummary =
            data.typeSummary.isEmpty ? 'لا توجد قطع' : data.typeSummary;
        final totalPieces = pieces.length;
        final orderNumber =
            (order['orderNumber'] ?? widget.orderNumber).toString();
        final customerValue = (order['customerName'] ?? '').toString().trim();
        final customerName =
            customerValue.isEmpty ? widget.customerName : customerValue;
        final phone =
            (order['phoneNumber'] ?? order['customerPhone'] ?? widget.phone)
                .toString();
        final requestDate = order['orderDate'] ?? widget.requestDate;
        final dueDate = order['deliveryDate'] ?? widget.dueDate;
        final status = pieces.isEmpty
            ? _resolveOrderStatusFromOrder(order)
            : calculateProductionOrderMetrics(
                pieces,
                routesByProductTypeId: widget.routesByProductTypeId,
              ).status;
        final typeCounts = <String, int>{};
        for (final item in data.orderItems) {
          final type = (item['pieceType'] ?? item['type'] ?? 'غير محدد')
              .toString()
              .trim();
          if (type.isEmpty || type == '-') continue;
          final quantity = (item['quantity'] as num?)?.toInt() ?? 1;
          typeCounts[type] = (typeCounts[type] ?? 0) + quantity;
        }
        if (typeCounts.isEmpty) {
          final byType = <String, int>{};
          for (final piece in pieces) {
            final type = _resolvePieceTypeForPiece(piece, data.orderItems);
            byType[type] = (byType[type] ?? 0) + 1;
          }
          typeCounts.addAll(byType);
        }

        final sequenceByPieceId = <int, int>{};
        final seenByType = <String, int>{};
        final sorted = [...pieces]..sort((a, b) {
            final aId = (a['pieceId'] ?? a['id'] ?? -1) as int? ?? -1;
            final bId = (b['pieceId'] ?? b['id'] ?? -1) as int? ?? -1;
            final aType = _resolvePieceTypeForPiece(a, data.orderItems);
            final bType = _resolvePieceTypeForPiece(b, data.orderItems);
            if (aType != bType) return aType.compareTo(bType);
            final aNumber = (a['pieceNumber'] as num?)?.toInt() ?? aId;
            final bNumber = (b['pieceNumber'] as num?)?.toInt() ?? bId;
            return aNumber.compareTo(bNumber);
          });

        for (final piece in sorted) {
          final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
          if (pieceId <= 0) continue;
          final type = _resolvePieceTypeForPiece(piece, data.orderItems);
          seenByType[type] = (seenByType[type] ?? 0) + 1;
          sequenceByPieceId[pieceId] = seenByType[type]!;
        }

        final rows = pieces.map((piece) {
          final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
          final type = _resolvePieceTypeForPiece(piece, data.orderItems);
          final pieceStatus = data.lastStatusByPieceId[pieceId] ??
              _normalizePieceStatus((piece['pieceStatus'] ?? '').toString());
          final trackingCode =
              (piece['trackingCode'] ?? piece['tracking'] ?? '-').toString();
          return DataRow(
            cells: [
              DataCell(Text(type.isEmpty || type == '-' ? 'غير محدد' : type)),
              DataCell(Text(_pieceNumberLabel(
                  piece, typeCounts, sequenceByPieceId, data.orderItems))),
              DataCell(Text(trackingCode)),
              DataCell(Text(pieceStatus)),
              DataCell(Text(_normalizePieceStatus(
                          (piece['lastEventStatus'] ?? piece['lastStage'] ?? '')
                              .toString()) ==
                      'غير محدد'
                  ? pieceStatus
                  : _normalizePieceStatus(
                      (piece['lastEventStatus'] ?? piece['lastStage'] ?? '')
                          .toString()))),
              DataCell(
                TextButton(
                  onPressed: pieceId > 0
                      ? () => AppNavigation.push(
                            context,
                            (_) => ProductionPieceDetailsScreen(
                              pieceId: pieceId,
                              onRefreshed: _reloadAfterPieceChange,
                            ),
                          )
                      : null,
                  child: const Text('تفاصيل'),
                ),
              ),
            ],
          );
        }).toList();

        return Scaffold(
          appBar: AppBar(title: Text('طلب $orderNumber')),
          body: SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.surface,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: UiPalette.borderSoft),
                    ),
                    child: Wrap(
                      spacing: 18,
                      runSpacing: 10,
                      children: [
                        _InfoChip(label: 'رقم الطلب', value: orderNumber),
                        _InfoChip(label: 'اسم العميل', value: customerName),
                        _InfoChip(
                            label: 'رقم الهاتف',
                            value: phone.isEmpty ? '-' : phone),
                        _InfoChip(
                            label: 'تاريخ الطلب',
                            value: formatDate(requestDate)),
                        _InfoChip(
                            label: 'تاريخ الاستلام',
                            value: formatDate(dueDate)),
                        _InfoChip(label: 'الحالة', value: status),
                        _InfoChip(
                          label: 'حالة التسليم',
                          value: _resolveDeliveryStateForOrder(
                            pieces,
                            widget.routesByProductTypeId,
                          ),
                        ),
                        _InfoChip(label: 'إجمالي القطع', value: '$totalPieces'),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'الأنواع: $typeSummary',
                    style: Theme.of(context).textTheme.bodyLarge,
                  ),
                  const SizedBox(height: 18),
                  Expanded(
                    child: SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: ConstrainedBox(
                        constraints: BoxConstraints(
                          minWidth: MediaQuery.of(context).size.width - 32,
                        ),
                        child: DataTable(
                          columns: const [
                            DataColumn(label: Text('نوع القطعة')),
                            DataColumn(label: Text('رقم القطعة')),
                            DataColumn(label: Text('TrackingCode')),
                            DataColumn(label: Text('الحالة الحالية')),
                            DataColumn(label: Text('آخر حركة')),
                            DataColumn(label: Text('تفاصيل')),
                          ],
                          rows: rows,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class _InfoChip extends StatelessWidget {
  const _InfoChip({required this.label, required this.value, super.key});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.labelSmall),
        const SizedBox(height: 4),
        Text(value, style: Theme.of(context).textTheme.bodyMedium),
      ],
    );
  }
}

class TotalPiecesTab extends StatefulWidget {
  const TotalPiecesTab({
    required this.pieces,
    required this.orderItems,
    required this.onRefresh,
    super.key,
  });

  final List<Map<String, dynamic>> pieces;
  final List<Map<String, dynamic>> orderItems;
  final VoidCallback onRefresh;

  @override
  State<TotalPiecesTab> createState() => _TotalPiecesTabState();
}

class _TotalPiecesTabState extends State<TotalPiecesTab> {
  final TextEditingController _dailyController = TextEditingController();
  final TextEditingController _monthlyController = TextEditingController();
  final TextEditingController _yearlyController = TextEditingController();

  DateTime get _now => DateTime.now();

  Map<String, int> _countByType(List<Map<String, dynamic>> pieces) {
    final counts = <String, int>{};
    for (final piece in pieces) {
      final directType =
          (piece['pieceType'] ?? piece['type'] ?? '').toString().trim();
      final type = directType.isNotEmpty && directType != '-'
          ? directType
          : _resolvePieceTypeFromOrderItems(piece, widget.orderItems);
      if (type.isEmpty || type == '-') continue;
      counts[type] = (counts[type] ?? 0) + 1;
    }
    return counts;
  }

  String _resolvePieceTypeFromOrderItems(
    Map<String, dynamic> piece,
    List<Map<String, dynamic>> orderItems,
  ) {
    final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
    final orderItemId =
        (piece['orderItemId'] ?? piece['order_item_id'] ?? -1) as int? ?? -1;

    if (pieceId > 0) {
      for (final item in orderItems) {
        final itemPieceId = (item['pieceId'] ?? item['id'] ?? -1) as int? ?? -1;
        if (itemPieceId == pieceId) {
          final type =
              (item['pieceType'] ?? item['type'] ?? '').toString().trim();
          if (type.isNotEmpty && type != '-') return type;
        }
      }
    }

    if (orderItemId > 0) {
      for (final item in orderItems) {
        final itemOrderItemId =
            (item['orderItemId'] ?? item['order_item_id'] ?? -1) as int? ?? -1;
        if (itemOrderItemId == orderItemId) {
          final type =
              (item['pieceType'] ?? item['type'] ?? '').toString().trim();
          if (type.isNotEmpty && type != '-') return type;
        }
      }
    }

    return '';
  }

  List<_PieceTypeCardData> _cardsForCounts(Map<String, int> counts) {
    final items = counts.entries.toList()
      ..sort((a, b) => a.key.compareTo(b.key));
    return items
        .map((entry) => _PieceTypeCardData(name: entry.key, count: entry.value))
        .toList();
  }

  List<_PieceTypeCardData> _cardsForDateRange(DateTime start, DateTime end) {
    final matches = widget.pieces.where((piece) {
      final dateValue =
          piece['createdDate'] ?? piece['createdAt'] ?? piece['created_at'];
      final parsed = DateTime.tryParse(dateValue?.toString() ?? '');
      if (parsed == null) return false;
      return !parsed.isBefore(start) && !parsed.isAfter(end);
    }).toList();

    return _cardsForCounts(_countByType(matches));
  }

  @override
  void dispose() {
    _dailyController.dispose();
    _monthlyController.dispose();
    _yearlyController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final allCards = _cardsForCounts(_countByType(widget.pieces));
    final todayCards = _cardsForDateRange(
      DateTime(_now.year, _now.month, _now.day),
      DateTime(_now.year, _now.month, _now.day, 23, 59, 59),
    );

    return RefreshIndicator(
      onRefresh: () async => widget.onRefresh(),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _PieceSectionHeader(title: 'إجمالي القطع الكلي'),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: allCards
                  .map((card) => SizedBox(
                        width: 150,
                        child: _PieceCountCard(
                            title: card.name, value: card.count),
                      ))
                  .toList(),
            ),
            const SizedBox(height: 20),
            _PieceSectionHeader(title: 'إجمالي قطع اليوم'),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: todayCards
                  .map((card) => SizedBox(
                        width: 150,
                        child: _PieceCountCard(
                            title: card.name, value: card.count),
                      ))
                  .toList(),
            ),
            const SizedBox(height: 20),
            _PieceSectionHeader(title: 'إحصائيات حسب الفترة'),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: _PeriodField(
                    controller: _dailyController,
                    hint: 'DD/MM/YYYY',
                    label: 'يومي',
                    onSubmitted: () {
                      final value = _dailyController.text.trim();
                      final parsed = _parseByDay(value);
                      if (parsed == null) return;
                      setState(() {});
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _PeriodField(
                    controller: _monthlyController,
                    hint: 'MM/YYYY',
                    label: 'شهري',
                    onSubmitted: () {
                      final value = _monthlyController.text.trim();
                      final parsed = _parseByMonth(value);
                      if (parsed == null) return;
                      setState(() {});
                    },
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _PeriodField(
                    controller: _yearlyController,
                    hint: 'YYYY',
                    label: 'سنوي',
                    onSubmitted: () {
                      final value = _yearlyController.text.trim();
                      final parsed = _parseByYear(value);
                      if (parsed == null) return;
                      setState(() {});
                    },
                  ),
                ),
                const SizedBox(width: 12),
                FilledButton.icon(
                  onPressed: widget.onRefresh,
                  icon: const Icon(Icons.refresh),
                  label: const Text('تحديث'),
                ),
              ],
            ),
            const SizedBox(height: 12),
            _SelectedPeriodSummary(
              pieces: widget.pieces,
              orderItems: widget.orderItems,
              daily: _dailyController.text.trim(),
              monthly: _monthlyController.text.trim(),
              yearly: _yearlyController.text.trim(),
            ),
          ],
        ),
      ),
    );
  }

  DateTime? _parseByDay(String text) {
    try {
      final parts = text.split('/');
      if (parts.length != 3) return null;
      final day = int.parse(parts[0]);
      final month = int.parse(parts[1]);
      final year = int.parse(parts[2]);
      return DateTime(year, month, day);
    } catch (_) {
      return null;
    }
  }

  DateTime? _parseByMonth(String text) {
    try {
      final parts = text.split('/');
      if (parts.length != 2) return null;
      final month = int.parse(parts[0]);
      final year = int.parse(parts[1]);
      return DateTime(year, month, 1);
    } catch (_) {
      return null;
    }
  }

  DateTime? _parseByYear(String text) {
    try {
      final year = int.parse(text);
      return DateTime(year, 1, 1);
    } catch (_) {
      return null;
    }
  }
}

class _PeriodField extends StatelessWidget {
  const _PeriodField({
    required this.controller,
    required this.hint,
    required this.label,
    required this.onSubmitted,
    super.key,
  });

  final TextEditingController controller;
  final String hint;
  final String label;
  final VoidCallback onSubmitted;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      textAlign: TextAlign.right,
      keyboardType: TextInputType.datetime,
      onSubmitted: (_) => onSubmitted(),
      decoration: InputDecoration(
        border: const OutlineInputBorder(),
        hintText: hint,
        labelText: label,
        isDense: true,
      ),
    );
  }
}

class _SelectedPeriodSummary extends StatelessWidget {
  const _SelectedPeriodSummary({
    required this.pieces,
    required this.orderItems,
    required this.daily,
    required this.monthly,
    required this.yearly,
    super.key,
  });

  final List<Map<String, dynamic>> pieces;
  final List<Map<String, dynamic>> orderItems;
  final String daily;
  final String monthly;
  final String yearly;

  Map<String, int> _countByType(List<Map<String, dynamic>> set) {
    final counts = <String, int>{};
    for (final piece in set) {
      final directType =
          (piece['pieceType'] ?? piece['type'] ?? '').toString().trim();
      String type = directType;
      if (type.isEmpty || type == '-') {
        final pieceId = (piece['pieceId'] ?? piece['id'] ?? -1) as int? ?? -1;
        final orderItemId =
            (piece['orderItemId'] ?? piece['order_item_id'] ?? -1) as int? ??
                -1;
        if (pieceId > 0) {
          for (final item in orderItems) {
            final itemPieceId =
                (item['pieceId'] ?? item['id'] ?? -1) as int? ?? -1;
            if (itemPieceId == pieceId) {
              type =
                  (item['pieceType'] ?? item['type'] ?? '').toString().trim();
              break;
            }
          }
        }
        if (type.isEmpty || type == '-') {
          if (orderItemId > 0) {
            for (final item in orderItems) {
              final itemOrderItemId = (item['orderItemId'] ??
                      item['order_item_id'] ??
                      -1) as int? ??
                  -1;
              if (itemOrderItemId == orderItemId) {
                type =
                    (item['pieceType'] ?? item['type'] ?? '').toString().trim();
                break;
              }
            }
          }
        }
      }
      if (type.isEmpty || type == '-') continue;
      counts[type] = (counts[type] ?? 0) + 1;
    }
    return counts;
  }

  List<_PieceTypeCardData> _cardsForSelectedPeriod() {
    final selected = <Map<String, dynamic>>[];
    final now = DateTime.now();

    if (daily.isNotEmpty) {
      try {
        final parts = daily.split('/');
        final day = int.parse(parts[0]);
        final month = int.parse(parts[1]);
        final year = int.parse(parts[2]);
        final dt = DateTime(year, month, day);
        final start = DateTime(dt.year, dt.month, dt.day);
        final end = DateTime(dt.year, dt.month, dt.day, 23, 59, 59);
        selected.addAll(pieces.where((piece) {
          final raw =
              piece['createdDate'] ?? piece['createdAt'] ?? piece['created_at'];
          final date = DateTime.tryParse(raw?.toString() ?? '');
          return date != null && !date.isBefore(start) && !date.isAfter(end);
        }));
      } catch (_) {}
    } else if (monthly.isNotEmpty) {
      try {
        final parts = monthly.split('/');
        final month = int.parse(parts[0]);
        final year = int.parse(parts[1]);
        final start = DateTime(year, month, 1);
        final end = DateTime(year, month + 1, 0, 23, 59, 59);
        selected.addAll(pieces.where((piece) {
          final raw =
              piece['createdDate'] ?? piece['createdAt'] ?? piece['created_at'];
          final date = DateTime.tryParse(raw?.toString() ?? '');
          return date != null && !date.isBefore(start) && !date.isAfter(end);
        }));
      } catch (_) {}
    } else if (yearly.isNotEmpty) {
      try {
        final year = int.parse(yearly);
        final start = DateTime(year, 1, 1);
        final end = DateTime(year, 12, 31, 23, 59, 59);
        selected.addAll(pieces.where((piece) {
          final raw =
              piece['createdDate'] ?? piece['createdAt'] ?? piece['created_at'];
          final date = DateTime.tryParse(raw?.toString() ?? '');
          return date != null && !date.isBefore(start) && !date.isAfter(end);
        }));
      } catch (_) {}
    } else {
      selected.addAll(pieces);
    }

    final counts = _countByType(selected);
    return counts.entries
        .map((entry) => _PieceTypeCardData(name: entry.key, count: entry.value))
        .toList();
  }

  @override
  Widget build(BuildContext context) {
    final cards = _cardsForSelectedPeriod();
    if (cards.isEmpty) {
      return const Text('لا توجد بيانات للفترة المحددة.');
    }

    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: cards
          .map((card) => SizedBox(
                width: 150,
                child: _PieceCountCard(title: card.name, value: card.count),
              ))
          .toList(),
    );
  }
}

class _PieceSectionHeader extends StatelessWidget {
  const _PieceSectionHeader({required this.title, super.key});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: Theme.of(context).textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.bold,
          ),
    );
  }
}

class _PieceTypeCardData {
  const _PieceTypeCardData({required this.name, required this.count});

  final String name;
  final int count;
}

class _PieceCountCard extends StatelessWidget {
  const _PieceCountCard({required this.title, required this.value, super.key});

  final String title;
  final int value;

  @override
  Widget build(BuildContext context) {
    final outer = Theme.of(context).brightness == Brightness.dark
        ? const Color(0xFF1A2B37)
        : const Color(0xFFF3F6F9);
    final inner = Theme.of(context).brightness == Brightness.dark
        ? const Color(0xFF12232E)
        : const Color(0xFFFFFFFF);

    return Container(
      width: 150,
      height: 64,
      padding: const EdgeInsets.all(2),
      decoration: BoxDecoration(
        color: outer,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              height: 18,
              alignment: Alignment.center,
              padding: const EdgeInsets.symmetric(horizontal: 8),
              decoration: BoxDecoration(
                color: outer,
                border: Border(
                    bottom: BorderSide(color: UiPalette.borderSoft, width: 1)),
              ),
              child: Text(
                title,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      height: 1.1,
                    ),
              ),
            ),
            Expanded(
              child: Container(
                color: inner,
                alignment: Alignment.center,
                child: Text(
                  value.toString(),
                  textAlign: TextAlign.center,
                  maxLines: 1,
                  overflow: TextOverflow.clip,
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                        fontSize: 26,
                        color: UiPalette.primaryBlue,
                        height: 1,
                      ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
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
                                '${p['trackingCode']}  •  ${_productionDisplayLabel(p['pieceStatus'])}'),
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
  const ProductionPieceDetailsScreen(
      {required this.pieceId, this.onRefreshed, super.key});
  final int pieceId;
  final Future<bool> Function()? onRefreshed;
  @override
  State<ProductionPieceDetailsScreen> createState() =>
      _ProductionPieceDetailsScreenState();
}

class _PieceDetailData {
  const _PieceDetailData({
    required this.card,
    required this.route,
    required this.scanners,
  });

  final Map<String, dynamic> card;
  final Map<String, dynamic>? route;
  final List<Map<String, dynamic>> scanners;
}

class _ProductionPieceDetailsScreenState
    extends State<ProductionPieceDetailsScreen> {
  final api = ProductionApi();
  late Future<_PieceDetailData> future;
  bool _manualAdvanceBusy = false;

  @override
  void initState() {
    super.initState();
    future = load();
  }

  Future<_PieceDetailData> load() async {
    final cardFuture = api
        .get('/production/pieces/${widget.pieceId}/work-card')
        .then((value) => value as Map<String, dynamic>);
    final scannersFuture = api.list('/production/scanners');

    final card = await cardFuture;
    final scanners = await scannersFuture;
    Map<String, dynamic>? route;
    final trackingCode = (card['trackingCode'] as String?)?.trim();
    if (trackingCode != null && trackingCode.isNotEmpty) {
      try {
        route = await api.getPieceRouteByTrackingCode(trackingCode);
        debugPrint(
            'Manual tracking route DEBUG: trackingCode=$trackingCode, route=${route ?? 'null'}');
      } catch (error, stackTrace) {
        debugPrint(
            'Manual tracking route ERROR: trackingCode=$trackingCode, error=$error');
        debugPrintStack(stackTrace: stackTrace);
        route = null;
      }
    }

    return _PieceDetailData(card: card, route: route, scanners: scanners);
  }

  void reload() {
    setState(() {
      _manualAdvanceBusy = false;
      future = load();
    });
  }

  Color _panelSurface(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? const Color(0xFF1A2B37) : const Color(0xFFF2F6F9);
  }

  Color _subtleSurface(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? const Color(0xFF0E181F) : const Color(0xFFFFFFFF);
  }

  Color _panelBorder(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? UiPalette.borderSoft : const Color(0xFFD9E3EE);
  }

  Future<void> _advanceManualStage(String requestedStage) async {
    final detail = await future;
    final productTypeId = int.tryParse(
          (detail.route?['productTypeId'] ?? '').toString(),
        ) ??
        0;
    final pieceType = (detail.route?['pieceType'] ?? detail.card['pieceType'])
        ?.toString()
        .trim();
    final trackingCode = (detail.card['trackingCode'] ?? '').toString().trim();
    final nextStage = (detail.route?['nextStage'] ?? '').toString().trim();

    if (productTypeId <= 0 || pieceType == null || pieceType.isEmpty) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تعذر تحديد نوع القطعة للانتقال اليدوي.'),
          backgroundColor: Colors.orange,
        ),
      );
      return;
    }

    if (requestedStage.isEmpty || requestedStage != nextStage) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('لا يمكن تنفيذ مرحلة غير المسموح بها.'),
          backgroundColor: Colors.orange,
        ),
      );
      return;
    }

    final scannerCode = '';
    if (!mounted) return;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('تأكيد التتبع اليدوي'),
        content: Text(
          'القطعة: ${detail.card['pieceType']}\nTrackingCode: $trackingCode\nالمرحلة: ${_stageLabel(requestedStage)}\n\nهل تريد تنفيذ هذه المرحلة الآن؟',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('تأكيد'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    setState(() => _manualAdvanceBusy = true);

    Map<String, dynamic> result;
    try {
      result = await api.advancePiece(
        trackingCode: trackingCode,
        pieceType: pieceType,
        productTypeId: productTypeId,
        requestedStage: requestedStage,
        scannerCode: '',
        employeeCode: '',
        operationReference:
            'ExecutionSource=ManualTest;Operation=ManualProductionAdvance;RequestedStage=$requestedStage;PerformedBySystemUser=admin',
      );
    } catch (error) {
      if (!mounted) return;
      final message = error is ProductionApiException
          ? (error.message ?? 'فشل تنفيذ المرحلة في Backend.')
          : 'تعذر تنفيذ المرحلة الحالية.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message), backgroundColor: Colors.red),
      );
      setState(() => _manualAdvanceBusy = false);
      return;
    }

    if (!mounted) return;
    if (result['updated'] != true) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(result['message']?.toString() ?? 'تعذر تنفيذ المرحلة.'),
          backgroundColor: Colors.orange,
        ),
      );
      setState(() => _manualAdvanceBusy = false);
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: const Text('تم تنفيذ المرحلة بنجاح.'),
        backgroundColor: Colors.green,
      ),
    );

    try {
      final refreshedDetails = await load();
      if (!mounted) return;
      setState(() {
        future = Future<_PieceDetailData>.value(refreshedDetails);
      });
      await widget.onRefreshed?.call();
    } catch (_) {
      // The API transaction already succeeded; the screen refresh is best effort.
    } finally {
      if (mounted) {
        setState(() => _manualAdvanceBusy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
      appBar: AppBar(title: const Text('تفاصيل القطعة'), actions: [
        IconButton(
            tooltip: 'تحديث',
            onPressed: reload,
            icon: const Icon(Icons.refresh))
      ]),
      body: FutureBuilder<_PieceDetailData>(
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

            final data = snapshot.data!;
            final card = data.card;
            final measurements = extractRealPieceMeasurements(
              parseMeasurementSnapshot(card['measurementSnapshot'] as String?),
            );
            final history = (card['trackingHistory'] as List? ?? const [])
                .cast<Map<String, dynamic>>();
            final notes = [card['notes1'], card['notes2']]
                .whereType<String>()
                .where((value) => value.trim().isNotEmpty)
                .join('\n');
            final specialRequests = [
              if ((card['request1'] ?? '').toString().trim().isNotEmpty)
                'طلب رقم 1: ${(card['request1'] ?? '').toString().trim()}',
              if ((card['request2'] ?? '').toString().trim().isNotEmpty)
                'طلب رقم 2: ${(card['request2'] ?? '').toString().trim()}',
              if ((card['specialRequest'] ?? '').toString().trim().isNotEmpty)
                'طلبات خاصة: ${(card['specialRequest'] ?? '').toString().trim()}',
            ];
            final routeStages = _buildManualStages(data.route);
            final currentStage = (card['pieceStatus'] ?? '').toString();
            final normalizedCurrent = currentStage.trim();
            final nextStage = _resolveManualNextStage(currentStage, data.route);
            final hasRealRoute = data.route != null && routeStages.isNotEmpty;
            final hasNoTrackingEvents = history.isEmpty;
            final isNewUnstartedPiece = (normalizedCurrent.isEmpty ||
                    normalizedCurrent == 'New' ||
                    normalizedCurrent == 'new') &&
                hasRealRoute &&
                hasNoTrackingEvents;
            debugPrint(
                'Manual tracking UI DEBUG: currentStage=$currentStage, nextStage=$nextStage, routeStages=$routeStages, route=${data.route}');

            final panelColor = _panelSurface(context);
            final subtleColor = _subtleSurface(context);
            final borderColor = _panelBorder(context);

            return ListView(padding: const EdgeInsets.all(20), children: [
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: panelColor,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: borderColor, width: 1.2),
                ),
                child: Wrap(
                  spacing: 10,
                  runSpacing: 12,
                  children: [
                    DetailField('كود العميل', card['customerCode']),
                    DetailField('اسم العميل', card['customerName']),
                    DetailField('الهاتف', card['phoneNumber']),
                    DetailField('رقم الطلب', card['orderNumber']),
                    DetailField(
                        'تاريخ التسليم', formatDate(card['deliveryDate'])),
                    DetailField('رقم القطعة', card['pieceNumber']),
                    DetailField('كود التتبع', card['trackingCode']),
                    DetailField('نوع القطعة', card['pieceType']),
                    DetailField(
                        'الحالة', _productionDisplayLabel(card['pieceStatus'])),
                    DetailField('نوع القماش', card['fabricType']),
                    DetailField('لون القماش', card['fabricColor'])
                  ],
                ),
              ),
              const SizedBox(height: 16),
              if (!hasRealRoute) ...[
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Theme.of(context).brightness == Brightness.dark
                        ? const Color(0xFF2F1F22)
                        : const Color(0xFFFDECEC),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: borderColor),
                  ),
                  child: Text(
                    'لا يوجد مسار إنتاج معرف لهذا النوع',
                    style: UiPalette.adaptiveTextStyle(
                      context,
                      backgroundColor:
                          Theme.of(context).brightness == Brightness.dark
                              ? const Color(0xFF2F1F22)
                              : const Color(0xFFFDECEC),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
              ] else if (isNewUnstartedPiece) ...[
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Theme.of(context).brightness == Brightness.dark
                        ? const Color(0xFF1F2C2A)
                        : const Color(0xFFEAF7F3),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: borderColor),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'لم تبدأ مراحل الإنتاج بعد',
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor:
                              Theme.of(context).brightness == Brightness.dark
                                  ? const Color(0xFF1F2C2A)
                                  : const Color(0xFFEAF7F3),
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'المرحلة التالية: ${_stageLabel(nextStage ?? routeStages.first)}',
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor:
                              Theme.of(context).brightness == Brightness.dark
                                  ? const Color(0xFF1F2C2A)
                                  : const Color(0xFFEAF7F3),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                Text('تتبع القطعة يدويًا',
                    style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 8),
                ManualTrackingStepper(
                  stages: routeStages,
                  currentStage: currentStage,
                  nextStage: nextStage,
                  isBusy: _manualAdvanceBusy,
                  onAdvance: _advanceManualStage,
                ),
                const SizedBox(height: 16),
              ] else ...[
                Text('تتبع القطعة يدويًا',
                    style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 8),
                ManualTrackingStepper(
                  stages: routeStages,
                  currentStage: currentStage,
                  nextStage: nextStage,
                  isBusy: _manualAdvanceBusy,
                  onAdvance: _advanceManualStage,
                ),
                const SizedBox(height: 16),
              ],
              Text('القياسات', style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: subtleColor,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: borderColor),
                ),
                child: MeasurementsView(values: measurements),
              ),
              const SizedBox(height: 16),
              Text('الطلبات الخاصة',
                  style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: subtleColor,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: borderColor),
                ),
                child: specialRequests.isEmpty
                    ? Text(
                        'لا توجد طلبات خاصة',
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor: subtleColor,
                        ),
                      )
                    : Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: specialRequests
                            .map((request) => Padding(
                                  padding: const EdgeInsets.only(bottom: 6),
                                  child: Text(
                                    request,
                                    style: UiPalette.adaptiveTextStyle(
                                      context,
                                      backgroundColor: subtleColor,
                                      fontWeight: FontWeight.w500,
                                    ),
                                  ),
                                ))
                            .toList(),
                      ),
              ),
              const SizedBox(height: 16),
              Text('الملاحظات', style: Theme.of(context).textTheme.titleLarge),
              Container(
                margin: const EdgeInsets.only(top: 8),
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: subtleColor,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: borderColor),
                ),
                child: Text(
                  notes.isEmpty ? '-' : notes,
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: subtleColor,
                  ),
                ),
              ),
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

class ManualTrackingStepper extends StatelessWidget {
  const ManualTrackingStepper({
    required this.stages,
    required this.currentStage,
    required this.nextStage,
    required this.isBusy,
    required this.onAdvance,
    super.key,
  });

  final List<String> stages;
  final String currentStage;
  final String? nextStage;
  final bool isBusy;
  final Future<void> Function(String requestedStage) onAdvance;

  @override
  Widget build(BuildContext context) {
    final completed = <String>{};
    final currentIndex = stages.indexOf(currentStage);
    if (currentIndex >= 0) {
      for (var i = 0; i <= currentIndex; i++) {
        completed.add(stages[i]);
      }
    }

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          for (final stage in stages)
            Padding(
              padding: const EdgeInsets.only(left: 8),
              child: _ManualTrackingStageButton(
                stage: stage,
                title: _stageLabel(stage),
                isCompleted: completed.contains(stage),
                isNext: stage == nextStage,
                isDisabled:
                    isBusy || stage != nextStage && !completed.contains(stage),
                onPressed: stage == nextStage && !isBusy
                    ? () => onAdvance(stage)
                    : null,
              ),
            ),
        ],
      ),
    );
  }
}

class _ManualTrackingStageButton extends StatelessWidget {
  const _ManualTrackingStageButton({
    required this.stage,
    required this.title,
    required this.isCompleted,
    required this.isNext,
    required this.isDisabled,
    required this.onPressed,
  });

  final String stage;
  final String title;
  final bool isCompleted;
  final bool isNext;
  final bool isDisabled;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final effectiveLabel = isCompleted ? 'تم $title' : title;
    final colorScheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final color = isCompleted
        ? colorScheme.primaryContainer
        : isNext
            ? colorScheme.primary
            : isDark
                ? const Color(0xFF1A2B37)
                : const Color(0xFFF2F6F9);

    return FilledButton(
      onPressed: isDisabled || isCompleted ? null : onPressed,
      style: FilledButton.styleFrom(
        backgroundColor: color,
        foregroundColor: isNext
            ? colorScheme.onPrimary
            : isCompleted
                ? colorScheme.onPrimaryContainer
                : UiPalette.adaptiveTextColor(color),
        disabledBackgroundColor: color,
        disabledForegroundColor: colorScheme.onSurfaceVariant,
      ),
      child: Text(effectiveLabel),
    );
  }
}

List<String> _buildManualStages(Map<String, dynamic>? route) {
  final rawValues = <Object?>[];

  if (route != null) {
    rawValues.add(route['route']);
    rawValues.add(route['stages']);
    rawValues.add(route['productionStages']);
  }

  final values = <String>[];
  for (final entry in rawValues) {
    if (entry is List) {
      for (final item in entry) {
        final value = item?.toString().trim();
        if (value != null && value.isNotEmpty && !values.contains(value)) {
          values.add(value);
        }
      }
    } else if (entry is String) {
      final value = entry.trim();
      if (value.isNotEmpty && !values.contains(value)) {
        values.add(value);
      }
    }
  }

  final normalized =
      values.where((stage) => stage != 'Ready' && stage != 'Delivery').toList();

  return normalized;
}

String? _resolveManualNextStage(
    String currentStage, Map<String, dynamic>? route) {
  final routeStages = _buildManualStages(route);
  if (routeStages.isEmpty) return null;

  final backendNext = (route?['nextStage'] ?? '').toString().trim();
  if (backendNext.isNotEmpty && routeStages.contains(backendNext)) {
    return backendNext;
  }

  final normalizedCurrent = currentStage.trim();
  if (normalizedCurrent.isEmpty || normalizedCurrent == 'New') {
    return routeStages.first;
  }

  final currentIndex = routeStages.indexOf(normalizedCurrent);
  if (currentIndex >= 0 && currentIndex + 1 < routeStages.length) {
    return routeStages[currentIndex + 1];
  }

  if (currentIndex == -1) {
    return routeStages.first;
  }

  return null;
}

String _stageLabel(String stage) {
  switch (stage.trim()) {
    case 'Printing':
      return 'الطباعة';
    case 'FabricPrep':
      return 'تجهيز القماش';
    case 'Cutting':
      return 'القص';
    case 'Sewing':
      return 'الخياطة';
    case 'Buttons':
      return 'الأزرار';
    case 'Ironing':
      return 'الكي';
    case 'Quality':
      return 'الجودة';
    case 'Assembly':
      return 'التجميع';
    default:
      return stage;
  }
}

String _productionDisplayLabel(Object? value) {
  final raw = value?.toString().trim() ?? '';
  if (raw.isEmpty) return raw;

  switch (raw.toLowerCase().replaceAll(' ', '')) {
    case 'new':
      return 'جديد';
    case 'ready':
    case 'readyfordelivery':
      return 'جاهزة للتسليم';
    case 'delivered':
      return 'تم التسليم';
    case 'inproduction':
    case 'inprogress':
      return 'قيد الإنتاج';
    case 'printing':
      return 'تمت الطباعة';
    case 'fabricprep':
      return 'تم تجهيز القماش';
    case 'cutting':
      return 'تم القص';
    case 'sewing':
      return 'تمت الخياطة';
    case 'buttons':
      return 'تم تركيب الأزرار';
    case 'ironing':
      return 'تم الكي';
    case 'quality':
      return 'تمت الجودة';
    case 'assembly':
      return 'تم التجميع';
    default:
      return _stageLabel(raw);
  }
}

List<String> _extractSpecialRequests(Map<String, dynamic> card) => [
      if ((card['request1'] ?? '').toString().trim().isNotEmpty)
        'طلب رقم 1: ${(card['request1'] ?? '').toString().trim()}',
      if ((card['request2'] ?? '').toString().trim().isNotEmpty)
        'طلب رقم 2: ${(card['request2'] ?? '').toString().trim()}',
      if ((card['specialRequest'] ?? '').toString().trim().isNotEmpty)
        'طلبات خاصة: ${(card['specialRequest'] ?? '').toString().trim()}',
    ];

class ProductionTimeline extends StatelessWidget {
  const ProductionTimeline({required this.events, super.key});
  final List<Map<String, dynamic>> events;

  @override
  Widget build(BuildContext context) {
    final ordered = [...events]..sort((a, b) {
        final aTime = DateTime.tryParse(
            (a['eventTime'] ?? a['createdAt'] ?? '').toString());
        final bTime = DateTime.tryParse(
            (b['eventTime'] ?? b['createdAt'] ?? '').toString());
        if (aTime == null && bTime == null) return 0;
        if (aTime == null) return 1;
        if (bTime == null) return -1;
        return bTime.compareTo(aTime);
      });

    if (ordered.isEmpty) {
      return const Text('لا توجد حركة إنتاج مسجلة على هذه القطعة.');
    }

    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Column(
      children: ordered.map((event) {
        final stage =
            (event['stage'] ?? event['status'] ?? 'غير محدد').toString();
        final status = (event['status'] ?? '').toString();
        final eventTime = event['eventTime'] ?? event['createdAt'] ?? '';
        final employee = (event['employeeCode'] ??
                event['employeeName'] ??
                event['employee'] ??
                '')
            .toString();
        final nextStage = (event['nextStage'] ?? '').toString().trim();
        final isManual = event['isManual'] == true ||
            event['manual'] == true ||
            (event['employeeCode'] ?? '').toString().trim().isEmpty &&
                (event['employeeName'] ?? '').toString().trim().isEmpty &&
                (event['employee'] ?? '').toString().trim().isEmpty;
        final displayEmployee = isManual
            ? 'تم تنفيذ المرحلة يدوياً'
            : (employee.isNotEmpty ? employee : 'غير محدد');
        final isReverted =
            event['isReverted'] == true || event['reverted'] == true;
        final surface =
            isDark ? const Color(0xFF0E181F) : const Color(0xFFFFFFFF);
        final border = isDark ? UiPalette.borderSoft : const Color(0xFFD9E3EE);

        return Container(
          margin: const EdgeInsets.only(bottom: 10),
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: surface,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: border),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(
                isReverted ? Icons.undo_rounded : Icons.check_circle_rounded,
                color: isReverted
                    ? Theme.of(context).colorScheme.error
                    : Theme.of(context).colorScheme.primary,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _productionDisplayLabel(stage),
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: surface,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      status.isNotEmpty
                          ? _productionDisplayLabel(status)
                          : 'غير محدد',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: surface,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${formatDateTime(eventTime)} • $displayEmployee',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: surface,
                        fontSize: 12,
                      ),
                    ),
                    if (nextStage.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          'المرحلة التالية: ${_stageLabel(nextStage)}',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: surface,
                            fontSize: 12,
                          ),
                        ),
                      ),
                    if (isReverted)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          'تمت إلغاء هذه الحركة',
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        );
      }).toList(),
    );
  }
}

class ProductionScanningTab extends StatefulWidget {
  const ProductionScanningTab(
      {required this.scanners,
      required this.api,
      required this.onRefreshed,
      super.key});

  final List<Map<String, dynamic>> scanners;
  final ProductionApi api;
  final VoidCallback onRefreshed;

  @override
  State<ProductionScanningTab> createState() => _ProductionScanningTabState();
}

class _ProductionScanningTabState extends State<ProductionScanningTab> {
  final trackingCodeController = TextEditingController();
  final scannerCodeController = TextEditingController();
  final employeeCodeController = TextEditingController();

  bool isLoadingRoute = false;
  bool isAdvancing = false;
  Map<String, dynamic>? routeData;
  Map<String, dynamic>? lastResult;
  String? errorMessage;

  @override
  void dispose() {
    trackingCodeController.dispose();
    scannerCodeController.dispose();
    employeeCodeController.dispose();
    super.dispose();
  }

  Future<void> loadRoute() async {
    final trackingCode = trackingCodeController.text.trim();
    if (trackingCode.isEmpty) {
      setState(() => errorMessage = 'يرجى إدخال TrackingCode أو مسحه أولاً.');
      return;
    }

    setState(() {
      isLoadingRoute = true;
      errorMessage = null;
      lastResult = null;
    });

    try {
      final route = await widget.api.getPieceRouteByTrackingCode(trackingCode);
      setState(() {
        routeData = route;
        if (route['nextStage'] != null &&
            scannerCodeController.text.trim().isEmpty) {
          final matchedScanner = widget.scanners.firstWhere(
              (scanner) => scanner['isActive'] == true,
              orElse: () => const <String, dynamic>{});
          if (matchedScanner.isNotEmpty) {
            scannerCodeController.text =
                matchedScanner['scannerCode']?.toString() ?? '';
          }
        }
      });
    } catch (error) {
      setState(() => errorMessage = error is ProductionApiException
          ? 'تعذر تحميل القطعة: ${error.statusCode}'
          : 'تعذر تحميل تفاصيل القطعة.');
    } finally {
      setState(() => isLoadingRoute = false);
    }
  }

  Future<void> advanceStage() async {
    final trackingCode = trackingCodeController.text.trim();
    final scannerCode = scannerCodeController.text.trim();
    final employeeCode = employeeCodeController.text.trim();
    final nextStage = routeData?['nextStage']?.toString();
    final productTypeId =
        int.tryParse((routeData?['productTypeId'] ?? '').toString()) ?? 0;

    if (trackingCode.isEmpty) {
      setState(() => errorMessage = 'يرجى إدخال TrackingCode أولاً.');
      return;
    }
    if (productTypeId <= 0) {
      setState(() =>
          errorMessage = 'تعذر تحديد معرف نوع المنتج الرسمي لهذه القطعة.');
      return;
    }
    if (nextStage == null || nextStage.isEmpty) {
      setState(() => errorMessage = 'لا توجد مرحلة لاحقة متاحة لهذه القطعة.');
      return;
    }
    if (scannerCode.isEmpty) {
      setState(() => errorMessage = 'يرجى تحديد كود الماسح قبل التأكيد.');
      return;
    }
    if (employeeCode.isEmpty) {
      setState(
          () => errorMessage = 'يرجى إدخال كود الموظف المسؤول عن المرحلة.');
      return;
    }

    setState(() {
      isAdvancing = true;
      errorMessage = null;
    });

    try {
      final result = await widget.api.advancePiece(
        trackingCode: trackingCode,
        pieceType: routeData?['pieceType']?.toString() ?? '',
        productTypeId: productTypeId,
        requestedStage: nextStage,
        scannerCode: scannerCode,
        employeeCode: employeeCode,
        isReadyMade: routeData?['isReadyMade'] == true,
      );

      setState(() => lastResult = result);
      if (result['updated'] == true) {
        final message =
            result['message']?.toString() ?? 'تم تحديث المرحلة بنجاح.';
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message), backgroundColor: Colors.green),
        );
        widget.onRefreshed();
        await loadRoute();
      } else {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content:
                  Text(result['message']?.toString() ?? 'تعذر تنفيذ المرحلة.'),
              backgroundColor: Colors.orange),
        );
      }
    } catch (error) {
      setState(() => errorMessage = error is ProductionApiException
          ? 'فشل تنفيذ المرحلة: ${error.statusCode}'
          : 'تعذر تنفيذ المرحلة الحالية.');
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
            content: Text(errorMessage ?? 'تعذر تنفيذ المرحلة.'),
            backgroundColor: Colors.red),
      );
    } finally {
      setState(() => isAdvancing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scannerOptions = widget.scanners
        .where((scanner) => scanner['isActive'] == true)
        .toList();

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: ListView(
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('مسح قطعة الإنتاج',
                        style: Theme.of(context).textTheme.titleLarge),
                    const SizedBox(height: 12),
                    TextField(
                      controller: trackingCodeController,
                      decoration: const InputDecoration(
                        labelText: 'TrackingCode',
                        prefixIcon: Icon(Icons.qr_code_2),
                        border: OutlineInputBorder(),
                      ),
                      textInputAction: TextInputAction.done,
                      onSubmitted: (_) => loadRoute(),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: DropdownButtonFormField<String>(
                            initialValue: scannerOptions.any((scanner) =>
                                    scanner['scannerCode']?.toString() ==
                                    scannerCodeController.text)
                                ? scannerCodeController.text
                                : null,
                            decoration: const InputDecoration(
                              labelText: 'الماسح',
                              border: OutlineInputBorder(),
                            ),
                            items: scannerOptions
                                .map((scanner) => DropdownMenuItem<String>(
                                    value: scanner['scannerCode']?.toString() ??
                                        '',
                                    child: Text(
                                        scanner['scannerCode']?.toString() ??
                                            '-')))
                                .toList(),
                            onChanged: (value) {
                              if (value != null)
                                scannerCodeController.text = value;
                            },
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: TextField(
                            controller: employeeCodeController,
                            decoration: const InputDecoration(
                              labelText: 'كود الموظف',
                              border: OutlineInputBorder(),
                            ),
                          ),
                        )
                      ],
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        FilledButton.icon(
                          onPressed: isLoadingRoute ? null : loadRoute,
                          icon: const Icon(Icons.search),
                          label: Text(isLoadingRoute
                              ? 'جاري التحميل...'
                              : 'تحميل القطعة'),
                        ),
                        const SizedBox(width: 12),
                        FilledButton.icon(
                          onPressed: isAdvancing || routeData == null
                              ? null
                              : advanceStage,
                          icon: const Icon(Icons.check_circle_outline),
                          label: Text(isAdvancing
                              ? 'جاري التنفيذ...'
                              : 'تأكيد المرحلة التالية'),
                          style: FilledButton.styleFrom(
                              backgroundColor: Colors.green),
                        )
                      ],
                    ),
                    if (errorMessage != null) ...[
                      const SizedBox(height: 10),
                      Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Colors.red.shade50,
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: Colors.red.shade200),
                        ),
                        child: Text(errorMessage!,
                            style: const TextStyle(color: Colors.red)),
                      )
                    ],
                  ],
                ),
              ),
            ),
            if (routeData != null) ...[
              const SizedBox(height: 16),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('تفاصيل القطعة',
                          style: Theme.of(context).textTheme.titleLarge),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 16,
                        runSpacing: 12,
                        children: [
                          DetailField('نوع القطعة', routeData!['pieceType']),
                          DetailField(
                              'المرحلة الحالية',
                              _productionDisplayLabel(
                                  routeData!['currentStage'])),
                          DetailField(
                              'المرحلة التالية',
                              _stageLabel(
                                  (routeData!['nextStage'] ?? '').toString())),
                          DetailField(
                              'مسار القطعة',
                              (routeData!['route'] as List?)
                                      ?.map((stage) =>
                                          _stageLabel(stage.toString()))
                                      .join(' → ') ??
                                  '-'),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ],
            if (lastResult != null) ...[
              const SizedBox(height: 16),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('نتيجة التنفيذ',
                          style: Theme.of(context).textTheme.titleLarge),
                      const SizedBox(height: 10),
                      Text(lastResult!['message']?.toString() ?? '-'),
                      const SizedBox(height: 8),
                      DetailField(
                          'الحالة السابقة',
                          _productionDisplayLabel(
                              lastResult!['previousStatus'])),
                      DetailField('الحالة الجديدة',
                          _productionDisplayLabel(lastResult!['newStatus'])),
                      DetailField(
                          'المرحلة التالية',
                          _stageLabel(
                              (lastResult!['nextStage'] ?? '').toString())),
                      DetailField('تم التنفيذ',
                          lastResult!['updated'] == true ? 'نعم' : 'لا'),
                    ],
                  ),
                ),
              )
            ]
          ],
        ),
      ),
    );
  }
}

class ReadyMadeTab extends StatefulWidget {
  const ReadyMadeTab(
      {required this.orders,
      required this.inventory,
      required this.readyPieces,
      required this.api,
      super.key});
  final List<Map<String, dynamic>> orders;
  final List<Map<String, dynamic>> inventory;
  final List<Map<String, dynamic>> readyPieces;
  final ProductionApi api;

  @override
  State<ReadyMadeTab> createState() => _ReadyMadeTabState();
}

class _ReadyMadeTabState extends State<ReadyMadeTab> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final query = _searchController.text.trim().toLowerCase();
    final filteredOrders = widget.orders.where((order) {
      if (query.isEmpty) return true;
      final number =
          (order['productionOrderNumber'] ?? '').toString().toLowerCase();
      final name = (order['productionName'] ?? '').toString().toLowerCase();
      final id =
          (order['readyMadeProductionOrderId'] ?? '').toString().toLowerCase();
      return number.contains(query) ||
          name.contains(query) ||
          id.contains(query);
    }).toList();

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(10, 8, 10, 4),
          child: TextField(
            controller: _searchController,
            onChanged: (_) => setState(() {}),
            textAlign: TextAlign.right,
            decoration: const InputDecoration(
              hintText: 'الاسم / الكود / الرقم',
              prefixIcon: Icon(Icons.search),
              border: OutlineInputBorder(),
              isDense: true,
            ),
          ),
        ),
        Expanded(
          child: filteredOrders.isEmpty
              ? const Center(
                  child: Text('لا توجد عناصر في الإنتاج الجاهز للبيع.'))
              : ListView.separated(
                  itemCount: filteredOrders.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (context, index) {
                    final order = filteredOrders[index];
                    return Card(
                      child: ExpansionTile(
                        title: Text(
                            '${order['productionOrderNumber']} - ${order['productionName']}'),
                        subtitle: Text('الحالة: ${order['status']}'),
                        children: [
                          FutureBuilder<List<Map<String, dynamic>>>(
                            future: widget.api.readyItems(
                              order['readyMadeProductionOrderId'] as int,
                            ),
                            builder: (context, snapshot) {
                              if (snapshot.connectionState !=
                                  ConnectionState.done) {
                                return const Padding(
                                  padding: EdgeInsets.all(16),
                                  child: CircularProgressIndicator(),
                                );
                              }
                              if (snapshot.hasError) {
                                return const ListTile(
                                  title: Text('تعذر تحميل بنود الأمر.'),
                                );
                              }
                              final items = snapshot.data ??
                                  const <Map<String, dynamic>>[];
                              return Column(
                                children: items
                                    .map((item) => ListTile(
                                          title: Text(
                                            '${item['pieceType']}  •  الكمية ${item['quantity']}',
                                          ),
                                          subtitle: Text(
                                            'القماش: ${item['fabricType'] ?? '-'}  •  الحالة: ${item['pieceStatus']}',
                                          ),
                                          trailing:
                                              const Icon(Icons.chevron_left),
                                          onTap: () => AppNavigation.push(
                                            context,
                                            (_) => ReadyMadeDetailsScreen(
                                              item: item,
                                              inventory: widget.inventory,
                                              readyPieces: widget.readyPieces,
                                              api: widget.api,
                                            ),
                                          ),
                                        ))
                                    .toList(),
                              );
                            },
                          ),
                        ],
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}

class ReadyMadeDetailsScreen extends StatelessWidget {
  const ReadyMadeDetailsScreen(
      {required this.item,
      required this.inventory,
      required this.readyPieces,
      required this.api,
      super.key});
  final Map<String, dynamic> item;
  final List<Map<String, dynamic>> inventory;
  final List<Map<String, dynamic>> readyPieces;
  final ProductionApi api;
  @override
    Widget build(BuildContext context) {
    final itemId = item['readyMadeProductionOrderItemId'] as int;
    final knownPieces = readyPieces
      .where((piece) => piece['orderItemId'] == itemId)
      .toList();
    return Scaffold(
      appBar: AppBar(title: Text('${item['pieceType']}')),
      body: FutureBuilder<List<Map<String, dynamic>>>(
        future: knownPieces.isNotEmpty
          ? Future<List<Map<String, dynamic>>>.value(knownPieces)
          : api.readyPieces(itemId),
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
                    return InkWell(
                      onTap: () => AppNavigation.push(
                        context,
                        (_) => ReadyMadePieceDetailsScreen(
                          piece: piece, api: api)),
                      child: Card(
                        child: Padding(
                          padding: const EdgeInsets.all(14),
                          child: Wrap(spacing: 18, runSpacing: 10, children: [
                            DetailField('رقم القطعة', piece['pieceNumber']),
                            DetailField('كود التتبع', piece['trackingCode']),
                            DetailField('حالة القطعة',
                                _productionDisplayLabel(piece['pieceStatus'])),
                            DetailField('تاريخ الإنشاء',
                                formatDate(piece['createdAt'])),
                            DetailField('سعر البيع المقترح',
                                stock?['suggestedSellingPrice']),
                            DetailField('حالة المخزون',
                                stock?['status'] ?? 'لم تدخل المخزون')
                          ]))));
                });
            }));
          }
}

class ReadyMadePieceDetailsScreen extends StatefulWidget {
  const ReadyMadePieceDetailsScreen({required this.piece, required this.api, super.key});

  final Map<String, dynamic> piece;
  final ProductionApi api;

  @override
  State<ReadyMadePieceDetailsScreen> createState() => _ReadyMadePieceDetailsScreenState();
}

class _ReadyMadePieceDetailsScreenState extends State<ReadyMadePieceDetailsScreen> {
  late Future<_ReadyMadePieceDetails> _future;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_ReadyMadePieceDetails> _load() async {
    final pieceId = widget.piece['readyMadeProductionOrderPieceInstanceId'] as int;
    final results = await Future.wait([
      widget.api.getReadyMadeWorkCard(pieceId),
      widget.api.get('/production/readymade-pieces/route?pieceId=$pieceId'),
      widget.api.getReadyMadePieceTracking(pieceId),
    ]);
    return _ReadyMadePieceDetails(
      card: results[0] as Map<String, dynamic>,
      route: results[1] as Map<String, dynamic>,
      history: results[2] as List<Map<String, dynamic>>,
    );
  }

  Future<void> _advance(String stage, _ReadyMadePieceDetails data) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await widget.api.advancePiece(
        trackingCode: (data.card['trackingCode'] ?? '').toString(),
        pieceType: (data.route['pieceType'] ?? data.card['pieceType'] ?? '').toString(),
        productTypeId: (data.route['productTypeId'] as num).toInt(),
        requestedStage: stage,
        scannerCode: '',
        employeeCode: '',
        operationReference: 'ExecutionSource=ManualTest;Operation=ReadyMadeProductionAdvance',
        isReadyMade: true,
      );
      if (mounted) setState(() => _future = _load());
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error is ProductionApiException ? (error.message ?? 'تعذر تنفيذ المرحلة.') : 'تعذر تنفيذ المرحلة.')),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: Text('${widget.piece['trackingCode'] ?? 'قطعة إنتاج جاهز'}'),
          actions: [
            IconButton(
              tooltip: 'بطاقة التشغيل',
              icon: const Icon(Icons.badge_outlined),
              onPressed: () => AppNavigation.push(
                context,
                (_) => WorkCardPreviewScreen(
                  pieceId: widget.piece['readyMadeProductionOrderPieceInstanceId'] as int,
                  readyMade: true,
                ),
              ),
            ),
          ],
        ),
        body: FutureBuilder<_ReadyMadePieceDetails>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return ProductionError(onRetry: () => setState(() => _future = _load()));
            }
            final data = snapshot.data!;
            final currentStage = (data.card['pieceStatus'] ?? 'New').toString();
            final stages = _buildManualStages(data.route);
            final nextStage = _resolveManualNextStage(currentStage, data.route);
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Wrap(spacing: 12, runSpacing: 12, children: [
                  DetailField('كود التتبع', data.card['trackingCode']),
                  DetailField('نوع القطعة', data.card['pieceType']),
                  DetailField('الحالة', _productionDisplayLabel(currentStage)),
                  DetailField('رقم أمر الإنتاج', data.card['orderNumber']),
                ]),
                const SizedBox(height: 16),
                Text('التتبع الرسمي', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 8),
                ManualTrackingStepper(
                  stages: stages,
                  currentStage: currentStage,
                  nextStage: nextStage,
                  isBusy: _busy,
                  onAdvance: (stage) => _advance(stage, data),
                ),
                const SizedBox(height: 16),
                Text('القياسات', style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 8),
                MeasurementsView(values: parseMeasurements(data.card['measurementSnapshot'] as String?)),
                const SizedBox(height: 16),
                Text('تاريخ التتبع', style: Theme.of(context).textTheme.titleLarge),
                ...data.history.map((event) => ListTile(
                      title: Text('${event['stage']} - ${event['status']}'),
                      subtitle: Text(formatDateTime(event['eventTime'])),
                    )),
              ],
            );
          },
        ),
      );
}

class _ReadyMadePieceDetails {
  const _ReadyMadePieceDetails({required this.card, required this.route, required this.history});
  final Map<String, dynamic> card;
  final Map<String, dynamic> route;
  final List<Map<String, dynamic>> history;
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
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final textColor = isDark ? UiPalette.textMain : const Color(0xFF1C2430);
    final labelColor = isDark ? UiPalette.textSoft : const Color(0xFF596A7B);

    return SizedBox(
      width: 190,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label,
            style: Theme.of(context)
                .textTheme
                .labelMedium
                ?.copyWith(color: labelColor)),
        SelectableText(value?.toString() ?? '-',
            style: Theme.of(context)
                .textTheme
                .titleSmall
                ?.copyWith(color: textColor, fontWeight: FontWeight.w600))
      ]),
    );
  }
}

class MeasurementsView extends StatelessWidget {
  const MeasurementsView({required this.values, super.key});
  final Map<String, dynamic> values;
  @override
  Widget build(BuildContext context) {
    final measurements = extractRealPieceMeasurements(values);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final chipColor =
        isDark ? const Color(0xFF1A2B37) : const Color(0xFFF1F5F9);
    final chipText = isDark ? UiPalette.textMain : const Color(0xFF1B2430);

    return measurements.isEmpty
        ? Text(
            'لا توجد قياسات محفوظة.',
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: isDark ? const Color(0xFF0E181F) : Colors.white,
            ),
          )
        : Wrap(
            spacing: 8,
            runSpacing: 8,
            children: measurements.entries
                .map((entry) => Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 10, vertical: 7),
                      decoration: BoxDecoration(
                        color: chipColor,
                        borderRadius: BorderRadius.circular(10),
                        border: Border.all(
                          color: isDark
                              ? UiPalette.borderSoft
                              : const Color(0xFFD8E2EE),
                        ),
                      ),
                      child: Text(
                        '${entry.key}: ${entry.value}',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            color: chipText, fontWeight: FontWeight.w600),
                      ),
                    ))
                .toList(),
          );
  }
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
  ProductionData({
    required this.dashboard,
    required this.pieces,
    this.orders = const [],
    this.orderItems = const [],
    this.readyPieces = const [],
    required this.readyOrders,
    required this.readyInventory,
    required this.wages,
    required this.scanners,
    required this.scans,
    required this.deliveries,
    this.routesByProductTypeId = const {},
  });

  final Map<String, dynamic> dashboard;
  final List<Map<String, dynamic>> pieces;
  final List<Map<String, dynamic>> orders;
  final List<Map<String, dynamic>> orderItems;
  final List<Map<String, dynamic>> readyPieces;
  final List<Map<String, dynamic>> readyOrders;
  final List<Map<String, dynamic>> readyInventory;
  final List<Map<String, dynamic>> wages;
  final List<Map<String, dynamic>> scanners;
  final List<Map<String, dynamic>> scans;
  final List<Map<String, dynamic>> deliveries;
  final Map<int, List<String>> routesByProductTypeId;
}

class ProductionApiException implements Exception {
  const ProductionApiException(this.statusCode, [this.message]);
  final int statusCode;
  final String? message;

  @override
  String toString() => message ?? 'ProductionApiException($statusCode)';
}

class ProductionApi {
  static const baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');

  Future<T> _withTimeout<T>(Future<T> work) {
    return work.timeout(const Duration(seconds: 20));
  }

  Future<dynamic> get(String path) async {
    final response = await _withTimeout(http.get(Uri.parse('$baseUrl$path')));
    if (response.statusCode < 200 || response.statusCode >= 300)
      throw ProductionApiException(response.statusCode);
    return jsonDecode(response.body);
  }

  Future<List<Map<String, dynamic>>> list(String path) async =>
      (await _withTimeout(get(path)) as List).cast<Map<String, dynamic>>();
  Future<ProductionData> load() async {
    final orders = await _withTimeout(list('/orders'));
    final orderItems = <Map<String, dynamic>>[];
    if (orders.isNotEmpty) {
      final itemsByOrder = await Future.wait(orders.map((order) {
        final orderId = (order['orderId'] ?? order['id'] ?? 0) as int;
        if (orderId <= 0) return Future.value(<Map<String, dynamic>>[]);
        return list('/orders/$orderId/items');
      }));
      for (final batch in itemsByOrder) {
        orderItems.addAll(batch);
      }
    }

    final values = await _withTimeout(Future.wait([
      get('/production/dashboard'),
      list('/production/pieces'),
      list('/production/readymade-pieces'),
      get('/settings/production-routes'),
      list('/production/readymade-orders'),
      list('/inventory/readymade'),
      list('/payroll/piece-wages'),
      list('/production/scanners'),
      list('/production/live-scan'),
      list('/production/deliveries')
    ]));
    return ProductionData(
      dashboard: values[0] as Map<String, dynamic>,
      pieces: values[1] as List<Map<String, dynamic>>,
      orders: orders,
      orderItems: orderItems,
      readyOrders: values[4] as List<Map<String, dynamic>>,
      readyInventory: values[5] as List<Map<String, dynamic>>,
      wages: values[6] as List<Map<String, dynamic>>,
      scanners: values[7] as List<Map<String, dynamic>>,
      scans: values[8] as List<Map<String, dynamic>>,
      deliveries: values[9] as List<Map<String, dynamic>>,
      readyPieces: values[2] as List<Map<String, dynamic>>,
      routesByProductTypeId: _parseProductionRoutes(values[3]),
    );
  }

  Map<int, List<String>> _parseProductionRoutes(dynamic payload) {
    final routes = payload is Map ? payload['routes'] : null;
    if (routes is! List) return const {};
    final result = <int, List<String>>{};
    for (final entry in routes) {
      if (entry is! Map) continue;
      final id = int.tryParse((entry['productTypeId'] ?? '').toString()) ?? 0;
      if (id <= 0 || entry['isEnabled'] == false) continue;
      final stages = entry['stages'];
      if (stages is List) {
        result[id] = stages
            .map((stage) => stage.toString())
            .where((stage) => stage.trim().isNotEmpty)
            .toList();
      }
    }
    return result;
  }

  Future<List<Map<String, dynamic>>> readyItems(int id) =>
      list('/production/readymade-orders/$id/items');
  Future<List<Map<String, dynamic>>> readyPieces(int id) =>
      list('/production/readymade-order-items/$id/pieces');

  Future<Map<String, dynamic>> getPieceRouteByTrackingCode(
      String trackingCode, {bool readyMade = false}) async {
    final encoded = Uri.encodeComponent(trackingCode.trim());
    if (readyMade) {
      return (await get('/production/readymade-pieces/route?trackingCode=$encoded')) as Map<String, dynamic>;
    }
    try {
      return (await get('/production/pieces/route?trackingCode=$encoded')) as Map<String, dynamic>;
    } on ProductionApiException catch (error) {
      if (error.statusCode != 404) rethrow;
      return (await get('/production/readymade-pieces/route?trackingCode=$encoded')) as Map<String, dynamic>;
    }
  }

    Future<Map<String, dynamic>> getReadyMadeWorkCard(int pieceId) async =>
      (await get('/production/readymade-pieces/$pieceId/work-card'))
        as Map<String, dynamic>;

    Future<List<Map<String, dynamic>>> getReadyMadePieceTracking(int pieceId) =>
      list('/production/readymade-pieces/$pieceId/tracking');

  Future<Map<String, dynamic>> advancePiece({
    required String trackingCode,
    required String pieceType,
    required int productTypeId,
    required String requestedStage,
    required String scannerCode,
    required String employeeCode,
    String? operationReference,
    bool isReadyMade = false,
  }) async {
    final uri = Uri.parse(isReadyMade
        ? '$baseUrl/production/readymade-pieces/advance'
        : '$baseUrl/production/pieces/advance');
    final payload = {
      'trackingCode': trackingCode,
      'pieceType': pieceType,
      'productTypeId': productTypeId,
      'requestedStage': requestedStage,
      'scannerCode': scannerCode,
      'employeeCode': employeeCode,
      'operationReference':
          operationReference ?? 'Flutter production manual tracking',
        'isReadyMade': isReadyMade,
    };

    final response = await http.post(
      uri,
      headers: {'Content-Type': 'application/json; charset=utf-8'},
      body: jsonEncode(payload),
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      dynamic decoded;
      try {
        decoded = jsonDecode(response.body);
      } catch (_) {
        decoded = null;
      }
      final message = decoded is Map<String, dynamic>
          ? (decoded['message'] ?? decoded['error'] ?? decoded['title'])
              ?.toString()
          : null;
      throw ProductionApiException(response.statusCode, message);
    }

    final decoded = jsonDecode(response.body);
    return decoded is Map<String, dynamic> ? decoded : <String, dynamic>{};
  }
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
