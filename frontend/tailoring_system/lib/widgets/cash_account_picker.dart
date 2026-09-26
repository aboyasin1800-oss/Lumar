import 'package:flutter/material.dart';

import '../models/finance_models.dart';
import '../repositories/finance_repository.dart';

class CashAccountPicker extends StatefulWidget {
  const CashAccountPicker({
    required this.value,
    required this.onChanged,
    this.enabled = true,
    super.key,
  });

  final int? value;
  final ValueChanged<int?> onChanged;
  final bool enabled;

  @override
  State<CashAccountPicker> createState() => _CashAccountPickerState();
}

class _CashAccountPickerState extends State<CashAccountPicker> {
  late final Future<List<CashAccount>> _accounts = FinanceRepository().getCashAccounts();

  @override
  Widget build(BuildContext context) => FutureBuilder<List<CashAccount>>(
        future: _accounts,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: LinearProgressIndicator(),
            );
          }
          if (snapshot.hasError) {
            return const Text('تعذر تحميل الحسابات النقدية المتاحة.');
          }

          final accounts = snapshot.data!
              .where((account) => account.isReceiptEnabled)
              .toList();
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