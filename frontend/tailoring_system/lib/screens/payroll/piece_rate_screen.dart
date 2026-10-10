import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../../core/ui_palette.dart';
import '../../models/payroll_models.dart';
import '../../repositories/payroll_repository.dart';

const String _baseUrl = String.fromEnvironment(
  'LUMAR_API_URL',
  defaultValue: 'http://127.0.0.1:5093',
);

String _displayOfficialLabel(String value) {
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

  return translations[raw.toLowerCase()] ?? raw;
}

class _OfficialProductType {
  const _OfficialProductType({
    required this.productTypeId,
    required this.productTypeCode,
    required this.productTypeNameAr,
    required this.stages,
  });

  final int productTypeId;
  final String productTypeCode;
  final String productTypeNameAr;
  final List<String> stages;

  String get displayName =>
      productTypeNameAr.trim().isNotEmpty ? productTypeNameAr : productTypeCode;

  factory _OfficialProductType.fromJson(Map<String, dynamic> json) {
    final productTypeId = int.tryParse(
          (json['productTypeId'] ?? json['ProductTypeId'] ?? 0).toString(),
        ) ??
        0;
    final productTypeCode = (json['productTypeCode'] ??
            json['ProductTypeCode'] ??
            json['code'] ??
            json['Code'] ??
            '')
        .toString()
        .trim();
    final productTypeNameAr = (json['productTypeNameAr'] ??
            json['ProductTypeNameAr'] ??
            json['nameAr'] ??
            json['NameAr'] ??
            json['name'] ??
            json['Name'] ??
            '')
        .toString()
        .trim();
    final rawStages = (json['stages'] ??
            json['Stages'] ??
            json['route'] ??
            json['Route'] ??
            const <dynamic>[]) as List<dynamic>? ??
        const <dynamic>[];
    final stages = rawStages
        .map((value) => value.toString().trim())
        .where((value) => value.isNotEmpty)
        .toList();

    return _OfficialProductType(
      productTypeId: productTypeId,
      productTypeCode: productTypeCode,
      productTypeNameAr: productTypeNameAr,
      stages: stages,
    );
  }
}

class _PieceRateCatalog {
  const _PieceRateCatalog({required this.productTypes, required this.stageMap});

  final List<_OfficialProductType> productTypes;
  final Map<int, List<String>> stageMap;
}

class _PieceRateLoadData {
  const _PieceRateLoadData({required this.rates, required this.catalog});

  final List<PieceWageRate> rates;
  final _PieceRateCatalog catalog;
}

class PieceRateScreen extends StatefulWidget {
  const PieceRateScreen({super.key, this.repository});

  final PayrollRepository? repository;

  @override
  State<PieceRateScreen> createState() => _PieceRateScreenState();
}

class _PieceRateScreenState extends State<PieceRateScreen> {
  late final PayrollRepository _repository;
  late Future<_PieceRateLoadData> _future;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? PayrollRepository();
    _future = _loadData();
  }

  Future<_PieceRateLoadData> _loadData() async {
    final rates = await _repository.getPieceWageRates();
    final catalog = await _loadOfficialCatalog();
    return _PieceRateLoadData(rates: rates, catalog: catalog);
  }

  Future<_PieceRateCatalog> _loadOfficialCatalog() async {
    final response =
        await http.get(Uri.parse('$_baseUrl/settings/production-routes'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(
          'تعذر تحميل مسارات الإنتاج الرسمية (${response.statusCode})');
    }

    final payload = jsonDecode(response.body) as Map<String, dynamic>;
    final entries = (payload['routes'] as List<dynamic>? ?? const <dynamic>[])
        .map((value) =>
            _OfficialProductType.fromJson(value as Map<String, dynamic>))
        .where((entry) => entry.productTypeId > 0)
        .toList();

    final stageMap = <int, List<String>>{};
    for (final entry in entries) {
      stageMap[entry.productTypeId] = entry.stages;
    }

    if (entries.isEmpty) {
      final fallbackResponse =
          await http.get(Uri.parse('$_baseUrl/consumption-rules'));
      if (fallbackResponse.statusCode < 200 ||
          fallbackResponse.statusCode >= 300) {
        throw Exception('لا توجد أنواع قطع رسمية متاحة في مسارات الإنتاج.');
      }

      final fallbackPayload =
          jsonDecode(fallbackResponse.body) as Map<String, dynamic>;
      final productTypes = (fallbackPayload['productTypes'] as List<dynamic>? ??
              const <dynamic>[])
          .map((value) => value as Map<String, dynamic>)
          .where((value) {
            final productTypeId = int.tryParse(
                    (value['productTypeId'] ?? value['ProductTypeId'] ?? 0)
                        .toString()) ??
                0;
            return productTypeId > 0;
          })
          .map((value) => _OfficialProductType(
                productTypeId: int.tryParse(
                        (value['productTypeId'] ?? value['ProductTypeId'] ?? 0)
                            .toString()) ??
                    0,
                productTypeCode:
                    (value['code'] ?? value['Code'] ?? '').toString().trim(),
                productTypeNameAr: (value['nameAr'] ??
                        value['NameAr'] ??
                        value['name'] ??
                        value['Name'] ??
                        '')
                    .toString()
                    .trim(),
                stages: const <String>[],
              ))
          .toList();

      return _PieceRateCatalog(productTypes: productTypes, stageMap: const {});
    }

    return _PieceRateCatalog(productTypes: entries, stageMap: stageMap);
  }

  void _reload() => setState(() => _future = _loadData());

  Future<void> _create() async {
    final catalog = (await _future).catalog;
    if (!mounted) return;
    if (catalog.productTypes.isEmpty) {
      final messenger = ScaffoldMessenger.maybeOf(context);
      if (messenger != null) {
        messenger.showSnackBar(
          const SnackBar(content: Text('لا توجد أنواع قطع رسمية متاحة حاليًا.')),
        );
      }
      return;
    }

    final result = await showDialog<_RateDraft>(
      context: context,
      builder: (_) =>
          _RateEditorDialog(officialProductTypes: catalog.productTypes),
    );
    if (result == null || !mounted) return;
    try {
      await _repository.createPieceWageRate(
        pieceType: result.pieceType,
        stage: result.stage,
        wageRate: result.wageRate,
        isActive: result.isActive,
        notes: result.notes,
      );
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(context);
      messenger?.showSnackBar(
        const SnackBar(content: Text('تم حفظ سعر القطعة بنجاح.')),
      );
      _reload();
    } catch (error) {
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(context);
      messenger?.showSnackBar(
        SnackBar(content: Text('تعذر حفظ سعر القطعة: $error')),
      );
    }
  }

  Future<void> _edit(PieceWageRate rate) async {
    final catalog = (await _future).catalog;
    if (!mounted) return;
    final result = await showDialog<_RateDraft>(
      context: context,
      builder: (_) => _RateEditorDialog(
        officialProductTypes: catalog.productTypes,
        initial: rate,
      ),
    );
    if (result == null || !mounted) return;
    try {
      await _repository.updatePieceWageRate(
        rate.id,
        pieceType: result.pieceType,
        stage: result.stage,
        wageRate: result.wageRate,
        isActive: result.isActive,
        notes: result.notes,
      );
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(context);
      messenger?.showSnackBar(
        const SnackBar(content: Text('تم تحديث سعر القطعة بنجاح.')),
      );
      _reload();
    } catch (error) {
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(context);
      messenger?.showSnackBar(
        SnackBar(content: Text('تعذر تحديث سعر القطعة: $error')),
      );
    }
  }

  Future<void> _delete(PieceWageRate rate) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        backgroundColor: UiPalette.surfaceCard,
        title: const Text('حذف سعر القطعة'),
        content: Text('هل تريد حذف ${rate.pieceType} • ${rate.stage}؟'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('إلغاء')),
          FilledButton.icon(
            onPressed: () => Navigator.pop(context, true),
            icon: const Icon(Icons.delete_outline_rounded),
            label: const Text('حذف'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await _repository.deletePieceWageRate(rate.id);
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(context);
      if (messenger != null) {
        messenger.showSnackBar(
          const SnackBar(content: Text('تم حذف سعر القطعة.')),
        );
      }
      _reload();
    } catch (error) {
      if (!mounted) return;
      final messenger = ScaffoldMessenger.maybeOf(context);
      if (messenger != null) {
        messenger.showSnackBar(
          SnackBar(content: Text('تعذر حذف سعر القطعة: $error')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.textMain,
          title: const Text('أسعار القطعة'),
          actions: [
            IconButton(
              tooltip: 'إضافة سعر جديد',
              onPressed: _create,
              icon: const Icon(Icons.add_circle_outline_rounded),
            ),
            IconButton(
              tooltip: 'تحديث',
              onPressed: _reload,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: FutureBuilder<_PieceRateLoadData>(
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
                    Text('تعذر تحميل أسعار القطعة:\n${snapshot.error}'),
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

            final data = snapshot.data!;
            final rates = data.rates;
            final productTypes = data.catalog.productTypes;
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  color: UiPalette.surfaceCard,
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        const Icon(Icons.attach_money_outlined,
                            color: UiPalette.primaryBlue),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('أسعار القطعة',
                                  style: Theme.of(context)
                                      .textTheme
                                      .titleMedium
                                      ?.copyWith(color: UiPalette.textMain)),
                              Text(
                                  '${rates.length} سعر مسجل • ${rates.where((item) => item.isActive).length} نشط • ${productTypes.length} نوع رسمي',
                                  style: Theme.of(context)
                                      .textTheme
                                      .bodyMedium
                                      ?.copyWith(color: UiPalette.textSoft)),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                if (rates.isEmpty)
                  const Center(child: Text('لا توجد أسعار مسجلة حاليًا.'))
                else
                  ...rates.map((item) => Card(
                        color: UiPalette.surfaceCard,
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          leading: Icon(
                              item.isActive
                                  ? Icons.check_circle_outline_rounded
                                  : Icons.pause_circle_outline_rounded,
                              color: item.isActive
                                  ? UiPalette.primaryBlue
                                  : UiPalette.textSoft),
                          title: Text('${_displayOfficialLabel(item.pieceType)} • ${_displayOfficialLabel(item.stage)}',
                              style: TextStyle(color: UiPalette.textMain)),
                          subtitle: item.notes == null ||
                                  item.notes!.trim().isEmpty
                              ? Text(item.isActive ? 'نشط' : 'متوقف',
                                  style: TextStyle(color: UiPalette.textSoft))
                              : Text(item.notes!,
                                  style: TextStyle(color: UiPalette.textSoft)),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                  '${NumberFormat('#,##0.00').format(item.wageRate)} ر.س',
                                  style: TextStyle(
                                      color: item.wageRate == 0
                                          ? UiPalette.primaryBlue
                                          : UiPalette.textMain)),
                              const SizedBox(width: 8),
                              IconButton(
                                  onPressed: () => _edit(item),
                                  icon: const Icon(Icons.edit_outlined),
                                  tooltip: 'تعديل'),
                              IconButton(
                                  onPressed: () => _delete(item),
                                  icon:
                                      const Icon(Icons.delete_outline_rounded),
                                  tooltip: 'حذف'),
                            ],
                          ),
                        ),
                      )),
              ],
            );
          },
        ),
      );
}

class _RateDraft {
  const _RateDraft({
    required this.pieceType,
    required this.stage,
    required this.wageRate,
    required this.isActive,
    this.notes,
  });

  final String pieceType;
  final String stage;
  final double wageRate;
  final bool isActive;
  final String? notes;
}

class _RateEditorDialog extends StatefulWidget {
  const _RateEditorDialog({
    required this.officialProductTypes,
    this.initial,
  });

  final List<_OfficialProductType> officialProductTypes;
  final PieceWageRate? initial;

  @override
  State<_RateEditorDialog> createState() => _RateEditorDialogState();
}

class _RateEditorDialogState extends State<_RateEditorDialog> {
  int? _selectedProductTypeId;
  String? _selectedStage;
  final _rateController = TextEditingController();
  final _notesController = TextEditingController();
  bool _isActive = true;

  @override
  void initState() {
    super.initState();
    final initial = widget.initial;
    if (widget.officialProductTypes.isNotEmpty) {
      _selectedProductTypeId = widget.officialProductTypes.first.productTypeId;
    }
    if (initial != null) {
      final match = widget.officialProductTypes.indexWhere(
        (type) =>
            type.displayName == initial.pieceType ||
            type.productTypeCode == initial.pieceType ||
            type.productTypeNameAr == initial.pieceType,
      );
      if (match >= 0) {
        _selectedProductTypeId =
            widget.officialProductTypes[match].productTypeId;
      }
      _selectedStage = initial.stage;
      _rateController.text = initial.wageRate.toStringAsFixed(2);
      _notesController.text = initial.notes ?? '';
      _isActive = initial.isActive;
    } else {
      final firstStages = _stageOptionsForSelectedType();
      _selectedStage = firstStages.isNotEmpty ? firstStages.first : null;
      _rateController.text = '0.00';
    }
  }

  _OfficialProductType? get _selectedType {
    if (_selectedProductTypeId == null) return null;
    for (final type in widget.officialProductTypes) {
      if (type.productTypeId == _selectedProductTypeId) {
        return type;
      }
    }
    return null;
  }

  List<String> _stageOptionsForSelectedType() {
    final selectedType = _selectedType;
    if (selectedType == null) return const <String>[];
    return selectedType.stages;
  }

  @override
  void dispose() {
    _rateController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  void _save() {
    final selectedType = _selectedType;
    final stage = (_selectedStage ?? '').trim();
    final wageRate = double.tryParse(_rateController.text.trim());
    if (selectedType == null || stage.isEmpty || wageRate == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('اختر نوع القطعة الرسمي والمرحلة والسعر الصحيح.')),
      );
      return;
    }
    if (wageRate < 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text(
                'السعر لا يمكن أن يكون سالبًا، والقيمة صفر مسموح بها للمرحلة غير المدفوعة.')),
      );
      return;
    }
    Navigator.pop(
      context,
      _RateDraft(
        pieceType: selectedType.displayName,
        stage: stage,
        wageRate: wageRate,
        isActive: _isActive,
        notes: _notesController.text.trim().isEmpty
            ? null
            : _notesController.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final stageOptions = _stageOptionsForSelectedType();
    return AlertDialog(
      backgroundColor: UiPalette.surfaceCard,
      title:
          Text(widget.initial == null ? 'إضافة سعر جديد' : 'تعديل سعر القطعة'),
      content: SizedBox(
        width: 430,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.officialProductTypes.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(bottom: 8),
                  child: Text(
                      'لا توجد أنواع قطع رسمية متاحة في مسارات الإنتاج الحالية.'),
                )
              else
                DropdownButtonFormField<int>(
                  initialValue: _selectedProductTypeId,
                  items: widget.officialProductTypes
                      .map(
                        (type) => DropdownMenuItem<int>(
                          value: type.productTypeId,
                          child: Text(_displayOfficialLabel(type.displayName)),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value == null) return;
                    setState(() {
                      _selectedProductTypeId = value;
                      final options = _stageOptionsForSelectedType();
                      if (options.isNotEmpty &&
                          !options.contains(_selectedStage)) {
                        _selectedStage = options.first;
                      }
                    });
                  },
                  decoration:
                      const InputDecoration(labelText: 'نوع القطعة الرسمي'),
                ),
              const SizedBox(height: 12),
              if (stageOptions.isEmpty)
                const Text(
                    'لا توجد مراحل رسمية فعالة لهذا النوع في مسار الإنتاج.')
              else
                DropdownButtonFormField<String>(
                  initialValue: stageOptions.contains(_selectedStage)
                      ? _selectedStage
                      : stageOptions.first,
                  items: stageOptions
                      .map(
                        (stage) => DropdownMenuItem<String>(
                          value: stage,
                          child: Text(_displayOfficialLabel(stage)),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value == null) return;
                    setState(() => _selectedStage = value);
                  },
                  decoration:
                      const InputDecoration(labelText: 'المرحلة الرسمية'),
                ),
              const SizedBox(height: 12),
              TextField(
                controller: _rateController,
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(labelText: 'السعر (ر.س)'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _notesController,
                maxLines: 3,
                decoration:
                    const InputDecoration(labelText: 'ملاحظات (اختياري)'),
              ),
              const SizedBox(height: 12),
              CheckboxListTile(
                value: _isActive,
                onChanged: (value) => setState(() => _isActive = value ?? true),
                title: const Text('مفعل'),
                controlAffinity: ListTileControlAffinity.leading,
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إلغاء')),
        FilledButton.icon(
          onPressed: _save,
          icon: const Icon(Icons.save_outlined),
          label: const Text('حفظ'),
        ),
      ],
    );
  }
}
