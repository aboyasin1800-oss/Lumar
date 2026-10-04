import 'package:flutter/material.dart';

import '../../shared/widgets/shell_page.dart';

class CustomerShell extends StatefulWidget {
  const CustomerShell({super.key});

  @override
  State<CustomerShell> createState() => _CustomerShellState();
}

class _CustomerShellState extends State<CustomerShell> {
  int _selectedIndex = 0;

  final List<_ShellTab> _tabs = const [
    _ShellTab(label: 'Home', icon: Icons.home_outlined),
    _ShellTab(label: 'Orders', icon: Icons.receipt_long_outlined),
    _ShellTab(label: 'Invoices', icon: Icons.receipt_outlined),
    _ShellTab(label: 'Profile', icon: Icons.person_outline),
  ];

  final Map<String, List<String>> _sections = const {
    'Home': ['Overview', 'Account summary', 'Recent activity'],
    'Orders': ['Current orders', 'Order history', 'Track delivery'],
    'Invoices': ['Payments', 'Receipts', 'Due invoices'],
    'Profile': ['Personal data', 'Preferences', 'Support'],
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
      title: 'Customer Shell',
      icon: Icons.person_outline,
      subtitle: 'Customer local navigation preview',
      drawer: Drawer(
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            const DrawerHeader(
              decoration: BoxDecoration(color: Colors.blueAccent),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.account_circle_rounded, size: 42, color: Colors.white),
                  SizedBox(height: 8),
                  Text('Customer', style: TextStyle(color: Colors.white, fontSize: 20)),
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
              caption: 'Placeholder content for the customer flow.',
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
