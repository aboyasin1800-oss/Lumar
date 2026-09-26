import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../core/app_message.dart';
import '../../core/ui_palette.dart';
import '../../models/finance_models.dart';
import '../../models/order_models.dart';
import '../../repositories/order_repository.dart';
import '../../widgets/cash_account_picker.dart';

class OrderDeliveryScreen extends StatefulWidget {
  const OrderDeliveryScreen({
    required this.orderId,
    this.repository,
    this.cashAccountsFuture,
    super.key,
  });

  final int orderId;
  final OrderRepository? repository;
  final Future<List<CashAccount>>? cashAccountsFuture;

  @override
  State<OrderDeliveryScreen> createState() => _OrderDeliveryScreenState();
}

class _OrderDeliveryScreenState extends State<OrderDeliveryScreen> {
  late final OrderRepository _repository =
      widget.repository ?? OrderRepository();
  late Future<OrderDetailsData> _future;
  final TextEditingController _amountController = TextEditingController();
  final TextEditingController _discountController = TextEditingController();
  final TextEditingController _referenceController = TextEditingController();
  bool _submitting = false;
  bool _delivering = false;
  bool _waivingBalance = false;
  bool _recognizingRevenue = false;
  bool _partialCollectionSelected = false;
  bool _initialized = false;
  int? _cashAccountId;
  CashAccountAvailability _cashAccountAvailability =
      CashAccountAvailability.loading;

  @override
  void initState() {
    super.initState();
    _future = _repository.getDetails(widget.orderId);
  }

  @override
  void dispose() {
    _amountController.dispose();
    _discountController.dispose();
    _referenceController.dispose();
    super.dispose();
  }

  void _reload() {
    if (!mounted) return;
    setState(() {
      _initialized = false;
      _amountController.clear();
      _discountController.clear();
      _referenceController.clear();
      _partialCollectionSelected = false;
      _cashAccountId = null;
      _cashAccountAvailability = CashAccountAvailability.loading;
      _future = _repository.getDetails(widget.orderId);
    });
  }

  void _seedFields(OrderDetails order) {
    if (_initialized) return;
    final suggested = order.remainingAmount;
    _amountController.text = suggested.toStringAsFixed(2);
    _discountController.text = '0.00';
    _referenceController.text =
        'RCPT-${order.number}-${DateTime.now().millisecondsSinceEpoch}';
    _initialized = true;
  }

  Future<void> _deliverOrder(OrderDetails order) async {
    if (_delivering || !mounted) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('تأكيد التسليم'),
        content: const Text(
          'سيتم تسجيل الطلب كتم التسليم ونقله إلى أرشيف التسليم. هل تريد المتابعة؟',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('تأكيد التسليم'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _delivering = true);
    try {
      await _repository.deliverOrder(order.id);
      if (!mounted) return;
      _showMessage('تم التسليم وإثبات الإيراد ونقل الطلب إلى الأرشيف.',
          isSuccess: true);
      _reload();
    } catch (error) {
      if (!mounted) return;
      _showMessage('تعذر تأكيد التسليم: $error');
    } finally {
      if (mounted) setState(() => _delivering = false);
    }
  }

  Future<void> _recognizeRevenue(OrderDetails order) async {
    if (_recognizingRevenue || !mounted) return;
    setState(() => _recognizingRevenue = true);
    try {
      await _repository.recognizeDeliveryRevenue(order.id);
      if (!mounted) return;
      _showMessage('تم إثبات الإيراد بنجاح.', isSuccess: true);
      _reload();
    } catch (error) {
      if (!mounted) return;
      _showMessage('تعذر إثبات الإيراد: $error');
    } finally {
      if (mounted) {
        setState(() => _recognizingRevenue = false);
      }
    }
  }

  Future<void> _submitPayment(OrderDetailsData data) async {
    if (_submitting || !mounted) return;

    final amount = double.tryParse(_amountController.text.trim());
    final discount = double.tryParse(_discountController.text.trim());
    if (amount == null || discount == null || amount < 0 || discount < 0) {
      _showMessage('قيمة المبلغ غير صحيحة.');
      return;
    }

    if (amount <= 0 && discount <= 0) {
      _showMessage('يجب إدخال مبلغ تحصيل أو خصم أكبر من الصفر.');
      return;
    }
    if (amount > 0 && _cashAccountId == null) {
      _showMessage(switch (_cashAccountAvailability) {
        CashAccountAvailability.failed =>
          'تعذر تحميل الحسابات النقدية، ولا يمكن تسجيل التحصيل الآن.',
        CashAccountAvailability.unavailable =>
          'لا يوجد حساب نقدي نشط ومؤهل لاستلام النقدية.',
        _ => 'اختر الحساب النقدي المستلم قبل تسجيل التحصيل.',
      });
      return;
    }

    if (amount + discount > data.order.remainingAmount) {
      _showMessage('لا يمكن أن يتجاوز التحصيل والخصم معاً المبلغ المتبقي.');
      return;
    }

    final reference = _referenceController.text.trim();
    if (reference.isEmpty) {
      _referenceController.text =
          'RCPT-${data.order.number}-${DateTime.now().millisecondsSinceEpoch}';
    }

    setState(() => _submitting = true);

    try {
      await _repository.settleCustomerBalance(
        data.order.id,
        amount,
        discount,
        _referenceController.text.trim(),
        paymentMethod: 'Cash',
        cashAccountId: _cashAccountId,
        notes: 'تسوية تحصيل وخصم من شاشة التسليم',
      );

      if (!mounted) return;
      _showMessage(
        'تم تسجيل التسوية: تحصيل ${NumberFormat('#,##0.00').format(amount)} ر.س وخصم ${NumberFormat('#,##0.00').format(discount)} ر.س',
        isSuccess: true,
      );
      _reload();
    } catch (error) {
      if (!mounted) return;
      _showMessage('تعذر تسجيل التحصيل: $error');
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  void _selectFullCollection(OrderDetails order) {
    final discount = double.tryParse(_discountController.text.trim()) ?? 0;
    _partialCollectionSelected = false;
    _amountController.text =
        math.max(0, order.remainingAmount - discount).toStringAsFixed(2);
    setState(() {});
  }

  void _selectPartialCollection(OrderDetails order) {
    final discount = double.tryParse(_discountController.text.trim()) ?? 0;
    final dueAfterDiscount = math.max(0, order.remainingAmount - discount);
    _partialCollectionSelected = true;
    _amountController.text = (dueAfterDiscount / 2).toStringAsFixed(2);
    setState(() {});
  }

  void _handleDiscountChanged(OrderDetails order, String value) {
    final discount = double.tryParse(value);
    if (discount != null && discount >= 0 && !_partialCollectionSelected) {
      _amountController.text =
          math.max(0, order.remainingAmount - discount).toStringAsFixed(2);
    }
    setState(() {});
  }

  Future<void> _waiveRemainingBalance(OrderDetails order) async {
    if (_waivingBalance || order.remainingAmount <= 0 || !mounted) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('تبرع بالرصيد'),
        content: Text(
          'سيتم إعفاء ${NumberFormat('#,##0.00').format(order.remainingAmount)} ر.س من رصيد العميل وتسجيله كتبرع، دون اعتباره تحصيلاً نقدياً. هل تريد المتابعة؟',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('تأكيد التبرع'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _waivingBalance = true);
    try {
      await _repository.waiveRemainingBalance(order.id);
      if (!mounted) return;
      _showMessage('تم تسجيل التبرع بالرصيد وإعفاء المتبقي.', isSuccess: true);
      _reload();
    } catch (error) {
      if (!mounted) return;
      _showMessage('تعذر تسجيل التبرع بالرصيد: $error');
    } finally {
      if (mounted) setState(() => _waivingBalance = false);
    }
  }

  void _showMessage(String message, {bool isSuccess = false}) {
    if (!mounted) return;
    AppMessage.show(
      context,
      message,
      type: isSuccess ? AppMessageType.success : AppMessageType.error,
    );
  }

  @override
  Widget build(BuildContext context) {
    final numberFormat = NumberFormat('#,##0.00');

    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.screenBackground,
        foregroundColor:
            UiPalette.adaptiveTextColor(UiPalette.screenBackground),
        title: const Text('تسوية وتسليم الطلب'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded),
          onPressed: AppNavigation.back,
        ),
      ),
      body: FutureBuilder<OrderDetailsData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError || !snapshot.hasData) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        size: 48, color: UiPalette.primaryBlue),
                    const SizedBox(height: 12),
                    Text(
                      'تعذر تحميل تفاصيل الطلب.',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.screenBackground,
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      snapshot.error?.toString() ?? 'يرجى المحاولة مرة أخرى.',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.screenBackground,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      onPressed: _reload,
                      icon: const Icon(Icons.refresh),
                      label: const Text('إعادة المحاولة'),
                    ),
                  ],
                ),
              ),
            );
          }

          final data = snapshot.data!;
          final order = data.order;
          _seedFields(order);

          final isDelivered = order.status.toLowerCase() == 'delivered';
          final isReadyForDelivery =
              order.status.toLowerCase() == 'readyfordelivery';
          final isReadyForCollection = isDelivered && order.revenueRecognized;
          final enteringAmount = double.tryParse(_amountController.text) ?? 0;
          final enteringDiscount =
              double.tryParse(_discountController.text) ?? 0;
          final amountAfterCollection = math.max(
              0, order.remainingAmount - enteringAmount - enteringDiscount);
          final amountText = numberFormat.format(enteringAmount);
          final discountText = numberFormat.format(enteringDiscount);

          return SafeArea(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(18),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (!isReadyForCollection)
                    Container(
                      margin: const EdgeInsets.only(bottom: 16),
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: UiPalette.surfaceCard,
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(color: UiPalette.borderSoft),
                      ),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Icon(Icons.info_outline_rounded,
                              color: UiPalette.primaryBlue),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              isReadyForDelivery
                                  ? 'أكد التسليم؛ سيُثبت الإيراد تلقائياً وتتاح بعدها خيارات التحصيل والخصم والتبرع بالرصيد.'
                                  : 'تتم إتاحة التسوية بعد اكتمال التسليم وإثبات الإيراد تلقائياً.',
                              style: UiPalette.adaptiveTextStyle(
                                context,
                                backgroundColor: UiPalette.surfaceCard,
                                fontSize: 13,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  _Panel(
                    title: 'ملخص الطلب',
                    child: Column(
                      children: [
                        _SummaryRow(label: 'رقم الطلب', value: order.number),
                        _SummaryRow(
                            label: 'اسم العميل',
                            value: data.customer.name ?? 'غير متوفر'),
                        _SummaryRow(
                            label: 'حالة الطلب',
                            value: _statusLabel(order.status)),
                        _SummaryRow(
                            label: 'حالة التسليم',
                            value: isDelivered ? 'تم التسليم' : 'قيد التنفيذ'),
                        _SummaryRow(
                            label: 'حالة الإيراد',
                            value: order.revenueRecognized
                                ? 'تم الاعتراف'
                                : 'غير مسجل'),
                        _SummaryRow(
                            label: 'الإجمالي بعد الخصم',
                            value: numberFormat.format(
                                order.totalAmount - order.discountAmount)),
                        _SummaryRow(
                            label: 'إجمالي المدفوع',
                            value: numberFormat.format(order.paidAmount)),
                        _SummaryRow(
                            label: 'المبلغ المتبقي',
                            value: numberFormat.format(order.remainingAmount)),
                      ],
                    ),
                  ),
                  if (data.financialTransactions.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    _Panel(
                      title: 'الحركات المالية المسجلة',
                      child: Column(
                        children: data.financialTransactions
                            .map(
                              (transaction) => _SummaryRow(
                                label: _financialTransactionLabel(
                                    transaction.transactionType),
                                value: numberFormat
                                    .format(transaction.amount),
                              ),
                            )
                            .toList(),
                      ),
                    ),
                  ],
                  const SizedBox(height: 16),
                  if (isReadyForDelivery) ...[
                    _Panel(
                      title: 'إجراء التسليم',
                      child: FilledButton.icon(
                        onPressed:
                            _delivering ? null : () => _deliverOrder(order),
                        icon: _delivering
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child:
                                    CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.local_shipping_outlined),
                        label: Text(
                          _delivering
                              ? 'جاري تأكيد التسليم...'
                              : 'تأكيد التسليم',
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],
                  _Panel(
                    title: 'تسوية المبلغ',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'القيمة المقترحة هي المبلغ المتبقي. يمكنك تحصيله كاملاً أو جزئياً، أو تسجيل تبرع بالرصيد المتبقي.',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 12,
                          ),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: _amountController,
                          enabled: isReadyForCollection &&
                              _partialCollectionSelected,
                          keyboardType: const TextInputType.numberWithOptions(
                              decimal: true),
                          onChanged: (_) => setState(() {}),
                          decoration: InputDecoration(
                            labelText: 'المبلغ الذي سيُحصّل',
                            filled: true,
                            fillColor: UiPalette.softBlue,
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide:
                                  BorderSide(color: UiPalette.borderSoft),
                            ),
                          ),
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.softBlue,
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: _discountController,
                          enabled: isReadyForCollection,
                          keyboardType: const TextInputType.numberWithOptions(
                              decimal: true),
                          onChanged: (value) =>
                              _handleDiscountChanged(order, value),
                          decoration: InputDecoration(
                            labelText: 'الخصم',
                            filled: true,
                            fillColor: UiPalette.softBlue,
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide:
                                  BorderSide(color: UiPalette.borderSoft),
                            ),
                          ),
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.softBlue,
                            fontSize: 18,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        if (enteringAmount > 0) ...[
                          const SizedBox(height: 12),
                          CashAccountPicker(
                            value: _cashAccountId,
                            enabled: isReadyForCollection,
                            accountsFuture: widget.cashAccountsFuture,
                            onAvailabilityChanged: (availability) =>
                              setState(() => _cashAccountAvailability =
                                availability),
                            onChanged: (value) =>
                                setState(() => _cashAccountId = value),
                          ),
                        ],
                        const SizedBox(height: 12),
                        TextField(
                          controller: _referenceController,
                          readOnly: true,
                          decoration: InputDecoration(
                            labelText: 'مرجع التحصيل',
                            filled: true,
                            fillColor: UiPalette.softBlue,
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide:
                                  BorderSide(color: UiPalette.borderSoft),
                            ),
                          ),
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.softBlue,
                            fontSize: 13,
                          ),
                        ),
                        const SizedBox(height: 16),
                        Row(
                          children: [
                            Expanded(
                              child: TextButton.icon(
                                onPressed: isReadyForCollection
                                    ? () => _selectFullCollection(order)
                                    : null,
                                icon:
                                    const Icon(Icons.currency_exchange_rounded),
                                label: const Text('تحصيل كامل المتبقي'),
                              ),
                            ),
                            Expanded(
                              child: TextButton.icon(
                                onPressed: isReadyForCollection
                                    ? () => _selectPartialCollection(order)
                                    : null,
                                icon: const Icon(Icons.percent_rounded),
                                label: const Text('تحصيل جزئي'),
                              ),
                            ),
                            Expanded(
                              child: TextButton.icon(
                                onPressed: isReadyForCollection &&
                                        !_waivingBalance &&
                                        order.remainingAmount > 0
                                    ? () => _waiveRemainingBalance(order)
                                    : null,
                                icon: _waivingBalance
                                    ? const SizedBox(
                                        width: 18,
                                        height: 18,
                                        child: CircularProgressIndicator(
                                            strokeWidth: 2),
                                      )
                                    : const Icon(
                                        Icons.volunteer_activism_outlined),
                                label: Text(
                                  _waivingBalance
                                      ? 'جاري التسجيل...'
                                      : 'تبرع بالرصيد',
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 16),
                        Row(
                          children: [
                            Expanded(
                              child: _InfoCard(
                                title: 'المبلغ الذي سيُحصّل',
                                value: amountText,
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: _InfoCard(
                                title: 'الخصم',
                                value: discountText,
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: _InfoCard(
                                title: 'المبلغ الذي سيبقى في الذمة',
                                value:
                                    numberFormat.format(amountAfterCollection),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 18),
                        if (!order.revenueRecognized && isDelivered)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: FilledButton.icon(
                              onPressed: _recognizingRevenue
                                  ? null
                                  : () => _recognizeRevenue(order),
                              icon: _recognizingRevenue
                                  ? const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(
                                          strokeWidth: 2),
                                    )
                                  : const Icon(Icons.fact_check_rounded),
                              label: const Text('إثبات الإيراد'),
                            ),
                          ),
                        Row(
                          children: [
                            Expanded(
                              child: FilledButton.icon(
                                onPressed: isReadyForCollection &&
                                        !_submitting &&
                                        enteringAmount >= 0 &&
                                        enteringDiscount >= 0 &&
                                        enteringAmount + enteringDiscount > 0 &&
                                        enteringAmount + enteringDiscount <=
                                            order.remainingAmount
                                    ? () => _submitPayment(data)
                                    : null,
                                icon: _submitting
                                    ? const SizedBox(
                                        width: 18,
                                        height: 18,
                                        child: CircularProgressIndicator(
                                            strokeWidth: 2),
                                      )
                                    : const Icon(
                                        Icons.check_circle_outline_rounded),
                                label: Text(_submitting
                                    ? 'جاري التنفيذ...'
                                    : 'تأكيد التحصيل'),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: OutlinedButton.icon(
                                onPressed: _reload,
                                icon: const Icon(Icons.refresh),
                                label: const Text('تحديث البيانات'),
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  String _statusLabel(String value) => switch (value.toLowerCase()) {
        'new' => 'جديد',
        'inproduction' || 'inprogress' => 'قيد الإنتاج',
        'readyfordelivery' => 'جاهز للتسليم',
        'delivered' => 'تم التسليم',
        'cancelled' => 'ملغي',
        _ => value,
      };

  String _financialTransactionLabel(String value) => switch (value) {
        'CustomerBalanceWaiver' => 'تبرع بالرصيد',
        'CustomerSettlementDiscount' => 'خصم تسوية',
        'CustomerPayment' => 'تحصيل ذمة',
        'CustomerAdvance' => 'دفعة مقدمة',
        'RevenueRecognized' => 'إثبات إيراد',
        _ => value,
      };
}

class _Panel extends StatelessWidget {
  const _Panel({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: UiPalette.surfaceCard,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              title,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 18,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 12),
            child,
          ],
        ),
      );
}

class _SummaryRow extends StatelessWidget {
  const _SummaryRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(
          children: [
            Expanded(
              flex: 2,
              child: Text(
                label,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontSize: 13,
                ).copyWith(color: UiPalette.textSoft),
              ),
            ),
            Expanded(
              flex: 3,
              child: Text(
                value,
                textAlign: TextAlign.left,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      );
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.title, required this.value});

  final String title;
  final String value;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: UiPalette.softBlue,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.softBlue,
                fontSize: 12,
              ).copyWith(color: UiPalette.textSoft),
            ),
            const SizedBox(height: 8),
            Text(
              value,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.softBlue,
                fontSize: 22,
                fontWeight: FontWeight.w800,
              ),
            ),
          ],
        ),
      );
}
