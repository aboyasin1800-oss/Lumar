import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class LoyaltyRewardsScreen extends StatefulWidget {
  const LoyaltyRewardsScreen({super.key, this.repository});

  final LoyaltyRepository? repository;

  @override
  State<LoyaltyRewardsScreen> createState() => _LoyaltyRewardsScreenState();
}

class _LoyaltyRewardsScreenState extends State<LoyaltyRewardsScreen> {
  late final LoyaltyRepository _repository =
      widget.repository ?? LoyaltyRepository();
  final _searchController = TextEditingController();
  Timer? _searchTimer;
  late Future<LoyaltyRewardsScreenData> _future;
  String? _rewardType;
  DateTime? _from;
  DateTime? _to;
  int? _customerId;
  Future<List<LoyaltyCustomerSearchResult>>? _suggestionsFuture;
  int _searchVersion = 0;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<LoyaltyRewardsScreenData> _load() => _repository.getRewardsScreen(
        search: _searchController.text,
        rewardType: _rewardType,
        from: _from,
        to: _to,
        customerId: _customerId,
      );

  void _reload() {
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
        _customerId = null;
      });
    });
  }

  void _selectCustomer(LoyaltyCustomerSearchResult customer) {
    _searchTimer?.cancel();
    _searchVersion++;
    _searchController.text =
        customer.customerName ?? customer.customerCode ?? '';
    setState(() {
      _customerId = customer.customerId;
      _suggestionsFuture = null;
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
      _rewardType = null;
      _from = null;
      _to = null;
      _customerId = null;
      _suggestionsFuture = null;
      _future = _load();
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(title: const Text('مكافآت الولاء')),
        body: FutureBuilder<LoyaltyRewardsScreenData>(
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
            if (data == null)
              return const Center(child: Text('لا توجد بيانات.'));
            final rewardTypes =
                data.rewards.map((item) => item.source).toSet().toList();
            return RefreshIndicator(
              onRefresh: () async => _reload(),
              child: ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  _buildFilters(rewardTypes),
                  const SizedBox(height: 16),
                  _buildSummary(data.summary),
                  const SizedBox(height: 16),
                  _buildHighlights(data),
                  const SizedBox(height: 16),
                  _buildRewards(data.rewards),
                ],
              ),
            );
          },
        ),
      );

  Widget _buildFilters(List<String> rewardTypes) => _Panel(
        child:
            Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          TextField(
            controller: _searchController,
            onChanged: _onSearchChanged,
            onSubmitted: (_) => _reload(),
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              labelText: 'بحث باسم العميل أو كود العميل',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: IconButton(
                onPressed: _reload,
                icon: const Icon(Icons.arrow_forward),
                tooltip: 'بحث',
              ),
            ),
          ),
          if (_suggestionsFuture != null)
            _CustomerSuggestions(
                future: _suggestionsFuture!, onSelected: _selectCustomer),
          const SizedBox(height: 12),
          Wrap(spacing: 10, runSpacing: 10, children: [
            SizedBox(
              width: 230,
              child: DropdownButtonFormField<String>(
                initialValue:
                    rewardTypes.contains(_rewardType) ? _rewardType : null,
                decoration:
                    const InputDecoration(labelText: 'نوع المكافأة الفعلي'),
                items: rewardTypes
                    .map((type) => DropdownMenuItem(
                        value: type, child: Text(_sourceLabel(type))))
                    .toList(),
                onChanged: (value) => setState(() {
                  _rewardType = value;
                  _future = _load();
                }),
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

  Widget _buildSummary(LoyaltyRewardsSummary summary) => Wrap(
        spacing: 12,
        runSpacing: 12,
        children: [
          _summaryCard('المكافآت الممنوحة', summary.rewardCount,
              Icons.card_giftcard_outlined),
          _summaryCard('إجمالي النقاط الممنوحة', summary.grantedPointsTotal,
              Icons.add_circle_outline),
          _summaryCard('العملاء المستفيدون', summary.beneficiaryCustomerCount,
              Icons.people_alt_outlined),
          _summaryCard('متوسط نقاط المكافأة', summary.averageRewardPoints,
              Icons.analytics_outlined),
          _summaryCard('أعلى مكافأة', summary.largestRewardPoints,
              Icons.workspace_premium_outlined),
        ],
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
                      style: const TextStyle(color: UiPalette.textSoft)),
                  const SizedBox(height: 4),
                  Text(_formatNumber(value),
                      style: const TextStyle(
                          fontSize: 20, fontWeight: FontWeight.bold)),
                ])),
          ]),
        ),
      );

  Widget _buildHighlights(LoyaltyRewardsScreenData data) => LayoutBuilder(
        builder: (context, constraints) {
          final width = constraints.maxWidth > 900
              ? (constraints.maxWidth - 12) / 2
              : constraints.maxWidth;
          return Wrap(spacing: 12, runSpacing: 12, children: [
            SizedBox(
                width: width,
                child: _Panel(child: _buildTopCustomers(data.topCustomers))),
            SizedBox(
                width: width,
                child:
                    _Panel(child: _buildLargestRewards(data.largestRewards))),
          ]);
        },
      );

  Widget _buildTopCustomers(List<LoyaltyRewardCustomer> customers) => _section(
        'أكثر العملاء حصولاً على المكافآت',
        customers.isEmpty
            ? const Text('لا توجد بيانات.')
            : Column(
                children: customers.asMap().entries.map((entry) {
                final item = entry.value;
                return ListTile(
                  dense: true,
                  leading: CircleAvatar(child: Text('${entry.key + 1}')),
                  title: Text(item.customerName ?? 'عميل ${item.customerId}'),
                  subtitle: Text(item.customerCode ?? 'بدون كود'),
                  trailing: Text('${_formatNumber(item.grantedPoints)} نقطة'),
                );
              }).toList()),
      );

  Widget _buildLargestRewards(List<LoyaltyRewardItem> rewards) => _section(
        'أكبر المكافآت الممنوحة',
        rewards.isEmpty
            ? const Text('لا توجد بيانات.')
            : Column(
                children: rewards
                    .map((item) => ListTile(
                          dense: true,
                          leading: const Icon(Icons.stars_outlined),
                          title: Text('${_formatNumber(item.points)} نقطة'),
                          subtitle: Text(
                              item.customerName ?? 'عميل ${item.customerId}'),
                          trailing: Text(
                              DateFormat('yyyy-MM-dd').format(item.createdAt)),
                          onTap: () => _showDetails(item),
                        ))
                    .toList()),
      );

  Widget _buildRewards(List<LoyaltyRewardItem> rewards) => _Panel(
        child: _section(
          'سجل المكافآت الفعلية',
          rewards.isEmpty
              ? const Text('لا توجد مكافآت مطابقة للفلاتر.')
              : Column(
                  children: rewards
                      .map((item) => ListTile(
                            contentPadding: EdgeInsets.zero,
                            leading: Icon(
                                item.isReversed
                                    ? Icons.history_outlined
                                    : Icons.card_giftcard_outlined,
                                color: item.isReversed
                                    ? UiPalette.textSoft
                                    : UiPalette.primaryBlue),
                            title: Text(
                                item.customerName ?? 'عميل ${item.customerId}'),
                            subtitle: Text(
                                '${_formatNumber(item.points)} نقطة • ${_sourceLabel(item.source)}\n${DateFormat('yyyy-MM-dd HH:mm').format(item.createdAt)}'),
                            isThreeLine: true,
                            trailing:
                                Text(item.isReversed ? 'معكوسة' : 'فعالة'),
                            onTap: () => _showDetails(item),
                          ))
                      .toList()),
        ),
      );

  Widget _section(String title, Widget child) =>
      Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(title,
            style: const TextStyle(fontSize: 17, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        child,
      ]);

  Future<void> _showDetails(LoyaltyRewardItem item) => showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('تفاصيل مكافأة الولاء'),
          content: SingleChildScrollView(
              child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                _detail('العميل', item.customerName ?? 'غير مسجل'),
                _detail('كود العميل', item.customerCode ?? 'غير مسجل'),
                _detail('النقاط', '${_formatNumber(item.points)} نقطة'),
                _detail('نوع المكافأة', _sourceLabel(item.source)),
                _detail('سبب المكافأة', _reasonLabel(item)),
                _detail('رقم الطلب المرتبط',
                    item.orderId?.toString() ?? 'غير مرتبط'),
                _detail('تاريخ العملية',
                    DateFormat('yyyy-MM-dd HH:mm').format(item.createdAt)),
                _detail('الرصيد بعد الإضافة',
                    '${_formatNumber(item.balanceAfter)} نقطة'),
                _detail('الحالة', item.isReversed ? 'معكوسة' : 'فعالة'),
              ])),
          actions: [
            TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('إغلاق'))
          ],
        ),
      );

  Widget _detail(String label, String value) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text('$label: $value'),
      );

  String _sourceLabel(String source) =>
      RlUiText.translate(source, fallback: 'نوع المكافأة الفعلي');

  String _reasonLabel(LoyaltyRewardItem item) {
    final notes = item.notes;
    if (notes == null || notes.trim().isEmpty) return 'غير مسجل';
    if (notes.startsWith('Earn points from order'))
      return 'نقاط مكتسبة من الطلب';
    if (notes.startsWith('Purchase points for order')) {
      return 'نقاط شراء للطلب${item.orderId == null ? '' : ' رقم ${item.orderId}'}';
    }
    return _sourceLabel(item.source);
  }

  String _formatNumber(num value) => value == value.roundToDouble()
      ? value.toInt().toString()
      : value.toStringAsFixed(2);
}

class _Panel extends StatelessWidget {
  const _Panel({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(padding: const EdgeInsets.all(16), child: child),
      );
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
          final customers =
              snapshot.data ?? const <LoyaltyCustomerSearchResult>[];
          return Column(
              children: customers
                  .map((customer) => ListTile(
                        dense: true,
                        title: Text(customer.customerName ??
                            'عميل ${customer.customerId}'),
                        subtitle: Text(customer.customerCode ?? 'بدون كود'),
                        onTap: () => onSelected(customer),
                      ))
                  .toList());
        },
      );
}
