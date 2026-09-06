import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../models/employee_models.dart';
import '../repositories/employee_repository.dart';
import 'employee_details_screen.dart';

final _employeeMoney = NumberFormat('#,##0.00');
final _employeeDate = DateFormat('yyyy/MM/dd');

String _employeeText(String? value) =>
    value == null || value.trim().isEmpty ? '-' : value;
String _employeeStatus(EmployeeOverviewRow row) {
  if (row.summary.isActive == false) return 'غير نشط';
  return switch (row.summary.status.toLowerCase()) {
    'active' => 'نشط',
    'suspended' => 'موقوف',
    'onleave' || 'on_leave' => 'مجاز',
    'terminated' => 'منتهي الخدمة',
    _ => row.summary.status,
  };
}

class EmployeesScreen extends StatefulWidget {
  const EmployeesScreen({super.key});

  @override
  State<EmployeesScreen> createState() => _EmployeesScreenState();
}

class _EmployeesScreenState extends State<EmployeesScreen> {
  final repository = EmployeeRepository();
  final search = TextEditingController();
  late Future<List<EmployeeOverviewRow>> future;
  int? departmentId;
  String? status;

  @override
  void initState() {
    super.initState();
    future = repository.getOverview();
  }

  @override
  void dispose() {
    search.dispose();
    super.dispose();
  }

  void reload() => setState(() => future = repository.getOverview());

  @override
  Widget build(BuildContext context) =>
      FutureBuilder<List<EmployeeOverviewRow>>(
        future: future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) return _EmployeeLoadError(onRetry: reload);
          final all = snapshot.data ?? const [];
          final departments = {
            for (final row in all)
              if (row.department != null) row.department!.id: row.department!
          }.values.toList()
            ..sort((left, right) => left.name.compareTo(right.name));
          final statuses = all.map(_employeeStatus).toSet().toList()..sort();
          final query = search.text.trim().toLowerCase();
          final rows = all.where((row) {
            return (departmentId == null ||
                    row.summary.departmentId == departmentId) &&
                (status == null || _employeeStatus(row) == status) &&
                (query.isEmpty ||
                    row.summary.name.toLowerCase().contains(query) ||
                    row.summary.code.toLowerCase().contains(query));
          }).toList();
          return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(children: [
                  Expanded(
                      child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                        Text('الموظفون',
                            style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 3),
                        Text('${all.length} موظف من السجلات الحالية'),
                      ])),
                  IconButton(
                      tooltip: 'تحديث',
                      onPressed: reload,
                      icon: const Icon(Icons.refresh)),
                ]),
                const SizedBox(height: 12),
                Wrap(spacing: 10, runSpacing: 10, children: [
                  SizedBox(
                      width: 320,
                      child: TextField(
                        controller: search,
                        textInputAction: TextInputAction.search,
                        onSubmitted: (_) => setState(() {}),
                        onChanged: (_) => setState(() {}),
                        decoration: const InputDecoration(
                            prefixIcon: Icon(Icons.search),
                            labelText: 'البحث بالاسم أو رقم الموظف',
                            border: OutlineInputBorder(),
                            isDense: true),
                      )),
                  SizedBox(
                      width: 220,
                      child: DropdownButtonFormField<int>(
                        initialValue: departmentId,
                        decoration: const InputDecoration(
                            labelText: 'القسم',
                            border: OutlineInputBorder(),
                            isDense: true),
                        items: [
                          const DropdownMenuItem(
                              value: null, child: Text('كل الأقسام')),
                          ...departments.map((item) => DropdownMenuItem(
                              value: item.id, child: Text(item.name)))
                        ],
                        onChanged: (value) =>
                            setState(() => departmentId = value),
                      )),
                  SizedBox(
                      width: 190,
                      child: DropdownButtonFormField<String>(
                        initialValue: status,
                        decoration: const InputDecoration(
                            labelText: 'الحالة',
                            border: OutlineInputBorder(),
                            isDense: true),
                        items: [
                          const DropdownMenuItem(
                              value: null, child: Text('كل الحالات')),
                          ...statuses.map((value) => DropdownMenuItem(
                              value: value, child: Text(value)))
                        ],
                        onChanged: (value) => setState(() => status = value),
                      )),
                ]),
                const SizedBox(height: 12),
                Expanded(
                    child: rows.isEmpty
                        ? const Center(child: Text('لا توجد نتائج مطابقة.'))
                        : LayoutBuilder(builder: (context, constraints) {
                            if (constraints.maxWidth < 760) {
                              return _EmployeeCards(rows: rows);
                            }
                            return _EmployeeTable(rows: rows);
                          })),
              ]);
        },
      );
}

class _EmployeeTable extends StatelessWidget {
  const _EmployeeTable({required this.rows});
  final List<EmployeeOverviewRow> rows;

  @override
  Widget build(BuildContext context) => Card(
        clipBehavior: Clip.antiAlias,
        child: Scrollbar(
            child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: SingleChildScrollView(
                    child: DataTable(
                  showCheckboxColumn: false,
                  columns: const [
                    DataColumn(label: Text('رقم الموظف')),
                    DataColumn(label: Text('الاسم')),
                    DataColumn(label: Text('القسم')),
                    DataColumn(label: Text('الوظيفة')),
                    DataColumn(label: Text('الراتب الأساسي')),
                    DataColumn(label: Text('الهاتف')),
                    DataColumn(label: Text('تاريخ التوظيف')),
                    DataColumn(label: Text('الحالة')),
                  ],
                  rows: rows
                      .map((row) => DataRow(
                            onSelectChanged: (_) => AppNavigation.push(
                                context,
                                (_) => EmployeeDetailsScreen(
                                    employeeId: row.summary.id)),
                            cells: [
                              DataCell(Text(row.summary.code)),
                              DataCell(Text(row.summary.name)),
                              DataCell(Text(row.department?.name ?? '-')),
                              DataCell(
                                  Text(_employeeText(row.summary.jobTitle))),
                              DataCell(Text(_employeeMoney
                                  .format(row.details.basicSalary))),
                              DataCell(
                                  Text(_employeeText(row.summary.phoneNumber))),
                              DataCell(Text(
                                  _employeeDate.format(row.details.hireDate))),
                              DataCell(_EmployeeStatusChip(
                                  label: _employeeStatus(row))),
                            ],
                          ))
                      .toList(),
                )))),
      );
}

class _EmployeeCards extends StatelessWidget {
  const _EmployeeCards({required this.rows});
  final List<EmployeeOverviewRow> rows;

  @override
  Widget build(BuildContext context) => ListView.separated(
        itemCount: rows.length,
        separatorBuilder: (_, __) => const SizedBox(height: 8),
        itemBuilder: (context, index) {
          final row = rows[index];
          return Card(
              child: ListTile(
            onTap: () => AppNavigation.push(context,
                (_) => EmployeeDetailsScreen(employeeId: row.summary.id)),
            leading: CircleAvatar(
                child: Text(row.summary.code.length > 3
                    ? row.summary.code.substring(0, 3)
                    : row.summary.code)),
            title: Text(row.summary.name),
            subtitle: Text(
                '${row.department?.name ?? '-'}  •  ${_employeeText(row.summary.jobTitle)}\n${_employeeText(row.summary.phoneNumber)}  •  ${_employeeMoney.format(row.details.basicSalary)}  •  ${_employeeDate.format(row.details.hireDate)}'),
            isThreeLine: true,
            trailing: _EmployeeStatusChip(label: _employeeStatus(row)),
          ));
        },
      );
}

class _EmployeeStatusChip extends StatelessWidget {
  const _EmployeeStatusChip({required this.label});
  final String label;

  @override
  Widget build(BuildContext context) => Chip(
        avatar: Icon(
            label == 'نشط'
                ? Icons.check_circle_outline
                : Icons.pause_circle_outline,
            size: 17),
        label: Text(label),
        visualDensity: VisualDensity.compact,
      );
}

class _EmployeeLoadError extends StatelessWidget {
  const _EmployeeLoadError({required this.onRetry});
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 42),
        const SizedBox(height: 10),
        const Text('تعذر تحميل بيانات الموظفين.'),
        const SizedBox(height: 10),
        FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة')),
      ]));
}
