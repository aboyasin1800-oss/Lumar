import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../features/employee/screens/employee_home_placeholder.dart';
import '../../repositories/employee_repository.dart';
import '../../repositories/payroll_repository.dart';
import '../../services/auth_state.dart';
import '../../shared/widgets/shell_page.dart';

class EmployeeShell extends StatefulWidget {
  const EmployeeShell({super.key});

  @override
  State<EmployeeShell> createState() => _EmployeeShellState();
}

class _EmployeeShellState extends State<EmployeeShell> {
  int _selectedIndex = 0;

  final List<_ShellTab> _tabs = const [
    _ShellTab(label: 'الرئيسية', icon: Icons.home_outlined),
    _ShellTab(label: 'الحضور', icon: Icons.calendar_month_outlined),
    _ShellTab(label: 'الراتب', icon: Icons.payments_outlined),
    _ShellTab(label: 'الإنتاج', icon: Icons.insights_outlined),
    _ShellTab(label: 'الملف', icon: Icons.person_outline),
  ];

  Widget _buildBody() {
    switch (_selectedIndex) {
      case 0:
        return const EmployeeHomeScreen();
      case 1:
        return const _CurrentEmployeeAttendanceScreen();
      case 2:
        return const _CurrentEmployeePayrollScreen();
      case 3:
        return const _CurrentEmployeeProductionScreen();
      case 4:
      default:
        return const _CurrentEmployeeProfileScreen();
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
      title: 'بوابة الموظف',
      icon: Icons.badge_outlined,
      subtitle: 'بيانات الموظف الحالي فقط',
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
                  Text('الموظف', style: TextStyle(color: Colors.white, fontSize: 20)),
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

class _CurrentEmployeeAttendanceScreen extends StatefulWidget {
  const _CurrentEmployeeAttendanceScreen();

  @override
  State<_CurrentEmployeeAttendanceScreen> createState() => _CurrentEmployeeAttendanceScreenState();
}

class _CurrentEmployeeAttendanceScreenState extends State<_CurrentEmployeeAttendanceScreen> {
  final EmployeeRepository _repository = EmployeeRepository();
  late Future<List<_AttendanceItem>> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<_AttendanceItem>> _load() async {
    final employeeId = AuthState.instance.user?.employeeId;
    if (employeeId == null) {
      throw StateError('لم يتم ربط هذا الحساب بموظف.');
    }

    final attendance = await _repository.getAttendance(employeeId);
    return attendance
        .map((item) => _AttendanceItem(
              date: DateFormat('yyyy/MM/dd').format(item.attendanceDate),
              status: item.isAbsent ? 'غياب' : 'حضور',
              hours: item.workedHours,
              overtime: item.overtimeHours,
            ))
        .toList()
      ..sort((a, b) => b.date.compareTo(a.date));
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<_AttendanceItem>>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _CurrentEmployeeError(message: snapshot.error.toString(), onRetry: _reload);
        }

        final rows = snapshot.data ?? const <_AttendanceItem>[];
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: ListTile(
                leading: const Icon(Icons.calendar_month_outlined, color: Colors.blue),
                title: const Text('الحضور الشخصي'),
                subtitle: Text('${rows.length} سجل حضور'),
              ),
            ),
            const SizedBox(height: 12),
            if (rows.isEmpty)
              const Card(
                child: Padding(
                  padding: EdgeInsets.all(16),
                  child: Text('لا توجد سجلات حضور لهذا الموظف.'),
                ),
              )
            else
              ...rows.map(
                (row) => Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    title: Text(row.date),
                    subtitle: Text('${row.status} • الساعات: ${row.hours.toStringAsFixed(1)} • إضـافـي: ${row.overtime.toStringAsFixed(1)}'),
                  ),
                ),
              ),
          ],
        );
      },
    );
  }
}

class _CurrentEmployeePayrollScreen extends StatefulWidget {
  const _CurrentEmployeePayrollScreen();

  @override
  State<_CurrentEmployeePayrollScreen> createState() => _CurrentEmployeePayrollScreenState();
}

class _CurrentEmployeePayrollScreenState extends State<_CurrentEmployeePayrollScreen> {
  final EmployeeRepository _employeeRepository = EmployeeRepository();
  late Future<_CurrentPayrollSummary> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_CurrentPayrollSummary> _load() async {
    final employeeId = AuthState.instance.user?.employeeId;
    if (employeeId == null) {
      throw StateError('لم يتم ربط هذا الحساب بموظف.');
    }

    final employee = await _employeeRepository.getEmployee(employeeId);
    final attendance = await _employeeRepository.getAttendance(employeeId);
    final workedHours = attendance.fold<double>(0, (sum, item) => sum + item.workedHours);
    final overtime = attendance.fold<double>(0, (sum, item) => sum + item.overtimeHours);

    return _CurrentPayrollSummary(
      employee: employee,
      grossSalary: employee.basicSalary,
      workedHours: workedHours,
      overtimeHours: overtime,
    );
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<_CurrentPayrollSummary>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _CurrentEmployeeError(message: snapshot.error.toString(), onRetry: _reload);
        }

        final summary = snapshot.data!;
        final employee = summary.employee;

        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      employee.fullName,
                      style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text('الراتب الأساسي: ${employee.basicSalary.toStringAsFixed(2)} ر.س'),
                    const SizedBox(height: 4),
                    Text('نوع الراتب: ${_salaryTypeLabel(employee.salaryType ?? '')}'),
                    const SizedBox(height: 4),
                    Text('الساعات المسجلة: ${summary.workedHours.toStringAsFixed(1)}'),
                    const SizedBox(height: 4),
                    Text('ساعات إضافية: ${summary.overtimeHours.toStringAsFixed(1)}'),
                  ],
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

class _CurrentEmployeeProductionScreen extends StatefulWidget {
  const _CurrentEmployeeProductionScreen();

  @override
  State<_CurrentEmployeeProductionScreen> createState() => _CurrentEmployeeProductionScreenState();
}

class _CurrentEmployeeProductionScreenState extends State<_CurrentEmployeeProductionScreen> {
  final PayrollRepository _repository = PayrollRepository();
  late Future<List<_ProductionRow>> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<List<_ProductionRow>> _load() async {
    final employeeId = AuthState.instance.user?.employeeId;
    if (employeeId == null) {
      throw StateError('لم يتم ربط هذا الحساب بموظف.');
    }

    final wages = await _repository.getPieceWages();
    final rows = wages.where((item) => item.employeeId == employeeId).toList();
    return rows
        .map((item) => _ProductionRow(
              pieceType: item.pieceType,
              stage: item.stage,
              quantity: item.quantity,
              totalWage: item.totalWage,
            ))
        .toList();
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<_ProductionRow>>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _CurrentEmployeeError(message: snapshot.error.toString(), onRetry: _reload);
        }

        final rows = snapshot.data ?? const <_ProductionRow>[];
        final total = rows.fold<double>(0, (sum, item) => sum + item.totalWage);

        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: ListTile(
                leading: const Icon(Icons.insights_outlined, color: Colors.blueAccent),
                title: const Text('الإنتاج الشخصي'),
                subtitle: Text('إجمالي مستحقك: ${total.toStringAsFixed(2)} ر.س'),
              ),
            ),
            const SizedBox(height: 12),
            if (rows.isEmpty)
              const Card(
                child: Padding(
                  padding: EdgeInsets.all(16),
                  child: Text('لا توجد سجلات إنتاج مرتبطة بهذا الموظف.'),
                ),
              )
            else
              ...rows.map(
                (row) => Card(
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    title: Text(row.pieceType),
                    subtitle: Text('المرحلة: ${row.stage} • الكمية: ${row.quantity.toStringAsFixed(1)}'),
                    trailing: Text('${row.totalWage.toStringAsFixed(2)} ر.س'),
                  ),
                ),
              ),
          ],
        );
      },
    );
  }
}

class _CurrentEmployeeProfileScreen extends StatefulWidget {
  const _CurrentEmployeeProfileScreen();

  @override
  State<_CurrentEmployeeProfileScreen> createState() => _CurrentEmployeeProfileScreenState();
}

class _CurrentEmployeeProfileScreenState extends State<_CurrentEmployeeProfileScreen> {
  final EmployeeRepository _repository = EmployeeRepository();
  late Future<dynamic> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<dynamic> _load() async {
    final employeeId = AuthState.instance.user?.employeeId;
    if (employeeId == null) {
      throw StateError('لم يتم ربط هذا الحساب بموظف.');
    }
    return _repository.getEmployee(employeeId);
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) {
    return FutureBuilder(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _CurrentEmployeeError(message: snapshot.error.toString(), onRetry: _reload);
        }

        final employee = snapshot.data as dynamic;
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      employee.fullName,
                      style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 10),
                    Text('الرقم: ${employee.code}'),
                    Text('المسمى: ${employee.jobTitle ?? 'غير محدد'}'),
                    Text('الهاتف: ${employee.phone ?? employee.phoneNumber ?? 'غير متوفر'}'),
                    Text('البريد: ${employee.email ?? 'غير متوفر'}'),
                    Text('العنوان: ${employee.address ?? 'غير متوفر'}'),
                  ],
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

class _CurrentEmployeeError extends StatelessWidget {
  const _CurrentEmployeeError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 44),
            const SizedBox(height: 12),
            Text(
              message.contains('لم يتم ربط')
                  ? 'لم يتم ربط هذا الحساب بموظف.'
                  : 'تعذّر تحميل بياناتك.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('إعادة المحاولة'),
            ),
          ],
        ),
      ),
    );
  }
}

String _salaryTypeLabel(String value) {
  final normalized = value.trim().toLowerCase();
  return switch (normalized) {
    'basicsalary' || 'basic' => 'راتب أساسي',
    'piecewage' || 'piece' => 'أجر قطعة',
    'salarypluspiece' || 'basicpluspiece' => 'راتب أساسي + أجر قطعة',
    _ => value.isEmpty ? 'غير محدد' : value,
  };
}

class _AttendanceItem {
  const _AttendanceItem({
    required this.date,
    required this.status,
    required this.hours,
    required this.overtime,
  });

  final String date;
  final String status;
  final double hours;
  final double overtime;
}

class _CurrentPayrollSummary {
  const _CurrentPayrollSummary({
    required this.employee,
    required this.grossSalary,
    required this.workedHours,
    required this.overtimeHours,
  });

  final dynamic employee;
  final double grossSalary;
  final double workedHours;
  final double overtimeHours;
}

class _ProductionRow {
  const _ProductionRow({
    required this.pieceType,
    required this.stage,
    required this.quantity,
    required this.totalWage,
  });

  final String pieceType;
  final String stage;
  final double quantity;
  final double totalWage;
}

class _ShellTab {
  const _ShellTab({
    required this.label,
    required this.icon,
  });

  final String label;
  final IconData icon;
}
