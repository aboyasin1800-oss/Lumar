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
    _ShellTab(label: 'الرئيسية', icon: Icons.home_outlined),
    _ShellTab(label: 'الطلبات', icon: Icons.shopping_bag_outlined),
    _ShellTab(label: 'المدفوعات', icon: Icons.payment_outlined),
    _ShellTab(label: 'الرسائل', icon: Icons.message_outlined),
    _ShellTab(label: 'الملف الشخصي', icon: Icons.person_outline),
  ];

  final Map<String, List<String>> _sections = const {
    'الرئيسية': ['لوحة المعلومات', 'ملخص الطلبات', 'التنبيهات'],
    'الطلبات': ['الطلبات المفتوحة', 'الطلبات المقبولة', 'حالة التسليم'],
    'المدفوعات': ['المدفوعات المعلقة', 'الفواتير', 'التسوية'],
    'الرسائل': ['الدردشة', 'الإعلانات', 'الدعم'],
    'الملف الشخصي': ['ملف المورد', 'التفضيلات', 'الدعم'],
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
      title: 'حساب المورد',
      icon: Icons.local_shipping_outlined,
      subtitle: 'واجهة أولية قيد التطوير',
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
                  Text('المورد', style: TextStyle(color: Colors.white, fontSize: 20)),
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
            caption: 'واجهة أولية قيد التطوير ولا تحتوي حالياً على وظائف تشغيلية.',
            trailing: Icon(tab.icon),
          ),
          ...items.map(
            (item) => ShellInfoCard(
              title: item,
              caption: 'محتوى مؤقت لواجهة المورد.',
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
