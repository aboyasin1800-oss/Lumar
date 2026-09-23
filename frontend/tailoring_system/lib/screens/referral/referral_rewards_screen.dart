import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/referral_models.dart';
import '../../repositories/referral_repository.dart';

class ReferralRewardsScreen extends StatefulWidget {
  const ReferralRewardsScreen({super.key, this.repository});

  final ReferralRepository? repository;

  @override
  State<ReferralRewardsScreen> createState() => _ReferralRewardsScreenState();
}

class _ReferralRewardsScreenState extends State<ReferralRewardsScreen> {
  late final ReferralRepository _repository =
      widget.repository ?? ReferralRepository();
  final _searchController = TextEditingController();
  String? _transactionType;
  DateTime? _from;
  DateTime? _to;
  late Future<ReferralRewardsData> _future;
  Future<List<ReferralDashboardSearchResult>>? _suggestionsFuture;
  Timer? _suggestionTimer;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _suggestionTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<ReferralRewardsData> _load() => _repository.getReferralRewardsScreen(
        transactionType: _transactionType,
        from: _from,
        to: _to,
        search: _searchController.text,
      );

  void _applyFilters() => setState(() => _future = _load());

  void _onSearchChanged(String value) {
    _suggestionTimer?.cancel();
    final query = value.trim();
    if (query.length < 2) {
      setState(() => _suggestionsFuture = null);
      return;
    }
    _suggestionTimer = Timer(const Duration(milliseconds: 250), () {
      if (!mounted) return;
      setState(() => _suggestionsFuture = _repository.searchDashboard(query));
    });
  }

  void _chooseSuggestion(ReferralDashboardSearchResult suggestion) {
    _searchController.text = suggestion.referralCode ??
        suggestion.customerCode ??
        suggestion.customerName ??
        '';
    _searchController.selection = TextSelection.collapsed(
      offset: _searchController.text.length,
    );
    setState(() => _suggestionsFuture = null);
    _applyFilters();
  }

  Future<void> _pickDate(bool isFrom) async {
    final value = await showDatePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime.now().add(const Duration(days: 1)),
      initialDate: (isFrom ? _from : _to) ?? DateTime.now(),
    );
    if (value == null) return;
    setState(() {
      if (isFrom) {
        _from = value;
      } else {
        _to = value;
      }
      _future = _load();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(title: const Text('مكافآت الإحالات')),
      body: FutureBuilder<ReferralRewardsData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text(RlUiText.friendlyError(snapshot.error)));
          }
          final data = snapshot.data;
          if (data == null) return const Center(child: Text('لا توجد بيانات.'));
          return RefreshIndicator(
            onRefresh: () async {
              setState(() => _future = _load());
              await _future;
            },
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                _buildFilters(),
                const SizedBox(height: 18),
                _buildSummary(data),
                const SizedBox(height: 18),
                _buildBeneficiaries(data.topBeneficiaries),
                const SizedBox(height: 18),
                _buildEvents(data.events),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildFilters() => _Panel(
        child: Wrap(
          spacing: 12,
          runSpacing: 12,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            SizedBox(
              width: 300,
              child: Column(
                children: [
                  TextField(
                    controller: _searchController,
                    textInputAction: TextInputAction.search,
                    onChanged: _onSearchChanged,
                    onSubmitted: (_) => _applyFilters(),
                    decoration: const InputDecoration(
                      labelText: 'بحث باسم العميل أو كود العميل',
                      prefixIcon: Icon(Icons.search),
                    ),
                  ),
                  if (_suggestionsFuture != null)
                    _SuggestionList(
                      future: _suggestionsFuture!,
                      onSelected: _chooseSuggestion,
                    ),
                ],
              ),
            ),
            SizedBox(
              width: 190,
              child: DropdownButtonFormField<String?>(
                initialValue: _transactionType,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'نوع المكافأة'),
                items: const [
                  DropdownMenuItem<String?>(
                      value: null, child: Text('كل الأنواع')),
                  DropdownMenuItem<String?>(
                      value: 'RewardGranted', child: Text('منح مكافأة إحالة')),
                  DropdownMenuItem<String?>(
                      value: 'RewardReversal', child: Text('عكس مكافأة إحالة')),
                ],
                onChanged: (value) {
                  _transactionType = value;
                  _applyFilters();
                },
              ),
            ),
            OutlinedButton.icon(
              onPressed: () => _pickDate(true),
              icon: const Icon(Icons.calendar_today_outlined),
              label: Text(_from == null ? 'من تاريخ' : _formatDate(_from!)),
            ),
            OutlinedButton.icon(
              onPressed: () => _pickDate(false),
              icon: const Icon(Icons.event_outlined),
              label: Text(_to == null ? 'إلى تاريخ' : _formatDate(_to!)),
            ),
            IconButton(
              tooltip: 'تطبيق الفلاتر',
              onPressed: _applyFilters,
              icon: const Icon(Icons.filter_alt_outlined),
            ),
          ],
        ),
      );

  Widget _buildSummary(ReferralRewardsData data) {
    final items = [
      _Metric(
          'المكافآت الممنوحة', data.grantedCount, Icons.card_giftcard_outlined),
      _Metric('المكافآت المعكوسة', data.reversalCount, Icons.undo_outlined),
      _Metric('إجمالي نقاط الإحالات الممنوحة', data.totalGrantedPoints,
          Icons.stars_outlined),
      _Metric(
          'عدد المستفيدين', data.beneficiaryCount, Icons.people_alt_outlined),
    ];
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: items
          .map((item) => SizedBox(
                width: 215,
                child: _Panel(
                  child: Row(
                    children: [
                      Icon(item.icon, color: UiPalette.primaryBlue),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(item.label,
                                style: TextStyle(
                                    color: UiPalette.textSoft, fontSize: 12)),
                            const SizedBox(height: 5),
                            Text(_formatNumber(item.value),
                                style: TextStyle(
                                    color: UiPalette.textMain,
                                    fontSize: 22,
                                    fontWeight: FontWeight.bold)),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ))
          .toList(),
    );
  }

  Widget _buildBeneficiaries(List<ReferralRewardBeneficiary> beneficiaries) =>
      _Panel(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('أعلى المستفيدين من الإحالة', style: _sectionStyle),
            const SizedBox(height: 10),
            if (beneficiaries.isEmpty) const Text('لا توجد مكافآت ممنوحة.'),
            ...beneficiaries.asMap().entries.map(
                  (entry) => Material(
                    color: Colors.transparent,
                    child: ListTile(
                      contentPadding: EdgeInsets.zero,
                      dense: true,
                      leading: CircleAvatar(
                          radius: 14, child: Text('${entry.key + 1}')),
                      title: Text(entry.value.customerName ?? 'عميل بدون اسم'),
                      subtitle:
                          Text(entry.value.customerCode ?? 'بدون كود عميل'),
                      trailing: Text(
                          '${_formatNumber(entry.value.totalPoints)} نقطة'),
                    ),
                  ),
                ),
          ],
        ),
      );

  Widget _buildEvents(List<ReferralRewardEvent> events) => _Panel(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('المكافآت الأخيرة', style: _sectionStyle),
            const SizedBox(height: 10),
            if (events.isEmpty) const Text('لا توجد مكافآت مطابقة.'),
            ...events.map(
              (event) => Material(
                color: Colors.transparent,
                child: ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(_eventIcon(event.transactionType),
                      color: _eventColor(event.transactionType)),
                  title: Text(_eventTitle(event.transactionType)),
                  subtitle: Text(
                      '${event.beneficiaryName ?? 'عميل بدون اسم'} • ${_formatDateTime(event.createdAt)}'),
                  trailing: Text('${_formatNumber(event.loyaltyPoints)} نقطة'),
                  onTap: () => _showDetails(event),
                ),
              ),
            ),
          ],
        ),
      );

  void _showDetails(ReferralRewardEvent event) {
    showDialog<void>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(_eventTitle(event.transactionType)),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _detail('المستفيد', event.beneficiaryName ?? 'غير محدد'),
              _detail('كود العميل', event.beneficiaryCode ?? 'غير متوفر'),
              _detail('العميل الذي سبب المكافأة',
                  event.referredCustomerName ?? 'غير محدد'),
              _detail('نوع الحدث', _eventTitle(event.transactionType)),
              _detail('النقاط', _formatNumber(event.loyaltyPoints)),
              _detail('تاريخ العملية', _formatDateTime(event.createdAt)),
              _detail('رقم الطلب', event.orderId?.toString() ?? 'غير متوفر'),
              _detail('الملاحظات', event.notes ?? 'لا توجد ملاحظات'),
            ],
          ),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('إغلاق')),
        ],
      ),
    );
  }

  Widget _detail(String label, String value) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text('$label: $value'),
      );

  IconData _eventIcon(String type) => type == 'RewardReversal'
      ? Icons.undo_outlined
      : Icons.card_giftcard_outlined;

  Color _eventColor(String type) => type == 'RewardReversal'
      ? const Color(0xFFE57373)
      : const Color(0xFF81C784);

  String _eventTitle(String type) =>
      type == 'RewardReversal' ? 'عكس مكافأة إحالة' : 'منح مكافأة إحالة';

  String _formatDate(DateTime value) =>
      DateFormat('yyyy-MM-dd').format(value.toLocal());

  String _formatDateTime(DateTime value) =>
      DateFormat('yyyy-MM-dd HH:mm').format(value.toLocal());

  String _formatNumber(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);

  TextStyle get _sectionStyle => const TextStyle(
      color: UiPalette.textMain, fontSize: 16, fontWeight: FontWeight.bold);
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
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: child,
      );
}

class _Metric {
  const _Metric(this.label, this.value, this.icon);

  final String label;
  final num value;
  final IconData icon;
}

class _SuggestionList extends StatelessWidget {
  const _SuggestionList({required this.future, required this.onSelected});

  final Future<List<ReferralDashboardSearchResult>> future;
  final ValueChanged<ReferralDashboardSearchResult> onSelected;

  @override
  Widget build(BuildContext context) =>
      FutureBuilder<List<ReferralDashboardSearchResult>>(
        future: future,
        builder: (context, snapshot) {
          final results =
              snapshot.data ?? const <ReferralDashboardSearchResult>[];
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const LinearProgressIndicator();
          }
          if (results.isEmpty) return const SizedBox.shrink();
          return Container(
            margin: const EdgeInsets.only(top: 4),
            decoration: BoxDecoration(
              color: UiPalette.softBlue,
              border: Border.all(color: UiPalette.borderSoft),
              borderRadius: BorderRadius.circular(6),
            ),
            child: Column(
              children: results.take(6).map((result) {
                final secondary = [
                  result.customerCode,
                  result.referralCode,
                ].whereType<String>().join(' | ');
                return ListTile(
                  dense: true,
                  leading: Icon(
                    result.resultType == 'code'
                        ? Icons.qr_code_2
                        : Icons.person_search_outlined,
                  ),
                  title: Text(result.customerName ?? 'عميل بدون اسم'),
                  subtitle: secondary.isEmpty ? null : Text(secondary),
                  onTap: () => onSelected(result),
                );
              }).toList(),
            ),
          );
        },
      );
}
