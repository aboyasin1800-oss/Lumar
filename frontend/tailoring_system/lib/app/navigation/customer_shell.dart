import 'package:flutter/material.dart';

import '../../features/customer/screens/customer_home_placeholder.dart';
import '../../features/customer/screens/customer_invoices_screen.dart';
import '../../features/customer/screens/customer_orders_screen.dart';
import '../../features/customer/screens/customer_profile_screen.dart';
import '../../services/auth_state.dart';
import '../../shared/widgets/shell_page.dart';

class CustomerShell extends StatefulWidget {
  const CustomerShell({required this.auth, super.key});

  final AuthState auth;

  @override
  State<CustomerShell> createState() => _CustomerShellState();
}

class _CustomerShellState extends State<CustomerShell> {
  int _selectedIndex = 0;

  final List<_ShellTab> _tabs = const [
    _ShellTab(label: 'الرئيسية', icon: Icons.home_outlined),
    _ShellTab(label: 'الطلبات', icon: Icons.receipt_long_outlined),
    _ShellTab(label: 'الفواتير', icon: Icons.receipt_outlined),
    _ShellTab(label: 'الملف الشخصي', icon: Icons.person_outline),
  ];

  void _selectTab(int index) {
    if (index < 0 || index >= _tabs.length) {
      return;
    }

    setState(() => _selectedIndex = index);
  }

  @override
  Widget build(BuildContext context) {
    final body = _selectedIndex == 0
        ? const CustomerHomeScreen()
        : _selectedIndex == 1
            ? const CustomerOrdersScreen()
            : _selectedIndex == 2
                ? const CustomerInvoicesScreen()
                : CustomerProfileScreen(auth: widget.auth);

    return ShellPage(
      title: 'عميل',
      icon: Icons.person_outline,
      subtitle: 'لوحة العميل',
      drawer: Drawer(
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            const DrawerHeader(
              decoration: BoxDecoration(color: Colors.blueAccent),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.account_circle_rounded,
                      size: 42, color: Colors.white),
                  SizedBox(height: 8),
                  Text('العميل',
                      style: TextStyle(color: Colors.white, fontSize: 20)),
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
      body: body,
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
