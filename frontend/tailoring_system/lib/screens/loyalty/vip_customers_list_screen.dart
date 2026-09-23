import 'package:flutter/material.dart';

import '../../core/app_navigation.dart';
import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';
import 'vip_levels_screen.dart';

enum _VipCustomerFilter { all, platinum, gold, silver, bronze }

class VipCustomersListScreen extends StatefulWidget {
  const VipCustomersListScreen({super.key, this.repository});

  final LoyaltyRepository? repository;

  @override
  State<VipCustomersListScreen> createState() => _VipCustomersListScreenState();
}

class _VipCustomersListScreenState extends State<VipCustomersListScreen> {
  late final LoyaltyRepository _repository =
      widget.repository ?? LoyaltyRepository();
  late Future<List<VipCustomerListItem>> _customersFuture;
  _VipCustomerFilter _filter = _VipCustomerFilter.all;

  @override
  void initState() {
    super.initState();
    _customersFuture = _repository.getVipCustomers();
  }

  Future<void> _refresh() async {
    setState(() => _customersFuture = _repository.getVipCustomers());
    await _customersFuture;
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('قائمة كبار العملاء'),
          actions: [
            IconButton(
              tooltip: 'تحديث القائمة',
              onPressed: _refresh,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: RefreshIndicator(
          onRefresh: _refresh,
          child: FutureBuilder<List<VipCustomerListItem>>(
            future: _customersFuture,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Center(child: CircularProgressIndicator());
              }
              if (snapshot.hasError) {
                return ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(20),
                  children: [
                    _ErrorPanel(
                      message: RlUiText.friendlyError(snapshot.error),
                      onRetry: _refresh,
                    ),
                  ],
                );
              }
              final customers = snapshot.data ?? const <VipCustomerListItem>[];
              return ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(20),
                children: [
                  _buildSummary(context, customers),
                  const SizedBox(height: 12),
                  _buildFilter(context),
                  const SizedBox(height: 12),
                  _buildRows(context, _filtered(customers)),
                ],
              );
            },
          ),
        ),
      );

  Widget _buildSummary(
    BuildContext context,
    List<VipCustomerListItem> customers,
  ) {
    final summary = [
      _SummaryItem(
          'بلاتيني', _count(customers, 'PLATINUM'), _levelColor('PLATINUM')),
      _SummaryItem('ذهبي', _count(customers, 'GOLD'), _levelColor('GOLD')),
      _SummaryItem('فضي', _count(customers, 'SILVER'), _levelColor('SILVER')),
      _SummaryItem(
          'برونزي', _count(customers, 'BRONZE'), _levelColor('BRONZE')),
    ];
    return Wrap(
      spacing: 10,
      runSpacing: 10,
      children: summary
          .map(
            (item) => Container(
              constraints: const BoxConstraints(minWidth: 150),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: item.color,
                borderRadius: BorderRadius.circular(6),
                border: Border.all(color: UiPalette.borderSoft),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.people_alt_outlined,
                      color: UiPalette.adaptiveTextColor(item.color), size: 18),
                  const SizedBox(width: 8),
                  Text(
                    '${item.label}: ${item.count}',
                    style: TextStyle(
                      color: UiPalette.adaptiveTextColor(item.color),
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          )
          .toList(),
    );
  }

  Widget _buildFilter(BuildContext context) =>
      SegmentedButton<_VipCustomerFilter>(
        segments: const [
          ButtonSegment(value: _VipCustomerFilter.all, label: Text('الكل')),
          ButtonSegment(
              value: _VipCustomerFilter.platinum, label: Text('بلاتيني')),
          ButtonSegment(value: _VipCustomerFilter.gold, label: Text('ذهبي')),
          ButtonSegment(value: _VipCustomerFilter.silver, label: Text('فضي')),
          ButtonSegment(
              value: _VipCustomerFilter.bronze, label: Text('برونزي')),
        ],
        selected: {_filter},
        onSelectionChanged: (selection) {
          if (selection.isEmpty) return;
          setState(() => _filter = selection.first);
        },
        showSelectedIcon: false,
      );

  Widget _buildRows(
    BuildContext context,
    List<VipCustomerListItem> customers,
  ) {
    if (customers.isEmpty) {
      return _EmptyPanel(
        message: _filter == _VipCustomerFilter.all
            ? 'لا توجد حسابات مصنفة حاليًا.'
            : 'لا يوجد عملاء في المستوى المحدد.',
      );
    }
    return LayoutBuilder(
      builder: (context, constraints) {
        final tableWidth =
            constraints.maxWidth < 980 ? 980.0 : constraints.maxWidth;
        return SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: SizedBox(
            width: tableWidth,
            child: Column(
              children: [
                _buildHeaderRow(context),
                ...customers
                    .map((customer) => _buildCustomerRow(context, customer)),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildHeaderRow(BuildContext context) => Container(
        color: UiPalette.softBlue,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        child: const Row(
          children: [
            _HeaderCell('اسم العميل', 3),
            _HeaderCell('المستوى', 2),
            _HeaderCell('النسبة', 1),
            _HeaderCell('الإحالات', 1),
            _HeaderCell('طلبات العميل', 1),
            _HeaderCell('طلبات الشبكة', 1),
            _HeaderCell('حجم الشبكة', 1),
          ],
        ),
      );

  Widget _buildCustomerRow(
    BuildContext context,
    VipCustomerListItem customer,
  ) {
    final rowColor = _levelColor(customer.vipLevelCode);
    final textColor = UiPalette.adaptiveTextColor(rowColor);
    final customerName = customer.customerName?.trim() ?? '';
    final customerCode = customer.customerCode?.trim() ?? '';
    final levelName = customer.vipLevelDisplayName.trim().isNotEmpty
        ? customer.vipLevelDisplayName.trim()
        : _arabicLevelName(customer.vipLevelCode);
    return Material(
      color: rowColor,
      child: InkWell(
        onTap: () => AppNavigation.push(
          context,
          (_) => VipLevelsScreen(
            repository: _repository,
            initialCustomerId: customer.customerId,
            initialCustomerName: customer.customerName,
          ),
        ),
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
          decoration: BoxDecoration(
            border: Border(
              bottom: BorderSide(color: textColor.withValues(alpha: 0.2)),
            ),
          ),
          child: Row(
            children: [
              _CustomerCell(
                flex: 3,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      customerName.isNotEmpty
                          ? customerName
                          : 'اسم العميل غير مسجل',
                      style: TextStyle(
                        color: textColor,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    if (customerCode.isNotEmpty)
                      Text(
                        customerCode,
                        style: TextStyle(
                          color: textColor.withValues(alpha: 0.75),
                          fontSize: 11,
                        ),
                      )
                    else
                      Text(
                        'كود العميل غير مسجل',
                        style: TextStyle(
                          color: textColor.withValues(alpha: 0.75),
                          fontSize: 11,
                        ),
                      ),
                  ],
                ),
              ),
              _CustomerCell(
                flex: 2,
                child: Text(
                  levelName,
                  style:
                      TextStyle(color: textColor, fontWeight: FontWeight.bold),
                ),
              ),
              _CustomerCell(
                child: Text('${_number(customer.score)}%',
                    style: TextStyle(color: textColor)),
              ),
              _CustomerCell(
                child: Text('${customer.directReferralCount}',
                    style: TextStyle(color: textColor)),
              ),
              _CustomerCell(
                child: Text('${customer.ownOrderCount}',
                    style: TextStyle(color: textColor)),
              ),
              _CustomerCell(
                child: Text('${customer.networkOrderCount}',
                    style: TextStyle(color: textColor)),
              ),
              _CustomerCell(
                child: Text('${customer.networkSize}',
                    style: TextStyle(color: textColor)),
              ),
            ],
          ),
        ),
      ),
    );
  }

  List<VipCustomerListItem> _filtered(List<VipCustomerListItem> customers) {
    final code = switch (_filter) {
      _VipCustomerFilter.all => null,
      _VipCustomerFilter.platinum => 'PLATINUM',
      _VipCustomerFilter.gold => 'GOLD',
      _VipCustomerFilter.silver => 'SILVER',
      _VipCustomerFilter.bronze => 'BRONZE',
    };
    if (code == null) return customers;
    return customers
        .where((customer) => customer.vipLevelCode.toUpperCase() == code)
        .toList();
  }

  int _count(List<VipCustomerListItem> customers, String code) => customers
      .where((customer) => customer.vipLevelCode.toUpperCase() == code)
      .length;

  Color _levelColor(String code) {
    switch (code.toUpperCase()) {
      case 'PLATINUM':
        return const Color(0xFF573A70);
      case 'GOLD':
        return const Color(0xFF66501A);
      case 'SILVER':
        return const Color(0xFF46515C);
      case 'BRONZE':
        return const Color(0xFF674326);
      default:
        return UiPalette.surfaceCard;
    }
  }

  String _arabicLevelName(String code) {
    switch (code.toUpperCase()) {
      case 'PLATINUM':
        return 'بلاتيني';
      case 'GOLD':
        return 'ذهبي';
      case 'SILVER':
        return 'فضي';
      case 'BRONZE':
        return 'برونزي';
      default:
        return 'المستوى غير محدد';
    }
  }

  String _number(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);
}

class _SummaryItem {
  const _SummaryItem(this.label, this.count, this.color);

  final String label;
  final int count;
  final Color color;
}

class _HeaderCell extends StatelessWidget {
  const _HeaderCell(this.label, this.flex);

  final String label;
  final int flex;

  @override
  Widget build(BuildContext context) => Expanded(
        flex: flex,
        child: Text(
          label,
          style: TextStyle(
            color: UiPalette.adaptiveTextColor(UiPalette.softBlue),
            fontSize: 12,
            fontWeight: FontWeight.bold,
          ),
        ),
      );
}

class _CustomerCell extends StatelessWidget {
  const _CustomerCell({required this.child, this.flex = 1});

  final Widget child;
  final int flex;

  @override
  Widget build(BuildContext context) => Expanded(flex: flex, child: child);
}

class _EmptyPanel extends StatelessWidget {
  const _EmptyPanel({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: UiPalette.surfaceCard,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Text(
          message,
          textAlign: TextAlign.center,
          style: TextStyle(
            color: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
          ),
        ),
      );
}

class _ErrorPanel extends StatelessWidget {
  const _ErrorPanel({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: UiPalette.surfaceCard,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Column(
          children: [
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(
                color: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
              ),
            ),
            const SizedBox(height: 10),
            IconButton(
              tooltip: 'إعادة المحاولة',
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
      );
}
