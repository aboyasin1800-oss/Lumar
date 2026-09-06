import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' as intl;

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';
import '../services/screen_chrome_state.dart';
import 'measurements_screen.dart';
import 'ready_made_production_screen.dart';
import 'ready_sales_screen.dart';

enum SalesViewMode {
  tabs,
  sessions,
  all,
  fullscreen,
}

final ValueNotifier<SalesViewMode> salesViewModeNotifier =
    ValueNotifier<SalesViewMode>(SalesViewMode.all);

class SalesScreen extends StatefulWidget {
  const SalesScreen({super.key});

  @override
  State<SalesScreen> createState() => _SalesScreenState();
}

class _SalesScreenState extends State<SalesScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );
  static const _defaultPieceType = '';
  static const _pieceTypeOptions = [
    'ثوب',
    'قميص',
    'يلق',
    'بنطلون',
    'كوت',
    'جاكيت',
    'بالطو',
    'مريلة',
    'فنيلة',
    'طاقية',
    'سروال',
    'مقطب',
  ];
  static const _dashboardCardHeight = 45.0;
  static const _headerTitleFontSize = 12.0;
  static const _sectionTitleFontSize = 16.0;
  static const _tabFontSize = 14.0;
  static const _kpiValueFontSize = 18.0;
  static const _fieldFontSize = 18.0;
  static const _fieldLabelFontSize = 15.0;
  static const _supportingFontSize = 16.0;
  static const _buttonFontSize = 16.0;

  static const Color _surfaceCard = Color.fromARGB(255, 18, 28, 40);
  static const Color _softBlue = Color.fromARGB(255, 32, 32, 32);
  static const Color _primaryBlue = Color.fromARGB(255, 2, 225, 180);
  static const Color _textMain = Color(0xFFEAF2FF);
  static const Color _textSoft = Color(0xFFB7C7DA);
  static const Color _borderSoft = Color.fromARGB(255, 43, 104, 101);

  int piecesCount = 1;
  int currentCustomerId = 0;
  String currentCustomerName = '';
  String currentCustomerCode = '';
  String currentCustomerPhone = '';
  bool customerFound = false;
  String currentPoints = '0';
  String accumulatedPoints = '0';
  String treeCustomerCount = '0';
  String totalAmount = '0.00';
  String paidAmount = '0.00';
  String discountAmount = '0.00';
  String remainingAmount = '0.00';
  String deliveryDate = '';
  String profitPercentage = '12';
  String profitAmount = '1200';
  String activeOrdersCount = '3';
  String totalOrdersCount = '1200';
  String todayOrdersCount = '24';
  bool _savingOrder = false;
  int _selectedTab = 0;
  int? _selectedPieceIndex;
  SalesViewMode _salesViewMode = SalesViewMode.all;

  int _nextSessionNumber = 1;
  final List<_SalesDraft> _drafts = [];
  final Map<int, bool> _hiddenMeasurementRows = {};

  final customerNameController = TextEditingController();
  final customerCodeController = TextEditingController();
  final customerIdController = TextEditingController();
  final phoneController = TextEditingController();
  final referrerController = TextEditingController();
  final currentPointsController = TextEditingController();
  final accumulatedPointsController = TextEditingController();
  final treeCustomerCountController = TextEditingController();
  final totalAmountController = TextEditingController();
  final paidAmountController = TextEditingController();
  final discountAmountController = TextEditingController();
  final remainingAmountController = TextEditingController();
  final deliveryDateController = TextEditingController();
  final profitPercentageController = TextEditingController();
  final profitAmountController = TextEditingController();
  final List<String> pieceTypes = [_defaultPieceType];
  final Map<String, Map<String, String>> measurementValuesByType = {};
  final Map<String, List<String>> _officialMeasurementFieldsByType = {};
  final Map<String, _FabricStockSnapshot> _fabricStockByCode = {};
  final Map<String, bool> _focusedEditableFieldStates = {};
  final List<TextEditingController> quantityControllers = [
    TextEditingController(text: '1')
  ];
  final List<TextEditingController> fabricCodeControllers = [
    TextEditingController()
  ];
  final List<TextEditingController> fabricTypeControllers = [
    TextEditingController()
  ];
  final List<TextEditingController> fabricColorControllers = [
    TextEditingController()
  ];
  final List<TextEditingController> catalogNumberControllers = [
    TextEditingController()
  ];
  final List<TextEditingController> availableInchesControllers = [
    TextEditingController()
  ];
  final List<TextEditingController> notes1Controllers = [
    TextEditingController()
  ];
  final List<TextEditingController> notes2Controllers = [
    TextEditingController()
  ];
  final List<TextEditingController> specialRequestsControllers = [
    TextEditingController()
  ];

  @override
  void initState() {
    super.initState();
    salesViewModeNotifier.addListener(_handleSalesViewModeChanged);
    _salesViewMode = salesViewModeNotifier.value;
    profitPercentageController.text = profitPercentage;
    profitAmountController.text = profitAmount;
    _fillCustomerControllers();
    _loadOfficialMeasurementFields();
    loadMeasurementPreview();
  }

  Future<void> searchCustomerByPhone() =>
      searchCustomerByTerm(phoneController.text.trim());

  Future<void> searchCustomerByTerm(String term) async {
    if (term.isEmpty) {
      _clearCustomer();
      await loadSelectedCustomer();
      return;
    }
    try {
      final uri = Uri.parse('$_baseUrl/customers/search')
          .replace(queryParameters: {'term': term});
      final response = await http.get(uri);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        _clearCustomer();
        await loadSelectedCustomer();
        return;
      }
      final decoded = jsonDecode(response.body);
      final customers = decoded is List
          ? decoded.whereType<Map<String, dynamic>>().toList()
          : <Map<String, dynamic>>[];
      if (customers.isEmpty) {
        _clearCustomer();
      } else if (customers.length == 1) {
        _applySelectedCustomer(customers.first);
      } else {
        final selected = await _pickCustomerFromPhoneMatches(customers);
        if (selected == null) return;
        _applySelectedCustomer(selected);
      }
      await loadSelectedCustomer();
    } catch (error) {
      debugPrint('Customer search failed: $error');
      _clearCustomer();
      await loadSelectedCustomer();
    }
  }

  void _clearCustomer() {
    if (!mounted) return;
    setState(() {
      customerFound = false;
      currentCustomerId = 0;
      currentCustomerCode = '';
      currentCustomerName = '';
    });
  }

  void _applySelectedCustomer(Map<String, dynamic> customer) {
    final id = int.tryParse(customer['customerId']?.toString() ?? '') ?? 0;
    setState(() {
      customerFound = id > 0;
      currentCustomerId = id;
      currentCustomerCode = customer['customerCode']?.toString() ?? '';
      currentCustomerName = customer['customerName']?.toString() ?? '';
      currentCustomerPhone =
          customer['phoneNumber']?.toString() ?? currentCustomerPhone;
    });
  }

  Future<Map<String, dynamic>?> _pickCustomerFromPhoneMatches(
    List<Map<String, dynamic>> customers,
  ) {
    return showDialog<Map<String, dynamic>>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('اختر العميل'),
        content: SizedBox(
          width: 420,
          child: ListView.separated(
            shrinkWrap: true,
            itemCount: customers.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (_, index) {
              final customer = customers[index];
              return ListTile(
                title: Text(
                    customer['customerName']?.toString() ?? 'عميل بدون اسم'),
                subtitle: Text(
                  'الكود: ${customer['customerCode'] ?? '-'} | الجوال: ${customer['phoneNumber'] ?? '-'}',
                ),
                onTap: () => Navigator.of(dialogContext).pop(customer),
              );
            },
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('إلغاء'),
          ),
        ],
      ),
    );
  }

  Future<void> _searchCustomerAndNextFocus(String term) async {
    await searchCustomerByTerm(term);
    if (mounted) FocusScope.of(context).nextFocus();
  }

  Future<void> saveOrder() async {
    if (_savingOrder) return;
    var customerCreated = false;
    final pendingMeasurements = {
      for (final entry in measurementValuesByType.entries)
        entry.key: Map<String, String>.from(entry.value),
    };
    currentCustomerPhone = phoneController.text.trim();
    if (currentCustomerPhone.isEmpty) {
      _showMessage('الرجاء إدخال رقم الهاتف');
      return;
    }
    if (!customerFound) {
      final name = customerNameController.text.trim();
      final code = customerCodeController.text.trim();
      if (name.isEmpty) {
        _showMessage('الرجاء إدخال اسم العميل لإنشاء عميل جديد');
        return;
      }
      if (code.isEmpty) {
        _showMessage('الرجاء إدخال كود العميل لإنشاء عميل جديد');
        return;
      }
      try {
        final response = await http.post(
          Uri.parse('$_baseUrl/customers'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode({
            'customerCode': code,
            'customerName': name,
            'phoneNumber': currentCustomerPhone,
          }),
        );
        if (response.statusCode != 200 && response.statusCode != 201) {
          _showMessage('تعذر إنشاء العميل. تحقق من البيانات المدخلة.');
          return;
        }
        final decoded = jsonDecode(response.body);
        if (decoded is Map<String, dynamic>) {
          setState(() {
            customerCreated = true;
            customerFound = true;
            currentCustomerId =
                int.tryParse(decoded['customerId']?.toString() ?? '') ?? 0;
            currentCustomerCode = decoded['customerCode']?.toString() ?? code;
            currentCustomerName = name;
          });
          _fillCustomerControllers();
          measurementValuesByType
            ..clear()
            ..addAll(pendingMeasurements);
        }
      } catch (error) {
        debugPrint('Customer creation failed: $error');
        _showMessage('تعذر الاتصال بالخادم لإنشاء العميل.');
        return;
      }
    }
    final total =
        double.tryParse(totalAmountController.text.trim().replaceAll(',', '.'));
    final discount = double.tryParse(
            discountAmountController.text.trim().replaceAll(',', '.')) ??
        0;
    final advance = double.tryParse(
            paidAmountController.text.trim().replaceAll(',', '.')) ??
        0;
    if (total == null ||
        total < 0 ||
        discount < 0 ||
        advance < 0 ||
        discount + advance > total) {
      _showMessage('تحقق من الإجمالي والخصم والدفعة المقدمة.');
      return;
    }
    final items = <Map<String, dynamic>>[];
    for (var index = 0; index < pieceTypes.length; index++) {
      final quantity = int.tryParse(quantityControllers[index].text.trim());
      if (quantity == null || quantity <= 0) {
        _showMessage('أدخل عددًا صحيحًا للقطعة رقم ${index + 1}.');
        return;
      }
      final fabricCode = fabricCodeControllers[index].text.trim();
      final fabricType = fabricTypeControllers[index].text.trim();
      final fabricColor = fabricColorControllers[index].text.trim();
      final hasFabric = fabricCode.isNotEmpty ||
          fabricType.isNotEmpty ||
          fabricColor.isNotEmpty;
      items.add({
        'pieceType': pieceTypes[index],
        'quantity': quantity,
        'fabricCode': fabricCode.isEmpty ? null : fabricCode,
        'fabricType': fabricType.isEmpty ? null : fabricType,
        'fabricColor': fabricColor.isEmpty ? null : fabricColor,
        'notes1': _nullableText(notes1Controllers[index]),
        'notes2': _nullableText(notes2Controllers[index]),
        'measurementSnapshot':
            jsonEncode(measurementValuesByType[pieceTypes[index]] ?? const {}),
        if (hasFabric)
          'fabric': {
            'fabricCode': fabricCode.isEmpty ? null : fabricCode,
            'fabricType': fabricType.isEmpty ? null : fabricType,
            'fabricColor': fabricColor.isEmpty ? null : fabricColor,
            'quantity': quantity,
            'unit': 'Piece',
            'unitCost': 0,
          },
      });
    }
    setState(() => _savingOrder = true);
    try {
      if (customerCreated) await _persistMeasurements();
      final delivery = deliveryDateController.text.trim();
      final response = await http.post(
        Uri.parse('$_baseUrl/orders'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'customerId': currentCustomerId,
          'orderDate': DateTime.now().toUtc().toIso8601String(),
          'deliveryDate': delivery.isEmpty
              ? null
              : DateTime.tryParse(delivery)?.toUtc().toIso8601String(),
          'totalAmount': total,
          'discountAmount': discount,
          'advancePayment': advance,
          'urgencyStatus': 'Normal',
          'saleCategory': 'TailoringOrder',
          'paymentMethod': advance > 0 ? 'Cash' : null,
          'items': items,
        }),
      );
      if (response.statusCode != 201) {
        debugPrint(
            'Order save failed (${response.statusCode}): ${response.body}');
        _showMessage('تعذر حفظ الطلب. تحقق من البيانات وحاول مجددًا.');
        return;
      }
      final decoded = jsonDecode(response.body) as Map<String, dynamic>;
      _showMessage('تم حفظ الطلب ${decoded['orderNumber'] ?? ''} بنجاح.');
    } catch (error) {
      debugPrint('Order creation failed: $error');
      _showMessage('تعذر الاتصال بالخادم لحفظ الطلب.');
    } finally {
      if (mounted) setState(() => _savingOrder = false);
    }
  }

  Future<void> _persistMeasurements() async {
    final api = MeasurementsApi(baseUrl: _baseUrl);
    for (final entry in measurementValuesByType.entries) {
      final values = <String, double>{};
      for (final measurement in entry.value.entries) {
        final value = double.tryParse(measurement.value.replaceAll(',', '.'));
        if (value != null && value >= 0) values[measurement.key] = value;
      }
      if (values.isNotEmpty) {
        await api.upsert(currentCustomerId, entry.key, values);
      }
    }
  }

  String? _nullableText(TextEditingController controller) {
    final value = controller.text.trim();
    return value.isEmpty ? null : value;
  }

  void _updateRemainingAmount(String _) {
    final total = double.tryParse(
            totalAmountController.text.trim().replaceAll(',', '.')) ??
        0;
    final discount = double.tryParse(
            discountAmountController.text.trim().replaceAll(',', '.')) ??
        0;
    final paid = double.tryParse(
            paidAmountController.text.trim().replaceAll(',', '.')) ??
        0;
    remainingAmountController.text =
        (total - discount - paid).clamp(0, double.infinity).toStringAsFixed(2);
  }

  void _showMessage(String message) {
    if (mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));
    }
  }

  void _fillCustomerControllers() {
    customerNameController.text = currentCustomerName;
    customerCodeController.text = currentCustomerCode;
    customerIdController.text =
        currentCustomerId > 0 ? '$currentCustomerId' : '';
    phoneController.text = currentCustomerPhone;
    currentPointsController.text = currentPoints;
    accumulatedPointsController.text = accumulatedPoints;
    treeCustomerCountController.text = treeCustomerCount;
    totalAmountController.text = totalAmount;
    paidAmountController.text = paidAmount;
    discountAmountController.text = discountAmount;
    remainingAmountController.text = remainingAmount;
    deliveryDateController.text = deliveryDate;
  }

  Future<void> loadSelectedCustomer() async {
    _fillCustomerControllers();
    await loadMeasurementPreview();
  }

  void _updateProfitAmount() {
    final percent = double.tryParse(profitPercentageController.text) ?? 0;
    final base = double.tryParse(totalAmount) ?? 0;
    profitAmount = (base * percent / 100).toStringAsFixed(2);
    profitAmountController.text = profitAmount;
  }

  List<String> _uniquePieceTypes() => pieceTypes.toSet().toList();

  void _syncPieceTypesWithCount() {
    while (pieceTypes.length < piecesCount) {
      pieceTypes.add(_defaultPieceType);
      quantityControllers.add(TextEditingController(text: '1'));
      fabricCodeControllers.add(TextEditingController());
      fabricTypeControllers.add(TextEditingController());
      fabricColorControllers.add(TextEditingController());
      catalogNumberControllers.add(TextEditingController());
      availableInchesControllers.add(TextEditingController());
      notes1Controllers.add(TextEditingController());
      notes2Controllers.add(TextEditingController());
      specialRequestsControllers.add(TextEditingController());
    }
    if (pieceTypes.length > piecesCount) {
      for (final controllers in [
        quantityControllers,
        fabricCodeControllers,
        fabricTypeControllers,
        fabricColorControllers,
        catalogNumberControllers,
        availableInchesControllers,
        notes1Controllers,
        notes2Controllers,
        specialRequestsControllers,
      ]) {
        for (final controller in controllers.sublist(piecesCount)) {
          controller.dispose();
        }
        controllers.removeRange(piecesCount, controllers.length);
      }
      pieceTypes.removeRange(piecesCount, pieceTypes.length);
    }
  }

  Future<void> _loadFabricStockData() async {
    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/fabrics'));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        return;
      }
      final decoded = jsonDecode(response.body);
      if (decoded is! List) {
        return;
      }
      final entries = decoded.whereType<Map<String, dynamic>>();
      final nextStock = <String, _FabricStockSnapshot>{};
      for (final entry in entries) {
        final code = (entry['fabricCode'] ?? entry['FabricCode'] ?? entry['itemCode'] ?? entry['ItemCode'])?.toString();
        if (code == null || code.trim().isEmpty) {
          continue;
        }
        final normalizedCode = code.trim().toUpperCase();
        final quantityInchRaw = entry['quantityInch'] ?? entry['QuantityInch'] ?? entry['availableQuantity'] ?? entry['AvailableQuantity'];
        final quantityInch = double.tryParse(quantityInchRaw?.toString() ?? '') ?? 0;
        final fallbackYards = double.tryParse((entry['availableQuantity'] ?? entry['AvailableQuantity'] ?? entry['quantityYard'] ?? entry['QuantityYard'])?.toString() ?? '') ?? 0;
        nextStock[normalizedCode] = _FabricStockSnapshot(
          fabricType: (entry['fabricName'] ?? entry['FabricName'] ?? entry['fabricType'] ?? entry['FabricType'])?.toString() ?? '',
          fabricColor: (entry['color'] ?? entry['Color'] ?? entry['fabricColor'] ?? entry['FabricColor'])?.toString() ?? '',
          catalogNumber: (entry['catalogNumber'] ?? entry['CatalogNumber'] ?? entry['barcode'] ?? entry['Barcode'])?.toString() ?? '',
          availableInches: quantityInch > 0 ? quantityInch : fallbackYards * 36,
        );
      }
      if (!mounted) return;
      setState(() => _fabricStockByCode
        ..clear()
        ..addAll(nextStock));
    } catch (error) {
      debugPrint('Failed to load fabric stock: $error');
    }
  }

  Future<void> _syncFabricDataForPiece(int pieceIndex) async {
    final code = fabricCodeControllers[pieceIndex].text.trim();
    final fabricTypeController = fabricTypeControllers[pieceIndex];
    final fabricColorController = fabricColorControllers[pieceIndex];
    final catalogController = catalogNumberControllers[pieceIndex];
    final availableController = availableInchesControllers[pieceIndex];

    if (code.isEmpty) {
      fabricTypeController.clear();
      fabricColorController.clear();
      catalogController.clear();
      availableController.clear();
      return;
    }

    final normalizedCode = code.toUpperCase();
    final snapshot = _fabricStockByCode[normalizedCode];
    if (snapshot == null) {
      if (_fabricStockByCode.isEmpty) {
        await _loadFabricStockData();
      }
      final refreshed = _fabricStockByCode[normalizedCode];
      if (refreshed == null) {
        availableController.clear();
        return;
      }
      fabricTypeController.text = refreshed.fabricType;
      fabricColorController.text = refreshed.fabricColor;
      catalogController.text = refreshed.catalogNumber;
      availableController.text = _formatFabricAvailable(refreshed.availableInches);
      return;
    }

    fabricTypeController.text = snapshot.fabricType;
    fabricColorController.text = snapshot.fabricColor;
    catalogController.text = snapshot.catalogNumber;
    availableController.text = _formatFabricAvailable(snapshot.availableInches);
  }

  double _estimatedConsumptionForPiece(int pieceIndex) {
    final quantity = int.tryParse(quantityControllers[pieceIndex].text.trim()) ?? 1;
    return _estimatedConsumption(pieceTypes[pieceIndex], quantity);
  }

  double _estimatedConsumption(String pieceType, int quantity) {
    final type = pieceType.toLowerCase();
    final base = type.contains('قميص') || type.contains('ثوب') || type.contains('فستان')
        ? 1.5
        : type.contains('بنطلون') || type.contains('سروال') || type.contains('شورت')
            ? 1.8
            : type.contains('جاكيت') || type.contains('بالطو') || type.contains('سترة')
                ? 2.2
                : 1.0;
    return base * quantity;
  }

  String _formatFabricAvailable(double value) => value <= 0 ? '0' : value.toStringAsFixed(1);

  Future<void> _loadOfficialMeasurementFields() async {
    final nextFields = <String, List<String>>{};
    try {
      final response = await http.get(Uri.parse('$_baseUrl/consumption-rules'));
      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is Map<String, dynamic>) {
          final productTypes = (decoded['productTypes'] as List? ?? const [])
              .whereType<Map<String, dynamic>>()
              .toList();
          final productTypeNames = <int, String>{
            for (final item in productTypes)
              (int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0):
                  (item['nameAr']?.toString() ?? '').trim(),
          };

          final groupedFields = <int, List<String>>{};
          for (final item in ((decoded['measurementFields'] as List?) ?? const [])
              .whereType<Map<String, dynamic>>()) {
            final productTypeId = int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0;
            final name = (item['nameAr']?.toString() ?? '').trim();
            if (productTypeId <= 0 || name.isEmpty) continue;
            groupedFields.putIfAbsent(productTypeId, () => <String>[]).add(name);
          }

          for (final entry in groupedFields.entries) {
            final productTypeName = productTypeNames[entry.key] ?? '';
            final pieceType = _normalizePieceTypeKey(productTypeName);
            if (pieceType.isEmpty) continue;
            nextFields[pieceType] = <String>[...entry.value.toSet().toList()];
          }
        }
      }
    } catch (error) {
      debugPrint('Failed to load official measurement fields: $error');
    }

    if (nextFields.isNotEmpty) {
      _officialMeasurementFieldsByType
        ..clear()
        ..addAll(nextFields);
    }
  }

  String _normalizePieceTypeKey(String value) {
    final normalized = value.trim();
    if (normalized.isEmpty) return '';
    final aliasMap = <String, String>{
      'ثوب قطري': 'ثوب',
      'ثوب حجازي': 'ثوب',
      'فانيلة': 'فنيلة',
      'فنيلة': 'فنيلة',
      'مقطب': 'مقطب',
      'مكتب': 'مقطب',
      'سروال': 'سروال',
      'بالطو': 'بالطو',
      'جاكيت': 'جاكيت',
      'ميكرو': 'ميكرو',
    };
    final direct = aliasMap[normalized];
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

  Future<void> loadMeasurementPreview() async {
    measurementValuesByType.clear();
    for (final type in _uniquePieceTypes()) {
      measurementValuesByType[type] = {
        for (final field in _measurementFieldsForPiece(type)) field: '---',
      };
    }
    if (currentCustomerId > 0) {
      try {
        final response = await http.get(
          Uri.parse('$_baseUrl/customers/$currentCustomerId/measurements'),
        );
        if (response.statusCode >= 200 && response.statusCode < 300) {
          final decoded = jsonDecode(response.body);
          if (decoded is List) {
            for (final item in decoded.whereType<Map<String, dynamic>>()) {
              final type = item['pieceType']?.toString();
              final name = item['measurementName']?.toString();
              final value = item['measurementValue']?.toString() ?? '';
              if (type != null &&
                  name != null &&
                  measurementValuesByType[type]?[name] == '---') {
                measurementValuesByType[type]![name] =
                    value.isEmpty ? '---' : value;
              }
            }
          }
        }
      } catch (error) {
        debugPrint('Failed to load measurements: $error');
      }
    }
    if (mounted) setState(() {});
  }

  List<String> _measurementFieldsForPiece(String pieceType) {
    final normalizedKey = _normalizePieceTypeKey(pieceType);
    final officialFields = _officialMeasurementFieldsByType[normalizedKey];
    if (officialFields != null && officialFields.isNotEmpty) {
      return officialFields;
    }

    const fieldsByPieceType = <String, List<String>>{
      'كوت': [
        'الطول',
        'الكتف',
        'اليد',
        'وسع الصدر',
        'وسع البطن',
        'فتحة اليد',
        'وسع المرفق',
      ],
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
        'فتحة أسفل الثوب',
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
        'وسع المرفق',
      ],
      'يلق': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'],
      'بنطلون': [
        'الطول',
        'الحزام',
        'الأرداف',
        'الفخذ',
        'الركبة',
        'الفتحة',
        'عرض الحزام',
      ],
      'مريلة': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'],
      'بالطو': ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
      'جاكيت': ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
      'سروال': ['الطول', 'الحزام', 'الأرداف'],
      'فنيلة': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن', 'الرقبة'],
      'مقطب': ['الطول', 'العرض'],
      'طاقية': ['دوران الرأس', 'ارتفاع الحزام'],
    };
    return fieldsByPieceType[normalizedKey] ?? fieldsByPieceType[pieceType] ?? const [];
  }

  @override
  void dispose() {
    ScreenChromeState.instance.setHideTopChrome(false);
    for (final controller in [
      customerNameController,
      customerCodeController,
      customerIdController,
      phoneController,
      referrerController,
      currentPointsController,
      accumulatedPointsController,
      treeCustomerCountController,
      totalAmountController,
      paidAmountController,
      discountAmountController,
      remainingAmountController,
      deliveryDateController,
      profitPercentageController,
      profitAmountController,
    ]) {
      controller.dispose();
    }
    salesViewModeNotifier.removeListener(_handleSalesViewModeChanged);
    for (final controllers in [
      quantityControllers,
      fabricCodeControllers,
      fabricTypeControllers,
      fabricColorControllers,
      catalogNumberControllers,
      availableInchesControllers,
      notes1Controllers,
      notes2Controllers,
      specialRequestsControllers,
    ]) {
      for (final controller in controllers) {
        controller.dispose();
      }
    }
    super.dispose();
  }

  void _handleSalesViewModeChanged() {
    final nextMode = salesViewModeNotifier.value;
    if (!mounted || _salesViewMode == nextMode) return;
    setState(() {
      _salesViewMode = nextMode;
      ScreenChromeState.instance.setHideTopChrome(nextMode == SalesViewMode.fullscreen);
    });
  }

  void _applySalesViewMode(SalesViewMode mode) {
    if (salesViewModeNotifier.value == mode && _salesViewMode == mode) return;
    salesViewModeNotifier.value = mode;
  }

  @override
  Widget build(BuildContext context) {
    final isLightTheme = Theme.of(context).brightness == Brightness.light;
    final screenBackground = isLightTheme
        ? const Color(0xFFF5F7FB)
        : const Color.fromARGB(255, 12, 20, 32);
    final currentMode = salesViewModeNotifier.value;
    if (_salesViewMode != currentMode) {
      _salesViewMode = currentMode;
    }

    final showTabs = _salesViewMode != SalesViewMode.fullscreen &&
        (_salesViewMode == SalesViewMode.tabs || _salesViewMode == SalesViewMode.all);
    final showHeader = _salesViewMode != SalesViewMode.fullscreen &&
        _selectedTab != 2 &&
        (_salesViewMode == SalesViewMode.sessions || _salesViewMode == SalesViewMode.all);

    return Scaffold(
      backgroundColor: screenBackground,
      body: Column(
        children: [
          if (showTabs) _buildSalesTabs(context),
          if (showTabs && showHeader) const SizedBox(height: 2),
          if (showHeader) _buildSalesHeader(context),
          Expanded(child: _buildSelectedTab(context)),
        ],
      ),
    );
  }

  Widget _buildDisplayModeSelector(BuildContext context) {
    final modeButtons = <SalesViewMode, String>{
      SalesViewMode.tabs: 'التبويبات',
      SalesViewMode.sessions: 'الشريط',
      SalesViewMode.all: 'الكل',
      SalesViewMode.fullscreen: 'ملء الشاشة',
    };
    final isLightTheme = Theme.of(context).brightness == Brightness.light;
    final chipBackground = isLightTheme ? const Color(0xFFEAF1F7) : const Color(0xFF1A2633);
    final chipBorder = isLightTheme ? const Color(0xFFD8E1F0) : const Color(0xFF32475E);
    final textColor = isLightTheme ? const Color(0xFF0F172A) : const Color(0xFFEAF2FF);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: Wrap(
        spacing: 8,
        runSpacing: 8,
        alignment: WrapAlignment.start,
        children: modeButtons.entries.map((entry) {
          final selected = _salesViewMode == entry.key;
          return ChoiceChip(
            label: Text(entry.value),
            selected: selected,
            showCheckmark: false,
            onSelected: (_) => _applySalesViewMode(entry.key),
            selectedColor: const Color.fromARGB(255, 2, 225, 180),
            backgroundColor: chipBackground,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            labelStyle: TextStyle(
              color: selected ? const Color.fromARGB(255, 0, 0, 0) : textColor,
              fontWeight: FontWeight.w700,
              fontSize: _supportingFontSize,
            ),
            side: BorderSide(
              color: selected ? const Color.fromARGB(255, 2, 225, 180) : chipBorder,
            ),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(5),
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildSalesHeader(BuildContext context) {
    final theme = Theme.of(context);
    final isLightTheme = theme.brightness == Brightness.light;
    final headerColors = isLightTheme
        ? <Color>[const Color(0xFFEAF3FF), const Color(0xFFDBF4EF), const Color(0xFFDDECF7)]
        : const <Color>[Color.fromARGB(255, 10, 16, 22), Color.fromARGB(255, 10, 16, 22), Color.fromARGB(255, 10, 16, 22)];
    final textColor = isLightTheme ? const Color(0xFF0F172A) : _textMain;
    return Container(
      height: 30,
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.centerLeft,
          end: Alignment.centerRight,
          colors: headerColors,
        ),
        border: Border.all(
          color: isLightTheme ? const Color(0xFFD8E1F0) : _primaryBlue,
          width: 1,
        ),
        borderRadius: BorderRadius.circular(5),
        boxShadow: const [
          BoxShadow(
            color: Color(0x1F1D4ED8),
            offset: Offset(0, 2),
            blurRadius: 8,
          ),
        ],
      ),
      child: Row(children: [
        FilledButton.icon(
          style: FilledButton.styleFrom(
            backgroundColor: const Color.fromARGB(255, 2, 225, 180),
            foregroundColor: const Color.fromARGB(255, 0, 0, 0),
            padding: const EdgeInsets.symmetric(horizontal: 16),
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(5),
            ),
          ),
          onPressed: _startNewSession,
          iconAlignment: IconAlignment.end,
          icon: const Icon(Icons.add, size: 20, color: Color.fromARGB(255, 23, 22, 37)),
          label: const Text('جلسة جديدة'),
        ),
        const SizedBox(width: 8), 
        Text(
          '',
          style: UiPalette.adaptiveTextStyle(
            context,
            backgroundColor: const Color(0xFF1D4ED8),
            fontSize: _headerTitleFontSize,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: _drafts.length,
            separatorBuilder: (_, __) => const SizedBox(width: 4),
            itemBuilder: (context, index) {
              final draft = _drafts[index];
              final label = draft.customerName.trim().isNotEmpty
                  ? draft.customerName
                  : 'جلسة ${draft.number}';

              return InkWell(
                onTap: () => _restoreDraft(index),
                borderRadius: BorderRadius.circular(5),
                child: Container(
                  height: 32,
                  padding: const EdgeInsets.symmetric(horizontal: 8),
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: const Color(0xFF1A2633),
                    borderRadius: BorderRadius.circular(5),
                    border: Border.all(color: const Color(0xFF32475E)),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      IconButton(
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(minWidth: 18, minHeight: 18),
                        splashRadius: 12,
                        tooltip: 'حذف الجلسة',
                        onPressed: () => _cancelDraft(index),
                        icon: const Icon(Icons.close, size: 12, color: _textSoft),
                      ),
                      const SizedBox(width: 1),
                      Text(
                        label,
                        style: theme.textTheme.bodySmall?.copyWith(
                          fontSize: _supportingFontSize,
                          fontWeight: FontWeight.w700,
                          color: textColor,
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ]),
    );
  }

  Widget _buildSalesTabs(BuildContext context) {
    return Container(
      color: const Color(0xFF101820),
      child: Row(children: [
        _salesTab(context, 0, 'مبيعات التفصيل'),
        _salesTab(context, 1, 'المبيعات الجاهزة'),
        _salesTab(context, 2, 'أوامر الإنتاج الجاهز'),
      ]),
    );
  }

  Widget _salesTab(BuildContext context, int index, String label) {
    final selected = _selectedTab == index;
    return Expanded(
      child: InkWell(
        onTap: () => setState(() => _selectedTab = index),
        child: Container(
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            border: Border(
              bottom: BorderSide(
                color: selected ? _primaryBlue : _borderSoft,
                width: selected ? 4 : 1,
              ),
            ),
          ),
          child: Text(
            label,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: selected ? const Color.fromARGB(255, 13, 25, 23) : const Color.fromARGB(255, 12, 26, 23),
              fontSize: _tabFontSize,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildSelectedTab(BuildContext context) {
    if (_selectedTab == 1) {
      return const Padding(padding: EdgeInsets.all(8), child: ReadySalesScreen());
    }
    if (_selectedTab == 2) {
      return const Padding(padding: EdgeInsets.all(8), child: ReadyMadeProductionScreen());
    }
    final theme = Theme.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsets.all(8),
      child: Column(children: [
        _buildDashboardCards(context),
        const SizedBox(height: 8),
        _buildMainTopSection(context),
        const SizedBox(height: 10),
        SizedBox(
          width: double.infinity,
          height: 46,
          child: FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: const Color.fromARGB(255, 2, 225, 180),
              foregroundColor: const Color.fromARGB(255, 0, 0, 0),
              padding: const EdgeInsets.symmetric(horizontal: 16),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(5),
              ),
            ),
            onPressed: _savingOrder ? null : saveOrder,
            child: _savingOrder
                ? SizedBox.square(
                    dimension: 18,
                    child: CircularProgressIndicator(strokeWidth: 2, color: theme.colorScheme.onPrimary),
                  )
                : const Text('حفظ الطلب'),
          ),
        ),
      ]),
    );
  }

  void _startNewSession() {
    FocusScope.of(context).unfocus();
    setState(() {
      _drafts.add(_captureDraft(_nextSessionNumber++));
      _clearCurrentSession();
    });
    loadMeasurementPreview();
    _showMessage('حُفظت الجلسة الحالية كمسودة محلية.');
  }

  _SalesDraft _captureDraft(int number) => _SalesDraft(
        number: number,
        customerId: currentCustomerId,
        customerFound: customerFound,
        customerName: customerNameController.text,
        customerCode: customerCodeController.text,
        phone: phoneController.text,
        referrer: referrerController.text,
        total: totalAmountController.text,
        paid: paidAmountController.text,
        discount: discountAmountController.text,
        remaining: remainingAmountController.text,
        deliveryDate: deliveryDateController.text,
        pieceTypes: List.of(pieceTypes),
        quantities: quantityControllers.map((item) => item.text).toList(),
        fabricCodes: fabricCodeControllers.map((item) => item.text).toList(),
        fabricTypes: fabricTypeControllers.map((item) => item.text).toList(),
        fabricColors: fabricColorControllers.map((item) => item.text).toList(),
        notes1: notes1Controllers.map((item) => item.text).toList(),
        notes2: notes2Controllers.map((item) => item.text).toList(),
        specialRequests:
            specialRequestsControllers.map((item) => item.text).toList(),
        measurements: {
          for (final entry in measurementValuesByType.entries)
            entry.key: Map<String, String>.from(entry.value)
        },
      );

  void _clearCurrentSession() {
    currentCustomerId = 0;
    customerFound = false;
    currentCustomerName = '';
    currentCustomerCode = '';
    currentCustomerPhone = '';
    piecesCount = 1;
    _selectedPieceIndex = null;
    _resizePieceControllers(1);
    pieceTypes[0] = _defaultPieceType;
    for (final controller in [
      customerNameController,
      customerCodeController,
      customerIdController,
      phoneController,
      referrerController,
      totalAmountController,
      paidAmountController,
      discountAmountController,
      remainingAmountController,
      deliveryDateController
    ]) {
      controller.clear();
    }
    quantityControllers[0].text = '1';
    for (final controller in [
      fabricCodeControllers[0],
      fabricTypeControllers[0],
      fabricColorControllers[0],
      notes1Controllers[0],
      notes2Controllers[0],
      specialRequestsControllers[0]
    ]) {
      controller.clear();
    }
    measurementValuesByType.clear();
  }

  void _restoreDraft(int index) {
    final draft = _drafts.removeAt(index);
    setState(() {
      currentCustomerId = draft.customerId;
      customerFound = draft.customerFound;
      currentCustomerName = draft.customerName;
      currentCustomerCode = draft.customerCode;
      currentCustomerPhone = draft.phone;
      piecesCount = draft.pieceTypes.length;
      _selectedPieceIndex = null;
      _resizePieceControllers(piecesCount);
      pieceTypes.setAll(0, draft.pieceTypes);
      _setControllerValues(quantityControllers, draft.quantities);
      _setControllerValues(fabricCodeControllers, draft.fabricCodes);
      _setControllerValues(fabricTypeControllers, draft.fabricTypes);
      _setControllerValues(fabricColorControllers, draft.fabricColors);
      _setControllerValues(notes1Controllers, draft.notes1);
      _setControllerValues(notes2Controllers, draft.notes2);
      _setControllerValues(specialRequestsControllers, draft.specialRequests);
      customerNameController.text = draft.customerName;
      customerCodeController.text = draft.customerCode;
      customerIdController.text =
          draft.customerId == 0 ? '' : '${draft.customerId}';
      phoneController.text = draft.phone;
      referrerController.text = draft.referrer;
      totalAmountController.text = draft.total;
      paidAmountController.text = draft.paid;
      discountAmountController.text = draft.discount;
      remainingAmountController.text = draft.remaining;
      deliveryDateController.text = draft.deliveryDate;
      measurementValuesByType
        ..clear()
        ..addAll({
          for (final entry in draft.measurements.entries)
            entry.key: Map<String, String>.from(entry.value)
        });
    });
  }

  void _cancelDraft(int index) => setState(() => _drafts.removeAt(index));

  void _setControllerValues(
      List<TextEditingController> controllers, List<String> values) {
    for (var index = 0; index < values.length; index++) {
      controllers[index].text = values[index];
    }
  }

  void _resizePieceControllers(int count) {
    piecesCount = count;
    _syncPieceTypesWithCount();
  }

  Widget _buildDashboardCards(BuildContext context) => LayoutBuilder(
        builder: (context, constraints) {
          final now = DateTime.now();
          final cards = <Widget>[
            _statValue(context, 'تاريخ اليوم', intl.DateFormat('yyyy/MM/dd').format(now)),
            _statValue(context, 'اسم اليوم', _arabicDayName(now.weekday)),
            _statValue(context, 'إجمالي الطلبات', totalOrdersCount),
            _statValue(context, 'طلبات اليوم', todayOrdersCount),
            _statInput(context, 'إجمالي الربح', profitAmountController, readOnly: true),
            _statInput(context, 'نسبة ربح الطلب', profitPercentageController, suffix: '%', onChanged: (_) => _updateProfitAmount()),
          ];
          final columns = constraints.maxWidth < 900 ? 2 : 6;
          const spacing = 6.0;
          final width = (constraints.maxWidth - spacing * (columns - 1)) / columns;
          return Wrap(
            spacing: spacing,
            runSpacing: 6,
            children: cards.map((card) => SizedBox(width: width, child: card)).toList(),
          );
        },
      );

  Widget _statInput(BuildContext context, String label, TextEditingController controller,
      {bool readOnly = false, String? suffix, ValueChanged<String>? onChanged}) {
    final theme = Theme.of(context);
    return SizedBox(
      height: _dashboardCardHeight,
      child: Card(
        elevation: 0,
        color: theme.colorScheme.surfaceContainer,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(5),
          side: BorderSide(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.6)),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
          child: TextFormField(
            controller: controller,
            readOnly: readOnly,
            onChanged: onChanged,
            textAlign: TextAlign.center,
            keyboardType: TextInputType.number,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: const Color.fromARGB(255, 18, 28, 40),
              fontSize: _kpiValueFontSize,
              fontWeight: FontWeight.bold,
            ),
            decoration: _statDecoration(context, label, suffix),
          ),
        ),
      ),
    );
  }

  Widget _statValue(BuildContext context, String label, String value) {
    final theme = Theme.of(context);
    return SizedBox(
      height: _dashboardCardHeight,
      child: Card(
        elevation: 0,
        color: theme.colorScheme.surfaceContainer,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(5),
          side: BorderSide(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.6)),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
          child: InputDecorator(
            decoration: _statDecoration(context, label, null),
            child: Center(child: Text(value, style: theme.textTheme.bodyMedium?.copyWith(fontSize: _kpiValueFontSize, fontWeight: FontWeight.bold))),
          ),
        ),
      ),
    );
  }

  InputDecoration _statDecoration(BuildContext context, String label, String? suffix) {
    final theme = Theme.of(context);
    return InputDecoration(
      labelText: label,
      suffixText: suffix,
      floatingLabelBehavior: FloatingLabelBehavior.always,
      labelStyle: theme.textTheme.labelSmall?.copyWith(fontSize: _fieldLabelFontSize, color: _textSoft),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: Color.fromARGB(255, 207, 211, 211))),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: Color.fromARGB(255, 44, 105, 103))),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: _primaryBlue, width: 1.6)),
      isDense: true,
      contentPadding: const EdgeInsets.symmetric(horizontal: 4, vertical: 5),
    );
  }

  String _arabicDayName(int weekday) => const [
        'الاثنين',
        'الثلاثاء',
        'الأربعاء',
        'الخميس',
        'الجمعة',
        'السبت',
        'الأحد'
      ][weekday - 1];

  Widget _buildMainTopSection(BuildContext context) => LayoutBuilder(
        builder: (context, constraints) => constraints.maxWidth < 900
            ? Column(children: [
                _customerCard(context),
                const SizedBox(height: 12),
                _buildPiecesSection(context),
              ])
            : Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: _customerCard(context),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 3,
                    child: _buildPiecesSection(context),
                  ),
                ],
              ),
      );

  Widget _customerCard(BuildContext context) => _darkCard(
        context,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(children: [
              Expanded(child: Text('معلومات العميل', style: _sectionTitleStyle(context))),
              SizedBox(
                height: 32,
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: const Color.fromARGB(255, 2, 225, 180),
                    foregroundColor: Colors.black,
                    padding: const EdgeInsets.symmetric(horizontal: 10),
                    shape: RoundedRectangleBorder(
                      side: const BorderSide(color: Colors.black, width: 1.1),
                      borderRadius: BorderRadius.circular(5),
                    ),
                  ),
                  onPressed: () {
                    _clearCustomer();
                    _fillCustomerControllers();
                    FocusScope.of(context).requestFocus();
                  },
                  icon: const Icon(Icons.person_add_alt_1, size: 15, color: Colors.black),
                  label: const Text('إنشاء عميل', style: TextStyle(fontSize: _buttonFontSize, color: Colors.black)),
                ),
              ),
            ]),
            const SizedBox(height: 8),
            _customerField(context, 'رقم الهاتف', phoneController,
                onChanged: (value) => currentCustomerPhone = value,
                onEditingComplete: () => _searchCustomerAndNextFocus(phoneController.text.trim())),
            _customerField(context, 'اسم العميل', customerNameController,
                onEditingComplete: () => _searchCustomerAndNextFocus(customerNameController.text.trim())),
            _customerField(context, 'كود العميل', customerCodeController,
                onEditingComplete: () => _searchCustomerAndNextFocus(customerCodeController.text.trim())),
            _customerField(context, 'العميل المعرف', referrerController),
            _customerField(context, 'صلة القرابة', null),
            _customerField(context, 'النقاط الحالية', currentPointsController, readOnly: true),
            _customerField(context, 'النقاط التراكمية', accumulatedPointsController, readOnly: true),
            _customerField(context, 'عدد العملاء في الشجرة', treeCustomerCountController, readOnly: true),
            Divider(height: 14, thickness: 1, color: Theme.of(context).colorScheme.outlineVariant),
            _customerField(context, 'القيمة الإجمالية', totalAmountController, onChanged: _updateRemainingAmount),
            _customerField(context, 'المدفوع مقدماً', paidAmountController, onChanged: _updateRemainingAmount),
            _customerField(context, 'الخصم', discountAmountController, onChanged: _updateRemainingAmount),
            _customerField(context, 'المتبقي بعد الخصم', remainingAmountController, readOnly: true),
            _customerField(context, 'تاريخ الاستلام YYYY-MM-DD', deliveryDateController),
          ],
        ),
      );

  Widget _customerField(BuildContext context, String label, TextEditingController? controller,
      {bool readOnly = false, ValueChanged<String>? onChanged, VoidCallback? onEditingComplete}) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: TextFormField(
        controller: controller,
        readOnly: readOnly,
        onChanged: onChanged,
        onEditingComplete: onEditingComplete ?? () => FocusScope.of(context).nextFocus(),
        textInputAction: TextInputAction.next,
        textAlign: TextAlign.right,
        decoration: InputDecoration(
          labelText: label,
          labelStyle: TextStyle(
            fontSize: _fieldLabelFontSize,
            color: UiPalette.adaptiveTextColor(_surfaceCard),
          ),
          filled: true,
          fillColor: const Color.fromARGB(255, 13, 19, 28),
          isDense: true,
          contentPadding: const EdgeInsets.symmetric(horizontal: 5, vertical: 8),
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: Color.fromARGB(255, 43, 104, 101))),
          enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: _borderSoft)),
          focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: _primaryBlue, width: 1.5)),
        ),
        style: theme.textTheme.bodyMedium?.copyWith(fontSize: _fieldFontSize, color: _textMain),
      ),
    );
  }

  Widget _buildPiecesSection(BuildContext context) {
    return _darkCard(
      context,
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (int index = 1; index <= piecesCount; index++) ...[
            _pieceCard(context, index),
            if (index != piecesCount) const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }

  Future<void> _addPiece() async {
    setState(() {
      piecesCount++;
      _syncPieceTypesWithCount();
    });
    await loadMeasurementPreview();
  }

  Future<void> _removePiece(int pieceIndex) async {
    if (piecesCount == 1) return;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('حذف القطعة'),
        content: Text('هل تريد حذف القطعة رقم ${pieceIndex + 1}؟'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('إلغاء')),
          FilledButton(
              onPressed: () => Navigator.pop(dialogContext, true),
              child: const Text('حذف')),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() {
      pieceTypes.removeAt(pieceIndex);
      for (final controllers in [
        quantityControllers,
        fabricCodeControllers,
        fabricTypeControllers,
        fabricColorControllers,
        notes1Controllers,
        notes2Controllers,
        specialRequestsControllers
      ]) {
        controllers.removeAt(pieceIndex).dispose();
      }
      piecesCount--;
      _selectedPieceIndex = null;
    });
    await loadMeasurementPreview();
  }

  Future<void> _openMeasurements(int pieceIndex) async {
    final pieceType = pieceTypes[pieceIndex];
    if (pieceType.isEmpty) return;
    final fields = _measurementFieldsForPiece(pieceType);
    final values = await AppNavigation.push<Map<String, String>>(
      context,
      (_) => MeasurementEntryScreen(
        pieceType: pieceType,
        fields: fields,
        initialValues: measurementValuesByType[pieceType] ?? const {},
        onSave: currentCustomerId > 0
            ? (values) => MeasurementsApi(baseUrl: _baseUrl)
                .upsert(currentCustomerId, pieceType, values)
            : null,
      ),
    );
    if (values != null && mounted) {
      setState(() => measurementValuesByType[pieceType] = values);
    }
  }

  Widget _pieceCard(BuildContext context, int index) {
    final pieceIndex = index - 1;
    final pieceType = pieceTypes[pieceIndex];
    final selected = _selectedPieceIndex == pieceIndex;
    final theme = Theme.of(context);
    final requiredConsumption = _estimatedConsumptionForPiece(pieceIndex);
    final availableInches = _fabricAvailableInches(pieceIndex);
    final isFabricShortage = availableInches != null && availableInches < requiredConsumption;
    final isMeasurementsHidden = _hiddenMeasurementRows[pieceIndex] ?? false;
    final canToggleMeasurements = pieceType.isNotEmpty;

    return Card(
      elevation: 1.5,
      color: _surfaceCard,
      shadowColor: Colors.black.withValues(alpha: 0.12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(5),
        side: BorderSide(
          color: selected ? const Color(0xFF7DE7D1) : const Color.fromARGB(255, 102, 203, 197),
          width: selected ? 1.8 : 1,
        ),
      ),
      child: InkWell(
        splashFactory: NoSplash.splashFactory,
        splashColor: Colors.transparent,
        highlightColor: Colors.transparent,
        hoverColor: Colors.transparent,
        focusColor: Colors.transparent,
        overlayColor: const WidgetStatePropertyAll(Colors.transparent),
        onTap: pieceType.isEmpty ? null : () => setState(() => _selectedPieceIndex = pieceIndex),
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(
                      color: const Color.fromARGB(255, 10, 18, 28),
                      border: Border.all(color: const Color(0xFF7DE7D1), width: 1.2),
                      borderRadius: BorderRadius.circular(5),
                    ),
                    child: Text(
                      'قطعة رقم $index',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: const Color.fromARGB(255, 10, 18, 28),
                        fontSize: _sectionTitleFontSize,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  const Spacer(),
                  if (index == 1)
                    Expanded(
                      child: SizedBox(
                        height: 34,
                        child: FilledButton.icon(
                          style: FilledButton.styleFrom(
                            backgroundColor: _primaryBlue,
                            foregroundColor: const Color.fromARGB(255, 0, 0, 1),
                            padding: const EdgeInsets.symmetric(horizontal: 10),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(5)),
                          ),
                          onPressed: _addPiece,
                          icon: const Icon(Icons.add, size: 16),
                          label: const Text('إضافة قطعة', style: TextStyle(fontSize: 11)),
                        ),
                      ),
                    ),
                  if (index == 1) const SizedBox(width: 8),
                  if (canToggleMeasurements) ...[
                    const SizedBox(width: 8),
                    Container(
                      decoration: BoxDecoration(
                        border: Border.all(color: const Color(0xFF7DE7D1), width: 1.2),
                        borderRadius: BorderRadius.circular(5),
                        color: const Color.fromARGB(255, 10, 18, 28),
                      ),
                      child: IconButton(
                        tooltip: isMeasurementsHidden ? 'إظهار صف المقاسات' : 'إخفاء صف المقاسات',
                        visualDensity: VisualDensity.compact,
                        color: isMeasurementsHidden ? theme.colorScheme.primary : theme.colorScheme.onSurface,
                        onPressed: () => setState(() {
                          _hiddenMeasurementRows[pieceIndex] = !isMeasurementsHidden;
                        }),
                        icon: Icon(
                          isMeasurementsHidden ? Icons.visibility_outlined : Icons.visibility_off_outlined,
                          size: 18,
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(width: 8),
                  Container(
                    decoration: BoxDecoration(
                      border: Border.all(color: const Color(0xFF7DE7D1), width: 1.2),
                      borderRadius: BorderRadius.circular(5),
                      color: const Color.fromARGB(255, 10, 18, 28),
                    ),
                    child: IconButton(
                      tooltip: 'حذف القطعة',
                      visualDensity: VisualDensity.compact,
                      color: theme.colorScheme.error,
                      onPressed: piecesCount == 1 ? null : () => _removePiece(pieceIndex),
                      icon: const Icon(Icons.delete_outline, size: 18),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              FocusTraversalGroup(
                policy: OrderedTraversalPolicy(),
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    const spacing = 6.0;
                    final row1Columns = constraints.maxWidth < 700 ? 2 : 7;
                    final row1Width = (constraints.maxWidth - spacing * (row1Columns - 1)) / row1Columns;
                    final row2Columns = constraints.maxWidth < 600 ? 2 : 6;
                    final row2Width = (constraints.maxWidth - spacing * (row2Columns - 1)) / row2Columns;

                    final row1Fields = <Widget>[
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(1),
                        child: Focus(
                          onKeyEvent: (_, event) => _moveToNextFieldOnEnter(context, event),
                          onFocusChange: (hasFocus) => _setEditableFieldFocusState(pieceIndex, 'type', hasFocus),
                          child: DropdownButtonFormField<String>(
                            initialValue: pieceType.isEmpty ? null : pieceType,
                            hint: const Text('اختر', style: TextStyle(fontSize: _fieldLabelFontSize)),
                            isExpanded: true,
                            dropdownColor: const Color(0xFF101820),
                            style: theme.textTheme.bodySmall?.copyWith(fontSize: _fieldFontSize, color: _textMain),
                            decoration: _pieceDecoration(context, 'نوع القطعة', selected: _isEditableFieldFocused(pieceIndex, 'type')),
                            items: _pieceTypeOptions
                                .map((type) => DropdownMenuItem(value: type, child: Text(type, maxLines: 1, overflow: TextOverflow.ellipsis)))
                                .toList(),
                            onChanged: (value) async {
                              if (value == null) return;
                              setState(() {
                                pieceTypes[pieceIndex] = value;
                                _selectedPieceIndex = pieceIndex;
                              });
                              await loadMeasurementPreview();
                            },
                          ),
                        ),
                      ),
                      FocusTraversalOrder(order: const NumericFocusOrder(2), child: _legacyReadOnlyField(context, 'النقاط المتوقعة', '')),
                      FocusTraversalOrder(order: const NumericFocusOrder(3), child: _pieceField(context, 'عدد القطع', quantityControllers[pieceIndex], number: true, pieceIndex: pieceIndex, fieldKey: 'quantity')),
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(4),
                        child: _pieceField(
                          context,
                          'كود القماش',
                          fabricCodeControllers[pieceIndex],
                          pieceIndex: pieceIndex,
                          fieldKey: 'fabricCode',
                          onChanged: (_) async {
                            setState(() {});
                            await _syncFabricDataForPiece(pieceIndex);
                          },
                        ),
                      ),
                      FocusTraversalOrder(order: const NumericFocusOrder(5), child: _legacyReadOnlyField(context, 'نوع القماش', fabricTypeControllers[pieceIndex].text)),
                      FocusTraversalOrder(order: const NumericFocusOrder(6), child: _legacyReadOnlyField(context, 'لون القماش', fabricColorControllers[pieceIndex].text)),
                      FocusTraversalOrder(order: const NumericFocusOrder(7), child: _legacyReadOnlyField(context, 'رقم الكتالوج', catalogNumberControllers[pieceIndex].text)),
                    ];

                    final row2Fields = <Widget>[
                      FocusTraversalOrder(order: const NumericFocusOrder(8), child: _pieceField(context, 'طلب رقم 1', notes1Controllers[pieceIndex], pieceIndex: pieceIndex, fieldKey: 'notes1')),
                      FocusTraversalOrder(order: const NumericFocusOrder(9), child: _pieceField(context, 'طلب رقم 2', notes2Controllers[pieceIndex], pieceIndex: pieceIndex, fieldKey: 'notes2')),
                      FocusTraversalOrder(order: const NumericFocusOrder(10), child: _pieceField(context, 'طلبات خاصة', specialRequestsControllers[pieceIndex], pieceIndex: pieceIndex, fieldKey: 'specialRequests')),
                      _legacyReadOnlyField(context, 'الاستهلاك', requiredConsumption.toStringAsFixed(1)),
                      _fabricAvailableField(context, pieceIndex, availableInches, requiredConsumption, isFabricShortage),
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(18),
                        child: SizedBox(
                          height: 30,
                          child: FilledButton.icon(
                            style: FilledButton.styleFrom(
                              backgroundColor: const Color.fromARGB(255, 2, 225, 180),
                              foregroundColor: const Color.fromARGB(255, 0, 6, 4),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(5),
                              ),
                            ),
                            onPressed: pieceType.isEmpty
                                ? null
                                : () {
                                    setState(() => _selectedPieceIndex = pieceIndex);
                                    _openMeasurements(pieceIndex);
                                  },
                            icon: const Icon(Icons.straighten, size: 24),
                            label: const Text('المقاسات', maxLines: 1, overflow: TextOverflow.ellipsis),
                          ),
                        ),
                      ),
                    ];

                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Wrap(
                          spacing: spacing,
                          runSpacing: 1,
                          children: row1Fields.map((field) => SizedBox(width: row1Width, height: 45, child: field)).toList(),
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: spacing,
                          runSpacing: 1,
                          children: row2Fields.map((field) => SizedBox(width: row2Width, height: 45, child: field)).toList(),
                        ),
                      ],
                    );
                  },
                ),
              ),
              if (pieceType.isNotEmpty && !(_hiddenMeasurementRows[pieceIndex] ?? false)) ...[
                const SizedBox(height: 10),
                _buildInlineMeasurements(context, pieceType),
              ],
            ],
          ),
        ),
      ),
    );
  }

  double? _fabricAvailableInches(int pieceIndex) {
    final code = fabricCodeControllers[pieceIndex].text.trim();
    if (code.isEmpty) return null;
    final snapshot = _fabricStockByCode[code.toUpperCase()];
    return snapshot?.availableInches;
  }

  Widget _fabricAvailableField(BuildContext context, int pieceIndex, double? availableInches, double requiredConsumption, bool isShortage) {
    final value = availableInches == null ? '---' : availableInches.toStringAsFixed(1);
    return Container(
      height: 30,
      padding: const EdgeInsets.all(1),
      decoration: BoxDecoration(
        color: isShortage ? const Color.fromARGB(255, 189, 71, 2) : const Color.fromARGB(255, 5, 246, 130),
        borderRadius: BorderRadius.circular(5),
        border: Border.all(
          color: isShortage ? const Color(0xFFFFC857) : const Color.fromARGB(255, 11, 79, 33),
          width: isShortage ? 1.4 : 1,
        ),
      ),
      child: InputDecorator(
        decoration: _pieceDecoration(context, 'الكمية المتوفرة').copyWith(
          filled: true,
          fillColor: isShortage ? const Color.fromARGB(255, 1, 16, 31) : const Color.fromARGB(255, 1, 16, 31),
          contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                fontSize: _fieldFontSize,
                color: isShortage ? const Color(0xFFFFF1C2) : Theme.of(context).colorScheme.onSurfaceVariant,
                fontWeight: FontWeight.w700,
              ),
            ),
            if (isShortage)
              Text(
                'لا يكفي',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 8,
                  color: Color(0xFFFFE082),
                  fontWeight: FontWeight.w600,
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _pieceField(
    BuildContext context,
    String label,
    TextEditingController controller, {
    bool number = false,
    int? pieceIndex,
    String? fieldKey,
    ValueChanged<String>? onChanged,
  }) {
    final key = pieceIndex == null || fieldKey == null ? label : 'piece_${pieceIndex}_$fieldKey';
    final isFocused = _focusedEditableFieldStates[key] ?? false;
    return Focus(
      onFocusChange: (hasFocus) => _setEditableFieldFocusState(pieceIndex, fieldKey, hasFocus),
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        decoration: _pieceDecoration(context, label, selected: isFocused),
        keyboardType: number ? TextInputType.number : null,
        textInputAction: TextInputAction.next,
        onSubmitted: (_) => FocusScope.of(context).nextFocus(),
        style: Theme.of(context).textTheme.bodySmall?.copyWith(fontSize: _fieldFontSize, color: _textMain),
      ),
    );
  }

  Widget _legacyReadOnlyField(BuildContext context, String label, String value) => InputDecorator(
        decoration: _pieceDecoration(context, label),
        child: Text(
          value,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(fontSize: _fieldFontSize, color: Theme.of(context).colorScheme.onSurfaceVariant),
        ),
      );

  KeyEventResult _moveToNextFieldOnEnter(BuildContext context, KeyEvent event) {
    if (event is KeyDownEvent &&
        (event.logicalKey == LogicalKeyboardKey.enter ||
            event.logicalKey == LogicalKeyboardKey.numpadEnter)) {
      FocusScope.of(context).nextFocus();
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  Widget _buildInlineMeasurements(BuildContext context, String pieceType) {
    final fields = _measurementFieldsForPiece(pieceType);
    if (fields.isEmpty) return const SizedBox.shrink();
    return LayoutBuilder(builder: (context, constraints) {
      const spacing = 4.0;
      final count = fields.length;
      final totalSpacing = spacing * (count > 0 ? count - 1 : 0);
      final itemWidth = count == 0 ? 0.0 : (constraints.maxWidth - totalSpacing) / count;
      return Wrap(
        spacing: spacing,
        runSpacing: 1,
        children: fields
            .map((field) => SizedBox(
                  width: itemWidth,
                  height: 50,
                  child: _measurementSmallField(
                      context: context,
                      label: field,
                      value: measurementValuesByType[pieceType]?[field] ?? '---'),
                ))
            .toList(),
      );
    });
  }

  Widget _measurementSmallField({required BuildContext context, required String label, required String value}) => InputDecorator(
        decoration: _pieceDecoration(context, label).copyWith(contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14)),
        child: Text(
          _formatInches(value),
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(fontSize: _fieldFontSize, color: Theme.of(context).colorScheme.onSurfaceVariant),
        ),
      );

  String _formatInches(String value) {
    if (value == '---' || value.trim().isEmpty) return '---';
    final number = double.tryParse(value.replaceAll(',', '.'));
    if (number == null) return value;
    final formatted = number == number.truncateToDouble()
        ? number.toInt().toString()
        : number
            .toString()
            .replaceFirst(RegExp(r'0+$'), '')
            .replaceFirst(RegExp(r'\.$'), '');
    return formatted;
  }

  InputDecoration _pieceDecoration(BuildContext context, String label, {bool selected = false}) {
    final theme = Theme.of(context);
    final isLightTheme = theme.brightness == Brightness.light;
    final borderColor = selected ? const Color(0xFF7DE7D1) : (isLightTheme ? const Color(0xFFB7C9D9) : const Color.fromARGB(255, 46, 93, 100));
    final fillColor = isLightTheme ? const Color(0xFFF8FAFC) : const Color.fromARGB(255, 3, 15, 28);
    final labelColor = isLightTheme ? const Color(0xFF475569) : _textSoft;
    return InputDecoration(
      labelText: label,
      labelStyle: theme.textTheme.labelSmall?.copyWith(fontSize: _fieldLabelFontSize, color: labelColor),
      floatingLabelBehavior: FloatingLabelBehavior.always,
      filled: true,
      fillColor: fillColor,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: BorderSide(color: borderColor)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: BorderSide(color: borderColor, width: 1.6)),
      isDense: true,
      contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 14),
    );
  }

  void _setEditableFieldFocusState(int? pieceIndex, String? fieldKey, bool hasFocus) {
    if (pieceIndex == null || fieldKey == null) return;
    final key = 'piece_${pieceIndex}_$fieldKey';
    if (!mounted) return;
    setState(() {
      if (hasFocus) {
        _focusedEditableFieldStates[key] = true;
      } else {
        _focusedEditableFieldStates.remove(key);
      }
    });
  }

  bool _isEditableFieldFocused(int pieceIndex, String fieldKey) {
    return _focusedEditableFieldStates['piece_${pieceIndex}_$fieldKey'] ?? false;
  }

  Widget _darkCard(BuildContext context, Widget child) {
    final isLightTheme = Theme.of(context).brightness == Brightness.light;
    return Card(
      elevation: 1.5,
      color: isLightTheme ? const Color(0xFFFFFFFF) : _surfaceCard,
      shadowColor: Colors.black.withValues(alpha: isLightTheme ? 0.04 : 0.10),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(5),
        side: BorderSide(color: (isLightTheme ? const Color(0xFFE2EAF5) : _borderSoft).withValues(alpha: 0.7)),
      ),
      child: Padding(padding: const EdgeInsets.all(10), child: child),
    );
  }

  TextStyle _sectionTitleStyle(BuildContext context) {
    return UiPalette.adaptiveTextStyle(
      context,
      backgroundColor: const Color.fromARGB(255, 10, 15, 21),
      fontSize: _sectionTitleFontSize,
      fontWeight: FontWeight.w700,
    );
  }
}

class _FabricStockSnapshot {
  const _FabricStockSnapshot({
    required this.fabricType,
    required this.fabricColor,
    required this.catalogNumber,
    required this.availableInches,
  });

  final String fabricType;
  final String fabricColor;
  final String catalogNumber;
  final double availableInches;
}

class _SalesDraft {
  const _SalesDraft({
    required this.number,
    required this.customerId,
    required this.customerFound,
    required this.customerName,
    required this.customerCode,
    required this.phone,
    required this.referrer,
    required this.total,
    required this.paid,
    required this.discount,
    required this.remaining,
    required this.deliveryDate,
    required this.pieceTypes,
    required this.quantities,
    required this.fabricCodes,
    required this.fabricTypes,
    required this.fabricColors,
    required this.notes1,
    required this.notes2,
    required this.specialRequests,
    required this.measurements,
  });

  final int number;
  final int customerId;
  final bool customerFound;
  final String customerName;
  final String customerCode;
  final String phone;
  final String referrer;
  final String total;
  final String paid;
  final String discount;
  final String remaining;
  final String deliveryDate;
  final List<String> pieceTypes;
  final List<String> quantities;
  final List<String> fabricCodes;
  final List<String> fabricTypes;
  final List<String> fabricColors;
  final List<String> notes1;
  final List<String> notes2;
  final List<String> specialRequests;
  final Map<String, Map<String, String>> measurements;
}
