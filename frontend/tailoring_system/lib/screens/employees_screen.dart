import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';
import '../models/employee_models.dart';
import '../repositories/employee_repository.dart';
import 'attendance/attendance_report_screen.dart';
import 'attendance/attendance_screen.dart';
import 'employee_details_screen.dart';
import 'employee_form_screen.dart';
import 'payroll/piece_wage_screen.dart';
import 'payroll/employee_production_screen.dart';
import 'payroll_screen.dart';

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

class EmployeesScreen extends StatelessWidget {
  const EmployeesScreen({super.key, this.initialTab = 0});

  final int initialTab;

  @override
  Widget build(BuildContext context) {
    const tabs = <Tab>[
      Tab(text: 'الموظفون'),
      Tab(text: 'الحضور'),
      Tab(text: 'تقرير الحضور'),
      Tab(text: 'الرواتب'),
      Tab(text: 'إنتاج الموظفين'),
      Tab(text: 'أجور القطعة'),
    ];

    return DefaultTabController(
      initialIndex: initialTab.clamp(0, tabs.length - 1),
      length: tabs.length,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('مساحة الموارد البشرية'),
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.textMain,
          elevation: 0,
          bottom: PreferredSize(
            preferredSize: const Size.fromHeight(48),
            child: Container(
              alignment: Alignment.centerRight,
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: TabBar(
                isScrollable: true,
                labelColor: UiPalette.textMain,
                unselectedLabelColor: UiPalette.textSoft,
                indicatorColor: UiPalette.primaryBlue,
                indicatorWeight: 3,
                tabs: tabs,
              ),
            ),
          ),
        ),
        body: const TabBarView(
          children: [
            _EmployeesListTab(),
            AttendanceScreen(),
            AttendanceReportScreen(),
            PayrollScreen(),
            EmployeeProductionScreen(),
            PieceWageScreen(),
          ],
        ),
      ),
    );
  }
}

class _EmployeesListTab extends StatefulWidget {
  const _EmployeesListTab();

  @override
  State<_EmployeesListTab> createState() => _EmployeesListTabState();
}

class _EmployeesListTabState extends State<_EmployeesListTab> {
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
          return Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: () async {
                          await AppNavigation.push(
                            context,
                            (_) => EmployeeFormScreen(onSaved: reload),
                          );
                          reload();
                        },
                        icon: const Icon(Icons.person_add_alt_1_outlined),
                        label: const Text('إضافة موظف'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    IconButton(
                      tooltip: 'تحديث',
                      onPressed: reload,
                      icon: const Icon(Icons.refresh_rounded),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
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
                          isDense: true,
                        ),
                      ),
                    ),
                    SizedBox(
                      width: 220,
                      child: DropdownButtonFormField<int>(
                        value: departmentId,
                        decoration: const InputDecoration(
                          labelText: 'القسم',
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                        items: [
                          const DropdownMenuItem(value: null, child: Text('كل الأقسام')),
                          ...departments.map((item) => DropdownMenuItem(
                              value: item.id, child: Text(item.name))),
                        ],
                        onChanged: (value) => setState(() => departmentId = value),
                      ),
                    ),
                    SizedBox(
                      width: 190,
                      child: DropdownButtonFormField<String>(
                        value: status,
                        decoration: const InputDecoration(
                          labelText: 'الحالة',
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                        items: [
                          const DropdownMenuItem(value: null, child: Text('كل الحالات')),
                          ...statuses.map((value) => DropdownMenuItem(
                              value: value, child: Text(value))),
                        ],
                        onChanged: (value) => setState(() => status = value),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Expanded(
                  child: rows.isEmpty
                      ? const Center(child: Text('لا توجد نتائج مطابقة.'))
                      : LayoutBuilder(
                          builder: (context, constraints) {
                            if (constraints.maxWidth < 760) {
                              return _EmployeeCards(rows: rows);
                            }
                            return _EmployeeTable(rows: rows);
                          },
                        ),
                ),
              ],
            ),
          );
        },
      );
}

class _EmployeeTable extends StatelessWidget {
  const _EmployeeTable({required this.rows});
  final List<EmployeeOverviewRow> rows;

  @override
  Widget build(BuildContext context) => Card(
    color: UiPalette.surfaceCard,
    clipBehavior: Clip.antiAlias,
    child: Scrollbar(
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: SingleChildScrollView(
          child: DataTable(
            showCheckboxColumn: false,
            headingRowColor: WidgetStateProperty.all(UiPalette.softBlue),
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
                          (_) => EmployeeDetailsScreen(employeeId: row.summary.id)),
                      cells: [
                        DataCell(Text(row.summary.code)),
                        DataCell(Text(row.summary.name)),
                        DataCell(Text(row.department?.name ?? '-')),
                        DataCell(Text(_employeeText(row.summary.jobTitle))),
                        DataCell(Text(_employeeMoney.format(row.details.basicSalary))),
                        DataCell(Text(_employeeText(row.summary.phoneNumber))),
                        DataCell(Text(_employeeDate.format(row.details.hireDate))),
                        DataCell(_EmployeeStatusChip(label: _employeeStatus(row))),
                      ],
                    ))
                .toList(),
          ),
        ),
      ),
    ),
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
        color: UiPalette.surfaceCard,
        child: ListTile(
          onTap: () => AppNavigation.push(
              context,
              (_) => EmployeeDetailsScreen(employeeId: row.summary.id)),
          leading: CircleAvatar(
            backgroundColor: UiPalette.softBlue,
            child: Text(
              row.summary.code.length > 3
                  ? row.summary.code.substring(0, 3)
                  : row.summary.code,
              style: const TextStyle(color: UiPalette.textMain),
            ),
          ),
          title: Text(row.summary.name, style: const TextStyle(color: UiPalette.textMain)),
          subtitle: Text(
            '${row.department?.name ?? '-'}  •  ${_employeeText(row.summary.jobTitle)}\n${_employeeText(row.summary.phoneNumber)}  •  ${_employeeMoney.format(row.details.basicSalary)}  •  ${_employeeDate.format(row.details.hireDate)}',
            style: const TextStyle(color: UiPalette.textSoft),
          ),
          isThreeLine: true,
          trailing: _EmployeeStatusChip(label: _employeeStatus(row)),
        ),
      );
    },
  );
}

class _EmployeeStatusChip extends StatelessWidget {
  const _EmployeeStatusChip({required this.label});
  final String label;

  @override
  Widget build(BuildContext context) => Chip(
    backgroundColor: label == 'نشط' ? UiPalette.primaryBlue.withAlpha(30) : UiPalette.softBlue,
    avatar: Icon(
      label == 'نشط' ? Icons.check_circle_outline : Icons.pause_circle_outline,
      size: 17,
      color: label == 'نشط' ? UiPalette.primaryBlue : UiPalette.textSoft,
    ),
    label: Text(label, style: const TextStyle(color: UiPalette.textMain)),
    visualDensity: VisualDensity.compact,
  );
}

class _EmployeeLoadError extends StatelessWidget {
  const _EmployeeLoadError({required this.onRetry});
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.cloud_off_outlined, size: 42, color: UiPalette.textSoft),
        const SizedBox(height: 10),
        const Text('تعذر تحميل بيانات الموظفين.', style: TextStyle(color: UiPalette.textMain)),
        const SizedBox(height: 10),
        FilledButton.icon(
          onPressed: onRetry,
          icon: const Icon(Icons.refresh),
          label: const Text('إعادة المحاولة'),
        ),
      ],
    ),
  );
}
