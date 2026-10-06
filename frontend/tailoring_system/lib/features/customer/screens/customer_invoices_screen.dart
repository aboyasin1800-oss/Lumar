import 'package:flutter/material.dart';

class CustomerInvoicesScreen extends StatelessWidget {
  const CustomerInvoicesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final invoices = [
      _InvoiceItem(
        number: 'INV-2026-042',
        total: '1,250 ر.س',
        dueDate: '15 مايو 2026',
        status: 'مفتوح',
        color: Colors.orange,
      ),
      _InvoiceItem(
        number: 'INV-2026-038',
        total: '860 ر.س',
        dueDate: '09 مايو 2026',
        status: 'مدفوع',
        color: Colors.green,
      ),
      _InvoiceItem(
        number: 'INV-2026-031',
        total: '2,140 ر.س',
        dueDate: '01 مايو 2026',
        status: 'متأخر',
        color: Colors.red,
      ),
    ];

    final totalOpen = 1250;
    final totalPaid = 860;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'الفواتير',
          style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(
              child: _SummaryCard(
                title: 'مفتوحة',
                value: '$totalOpen ر.س',
                color: Colors.orange,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _SummaryCard(
                title: 'مدفوعة',
                value: '$totalPaid ر.س',
                color: Colors.green,
              ),
            ),
          ],
        ),
        const SizedBox(height: 20),
        ...invoices.map(
          (invoice) => Card(
            margin: const EdgeInsets.only(bottom: 12),
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          invoice.number,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 6,
                        ),
                        decoration: BoxDecoration(
                          color: invoice.color.withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Text(
                          invoice.status,
                          style: TextStyle(
                            color: invoice.color,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Icon(Icons.payments_outlined, size: 18),
                      const SizedBox(width: 8),
                      Text(
                        'الإجمالي: ${invoice.total}',
                        style: const TextStyle(fontSize: 15),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      const Icon(Icons.calendar_today_outlined, size: 18),
                      const SizedBox(width: 8),
                      Text(
                        'تاريخ الاستحقاق: ${invoice.dueDate}',
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.title,
    required this.value,
    required this.color,
  });

  final String title;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: TextStyle(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              value,
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: color,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _InvoiceItem {
  const _InvoiceItem({
    required this.number,
    required this.total,
    required this.dueDate,
    required this.status,
    required this.color,
  });

  final String number;
  final String total;
  final String dueDate;
  final String status;
  final Color color;
}
