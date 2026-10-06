import 'package:flutter/material.dart';

class CustomerOrdersScreen extends StatelessWidget {
  const CustomerOrdersScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final orders = [
      _OrderItem(
        id: 'ORD-000125',
        status: 'قيد التنفيذ',
        date: '12 مايو 2026',
        details: 'تم تجهيز الطلب وجاري التوصيل',
        color: Colors.blue,
      ),
      _OrderItem(
        id: 'ORD-000121',
        status: 'مكتمل',
        date: '09 مايو 2026',
        details: 'تم استلام الطلب بنجاح',
        color: Colors.green,
      ),
      _OrderItem(
        id: 'ORD-000118',
        status: 'جاهز للتسليم',
        date: '04 مايو 2026',
        details: 'بانتظار الموعد المخصص',
        color: Colors.orange,
      ),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'الطلبات',
          style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 16),
        ...orders.map(
          (order) => Card(
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
                          order.id,
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
                          color: order.color.withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Text(
                          order.status,
                          style: TextStyle(
                            color: order.color,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Text(
                    order.date,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    order.details,
                    style: const TextStyle(fontSize: 14),
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

class _OrderItem {
  const _OrderItem({
    required this.id,
    required this.status,
    required this.date,
    required this.details,
    required this.color,
  });

  final String id;
  final String status;
  final String date;
  final String details;
  final Color color;
}
