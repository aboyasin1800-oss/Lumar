import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

String _friendlyApiMessage(String raw, String fallback) {
  final text = raw.trim();
  final jsonStart = text.indexOf('{');
  if (jsonStart >= 0) {
    try {
      final decoded = jsonDecode(text.substring(jsonStart));
      if (decoded is Map<String, dynamic>) {
        final message = decoded['message'] ?? decoded['title'];
        if (message is String && message.trim().isNotEmpty) {
          return message.trim();
        }
      }
    } catch (_) {
      // Keep the fallback for non-JSON responses.
    }
  }

  if (text.startsWith('Exception:')) {
    final exceptionMessage = text.substring('Exception:'.length).trim();
    if (exceptionMessage.isNotEmpty) {
      return exceptionMessage;
    }
  }

  return text.isEmpty ? fallback : text;
}

class ConsumptionRulesSettingsScreen extends StatefulWidget {
  const ConsumptionRulesSettingsScreen({super.key});

  @override
  State<ConsumptionRulesSettingsScreen> createState() =>
      _ConsumptionRulesSettingsScreenState();
}

class _ConsumptionRulesSettingsScreenState
    extends State<ConsumptionRulesSettingsScreen> {
  static const String _baseUrl = 'http://127.0.0.1:5093';

  late Future<ConsumptionRulesDashboard> _dashboardFuture;
  int? _expandedProductTypeId;

  @override
  void initState() {
    super.initState();
    _dashboardFuture = _loadDashboard();
  }

  Future<ConsumptionRulesDashboard> _loadDashboard() async {
    final response = await http.get(Uri.parse('$_baseUrl/consumption-rules'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('HTTP ${response.statusCode}: ${response.body}');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('استجابة غير صالحة');
    }

    return ConsumptionRulesDashboard.fromJson(decoded);
  }

  Future<void> _refresh() async {
    final refreshFuture = _loadDashboard();
    setState(() {
      _dashboardFuture = refreshFuture;
    });
    await refreshFuture;
  }

  void _toggleProduct(int productTypeId) {
    setState(() {
      _expandedProductTypeId =
          _expandedProductTypeId == productTypeId ? null : productTypeId;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.textMain,
        title: const Text('مصمم قواعد استهلاك القماش'),
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: FutureBuilder<ConsumptionRulesDashboard>(
        future: _dashboardFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(
              child: SizedBox(
                width: 280,
                child: LinearProgressIndicator(
                  color: UiPalette.primaryBlue,
                  backgroundColor: UiPalette.softBlue,
                ),
              ),
            );
          }

          if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Container(
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: UiPalette.surfaceCard,
                    borderRadius: BorderRadius.circular(18),
                    border: Border.all(color: UiPalette.borderSoft),
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.error_outline_rounded,
                          size: 42, color: UiPalette.primaryBlue),
                      const SizedBox(height: 12),
                      const Text('تعذر تحميل بيانات القواعد',
                          style: TextStyle(
                              color: UiPalette.textMain, fontSize: 18)),
                      const SizedBox(height: 8),
                      Text(snapshot.error.toString(),
                          textAlign: TextAlign.center,
                          style: const TextStyle(color: UiPalette.textSoft)),
                      const SizedBox(height: 14),
                      FilledButton.icon(
                        onPressed: _refresh,
                        icon: const Icon(Icons.restart_alt_rounded),
                        label: const Text('إعادة المحاولة'),
                      ),
                    ],
                  ),
                ),
              ),
            );
          }

          final dashboard = snapshot.data ?? ConsumptionRulesDashboard.empty();
          if (_expandedProductTypeId == null &&
              dashboard.productTypes.isNotEmpty) {
            _expandedProductTypeId = dashboard.productTypes.first.productTypeId;
          }

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _SummaryCard(dashboard: dashboard),
              const SizedBox(height: 12),
              ...dashboard.productTypes.map((productType) {
                final productRules = dashboard.rules
                    .where((rule) =>
                        rule.productTypeId == productType.productTypeId)
                    .toList();
                final productFields = dashboard.measurementFields
                    .where((field) =>
                        field.productTypeId == productType.productTypeId)
                    .toList();
                final isOpen =
                    _expandedProductTypeId == productType.productTypeId;

                return Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Container(
                    decoration: BoxDecoration(
                      color: UiPalette.surfaceCard,
                      borderRadius: BorderRadius.circular(5),
                      border: Border.all(color: UiPalette.borderSoft),
                    ),
                    child: Column(
                      children: [
                        Padding(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 16, vertical: 10),
                          child: Row(
                            textDirection: TextDirection.rtl,
                            children: [
                              Expanded(
                                child: InkWell(
                                  borderRadius: BorderRadius.circular(12),
                                  onTap: () =>
                                      _toggleProduct(productType.productTypeId),
                                  child: Row(
                                    textDirection: TextDirection.rtl,
                                    children: [
                                      Text(
                                        productType.nameAr.isEmpty
                                            ? 'غير محدد'
                                            : productType.nameAr,
                                        style: const TextStyle(
                                            color: UiPalette.textMain,
                                            fontSize: 16,
                                            fontWeight: FontWeight.bold),
                                      ),
                                      const SizedBox(width: 10),
                                      Text(
                                        'عدد القواعد: ${productRules.length}',
                                        style: const TextStyle(
                                            color: UiPalette.textSoft,
                                            fontSize: 12),
                                      ),
                                      const SizedBox(width: 10),
                                      Container(
                                        padding: const EdgeInsets.symmetric(
                                            horizontal: 8, vertical: 5),
                                        decoration: BoxDecoration(
                                          color: productRules.isEmpty
                                              ? const Color(0xFF7B5A00)
                                                  .withValues(alpha: 0.18)
                                              : UiPalette.primaryBlue
                                                  .withValues(alpha: 0.18),
                                          borderRadius:
                                              BorderRadius.circular(999),
                                        ),
                                        child: Text(
                                          productRules.isEmpty
                                              ? 'تحتاج إعداد'
                                              : 'مكتملة',
                                          style: TextStyle(
                                            color: productRules.isEmpty
                                                ? const Color(0xFFFFD166)
                                                : UiPalette.primaryBlue,
                                            fontSize: 11,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ),
                                      const SizedBox(width: 10),
                                      Text(
                                        productType.isActive
                                            ? 'نشط'
                                            : 'غير نشط',
                                        style: TextStyle(
                                          color: productType.isActive
                                              ? UiPalette.primaryBlue
                                              : UiPalette.textSoft,
                                          fontSize: 11,
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                              if (isOpen)
                                IconButton(
                                  tooltip: 'إغلاق',
                                  padding: EdgeInsets.zero,
                                  constraints: const BoxConstraints(
                                      minWidth: 28, minHeight: 28),
                                  onPressed: () =>
                                      _toggleProduct(productType.productTypeId),
                                  icon: const Icon(Icons.close_rounded,
                                      color: UiPalette.textSoft, size: 20),
                                )
                              else
                                Icon(
                                  Icons.keyboard_arrow_down_rounded,
                                  color: UiPalette.textSoft,
                                ),
                            ],
                          ),
                        ),
                        if (isOpen)
                          _ProductDesigner(
                            productType: productType,
                            dashboard: dashboard,
                            productFields: productFields,
                            productRules: productRules,
                            onRefresh: _refresh,
                            onProductSelected: (productTypeId) {
                              if (productTypeId != null) {
                                _toggleProduct(productTypeId);
                              }
                            },
                          ),
                      ],
                    ),
                  ),
                );
              }),
            ],
          );
        },
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.dashboard});

  final ConsumptionRulesDashboard dashboard;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            textDirection: TextDirection.rtl,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Expanded(
                child: Text(
                  'إدارة قواعد استهلاك القماش',
                  style: TextStyle(
                      color: UiPalette.textMain,
                      fontSize: 20,
                      fontWeight: FontWeight.bold),
                ),
              ),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: dashboard.hasIntegrityProblems
                      ? const Color(0xFFFFC857).withValues(alpha: 0.18)
                      : UiPalette.primaryBlue.withValues(alpha: 0.18),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  dashboard.hasIntegrityProblems
                      ? 'توجد ملاحظات'
                      : 'سلامة القواعد جيدة',
                  style: TextStyle(
                    color: dashboard.hasIntegrityProblems
                        ? const Color(0xFFFFC857)
                        : UiPalette.primaryBlue,
                    fontWeight: FontWeight.bold,
                    fontSize: 11,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              _MetricPill(
                  label: 'أنواع القطع',
                  value: '${dashboard.productTypes.length}'),
              _MetricPill(
                  label: 'القياسات',
                  value: '${dashboard.measurementFields.length}'),
              _MetricPill(label: 'القواعد', value: '${dashboard.rules.length}'),
              _MetricPill(
                  label: 'نشطة',
                  value:
                      '${dashboard.rules.where((rule) => rule.status.toLowerCase() == 'active').length}'),
            ],
          ),
        ],
      ),
    );
  }
}

class _MetricPill extends StatelessWidget {
  const _MetricPill({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: UiPalette.softBlue,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        textDirection: TextDirection.rtl,
        children: [
          Text(label,
              style: const TextStyle(color: UiPalette.textSoft, fontSize: 11)),
          const SizedBox(width: 8),
          Text(value,
              style: const TextStyle(
                  color: UiPalette.textMain,
                  fontWeight: FontWeight.bold,
                  fontSize: 12)),
        ],
      ),
    );
  }
}

class _ProductDesigner extends StatefulWidget {
  const _ProductDesigner({
    required this.productType,
    required this.dashboard,
    required this.productFields,
    required this.productRules,
    required this.onRefresh,
    required this.onProductSelected,
  });

  final ConsumptionRuleProductType productType;
  final ConsumptionRulesDashboard dashboard;
  final List<ConsumptionRuleMeasurementField> productFields;
  final List<ConsumptionRuleRecord> productRules;
  final Future<void> Function() onRefresh;
  final ValueChanged<int?> onProductSelected;

  @override
  State<_ProductDesigner> createState() => _ProductDesignerState();
}

class _ProductDesignerState extends State<_ProductDesigner> {
  static const String _baseUrl = 'http://127.0.0.1:5093';

  final TextEditingController _rowCountController = TextEditingController();
  final FocusNode _rowCountFocusNode = FocusNode();
  final List<_RuleDraft> _draftRows = [];

  static int? _safeProductTypeValue(
      int? requestedValue, List<ConsumptionRuleProductType> items) {
    if (items.isEmpty) return null;

    final ids = <int>{};
    for (final item in items) {
      if (ids.add(item.productTypeId)) {
        if (requestedValue != null && item.productTypeId == requestedValue) {
          return requestedValue;
        }
      }
    }

    return items.first.productTypeId;
  }

  @override
  void initState() {
    super.initState();
    _syncRowsFromRules();
  }

  @override
  void didUpdateWidget(covariant _ProductDesigner oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.productRules != widget.productRules) {
      _syncRowsFromRules();
    }
  }

  @override
  void dispose() {
    _rowCountFocusNode.dispose();
    _rowCountController.dispose();
    super.dispose();
  }

  void _syncRowsFromRules() {
    final persistedRules = widget.productRules
        .map((rule) => _RuleDraft.fromRule(rule, widget.productFields,
            sizeClasses: widget.dashboard.sizeClasses))
        .toList();
    final pendingLocalRows =
        _draftRows.where((draft) => !draft.isSaved).toList();
    _draftRows
      ..clear()
      ..addAll(persistedRules)
      ..addAll(pendingLocalRows);
  }

  void _addBlankRows(int count) {
    if (count <= 0) return;
    setState(() {
      final nextIndex = _draftRows.length;
      for (var i = 0; i < count; i++) {
        _draftRows.add(_RuleDraft.blank(
          rowLabel: 'قاعدة ${nextIndex + i + 1}',
          productTypeId: widget.productType.productTypeId,
          productFields: widget.productFields,
        ));
      }
    });
  }

  Future<void> _saveDraft(_RuleDraft draft) async {
    final formula = draft.generatedFormula(widget.productFields);
    final productTypeId = widget.productType.productTypeId;
    final firstField = widget.productFields.firstWhereOrNull(
        (field) => field.measurementFieldId == draft.firstMeasurementId);
    final secondField = widget.productFields.firstWhereOrNull(
        (field) => field.measurementFieldId == draft.secondMeasurementId);
    final conditionalField = widget.productFields.firstWhereOrNull(
        (field) => field.measurementFieldId == draft.conditionalMeasurementId);

    final payload = <String, dynamic>{
      'productTypeId': productTypeId,
      'sizeClassId': draft.sizeClassId,
      'name': draft.name,
      'ruleType': 'Formula',
      'formula': formula,
      'resultUnit': draft.resultUnit,
      'priority': draft.priority,
      'status': draft.status,
      'fabricWidth': draft.fabricWidth,
      'fabricWidthUnit': draft.fabricWidthUnit,
      'firstMeasurementCode': firstField?.code,
      'firstFactor': draft.firstFactor ?? 1,
      'secondMeasurementCode': secondField?.code,
      'secondFactor': draft.secondFactor ?? 0,
      'conditionalMeasurementCode': conditionalField?.code,
      'minimumValue': draft.minimumValue,
      'maximumValue': draft.maximumValue,
      'fixedIncrease': draft.fixedIncrease ?? 0,
    };

    debugPrint(
        '[ConsumptionRules] save isNew=${!draft.isSaved} isDirty=${draft.isDirty} method=${draft.isSaved ? 'PUT' : 'POST'} uri=${draft.isSaved ? '$_baseUrl/consumption-rules/${draft.id}' : '$_baseUrl/consumption-rules'} payload=${jsonEncode(payload)}');

    final response = draft.isSaved
        ? await http.put(
            Uri.parse('$_baseUrl/consumption-rules/${draft.id}'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(payload),
          )
        : await http.post(
            Uri.parse('$_baseUrl/consumption-rules'),
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(payload),
          );

    debugPrint(
        '[ConsumptionRules] response status=${response.statusCode} body=${response.body}');

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception(response.body);
    }

    if (mounted) {
      final decoded = jsonDecode(response.body);
      final persistedId = decoded is Map<String, dynamic>
          ? decoded['consumptionRuleId'] as int?
          : null;
      setState(() {
        final index = _draftRows.indexOf(draft);
        if (index >= 0) {
          _draftRows[index] = draft.copyWith(
            id: persistedId,
            formulaRaw: formula,
            isEditing: false,
          );
        }
      });
      await widget.onRefresh();
    }
  }

  Future<void> _disableDraft(_RuleDraft draft) async {
    if (!draft.isSaved) {
      setState(() {
        _draftRows.remove(draft);
      });
      return;
    }

    final response = await http.put(
      Uri.parse('$_baseUrl/consumption-rules/${draft.id}'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'name': draft.name,
        'formula': draft.formulaRaw,
        'resultUnit': draft.resultUnit,
        'priority': draft.priority,
        'status': 'Inactive',
        'sizeClassId': draft.sizeClassId,
        'fabricWidth': draft.fabricWidth,
        'fabricWidthUnit': draft.fabricWidthUnit,
      }),
    );

    if (response.statusCode >= 200 && response.statusCode < 300 && mounted) {
      await widget.onRefresh();
    }
  }

  Future<void> _saveSingleRuleAsBatch(_RuleDraft row) async {
    final messenger = ScaffoldMessenger.maybeOf(context);
    try {
      final formula = row.generatedFormula(widget.productFields);
      final firstField = widget.productFields.firstWhereOrNull(
          (field) => field.measurementFieldId == row.firstMeasurementId);
      final secondField = widget.productFields.firstWhereOrNull(
          (field) => field.measurementFieldId == row.secondMeasurementId);
      final conditionalField = widget.productFields.firstWhereOrNull(
          (field) => field.measurementFieldId == row.conditionalMeasurementId);

      if (row.name.trim().isEmpty) {
        throw Exception('اسم القاعدة مطلوب.');
      }

      if (row.fabricWidth == null || row.fabricWidth! <= 0) {
        throw Exception('عرض القماش مطلوب.');
      }

      if (firstField == null ||
          row.firstMeasurementId == null ||
          (row.firstFactor ?? 0) <= 0) {
        throw Exception('القياس الأول ومعامله مطلوبان.');
      }

      if (row.resultUnit.trim().isEmpty) {
        throw Exception('وحدة النتيجة مطلوبة.');
      }

      if (formula.trim().isEmpty || formula == '0') {
        throw Exception('صيغة القاعدة غير مكتملة.');
      }

      final rules = [
        {
          'productTypeId': widget.productType.productTypeId,
          'sizeClassId': row.sizeClassId,
          'fabricWidth': row.fabricWidth,
          'fabricWidthUnit': row.fabricWidthUnit,
          'firstMeasurementCode': firstField.code,
          'firstFactor': row.firstFactor ?? 1,
          'secondMeasurementCode': secondField?.code,
          'secondFactor': row.secondFactor ?? 0,
          'conditionalMeasurementCode': conditionalField?.code,
          'minimumValue': row.minimumValue,
          'maximumValue': row.maximumValue,
          'fixedIncrease': row.fixedIncrease ?? 0,
          'name': row.name,
          'ruleType': 'Formula',
          'formula': formula,
          'resultUnit': row.resultUnit,
          'priority': row.priority,
          'status': row.status,
        }
      ];

      final response = await http.post(
        Uri.parse('$_baseUrl/consumption-rules/batch'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'productTypeId': widget.productType.productTypeId,
          'rules': rules,
        }),
      );

      debugPrint(
          '[ConsumptionRules] batch create uri=$_baseUrl/consumption-rules/batch payload=${jsonEncode({
            'productTypeId': widget.productType.productTypeId,
            'rules': rules
          })} status=${response.statusCode} body=${response.body}');

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(
            _friendlyApiMessage(response.body, 'تعذر حفظ القاعدة.'));
      }

      final decoded = jsonDecode(response.body);
      final persistedId = decoded is List && decoded.isNotEmpty
          ? (decoded.first as Map<String, dynamic>)['consumptionRuleId'] as int?
          : null;
      if (mounted) {
        setState(() {
          final index = _draftRows.indexOf(row);
          if (index >= 0) {
            _draftRows[index] = row.copyWith(
              id: persistedId,
              formulaRaw: formula,
              isEditing: false,
            );
          }
        });
      }
      await widget.onRefresh();
      if (!mounted) return;
      messenger?.showSnackBar(
          const SnackBar(content: Text('تم حفظ القاعدة بنجاح.')));
    } catch (error) {
      if (!mounted) return;
      messenger?.showSnackBar(SnackBar(
          content: Text(
              _friendlyApiMessage(error.toString(), 'تعذر حفظ القاعدة.'))));
    }
  }

  Future<void> _saveProductRulesBatch() async {
    final messenger = ScaffoldMessenger.maybeOf(context);
    try {
      final rules = <Map<String, dynamic>>[];
      final invalidReasons = <String>[];

      for (final row in _draftRows) {
        final formula = row.generatedFormula(widget.productFields);
        final firstField = widget.productFields.firstWhereOrNull(
            (field) => field.measurementFieldId == row.firstMeasurementId);
        final secondField = widget.productFields.firstWhereOrNull(
            (field) => field.measurementFieldId == row.secondMeasurementId);
        final conditionalField = widget.productFields.firstWhereOrNull(
            (field) =>
                field.measurementFieldId == row.conditionalMeasurementId);

        if (row.name.trim().isEmpty) {
          invalidReasons.add('قاعدة جديدة ناقصة: الاسم مطلوب.');
          continue;
        }

        if (row.fabricWidth == null || row.fabricWidth! <= 0) {
          invalidReasons.add('قاعدة ${row.name} ناقصة: عرض القماش مطلوب.');
          continue;
        }

        if (firstField == null ||
            row.firstMeasurementId == null ||
            (row.firstFactor ?? 0) <= 0) {
          invalidReasons
              .add('قاعدة ${row.name} ناقصة: القياس الأول ومعامله مطلوبان.');
          continue;
        }

        if (row.resultUnit.trim().isEmpty) {
          invalidReasons.add('قاعدة ${row.name} ناقصة: وحدة النتيجة مطلوبة.');
          continue;
        }

        if (formula.trim().isEmpty || formula == '0') {
          invalidReasons.add('قاعدة ${row.name} ناقصة: الصيغة غير مكتملة.');
          continue;
        }

        rules.add({
          'productTypeId': widget.productType.productTypeId,
          'sizeClassId': row.sizeClassId,
          'fabricWidth': row.fabricWidth,
          'fabricWidthUnit': row.fabricWidthUnit,
          'firstMeasurementCode': firstField.code,
          'firstFactor': row.firstFactor ?? 1,
          'secondMeasurementCode': secondField?.code,
          'secondFactor': row.secondFactor ?? 0,
          'conditionalMeasurementCode': conditionalField?.code,
          'minimumValue': row.minimumValue,
          'maximumValue': row.maximumValue,
          'fixedIncrease': row.fixedIncrease ?? 0,
          'name': row.name,
          'ruleType': 'Formula',
          'formula': formula,
          'resultUnit': row.resultUnit,
          'priority': row.priority,
          'status': row.status,
        });
      }

      if (rules.isEmpty) {
        throw Exception(invalidReasons.isNotEmpty
            ? invalidReasons.first
            : 'لا توجد قواعد لإرسالها للحفظ.');
      }

      final response = await http.post(
        Uri.parse('$_baseUrl/consumption-rules/batch'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'productTypeId': widget.productType.productTypeId,
          'rules': rules,
        }),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(
            _friendlyApiMessage(response.body, 'تعذر حفظ قواعد القطعة.'));
      }

      await widget.onRefresh();
      if (!mounted) return;
      messenger?.showSnackBar(
          const SnackBar(content: Text('تم حفظ قواعد القطعة بنجاح.')));
    } catch (error) {
      if (!mounted) return;
      messenger?.showSnackBar(SnackBar(
          content: Text(_friendlyApiMessage(
              error.toString(), 'تعذر حفظ قواعد القطعة.'))));
    }
  }

  @override
  Widget build(BuildContext context) {
    final visibleFields = widget.productFields;
    final hasFields = visibleFields.isNotEmpty;
    final uniqueProductTypes = <ConsumptionRuleProductType>[];
    final seenProductTypes = <int>{};
    for (final item in widget.dashboard.productTypes) {
      if (seenProductTypes.add(item.productTypeId)) {
        uniqueProductTypes.add(item);
      }
    }

    final safeSelectedProductType = _safeProductTypeValue(
        widget.productType.productTypeId, uniqueProductTypes);

    return Container(
      padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SizedBox(height: 10),
          Row(
            textDirection: TextDirection.rtl,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      widget.productType.nameAr.isEmpty
                          ? 'غير محدد'
                          : widget.productType.nameAr,
                      style: const TextStyle(
                          color: UiPalette.textMain,
                          fontSize: 18,
                          fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'عدد القياسات: ${visibleFields.length}',
                      style: const TextStyle(
                          color: UiPalette.textSoft, fontSize: 12),
                    ),
                  ],
                ),
              ),
              if (uniqueProductTypes.isNotEmpty)
                SizedBox(
                  width: 250,
                  child: DropdownButtonFormField<int>(
                    initialValue: safeSelectedProductType,
                    decoration: InputDecoration(
                      labelText: 'نوع القطعة',
                      filled: true,
                      fillColor: UiPalette.surfaceCard,
                      border: const OutlineInputBorder(
                        borderSide: BorderSide(color: UiPalette.borderSoft),
                      ),
                      enabledBorder: const OutlineInputBorder(
                        borderSide: BorderSide(color: UiPalette.borderSoft),
                      ),
                      focusedBorder: const OutlineInputBorder(
                        borderSide: BorderSide(color: UiPalette.primaryBlue),
                      ),
                      errorBorder: const OutlineInputBorder(
                        borderSide: BorderSide(color: UiPalette.borderSoft),
                      ),
                      focusedErrorBorder: const OutlineInputBorder(
                        borderSide: BorderSide(color: UiPalette.borderSoft),
                      ),
                      labelStyle: const TextStyle(color: UiPalette.textSoft),
                    ),
                    items: uniqueProductTypes
                        .map((item) => DropdownMenuItem<int>(
                              value: item.productTypeId,
                              child: Text(item.nameAr.isEmpty
                                  ? 'غير محدد'
                                  : item.nameAr),
                            ))
                        .toList(),
                    onChanged: (value) => widget.onProductSelected(value),
                  ),
                ),
              const SizedBox(width: 10),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: UiPalette.softBlue,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  'عدد القواعد: ${widget.productRules.length}',
                  style: const TextStyle(
                      color: UiPalette.textMain,
                      fontSize: 12,
                      fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (!hasFields)
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: UiPalette.softBlue,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: UiPalette.borderSoft),
              ),
              child: const Text('لا يوجد ملف قياسات مرتبط بهذه القطعة',
                  style: TextStyle(color: UiPalette.textSoft)),
            ),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: UiPalette.softBlue,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: UiPalette.borderSoft),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  textDirection: TextDirection.rtl,
                  children: [
                    SizedBox(
                      width: 120,
                      child: TextField(
                        controller: _rowCountController,
                        focusNode: _rowCountFocusNode,
                        keyboardType: TextInputType.number,
                        textInputAction: TextInputAction.done,
                        textAlign: TextAlign.right,
                        onSubmitted: (_) {
                          final count =
                              int.tryParse(_rowCountController.text.trim()) ??
                                  0;
                          if (count > 0) {
                            _addBlankRows(count);
                            _rowCountController.clear();
                            FocusScope.of(context).unfocus();
                          }
                        },
                        decoration: const InputDecoration(
                          labelText: 'عدد القواعد',
                          border: OutlineInputBorder(),
                          filled: true,
                          fillColor: UiPalette.surfaceCard,
                          labelStyle: TextStyle(color: UiPalette.textSoft),
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.start,
                        children: [
                          FilledButton.icon(
                            onPressed: () {
                              final count = int.tryParse(
                                      _rowCountController.text.trim()) ??
                                  0;
                              if (count > 0) {
                                _addBlankRows(count);
                                _rowCountController.clear();
                              }
                            },
                            icon: const Icon(Icons.add_rounded),
                            label: const Text('تطبيق العدد'),
                          ),
                          const SizedBox(width: 10),
                          FilledButton.icon(
                            onPressed: () => _addBlankRows(1),
                            icon: const Icon(Icons.note_add_rounded),
                            label: const Text('إضافة قاعدة'),
                          ),
                          const SizedBox(width: 10),
                          FilledButton.icon(
                            onPressed: _saveProductRulesBatch,
                            icon: const Icon(Icons.save_rounded),
                            label: const Text('حفظ قواعد القطعة'),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  alignment: WrapAlignment.end,
                  children: visibleFields
                      .map((field) => Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 10, vertical: 6),
                            decoration: BoxDecoration(
                              color: UiPalette.surfaceCard,
                              borderRadius: BorderRadius.circular(999),
                            ),
                            child: Text(
                              field.nameAr.isEmpty ? field.code : field.nameAr,
                              style: const TextStyle(
                                  color: UiPalette.textMain, fontSize: 12),
                            ),
                          ))
                      .toList(),
                ),
                if (!hasFields)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text(
                        'لا توجد قياسات مرتبطة، ويمكن إضافة قاعدة وتعديلها مع تعطيل الحقول المرتبطة بالقياسات فقط.',
                        style:
                            TextStyle(color: UiPalette.textSoft, fontSize: 12)),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          ..._draftRows.map((draft) => _RuleRow(
                draft: draft,
                productFields: visibleFields,
                sizeClasses: widget.dashboard.sizeClasses
                    .where((item) =>
                        item.productTypeId == widget.productType.productTypeId)
                    .toList(),
                onChanged: (updated) {
                  setState(() {
                    final index = _draftRows.indexOf(draft);
                    if (index >= 0) {
                      _draftRows[index] = updated;
                    }
                  });
                },
                onToggleEdit: () {
                  setState(() {
                    final index = _draftRows.indexOf(draft);
                    if (index >= 0) {
                      _draftRows[index] = _draftRows[index]
                          .copyWith(isEditing: !_draftRows[index].isEditing);
                    }
                  });
                },
                onSave: () async {
                  try {
                    if (draft.isSaved) {
                      await _saveDraft(draft);
                      return;
                    }
                    await _saveSingleRuleAsBatch(draft);
                  } catch (error) {
                    if (!mounted) return;
                    ScaffoldMessenger.maybeOf(context)?.showSnackBar(
                      SnackBar(
                        content: Text(_friendlyApiMessage(
                            error.toString(), 'تعذر حفظ التعديل.')),
                      ),
                    );
                  }
                },
                onCancel: () {
                  setState(() {
                    if (!draft.isSaved) {
                      _draftRows.remove(draft);
                    } else {
                      final index = _draftRows.indexOf(draft);
                      if (index >= 0) {
                        final original = widget.productRules.firstWhere(
                          (rule) => rule.consumptionRuleId == draft.id,
                          orElse: () => widget.productRules.first,
                        );
                        _draftRows[index] =
                            _RuleDraft.fromRule(original, widget.productFields)
                                .copyWith(isEditing: false);
                      }
                    }
                  });
                },
                onDisable: () async {
                  final messenger = ScaffoldMessenger.maybeOf(context);
                  try {
                    await _disableDraft(draft);
                  } catch (error) {
                    if (!mounted) return;
                    messenger?.showSnackBar(
                      SnackBar(content: Text('تعذر تعطيل القاعدة: $error')),
                    );
                  }
                },
              )),
        ],
      ),
    );
  }
}

String _uiText(String? value) {
  final text = (value ?? '').trim();
  if (text.isEmpty) {
    return '';
  }

  final translations = <String, String>{
    'KOT LARGE': 'كوت كبير',
    'KOT HUGE': 'كوت ضخم',
    'KOT MEDIUM': 'كوت متوسط',
    'KOT SMALL': 'كوت صغير',
    'KOT': 'كوت',
    'Active': 'نشطة',
    'Inactive': 'غير نشطة',
    'Small': 'صغير',
    'Medium': 'متوسط',
    'Large': 'كبير',
    'XL': 'إكس إل',
    'Extra Large': 'كبير جدًا',
    'No measurement': 'لا يوجد',
    'Not set': 'غير محدد',
    'kot_length': 'طول الكوت',
    'sleeve_length': 'طول الكم',
    'neck_length': 'طول الرقبة',
    'chest': 'الصدر',
    'waist': 'الخصر',
    'hip': 'الورك',
    'shoulder': 'الكتف',
    'arm_length': 'طول الذراع',
    'length': 'الطول',
    'width': 'العرض',
    'height': 'الارتفاع',
    'cm': 'سم',
    'inch': 'بوصة',
    'inches': 'بوصة',
    'Formula': 'صيغة',
    'Rule': 'قاعدة',
    'Measurement': 'قياس',
    'Status': 'الحالة',
    'Category': 'الفئة',
    'Result Unit': 'وحدة النتيجة',
    '+': ' + ',
  };

  var translated = text;
  for (final entry in translations.entries) {
    translated = translated.replaceAll(entry.key, entry.value);
  }

  if (translated == text && text.contains('_')) {
    translated = text.replaceAll('_', ' ');
  }

  return translated.trim();
}

class _RuleRow extends StatelessWidget {
  const _RuleRow({
    required this.draft,
    required this.productFields,
    required this.sizeClasses,
    required this.onChanged,
    required this.onToggleEdit,
    required this.onSave,
    required this.onCancel,
    required this.onDisable,
  });

  final _RuleDraft draft;
  final List<ConsumptionRuleMeasurementField> productFields;
  final List<ConsumptionRuleSizeClass> sizeClasses;
  final ValueChanged<_RuleDraft> onChanged;
  final VoidCallback onToggleEdit;
  final VoidCallback onSave;
  final VoidCallback onCancel;
  final VoidCallback onDisable;

  static String? _safeStringValue(
      String? rawValue, List<String> allowedValues) {
    if (rawValue == null) return null;
    final trimmed = rawValue.trim();
    if (trimmed.isEmpty) return null;
    for (final item in allowedValues) {
      if (item == trimmed) {
        return item;
      }
    }
    return null;
  }

  static String _normalizeResultUnit(String? rawValue) {
    final value = (rawValue ?? '').trim();
    if (value.isEmpty) return 'بوصة';
    final lower = value.toLowerCase();
    switch (lower) {
      case 'inch':
      case 'inches':
      case 'in':
        return 'بوصة';
      case 'cm':
      case 'centimeter':
      case 'centimeters':
      case 'centimetre':
      case 'centimetres':
        return 'سم';
      case 'بوصة':
      case 'سم':
        return value;
      default:
        return 'بوصة';
    }
  }

  static String _uiText(String? value) {
    final text = (value ?? '').trim();
    if (text.isEmpty) {
      return '';
    }

    final translations = <String, String>{
      'KOT LARGE': 'كوت كبير',
      'KOT HUGE': 'كوت ضخم',
      'KOT MEDIUM': 'كوت متوسط',
      'KOT SMALL': 'كوت صغير',
      'KOT': 'كوت',
      'Active': 'نشطة',
      'Inactive': 'غير نشطة',
      'Small': 'صغير',
      'Medium': 'متوسط',
      'Large': 'كبير',
      'XL': 'إكس إل',
      'Extra Large': 'كبير جدًا',
      'No measurement': 'لا يوجد',
      'Not set': 'غير محدد',
      'kot_length': 'طول الكوت',
      'sleeve_length': 'طول الكم',
      'neck_length': 'طول الرقبة',
      'chest': 'الصدر',
      'waist': 'الخصر',
      'hip': 'الورك',
      'shoulder': 'الكتف',
      'arm_length': 'طول الذراع',
      'length': 'الطول',
      'width': 'العرض',
      'height': 'الارتفاع',
      'cm': 'سم',
      'inch': 'بوصة',
      'inches': 'بوصة',
      'Formula': 'صيغة',
      'Rule': 'قاعدة',
      'Measurement': 'قياس',
      'Status': 'الحالة',
      'Category': 'الفئة',
      'Result Unit': 'وحدة النتيجة',
      '+': ' + ',
    };

    var translated = text;
    for (final entry in translations.entries) {
      translated = translated.replaceAll(entry.key, entry.value);
    }

    if (translated == text && text.contains('_')) {
      translated = text.replaceAll('_', ' ');
    }

    return translated.trim();
  }

  static String _normalizeStatusValue(String? rawValue) {
    final value = (rawValue ?? '').trim();
    if (value.isEmpty) return 'Active';
    final lower = value.toLowerCase();
    switch (lower) {
      case 'نشطة':
      case 'active':
        return 'Active';
      case 'غير نشطة':
      case 'inactive':
        return 'Inactive';
      default:
        return value;
    }
  }

  @override
  Widget build(BuildContext context) {
    final formulaPreview = draft.generatedFormula(productFields);
    final label =
        draft.name.trim().isEmpty ? 'قاعدة جديدة' : _uiText(draft.name);
    final canEdit = draft.isEditing || !draft.isSaved;
    final uniqueFields = <ConsumptionRuleMeasurementField>[];
    final seenFieldIds = <int>{};
    for (final field in productFields) {
      if (seenFieldIds.add(field.measurementFieldId)) {
        uniqueFields.add(field);
      }
    }

    final safeFirstMeasurementId = productFields.isEmpty
        ? null
        : (uniqueFields.any(
                (field) => field.measurementFieldId == draft.firstMeasurementId)
            ? draft.firstMeasurementId
            : uniqueFields.first.measurementFieldId);
    final safeSecondMeasurementId = productFields.isEmpty
        ? null
        : (uniqueFields.any((field) =>
                field.measurementFieldId == draft.secondMeasurementId)
            ? draft.secondMeasurementId
            : null);
    final safeConditionalMeasurementId = productFields.isEmpty
        ? null
        : (uniqueFields.any((field) =>
                field.measurementFieldId == draft.conditionalMeasurementId)
            ? draft.conditionalMeasurementId
            : null);
    final categoryItems = const ['صغير', 'متوسط', 'كبير', 'ضخم'];
    final selectedCategory =
        categoryItems.contains(draft.name) ? draft.name : null;

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            textDirection: TextDirection.rtl,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(label,
                    style: const TextStyle(
                        color: UiPalette.textMain,
                        fontWeight: FontWeight.bold)),
              ),
              Row(
                children: [
                  if (draft.isSaved)
                    TextButton.icon(
                      onPressed: draft.isEditing ? onSave : onToggleEdit,
                      icon: Icon(
                          draft.isEditing
                              ? Icons.save_alt_rounded
                              : Icons.edit_rounded,
                          size: 18),
                      label: Text(
                          draft.isEditing ? 'حفظ التعديل' : 'تعديل القاعدة'),
                    )
                  else
                    TextButton.icon(
                      onPressed: onSave,
                      icon: const Icon(Icons.save_rounded, size: 18),
                      label: const Text('حفظ'),
                    ),
                  if (draft.isEditing)
                    TextButton.icon(
                      onPressed: onCancel,
                      icon: const Icon(Icons.close_rounded, size: 18),
                      label: const Text('إلغاء'),
                    ),
                  TextButton.icon(
                      onPressed: onDisable,
                      icon: const Icon(Icons.block_rounded, size: 18),
                      label: const Text('تعطيل')),
                ],
              ),
            ],
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              SizedBox(
                width: 200,
                child: DropdownButtonFormField<String>(
                  value: selectedCategory,
                  decoration: InputDecoration(
                    labelText: categoryItems.contains(draft.name)
                        ? 'فئة القاعدة'
                        : 'اسم القاعدة المحفوظ',
                    border: const OutlineInputBorder(),
                    hintText:
                        categoryItems.contains(draft.name) ? null : draft.name,
                  ),
                  items: categoryItems
                      .map((item) => DropdownMenuItem<String>(
                          value: item, child: Text(item)))
                      .toList(),
                  onChanged: canEdit
                      ? (value) =>
                          onChanged(draft.copyWith(name: value ?? draft.name))
                      : null,
                ),
              ),
              SizedBox(
                width: 150,
                child: TextFormField(
                  initialValue: draft.fabricWidth == null
                      ? ''
                      : draft.fabricWidth!
                          .toStringAsFixed(draft.fabricWidth! % 1 == 0 ? 0 : 2),
                  textAlign: TextAlign.right,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                  decoration: const InputDecoration(
                      labelText: 'عرض القماش', border: OutlineInputBorder()),
                  enabled: canEdit,
                  onChanged: canEdit
                      ? (value) => onChanged(
                          draft.copyWith(fabricWidth: double.tryParse(value)))
                      : null,
                ),
              ),
              IgnorePointer(
                ignoring: productFields.isEmpty,
                child: SizedBox(
                  width: 180,
                  child: DropdownButtonFormField<int>(
                    initialValue: safeFirstMeasurementId,
                    decoration: const InputDecoration(
                        labelText: 'القياس الأول',
                        border: OutlineInputBorder()),
                    items: productFields
                        .map((field) => DropdownMenuItem<int>(
                            value: field.measurementFieldId,
                            child: Text(field.nameAr.isEmpty
                                ? field.code
                                : field.nameAr)))
                        .toList(),
                    onChanged: canEdit && productFields.isNotEmpty
                        ? (value) =>
                            onChanged(draft.copyWith(firstMeasurementId: value))
                        : null,
                  ),
                ),
              ),
              SizedBox(
                width: 140,
                child: TextFormField(
                  initialValue: draft.firstFactor == null
                      ? ''
                      : draft.firstFactor.toString(),
                  textAlign: TextAlign.right,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                  decoration: const InputDecoration(
                      labelText: 'المعامل الأول', border: OutlineInputBorder()),
                  enabled: canEdit,
                  onChanged: canEdit
                      ? (value) => onChanged(
                          draft.copyWith(firstFactor: double.tryParse(value)))
                      : null,
                ),
              ),
              IgnorePointer(
                ignoring: productFields.isEmpty,
                child: SizedBox(
                  width: 180,
                  child: DropdownButtonFormField<int?>(
                    initialValue: safeSecondMeasurementId,
                    decoration: const InputDecoration(
                        labelText: 'القياس الثاني',
                        border: OutlineInputBorder()),
                    items: [
                      const DropdownMenuItem<int?>(
                          value: null, child: Text('لا يوجد')),
                      ...productFields.map((field) => DropdownMenuItem<int?>(
                          value: field.measurementFieldId,
                          child: Text(field.nameAr.isEmpty
                              ? field.code
                              : field.nameAr))),
                    ],
                    onChanged: canEdit && productFields.isNotEmpty
                        ? (value) => onChanged(draft.copyWith(
                              secondMeasurementId: value,
                              clearSecondMeasurementId: value == null,
                            ))
                        : null,
                  ),
                ),
              ),
              SizedBox(
                width: 140,
                child: TextFormField(
                  initialValue: draft.secondFactor == null
                      ? ''
                      : draft.secondFactor.toString(),
                  textAlign: TextAlign.right,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                  decoration: const InputDecoration(
                      labelText: 'المعامل الثاني',
                      border: OutlineInputBorder()),
                  enabled: canEdit && draft.secondMeasurementId != null,
                  onChanged: canEdit && draft.secondMeasurementId != null
                      ? (value) => onChanged(draft.copyWith(
                            secondFactor: double.tryParse(value),
                            clearSecondFactor: value.trim().isEmpty,
                          ))
                      : null,
                ),
              ),
              IgnorePointer(
                ignoring: productFields.isEmpty,
                child: SizedBox(
                  width: 180,
                  child: DropdownButtonFormField<int?>(
                    initialValue: safeConditionalMeasurementId,
                    decoration: const InputDecoration(
                        labelText: 'القياس الشرطي',
                        border: OutlineInputBorder()),
                    items: [
                      const DropdownMenuItem<int?>(
                          value: null, child: Text('غير محدد')),
                      ...productFields.map((field) => DropdownMenuItem<int?>(
                          value: field.measurementFieldId,
                          child: Text(field.nameAr.isEmpty
                              ? field.code
                              : field.nameAr))),
                    ],
                    onChanged: canEdit && productFields.isNotEmpty
                        ? (value) => onChanged(draft.copyWith(
                              conditionalMeasurementId: value,
                              clearConditionalMeasurementId: value == null,
                            ))
                        : null,
                  ),
                ),
              ),
              SizedBox(
                width: 120,
                child: TextFormField(
                  initialValue: draft.minimumValue == null
                      ? ''
                      : draft.minimumValue.toString(),
                  textAlign: TextAlign.right,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                  decoration: const InputDecoration(
                      labelText: 'من', border: OutlineInputBorder()),
                  enabled: canEdit,
                  onChanged: canEdit
                      ? (value) => onChanged(draft.copyWith(
                            minimumValue: double.tryParse(value),
                            clearMinimumValue: value.trim().isEmpty,
                          ))
                      : null,
                ),
              ),
              SizedBox(
                width: 120,
                child: TextFormField(
                  initialValue: draft.maximumValue == null
                      ? ''
                      : draft.maximumValue.toString(),
                  textAlign: TextAlign.right,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                  decoration: const InputDecoration(
                      labelText: 'إلى', border: OutlineInputBorder()),
                  enabled: canEdit,
                  onChanged: canEdit
                      ? (value) => onChanged(draft.copyWith(
                            maximumValue: double.tryParse(value),
                            clearMaximumValue: value.trim().isEmpty,
                          ))
                      : null,
                ),
              ),
              SizedBox(
                width: 140,
                child: TextFormField(
                  initialValue: draft.fixedIncrease == null
                      ? ''
                      : draft.fixedIncrease.toString(),
                  textAlign: TextAlign.right,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                  decoration: const InputDecoration(
                      labelText: 'الزيادة الثابتة',
                      border: OutlineInputBorder()),
                  enabled: canEdit,
                  onChanged: canEdit
                      ? (value) => onChanged(draft.copyWith(
                            fixedIncrease: double.tryParse(value),
                            clearFixedIncrease: value.trim().isEmpty,
                          ))
                      : null,
                ),
              ),
              SizedBox(
                width: 140,
                child: DropdownButtonFormField<String>(
                  initialValue: _safeStringValue(
                      _normalizeResultUnit(draft.resultUnit),
                      const ['بوصة', 'سم']),
                  decoration: const InputDecoration(
                      labelText: 'وحدة النتيجة', border: OutlineInputBorder()),
                  items: const [
                    DropdownMenuItem<String>(
                        value: 'بوصة', child: Text('بوصة')),
                    DropdownMenuItem<String>(value: 'سم', child: Text('سم')),
                  ],
                  onChanged: canEdit
                      ? (value) => onChanged(draft.copyWith(
                          resultUnit: _normalizeResultUnit(value)))
                      : null,
                ),
              ),
              SizedBox(
                width: 160,
                child: DropdownButtonFormField<String>(
                  initialValue: _safeStringValue(
                      _normalizeStatusValue(draft.status),
                      const ['Active', 'Inactive']),
                  decoration: const InputDecoration(
                      labelText: 'حالة القاعدة', border: OutlineInputBorder()),
                  items: const [
                    DropdownMenuItem<String>(
                        value: 'Active', child: Text('نشطة')),
                    DropdownMenuItem<String>(
                        value: 'Inactive', child: Text('غير نشطة')),
                  ],
                  onChanged: canEdit
                      ? (value) => onChanged(
                          draft.copyWith(status: _normalizeStatusValue(value)))
                      : null,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          const Align(
            alignment: Alignment.centerRight,
            child: Text(
              'قيمة «من» مشمولة، وقيمة «إلى» غير مشمولة. اترك «إلى» فارغة للفئة الأخيرة المفتوحة.',
              style: TextStyle(color: UiPalette.textSoft, fontSize: 12),
            ),
          ),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: UiPalette.softBlue,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Text(
              draft.isEditing
                  ? 'المعادلة الحالية = ${_uiText(formulaPreview)}'
                  : draft.formulaRaw.trim().isNotEmpty
                      ? 'المعادلة المحفوظة = ${_uiText(draft.formulaRaw.trim())}'
                      : 'الاستهلاك = ${_uiText(draft.previewText(productFields))}',
              style: const TextStyle(color: UiPalette.textMain),
            ),
          ),
          if (draft.needsManualReview) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: const Color(0xFF7B5A00).withValues(alpha: 0.16),
                borderRadius: BorderRadius.circular(10),
              ),
              child: const Text('تحتاج مراجعة يدوية',
                  style: TextStyle(color: Color(0xFFFFD166))),
            ),
          ],
          if (draft.formulaRaw.trim().isNotEmpty) ...[
            const SizedBox(height: 8),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('القيمة المحفوظة من قاعدة البيانات',
                      style: TextStyle(
                          color: UiPalette.textMain,
                          fontWeight: FontWeight.bold)),
                  const SizedBox(height: 6),
                  Text(
                    draft.formulaRaw.trim(),
                    style: const TextStyle(color: UiPalette.textSoft),
                  ),
                  if (!categoryItems.contains(draft.name)) ...[
                    const SizedBox(height: 8),
                    const Text(
                      'قاعدة محفوظة لا يمكن تحليلها تلقائياً; يتم عرض القيمة الأصلية كما هي من قاعدة البيانات.',
                      style: TextStyle(color: Color(0xFFFFD166)),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _RuleDraft {
  _RuleDraft({
    required this.id,
    required this.productTypeId,
    required this.name,
    required this.fabricWidth,
    required this.fabricWidthUnit,
    required this.sizeClassId,
    required this.minimumValue,
    required this.maximumValue,
    required this.firstMeasurementId,
    required this.firstFactor,
    required this.secondMeasurementId,
    required this.secondFactor,
    required this.conditionalMeasurementId,
    required this.fixedIncrease,
    required this.resultUnit,
    required this.priority,
    required this.status,
    required this.isActive,
    required this.isEditing,
    required this.needsManualReview,
    required this.formulaRaw,
  });

  final int? id;
  final int productTypeId;
  final String name;
  final double? fabricWidth;
  final String fabricWidthUnit;
  final int? sizeClassId;
  final double? minimumValue;
  final double? maximumValue;
  final int? firstMeasurementId;
  final double? firstFactor;
  final int? secondMeasurementId;
  final double? secondFactor;
  final int? conditionalMeasurementId;
  final double? fixedIncrease;
  final String resultUnit;
  final int priority;
  final String status;
  final bool isActive;
  final bool isEditing;
  final bool needsManualReview;
  final String formulaRaw;

  bool get isSaved => id != null;

  _RuleDraft copyWith({
    int? id,
    int? productTypeId,
    String? name,
    double? fabricWidth,
    String? fabricWidthUnit,
    int? sizeClassId,
    double? minimumValue,
    double? maximumValue,
    int? firstMeasurementId,
    double? firstFactor,
    int? secondMeasurementId,
    double? secondFactor,
    int? conditionalMeasurementId,
    double? fixedIncrease,
    String? resultUnit,
    int? priority,
    String? status,
    bool? isActive,
    bool? isEditing,
    bool? needsManualReview,
    String? formulaRaw,
    bool clearMinimumValue = false,
    bool clearMaximumValue = false,
    bool clearSecondMeasurementId = false,
    bool clearSecondFactor = false,
    bool clearConditionalMeasurementId = false,
    bool clearFixedIncrease = false,
  }) {
    return _RuleDraft(
      id: id ?? this.id,
      productTypeId: productTypeId ?? this.productTypeId,
      name: name ?? this.name,
      fabricWidth: fabricWidth ?? this.fabricWidth,
      fabricWidthUnit: fabricWidthUnit ?? this.fabricWidthUnit,
      sizeClassId: sizeClassId ?? this.sizeClassId,
      minimumValue:
          clearMinimumValue ? null : (minimumValue ?? this.minimumValue),
      maximumValue:
          clearMaximumValue ? null : (maximumValue ?? this.maximumValue),
      firstMeasurementId: firstMeasurementId ?? this.firstMeasurementId,
      firstFactor: firstFactor ?? this.firstFactor,
      secondMeasurementId: clearSecondMeasurementId
          ? null
          : (secondMeasurementId ?? this.secondMeasurementId),
      secondFactor:
          clearSecondFactor ? null : (secondFactor ?? this.secondFactor),
      conditionalMeasurementId: clearConditionalMeasurementId
          ? null
          : (conditionalMeasurementId ?? this.conditionalMeasurementId),
      fixedIncrease:
          clearFixedIncrease ? null : (fixedIncrease ?? this.fixedIncrease),
      resultUnit: resultUnit ?? this.resultUnit,
      priority: priority ?? this.priority,
      status: status ?? this.status,
      isActive: isActive ?? this.isActive,
      isEditing: isEditing ?? this.isEditing,
      needsManualReview: needsManualReview ?? this.needsManualReview,
      formulaRaw: formulaRaw ?? this.formulaRaw,
    );
  }

  factory _RuleDraft.blank({
    required String rowLabel,
    required int productTypeId,
    required List<ConsumptionRuleMeasurementField> productFields,
  }) {
    return _RuleDraft(
      id: null,
      productTypeId: productTypeId,
      name: 'صغير',
      fabricWidth: null,
      fabricWidthUnit: 'بوصة',
      sizeClassId: null,
      minimumValue: null,
      maximumValue: null,
      firstMeasurementId:
          productFields.isEmpty ? null : productFields.first.measurementFieldId,
      firstFactor: null,
      secondMeasurementId: null,
      secondFactor: null,
      conditionalMeasurementId: null,
      fixedIncrease: null,
      resultUnit: 'بوصة',
      priority: 100,
      status: 'Active',
      isActive: true,
      isEditing: false,
      needsManualReview: false,
      formulaRaw: '',
    );
  }

  factory _RuleDraft.fromRule(
    ConsumptionRuleRecord rule,
    List<ConsumptionRuleMeasurementField> fields, {
    List<ConsumptionRuleSizeClass> sizeClasses = const [],
  }) {
    final formula = rule.formula.trim();
    final sizeClass = sizeClasses
        .firstWhereOrNull((item) => item.sizeClassId == rule.sizeClassId);
    final parsed = _parseFormulaSnapshot(formula, fields, sizeClass);

    return _RuleDraft(
      id: rule.consumptionRuleId,
      productTypeId: rule.productTypeId,
      name: rule.name,
      fabricWidth: rule.fabricWidth,
      fabricWidthUnit: rule.fabricWidthUnit?.isNotEmpty == true
          ? rule.fabricWidthUnit!
          : 'بوصة',
      sizeClassId: rule.sizeClassId,
      minimumValue:
          rule.minimumValue ?? parsed.minimumValue ?? sizeClass?.minimumValue,
      maximumValue:
          rule.maximumValue ?? parsed.maximumValue ?? sizeClass?.maximumValue,
      firstMeasurementId: parsed.firstMeasurementId,
      firstFactor: parsed.firstFactor,
      secondMeasurementId: parsed.secondMeasurementId,
      secondFactor: parsed.secondFactor,
      conditionalMeasurementId: parsed.conditionalMeasurementId,
      fixedIncrease: parsed.fixedIncrease,
      resultUnit: rule.resultUnit.isEmpty ? 'بوصة' : rule.resultUnit,
      priority: rule.priority,
      status: rule.status,
      isActive: rule.isActive,
      isEditing: false,
      needsManualReview: !_shouldReview(rule, formula, fields),
      formulaRaw: rule.formula,
    );
  }

  static _ParsedFormulaSnapshot _parseFormulaSnapshot(
    String formula,
    List<ConsumptionRuleMeasurementField> fields,
    ConsumptionRuleSizeClass? sizeClass,
  ) {
    if (formula.isEmpty) {
      return const _ParsedFormulaSnapshot(
        firstMeasurementId: null,
        firstFactor: null,
        secondMeasurementId: null,
        secondFactor: null,
        conditionalMeasurementId: null,
        fixedIncrease: null,
        minimumValue: null,
        maximumValue: null,
      );
    }

    final codeMatches = <_FieldFormulaMatch>[];
    final lowerFormula = formula.toLowerCase();
    for (final field in fields) {
      final code = field.code.trim();
      if (code.isEmpty) continue;
      final lowerCode = code.toLowerCase();
      final index = lowerFormula.indexOf(lowerCode);
      if (index < 0) continue;
      final factorMatch = RegExp(
              '${RegExp.escape(code)}\\s*\\*\\s*([0-9]+(?:\\.[0-9]+)?)',
              caseSensitive: false)
          .firstMatch(formula);
      final factor = factorMatch != null
          ? (double.tryParse(factorMatch.group(1) ?? '') ?? 1.0)
          : 1.0;
      codeMatches.add(_FieldFormulaMatch(
        fieldId: field.measurementFieldId,
        code: code,
        index: index,
        factor: factor,
      ));
    }

    codeMatches.sort((a, b) => a.index.compareTo(b.index));
    final first = codeMatches.isNotEmpty ? codeMatches.first : null;
    final second = codeMatches.length > 1 ? codeMatches[1] : null;

    final conditionalMeasurementId = sizeClass == null
        ? null
        : fields
            .firstWhereOrNull((field) =>
                field.code.trim().equalsIgnoreCase(sizeClass.measurementCode))
            ?.measurementFieldId;

    final fixedIncrease = _parseFixedIncrease(formula);
    final minimumValue = sizeClass?.minimumValue;
    final maximumValue = sizeClass?.maximumValue;

    return _ParsedFormulaSnapshot(
      firstMeasurementId: first?.fieldId,
      firstFactor:
          first != null && factorFromFormula(formula, first.code) != null
              ? factorFromFormula(formula, first.code)
              : null,
      secondMeasurementId: second?.fieldId,
      secondFactor:
          second != null && factorFromFormula(formula, second.code) != null
              ? factorFromFormula(formula, second.code)
              : null,
      conditionalMeasurementId: conditionalMeasurementId,
      fixedIncrease: fixedIncrease,
      minimumValue: minimumValue,
      maximumValue: maximumValue,
    );
  }

  static double? factorFromFormula(String formula, String code) {
    if (code.trim().isEmpty) {
      return null;
    }

    final match = RegExp(
            '${RegExp.escape(code)}\\s*\\*\\s*([0-9]+(?:\\.[0-9]+)?)',
            caseSensitive: false)
        .firstMatch(formula);
    if (match != null) {
      return double.tryParse(match.group(1) ?? '');
    }

    return formula.toLowerCase().contains(code.toLowerCase()) ? 1 : null;
  }

  static double? _parseFixedIncrease(String formula) {
    if (formula.isEmpty) return null;
    final match = RegExp(r'\+\s*([0-9]+(?:\.[0-9]+)?)\s*(?:$|\+|\-)',
            caseSensitive: false)
        .allMatches(formula)
        .lastOrNull;
    if (match != null) {
      return double.tryParse(match.group(1) ?? '');
    }

    final finalMatch =
        RegExp(r'\+\s*([0-9]+(?:\.[0-9]+)?)\s*$', caseSensitive: false)
            .firstMatch(formula);
    if (finalMatch != null) {
      return double.tryParse(finalMatch.group(1) ?? '');
    }

    return null;
  }

  String generatedFormula(List<ConsumptionRuleMeasurementField> fields) {
    final firstField = fields.firstWhereOrNull(
        (field) => field.measurementFieldId == firstMeasurementId);
    final secondField = fields.firstWhereOrNull(
        (field) => field.measurementFieldId == secondMeasurementId);
    final parts = <String>[];

    if (firstField != null && (firstFactor ?? 0) > 0) {
      final first = firstFactor == 1
          ? firstField.code
          : '${firstField.code} * $firstFactor';
      parts.add(first);
    }
    if (secondField != null && (secondFactor ?? 0) > 0) {
      final second = secondFactor == 1
          ? secondField.code
          : '${secondField.code} * $secondFactor';
      parts.add(second);
    }
    if ((fixedIncrease ?? 0) > 0) {
      parts.add(fixedIncrease.toString());
    }
    return parts.isNotEmpty ? parts.join(' + ').trim() : formulaRaw.trim();
  }

  bool get isDirty => isEditing || !isSaved;

  String previewText(List<ConsumptionRuleMeasurementField> fields) {
    final raw = formulaRaw.trim();
    if (raw.isNotEmpty) {
      return 'قاعدة محفوظة: ${_uiText(raw)}';
    }

    final firstField = fields.firstWhereOrNull(
        (field) => field.measurementFieldId == firstMeasurementId);
    final secondField = fields.firstWhereOrNull(
        (field) => field.measurementFieldId == secondMeasurementId);
    final parts = <String>[];

    if (firstField != null && (firstFactor ?? 0) > 0) {
      final label =
          firstField.nameAr.isEmpty ? firstField.code : firstField.nameAr;
      parts.add(firstFactor == 1 ? '$label × 1' : '$label × $firstFactor');
    }
    if (secondField != null && (secondFactor ?? 0) > 0) {
      final label =
          secondField.nameAr.isEmpty ? secondField.code : secondField.nameAr;
      parts.add(secondFactor == 1 ? '$label × 1' : '$label × $secondFactor');
    }
    if ((fixedIncrease ?? 0) > 0) {
      parts.add('$fixedIncrease ${resultUnit.isEmpty ? 'بوصة' : resultUnit}');
    }
    if (parts.isEmpty) {
      return 'قاعدة محفوظة لا يمكن تحليلها تلقائياً';
    }
    return parts.join(' + ');
  }

  static bool _shouldReview(ConsumptionRuleRecord rule, String formula,
      List<ConsumptionRuleMeasurementField> fields) {
    if (rule.productTypeId <= 0 || rule.status.isEmpty) return true;
    if (formula.trim().isEmpty) return true;
    if (fields.isEmpty) return false;
    final hasKnownField = fields.any((field) => formula.contains(field.code));
    if (!hasKnownField && rule.formula.isNotEmpty) {
      return true;
    }
    return false;
  }
}

class _ParsedFormulaSnapshot {
  const _ParsedFormulaSnapshot({
    required this.firstMeasurementId,
    required this.firstFactor,
    required this.secondMeasurementId,
    required this.secondFactor,
    required this.conditionalMeasurementId,
    required this.fixedIncrease,
    required this.minimumValue,
    required this.maximumValue,
  });

  final int? firstMeasurementId;
  final double? firstFactor;
  final int? secondMeasurementId;
  final double? secondFactor;
  final int? conditionalMeasurementId;
  final double? fixedIncrease;
  final double? minimumValue;
  final double? maximumValue;
}

class _FieldFormulaMatch {
  const _FieldFormulaMatch({
    required this.fieldId,
    required this.code,
    required this.index,
    required this.factor,
  });

  final int fieldId;
  final String code;
  final int index;
  final double factor;
}

extension _StringCaseOn on String {
  bool equalsIgnoreCase(String other) => toLowerCase() == other.toLowerCase();
}

extension _IterableLast<T> on Iterable<T> {
  T? get lastOrNull {
    final iterator = this.iterator;
    if (!iterator.moveNext()) {
      return null;
    }
    T? result;
    do {
      result = iterator.current;
    } while (iterator.moveNext());
    return result;
  }
}

class ConsumptionRulesDashboard {
  const ConsumptionRulesDashboard({
    required this.productTypes,
    required this.sizeClasses,
    required this.rules,
    required this.measurementProfiles,
    required this.measurementFields,
  });

  final List<ConsumptionRuleProductType> productTypes;
  final List<ConsumptionRuleSizeClass> sizeClasses;
  final List<ConsumptionRuleRecord> rules;
  final List<ConsumptionRuleMeasurementProfile> measurementProfiles;
  final List<ConsumptionRuleMeasurementField> measurementFields;

  bool get hasIntegrityProblems => rules.isEmpty || measurementFields.isEmpty;

  factory ConsumptionRulesDashboard.empty() {
    return const ConsumptionRulesDashboard(
      productTypes: [],
      sizeClasses: [],
      rules: [],
      measurementProfiles: [],
      measurementFields: [],
    );
  }

  factory ConsumptionRulesDashboard.fromJson(Map<String, dynamic> json) {
    return ConsumptionRulesDashboard(
      productTypes: ((json['productTypes'] as List?) ?? const [])
          .map((item) =>
              ConsumptionRuleProductType.fromJson(item as Map<String, dynamic>))
          .toList(),
      sizeClasses: ((json['sizeClasses'] as List?) ?? const [])
          .map((item) =>
              ConsumptionRuleSizeClass.fromJson(item as Map<String, dynamic>))
          .toList(),
      rules: ((json['rules'] as List?) ?? const [])
          .map((item) =>
              ConsumptionRuleRecord.fromJson(item as Map<String, dynamic>))
          .toList(),
      measurementProfiles: ((json['measurementProfiles'] as List?) ?? const [])
          .map((item) => ConsumptionRuleMeasurementProfile.fromJson(
              item as Map<String, dynamic>))
          .toList(),
      measurementFields: ((json['measurementFields'] as List?) ?? const [])
          .map((item) => ConsumptionRuleMeasurementField.fromJson(
              item as Map<String, dynamic>))
          .toList(),
    );
  }
}

class ConsumptionRuleProductType {
  const ConsumptionRuleProductType({
    required this.productTypeId,
    required this.nameAr,
    required this.isActive,
  });

  final int productTypeId;
  final String nameAr;
  final bool isActive;

  factory ConsumptionRuleProductType.fromJson(Map<String, dynamic> json) {
    return ConsumptionRuleProductType(
      productTypeId: json['productTypeId'] as int? ?? 0,
      nameAr: json['nameAr'] as String? ?? '',
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class ConsumptionRuleSizeClass {
  const ConsumptionRuleSizeClass({
    required this.sizeClassId,
    required this.productTypeId,
    required this.code,
    required this.nameAr,
    required this.measurementCode,
    required this.minimumValue,
    required this.maximumValue,
  });

  final int sizeClassId;
  final int productTypeId;
  final String code;
  final String nameAr;
  final String measurementCode;
  final double? minimumValue;
  final double? maximumValue;

  factory ConsumptionRuleSizeClass.fromJson(Map<String, dynamic> json) {
    return ConsumptionRuleSizeClass(
      sizeClassId: json['sizeClassId'] as int? ?? 0,
      productTypeId: json['productTypeId'] as int? ?? 0,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      measurementCode: json['measurementCode'] as String? ?? '',
      minimumValue: (json['minimumValue'] is num)
          ? (json['minimumValue'] as num).toDouble()
          : null,
      maximumValue: (json['maximumValue'] is num)
          ? (json['maximumValue'] as num).toDouble()
          : null,
    );
  }
}

class ConsumptionRuleRecord {
  const ConsumptionRuleRecord({
    required this.consumptionRuleId,
    required this.productTypeId,
    required this.name,
    required this.resultUnit,
    required this.priority,
    required this.status,
    required this.isActive,
    required this.sizeClassId,
    required this.formula,
    required this.fabricWidth,
    required this.fabricWidthUnit,
    required this.measurementCode,
    required this.minimumValue,
    required this.maximumValue,
  });

  final int consumptionRuleId;
  final int productTypeId;
  final String name;
  final String resultUnit;
  final int priority;
  final String status;
  final bool isActive;
  final int? sizeClassId;
  final String formula;
  final double? fabricWidth;
  final String? fabricWidthUnit;
  final String? measurementCode;
  final double? minimumValue;
  final double? maximumValue;

  factory ConsumptionRuleRecord.fromJson(Map<String, dynamic> json) {
    return ConsumptionRuleRecord(
      consumptionRuleId: json['consumptionRuleId'] as int? ?? 0,
      productTypeId: json['productTypeId'] as int? ?? 0,
      name: json['name'] as String? ?? '',
      resultUnit: json['resultUnit'] as String? ?? 'بوصة',
      priority: json['priority'] as int? ?? 100,
      status: json['status'] as String? ?? 'Active',
      isActive: json['isActive'] as bool? ?? true,
      sizeClassId: json['sizeClassId'] as int?,
      formula: json['formula'] as String? ?? '',
      fabricWidth: (json['fabricWidth'] is num)
          ? (json['fabricWidth'] as num).toDouble()
          : null,
      fabricWidthUnit: json['fabricWidthUnit'] as String?,
      measurementCode: json['measurementCode'] as String?,
      minimumValue: (json['minimumValue'] is num)
          ? (json['minimumValue'] as num).toDouble()
          : null,
      maximumValue: (json['maximumValue'] is num)
          ? (json['maximumValue'] as num).toDouble()
          : null,
    );
  }
}

class ConsumptionRuleMeasurementProfile {
  const ConsumptionRuleMeasurementProfile({
    required this.measurementProfileId,
    required this.productTypeId,
    required this.name,
  });

  final int measurementProfileId;
  final int productTypeId;
  final String name;

  factory ConsumptionRuleMeasurementProfile.fromJson(
      Map<String, dynamic> json) {
    return ConsumptionRuleMeasurementProfile(
      measurementProfileId: json['measurementProfileId'] as int? ?? 0,
      productTypeId: json['productTypeId'] as int? ?? 0,
      name: json['name'] as String? ?? '',
    );
  }
}

class ConsumptionRuleMeasurementField {
  const ConsumptionRuleMeasurementField({
    required this.measurementFieldId,
    required this.productTypeId,
    required this.code,
    required this.nameAr,
    required this.unit,
    required this.isRequired,
    required this.sequence,
  });

  final int measurementFieldId;
  final int productTypeId;
  final String code;
  final String nameAr;
  final String unit;
  final bool isRequired;
  final int sequence;

  factory ConsumptionRuleMeasurementField.fromJson(Map<String, dynamic> json) {
    return ConsumptionRuleMeasurementField(
      measurementFieldId: json['measurementFieldId'] as int? ?? 0,
      productTypeId: json['productTypeId'] as int? ?? 0,
      code: json['code'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      unit: json['unit'] as String? ?? 'بوصة',
      isRequired: json['isRequired'] as bool? ?? false,
      sequence: json['sequence'] as int? ?? 0,
    );
  }
}

extension _NullableIterable<T> on Iterable<T> {
  T? firstWhereOrNull(bool Function(T element) test) {
    for (final item in this) {
      if (test(item)) {
        return item;
      }
    }
    return null;
  }
}
