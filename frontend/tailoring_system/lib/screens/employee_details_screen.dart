import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../models/employee_models.dart';
import '../repositories/employee_repository.dart';

final _detailsMoney = NumberFormat('#,##0.00');
final _detailsDate = DateFormat('yyyy/MM/dd');
final _detailsTime = DateFormat('HH:mm');

String _detailsText(String? value) =>
    value == null || value.trim().isEmpty ? '-' : value;
String _detailsStatus(EmployeeDetails employee) {
  if (employee.isActive == false) return 'غير نشط';
  return switch (employee.status.toLowerCase()) {
    'active' => 'نشط',
    'suspended' => 'موقوف',
    'onleave' || 'on_leave' => 'مجاز',
    'terminated' => 'منتهي الخدمة',
    _ => employee.status,
  };
}

String _leaveStatus(String value) => switch (value.toLowerCase()) {
      'approved' => 'معتمدة',
      'pending' => 'قيد المراجعة',
      'rejected' => 'مرفوضة',
      'cancelled' => 'ملغاة',
      _ => value,
    };

String _leaveType(String value) => switch (value.toLowerCase()) {
      'annual' => 'سنوية',
      'sick' => 'مرضية',
      'unpaid' => 'بدون راتب',
      'emergency' => 'طارئة',
      _ => value,
    };

class EmployeeDetailsScreen extends StatefulWidget {
  const EmployeeDetailsScreen({required this.employeeId, super.key});
  final int employeeId;

  @override
  State<EmployeeDetailsScreen> createState() => _EmployeeDetailsScreenState();
}

class _EmployeeDetailsScreenState extends State<EmployeeDetailsScreen> {
  final repository = EmployeeRepository();
  late Future<EmployeeDetailsData> future;

  @override
  void initState() {
    super.initState();
    future = repository.getDetails(widget.employeeId);
  }

  void reload() =>
      setState(() => future = repository.getDetails(widget.employeeId));

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('تفاصيل الموظف'),
          actions: [
            IconButton(
                tooltip: 'تحديث',
                onPressed: reload,
                icon: const Icon(Icons.refresh))
          ],
        ),
        body: SafeArea(
            child: FutureBuilder<EmployeeDetailsData>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError || snapshot.data == null) {
              return _EmployeeDetailsError(onRetry: reload);
            }
            final data = snapshot.data!;
            final employee = data.employee;
            final present =
                data.attendance.where((item) => !item.isAbsent).length;
            final absent =
                data.attendance.where((item) => item.isAbsent).length;
            return ListView(padding: const EdgeInsets.all(16), children: [
              Card(
                  child: Padding(
                      padding: const EdgeInsets.all(18),
                      child: Row(children: [
                        CircleAvatar(
                            radius: 28,
                            child: Text(employee.name.isEmpty
                                ? '-'
                                : employee.name.characters.first)),
                        const SizedBox(width: 14),
                        Expanded(
                            child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                              Text(employee.name,
                                  style:
                                      Theme.of(context).textTheme.titleLarge),
                              const SizedBox(height: 4),
                              Text(
                                  '${employee.code}  •  ${_detailsText(employee.jobTitle)}  •  ${data.department?.name ?? '-'}'),
                            ])),
                        Chip(label: Text(_detailsStatus(employee))),
                      ]))),
              const SizedBox(height: 12),
              _EmployeeSection(
                  title: 'البيانات الأساسية',
                  icon: Icons.badge_outlined,
                  child: Wrap(spacing: 28, runSpacing: 14, children: [
                    _DetailValue('رقم الموظف', employee.code),
                    _DetailValue('الاسم', employee.name),
                    _DetailValue('الاسم الكامل', employee.fullName),
                    _DetailValue('القسم', data.department?.name ?? '-'),
                    _DetailValue('الوظيفة', _detailsText(employee.jobTitle)),
                    _DetailValue('الراتب الأساسي',
                        _detailsMoney.format(employee.basicSalary)),
                    _DetailValue(
                        'نوع الراتب', _detailsText(employee.salaryType)),
                    _DetailValue('تاريخ التوظيف',
                        _detailsDate.format(employee.hireDate)),
                    _DetailValue(
                        'تاريخ انتهاء الخدمة',
                        employee.terminationDate == null
                            ? '-'
                            : _detailsDate.format(employee.terminationDate!)),
                    _DetailValue('الحالة', _detailsStatus(employee)),
                  ])),
              const SizedBox(height: 12),
              _EmployeeSection(
                  title: 'التواصل والملاحظات',
                  icon: Icons.contact_phone_outlined,
                  child: Wrap(spacing: 28, runSpacing: 14, children: [
                    _DetailValue('الهاتف',
                        _detailsText(employee.phone ?? employee.phoneNumber)),
                    _DetailValue(
                        'البريد الإلكتروني', _detailsText(employee.email)),
                    _DetailValue('العنوان', _detailsText(employee.address),
                        width: 300),
                    _DetailValue('ملاحظات', _detailsText(employee.notes),
                        width: 420),
                  ])),
              const SizedBox(height: 12),
              _EmployeeSection(
                  title: 'الوثائق',
                  icon: Icons.folder_open_outlined,
                  child: _DetailsTable(
                    columns: const [
                      'نوع الوثيقة',
                      'تاريخ الإضافة',
                      'الملف',
                      'الملاحظات'
                    ],
                    rows: data.documents
                        .map((item) => [
                              item.type,
                              _detailsDate.format(item.createdAt),
                              _detailsText(item.filePath),
                              _detailsText(item.notes)
                            ])
                        .toList(),
                    empty: 'لا توجد وثائق مسجلة لهذا الموظف.',
                  )),
              const SizedBox(height: 12),
              _EmployeeSection(
                  title: 'الحضور',
                  icon: Icons.calendar_month_outlined,
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Wrap(spacing: 10, runSpacing: 10, children: [
                          _CountCard(
                              'الحضور', present, Icons.check_circle_outline),
                          _CountCard(
                              'الغياب', absent, Icons.event_busy_outlined),
                          _CountCard(
                              'الإجازات المسجلة',
                              data.leaveRequests.length,
                              Icons.beach_access_outlined),
                          const _UnavailableCountCard(
                              'التأخير', 'غير متاح في عقد الحضور'),
                        ]),
                        const SizedBox(height: 10),
                        _DetailsTable(
                          columns: const [
                            'التاريخ',
                            'الدخول',
                            'الخروج',
                            'ساعات العمل',
                            'الإضافي',
                            'الحالة',
                            'ملاحظات'
                          ],
                          rows: data.attendance
                              .map((item) => [
                                    _detailsDate.format(item.attendanceDate),
                                    item.checkInTime == null
                                        ? '-'
                                        : _detailsTime
                                            .format(item.checkInTime!),
                                    item.checkOutTime == null
                                        ? '-'
                                        : _detailsTime
                                            .format(item.checkOutTime!),
                                    _detailsMoney.format(item.workedHours),
                                    _detailsMoney.format(item.overtimeHours),
                                    item.isAbsent ? 'غياب' : 'حضور',
                                    _detailsText(
                                        item.notes ?? item.absenceReason),
                                  ])
                              .toList(),
                          empty: 'لا توجد سجلات حضور لهذا الموظف.',
                        ),
                      ])),
              const SizedBox(height: 12),
              _EmployeeSection(
                  title: 'الإجازات',
                  icon: Icons.beach_access_outlined,
                  child: _DetailsTable(
                    columns: const [
                      'نوع الإجازة',
                      'البداية',
                      'النهاية',
                      'الأيام',
                      'الحالة',
                      'السبب'
                    ],
                    rows: data.leaveRequests
                        .map((item) => [
                              _leaveType(item.type),
                              _detailsDate.format(item.startDate),
                              _detailsDate.format(item.endDate),
                              _detailsMoney.format(item.requestedDays),
                              _leaveStatus(item.status),
                              _detailsText(item.reason)
                            ])
                        .toList(),
                    empty: 'لا توجد طلبات إجازة لهذا الموظف.',
                  )),
            ]);
          },
        )),
      );
}

class _EmployeeSection extends StatelessWidget {
  const _EmployeeSection(
      {required this.title, required this.icon, required this.child});
  final String title;
  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) => Card(
      child: Padding(
          padding: const EdgeInsets.all(16),
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Row(children: [
              Icon(icon),
              const SizedBox(width: 8),
              Text(title, style: Theme.of(context).textTheme.titleMedium)
            ]),
            const SizedBox(height: 12),
            child,
          ])));
}

class _DetailValue extends StatelessWidget {
  const _DetailValue(this.label, this.value, {this.width = 220});
  final String label;
  final String value;
  final double width;

  @override
  Widget build(BuildContext context) => SizedBox(
      width: width,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: Theme.of(context).textTheme.labelMedium),
        const SizedBox(height: 3),
        SelectableText(value, style: Theme.of(context).textTheme.titleSmall),
      ]));
}

class _CountCard extends StatelessWidget {
  const _CountCard(this.label, this.value, this.icon);
  final String label;
  final int value;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Container(
        width: 170,
        height: 72,
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(8)),
        child: Row(children: [
          Icon(icon),
          const SizedBox(width: 9),
          Expanded(
              child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                Text(label),
                Text('$value', style: Theme.of(context).textTheme.titleLarge)
              ]))
        ]),
      );
}

class _UnavailableCountCard extends StatelessWidget {
  const _UnavailableCountCard(this.label, this.reason);
  final String label;
  final String reason;

  @override
  Widget build(BuildContext context) => Tooltip(
        message: reason,
        child: Container(
          width: 205,
          height: 72,
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surfaceContainerHighest,
              borderRadius: BorderRadius.circular(8)),
          child: const Row(children: [
            Icon(Icons.info_outline),
            SizedBox(width: 9),
            Expanded(child: Text('التأخير\nغير متاح'))
          ]),
        ),
      );
}

class _DetailsTable extends StatelessWidget {
  const _DetailsTable(
      {required this.columns, required this.rows, required this.empty});
  final List<String> columns;
  final List<List<String>> rows;
  final String empty;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Text(empty));
    }
    return SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: columns
              .map((value) => DataColumn(
                  label: Text(value,
                      style: const TextStyle(fontWeight: FontWeight.bold))))
              .toList(),
          rows: rows
              .map((row) => DataRow(
                  cells: row
                      .map((value) => DataCell(SelectableText(value)))
                      .toList()))
              .toList(),
        ));
  }
}

class _EmployeeDetailsError extends StatelessWidget {
  const _EmployeeDetailsError({required this.onRetry});
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 42),
        const SizedBox(height: 10),
        const Text('تعذر تحميل تفاصيل الموظف.'),
        const SizedBox(height: 10),
        FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة')),
      ]));
}
