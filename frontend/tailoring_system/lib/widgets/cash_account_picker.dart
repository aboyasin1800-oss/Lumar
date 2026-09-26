import 'package:flutter/material.dart';

import '../models/finance_models.dart';
import '../repositories/finance_repository.dart';

List<CashAccount> eligibleReceivingCashAccounts(
  Iterable<CashAccount> accounts,
) => accounts
    .where((account) => account.isActive && account.isReceiptEnabled)
    .toList(growable: false);

  enum CashAccountAvailability { loading, ready, unavailable, failed }

class CashAccountPicker extends StatefulWidget {
  const CashAccountPicker({
    required this.value,
    required this.onChanged,
    this.enabled = true,
    this.accountsFuture,
    this.onAvailabilityChanged,
    super.key,
  });

  final int? value;
  final ValueChanged<int?> onChanged;
  final bool enabled;
  final Future<List<CashAccount>>? accountsFuture;
  final ValueChanged<CashAccountAvailability>? onAvailabilityChanged;

  @override
  State<CashAccountPicker> createState() => _CashAccountPickerState();
}

class _CashAccountPickerState extends State<CashAccountPicker> {
  late final Future<List<CashAccount>> _accounts;
  int? _autoSelectedId;
  CashAccountAvailability? _lastAvailability;

  @override
  void initState() {
    super.initState();
    _accounts = widget.accountsFuture ?? FinanceRepository().getCashAccounts();
  }

  void _selectOnlyEligibleAccount(List<CashAccount> accounts) {
    if (widget.value != null || accounts.length != 1) return;
    final accountId = accounts.single.id;
    if (_autoSelectedId == accountId) return;
    _autoSelectedId = accountId;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && widget.value == null) widget.onChanged(accountId);
    });
  }

  void _reportAvailability(CashAccountAvailability availability) {
    if (_lastAvailability == availability) return;
    _lastAvailability = availability;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) widget.onAvailabilityChanged?.call(availability);
    });
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<List<CashAccount>>(
        future: _accounts,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            _reportAvailability(CashAccountAvailability.loading);
            return const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: LinearProgressIndicator(),
            );
          }
          if (snapshot.hasError) {
            _reportAvailability(CashAccountAvailability.failed);
            return const Text('تعذر تحميل الحسابات النقدية المتاحة.');
          }

          final accounts = eligibleReceivingCashAccounts(snapshot.data!);
          _selectOnlyEligibleAccount(accounts);
          if (accounts.isEmpty) {
            _reportAvailability(CashAccountAvailability.unavailable);
            return const Text(
              'لا يوجد حساب نقدي نشط ومؤهل لاستلام النقدية.',
            );
          }
          _reportAvailability(CashAccountAvailability.ready);
          return DropdownButtonFormField<int>(
            initialValue: accounts.any((account) => account.id == widget.value)
                ? widget.value
                : null,
            isExpanded: true,
            decoration: const InputDecoration(
              labelText: 'الحساب النقدي المستلم',
              border: OutlineInputBorder(),
            ),
            hint: const Text('اختر الحساب النقدي'),
            items: accounts
                .map(
                  (account) => DropdownMenuItem<int>(
                    value: account.id,
                    child: Text('${account.accountName} (${account.currencyCode ?? '-'})'),
                  ),
                )
                .toList(),
            onChanged: widget.enabled && accounts.isNotEmpty ? widget.onChanged : null,
          );
        },
      );
}