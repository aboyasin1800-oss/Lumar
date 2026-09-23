import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../models/finance_models.dart';
import '../models/payroll_models.dart';

final _ledgerMoney = NumberFormat('#,##0.00');
final _ledgerDate = DateFormat('yyyy/MM/dd');

String _ledgerText(String? value) =>
    value == null || value.trim().isEmpty ? '-' : value;

class EmployeeDrawsScreen extends StatelessWidget {
  const EmployeeDrawsScreen({
    required this.employee,
    this.draws = const [],
    this.settlements = const [],
    this.pieceWages = const [],
    this.pieceWageTotal,
    this.operatingExpenses = const [],
    super.key,
  });

  final PayrollEmployee employee;
  final List<EmployeeDraw> draws;
  final List<PayrollSettlement> settlements;
  final List<PieceWageRecord> pieceWages;
  final double? pieceWageTotal;
  final List<FinancialTransaction> operatingExpenses;

  double get drawTotal => draws.fold<double>(0, (sum, item) => sum + (item.amount ?? 0));
  double get settlementTotal => settlements.fold<double>(0, (sum, item) => sum + item.amount);
  double get currentBalance => drawTotal - settlementTotal;
  double get pieceWageLedgerTotal => pieceWageTotal ?? pieceWages.fold<double>(0, (sum, item) => sum + item.totalWage);
  double get operatingExpenseTotal => operatingExpenses.fold<double>(0, (sum, item) => sum + item.amount);

  @override
  Widget build(BuildContext context) {
    final rows = [
      _LedgerSummaryRow('إجمالي السلف', drawTotal, Icons.account_balance_wallet_outlined),
      _LedgerSummaryRow('إجمالي التسويات', settlementTotal, Icons.replay_circle_filled),
      _LedgerSummaryRow('الرصيد الجاري', currentBalance, Icons.savings_outlined, emphasized: true),
      _LedgerSummaryRow('أجور القطعة', pieceWageLedgerTotal, Icons.precision_manufacturing_outlined),
      _LedgerSummaryRow('المصروفات', operatingExpenseTotal, Icons.storefront_outlined),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('دفتر السلف والتسويات'),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(
              '${employee.code} • ${employee.name}',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            GridView.count(
              crossAxisCount: 2,
              shrinkWrap: true,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 2.4,
              children: rows.map((row) => _SummaryCard(row: row)).toList(),
            ),
            const SizedBox(height: 12),
            _LedgerSection(
              title: 'سجل السلف',
              icon: Icons.account_balance_wallet_outlined,
              child: _LedgerTable(
                columns: const ['المبلغ', 'التاريخ', 'ملاحظات'],
                rows: draws
                    .map((draw) => [
                          _ledgerMoney.format(draw.amount ?? 0),
                          draw.drawDate == null ? '-' : _ledgerDate.format(draw.drawDate!),
                          _ledgerText(draw.notes),
                        ])
                    .toList(),
                empty: 'لا توجد سلف مسجلة لهذا الموظف.',
              ),
            ),
            const SizedBox(height: 12),
            _LedgerSection(
              title: 'سجل التسويات',
              icon: Icons.replay_circle_filled,
              child: _LedgerTable(
                columns: const ['المبلغ', 'التاريخ', 'ملاحظات'],
                rows: settlements
                    .map((settlement) => [
                          _ledgerMoney.format(settlement.amount),
                          _ledgerDate.format(settlement.settlementDate),
                          _ledgerText(settlement.notes),
                        ])
                    .toList(),
                empty: 'لا توجد تسويات مسجلة لهذا الموظف.',
              ),
            ),
            const SizedBox(height: 12),
            _LedgerSection(
              title: 'أجور القطعة',
              icon: Icons.precision_manufacturing_outlined,
              child: _LedgerTable(
                columns: const ['القطعة', 'المرحلة', 'القيمة', 'الحالة'],
                rows: pieceWages
                    .map((entry) => [
                          entry.pieceType,
                          entry.stage,
                          _ledgerMoney.format(entry.totalWage),
                          entry.status,
                        ])
                    .toList(),
                empty: 'لا توجد سجلات أجور قطعة مسجلة لهذا الموظف.',
              ),
            ),
            const SizedBox(height: 12),
            _LedgerSection(
              title: 'مصروفات المحل',
              icon: Icons.storefront_outlined,
              child: _LedgerTable(
                columns: const ['المرجع', 'المبلغ', 'الوصف'],
                rows: operatingExpenses
                    .map((expense) => [
                          expense.referenceNumber,
                          _ledgerMoney.format(expense.amount),
                          _ledgerText(expense.description),
                        ])
                    .toList(),
                empty: 'لا توجد مصروفات محل مرتبطة بهذا الموظف.',
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _LedgerSection extends StatelessWidget {
  const _LedgerSection({
    required this.title,
    required this.icon,
    required this.child,
    super.key,
  });

  final String title;
  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Icon(icon),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      title,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              child,
            ],
          ),
        ),
      );
}

class _LedgerSummaryRow {
  const _LedgerSummaryRow(
    this.label,
    this.amount,
    this.icon, {
    this.emphasized = false,
  });

  final String label;
  final double amount;
  final IconData icon;
  final bool emphasized;
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.row, super.key});

  final _LedgerSummaryRow row;

  @override
  Widget build(BuildContext context) {
    final color = row.emphasized
        ? Theme.of(context).colorScheme.primaryContainer
        : Theme.of(context).colorScheme.surfaceContainerHighest;
    final textStyle = Theme.of(context).textTheme.titleMedium?.copyWith(
      fontWeight: FontWeight.bold,
    );

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Row(
            children: [
              Icon(row.icon),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  row.label,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            _ledgerMoney.format(row.amount),
            style: textStyle,
          ),
        ],
      ),
    );
  }
}

class _LedgerTable extends StatelessWidget {
  const _LedgerTable({
    required this.columns,
    required this.rows,
    required this.empty,
    super.key,
  });

  final List<String> columns;
  final List<List<String>> rows;
  final String empty;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Text(empty),
      );
    }

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: DataTable(
        columns: columns
            .map((column) => DataColumn(label: Text(column)))
            .toList(),
        rows: rows
            .map(
              (row) => DataRow(
                cells: row
                    .map((cell) => DataCell(SelectableText(cell)))
                    .toList(),
              ),
            )
            .toList(),
      ),
    );
  }
}