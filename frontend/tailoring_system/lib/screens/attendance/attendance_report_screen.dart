import 'package:flutter/material.dart';

import '../../models/employee_models.dart';
import '../../repositories/employee_repository.dart';

class AttendanceReportScreen extends StatefulWidget {
  const AttendanceReportScreen({super.key, this.repository});

  final EmployeeRepository? repository;

  @override
  State<AttendanceReportScreen> createState() => _AttendanceReportScreenState();
}

class _AttendanceReportScreenState extends State<AttendanceReportScreen> {
  late final EmployeeRepository _repository;
  late Future<List<_AttendanceReportRow>> _future;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? EmployeeRepository();
    _future = _load();
  }

  Future<List<_AttendanceReportRow>> _load() async {
    final employees = await _repository.getEmployees();
    final rows = await Future.wait(employees.map((employee) async {
      final attendance = await _repository.getAttendance(employee.id);
      final present = attendance.where((item) => !item.isAbsent).length;
      final absent = attendance.where((item) => item.isAbsent).length;
      final hours = attendance.fold<double>(0, (sum, item) => sum + item.workedHours + item.overtimeHours);
      return _AttendanceReportRow(
        employee: employee,
        present: present,
        absent: absent,
        totalHours: hours,
      );
    }));
    rows.sort((a, b) => b.totalHours.compareTo(a.totalHours));
    return rows;
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('تقرير الحضور'),
          actions: [
            IconButton(
              tooltip: 'تحديث',
              onPressed: _reload,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: FutureBuilder<List<_AttendanceReportRow>>(
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
                    Text('تعذر تحميل تقرير الحضور:\n${snapshot.error}'),
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

            final rows = snapshot.data ?? const <_AttendanceReportRow>[];
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (rows.isEmpty)
                  const Center(child: Text('لا توجد بيانات حضور في هذا التقرير.'))
                else
                  ...rows.map((row) => Card(
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          title: Text(row.employee.name),
                          subtitle: Text('حضور: ${row.present} • غياب: ${row.absent} • الساعات: ${row.totalHours.toStringAsFixed(1)}'),
                        ),
                      )),
              ],
            );
          },
        ),
      );
}

class _AttendanceReportRow {
  const _AttendanceReportRow({
    required this.employee,
    required this.present,
    required this.absent,
    required this.totalHours,
  });

  final EmployeeSummary employee;
  final int present;
  final int absent;
  final double totalHours;
}
