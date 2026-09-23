import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/rl_ui_text.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class LoyaltyRedemptionScreen extends StatefulWidget {
  const LoyaltyRedemptionScreen({super.key, this.customerId});

  final int? customerId;

  @override
  State<LoyaltyRedemptionScreen> createState() =>
      _LoyaltyRedemptionScreenState();
}

class _LoyaltyRedemptionScreenState extends State<LoyaltyRedemptionScreen> {
  final LoyaltyRepository _repository = LoyaltyRepository();
  final _customerController = TextEditingController();
  final _pointsController = TextEditingController();
  Timer? _searchTimer;
  List<LoyaltyCustomerSearchResult> _customers = const [];
  List<Map<String, dynamic>> _orders = const [];
  LoyaltyCustomerSearchResult? _customer;
  LoyaltyAccount? _account;
  LoyaltyRedemptionPreview? _preview;
  LoyaltyRedemptionResult? _result;
  double? _pointValue;
  int? _orderId;
  bool _loading = false;
  bool _searching = false;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    if (widget.customerId != null) _loadCustomerById(widget.customerId!);
    _loadPointValue();
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _customerController.dispose();
    _pointsController.dispose();
    super.dispose();
  }

  Future<void> _loadPointValue() async {
    try {
      final settings = await _repository.getProgramSettings();
      if (mounted && settings.isNotEmpty) {
        setState(() => _pointValue = settings.first.pointMonetaryValue);
      }
    } catch (_) {}
  }

  void _onCustomerChanged(String value) {
    _searchTimer?.cancel();
    _searchTimer = Timer(const Duration(milliseconds: 350), () async {
      if (value.trim().isEmpty) {
        if (mounted) setState(() => _customers = const []);
        return;
      }
      setState(() => _searching = true);
      try {
        final customers = await _repository.searchCustomers(value);
        if (mounted) setState(() => _customers = customers);
      } finally {
        if (mounted) setState(() => _searching = false);
      }
    });
  }

  Future<void> _loadCustomerById(int customerId) async {
    try {
      final customers = await _repository.searchCustomers('$customerId');
      final customer =
          customers.firstWhere((item) => item.customerId == customerId);
      await _selectCustomer(customer);
    } catch (_) {}
  }

  Future<void> _selectCustomer(LoyaltyCustomerSearchResult customer) async {
    setState(() {
      _customer = customer;
      _customers = const [];
      _customerController.text =
          customer.customerName ?? '${customer.customerId}';
      _loading = true;
      _preview = null;
      _result = null;
    });
    try {
      final values = await Future.wait([
        _repository.getAccount(customer.customerId),
        _repository.getOrders(),
      ]);
      if (!mounted) return;
      final allOrders = values[1] as List<Map<String, dynamic>>;
      setState(() {
        _account = values[0] as LoyaltyAccount;
        _orders = allOrders
            .where((order) =>
                order['customerId'] == customer.customerId &&
                (order['remainingAmount'] as num? ?? 0) > 0)
            .toList();
      });
    } catch (error) {
      _showError(error);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _previewRedemption() async {
    final customer = _customer;
    final value = _pointValue;
    final points = double.tryParse(_pointsController.text.trim());
    if (customer == null ||
        _orderId == null ||
        points == null ||
        points <= 0 ||
        value == null) {
      _showMessage(
          'اختر العميل والطلب وأدخل نقاطًا صحيحة بعد تحميل قيمة النقطة.');
      return;
    }
    setState(() => _loading = true);
    try {
      final preview = await _repository.previewRedemption(
        customerId: customer.customerId,
        orderId: _orderId!,
        pointsRedeemed: points,
        pointMonetaryValue: value,
      );
      if (mounted) setState(() => _preview = preview);
    } catch (error) {
      _showError(error);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _applyRedemption() async {
    if (_submitting ||
        _preview?.isValid != true ||
        _customer == null ||
        _orderId == null ||
        _pointValue == null) {
      return;
    }
    setState(() => _submitting = true);
    try {
      final result = await _repository.applyRedemption(
        customerId: _customer!.customerId,
        orderId: _orderId!,
        pointsRedeemed: _preview!.pointsRedeemed,
        pointMonetaryValue: _pointValue!,
      );
      if (!mounted) return;
      await _selectCustomer(_customer!);
      if (!mounted) return;
      setState(() => _result = result);
      _showMessage('تم تنفيذ الاستبدال بنجاح وتحديث الرصيد.');
    } catch (error) {
      _showError(error);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _showMessage(String message) => ScaffoldMessenger.of(context)
      .showSnackBar(SnackBar(content: Text(message)));

  void _showError(Object error) => _showMessage(
      RlUiText.friendlyError(error, fallback: 'تعذر تنفيذ العملية.'));

  @override
  Widget build(BuildContext context) {
    final account = _account;
    return Scaffold(
      appBar: AppBar(title: const Text('استبدال نقاط الولاء')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          TextField(
            controller: _customerController,
            onChanged: _onCustomerChanged,
            decoration: InputDecoration(
                labelText: 'بحث عن العميل',
                suffixIcon: _searching
                    ? const CircularProgressIndicator()
                    : const Icon(Icons.person_search_outlined)),
          ),
          if (_customers.isNotEmpty)
            ..._customers.map((customer) => ListTile(
                  title: Text(
                      customer.customerName ?? 'عميل ${customer.customerId}'),
                  subtitle: Text(
                      '${customer.customerCode ?? ''} ${customer.phoneNumber ?? ''}'),
                  onTap: () => _selectCustomer(customer),
                )),
          if (_loading) const LinearProgressIndicator(),
          if (account != null) ...[
            const SizedBox(height: 16),
            Card(
                child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Text(
                        'الرصيد الحالي: ${account.currentPoints} نقطة\nإجمالي المكتسب: ${account.lifetimeEarnedPoints}\nإجمالي المستبدل: ${account.lifetimeRedeemedPoints}'))),
            const SizedBox(height: 16),
            DropdownButtonFormField<int>(
              initialValue: _orderId,
              decoration:
                  const InputDecoration(labelText: 'الطلب المستحق للخصم'),
              items: _orders
                  .map((order) => DropdownMenuItem<int>(
                        value: (order['orderId'] as num).toInt(),
                        child: Text(
                            '#${order['orderNumber']} - المتبقي ${order['remainingAmount']}'),
                      ))
                  .toList(),
              onChanged: (value) => setState(() {
                _orderId = value;
                _preview = null;
              }),
            ),
            const SizedBox(height: 12),
            TextField(
                controller: _pointsController,
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                    labelText: 'النقاط المراد استبدالها')),
            const SizedBox(height: 16),
            FilledButton.icon(
                onPressed: _loading ? null : _previewRedemption,
                icon: const Icon(Icons.preview_outlined),
                label: const Text('معاينة الاستبدال')),
          ],
          if (_preview != null)
            Card(
                child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(_preview!.isValid
                              ? 'المعاينة صالحة'
                              : (_preview!.validationMessage.isEmpty
                                  ? 'المعاينة غير صالحة'
                                  : _preview!.validationMessage)),
                          Text('قيمة الخصم: ${_preview!.discountAmount}'),
                          const SizedBox(height: 12),
                          FilledButton.icon(
                              onPressed: _preview!.isValid && !_submitting
                                  ? _applyRedemption
                                  : null,
                              icon: const Icon(Icons.check_circle_outline),
                              label: Text(_submitting
                                  ? 'جارٍ التنفيذ...'
                                  : 'تنفيذ الاستبدال')),
                        ]))),
          if (_result != null)
            Card(
                child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Text(
                        'تمت العملية رقم ${_result!.redemptionId}\nرصيد النقاط قبل: ${_result!.balanceBefore}\nالنقاط المستبدلة: ${_result!.pointsRedeemed}\nرصيد النقاط بعد: ${_result!.balanceAfter}\nقيمة النقطة: ${_pointValue ?? 0}\nرصيد الولاء المطبق: ${_result!.amount}\nمتبقي الطلب قبل: ${_result!.remainingOrderAmountBefore}\nمتبقي الطلب بعد: ${_result!.remainingOrderAmountAfter}\nالتاريخ: ${_result!.createdAt}'))),
        ],
      ),
    );
  }
}
