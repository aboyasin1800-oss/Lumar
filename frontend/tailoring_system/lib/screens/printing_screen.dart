import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import '../core/measurement_snapshot.dart';
import '../core/document_printing.dart';
import '../core/ui_palette.dart';

class PrintingScreen extends StatelessWidget {
  const PrintingScreen({super.key});

  @override
  Widget build(BuildContext context) => const PrintingCenterScreen();
}

class PrintingCenterScreen extends StatefulWidget {
  const PrintingCenterScreen({super.key});

  @override
  State<PrintingCenterScreen> createState() => _PrintingCenterScreenState();
}

class _PrintingCenterScreenState extends State<PrintingCenterScreen>
    with SingleTickerProviderStateMixin {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  late final TabController _tabController;
  late Future<_PrintingCenterState> _future;
  final TextEditingController _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
    _future = _loadCenter();
  }

  @override
  void dispose() {
    _tabController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  Future<_PrintingCenterState> _loadCenter() async {
    final ordersResponse = await http.get(Uri.parse('$_baseUrl/orders'));
    if (ordersResponse.statusCode < 200 || ordersResponse.statusCode >= 300) {
      throw Exception('تعذر تحميل الطلبات.');
    }
    final readyOrdersResponse = await http.get(
      Uri.parse('$_baseUrl/production/readymade-orders'),
    );

    final customersResponse = await http.get(Uri.parse('$_baseUrl/customers'));
    final Map<int, _CustomerSnapshot> customers = {};
    if (customersResponse.statusCode >= 200 &&
        customersResponse.statusCode < 300) {
      final body = jsonDecode(customersResponse.body);
      if (body is List) {
        for (final item in body) {
          if (item is Map<String, dynamic>) {
            final id = (item['customerId'] as int?) ?? 0;
            if (id > 0) {
              customers[id] = _CustomerSnapshot(
                name: (item['customerName'] ?? '').toString(),
                phone: (item['phoneNumber'] ?? '').toString(),
              );
            }
          }
        }
      }
    }

    final orderList = (jsonDecode(ordersResponse.body) as List)
        .cast<Map<String, dynamic>>()
        .map((entry) => _OrderSummary.fromJson(entry, customers))
        .toList()
      ..sort((a, b) => b.orderDate.compareTo(a.orderDate));
    if (readyOrdersResponse.statusCode >= 200 &&
        readyOrdersResponse.statusCode < 300) {
      orderList.addAll(
        (jsonDecode(readyOrdersResponse.body) as List)
            .cast<Map<String, dynamic>>()
            .map((entry) => _OrderSummary.fromReadyMadeJson(entry)),
      );
      orderList.sort((a, b) => b.orderDate.compareTo(a.orderDate));
    }

    final header = await _loadHeaderSettings();
    return _PrintingCenterState(
      orders: orderList,
      header: header,
    );
  }

  Future<_MeasurementHeaderSettings> _loadHeaderSettings() async {
    final entries = <String, String>{};
    final keys = [
      'MeasurementCardHeaderImage',
      'MeasurementCardHeaderUseImage',
      'MeasurementCardHeaderName',
      'MeasurementCardLocation',
      'MeasurementCardPhone1',
      'MeasurementCardPhone2',
    ];

    for (final key in keys) {
      final response = await http.get(
        Uri.parse('$_baseUrl/settings/by-key/${Uri.encodeComponent(key)}'),
      );
      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is Map<String, dynamic>) {
          entries[key] = (decoded['settingValue'] ?? '').toString();
        }
      }
    }

    final useImage = _toBool(entries['MeasurementCardHeaderUseImage']);
    return _MeasurementHeaderSettings(
      useImage: useImage,
      imageValue: entries['MeasurementCardHeaderImage'] ?? '',
      name: entries['MeasurementCardHeaderName']?.trim().isNotEmpty == true
          ? entries['MeasurementCardHeaderName']!
          : 'LUMAR',
      location: entries['MeasurementCardLocation'] ?? '',
      phone1: entries['MeasurementCardPhone1'] ?? '',
      phone2: entries['MeasurementCardPhone2'] ?? '',
    );
  }

  bool _toBool(String? value) {
    if (value == null) return false;
    final normalized = value.trim().toLowerCase();
    return normalized == 'true' || normalized == '1' || normalized == 'yes';
  }

  Future<void> _refresh() async {
    setState(() {
      _future = _loadCenter();
    });
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        title: const Text('مركز الطباعة'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_outlined),
          ),
        ],
      ),
      body: SafeArea(
        child: FutureBuilder<_PrintingCenterState>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline, size: 42),
                    const SizedBox(height: 12),
                    Text(
                      'تعذر تحميل مركز الطباعة.\n${snapshot.error}',
                      textAlign: TextAlign.center,
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.screenBackground,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _refresh,
                      icon: const Icon(Icons.refresh),
                      label: const Text('إعادة المحاولة'),
                    ),
                  ],
                ),
              );
            }

            final state = snapshot.data!;
            final filtered = state.orders.where((order) {
              final query = _searchController.text.trim().toLowerCase();
              if (query.isEmpty) return true;
              return order.customerName.toLowerCase().contains(query) ||
                  order.customerPhone.contains(query) ||
                  order.orderNumber.toLowerCase().contains(query) ||
                  order.orderId.toString().contains(query);
            }).toList();

            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TabBar(
                  controller: _tabController,
                  isScrollable: false,
                  tabAlignment: TabAlignment.fill,
                  tabs: const [
                    Tab(icon: Icon(Icons.credit_card), text: 'بطاقات المقاسات'),
                    Tab(
                        icon: Icon(Icons.receipt_long_outlined),
                        text: 'الإيصالات'),
                    Tab(
                        icon: Icon(Icons.description_outlined),
                        text: 'سندات القبض'),
                    Tab(
                        icon: Icon(Icons.fact_check_outlined),
                        text: 'الفواتير'),
                  ],
                ),
                const SizedBox(height: 7),
                Expanded(
                  child: TabBarView(
                    controller: _tabController,
                    children: [
                      _MeasurementCardsTab(
                        orders: filtered,
                        header: state.header,
                        baseUrl: _baseUrl,
                        onRefresh: _refresh,
                      ),
                      _PlaceholderTab(
                        title: 'طباعة الإيصالات',
                        subtitle: 'سيتم تنفيذها في المرحلة القادمة.',
                      ),
                      _PlaceholderTab(
                        title: 'طباعة سندات القبض',
                        subtitle: 'سيتم تنفيذها في المرحلة القادمة.',
                      ),
                      _PlaceholderTab(
                        title: 'طباعة الفواتير',
                        subtitle: 'سيتم تنفيذها في المرحلة القادمة.',
                      ),
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
}

class _MeasurementCardsTab extends StatefulWidget {
  const _MeasurementCardsTab({
    required this.orders,
    required this.header,
    required this.baseUrl,
    required this.onRefresh,
  });

  final List<_OrderSummary> orders;
  final _MeasurementHeaderSettings header;
  final String baseUrl;
  final Future<void> Function() onRefresh;

  @override
  State<_MeasurementCardsTab> createState() => _MeasurementCardsTabState();
}

class _MeasurementCardsTabState extends State<_MeasurementCardsTab> {
  final _searchController = TextEditingController();
  _OrderSummary? _selectedOrder;
  List<_OrderPieceDetail> _pieces = const [];
  final Map<int, String> _pieceTypeLabels = {};
  bool _loadingPieces = false;
  bool _loadingUnprintedSummary = false;
  int _allUnprintedCards = 0;

  @override
  void initState() {
    super.initState();
    _refreshUnprintedSummary();
  }

  @override
  void didUpdateWidget(covariant _MeasurementCardsTab oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.orders != widget.orders) {
      _refreshUnprintedSummary();
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _refreshUnprintedSummary() async {
    if (_loadingUnprintedSummary) return;
    setState(() {
      _loadingUnprintedSummary = true;
    });

    try {
      int totalUnprintedCards = 0;

      for (final order in widget.orders) {
        if (order.isCancelled) continue;
        final pieces = await _fetchOrderPiecesOnce(order);
        final unprinted = pieces.where((piece) => !piece.isPrinted).length;
        totalUnprintedCards += unprinted;
      }

      if (mounted) {
        setState(() {
          _allUnprintedCards = totalUnprintedCards;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _allUnprintedCards = 0;
        });
      }
    } finally {
      if (mounted) {
        setState(() {
          _loadingUnprintedSummary = false;
        });
      }
    }
  }

  Future<List<_OrderPieceDetail>> _fetchOrderPiecesOnce(_OrderSummary order) async {
    final response = await http.get(Uri.parse(order.isReadyMade
      ? '${widget.baseUrl}/production/readymade-orders/${order.orderId}/items'
      : '${widget.baseUrl}/orders/${order.orderId}/pieces'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      return const [];
    }

    final piecesJson = jsonDecode(response.body) as List;
    final pieces = <_OrderPieceDetail>[];
    for (final entry in piecesJson) {
      if (entry is Map<String, dynamic>) {
        if (order.isReadyMade) {
          final itemId = (entry['readyMadeProductionOrderItemId'] as int?) ?? 0;
          if (itemId <= 0) continue;
          final pieceResponse = await http.get(Uri.parse(
              '${widget.baseUrl}/production/readymade-order-items/$itemId/pieces'));
          if (pieceResponse.statusCode < 200 || pieceResponse.statusCode >= 300) continue;
          for (final pieceJson in (jsonDecode(pieceResponse.body) as List).cast<Map<String, dynamic>>()) {
            final pieceId = (pieceJson['readyMadeProductionOrderPieceInstanceId'] as int?) ?? 0;
            if (pieceId > 0) {
              try { pieces.add(await _loadPieceCard(pieceId, readyMade: true)); } catch (_) {}
            }
          }
        } else {
          final pieceId = (entry['pieceId'] as int?) ?? 0;
          if (pieceId <= 0) continue;
          try {
            pieces.add(await _loadPieceCard(pieceId));
          } catch (_) {
            // ignore unreadable pieces in aggregated summary
          }
        }
      }
    }

    return pieces;
  }

  Future<void> _selectOrder(_OrderSummary order) async {
    setState(() {
      _selectedOrder = order;
      _loadingPieces = true;
    });

    try {
      final response = await http.get(Uri.parse(order.isReadyMade
          ? '${widget.baseUrl}/production/readymade-orders/${order.orderId}/items'
          : '${widget.baseUrl}/orders/${order.orderId}/pieces'));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر تحميل قطع الطلب.');
      }

      final piecesJson = jsonDecode(response.body) as List;
      final pieces = <_OrderPieceDetail>[];
      for (final entry in piecesJson) {
        if (entry is Map<String, dynamic>) {
          if (order.isReadyMade) {
            final itemId = (entry['readyMadeProductionOrderItemId'] as int?) ?? 0;
            if (itemId <= 0) continue;
            final pieceResponse = await http.get(Uri.parse(
                '${widget.baseUrl}/production/readymade-order-items/$itemId/pieces'));
            if (pieceResponse.statusCode < 200 || pieceResponse.statusCode >= 300) continue;
            for (final pieceJson in (jsonDecode(pieceResponse.body) as List).cast<Map<String, dynamic>>()) {
              final pieceId = (pieceJson['readyMadeProductionOrderPieceInstanceId'] as int?) ?? 0;
              if (pieceId > 0) pieces.add(await _loadPieceCard(pieceId, readyMade: true));
            }
          } else {
            final pieceId = (entry['pieceId'] as int?) ?? 0;
            if (pieceId <= 0) continue;
            pieces.add(await _loadPieceCard(pieceId));
          }
        }
      }

      final typeCounts = <String, int>{};
      for (final piece in pieces) {
        final key = piece.pieceType.trim();
        if (key.isEmpty) continue;
        typeCounts[key] = (typeCounts[key] ?? 0) + 1;
      }

      final ordinalByType = <String, int>{};
      final labels = <int, String>{};
      for (final piece in pieces) {
        final key = piece.pieceType.trim();
        if (key.isEmpty) {
          labels[piece.pieceId] = piece.pieceType;
          continue;
        }
        final current = (ordinalByType[key] ?? 0) + 1;
        ordinalByType[key] = current;
        final total = typeCounts[key] ?? current;
        labels[piece.pieceId] = '$key ($current من $total)';
      }

      setState(() {
        _pieces = pieces;
        _pieceTypeLabels.clear();
        _pieceTypeLabels.addAll(labels);
      });
    } catch (_) {
      setState(() {
        _pieces = const [];
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content: Text('تعذر تحميل بطاقات المقاسات لهذا الطلب.')),
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          _loadingPieces = false;
        });
      }
    }
  }

  Future<_OrderPieceDetail> _loadPieceCard(int pieceId, {bool readyMade = false}) async {
    final response = await http.get(
      Uri.parse(readyMade
          ? '${widget.baseUrl}/production/readymade-pieces/$pieceId/work-card'
          : '${widget.baseUrl}/production/pieces/$pieceId/work-card'),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تحميل تفاصيل القطعة.');
    }

    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return _OrderPieceDetail.fromJson(json);
  }

  Future<void> _showReprintDialog(_OrderPieceDetail piece) async {
    final stateContext = context;
    final reasonController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: stateContext,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('إعادة الطباعة'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('يرجى إدخال سبب إعادة الطباعة ثم تأكيد التنفيذ.'),
              const SizedBox(height: 12),
              TextField(
                controller: reasonController,
                minLines: 2,
                maxLines: 4,
                decoration: const InputDecoration(
                  border: OutlineInputBorder(),
                  hintText: 'سبب إعادة الطباعة',
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('إلغاء'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(true),
              child: const Text('تأكيد وإعادة الطباعة'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !mounted) return;

    final reason = reasonController.text.trim();
    if (reason.isEmpty) {
      final messenger = ScaffoldMessenger.maybeOf(stateContext);
      messenger?.showSnackBar(
        const SnackBar(
            content: Text('يجب إدخال سبب إعادة الطباعة قبل المتابعة.')),
      );
      return;
    }

    final messenger = ScaffoldMessenger.maybeOf(stateContext);
    messenger?.showSnackBar(
      SnackBar(content: Text('تم تسجيل سبب إعادة الطباعة: $reason')),
    );

    await _PrintService.execute(
      context: stateContext,
      piece: piece,
      header: widget.header,
      target: _PrintTarget.officePrinter,
    );
  }

  Future<void> _printSelectedPiece(_OrderPieceDetail piece) async {
    if (!mounted) return;

    final target = await _showPrintOptionsDialog(title: 'طباعة بطاقة محددة');
    if (target == null || !mounted) return;

    if (piece.isPrinted && target == _PrintTarget.officePrinter) {
      await _showReprintDialog(piece);
      return;
    }

    await _PrintService.execute(
      context: context,
      piece: piece,
      header: widget.header,
      target: target,
    );
  }

  Future<_PrintTarget?> _showPrintOptionsDialog({required String title}) async {
    return showModalBottomSheet<_PrintTarget>(
      context: context,
      backgroundColor: UiPalette.surfaceCard,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (sheetContext) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  title,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                      fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 12),
                ListTile(
                  leading: const Icon(Icons.save_alt_outlined),
                  title: const Text('حفظ PDF'),
                  onTap: () =>
                      Navigator.of(sheetContext).pop(_PrintTarget.savePdf),
                ),
                ListTile(
                  leading: const Icon(Icons.print_outlined),
                  title: const Text('طباعة على طابعة مكتبية'),
                  onTap: () => Navigator.of(sheetContext)
                      .pop(_PrintTarget.officePrinter),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _confirmAndPrintAllUnprintedCards() async {
    final stateContext = context;
    if (_allUnprintedCards <= 0) {
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(stateContext);
      messenger?.showSnackBar(
        const SnackBar(content: Text('لا توجد بطاقات غير مطبوعة حالياً.')),
      );
      return;
    }

    final target = await _showPrintOptionsDialog(
        title: 'طباعة جميع البطاقات النشطة غير المطبوعة');
    if (target == null || !mounted) return;

    for (final order in widget.orders) {
      if (order.isCancelled) continue;
      final pieces = await _fetchOrderPiecesOnce(order);
      for (final piece in pieces.where((entry) => !entry.isPrinted)) {
        await _PrintService.execute(
          context: stateContext,
          piece: piece,
          header: widget.header,
          target: target,
        );
      }
    }
  }

  Widget _buildOrderList(List<_OrderSummary> filtered) {
    if (filtered.isEmpty) {
      return const Center(child: Text('لا توجد طلبات مطابقة للبحث الحالي.'));
    }

    return ListView.separated(
      padding: const EdgeInsets.only(bottom: 8),
      itemCount: filtered.length,
      separatorBuilder: (_, __) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final order = filtered[index];
        final isSelected = order.orderId == _selectedOrder?.orderId &&
            order.isReadyMade == _selectedOrder?.isReadyMade;
        final backgroundColor =
            isSelected ? UiPalette.primaryDark : UiPalette.surfaceCard;
        final secondaryText = order.customerPhone.trim().isEmpty
            ? 'بدون رقم هاتف'
            : order.customerPhone;
        final orderKind = order.isReadyMade ? 'إنتاج جاهز' : 'طلب تفصيل';
        final status = order.isCancelled ? 'ملغي' : orderKind;

        return Material(
          color: backgroundColor,
          borderRadius: BorderRadius.circular(12),
          child: InkWell(
            borderRadius: BorderRadius.circular(12),
            onTap: () => _selectOrder(order),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              child: Row(
                children: [
                  Icon(
                    order.isReadyMade
                        ? Icons.inventory_2_outlined
                        : Icons.receipt_long_outlined,
                    color: UiPalette.adaptiveTextColor(backgroundColor),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          order.orderNumber.isEmpty
                              ? 'طلب رقم ${order.orderId}'
                              : order.orderNumber,
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: backgroundColor,
                            fontSize: 15,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${order.customerName} • $secondaryText',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: backgroundColor,
                            fontSize: 12,
                          ),
                        ),
                        const SizedBox(height: 3),
                        Text(
                          '${DateFormat('yyyy/MM/dd').format(order.orderDate)} • $status',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: backgroundColor,
                            fontSize: 11,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Icon(
                    isSelected
                        ? Icons.keyboard_arrow_down_rounded
                        : Icons.chevron_left_rounded,
                    color: UiPalette.adaptiveTextColor(backgroundColor),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildSelectedOrderHeader(BuildContext context) {
    final order = _selectedOrder!;
    final backgroundColor = UiPalette.softBlue;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  order.orderNumber.isEmpty
                      ? 'طلب رقم ${order.orderId}'
                      : order.orderNumber,
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: backgroundColor,
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  '${order.customerName} • ${order.isReadyMade ? 'إنتاج جاهز' : 'طلب تفصيل'}',
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: backgroundColor,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          TextButton.icon(
            onPressed: () {
              setState(() {
                _selectedOrder = null;
                _pieces = const [];
                _pieceTypeLabels.clear();
              });
            },
            icon: const Icon(Icons.list_alt_outlined),
            label: const Text('قائمة الطلبات'),
          ),
        ],
      ),
    );
  }

  Widget _buildPiecesList(BuildContext context) {
    if (_loadingPieces) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_pieces.isEmpty) {
      return const Center(child: Text('لا توجد بطاقات مقاسات لهذا الطلب.'));
    }

    return ListView.separated(
      padding: const EdgeInsets.only(top: 10, bottom: 8),
      itemCount: _pieces.length,
      separatorBuilder: (_, __) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final piece = _pieces[index];
        final isPrinted = piece.isPrinted;
        final title = _pieceTypeLabels[piece.pieceId]?.trim().isNotEmpty == true
            ? _pieceTypeLabels[piece.pieceId]!
            : piece.pieceType.trim().isNotEmpty
                ? piece.pieceType
                : 'القطعة ${piece.pieceNumber}';
        final statusText = isPrinted ? 'مطبوعة - إعادة الطباعة متاحة' : 'غير مطبوعة';

        return Material(
          color: UiPalette.surfaceCard,
          borderRadius: BorderRadius.circular(12),
          child: ListTile(
            contentPadding:
                const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            leading: Icon(
              isPrinted
                  ? Icons.check_circle_outline
                  : Icons.print_disabled_outlined,
              color: isPrinted ? Colors.greenAccent : Colors.orangeAccent,
            ),
            title: Text(
              title,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 14,
                fontWeight: FontWeight.bold,
              ),
            ),
            subtitle: Text(
              '$statusText • رقم القطعة ${piece.pieceNumber}',
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 12,
              ),
            ),
            trailing: FilledButton.icon(
              onPressed: () => _printSelectedPiece(piece),
              icon: const Icon(Icons.print_outlined, size: 17),
              label: Text(isPrinted ? 'إعادة الطباعة' : 'طباعة'),
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final filtered = widget.orders.where((order) {
      final query = _searchController.text.trim().toLowerCase();
      if (query.isEmpty) return true;
      return order.customerName.toLowerCase().contains(query) ||
          order.customerPhone.contains(query) ||
          order.orderNumber.toLowerCase().contains(query) ||
          order.orderId.toString().contains(query);
    }).toList();

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        children: [
          TextField(
            controller: _searchController,
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(
              filled: true,
              fillColor: UiPalette.surfaceCard,
              prefixIcon: const Icon(Icons.search_outlined),
              hintText: 'ابحث باسم العميل أو رقم الهاتف أو رقم الطلب',
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(12),
              ),
              contentPadding:
                  const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: _allUnprintedCards > 0
                      ? _confirmAndPrintAllUnprintedCards
                      : null,
                  icon: const Icon(Icons.print_disabled_outlined),
                  label: _loadingUnprintedSummary
                      ? const Text('جارٍ الحساب...')
                      : Text(
                          'طباعة جميع البطاقات غير المطبوعة ($_allUnprintedCards)'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (_selectedOrder != null) ...[
            _buildSelectedOrderHeader(context),
            const SizedBox(height: 10),
            SizedBox(
              height: 210,
              child: _buildOrderList(filtered),
            ),
            const SizedBox(height: 10),
            Expanded(child: _buildPiecesList(context)),
          ] else
            Expanded(child: _buildOrderList(filtered)),
        ],
      ),
    );
  }
}

enum _PrintTarget {
  pdfPreview,
  savePdf,
  officePrinter,
  labelPrinter,
}

class _PrintService {
  static const String _defaultPdfSaveRoot =
      r'D:\تجارب طباعة المقاسات والمستندات';

  static Future<String> _resolveSaveDirectory(BuildContext context) async {
    final defaultDirectory = Directory(_defaultPdfSaveRoot);
    if (!await defaultDirectory.exists()) {
      await defaultDirectory.create(recursive: true);
    }

    final controller = TextEditingController(text: defaultDirectory.path);
    final selectedPath = await showDialog<String>(
      context: context,
      builder: (dialogContext) {
        return AlertDialog(
          title: const Text('مسار حفظ ملفات PDF'),
          content: SizedBox(
            width: 420,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('اختر مسار الحفظ أو عدّل المسار الافتراضي.'),
                const SizedBox(height: 12),
                TextField(
                  controller: controller,
                  minLines: 1,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    border: OutlineInputBorder(),
                    hintText: r'D:\تجارب طباعة المقاسات والمستندات',
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('إلغاء'),
            ),
            FilledButton(
              onPressed: () {
                final trimmed = controller.text.trim();
                final targetPath =
                    trimmed.isNotEmpty ? trimmed : defaultDirectory.path;
                Navigator.of(dialogContext).pop(targetPath);
              },
              child: const Text('حفظ هنا'),
            ),
          ],
        );
      },
    );

    final finalPath = (selectedPath ?? defaultDirectory.path).trim();
    final directory = Directory(finalPath);
    if (!await directory.exists()) {
      await directory.create(recursive: true);
    }
    return directory.path;
  }

  static Future<void> execute({
    required BuildContext context,
    required _OrderPieceDetail piece,
    required _MeasurementHeaderSettings header,
    required _PrintTarget target,
  }) async {
    final messenger = ScaffoldMessenger.maybeOf(context);
    switch (target) {
      case _PrintTarget.pdfPreview:
        final bytes =
            await _MeasurementCardPdfGenerator.generate(piece, header);
        await Printing.layoutPdf(onLayout: (_) => bytes);
        messenger?.showSnackBar(
          const SnackBar(content: Text('تم تجهيز معاينة PDF لبطاقة المقاسات.')),
        );
        break;
      case _PrintTarget.savePdf:
        final bytes =
            await _MeasurementCardPdfGenerator.generate(piece, header);
        final directoryPath = await _resolveSaveDirectory(context);
        final fileName =
            'measurement_card_${DateTime.now().millisecondsSinceEpoch}.pdf';
        final fullPath = '${directoryPath}${Platform.pathSeparator}$fileName';
        final file = File(fullPath);
        await file.writeAsBytes(bytes);
        messenger?.showSnackBar(
          SnackBar(content: Text('تم حفظ PDF في: $fullPath')),
        );
        break;
      case _PrintTarget.officePrinter:
        final bytes =
            await _MeasurementCardPdfGenerator.generate(piece, header);
        await Printing.layoutPdf(onLayout: (_) => bytes);
        messenger?.showSnackBar(
          const SnackBar(
              content:
                  Text('تم إرسال الملف إلى Print Dialog للطباعة الورقية.')),
        );
        break;
      case _PrintTarget.labelPrinter:
        messenger?.showSnackBar(
          const SnackBar(
            content: Text(
                'طباعة Label Printer مستعدة للتوسعة لاحقاً؛ لا يوجد تكامل فعلي للطابعة الحالية في هذه المرحلة.'),
          ),
        );
        break;
    }
  }
}

class _MeasurementCardPreview extends StatelessWidget {
  const _MeasurementCardPreview({
    required this.piece,
    required this.header,
    required this.onPrintSingle,
    required this.onReprintRequest,
  });

  final _OrderPieceDetail piece;
  final _MeasurementHeaderSettings header;
  final Future<void> Function() onPrintSingle;
  final Future<void> Function(_OrderPieceDetail piece) onReprintRequest;

  @override
  Widget build(BuildContext context) {
    final qrValue = piece.qrCode.trim().isNotEmpty ? piece.qrCode : 'LUMAR';
    final barcodeValue =
        piece.barcode.trim().isNotEmpty ? piece.barcode : piece.trackingCode;
    final pieceTypeLabel = piece.pieceType;
    final fabricRows = <Widget>[];
    final specialRequests = <Widget>[];

    final fabricSectionHasContent = piece.fabricCode.isNotEmpty ||
        piece.fabricType.isNotEmpty ||
        piece.fabricColor.isNotEmpty ||
        piece.catalogNumber.isNotEmpty ||
        piece.consumptionDisplay.isNotEmpty;

    final specialSectionHasContent = piece.request1.isNotEmpty ||
        piece.request2.isNotEmpty ||
        piece.specialRequest.isNotEmpty;

    if (fabricSectionHasContent) {
      final row1 = Row(
        children: [
          Expanded(
              child: _InfoRow(
                  label: 'كود القماش',
                  value: piece.fabricCode.isNotEmpty
                      ? piece.fabricCode
                      : 'غير محدد')),
          Expanded(
              child: _InfoRow(
                  label: 'نوع القماش',
                  value: piece.fabricType.isNotEmpty
                      ? piece.fabricType
                      : 'غير محدد')),
        ],
      );
      final row2 = Row(
        children: [
          Expanded(
              child: _InfoRow(
                  label: 'لون القماش',
                  value: piece.fabricColor.isNotEmpty
                      ? piece.fabricColor
                      : 'غير محدد')),
          Expanded(
              child: _InfoRow(
                  label: 'رقم الكتالوج',
                  value: piece.catalogNumber.isNotEmpty
                      ? piece.catalogNumber
                      : 'غير محدد')),
        ],
      );
      final row3 = Row(
        children: [
          Expanded(
              child: _InfoRow(
                  label: 'الاستهلاك',
                  value: piece.consumptionDisplay.isNotEmpty
                      ? piece.consumptionDisplay
                      : 'غير محدد')),
          const Expanded(child: SizedBox()),
        ],
      );
      fabricRows.addAll([row1, row2, row3]);
    }

    if (specialSectionHasContent) {
      specialRequests.add(_InfoRow(
          label: '  طلب رقم1',
          value: piece.request1.isNotEmpty ? piece.request1 : 'لا يوجد'));
      specialRequests.add(_InfoRow(
          label: 'طلب رقم2',
          value: piece.request2.isNotEmpty ? piece.request2 : 'لا يوجد'));
      specialRequests.add(_InfoRow(
          label: 'طلبات خاصة',
          value: piece.specialRequest.isNotEmpty
              ? piece.specialRequest
              : 'لا يوجد'));
    }

    return Directionality(
      textDirection: Directionality.of(context),
      child: Container(
        width: 330,
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border.all(color: Colors.black, width: 1.6),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _buildHeader(context),
            const Divider(height: 1, color: Colors.black),
            Container(
              decoration: BoxDecoration(
                border: Border.all(color: Colors.black, width: 1.2),
              ),
              padding: const EdgeInsets.all(8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _InfoRow(
                            label: 'اسم العميل', value: piece.customerName),
                        _InfoRow(label: 'نوع القطعة', value: pieceTypeLabel),
                        _InfoRow(
                            label: 'رقم الطلب', value: piece.orderNumber),
                        _InfoRow(
                            label: 'تاريخ الطلب', value: piece.orderDateText),
                        _InfoRow(
                            label: 'تاريخ التسليم',
                            value: piece.deliveryDateText),
                        _InfoRow(
                            label: 'رمز التتبع', value: piece.trackingCode),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  SizedBox(
                    width: 90,
                    child: _QrCodeBox(value: qrValue),
                  ),
                ],
              ),
            ),
            const Divider(height: 1, color: Colors.black),
            _BarcodeBox(value: barcodeValue),
            const Divider(height: 1, color: Colors.black),
            if (fabricRows.isNotEmpty)
              Container(
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.black, width: 1.2),
                ),
                padding: const EdgeInsets.all(8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('بيانات القماش',
                        style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: Colors.black)),
                    const SizedBox(height: 6),
                    ...fabricRows,
                  ],
                ),
              ),
            if (fabricRows.isNotEmpty)
              const Divider(height: 1, color: Colors.black),
            if (specialRequests.isNotEmpty)
              Container(
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.black, width: 1.2),
                ),
                padding: const EdgeInsets.all(8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(' 4',
                        style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: Colors.black)),
                    const SizedBox(height: 6),
                    ...specialRequests,
                  ],
                ),
              ),
            if (specialRequests.isNotEmpty)
              const Divider(height: 1, color: Colors.black),
            if (piece.measurements.isNotEmpty)
              Container(
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.black, width: 1.2),
                ),
                padding: const EdgeInsets.all(8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('القياسات',
                        style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: Colors.black)),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: piece.measurements.entries
                          .map((entry) => SizedBox(
                                width: 110,
                                child: _MeasurementCell(
                                  label: entry.key,
                                  value: entry.value,
                                ),
                              ))
                          .toList(),
                    ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context) {
    if (header.useImage) {
      final imageWidget = _headerImageWidget(header.imageValue);
      if (imageWidget != null) {
        return SizedBox(
          height: 50,
          child: imageWidget,
        );
      }
    }

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            header.name,
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: Colors.black,
            ),
          ),
          if (header.location.isNotEmpty)
            Text(
              header.location,
              style: const TextStyle(fontSize: 12, color: Colors.black87),
            ),
          if (header.phone1.isNotEmpty || header.phone2.isNotEmpty)
            Text(
              header.phone1.isNotEmpty && header.phone2.isNotEmpty
                  ? '${header.phone1} • ${header.phone2}'
                  : '${header.phone1}${header.phone2}',
              style: const TextStyle(fontSize: 9, color: Colors.black87),
            ),
        ],
      ),
    );
  }

  Widget? _headerImageWidget(String imageValue) {
    if (imageValue.isEmpty) return null;
    final safe = imageValue.trim();
    if (safe.startsWith('data:image')) {
      final data = safe.split(',').last;
      try {
        return Image.memory(
          base64Decode(data),
          fit: BoxFit.contain,
        );
      } catch (_) {
        return null;
      }
    }
    if (safe.startsWith('http')) {
      return Image.network(
        safe,
        fit: BoxFit.contain,
        errorBuilder: (_, __, ___) => const SizedBox.shrink(),
      );
    }
    return null;
  }
}

class _MeasurementCardPdfGenerator {
  static Future<Uint8List> generate(
    _OrderPieceDetail piece,
    _MeasurementHeaderSettings header,
  ) async {
    final pdf = pw.Document();
    final arabicFont = await DocumentPrintSupport.loadArabicFont();
    final headerWidget = await DocumentPrintSupport.buildHeader(
      DocumentPrintHeader(
        useImage: header.useImage,
        imageValue: header.imageValue,
        name: header.name,
        location: header.location,
        phone1: header.phone1,
        phone2: header.phone2,
      ),
      arabicFont,
    );
    final qrValue = piece.qrCode.trim().isNotEmpty ? piece.qrCode : 'LUMAR';
    final barcodeValue =
        piece.barcode.trim().isNotEmpty ? piece.barcode : piece.trackingCode;
    final typeLabel =
        piece.pieceType.trim().isNotEmpty ? piece.pieceType : 'غير محدد';

    final fabricRows = <pw.Widget>[];
    if (piece.fabricCode.isNotEmpty ||
        piece.fabricType.isNotEmpty ||
        piece.fabricColor.isNotEmpty ||
        piece.catalogNumber.isNotEmpty ||
        piece.consumptionDisplay.isNotEmpty) {
      fabricRows.add(
        pw.Row(
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Expanded(
              child: pw.Text(
                  'الكود: ${piece.fabricCode.isNotEmpty ? piece.fabricCode : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(font: arabicFont, fontSize: 11)),
            ),
            pw.SizedBox(width: 4),
            pw.Expanded(
              child: pw.Text(
                  'اللنوع : ${piece.fabricType.isNotEmpty ? piece.fabricType : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(font: arabicFont, fontSize: 11)),
            ),
          ],
        ),
      );
      fabricRows.add(pw.SizedBox(height: 4));
      fabricRows.add(
        pw.Row(
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Expanded(
              child: pw.Text(
                  'اللون : ${piece.fabricColor.isNotEmpty ? piece.fabricColor : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(font: arabicFont, fontSize: 11)),
            ),
            pw.SizedBox(width: 4),
            pw.Expanded(
              child: pw.Text(
                  ' الكتالوج: ${piece.catalogNumber.isNotEmpty ? piece.catalogNumber : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(font: arabicFont, fontSize: 11)),
            ),
          ],
        ),
      );
      fabricRows.add(pw.SizedBox(height: 4));
      fabricRows.add(
        pw.Text(
            'الاستهلاك: ${piece.consumptionDisplay.isNotEmpty ? piece.consumptionDisplay : 'غير محدد'}',
            textAlign: pw.TextAlign.right,
            textDirection: pw.TextDirection.rtl,
            style: pw.TextStyle(font: arabicFont, fontSize: 11)),
      );
    }

    final specialRows = <pw.Widget>[];
    if (piece.request1.isNotEmpty ||
        piece.request2.isNotEmpty ||
        piece.specialRequest.isNotEmpty) {
      specialRows.add(pw.Align(
        alignment: pw.Alignment.centerRight,
        child: pw.Text(
          '  1: ${piece.request1.isNotEmpty ? piece.request1 : 'لا يوجد'}',
          textAlign: pw.TextAlign.right,
          textDirection: pw.TextDirection.rtl,
          style: pw.TextStyle(font: arabicFont, fontSize: 11.2),
        ),
      ));
      specialRows.add(pw.SizedBox(height: 4));
      specialRows.add(pw.Align(
        alignment: pw.Alignment.centerRight,
        child: pw.Text(
          '  2: ${piece.request2.isNotEmpty ? piece.request2 : 'لا يوجد'}',
          textAlign: pw.TextAlign.right,
          textDirection: pw.TextDirection.rtl,
          style: pw.TextStyle(font: arabicFont, fontSize: 11.2),
        ),
      ));
      specialRows.add(pw.SizedBox(height: 4));
      specialRows.add(pw.Align(
        alignment: pw.Alignment.centerRight,
        child: pw.Text(
          '4 : ${piece.specialRequest.isNotEmpty ? piece.specialRequest : 'لا يوجد'}',
          textAlign: pw.TextAlign.right,
          textDirection: pw.TextDirection.rtl,
          style: pw.TextStyle(font: arabicFont, fontSize: 11.2),
        ),
      ));
    }

    String measurementText(dynamic value) {
      if (value == null) return '-';
      final text = value.toString().trim();
      if (text.isEmpty) return '-';
      final asNum = num.tryParse(text);
      if (asNum == null) return text;
      if (asNum is int || asNum % 1 == 0) {
        return asNum.toInt().toString();
      }
      return text;
    }

    final measurementCells = piece.measurements.entries
        .where((entry) =>
            entry.value != null && entry.value.toString().trim().isNotEmpty)
        .map((entry) => pw.SizedBox(
              width: 40,
              child: pw.Container(
                padding: const pw.EdgeInsets.all(2),
                decoration: pw.BoxDecoration(
                  border: pw.Border.all(width: 0.4),
                ),
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                  children: [
                    pw.Text(
                      entry.key,
                      textAlign: pw.TextAlign.right,
                      textDirection: pw.TextDirection.rtl,
                      style: pw.TextStyle(
                          font: arabicFont,
                          fontSize: 6.8,
                          fontWeight: pw.FontWeight.bold),
                    ),
                    pw.SizedBox(height: 2),
                    pw.Text(
                      measurementText(entry.value),
                      textAlign: pw.TextAlign.right,
                      textDirection: pw.TextDirection.rtl,
                      style: pw.TextStyle(font: arabicFont, fontSize: 16),
                    ),
                  ],
                ),
              ),
            ))
        .toList();

    final measurementRows = <pw.Widget>[];
    for (var i = 0; i < measurementCells.length; i += 5) {
      final rowCells = measurementCells.sublist(
        i,
        i + 5 < measurementCells.length ? i + 5 : measurementCells.length,
      );
      measurementRows.add(
        pw.Row(
          mainAxisAlignment: pw.MainAxisAlignment.spaceEvenly,
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: rowCells,
        ),
      );
    }

    pdf.addPage(
      pw.Page(
        pageFormat: DocumentPrintSupport.a5Portrait,
        build: (pw.Context context) {
          final rows = <pw.Widget>[];

          if (headerWidget != null) {
            rows.add(pw.Container(
              padding: const pw.EdgeInsets.only(bottom: 4),
              child: headerWidget,
            ));
          }

          rows.add(pw.Divider(thickness: 1));
          rows.add(pw.SizedBox(height: 0));

          rows.add(
            pw.Container(
              decoration: pw.BoxDecoration(
                border: pw.Border.all(width: 1.3),
              ),
              padding: const pw.EdgeInsets.all(6),
              child: pw.Row(
                crossAxisAlignment: pw.CrossAxisAlignment.start,
                children: [
                  pw.Container(
                    margin: const pw.EdgeInsets.only(top: 20),
                    width: 60,
                    child: pw.BarcodeWidget(
                      barcode: pw.Barcode.qrCode(),
                      data: qrValue,
                      width: 60,
                      height: 60,
                      drawText: false,
                    ),
                  ),
                  pw.SizedBox(width: 8),
                  pw.Expanded(
                    child: pw.Column(
                      crossAxisAlignment: pw.CrossAxisAlignment.end,
                      children: [
                        pw.Text(
                          '/  ${piece.customerName}',
                          textAlign: pw.TextAlign.right,
                          textDirection: pw.TextDirection.rtl,
                          softWrap: false,
                          maxLines: 1,
                          style: pw.TextStyle(font: arabicFont, fontSize: 12),
                        ),
                        pw.Text('النوع: $typeLabel',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('الطلب: ${piece.orderNumber}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('تاريخ الطلب: ${piece.orderDateText}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('التسليم: ${piece.deliveryDateText}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('التتبع: ${piece.trackingCode}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          );

          rows.add(
            pw.Container(
              decoration: pw.BoxDecoration(border: pw.Border.all(width: 0.8)),
              padding: const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              child: pw.BarcodeWidget(
                barcode: pw.Barcode.code128(),
                data: barcodeValue,
                width: 160,
                height: 40,
                drawText: false,
              ),
            ),
          );

          if (fabricRows.isNotEmpty) {
            rows.add(
              pw.Divider(
                height: 0.7,
                color: const PdfColor.fromInt(0xFF000000),
              ),
            );
            rows.add(
              pw.Container(
                decoration: pw.BoxDecoration(border: pw.Border.all(width: 0.8)),
                padding: const pw.EdgeInsets.all(6),
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.start,
                  children: [
                    pw.Text('بيانات القماش',
                        textAlign: pw.TextAlign.right,
                        textDirection: pw.TextDirection.rtl,
                        style: pw.TextStyle(
                            font: arabicFont,
                            fontSize: 7,
                            fontWeight: pw.FontWeight.bold)),
                    pw.SizedBox(height: 2),
                    ...fabricRows,
                  ],
                ),
              ),
            );
          }

          if (specialRows.isNotEmpty) {
            rows.add(
              pw.Divider(
                height: 0.7,
                color: const PdfColor.fromInt(0xFF000000),
              ),
            );
            rows.add(
              pw.Container(
                decoration: pw.BoxDecoration(border: pw.Border.all(width: 0.8)),
                padding: const pw.EdgeInsets.all(6),
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.start,
                  children: [
                    pw.Text('الطلبات ',
                        textAlign: pw.TextAlign.right,
                        textDirection: pw.TextDirection.rtl,
                        style: pw.TextStyle(
                            font: arabicFont,
                            fontSize: 7,
                            fontWeight: pw.FontWeight.bold)),
                    pw.SizedBox(height: 2),
                    ...specialRows,
                  ],
                ),
              ),
            );
          }

          if (piece.measurements.isNotEmpty) {
            rows.add(
              pw.Divider(
                height: 0.7,
                color: const PdfColor.fromInt(0xFF000000),
              ),
            );
            rows.add(
              pw.Container(
                decoration: pw.BoxDecoration(border: pw.Border.all(width: 0.8)),
                padding: const pw.EdgeInsets.fromLTRB(4, 0, 6, 5),
                child: pw.Column(
                  children: measurementRows,
                ),
              ),
            );
          }

          return pw.Column(
            crossAxisAlignment: pw.CrossAxisAlignment.stretch,
            children: rows,
          );
        },
      ),
    );

    return pdf.save();
  }

}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final dynamic value;

  @override
  Widget build(BuildContext context) {
    final text = value == null || value.toString().trim().isEmpty
        ? '-'
        : value.toString();
    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: RichText(
        textDirection: Directionality.of(context),
        text: TextSpan(
          style: const TextStyle(color: Colors.black87, fontSize: 9),
          children: [
            TextSpan(
              text: '$label: ',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            TextSpan(text: text),
          ],
        ),
      ),
    );
  }
}

class _MeasurementCell extends StatelessWidget {
  const _MeasurementCell({required this.label, required this.value});

  final String label;
  final dynamic value;

  @override
  Widget build(BuildContext context) {
    final textValue = value == null || value.toString().trim().isEmpty
        ? '-'
        : value.toString();
    return Container(
      padding: const EdgeInsets.all(4),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label,
            style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: Colors.black87),
          ),
          const SizedBox(height: 2),
          Text(
            textValue,
            style: const TextStyle(fontSize: 11, color: Colors.black),
          ),
        ],
      ),
    );
  }
}

class _QrCodeBox extends StatelessWidget {
  const _QrCodeBox({required this.value});

  final String value;

  @override
  Widget build(BuildContext context) {
    final safeValue = value.trim();
    final safeLength = safeValue.isEmpty ? 1 : safeValue.length;
    final items = List.generate(16, (row) {
      final pattern = List.generate(16, (col) {
        final charIndex = (row * 2 + (col % 2)) % safeLength;
        final seed = (safeValue.isEmpty
            ? (row + col) % 2
            : (safeValue.codeUnits[charIndex] + row * 13 + col * 7) % 2);
        return seed == 0 ? Colors.black : Colors.white;
      });
      return Row(
        children: pattern
            .map((color) => Expanded(child: Container(height: 5, color: color)))
            .toList(),
      );
    });

    return Container(
      padding: const EdgeInsets.all(6),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: Colors.black, width: .2),
      ),
      child: Column(
        children: [
          ...items,
        ],
      ),
    );
  }
}

class _BarcodeBox extends StatelessWidget {
  const _BarcodeBox({required this.value});

  final String value;

  @override
  Widget build(BuildContext context) {
    final safeValue = value.trim();
    final displayValue = safeValue.isEmpty ? '---' : safeValue;
    final bars = List.generate(
        (safeValue.isEmpty ? 8 : safeValue.length * 2) + 4, (index) {
      final isDark = (index + displayValue.length) % 3 == 0 || index % 2 == 0;
      return Expanded(
        child: Container(
          height: 38,
          color: isDark ? Colors.black : Colors.white,
        ),
      );
    });

    return Container(
      padding: const EdgeInsets.all(6),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: Colors.black, width: 1.2),
      ),
      child: Column(
        children: [
          Row(children: bars),
          const SizedBox(height: 4),
          Text(
            displayValue,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 7, color: Colors.black87),
          ),
        ],
      ),
    );
  }
}

class _PlaceholderTab extends StatelessWidget {
  const _PlaceholderTab({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        padding: const EdgeInsets.all(28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.construction_outlined, size: 42),
            const SizedBox(height: 12),
            Text(
              title,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 20,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              subtitle,
              textAlign: TextAlign.center,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 14,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PrintingCenterState {
  const _PrintingCenterState({required this.orders, required this.header});

  final List<_OrderSummary> orders;
  final _MeasurementHeaderSettings header;
}

class _MeasurementHeaderSettings {
  const _MeasurementHeaderSettings({
    required this.useImage,
    required this.imageValue,
    required this.name,
    required this.location,
    required this.phone1,
    required this.phone2,
  });

  final bool useImage;
  final String imageValue;
  final String name;
  final String location;
  final String phone1;
  final String phone2;
}

class _CustomerSnapshot {
  const _CustomerSnapshot({required this.name, required this.phone});

  final String name;
  final String phone;
}

class _OrderSummary {
  const _OrderSummary({
    required this.orderId,
    required this.orderNumber,
    required this.customerName,
    required this.customerPhone,
    required this.orderDate,
    required this.orderStatus,
    this.isReadyMade = false,
  });

  factory _OrderSummary.fromJson(
    Map<String, dynamic> json,
    Map<int, _CustomerSnapshot> customerMap,
  ) {
    final id = (json['orderId'] as int?) ?? 0;
    final customerId = (json['customerId'] as int?) ?? 0;
    final customer = customerMap[customerId] ??
        const _CustomerSnapshot(name: 'غير محدد', phone: '');

    final orderDateValue =
        (json['orderDate'] ?? json['createdAt'] ?? '').toString();
    final orderStatus =
        (json['orderStatus'] ?? json['status'] ?? 'New').toString();
    return _OrderSummary(
      orderId: id,
      orderNumber: (json['orderNumber'] ?? '').toString(),
      customerName: customer.name,
      customerPhone: customer.phone,
      orderDate: DateTime.tryParse(orderDateValue) ?? DateTime.now(),
      orderStatus: orderStatus,
    );
  }

  factory _OrderSummary.fromReadyMadeJson(Map<String, dynamic> json) =>
      _OrderSummary(
        orderId: (json['readyMadeProductionOrderId'] as num?)?.toInt() ?? 0,
        orderNumber: (json['productionOrderNumber'] ?? '').toString(),
        customerName: 'إنتاج جاهز',
        customerPhone: '',
        orderDate: DateTime.tryParse((json['createdAt'] ?? '').toString()) ?? DateTime.now(),
        orderStatus: (json['status'] ?? 'New').toString(),
        isReadyMade: true,
      );

  final int orderId;
  final String orderNumber;
  final String customerName;
  final String customerPhone;
  final DateTime orderDate;
  final String orderStatus;
  final bool isReadyMade;

  bool get isCancelled => orderStatus.trim().toLowerCase() == 'cancelled';
}

class _OrderPieceDetail {
  const _OrderPieceDetail({
    required this.pieceId,
    required this.orderNumber,
    required this.pieceNumber,
    required this.customerName,
    required this.pieceType,
    required this.trackingCode,
    required this.qrCode,
    required this.barcode,
    required this.notes,
    required this.orderDate,
    required this.deliveryDate,
    required this.fabricCode,
    required this.fabricType,
    required this.fabricColor,
    required this.catalogNumber,
    required this.consumption,
    required this.consumptionUnit,
    required this.request1,
    required this.request2,
    required this.specialRequest,
    required this.measurements,
    required this.pieceStatus,
  });

  factory _OrderPieceDetail.fromJson(Map<String, dynamic> json) {
    final snapshotValue = json['measurementSnapshot'];
    final measurementMap = parseMeasurementSnapshot(snapshotValue);

    final rawTracking = (json['trackingCode'] ?? '').toString();
    final statusValue =
        (json['pieceStatus'] ?? json['status'] ?? '').toString();
    final fabricCode = _firstAvailableString([
      json['fabricCode'],
      measurementMap['fabricCode'],
      measurementMap['FabricCode'],
      json['fabric_code'],
    ]);
    final fabricType = _firstAvailableString([
      json['fabricType'],
      measurementMap['fabricType'],
      measurementMap['FabricType'],
      json['fabric_type'],
    ]);
    final fabricColor = _firstAvailableString([
      json['fabricColor'],
      measurementMap['fabricColor'],
      measurementMap['FabricColor'],
      json['fabric_color'],
    ]);
    final catalogNumber = _firstAvailableString([
      json['catalogNumber'],
      measurementMap['catalogNumber'],
      measurementMap['CatalogNumber'],
      measurementMap['barcode'],
      measurementMap['Barcode'],
      json['barcode'],
    ]);
    final consumptionRaw = _firstAvailableString([
      json['consumption'],
      json['consumptionValue'],
      measurementMap['_consumption'],
      measurementMap['consumption'],
      measurementMap['Consumption'],
      measurementMap['quantityInch'],
      measurementMap['QuantityInch'],
      measurementMap['availableInches'],
      measurementMap['AvailableInches'],
    ]);
    final consumption = double.tryParse(consumptionRaw) ?? 0;
    final consumptionUnit = _firstAvailableString([
      json['consumptionUnit'],
      measurementMap['_consumptionUnit'],
      measurementMap['consumptionUnit'],
      measurementMap['ConsumptionUnit'],
      json['unit'],
      measurementMap['unit'],
      'بوصة',
    ]);
    final request1 = _firstAvailableString([
      json['request1'],
      json['notes1'],
      measurementMap['request1'],
      measurementMap['Request1'],
    ]);
    final request2 = _firstAvailableString([
      json['request2'],
      json['notes2'],
      measurementMap['request2'],
      measurementMap['Request2'],
    ]);
    final specialRequest = _firstAvailableString([
      json['specialRequest'],
      json['specialRequests'],
      measurementMap['specialRequest'],
      measurementMap['SpecialRequest'],
    ]);
    final sanitizedMeasurements = extractRealPieceMeasurements(measurementMap);

    return _OrderPieceDetail(
      pieceId: (json['pieceId'] as int?) ?? 0,
      orderNumber: (json['orderNumber'] ?? '').toString(),
      pieceNumber: (json['pieceNumber'] as int?) ?? 0,
      customerName: (json['customerName'] ?? '').toString().isEmpty
          ? 'غير محدد'
          : (json['customerName'] ?? '').toString(),
      pieceType: (json['pieceType'] ?? '').toString(),
      trackingCode: rawTracking,
      qrCode: rawTracking.isNotEmpty ? rawTracking : 'LUMAR',
      barcode: (json['barcode'] ?? rawTracking).toString().isNotEmpty
          ? (json['barcode'] ?? rawTracking).toString()
          : rawTracking,
      notes: [request1, request2, specialRequest]
          .where((value) => value.trim().isNotEmpty)
          .join(' • '),
      orderDate: DateTime.tryParse((json['orderDate'] ?? '').toString()),
      deliveryDate: DateTime.tryParse((json['deliveryDate'] ?? '').toString()),
      fabricCode: fabricCode,
      fabricType: fabricType,
      fabricColor: fabricColor,
      catalogNumber: catalogNumber,
      consumption: consumption,
      consumptionUnit: consumptionUnit,
      request1: request1,
      request2: request2,
      specialRequest: specialRequest,
      measurements: sanitizedMeasurements,
      pieceStatus: statusValue,
    );
  }

  static Map<String, dynamic> _sanitizeMeasurements(
          Map<String, dynamic> source) =>
      extractRealPieceMeasurements(source);

  static String _firstAvailableString(Iterable<dynamic> values) {
    for (final value in values) {
      final text = value?.toString().trim();
      if (text != null && text.isNotEmpty) {
        return text;
      }
    }
    return '';
  }

  final int pieceId;
  final String orderNumber;
  final int pieceNumber;
  final String customerName;
  final String pieceType;
  final String trackingCode;
  final String qrCode;
  final String barcode;
  final String notes;
  final DateTime? orderDate;
  final DateTime? deliveryDate;
  final String fabricCode;
  final String fabricType;
  final String fabricColor;
  final String catalogNumber;
  final double consumption;
  final String consumptionUnit;
  final String request1;
  final String request2;
  final String specialRequest;
  final Map<String, dynamic> measurements;
  final String pieceStatus;

  String get consumptionDisplay {
    if (consumption == 0 && consumptionUnit.trim().isEmpty) return '';
    final unitText =
        consumptionUnit.trim().isEmpty ? 'بوصة' : consumptionUnit.trim();
    return '${consumption.toStringAsFixed(2)} $unitText';
  }

  bool get isPrinted {
    final normalized = pieceStatus.trim().toLowerCase();
    return normalized == 'printed' ||
        normalized == 'reprinted' ||
        normalized == 'printed_again' ||
        normalized == 'completed' ||
        normalized == 'done' ||
        normalized.contains('printed') ||
        normalized.contains('completed');
  }

  String get orderDateText =>
      orderDate == null ? '-' : DateFormat('yyyy/MM/dd').format(orderDate!);
  String get deliveryDateText => deliveryDate == null
      ? '-'
      : DateFormat('yyyy/MM/dd').format(deliveryDate!);
}
