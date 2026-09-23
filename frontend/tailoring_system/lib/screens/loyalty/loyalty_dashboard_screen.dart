import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../core/app_routes.dart';
import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class LoyaltyDashboardScreen extends StatefulWidget {
  const LoyaltyDashboardScreen({super.key, this.repository});

  final LoyaltyRepository? repository;

  @override
  State<LoyaltyDashboardScreen> createState() => _LoyaltyDashboardScreenState();
}

class _LoyaltyDashboardScreenState extends State<LoyaltyDashboardScreen> {
  late final LoyaltyRepository _repository =
      widget.repository ?? LoyaltyRepository();
  final _searchController = TextEditingController();
  late Future<LoyaltyDashboardData> _future;
  Future<List<LoyaltyCustomerSearchResult>>? _suggestionsFuture;
  Timer? _suggestionTimer;
  int _searchVersion = 0;

  @override
  void initState() {
    super.initState();
    _future = _repository.getDashboard();
  }

  @override
  void dispose() {
    _suggestionTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    setState(() => _future = _repository.getDashboard());
    await _future;
  }

  void _onSearchChanged(String value) {
    final searchVersion = ++_searchVersion;
    _suggestionTimer?.cancel();
    final term = value.trim();
    if (term.length < 2) {
      setState(() => _suggestionsFuture = null);
      return;
    }
    _suggestionTimer = Timer(const Duration(milliseconds: 250), () {
      if (!mounted) return;
      final request = _repository.searchCustomers(term);
      setState(() {
        if (searchVersion == _searchVersion) {
          _suggestionsFuture = request;
        }
      });
    });
  }

  void _openCustomer(LoyaltyCustomerSearchResult customer) {
    _suggestionTimer?.cancel();
    _searchController.clear();
    setState(() {
      _searchVersion++;
      _suggestionsFuture = null;
    });
    AppNavigation.pushNamed(
      context,
      AppRoutes.loyaltyTransactions,
      arguments: customer.customerId,
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(title: const Text('لوحة الولاء والنقاط')),
        body: FutureBuilder<LoyaltyDashboardData>(
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
              onRefresh: _refresh,
              child: ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  _buildSearch(),
                  const SizedBox(height: 18),
                  _buildMetrics(data),
                  const SizedBox(height: 18),
                  _buildTopCustomers(data),
                  const SizedBox(height: 18),
                  _buildVipLevels(data.vipLevels),
                  const SizedBox(height: 18),
                  _buildRecentActivities(data.recentActivities),
                  const SizedBox(height: 18),
                  _buildProgramSummary(data.programSummary),
                ],
              ),
            );
          },
        ),
      );

  Widget _buildSearch() => _Panel(
        child: Column(
          children: [
            TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              onChanged: _onSearchChanged,
              decoration: const InputDecoration(
                labelText: 'بحث باسم العميل أو كود العميل',
                prefixIcon: Icon(Icons.search),
              ),
            ),
            if (_suggestionsFuture != null)
              _CustomerSuggestions(
                future: _suggestionsFuture!,
                onSelected: _openCustomer,
              ),
          ],
        ),
      );

  Widget _buildMetrics(LoyaltyDashboardData data) {
    final metrics = [
      _Metric('عدد حسابات الولاء', data.accountCount,
          Icons.account_balance_wallet_outlined),
      _Metric('إجمالي النقاط الحالية', data.currentPointsTotal,
          Icons.account_balance_outlined),
      _Metric('إجمالي النقاط المكتسبة', data.earnedPointsTotal,
          Icons.add_circle_outline),
      _Metric('إجمالي النقاط المستبدلة', data.redeemedPointsTotal,
          Icons.redeem_outlined),
      _Metric('إجمالي النقاط المعكوسة', data.reversedPointsTotal,
          Icons.undo_outlined),
      _Metric('إجمالي النقاط المعدلة', data.adjustedPointsTotal,
          Icons.tune_outlined),
      _Metric('عدد العملاء النشطين', data.activeCustomerCount,
          Icons.people_alt_outlined),
    ];
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: metrics
          .map((metric) => SizedBox(
                width: 215,
                child: _Panel(
                  child: Row(
                    children: [
                      Icon(metric.icon, color: UiPalette.primaryBlue),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(metric.label,
                                style: TextStyle(
                                    color: UiPalette.textSoft, fontSize: 12)),
                            const SizedBox(height: 5),
                            Text(_formatNumber(metric.value),
                                style: TextStyle(
                                    color: UiPalette.textMain,
                                    fontSize: 20,
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

  Widget _buildTopCustomers(LoyaltyDashboardData data) => LayoutBuilder(
        builder: (context, constraints) {
          final panels = [
            _rankingPanel(
                'أعلى العملاء امتلاكاً للنقاط', data.topCustomersByBalance),
            _rankingPanel(
                'أكثر العملاء كسباً للنقاط', data.topCustomersByEarnedPoints),
          ];
          if (constraints.maxWidth >= 800) {
            return Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: panels
                    .map((panel) => Expanded(
                        child: Padding(
                            padding: const EdgeInsetsDirectional.only(end: 12),
                            child: panel)))
                    .toList());
          }
          return Column(
              children: panels
                  .map((panel) => Padding(
                      padding: const EdgeInsets.only(bottom: 12), child: panel))
                  .toList());
        },
      );

  Widget _rankingPanel(String title, List<LoyaltyTopCustomer> customers) =>
      _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title, style: _sectionStyle),
          const SizedBox(height: 10),
          if (customers.isEmpty) const Text('لا توجد بيانات.'),
          ...customers.asMap().entries.map((entry) => ListTile(
                contentPadding: EdgeInsets.zero,
                dense: true,
                leading:
                    CircleAvatar(radius: 14, child: Text('${entry.key + 1}')),
                title: Text(entry.value.customerName ?? 'عميل بدون اسم'),
                subtitle: Text(entry.value.customerCode ?? 'بدون كود'),
                trailing: Text('${_formatNumber(entry.value.points)} نقطة'),
              )),
        ]),
      );

  Widget _buildVipLevels(List<LoyaltyVipSummary> levels) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('مستويات كبار العملاء', style: _sectionStyle),
          const SizedBox(height: 10),
          if (levels.isEmpty) const Text('لا توجد مستويات.'),
          ...levels.map((level) => ListTile(
                contentPadding: EdgeInsets.zero,
                dense: true,
                leading: const Icon(Icons.workspace_premium_outlined),
                title: Text(level.displayName ?? 'مستوى بدون اسم'),
                subtitle: Text(
                    'الحد الأدنى: ${_formatNumber(level.minimumPoints)} نقطة'),
                trailing: Text('${level.customerCount} عميل'),
              )),
        ]),
      );

  Widget _buildRecentActivities(List<LoyaltyActivity> activities) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('آخر نشاطات الولاء', style: _sectionStyle),
          const SizedBox(height: 10),
          if (activities.isEmpty) const Text('لا توجد حركات ولاء.'),
          ...activities.map((activity) => ListTile(
                contentPadding: EdgeInsets.zero,
                dense: true,
                leading: Icon(_activityIcon(activity.transactionType),
                    color: UiPalette.primaryBlue),
                title: Text(_activityTitle(activity.transactionType)),
                subtitle: Text(
                    '${activity.customerName ?? 'عميل بدون اسم'} • ${DateFormat('yyyy-MM-dd HH:mm').format(activity.createdAt.toLocal())}'),
                trailing: Text('${_formatNumber(activity.points)} نقطة'),
              )),
        ]),
      );

  Widget _buildProgramSummary(LoyaltyProgramSummary summary) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('ملخص برنامج الولاء', style: _sectionStyle),
          const SizedBox(height: 12),
          Wrap(spacing: 24, runSpacing: 14, children: [
            _SummaryValue('عدد الحسابات', summary.accountCount),
            _SummaryValue('عدد الحركات', summary.transactionCount),
            _SummaryValue('عمليات الاستبدال', summary.redemptionCount),
            _SummaryValue('الرصيد الحالي', summary.currentPointsTotal),
            _SummaryValue('عمليات كسب النقاط', summary.earnCount),
            _SummaryValue('عمليات الاستبدال', summary.redeemCount),
            _SummaryValue('عمليات العكس', summary.reversalCount),
            _SummaryValue('عمليات التعديل', summary.adjustCount),
          ]),
        ]),
      );

  IconData _activityIcon(String type) => switch (type) {
        'Earn' => Icons.add_circle_outline,
        'Redeem' => Icons.redeem_outlined,
        'Reversal' => Icons.undo_outlined,
        'Adjust' => Icons.tune_outlined,
        _ => Icons.timeline_outlined,
      };

  String _activityTitle(String type) => switch (type) {
        'Earn' => 'كسب نقاط',
        'Redeem' => 'استبدال نقاط',
        'Reversal' => 'عكس نقاط',
        'Adjust' => 'تعديل نقاط',
        _ => 'حركة ولاء',
      };

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
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text('تعذر تحميل اقتراحات العملاء.'),
              ),
            );
          }
          final results =
              snapshot.data ?? const <LoyaltyCustomerSearchResult>[];
          if (results.isEmpty) {
            return const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text('لا يوجد عميل مطابق.'),
              ),
            );
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
                            if (customer.customerCode?.isNotEmpty == true)
                              customer.customerCode!,
                            if (customer.phoneNumber?.isNotEmpty == true)
                              customer.phoneNumber!,
                          ].join(' • ')),
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

class _Metric {
  const _Metric(this.label, this.value, this.icon);
  final String label;
  final num value;
  final IconData icon;
}

class _SummaryValue extends StatelessWidget {
  const _SummaryValue(this.label, this.value);
  final String label;
  final Object value;
  @override
  Widget build(BuildContext context) =>
      Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: TextStyle(color: UiPalette.textSoft)),
        const SizedBox(height: 4),
        Text('$value',
            style: TextStyle(
                color: UiPalette.primaryBlue,
                fontSize: 18,
                fontWeight: FontWeight.bold)),
      ]);
}
