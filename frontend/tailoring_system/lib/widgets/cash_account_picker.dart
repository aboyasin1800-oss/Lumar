import 'package:flutter/material.dart';

import '../models/finance_models.dart';
import '../repositories/finance_repository.dart';

enum CashAccountDirection { receiving, disbursing }

List<CashAccount> eligibleCashAccounts(
  Iterable<CashAccount> accounts, {
  CashAccountDirection direction = CashAccountDirection.receiving,
}) =>
    accounts.where((account) {
      if (!account.isActive) return false;
      return direction == CashAccountDirection.receiving
          ? account.isReceiptEnabled
          : account.isDisbursementEnabled;
    }).toList(growable: false);

List<CashAccount> eligibleReceivingCashAccounts(
  Iterable<CashAccount> accounts,
) =>
    eligibleCashAccounts(accounts, direction: CashAccountDirection.receiving);

enum CashAccountAvailability { loading, ready, unavailable, failed }

class CashAccountPicker extends StatefulWidget {
  const CashAccountPicker({
    required this.value,
    required this.onChanged,
    this.enabled = true,
    this.accountsFuture,
    this.onAvailabilityChanged,
    this.direction = CashAccountDirection.receiving,
    super.key,
  });

  final int? value;
  final ValueChanged<int?> onChanged;
  final bool enabled;
  final Future<List<CashAccount>>? accountsFuture;
  final ValueChanged<CashAccountAvailability>? onAvailabilityChanged;
  final CashAccountDirection direction;

  @override
  State<CashAccountPicker> createState() => _CashAccountPickerState();
}

class _CashAccountPickerState extends State<CashAccountPicker> {
  late final Future<List<CashAccount>> _accounts;
  CashAccountAvailability? _lastAvailability;

  @override
  void initState() {
    super.initState();
    _accounts = widget.accountsFuture ?? FinanceRepository().getCashAccounts();
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

          final accounts = eligibleCashAccounts(
            snapshot.data!,
            direction: widget.direction,
          );
          if (accounts.isEmpty) {
            _reportAvailability(CashAccountAvailability.unavailable);
            final label = widget.direction == CashAccountDirection.disbursing
                ? 'لا يوجد حساب نقدي نشط ومؤهل للسحب النقدي.'
                : 'لا يوجد حساب نقدي نشط ومؤهل لاستلام النقدية.';
            return Text(label);
          }
          _reportAvailability(CashAccountAvailability.ready);
          final selectedValue = widget.value != null &&
                  accounts.any((account) => account.id == widget.value)
              ? widget.value
              : null;
          return DropdownButtonFormField<int>(
            initialValue: selectedValue,
            isExpanded: true,
            decoration: InputDecoration(
              labelText: widget.direction == CashAccountDirection.disbursing
                  ? 'الحساب النقدي المسحوب منه'
                  : 'الحساب النقدي المستلم',
              border: const OutlineInputBorder(),
            ),
            hint: const Text('اختر الحساب النقدي'),
            items: accounts
                .map(
                  (account) => DropdownMenuItem<int>(
                    value: account.id,
                    child: Text(
                        '${account.accountName} (${account.currencyCode ?? '-'})'),
                  ),
                )
                .toList(),
            onChanged:
                widget.enabled && accounts.isNotEmpty ? widget.onChanged : null,
          );
        },
      );
}
