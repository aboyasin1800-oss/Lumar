import 'package:flutter/material.dart';

import '../../app/navigation/app_router.dart';

class RoleGate extends StatelessWidget {
  const RoleGate({super.key});

  @override
  Widget build(BuildContext context) {
    final entries = <_RoleEntry>[
      _RoleEntry(
        title: 'حساب العميل',
        subtitle: 'واجهة العميل الشخصية فقط',
        icon: Icons.person_outline,
        route: AppRouter.customerShell,
      ),
      _RoleEntry(
        title: 'حساب الموظف',
        subtitle: 'واجهة الموظف الحالية فقط',
        icon: Icons.badge_outlined,
        route: AppRouter.employeeShell,
      ),
      _RoleEntry(
        title: 'حساب المورد',
        subtitle: 'واجهة المورد الشخصية فقط',
        icon: Icons.local_shipping_outlined,
        route: AppRouter.supplierShell,
      ),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('التطبيق الموحد'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'اختر نوع الحساب',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text(
              'واجهة أولية قيد التطوير ولا تحتوي حالياً على وظائف تشغيلية.',
              style: TextStyle(fontSize: 14, color: Colors.grey),
            ),
            const SizedBox(height: 24),
            ...entries.map(
              (entry) => Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Card(
                  child: ListTile(
                    leading: Icon(entry.icon),
                    title: Text(entry.title),
                    subtitle: Text(entry.subtitle),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.of(context).pushNamed(entry.route),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RoleEntry {
  const _RoleEntry({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.route,
  });

  final String title;
  final String subtitle;
  final IconData icon;
  final String route;
}
