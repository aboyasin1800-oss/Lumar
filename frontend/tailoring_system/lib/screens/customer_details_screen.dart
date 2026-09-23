import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../models/customer_models.dart';
import '../repositories/customer_repository.dart';

final _customerMoney = NumberFormat('#,##0.00');
final _customerDate = DateFormat('yyyy/MM/dd');

String _customerText(String? value) =>
    value == null || value.trim().isEmpty ? '-' : value;
String _paymentMethod(String? value) => switch (value?.toLowerCase()) {
      'cash' || 'كاش' => 'نقدي',
      'credit' => 'آجل',
      'refund' => 'استرداد',
      _ => _customerText(value),
    };

String _paymentKind(String value) => switch (value.toLowerCase()) {
      'advance' => 'دفعة',
      'debtcollection' => 'تحصيل',
      'refund' => 'استرداد',
      _ => value,
    };

String _orderKind(String value) => switch (value) {
      'ReadyMadeSale' => 'بيع جاهز',
      'TailoringOrder' => 'طلب تفصيل',
      _ => value,
    };

class CustomerDetailsScreen extends StatefulWidget {
  const CustomerDetailsScreen({required this.customerId, super.key});
  final int customerId;

  @override
  State<CustomerDetailsScreen> createState() => _CustomerDetailsScreenState();
}

class _CustomerDetailsScreenState extends State<CustomerDetailsScreen> {
  final repository = CustomerRepository();
  late Future<CustomerDetailsData> future;

  @override
  void initState() {
    super.initState();
    future = repository.getDetails(widget.customerId);
  }

  void reload() =>
      setState(() => future = repository.getDetails(widget.customerId));

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('تفاصيل العميل'),
          actions: [
            IconButton(
                tooltip: 'تحديث',
                onPressed: reload,
                icon: const Icon(Icons.refresh))
          ],
        ),
        body: SafeArea(
            child: FutureBuilder<CustomerDetailsData>(
          future: future,
          builder: (context, snapshot) {
                        if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
                        }
                        if (snapshot.hasError || snapshot.data == null) {
              return _CustomerError(onRetry: reload);
                        }
            final data = snapshot.data!;
            final customer = data.customer;
            final totalValue = data.orders
                .fold<double>(0, (sum, order) => sum + order.totalAmount);
            final totalPaid = data.orders
                .fold<double>(0, (sum, order) => sum + order.paidAmount);
            final totalRemaining = data.orders
                .fold<double>(0, (sum, order) => sum + order.remainingAmount);
            final totalRefunds = data.payments
                .where(
                    (payment) => payment.paymentKind.toLowerCase() == 'refund')
                .fold<double>(0, (sum, payment) => sum + payment.amount.abs());
            final totalDiscounts = data.orders
                .fold<double>(0, (sum, order) => sum + order.discountAmount);
            final totalCancellations = data.financialTransactions
                .where((transaction) =>
                    transaction.transactionType == 'OrderCancellationRefund')
                .fold<double>(
                    0, (sum, transaction) => sum + transaction.amount.abs());
            final activities = _activities(data);
            return ListView(padding: const EdgeInsets.all(16), children: [
              Card(
                  child: Padding(
                      padding: const EdgeInsets.all(18),
                      child: Wrap(
                          spacing: 18,
                          runSpacing: 14,
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            CircleAvatar(
                                radius: 28,
                                child: Text(_customerText(customer.name)
                                    .characters
                                    .first)),
                            SizedBox(
                                width: 360,
                                child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(_customerText(customer.name),
                                          style: Theme.of(context)
                                              .textTheme
                                              .titleLarge),
                                      const SizedBox(height: 4),
                                      Text(
                                          '${_customerText(customer.code)}  •  ${_customerText(customer.phoneNumber)}'),
                                    ])),
                            Chip(
                                avatar: Icon(
                                    customer.isActive
                                        ? Icons.check_circle_outline
                                        : Icons.pause_circle_outline,
                                    size: 17),
                                label: Text(
                                    customer.isActive ? 'نشط' : 'غير نشط')),
                          ]))),
              const SizedBox(height: 12),
              _CustomerSection(
                  title: 'البيانات الأساسية',
                  icon: Icons.person_outline,
                  child: Wrap(spacing: 28, runSpacing: 14, children: [
                    _CustomerValue('المعرف', '${customer.id}'),
                    _CustomerValue('كود العميل', _customerText(customer.code)),
                    _CustomerValue('الاسم', _customerText(customer.name)),
                    _CustomerValue(
                        'الهاتف', _customerText(customer.phoneNumber)),
                    _CustomerValue('العنوان', _customerText(customer.address),
                        width: 320),
                    _CustomerValue('نوع العلاقة',
                        _customerText(customer.relationshipType)),
                    _CustomerValue(
                        'العميل المحيل',
                        _customerText(customer.parentCustomerName ??
                            customer.parentCustomerCode)),
                    const _UnavailableValue('تاريخ الإنشاء',
                        'لا يوجد حقل تاريخ إنشاء في جدول أو DTO العميل الحالي.'),
                    _CustomerValue('ملاحظات', _customerText(customer.notes),
                        width: 440),
                  ])),
              const SizedBox(height: 12),
              _CustomerSection(
                  title: 'برنامج الولاء والإحالة',
                  icon: Icons.workspace_premium_outlined,
                  child: Wrap(spacing: 10, runSpacing: 10, children: [
                    _Metric(
                        'الرصيد الحالي',
                        _customerMoney.format(data.loyalty?.currentPoints ??
                            customer.totalPoints ??
                            0),
                        Icons.stars_outlined),
                    _Metric(
                        'إجمالي المكتسب',
                        data.loyalty == null
                            ? '-'
                            : _customerMoney
                                .format(data.loyalty!.lifetimeEarnedPoints),
                        Icons.trending_up),
                    _Metric(
                        'إجمالي المستخدم',
                        data.loyalty == null
                            ? '-'
                            : _customerMoney
                                .format(data.loyalty!.lifetimeRedeemedPoints),
                        Icons.redeem_outlined),
                    _Metric(
                        'عملاء شجرة الإحالة',
                        data.referral?.totalReferrals.toString() ?? '-',
                        Icons.account_tree_outlined),
                    _Metric(
                        'إجمالي القطع',
                        customer.totalPieces?.toString() ?? '-',
                        Icons.content_cut),
                    _Metric(
                        'إجمالي الديون',
                        customer.totalDebts == null
                            ? '-'
                            : _customerMoney.format(customer.totalDebts),
                        Icons.account_balance_wallet_outlined),
                  ])),
              const SizedBox(height: 12),
              _CustomerSection(
                  title: 'كشف الحساب الموحد',
                  icon: Icons.receipt_long_outlined,
                  action: Wrap(spacing: 8, runSpacing: 8, children: const [
                    _DisabledCustomerAction('إضافة دفعة', Icons.add_card,
                        'لا توجد خدمة حالية لإضافة دفعة إلى طلب قائم.'),
                    _DisabledCustomerAction(
                        'خصم أو إعفاء دين',
                        Icons.money_off_outlined,
                        'لا توجد خدمة حالية لإنشاء خصم أو إعفاء دين.'),
                  ]),
                  child: Wrap(spacing: 10, runSpacing: 10, children: [
                    _Metric('إجمالي القيمة', _customerMoney.format(totalValue),
                        Icons.calculate_outlined),
                    _Metric('إجمالي المدفوع', _customerMoney.format(totalPaid),
                        Icons.payments_outlined),
                    _Metric(
                        'إجمالي المتبقي',
                        _customerMoney.format(totalRemaining),
                        Icons.pending_actions_outlined),
                    _Metric(
                        'إجمالي المرتجعات',
                        _customerMoney.format(totalRefunds),
                        Icons.currency_exchange),
                    _Metric(
                        'إجمالي الخصومات',
                        _customerMoney.format(totalDiscounts),
                        Icons.discount_outlined),
                    _Metric(
                        'إجمالي الإلغاءات',
                        _customerMoney.format(totalCancellations),
                        Icons.cancel_outlined),
                    _Metric(
                        'إجمالي رصيد الولاء',
                        _customerMoney.format(data.loyalty?.currentPoints ??
                            customer.totalPoints ??
                            0),
                        Icons.loyalty_outlined),
                    _Metric(
                        'الرصيد النهائي المخزن',
                        customer.totalDebts == null
                            ? '-'
                            : _customerMoney.format(customer.totalDebts),
                        Icons.account_balance_outlined,
                        emphasized: true),
                  ])),
              const SizedBox(height: 12),
              _CustomerSection(
                  title: 'أحدث العمليات',
                  icon: Icons.history,
                  child: _CustomerActivities(items: activities)),
              const SizedBox(height: 12),
              _CustomerSection(
                  title: 'كشف الأستاذ',
                  icon: Icons.menu_book_outlined,
                  child: _CustomerTable(
                    columns: const [
                      'المرجع',
                      'مدين',
                      'دائن',
                      'الرصيد بعد الحركة',
                      'التاريخ'
                    ],
                    rows: data.ledger
                        .map((item) => [
                              item.referenceNumber,
                              _customerMoney.format(item.debitAmount),
                              _customerMoney.format(item.creditAmount),
                              _customerMoney
                                  .format(item.balanceAfterTransaction),
                              _customerDate.format(item.createdAt)
                            ])
                        .toList(),
                    empty: 'لا توجد حركات أستاذ لهذا العميل.',
                  )),
            ]);
          },
        )),
      );

  List<_CustomerActivity> _activities(CustomerDetailsData data) {
    final activities = <_CustomerActivity>[
      ...data.orders.map((order) => _CustomerActivity(
          reference: order.number,
          type: _orderKind(order.saleCategory),
          amount: order.totalAmount,
          date: order.orderDate)),
      ...data.payments.map((payment) => _CustomerActivity(
          reference: _customerText(payment.referenceNumber),
          type:
              '${_paymentKind(payment.paymentKind)} - ${_paymentMethod(payment.paymentMethod)}',
          amount: payment.amount,
          date: payment.paymentDate)),
      ...data.financialTransactions
          .where((transaction) =>
              transaction.transactionType == 'CustomerWriteOff')
          .map((transaction) => _CustomerActivity(
              reference: transaction.referenceNumber,
              type: 'خصم أو إعفاء دين',
              amount: transaction.amount,
              date: transaction.createdAt)),
      ...data.financialTransactions
          .where((transaction) =>
              transaction.transactionType == 'OrderCancellationRefund')
          .map((transaction) => _CustomerActivity(
              reference: transaction.referenceNumber,
              type: 'إلغاء',
              amount: transaction.amount,
              date: transaction.createdAt)),
      ...data.ledger
          .where((entry) => entry.referenceNumber.startsWith('LoyaltyCredit:'))
          .map((entry) => _CustomerActivity(
              reference: _loyaltyCreditOrder(entry.referenceNumber),
              type: 'رصيد ولاء',
              amount: entry.creditAmount,
              date: entry.createdAt)),
    ];
    activities.sort((left, right) => right.date.compareTo(left.date));
    return activities;
  }
}

String _loyaltyCreditOrder(String reference) {
    final parts = reference.split(':');
    return parts.length > 2 ? parts[2] : reference;
}

class _CustomerActivity {
  const _CustomerActivity(
      {required this.reference,
      required this.type,
      required this.amount,
      required this.date});
  final String reference;
  final String type;
  final double amount;
  final DateTime date;
}

class _CustomerActivities extends StatelessWidget {
  const _CustomerActivities({required this.items});
  final List<_CustomerActivity> items;

  @override
  Widget build(BuildContext context) {
        if (items.isEmpty) {
      return const Padding(
          padding: EdgeInsets.symmetric(vertical: 12),
          child: Text('لا توجد عمليات مرتبطة بالعميل في المصادر الحالية.'));
        }
    final visible = items.take(30).toList();
    return LayoutBuilder(builder: (context, constraints) {
      if (constraints.maxWidth >= 720) {
        return _CustomerTable(
          columns: const ['المرجع', 'النوع', 'المبلغ', 'التاريخ'],
          rows: visible
              .map((item) => [
                    item.reference,
                    item.type,
                    _customerMoney.format(item.amount),
                    _customerDate.format(item.date)
                  ])
              .toList(),
          empty: '',
        );
      }
      return Column(
          children: visible
              .map((item) => ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.swap_horiz_outlined),
                    title: Text(item.type),
                    subtitle: Text(
                        '${item.reference}  •  ${_customerDate.format(item.date)}'),
                    trailing: Text(_customerMoney.format(item.amount)),
                  ))
              .toList());
    });
  }
}

class _CustomerSection extends StatelessWidget {
  const _CustomerSection(
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

class _CustomerValue extends StatelessWidget {
  const _CustomerValue(this.label, this.value, {this.width = 220});
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

class _UnavailableValue extends StatelessWidget {
  const _UnavailableValue(this.label, this.reason);
  final String label;
  final String reason;

  @override
  Widget build(BuildContext context) => Tooltip(
      message: reason,
      child: const SizedBox(
          width: 220,
          child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('تاريخ الإنشاء'),
                SizedBox(height: 3),
                Text('غير متاح')
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
                    style: Theme.of(context)
                        .textTheme
                        .titleMedium
                        ?.copyWith(fontWeight: FontWeight.bold))
              ]))
        ]),
      );
}

class _DisabledCustomerAction extends StatelessWidget {
  const _DisabledCustomerAction(this.label, this.icon, this.reason);
  final String label;
  final IconData icon;
  final String reason;

  @override
  Widget build(BuildContext context) => Tooltip(
      message: reason,
      child: OutlinedButton.icon(
          onPressed: null, icon: Icon(icon), label: Text(label)));
}

class _CustomerTable extends StatelessWidget {
  const _CustomerTable(
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
        child: ConstrainedBox(
          constraints: BoxConstraints(
              minWidth: math.min(MediaQuery.sizeOf(context).width - 72, 920)),
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
          ),
        ));
  }
}

class _CustomerError extends StatelessWidget {
  const _CustomerError({required this.onRetry});
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 42),
        const SizedBox(height: 10),
        const Text('تعذر تحميل تفاصيل العميل.'),
        const SizedBox(height: 10),
        FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة')),
      ]));
}
