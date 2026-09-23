import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/currency_formatter.dart';
import '../../core/ui_palette.dart';
import '../../services/currency_settings_service.dart';

class PieceCostManagementScreen extends StatefulWidget {
  const PieceCostManagementScreen({super.key});

  @override
  State<PieceCostManagementScreen> createState() =>
      _PieceCostManagementScreenState();
}

class _PieceCostManagementScreenState extends State<PieceCostManagementScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  late Future<List<_PieceCost>> _future;
  late Future<CurrencySettings> _currencyFuture;
  final _currencyService = CurrencySettingsService();
  _PieceCost? _selected;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _future = _load();
    _currencyFuture = _currencyService.load();
  }

  @override
  void dispose() {
    _currencyService.dispose();
    super.dispose();
  }

  Future<List<_PieceCost>> _load() async {
    final response =
        await http.get(Uri.parse('$_baseUrl/piece-cost-management'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('HTTP ${response.statusCode}');
    }
    final decoded = jsonDecode(response.body) as List;
    final items = decoded
        .whereType<Map<String, dynamic>>()
        .map(_PieceCost.fromJson)
        .toList();
    if (_selected != null) {
      _selected = items.firstWhere(
        (item) => item.productTypeId == _selected!.productTypeId,
        orElse: () => items.isEmpty ? _PieceCost.empty() : items.first,
      );
    } else if (items.isNotEmpty) {
      _selected = items.first;
    }
    return items;
  }

  Future<void> _refresh() async {
    final future = _load();
    setState(() => _future = future);
    await future;
  }

  Future<void> _save(_PieceCost value) async {
    setState(() => _saving = true);
    try {
      final response = await http.put(
        Uri.parse('$_baseUrl/piece-cost-management/${value.productTypeId}'),
        headers: const {'Content-Type': 'application/json'},
        body: jsonEncode(value.toRequest()),
      );
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('HTTP ${response.statusCode}');
      }
      final saved = _PieceCost.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>,
      );
      setState(() {
        _selected = saved;
        _future = _future.then(
          (items) => items
              .map((item) =>
                  item.productTypeId == saved.productTypeId ? saved : item)
              .toList(),
        );
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تم حفظ تكاليف ${saved.pieceName}.')),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تعذر حفظ تكاليف القطعة.')),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.textMain,
        title: const Text('إدارة تكاليف القطع'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _saving ? null : _refresh,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: FutureBuilder<List<_PieceCost>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return _ErrorView(onRetry: _refresh);
          }
          final items = snapshot.data ?? const <_PieceCost>[];
          if (items.isEmpty) {
            return const Center(child: Text('لا توجد أنواع قطع نشطة.'));
          }
          final selected = _selected ?? items.first;
          return FutureBuilder<CurrencySettings>(
            future: _currencyFuture,
            builder: (context, currencySnapshot) {
              if (currencySnapshot.connectionState == ConnectionState.waiting) {
                return const Center(child: CircularProgressIndicator());
              }
              if (currencySnapshot.hasError || !currencySnapshot.hasData) {
                return _ErrorView(onRetry: () async {
                  setState(() {
                    _currencyFuture = _currencyService.load();
                  });
                });
              }
              return _CostWorkspace(
                items: items,
                selected: selected,
                formatter: CurrencyFormatter(settings: currencySnapshot.data!),
                saving: _saving,
                onSelected: (value) => setState(() => _selected = value),
                onSave: _save,
              );
            },
          );
        },
      ),
    );
  }
}

class _CostWorkspace extends StatefulWidget {
  const _CostWorkspace({
    required this.items,
    required this.selected,
    required this.formatter,
    required this.saving,
    required this.onSelected,
    required this.onSave,
  });

  final List<_PieceCost> items;
  final _PieceCost selected;
  final CurrencyFormatter formatter;
  final bool saving;
  final ValueChanged<_PieceCost> onSelected;
  final Future<void> Function(_PieceCost value) onSave;

  @override
  State<_CostWorkspace> createState() => _CostWorkspaceState();
}

class _CostWorkspaceState extends State<_CostWorkspace> {
  late final Map<String, TextEditingController> _controllers;

  @override
  void initState() {
    super.initState();
    _controllers = {};
    _syncControllers(widget.selected);
  }

  @override
  void didUpdateWidget(covariant _CostWorkspace oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.selected.productTypeId != widget.selected.productTypeId ||
        oldWidget.selected != widget.selected) {
      _syncControllers(widget.selected);
    }
  }

  void _syncControllers(_PieceCost value) {
    final values = value.editableValues;
    for (final entry in values.entries) {
      final controller = _controllers.putIfAbsent(
        entry.key,
        () => TextEditingController(),
      );
      controller.text = _number(entry.value);
    }
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final values = <String, double>{};
    for (final entry in _controllers.entries) {
      final value = double.tryParse(entry.value.text.trim());
      if (value == null || value < 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('أدخل قيماً رقمية صحيحة وغير سالبة.')),
        );
        return;
      }
      values[entry.key] = value;
    }
    await widget.onSave(widget.selected.withValues(values));
  }

  TextEditingController _controller(String key) => _controllers[key]!;

  @override
  Widget build(BuildContext context) {
    final selected = widget.selected;
    final total = _value('sewingCost') +
        _value('consumablesCost') +
        _value('ironingAndPackagingCost') +
        _value('fixedOperatingCost');
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1180),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _Panel(
                child: DropdownButtonFormField<int>(
                  initialValue: selected.productTypeId,
                  decoration: const InputDecoration(
                    labelText: 'نوع القطعة',
                    prefixIcon: Icon(Icons.checkroom_outlined),
                    border: OutlineInputBorder(),
                  ),
                  items: widget.items
                      .map((item) => DropdownMenuItem<int>(
                            value: item.productTypeId,
                            child: Text('${item.pieceName} (${item.code})'),
                          ))
                      .toList(),
                  onChanged: widget.saving
                      ? null
                      : (id) {
                          final item = widget.items.firstWhere(
                            (entry) => entry.productTypeId == id,
                          );
                          widget.onSelected(item);
                        },
                ),
              ),
              const SizedBox(height: 16),
              LayoutBuilder(
                builder: (context, constraints) {
                  final compact = constraints.maxWidth < 800;
                  final direct = _Panel(
                    title: 'التكاليف التشغيلية',
                    icon: Icons.factory_outlined,
                    child: _fields([
                      _field('sewingCost', 'أجور الخياطة'),
                      _disabledField('أجور القص', 'لا يوجد مصدر رسمي حالياً'),
                      _disabledField('الكهرباء', 'لا يوجد مصدر مباشر للقطعة'),
                      _field('consumablesCost', 'التكاليف التشغيلية الصغيرة'),
                      _field('ironingAndPackagingCost', 'الكي والتغليف'),
                      _field(
                          'fixedOperatingCost', 'التشغيل الثابت الخاص بالقطعة'),
                    ]),
                  );
                  final general = _Panel(
                    title: 'التكاليف الثابتة للمنشأة',
                    icon: Icons.business_outlined,
                    child: _fields([
                      _field('monthlyRent', 'الإيجارات'),
                      _field('monthlySalaries', 'الرواتب'),
                      _field('monthlyElectricity', 'الكهرباء'),
                      _field('monthlyWater', 'المياه'),
                      _field('monthlyInternet', 'الإنترنت'),
                      _field('monthlyDepreciation', 'الإهلاكات'),
                    ]),
                  );
                  return compact
                      ? Column(children: [
                          direct,
                          const SizedBox(height: 16),
                          general
                        ])
                      : Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                              Expanded(child: direct),
                              const SizedBox(width: 16),
                              Expanded(child: general),
                            ]);
                },
              ),
              const SizedBox(height: 16),
              _Panel(
                title: 'ملخص تكاليف القطعة',
                icon: Icons.calculate_outlined,
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        'الإجمالي التشغيلي للقطعة المختارة',
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor: UiPalette.surfaceCard,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                    Text(
                      widget.formatter.amount(total),
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            color: UiPalette.primaryBlue,
                            fontWeight: FontWeight.bold,
                          ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              Align(
                alignment: Alignment.centerLeft,
                child: FilledButton.icon(
                  onPressed: widget.saving ? null : _save,
                  icon: widget.saving
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.save_outlined),
                  label: const Text('حفظ التكاليف'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _fields(List<Widget> fields) => LayoutBuilder(
        builder: (context, constraints) {
          final width = constraints.maxWidth < 480
              ? constraints.maxWidth
              : (constraints.maxWidth - 12) / 2;
          return Wrap(
            spacing: 12,
            runSpacing: 12,
            children: fields
                .map((field) => SizedBox(width: width, child: field))
                .toList(),
          );
        },
      );

  Widget _field(String key, String label) => TextFormField(
        controller: _controller(key),
        enabled: !widget.saving,
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        textInputAction: TextInputAction.next,
        decoration: InputDecoration(
          labelText: label,
          prefixText: '${widget.formatter.symbol} ',
          border: const OutlineInputBorder(),
        ),
        onChanged: (_) => setState(() {}),
      );

  Widget _disabledField(String label, String helper) => TextFormField(
        enabled: false,
        decoration: InputDecoration(
          labelText: label,
          helperText: helper,
          border: const OutlineInputBorder(),
        ),
      );

  double _value(String key) =>
      double.tryParse(_controller(key).text.trim()) ?? 0;

  String _number(double value) => value == value.truncateToDouble()
      ? value.toInt().toString()
      : value.toString();
}

class _Panel extends StatelessWidget {
  const _Panel({required this.child, this.title, this.icon});

  final Widget child;
  final String? title;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (title != null) ...[
            Row(
              textDirection: TextDirection.rtl,
              children: [
                Icon(icon, color: UiPalette.primaryBlue),
                const SizedBox(width: 8),
                Text(
                  title!,
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: UiPalette.surfaceCard,
                    fontSize: 17,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
          ],
          child,
        ],
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.onRetry});

  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_outlined, size: 44),
            const SizedBox(height: 10),
            const Text('تعذر تحميل تكاليف القطع.'),
            const SizedBox(height: 10),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh_rounded),
              label: const Text('إعادة المحاولة'),
            ),
          ],
        ),
      );
}

class _PieceCost {
  const _PieceCost({
    required this.productTypeId,
    required this.code,
    required this.pieceName,
    required this.sewingCost,
    required this.consumablesCost,
    required this.ironingAndPackagingCost,
    required this.fixedOperatingCost,
    required this.monthlyRent,
    required this.monthlySalaries,
    required this.monthlyElectricity,
    required this.monthlyWater,
    required this.monthlyInternet,
    required this.monthlyDepreciation,
  });

  final int productTypeId;
  final String code;
  final String pieceName;
  final double sewingCost;
  final double consumablesCost;
  final double ironingAndPackagingCost;
  final double fixedOperatingCost;
  final double monthlyRent;
  final double monthlySalaries;
  final double monthlyElectricity;
  final double monthlyWater;
  final double monthlyInternet;
  final double monthlyDepreciation;

  factory _PieceCost.empty() => const _PieceCost(
        productTypeId: 0,
        code: '',
        pieceName: '',
        sewingCost: 0,
        consumablesCost: 0,
        ironingAndPackagingCost: 0,
        fixedOperatingCost: 0,
        monthlyRent: 0,
        monthlySalaries: 0,
        monthlyElectricity: 0,
        monthlyWater: 0,
        monthlyInternet: 0,
        monthlyDepreciation: 0,
      );

  factory _PieceCost.fromJson(Map<String, dynamic> json) => _PieceCost(
        productTypeId: json['productTypeId'] as int? ?? 0,
        code: json['code']?.toString() ?? '',
        pieceName: json['pieceName']?.toString() ?? '',
        sewingCost: _numberValue(json['sewingCost']),
        consumablesCost: _numberValue(json['consumablesCost']),
        ironingAndPackagingCost: _numberValue(json['ironingAndPackagingCost']),
        fixedOperatingCost: _numberValue(json['fixedOperatingCost']),
        monthlyRent: _numberValue(json['monthlyRent']),
        monthlySalaries: _numberValue(json['monthlySalaries']),
        monthlyElectricity: _numberValue(json['monthlyElectricity']),
        monthlyWater: _numberValue(json['monthlyWater']),
        monthlyInternet: _numberValue(json['monthlyInternet']),
        monthlyDepreciation: _numberValue(json['monthlyDepreciation']),
      );

  Map<String, double> get editableValues => {
        'sewingCost': sewingCost,
        'consumablesCost': consumablesCost,
        'ironingAndPackagingCost': ironingAndPackagingCost,
        'fixedOperatingCost': fixedOperatingCost,
        'monthlyRent': monthlyRent,
        'monthlySalaries': monthlySalaries,
        'monthlyElectricity': monthlyElectricity,
        'monthlyWater': monthlyWater,
        'monthlyInternet': monthlyInternet,
        'monthlyDepreciation': monthlyDepreciation,
      };

  Map<String, double> toRequest() => editableValues;

  _PieceCost withValues(Map<String, double> values) => _PieceCost(
        productTypeId: productTypeId,
        code: code,
        pieceName: pieceName,
        sewingCost: values['sewingCost'] ?? sewingCost,
        consumablesCost: values['consumablesCost'] ?? consumablesCost,
        ironingAndPackagingCost:
            values['ironingAndPackagingCost'] ?? ironingAndPackagingCost,
        fixedOperatingCost: values['fixedOperatingCost'] ?? fixedOperatingCost,
        monthlyRent: values['monthlyRent'] ?? monthlyRent,
        monthlySalaries: values['monthlySalaries'] ?? monthlySalaries,
        monthlyElectricity: values['monthlyElectricity'] ?? monthlyElectricity,
        monthlyWater: values['monthlyWater'] ?? monthlyWater,
        monthlyInternet: values['monthlyInternet'] ?? monthlyInternet,
        monthlyDepreciation:
            values['monthlyDepreciation'] ?? monthlyDepreciation,
      );
}

double _numberValue(Object? value) => value is num
    ? value.toDouble()
    : double.tryParse(value?.toString() ?? '') ?? 0;
