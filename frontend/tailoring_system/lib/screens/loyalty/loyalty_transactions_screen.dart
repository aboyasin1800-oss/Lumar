import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class LoyaltyTransactionsScreen extends StatefulWidget {
  const LoyaltyTransactionsScreen(
      {super.key, this.customerId, this.repository});

  final int? customerId;
  final LoyaltyRepository? repository;

  @override
  State<LoyaltyTransactionsScreen> createState() =>
      _LoyaltyTransactionsScreenState();
}

class _LoyaltyTransactionsScreenState extends State<LoyaltyTransactionsScreen> {
  late final LoyaltyRepository _repository =
      widget.repository ?? LoyaltyRepository();
  final _searchController = TextEditingController();
  Timer? _searchTimer;
  late Future<LoyaltyTransactionsScreenData> _future;
  String? _transactionType;
  DateTime? _from;
  DateTime? _to;
  int? _customerId;
  Future<List<LoyaltyCustomerSearchResult>>? _suggestionsFuture;
  int _searchVersion = 0;
  bool _searching = false;

  @override
  void initState() {
    super.initState();
    _customerId = _initialCustomerId;
    _future = _load();
  }

  int? get _initialCustomerId =>
      widget.customerId != null && widget.customerId! > 0
          ? widget.customerId
          : null;

  @override
  void dispose() {
    _searchTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<LoyaltyTransactionsScreenData> _load() =>
      _repository.getTransactionsScreen(
        search: _searchController.text,
        transactionType: _transactionType,
        from: _from,
        to: _to,
        customerId: _customerId,
      );

  void _reload() => setState(() => _future = _load());

  void _onSearchChanged(String value) {
    _searchTimer?.cancel();
    final version = ++_searchVersion;
    final term = value.trim();
    _searchTimer = Timer(const Duration(milliseconds: 350), () async {
      if (!mounted || version != _searchVersion) return;
      if (term.isEmpty) {
        setState(() {
          _suggestionsFuture = null;
          _customerId = _initialCustomerId;
          _searching = false;
          _future = _load();
        });
        return;
      }
      setState(() {
        _suggestionsFuture = null;
        _searching = true;
      });
      try {
        final customers = await _repository.searchCustomers(term);
        if (!mounted || version != _searchVersion) return;
        setState(() {
          _suggestionsFuture = Future.value(customers);
        });
      } catch (error) {
        if (!mounted || version != _searchVersion) return;
        setState(() {
          _suggestionsFuture = Future<List<LoyaltyCustomerSearchResult>>.error(
            error,
          );
        });
      } finally {
        if (mounted && version == _searchVersion) {
          setState(() => _searching = false);
        }
      }
    });
  }

  void _submitCustomerSearch() {
    _searchTimer?.cancel();
    _searchVersion++;
    final term = _searchController.text.trim();
    setState(() {
      _customerId = term.isEmpty ? _initialCustomerId : null;
      _suggestionsFuture = null;
      _searching = false;
      _future = _load();
    });
  }

  void _selectCustomer(LoyaltyCustomerSearchResult customer) {
    _searchTimer?.cancel();
    _searchVersion++;
    _searchController
      ..text = customer.customerName ?? customer.customerCode ?? ''
      ..selection = TextSelection.collapsed(
        offset: _searchController.text.length,
      );
    setState(() {
      _customerId = customer.customerId;
      _suggestionsFuture = null;
      _searching = false;
      _future = _load();
    });
  }

  void _clearCustomerSearch() {
    _searchTimer?.cancel();
    _searchVersion++;
    _searchController.clear();    
    setState(() {
      _customerId = _initialCustomerId;
      _suggestionsFuture = null;
      _searching = false;
      _future = _load();
    });
  }

  Future<void> _pickDate({required bool from}) async {
    final selected = await showDatePicker(
      context: context,
      initialDate: (from ? _from : _to) ?? DateTime.now(),
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      helpText: from ? 'اختر تاريخ البداية' : 'اختر تاريخ النهاية',
    );
    if (!mounted || selected == null) return;
    setState(() {
      if (from) {
        _from = selected;
      } else {
        _to = selected;
      }
      _future = _load();
    });
  }

  void _clearFilters() {
    _searchTimer?.cancel();
    _searchVersion++;
    _searchController.clear();
    setState(() {
      _transactionType = null;
      _from = null;
      _to = null;
      _customerId = null;
      _suggestionsFuture = null;
      _searching = false;
      _future = _load();
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(title: const Text('حركات الولاء')),
        body: FutureBuilder<LoyaltyTransactionsScreenData>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                  child: Text(RlUiText.friendlyError(snapshot.error)));
            }
            final data = snapshot.data;
            if (data == null) {
              return const Center(child: Text('لا توجد بيانات.'));
            }
            return RefreshIndicator(
              onRefresh: () async => _reload(),
              child: ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  _buildFilters(),
                  const SizedBox(height: 16),
                  _buildScope(),
                  const SizedBox(height: 16),
                  _buildSummary(data.summary),
                  const SizedBox(height: 16),
                  _buildDistribution(data.summary.types),
                  const SizedBox(height: 16),
                  _buildTransactions(data.transactions),
                ],
              ),
            );
          },
        ),
      );

  Widget _buildFilters() => _Panel(
        child:
            Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          TextField(
            controller: _searchController,
            onChanged: _onSearchChanged,
            onSubmitted: (_) => _submitCustomerSearch(),
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              labelText: 'بحث باسم العميل أو كود العميل',
              prefixIcon: Icon(Icons.search),
              suffixIcon: _searching
                  ? const Padding(
                      padding: EdgeInsets.all(12),
                      child: SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                    )
                  : _searchController.text.isEmpty
                      ? null
                      : IconButton(
                          tooltip: 'مسح بحث العميل',
                          onPressed: _clearCustomerSearch,
                          icon: const Icon(Icons.clear),
                        ),
            ),
          ),
          if (_suggestionsFuture != null)
            _CustomerSuggestions(
              future: _suggestionsFuture!,
              onSelected: _selectCustomer,
            ),
          const SizedBox(height: 12),
          Wrap(spacing: 10, runSpacing: 10, children: [
            SizedBox(
              width: 210,
              child: DropdownButtonFormField<String>(
                initialValue: _transactionType,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'نوع الحركة'),
                items: const [
                  DropdownMenuItem(value: 'Earn', child: Text('كسب نقاط')),
                  DropdownMenuItem(
                      value: 'Redeem', child: Text('استبدال نقاط')),
                  DropdownMenuItem(value: 'Reversal', child: Text('عكس نقاط')),
                  DropdownMenuItem(value: 'Adjust', child: Text('تعديل نقاط')),
                ],
                onChanged: (value) {
                  setState(() {
                    _transactionType = value;
                    _future = _load();
                  });
                },
              ),
            ),
            OutlinedButton.icon(
              onPressed: () => _pickDate(from: true),
              icon: const Icon(Icons.calendar_today_outlined),
              label: Text(_from == null
                  ? 'من تاريخ'
                  : DateFormat('yyyy-MM-dd').format(_from!)),
            ),
            OutlinedButton.icon(
              onPressed: () => _pickDate(from: false),
              icon: const Icon(Icons.event_outlined),
              label: Text(_to == null
                  ? 'إلى تاريخ'
                  : DateFormat('yyyy-MM-dd').format(_to!)),
            ),
            TextButton.icon(
              onPressed: _clearFilters,
              icon: const Icon(Icons.clear_all),
              label: const Text('مسح الفلاتر'),
            ),
          ]),
        ]),
      );

  Widget _buildSummary(LoyaltyTransactionsSummary summary) => Wrap(
        spacing: 12,
        runSpacing: 12,
        children: [
          _summaryCard('إجمالي الحركات', summary.transactionCount,
              Icons.receipt_long_outlined),
          _summaryCard('النقاط المكتسبة', summary.earnedPointsTotal,
              Icons.add_circle_outline),
          _summaryCard('النقاط المستبدلة', summary.redeemedPointsTotal,
              Icons.redeem_outlined),
          _summaryCard('النقاط المعكوسة', summary.reversedPointsTotal,
              Icons.undo_outlined),
          _summaryCard(
              'عدد التعديلات', summary.adjustCount, Icons.tune_outlined),
          _summaryCard('العملاء النشطون', summary.activeCustomerCount,
              Icons.people_alt_outlined),
        ],
      );

  Widget _buildScope() => _Panel(
        child: Text(
          _customerId == null
              ? 'النطاق الحالي: جميع العملاء'
              : 'النطاق الحالي: العميل رقم $_customerId',
          style: TextStyle(color: UiPalette.textSoft),
        ),
      );

  Widget _summaryCard(String label, num value, IconData icon) => SizedBox(
        width: 210,
        child: _Panel(
          child: Row(children: [
            Icon(icon, color: UiPalette.primaryBlue),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(label,
                        style:
                            TextStyle(color: UiPalette.textSoft, fontSize: 12)),
                    const SizedBox(height: 4),
                    Text(_formatNumber(value),
                        style: TextStyle(
                            color: UiPalette.textMain,
                            fontSize: 19,
                            fontWeight: FontWeight.bold)),
                  ]),
            ),
          ]),
        ),
      );

  Widget _buildDistribution(List<LoyaltyTransactionTypeSummary> types) =>
      _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('توزيع أنواع الحركات', style: _sectionStyle),
          const SizedBox(height: 10),
          if (types.isEmpty) const Text('لا توجد حركات ضمن الفلاتر الحالية.'),
          Wrap(
            spacing: 18,
            runSpacing: 12,
            children: types
                .map((type) => Row(mainAxisSize: MainAxisSize.min, children: [
                      Icon(_typeIcon(type.transactionType),
                          color: _typeColor(type.transactionType)),
                      const SizedBox(width: 6),
                      Text(
                          '${_typeLabel(type.transactionType)}: ${type.transactionCount} حركة'),
                    ]))
                .toList(),
          ),
        ]),
      );

  Widget _buildTransactions(List<LoyaltyTransactionListItem> items) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('السجل التاريخي للحركات', style: _sectionStyle),
          const SizedBox(height: 10),
          if (items.isEmpty) const Text('لا توجد حركات ضمن الفلاتر الحالية.'),
          ...items.map(_transactionTile),
        ]),
      );

  Widget _transactionTile(LoyaltyTransactionListItem item) => ListTile(
        contentPadding: EdgeInsets.zero,
        leading: CircleAvatar(
          backgroundColor:
              _typeColor(item.transactionType).withValues(alpha: .14),
          child: Icon(_typeIcon(item.transactionType),
              color: _typeColor(item.transactionType)),
        ),
        title: Text(_typeLabel(item.transactionType)),
        subtitle: Text(
          '${item.customerName ?? 'عميل بدون اسم'} • ${item.customerCode ?? 'بدون كود'}\n'
          '${DateFormat('yyyy-MM-dd HH:mm').format(item.createdAt.toLocal())}'
          '${item.orderId == null ? '' : ' • الطلب ${item.orderId}'}',
        ),
        trailing: Text(_signedPoints(item.points),
            style: TextStyle(
                color: _typeColor(item.transactionType),
                fontWeight: FontWeight.bold)),
        onTap: () => _showDetails(item),
      );

  void _showDetails(LoyaltyTransactionListItem item) {
    showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(_typeLabel(item.transactionType)),
        content: SingleChildScrollView(
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            _detail('العميل', item.customerName ?? 'غير معروف'),
            _detail('كود العميل', item.customerCode ?? 'غير متوفر'),
            _detail(
                'تاريخ الحركة',
                DateFormat('yyyy-MM-dd HH:mm:ss')
                    .format(item.createdAt.toLocal())),
            _detail('النقاط', _signedPoints(item.points)),
            _detail('الرصيد قبل الحركة', _formatNumber(item.balanceBefore)),
            _detail('الرصيد بعد الحركة', _formatNumber(item.balanceAfter)),
            _detail('رقم الطلب', item.orderId?.toString() ?? 'غير مرتبط'),
            _detail('رقم المكافأة', item.rewardId?.toString() ?? 'غير مرتبط'),
            _detail('المصدر', _sourceLabel(item.source)),
            _detail('الملاحظات', _notesLabel(item.notes)),
          ]),
        ),
        actions: [
          TextButton(onPressed: AppNavigation.back, child: const Text('إغلاق'))
        ],
      ),
    );
  }

  Widget _detail(String label, String value) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text('$label: $value'),
      );

  String _typeLabel(String type) => switch (type) {
        'Earn' => 'كسب نقاط',
        'Redeem' => 'استبدال نقاط',
        'Reversal' => 'عكس نقاط',
        'Adjust' => 'تعديل نقاط',
        _ => 'حركة ولاء أخرى',
      };

  String _sourceLabel(String source) => switch (source.trim()) {
        '' => 'غير مرتبط',
        'Not linked' => 'غير مرتبط',
        'PieceOrderCancellation' => 'إلغاء طلب قطعة',
        'PieceOrderCompletion' => 'إكمال طلب قطعة',
        'PiecePurchase' => 'شراء قطعة',
        'PieceReferral' => 'إحالة قطعة',
        'OrderCompletion' => 'إكمال الطلب',
        'OrderCancellation' => 'إلغاء الطلب',
        'OrderLoyaltyCredit' => 'رصيد ولاء للطلب',
        'ReferralReward' => 'مكافأة إحالة',
        'ManualAdjustment' => 'تعديل يدوي',
        'System' => 'النظام',
        _ => _cleanTechnicalText(source),
      };

  String _notesLabel(String? notes) {
    final value = notes?.trim();
    if (value == null || value.isEmpty) return 'لا توجد ملاحظات';
    if (value == 'Not linked') return 'غير مرتبط';

    // Keep the human-readable part and hide technical reconciliation metadata.
    final displayValue = value.split('|').first.trim();

    final reversal = RegExp(
      r'^Reversal for order\s+(.+)$',
      caseSensitive: false,
    ).firstMatch(displayValue);
    if (reversal != null) {
      return 'عكس حركة مرتبطة بالطلب ${reversal.group(1)}';
    }

    final purchase = RegExp(
      r'^(?:Purchase points for order|Loyalty credit for order)\s+(.+?)(?:\s+\((\d+)\s+pieces?\))?$',
      caseSensitive: false,
    ).firstMatch(displayValue);
    if (purchase != null) {
      final quantity = purchase.group(2);
      return 'نقاط مرتبطة بالطلب ${purchase.group(1)}'
          '${quantity == null ? '' : ' (عدد القطع: $quantity)'}';
    }

    final referral = RegExp(
      r'^Referral points for order\s+(.+?)(?:\s+\((\d+)\s+generation\))?$',
      caseSensitive: false,
    ).firstMatch(displayValue);
    if (referral != null) {
      final generation = referral.group(2);
      return 'نقاط إحالة مرتبطة بالطلب ${referral.group(1)}'
          '${generation == null ? '' : ' (المستوى: $generation)'}';
    }

    final source = _sourceLabel(displayValue);
    return source == displayValue ? _cleanTechnicalText(displayValue) : source;
  }

  String _cleanTechnicalText(String value) {
    final normalized = value.trim();
    if (normalized.isEmpty || normalized == 'Not linked') return 'غير مرتبط';
    return normalized
        .replaceAll('PieceOrderCancellation', 'إلغاء طلب قطعة')
        .replaceAll('PieceOrderCompletion', 'إكمال طلب قطعة')
        .replaceAll('PiecePurchase', 'شراء قطعة')
        .replaceAll('PieceReferral', 'إحالة قطعة')
        .replaceAll('OrderCancellation', 'إلغاء الطلب')
        .replaceAll('OrderCompletion', 'إكمال الطلب')
        .replaceAll('OrderLoyaltyCredit', 'رصيد ولاء للطلب')
        .replaceAll('ReferralReward', 'مكافأة إحالة')
        .replaceAll('ManualAdjustment', 'تعديل يدوي')
        .replaceAll('Earn', 'كسب نقاط')
        .replaceAll('Redeem', 'استبدال نقاط')
        .replaceAll('Reversal', 'عكس نقاط')
        .replaceAll('Adjust', 'تعديل نقاط');
  }

  IconData _typeIcon(String type) => switch (type) {
        'Earn' => Icons.add_circle_outline,
        'Redeem' => Icons.redeem_outlined,
        'Reversal' => Icons.undo_outlined,
        'Adjust' => Icons.tune_outlined,
        _ => Icons.timeline_outlined,
      };

  Color _typeColor(String type) => switch (type) {
        'Earn' => Colors.green,
        'Redeem' => UiPalette.primaryBlue,
        'Reversal' => Colors.red,
        'Adjust' => Colors.orange,
        _ => UiPalette.textSoft,
      };

  String _signedPoints(double value) =>
      value > 0 ? '+${_formatNumber(value)}' : _formatNumber(value);
  String _formatNumber(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);
  TextStyle get _sectionStyle => const TextStyle(
      color: UiPalette.textMain, fontSize: 16, fontWeight: FontWeight.bold);
}

class _CustomerSuggestions extends StatelessWidget {
  const _CustomerSuggestions({required this.future, required this.onSelected});
  final Future<List<LoyaltyCustomerSearchResult>> future;
  final ValueChanged<LoyaltyCustomerSearchResult> onSelected;

  @override
  Widget build(BuildContext context) =>
      FutureBuilder<List<LoyaltyCustomerSearchResult>>(
        future: future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const LinearProgressIndicator();
          }
          if (snapshot.hasError) {
            return const Padding(
                padding: EdgeInsets.only(top: 8),
                child: Text('تعذر تحميل اقتراحات العملاء.'));
          }
          final results =
              snapshot.data ?? const <LoyaltyCustomerSearchResult>[];
          if (results.isEmpty) {
            return const Padding(
                padding: EdgeInsets.only(top: 8),
                child: Text('لا يوجد عميل مطابق.'));
          }
          return Container(
            margin: const EdgeInsets.only(top: 4),
            decoration: BoxDecoration(
                color: UiPalette.softBlue,
                border: Border.all(color: UiPalette.borderSoft),
                borderRadius: BorderRadius.circular(6)),
            child: Column(
                children: results
                    .take(6)
                    .map((customer) => ListTile(
                          dense: true,
                          leading: const Icon(Icons.person_search_outlined),
                          title: Text(customer.customerName ?? 'عميل بدون اسم'),
                          subtitle: Text([
                            customer.customerCode,
                            customer.phoneNumber
                          ]
                              .whereType<String>()
                              .where((value) => value.isNotEmpty)
                              .join(' • ')),
                          onTap: () => onSelected(customer),
                        ))
                    .toList()),
          );
        },
      );
}

class _Panel extends StatelessWidget {
  const _Panel({required this.child});
  final Widget child;
  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
            color: UiPalette.surfaceCard,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: UiPalette.borderSoft)),
        child: child,
      );
}
