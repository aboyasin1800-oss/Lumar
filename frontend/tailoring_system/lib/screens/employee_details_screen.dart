import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';
import '../models/employee_models.dart';
import '../models/finance_models.dart';
import '../models/payroll_models.dart';
import '../repositories/employee_repository.dart';
import '../repositories/finance_repository.dart';
import '../repositories/payroll_repository.dart';
import 'employee_form_screen.dart';

final _detailsMoney = NumberFormat('#,##0.00');
final _detailsDate = DateFormat('yyyy/MM/dd');

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

String _salaryTypeLabel(EmployeeDetails employee) {
  final rawValue = (employee.salaryType ?? '').trim();
  if (rawValue.isNotEmpty) {
    return switch (rawValue.toLowerCase()) {
      'basicsalary' => 'راتب أساسي',
      'basic' => 'راتب أساسي',
      'piecewage' => 'أجر قطعة',
      'piece' => 'أجر قطعة',
      'salarypluspiece' || 'basicpluspiece' => 'راتب أساسي + أجر قطعة',
      _ => rawValue,
    };
  }

  final hasBasic = employee.basicSalary > 0;
  final hasPieceRate = employee.pieceWageRate > 0;

  if (hasPieceRate && !hasBasic) return 'أجر قطعة (مستخرج من PieceWageRate)';
  if (hasBasic && !hasPieceRate) return 'راتب أساسي';
  if (hasBasic && hasPieceRate) return 'راتب أساسي + أجر قطعة';
  return 'غير محدد في قاعدة البيانات';
}

String _scannerStatus(EmployeeDetails employee) {
  final scannerCode = employee.scannerCode?.trim();
  if (scannerCode == null || scannerCode.isEmpty) {
    return 'غير مرتبط بماسح';
  }
  return 'مرتبطة بالماسح: $scannerCode';
}

class _EmployeeFinancialSummary {
  const _EmployeeFinancialSummary({
    required this.draws,
    required this.settlements,
    required this.pieceWages,
    required this.operatingExpenses,
  });

  final List<EmployeeDraw> draws;
  final List<PayrollSettlement> settlements;
  final List<PieceWageRecord> pieceWages;
  final List<FinancialTransaction> operatingExpenses;

  double get drawTotal =>
      draws.fold<double>(0, (sum, item) => sum + (item.amount ?? 0));
  double get settlementTotal =>
      settlements.fold<double>(0, (sum, item) => sum + item.amount);
  double get currentBalance => drawTotal - settlementTotal;
  double get pieceWageTotal =>
      pieceWages.fold<double>(0, (sum, item) => sum + item.totalWage);
  double get operatingExpenseTotal =>
      operatingExpenses.fold<double>(0, (sum, item) => sum + item.amount);
}

class EmployeeDetailsScreen extends StatefulWidget {
  EmployeeDetailsScreen({
    required this.employeeId,
    EmployeeRepository? repository,
    super.key,
  }) : repository = repository ?? EmployeeRepository();

  final int employeeId;
  final EmployeeRepository repository;

  @override
  State<EmployeeDetailsScreen> createState() => _EmployeeDetailsScreenState();
}

class _EmployeeDetailsScreenState extends State<EmployeeDetailsScreen>
    with SingleTickerProviderStateMixin {
  late final EmployeeRepository repository = widget.repository;
  late final TabController _tabController;
  late Future<EmployeeDetailsData> future;
  late Future<_EmployeeFinancialSummary> financialFuture;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
    future = repository.getDetails(widget.employeeId);
    financialFuture = _loadFinancialSummary();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<_EmployeeFinancialSummary> _loadFinancialSummary() async {
    final payroll = PayrollRepository();
    final finance = FinanceRepository();

    final draws = await payroll.getDraws(widget.employeeId);
    final settlements = await payroll.getSettlements(widget.employeeId);
    final wages = (await payroll.getPieceWages())
        .where((item) => item.employeeId == widget.employeeId)
        .toList();
    final expenses = (await finance.getTransactions())
        .where((item) => item.transactionType == 'OperatingExpense')
        .toList();

    return _EmployeeFinancialSummary(
      draws: draws,
      settlements: settlements,
      pieceWages: wages,
      operatingExpenses: expenses,
    );
  }

  void reload() => setState(() {
        future = repository.getDetails(widget.employeeId);
        financialFuture = _loadFinancialSummary();
      });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        title: const Text('ملف الموظف'),
        foregroundColor: UiPalette.textMain,
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: reload,
            icon: const Icon(Icons.refresh_rounded),
          ),
          IconButton(
            tooltip: 'تعديل الموظف',
            onPressed: () async {
              final current = await repository.getEmployee(widget.employeeId);
              if (!mounted || !context.mounted) return;

              final result = await AppNavigation.push(
                context,
                (_) => EmployeeFormScreen(
                  employee: current,
                  onSaved: () => reload(),
                ),
              );
              if (result != null && mounted) reload();
            },
            icon: const Icon(Icons.edit_outlined),
          ),
        ],
      ),
      body: SafeArea(
        child: FutureBuilder<EmployeeDetailsData>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(
                child: CircularProgressIndicator(color: UiPalette.primaryBlue),
              );
            }

            if (snapshot.hasError || snapshot.data == null) {
              return _EmployeeDetailsError(onRetry: reload);
            }

            final data = snapshot.data!;
            final employee = data.employee;
            final status = _detailsStatus(employee);
            final scannerText = _scannerStatus(employee);
            final present =
                data.attendance.where((item) => !item.isAbsent).length;
            final absent =
                data.attendance.where((item) => item.isAbsent).length;

            return FutureBuilder<_EmployeeFinancialSummary>(
              future: financialFuture,
              builder: (context, financialSnapshot) {
                final financial = financialSnapshot.data ??
                    _EmployeeFinancialSummary(
                      draws: const [],
                      settlements: const [],
                      pieceWages: const [],
                      operatingExpenses: const [],
                    );

                return Column(
                  children: [
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.fromLTRB(18, 18, 18, 12),
                      color: UiPalette.surfaceCard,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              CircleAvatar(
                                radius: 30,
                                backgroundColor: UiPalette.primaryBlue,
                                child: Text(
                                  employee.name.trim().isEmpty
                                      ? '-'
                                      : employee.name.trim().characters.first,
                                  style: const TextStyle(
                                    color: UiPalette.textMain,
                                    fontSize: 22,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                              ),
                              const SizedBox(width: 16),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      employee.name,
                                      style: UiPalette.adaptiveTextStyle(
                                        context,
                                        backgroundColor: UiPalette.surfaceCard,
                                        fontSize: 24,
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                    const SizedBox(height: 6),
                                    Text(
                                      '${employee.code}  •  ${_detailsText(employee.jobTitle)}',
                                      style: UiPalette.adaptiveTextStyle(
                                        context,
                                        backgroundColor: UiPalette.surfaceCard,
                                        fontSize: 14,
                                      ),
                                    ),
                                    const SizedBox(height: 6),
                                    Text(
                                      data.department?.name ?? '-',
                                      style: UiPalette.adaptiveTextStyle(
                                        context,
                                        backgroundColor: UiPalette.surfaceCard,
                                        fontSize: 13,
                                      ).copyWith(color: UiPalette.textSoft),
                                    ),
                                  ],
                                ),
                              ),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                  vertical: 8,
                                ),
                                decoration: BoxDecoration(
                                  color: UiPalette.primaryBlue.withAlpha(32),
                                  borderRadius: BorderRadius.circular(16),
                                  border: Border.all(
                                    color: UiPalette.primaryBlue,
                                    width: 1,
                                  ),
                                ),
                                child: Text(
                                  status,
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.primaryBlue,
                                    fontSize: 12,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 14),
                          Wrap(
                            spacing: 10,
                            runSpacing: 8,
                            children: [
                              _HeaderBadge(
                                label: scannerText,
                                icon: Icons.qr_code_2_rounded,
                              ),
                              _HeaderBadge(
                                label:
                                    'نوع الراتب: ${_salaryTypeLabel(employee)}',
                                icon: Icons.account_balance_wallet_outlined,
                              ),
                              _HeaderBadge(
                                label:
                                    'تاريخ التوظيف: ${_detailsDate.format(employee.hireDate)}',
                                icon: Icons.calendar_month_rounded,
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    Container(
                      color: UiPalette.screenBackground,
                      child: TabBar(
                        controller: _tabController,
                        isScrollable: false,
                        labelColor: UiPalette.textMain,
                        unselectedLabelColor: UiPalette.textSoft,
                        indicatorColor: UiPalette.primaryBlue,
                        indicatorWeight: 3,
                        tabs: const [
                          Tab(text: 'بيانات الموظف'),
                          Tab(text: 'كشف شهري'),
                          Tab(text: 'عقد العمل'),
                        ],
                      ),
                    ),
                    Expanded(
                      child: TabBarView(
                        controller: _tabController,
                        children: [
                          _OverviewTab(
                            employee: employee,
                            data: data,
                            financial: financial,
                            onReload: reload,
                          ),
                          _MonthlyTab(
                            employee: employee,
                            attendance: data.attendance,
                            leaveRequests: data.leaveRequests,
                            financial: financial,
                            present: present,
                            absent: absent,
                          ),
                          _ContractTab(
                            employee: employee,
                            repository: repository,
                          ),
                        ],
                      ),
                    ),
                  ],
                );
              },
            );
          },
        ),
      ),
    );
  }
}

class _OverviewTab extends StatelessWidget {
  const _OverviewTab({
    required this.employee,
    required this.data,
    required this.financial,
    required this.onReload,
  });

  final EmployeeDetails employee;
  final EmployeeDetailsData data;
  final _EmployeeFinancialSummary financial;
  final VoidCallback onReload;

  @override
  Widget build(BuildContext context) {
    final personalItems = <_InfoTileData>[
      _InfoTileData('رقم الموظف', employee.code),
      _InfoTileData('الاسم الكامل', _detailsText(employee.fullName)),
      _InfoTileData('القسم', data.department?.name ?? '-'),
      _InfoTileData('الوظيفة', _detailsText(employee.jobTitle)),
      _InfoTileData(
          'الراتب الأساسي', _detailsMoney.format(employee.basicSalary)),
      _InfoTileData('نوع الراتب', _salaryTypeLabel(employee)),
      _InfoTileData('سعر القطعة', _detailsMoney.format(employee.pieceWageRate)),
      _InfoTileData(
          'الهاتف', _detailsText(employee.phone ?? employee.phoneNumber)),
      _InfoTileData('الحالة', _detailsStatus(employee)),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _Panel(
          title: 'البيانات الأساسية',
          icon: Icons.badge_outlined,
          child: Wrap(
            spacing: 16,
            runSpacing: 12,
            children: personalItems
                .map(
                  (item) => SizedBox(
                    width: 220,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          item.label,
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ).copyWith(color: UiPalette.textSoft),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          item.value,
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 14,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ],
                    ),
                  ),
                )
                .toList(),
          ),
        ),
        const SizedBox(height: 12),
        _Panel(
          title: 'كشف الحساب المالي',
          icon: Icons.account_balance_wallet_outlined,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  _MetricCard(
                    label: 'إجمالي السلف',
                    value: _detailsMoney.format(financial.drawTotal),
                    accent: UiPalette.primaryBlue,
                  ),
                  _MetricCard(
                    label: 'أجور القطعة',
                    value: _detailsMoney.format(financial.pieceWageTotal),
                    accent: UiPalette.softBlue,
                  ),
                  _MetricCard(
                    label: 'المصروفات اليومية',
                    value:
                        _detailsMoney.format(financial.operatingExpenseTotal),
                    accent: UiPalette.primaryDark,
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        _Panel(
          title: 'معلومات التواصل',
          icon: Icons.contact_phone_outlined,
          child: Wrap(
            spacing: 18,
            runSpacing: 12,
            children: [
              _InfoValueTile('البريد الإلكتروني', _detailsText(employee.email)),
              _InfoValueTile('العنوان', _detailsText(employee.address),
                  width: 300),
              _InfoValueTile('ملاحظات', _detailsText(employee.notes),
                  width: 420),
            ],
          ),
        ),
      ],
    );
  }
}

class _MonthlyTab extends StatefulWidget {
  const _MonthlyTab({
    required this.employee,
    required this.attendance,
    required this.leaveRequests,
    required this.financial,
    required this.present,
    required this.absent,
  });

  final EmployeeDetails employee;
  final List<EmployeeAttendanceRecord> attendance;
  final List<EmployeeLeaveRequest> leaveRequests;
  final _EmployeeFinancialSummary financial;
  final int present;
  final int absent;

  @override
  State<_MonthlyTab> createState() => _MonthlyTabState();
}

const String _employeeMonthlyApiBaseUrl = String.fromEnvironment(
  'LUMAR_API_URL',
  defaultValue: 'http://127.0.0.1:5093',
);

class _PieceDetailSnapshot {
  const _PieceDetailSnapshot({
    required this.pieceId,
    required this.orderNumber,
    required this.customerName,
    required this.trackingCode,
    required this.pieceType,
    required this.fabricType,
    required this.fabricCode,
    required this.fabricColor,
    required this.completedAt,
    required this.totalWage,
    required this.quantity,
  });

  final int pieceId;
  final String orderNumber;
  final String customerName;
  final String trackingCode;
  final String pieceType;
  final String fabricType;
  final String fabricCode;
  final String fabricColor;
  final DateTime? completedAt;
  final double totalWage;
  final double quantity;

  factory _PieceDetailSnapshot.fromRecord(
    PieceWageRecord record,
    Map<String, dynamic>? workCard, {
    String fallbackOrder = '-',
    String fallbackCustomer = 'غير محدد',
    String fallbackTracking = '-',
    String fallbackPieceType = '-',
    String fallbackFabricType = '-',
    String fallbackFabricCode = '-',
    String fallbackFabricColor = '-',
  }) {
    final data = workCard ?? const <String, dynamic>{};
    final history = (data['trackingHistory'] is List)
        ? (data['trackingHistory'] as List)
            .whereType<Map<String, dynamic>>()
            .toList()
        : <Map<String, dynamic>>[];
    DateTime? latestEvent;
    for (final entry in history) {
      final rawValue = entry['eventTime'];
      if (rawValue is String && rawValue.trim().isNotEmpty) {
        final parsed = DateTime.tryParse(rawValue);
        if (parsed != null &&
            (latestEvent == null || parsed.isAfter(latestEvent))) {
          latestEvent = parsed;
        }
      }
    }

    return _PieceDetailSnapshot(
      pieceId: record.pieceId,
      orderNumber: (data['orderNumber'] as String?)?.trim().isNotEmpty == true
          ? data['orderNumber'] as String
          : fallbackOrder,
      customerName: (data['customerName'] as String?)?.trim().isNotEmpty == true
          ? data['customerName'] as String
          : fallbackCustomer,
      trackingCode: (data['trackingCode'] as String?)?.trim().isNotEmpty == true
          ? data['trackingCode'] as String
          : fallbackTracking,
      pieceType: (data['pieceType'] as String?)?.trim().isNotEmpty == true
          ? data['pieceType'] as String
          : fallbackPieceType,
      fabricType: (data['fabricType'] as String?)?.trim().isNotEmpty == true
          ? data['fabricType'] as String
          : fallbackFabricType,
      fabricCode: (data['fabricCode'] as String?)?.trim().isNotEmpty == true
          ? data['fabricCode'] as String
          : fallbackFabricCode,
      fabricColor: (data['fabricColor'] as String?)?.trim().isNotEmpty == true
          ? data['fabricColor'] as String
          : fallbackFabricColor,
      completedAt: latestEvent ??
          (data['deliveryDate'] != null
              ? DateTime.tryParse(data['deliveryDate'] as String)
              : null),
      totalWage: record.totalWage,
      quantity: record.quantity,
    );
  }
}

class _MonthlyTabState extends State<_MonthlyTab> {
  late int _selectedYear;
  late int _selectedMonth;

  @override
  void initState() {
    super.initState();
    final allYears = <int>{
      ...widget.financial.pieceWages.map((item) => item.createdAt.year),
      ...widget.financial.draws
          .where((item) => item.drawDate != null)
          .map((item) => item.drawDate!.year),
    }.toList()
      ..sort();

    _selectedYear = allYears.isEmpty ? DateTime.now().year : allYears.last;
    final monthsWithData = <int>{
      ...widget.financial.pieceWages
          .where((item) => item.createdAt.year == _selectedYear)
          .map((item) => item.createdAt.month),
      ...widget.financial.draws
          .where((item) =>
              item.drawDate != null && item.drawDate!.year == _selectedYear)
          .map((item) => item.drawDate!.month),
    }.toList()
      ..sort();

    _selectedMonth =
        monthsWithData.isEmpty ? DateTime.now().month : monthsWithData.last;
  }

  List<PieceWageRecord> get _filteredWages => widget.financial.pieceWages
      .where((item) =>
          item.createdAt.year == _selectedYear &&
          item.createdAt.month == _selectedMonth)
      .toList()
    ..sort((a, b) => a.createdAt.compareTo(b.createdAt));

  List<EmployeeDraw> get _filteredDraws => widget.financial.draws
      .where((item) =>
          item.drawDate != null &&
          item.drawDate!.year == _selectedYear &&
          item.drawDate!.month == _selectedMonth)
      .toList()
    ..sort((a, b) => (a.drawDate ?? DateTime.fromMillisecondsSinceEpoch(0))
        .compareTo(b.drawDate ?? DateTime.fromMillisecondsSinceEpoch(0)));

  List<int> get _availableYears {
    final years = <int>{
      ...widget.financial.pieceWages.map((item) => item.createdAt.year),
      ...widget.financial.draws
          .where((item) => item.drawDate != null)
          .map((item) => item.drawDate!.year),
    }.toList();
    years.sort();
    return years;
  }

  List<int> get _availableMonths {
    final months = <int>{
      ...widget.financial.pieceWages
          .where((item) => item.createdAt.year == _selectedYear)
          .map((item) => item.createdAt.month),
      ...widget.financial.draws
          .where((item) =>
              item.drawDate != null && item.drawDate!.year == _selectedYear)
          .map((item) => item.drawDate!.month),
    }.toList();
    months.sort();
    return months;
  }

  List<_MonthlyDaySummary> get _daySummaries {
    final map = <DateTime, _MonthlyDaySummary>{};

    for (final item in _filteredWages) {
      final date = DateTime(
          item.createdAt.year, item.createdAt.month, item.createdAt.day);
      final summary =
          map.putIfAbsent(date, () => _MonthlyDaySummary(date: date));
      summary.addWage(item);
    }

    for (final item in _filteredDraws) {
      final date = item.drawDate ?? DateTime(_selectedYear, _selectedMonth, 1);
      final day = DateTime(date.year, date.month, date.day);
      final summary = map.putIfAbsent(day, () => _MonthlyDaySummary(date: day));
      summary.addDraw(item.amount ?? 0);
    }

    final values = map.values.toList();
    values.sort((a, b) => a.date.compareTo(b.date));
    return values;
  }

  bool get _isPieceWageEmployee {
    final rawSalaryType = (widget.employee.salaryType ?? '').trim();
    if (rawSalaryType.isNotEmpty) {
      final normalized = rawSalaryType.toLowerCase();
      if (normalized == 'piecewage' || normalized == 'piece') {
        return true;
      }
      if (normalized == 'basicpluspiece' || normalized == 'salarypluspiece') {
        return true;
      }
    }

    return widget.employee.pieceWageRate > 0 &&
        widget.employee.basicSalary <= 0;
  }

  _MonthlyCycleSummary get _cycleSummary {
    final pieceTypes = <String, _PieceTypeSummary>{};
    for (final item in _filteredWages) {
      final summary = pieceTypes.putIfAbsent(
        item.pieceType,
        () => _PieceTypeSummary(pieceType: item.pieceType),
      );
      summary.add(item);
    }

    final totalWage =
        _filteredWages.fold<double>(0, (sum, item) => sum + item.totalWage);
    final totalQuantity =
        _filteredWages.fold<double>(0, (sum, item) => sum + item.quantity);
    final totalDraws =
        _filteredDraws.fold<double>(0, (sum, item) => sum + (item.amount ?? 0));
    final totalDailyExpense = 0.0;
    final totalSettlements = 0.0;

    final netDue = _isPieceWageEmployee
        ? totalWage - totalDraws - totalDailyExpense
        : (widget.employee.basicSalary > 0
            ? widget.employee.basicSalary - totalDraws
            : totalWage - totalDraws - totalDailyExpense);

    return _MonthlyCycleSummary(
      totalWage: totalWage,
      totalQuantity: totalQuantity,
      totalDraws: totalDraws,
      totalSettlements: totalSettlements,
      totalDailyExpense: totalDailyExpense,
      openDrawBalance: totalDraws,
      netDue: netDue,
      pieceTypes: pieceTypes.values.toList()
        ..sort((a, b) => b.totalWage.compareTo(a.totalWage)),
    );
  }

  void _onChangeYear(int? value) {
    if (value == null) return;
    setState(() {
      _selectedYear = value;
      if (!_availableMonths.contains(_selectedMonth)) {
        _selectedMonth = _availableMonths.isEmpty
            ? DateTime.now().month
            : _availableMonths.last;
      }
    });
  }

  void _onChangeMonth(int? value) {
    if (value == null) return;
    setState(() {
      _selectedMonth = value;
    });
  }

  Future<Map<String, dynamic>?> _loadPieceWorkCard(int pieceId) async {
    try {
      final response = await http.get(
        Uri.parse(
            '$_employeeMonthlyApiBaseUrl/production/pieces/$pieceId/work-card'),
      );
      if (response.statusCode < 200 || response.statusCode >= 300) {
        return null;
      }
      final decoded = jsonDecode(response.body);
      return decoded is Map<String, dynamic> ? decoded : null;
    } catch (_) {
      return null;
    }
  }

  void _showPieceTypeDetail(String pieceType) {
    final entries = _filteredWages
        .where((item) => item.pieceType == pieceType)
        .toList()
      ..sort((a, b) => a.createdAt.compareTo(b.createdAt));

    if (!mounted) return;
    showDialog<void>(
      context: context,
      builder: (dialogContext) => FutureBuilder<List<_PieceDetailSnapshot>>(
        future: Future.wait(
          entries.map((entry) async {
            final workCard = await _loadPieceWorkCard(entry.pieceId);
            return _PieceDetailSnapshot.fromRecord(
              entry,
              workCard,
              fallbackOrder: '—',
              fallbackCustomer: 'غير محدد',
              fallbackTracking: entry.pieceId.toString(),
              fallbackPieceType: pieceType,
            );
          }),
        ),
        builder: (context, snapshot) {
          final items = snapshot.data ?? const <_PieceDetailSnapshot>[];
          return AlertDialog(
            backgroundColor: UiPalette.surfaceCard,
            title: Text(
              'تفاصيل النوع: $pieceType',
              style: UiPalette.adaptiveTextStyle(
                dialogContext,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 18,
                fontWeight: FontWeight.w800,
              ),
            ),
            content: SizedBox(
              width: 980,
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: SingleChildScrollView(
                  child: DataTable(
                    headingRowColor:
                        WidgetStateProperty.all(UiPalette.softBlue),
                    columns: const [
                      DataColumn(label: Text('اسم العميل')),
                      DataColumn(label: Text('رقم الطلب')),
                      DataColumn(label: Text('كود القطعة')),
                      DataColumn(label: Text('النوع')),
                      DataColumn(label: Text('نوع القماش')),
                      DataColumn(label: Text('كود القماش')),
                      DataColumn(label: Text('لون القماش')),
                      DataColumn(label: Text('الكمية')),
                      DataColumn(label: Text('الأجر')),
                      DataColumn(label: Text('وقت الإنجاز')),
                    ],
                    rows: items
                        .map(
                          (item) => DataRow(
                            cells: [
                              DataCell(Text(item.customerName)),
                              DataCell(Text(item.orderNumber)),
                              DataCell(Text(item.trackingCode)),
                              DataCell(Text(item.pieceType)),
                              DataCell(Text(item.fabricType)),
                              DataCell(Text(item.fabricCode)),
                              DataCell(Text(item.fabricColor)),
                              DataCell(Text(item.quantity.toStringAsFixed(0))),
                              DataCell(
                                  Text(_detailsMoney.format(item.totalWage))),
                              DataCell(
                                Text(
                                  item.completedAt == null
                                      ? '-'
                                      : DateFormat('yyyy/MM/dd HH:mm', 'ar')
                                          .format(item.completedAt!),
                                ),
                              ),
                            ],
                          ),
                        )
                        .toList(),
                  ),
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('إغلاق'),
              ),
            ],
          );
        },
      ),
    );
  }

  void _showPrintMessage(String label) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
            'تم تجهيز $label بنجاح. سيتم التنفيذ الكامل في طبقة الطباعة لاحقاً.'),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final periodLabel = DateFormat('MMMM yyyy', 'ar')
        .format(DateTime(_selectedYear, _selectedMonth));
    final cycle = _cycleSummary;
    final dayRows = _daySummaries;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _Panel(
          title: 'كشف شهري: ${widget.employee.name}',
          icon: Icons.calendar_month_outlined,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(
                    child: DropdownButtonFormField<int>(
                      initialValue: _selectedYear,
                      decoration: const InputDecoration(labelText: 'السنة'),
                      items: _availableYears
                          .map((year) => DropdownMenuItem<int>(
                              value: year, child: Text('$year')))
                          .toList(),
                      onChanged: _onChangeYear,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: DropdownButtonFormField<int>(
                      initialValue: _selectedMonth,
                      decoration: const InputDecoration(labelText: 'الشهر'),
                      items: _availableMonths
                          .map((month) => DropdownMenuItem<int>(
                              value: month,
                              child: Text(DateFormat('MMMM', 'ar')
                                  .format(DateTime(_selectedYear, month)))))
                          .toList(),
                      onChanged: _onChangeMonth,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  FilledButton.icon(
                    onPressed: () => setState(() {}),
                    icon: const Icon(Icons.filter_alt_rounded),
                    label: const Text('عرض الفترة'),
                  ),
                  FilledButton.tonalIcon(
                    onPressed: () => _showPrintMessage('كشف الشهر المحدد'),
                    icon: const Icon(Icons.print_rounded),
                    label: const Text('طباعة الشهر المحدد'),
                  ),
                  OutlinedButton.icon(
                    onPressed: () => _showPrintMessage('اختيار عدة أشهر'),
                    icon: const Icon(Icons.calendar_view_month_rounded),
                    label: const Text('اختيار عدة أشهر'),
                  ),
                  OutlinedButton.icon(
                    onPressed: () => _showPrintMessage('الطباعة المتعددة'),
                    icon: const Icon(Icons.print_disabled_rounded),
                    label: const Text('طباعة الأشهر المحددة'),
                  ),
                  OutlinedButton.icon(
                    onPressed: () => _showPrintMessage('الكشف الكامل'),
                    icon: const Icon(Icons.description_rounded),
                    label: const Text('طباعة الكشف الكامل'),
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            _MetricCard(
                label: 'الشهر',
                value: periodLabel,
                accent: UiPalette.primaryBlue),
            _MetricCard(
                label: 'الإنتاج',
                value: '${cycle.totalQuantity.toStringAsFixed(0)} قطعة',
                accent: UiPalette.softBlue),
            _MetricCard(
                label: 'قيمة الإنتاج',
                value: _detailsMoney.format(cycle.totalWage),
                accent: UiPalette.primaryDark),
            _MetricCard(
                label: 'السلف',
                value: _detailsMoney.format(cycle.totalDraws),
                accent: UiPalette.purpleAccent),
            _MetricCard(
                label: 'المصروفات اليومية',
                value: _detailsMoney.format(cycle.totalDailyExpense),
                accent: UiPalette.softBlue),
            _MetricCard(
                label: 'المتبقي له',
                value: _detailsMoney.format(cycle.netDue),
                accent: UiPalette.primaryBlue),
          ],
        ),
        const SizedBox(height: 12),
        _Panel(
          title: 'أنواع القطع المسجلة في الفترة',
          icon: Icons.precision_manufacturing_outlined,
          child: cycle.pieceTypes.isEmpty
              ? const _EmptyState(
                  title: 'لا توجد بيانات إنتاج فعلية في هذا الشهر.',
                  body:
                      'القيم الظاهرة هنا تُستخرج مباشرة من PieceWageRecords و TrackingEvents في قاعدة البيانات الحالية.',
                )
              : Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: cycle.pieceTypes
                      .map(
                        (entry) => InkWell(
                          onTap: () => _showPieceTypeDetail(entry.pieceType),
                          borderRadius: BorderRadius.circular(12),
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 12, vertical: 10),
                            decoration: BoxDecoration(
                              color: UiPalette.softBlue,
                              borderRadius: BorderRadius.circular(12),
                              border: Border.all(color: UiPalette.borderSoft),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text(
                                  entry.pieceType,
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.softBlue,
                                    fontSize: 12,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Text(
                                  '${entry.totalQuantity.toStringAsFixed(0)} • ${_detailsMoney.format(entry.totalWage)}',
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.softBlue,
                                    fontSize: 11,
                                  ).copyWith(color: UiPalette.textSoft),
                                ),
                              ],
                            ),
                          ),
                        ),
                      )
                      .toList(),
                ),
        ),
        const SizedBox(height: 12),
        _Panel(
          title: 'جدول الكشف الشهري',
          icon: Icons.table_chart_rounded,
          child: dayRows.isEmpty
              ? const _EmptyState(
                  title: 'لا توجد أيام إنتاج لهذا الشهر.',
                  body:
                      'سيظهر هنا كل يوم فيه إنتاج فعلي، مع مجموع القطع والسلف والمصروفات اليومية فقط.',
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: SizedBox(
                        width: 980,
                        child: DataTable(
                          headingRowColor:
                              WidgetStateProperty.all(UiPalette.softBlue),
                          columns: const [
                            DataColumn(label: Text('اليوم / التاريخ')),
                            DataColumn(label: Text('الإنتاج اليومي')),
                            DataColumn(label: Text('القطع المشتغلة')),
                            DataColumn(label: Text('المصروفات اليومية')),
                            DataColumn(label: Text('السلف')),
                          ],
                          rows: [
                            ...dayRows.map(
                              (day) => DataRow(
                                cells: [
                                  DataCell(
                                    Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        Text(
                                          DateFormat('EEE', 'ar')
                                              .format(day.date),
                                          style: UiPalette.adaptiveTextStyle(
                                            context,
                                            backgroundColor:
                                                UiPalette.surfaceCard,
                                            fontSize: 11,
                                          ).copyWith(color: UiPalette.textSoft),
                                        ),
                                        Text(
                                          _detailsDate.format(day.date),
                                          style: UiPalette.adaptiveTextStyle(
                                            context,
                                            backgroundColor:
                                                UiPalette.surfaceCard,
                                            fontSize: 12,
                                            fontWeight: FontWeight.w700,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  DataCell(
                                    Text(
                                      '${day.totalQuantity.toStringAsFixed(0)} قطعة\n${_detailsMoney.format(day.totalWage)} ر.س',
                                      style: UiPalette.adaptiveTextStyle(
                                        context,
                                        backgroundColor: UiPalette.surfaceCard,
                                        fontSize: 12,
                                      ),
                                    ),
                                  ),
                                  DataCell(
                                    SizedBox(
                                      width: 220,
                                      child: Wrap(
                                        spacing: 6,
                                        runSpacing: 6,
                                        children: day.pieceTypes
                                            .map(
                                              (pieceType) => InkWell(
                                                onTap: () =>
                                                    _showPieceTypeDetail(
                                                        pieceType),
                                                child: Container(
                                                  padding: const EdgeInsets
                                                      .symmetric(
                                                      horizontal: 8,
                                                      vertical: 6),
                                                  decoration: BoxDecoration(
                                                    color: UiPalette.softBlue,
                                                    borderRadius:
                                                        BorderRadius.circular(
                                                            10),
                                                    border: Border.all(
                                                        color: UiPalette
                                                            .borderSoft),
                                                  ),
                                                  child: Text(
                                                    pieceType,
                                                    style: UiPalette
                                                        .adaptiveTextStyle(
                                                      context,
                                                      backgroundColor:
                                                          UiPalette.softBlue,
                                                      fontSize: 11,
                                                      fontWeight:
                                                          FontWeight.w700,
                                                    ),
                                                  ),
                                                ),
                                              ),
                                            )
                                            .toList(),
                                      ),
                                    ),
                                  ),
                                  DataCell(
                                    Text(
                                      '0.00',
                                      style: UiPalette.adaptiveTextStyle(
                                        context,
                                        backgroundColor: UiPalette.surfaceCard,
                                        fontSize: 12,
                                      ),
                                    ),
                                  ),
                                  DataCell(
                                    Text(
                                      _detailsMoney.format(day.totalDraws),
                                      style: UiPalette.adaptiveTextStyle(
                                        context,
                                        backgroundColor: UiPalette.surfaceCard,
                                        fontSize: 12,
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            DataRow(
                              color: WidgetStateProperty.all(
                                  UiPalette.softBlue.withAlpha(80)),
                              cells: [
                                DataCell(Text(
                                    'إجمالي ${DateFormat('MMMM yyyy', 'ar').format(DateTime(_selectedYear, _selectedMonth))}')),
                                DataCell(Text(
                                    '${cycle.totalQuantity.toStringAsFixed(0)} قطعة\n${_detailsMoney.format(cycle.totalWage)} ر.س')),
                                DataCell(Text(
                                    '${cycle.pieceTypes.length} نوع\n${cycle.pieceTypes.map((item) => item.pieceType).join(' • ')}')),
                                DataCell(Text(_detailsMoney
                                    .format(cycle.totalDailyExpense))),
                                DataCell(Text(
                                    _detailsMoney.format(cycle.totalDraws))),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: UiPalette.softBlue,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: UiPalette.borderSoft),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'إجمالي الشهر: ${DateFormat('MMMM yyyy', 'ar').format(DateTime(_selectedYear, _selectedMonth))}',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.softBlue,
                              fontSize: 16,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          const SizedBox(height: 8),
                          Text(
                              'إجمالي القطع: ${cycle.totalQuantity.toStringAsFixed(0)} قطع'),
                          Text(
                              'تفصيل الإنتاج: ${cycle.pieceTypes.map((item) => '${item.pieceType}: ${item.totalQuantity.toStringAsFixed(0)}').join(' • ')}'),
                          Text(
                              'قيمة الإنتاج: ${_detailsMoney.format(cycle.totalWage)} ر.س'),
                          Text(
                              'إجمالي المصروفات اليومية: ${_detailsMoney.format(cycle.totalDailyExpense)} ر.س'),
                          Text(
                              'إجمالي السلف: ${_detailsMoney.format(cycle.totalDraws)} ر.س'),
                          Text(
                              'المتبقي له: ${_detailsMoney.format(cycle.netDue)} ر.س'),
                          const SizedBox(height: 10),
                          Text('مكان التوقيع: ____________________________'),
                        ],
                      ),
                    ),
                  ],
                ),
        ),
      ],
    );
  }
}

class _MonthlyDaySummary {
  _MonthlyDaySummary({required this.date}) : pieceTypes = <String>{};

  final DateTime date;
  final Set<String> pieceTypes;
  double totalWage = 0;
  double totalQuantity = 0;
  double totalDraws = 0;
  double totalDailyExpense = 0;

  void addWage(PieceWageRecord item) {
    totalWage += item.totalWage;
    totalQuantity += item.quantity;
    if (item.pieceType.trim().isNotEmpty) {
      pieceTypes.add(item.pieceType.trim());
    }
  }

  void addDraw(double value) => totalDraws += value;

  void addExpense(double value) => totalDailyExpense += value;
}

class _MonthlyCycleSummary {
  const _MonthlyCycleSummary({
    required this.totalWage,
    required this.totalQuantity,
    required this.totalDraws,
    required this.totalSettlements,
    required this.totalDailyExpense,
    required this.openDrawBalance,
    required this.netDue,
    required this.pieceTypes,
  });

  final double totalWage;
  final double totalQuantity;
  final double totalDraws;
  final double totalSettlements;
  final double totalDailyExpense;
  final double openDrawBalance;
  final double netDue;
  final List<_PieceTypeSummary> pieceTypes;
}

class _PieceTypeSummary {
  _PieceTypeSummary({required this.pieceType});

  final String pieceType;
  double totalWage = 0;
  double totalQuantity = 0;
  int count = 0;

  void add(PieceWageRecord item) {
    totalWage += item.totalWage;
    totalQuantity += item.quantity;
    count += 1;
  }
}

class _ContractTab extends StatefulWidget {
  const _ContractTab({required this.employee, required this.repository});

  final EmployeeDetails employee;
  final EmployeeRepository repository;

  @override
  State<_ContractTab> createState() => _ContractTabState();
}

class _ContractTabState extends State<_ContractTab> {
  late Future<EmployeeContract> _contractFuture;

  @override
  void initState() {
    super.initState();
    _contractFuture = widget.repository.getEmployeeContract(widget.employee.id);
  }

  Future<void> _generate() async {
    setState(() {
      _contractFuture = widget.repository.generateEmployeeContract(widget.employee.id);
    });
  }

  Future<void> _printContract(EmployeeContract contract) async {
    final pdf = pw.Document();
    final arabicFont = await _loadArabicFont();
    final documentText = (contract.text ?? '').trim();

    pdf.addPage(
      pw.Page(
        pageFormat: PdfPageFormat.a4,
        build: (pw.Context context) {
          return pw.Column(
            crossAxisAlignment: pw.CrossAxisAlignment.start,
            children: [
              pw.Text(
                'عقد العمل',
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(
                  font: arabicFont,
                  fontSize: 24,
                  fontWeight: pw.FontWeight.bold,
                ),
              ),
              pw.SizedBox(height: 18),
              pw.Text(
                'رقم العقد: ${contract.number ?? '-'}',
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(font: arabicFont, fontSize: 12),
              ),
              pw.Text(
                'نوع العقد: ${contract.type ?? '-'}',
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(font: arabicFont, fontSize: 12),
              ),
              pw.Text(
                'الحالة: ${contract.status ?? '-'}',
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(font: arabicFont, fontSize: 12),
              ),
              pw.Text(
                'تاريخ البداية: ${contract.startDate == null ? '-' : _detailsDate.format(contract.startDate!)}',
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(font: arabicFont, fontSize: 12),
              ),
              pw.Text(
                'تاريخ النهاية: ${contract.endDate == null ? '-' : _detailsDate.format(contract.endDate!)}',
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(font: arabicFont, fontSize: 12),
              ),
              pw.Divider(),
              pw.SizedBox(height: 12),
              pw.Text(
                documentText.isEmpty ? 'لا يوجد نص عقد' : documentText,
                textAlign: pw.TextAlign.right,
                textDirection: pw.TextDirection.rtl,
                style: pw.TextStyle(font: arabicFont, fontSize: 12, height: 1.6),
              ),
            ],
          );
        },
      ),
    );

    final bytes = await pdf.save();
    await Printing.layoutPdf(onLayout: (_) => bytes);
  }

  Future<pw.Font> _loadArabicFont() async {
    final fontData = await rootBundle.load('assets/fonts/Tahoma.ttf');
    return pw.Font.ttf(fontData);
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<EmployeeContract>(
      future: _contractFuture,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(
            child: CircularProgressIndicator(color: UiPalette.primaryBlue),
          );
        }

        if (snapshot.hasError) {
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _Panel(
                title: 'عقد العمل',
                icon: Icons.description_outlined,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Icon(Icons.warning_amber_rounded,
                            color: Colors.amber, size: 32),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'تعذر قراءة العقد من الخادم.',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Text(
                      'يرجى إعادة المحاولة أو إنشاء نسخة جديدة من العقد.',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.surfaceCard,
                        fontSize: 13,
                      ).copyWith(color: UiPalette.textSoft),
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _generate,
                      icon: const Icon(Icons.refresh_rounded),
                      label: const Text('إنشاء العقد'),
                    ),
                  ],
                ),
              ),
            ],
          );
        }

        final contract = snapshot.data;
        if (contract == null || (contract.text ?? '').trim().isEmpty) {
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _Panel(
                title: 'عقد العمل',
                icon: Icons.description_outlined,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(Icons.info_outline_rounded,
                        size: 38, color: UiPalette.primaryBlue),
                    const SizedBox(height: 12),
                    Text(
                      'لا توجد نسخة عقدية محفوظة لهذا الموظف حتى الآن.',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.surfaceCard,
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _generate,
                      icon: const Icon(Icons.description_outlined),
                      label: const Text('توليد العقد'),
                    ),
                  ],
                ),
              ),
            ],
          );
        }

        final contractText = contract.text ?? '';
        final statusLabel = (contract.status ?? 'Active').trim();

        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            _Panel(
              title: 'بيانات العقد',
              icon: Icons.description_outlined,
              child: Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  _InfoTileData('رقم العقد', _detailsText(contract.number)),
                  _InfoTileData('نوع العقد', _detailsText(contract.type)),
                  _InfoTileData('الحالة', _detailsText(statusLabel)),
                  _InfoTileData(
                    'تاريخ البداية',
                    contract.startDate == null
                        ? '-'
                        : _detailsDate.format(contract.startDate!),
                  ),
                  _InfoTileData(
                    'تاريخ النهاية',
                    contract.endDate == null
                        ? '-'
                        : _detailsDate.format(contract.endDate!),
                  ),
                  _InfoTileData(
                    'تاريخ التوقيع',
                    contract.signedDate == null
                        ? '-'
                        : _detailsDate.format(contract.signedDate!),
                  ),
                ]
                    .map((item) => SizedBox(
                          width: 220,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                item.label,
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 12,
                                ).copyWith(color: UiPalette.textSoft),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                item.value,
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 14,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ],
                          ),
                        ))
                    .toList(),
              ),
            ),
            const SizedBox(height: 16),
            _Panel(
              title: 'نص العقد',
              icon: Icons.notes_rounded,
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: UiPalette.softBlue,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: UiPalette.borderSoft),
                ),
                child: SelectableText(
                  contractText,
                  textAlign: TextAlign.right,
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: UiPalette.softBlue,
                    fontSize: 14,
                  ).copyWith(height: 1.8),
                ),
              ),
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                FilledButton.icon(
                  onPressed: () => _printContract(contract),
                  icon: const Icon(Icons.print_rounded),
                  label: const Text('طباعة العقد'),
                ),
                const SizedBox(width: 12),
                FilledButton.icon(
                  onPressed: _generate,
                  icon: const Icon(Icons.refresh_rounded),
                  label: const Text('إعادة توليد العقد'),
                ),
              ],
            ),
          ],
        );
      },
    );
  }
}

class _Panel extends StatelessWidget {
  const _Panel({required this.title, required this.icon, required this.child});

  final String title;
  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: UiPalette.primaryBlue),
              const SizedBox(width: 8),
              Text(
                title,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          child,
        ],
      ),
    );
  }
}

class _HeaderBadge extends StatelessWidget {
  const _HeaderBadge({required this.label, required this.icon});

  final String label;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: UiPalette.softBlue,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 16, color: UiPalette.primaryBlue),
          const SizedBox(width: 6),
          Text(
            label,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.softBlue,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.label,
    required this.value,
    required this.accent,
  });

  final String label;
  final String value;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 160,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: accent.withAlpha(18),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: accent.withAlpha(100)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: accent,
              fontSize: 12,
            ).copyWith(color: UiPalette.textSoft),
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: accent,
              fontSize: 18,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoTileData {
  const _InfoTileData(this.label, this.value);

  final String label;
  final String value;
}

class _InfoValueTile extends StatelessWidget {
  const _InfoValueTile(this.label, this.value, {this.width = 220});

  final String label;
  final String value;
  final double width;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.surfaceCard,
              fontSize: 12,
            ).copyWith(color: UiPalette.textSoft),
          ),
          const SizedBox(height: 4),
          Text(
            value,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.surfaceCard,
              fontSize: 14,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: UiPalette.softBlue,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.softBlue,
              fontSize: 16,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            body,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.softBlue,
              fontSize: 13,
            ).copyWith(color: UiPalette.textSoft),
          ),
        ],
      ),
    );
  }
}

class _EmployeeDetailsError extends StatelessWidget {
  const _EmployeeDetailsError({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_outlined,
                size: 42, color: UiPalette.primaryBlue),
            const SizedBox(height: 10),
            Text(
              'تعذر تحميل تفاصيل الموظف.',
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 16,
                fontWeight: FontWeight.w700,
              ),
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
