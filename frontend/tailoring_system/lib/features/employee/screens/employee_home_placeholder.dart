import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../models/employee_models.dart';
import '../../../repositories/employee_repository.dart';
import '../../../services/auth_state.dart';

class EmployeeHomeScreen extends StatefulWidget {
  const EmployeeHomeScreen({super.key});

  @override
  State<EmployeeHomeScreen> createState() => _EmployeeHomeScreenState();
}

class _EmployeeHomeScreenState extends State<EmployeeHomeScreen> {
  final EmployeeRepository _repository = EmployeeRepository();
  late Future<_EmployeeDashboardData> _future;

  @override
  void initState() {
    super.initState();
    _future = _loadDashboard();
  }

  Future<_EmployeeDashboardData> _loadDashboard() async {
    final employeeId = AuthState.instance.user?.employeeId;
    if (employeeId == null) {
      throw StateError('لم يتم ربط حساب الموظف الحالي بأي موظف في النظام.');
    }

    final employee = await _repository.getEmployee(employeeId);
    final attendance = await _repository.getAttendance(employeeId);
    final leaveRequests = await _repository.getLeaveRequests(employeeId);

    final present = attendance.where((item) => !item.isAbsent).length;
    final absent = attendance.where((item) => item.isAbsent).length;

    return _EmployeeDashboardData(
      employee: employee,
      attendanceCount: attendance.length,
      presentCount: present,
      absentCount: absent,
      leaveRequests: leaveRequests.length,
    );
  }

  void _reload() => setState(() => _future = _loadDashboard());

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<_EmployeeDashboardData>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }

        if (snapshot.hasError) {
          final error = snapshot.error.toString();
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.person_off_outlined, size: 48),
                  const SizedBox(height: 16),
                  Text(
                    error.contains('لم يتم ربط')
                        ? 'لم يتم ربط هذا الحساب بموظف.'
                        : 'تعذّر تحميل بيانات الموظف.',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: _reload,
                    icon: const Icon(Icons.refresh),
                    label: const Text('إعادة المحاولة'),
                  ),
                ],
              ),
            ),
          );
        }

        final data = snapshot.data!;
        final employee = data.employee;
        final statusLabel = _employeeStatus(employee.status);
        final hireDate = DateFormat('yyyy/MM/dd').format(employee.hireDate);

        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        CircleAvatar(
                          radius: 28,
                          backgroundColor: Colors.blue.shade100,
                          child: Text(
                            employee.name.isNotEmpty ? employee.name.substring(0, 1) : 'م',
                            style: const TextStyle(
                              fontSize: 24,
                              fontWeight: FontWeight.bold,
                              color: Colors.blue,
                            ),
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                employee.fullName,
                                style: const TextStyle(
                                  fontSize: 20,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              Text(employee.jobTitle ?? 'بدون مسمى'),
                              const SizedBox(height: 6),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                decoration: BoxDecoration(
                                  color: employee.isActive == false ? Colors.red.shade50 : Colors.green.shade50,
                                  borderRadius: BorderRadius.circular(999),
                                ),
                                child: Text(
                                  statusLabel,
                                  style: TextStyle(
                                    color: employee.isActive == false ? Colors.red : Colors.green.shade800,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: _SummaryTile(
                    title: 'الحضور',
                    value: '${data.presentCount}',
                    subtitle: 'سجل حضور',
                    icon: Icons.calendar_month_outlined,
                    color: Colors.green,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _SummaryTile(
                    title: 'الغياب',
                    value: '${data.absentCount}',
                    subtitle: 'سجل غياب',
                    icon: Icons.event_busy_outlined,
                    color: Colors.orange,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: _SummaryTile(
                    title: 'الإجازات',
                    value: '${data.leaveRequests}',
                    subtitle: 'طلب إجازة',
                    icon: Icons.beach_access_outlined,
                    color: Colors.blue,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _SummaryTile(
                    title: 'تاريخ التوظيف',
                    value: hireDate,
                    subtitle: 'تاريخ الانضمام',
                    icon: Icons.date_range_outlined,
                    color: Colors.purple,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            _InfoCard(
              title: 'معلومات الموظف',
              items: [
                _InfoItem(label: 'رقم الموظف', value: employee.code),
                _InfoItem(label: 'الهاتف', value: employee.phone ?? employee.phoneNumber ?? 'غير متوفر'),
                _InfoItem(label: 'الراتب الأساسي', value: '${employee.basicSalary.toStringAsFixed(2)} ر.س'),
                _InfoItem(label: 'نوع الراتب', value: _salaryType(employee.salaryType ?? '')),
              ],
            ),
          ],
        );
      },
    );
  }
}

String _employeeStatus(String value) {
  final normalized = value.trim().toLowerCase();
  return switch (normalized) {
    'active' => 'نشط',
    'suspended' => 'موقوف',
    'onleave' || 'on_leave' => 'مجاز',
    'terminated' => 'منتهي الخدمة',
    _ => value.isEmpty ? 'غير محدد' : value,
  };
}

String _salaryType(String value) {
  final normalized = value.trim().toLowerCase();
  return switch (normalized) {
    'basicsalary' || 'basic' => 'راتب أساسي',
    'piecewage' || 'piece' => 'أجر قطعة',
    'salarypluspiece' || 'basicpluspiece' => 'راتب أساسي + أجر قطعة',
    _ => value.isEmpty ? 'غير محدد' : value,
  };
}

class _EmployeeDashboardData {
  const _EmployeeDashboardData({
    required this.employee,
    required this.attendanceCount,
    required this.presentCount,
    required this.absentCount,
    required this.leaveRequests,
  });

  final EmployeeDetails employee;
  final int attendanceCount;
  final int presentCount;
  final int absentCount;
  final int leaveRequests;
}

class _SummaryTile extends StatelessWidget {
  const _SummaryTile({
    required this.title,
    required this.value,
    required this.subtitle,
    required this.icon,
    required this.color,
  });

  final String title;
  final String value;
  final String subtitle;
  final IconData icon;
  final MaterialColor color;

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
                Icon(icon, color: color),
                const SizedBox(width: 8),
                Text(title, style: const TextStyle(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              value,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 4),
            Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.title, required this.items});

  final String title;
  final List<_InfoItem> items;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 12),
            ...items.map(
              (item) => Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      flex: 2,
                      child: Text(
                        item.label,
                        style: const TextStyle(color: Colors.grey),
                      ),
                    ),
                    Expanded(
                      flex: 3,
                      child: Text(
                        item.value,
                        textAlign: TextAlign.start,
                        style: const TextStyle(fontWeight: FontWeight.w500),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _InfoItem {
  const _InfoItem({required this.label, required this.value});

  final String label;
  final String value;
}
