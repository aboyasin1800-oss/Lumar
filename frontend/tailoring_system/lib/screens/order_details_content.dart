import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../models/order_models.dart';
import '../repositories/order_repository.dart';
import 'customer_details_screen.dart';
import 'printing/work_card_screen.dart';

final _money = NumberFormat('#,##0.00');
final _date = DateFormat('yyyy/MM/dd');
final _dateTime = DateFormat('yyyy/MM/dd HH:mm');

String _text(String? value) =>
    value == null || value.trim().isEmpty ? '-' : value;

List<_RequestFieldValue> _requestFieldValues(OrderItem? item) => [
      _RequestFieldValue('طلب رقم 1', item?.request1),
      _RequestFieldValue('طلب رقم 2', item?.request2),
      _RequestFieldValue('طلبات خاصة', item?.specialRequest),
    ]
        .where((entry) =>
            entry.value != null && entry.value!.trim().isNotEmpty)
        .toList();

class _RequestFieldValue {
  const _RequestFieldValue(this.label, this.value);

  final String label;
  final String? value;
}

String _status(String value) => switch (value.toLowerCase()) {
      'new' => 'جديد',
      'started' => 'تم البدء',
      'inproduction' || 'inprogress' => 'قيد الإنتاج',
      'ready' || 'readyfordelivery' => 'جاهز للتسليم',
      'readyforsale' => 'جاهز للبيع',
      'delivered' => 'تم التسليم',
      'paid' => 'مدفوع',
      'partialpaid' => 'مدفوع جزئياً',
      'cancelled' => 'ملغي',
      'printing' => 'الطباعة',
      'cutting' => 'القص',
      'sewing' => 'الخياطة',
      'assembly' => 'التجميع',
      'buttons' => 'الأزرار',
      'ironing' => 'الكي',
      'quality' => 'الجودة',
      _ => value,
    };
String _stage(String value) => switch (value.toLowerCase()) {
      'ordercreated' => 'إنشاء الطلب',
      'printing' => 'الطباعة',
      'cutting' => 'القص',
      'sewing' => 'الخياطة',
      'assembly' => 'التجميع',
      'buttons' => 'الأزرار',
      'ironing' => 'الكي',
      'quality' => 'الجودة',
      'production' => 'الإنتاج',
      'ready' => 'الجاهزية',
      'delivery' => 'التسليم',
      'reversal' => 'عكس الحركة',
      _ => value,
    };
String _paymentMethod(String? value) => switch (value?.toLowerCase()) {
      'cash' || 'كاش' => 'نقدي',
      'credit' => 'آجل',
      'refund' => 'استرداد',
      _ => _text(value)
    };
String _paymentKind(String value) => switch (value.toLowerCase()) {
      'advance' => 'دفعة مقدمة',
      'debtcollection' => 'تحصيل دين',
      'refund' => 'استرداد',
      _ => value
    };
String _transactionType(String value) => switch (value) {
      'DeliveryCost' => 'تكلفة التسليم',
      'RevenueRecognized' => 'إثبات الإيراد',
      'RevenueReversal' => 'عكس الإيراد',
      'OrderCancellationRefund' => 'استرداد إلغاء الطلب',
      'CustomerPayment' => 'دفعة عميل',
      'CustomerWriteOff' => 'خصم أو إعفاء عميل',
      'FabricCost' => 'تكلفة القماش',
      'CostReversal' => 'عكس التكلفة',
      'ReadyMadeRevenue' => 'إيراد بيع جاهز',
      'ReadyMadeCost' => 'تكلفة بيع جاهز',
      _ => value,
    };
String _measurementLabel(String value) => switch (value.toLowerCase()) {
      'length' => 'الطول',
      'neck' => 'الرقبة',
      'shoulder' => 'الكتف',
      'chest' => 'الصدر',
      'sleeve' => 'الكم',
      'waist' => 'الكمر',
      'abdomen' || 'belly' => 'البطن',
      'size' => 'المقاس',
      _ => value,
    };

class OrderDetailsContent extends StatefulWidget {
  const OrderDetailsContent({required this.orderId, super.key});
  final int orderId;

  @override
  State<OrderDetailsContent> createState() => _OrderDetailsContentState();
}

class _OrderDetailsContentState extends State<OrderDetailsContent> {
  final repository = OrderRepository();
  late Future<OrderDetailsData> future;
  bool _isCancelling = false;

  @override
  void initState() {
    super.initState();
    future = repository.getDetails(widget.orderId);
  }

  void _applyFuture(Future<OrderDetailsData> nextFuture) {
    if (!mounted) return;
    setState(() {
      future = nextFuture;
    });
  }

  void _setCancelling(bool value) {
    if (!mounted) return;
    setState(() {
      _isCancelling = value;
    });
  }

  void reload() {
    if (!mounted) return;
    setState(() => future = repository.getDetails(widget.orderId));
  }

  Future<void> _handleCancelOrder() async {
    if (_isCancelling) return;

    try {
      final current = await future;
      if (!mounted) return;

      if (current.order.status.toLowerCase() == 'cancelled') {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content:
                  Text('هذا الطلب ملغي بالفعل ولا توجد حاجة لإعادة الإلغاء.')),
        );
        return;
      }

      _setCancelling(true);
      await repository.cancelOrder(
        current.order.id,
        reason: 'إلغاء الطلب من شاشة التفاصيل',
        cancelledBy: 'FlutterApp',
      );

      final refreshed = await repository.getDetails(current.order.id);
      if (!mounted) return;
      _applyFuture(Future.value(refreshed));

      final eligiblePieces = _eligiblePiecesForDisposition(refreshed);
      if (eligiblePieces.isEmpty) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم إلغاء الطلب واسترداد مبلغ العميل.')),
        );
        return;
      }

      await _showDispositionDialog(eligiblePieces);
      if (!mounted) return;
      _applyFuture(repository.getDetails(current.order.id));
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر إلغاء الطلب: ${error.toString()}')),
      );
    } finally {
      if (mounted) {
        _setCancelling(false);
      }
    }
  }

  Future<void> _showDispositionDialog(
    List<_DispositionCandidate> eligiblePieces,
  ) async {
    if (!mounted) return;

    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        title: const Text('تم إلغاء الطلب واسترداد مبلغ العميل.'),
        content: SizedBox(
          width: 560,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'توجد قطعة أو أكثر بدأت الإنتاج أو أصبحت جاهزة.\nهل تريد إكمال إنتاج هذه القطع وتحويلها إلى مخزون منتجاتنا الجاهزة؟',
                  style: TextStyle(height: 1.5),
                ),
                const SizedBox(height: 12),
                ...eligiblePieces.map((piece) => Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Card(
                        child: Padding(
                          padding: const EdgeInsets.all(10),
                          child: Row(
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      'القطعة ${piece.number} • ${piece.trackingCode}',
                                      style: Theme.of(context)
                                          .textTheme
                                          .titleSmall,
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      'الحالة: ${_status(piece.status)}',
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodyMedium,
                                    ),
                                  ],
                                ),
                              ),
                              TextButton(
                                onPressed: () async {
                                  Navigator.of(dialogContext).pop();
                                  await _decidePiece(
                                      piece.id, 'ContinueToReadyInventory');
                                },
                                child: const Text(
                                    'نعم، إكمال الإنتاج والتحويل إلى الجاهز'),
                              ),
                              const SizedBox(width: 4),
                              TextButton(
                                onPressed: () async {
                                  Navigator.of(dialogContext).pop();
                                  await _decidePiece(piece.id, 'StopAndHold');
                                },
                                child: const Text('لا، إيقاف القطع واحتجازها'),
                              ),
                            ],
                          ),
                        ),
                      ),
                    )),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('رجوع'),
          ),
        ],
      ),
    );
  }

  Future<void> _decidePiece(int pieceId, String decision) async {
    try {
      await repository.savePieceDisposition(
        pieceId,
        decision,
        reason: decision == 'ContinueToReadyInventory'
            ? 'تأكيد إكمال الإنتاج والتحويل إلى الجاهز'
            : 'تأكيد إيقاف القطعة واحتجازها',
        decidedBy: 'FlutterApp',
      );

      if (decision == 'ContinueToReadyInventory') {
        await repository.executePieceDisposition(pieceId);
      }

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            decision == 'ContinueToReadyInventory'
                ? 'تم حفظ قرار استمرار القطعة في الجاهز بنجاح.'
                : 'تم حفظ قرار احتجاز القطعة بنجاح.',
          ),
        ),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حفظ قرار القطعة: ${error.toString()}')),
      );
    }
  }

  List<_DispositionCandidate> _eligiblePiecesForDisposition(
    OrderDetailsData data,
  ) {
    final tracked = data.trackingEvents;
    final candidates = <_DispositionCandidate>[];
    for (final piece in data.pieces) {
      final hasStartedProduction = piece.status.toLowerCase() != 'new' ||
          tracked.any((event) => event.pieceId == piece.id);
      if (!hasStartedProduction) {
        continue;
      }

      candidates.add(_DispositionCandidate(
        id: piece.id,
        number: piece.number,
        trackingCode: piece.trackingCode,
        status: piece.status,
      ));
    }
    return candidates;
  }

  @override
  Widget build(BuildContext context) => Scaffold(
      appBar: AppBar(title: Text('تفاصيل الطلب ${widget.orderId}'), actions: [
        IconButton(
            tooltip: 'تحديث',
            onPressed: reload,
            icon: const Icon(Icons.refresh))
      ]),
      body: SafeArea(
          child: FutureBuilder<OrderDetailsData>(
        future: future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError || snapshot.data == null) {
            return _LoadError(onRetry: reload);
          }
          return _DetailsBody(
            data: snapshot.data!,
            onCancelOrder: _handleCancelOrder,
          );
        },
      )));
}

class _DetailsBody extends StatelessWidget {
  const _DetailsBody({required this.data, required this.onCancelOrder});
  final OrderDetailsData data;
  final VoidCallback onCancelOrder;

  @override
  Widget build(BuildContext context) {
    final order = data.order;
    final refunds = data.payments
        .where((payment) => payment.paymentKind.toLowerCase() == 'refund')
        .fold<double>(0, (sum, payment) => sum + payment.amount.abs());
    final pieceCounts = <String, int>{};
    for (final item in data.items) {
      pieceCounts[item.pieceType] =
          (pieceCounts[item.pieceType] ?? 0) + item.quantity;
    }
    final pieceSummary = pieceCounts.entries
        .map((entry) => '${entry.value} ${entry.key}')
        .join(' | ');

    return ListView(
      padding: const EdgeInsets.all(12),
      children: [
        _SummaryCard(
          order: order,
          customer: data.customer,
          pieceCount: data.pieces.length,
          pieceSummary: pieceSummary,
          onCancelOrder: onCancelOrder,
          onOpenCustomer: () => AppNavigation.push(
            context,
            (_) => CustomerDetailsScreen(customerId: data.customer.id),
          ),
        ),
        const SizedBox(height: 10),
        _Section(
          title: 'القطع',
          icon: Icons.content_cut,
          initiallyExpanded: true,
          child: _PiecesView(data: data),
        ),
        const SizedBox(height: 10),
        _Section(
          title: 'التكلفة',
          icon: Icons.price_check_outlined,
          child: _CostTable(data: data),
        ),
        const SizedBox(height: 10),
        _Section(
          title: 'كشف الحساب الخاص بالطلب',
          icon: Icons.account_balance_outlined,
          child: _LedgerSection(
            order: order,
            refunds: refunds,
            ledgerEntries: data.ledgerEntries,
          ),
        ),
        const SizedBox(height: 10),
        _Section(
          title: 'الدفعات',
          icon: Icons.payments_outlined,
          initiallyExpanded: false,
          child: _PaymentsSection(payments: data.payments),
        ),
        const SizedBox(height: 10),
        _Section(
          title: 'المعاملات المالية',
          icon: Icons.swap_horiz_outlined,
          initiallyExpanded: false,
          child: _FinancialSection(items: data.financialTransactions),
        ),
      ],
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.order,
    required this.customer,
    required this.pieceCount,
    required this.pieceSummary,
    required this.onCancelOrder,
    required this.onOpenCustomer,
  });

  final OrderDetails order;
  final dynamic customer;
  final int pieceCount;
  final String pieceSummary;
  final VoidCallback onCancelOrder;
  final VoidCallback onOpenCustomer;

  @override
  Widget build(BuildContext context) {
    final statusText = _status(order.status);
    final effectiveCustomer = customer;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                  radius: 26,
                  child: Text('${order.id}'),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        order.number,
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${effectiveCustomer.name} • ${_text(effectiveCustomer.phoneNumber)}',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
                Chip(label: Text(statusText)),
              ],
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 10,
              runSpacing: 10,
              children: [
                _Metric(
                    'حالة الطلب', _status(order.status), Icons.info_outline),
                _Metric(
                  'تاريخ الاستلام',
                  order.deliveryDate == null
                      ? '-'
                      : _date.format(order.deliveryDate!),
                  Icons.calendar_today_outlined,
                ),
                _Metric('إجمالي الطلب', _money.format(order.totalAmount),
                    Icons.calculate_outlined),
                _Metric('المدفوع', _money.format(order.paidAmount),
                    Icons.payments_outlined),
                _Metric('المتبقي', _money.format(order.remainingAmount),
                    Icons.pending_actions_outlined,
                    emphasized: true),
                _Metric('عدد القطع', '$pieceCount', Icons.content_cut),
                _Metric('القطع', pieceSummary.isEmpty ? '-' : pieceSummary,
                    Icons.inventory_2_outlined),
                _Metric('تاريخ الطلب', _dateTime.format(order.orderDate),
                    Icons.calendar_today_outlined),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: FilledButton.icon(
                    onPressed: order.status.toLowerCase() == 'cancelled'
                        ? null
                        : onCancelOrder,
                    icon: const Icon(Icons.cancel_outlined),
                    label: const Text('إلغاء الطلب'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: onOpenCustomer,
                    icon: const Icon(Icons.person_outline),
                    label: const Text('تفاصيل العميل'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _PiecesView extends StatelessWidget {
  const _PiecesView({required this.data});
  final OrderDetailsData data;

  @override
  Widget build(BuildContext context) {
    if (data.pieces.isEmpty) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 8),
        child: Text('لا توجد قطع مرتبطة بهذا الطلب.'),
      );
    }

    final itemMap = {for (final item in data.items) item.id: item};
    return Column(
      children: data.pieces.map((piece) {
        final item = itemMap[piece.orderItemId];
        return _PieceCard(
          piece: piece,
          item: item,
          events: data.trackingEvents
              .where((event) => event.pieceId == piece.id)
              .toList(),
        );
      }).toList(),
    );
  }
}

class _PieceCard extends StatefulWidget {
  const _PieceCard({
    required this.piece,
    required this.item,
    required this.events,
  });

  final OrderPiece piece;
  final OrderItem? item;
  final List<OrderTrackingEvent> events;

  @override
  State<_PieceCard> createState() => _PieceCardState();
}

class _PieceCardState extends State<_PieceCard> {
  late Future<ProductionStageRoute> _routeFuture;
  bool _showMeasurements = false;

  @override
  void initState() {
    super.initState();
    _routeFuture = OrderRepository().getPieceRoute(widget.piece.id);
  }

  Future<void> _advanceStage(String requestedStage, String pieceType) async {
    final route = await _routeFuture;
    if (!mounted) return;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('تأكيد انتقال المرحلة'),
        content: Text(
          'القطعة: ${widget.piece.number}\nTrackingCode: ${widget.piece.trackingCode}\nالمرحلة الحالية: ${_stageText(route.currentStage)}\nالمرحلة الجديدة: ${_stageText(requestedStage)}',
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('إلغاء')),
          FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('تأكيد')),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      final result = await OrderRepository().advancePieceStage(
        pieceId: widget.piece.id,
        pieceType: pieceType,
        requestedStage: requestedStage,
        trackingCode: widget.piece.trackingCode,
        operationReference: 'ManualFromOrderDetails',
      );
      if (!mounted) return;
      setState(() {
        _routeFuture = Future.value(ProductionStageRoute(
          pieceType: pieceType,
          route: route.route,
          currentStage: result.newStatus,
          nextStage: result.nextStage,
        ));
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
            content: Text(result.message.isEmpty
                ? 'تم تحديث المرحلة بنجاح.'
                : result.message)),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر تحديث المرحلة: ${error.toString()}')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final measurements = item?.measurements ?? const {};
    final requestValues = _requestFieldValues(item);

    return FutureBuilder<ProductionStageRoute>(
      future: _routeFuture,
      builder: (context, snapshot) {
        final route = snapshot.data ??
            ProductionStageRoute(
              pieceType: item?.pieceType ?? '-',
              route: const [],
              currentStage: widget.piece.status,
              nextStage: null,
            );
        final stages = route.route.isEmpty ? const <String>[] : route.route;
        final currentIndex = stages.indexOf(route.currentStage);
        final nextAllowedStage = route.nextStage;
        final icons = <String, IconData>{
          'Printing': Icons.print_outlined,
          'FabricPrep': Icons.inventory_2_outlined,
          'Cutting': Icons.content_cut_outlined,
          'Sewing': Icons.design_services_outlined,
          'Buttons': Icons.checkroom_outlined,
          'Ironing': Icons.iron_outlined,
          'Quality': Icons.verified_outlined,
          'Assembly': Icons.precision_manufacturing_outlined,
          'Ready': Icons.done_all_outlined,
          'Delivery': Icons.local_shipping_outlined,
        };

        return Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: Padding(
            padding: const EdgeInsets.all(10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: [
                    CircleAvatar(
                        radius: 18, child: Text('${widget.piece.number}')),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(item?.pieceType ?? '-',
                              style: Theme.of(context).textTheme.titleSmall),
                          Text(widget.piece.trackingCode,
                              style: Theme.of(context).textTheme.bodySmall),
                        ],
                      ),
                    ),
                    IconButton(
                      tooltip: 'عرض بطاقة التشغيل',
                      onPressed: () => AppNavigation.push(
                          context,
                          (_) =>
                              WorkCardPreviewScreen(pieceId: widget.piece.id)),
                      icon: const Icon(Icons.open_in_new),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _DetailPill(
                        label: 'الحالة', value: _status(widget.piece.status)),
                    _DetailPill(
                        label: 'القماش', value: _text(item?.fabricType)),
                    _DetailPill(
                        label: 'اللون', value: _text(item?.fabricColor)),
                    _DetailPill(
                        label: 'الكتالوج', value: _text(item?.catalogNumber)),
                  ],
                ),
                const SizedBox(height: 8),
                if (requestValues.isNotEmpty) ...[
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color:
                          Theme.of(context).colorScheme.surfaceContainerHighest,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('الطلبات الخاصة',
                            style: TextStyle(fontWeight: FontWeight.bold)),
                        const SizedBox(height: 6),
                        ...requestValues.map((entry) => Padding(
                              padding: const EdgeInsets.only(bottom: 4),
                              child: Text(
                                  '${entry.label}: ${entry.value!.trim()}',
                                  style: Theme.of(context).textTheme.bodySmall),
                            )),
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                ] else
                  const Padding(
                    padding: EdgeInsets.only(bottom: 8),
                    child: Text('لا توجد طلبات خاصة'),
                  ),
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'الكمية المطلوبة: ${_text(item?.fabricCode)} • المستهلكة: ${item?.consumption == null ? 'غير محسوبة' : _money.format(item?.consumption ?? 0)}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                    TextButton(
                      onPressed: () => setState(
                          () => _showMeasurements = !_showMeasurements),
                      child: Text(
                          _showMeasurements ? 'إخفاء القياسات' : 'القياسات'),
                    ),
                  ],
                ),
                if (_showMeasurements && measurements.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: measurements.entries.map((entry) {
                      return Container(
                        width: 150,
                        padding: const EdgeInsets.all(8),
                        decoration: BoxDecoration(
                          border: Border.all(
                              color:
                                  Theme.of(context).colorScheme.outlineVariant),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(_measurementLabel(entry.key),
                                style: Theme.of(context).textTheme.labelSmall),
                            const SizedBox(height: 3),
                            Text(entry.value.toString(),
                                style: Theme.of(context).textTheme.bodyMedium),
                          ],
                        ),
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 8),
                ] else if (_showMeasurements) ...[
                  const Padding(
                    padding: EdgeInsets.only(bottom: 6),
                    child: Text('لا توجد قياسات محفوظة للقطعة.'),
                  ),
                ],
                if (stages.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: Row(
                      children: stages.asMap().entries.map((entry) {
                        final index = entry.key;
                        final stage = entry.value;
                        final isCompleted = currentIndex >= index;
                        final isNext = nextAllowedStage == stage;
                        final isPast = currentIndex > index;
                        final label = _stageText(stage);
                        final buttonEnabled =
                            isNext && item != null && item.pieceType.isNotEmpty;
                        final color = isCompleted
                            ? Colors.grey.shade300
                            : isNext
                                ? Colors.green.shade200
                                : isPast
                                    ? Colors.grey.shade100
                                    : Colors.grey.shade900;
                        final borderColor = isCompleted
                            ? Colors.grey.shade500
                            : isNext
                                ? Colors.green.shade700
                                : Colors.grey.shade700;
                        final textColor = isCompleted || isNext
                            ? Colors.black87
                            : Colors.white70;

                        return Padding(
                          padding: const EdgeInsets.only(left: 6),
                          child: InkWell(
                            onTap: buttonEnabled
                                ? () => _advanceStage(stage, item!.pieceType)
                                : null,
                            child: Container(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 8, vertical: 6),
                              decoration: BoxDecoration(
                                color: color,
                                border: Border.all(color: borderColor),
                                borderRadius: BorderRadius.circular(8),
                              ),
                              child: Row(
                                children: [
                                  Icon(
                                    icons[stage] ?? Icons.circle,
                                    size: 14,
                                    color: textColor,
                                  ),
                                  const SizedBox(width: 4),
                                  Text(
                                    isCompleted ? 'تم $label' : label,
                                    style: Theme.of(context)
                                        .textTheme
                                        .labelSmall
                                        ?.copyWith(
                                          color: textColor,
                                          fontWeight: isNext
                                              ? FontWeight.bold
                                              : FontWeight.normal,
                                        ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        );
                      }).toList(),
                    ),
                  ),
                ],
              ],
            ),
          ),
        );
      },
    );
  }
}

class _DetailPill extends StatelessWidget {
  const _DetailPill({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(20),
        ),
        child: Text('$label: $value',
            style: Theme.of(context).textTheme.bodySmall),
      );
}

class _CostTable extends StatelessWidget {
  const _CostTable({required this.data});
  final OrderDetailsData data;

  @override
  Widget build(BuildContext context) {
    final rows = data.items.map((item) {
      final pieceRow =
          data.pieces.where((piece) => piece.orderItemId == item.id).toList();
      final fabric = data.fabrics.firstWhere(
        (fabric) => fabric.orderItemId == item.id,
        orElse: () => OrderFabric(
          id: 0,
          orderItemId: item.id,
          inventoryItemId: null,
          fabricCode: null,
          fabricType: null,
          fabricColor: null,
          quantity: 0,
          unit: 'Meter',
          unitCost: 0,
          totalCost: 0,
          consumedQuantity: 0,
          yardPrice: null,
          createdDate: DateTime.now(),
        ),
      );
      final projectedCost =
          item.fullCost ?? fabric.consumedCost ?? fabric.totalCost;
      final pieceCount = pieceRow.length;
      return [
        _text(item.pieceType),
        pieceCount > 0 ? pieceRow.map((p) => p.trackingCode).join(' / ') : '-',
        _text(fabric.fabricCode),
        _text(fabric.fabricType),
        _text(fabric.fabricColor),
        projectedCost == 0 ? 'غير محسوبة' : _money.format(projectedCost),
        _money.format(fabric.consumedQuantity),
        _money.format(fabric.totalCost),
        item.fullCost == null ? 'غير محسوبة' : _money.format(item.fullCost!),
      ];
    }).toList();

    return _DataTable(
      columns: const [
        'نوع القطعة',
        'TrackingCode',
        'كود القماش',
        'نوع القماش',
        'اللون',
        'التكلفة التشغيلية',
        'المستهلك',
        'قيمة القماش',
        'إجمالي القطعة',
      ],
      rows: rows,
      empty: 'لا توجد تكلفة مفعلة للقطع في هذا الطلب.',
    );
  }
}

class _LedgerSection extends StatelessWidget {
  const _LedgerSection({
    required this.order,
    required this.refunds,
    required this.ledgerEntries,
  });

  final OrderDetails order;
  final double refunds;
  final List<OrderLedgerEntry> ledgerEntries;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _Metric('الإجمالي', _money.format(order.totalAmount),
                Icons.calculate_outlined),
            _Metric('الخصم', _money.format(order.discountAmount),
                Icons.discount_outlined),
            _Metric('المدفوع', _money.format(order.paidAmount),
                Icons.payments_outlined),
            _Metric(
                'المرتجعات', _money.format(refunds), Icons.currency_exchange),
            _Metric('المتبقي', _money.format(order.remainingAmount),
                Icons.pending_actions_outlined,
                emphasized: true),
          ],
        ),
        if (ledgerEntries.isNotEmpty) ...[
          const SizedBox(height: 8),
          _DataTable(
            columns: const [
              'المرجع',
              'مدين',
              'دائن',
              'الرصيد بعد الحركة',
              'التاريخ'
            ],
            rows: ledgerEntries
                .map((entry) => [
                      entry.referenceNumber,
                      _money.format(entry.debitAmount),
                      _money.format(entry.creditAmount),
                      _money.format(entry.balanceAfterTransaction),
                      _dateTime.format(entry.createdAt),
                    ])
                .toList(),
            empty: '',
          ),
        ] else
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('لا توجد حركات كشف حساب مرتبطة بهذا الطلب.'),
          ),
      ],
    );
  }
}

class _PaymentsSection extends StatelessWidget {
  const _PaymentsSection({required this.payments});
  final List<OrderPayment> payments;

  @override
  Widget build(BuildContext context) => _DataTable(
        columns: const [
          'المرجع',
          'النوع',
          'الطريقة',
          'المبلغ',
          'التاريخ',
          'الملاحظات'
        ],
        rows: payments
            .map((payment) => [
                  _text(payment.referenceNumber),
                  _paymentKind(payment.paymentKind),
                  _paymentMethod(payment.paymentMethod),
                  _money.format(payment.amount),
                  _dateTime.format(payment.paymentDate),
                  _text(payment.notes),
                ])
            .toList(),
        empty: 'لا توجد دفعات مرتبطة بهذا الطلب.',
      );
}

class _FinancialSection extends StatelessWidget {
  const _FinancialSection({required this.items});
  final List<OrderFinancialTransaction> items;

  @override
  Widget build(BuildContext context) => _DataTable(
        columns: const [
          'المرجع',
          'TransactionType',
          'المبلغ',
          'التاريخ',
          'الوصف'
        ],
        rows: items
            .map((transaction) => [
                  transaction.referenceNumber,
                  _transactionType(transaction.transactionType),
                  _money.format(transaction.amount),
                  _dateTime.format(transaction.createdAt),
                  _text(transaction.description),
                ])
            .toList(),
        empty: 'لا توجد معاملات مالية مرتبطة بهذا الطلب.',
      );
}

String _stageText(String stage) => switch (stage) {
      'Printing' => 'الطباعة',
      'FabricPrep' => 'تجهيز القماش',
      'Cutting' => 'القص',
      'Sewing' => 'الخياطة',
      'Buttons' => 'الأزرار',
      'Ironing' => 'الكي',
      'Quality' => 'الجودة',
      'Assembly' => 'التجميع',
      'Ready' => 'جاهز',
      'Delivery' => 'التسليم',
      _ => stage,
    };

class _MeasurementsView extends StatelessWidget {
  const _MeasurementsView({required this.items});
  final List<OrderItem> items;

  @override
  Widget build(BuildContext context) {
    final measured =
        items.where((item) => item.measurements.isNotEmpty).toList();
    if (measured.isEmpty) {
      return const Padding(
          padding: EdgeInsets.symmetric(vertical: 12),
          child: Text('لا توجد قياسات محفوظة في بنود الطلب.'));
    }
    return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: measured
            .map((item) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(item.pieceType,
                            style: Theme.of(context).textTheme.titleSmall),
                        const SizedBox(height: 8),
                        Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: item.measurements.entries
                                .map((entry) => Container(
                                      width: 150,
                                      padding: const EdgeInsets.all(10),
                                      decoration: BoxDecoration(
                                          border: Border.all(
                                              color: Theme.of(context)
                                                  .colorScheme
                                                  .outlineVariant),
                                          borderRadius:
                                              BorderRadius.circular(6)),
                                      child: Column(
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Text(_measurementLabel(entry.key),
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .labelMedium),
                                            const SizedBox(height: 3),
                                            Text(entry.value.toString(),
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .titleSmall)
                                          ]),
                                    ))
                                .toList()),
                      ]),
                ))
            .toList());
  }
}

class _ProductionItem extends StatelessWidget {
  const _ProductionItem({required this.item});
  final OrderItem item;

  @override
  Widget build(BuildContext context) {
    final fields = <(String, String)>[
      ('نوع القطعة', item.pieceType),
      ('رقم الكتالوج', _text(item.catalogNumber)),
      (
        'الاستهلاك المخزن',
        item.consumption == null ? '-' : _money.format(item.consumption)
      ),
      ('نوع القماش', _text(item.fabricType)),
      ('اللون', _text(item.fabricColor)),
      (
        'التكلفة الكاملة المخزنة',
        item.fullCost == null ? '-' : _money.format(item.fullCost)
      ),
      ...item.storedProductionFields.entries
          .where((entry) => entry.key != 'fullCost')
          .map(
              (entry) => (_productionField(entry.key), entry.value.toString())),
    ];
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
          border:
              Border.all(color: Theme.of(context).colorScheme.outlineVariant),
          borderRadius: BorderRadius.circular(8)),
      child: Wrap(
          spacing: 24,
          runSpacing: 10,
          children: fields
              .map((field) => _Value(field.$1, field.$2, width: 190))
              .toList()),
    );
  }

  String _productionField(String key) => switch (key) {
        'source' => 'المصدر',
        'productType' => 'نوع المنتج',
        'productCode' => 'كود المنتج',
        'quantity' => 'الكمية',
        'unitPrice' => 'سعر الوحدة',
        'lineTotal' => 'إجمالي البند',
        _ => key
      };
}

class _TrackingTimeline extends StatelessWidget {
  const _TrackingTimeline({required this.events});
  final List<OrderTrackingEvent> events;

  @override
  Widget build(BuildContext context) {
    if (events.isEmpty) {
      return const Padding(
          padding: EdgeInsets.symmetric(vertical: 12),
          child: Text('لا توجد أحداث تتبع مرتبطة بقطع هذا الطلب.'));
    }
    return Column(
        children: events
            .map((event) => ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(event.isReverted
                      ? Icons.undo
                      : Icons.radio_button_checked),
                  title:
                      Text('${_stage(event.stage)} - ${_status(event.status)}'),
                  subtitle: Text(
                      '${_dateTime.format(event.eventTime)}  •  القطعة ${event.pieceId ?? '-'}${event.employeeCode == null ? '' : '  •  الموظف ${event.employeeCode}'}${event.notes == null ? '' : '\n${event.notes}'}'),
                ))
            .toList());
  }
}

class _Section extends StatelessWidget {
  const _Section({
    required this.title,
    required this.icon,
    required this.child,
    this.initiallyExpanded = true,
  });
  final String title;
  final IconData icon;
  final Widget child;
  final bool initiallyExpanded;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Wrap(
              spacing: 10,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Icon(icon),
                Text(title, style: Theme.of(context).textTheme.titleMedium),
                if (!initiallyExpanded)
                  const Icon(Icons.keyboard_arrow_down, size: 18),
              ],
            ),
            const SizedBox(height: 10),
            child,
          ],
        ),
      ),
    );
  }
}

class _Metric extends StatelessWidget {
  const _Metric(this.label, this.value, this.icon, {this.emphasized = false});
  final String label;
  final String value;
  final IconData icon;
  final bool emphasized;

  @override
  Widget build(BuildContext context) => Container(
        width: 205,
        height: 78,
        padding: const EdgeInsets.all(11),
        decoration: BoxDecoration(
            color: emphasized
                ? Theme.of(context).colorScheme.primaryContainer
                : Theme.of(context).colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(8)),
        child: Row(children: [
          Icon(icon),
          const SizedBox(width: 9),
          Expanded(
              child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                Text(label, maxLines: 1, overflow: TextOverflow.ellipsis),
                Text(value,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context)
                        .textTheme
                        .titleMedium
                        ?.copyWith(fontWeight: FontWeight.bold))
              ]))
        ]),
      );
}

class _Value extends StatelessWidget {
  const _Value(this.label, this.value, {this.width = 220});
  final String label;
  final String value;
  final double width;

  @override
  Widget build(BuildContext context) => SizedBox(
      width: width,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: Theme.of(context).textTheme.labelMedium),
        const SizedBox(height: 3),
        SelectableText(value, style: Theme.of(context).textTheme.titleSmall)
      ]));
}

class _DispositionCandidate {
  const _DispositionCandidate({
    required this.id,
    required this.number,
    required this.trackingCode,
    required this.status,
  });

  final int id;
  final int number;
  final String? trackingCode;
  final String status;
}

class _DataTable extends StatelessWidget {
  const _DataTable(
      {required this.columns, required this.rows, required this.empty});
  final List<String> columns;
  final List<List<String>> rows;
  final String empty;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Text(empty));
    }
    return SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: columns
              .map((value) => DataColumn(
                  label: Text(value,
                      style: const TextStyle(fontWeight: FontWeight.bold))))
              .toList(),
          rows: rows
              .map((row) => DataRow(
                  cells: row
                      .map((value) => DataCell(SelectableText(value)))
                      .toList()))
              .toList(),
        ));
  }
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.onRetry});
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 42),
        const SizedBox(height: 10),
        const Text('تعذر تحميل تفاصيل الطلب.'),
        const SizedBox(height: 10),
        FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة')),
      ]));
}
