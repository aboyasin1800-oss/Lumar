import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/referral_models.dart';
import '../../repositories/referral_repository.dart';

class ReferralCodesScreen extends StatefulWidget {
  const ReferralCodesScreen({super.key, this.repository});

  final ReferralRepository? repository;

  @override
  State<ReferralCodesScreen> createState() => _ReferralCodesScreenState();
}

class _ReferralCodesScreenState extends State<ReferralCodesScreen> {
  late final ReferralRepository _repository =
      widget.repository ?? ReferralRepository();
  final _searchController = TextEditingController();
  String? _statusFilter;
  late Future<_ReferralCodesData> _future;

  @override
  void initState() {
    super.initState();
    _future = _loadData();
    _searchController.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<_ReferralCodesData> _loadData() async {
    final customers = <int, _CustomerInfo>{};
    final identities = await _repository.getAllCustomerIdentities();
    final customerIds =
        identities.map((identity) => identity.customerId).toSet();
    for (final identity in identities) {
      customers[identity.customerId] = _CustomerInfo(
        name: identity.customerName,
        code: identity.customerCode,
      );
    }

    final codeResults =
        await Future.wait(customerIds.map(_repository.getCodes));
    final codes = <int, ReferralCode>{};
    for (final result in codeResults) {
      for (final code in result) {
        codes[code.referralCodeId] = code;
      }
    }

    final transactionResults = await Future.wait(
      customerIds.map(_repository.getTransactions),
    );
    final transactions = <int, ReferralTransaction>{};
    for (final result in transactionResults) {
      for (final transaction in result) {
        transactions[transaction.referralTransactionId] = transaction;
      }
    }
    return _ReferralCodesData(
      codes: codes.values.toList()
        ..sort((a, b) => b.createdAt.compareTo(a.createdAt)),
      customers: customers,
      transactions: transactions.values.toList(),
    );
  }

  List<_ReferralCodeRow> _rows(_ReferralCodesData data) {
    final query = _searchController.text.trim().toLowerCase();
    return data.codes.map((code) {
      final customer = data.customers[code.customerId];
      final registrations = data.transactions
          .where((transaction) =>
              transaction.referralCodeId == code.referralCodeId &&
              transaction.transactionType == 'Registration')
          .toList();
      final rewardAmount = data.transactions
          .where((transaction) =>
              transaction.referralCodeId == code.referralCodeId &&
              transaction.transactionType == 'RewardGranted')
          .fold<double>(
              0, (sum, transaction) => sum + transaction.fixedRewardAmount);
      final rewardPoints = data.transactions
          .where((transaction) =>
              transaction.referralCodeId == code.referralCodeId &&
              transaction.transactionType == 'RewardGranted')
          .fold<double>(
              0, (sum, transaction) => sum + transaction.loyaltyPoints);
      final searchable = [customer?.name, customer?.code, code.code]
          .whereType<String>()
          .join(' ')
          .toLowerCase();
      return _ReferralCodeRow(
        code: code,
        customer: customer,
        registrations: registrations,
        rewardAmount: rewardAmount,
        rewardPoints: rewardPoints,
        matchesSearch: query.isEmpty || searchable.contains(query),
      );
    }).where((row) {
      final matchesStatus = _statusFilter == null ||
          (_statusFilter == 'active' && row.code.isActive) ||
          (_statusFilter == 'inactive' && !row.code.isActive);
      return row.matchesSearch && matchesStatus;
    }).toList();
  }

  void _refresh() => setState(() => _future = _loadData());

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(title: const Text('أكواد الإحالة')),
      body: FutureBuilder<_ReferralCodesData>(
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
          final rows = _rows(data);
          return RefreshIndicator(
            onRefresh: () async => _refresh(),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _Filters(
                  searchController: _searchController,
                  statusFilter: _statusFilter,
                  onStatusChanged: (value) =>
                      setState(() => _statusFilter = value),
                  onRefresh: _refresh,
                ),
                const SizedBox(height: 16),
                _Summary(rows: rows),
                const SizedBox(height: 16),
                if (rows.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(32),
                    child: Center(child: Text('لا توجد أكواد إحالة مطابقة.')),
                  )
                else
                  ...rows.map((row) => _CodeCard(
                        row: row,
                        onTap: () => _showDetails(row),
                      )),
              ],
            ),
          );
        },
      ),
    );
  }

  void _showDetails(_ReferralCodeRow row) {
    showDialog<void>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(row.code.code),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _DetailRow('اسم العميل', row.customer?.name ?? '-'),
              _DetailRow('كود العميل', row.customer?.code ?? '-'),
              _DetailRow('كود الإحالة', row.code.code),
              _DetailRow('تاريخ الإنشاء', _formatDate(row.code.createdAt)),
              _DetailRow('الحالة', row.code.isActive ? 'فعال' : 'غير فعال'),
              _DetailRow('مرات الاستخدام', '${row.registrations.length}'),
              _DetailRow('العملاء المسجلون', '${row.registrations.length}'),
              _DetailRow(
                  'إجمالي المكافآت المالية', row.rewardAmount.toString()),
              _DetailRow(
                  'إجمالي نقاط المكافآت الممنوحة', row.rewardPoints.toString()),
              const SizedBox(height: 8),
              const Text('العملاء الذين استخدموا الكود',
                  style: TextStyle(fontWeight: FontWeight.bold)),
              ...row.registrations.map(
                (transaction) =>
                    Text('العميل رقم ${transaction.referredCustomerId ?? '-'}'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إغلاق'),
          ),
        ],
      ),
    );
  }
}

class _Filters extends StatelessWidget {
  const _Filters({
    required this.searchController,
    required this.statusFilter,
    required this.onStatusChanged,
    required this.onRefresh,
  });
  final TextEditingController searchController;
  final String? statusFilter;
  final ValueChanged<String?> onStatusChanged;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) => Wrap(
        spacing: 12,
        runSpacing: 12,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          SizedBox(
            width: 320,
            child: TextField(
              controller: searchController,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => FocusScope.of(context).unfocus(),
              decoration: const InputDecoration(
                labelText: 'بحث باسم العميل أو الكود',
                prefixIcon: Icon(Icons.search),
              ),
            ),
          ),
          SizedBox(
            width: 210,
            child: DropdownButtonFormField<String>(
              initialValue: statusFilter,
              decoration: const InputDecoration(labelText: 'حالة الكود'),
              items: const [
                DropdownMenuItem(value: null, child: Text('كل الحالات')),
                DropdownMenuItem(value: 'active', child: Text('فعال')),
                DropdownMenuItem(value: 'inactive', child: Text('غير فعال')),
              ],
              onChanged: onStatusChanged,
            ),
          ),
          IconButton(
            onPressed: onRefresh,
            tooltip: 'تحديث',
            icon: const Icon(Icons.refresh),
          ),
        ],
      );
}

class _Summary extends StatelessWidget {
  const _Summary({required this.rows});
  final List<_ReferralCodeRow> rows;

  @override
  Widget build(BuildContext context) {
    final active = rows.where((row) => row.code.isActive).length;
    final used = rows.where((row) => row.registrations.isNotEmpty).length;
    final customers =
        rows.fold<int>(0, (sum, row) => sum + row.registrations.length);
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        _SummaryTile('إجمالي الأكواد', '${rows.length}'),
        _SummaryTile('الأكواد الفعالة', '$active'),
        _SummaryTile('الأكواد المستخدمة', '$used'),
        _SummaryTile('العملاء المسجلون', '$customers'),
      ],
    );
  }
}

class _SummaryTile extends StatelessWidget {
  const _SummaryTile(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Container(
        width: 190,
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: UiPalette.surfaceCard,
          border: Border.all(color: UiPalette.borderSoft),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: const TextStyle(color: UiPalette.textSoft)),
            const SizedBox(height: 6),
            Text(value,
                style: const TextStyle(
                    color: UiPalette.textMain,
                    fontSize: 20,
                    fontWeight: FontWeight.bold)),
          ],
        ),
      );
}

class _CodeCard extends StatelessWidget {
  const _CodeCard({required this.row, required this.onTap});
  final _ReferralCodeRow row;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Card(
        color: UiPalette.surfaceCard,
        child: ListTile(
          onTap: onTap,
          leading: const Icon(Icons.confirmation_number_outlined,
              color: UiPalette.primaryBlue),
          title: Text(row.code.code),
          subtitle: Text(
              '${row.customer?.name ?? '-'}  •  ${row.customer?.code ?? '-'}\n${row.code.isActive ? 'فعال' : 'غير فعال'}  •  ${_formatDate(row.code.createdAt)}'),
          isThreeLine: true,
          trailing: Text('${row.registrations.length} عميل'),
        ),
      );
}

class _DetailRow extends StatelessWidget {
  const _DetailRow(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
                width: 125,
                child: Text(label,
                    style: const TextStyle(fontWeight: FontWeight.bold))),
            Expanded(child: Text(value)),
          ],
        ),
      );
}

class _ReferralCodesData {
  const _ReferralCodesData({
    required this.codes,
    required this.customers,
    required this.transactions,
  });
  final List<ReferralCode> codes;
  final Map<int, _CustomerInfo> customers;
  final List<ReferralTransaction> transactions;
}

class _CustomerInfo {
  const _CustomerInfo({this.name, this.code});
  final String? name;
  final String? code;
}

class _ReferralCodeRow {
  const _ReferralCodeRow({
    required this.code,
    required this.customer,
    required this.registrations,
    required this.rewardAmount,
    required this.rewardPoints,
    required this.matchesSearch,
  });
  final ReferralCode code;
  final _CustomerInfo? customer;
  final List<ReferralTransaction> registrations;
  final double rewardAmount;
  final double rewardPoints;
  final bool matchesSearch;
}

String _formatDate(DateTime date) =>
    DateFormat('yyyy-MM-dd HH:mm').format(date.toLocal());
