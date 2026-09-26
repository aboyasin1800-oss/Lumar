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

enum _DeliverySettlementMode { full, partial, waive, none }

class _DeliverySettlementRequest {
  const _DeliverySettlementRequest({
    required this.mode,
    required this.amount,
    required this.discount,
    required this.cashAccountId,
    required this.referenceNumber,
  });

  final _DeliverySettlementMode mode;
  final double amount;
  final double discount;
  final int? cashAccountId;
  final String referenceNumber;
}

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
  bool _deliveryDialogOpen = false;
  bool _waivingBalance = false;
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
    _initialized = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _amountController.text = order.remainingAmount.toStringAsFixed(2);
      _discountController.text = '0.00';
      _referenceController.text =
          'RCPT-${order.number}-${DateTime.now().millisecondsSinceEpoch}';
      setState(() {});
    });
  }

  Future<void> _deliverOrder(OrderDetailsData data) async {
    if (_delivering || _deliveryDialogOpen || !mounted) return;

    setState(() => _deliveryDialogOpen = true);
    final request = await _showDeliverySettlementDialog(data);
    if (!mounted) return;
    if (request == null) {
      setState(() => _deliveryDialogOpen = false);
      return;
    }

    setState(() {
      _deliveryDialogOpen = false;
      _delivering = true;
    });
    try {
      await _repository.deliverOrder(data.order.id);
    } catch (error) {
      if (!mounted) return;
      await _showError(error);
      _reload();
      return;
    }

    try {
      if (request.mode == _DeliverySettlementMode.waive) {
        await _repository.waiveRemainingBalance(data.order.id);
      } else if (request.amount > 0 || request.discount > 0) {
        await _repository.settleCustomerBalance(
          data.order.id,
          request.amount,
          request.discount,
          request.referenceNumber,
          paymentMethod: 'Cash',
          cashAccountId: request.cashAccountId,
          notes: 'تحصيل من تنفيذ التسليم الموحد',
        );
      }
      if (!mounted) return;
      _showMessage(
        request.amount > 0 || request.discount > 0 ||
                request.mode == _DeliverySettlementMode.waive
            ? 'تم تسليم الطلب وإثبات الإيراد وتطبيق العربون وتسجيل التسوية بنجاح.'
            : 'تم تسليم الطلب وإثبات الإيراد وتطبيق العربون بنجاح.',
        isSuccess: true,
      );
      _reload();
    } catch (error) {
      if (!mounted) return;
      await _showError(error);
      _reload();
    } finally {
      if (mounted) setState(() => _delivering = false);
    }
  }

  Future<_DeliverySettlementRequest?> _showDeliverySettlementDialog(
      OrderDetailsData data) async {
    final order = data.order;
    _seedFields(order);
    var mode = _DeliverySettlementMode.full;

    return showDialog<_DeliverySettlementRequest>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) {
          final amount = double.tryParse(_amountController.text.trim()) ?? 0;
          final discount =
              double.tryParse(_discountController.text.trim()) ?? 0;
          final canCollect = mode == _DeliverySettlementMode.full ||
              mode == _DeliverySettlementMode.partial;
          final remaining = math.max(0, order.remainingAmount - amount - discount);

          void selectMode(_DeliverySettlementMode nextMode) {
            mode = nextMode;
            if (nextMode == _DeliverySettlementMode.full) {
              _amountController.text =
                  math.max(0, order.remainingAmount - discount).toStringAsFixed(2);
            } else if (nextMode == _DeliverySettlementMode.partial) {
              _amountController.clear();
            } else {
              _amountController.text = '0.00';
              _discountController.text = '0.00';
              _cashAccountId = null;
            }
            setDialogState(() {});
          }

          void confirm() {
            if (canCollect &&
                (amount < 0 ||
                    discount < 0 ||
                    amount + discount > order.remainingAmount ||
                    (mode == _DeliverySettlementMode.partial && amount <= 0))) {
              _showMessage('تحقق من مبلغ التحصيل والخصم قبل التنفيذ.');
              return;
            }
            if (canCollect && amount > 0 && _cashAccountId == null) {
              _showMessage('اختر الحساب النقدي المستلم قبل تنفيذ التسليم.');
              return;
            }
            final reference = _referenceController.text.trim().isEmpty
                ? 'RCPT-${order.number}-${DateTime.now().millisecondsSinceEpoch}'
                : _referenceController.text.trim();
            Navigator.of(dialogContext).pop(_DeliverySettlementRequest(
              mode: mode,
              amount: canCollect ? amount : 0,
              discount: canCollect ? discount : 0,
              cashAccountId: canCollect ? _cashAccountId : null,
              referenceNumber: reference,
            ));
          }

          return AlertDialog(
            title: const Text('تسليم الطلب'),
            content: SizedBox(
              width: 460,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('المبلغ المتبقي: ${NumberFormat('#,##0.00').format(order.remainingAmount)}'),
                    const SizedBox(height: 12),
                    const Text('نوع التسوية'),
                    RadioGroup<_DeliverySettlementMode>(
                      groupValue: mode,
                      onChanged: (value) {
                        if (value != null) selectMode(value);
                      },
                      child: Column(
                        children: [
                          const RadioListTile<_DeliverySettlementMode>(
                            value: _DeliverySettlementMode.full,
                            title: Text('تحصيل كامل المتبقي'),
                          ),
                          const RadioListTile<_DeliverySettlementMode>(
                            value: _DeliverySettlementMode.partial,
                            title: Text('تحصيل جزئي'),
                          ),
                          RadioListTile<_DeliverySettlementMode>(
                            value: _DeliverySettlementMode.waive,
                            enabled: order.remainingAmount > 0,
                            title: const Text('تبرع بالمتبقي'),
                          ),
                          const RadioListTile<_DeliverySettlementMode>(
                            value: _DeliverySettlementMode.none,
                            title: Text('تسليم بدون تحصيل'),
                          ),
                        ],
                      ),
                    ),
                    TextField(
                      controller: _amountController,
                      enabled: mode == _DeliverySettlementMode.partial,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      onChanged: (_) => setDialogState(() {}),
                      decoration: const InputDecoration(labelText: 'المبلغ الذي سيُحصّل'),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _discountController,
                      enabled: canCollect,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      onChanged: (value) {
                        if (mode == _DeliverySettlementMode.full) {
                          final updatedDiscount = double.tryParse(value) ?? 0;
                          _amountController.text = math.max(
                            0,
                            order.remainingAmount - updatedDiscount,
                          ).toStringAsFixed(2);
                        }
                        setDialogState(() {});
                      },
                      decoration: const InputDecoration(labelText: 'الخصم'),
                    ),
                    if (canCollect && amount > 0) ...[
                      const SizedBox(height: 12),
                      CashAccountPicker(
                        value: _cashAccountId,
                        enabled: true,
                        accountsFuture: widget.cashAccountsFuture,
                        onAvailabilityChanged: (availability) {
                          _cashAccountAvailability = availability;
                          setDialogState(() {});
                        },
                        onChanged: (value) {
                          _cashAccountId = value;
                          setDialogState(() {});
                        },
                      ),
                    ],
                    const SizedBox(height: 12),
                    TextField(
                      controller: _referenceController,
                      enabled: canCollect && amount > 0,
                      decoration: const InputDecoration(labelText: 'مرجع التحصيل'),
                    ),
                    const SizedBox(height: 16),
                    Text('سيتم تنفيذ العمليات التالية:\n✓ تسليم الطلب\n✓ إثبات الإيراد\n✓ تطبيق العربون إن وجد${canCollect && amount > 0 ? '\n✓ تحصيل المبلغ المحدد' : ''}\n\nالمبلغ المحصل: ${NumberFormat('#,##0.00').format(canCollect ? amount : 0)}\nالمبلغ الذي سيبقى ذمة: ${NumberFormat('#,##0.00').format(remaining)}'),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('إلغاء'),
              ),
              FilledButton.icon(
                onPressed: confirm,
                icon: const Icon(Icons.local_shipping_outlined),
                label: const Text('تنفيذ التسليم'),
              ),
            ],
          );
        },
      ),
    );
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
      await _showError(error);
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
      await _showError(error);
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

  Future<void> _showError(Object error) {
    if (!mounted) return Future.value();
    return AppMessage.showErrorDialog(context, error);
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
                        onPressed: _delivering
                            ? null
                            : () => _deliverOrder(data),
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
                              ? 'جاري تنفيذ التسليم...'
                              : 'تسليم الطلب',
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],
                  if (!isReadyForDelivery)
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
