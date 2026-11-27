import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';
import 'package:uuid/uuid.dart';

import '../core/measurement_snapshot.dart';
import '../core/tailoring_order_documents_printing.dart';
import '../core/document_printing.dart';
import '../core/ui_palette.dart';
import '../repositories/finance_repository.dart';
import '../services/auth_state.dart';

class PrintingScreen extends StatelessWidget {
  const PrintingScreen(
      {required this.auth, this.initialTabIndex = 0, super.key});

  final AuthState auth;
  final int initialTabIndex;

  @override
  Widget build(BuildContext context) =>
      PrintingCenterScreen(auth: auth, initialTabIndex: initialTabIndex);
}

class PrintingCenterScreen extends StatefulWidget {
  const PrintingCenterScreen(
      {required this.auth, this.initialTabIndex = 0, super.key});

  final AuthState auth;
  final int initialTabIndex;

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
  int _activeTabIndex = 0;
  Future<List<_FabricRollLabel>>? _fabricLabelsCache;
  final TextEditingController _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _activeTabIndex = widget.initialTabIndex.clamp(0, 4);
    _tabController =
        TabController(length: 5, initialIndex: _activeTabIndex, vsync: this);
    _tabController.addListener(() {
      if (_tabController.indexIsChanging) return;
      if (_activeTabIndex == _tabController.index) return;
      setState(() => _activeTabIndex = _tabController.index);
    });
  }

  @override
  void dispose() {
    _tabController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final isLight = Theme.of(context).brightness == Brightness.light;
    return Scaffold(
      backgroundColor:
          isLight ? const Color(0xFFF4F7FA) : UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor:
            isLight ? const Color(0xFFFFFFFF) : UiPalette.surfaceCard,
        foregroundColor: isLight
            ? const Color(0xFF172033)
            : UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        title: Text(
          'مركز الطباعة',
          style: TextStyle(
            color: isLight ? const Color(0xFF172033) : UiPalette.textMain,
            fontWeight: FontWeight.w700,
          ),
        ),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_outlined),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TabBar(
              controller: _tabController,
              isScrollable: false,
              tabAlignment: TabAlignment.fill,
              dividerColor: isLight ? const Color(0xFFCBD5E1) : null,
              labelColor: isLight ? const Color(0xFF168F86) : null,
              unselectedLabelColor: isLight ? const Color(0xFF5B687A) : null,
              indicatorColor: isLight ? const Color(0xFF168F86) : null,
              tabs: const [
                Tab(icon: Icon(Icons.credit_card), text: 'بطاقات المقاسات'),
                Tab(icon: Icon(Icons.receipt_long_outlined), text: 'الإيصالات'),
                Tab(
                    icon: Icon(Icons.description_outlined),
                    text: 'سندات القبض'),
                Tab(icon: Icon(Icons.fact_check_outlined), text: 'الفواتير'),
                Tab(icon: Icon(Icons.label_outline), text: 'ملصقات الأقمشة'),
              ],
            ),
            const SizedBox(height: 7),
            Expanded(
              child: TabBarView(
                controller: _tabController,
                children: [
                  _MeasurementCardsTab(
                    active: _activeTabIndex == 0,
                    orders: const <_OrderSummary>[],
                    header: const _MeasurementHeaderSettings(
                        useImage: false,
                        imageValue: '',
                        name: 'LUMAR',
                        location: '',
                        phone1: '',
                        phone2: ''),
                    baseUrl: _baseUrl,
                    auth: widget.auth,
                    onRefresh: _refresh,
                  ),
                  _TailoringOrdersPrintingTab(
                      baseUrl: _baseUrl,
                      auth: widget.auth,
                      finalInvoice: false),
                  const _PlaceholderTab(
                      title: 'طباعة سندات القبض',
                      subtitle: 'سيتم تنفيذها في المرحلة القادمة.'),
                  _TailoringOrdersPrintingTab(
                      baseUrl: _baseUrl, auth: widget.auth, finalInvoice: true),
                  _FabricRollLabelsTab(
                      baseUrl: _baseUrl,
                      auth: widget.auth,
                      cachedLabels: _fabricLabelsCache),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MeasurementCardsTab extends StatefulWidget {
  const _MeasurementCardsTab({
    required this.active,
    required this.orders,
    required this.header,
    required this.baseUrl,
    required this.auth,
    required this.onRefresh,
  });

  final bool active;
  final List<_OrderSummary> orders;
  final _MeasurementHeaderSettings header;
  final String baseUrl;
  final AuthState auth;
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
  bool _loadingPrintAction = false;
  int _allUnprintedCards = 0;
  int _allPrintedCards = 0;
  List<_MeasurementStatusType> _unprintedByType = const [];
  final FlutterSecureStorage _secureStorage = const FlutterSecureStorage();
  List<_BulkPrintPage> _pendingBulkPages = const [];
  String? _pendingBulkBatchId;
  final Map<int, List<_PrintHistoryRecord>> _printHistoryCache = {};
  List<_OrderSummary> _orders = const [];
  _MeasurementHeaderSettings _header = const _MeasurementHeaderSettings(
      useImage: false,
      imageValue: '',
      name: 'LUMAR',
      location: '',
      phone1: '',
      phone2: '');

  bool _startedLazyLoad = false;

  void _startLazyLoad() {
    if (!widget.active || _startedLazyLoad) return;
    _startedLazyLoad = true;
    _loadMeasurementData();
  }

  Future<void> _loadMeasurementData() async {
    if (!mounted) return;
    try {
      final customersResponse = await http
          .get(Uri.parse('${widget.baseUrl}/customers'), headers: _authHeaders);
      final customers = <int, _CustomerSnapshot>{};
      if (customersResponse.statusCode >= 200 &&
          customersResponse.statusCode < 300) {
        final body = jsonDecode(customersResponse.body);
        if (body is List) {
          for (final item in body.whereType<Map<String, dynamic>>()) {
            final id = (item['customerId'] as num?)?.toInt() ?? 0;
            if (id > 0)
              customers[id] = _CustomerSnapshot(
                  name: (item['customerName'] ?? '').toString(),
                  phone: (item['phoneNumber'] ?? '').toString());
          }
        }
      }
      final responses = await Future.wait([
        http.get(Uri.parse('${widget.baseUrl}/orders'), headers: _authHeaders),
        http.get(Uri.parse('${widget.baseUrl}/production/readymade-orders'),
            headers: _authHeaders),
      ]);
      final ordersResponse = responses[0];
      final readyOrdersResponse = responses[1];
      final orderList = <_OrderSummary>[];
      if (ordersResponse.statusCode >= 200 && ordersResponse.statusCode < 300) {
        orderList.addAll((jsonDecode(ordersResponse.body) as List)
            .whereType<Map<String, dynamic>>()
            .map((entry) => _OrderSummary.fromJson(entry, customers)));
      }
      if (readyOrdersResponse.statusCode >= 200 &&
          readyOrdersResponse.statusCode < 300) {
        orderList.addAll((jsonDecode(readyOrdersResponse.body) as List)
            .whereType<Map<String, dynamic>>()
            .map(_OrderSummary.fromReadyMadeJson));
      }
      orderList.sort((a, b) => b.orderDate.compareTo(a.orderDate));

      final headerKeys = <String>[
        'MeasurementCardHeaderImage',
        'MeasurementCardHeaderUseImage',
        'MeasurementCardHeaderName',
        'MeasurementCardLocation',
        'MeasurementCardPhone1',
        'MeasurementCardPhone2'
      ];
      final headerResponses = await Future.wait(headerKeys.map((key) => http.get(
          Uri.parse(
              '${widget.baseUrl}/settings/by-key/${Uri.encodeComponent(key)}'),
          headers: _authHeaders)));
      final entries = <String, String>{};
      for (var i = 0; i < headerKeys.length; i++) {
        final response = headerResponses[i];
        if (response.statusCode >= 200 && response.statusCode < 300) {
          final decoded = jsonDecode(response.body);
          if (decoded is Map<String, dynamic>)
            entries[headerKeys[i]] = (decoded['settingValue'] ?? '').toString();
        }
      }
      final useImage =
          entries['MeasurementCardHeaderUseImage']?.trim().toLowerCase() ==
              'true';
      final header = _MeasurementHeaderSettings(
          useImage: useImage,
          imageValue: entries['MeasurementCardHeaderImage'] ?? '',
          name: entries['MeasurementCardHeaderName']?.trim().isNotEmpty == true
              ? entries['MeasurementCardHeaderName']!
              : 'LUMAR',
          location: entries['MeasurementCardLocation'] ?? '',
          phone1: entries['MeasurementCardPhone1'] ?? '',
          phone2: entries['MeasurementCardPhone2'] ?? '');
      if (!mounted) return;
      setState(() {
        _orders = orderList;
        _header = header;
      });
      await _refreshUnprintedSummary();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _orders = const [];
        _allUnprintedCards = 0;
      });
    }
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _startLazyLoad();
      _restorePendingBulk();
    });
  }

  Future<void> _restorePendingBulk() async {
    try {
      final raw = await _secureStorage.read(key: 'lumar_pending_measurement_card_bulk');
      if (raw == null || raw.trim().isEmpty || !mounted) return;
      final decoded = jsonDecode(raw);
      if (decoded is! Map<String, dynamic>) return;
      final pages = (decoded['pages'] as List? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(_BulkPrintPage.fromJson)
          .toList();
      if (pages.isEmpty) return;
      setState(() {
        _pendingBulkPages = pages;
        _pendingBulkBatchId = (decoded['batchId'] ?? '').toString();
      });
    } catch (_) {}
  }

  Future<void> _persistPendingBulk(String batchId, List<_BulkPrintPage> pages) async {
    _pendingBulkPages = pages;
    _pendingBulkBatchId = batchId;
    await _secureStorage.write(
      key: 'lumar_pending_measurement_card_bulk',
      value: jsonEncode({
        'batchId': batchId,
        'pages': pages.map((page) => page.toJson()).toList(),
      }),
    );
  }

  Future<void> _clearPendingBulk() async {
    _pendingBulkPages = const [];
    _pendingBulkBatchId = null;
    await _secureStorage.delete(key: 'lumar_pending_measurement_card_bulk');
  }

  @override
  void didUpdateWidget(covariant _MeasurementCardsTab oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !oldWidget.active) _startLazyLoad();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _refreshUnprintedSummary() async {
    if (_loadingUnprintedSummary) return;
    setState(() => _loadingUnprintedSummary = true);

    try {
      final query = _selectedOrder?.orderId;
      final suffix = query == null ? '' : '?orderId=$query';
      final response = await http.get(
        Uri.parse('${widget.baseUrl}/printing/measurement-card-status-summary$suffix'),
        headers: _authHeaders,
      );
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(_readApiError(response));
      }
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      final types = (body['unprintedByProductType'] as List? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(_MeasurementStatusType.fromJson)
          .toList();
      if (!mounted) return;
      setState(() {
        _allPrintedCards = (body['printedTotal'] as num?)?.toInt() ?? 0;
        _allUnprintedCards = (body['unprintedTotal'] as num?)?.toInt() ?? 0;
        _unprintedByType = types;
      });
    } catch (_) {
      if (mounted) {
        setState(() {
          _allPrintedCards = 0;
          _allUnprintedCards = 0;
          _unprintedByType = const [];
        });
      }
    } finally {
      if (mounted) setState(() => _loadingUnprintedSummary = false);
    }
  }

  Future<List<_OrderPieceDetail>> _fetchOrderPiecesOnce(
      _OrderSummary order) async {
    final response = await http.get(
      Uri.parse(order.isReadyMade
          ? '${widget.baseUrl}/production/readymade-orders/${order.orderId}/items'
          : '${widget.baseUrl}/orders/${order.orderId}/pieces'),
      headers: _authHeaders,
    );
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
          final pieceResponse = await http.get(
            Uri.parse(
                '${widget.baseUrl}/production/readymade-order-items/$itemId/pieces'),
            headers: _authHeaders,
          );
          if (pieceResponse.statusCode < 200 || pieceResponse.statusCode >= 300)
            continue;
          for (final pieceJson in (jsonDecode(pieceResponse.body) as List)
              .cast<Map<String, dynamic>>()) {
            final pieceId =
                (pieceJson['readyMadeProductionOrderPieceInstanceId']
                        as int?) ??
                    0;
            if (pieceId > 0) {
              try {
                pieces.add(await _loadPieceCard(pieceId, readyMade: true));
              } catch (_) {}
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
      final response = await http.get(
        Uri.parse(order.isReadyMade
            ? '${widget.baseUrl}/production/readymade-orders/${order.orderId}/items'
            : '${widget.baseUrl}/orders/${order.orderId}/pieces'),
        headers: _authHeaders,
      );
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر تحميل قطع الطلب.');
      }

      final piecesJson = jsonDecode(response.body) as List;
      final pieces = <_OrderPieceDetail>[];
      for (final entry in piecesJson) {
        if (entry is Map<String, dynamic>) {
          if (order.isReadyMade) {
            final itemId =
                (entry['readyMadeProductionOrderItemId'] as int?) ?? 0;
            if (itemId <= 0) continue;
            final pieceResponse = await http.get(
              Uri.parse(
                  '${widget.baseUrl}/production/readymade-order-items/$itemId/pieces'),
              headers: _authHeaders,
            );
            if (pieceResponse.statusCode < 200 ||
                pieceResponse.statusCode >= 300) continue;
            for (final pieceJson in (jsonDecode(pieceResponse.body) as List)
                .cast<Map<String, dynamic>>()) {
              final pieceId =
                  (pieceJson['readyMadeProductionOrderPieceInstanceId']
                          as int?) ??
                      0;
              if (pieceId > 0)
                pieces.add(await _loadPieceCard(pieceId, readyMade: true));
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

  Future<_OrderPieceDetail> _loadPieceCard(int pieceId,
      {bool readyMade = false}) async {
    final response = await http.get(
      Uri.parse(readyMade
          ? '${widget.baseUrl}/production/readymade-pieces/$pieceId/work-card'
          : '${widget.baseUrl}/production/pieces/$pieceId/work-card'),
      headers: _authHeaders,
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تحميل تفاصيل القطعة.');
    }

    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return _OrderPieceDetail.fromJson(json, readyMade: readyMade);
  }

  Map<String, String> get _authHeaders {
    final token = widget.auth.token;
    return {
      'Content-Type': 'application/json',
      if (token != null && token.trim().isNotEmpty)
        'Authorization': 'Bearer ${token.trim()}',
    };
  }

  bool _isPiecePrinted(_OrderPieceDetail piece) {
    final history = _printHistoryCache[piece.pieceId];
    return piece.isPrinted ||
        (history?.any((entry) => entry.printStatus == 'Completed') ?? false);
  }

  Future<List<_PrintHistoryRecord>> _fetchPrintHistory(
      _OrderPieceDetail piece) async {
    final path = piece.isReadyMade
        ? '${widget.baseUrl}/printing/readymade-pieces/${piece.pieceId}/history'
        : '${widget.baseUrl}/printing/pieces/${piece.pieceId}/history';
    final response = await http.get(Uri.parse(path), headers: _authHeaders);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(_readApiError(response));
    }
    final body = jsonDecode(response.body);
    final history = body is List
        ? body
            .whereType<Map<String, dynamic>>()
            .map(_PrintHistoryRecord.fromJson)
            .toList()
        : <_PrintHistoryRecord>[];
    _printHistoryCache[piece.pieceId] = history;
    return history;
  }

  Future<List<_EmployeeOption>> _fetchEmployees() async {
    final response = await http.get(
      Uri.parse('${widget.baseUrl}/employees'),
      headers: _authHeaders,
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(_readApiError(response));
    }
    final body = jsonDecode(response.body);
    if (body is! List) return const [];
    return body
        .whereType<Map<String, dynamic>>()
        .map(_EmployeeOption.fromJson)
        .where((employee) => employee.isActive)
        .toList();
  }

  Future<List<_CustomerOption>> _fetchCustomers() async {
    final response = await http.get(
      Uri.parse('${widget.baseUrl}/customers'),
      headers: _authHeaders,
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(_readApiError(response));
    }
    final body = jsonDecode(response.body);
    if (body is! List) return const [];
    return body
        .whereType<Map<String, dynamic>>()
        .map(_CustomerOption.fromJson)
        .toList();
  }

  Future<_PrintHistoryRecord> _preparePrint(
    _OrderPieceDetail piece,
    _ReprintFormData? form,
  ) async {
    final path = piece.isReadyMade
        ? '${widget.baseUrl}/printing/readymade-pieces/${piece.pieceId}/prepare'
        : '${widget.baseUrl}/printing/pieces/${piece.pieceId}/prepare';
    final response = await http.post(
      Uri.parse(path),
      headers: _authHeaders,
      body: jsonEncode({
        'reprintReasonCode': form?.reasonCode,
        'damageReason': form?.damageReason,
        'responsibleEmployeeId': form?.responsibleEmployeeId,
        'notes': form?.notes,
        'saleAmount': form?.saleAmount,
        'salePaymentType': form?.paymentType,
        'saleCustomerId': form?.saleCustomerId,
      }),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(_readApiError(response));
    }
    final record = _PrintHistoryRecord.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
    final history = [...?_printHistoryCache[piece.pieceId]];
    history
        .removeWhere((entry) => entry.printHistoryId == record.printHistoryId);
    history.add(record);
    _printHistoryCache[piece.pieceId] = history;
    return record;
  }

  Future<_PrintHistoryRecord> _completePrint(
      _OrderPieceDetail piece, int printHistoryId, int? cashAccountId) async {
    final response = await http.post(
      Uri.parse('${widget.baseUrl}/printing/history/$printHistoryId/complete'),
      headers: _authHeaders,
      body: jsonEncode({'cashAccountId': cashAccountId}),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(_readApiError(response));
    }
    final record = _PrintHistoryRecord.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>);
    final history = [...?_printHistoryCache[piece.pieceId]];
    history
        .removeWhere((entry) => entry.printHistoryId == record.printHistoryId);
    history.add(record);
    _printHistoryCache[piece.pieceId] = history;
    return record;
  }

  Future<void> _failPrint(int printHistoryId, String reason) async {
    await http.post(
      Uri.parse('${widget.baseUrl}/printing/history/$printHistoryId/fail'),
      headers: _authHeaders,
      body: jsonEncode({'failureReason': reason}),
    );
  }

  String _readApiError(http.Response response) {
    try {
      final body = jsonDecode(response.body);
      if (body is String && body.trim().isNotEmpty) return body;
      if (body is Map<String, dynamic>) {
        final message = body['message'] ?? body['title'] ?? body['detail'];
        if (message is String && message.trim().isNotEmpty) return message;
      }
    } catch (_) {
      // Use the Arabic fallback for non-JSON API failures.
    }
    return 'تعذر تنفيذ عملية سجل الطباعة.';
  }

  Future<bool> _confirmReprint(
      _OrderPieceDetail piece, List<_PrintHistoryRecord> history) async {
    final completed = history
        .where((entry) => entry.printStatus == 'Completed')
        .toList()
      ..sort((a, b) => (b.printedAtUtc ?? b.createdAt)
          .compareTo(a.printedAtUtc ?? a.createdAt));
    final copyCount = completed.isNotEmpty
        ? completed.length
        : piece.isPrinted
            ? 1
            : 0;
    final latest = completed.isEmpty
        ? 'لا يوجد تاريخ رسمي سابق'
        : DateFormat('yyyy/MM/dd HH:mm')
            .format(completed.first.printedAtUtc ?? completed.first.createdAt);

    return await showDialog<bool>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('إعادة الطباعة'),
            content: Text(
              'هذه البطاقة مطبوعة سابقاً. هل تريد إعادة طباعتها؟\n\n'
              'عدد النسخ المسجلة: $copyCount\nآخر طباعة: $latest',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('إلغاء'),
              ),
              FilledButton(
                onPressed: () => Navigator.of(dialogContext).pop(true),
                child: const Text('متابعة'),
              ),
            ],
          ),
        ) ??
        false;
  }

  Future<_ReprintFormData?> _showReprintForm(_OrderPieceDetail piece) async {
    final employees = await _fetchEmployees();
    final customers =
        piece.isReadyMade ? await _fetchCustomers() : const <_CustomerOption>[];
    if (!mounted) return null;
    final damageController = TextEditingController();
    final saleAmountController = TextEditingController();
    final notesController = TextEditingController();
    String? reasonCode;
    int? responsibleEmployeeId;
    int? saleCustomerId;
    int? cashAccountId;
    var paymentType = 'Cash';
    String? errorText;

    final result = await showDialog<_ReprintFormData>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) {
          final isDamagedPiece = reasonCode == 'DamagedPiece';
          final isPieceSold = reasonCode == 'PieceSold';
          return AlertDialog(
            title: const Text('بيانات إعادة الطباعة'),
            content: SizedBox(
              width: 460,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text('اختر سبب إعادة الطباعة قبل المتابعة.'),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: reasonCode,
                      decoration: const InputDecoration(
                        labelText: 'سبب إعادة الطباعة',
                        border: OutlineInputBorder(),
                      ),
                      items: const [
                        DropdownMenuItem(
                            value: 'DamagedCard', child: Text('البطاقة تالفة')),
                        DropdownMenuItem(
                            value: 'LostCard', child: Text('البطاقة مفقودة')),
                        DropdownMenuItem(
                            value: 'DamagedPiece', child: Text('القطعة تالفة')),
                        DropdownMenuItem(
                            value: 'PieceSold', child: Text('تم بيع القطعة')),
                      ],
                      onChanged: (value) => setDialogState(() {
                        reasonCode = value;
                        errorText = null;
                      }),
                    ),
                    if (isDamagedPiece) ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: damageController,
                        minLines: 2,
                        maxLines: 4,
                        decoration: const InputDecoration(
                          labelText: 'سبب تلف القطعة',
                          border: OutlineInputBorder(),
                        ),
                      ),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<int>(
                        initialValue: responsibleEmployeeId,
                        decoration: const InputDecoration(
                          labelText: 'الموظف المسؤول',
                          border: OutlineInputBorder(),
                        ),
                        items: employees
                            .map((employee) => DropdownMenuItem<int>(
                                  value: employee.id,
                                  child: Text(
                                      '${employee.name} • ${employee.code}'),
                                ))
                            .toList(),
                        onChanged: (value) => setDialogState(() {
                          responsibleEmployeeId = value;
                          errorText = null;
                        }),
                      ),
                    ],
                    if (isPieceSold) ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: saleAmountController,
                        keyboardType: const TextInputType.numberWithOptions(
                            decimal: true),
                        decoration: const InputDecoration(
                          labelText: 'قيمة البيع الفعلية',
                          border: OutlineInputBorder(),
                        ),
                      ),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<String>(
                        initialValue: paymentType,
                        decoration: const InputDecoration(
                          labelText: 'طريقة الدفع',
                          border: OutlineInputBorder(),
                        ),
                        items: const [
                          DropdownMenuItem(value: 'Cash', child: Text('نقداً')),
                          DropdownMenuItem(
                              value: 'Credit', child: Text('آجلاً')),
                          DropdownMenuItem(
                              value: 'Donation', child: Text('تبرعاً')),
                        ],
                        onChanged: (value) => setDialogState(() {
                          paymentType = value ?? 'Cash';
                          errorText = null;
                        }),
                      ),
                      if (paymentType == 'Cash') ...[
                        const SizedBox(height: 12),
                        Text(
                          'سيتم استخدام الصندوق النقدي العام من إعدادات النظام تلقائيًا.',
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                      if (piece.isReadyMade) ...[
                        const SizedBox(height: 12),
                        DropdownButtonFormField<int>(
                          initialValue: saleCustomerId,
                          decoration: const InputDecoration(
                            labelText: 'عميل البيع',
                            border: OutlineInputBorder(),
                          ),
                          items: customers
                              .map((customer) => DropdownMenuItem<int>(
                                    value: customer.id,
                                    child: Text(customer.name),
                                  ))
                              .toList(),
                          onChanged: (value) => setDialogState(() {
                            saleCustomerId = value;
                            errorText = null;
                          }),
                        ),
                      ],
                    ],
                    const SizedBox(height: 12),
                    TextField(
                      controller: notesController,
                      minLines: 1,
                      maxLines: 3,
                      decoration: const InputDecoration(
                        labelText: 'ملاحظات اختيارية',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    if (errorText != null) ...[
                      const SizedBox(height: 8),
                      Text(errorText!,
                          style: const TextStyle(color: Colors.redAccent)),
                    ],
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('إلغاء'),
              ),
              FilledButton(
                onPressed: () async {
                  final saleAmount =
                      double.tryParse(saleAmountController.text.trim());
                  if (reasonCode == null) {
                    setDialogState(
                        () => errorText = 'اختيار سبب إعادة الطباعة مطلوب.');
                    return;
                  }
                  if (isDamagedPiece &&
                      (damageController.text.trim().isEmpty ||
                          responsibleEmployeeId == null)) {
                    setDialogState(() => errorText =
                        'سبب التلف والموظف المسؤول مطلوبان قبل المتابعة.');
                    return;
                  }
                  if (isPieceSold) {
                    if (paymentType == 'Cash') {
                      final generalCashAccount =
                          await FinanceRepository().resolveGeneralCashAccount(
                        requireReceiptEnabled: true,
                      );
                      if (!dialogContext.mounted) return;
                      if (generalCashAccount == null) {
                        setDialogState(() => errorText =
                            'الصندوق النقدي العام غير محدد أو غير متاح. يرجى تحديده من الإعدادات المالية.');
                        return;
                      }
                      cashAccountId = generalCashAccount.id;
                    }
                    if (saleAmount == null ||
                        saleAmount <= 0 ||
                        (piece.isReadyMade && saleCustomerId == null)) {
                      setDialogState(() => errorText =
                          'قيمة البيع وعميل البيع المطلوبان قبل المتابعة.');
                      return;
                    }
                  }
                  Navigator.of(dialogContext).pop(_ReprintFormData(
                    reasonCode: reasonCode!,
                    damageReason:
                        isDamagedPiece ? damageController.text.trim() : null,
                    responsibleEmployeeId:
                        isDamagedPiece ? responsibleEmployeeId : null,
                    notes: notesController.text.trim().isEmpty
                        ? null
                        : notesController.text.trim(),
                    saleAmount: isPieceSold ? saleAmount : null,
                    paymentType: isPieceSold ? paymentType : null,
                    cashAccountId: isPieceSold ? cashAccountId : null,
                    saleCustomerId: isPieceSold ? saleCustomerId : null,
                  ));
                },
                child: const Text('موافق وطباعة'),
              ),
            ],
          );
        },
      ),
    );

    damageController.dispose();
    saleAmountController.dispose();
    notesController.dispose();
    return result;
  }

  Future<void> _executePrint(
    _OrderPieceDetail piece,
    _ReprintFormData? form,
    _PrintTarget target,
  ) async {
    if (_loadingPrintAction || !mounted) return;
    setState(() => _loadingPrintAction = true);
    _PrintHistoryRecord? prepared;
    try {
      prepared = await _preparePrint(piece, form);
      if (!mounted) return;
      final copyNumber = prepared.copyNumber;
      await _PrintService.execute(
        context: context,
        piece: piece,
        header: _header,
        target: target,
        copyNumber: copyNumber,
      );
      await _completePrint(piece, prepared.printHistoryId, form?.cashAccountId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text(form == null
                  ? 'تم اعتماد الطباعة الأولى.'
                  : 'تم اعتماد إعادة الطباعة بالنسخة $copyNumber.')),
        );
        if (form == null && _allUnprintedCards > 0) {
          setState(() => _allUnprintedCards -= 1);
        }
      }
    } catch (error) {
      if (prepared != null && prepared.printStatus == 'Reserved') {
        try {
          await _failPrint(prepared.printHistoryId, error.toString());
        } catch (_) {
          // The backend remains the source of truth for failed reservations.
        }
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تعذر اعتماد الطباعة: $error')),
        );
      }
    } finally {
      if (mounted) setState(() => _loadingPrintAction = false);
    }
  }

  Future<void> _printSelectedPiece(_OrderPieceDetail piece) async {
    if (!mounted || _loadingPrintAction) return;

    List<_PrintHistoryRecord> history;
    try {
      history = await _fetchPrintHistory(piece);
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تعذر قراءة سجل الطباعة: $error')));
      return;
    }

    _ReprintFormData? form;
    if (_isPiecePrinted(piece)) {
      if (!await _confirmReprint(piece, history) || !mounted) return;
      try {
        form = await _showReprintForm(piece);
      } catch (error) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('تعذر تحميل بيانات إعادة الطباعة: $error')));
        return;
      }
      if (form == null || !mounted) return;
    }

    final target = await _showPrintOptionsDialog(
        title: form == null ? 'طباعة بطاقة محددة' : 'إعادة طباعة بطاقة محددة');
    if (target == null || !mounted) return;
    await _executePrint(piece, form, target);
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

    await _executeBulkPrint(target);
  }

  Future<void> _executeBulkPrint(_PrintTarget target) async {
    if (_loadingPrintAction || !mounted) return;
    setState(() => _loadingPrintAction = true);

    try {
      final batchId = const Uuid().v4();
      final orderId = _selectedOrder?.orderId;
      final response = await http.post(
        Uri.parse('${widget.baseUrl}/printing/bulk/prepare'),
        headers: _authHeaders,
        body: jsonEncode({
          'batchRequestId': batchId,
          'snapshotId': const Uuid().v4(),
          'orderId': orderId,
        }),
      );
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(_readApiError(response));
      }

      final body = jsonDecode(response.body) as Map<String, dynamic>;
      final reserved = (body['reservedItems'] as List? ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList();
      final cards = (body['cards'] as List? ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList();
      if (reserved.isEmpty) {
        throw Exception('لا توجد بطاقات قابلة للحجز في اللقطة الحالية.');
      }

      final copiesByKey = <String, int>{};
      for (final item in reserved) {
        final isReadyMade = item['isReadyMade'] == true;
        final pieceId = (item['pieceId'] as num?)?.toInt() ?? 0;
        copiesByKey['${isReadyMade ? 'r' : 't'}:$pieceId'] =
            (item['copyNumber'] as num?)?.toInt() ?? 1;
      }

      final pieces = <_OrderPieceDetail>[];
      final copyNumbers = <int>[];
      final pages = <_BulkPrintPage>[];
      for (var index = 0; index < cards.length; index++) {
        final card = cards[index];
        final isReadyMade = card['isReadyMade'] == true;
        final pieceId = (card['pieceId'] as num?)?.toInt() ?? 0;
        final key = '${isReadyMade ? 'r' : 't'}:$pieceId';
        if (!copiesByKey.containsKey(key)) continue;
        final item = reserved.firstWhere(
          (entry) => entry['isReadyMade'] == isReadyMade && (entry['pieceId'] as num?)?.toInt() == pieceId,
          orElse: () => const <String, dynamic>{},
        );
        if (item.isEmpty) continue;
        final copyNumber = copiesByKey[key]!;
        final printHistoryId = (item['printHistoryId'] as num?)?.toInt() ?? 0;
        final printRequestId = item['printRequestId']?.toString() ?? '';
        pieces.add(_OrderPieceDetail.fromJson(card, readyMade: isReadyMade));
        copyNumbers.add(copyNumber);
        pages.add(_BulkPrintPage(
          pageNumber: pages.length + 1,
          isReadyMade: isReadyMade,
          pieceId: pieceId,
          printHistoryId: printHistoryId,
          printRequestId: printRequestId,
          copyNumber: copyNumber,
          orderNumber: (card['orderNumber'] ?? '').toString(),
          trackingCode: (card['trackingCode'] ?? '').toString(),
          pieceType: (card['pieceType'] ?? '').toString(),
          pieceNumber: (card['pieceNumber'] as num?)?.toInt(),
          cardJson: card,
        ));
      }

      if (pieces.isEmpty) {
        throw Exception('تعذر بناء بيانات بطاقات الدفعة.');
      }

      final bytes = await _MeasurementCardPdfGenerator.generateMany(
        pieces,
        copyNumbers,
        _header,
      );

      if (target == _PrintTarget.savePdf) {
        if (!mounted) return;
        final directoryPath = await _PrintService.resolveSaveDirectory(context);
        final fullPath = '$directoryPath${Platform.pathSeparator}measurement_cards_${DateTime.now().millisecondsSinceEpoch}.pdf';
        await File(fullPath).writeAsBytes(bytes);
      } else if (target == _PrintTarget.officePrinter ||
          target == _PrintTarget.pdfPreview) {
        await Printing.layoutPdf(onLayout: (_) async => bytes);
      }

      await _persistPendingBulk(batchId, pages);
      await _showBulkOutcomeDialog(batchId, pages);
      await _refreshUnprintedSummary();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تم تجهيز دفعة بطاقات من ${reserved.length} بطاقة عبر نافذة طباعة واحدة.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر تنفيذ الطباعة الجماعية: $error')),
      );
    } finally {
      if (mounted) setState(() => _loadingPrintAction = false);
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
        final isLight = Theme.of(context).brightness == Brightness.light;
        final backgroundColor = isLight
            ? (isSelected ? const Color(0xFFE8F5F2) : const Color(0xFFFFFFFF))
            : (isSelected ? UiPalette.primaryDark : UiPalette.surfaceCard);
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
        final isPrinted = _isPiecePrinted(piece);
        final title = _pieceTypeLabels[piece.pieceId]?.trim().isNotEmpty == true
            ? _pieceTypeLabels[piece.pieceId]!
            : piece.pieceType.trim().isNotEmpty
                ? piece.pieceType
                : 'القطعة ${piece.pieceNumber}';
        final statusText =
            isPrinted ? 'مطبوعة - إعادة الطباعة متاحة' : 'غير مطبوعة';

        return Material(
          color: Theme.of(context).brightness == Brightness.light
              ? const Color(0xFFFFFFFF)
              : UiPalette.surfaceCard,
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

  Future<void> _showBulkOutcomeDialog(String batchId, List<_BulkPrintPage> pages) async {
    final choice = await showDialog<_BulkOutcome>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        title: const Text('تأكيد نتيجة الطباعة الجماعية'),
        content: const Text('اختر ما خرج فعليًا من الطابعة. لن يتم اعتماد أي بطاقة قبل هذا التأكيد.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(_BulkOutcome.cancelled),
            child: const Text('أُلغيت ولم تبدأ'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(_BulkOutcome.unknown),
            child: const Text('النتيجة غير معروفة'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(_BulkOutcome.partial),
            child: const Text('توقفت جزئيًا'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(_BulkOutcome.full),
            child: Text('تمت الطباعة كاملة (${pages.length})'),
          ),
        ],
      ),
    );

    if (choice == null) return;
    if (choice == _BulkOutcome.full) {
      await _confirmBulkPages(batchId, pages);
      await _clearPendingBulk();
      return;
    }
    if (choice == _BulkOutcome.cancelled) {
      await _cancelBulkPages(batchId, pages);
      await _clearPendingBulk();
      return;
    }
    if (choice == _BulkOutcome.unknown) return;

    var cutoff = pages.length;
    var confirmedThroughLast = true;
    final partialChoice = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('تحديد آخر صفحة خرجت سليمة'),
          content: SizedBox(
            width: 520,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  DropdownButtonFormField<bool>(
                    initialValue: confirmedThroughLast,
                    decoration: const InputDecoration(labelText: 'آلية التعريف'),
                    items: const [
                      DropdownMenuItem(value: true, child: Text('آخر بطاقة خرجت سليمة')),
                      DropdownMenuItem(value: false, child: Text('أول بطاقة يجب إعادة الطباعة منها')),
                    ],
                    onChanged: (value) {
                      if (value == null) return;
                      setDialogState(() => confirmedThroughLast = value);
                    },
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<int>(
                    initialValue: cutoff,
                    decoration: const InputDecoration(labelText: 'رقم الصفحة'),
                    items: [
                      for (final page in pages)
                        DropdownMenuItem(value: page.pageNumber, child: Text('${page.pageNumber} — ${page.orderNumber} — ${page.trackingCode} — ${page.pieceType}')),
                    ],
                    onChanged: (value) {
                      if (value != null) setDialogState(() => cutoff = value);
                    },
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('إلغاء')), 
            FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('اعتماد النتيجة الجزئية')),
          ],
        ),
      ),
    );
    if (partialChoice != true) return;

    final confirmedCount = confirmedThroughLast ? cutoff : cutoff - 1;
    final confirmed = pages.where((page) => page.pageNumber <= confirmedCount).toList();
    final remaining = confirmedThroughLast
        ? pages.where((page) => page.pageNumber > cutoff).toList()
        : pages.where((page) => page.pageNumber >= cutoff).toList();

    await _confirmBulkPages(batchId, confirmed);
    if (remaining.isEmpty) {
      await _clearPendingBulk();
    } else {
      final resumed = remaining.asMap().entries.map((entry) => _BulkPrintPage(
        pageNumber: entry.key + 1,
        isReadyMade: entry.value.isReadyMade,
        pieceId: entry.value.pieceId,
        printHistoryId: entry.value.printHistoryId,
        printRequestId: entry.value.printRequestId,
        copyNumber: entry.value.copyNumber,
        orderNumber: entry.value.orderNumber,
        trackingCode: entry.value.trackingCode,
        pieceType: entry.value.pieceType,
        pieceNumber: entry.value.pieceNumber,
        cardJson: entry.value.cardJson,
      )).toList();
      await _persistPendingBulk(batchId, resumed);
    }
  }

  Future<void> _confirmBulkPages(String batchId, List<_BulkPrintPage> pages) async {
    if (pages.isEmpty) return;
    await http.post(
      Uri.parse('${widget.baseUrl}/printing/bulk/confirm'),
      headers: _authHeaders,
      body: jsonEncode({
        'batchRequestId': batchId,
        'printHistoryIds': pages.map((page) => page.printHistoryId).toList(),
      }),
    );
  }

  Future<void> _cancelBulkPages(String batchId, List<_BulkPrintPage> pages) async {
    if (pages.isEmpty) return;
    await http.post(
      Uri.parse('${widget.baseUrl}/printing/bulk/cancel?reason=${Uri.encodeComponent('تم إلغاء الدفعة قبل تأكيد الطباعة')}'),
      headers: _authHeaders,
      body: jsonEncode({
        'batchRequestId': batchId,
        'printHistoryIds': pages.map((page) => page.printHistoryId).toList(),
      }),
    );
  }

  Future<void> _resumePendingBulk() async {
    if (_pendingBulkBatchId == null || _pendingBulkPages.isEmpty) return;
    final pieces = _pendingBulkPages.map((page) => _OrderPieceDetail.fromJson(page.cardJson, readyMade: page.isReadyMade)).toList();
    final copyNumbers = _pendingBulkPages.map((page) => page.copyNumber).toList();
    final target = await _showPrintOptionsDialog(title: 'استئناف البطاقات غير المؤكدة');
    if (target == null || !mounted) return;
    final bytes = await _MeasurementCardPdfGenerator.generateMany(pieces, copyNumbers, _header);
    if (target == _PrintTarget.officePrinter || target == _PrintTarget.pdfPreview) {
      await Printing.layoutPdf(onLayout: (_) async => bytes);
    }
    await _showBulkOutcomeDialog(_pendingBulkBatchId!, _pendingBulkPages);
  }

  Widget _buildStatusBar(BuildContext context) {
    final isLight = Theme.of(context).brightness == Brightness.light;
    final background = isLight ? const Color(0xFFF7FAFA) : UiPalette.surfaceCard;
    final border = isLight ? const Color(0xFFDDE7E4) : const Color(0xFF3A4A46);

    Widget card({required IconData icon, required String label, required int value, required Color color}) {
      return Container(
        constraints: const BoxConstraints(minWidth: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: border),
        ),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(width: 8),
          Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
            Text(label, style: TextStyle(fontSize: 11, color: isLight ? const Color(0xFF66756F) : const Color(0xFFB8C5C1))),
            Text('$value', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: isLight ? const Color(0xFF17362E) : Colors.white)),
          ]),
        ]),
      );
    }

    return SizedBox(
      height: 58,
      width: double.infinity,
      child: Directionality(
        textDirection: ui.TextDirection.rtl,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          itemCount: 2 + _unprintedByType.length,
          separatorBuilder: (_, __) => const SizedBox(width: 8),
          itemBuilder: (context, index) {
            if (index == 0) {
              return card(icon: Icons.print_outlined, label: 'البطاقات المطبوعة', value: _allPrintedCards, color: const Color(0xFF2E9D5A));
            }
            if (index == 1) {
              return card(icon: Icons.print_disabled_outlined, label: 'البطاقات غير المطبوعة', value: _allUnprintedCards, color: const Color(0xFFE5A63A));
            }
            final type = _unprintedByType[index - 2];
            return card(icon: Icons.category_outlined, label: type.productTypeName, value: type.count, color: const Color(0xFF2F6FED));
          },
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final filtered = _orders.where((order) {
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
              fillColor: Theme.of(context).brightness == Brightness.light
                  ? const Color(0xFFF8FAFC)
                  : UiPalette.surfaceCard,
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
          _buildStatusBar(context),
          if (_pendingBulkPages.isNotEmpty) ...[
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                onPressed: _resumePendingBulk,
                icon: const Icon(Icons.restart_alt),
                label: Text('استئناف دفعة غير محسومة (${_pendingBulkPages.length} بطاقة)'),
              ),
            ),
          ],
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

class _MeasurementStatusType {
  const _MeasurementStatusType({
    required this.productTypeId,
    required this.productTypeName,
    required this.count,
  });

  factory _MeasurementStatusType.fromJson(Map<String, dynamic> json) {
    return _MeasurementStatusType(
      productTypeId: (json['productTypeId'] as num?)?.toInt() ?? 0,
      productTypeName: (json['productTypeName'] ?? 'غير محدد').toString(),
      count: (json['count'] as num?)?.toInt() ?? 0,
    );
  }

  final int productTypeId;
  final String productTypeName;
  final int count;
}

enum _BulkOutcome { full, partial, cancelled, unknown }

class _BulkPrintPage {
  const _BulkPrintPage({
    required this.pageNumber,
    required this.isReadyMade,
    required this.pieceId,
    required this.printHistoryId,
    required this.printRequestId,
    required this.copyNumber,
    required this.orderNumber,
    required this.trackingCode,
    required this.pieceType,
    required this.pieceNumber,
    required this.cardJson,
  });

  factory _BulkPrintPage.fromJson(Map<String, dynamic> json) {
    final card = json['cardJson'] as Map<String, dynamic>? ?? const <String, dynamic>{};
    return _BulkPrintPage(
      pageNumber: (json['pageNumber'] as num?)?.toInt() ?? 1,
      isReadyMade: json['isReadyMade'] == true,
      pieceId: (json['pieceId'] as num?)?.toInt() ?? 0,
      printHistoryId: (json['printHistoryId'] as num?)?.toInt() ?? 0,
      printRequestId: (json['printRequestId'] ?? '').toString(),
      copyNumber: (json['copyNumber'] as num?)?.toInt() ?? 1,
      orderNumber: (json['orderNumber'] ?? '').toString(),
      trackingCode: (json['trackingCode'] ?? '').toString(),
      pieceType: (json['pieceType'] ?? '').toString(),
      pieceNumber: (json['pieceNumber'] as num?)?.toInt(),
      cardJson: Map<String, dynamic>.from(card),
    );
  }

  final int pageNumber;
  final bool isReadyMade;
  final int pieceId;
  final int printHistoryId;
  final String printRequestId;
  final int copyNumber;
  final String orderNumber;
  final String trackingCode;
  final String pieceType;
  final int? pieceNumber;
  final Map<String, dynamic> cardJson;

  Map<String, dynamic> toJson() => {
    'pageNumber': pageNumber,
    'isReadyMade': isReadyMade,
    'pieceId': pieceId,
    'printHistoryId': printHistoryId,
    'printRequestId': printRequestId,
    'copyNumber': copyNumber,
    'orderNumber': orderNumber,
    'trackingCode': trackingCode,
    'pieceType': pieceType,
    'pieceNumber': pieceNumber,
    'cardJson': cardJson,
  };
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
    if (!context.mounted) return defaultDirectory.path;
    final selectedPath = await showDialog<String>(
      context: context,
      builder: (dialogContext) {
        final isLight = Theme.of(dialogContext).brightness == Brightness.light;
        return AlertDialog(
          backgroundColor:
              isLight ? const Color(0xFFFFFFFF) : UiPalette.surfaceCard,
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

  static Future<String> resolveSaveDirectory(BuildContext context) =>
      _PrintService._resolveSaveDirectory(context);

  static Future<void> execute({
    required BuildContext context,
    required _OrderPieceDetail piece,
    required _MeasurementHeaderSettings header,
    required _PrintTarget target,
    int copyNumber = 1,
  }) async {
    final messenger = ScaffoldMessenger.maybeOf(context);
    switch (target) {
      case _PrintTarget.pdfPreview:
        final bytes = await _MeasurementCardPdfGenerator.generate(piece, header,
            copyNumber: copyNumber);
        await Printing.layoutPdf(onLayout: (_) => bytes);
        messenger?.showSnackBar(
          const SnackBar(content: Text('تم تجهيز معاينة PDF لبطاقة المقاسات.')),
        );
        break;
      case _PrintTarget.savePdf:
        final bytes = await _MeasurementCardPdfGenerator.generate(piece, header,
            copyNumber: copyNumber);
        if (!context.mounted) return;
        final directoryPath = await _resolveSaveDirectory(context);
        final fileName =
            'measurement_card_${DateTime.now().millisecondsSinceEpoch}.pdf';
        final fullPath = '$directoryPath${Platform.pathSeparator}$fileName';
        final file = File(fullPath);
        await file.writeAsBytes(bytes);
        messenger?.showSnackBar(
          SnackBar(content: Text('تم حفظ PDF في: $fullPath')),
        );
        break;
      case _PrintTarget.officePrinter:
        final bytes = await _MeasurementCardPdfGenerator.generate(piece, header,
            copyNumber: copyNumber);
        await Printing.layoutPdf(onLayout: (_) => bytes);
        messenger?.showSnackBar(
          const SnackBar(
              content: Text('تم إرسال البطاقة إلى نافذة الطباعة المكتبية.')),
        );
        break;
      case _PrintTarget.labelPrinter:
        messenger?.showSnackBar(
          const SnackBar(
            content: Text(
                'طباعة الملصقات قابلة للتوسعة لاحقاً؛ لا يوجد تكامل فعلي للطابعة الحالية في هذه المرحلة.'),
          ),
        );
        break;
    }
  }
}

class _MeasurementCardPdfGenerator {
  static String _copyLabel(int copyNumber) {
    switch (copyNumber) {
      case 2:
        return 'النسخة الثانية';
      case 3:
        return 'النسخة الثالثة';
      case 4:
        return 'النسخة الرابعة';
      default:
        return copyNumber > 1 ? 'النسخة رقم $copyNumber' : '';
    }
  }

  static Future<Uint8List> generateMany(
    List<_OrderPieceDetail> pieces,
    List<int> copyNumbers,
    _MeasurementHeaderSettings header,
  ) async {
    final pdf = pw.Document();
    final layout = DocumentPrintSupport.layout;
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
      imageHeight: layout.headerImageHeight,
    );

    for (var index = 0; index < pieces.length; index++) {
      final piece = pieces[index];
      final copyNumber = copyNumbers[index];
      final typeLabel = piece.pieceType.trim().isNotEmpty
          ? piece.pieceType
          : 'غير محدد';
      final barcodeValue = piece.barcode.trim().isNotEmpty
          ? piece.barcode
          : piece.trackingCode;
      final measurementRows = DocumentPrintSupport.buildMeasurementGridRows(
        piece.measurements,
      ).map((rowEntries) {
        return pw.Table(
          border: pw.TableBorder.all(width: layout.measurementTableBorderWidth),
          children: [
            pw.TableRow(
              children: rowEntries.map((entry) => pw.Container(
                padding: pw.EdgeInsets.symmetric(
                  horizontal: layout.measurementCellPaddingX,
                  vertical: layout.measurementCellPaddingY,
                ),
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                  children: [
                    pw.Text(entry.key,
                        textAlign: pw.TextAlign.center,
                        textDirection: pw.TextDirection.rtl,
                        style: pw.TextStyle(font: arabicFont, fontSize: layout.measurementLabelSize, fontWeight: pw.FontWeight.bold)),
                    pw.SizedBox(height: layout.measurementCellPaddingY),
                    pw.Text(entry.value?.toString().trim().isNotEmpty == true
                            ? entry.value.toString()
                            : '-',
                        textAlign: pw.TextAlign.center,
                        textDirection: pw.TextDirection.rtl,
                        style: pw.TextStyle(font: arabicFont, fontSize: layout.measurementValueSize, fontWeight: pw.FontWeight.bold)),
                  ],
                ),
              )).toList(),
            ),
          ],
        );
      }).toList();

      pdf.addPage(pw.Page(
        pageFormat: DocumentPrintSupport.measurementCardPortrait,
        build: (pw.Context context) {
          return pw.Column(
            crossAxisAlignment: pw.CrossAxisAlignment.stretch,
            children: [
              headerWidget,
              pw.SizedBox(height: 6),
              pw.Divider(thickness: 1),
              if (copyNumber > 1)
                pw.Text(_copyLabel(copyNumber),
                    textAlign: pw.TextAlign.right,
                    textDirection: pw.TextDirection.rtl,
                    style: pw.TextStyle(font: arabicFont, fontSize: 12, fontWeight: pw.FontWeight.bold, color: PdfColors.red)),
              pw.Container(
                decoration: pw.BoxDecoration(border: pw.Border.all(width: layout.outerBorderWidth)),
                padding: pw.EdgeInsets.all(layout.sectionPadding),
                child: pw.Row(
                  crossAxisAlignment: pw.CrossAxisAlignment.start,
                  children: [
                    pw.Container(
                      width: 60,
                      child: pw.BarcodeWidget(barcode: pw.Barcode.qrCode(), data: piece.qrCode, width: 60, height: 60, drawText: false),
                    ),
                    pw.SizedBox(width: 8),
                    pw.Expanded(child: pw.Column(
                      crossAxisAlignment: pw.CrossAxisAlignment.end,
                      children: [
                        pw.Text('/  ${piece.customerName}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: 12)),
                        pw.Text('النوع: $typeLabel', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('الطلب: ${piece.orderNumber}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('تاريخ الطلب: ${piece.orderDateText}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('الاستلام: ${piece.deliveryDateText}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('التتبع: ${piece.trackingCode}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: 11)),
                      ],
                    )),
                  ],
                ),
              ),
              pw.SizedBox(height: 4),
              pw.Container(
                decoration: pw.BoxDecoration(border: pw.Border.all(width: 0.8)),
                padding: const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                child: pw.BarcodeWidget(barcode: pw.Barcode.code128(), data: barcodeValue, width: 140, height: 30, drawText: false),
              ),
              if (piece.fabricCode.isNotEmpty || piece.fabricType.isNotEmpty || piece.fabricColor.isNotEmpty || piece.catalogNumber.isNotEmpty)
                pw.Container(
                  margin: pw.EdgeInsets.only(top: layout.dividerHeight),
                  decoration: pw.BoxDecoration(border: pw.Border.all(width: layout.innerBorderWidth)),
                  padding: pw.EdgeInsets.all(layout.sectionPadding),
                  child: pw.Column(crossAxisAlignment: pw.CrossAxisAlignment.start, children: [
                    pw.Text('بيانات القماش', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: layout.sectionTitleSize, fontWeight: pw.FontWeight.bold)),
                    pw.Text('الكود: ${piece.fabricCode.isNotEmpty ? piece.fabricCode : 'غير محدد'}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: layout.metaTextSize)),
                    pw.Text('النوع: ${piece.fabricType.isNotEmpty ? piece.fabricType : 'غير محدد'}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: layout.metaTextSize)),
                    pw.Text('اللون: ${piece.fabricColor.isNotEmpty ? piece.fabricColor : 'غير محدد'}', textAlign: pw.TextAlign.right, textDirection: pw.TextDirection.rtl, style: pw.TextStyle(font: arabicFont, fontSize: layout.metaTextSize)),
                  ]),
                ),
              if (measurementRows.isNotEmpty)
                pw.Container(
                  margin: pw.EdgeInsets.only(top: layout.dividerHeight),
                  decoration: pw.BoxDecoration(border: pw.Border.all(width: layout.innerBorderWidth)),
                  padding: pw.EdgeInsets.all(layout.sectionPadding),
                  child: pw.Column(children: measurementRows),
                ),
            ],
          );
        },
      ),
    );
    }

    return pdf.save();
  }

  static Future<Uint8List> generate(
      _OrderPieceDetail piece, _MeasurementHeaderSettings header,
      {int copyNumber = 1}) async {
    final pdf = pw.Document();
    final layout = DocumentPrintSupport.layout;
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
      imageHeight: layout.headerImageHeight,
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
                  style: pw.TextStyle(
                      font: arabicFont, fontSize: layout.metaTextSize)),
            ),
            pw.SizedBox(width: layout.sectionSpacing),
            pw.Expanded(
              child: pw.Text(
                  'اللنوع : ${piece.fabricType.isNotEmpty ? piece.fabricType : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(
                      font: arabicFont, fontSize: layout.metaTextSize)),
            ),
          ],
        ),
      );
      fabricRows.add(pw.SizedBox(height: layout.sectionSpacing));
      fabricRows.add(
        pw.Row(
          crossAxisAlignment: pw.CrossAxisAlignment.start,
          children: [
            pw.Expanded(
              child: pw.Text(
                  'اللون : ${piece.fabricColor.isNotEmpty ? piece.fabricColor : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(
                      font: arabicFont, fontSize: layout.metaTextSize)),
            ),
            pw.SizedBox(width: layout.sectionSpacing),
            pw.Expanded(
              child: pw.Text(
                  ' الكتالوج: ${piece.catalogNumber.isNotEmpty ? piece.catalogNumber : 'غير محدد'}',
                  textAlign: pw.TextAlign.right,
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(
                      font: arabicFont, fontSize: layout.metaTextSize)),
            ),
          ],
        ),
      );
      fabricRows.add(pw.SizedBox(height: layout.sectionSpacing));
      fabricRows.add(
        pw.Text(
            'الاستهلاك: ${piece.consumptionDisplay.isNotEmpty ? piece.consumptionDisplay : 'غير محدد'}',
            textAlign: pw.TextAlign.right,
            textDirection: pw.TextDirection.rtl,
            style:
                pw.TextStyle(font: arabicFont, fontSize: layout.metaTextSize)),
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
          style:
              pw.TextStyle(font: arabicFont, fontSize: layout.requestTextSize),
        ),
      ));
      specialRows.add(pw.SizedBox(height: layout.sectionSpacing));
      specialRows.add(pw.Align(
        alignment: pw.Alignment.centerRight,
        child: pw.Text(
          '  2: ${piece.request2.isNotEmpty ? piece.request2 : 'لا يوجد'}',
          textAlign: pw.TextAlign.right,
          textDirection: pw.TextDirection.rtl,
          style:
              pw.TextStyle(font: arabicFont, fontSize: layout.requestTextSize),
        ),
      ));
      specialRows.add(pw.SizedBox(height: layout.sectionSpacing));
      specialRows.add(pw.Align(
        alignment: pw.Alignment.centerRight,
        child: pw.Text(
          '4 : ${piece.specialRequest.isNotEmpty ? piece.specialRequest : 'لا يوجد'}',
          textAlign: pw.TextAlign.right,
          textDirection: pw.TextDirection.rtl,
          style:
              pw.TextStyle(font: arabicFont, fontSize: layout.requestTextSize),
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

    final measurementRows = <pw.Widget>[];
    for (final rowEntries
        in DocumentPrintSupport.buildMeasurementGridRows(piece.measurements)) {
      final rowCells = rowEntries
          .map(
            (entry) => pw.Container(
              padding: pw.EdgeInsets.symmetric(
                horizontal: layout.measurementCellPaddingX,
                vertical: layout.measurementCellPaddingY,
              ),
              child: pw.Column(
                mainAxisSize: pw.MainAxisSize.min,
                crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                children: [
                  pw.Text(
                    entry.key,
                    textAlign: pw.TextAlign.center,
                    textDirection: pw.TextDirection.rtl,
                    style: pw.TextStyle(
                      font: arabicFont,
                      fontSize: layout.measurementLabelSize,
                      fontWeight: pw.FontWeight.bold,
                    ),
                  ),
                  pw.SizedBox(height: layout.measurementCellPaddingY),
                  pw.Text(
                    measurementText(entry.value),
                    textAlign: pw.TextAlign.center,
                    textDirection: pw.TextDirection.rtl,
                    style: pw.TextStyle(
                      font: arabicFont,
                      fontSize: layout.measurementValueSize,
                      fontWeight: pw.FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          )
          .toList();

      measurementRows.add(
        pw.Padding(
          padding: const pw.EdgeInsets.only(bottom: 0),
          child: pw.Table(
            columnWidths: {
              for (var i = 0; i < rowCells.length; i++)
                i: const pw.FlexColumnWidth(1),
            },
            border:
                pw.TableBorder.all(width: layout.measurementTableBorderWidth),
            children: [
              pw.TableRow(
                children: rowCells,
              ),
            ],
          ),
        ),
      );
    }

    pdf.addPage(
      pw.Page(
        pageFormat: DocumentPrintSupport.measurementCardPortrait,
        build: (pw.Context context) {
          final rows = <pw.Widget>[];

          rows.add(pw.Container(
            padding: const pw.EdgeInsets.only(bottom: 4),
            child: headerWidget,
          ));

          rows.add(pw.Divider(thickness: 1));
          if (copyNumber > 1) {
            rows.add(
              pw.Align(
                alignment: pw.Alignment.centerRight,
                child: pw.Text(
                  _copyLabel(copyNumber),
                  textDirection: pw.TextDirection.rtl,
                  style: pw.TextStyle(
                    font: arabicFont,
                    fontSize: 12,
                    fontWeight: pw.FontWeight.bold,
                    color: PdfColors.red,
                  ),
                ),
              ),
            );
          }
          rows.add(pw.SizedBox(height: 0));

          rows.add(
            pw.Container(
              decoration: pw.BoxDecoration(
                border: pw.Border.all(width: layout.outerBorderWidth),
              ),
              padding: pw.EdgeInsets.all(layout.sectionPadding),
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
                            style:
                                pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('الطلب: ${piece.orderNumber}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style:
                                pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('تاريخ الطلب: ${piece.orderDateText}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style:
                                pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('الاستلام: ${piece.deliveryDateText}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style:
                                pw.TextStyle(font: arabicFont, fontSize: 11)),
                        pw.Text('التتبع: ${piece.trackingCode}',
                            textAlign: pw.TextAlign.right,
                            textDirection: pw.TextDirection.rtl,
                            style:
                                pw.TextStyle(font: arabicFont, fontSize: 11)),
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
              padding:
                  const pw.EdgeInsets.symmetric(horizontal: 6, vertical: 2),
              child: pw.BarcodeWidget(
                barcode: pw.Barcode.code128(),
                data: barcodeValue,
                width: 140,
                height: 30,
                drawText: false,
              ),
            ),
          );

          if (fabricRows.isNotEmpty) {
            rows.add(
              pw.Divider(
                height: layout.dividerHeight,
                color: const PdfColor.fromInt(0xFF000000),
              ),
            );
            rows.add(
              pw.Container(
                decoration: pw.BoxDecoration(
                    border: pw.Border.all(width: layout.innerBorderWidth)),
                padding: pw.EdgeInsets.all(layout.sectionPadding),
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.start,
                  children: [
                    pw.Text('بيانات القماش',
                        textAlign: pw.TextAlign.right,
                        textDirection: pw.TextDirection.rtl,
                        style: pw.TextStyle(
                            font: arabicFont,
                            fontSize: layout.sectionTitleSize,
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
                height: layout.dividerHeight,
                color: const PdfColor.fromInt(0xFF000000),
              ),
            );
            rows.add(
              pw.Container(
                decoration: pw.BoxDecoration(
                    border: pw.Border.all(width: layout.innerBorderWidth)),
                padding: pw.EdgeInsets.all(layout.sectionPadding),
                child: pw.Column(
                  crossAxisAlignment: pw.CrossAxisAlignment.start,
                  children: [
                    pw.Text('الطلبات ',
                        textAlign: pw.TextAlign.right,
                        textDirection: pw.TextDirection.rtl,
                        style: pw.TextStyle(
                            font: arabicFont,
                            fontSize: layout.sectionTitleSize,
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
                height: layout.dividerHeight,
                color: const PdfColor.fromInt(0xFF000000),
              ),
            );
            rows.add(
              pw.Container(
                decoration: pw.BoxDecoration(
                    border: pw.Border.all(width: layout.innerBorderWidth)),
                padding: pw.EdgeInsets.fromLTRB(
                  layout.sectionSpacing,
                  0,
                  layout.sectionPadding,
                  layout.sectionPadding,
                ),
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

class _TailoringOrdersPrintingTab extends StatefulWidget {
  const _TailoringOrdersPrintingTab(
      {required this.baseUrl, required this.auth, required this.finalInvoice});
  final String baseUrl;
  final AuthState auth;
  final bool finalInvoice;

  @override
  State<_TailoringOrdersPrintingTab> createState() =>
      _TailoringOrdersPrintingTabState();
}

class _TailoringOrdersPrintingTabState
    extends State<_TailoringOrdersPrintingTab> {
  final TextEditingController _search = TextEditingController();
  Future<List<_OrderSummary>>? _future;

  Map<String, String> get _headers {
    final token = widget.auth.token;
    return {
      'Accept': 'application/json',
      if (token != null && token.trim().isNotEmpty)
        'Authorization': 'Bearer ${token.trim()}',
    };
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  void _load() {
    setState(() {
      _future = _fetch();
    });
  }

  Future<List<_OrderSummary>> _fetch() async {
    final customersResponse = await http
        .get(Uri.parse('${widget.baseUrl}/customers'), headers: _headers);
    final customers = <int, _CustomerSnapshot>{};
    if (customersResponse.statusCode >= 200 &&
        customersResponse.statusCode < 300) {
      final body = jsonDecode(customersResponse.body);
      if (body is List) {
        for (final item in body.whereType<Map<String, dynamic>>()) {
          final id = (item['customerId'] as num?)?.toInt() ?? 0;
          if (id > 0) {
            customers[id] = _CustomerSnapshot(
                name: (item['customerName'] ?? '').toString(),
                phone: (item['phoneNumber'] ?? '').toString());
          }
        }
      }
    }
    final response = await http.get(Uri.parse('${widget.baseUrl}/orders'),
        headers: _headers);
    if (response.statusCode < 200 || response.statusCode >= 300)
      throw StateError('تعذر تحميل الطلبات.');
    final entries = (jsonDecode(response.body) as List)
        .whereType<Map<String, dynamic>>()
        .map((json) => _OrderSummary.fromJson(json, customers))
        .where((order) => !order.isReadyMade)
        .where((order) {
      if (!widget.finalInvoice) return true;
      return order.orderStatus.toLowerCase() == 'delivered';
    }).toList()
      ..sort((a, b) => b.orderDate.compareTo(a.orderDate));
    return entries;
  }

  @override
  Widget build(BuildContext context) {
    return Column(children: [
      Padding(
          padding: const EdgeInsets.all(12),
          child: TextField(
              controller: _search,
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(
                  prefixIcon: Icon(Icons.search),
                  hintText: 'البحث برقم الطلب أو اسم/كود/هاتف العميل'))),
      Expanded(
          child: FutureBuilder<List<_OrderSummary>>(
              future: _future,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting)
                  return const Center(child: CircularProgressIndicator());
                if (snapshot.hasError)
                  return Center(child: Text('تعذر تحميل الطلبات المحفوظة.'));
                final query = _search.text.trim().toLowerCase();
                final filtered = (snapshot.data ?? []).where((order) {
                  if (query.isEmpty) return true;
                  return order.orderNumber.toLowerCase().contains(query) ||
                      order.customerName.toLowerCase().contains(query) ||
                      order.customerPhone.toLowerCase().contains(query);
                }).toList();
                if (filtered.isEmpty)
                  return Center(
                      child: Text(widget.finalInvoice
                          ? 'لا توجد طلبات مسلمة.'
                          : 'لا توجد طلبات محفوظة مطابقة.'));
                return ListView.separated(
                    padding: const EdgeInsets.all(12),
                    itemCount: filtered.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (context, index) {
                      final order = filtered[index];
                      return Card(
                          child: ListTile(
                              title: Text(order.orderNumber),
                              subtitle: Text(
                                  '${order.customerName} • ${order.customerPhone.isEmpty ? '-' : order.customerPhone}\n${order.orderStatus}'),
                              trailing: FilledButton.icon(
                                  onPressed: () async {
                                    final confirmed =
                                        await TailoringOrderDocumentsPrinting
                                            .confirmReprint(context);
                                    if (!confirmed) return;
                                    try {
                                      if (widget.finalInvoice) {
                                        await TailoringOrderDocumentsPrinting
                                            .printFinalInvoice(
                                                context, order.orderId);
                                      } else {
                                        await TailoringOrderDocumentsPrinting
                                            .printInitialReceipt(
                                                context, order.orderId);
                                      }
                                    } catch (_) {
                                      if (!context.mounted) return;
                                      ScaffoldMessenger.of(context)
                                          .showSnackBar(SnackBar(
                                              content: Text(widget.finalInvoice
                                                  ? 'تعذر تحميل بيانات التسوية النهائية للطلب.'
                                                  : 'تعذر تحميل بيانات الطلب المحفوظ.')));
                                    }
                                  },
                                  icon: Icon(widget.finalInvoice
                                      ? Icons.description_outlined
                                      : Icons.receipt_long_outlined),
                                  label: Text(widget.finalInvoice
                                      ? 'طباعة الفاتورة النهائية'
                                      : 'طباعة الإيصال'))));
                    });
              }))
    ]);
  }
}

class _PlaceholderTab extends StatelessWidget {
  const _PlaceholderTab({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final isLight = Theme.of(context).brightness == Brightness.light;
    return ColoredBox(
      color: isLight ? const Color(0xFFF4F7FA) : UiPalette.screenBackground,
      child: Center(
        child: Container(
          padding: const EdgeInsets.all(28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.construction_outlined,
                  size: 42, color: isLight ? const Color(0xFF168F86) : null),
              const SizedBox(height: 12),
              Text(
                title,
                style: TextStyle(
                  color: isLight ? const Color(0xFF172033) : UiPalette.textMain,
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                subtitle,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: isLight ? const Color(0xFF5B687A) : UiPalette.textSoft,
                  fontSize: 14,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FabricRollLabelsTab extends StatefulWidget {
  const _FabricRollLabelsTab(
      {required this.baseUrl, required this.auth, this.cachedLabels});

  final String baseUrl;
  final AuthState auth;
  final Future<List<_FabricRollLabel>>? cachedLabels;

  @override
  State<_FabricRollLabelsTab> createState() => _FabricRollLabelsTabState();
}

class _FabricRollLabelsTabState extends State<_FabricRollLabelsTab> {
  final _searchController = TextEditingController();
  final Set<int> _selected = <int>{};
  final Set<int> _selectedGroups = <int>{};
  double _labelWidthMm = 70;
  double _labelHeightMm = 50;
  late final TextEditingController _widthController;
  late final TextEditingController _heightController;
  Future<List<_FabricRollLabel>>? _labels;

  @override
  void initState() {
    super.initState();
    _widthController =
        TextEditingController(text: _labelWidthMm.toStringAsFixed(0));
    _heightController =
        TextEditingController(text: _labelHeightMm.toStringAsFixed(0));
    _labels = widget.cachedLabels ?? _loadLabels();
  }

  @override
  void dispose() {
    _searchController.dispose();
    _widthController.dispose();
    _heightController.dispose();
    super.dispose();
  }

  Map<String, String> get _headers {
    final token = widget.auth.token;
    return {
      'Accept': 'application/json',
      if (token != null && token.trim().isNotEmpty)
        'Authorization': 'Bearer ${token.trim()}',
    };
  }

  Future<List<_FabricRollLabel>> _loadLabels(
      {int? goodsReceiptItemId,
      String? catalogNumber,
      String? fabricCode}) async {
    final query = <String>[];
    if (goodsReceiptItemId != null)
      query.add('goodsReceiptItemId=$goodsReceiptItemId');
    if (catalogNumber != null && catalogNumber.trim().isNotEmpty) {
      query.add(
          'catalogNumber=${Uri.encodeQueryComponent(catalogNumber.trim())}');
    }
    if (fabricCode != null && fabricCode.trim().isNotEmpty) {
      query.add('fabricCode=${Uri.encodeQueryComponent(fabricCode.trim())}');
    }
    final uri = Uri.parse(
        '${widget.baseUrl}/inventory/fabric-roll-labels${query.isEmpty ? '' : '?${query.join('&')}'}');
    final response = await http.get(uri, headers: _headers);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تحميل ملصقات الرولات.');
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) return const <_FabricRollLabel>[];
    return decoded
        .cast<Map<String, dynamic>>()
        .map(_FabricRollLabel.fromJson)
        .toList();
  }

  Future<void> _search() async {
    final query = _searchController.text.trim();
    setState(() {
      _selected.clear();
      _labels = _loadLabels(catalogNumber: query, fabricCode: query);
    });
  }

  Future<Uint8List> _buildPdf(List<_FabricRollLabel> labels,
      {bool includeCatalogLabels = true}) async {
    pw.Font? arabicFont;
    try {
      arabicFont = await DocumentPrintSupport.loadArabicFont();
    } catch (_) {}

    final pdf = pw.Document();
    final pageFormat = PdfPageFormat(
      _labelWidthMm * PdfPageFormat.mm,
      _labelHeightMm * PdfPageFormat.mm,
      marginAll: 2 * PdfPageFormat.mm,
    );

    final catalogs = <String>{};
    if (includeCatalogLabels) {
      for (final label in labels) {
        final catalog = label.catalogNumber?.trim();
        if (catalog != null && catalog.isNotEmpty && catalogs.add(catalog)) {
          pdf.addPage(pw.Page(
            pageFormat: pageFormat,
            build: (_) => pw.Directionality(
              textDirection: pw.TextDirection.rtl,
              child: pw.Center(
                child: pw.Text(
                  catalog,
                  style: pw.TextStyle(
                    font: arabicFont,
                    fontSize: 18,
                    fontWeight: pw.FontWeight.bold,
                  ),
                ),
              ),
            ),
          ));
        }
      }
    }

    for (final label in labels) {
      pdf.addPage(pw.Page(
        pageFormat: pageFormat,
        build: (_) {
          final bodyStyle = pw.TextStyle(font: arabicFont, fontSize: 7);
          return pw.Directionality(
            textDirection: pw.TextDirection.rtl,
            child: pw.Column(
              crossAxisAlignment: pw.CrossAxisAlignment.stretch,
              children: [
                pw.Text(
                  label.catalogNumber ?? '-',
                  style: pw.TextStyle(
                      font: arabicFont,
                      fontSize: 9,
                      fontWeight: pw.FontWeight.bold),
                  textDirection: pw.TextDirection.rtl,
                ),
                pw.SizedBox(height: 1),
                pw.Text(
                  label.fabricCode,
                  style: pw.TextStyle(
                      font: arabicFont,
                      fontSize: 11,
                      fontWeight: pw.FontWeight.bold),
                  textDirection: pw.TextDirection.ltr,
                ),
                pw.SizedBox(height: 1),
                pw.BarcodeWidget(
                  barcode: pw.Barcode.code128(),
                  data: label.fabricCode,
                  width: 150,
                  height: 24,
                  drawText: false,
                ),
                pw.SizedBox(height: 1),
                pw.Row(
                  mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                  children: [
                    pw.Text('نوع القماش: ${label.fabricType}',
                        style: bodyStyle, textDirection: pw.TextDirection.rtl),
                    pw.Text('اللون: ${label.fabricColor ?? '-'}',
                        style: bodyStyle, textDirection: pw.TextDirection.rtl),
                  ],
                ),
                pw.Row(
                  mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
                  children: [
                    pw.Text(
                        'العرض: ${label.fabricWidth?.toStringAsFixed(2) ?? '-'} ${label.fabricWidthUnit == 'Inch' ? 'إنش' : (label.fabricWidthUnit ?? '')}',
                        style: bodyStyle,
                        textDirection: pw.TextDirection.rtl),
                    pw.Text('الكمية: ${label.quantity.toStringAsFixed(2)}',
                        style: bodyStyle, textDirection: pw.TextDirection.rtl),
                  ],
                ),
              ],
            ),
          );
        },
      ));
    }
    return pdf.save();
  }

  Future<void> _printLabels(List<_FabricRollLabel> labels,
      {bool includeCatalogLabels = true}) async {
    if (labels.isEmpty) return;
    final alreadyPrinted = labels.where((label) => label.isPrinted).toList();
    if (alreadyPrinted.isNotEmpty) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (dialogContext) {
          final isLight =
              Theme.of(dialogContext).brightness == Brightness.light;
          return AlertDialog(
            backgroundColor:
                isLight ? const Color(0xFFFFFFFF) : UiPalette.surfaceCard,
            title: const Text('إعادة الطباعة'),
            content: const Text('سبق طباعة هذا الملصق. هل تريد إعادة الطباعة؟'),
            actions: [
              TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(false),
                  child: const Text('إلغاء')),
              FilledButton(
                  onPressed: () => Navigator.of(dialogContext).pop(true),
                  child: const Text('إعادة الطباعة')),
            ],
          );
        },
      );
      if (confirmed != true) return;
    }
    try {
      final bytes =
          await _buildPdf(labels, includeCatalogLabels: includeCatalogLabels);
      await Printing.layoutPdf(onLayout: (_) async => bytes);
      if (!mounted) return;
      final printOperationId = const Uuid().v4();
      final printRecordResponse = await http.post(
        Uri.parse('${widget.baseUrl}/inventory/fabric-roll-labels/prints'),
        headers: {..._headers, 'Content-Type': 'application/json'},
        body: jsonEncode(<String, dynamic>{
          'fabricRollIds': labels.map((label) => label.fabricRollId).toList(),
          'printOperationId': printOperationId
        }),
      );
      if (!mounted) return;
      if (printRecordResponse.statusCode < 200 ||
          printRecordResponse.statusCode >= 300) {
        throw Exception('تعذر تسجيل نجاح الطباعة.');
      }
      setState(() {
        _labels = _loadLabels();
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تم تجهيز ${labels.length} ملصق للطباعة.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر تجهيز الملصقات للطباعة: $error')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _searchController,
                  decoration: InputDecoration(
                    labelText: 'بحث برقم الكتالوج أو FabricCode',
                    prefixIcon: const Icon(Icons.search),
                    filled: true,
                    fillColor: Theme.of(context).brightness == Brightness.light
                        ? const Color(0xFFF8FAFC)
                        : null,
                    border: OutlineInputBorder(
                      borderSide: BorderSide(
                        color: Theme.of(context).brightness == Brightness.light
                            ? const Color(0xFFCBD5E1)
                            : UiPalette.borderSoft,
                      ),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderSide: BorderSide(
                        color: Theme.of(context).brightness == Brightness.light
                            ? const Color(0xFF168F86)
                            : UiPalette.primaryBlue,
                        width: 1.5,
                      ),
                    ),
                  ),
                  onSubmitted: (_) => _search(),
                ),
              ),
              const SizedBox(width: 8),
              IconButton(onPressed: _search, icon: const Icon(Icons.search)),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Row(
            children: [
              SizedBox(
                  width: 140,
                  child: TextField(
                      controller: _widthController,
                      keyboardType: TextInputType.number,
                      decoration: InputDecoration(
                          labelText: 'عرض الملصق (مم)',
                          filled: true,
                          fillColor:
                              Theme.of(context).brightness == Brightness.light
                                  ? const Color(0xFFFFFFFF)
                                  : null,
                          labelStyle: TextStyle(
                              color: Theme.of(context).brightness ==
                                      Brightness.light
                                  ? const Color(0xFF172033)
                                  : null),
                          enabledBorder: OutlineInputBorder(
                              borderSide: BorderSide(
                                  color: Theme.of(context).brightness ==
                                          Brightness.light
                                      ? const Color(0xFFCBD5E1)
                                      : UiPalette.borderSoft)),
                          focusedBorder: OutlineInputBorder(
                              borderSide: BorderSide(
                                  color: Theme.of(context).brightness ==
                                          Brightness.light
                                      ? const Color(0xFF168F86)
                                      : UiPalette.primaryBlue,
                                  width: 1.5))),
                      onChanged: (v) {
                        final value = double.tryParse(v);
                        if (value != null && value > 0)
                          setState(() => _labelWidthMm = value);
                      })),
              const SizedBox(width: 8),
              SizedBox(
                  width: 140,
                  child: TextField(
                      controller: _heightController,
                      keyboardType: TextInputType.number,
                      decoration: InputDecoration(
                          labelText: 'ارتفاع الملصق (مم)',
                          filled: true,
                          fillColor:
                              Theme.of(context).brightness == Brightness.light
                                  ? const Color(0xFFFFFFFF)
                                  : null,
                          labelStyle: TextStyle(
                              color: Theme.of(context).brightness ==
                                      Brightness.light
                                  ? const Color(0xFF172033)
                                  : null),
                          enabledBorder: OutlineInputBorder(
                              borderSide: BorderSide(
                                  color: Theme.of(context).brightness ==
                                          Brightness.light
                                      ? const Color(0xFFCBD5E1)
                                      : UiPalette.borderSoft)),
                          focusedBorder: OutlineInputBorder(
                              borderSide: BorderSide(
                                  color: Theme.of(context).brightness ==
                                          Brightness.light
                                      ? const Color(0xFF168F86)
                                      : UiPalette.primaryBlue,
                                  width: 1.5))),
                      onChanged: (v) {
                        final value = double.tryParse(v);
                        if (value != null && value > 0)
                          setState(() => _labelHeightMm = value);
                      })),
            ],
          ),
        ),
        const SizedBox(height: 8),
        Expanded(
          child: FutureBuilder<List<_FabricRollLabel>>(
            future: _labels,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done)
                return const Center(child: CircularProgressIndicator());
              if (snapshot.hasError)
                return Center(child: Text(snapshot.error.toString()));
              final labels = snapshot.data ?? const <_FabricRollLabel>[];
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    child: Row(
                      children: [
                        FilledButton.icon(
                          onPressed: () {
                            final selectedLabels = labels
                                .where((label) =>
                                    _selected.contains(label.fabricRollId))
                                .toList();
                            _printLabels(selectedLabels,
                                includeCatalogLabels:
                                    selectedLabels.length != 1);
                          },
                          icon: const Icon(Icons.print_outlined),
                          label: const Text('طباعة المحدد'),
                        ),
                        const SizedBox(width: 8),
                        FilledButton.tonalIcon(
                            onPressed: () => _printLabels(labels),
                            icon: const Icon(Icons.print),
                            label: const Text('طباعة جميع رولات المخزن')),
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                  Expanded(
                    child: Builder(builder: (context) {
                      final groups = <int, List<_FabricRollLabel>>{};
                      for (final label in labels) {
                        groups
                            .putIfAbsent(label.goodsReceiptItemId,
                                () => <_FabricRollLabel>[])
                            .add(label);
                      }
                      final entries = groups.entries.toList()
                        ..sort((a, b) => a.key.compareTo(b.key));
                      return ListView.builder(
                        padding: const EdgeInsets.all(12),
                        itemCount: entries.length,
                        itemBuilder: (context, index) {
                          final entry = entries[index];
                          final groupLabels = entry.value;
                          final first = groupLabels.first;
                          final selectedCount = groupLabels
                              .where((label) =>
                                  _selected.contains(label.fabricRollId))
                              .length;
                          final allSelected =
                              selectedCount == groupLabels.length;
                          final printedCount = groupLabels
                              .where((label) => label.isPrinted)
                              .length;
                          final groupStatus = printedCount == 0
                              ? 'غير مطبوع'
                              : printedCount == groupLabels.length
                                  ? 'تمت طباعة جميع الرولات'
                                  : 'مطبوع جزئيًا';
                          final isLight =
                              Theme.of(context).brightness == Brightness.light;
                          return Card(
                            margin: const EdgeInsets.only(bottom: 12),
                            color: isLight ? const Color(0xFFFFFFFF) : null,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12),
                              side: BorderSide(
                                color: isLight
                                    ? const Color(0xFFCBD5E1)
                                    : UiPalette.borderSoft,
                              ),
                            ),
                            child: Padding(
                              padding: const EdgeInsets.all(12),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  Row(
                                    children: [
                                      Checkbox(
                                        value: allSelected,
                                        onChanged: (value) => setState(() {
                                          if (value == true) {
                                            _selected.addAll(groupLabels.map(
                                                (label) => label.fabricRollId));
                                            _selectedGroups.add(entry.key);
                                          } else {
                                            _selected.removeAll(groupLabels.map(
                                                (label) => label.fabricRollId));
                                            _selectedGroups.remove(entry.key);
                                          }
                                        }),
                                      ),
                                      Expanded(
                                        child: Column(
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Text(first.catalogNumber ?? '-',
                                                style: const TextStyle(
                                                    fontSize: 16,
                                                    fontWeight:
                                                        FontWeight.bold)),
                                            Text(
                                                '${first.fabricType}${first.fabricColor == null || first.fabricColor!.isEmpty ? '' : ' • ${first.fabricColor}'}'),
                                          ],
                                        ),
                                      ),
                                      Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.end,
                                        children: [
                                          Text('${groupLabels.length} رول',
                                              style: const TextStyle(
                                                  fontWeight: FontWeight.w700)),
                                          Text(groupStatus,
                                              style: TextStyle(
                                                  color: printedCount == 0
                                                      ? Colors.grey
                                                      : Colors.green,
                                                  fontSize: 12)),
                                        ],
                                      ),
                                    ],
                                  ),
                                  FilledButton.tonalIcon(
                                    onPressed: () => _printLabels(groupLabels),
                                    icon: const Icon(Icons.print_outlined),
                                    label: const Text('طباعة البند كاملًا'),
                                  ),
                                  const SizedBox(height: 8),
                                  ...groupLabels.map((label) {
                                    final checked =
                                        _selected.contains(label.fabricRollId);
                                    final printed = label.isPrinted;
                                    return CheckboxListTile(
                                      value: checked,
                                      onChanged: (value) => setState(() {
                                        if (value == true) {
                                          _selected.add(label.fabricRollId);
                                        } else {
                                          _selected.remove(label.fabricRollId);
                                        }
                                      }),
                                      title: Text(label.fabricCode),
                                      subtitle: Text(
                                          'Catalog: ${label.catalogNumber ?? '-'} | ${label.fabricType}',
                                          style: TextStyle(
                                              color: printed
                                                  ? Colors.green
                                                  : null)),
                                      secondary: printed
                                          ? const Chip(
                                              label: Text('تمت الطباعة'))
                                          : const Icon(Icons.label_outline),
                                    );
                                  }),
                                ],
                              ),
                            ),
                          );
                        },
                      );
                    }),
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }
}

class _FabricRollLabel {
  const _FabricRollLabel(
      {required this.fabricRollId,
      required this.goodsReceiptItemId,
      required this.catalogNumber,
      required this.fabricCode,
      required this.fabricType,
      required this.fabricColor,
      required this.fabricWidth,
      required this.fabricWidthUnit,
      required this.quantity,
      required this.unitCost,
      required this.isPrinted,
      required this.lastPrintedAt,
      required this.printCount});
  factory _FabricRollLabel.fromJson(Map<String, dynamic> json) =>
      _FabricRollLabel(
        fabricRollId: (json['fabricRollId'] as num).toInt(),
        goodsReceiptItemId: (json['goodsReceiptItemId'] as num).toInt(),
        catalogNumber: json['catalogNumber']?.toString(),
        fabricCode: (json['fabricCode'] ?? '').toString(),
        fabricType: (json['fabricType'] ?? '').toString(),
        fabricColor: json['fabricColor']?.toString(),
        fabricWidth: (json['fabricWidth'] as num?)?.toDouble(),
        fabricWidthUnit: json['fabricWidthUnit']?.toString(),
        quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
        unitCost: (json['unitCost'] as num?)?.toDouble() ?? 0,
        isPrinted: json['isPrinted'] == true,
        lastPrintedAt: json['lastPrintedAt']?.toString(),
        printCount: (json['printCount'] as num?)?.toInt() ?? 0,
      );
  final int fabricRollId;
  final int goodsReceiptItemId;
  final String? catalogNumber;
  final String fabricCode;
  final String fabricType;
  final String? fabricColor;
  final double? fabricWidth;
  final String? fabricWidthUnit;
  final double quantity;
  final double unitCost;
  final bool isPrinted;
  final String? lastPrintedAt;
  final int printCount;
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
        orderDate: DateTime.tryParse((json['createdAt'] ?? '').toString()) ??
            DateTime.now(),
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
    this.isReadyMade = false,
  });

  factory _OrderPieceDetail.fromJson(
    Map<String, dynamic> json, {
    bool readyMade = false,
  }) {
    final snapshotValue = json['measurementSnapshot'];
    final measurementMap = parseMeasurementSnapshot(snapshotValue);

    final rawTracking = (json['trackingCode'] ?? '').toString();
    final statusValue =
        (json['pieceStatus'] ?? json['status'] ?? '').toString();
    final fabricCode = DocumentPrintSupport.firstAvailableString([
      json['fabricCode'],
      json['FabricCode'],
      json['fabric_code'],
      json['Fabric_Code'],
      measurementMap['fabricCode'],
      measurementMap['FabricCode'],
    ]);
    final fabricType = DocumentPrintSupport.firstAvailableString([
      json['fabricType'],
      json['FabricType'],
      json['fabric_type'],
      json['Fabric_Type'],
      measurementMap['fabricType'],
      measurementMap['FabricType'],
    ]);
    final fabricColor = DocumentPrintSupport.firstAvailableString([
      json['fabricColor'],
      json['FabricColor'],
      json['fabric_color'],
      json['Fabric_Color'],
      measurementMap['fabricColor'],
      measurementMap['FabricColor'],
    ]);
    final catalogNumber = DocumentPrintSupport.firstAvailableString([
      json['catalogNumber'],
      json['CatalogNumber'],
      json['catalog_number'],
      json['Catalog_Number'],
      measurementMap['catalogNumber'],
      measurementMap['CatalogNumber'],
      measurementMap['barcode'],
      measurementMap['Barcode'],
      json['barcode'],
      json['Barcode'],
    ]);
    final consumptionRaw = DocumentPrintSupport.firstAvailableString([
      json['consumption'],
      json['Consumption'],
      json['consumptionValue'],
      json['ConsumptionValue'],
      measurementMap['_consumption'],
      measurementMap['consumption'],
      measurementMap['Consumption'],
      measurementMap['quantityInch'],
      measurementMap['QuantityInch'],
      measurementMap['availableInches'],
      measurementMap['AvailableInches'],
    ]);
    final consumption = double.tryParse(consumptionRaw) ?? 0;
    final consumptionUnit = DocumentPrintSupport.firstAvailableString([
      json['consumptionUnit'],
      json['ConsumptionUnit'],
      measurementMap['_consumptionUnit'],
      measurementMap['consumptionUnit'],
      measurementMap['ConsumptionUnit'],
      json['unit'],
      json['Unit'],
      measurementMap['unit'],
      measurementMap['Unit'],
      'بوصة',
    ]);
    final request1 = DocumentPrintSupport.firstAvailableString([
      json['request1'],
      json['Request1'],
      json['notes1'],
      json['Notes1'],
      measurementMap['request1'],
      measurementMap['Request1'],
    ]);
    final request2 = DocumentPrintSupport.firstAvailableString([
      json['request2'],
      json['Request2'],
      json['notes2'],
      json['Notes2'],
      measurementMap['request2'],
      measurementMap['Request2'],
    ]);
    final specialRequest = DocumentPrintSupport.firstAvailableString([
      json['specialRequest'],
      json['SpecialRequest'],
      json['specialRequests'],
      json['SpecialRequests'],
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
      isReadyMade: readyMade,
    );
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
  final bool isReadyMade;

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

class _PrintHistoryRecord {
  const _PrintHistoryRecord({
    required this.printHistoryId,
    required this.printStatus,
    required this.copyNumber,
    required this.trackingCode,
    required this.createdAt,
    this.printedAtUtc,
    this.reprintReasonCode,
    this.damageReason,
    this.responsibleEmployeeId,
    this.saleAmount,
    this.salePaymentType,
    this.financialTransactionReference,
  });

  factory _PrintHistoryRecord.fromJson(Map<String, dynamic> json) {
    int? optionalInt(String key) => (json[key] as num?)?.toInt();
    double? optionalDouble(String key) => (json[key] as num?)?.toDouble();
    DateTime? optionalDate(String key) =>
        DateTime.tryParse((json[key] ?? '').toString());

    return _PrintHistoryRecord(
      printHistoryId: (json['printHistoryId'] as num?)?.toInt() ?? 0,
      printStatus: (json['printStatus'] ?? '').toString(),
      copyNumber: (json['copyNumber'] as num?)?.toInt() ?? 0,
      trackingCode: (json['trackingCode'] ?? '').toString(),
      createdAt: optionalDate('createdAt') ?? DateTime.now(),
      printedAtUtc: optionalDate('printedAtUtc'),
      reprintReasonCode: json['reprintReasonCode']?.toString(),
      damageReason: json['damageReason']?.toString(),
      responsibleEmployeeId: optionalInt('responsibleEmployeeId'),
      saleAmount: optionalDouble('saleAmount'),
      salePaymentType: json['salePaymentType']?.toString(),
      financialTransactionReference:
          json['financialTransactionReference']?.toString(),
    );
  }

  final int printHistoryId;
  final String printStatus;
  final int copyNumber;
  final String trackingCode;
  final DateTime createdAt;
  final DateTime? printedAtUtc;
  final String? reprintReasonCode;
  final String? damageReason;
  final int? responsibleEmployeeId;
  final double? saleAmount;
  final String? salePaymentType;
  final String? financialTransactionReference;
}

class _EmployeeOption {
  const _EmployeeOption(
      {required this.id,
      required this.code,
      required this.name,
      required this.isActive});

  factory _EmployeeOption.fromJson(Map<String, dynamic> json) {
    final status = (json['status'] ?? '').toString().trim().toLowerCase();
    return _EmployeeOption(
      id: (json['employeeId'] as num?)?.toInt() ?? 0,
      code: (json['employeeCode'] ?? '').toString(),
      name: (json['employeeName'] ?? json['fullName'] ?? '').toString(),
      isActive: json['isActive'] as bool? ?? status == 'active',
    );
  }

  final int id;
  final String code;
  final String name;
  final bool isActive;
}

class _CustomerOption {
  const _CustomerOption({required this.id, required this.name});

  factory _CustomerOption.fromJson(Map<String, dynamic> json) =>
      _CustomerOption(
        id: (json['customerId'] as num?)?.toInt() ?? 0,
        name: (json['customerName'] ?? '').toString(),
      );

  final int id;
  final String name;
}

class _ReprintFormData {
  const _ReprintFormData({
    required this.reasonCode,
    this.damageReason,
    this.responsibleEmployeeId,
    this.notes,
    this.saleAmount,
    this.paymentType,
    this.cashAccountId,
    this.saleCustomerId,
  });

  final String reasonCode;
  final String? damageReason;
  final int? responsibleEmployeeId;
  final String? notes;
  final double? saleAmount;
  final String? paymentType;
  final int? cashAccountId;
  final int? saleCustomerId;
}
