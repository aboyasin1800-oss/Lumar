import 'package:flutter/material.dart';

import '../../app/navigation/app_router.dart';

class RoleGate extends StatelessWidget {
  const RoleGate({super.key});

  @override
  Widget build(BuildContext context) {
    final entries = <_RoleEntry>[
      _RoleEntry(
        title: 'Customer Shell',
        subtitle: 'Role placeholder for customer-facing mobile experience',
        icon: Icons.person_outline,
        route: AppRouter.customerShell,
      ),
      _RoleEntry(
        title: 'Employee Shell',
        subtitle: 'Role placeholder for employee workflow and tasks',
        icon: Icons.badge_outlined,
        route: AppRouter.employeeShell,
      ),
      _RoleEntry(
        title: 'Supplier Shell',
        subtitle: 'Role placeholder for supplier order and payment workflow',
        icon: Icons.local_shipping_outlined,
        route: AppRouter.supplierShell,
      ),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('Unified Mobile App'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Select a shell preview',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text(
              'This is a UI-only foundation preview. No auth, session, or backend logic.',
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
