import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class LoyaltyRedemptionsHistoryScreen extends StatefulWidget {
  const LoyaltyRedemptionsHistoryScreen({super.key, this.customerId});

  final int? customerId;

  @override
  State<LoyaltyRedemptionsHistoryScreen> createState() =>
      _LoyaltyRedemptionsHistoryScreenState();
}

class _LoyaltyRedemptionsHistoryScreenState
    extends State<LoyaltyRedemptionsHistoryScreen> {
  final LoyaltyRepository _repository = LoyaltyRepository();
  final TextEditingController _searchController = TextEditingController();
  Timer? _searchTimer;
  Future<List<LoyaltyCustomerSearchResult>>? _suggestionsFuture;
  int _searchVersion = 0;
  late Future<LoyaltyRedemptionHistoryData> _future;
  String _type = 'الكل';
  DateTime? _from;
  DateTime? _to;
  int? _customerId;

  @override
  void initState() {
    super.initState();
    _customerId = widget.customerId;
    _future = _load();
  }

  Future<LoyaltyRedemptionHistoryData> _load() =>
      _repository.getRedemptionsHistory(
        search: _searchController.text,
        transactionType: _type == 'الكل' ? null : _type,
        from: _from,
        to: _to,
        customerId: _customerId,
      );

  void _refresh() {
    _searchTimer?.cancel();
    _searchVersion++;
    setState(() => _future = _load());
  }

  void _onSearchChanged(String value) {
    final version = ++_searchVersion;
    _searchTimer?.cancel();
    if (value.trim().length < 2) {
      if (_suggestionsFuture != null && mounted) {
        setState(() => _suggestionsFuture = null);
      }
      return;
    }
    _searchTimer = Timer(const Duration(milliseconds: 250), () {
      if (!mounted || version != _searchVersion) return;
      setState(() {
        _suggestionsFuture = _repository.searchCustomers(value.trim());
      });
    });
  }

  void _selectCustomer(LoyaltyCustomerSearchResult customer) {
    _searchTimer?.cancel();
    _searchVersion++;
    _searchController
      ..text = customer.customerName ?? customer.customerCode ?? ''
      ..selection =
          TextSelection.collapsed(offset: _searchController.text.length);
    setState(() {
      _customerId = customer.customerId;
      _suggestionsFuture = null;
      _future = _load();
    });
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final palette = Theme.of(context).brightness == Brightness.dark
        ? _DarkPalette()
        : _LightPalette();
    return Scaffold(
      backgroundColor: palette.background,
      appBar: AppBar(
          title: const Text('سجل استبدالات النقاط'),
          backgroundColor: palette.surface,
          foregroundColor: palette.text),
      body: FutureBuilder<LoyaltyRedemptionHistoryData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting)
            return Center(
                child: CircularProgressIndicator(color: palette.primary));
          if (snapshot.hasError)
            return Center(
                child: Text(RlUiText.friendlyError(snapshot.error),
                    style: TextStyle(color: palette.text)));
          final data = snapshot.data!;
          return Column(children: [
            _filters(palette, data.items),
            _summary(palette, data.summary),
            Expanded(child: _items(palette, data.items))
          ]);
        },
      ),
    );
  }

  Widget _filters(_Palette palette, List<LoyaltyRedemptionHistoryItem> items) {
    final customers = <int, String>{
      for (final item in items)
        item.customerId: item.customerName ?? 'العميل ${item.customerId}'
    };
    return Container(
      margin: const EdgeInsets.all(16),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
          color: palette.surface,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: palette.border)),
      child: Wrap(
          spacing: 10,
          runSpacing: 10,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            SizedBox(
                width: 330,
                child: TextField(
                  controller: _searchController,
                  textInputAction: TextInputAction.search,
                  onChanged: _onSearchChanged,
                  onSubmitted: (_) => _refresh(),
                  decoration: InputDecoration(
                      hintText: 'اسم العميل أو الكود أو رقم الطلب',
                      filled: true,
                      fillColor: palette.field,
                      prefixIcon: Icon(Icons.search, color: palette.primary),
                      suffixIcon: IconButton(
                          onPressed: _refresh,
                          icon:
                              Icon(Icons.arrow_forward, color: palette.primary),
                          tooltip: 'بحث'),
                      border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(10),
                          borderSide: BorderSide(color: palette.border))),
                )),
            if (_suggestionsFuture != null)
              SizedBox(
                width: 330,
                child: FutureBuilder<List<LoyaltyCustomerSearchResult>>(
                  future: _suggestionsFuture,
                  builder: (context, snapshot) {
                    final suggestions =
                        snapshot.data ?? const <LoyaltyCustomerSearchResult>[];
                    if (suggestions.isEmpty) return const SizedBox.shrink();
                    return Card(
                      color: palette.surface,
                      child: Column(
                        children: suggestions
                            .take(6)
                            .map((customer) => ListTile(
                                  dense: true,
                                  title: Text(
                                      customer.customerName ?? 'عميل غير مسمى'),
                                  subtitle:
                                      Text(customer.customerCode ?? 'بدون كود'),
                                  onTap: () => _selectCustomer(customer),
                                ))
                            .toList(),
                      ),
                    );
                  },
                ),
              ),
            SizedBox(
              width: 150,
              child: DropdownButtonFormField<String>(
                value: _type,
                decoration: const InputDecoration(labelText: 'نوع العملية'),
                items: const [
                  DropdownMenuItem(value: 'الكل', child: Text('كل العمليات')),
                  DropdownMenuItem(
                      value: 'Redeem', child: Text('استبدال نقاط')),
                  DropdownMenuItem(
                      value: 'Reversal', child: Text('إلغاء استبدال')),
                ],
                onChanged: (value) {
                  if (value == null) return;
                  setState(() {
                    _type = value;
                    _future = _load();
                  });
                },
              ),
            ),
            _dateFilterButton(
              palette,
              label: 'من تاريخ',
              value: _from,
              onPressed: () => _pickDate(true),
              onClear: _from == null ? null : () => _clearDate(true),
            ),
            _dateFilterButton(
              palette,
              label: 'إلى تاريخ',
              value: _to,
              onPressed: () => _pickDate(false),
              onClear: _to == null ? null : () => _clearDate(false),
            ),
            if (_from != null ||
                _to != null ||
                _customerId != null ||
                _searchController.text.isNotEmpty)
              IconButton(
                  onPressed: () {
                    _searchTimer?.cancel();
                    _searchVersion++;
                    _searchController.clear();
                    _from = null;
                    _to = null;
                    _customerId = null;
                    _type = 'الكل';
                    _suggestionsFuture = null;
                    _refresh();
                  },
                  icon: const Icon(Icons.clear),
                  tooltip: 'مسح الفلاتر'),
            ...customers.entries.map((entry) => ChoiceChip(
                label: Text(entry.value),
                selected: _customerId == entry.key,
                onSelected: (_) {
                  _customerId = _customerId == entry.key ? null : entry.key;
                  _refresh();
                })),
          ]),
    );
  }

  Widget _summary(_Palette palette, LoyaltyRedemptionHistorySummary summary) =>
      SizedBox(
        height: 92,
        child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            children: [
              _summaryCard(palette, 'عمليات الاستبدال',
                  '${summary.redemptionCount}', Icons.receipt_long),
              _summaryCard(palette, 'النقاط المستبدلة',
                  summary.redeemedPointsTotal.toStringAsFixed(2), Icons.stars),
              _summaryCard(palette, 'قيمة الرصيد',
                  summary.creditAmountTotal.toStringAsFixed(2), Icons.payments),
              _summaryCard(palette, 'عمليات الإلغاء',
                  '${summary.reversalCount}', Icons.undo),
              _summaryCard(
                  palette, 'العملاء', '${summary.customerCount}', Icons.people),
            ]),
      );

  Widget _summaryCard(
          _Palette palette, String label, String value, IconData icon) =>
      Container(
          width: 180,
          margin: const EdgeInsets.only(left: 10),
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
              color: palette.surface,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: palette.border)),
          child: Row(children: [
            Icon(icon, color: palette.primary),
            const SizedBox(width: 9),
            Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(label, style: TextStyle(color: palette.text)),
              Text(value,
                  style: TextStyle(
                      color: palette.primary,
                      fontSize: 18,
                      fontWeight: FontWeight.bold))
            ])
          ]));

  Widget _dateFilterButton(
    _Palette palette, {
    required String label,
    required DateTime? value,
    required VoidCallback onPressed,
    required VoidCallback? onClear,
  }) =>
      SizedBox(
        width: 150,
        child: OutlinedButton.icon(
          onPressed: onPressed,
          icon: const Icon(Icons.calendar_today_outlined),
          label: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Flexible(
                child: Text(
                  value == null ? label : '$label\n${_formatDate(value)}',
                  textAlign: TextAlign.center,
                ),
              ),
              if (onClear != null)
                IconButton(
                  onPressed: onClear,
                  icon: const Icon(Icons.close, size: 16),
                  tooltip: 'مسح $label',
                  padding: EdgeInsets.zero,
                  constraints:
                      const BoxConstraints(minWidth: 24, minHeight: 24),
                ),
            ],
          ),
        ),
      );

  Widget _items(_Palette palette, List<LoyaltyRedemptionHistoryItem> items) {
    if (items.isEmpty)
      return Center(
          child: Text('لا توجد بيانات للعرض',
              style: TextStyle(color: palette.text)));
    return ListView.builder(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
        itemCount: items.length,
        itemBuilder: (context, index) {
          final item = items[index];
          final reversal = item.transactionType == 'Reversal';
          return Card(
              color: palette.surface,
              child: ListTile(
                onTap: () => _showDetails(context, item, palette),
                leading: CircleAvatar(
                    backgroundColor:
                        (reversal ? Colors.orange : palette.primary)
                            .withOpacity(.18),
                    child: Icon(reversal ? Icons.undo : Icons.stars,
                        color: reversal ? Colors.orange : palette.primary)),
                title: Text(reversal ? 'إلغاء استبدال النقاط' : 'استبدال نقاط',
                    style: TextStyle(
                        color: palette.text, fontWeight: FontWeight.bold)),
                subtitle: Text(
                    '${item.customerName ?? 'العميل ${item.customerId}'}  •  ${item.customerCode ?? 'بدون كود'}\n${item.orderNumber ?? (item.orderId == null ? 'بدون طلب' : 'طلب #${item.orderId}')}  •  ${_formatDateTime(item.createdAt)}',
                    style: TextStyle(color: palette.text)),
                trailing: Text('${item.points.abs().toStringAsFixed(2)} نقطة',
                    style: TextStyle(
                        color: palette.primary, fontWeight: FontWeight.bold)),
              ));
        });
  }

  Future<void> _pickDate(bool from) async {
    final value = await showDatePicker(
        context: context,
        firstDate: DateTime(2020),
        lastDate: DateTime.now().add(const Duration(days: 1)),
        initialDate:
            from ? (_from ?? DateTime.now()) : (_to ?? DateTime.now()));
    if (value == null) return;
    if (from && _to != null && value.isAfter(_to!)) {
      _showDateError('تاريخ البداية يجب أن يسبق تاريخ النهاية');
      return;
    }
    if (!from && _from != null && value.isBefore(_from!)) {
      _showDateError('تاريخ النهاية يجب أن يلي تاريخ البداية');
      return;
    }
    setState(() {
      if (from) {
        _from = value;
      } else {
        _to = value;
      }
      _future = _load();
    });
  }

  void _clearDate(bool from) {
    setState(() {
      if (from) {
        _from = null;
      } else {
        _to = null;
      }
      _future = _load();
    });
  }

  void _showDateError(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  void _showDetails(BuildContext context, LoyaltyRedemptionHistoryItem item,
      _Palette palette) {
    showDialog<void>(
        context: context,
        builder: (_) => AlertDialog(
                backgroundColor: palette.surface,
                title: Text('تفاصيل حركة الولاء',
                    style: TextStyle(color: palette.text)),
                content: SingleChildScrollView(
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                      _detail(
                          'العميل', item.customerName ?? 'غير متوفر', palette),
                      _detail('كود العميل', item.customerCode ?? 'غير متوفر',
                          palette),
                      _detail(
                          'نوع العملية',
                          item.transactionType == 'Reversal'
                              ? 'إلغاء استبدال النقاط'
                              : 'استبدال نقاط',
                          palette),
                      _detail('النقاط', item.points.abs().toStringAsFixed(2),
                          palette),
                      _detail('الرصيد قبل الحركة',
                          item.balanceBefore.toStringAsFixed(2), palette),
                      _detail('الرصيد بعد الحركة',
                          item.balanceAfter.toStringAsFixed(2), palette),
                      _detail(
                          'القيمة المالية',
                          item.creditAmount?.toStringAsFixed(2) ?? 'غير متوفرة',
                          palette),
                      _detail('رقم المرجع', item.referenceNumber ?? 'غير متوفر',
                          palette),
                      _detail(
                          'المرجع المالي',
                          item.financialReference ?? 'لا يوجد ارتباط مالي',
                          palette),
                      _detail(
                          'التاريخ', _formatDateTime(item.createdAt), palette),
                    ])),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(context),
                      child: const Text('إغلاق'))
                ]));
  }

  Widget _detail(String label, String value, _Palette palette) => Padding(
      padding: const EdgeInsets.only(bottom: 9),
      child: Text('$label: $value', style: TextStyle(color: palette.text)));
}

String _formatDate(DateTime value) =>
    '${value.year}/${value.month.toString().padLeft(2, '0')}/${value.day.toString().padLeft(2, '0')}';
String _formatDateTime(DateTime value) =>
    '${_formatDate(value.toLocal())} ${value.toLocal().hour.toString().padLeft(2, '0')}:${value.toLocal().minute.toString().padLeft(2, '0')}';

abstract class _Palette {
  Color get background;
  Color get surface;
  Color get field;
  Color get text;
  Color get primary;
  Color get border;
}

class _DarkPalette implements _Palette {
  @override
  Color get background => UiPalette.darkBackground;
  @override
  Color get surface => UiPalette.darkSurface;
  @override
  Color get field => UiPalette.darkSurfaceAlt;
  @override
  Color get text => UiPalette.darkText;
  @override
  Color get primary => UiPalette.primary;
  @override
  Color get border => UiPalette.primaryBorder;
}

class _LightPalette implements _Palette {
  @override
  Color get background => UiPalette.lightBackground;
  @override
  Color get surface => UiPalette.lightCard;
  @override
  Color get field => UiPalette.lightField;
  @override
  Color get text => UiPalette.lightText;
  @override
  Color get primary => UiPalette.primary;
  @override
  Color get border => UiPalette.primaryBorder;
}
