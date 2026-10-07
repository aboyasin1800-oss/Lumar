import 'package:flutter/material.dart';

import '../../screens/attendance/attendance_screen.dart';
import '../../screens/employees_screen.dart';
import '../../screens/payroll_screen.dart';
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

  Widget _buildBody() {
    switch (_selectedIndex) {
      case 0:
        return const EmployeesScreen(initialTab: 0);
      case 1:
        return const EmployeesScreen(initialTab: 0);
      case 2:
        return const AttendanceScreen();
      case 3:
        return const PayrollScreen();
      case 4:
      default:
        return const EmployeesScreen(initialTab: 0);
    }
  }

  void _selectTab(int index) {
    if (index < 0 || index >= _tabs.length) {
      return;
    }

    setState(() => _selectedIndex = index);
  }

  @override
  Widget build(BuildContext context) {
    return ShellPage(
      title: 'Employee Shell',
      icon: Icons.badge_outlined,
      subtitle: 'Employee operational overview',
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
      body: _buildBody(),
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
