import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/ui_palette.dart';
import '../models/employee_models.dart';
import '../repositories/employee_repository.dart';

class EmployeeFormScreen extends StatefulWidget {
  const EmployeeFormScreen({
    super.key,
    this.employee,
    this.onSaved,
    this.repository,
  });

  final EmployeeDetails? employee;
  final VoidCallback? onSaved;
  final EmployeeRepository? repository;

  @override
  State<EmployeeFormScreen> createState() => _EmployeeFormScreenState();
}

class _EmployeeFormScreenState extends State<EmployeeFormScreen> {
  late final EmployeeRepository repository;
  final _formKey = GlobalKey<FormState>();
  final _employeeCode = TextEditingController();
  final _fullName = TextEditingController();
  final _phoneNumber = TextEditingController();
  final _basicSalary = TextEditingController();
  final _hireDate = TextEditingController();

  static const Map<String, String> _statusLabels = {
    'Active': 'نشط',
    'Inactive': 'غير نشط',
    'Suspended': 'موقوف',
    'Terminated': 'منتهي الخدمة',
    'OnLeave': 'في إجازة',
  };

  static const String _salaryTypeBasic = 'BasicSalary';
  static const String _salaryTypePiece = 'PieceWage';

  late List<EmployeeDepartment> _departments;
  List<String> _productionOptions = const <String>[];
  final List<EmployeePieceRateRow> _pieceRateRows = <EmployeePieceRateRow>[
    const EmployeePieceRateRow(),
  ];
  int? _departmentId;
  String _status = 'Active';
  String _salaryType = _salaryTypeBasic;
  bool _saving = false;
  bool _loadingDepartments = true;
  bool _loadingProductionOptions = true;

  String _labelForStatus(String value) => _statusLabels[value] ?? value;

  @override
  void initState() {
    super.initState();
    repository = widget.repository ?? EmployeeRepository();
    _departments = const [];
    _seedFromEmployee();
    _loadDepartments();
    _loadProductionRouteOptions();
  }

  void _seedFromEmployee() {
    final employee = widget.employee;
    if (employee == null) {
      _status = 'Active';
      _salaryType = _salaryTypeBasic;
      _hireDate.text = DateFormat('yyyy/MM/dd').format(DateTime.now());
      return;
    }

    _employeeCode.text = employee.code;
    _fullName.text = employee.fullName;
    _phoneNumber.text = employee.phoneNumber ?? '';
    _basicSalary.text = employee.basicSalary.toStringAsFixed(2);
    _hireDate.text = DateFormat('yyyy/MM/dd').format(employee.hireDate);
    _departmentId = employee.departmentId;
    _status = employee.status;
    _salaryType = employee.salaryType == _salaryTypePiece ? _salaryTypePiece : _salaryTypeBasic;
  }

  Future<void> _loadProductionRouteOptions() async {
    try {
      final options = await repository.getProductionRouteOptions();
      if (!mounted) return;
      setState(() {
        _productionOptions = options;
        _loadingProductionOptions = false;
        if (_pieceRateRows.isEmpty || _pieceRateRows.every((row) => row.pieceType.isEmpty && row.rateText.isEmpty)) {
          _pieceRateRows.clear();
          _pieceRateRows.add(const EmployeePieceRateRow());
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingProductionOptions = false);
      _showMessage('تعذر تحميل أنواع القطع المتاحة.', isError: true);
    }
  }

  Future<void> _loadDepartments() async {
    try {
      final departments = await repository.getDepartments();
      if (!mounted) return;
      setState(() {
        _departments = departments;
        if (_departmentId == null && departments.isNotEmpty) {
          _departmentId = departments.first.id;
        }
        if (widget.employee != null && departments.any((d) => d.id == widget.employee!.departmentId)) {
          _departmentId = widget.employee!.departmentId;
        }
        _loadingDepartments = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingDepartments = false);
      _showMessage('تعذر تحميل أقسام الموظفين.', isError: true);
    }
  }

  Future<void> _pickHireDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.tryParse(_hireDate.text) ?? now,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );

    if (picked == null) return;
    setState(() {
      _hireDate.text = DateFormat('yyyy/MM/dd').format(picked);
    });
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_departmentId == null || _departmentId! <= 0) {
      _showMessage('القسم مطلوب.', isError: true);
      return;
    }

    if (_salaryType == _salaryTypePiece) {
      final validRows = _pieceRateRows
          .where((row) => row.pieceType.trim().isNotEmpty || row.rateText.trim().isNotEmpty)
          .toList();

      if (validRows.isEmpty) {
        _showMessage('يجب إضافة سطر واحد على الأقل لأنواع القطع.', isError: true);
        return;
      }

      for (final row in validRows) {
        if (row.pieceType.trim().isEmpty) {
          _showMessage('نوع القطعة مطلوب في كل سطر.', isError: true);
          return;
        }
        final rate = double.tryParse(row.rateText.trim());
        if (rate == null || rate <= 0) {
          _showMessage('السعر لكل نوع قطعة يجب أن يكون أكبر من صفر.', isError: true);
          return;
        }
      }
    }

    final salaryValue = _salaryType == _salaryTypePiece
        ? 0.01
        : (double.tryParse(_basicSalary.text.trim()) ?? 0.0);

    if (salaryValue <= 0) {
      _showMessage('يجب أن يكون الراتب الأساسي أكبر من صفر.', isError: true);
      return;
    }

    final payload = EmployeeWritePayload(
      employeeCode: _employeeCode.text,
      fullName: _fullName.text,
      departmentId: _departmentId!,
      basicSalary: salaryValue,
      phoneNumber: _phoneNumber.text,
      hireDate: DateTime.tryParse(_hireDate.text),
      status: _status,
      salaryType: _salaryType,
      pieceRates: _salaryType == _salaryTypePiece
          ? _pieceRateRows
              .where((row) => row.pieceType.trim().isNotEmpty || row.rateText.trim().isNotEmpty)
              .toList()
          : const <EmployeePieceRateRow>[],
    );

    setState(() => _saving = true);

    try {
      if (widget.employee == null) {
        await repository.createEmployee(payload);
      } else {
        await repository.updateEmployee(widget.employee!.id, payload);
      }

      if (!mounted) return;
      widget.onSaved?.call();
      _showMessage(
        widget.employee == null ? 'تمت إضافة الموظف بنجاح.' : 'تم تحديث الموظف بنجاح.',
      );
      if (mounted) Navigator.pop(context);
    } on EmployeeApiException catch (error) {
      if (!mounted) return;
      _showMessage(error.message, isError: true);
    } catch (_) {
      if (!mounted) return;
      _showMessage('تعذر حفظ بيانات الموظف.', isError: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _showMessage(String message, {bool isError = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Colors.red.shade700 : UiPalette.primaryDark,
      ),
    );
  }

  @override
  void dispose() {
    _employeeCode.dispose();
    _fullName.dispose();
    _phoneNumber.dispose();
    _basicSalary.dispose();
    _hireDate.dispose();
    super.dispose();
  }

  void _addPieceRateRow() {
    setState(() {
      _pieceRateRows.add(const EmployeePieceRateRow());
    });
  }

  void _removePieceRateRow(int index) {
    if (_pieceRateRows.length <= 1) {
      setState(() => _pieceRateRows[0] = const EmployeePieceRateRow());
      return;
    }
    setState(() => _pieceRateRows.removeAt(index));
  }

  @override
  Widget build(BuildContext context) {
    final isEdit = widget.employee != null;
    final showPieceWageConfig = _salaryType == _salaryTypePiece;

    return Scaffold(
      appBar: AppBar(
        title: Text(isEdit ? 'تعديل موظف' : 'إضافة موظف'),
      ),
      body: SafeArea(
        child: _loadingDepartments || _loadingProductionOptions
            ? const Center(child: CircularProgressIndicator())
            : Padding(
                padding: const EdgeInsets.all(16),
                child: Form(
                  key: _formKey,
                  child: ListView(
                    children: [
                      Card(
                        color: UiPalette.surfaceCard,
                        child: Padding(
                          padding: const EdgeInsets.all(18),
                          child: Wrap(
                            spacing: 18,
                            runSpacing: 18,
                            children: [
                              SizedBox(
                                width: 320,
                                child: TextFormField(
                                  controller: _employeeCode,
                                  textInputAction: TextInputAction.next,
                                  decoration: const InputDecoration(
                                    labelText: 'رمز الموظف',
                                    border: OutlineInputBorder(),
                                  ),
                                  validator: (value) =>
                                      (value == null || value.trim().isEmpty)
                                          ? 'رمز الموظف مطلوب.'
                                          : null,
                                ),
                              ),
                              SizedBox(
                                width: 420,
                                child: TextFormField(
                                  controller: _fullName,
                                  textInputAction: TextInputAction.next,
                                  decoration: const InputDecoration(
                                    labelText: 'الاسم الكامل',
                                    border: OutlineInputBorder(),
                                  ),
                                  validator: (value) =>
                                      (value == null || value.trim().isEmpty)
                                          ? 'الاسم الكامل مطلوب.'
                                          : null,
                                ),
                              ),
                              SizedBox(
                                width: 260,
                                child: DropdownButtonFormField<int>(
                                  value: _departmentId,
                                  decoration: const InputDecoration(
                                    labelText: 'القسم',
                                    border: OutlineInputBorder(),
                                  ),
                                  items: _departments
                                      .map(
                                        (department) => DropdownMenuItem<int>(
                                          value: department.id,
                                          child: Text(department.name),
                                        ),
                                      )
                                      .toList(),
                                  onChanged: (value) => setState(() => _departmentId = value),
                                  validator: (value) =>
                                      value == null || value <= 0
                                          ? 'القسم مطلوب.'
                                          : null,
                                ),
                              ),
                              SizedBox(
                                width: 280,
                                child: DropdownButtonFormField<String>(
                                  key: const ValueKey('employee_salary_type'),
                                  value: _salaryType,
                                  isExpanded: true,
                                  decoration: const InputDecoration(
                                    labelText: 'نوع الأجر',
                                    border: OutlineInputBorder(),
                                  ),
                                  items: const [
                                    DropdownMenuItem(
                                      value: _salaryTypeBasic,
                                      child: Text('راتب أساسي'),
                                    ),
                                    DropdownMenuItem(
                                      value: _salaryTypePiece,
                                      child: Text('أجر قطعة'),
                                    ),
                                  ],
                                  onChanged: (value) => setState(() => _salaryType = value ?? _salaryTypeBasic),
                                ),
                              ),
                              if (!showPieceWageConfig)
                                SizedBox(
                                  width: 220,
                                  child: TextFormField(
                                    controller: _basicSalary,
                                    keyboardType: const TextInputType.numberWithOptions(
                                      decimal: true,
                                    ),
                                    decoration: const InputDecoration(
                                      labelText: 'الراتب الأساسي',
                                      border: OutlineInputBorder(),
                                    ),
                                    validator: (value) {
                                      final parsed = double.tryParse(value?.trim() ?? '');
                                      if (value == null || value.trim().isEmpty) {
                                        return 'الراتب الأساسي مطلوب.';
                                      }
                                      if (parsed == null || parsed <= 0) {
                                        return 'يجب أن يكون الراتب الأساسي أكبر من صفر.';
                                      }
                                      return null;
                                    },
                                  ),
                                ),
                              SizedBox(
                                width: 260,
                                child: TextFormField(
                                  controller: _phoneNumber,
                                  keyboardType: TextInputType.phone,
                                  decoration: const InputDecoration(
                                    labelText: 'رقم الهاتف',
                                    border: OutlineInputBorder(),
                                  ),
                                ),
                              ),
                              SizedBox(
                                width: 220,
                                child: TextFormField(
                                  controller: _hireDate,
                                  readOnly: true,
                                  onTap: _pickHireDate,
                                  decoration: InputDecoration(
                                    labelText: 'تاريخ التوظيف',
                                    border: const OutlineInputBorder(),
                                    suffixIcon: IconButton(
                                      onPressed: _pickHireDate,
                                      icon: const Icon(Icons.calendar_today_outlined),
                                    ),
                                  ),
                                ),
                              ),
                              SizedBox(
                                width: 220,
                                child: DropdownButtonFormField<String>(
                                  value: _status,
                                  decoration: const InputDecoration(
                                    labelText: 'الحالة',
                                    border: OutlineInputBorder(),
                                  ),
                                  items: const [
                                    DropdownMenuItem(
                                      value: 'Active',
                                      child: Text('نشط'),
                                    ),
                                    DropdownMenuItem(
                                      value: 'Inactive',
                                      child: Text('غير نشط'),
                                    ),
                                    DropdownMenuItem(
                                      value: 'Suspended',
                                      child: Text('موقوف'),
                                    ),
                                    DropdownMenuItem(
                                      value: 'Terminated',
                                      child: Text('منتهي الخدمة'),
                                    ),
                                    DropdownMenuItem(
                                      value: 'OnLeave',
                                      child: Text('في إجازة'),
                                    ),
                                  ],
                                  onChanged: (value) => setState(() => _status = value ?? 'Active'),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                      if (showPieceWageConfig) ...[
                        const SizedBox(height: 18),
                        Card(
                          color: UiPalette.surfaceCard,
                          child: Padding(
                            padding: const EdgeInsets.all(16),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    const Icon(Icons.precision_manufacturing_outlined),
                                    const SizedBox(width: 8),
                                    const Text(
                                      'أسعار القطع',
                                      style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                                    ),
                                    const Spacer(),
                                    TextButton.icon(
                                      onPressed: _addPieceRateRow,
                                      icon: const Icon(Icons.add),
                                      label: const Text('إضافة سطر'),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 12),
                                ...List.generate(_pieceRateRows.length, (index) {
                                  final row = _pieceRateRows[index];
                                  return Padding(
                                    padding: const EdgeInsets.only(bottom: 12),
                                    child: Wrap(
                                      spacing: 12,
                                      runSpacing: 12,
                                      crossAxisAlignment: WrapCrossAlignment.center,
                                      children: [
                                        SizedBox(
                                          width: 320,
                                          child: DropdownButtonFormField<String>(
                                            key: ValueKey('piece_type_${index}_dropdown'),
                                            value: row.pieceType.isEmpty ? null : row.pieceType,
                                            isExpanded: true,
                                            decoration: const InputDecoration(
                                              labelText: 'نوع القطعة',
                                              border: OutlineInputBorder(),
                                            ),
                                            items: _productionOptions
                                                .map(
                                                  (option) => DropdownMenuItem<String>(
                                                    value: option,
                                                    child: Text(option),
                                                  ),
                                                )
                                                .toList(),
                                            onChanged: (value) {
                                              setState(() {
                                                _pieceRateRows[index] = row.copyWith(pieceType: value ?? '');
                                              });
                                            },
                                            validator: (value) =>
                                                (value == null || value.trim().isEmpty)
                                                    ? 'نوع القطعة مطلوب.'
                                                    : null,
                                          ),
                                        ),
                                        SizedBox(
                                          width: 170,
                                          child: TextFormField(
                                            initialValue: row.rateText,
                                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                            decoration: const InputDecoration(
                                              labelText: 'السعر',
                                              border: OutlineInputBorder(),
                                            ),
                                            onChanged: (value) {
                                              setState(() {
                                                _pieceRateRows[index] = row.copyWith(rateText: value);
                                              });
                                            },
                                          ),
                                        ),
                                        IconButton(
                                          onPressed: () => _removePieceRateRow(index),
                                          icon: const Icon(Icons.delete_outline),
                                          tooltip: 'حذف السطر',
                                        ),
                                      ],
                                    ),
                                  );
                                }),
                              ],
                            ),
                          ),
                        ),
                      ],
                      const SizedBox(height: 18),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          OutlinedButton(
                            onPressed: _saving ? null : () => Navigator.pop(context),
                            child: const Text('إلغاء'),
                          ),
                          const SizedBox(width: 12),
                          FilledButton.icon(
                            onPressed: _saving ? null : _save,
                            icon: _saving
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Icon(Icons.save_outlined),
                            label: Text(_saving ? 'جارٍ الحفظ...' : 'حفظ'),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
      ),
    );
  }
}
