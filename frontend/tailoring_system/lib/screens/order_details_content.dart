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
String _urgency(String value) => switch (value.toLowerCase()) {
      'normal' => 'عادي',
      'medium' => 'متوسط',
      'urgent' => 'عاجل',
      _ => value
    };
String _category(String value) => switch (value) {
      'TailoringOrder' => 'طلب تفصيل',
      'ReadyMadeSale' => 'بيع جاهز',
      _ => value
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
String _unit(String value) => switch (value.toLowerCase()) {
      'yard' => 'ياردة',
      'meter' => 'متر',
      'piece' => 'قطعة',
      _ => value
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

  @override
  void initState() {
    super.initState();
    future = repository.getDetails(widget.orderId);
  }

  void reload() =>
      setState(() => future = repository.getDetails(widget.orderId));

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
            return _DetailsBody(data: snapshot.data!);
          },
        )),
      );
}

class _DetailsBody extends StatelessWidget {
  const _DetailsBody({required this.data});
  final OrderDetailsData data;

  @override
  Widget build(BuildContext context) {
    final order = data.order;
    final refunds = data.payments
        .where((payment) => payment.paymentKind.toLowerCase() == 'refund')
        .fold<double>(0, (sum, payment) => sum + payment.amount.abs());
    final storedFullCosts =
        data.items.map((item) => item.fullCost).whereType<double>().toList();
    final fabricCosts = data.fabrics
        .map((fabric) => fabric.consumedCost)
        .whereType<double>()
        .toList();
    return ListView(padding: const EdgeInsets.all(16), children: [
      Card(
          child: Padding(
              padding: const EdgeInsets.all(18),
              child: Wrap(
                  spacing: 18,
                  runSpacing: 14,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    CircleAvatar(radius: 28, child: Text('${order.id}')),
                    SizedBox(
                        width: 420,
                        child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(order.number,
                                  style:
                                      Theme.of(context).textTheme.titleLarge),
                              const SizedBox(height: 4),
                              Text(
                                  '${_category(order.saleCategory)}  •  ${_date.format(order.orderDate)}')
                            ])),
                    Chip(label: Text(_status(order.status))),
                    Chip(
                        avatar: const Icon(Icons.flag_outlined, size: 17),
                        label: Text(_urgency(order.urgencyStatus))),
                  ]))),
      const SizedBox(height: 12),
      _Section(
          title: 'ملخص الطلب',
          icon: Icons.receipt_long_outlined,
          child: Wrap(spacing: 10, runSpacing: 10, children: [
            _Metric('إجمالي الطلب', _money.format(order.totalAmount),
                Icons.calculate_outlined),
            _Metric('المدفوع', _money.format(order.paidAmount),
                Icons.payments_outlined),
            _Metric('المتبقي', _money.format(order.remainingAmount),
                Icons.pending_actions_outlined,
                emphasized: true),
            _Metric('عدد العناصر', '${data.items.length}',
                Icons.view_list_outlined),
            _Metric('عدد القطع', '${data.pieces.length}', Icons.content_cut),
            _Metric('أحداث التتبع', '${data.trackingEvents.length}',
                Icons.route_outlined),
            _Metric('الحالة', _status(order.status), Icons.info_outline),
            _Metric(
                'الأولوية', _urgency(order.urgencyStatus), Icons.flag_outlined),
            _Metric('تاريخ الطلب', _date.format(order.orderDate),
                Icons.calendar_today_outlined),
            _Metric(
                'تاريخ التسليم',
                order.deliveryDate == null
                    ? '-'
                    : _date.format(order.deliveryDate!),
                Icons.local_shipping_outlined),
          ])),
      const SizedBox(height: 12),
      const _Section(
          title: 'العمليات',
          icon: Icons.tune_outlined,
          child: Wrap(spacing: 8, runSpacing: 8, children: [
            _DisabledAction('تحديث الحالة', Icons.sync_alt,
                'خدمة تحديث الطلب الحالية ترجع 405.'),
            _DisabledAction('إلغاء الطلب', Icons.cancel_outlined,
                'لا توجد خدمة إلغاء طلب حالية.'),
            _DisabledAction('إعادة التفعيل', Icons.restore_outlined,
                'لا توجد خدمة إعادة تفعيل طلب حالية.'),
            _DisabledAction('طباعة الطلب', Icons.print_outlined,
                'لا توجد خدمة طباعة فعلية للطلب.'),
            _DisabledAction('طباعة الفاتورة', Icons.receipt_outlined,
                'لا توجد خدمة طباعة فاتورة فعلية.'),
          ])),
      const SizedBox(height: 12),
      _Section(
          title: 'العميل',
          icon: Icons.person_outline,
          action: FilledButton.icon(
              onPressed: () => AppNavigation.push(context,
                  (_) => CustomerDetailsScreen(customerId: data.customer.id)),
              icon: const Icon(Icons.open_in_new),
              label: const Text('تفاصيل العميل')),
          child: Wrap(spacing: 28, runSpacing: 14, children: [
            _Value('كود العميل', _text(data.customer.code)),
            _Value('الاسم', _text(data.customer.name)),
            _Value('الهاتف', _text(data.customer.phoneNumber)),
          ])),
      const SizedBox(height: 12),
      _Section(
          title: 'معلومات الطلب',
          icon: Icons.notes_outlined,
          child: Wrap(spacing: 28, runSpacing: 14, children: [
            _Value('الحالة', _status(order.status)),
            _Value('الأولوية', _urgency(order.urgencyStatus)),
            _Value('الفئة', _category(order.saleCategory)),
            _Value('تاريخ الإنشاء', _dateTime.format(order.createdDate)),
            _Value('ملاحظات', _text(order.notes), width: 420),
            if (order.cancellationReason != null)
              _Value('سبب الإلغاء', _text(order.cancellationReason),
                  width: 320),
          ])),
      const SizedBox(height: 12),
      _Section(
          title: 'القطع',
          icon: Icons.content_cut,
          child: _PiecesView(data: data)),
      const SizedBox(height: 12),
      _Section(
          title: 'الأقمشة',
          icon: Icons.texture_outlined,
          child: _DataTable(
            columns: const [
              'كود القماش',
              'النوع',
              'اللون',
              'الكمية المطلوبة',
              'الكمية المستهلكة',
              'سعر الياردة الفعلي',
              'تكلفة القماش'
            ],
            rows: data.fabrics
                .map((fabric) => [
                      _text(fabric.fabricCode),
                      _text(fabric.fabricType),
                      _text(fabric.fabricColor),
                      '${_money.format(fabric.quantity)} ${_unit(fabric.unit)}',
                      '${_money.format(fabric.consumedQuantity)} ${_unit(fabric.unit)}',
                      fabric.yardPrice == null
                          ? 'غير متاح'
                          : _money.format(fabric.yardPrice),
                      fabric.consumedCost == null
                          ? 'غير متاح'
                          : _money.format(fabric.consumedCost),
                    ])
                .toList(),
            empty: 'لا توجد سجلات أقمشة مرتبطة بهذا الطلب.',
          )),
      const SizedBox(height: 12),
      _Section(
          title: 'القياسات',
          icon: Icons.straighten_outlined,
          child: _MeasurementsView(items: data.items)),
      const SizedBox(height: 12),
      _Section(
          title: 'بيانات الإنتاج والتكلفة المخزنة',
          icon: Icons.precision_manufacturing_outlined,
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Wrap(spacing: 10, runSpacing: 10, children: [
              _Metric(
                  'تكلفة القماش المحسوبة',
                  fabricCosts.isEmpty
                      ? 'غير متاح'
                      : _money.format(fabricCosts.fold<double>(
                          0, (sum, value) => sum + value)),
                  Icons.texture_outlined),
              _Metric(
                  'التكلفة الكاملة المخزنة',
                  storedFullCosts.isEmpty
                      ? 'غير متاح'
                      : _money.format(storedFullCosts.fold<double>(
                          0, (sum, value) => sum + value)),
                  Icons.price_check_outlined),
            ]),
            const SizedBox(height: 10),
            ...data.items.map((item) => _ProductionItem(item: item)),
          ])),
      const SizedBox(height: 12),
      _Section(
          title: 'أحداث التتبع',
          icon: Icons.route_outlined,
          child: _TrackingTimeline(events: data.trackingEvents)),
      const SizedBox(height: 12),
      _Section(
          title: 'المدفوعات',
          icon: Icons.payments_outlined,
          child: _DataTable(
            columns: const [
              'المرجع',
              'النوع',
              'الطريقة',
              'المبلغ',
              'التاريخ',
              'الملاحظات'
            ],
            rows: data.payments
                .map((payment) => [
                      _text(payment.referenceNumber),
                      _paymentKind(payment.paymentKind),
                      _paymentMethod(payment.paymentMethod),
                      _money.format(payment.amount),
                      _dateTime.format(payment.paymentDate),
                      _text(payment.notes)
                    ])
                .toList(),
            empty: 'لا توجد مدفوعات مرتبطة بهذا الطلب.',
          )),
      const SizedBox(height: 12),
      _Section(
          title: 'كشف الحساب الخاص بالطلب',
          icon: Icons.account_balance_outlined,
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Wrap(spacing: 10, runSpacing: 10, children: [
              _Metric('الإجمالي', _money.format(order.totalAmount),
                  Icons.calculate_outlined),
              _Metric('المدفوع', _money.format(order.paidAmount),
                  Icons.payments_outlined),
              _Metric('المتبقي', _money.format(order.remainingAmount),
                  Icons.pending_actions_outlined),
              _Metric('الخصومات', _money.format(order.discountAmount),
                  Icons.discount_outlined),
              _Metric(
                  'المرتجعات', _money.format(refunds), Icons.currency_exchange),
              _Metric(
                  'الرصيد النهائي المخزن',
                  _money.format(order.remainingAmount),
                  Icons.account_balance_wallet_outlined,
                  emphasized: true),
            ]),
            if (data.ledgerEntries.isNotEmpty) ...[
              const SizedBox(height: 10),
              _DataTable(
                  columns: const [
                    'المرجع',
                    'مدين',
                    'دائن',
                    'الرصيد بعد الحركة',
                    'التاريخ'
                  ],
                  rows: data.ledgerEntries
                      .map((entry) => [
                            entry.referenceNumber,
                            _money.format(entry.debitAmount),
                            _money.format(entry.creditAmount),
                            _money.format(entry.balanceAfterTransaction),
                            _dateTime.format(entry.createdAt)
                          ])
                      .toList(),
                  empty: ''),
            ],
          ])),
      const SizedBox(height: 12),
      _Section(
          title: 'المعاملات المالية',
          icon: Icons.swap_horiz_outlined,
          child: _DataTable(
            columns: const ['المرجع', 'النوع', 'المبلغ', 'التاريخ', 'الوصف'],
            rows: data.financialTransactions
                .map((transaction) => [
                      transaction.referenceNumber,
                      _transactionType(transaction.transactionType),
                      _money.format(transaction.amount),
                      _dateTime.format(transaction.createdAt),
                      _text(transaction.description)
                    ])
                .toList(),
            empty: 'لا توجد معاملات مالية مرتبطة بمرجع هذا الطلب.',
          )),
    ]);
  }
}

class _PiecesView extends StatelessWidget {
  const _PiecesView({required this.data});
  final OrderDetailsData data;

  String? _size(OrderItem? item) {
    if (item == null) return null;
    for (final entry in item.measurements.entries) {
      if (_measurementLabel(entry.key) == 'المقاس') {
        return entry.value.toString();
      }
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    if (data.pieces.isEmpty) {
      return const Padding(
          padding: EdgeInsets.symmetric(vertical: 12),
          child: Text('لا توجد قطع مرتبطة بهذا الطلب.'));
    }
    final items = {for (final item in data.items) item.id: item};
    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth < 720 ? constraints.maxWidth : 330.0;
      return Wrap(
          spacing: 10,
          runSpacing: 10,
          children: data.pieces.map((piece) {
            final item = items[piece.orderItemId];
            final cost =
                item != null && item.quantity == 1 && item.fullCost != null
                    ? _money.format(item.fullCost)
                    : 'غير محددة للقطعة';
            return SizedBox(
                width: width,
                child: Material(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(8),
                  clipBehavior: Clip.antiAlias,
                  child: InkWell(
                      onTap: () => AppNavigation.push(context,
                          (_) => WorkCardPreviewScreen(pieceId: piece.id)),
                      child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Row(children: [
                                  CircleAvatar(child: Text('${piece.number}')),
                                  const SizedBox(width: 10),
                                  Expanded(
                                      child: Text(item?.pieceType ?? '-',
                                          style: Theme.of(context)
                                              .textTheme
                                              .titleMedium)),
                                  const Icon(Icons.chevron_left)
                                ]),
                                const SizedBox(height: 10),
                                Text('اللون: ${_text(item?.fabricColor)}'),
                                Text('المقاس: ${_size(item) ?? '-'}'),
                                Text('الحالة: ${_status(piece.status)}'),
                                Text('التكلفة: $cost'),
                                Text('التتبع: ${piece.trackingCode}',
                                    style:
                                        Theme.of(context).textTheme.bodySmall),
                              ]))),
                ));
          }).toList());
    });
  }
}

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
  const _Section(
      {required this.title,
      required this.icon,
      required this.child,
      this.action});
  final String title;
  final IconData icon;
  final Widget child;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Card(
      child: Padding(
          padding: const EdgeInsets.all(16),
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Wrap(
                spacing: 10,
                runSpacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Icon(icon),
                  Text(title, style: Theme.of(context).textTheme.titleMedium),
                  if (action != null) action!
                ]),
            const SizedBox(height: 12),
            child,
          ])));
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

class _DisabledAction extends StatelessWidget {
  const _DisabledAction(this.label, this.icon, this.reason);
  final String label;
  final IconData icon;
  final String reason;

  @override
  Widget build(BuildContext context) => Tooltip(
      message: reason,
      child: OutlinedButton.icon(
          onPressed: null, icon: Icon(icon), label: Text(label)));
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
