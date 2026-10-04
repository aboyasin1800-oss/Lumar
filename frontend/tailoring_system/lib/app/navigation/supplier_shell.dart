import 'package:flutter/material.dart';

import '../../shared/widgets/shell_page.dart';

class SupplierShell extends StatefulWidget {
  const SupplierShell({super.key});

  @override
  State<SupplierShell> createState() => _SupplierShellState();
}

class _SupplierShellState extends State<SupplierShell> {
  int _selectedIndex = 0;

  final List<_ShellTab> _tabs = const [
    _ShellTab(label: 'Home', icon: Icons.home_outlined),
    _ShellTab(label: 'Orders', icon: Icons.shopping_bag_outlined),
    _ShellTab(label: 'Payments', icon: Icons.payment_outlined),
    _ShellTab(label: 'Messages', icon: Icons.message_outlined),
    _ShellTab(label: 'Profile', icon: Icons.person_outline),
  ];

  final Map<String, List<String>> _sections = const {
    'Home': ['Dashboard', 'Orders summary', 'Alerts'],
    'Orders': ['Open orders', 'Accepted orders', 'Delivery status'],
    'Payments': ['Pending payment', 'Invoices', 'Settlement'],
    'Messages': ['Supplier chat', 'Announcements', 'Support'],
    'Profile': ['Vendor profile', 'Preferences', 'Support'],
  };

  void _selectTab(int index) {
    if (index < 0 || index >= _tabs.length) {
      return;
    }

    setState(() => _selectedIndex = index);
  }

  @override
  Widget build(BuildContext context) {
    final tab = _tabs[_selectedIndex];
    final items = _sections[tab.label] ?? const <String>[];

    return ShellPage(
      title: 'Supplier Shell',
      icon: Icons.local_shipping_outlined,
      subtitle: 'Supplier local navigation preview',
      drawer: Drawer(
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            const DrawerHeader(
              decoration: BoxDecoration(color: Colors.green),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.inventory_2_rounded, size: 42, color: Colors.white),
                  SizedBox(height: 8),
                  Text('Supplier', style: TextStyle(color: Colors.white, fontSize: 20)),
                ],
              ),
            ),
            ...List.generate(_tabs.length, (index) {
              final item = _tabs[index];
              return ListTile(
                leading: Icon(item.icon),
                title: Text(item.label),
                selected: _selectedIndex == index,
                onTap: () {
                  _selectTab(index);
                  Navigator.of(context).pop();
                },
              );
            }),
          ],
        ),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          ShellInfoCard(
            title: tab.label,
            caption: 'Local-only shell navigation. No business logic or backend calls.',
            trailing: Icon(tab.icon),
          ),
          ...items.map(
            (item) => ShellInfoCard(
              title: item,
              caption: 'Placeholder content for the supplier flow.',
            ),
          ),
        ],
      ),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _selectedIndex,
        onTap: _selectTab,
        type: BottomNavigationBarType.fixed,
        items: List.generate(_tabs.length, (index) {
          return BottomNavigationBarItem(
            icon: Icon(_tabs[index].icon),
            label: _tabs[index].label,
          );
        }),
      ),
    );
  }
}

class _ShellTab {
  const _ShellTab({
    required this.label,
    required this.icon,
  });

  final String label;
  final IconData icon;
}
