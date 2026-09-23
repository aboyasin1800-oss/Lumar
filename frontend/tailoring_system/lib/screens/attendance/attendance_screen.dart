import 'package:flutter/material.dart';

import '../../models/employee_models.dart';
import '../../repositories/employee_repository.dart';

class AttendanceScreen extends StatefulWidget {
  const AttendanceScreen({super.key, this.repository});

  final EmployeeRepository? repository;

  @override
  State<AttendanceScreen> createState() => _AttendanceScreenState();
}

class _AttendanceScreenState extends State<AttendanceScreen> {
  late final EmployeeRepository _repository;
  late Future<List<_AttendanceRow>> _future;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? EmployeeRepository();
    _future = _load();
  }

  Future<List<_AttendanceRow>> _load() async {
    final employees = await _repository.getEmployees();
    final rows = await Future.wait(employees.map((employee) async {
      final attendance = await _repository.getAttendance(employee.id);
      final present = attendance.where((item) => !item.isAbsent).length;
      final absent = attendance.where((item) => item.isAbsent).length;
      final overtime = attendance.fold<double>(0, (sum, item) => sum + item.overtimeHours);
      return _AttendanceRow(
        employee: employee,
        present: present,
        absent: absent,
        overtimeHours: overtime,
      );
    }));
    return rows..sort((a, b) => a.employee.name.compareTo(b.employee.name));
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('الحضور'),
          actions: [
            IconButton(
              tooltip: 'تحديث',
              onPressed: _reload,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: FutureBuilder<List<_AttendanceRow>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.cloud_off_outlined, size: 42),
                    const SizedBox(height: 12),
                    Text('تعذر تحميل الحضور:\n${snapshot.error}'),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _reload,
                      icon: const Icon(Icons.refresh),
                      label: const Text('إعادة المحاولة'),
                    ),
                  ],
                ),
              );
            }

            final rows = snapshot.data ?? const <_AttendanceRow>[];
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        const Icon(Icons.calendar_month_outlined),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('ملخص الحضور', style: Theme.of(context).textTheme.titleMedium),
                              Text('${rows.length} موظف', style: Theme.of(context).textTheme.bodyMedium),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                if (rows.isEmpty)
                  const Center(child: Text('لا توجد سجلات حضور في النظام الحالي.'))
                else
                  ...rows.map((row) => Card(
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          leading: CircleAvatar(child: Text(row.employee.code.substring(0, row.employee.code.length > 2 ? 2 : row.employee.code.length))),
                          title: Text(row.employee.name),
                          subtitle: Text('حضور: ${row.present} • غياب: ${row.absent} • ساعات إضافية: ${row.overtimeHours.toStringAsFixed(1)}'),
                        ),
                      )),
              ],
            );
          },
        ),
      );
}

class _AttendanceRow {
  const _AttendanceRow({
    required this.employee,
    required this.present,
    required this.absent,
    required this.overtimeHours,
  });

  final EmployeeSummary employee;
  final int present;
  final int absent;
  final double overtimeHours;
}
