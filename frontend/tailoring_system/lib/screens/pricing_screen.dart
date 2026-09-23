import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';
import 'settings/piece_cost_management_screen.dart';

class PricingScreen extends StatefulWidget {
  const PricingScreen({super.key});

  @override
  State<PricingScreen> createState() => _PricingScreenState();
}

class _PricingScreenState extends State<PricingScreen> {
  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  final _fabricCode = TextEditingController();
  final _consumption = TextEditingController();
  final _globalProfit = TextEditingController();
  late Future<_PricingData> _dataFuture;
  final _results = <int, _PricingResult>{};
  int _calculationVersion = 0;
  bool _calculating = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _dataFuture = _loadData();
  }

  @override
  void dispose() {
    _fabricCode.dispose();
    _consumption.dispose();
    _globalProfit.dispose();
    super.dispose();
  }

  Future<_PricingData> _loadData() async {
    final responses = await Future.wait([
      http.get(Uri.parse('$_baseUrl/piece-cost-management')),
      http.get(Uri.parse('$_baseUrl/pricing-engine/profit-settings')),
    ]);
    for (final response in responses) {
      if (response.statusCode < 200 || response.statusCode >= 300)
        throw Exception('HTTP ${response.statusCode}');
    }
    final costs = (jsonDecode(responses[0].body) as List)
        .cast<Map<String, dynamic>>()
        .map(_PieceCostSetting.fromJson)
        .toList();
    final profitJson = jsonDecode(responses[1].body) as Map<String, dynamic>;
    final rawProductProfits =
        (profitJson['productTypeProfitPercentages'] as Map?) ?? {};
    final productProfits = <int, double>{};
    for (final entry in rawProductProfits.entries) {
      final id = int.tryParse(entry.key.toString());
      if (id != null && entry.value is num)
        productProfits[id] = (entry.value as num).toDouble();
    }
    final global =
        (profitJson['globalProfitPercentage'] as num?)?.toDouble() ?? 0;
    _globalProfit.text = _editableNumber(global);
    return _PricingData(
        costs: costs, globalProfit: global, productProfits: productProfits);
  }

  Future<void> _refresh() async {
    setState(() {
      _error = null;
      _results.clear();
      _dataFuture = _loadData();
    });
    await _dataFuture;
  }

  Future<void> _saveGlobal() async {
    final value = double.tryParse(_globalProfit.text.trim());
    if (value == null || value < 0) {
      setState(() => _error = 'نسبة الربح العامة يجب أن تكون رقمًا غير سالب.');
      return;
    }
    try {
      await _save('$_baseUrl/pricing-engine/profit-settings/global', value);
      await _refresh();
      await _calculate();
    } catch (exception) {
      if (mounted)
        setState(() =>
            _error = exception.toString().replaceFirst('Exception: ', ''));
    }
  }

  Future<void> _saveProductProfit(_PieceCostSetting item, double value) async {
    await _save(
        '$_baseUrl/pricing-engine/profit-settings/product-type/${item.productTypeId}',
        value);
    await _refresh();
    await _calculate();
  }

  Future<void> _save(String url, double value) async {
    final response = await http.put(Uri.parse(url),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'profitPercentage': value}));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      final body = jsonDecode(response.body);
      throw Exception(
          body is Map ? body['message'] ?? 'تعذر الحفظ.' : 'تعذر الحفظ.');
    }
  }

  Future<void> _calculate() async {
    final fabricCode = _fabricCode.text.trim();
    final consumption = double.tryParse(_consumption.text.trim());
    if (fabricCode.isEmpty || consumption == null || consumption < 0) {
      setState(() => _error = 'أدخل كود القماش والاستهلاك بالبوصة للمحاكاة.');
      return;
    }
    final data = await _dataFuture;
    final version = ++_calculationVersion;
    setState(() {
      _error = null;
      _calculating = true;
      _results.clear();
    });
    try {
      for (final item in data.costs) {
        final response = await http.post(
          Uri.parse('$_baseUrl/pricing-engine/calculate'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode({
            'productTypeId': item.productTypeId,
            'fabricCode': fabricCode,
            'consumption': consumption,
            'consumptionUnit': 'Inch',
            'quantity': 1,
            'pieceProfitPercentage':
                data.productProfits[item.productTypeId] ?? 0,
            'globalProfitPercentage': data.globalProfit,
          }),
        );
        if (version != _calculationVersion || !mounted) return;
        _results[item.productTypeId] =
            response.statusCode >= 200 && response.statusCode < 300
                ? _PricingResult.fromJson(
                    jsonDecode(response.body) as Map<String, dynamic>)
                : _PricingResult.error('تعذر الاتصال بمحرك التسعير.');
        setState(() {});
      }
    } finally {
      if (mounted && version == _calculationVersion)
        setState(() => _calculating = false);
    }
  }

  Future<void> _editProfit(_PricingData data, _PieceCostSetting item) async {
    final value = await showDialog<double>(
        context: context,
        builder: (_) => _ProfitDialog(
            initialValue: data.productProfits[item.productTypeId] ?? 0,
            pieceName: item.pieceName));
    if (value == null || !mounted) return;
    try {
      await _saveProductProfit(item, value);
    } catch (exception) {
      if (mounted)
        setState(() =>
            _error = exception.toString().replaceFirst('Exception: ', ''));
    }
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<_PricingData>(
        future: _dataFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done)
            return const Center(child: CircularProgressIndicator());
          if (snapshot.hasError) return _LoadError(onRetry: _refresh);
          final data = snapshot.data!;
          return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(children: [
                  Expanded(
                      child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                        Text('محرك التسعير المركزي',
                            style: UiPalette.adaptiveTextStyle(context,
                                backgroundColor: UiPalette.screenBackground,
                                fontSize: 22,
                                fontWeight: FontWeight.w800)),
                        const SizedBox(height: 4),
                        Text(
                            'التكاليف التشغيلية للقراءة فقط. المدخلات أدناه محاكاة ولا تنشئ عملية بيع.',
                            style: UiPalette.adaptiveTextStyle(context,
                                backgroundColor: UiPalette.screenBackground,
                                fontSize: 13)),
                      ])),
                  IconButton(
                      tooltip: 'تحديث البيانات',
                      onPressed: _refresh,
                      icon: const Icon(Icons.refresh_rounded)),
                  FilledButton.icon(
                      onPressed: () => AppNavigation.push(
                          context, (_) => const PieceCostManagementScreen()),
                      icon: const Icon(Icons.settings_outlined),
                      label: const Text('إدارة التكاليف')),
                ]),
                const SizedBox(height: 16),
                _SimulationPanel(
                    fabricCode: _fabricCode,
                    consumption: _consumption,
                    globalProfit: _globalProfit,
                    saving: _calculating,
                    onCalculate: _calculate,
                    onSaveGlobal: _saveGlobal),
                if (_error != null)
                  Padding(
                      padding: const EdgeInsets.only(top: 10),
                      child: Text(_error!,
                          style: TextStyle(
                              color: Theme.of(context).colorScheme.error))),
                const SizedBox(height: 14),
                Expanded(
                    child: ListView.separated(
                        itemCount: data.costs.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 10),
                        itemBuilder: (_, index) {
                          final item = data.costs[index];
                          return _PricingCard(
                              item: item,
                              profit:
                                  data.productProfits[item.productTypeId] ?? 0,
                              globalProfit: data.globalProfit,
                              result: _results[item.productTypeId],
                              onEdit: () => _editProfit(data, item));
                        })),
              ]);
        },
      );
}

class _SimulationPanel extends StatelessWidget {
  const _SimulationPanel(
      {required this.fabricCode,
      required this.consumption,
      required this.globalProfit,
      required this.saving,
      required this.onCalculate,
      required this.onSaveGlobal});
  final TextEditingController fabricCode;
  final TextEditingController consumption;
  final TextEditingController globalProfit;
  final bool saving;
  final Future<void> Function() onCalculate;
  final Future<void> Function() onSaveGlobal;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
            color: UiPalette.surfaceCard,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: UiPalette.borderSoft)),
        child: Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.end,
            children: [
              SizedBox(
                  width: 190,
                  child: TextField(
                      controller: globalProfit,
                      keyboardType:
                          const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(
                          labelText: 'نسبة الربح العامة',
                          suffixText: '%',
                          border: OutlineInputBorder(),
                          isDense: true))),
              FilledButton.icon(
                  onPressed: saving ? null : onSaveGlobal,
                  icon: const Icon(Icons.save_outlined),
                  label: const Text('حفظ الربح العام')),
              SizedBox(
                  width: 180,
                  child: TextField(
                      controller: fabricCode,
                      textCapitalization: TextCapitalization.characters,
                      decoration: const InputDecoration(
                          labelText: 'كود القماش للمحاكاة',
                          border: OutlineInputBorder(),
                          isDense: true))),
              SizedBox(
                  width: 150,
                  child: TextField(
                      controller: consumption,
                      keyboardType:
                          const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(
                          labelText: 'الاستهلاك بالبوصة',
                          border: OutlineInputBorder(),
                          isDense: true))),
              FilledButton.icon(
                  onPressed: saving ? null : onCalculate,
                  icon: const Icon(Icons.calculate_outlined),
                  label: const Text('احتساب المحاكاة')),
            ]),
      );
}

class _PricingCard extends StatelessWidget {
  const _PricingCard(
      {required this.item,
      required this.profit,
      required this.globalProfit,
      required this.result,
      required this.onEdit});
  final _PieceCostSetting item;
  final double profit;
  final double globalProfit;
  final _PricingResult? result;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
            color: UiPalette.surfaceCard,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: UiPalette.borderSoft)),
        child:
            Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Row(children: [
            Expanded(
                child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                  Text(item.pieceName,
                      style: UiPalette.adaptiveTextStyle(context,
                          backgroundColor: UiPalette.surfaceCard,
                          fontSize: 17,
                          fontWeight: FontWeight.w800)),
                  Text(
                      '${item.pieceCode}  |  ProductTypeId: ${item.productTypeId}',
                      style: UiPalette.adaptiveTextStyle(context,
                          backgroundColor: UiPalette.surfaceCard, fontSize: 12))
                ])),
            TextButton.icon(
                onPressed: onEdit,
                icon: const Icon(Icons.edit_outlined),
                label: const Text('تعديل التسعير'))
          ]),
          const Divider(),
          Wrap(spacing: 20, runSpacing: 10, children: [
            _Value(label: 'الخياطة', value: item.sewingCost),
            _Value(label: 'الأدوات', value: item.consumablesCost),
            _Value(label: 'الكي والتغليف', value: item.ironingAndPackagingCost),
            _Value(label: 'التشغيل الثابت', value: item.fixedOperatingCost),
            _Value(
                label: 'الإجمالي التشغيلي',
                value: item.totalOperationalCost,
                emphasized: true),
            _Value(
                label: 'اكتمال التكلفة',
                text: item.isComplete ? 'مكتملة' : 'ناقصة'),
            _Value(
                label: 'ربح القطعة', text: '${_numberFormat.format(profit)}%'),
            _Value(
                label: 'الربح العام',
                text: '${_numberFormat.format(globalProfit)}%'),
          ]),
          const SizedBox(height: 10),
          if (result == null)
            const Text('أدخل بيانات المحاكاة واضغط احتساب المحاكاة لعرض السعر.',
                style: TextStyle(color: UiPalette.textSoft))
          else
            _ResultView(result: result!),
        ]),
      );
}

class _ResultView extends StatelessWidget {
  const _ResultView({required this.result});
  final _PricingResult result;

  @override
  Widget build(BuildContext context) {
    if (!result.ready)
      return Text(result.reason ?? 'تعذر إصدار سعر نهائي.',
          style: TextStyle(color: Theme.of(context).colorScheme.error));
    return Wrap(spacing: 20, runSpacing: 10, children: [
      _Value(label: 'تكلفة القماش', value: result.fabricCost),
      _Value(label: 'التكلفة الكاملة', value: result.fullCost),
      _Value(label: 'قيمة ربح القطعة', value: result.pieceProfitValue),
      _Value(label: 'بعد ربح القطعة', value: result.afterPieceProfit),
      _Value(label: 'الربح العام', value: result.globalProfitValue),
      _Value(
          label: 'السعر النهائي المقترح',
          value: result.finalPrice,
          emphasized: true),
    ]);
  }
}

class _ProfitDialog extends StatefulWidget {
  const _ProfitDialog({required this.initialValue, required this.pieceName});
  final double initialValue;
  final String pieceName;
  @override
  State<_ProfitDialog> createState() => _ProfitDialogState();
}

class _ProfitDialogState extends State<_ProfitDialog> {
  late final TextEditingController controller;
  final formKey = GlobalKey<FormState>();
  @override
  void initState() {
    super.initState();
    controller =
        TextEditingController(text: _editableNumber(widget.initialValue));
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
          title: Text('تعديل ربح ${widget.pieceName}'),
          content: Form(
              key: formKey,
              child: TextFormField(
                  controller: controller,
                  autofocus: true,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.done,
                  onFieldSubmitted: (_) => _submit(),
                  decoration: const InputDecoration(
                      labelText: 'نسبة ربح القطعة',
                      suffixText: '%',
                      border: OutlineInputBorder()),
                  validator: (value) {
                    final number = double.tryParse(value?.trim() ?? '');
                    if (number == null) return 'أدخل قيمة رقمية';
                    if (number < 0) return 'لا يمكن أن تكون سالبة';
                    return null;
                  })),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('إلغاء')),
            FilledButton(onPressed: _submit, child: const Text('حفظ'))
          ]);
  void _submit() {
    if (formKey.currentState?.validate() ?? false)
      Navigator.pop(context, double.parse(controller.text.trim()));
  }
}

class _PricingData {
  const _PricingData(
      {required this.costs,
      required this.globalProfit,
      required this.productProfits});
  final List<_PieceCostSetting> costs;
  final double globalProfit;
  final Map<int, double> productProfits;
}

class _PricingResult {
  const _PricingResult(
      {required this.ready,
      this.reason,
      this.fabricCost = 0,
      this.fullCost = 0,
      this.pieceProfitValue = 0,
      this.afterPieceProfit = 0,
      this.globalProfitValue = 0,
      this.finalPrice = 0});
  factory _PricingResult.fromJson(Map<String, dynamic> json) => _PricingResult(
      ready: json['isReady'] == true,
      reason: (json['reasons'] as List?)?.join(' '),
      fabricCost: (json['fabricCostPerPiece'] as num?)?.toDouble() ?? 0,
      fullCost: (json['fullCostPerPiece'] as num?)?.toDouble() ?? 0,
      pieceProfitValue:
          (json['pieceProfitValuePerPiece'] as num?)?.toDouble() ?? 0,
      afterPieceProfit:
          (json['priceAfterPieceProfitPerPiece'] as num?)?.toDouble() ?? 0,
      globalProfitValue:
          (json['globalProfitValuePerPiece'] as num?)?.toDouble() ?? 0,
      finalPrice: (json['finalPricePerPiece'] as num?)?.toDouble() ?? 0);
  factory _PricingResult.error(String reason) =>
      _PricingResult(ready: false, reason: reason);
  final bool ready;
  final String? reason;
  final double fabricCost;
  final double fullCost;
  final double pieceProfitValue;
  final double afterPieceProfit;
  final double globalProfitValue;
  final double finalPrice;
}

class _PieceCostSetting {
  const _PieceCostSetting(
      {required this.productTypeId,
      required this.pieceCode,
      required this.pieceName,
      required this.sewingCost,
      required this.consumablesCost,
      required this.ironingAndPackagingCost,
      required this.fixedOperatingCost,
      required this.totalOperationalCost});
  factory _PieceCostSetting.fromJson(Map<String, dynamic> json) =>
      _PieceCostSetting(
          productTypeId: json['productTypeId'] as int,
          pieceCode: (json['pieceCode'] ?? json['code'])?.toString() ?? '-',
          pieceName: json['pieceName']?.toString() ?? '-',
          sewingCost: (json['sewingCost'] as num?)?.toDouble() ?? 0,
          consumablesCost: (json['consumablesCost'] as num?)?.toDouble() ?? 0,
          ironingAndPackagingCost:
              (json['ironingAndPackagingCost'] as num?)?.toDouble() ?? 0,
          fixedOperatingCost:
              (json['fixedOperatingCost'] as num?)?.toDouble() ?? 0,
          totalOperationalCost:
              (json['totalOperationalCost'] as num?)?.toDouble() ?? 0);
  final int productTypeId;
  final String pieceCode;
  final String pieceName;
  final double sewingCost;
  final double consumablesCost;
  final double ironingAndPackagingCost;
  final double fixedOperatingCost;
  final double totalOperationalCost;
  bool get isComplete =>
      sewingCost > 0 &&
      consumablesCost > 0 &&
      ironingAndPackagingCost > 0 &&
      fixedOperatingCost > 0;
}

class _Value extends StatelessWidget {
  const _Value(
      {required this.label, this.value, this.text, this.emphasized = false});
  final String label;
  final double? value;
  final String? text;
  final bool emphasized;
  @override
  Widget build(BuildContext context) => SizedBox(
      width: 145,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label,
            style: UiPalette.adaptiveTextStyle(context,
                backgroundColor: UiPalette.surfaceCard, fontSize: 12)),
        Text(text ?? _money(value ?? 0),
            style: UiPalette.adaptiveTextStyle(context,
                backgroundColor: UiPalette.surfaceCard,
                fontWeight: emphasized ? FontWeight.w800 : FontWeight.w500))
      ]));
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.onRetry});
  final Future<void> Function() onRetry;
  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Icon(Icons.cloud_off_outlined, size: 44),
        const SizedBox(height: 10),
        const Text('تعذر تحميل بيانات محرك التسعير.'),
        const SizedBox(height: 10),
        FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh_rounded),
            label: const Text('إعادة المحاولة'))
      ]));
}

final _numberFormat = NumberFormat('#,##0.##');
String _money(double value) => '${_numberFormat.format(value)} ر.س';
String _editableNumber(double value) => value == value.truncateToDouble()
    ? value.toInt().toString()
    : value.toString();
