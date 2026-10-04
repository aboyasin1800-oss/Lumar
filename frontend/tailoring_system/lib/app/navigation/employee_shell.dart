import 'package:flutter/material.dart';

import '../../shared/widgets/shell_page.dart';

class EmployeeShell extends StatefulWidget {
  const EmployeeShell({super.key});

  @override
  State<EmployeeShell> createState() => _EmployeeShellState();
}

class _EmployeeShellState extends State<EmployeeShell> {
  int _selectedIndex = 0;

  final List<_ShellTab> _tabs = const [
    _ShellTab(label: 'Home', icon: Icons.home_outlined),
    _ShellTab(label: 'Tasks', icon: Icons.task_alt_outlined),
    _ShellTab(label: 'Attendance', icon: Icons.calendar_month_outlined),
    _ShellTab(label: 'Payroll', icon: Icons.payments_outlined),
    _ShellTab(label: 'Profile', icon: Icons.person_outline),
  ];

  final Map<String, List<String>> _sections = const {
    'Home': ['Today overview', 'Inbox', 'Recent updates'],
    'Tasks': ['Assigned tasks', 'To do list', 'Approvals'],
    'Attendance': ['Presence record', 'Shift summary', 'Schedule'],
    'Payroll': ['Salary view', 'Payslips', 'Bank details'],
    'Profile': ['Personal details', 'Shift preferences', 'Support'],
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
      title: 'Employee Shell',
      icon: Icons.badge_outlined,
      subtitle: 'Employee local navigation preview',
      drawer: Drawer(
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            const DrawerHeader(
              decoration: BoxDecoration(color: Colors.deepPurple),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.badge_rounded, size: 42, color: Colors.white),
                  SizedBox(height: 8),
                  Text('Employee', style: TextStyle(color: Colors.white, fontSize: 20)),
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
              caption: 'Placeholder content for the employee flow.',
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
