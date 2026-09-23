import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/rl_ui_text.dart';
import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';
import 'vip_customers_list_screen.dart';

class VipLevelsScreen extends StatefulWidget {
  const VipLevelsScreen({
    super.key,
    this.repository,
    this.initialCustomerId,
    this.initialCustomerName,
  });

  final LoyaltyRepository? repository;
  final int? initialCustomerId;
  final String? initialCustomerName;

  @override
  State<VipLevelsScreen> createState() => _VipLevelsScreenState();
}

class _VipLevelsScreenState extends State<VipLevelsScreen> {
  late final LoyaltyRepository _repository =
      widget.repository ?? LoyaltyRepository();
  final _searchController = TextEditingController();
  late Future<List<VipEvaluationCriteria>> _criteriaFuture;
  Future<VipEvaluation>? _evaluationFuture;
  Future<List<LoyaltyCustomerSearchResult>>? _suggestionsFuture;
  Timer? _suggestionTimer;
  int _searchVersion = 0;
  int? _selectedCustomerId;

  @override
  void initState() {
    super.initState();
    _criteriaFuture = _repository.getVipEvaluationCriteria();
    if (widget.initialCustomerId != null) {
      _selectedCustomerId = widget.initialCustomerId;
      _searchController.text = widget.initialCustomerName ??
          widget.initialCustomerId!.toString();
      _evaluationFuture =
          _repository.getVipEvaluation(widget.initialCustomerId!);
    }
  }

  @override
  void dispose() {
    _suggestionTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    setState(() {
      _criteriaFuture = _repository.getVipEvaluationCriteria();
      if (_selectedCustomerId != null) {
        _evaluationFuture = _repository.getVipEvaluation(_selectedCustomerId!);
      }
    });
    await _criteriaFuture;
    if (_evaluationFuture != null) await _evaluationFuture;
  }

  void _onSearchChanged(String value) {
    final version = ++_searchVersion;
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
        if (version == _searchVersion) _suggestionsFuture = request;
      });
    });
  }

  void _selectCustomer(LoyaltyCustomerSearchResult customer) {
    _searchVersion++;
    _suggestionTimer?.cancel();
    _searchController.text = customer.customerName ?? '${customer.customerId}';
    setState(() {
      _selectedCustomerId = customer.customerId;
      _suggestionsFuture = null;
      _evaluationFuture = _repository.getVipEvaluation(customer.customerId);
    });
  }

  void _submitCustomerSearch(String value) {
    final customerId = int.tryParse(value.trim());
    if (customerId == null || customerId <= 0) return;
    _searchVersion++;
    _suggestionTimer?.cancel();
    setState(() {
      _selectedCustomerId = customerId;
      _suggestionsFuture = null;
      _evaluationFuture = _repository.getVipEvaluation(customerId);
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('تقييم مستويات كبار العملاء'),
          actions: [
            IconButton(
              tooltip: 'تحديث التقييمات',
              onPressed: _refresh,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: RefreshIndicator(
          onRefresh: _refresh,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(20),
            children: [
              _buildCustomersListButton(context),
              const SizedBox(height: 12),
              _buildCustomerSearch(context),
              const SizedBox(height: 16),
              if (_evaluationFuture == null)
                _buildEmptySelection(context)
              else
                _buildEvaluation(context),
              const SizedBox(height: 16),
              _buildCriteria(context),
            ],
          ),
        ),
      );

  Widget _buildCustomersListButton(BuildContext context) => _Panel(
        child: Align(
          alignment: AlignmentDirectional.centerStart,
          child: OutlinedButton.icon(
            onPressed: () => AppNavigation.push(
              context,
              (_) => VipCustomersListScreen(repository: _repository),
            ),
            icon: const Icon(Icons.groups_2_outlined),
            label: const Text('عرض كبار العملاء'),
          ),
        ),
      );

  Widget _buildCustomerSearch(BuildContext context) => _Panel(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('العميل', style: _sectionStyle(context)),
            const SizedBox(height: 10),
            TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              onChanged: _onSearchChanged,
              onSubmitted: _submitCustomerSearch,
              decoration: const InputDecoration(
                labelText: 'اسم العميل أو الكود أو رقم العميل',
                prefixIcon: Icon(Icons.person_search_outlined),
              ),
            ),
            if (_suggestionsFuture != null)
              _CustomerSuggestions(
                future: _suggestionsFuture!,
                onSelected: _selectCustomer,
              ),
          ],
        ),
      );

  Widget _buildEmptySelection(BuildContext context) => _Panel(
        child: Row(
          children: [
            Icon(Icons.insights_outlined, color: UiPalette.primaryBlue),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                'اختر عميلاً لعرض المستوى المحسوب من الإحالات والنشاط والطلبات وحجم الشبكة.',
                style: _bodyStyle(context),
              ),
            ),
          ],
        ),
      );

  Widget _buildEvaluation(BuildContext context) => FutureBuilder<VipEvaluation>(
        future: _evaluationFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const _Panel(
                child: Center(child: CircularProgressIndicator()));
          }
          if (snapshot.hasError) {
            return _ErrorState(
              message: RlUiText.friendlyError(snapshot.error),
              onRetry: () {
                if (_selectedCustomerId == null) return;
                setState(() => _evaluationFuture =
                    _repository.getVipEvaluation(_selectedCustomerId!));
              },
            );
          }
          final evaluation = snapshot.data;
          if (evaluation == null) {
            return const _ErrorState(message: 'لم يصل تقييم للعميل المحدد.');
          }
          return _buildEvaluationPanel(context, evaluation);
        },
      );

  Widget _buildEvaluationPanel(
    BuildContext context,
    VipEvaluation evaluation,
  ) {
    final levelSurface = UiPalette.primaryDark;
    final levelText = UiPalette.adaptiveTextColor(levelSurface);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Panel(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                evaluation.customerName ?? 'عميل بدون اسم',
                style: _sectionStyle(context),
              ),
              if (evaluation.customerCode?.isNotEmpty == true) ...[
                const SizedBox(height: 4),
                Text(evaluation.customerCode!, style: _mutedStyle(context)),
              ],
              const SizedBox(height: 14),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: levelSurface,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  children: [
                    Icon(Icons.workspace_premium_outlined, color: levelText),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        evaluation.evaluatedVipLevelDisplayName,
                        style: TextStyle(
                          color: levelText,
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    Text(
                      '${_number(evaluation.score)}%',
                      style: TextStyle(
                        color: levelText,
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 14),
              Text(evaluation.reason, style: _bodyStyle(context)),
              const SizedBox(height: 10),
              Text(
                'آخر تقييم: ${DateFormat('yyyy-MM-dd HH:mm').format(evaluation.evaluatedAtUtc.toLocal())}',
                style: _mutedStyle(context),
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        _buildMetrics(context, evaluation),
      ],
    );
  }

  Widget _buildMetrics(BuildContext context, VipEvaluation evaluation) {
    final metrics = <_VipMetric>[
      _VipMetric('الإحالات المباشرة', evaluation.directReferralCount,
          Icons.person_add_alt_1_outlined),
      _VipMetric(
          'حجم الشبكة', evaluation.networkSize, Icons.account_tree_outlined),
      _VipMetric(
          'عمق الشبكة', evaluation.networkMaxDepth, Icons.layers_outlined),
      _VipMetric('طلبات العميل', evaluation.ownOrderCount,
          Icons.shopping_bag_outlined),
      _VipMetric('طلبات الشبكة', evaluation.networkOrderCount,
          Icons.shopping_cart_checkout_outlined),
      _VipMetric(
        'المستويات 1 / 2 / 3 / 4',
        '${evaluation.level1ReferralCount} / ${evaluation.level2ReferralCount} / ${evaluation.level3ReferralCount} / ${evaluation.level4ReferralCount}',
        Icons.stacked_bar_chart_outlined,
      ),
    ];
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: metrics
          .map((metric) => SizedBox(
                width: 220,
                child: _Panel(
                  child: Row(
                    children: [
                      Icon(metric.icon, color: UiPalette.primaryBlue),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(metric.label, style: _mutedStyle(context)),
                            const SizedBox(height: 5),
                            Text(
                              '${metric.value}',
                              style: TextStyle(
                                color: _surfaceTextColor,
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
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

  Widget _buildCriteria(BuildContext context) =>
      FutureBuilder<List<VipEvaluationCriteria>>(
        future: _criteriaFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const _Panel(child: LinearProgressIndicator());
          }
          if (snapshot.hasError) {
            return _ErrorState(
              message: RlUiText.friendlyError(snapshot.error),
              onRetry: _refresh,
            );
          }
          final criteria = snapshot.data ?? const <VipEvaluationCriteria>[];
          return _Panel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('معايير المحرك', style: _sectionStyle(context)),
                const SizedBox(height: 10),
                if (criteria.isEmpty)
                  Text('لا توجد معايير مفعلة.', style: _bodyStyle(context)),
                ...criteria.map(
                  (item) => Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(item.displayName, style: _bodyStyle(context)),
                        const SizedBox(height: 3),
                        Text(
                          'مباشر ${item.minimumDirectReferrals} • عميل ${item.minimumOwnOrders} • شبكة ${item.minimumNetworkOrders} • حجم ${item.minimumNetworkSize} • اجتياز ${_number(item.minimumScore)}%',
                          style: _mutedStyle(context),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      );

  TextStyle _sectionStyle(BuildContext context) => TextStyle(
        color: _surfaceTextColor,
        fontSize: 16,
        fontWeight: FontWeight.bold,
      );

  TextStyle _bodyStyle(BuildContext context) => TextStyle(
        color: _surfaceTextColor,
        height: 1.45,
      );

  TextStyle _mutedStyle(BuildContext context) =>
      TextStyle(color: UiPalette.textSoft, fontSize: 12);

  Color get _surfaceTextColor =>
      UiPalette.adaptiveTextColor(UiPalette.surfaceCard);

  String _number(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);
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
              child: Text('تعذر تحميل اقتراحات العملاء.'),
            );
          }
          final results =
              snapshot.data ?? const <LoyaltyCustomerSearchResult>[];
          if (results.isEmpty) {
            return const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Text('لا يوجد عميل مطابق.'),
            );
          }
          return Container(
            margin: const EdgeInsets.only(top: 4),
            decoration: BoxDecoration(
              color: UiPalette.softBlue,
              border: Border.all(color: UiPalette.borderSoft),
              borderRadius: BorderRadius.circular(6),
            ),
            child: Column(
              children: results
                  .take(6)
                  .map(
                    (customer) => ListTile(
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
                    ),
                  )
                  .toList(),
            ),
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
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: child,
      );
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) => _Panel(
        child: Column(
          children: [
            Text(message, textAlign: TextAlign.center),
            if (onRetry != null) ...[
              const SizedBox(height: 10),
              IconButton(
                tooltip: 'إعادة المحاولة',
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
              ),
            ],
          ],
        ),
      );
}

class _VipMetric {
  const _VipMetric(this.label, this.value, this.icon);

  final String label;
  final Object value;
  final IconData icon;
}
