import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/referral_models.dart';
import '../../repositories/referral_repository.dart';

class ReferralHistoryScreen extends StatefulWidget {
  const ReferralHistoryScreen({super.key, this.repository});

  final ReferralRepository? repository;

  @override
  State<ReferralHistoryScreen> createState() => _ReferralHistoryScreenState();
}

class _ReferralHistoryScreenState extends State<ReferralHistoryScreen> {
  late final ReferralRepository _repository =
      widget.repository ?? ReferralRepository();
  final _searchController = TextEditingController();
  String? _typeFilter;
  DateTime? _fromDate;
  DateTime? _toDate;
  late Future<_ReferralHistoryData> _future;

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

  Future<_ReferralHistoryData> _loadData() async {
    final transactions = await _repository.getAllTransactions();
    final customerIds = <int>{};
    for (final transaction in transactions) {
      customerIds.add(transaction.referrerCustomerId);
      if (transaction.referredCustomerId != null) {
        customerIds.add(transaction.referredCustomerId!);
      }
    }
    final details = <int, _CustomerInfo>{};
    final codes = <int, String>{};
    final roots = await _repository.getRoots();
    for (final root in roots) {
      details[root.customerId] = _CustomerInfo(
        name: root.customerName,
        code: root.customerCode,
      );
      final tree = await _repository.getTree(root.customerId);
      void addNode(ReferralTreeNode node) {
        customerIds.add(node.customerId);
        details[node.customerId] = _CustomerInfo(
          name: node.customerName,
          code: node.customerCode,
          referralCode: node.referralCode,
        );
        for (final child in node.children) {
          addNode(child);
        }
      }

      for (final child in tree.children) {
        addNode(child);
      }
    }
    for (final customerId in customerIds) {
      for (final code in await _repository.getCodes(customerId)) {
        codes[code.referralCodeId] = code.code;
      }
    }
    return _ReferralHistoryData(
      transactions: transactions,
      customers: details,
      referralCodes: codes,
    );
  }

  List<ReferralTransaction> _filtered(_ReferralHistoryData data) {
    final query = _searchController.text.trim().toLowerCase();
    return data.transactions.where((transaction) {
      final referrer = data.customers[transaction.referrerCustomerId];
      final referred = transaction.referredCustomerId == null
          ? null
          : data.customers[transaction.referredCustomerId!];
      final referralCode = transaction.referralCodeId == null
          ? null
          : data.referralCodes[transaction.referralCodeId!];
      final searchable = [
        referrer?.name,
        referrer?.code,
        referred?.name,
        referred?.code,
        referralCode,
      ].whereType<String>().join(' ').toLowerCase();
      final date = transaction.createdAt.toLocal();
      final afterFrom = _fromDate == null || !date.isBefore(_fromDate!);
      final beforeTo = _toDate == null ||
          date.isBefore(_toDate!.add(const Duration(days: 1)));
      return (query.isEmpty || searchable.contains(query)) &&
          (_typeFilter == null || transaction.transactionType == _typeFilter) &&
          afterFrom &&
          beforeTo;
    }).toList();
  }

  void _refresh() => setState(() => _future = _loadData());

  Future<void> _chooseDate(bool from) async {
    final picked = await showDatePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime.now().add(const Duration(days: 1)),
      initialDate: (from ? _fromDate : _toDate) ?? DateTime.now(),
    );
    if (picked == null) return;
    setState(() {
      if (from) {
        _fromDate = picked;
      } else {
        _toDate = picked;
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(title: const Text('سجل أحداث الإحالات')),
      body: FutureBuilder<_ReferralHistoryData>(
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
          final items = _filtered(data);
          final types = data.transactions
              .map((item) => item.transactionType)
              .toSet()
              .toList()
            ..sort();
          return RefreshIndicator(
            onRefresh: () async => _refresh(),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _Filters(
                  searchController: _searchController,
                  typeFilter: _typeFilter,
                  types: types,
                  fromDate: _fromDate,
                  toDate: _toDate,
                  suggestions: _suggestions(data),
                  onTypeChanged: (value) => setState(() => _typeFilter = value),
                  onChooseDate: _chooseDate,
                  onRefresh: _refresh,
                  onSuggestionSelected: (value) {
                    _searchController.text = value;
                    _searchController.selection = TextSelection.collapsed(
                      offset: value.length,
                    );
                  },
                ),
                const SizedBox(height: 16),
                _Summary(transactions: items),
                const SizedBox(height: 16),
                if (items.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(32),
                    child: Center(child: Text('لا توجد أحداث إحالة مطابقة.')),
                  )
                else
                  ...items.map(
                    (transaction) => _EventCard(
                      transaction: transaction,
                      referrer: data.customers[transaction.referrerCustomerId],
                      referred: transaction.referredCustomerId == null
                          ? null
                          : data.customers[transaction.referredCustomerId!],
                      referralCode: transaction.referralCodeId == null
                          ? null
                          : data.referralCodes[transaction.referralCodeId!],
                      onTap: () => _showDetails(
                        transaction,
                        data.customers,
                        data.referralCodes,
                      ),
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }

  List<String> _suggestions(_ReferralHistoryData data) {
    final values = <String>{};
    for (final customer in data.customers.values) {
      if (customer.name != null) values.add(customer.name!);
      if (customer.code != null) values.add(customer.code!);
    }
    values.addAll(data.referralCodes.values);
    final query = _searchController.text.trim().toLowerCase();
    if (query.length < 2) return const [];
    if (values.any((value) => value.toLowerCase() == query)) return const [];
    return values
        .where((value) => query.isEmpty || value.toLowerCase().contains(query))
        .take(8)
        .toList();
  }

  void _showDetails(
    ReferralTransaction transaction,
    Map<int, _CustomerInfo> customers,
    Map<int, String> codes,
  ) {
    final referrer = customers[transaction.referrerCustomerId];
    final referred = transaction.referredCustomerId == null
        ? null
        : customers[transaction.referredCustomerId!];
    showDialog<void>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(RlUiText.translate(transaction.transactionType)),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _DetailRow(
                  'نوع الحدث', RlUiText.translate(transaction.transactionType)),
              _DetailRow('تاريخ الحدث', _formatDate(transaction.createdAt)),
              _DetailRow('المحيل', _customerLabel(referrer)),
              _DetailRow('المحال', _customerLabel(referred)),
              _DetailRow(
                  'كود الإحالة',
                  transaction.referralCodeId == null
                      ? '-'
                      : codes[transaction.referralCodeId!] ?? '-'),
              _DetailRow('رقم الطلب', '${transaction.orderId ?? '-'}'),
              _DetailRow('النقاط', transaction.loyaltyPoints.toString()),
              _DetailRow('الملاحظات', transaction.notes ?? '-'),
            ],
          ),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('إغلاق'))
        ],
      ),
    );
  }
}

class _Filters extends StatelessWidget {
  const _Filters(
      {required this.searchController,
      required this.typeFilter,
      required this.types,
      required this.fromDate,
      required this.toDate,
      required this.suggestions,
      required this.onTypeChanged,
      required this.onChooseDate,
      required this.onRefresh,
      required this.onSuggestionSelected});
  final TextEditingController searchController;
  final String? typeFilter;
  final List<String> types;
  final DateTime? fromDate;
  final DateTime? toDate;
  final List<String> suggestions;
  final ValueChanged<String?> onTypeChanged;
  final Future<void> Function(bool from) onChooseDate;
  final VoidCallback onRefresh;
  final ValueChanged<String> onSuggestionSelected;

  @override
  Widget build(BuildContext context) => Wrap(
        spacing: 12,
        runSpacing: 12,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          SizedBox(
              width: 300,
              child: Column(children: [
                TextField(
                    controller: searchController,
                    textInputAction: TextInputAction.search,
                    decoration: const InputDecoration(
                        labelText: 'بحث باسم العميل أو الكود',
                        prefixIcon: Icon(Icons.search))),
                if (suggestions.isNotEmpty)
                  Container(
                    margin: const EdgeInsets.only(top: 4),
                    decoration: BoxDecoration(
                      color: UiPalette.softBlue,
                      border: Border.all(color: UiPalette.borderSoft),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Column(
                      children: suggestions
                          .map((suggestion) => ListTile(
                                dense: true,
                                title: Text(suggestion),
                                onTap: () => onSuggestionSelected(suggestion),
                              ))
                          .toList(),
                    ),
                  ),
              ])),
          SizedBox(
              width: 220,
              child: DropdownButtonFormField<String>(
                  initialValue: typeFilter,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'نوع الحدث'),
                  items: [
                    const DropdownMenuItem(
                        value: null, child: Text('كل الأحداث')),
                    ...types.map((type) => DropdownMenuItem(
                        value: type, child: Text(RlUiText.translate(type))))
                  ],
                  onChanged: onTypeChanged)),
          OutlinedButton.icon(
              onPressed: () => onChooseDate(true),
              icon: const Icon(Icons.calendar_today_outlined),
              label:
                  Text(fromDate == null ? 'من تاريخ' : _formatDate(fromDate!))),
          OutlinedButton.icon(
              onPressed: () => onChooseDate(false),
              icon: const Icon(Icons.event_outlined),
              label: Text(toDate == null ? 'إلى تاريخ' : _formatDate(toDate!))),
          IconButton(
              onPressed: onRefresh,
              tooltip: 'تحديث',
              icon: const Icon(Icons.refresh)),
        ],
      );
}

class _Summary extends StatelessWidget {
  const _Summary({required this.transactions});
  final List<ReferralTransaction> transactions;

  @override
  Widget build(BuildContext context) {
    final registrations = transactions
        .where((item) => item.transactionType == 'Registration')
        .length;
    final granted = transactions
        .where((item) => item.transactionType == 'RewardGranted')
        .length;
    final reversed = transactions
        .where((item) => item.transactionType == 'RewardReversal')
        .length;
    final points =
        transactions.fold<double>(0, (sum, item) => sum + item.loyaltyPoints);
    return Wrap(spacing: 12, runSpacing: 12, children: [
      _SummaryTile('تسجيلات الإحالة', '$registrations'),
      _SummaryTile('المكافآت الممنوحة', '$granted'),
      _SummaryTile('المكافآت المعكوسة', '$reversed'),
      _SummaryTile('صافي نقاط الإحالات بعد العكس', points.toString()),
    ]);
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
          borderRadius: BorderRadius.circular(8)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: const TextStyle(color: UiPalette.textSoft)),
        const SizedBox(height: 6),
        Text(value,
            style: const TextStyle(
                color: UiPalette.textMain,
                fontSize: 20,
                fontWeight: FontWeight.bold))
      ]));
}

class _EventCard extends StatelessWidget {
  const _EventCard(
      {required this.transaction,
      required this.referrer,
      required this.referred,
      required this.referralCode,
      required this.onTap});
  final ReferralTransaction transaction;
  final _CustomerInfo? referrer;
  final _CustomerInfo? referred;
  final String? referralCode;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Card(
      color: UiPalette.surfaceCard,
      child: ListTile(
          onTap: onTap,
          leading: Icon(_eventIcon(transaction.transactionType),
              color: UiPalette.primaryBlue),
          title: Text(RlUiText.translate(transaction.transactionType)),
          subtitle: Text(
              '${_customerLabel(referrer)}  ←  ${_customerLabel(referred)}\n${_formatDate(transaction.createdAt)}${referralCode == null ? '' : '  •  $referralCode'}'),
          isThreeLine: true,
          trailing: Text('${transaction.loyaltyPoints} نقطة')));
}

class _DetailRow extends StatelessWidget {
  const _DetailRow(this.label, this.value);
  final String label;
  final String value;
  @override
  Widget build(BuildContext context) => Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        SizedBox(
            width: 105,
            child: Text(label,
                style: const TextStyle(fontWeight: FontWeight.bold))),
        Expanded(child: Text(value))
      ]));
}

class _ReferralHistoryData {
  const _ReferralHistoryData(
      {required this.transactions,
      required this.customers,
      required this.referralCodes});
  final List<ReferralTransaction> transactions;
  final Map<int, _CustomerInfo> customers;
  final Map<int, String> referralCodes;
}

class _CustomerInfo {
  const _CustomerInfo({this.name, this.code, this.referralCode});
  final String? name;
  final String? code;
  final String? referralCode;
}

String _customerLabel(_CustomerInfo? customer) =>
    customer?.name ?? customer?.code ?? '-';
String _formatDate(DateTime date) =>
    DateFormat('yyyy-MM-dd HH:mm').format(date.toLocal());
IconData _eventIcon(String type) => switch (type) {
      'Registration' => Icons.person_add_alt_1,
      'RewardGranted' => Icons.card_giftcard_outlined,
      'RewardReversal' => Icons.undo_outlined,
      _ => Icons.history
    };
