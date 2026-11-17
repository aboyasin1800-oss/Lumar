import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../core/ui_palette.dart';
import '../models/employee_models.dart';
import '../repositories/employee_repository.dart';

DateTime? parseHireDateFromDisplay(String value) {
  final text = value.trim();
  if (text.isEmpty) return null;

  try {
    return DateFormat('yyyy/MM/dd').parseStrict(text);
  } catch (_) {
    return null;
  }
}

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

String _displayProductionLabel(String value) {
  final raw = value.trim();
  if (raw.isEmpty) return '';

  const translations = <String, String>{
    'printing': 'طباعة',
    'cutting': 'قطع',
    'sewing': 'خياطة',
    'embroidery': 'طباعة/تطريز',
    'washing': 'غسيل',
    'finishing': 'تجهيز',
    'packing': 'تغليف',
    'quality': 'جودة',
    'delivery': 'تسليم',
    'assembly': 'تجميع',
  };

  final lower = raw.toLowerCase();
  return translations[lower] ?? raw;
}

class _EmployeeFormScreenState extends State<EmployeeFormScreen> {
  late final EmployeeRepository repository;
  final _formKey = GlobalKey<FormState>();
  final _employeeCode = TextEditingController();
  final _fullName = TextEditingController();
  final _phoneNumber = TextEditingController();
  final _basicSalary = TextEditingController();
  final _hireDate = TextEditingController();
  DateTime? _hireDateValue;

  static const String _salaryTypeBasic = 'BasicSalary';
  static const String _salaryTypePiece = 'PieceWage';

  late List<EmployeeDepartment> _departments;
  List<PieceWageProductTypeOption> _productionOptions = const <PieceWageProductTypeOption>[];
  final Map<int, List<String>> _stageOptionsByProductTypeId = <int, List<String>>{};
  final Map<int, Map<String, double>> _officialPieceWageRatesByProductTypeId = <int, Map<String, double>>{};
  final List<EmployeePieceRateRow> _pieceRateRows = <EmployeePieceRateRow>[
    const EmployeePieceRateRow(),
  ];
  int? _departmentId;
  String _status = 'Active';
  String _salaryType = _salaryTypeBasic;
  bool _saving = false;
  bool _loadingDepartments = true;
  bool _loadingProductionOptions = true;

  @override
  void initState() {
    super.initState();
    repository = widget.repository ?? EmployeeRepository();
    _departments = const [];
    _seedFromEmployee();
    _loadDepartments();
    _loadProductionRouteOptions();
    _loadOfficialPieceWageRates();
  }

  Future<void> _loadOfficialPieceWageRates() async {
    try {
      final officialRates = await repository.getOfficialPieceWageRatesByProductTypeId();
      if (!mounted) return;
      setState(() {
        _officialPieceWageRatesByProductTypeId.clear();
        _officialPieceWageRatesByProductTypeId.addAll(officialRates);
      });
    } catch (_) {
      if (!mounted) return;
      // The official rate source is optional for the employee form; it should not block the contract flow.
    }
  }

  String _displayOfficialPieceRate(int? productTypeId, String stage) {
    final rate = productTypeId == null ? null : _officialPieceWageRatesByProductTypeId[productTypeId]?[stage.trim()];
    if (rate == null) {
      return 'غير متاح';
    }
    return '${NumberFormat('#,##0.00').format(rate)} ر.س';
  }

  void _seedFromEmployee() {
    final employee = widget.employee;
    if (employee == null) {
      _status = 'Active';
      _salaryType = _salaryTypeBasic;
      _hireDateValue = DateTime.now();
      _hireDate.text = DateFormat('yyyy/MM/dd').format(_hireDateValue!);
      return;
    }

    _employeeCode.text = employee.code;
    _fullName.text = employee.fullName;
    _phoneNumber.text = employee.phoneNumber ?? '';
    _basicSalary.text = employee.basicSalary.toStringAsFixed(2);
    _hireDateValue = employee.hireDate;
    _hireDate.text = DateFormat('yyyy/MM/dd').format(_hireDateValue!);
    _departmentId = employee.departmentId;
    _status = employee.status;
    _salaryType = employee.salaryType == _salaryTypePiece ? _salaryTypePiece : _salaryTypeBasic;

    if (_salaryType == _salaryTypePiece) {
      _pieceRateRows
        ..clear()
        ..addAll(
          employee.pieceRates.isNotEmpty
              ? employee.pieceRates
              : const <EmployeePieceRateRow>[EmployeePieceRateRow()],
        );
    }
  }

  Future<void> _loadProductionRouteOptions() async {
    try {
      final options = await repository.getPieceWageProductTypes();
      final stageMap = <int, List<String>>{};
      for (final option in options) {
        if (option.productTypeId > 0) {
          stageMap[option.productTypeId] = option.allowedStages;
        }
      }
      if (!mounted) return;
      setState(() {
        _productionOptions = options;
        _stageOptionsByProductTypeId.clear();
        _stageOptionsByProductTypeId.addAll(stageMap);
        _loadingProductionOptions = false;
        if (_pieceRateRows.isEmpty || _pieceRateRows.every((row) => row.productTypeId == null && row.pieceType.isEmpty && row.stage.isEmpty && row.rateText.isEmpty)) {
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
        if (departments.isEmpty) {
          _departmentId = null;
        } else if (_departmentId == null || !departments.any((d) => d.id == _departmentId)) {
          _departmentId = departments.first.id;
        }
        if (widget.employee != null && departments.any((d) => d.id == widget.employee!.departmentId)) {
          _departmentId = widget.employee!.departmentId;
        }
        _loadingDepartments = false;
      });
    } on EmployeeApiException catch (error) {
      if (!mounted) return;
      setState(() {
        _departments = const [];
        _departmentId = null;
        _loadingDepartments = false;
      });
      final friendlyMessage = switch (error.statusCode) {
        401 => 'انتهت الجلسة. سجّل الدخول من جديد.',
        403 => 'ليس لديك صلاحية للوصول إلى أقسام الموظفين.',
        _ => 'تعذر تحميل أقسام الموظفين.',
      };
      _showMessage(friendlyMessage, isError: true);
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _departments = const [];
        _departmentId = null;
        _loadingDepartments = false;
      });
      _showMessage('تعذر تحميل أقسام الموظفين.', isError: true);
    }
  }

  Future<void> _pickHireDate() async {
    final now = DateTime.now();
    final parsedInitial = _hireDateValue ?? parseHireDateFromDisplay(_hireDate.text) ?? now;
    final picked = await showDatePicker(
      context: context,
      initialDate: parsedInitial,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );

    if (picked == null) return;
    setState(() {
      _hireDateValue = picked;
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
          .where((row) => row.pieceType.trim().isNotEmpty || row.stage.trim().isNotEmpty || row.rateText.trim().isNotEmpty)
          .toList();

      if (validRows.isEmpty) {
        _showMessage('يجب إضافة سطر واحد على الأقل لأنواع القطع.', isError: true);
        return;
      }

      for (final row in validRows) {
        if (row.productTypeId == null || row.productTypeId! <= 0) {
          _showMessage('نوع القطعة مطلوب في كل سطر.', isError: true);
          return;
        }
        if (row.stage.trim().isEmpty) {
          _showMessage('مرحلة العمل مطلوبة في كل سطر.', isError: true);
          return;
        }

        final option = _productionOptions.firstWhere(
          (entry) => entry.productTypeId == row.productTypeId,
          orElse: () => const PieceWageProductTypeOption(
            productTypeId: 0,
            code: '',
            displayName: '',
            allowedStages: <String>[],
          ),
        );
        final officialRate = _officialPieceWageRatesByProductTypeId[row.productTypeId]?[row.stage.trim()];
        if (officialRate == null) {
          final label = option.displayName.isNotEmpty ? option.displayName : 'نوع القطعة المحدد';
          _showMessage(
            'السعر العام الرسمي غير متاح لـ "$label" في المرحلة "${row.stage.trim()}". لا يمكن حفظ الأجر دون مصدر السعر الرسمي.',
            isError: true,
          );
          return;
        }

        final extra = row.parsedRate ?? 0.0;
        if (extra < 0) {
          _showMessage('الإضافي لا يمكن أن يكون سالباً.', isError: true);
          return;
        }
      }
    }

    final salaryValue = _salaryType == _salaryTypePiece
        ? 0.0
        : (double.tryParse(_basicSalary.text.trim()) ?? 0.0);

    if (_salaryType == _salaryTypeBasic && salaryValue <= 0) {
      _showMessage('يجب أن يكون الراتب الأساسي أكبر من صفر.', isError: true);
      return;
    }

    if (_salaryType == _salaryTypePiece && salaryValue != 0) {
      _showMessage('لأجر القطعة يجب أن يكون الراتب الأساسي صفرًا.', isError: true);
      return;
    }

    final parsedHireDate = _hireDateValue ?? parseHireDateFromDisplay(_hireDate.text);
    if (parsedHireDate == null) {
      _showMessage('تاريخ التوظيف غير صحيح.', isError: true);
      return;
    }

    final payload = EmployeeWritePayload(
      employeeCode: _employeeCode.text,
      fullName: _fullName.text,
      departmentId: _departmentId!,
      basicSalary: salaryValue,
      phoneNumber: _phoneNumber.text,
      hireDate: parsedHireDate,
      status: _status,
      salaryType: _salaryType,
      officialPieceRates: _officialPieceWageRatesByProductTypeId.map(
        (productTypeId, ratesByStage) =>
            MapEntry(productTypeId.toString(), ratesByStage),
      ),
      pieceRates: _salaryType == _salaryTypePiece
          ? _pieceRateRows
              .where((row) => row.pieceType.trim().isNotEmpty || row.stage.trim().isNotEmpty || row.rateText.trim().isNotEmpty)
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
                                  readOnly: true,
                                  enabled: false,
                                  textInputAction: TextInputAction.next,
                                  decoration: const InputDecoration(
                                    labelText: 'رمز الموظف',
                                    hintText: 'سيتم توليده تلقائياً من النظام',
                                    border: OutlineInputBorder(),
                                  ),
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
                                  initialValue: _departments.any((department) => department.id == _departmentId)
                                      ? _departmentId
                                      : null,
                                  decoration: const InputDecoration(
                                    labelText: 'القسم',
                                    border: OutlineInputBorder(),
                                  ),
                                  hint: _departments.isEmpty ? const Text('لا توجد أقسام متاحة') : null,
                                  items: _departments
                                      .map(
                                        (department) => DropdownMenuItem<int>(
                                          value: department.id,
                                          child: Text(department.name),
                                        ),
                                      )
                                      .toList(),
                                  onChanged: _departments.isEmpty
                                      ? null
                                      : (value) => setState(() => _departmentId = value),
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
                                  initialValue: _salaryType,
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
                                width: 260,
                                child: DropdownButtonFormField<String>(
                                  initialValue: _status,
                                  isExpanded: true,
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
                                          width: 280,
                                          child: DropdownButtonFormField<int>(
                                            key: ValueKey('piece_type_${index}_dropdown'),
                                            value: row.productTypeId != null && row.productTypeId! > 0 ? row.productTypeId : null,
                                            isExpanded: true,
                                            decoration: const InputDecoration(
                                              labelText: 'نوع القطعة',
                                              border: OutlineInputBorder(),
                                            ),
                                            items: _productionOptions
                                                .map(
                                                  (option) => DropdownMenuItem<int>(
                                                    value: option.productTypeId,
                                                    child: Text(option.displayName),
                                                  ),
                                                )
                                                .toList(),
                                            onChanged: (value) {
                                              setState(() {
                                                final nextTypeId = value ?? 0;
                                                final selectedOption = _productionOptions.firstWhere(
                                                  (option) => option.productTypeId == nextTypeId,
                                                  orElse: () => const PieceWageProductTypeOption(
                                                    productTypeId: 0,
                                                    code: '',
                                                    displayName: '',
                                                    allowedStages: <String>[],
                                                  ),
                                                );
                                                final nextStages = _stageOptionsByProductTypeId[nextTypeId] ?? const <String>[];
                                                final nextStage = nextStages.contains(row.stage) ? row.stage : (nextStages.isNotEmpty ? nextStages.first : '');
                                                _pieceRateRows[index] = row.copyWith(
                                                  productTypeId: nextTypeId,
                                                  pieceType: selectedOption.label,
                                                  stage: nextStage,
                                                );
                                              });
                                            },
                                            validator: (value) =>
                                                (value == null || value <= 0)
                                                    ? 'نوع القطعة مطلوب.'
                                                    : null,
                                          ),
                                        ),
                                        SizedBox(
                                          width: 220,
                                          child: DropdownButtonFormField<String>(
                                            key: ValueKey('piece_stage_${index}_dropdown'),
                                            value: row.stage.isEmpty ? null : row.stage,
                                            isExpanded: true,
                                            decoration: const InputDecoration(
                                              labelText: 'المرحلة',
                                              border: OutlineInputBorder(),
                                            ),
                                            items: (_stageOptionsByProductTypeId[row.productTypeId] ?? const <String>[])
                                                .map(
                                                  (option) => DropdownMenuItem<String>(
                                                    value: option,
                                                    child: Text(_displayProductionLabel(option)),
                                                  ),
                                                )
                                                .toList(),
                                            onChanged: (value) {
                                              setState(() {
                                                _pieceRateRows[index] = row.copyWith(stage: value ?? '');
                                              });
                                            },
                                            validator: (value) =>
                                                (value == null || value.trim().isEmpty)
                                                    ? 'المرحلة مطلوبة.'
                                                    : null,
                                          ),
                                        ),
                                        SizedBox(
                                          width: 190,
                                          child: TextFormField(
                                            readOnly: true,
                                            initialValue: row.productTypeId == null || row.stage.isEmpty
                                                ? ''
                                                : _displayOfficialPieceRate(row.productTypeId, row.stage),
                                            decoration: const InputDecoration(
                                              labelText: 'السعر العام',
                                              border: OutlineInputBorder(),
                                            ),
                                          ),
                                        ),
                                        SizedBox(
                                          width: 170,
                                          child: TextFormField(
                                            initialValue: row.rateText,
                                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                            decoration: const InputDecoration(
                                              labelText: 'إضافي',
                                              hintText: 'اختياري',
                                              border: OutlineInputBorder(),
                                            ),
                                            onChanged: (value) {
                                              setState(() {
                                                _pieceRateRows[index] = row.copyWith(rateText: value);
                                              });
                                            },
                                            validator: (value) {
                                              final trimmed = (value ?? '').trim();
                                              if (trimmed.isEmpty) {
                                                return null;
                                              }
                                              final parsed = double.tryParse(trimmed);
                                              if (parsed == null || parsed < 0) {
                                                return 'الإضافي لا يمكن أن يكون سالباً.';
                                              }
                                              return null;
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
