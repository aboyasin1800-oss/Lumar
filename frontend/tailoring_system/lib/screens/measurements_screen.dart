import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/app_navigation.dart';
import 'measurement_piece_tables.dart';

class MeasurementEntryScreen extends StatefulWidget {
  const MeasurementEntryScreen({
    required this.pieceType,
    required this.fields,
    this.initialValues = const {},
    this.onSave,
    super.key,
  });

  final String pieceType;
  final List<String> fields;
  final Map<String, String> initialValues;
  final Future<void> Function(Map<String, double> values)? onSave;

  @override
  State<MeasurementEntryScreen> createState() => _MeasurementEntryScreenState();
}

class _MeasurementEntryScreenState extends State<MeasurementEntryScreen> {
  late final Map<String, TextEditingController> _controllers;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _controllers = {
      for (final field in widget.fields)
        field: TextEditingController(
          text: widget.initialValues[field] == '---'
              ? ''
              : widget.initialValues[field] ?? '',
        ),
    };
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final numericValues = <String, double>{};
    final enteredValues = <String, String>{};
    for (final entry in _controllers.entries) {
      final text = entry.value.text.trim();
      if (text.isEmpty) continue;
      final value = double.tryParse(text.replaceAll(',', '.'));
      if (value == null || value < 0) {
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('أدخل قيمة صحيحة لحقل ${entry.key}.')));
        return;
      }
      numericValues[entry.key] = value;
      enteredValues[entry.key] = text.replaceAll(',', '.');
    }
    if (numericValues.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('أدخل قياسًا واحدًا على الأقل.')));
      return;
    }
    setState(() => _saving = true);
    try {
      await widget.onSave?.call(numericValues);
      if (!mounted) return;
      Navigator.of(context).pop(enteredValues);
    } catch (error) {
      if (!mounted) return;
      setState(() => _saving = false);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(error.toString().replaceFirst('HttpException: ', ''))));
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text('مقاسات ${widget.pieceType}')),
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 900),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'أدخل مقاسات قطعة ${widget.pieceType}',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 8),
                    const Text(
                        'تُعاد القيم إلى الطلب الحالي عند الضغط على حفظ.'),
                    const SizedBox(height: 24),
                    LayoutBuilder(
                      builder: (context, constraints) {
                        final fieldWidth = constraints.maxWidth < 600
                            ? constraints.maxWidth
                            : (constraints.maxWidth - 16) / 2;
                        return Wrap(
                          spacing: 16,
                          runSpacing: 16,
                          children: widget.fields
                              .map((field) => SizedBox(
                                    width: fieldWidth,
                                    child: TextFormField(
                                      controller: _controllers[field],
                                      keyboardType:
                                          const TextInputType.numberWithOptions(
                                              decimal: true),
                                      textInputAction:
                                          field == widget.fields.last
                                              ? TextInputAction.done
                                              : TextInputAction.next,
                                      onFieldSubmitted: (_) {
                                        if (field == widget.fields.last) {
                                          _save();
                                        }
                                      },
                                      decoration: () {
                                        final isLight =
                                            Theme.of(context).brightness ==
                                                Brightness.light;
                                        final normalBorder = isLight
                                            ? const Color(0xFFCBD5E1)
                                            : const Color(0xFF2B6865);
                                        final focusedBorder = isLight
                                            ? const Color(0xFF168F86)
                                            : const Color(0xFF02E1B4);
                                        return InputDecoration(
                                          labelText: field,
                                          filled: true,
                                          fillColor: isLight
                                              ? const Color(0xFFF8FAFC)
                                              : const Color(0xFF0D131C),
                                          labelStyle: TextStyle(
                                            fontSize: 13,
                                            color: isLight
                                                ? const Color(0xFF475569)
                                                : const Color(0xFFB7C7DA),
                                          ),
                                          border: OutlineInputBorder(
                                            borderSide:
                                                BorderSide(color: normalBorder),
                                          ),
                                          enabledBorder: OutlineInputBorder(
                                            borderSide:
                                                BorderSide(color: normalBorder),
                                          ),
                                          focusedBorder: OutlineInputBorder(
                                            borderSide: BorderSide(
                                                color: focusedBorder,
                                                width: 1.5),
                                          ),
                                        );
                                      }(),
                                    ),
                                  ))
                              .toList(),
                        );
                      },
                    ),
                    const SizedBox(height: 24),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        TextButton(
                          onPressed: _saving
                              ? null
                              : () => Navigator.of(context).pop(),
                          child: const Text('إلغاء'),
                        ),
                        const SizedBox(width: 12),
                        FilledButton.icon(
                          onPressed: _saving ? null : _save,
                          icon: _saving
                              ? const SizedBox.square(
                                  dimension: 18,
                                  child:
                                      CircularProgressIndicator(strokeWidth: 2))
                              : const Icon(Icons.save_outlined),
                          label: const Text('حفظ المقاسات',
                              style: TextStyle(fontWeight: FontWeight.w700)),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
}

class MeasurementsScreen extends StatefulWidget {
  const MeasurementsScreen({this.customerId, this.pieceType, super.key});

  final int? customerId;
  final String? pieceType;

  @override
  State<MeasurementsScreen> createState() => _MeasurementsScreenState();
}

class _MeasurementsScreenState extends State<MeasurementsScreen> {
  final _customerSearchController = TextEditingController();
  final _customerSearchFocusNode = FocusNode();
  final _api = MeasurementsApi();
  final Map<String, List<String>> _officialFieldsByPieceType = {};
  Future<List<CustomerMeasurement>>? _measurements;
  int? _selectedCustomerId;
  String? _selectedCustomerLabel;
  int _searchGeneration = 0;

  static const _fieldsByPieceType = <String, List<String>>{
    'ثوب': [
      'الطول',
      'الكتف',
      'اليد',
      'وسع الصدر',
      'وسع البطن',
      'الرقبة',
      'طول الكبك',
      'عرض الكبك',
      'وسع المرفق',
      'فتحة أسفل الثوب'
    ],
    'قميص': [
      'الطول',
      'الكتف',
      'اليد',
      'وسع الصدر',
      'وسع البطن',
      'الرقبة',
      'طول الكبك',
      'عرض الكبك',
      'وسع المرفق'
    ],
    'بنطلون': [
      'الطول',
      'الحزام',
      'الأرداف',
      'الفخذ',
      'الركبة',
      'الفتحة',
      'عرض الحزام'
    ],
    'كوت': [
      'الطول',
      'الكتف',
      'اليد',
      'وسع الصدر',
      'وسع البطن',
      'فتحة اليد',
      'وسع المرفق'
    ],
    'يلق': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'],
    'بالطو': ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
    'جاكيت': ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
  };

  @override
  void initState() {
    super.initState();
    _loadOfficialMeasurementFields();
    if (widget.customerId != null && widget.customerId! > 0) {
      _selectedCustomerId = widget.customerId;
      _selectedCustomerLabel = 'العميل رقم ${widget.customerId}';
      _customerSearchController.text = _selectedCustomerLabel!;
      _measurements = _api.getByCustomerId(widget.customerId!);
    }
  }

  @override
  void dispose() {
    _customerSearchController.dispose();
    _customerSearchFocusNode.dispose();
    super.dispose();
  }

  Future<void> _loadOfficialMeasurementFields() async {
    try {
      final response =
          await http.get(Uri.parse('http://127.0.0.1:5093/consumption-rules'));
      if (response.statusCode < 200 || response.statusCode >= 300) return;
      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) return;

      final byProductId = <int, List<String>>{};
      for (final item in ((decoded['measurementFields'] as List?) ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList()
        ..sort((a, b) {
          final sa = int.tryParse(a['sequence']?.toString() ?? '') ?? 1 << 30;
          final sb = int.tryParse(b['sequence']?.toString() ?? '') ?? 1 << 30;
          return sa.compareTo(sb);
        })) {
        final productTypeId =
            int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0;
        final name = (item['nameAr']?.toString() ?? '').trim();
        if (productTypeId <= 0 || name.isEmpty) continue;
        byProductId.putIfAbsent(productTypeId, () => <String>[]).add(name);
      }

      final productTypeNames = <int, String>{
        for (final item in ((decoded['productTypes'] as List?) ?? const [])
            .whereType<Map<String, dynamic>>())
          (int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0):
              (item['nameAr']?.toString() ?? '').trim(),
      };

      final nextFields = <String, List<String>>{};
      for (final entry in byProductId.entries) {
        final productName = productTypeNames[entry.key] ?? '';
        final pieceType = _normalizePieceTypeKey(productName);
        if (pieceType.isEmpty) continue;
        nextFields[pieceType] = [...entry.value.toSet()];
      }

      if (mounted && nextFields.isNotEmpty) {
        setState(() {
          _officialFieldsByPieceType
            ..clear()
            ..addAll(nextFields);
        });
      }
    } catch (_) {
      // Fallback keeps the existing local map when the server is unavailable.
    }
  }

  String _normalizePieceTypeKey(String value) {
    final normalized = value.trim();
    if (normalized.isEmpty) return '';
    const aliases = <String, String>{
      'ثوب قطري': 'ثوب',
      'ثوب حجازي': 'ثوب',
      'فانيلة': 'فنيلة',
      'فنيلة': 'فنيلة',
      'مقطب': 'مقطب',
      'مكتب': 'مقطب',
    };
    final direct = aliases[normalized];
    if (direct != null) return direct;
    if (normalized.contains('ثوب')) return 'ثوب';
    if (normalized.contains('قميص')) return 'قميص';
    if (normalized.contains('يلق')) return 'يلق';
    if (normalized.contains('بنطلون')) return 'بنطلون';
    if (normalized.contains('كوت')) return 'كوت';
    if (normalized.contains('مريلة')) return 'مريلة';
    if (normalized.contains('طاقية')) return 'طاقية';
    if (normalized.contains('سروال')) return 'سروال';
    if (normalized.contains('مقطب')) return 'مقطب';
    if (normalized.contains('مكتب')) return 'مقطب';
    return normalized;
  }

  List<String> _resolvedFieldsForPieceType(String pieceType) {
    final normalized = _normalizePieceTypeKey(pieceType);
    final official = _officialFieldsByPieceType[normalized];
    if (official != null && official.isNotEmpty) {
      return official;
    }
    return _fieldsByPieceType[normalized] ??
        _fieldsByPieceType[pieceType] ??
        const [];
  }

  void _loadMeasurements() {
    final customerId = _selectedCustomerId;
    if (customerId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('اختر عميلًا من الاقتراحات أولًا.')));
      return;
    }
    setState(() => _measurements = _api.getByCustomerId(customerId));
  }

  Future<Iterable<CustomerSearchResult>> _searchCustomers(
      TextEditingValue value) async {
    final term = value.text.trim();
    if (term.isEmpty || term == _selectedCustomerLabel) return const [];
    final generation = ++_searchGeneration;
    await Future<void>.delayed(const Duration(milliseconds: 300));
    if (generation != _searchGeneration) return const [];
    final customers = await _api.searchCustomers(term);
    final normalizedTerm = term.toLowerCase();
    final exactMatches = customers
        .where((customer) =>
            customer.customerName.toLowerCase() == normalizedTerm ||
            customer.customerCode.toLowerCase() == normalizedTerm ||
            customer.phoneNumber.toLowerCase() == normalizedTerm)
        .toList();
    if (exactMatches.length == 1 && mounted) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && generation == _searchGeneration) {
          _selectCustomer(exactMatches.single);
        }
      });
    }
    return customers;
  }

  void _selectCustomer(CustomerSearchResult customer) {
    setState(() {
      _selectedCustomerId = customer.customerId;
      _selectedCustomerLabel = customer.label;
      _measurements = _api.getByCustomerId(customer.customerId);
    });
    _customerSearchController.text = customer.label;
    _customerSearchController.selection =
        TextSelection.collapsed(offset: customer.label.length);
    _customerSearchFocusNode.unfocus();
  }

  Future<void> _editPieceTypeMeasurements(
      String pieceType, List<CustomerMeasurement> allMeasurements) async {
    final customerId = _selectedCustomerId;
    if (customerId == null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('اختر عميلًا أولًا.')));
      return;
    }

    final latest = _latestMeasurements(
        allMeasurements.where((item) => item.pieceType == pieceType));
    final fields = {
      ..._resolvedFieldsForPieceType(pieceType),
      ...latest.keys,
    }.toList();

    final saved = await AppNavigation.push<Map<String, String>>(
        context,
        (_) => MeasurementEntryScreen(
              pieceType: pieceType,
              fields: fields,
              initialValues: {
                for (final entry in latest.entries) entry.key: entry.value.value
              },
              onSave: (values) async {
                final returned =
                    await _api.upsert(customerId, pieceType, values);
                final persisted = await _api.getByCustomerId(customerId);
                final persistedLatest = _latestMeasurements(
                    persisted.where((item) => item.pieceType == pieceType));
                for (final entry in values.entries) {
                  final actual = persistedLatest[entry.key];
                  if (actual == null) {
                    throw const HttpException(
                        'لم يُحفظ قياس المقاصة في السجل.');
                  }
                  final expected = entry.value;
                  final actualValue =
                      num.tryParse(actual.value.replaceAll(',', '.'));
                  if (actualValue == null ||
                      actualValue.toDouble() != expected) {
                    throw const HttpException(
                        'تعذر التحقق من القيمة المحفوظة في السجل.');
                  }
                }
                for (final row in returned) {
                  if (row.pieceType.trim() != pieceType.trim()) {
                    throw const HttpException(
                        'استجابة الحفظ تخص نوع قطعة غير مطلوب.');
                  }
                }
              },
            ));
    if (saved != null && mounted) {
      final persisted = await _api.getByCustomerId(customerId);
      if (!mounted) return;
      final persistedLatest = _latestMeasurements(
          persisted.where((item) => item.pieceType == pieceType));
      final allSaved = saved.entries.every((entry) {
        final actual = persistedLatest[entry.key];
        if (actual == null) return false;
        final expected = num.tryParse(entry.value.replaceAll(',', '.'));
        final actualValue = num.tryParse(actual.value.replaceAll(',', '.'));
        return expected == actualValue;
      });
      if (!allSaved) {
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تعذر التحقق من حفظ المقاسات.')));
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          setState(() => _measurements = Future.value(persisted));
        });
        return;
      }
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تم حفظ المقاسات بنجاح.')));
        setState(() => _measurements = Future.value(persisted));
      });
    }
  }

  Map<String, CustomerMeasurement> _latestMeasurements(
      Iterable<CustomerMeasurement> measurements) {
    final latest = <String, CustomerMeasurement>{};
    for (final measurement in measurements) {
      final current = latest[measurement.measurementName];
      if (current == null ||
          measurement.revisionNumber > current.revisionNumber ||
          (measurement.revisionNumber == current.revisionNumber &&
              measurement.createdAtUtc.isAfter(current.createdAtUtc))) {
        latest[measurement.measurementName] = measurement;
      }
    }
    return latest;
  }

  @override
  Widget build(BuildContext context) {
    final isLight = Theme.of(context).brightness == Brightness.light;
    return ColoredBox(
      color: isLight
          ? const Color(0xFFF4F7FA)
          : Theme.of(context).scaffoldBackgroundColor,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('قياسات العميل', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 16),
          Row(children: [
            Expanded(
                child: RawAutocomplete<CustomerSearchResult>(
              textEditingController: _customerSearchController,
              focusNode: _customerSearchFocusNode,
              displayStringForOption: (customer) => customer.label,
              optionsBuilder: _searchCustomers,
              onSelected: _selectCustomer,
              fieldViewBuilder: (context, controller, focusNode, onSubmitted) =>
                  TextField(
                controller: controller,
                focusNode: focusNode,
                onChanged: (value) {
                  if (value != _selectedCustomerLabel) {
                    _selectedCustomerId = null;
                  }
                },
                onSubmitted: (_) => onSubmitted(),
                decoration: InputDecoration(
                  filled: true,
                  fillColor: isLight ? const Color(0xFFFFFFFF) : null,
                  labelText: 'اسم العميل أو الكود أو رقم الهاتف',
                  labelStyle: TextStyle(
                    color: isLight ? const Color(0xFF475569) : null,
                  ),
                  prefixIcon: Icon(
                    Icons.person_search_outlined,
                    color: isLight ? const Color(0xFF475569) : null,
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderSide: BorderSide(
                      color: isLight
                          ? const Color(0xFFCBD5E1)
                          : const Color(0xFF2B6865),
                    ),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderSide: BorderSide(
                      color: isLight
                          ? const Color(0xFF168F86)
                          : const Color(0xFF02E1B4),
                      width: 1.5,
                    ),
                  ),
                  border: OutlineInputBorder(),
                ),
              ),
              optionsViewBuilder: (context, onSelected, options) => Align(
                alignment: AlignmentDirectional.topStart,
                child: Material(
                  elevation: 8,
                  clipBehavior: Clip.antiAlias,
                  borderRadius: BorderRadius.circular(6),
                  child: ConstrainedBox(
                    constraints:
                        const BoxConstraints(maxHeight: 320, maxWidth: 720),
                    child: ListView.builder(
                      padding: EdgeInsets.zero,
                      shrinkWrap: true,
                      itemCount: options.length,
                      itemBuilder: (context, index) {
                        final customer = options.elementAt(index);
                        return ListTile(
                          leading: const Icon(Icons.person_outline),
                          title: Text(customer.customerName),
                          subtitle: Text(
                              'الكود: ${customer.customerCode.isEmpty ? '-' : customer.customerCode}   الهاتف: ${customer.phoneNumber.isEmpty ? '-' : customer.phoneNumber}'),
                          onTap: () => onSelected(customer),
                        );
                      },
                    ),
                  ),
                ),
              ),
            )),
            const SizedBox(width: 8),
            FilledButton.icon(
                onPressed: _loadMeasurements,
                icon: const Icon(Icons.search),
                label: const Text('عرض القياسات')),
          ]),
          const SizedBox(height: 16),
          Expanded(
              child: _measurements == null
                  ? const Center(
                      child: Text('ابحث عن العميل ثم اختره لعرض القياسات.'))
                  : FutureBuilder<List<CustomerMeasurement>>(
                      future: _measurements,
                      builder: (context, snapshot) {
                        if (snapshot.connectionState != ConnectionState.done) {
                          return const Center(
                              child: CircularProgressIndicator());
                        }
                        if (snapshot.hasError) {
                          return const Center(
                              child: Text('تعذر تحميل قياسات العميل.'));
                        }
                        final allMeasurements = snapshot.data ?? [];
                        final filtered = allMeasurements
                            .where((measurement) =>
                                widget.pieceType == null ||
                                measurement.pieceType == widget.pieceType)
                            .toList();
                        return Padding(
                          padding: const EdgeInsetsDirectional.symmetric(
                              horizontal: 16),
                          child: MeasurementPieceTables(
                            measurements: filtered,
                            officialFieldOrder: _officialFieldsByPieceType,
                            onEdit: (pieceType) =>
                                _editPieceTypeMeasurements(pieceType, filtered),
                          ),
                        );
                      })),
        ],
      ),
    );
  }
}

class CustomerMeasurement {
  const CustomerMeasurement(
      {required this.pieceType,
      required this.measurementName,
      required this.value,
      required this.createdAtUtc,
      required this.revisionNumber});
  factory CustomerMeasurement.fromJson(Map<String, dynamic> json) =>
      CustomerMeasurement(
          pieceType: json['pieceType'] as String? ?? '',
          measurementName: json['measurementName'] as String? ?? '',
          value: (json['measurementValue'] as num?)?.toString() ?? '0',
          createdAtUtc:
              DateTime.tryParse(json['createdAtUtc'] as String? ?? '') ??
                  DateTime.fromMillisecondsSinceEpoch(0),
          revisionNumber: json['revisionNumber'] as int? ?? 0);
  final String pieceType;
  final String measurementName;
  final String value;
  final DateTime createdAtUtc;
  final int revisionNumber;
}

class CustomerSearchResult {
  const CustomerSearchResult(
      {required this.customerId,
      required this.customerName,
      required this.customerCode,
      required this.phoneNumber});
  factory CustomerSearchResult.fromJson(Map<String, dynamic> json) =>
      CustomerSearchResult(
        customerId: json['customerId'] as int? ?? 0,
        customerName: json['customerName'] as String? ?? 'عميل بدون اسم',
        customerCode: json['customerCode'] as String? ?? '',
        phoneNumber: json['phoneNumber'] as String? ?? '',
      );
  final int customerId;
  final String customerName;
  final String customerCode;
  final String phoneNumber;
  String get label =>
      '$customerName | ${customerCode.isEmpty ? '-' : customerCode} | ${phoneNumber.isEmpty ? '-' : phoneNumber}';
}

class MeasurementsApi {
  MeasurementsApi(
      {this.baseUrl = const String.fromEnvironment('LUMAR_API_URL',
          defaultValue: 'http://127.0.0.1:5093')});
  final String baseUrl;

  Future<List<CustomerSearchResult>> searchCustomers(String term) async {
    final client = HttpClient();
    try {
      final uri = Uri.parse('$baseUrl/customers/search')
          .replace(queryParameters: {'term': term});
      final request = await client.getUrl(uri);
      final response = await request.close();
      final body = await utf8.decoder.bind(response).join();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw HttpException('فشل البحث عن العملاء.');
      }
      return (jsonDecode(body) as List)
          .whereType<Map<String, dynamic>>()
          .map(CustomerSearchResult.fromJson)
          .toList();
    } finally {
      client.close();
    }
  }

  Future<List<CustomerMeasurement>> getByCustomerId(int customerId) async {
    final client = HttpClient();
    try {
      final request = await client
          .getUrl(Uri.parse('$baseUrl/customers/$customerId/measurements'));
      final response = await request.close();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw HttpException('فشل تحميل القياسات.');
      }
      final body = await utf8.decoder.bind(response).join();
      return (jsonDecode(body) as List)
          .cast<Map<String, dynamic>>()
          .map(CustomerMeasurement.fromJson)
          .toList();
    } finally {
      client.close();
    }
  }

  Future<List<CustomerMeasurement>> upsert(
      int customerId, String pieceType, Map<String, double> values) async {
    final client = HttpClient();
    try {
      final request = await client
          .putUrl(Uri.parse('$baseUrl/customers/$customerId/measurements'));
      request.headers.contentType = ContentType.json;
      request.write(jsonEncode({
        'pieceType': pieceType,
        'measurements': values.entries
            .map((entry) =>
                {'measurementName': entry.key, 'measurementValue': entry.value})
            .toList(),
      }));
      final response = await request.close();
      final body = await utf8.decoder.bind(response).join();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw HttpException(body.isEmpty ? 'فشل حفظ المقاسات.' : body);
      }
      if (body.isEmpty) {
        throw const HttpException('تعذر تأكيد حفظ المقاسات من الخادم.');
      }
      final saved = jsonDecode(body);
      if (saved is! List) {
        throw const HttpException('استجابة حفظ المقاسات غير صالحة.');
      }
      final returned = saved
          .whereType<Map<String, dynamic>>()
          .map(CustomerMeasurement.fromJson)
          .toList();
      if (returned.isEmpty) {
        throw const HttpException('لم يثبت الخادم حفظ المقاسات.');
      }
      return returned;
    } finally {
      client.close();
    }
  }
}
