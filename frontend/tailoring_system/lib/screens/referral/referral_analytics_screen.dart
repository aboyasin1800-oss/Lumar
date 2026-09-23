import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/referral_models.dart';
import '../../repositories/referral_repository.dart';

class ReferralAnalyticsScreen extends StatefulWidget {
  const ReferralAnalyticsScreen({super.key, this.repository});

  final ReferralRepository? repository;

  @override
  State<ReferralAnalyticsScreen> createState() =>
      _ReferralAnalyticsScreenState();
}

class _ReferralAnalyticsScreenState extends State<ReferralAnalyticsScreen> {
  late final ReferralRepository _repository =
      widget.repository ?? ReferralRepository();
  final _searchController = TextEditingController();
  DateTime? _from;
  DateTime? _to;
  late Future<ReferralAnalyticsData> _future;
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

  Future<ReferralAnalyticsData> _load() => _repository.getReferralAnalytics(
        search: _searchController.text,
        from: _from,
        to: _to,
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

  Future<void> _refresh() async {
    setState(() => _future = _load());
    await _future;
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(title: const Text('تحليلات الإحالات')),
        body: FutureBuilder<ReferralAnalyticsData>(
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
                  _buildFilters(),
                  const SizedBox(height: 18),
                  _buildMetrics(data),
                  const SizedBox(height: 18),
                  _buildRankings(data),
                  const SizedBox(height: 18),
                  _buildQuality(data.quality),
                  const SizedBox(height: 18),
                  _buildActivity(data.activity),
                  const SizedBox(height: 18),
                  _buildTree(data.tree),
                ],
              ),
            );
          },
        ),
      );

  Widget _buildFilters() => _Panel(
        child: Wrap(
          spacing: 12,
          runSpacing: 12,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            SizedBox(
              width: 320,
              child: Column(
                children: [
                  TextField(
                    controller: _searchController,
                    textInputAction: TextInputAction.search,
                    onChanged: _onSearchChanged,
                    onSubmitted: (_) => _applyFilters(),
                    decoration: const InputDecoration(
                      labelText: 'بحث باسم العميل أو كوده أو كود الإحالة',
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

  Widget _buildMetrics(ReferralAnalyticsData data) {
    final metrics = [
      _Metric('إجمالي تسجيلات الإحالة', data.totalRegistrations,
          Icons.how_to_reg_outlined),
      _Metric('إجمالي العملاء المحالين', data.totalReferredCustomers,
          Icons.people_alt_outlined),
      _Metric('إجمالي مكافآت الإحالة', data.rewardsGranted,
          Icons.card_giftcard_outlined),
      _Metric('إجمالي نقاط الإحالات الممنوحة', data.totalReferralPoints,
          Icons.stars_outlined),
      _Metric('متوسط الإحالات لكل عميل', data.averageReferralsPerCustomer,
          Icons.functions_outlined),
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
                                    fontSize: 21,
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

  Widget _buildRankings(ReferralAnalyticsData data) => LayoutBuilder(
        builder: (context, constraints) {
          final panels = [
            _rankingPanel(
                'أفضل المحيلين',
                data.topReferrers
                    .map((item) => _RankRow(
                        item.customerName ?? 'عميل بدون اسم',
                        '${item.referralCount} إحالة'))
                    .toList()),
            _codePanel(data.topCodes),
            _rankingPanel(
                'أفضل العملاء حسب المكافآت',
                data.topRewardCustomers
                    .map((item) => _RankRow(
                        item.customerName ?? 'عميل بدون اسم',
                        '${_formatNumber(item.totalPoints)} نقطة'))
                    .toList()),
          ];
          if (constraints.maxWidth >= 900) {
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

  Widget _rankingPanel(String title, List<_RankRow> rows) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title, style: _sectionStyle),
          const SizedBox(height: 10),
          if (rows.isEmpty) const Text('لا توجد بيانات.'),
          ...rows.asMap().entries.map((entry) => ListTile(
                contentPadding: EdgeInsets.zero,
                dense: true,
                leading:
                    CircleAvatar(radius: 14, child: Text('${entry.key + 1}')),
                title: Text(entry.value.name),
                trailing: Text(entry.value.value),
              )),
        ]),
      );

  Widget _codePanel(List<ReferralAnalyticsCode> codes) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('أكثر الأكواد استخداماً', style: _sectionStyle),
          const SizedBox(height: 10),
          if (codes.isEmpty) const Text('لا توجد بيانات.'),
          ...codes.map((code) => ListTile(
                contentPadding: EdgeInsets.zero,
                dense: true,
                title: Text(code.code),
                subtitle: Text(code.customerName ?? 'عميل بدون اسم'),
                trailing: Text('${code.usageCount} استخدام'),
              )),
        ]),
      );

  Widget _buildQuality(ReferralAnalyticsQuality quality) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('جودة برنامج الإحالات', style: _sectionStyle),
          const SizedBox(height: 12),
          Wrap(spacing: 24, runSpacing: 14, children: [
            _QualityValue(
                'متوسط الاستخدام لكل كود', quality.averageUsagePerCode),
            _QualityValue('نسبة العملاء المسجلين عبر الإحالة',
                quality.referredCustomersRate,
                suffix: '%'),
            _QualityValue('نسبة المكافآت إلى التسجيلات',
                quality.rewardsToRegistrationsRate,
                suffix: '%'),
            _QualityValue(
                'نسبة العكس إلى المكافآت', quality.reversalsToRewardsRate,
                suffix: '%'),
            _QualityValue('عدد المكافآت المعكوسة', quality.rewardReversals),
          ]),
        ]),
      );

  Widget _buildActivity(ReferralAnalyticsActivity activity) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('النشاط الزمني للإحالات', style: _sectionStyle),
          const SizedBox(height: 12),
          Wrap(spacing: 28, runSpacing: 12, children: [
            _QualityValue('اليوم', activity.todayRegistrations),
            _QualityValue('هذا الأسبوع', activity.thisWeekRegistrations),
            _QualityValue('هذا الشهر', activity.thisMonthRegistrations),
          ]),
          const SizedBox(height: 18),
          if (activity.dailyRegistrations.isEmpty)
            const Text('لا توجد بيانات زمنية.')
          else
            SizedBox(
                height: 150, child: _ActivityBars(activity.dailyRegistrations)),
        ]),
      );

  Widget _buildTree(ReferralAnalyticsTree tree) => _Panel(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('تحليل شبكة الإحالات', style: _sectionStyle),
          const SizedBox(height: 12),
          Wrap(spacing: 28, runSpacing: 12, children: [
            _QualityValue('عدد الجذور', tree.rootCount),
            _QualityValue('أكبر عمق فعلي', tree.maxDepth),
            _QualityValue('حجم أكبر شبكة', tree.largestNetworkSize),
            _QualityValue('صاحب أكبر شبكة',
                tree.largestNetworkCustomerName ?? 'غير محدد'),
          ]),
        ]),
      );

  String _formatDate(DateTime value) =>
      DateFormat('yyyy-MM-dd').format(value.toLocal());

  String _formatNumber(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);

  TextStyle get _sectionStyle => const TextStyle(
      color: UiPalette.textMain, fontSize: 16, fontWeight: FontWeight.bold);
}

class _ActivityBars extends StatelessWidget {
  const _ActivityBars(this.periods);

  final List<ReferralAnalyticsPeriod> periods;

  @override
  Widget build(BuildContext context) => Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: periods.map((period) {
          final height = period.registrationCount == 0 ? 4.0 : 100.0;
          return Expanded(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 2),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  Text('${period.registrationCount}',
                      style: const TextStyle(fontSize: 11)),
                  const SizedBox(height: 4),
                  Container(height: height, color: UiPalette.primaryBlue),
                  const SizedBox(height: 4),
                  Text(DateFormat('MM/dd').format(period.period.toLocal()),
                      style: const TextStyle(fontSize: 10)),
                ],
              ),
            ),
          );
        }).toList(),
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

class _RankRow {
  const _RankRow(this.name, this.value);
  final String name;
  final String value;
}

class _QualityValue extends StatelessWidget {
  const _QualityValue(this.label, this.value, {this.suffix = ''});
  final String label;
  final Object value;
  final String suffix;
  @override
  Widget build(BuildContext context) =>
      Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: TextStyle(color: UiPalette.textSoft)),
        const SizedBox(height: 4),
        Text('$value$suffix',
            style: TextStyle(
                color: UiPalette.primaryBlue,
                fontSize: 18,
                fontWeight: FontWeight.bold)),
      ]);
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
