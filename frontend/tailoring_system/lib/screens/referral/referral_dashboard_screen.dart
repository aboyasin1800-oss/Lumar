import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../core/app_routes.dart';
import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/referral_models.dart';
import '../../repositories/referral_repository.dart';

class ReferralDashboardScreen extends StatefulWidget {
  const ReferralDashboardScreen({super.key, this.repository});

  final ReferralRepository? repository;

  @override
  State<ReferralDashboardScreen> createState() =>
      _ReferralDashboardScreenState();
}

class _ReferralDashboardScreenState extends State<ReferralDashboardScreen> {
  late final ReferralRepository _repository =
      widget.repository ?? ReferralRepository();
  final _searchController = TextEditingController();
  late Future<ReferralDashboard> _future;
  Future<List<ReferralDashboardSearchResult>>? _searchFuture;

  @override
  void initState() {
    super.initState();
    _future = _repository.getDashboard();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    setState(() => _future = _repository.getDashboard());
    await _future;
  }

  void _search() {
    final query = _searchController.text.trim();
    setState(() {
      _searchFuture = query.isEmpty ? null : _repository.searchDashboard(query);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(title: const Text('لوحة الإحالات')),
      body: FutureBuilder<ReferralDashboard>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text(RlUiText.friendlyError(snapshot.error)));
          }
          final dashboard = snapshot.data;
          if (dashboard == null) {
            return const Center(child: Text('لا توجد بيانات.'));
          }
          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                _buildSearch(context),
                const SizedBox(height: 20),
                _buildMetrics(dashboard),
                const SizedBox(height: 20),
                _buildHighlights(context, dashboard),
                const SizedBox(height: 20),
                _buildRecentEvents(dashboard.recentEvents),
                const SizedBox(height: 20),
                _buildTreeSummary(dashboard.treeSummary),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildSearch(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: _searchController,
          textInputAction: TextInputAction.search,
          onSubmitted: (_) => _search(),
          decoration: InputDecoration(
            labelText: 'بحث باسم العميل أو كود العميل أو كود الإحالة',
            prefixIcon: const Icon(Icons.search),
            suffixIcon: IconButton(
              tooltip: 'بحث',
              onPressed: _search,
              icon: const Icon(Icons.arrow_forward_rounded),
            ),
          ),
        ),
        if (_searchFuture != null)
          FutureBuilder<List<ReferralDashboardSearchResult>>(
            future: _searchFuture,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const LinearProgressIndicator();
              }
              if (snapshot.hasError) return const Text('تعذر تنفيذ البحث.');
              final results = snapshot.data ?? const [];
              if (results.isEmpty) {
                return const Padding(
                  padding: EdgeInsets.all(12),
                  child: Text('لا توجد نتائج مطابقة.'),
                );
              }
              return _Panel(
                child: Column(
                  children: results
                      .map((result) => ListTile(
                            leading: Icon(result.resultType == 'code'
                                ? Icons.qr_code_2
                                : Icons.person_search_outlined),
                            title: Text(result.customerName ?? 'عميل بدون اسم'),
                            subtitle: Text([
                              result.customerCode,
                              result.referralCode
                            ].whereType<String>().join(' | ')),
                            trailing: const Icon(Icons.chevron_left),
                            onTap: () {
                              if (result.resultType == 'code') {
                                AppNavigation.pushNamed(
                                    context, AppRoutes.referralCodes);
                              } else {
                                AppNavigation.pushNamed(
                                    context, AppRoutes.referralTree,
                                    arguments: result.customerId);
                              }
                            },
                          ))
                      .toList(),
                ),
              );
            },
          ),
      ],
    );
  }

  Widget _buildMetrics(ReferralDashboard dashboard) {
    final metrics = [
      _Metric('تسجيلات الإحالة', dashboard.totalRegistrations,
          Icons.how_to_reg_outlined),
      _Metric('العملاء المشاركون', dashboard.participatingCustomers,
          Icons.people_alt_outlined),
      _Metric('المكافآت الممنوحة', dashboard.rewardsGranted,
          Icons.card_giftcard_outlined),
      _Metric('عمليات عكس المكافآت', dashboard.rewardReversals,
          Icons.undo_outlined),
      _Metric('إجمالي نقاط الإحالات الممنوحة', dashboard.totalReferralPoints,
          Icons.stars_outlined),
    ];
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: metrics
          .map((metric) => SizedBox(
                width: 190,
                child: _Panel(
                    child: Row(children: [
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
                                fontSize: 22,
                                fontWeight: FontWeight.bold)),
                      ])),
                ])),
              ))
          .toList(),
    );
  }

  Widget _buildHighlights(BuildContext context, ReferralDashboard dashboard) {
    return LayoutBuilder(builder: (context, constraints) {
      final wide = constraints.maxWidth >= 900;
      final children = [
        _rankingPanel(
            'أكثر المحيلين نشاطاً',
            dashboard.topReferrers
                .map((item) => _RankRow(item.customerName ?? 'عميل بدون اسم',
                    '${item.referralCount} إحالة'))
                .toList()),
        _rankingPanel(
            'أكثر العملاء استقبالاً للإحالات',
            dashboard.topReceivers
                .map((item) => _RankRow(item.customerName ?? 'عميل بدون اسم',
                    '${item.referralCount} إحالة'))
                .toList()),
        _codesPanel(dashboard.topCodes),
      ];
      return wide
          ? Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: children
                  .map((child) => Expanded(
                      child: Padding(
                          padding: const EdgeInsetsDirectional.only(end: 12),
                          child: child)))
                  .toList())
          : Column(
              children: children
                  .map((child) => Padding(
                      padding: const EdgeInsets.only(bottom: 12), child: child))
                  .toList());
    });
  }

  Widget _rankingPanel(String title, List<_RankRow> rows) => _Panel(
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(title,
            style: TextStyle(
                color: UiPalette.textMain,
                fontSize: 16,
                fontWeight: FontWeight.bold)),
        const SizedBox(height: 10),
        if (rows.isEmpty) const Text('لا توجد بيانات.'),
        ...rows.asMap().entries.map((entry) => ListTile(
            contentPadding: EdgeInsets.zero,
            dense: true,
            leading: CircleAvatar(radius: 14, child: Text('${entry.key + 1}')),
            title: Text(entry.value.name),
            trailing: Text(entry.value.value))),
      ]));

  Widget _codesPanel(List<ReferralDashboardCode> codes) => _Panel(
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('أكثر أكواد الإحالة استخداماً',
            style: TextStyle(
                color: UiPalette.textMain,
                fontSize: 16,
                fontWeight: FontWeight.bold)),
        const SizedBox(height: 10),
        if (codes.isEmpty) const Text('لا توجد أكواد.'),
        ...codes.map((code) => ListTile(
            contentPadding: EdgeInsets.zero,
            dense: true,
            title: Text(code.code),
            subtitle: Text(code.customerName ?? 'عميل بدون اسم'),
            trailing: Text('${code.usageCount} استخدام'))),
      ]));

  Widget _buildRecentEvents(List<ReferralDashboardEvent> events) => _Panel(
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('أحدث أحداث الإحالة',
            style: TextStyle(
                color: UiPalette.textMain,
                fontSize: 16,
                fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        if (events.isEmpty) const Text('لا توجد أحداث.'),
        ...events.map((event) => ListTile(
            contentPadding: EdgeInsets.zero,
            dense: true,
            leading: Icon(_eventIcon(event.transactionType),
                color: UiPalette.primaryBlue),
            title: Text(RlUiText.translate(event.transactionType,
                fallback: 'حدث إحالة')),
            subtitle: Text(
                '${event.referrerName ?? 'عميل'}${event.referredName == null ? '' : ' ← ${event.referredName}'}'),
            trailing: Text(
                DateFormat('MM/dd HH:mm').format(event.createdAt.toLocal())))),
      ]));

  Widget _buildTreeSummary(ReferralDashboardTreeSummary summary) => _Panel(
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('ملخص الشجرة',
            style: TextStyle(
                color: UiPalette.textMain,
                fontSize: 16,
                fontWeight: FontWeight.bold)),
        const SizedBox(height: 12),
        Wrap(spacing: 28, runSpacing: 12, children: [
          _SummaryValue('عدد الجذور', summary.rootCount),
          _SummaryValue('أكبر عمق', summary.maxDepth),
          _SummaryValue('أكبر عدد تابعين مباشرين', summary.maxDirectReferrals),
        ]),
      ]));

  IconData _eventIcon(String type) => switch (type) {
        'Registration' => Icons.person_add_alt_1_outlined,
        'RewardGranted' => Icons.card_giftcard_outlined,
        'RewardReversal' => Icons.undo_outlined,
        _ => Icons.timeline_outlined,
      };

  String _formatNumber(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);
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
      child: child);
}

class _Metric {
  const _Metric(this.label, this.value, this.icon);
  final String label;
  final num value;
  final IconData icon;
}

class _RankRow {
  const _RankRow(this.name, this.value);
  final String name;
  final String value;
}

class _SummaryValue extends StatelessWidget {
  const _SummaryValue(this.label, this.value);
  final String label;
  final int value;
  @override
  Widget build(BuildContext context) =>
      Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: TextStyle(color: UiPalette.textSoft)),
        const SizedBox(height: 4),
        Text('$value',
            style: TextStyle(
                color: UiPalette.primaryBlue,
                fontSize: 21,
                fontWeight: FontWeight.bold))
      ]);
}
