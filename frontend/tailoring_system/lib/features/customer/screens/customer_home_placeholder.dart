import 'package:flutter/material.dart';

class CustomerHomeScreen extends StatelessWidget {
  const CustomerHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final items = [
      _MetricCard(
        title: 'الطلبات الحالية',
        value: '08',
        subtitle: 'قيد التنفيذ',
        color: Colors.blue,
      ),
      _MetricCard(
        title: 'الطلبات المكتملة',
        value: '24',
        subtitle: 'هذا الشهر',
        color: Colors.green,
      ),
      _MetricCard(
        title: 'الفواتير المفتوحة',
        value: '03',
        subtitle: 'مطلوب سداد',
        color: Colors.orange,
      ),
      _MetricCard(
        title: 'آخر طلب',
        value: '#1842',
        subtitle: 'قبل 2 يوم',
        color: Colors.purple,
      ),
    ];

    final activities = [
      _ActivityRow(
        title: 'تم تجهيز الطلب 1842',
        time: 'منذ 2 ساعة',
        icon: Icons.local_shipping_outlined,
      ),
      _ActivityRow(
        title: 'تم إيداع دفعة فاتورة 1200',
        time: 'اليوم',
        icon: Icons.payments_outlined,
      ),
      _ActivityRow(
        title: 'تم تحديث حالة الطلب 1825',
        time: 'أمس',
        icon: Icons.check_circle_outline,
      ),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _GreetingCard(),
        const SizedBox(height: 16),
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 12,
          crossAxisSpacing: 12,
          childAspectRatio: 1.35,
          children: items,
        ),
        const SizedBox(height: 16),
        const Text(
          'الإجراءات السريعة',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: _QuickActionCard(
                title: 'طلب جديد',
                icon: Icons.add_shopping_cart_outlined,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _QuickActionCard(
                title: 'الفواتير',
                icon: Icons.receipt_long_outlined,
              ),
            ),
          ],
        ),
        const SizedBox(height: 18),
        const Text(
          'آخر النشاطات',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 10),
        ...activities.map((activity) => _ActivityCard(activity: activity)),
      ],
    );
  }
}

class _GreetingCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Card(
      color: Theme.of(context).colorScheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            CircleAvatar(
              radius: 28,
              backgroundColor: Colors.white24,
              child: const Icon(Icons.person_outline, size: 28),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  Text(
                    'مرحباً، عبد الرحمن',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                  ),
                  SizedBox(height: 4),
                  Text(
                    'تحديث سريع عن طلباتك والفواتير',
                    style: TextStyle(fontSize: 13),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.title,
    required this.value,
    required this.subtitle,
    required this.color,
  });

  final String title;
  final String value;
  final String subtitle;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  width: 10,
                  height: 10,
                  decoration: BoxDecoration(
                    color: color,
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 18),
            Text(
              value,
              style: const TextStyle(
                fontSize: 28,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              subtitle,
              style: TextStyle(
                fontSize: 12,
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _QuickActionCard extends StatelessWidget {
  const _QuickActionCard({required this.title, required this.icon});

  final String title;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 28),
            const SizedBox(height: 10),
            Text(
              title,
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
          ],
        ),
      ),
    );
  }
}

class _ActivityRow {
  const _ActivityRow({
    required this.title,
    required this.time,
    required this.icon,
  });

  final String title;
  final String time;
  final IconData icon;
}

class _ActivityCard extends StatelessWidget {
  const _ActivityCard({required this.activity});

  final _ActivityRow activity;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: Theme.of(context).colorScheme.primaryContainer,
          child: Icon(activity.icon),
        ),
        title: Text(activity.title),
        subtitle: Text(activity.time),
      ),
    );
  }
}
