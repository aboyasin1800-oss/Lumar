import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' as intl;

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';
import '../models/customer_creation_models.dart';
import '../repositories/loyalty_repository.dart';
import '../services/screen_chrome_state.dart';
import 'customers/customer_create_screen.dart';
import 'measurements_screen.dart';
import 'ready_made_production_screen.dart';
import 'ready_sales_screen.dart';

enum SalesViewMode {
  tabs,
  sessions,
  all,
  fullscreen,
}

DateTime calculateDeliveryDateFromDays(int days, {DateTime? base}) {
  final anchor = (base ?? DateTime.now()).toLocal();
  final normalized = DateTime(anchor.year, anchor.month, anchor.day);
  return normalized.add(Duration(days: days));
}

final ValueNotifier<SalesViewMode> salesViewModeNotifier =
    ValueNotifier<SalesViewMode>(SalesViewMode.all);

class SalesScreen extends StatefulWidget {
  const SalesScreen({super.key});

  @override
  State<SalesScreen> createState() => _SalesScreenState();
}

class _SalesScreenState extends State<SalesScreen> with WidgetsBindingObserver {
  static const _activeSessionStorageKey = 'lumar_sales_active_session_v1';
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );
  final _sessionStorage = const FlutterSecureStorage();
  static const _defaultPieceType = '';
  static const _fallbackPieceTypeOptions = [
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
  final List<String> _officialPieceTypeOptions = [];
  static const _dashboardCardHeight = 52.0;
  static const _headerTitleFontSize = 12.0;
  static const _sectionTitleFontSize = 16.0;
  static const _tabFontSize = 14.0;
  static const _kpiValueFontSize = 18.0;
  static const _fieldFontSize = 20.0;
  static const _fieldLabelFontSize = 16.0;
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
  bool _isInitialLoading = true;
  bool _isRefreshingOfficialCatalog = false;
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
  final relationshipController = TextEditingController();
  final currentPointsController = TextEditingController();
  final accumulatedPointsController = TextEditingController();
  final treeCustomerCountController = TextEditingController();
  final totalAmountController = TextEditingController();
  final paidAmountController = TextEditingController();
  final discountAmountController = TextEditingController();
  final remainingAmountController = TextEditingController();
  final deliveryDateController = TextEditingController();
  final deliveryDaysController = TextEditingController();
  final profitPercentageController = TextEditingController();
  final profitAmountController = TextEditingController();
  final List<String> pieceTypes = [_defaultPieceType];
  final Map<String, Map<String, String>> measurementValuesByType = {};
  final Map<String, List<String>> _officialMeasurementFieldsByType = {};
  final Map<String, Map<String, String>> _measurementCodesByType = {};
  final Map<String, int> _productTypeIdsByType = {};
  final List<double?> _calculatedConsumptions = [null];
  final List<String> _consumptionUnits = [''];
  final List<String> _consumptionMessages = [''];
  final List<_SalesPricingQuote?> _pricingQuotes = [null];
  final List<int> _pricingVersions = [0];
  final List<double?> _expectedLoyaltyPoints = [0];
  final List<int> _expectedLoyaltyPointVersions = [0];
  final Map<String, _FabricStockSnapshot> _fabricStockByCode = {};
  final Map<String, bool> _focusedEditableFieldStates = {};
  final LoyaltyRepository _loyaltyRepository = LoyaltyRepository();
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
    WidgetsBinding.instance.addObserver(this);
    salesViewModeNotifier.addListener(_handleSalesViewModeChanged);
    _salesViewMode = salesViewModeNotifier.value;
    profitPercentageController.text = profitPercentage;
    profitAmountController.text = profitAmount;
    _fillCustomerControllers();
    unawaited(_restorePersistedSession());
    _initializeScreenData();
  }

  Future<void> _initializeScreenData() async {
    await Future.wait([
      _loadOfficialMeasurementFields(),
      loadMeasurementPreview(),
    ]);
    if (!mounted) return;
    setState(() => _isInitialLoading = false);
  }

  Future<void> searchCustomerByPhone() =>
      searchCustomerByTerm(phoneController.text.trim());

  Future<void> _openCustomerCreation() async {
    final created = await AppNavigation.push<CustomerCreationResult>(
      context,
      (_) => CustomerCreateScreen(initialPhone: phoneController.text.trim()),
    );
    if (!mounted || created == null) return;
    setState(() {
      customerFound = true;
      currentCustomerId = created.customerId;
      currentCustomerName = created.customerName;
      currentCustomerCode = created.customerCode;
      currentCustomerPhone = created.phoneNumber;
      currentPoints = '0';
      accumulatedPoints = _formatCustomerNumber(created.totalPoints);
      treeCustomerCount = '${created.referralCount}';
      referrerController.text = created.referrerCustomerName ?? '';
      relationshipController.text = created.relationshipType ?? '';
    });
    _fillCustomerControllers();
    await _loadCustomerSummary(created.customerId);
    await loadSelectedCustomer();
  }

  String _formatCustomerNumber(num value) =>
      value % 1 == 0 ? value.toInt().toString() : value.toStringAsFixed(2);

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
      await _loadCustomerSummary(currentCustomerId);
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
      currentCustomerPhone = '';
      currentPoints = '0';
      accumulatedPoints = '0';
      treeCustomerCount = '0';
      referrerController.clear();
      relationshipController.clear();
    });
    _updateCurrentOrderPoints();
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
      referrerController.clear();
      relationshipController.clear();
      accumulatedPoints = '0';
      treeCustomerCount = '0';
      currentPoints = '0';
    });
    _updateCurrentOrderPoints();
  }

  Future<void> _loadCustomerSummary(int customerId) async {
    if (customerId <= 0) return;
    try {
      final responses = await Future.wait([
        http.get(Uri.parse('$_baseUrl/customers/$customerId')),
        http.get(Uri.parse('$_baseUrl/customers/$customerId/loyalty')),
        http.get(
          Uri.parse('$_baseUrl/api/referrals/customers/$customerId/tree'),
        ),
      ]);
      final detailsResponse = responses[0];
      if (detailsResponse.statusCode < 200 ||
          detailsResponse.statusCode >= 300) {
        return;
      }
      final details =
          Map<String, dynamic>.from(jsonDecode(detailsResponse.body));
      final loyaltyResponse = responses[1];
      final loyalty = loyaltyResponse.statusCode >= 200 &&
              loyaltyResponse.statusCode < 300 &&
              loyaltyResponse.body.trim().isNotEmpty
          ? Map<String, dynamic>.from(jsonDecode(loyaltyResponse.body))
          : const <String, dynamic>{};
      final treeResponse = responses[2];
      final tree = treeResponse.statusCode >= 200 &&
              treeResponse.statusCode < 300 &&
              treeResponse.body.trim().isNotEmpty
          ? Map<String, dynamic>.from(jsonDecode(treeResponse.body))
          : const <String, dynamic>{};
      final lifetimePoints =
          (loyalty['lifetimeEarnedPoints'] as num?)?.toDouble() ??
              (details['totalPoints'] as num?)?.toDouble() ??
              0;
      final descendantsCount =
          (tree['totalDescendantsCount'] as num?)?.toInt() ?? 0;
      if (!mounted || currentCustomerId != customerId) return;
      setState(() {
        referrerController.text =
            details['parentCustomerName']?.toString() ?? '';
        relationshipController.text =
            details['relationshipType']?.toString() ?? '';
        accumulatedPoints = _formatCustomerNumber(lifetimePoints);
        treeCustomerCount = '$descendantsCount';
        currentPoints = '0';
      });
      _updateCurrentOrderPoints();
      _fillCustomerControllers();
    } catch (error) {
      debugPrint('Failed to load customer sales summary: $error');
    }
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
    currentCustomerPhone = phoneController.text.trim();
    if (currentCustomerPhone.isEmpty) {
      _showMessage('الرجاء إدخال رقم الهاتف');
      return;
    }
    if (!customerFound || currentCustomerId <= 0) {
      _showMessage('أنشئ العميل أولاً من زر إنشاء عميل ثم احفظ الطلب.');
      return;
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
      final productTypeId =
          _productTypeIdsByType[_normalizePieceTypeKey(pieceTypes[index])];
      if (productTypeId == null || productTypeId <= 0) {
        _showMessage(
            'تعذر إنشاء الطلب: نوع القطعة غير مرتبط بمعرف رسمي.');
        return;
      }
      final quantity = int.tryParse(quantityControllers[index].text.trim());
      if (quantity == null || quantity <= 0) {
        _showMessage('أدخل عددًا صحيحًا للقطعة رقم ${index + 1}.');
        return;
      }
      final calculatedConsumption = _calculatedConsumptions[index];
      if (pieceTypes[index].isNotEmpty && calculatedConsumption == null) {
        _showMessage(_consumptionMessages[index].isEmpty
            ? 'تعذر حساب استهلاك القطعة رقم ${index + 1} من القواعد الرسمية.'
            : _consumptionMessages[index]);
        return;
      }
      final fabricCode = fabricCodeControllers[index].text.trim();
      final quote = _pricingQuotes[index];
      if (pieceTypes[index].isEmpty ||
          fabricCode.isEmpty ||
          quote == null ||
          !quote.isReady) {
        _showMessage(quote?.reason ??
            'تعذر احتساب سعر القطعة رقم ${index + 1}. أكمل بيانات القطعة والقماش.');
        return;
      }
      final fabricType = fabricTypeControllers[index].text.trim();
      final fabricColor = fabricColorControllers[index].text.trim();
      final catalogNumber = catalogNumberControllers[index].text.trim();
      final hasFabric = fabricCode.isNotEmpty ||
          fabricType.isNotEmpty ||
          fabricColor.isNotEmpty;
      final request1 = _nullableText(notes1Controllers[index]);
      final request2 = _nullableText(notes2Controllers[index]);
      final specialRequest = _nullableText(specialRequestsControllers[index]);
      final measurementMap = Map<String, dynamic>.from(
          measurementValuesByType[_pieceMeasurementKey(index)] ?? const {});
      if (catalogNumber.isNotEmpty) {
        measurementMap['_catalogNumber'] = catalogNumber;
      }
      if (calculatedConsumption != null) {
        measurementMap['_consumption'] =
            calculatedConsumption.toStringAsFixed(2);
      }
      if (_consumptionUnits[index].isNotEmpty) {
        measurementMap['_consumptionUnit'] = _consumptionUnits[index];
      }
      if (fabricCode.isNotEmpty) measurementMap['fabricCode'] = fabricCode;
      if (fabricType.isNotEmpty) measurementMap['fabricType'] = fabricType;
      if (fabricColor.isNotEmpty) measurementMap['fabricColor'] = fabricColor;
      if (request1 != null) measurementMap['request1'] = request1;
      if (request2 != null) measurementMap['request2'] = request2;
      if (specialRequest != null)
        measurementMap['specialRequest'] = specialRequest;
      items.add({
        'pieceType': pieceTypes[index],
        'productTypeId': productTypeId,
        'quantity': quantity,
        'fabricCode': fabricCode.isEmpty ? null : fabricCode,
        'fabricType': fabricType.isEmpty ? null : fabricType,
        'fabricColor': fabricColor.isEmpty ? null : fabricColor,
        'catalogNumber': catalogNumber.isEmpty ? null : catalogNumber,
        'consumption':
            calculatedConsumption == null ? null : calculatedConsumption,
        'consumptionUnit':
            _consumptionUnits[index].isEmpty ? null : _consumptionUnits[index],
        'request1': request1,
        'request2': request2,
        'specialRequest': specialRequest,
        'notes1': null,
        'notes2': null,
        'measurementSnapshot': jsonEncode(measurementMap),
        if (hasFabric)
          'fabric': {
            'fabricCode': fabricCode.isEmpty ? null : fabricCode,
            'fabricType': fabricType.isEmpty ? null : fabricType,
            'fabricColor': fabricColor.isEmpty ? null : fabricColor,
            'quantity': calculatedConsumption == null
                ? quantity
                : calculatedConsumption * quantity,
            'unit': _consumptionUnits[index].isEmpty
                ? 'Piece'
                : _consumptionUnits[index],
            'unitCost': quote.inchPrice,
          },
      });
    }
    setState(() => _savingOrder = true);
    try {
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
        _showMessage(_apiErrorMessage(
            response.body, 'تعذر حفظ الطلب. تحقق من البيانات وحاول مجددًا.'));
        return;
      }
      final decoded = jsonDecode(response.body) as Map<String, dynamic>;
      await _clearPersistedSession();
      _showMessage('تم حفظ الطلب ${decoded['orderNumber'] ?? ''} بنجاح.');
    } catch (error) {
      debugPrint('Order creation failed: $error');
      _showMessage('تعذر الاتصال بالخادم لحفظ الطلب.');
    } finally {
      if (mounted) setState(() => _savingOrder = false);
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
        (total - discount - paid).toStringAsFixed(2);
  }

  void _syncDeliveryDaysFromDate(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      if (deliveryDaysController.text.trim().isNotEmpty) {
        deliveryDaysController.clear();
      }
      return;
    }

    final parsedDate = DateTime.tryParse(trimmed);
    if (parsedDate == null) {
      return;
    }

    final baseDate = DateTime(
      DateTime.now().year,
      DateTime.now().month,
      DateTime.now().day,
    );
    final deltaDays = parsedDate.difference(baseDate).inDays;
    final nextDaysValue = deltaDays >= 0 ? deltaDays.toString() : '';
    if (deliveryDaysController.text.trim() != nextDaysValue) {
      deliveryDaysController.text = nextDaysValue;
    }
  }

  void _applyDeliveryDaysShortcut(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return;
    }

    final days = int.tryParse(trimmed);
    if (days == null || days < 0) {
      return;
    }

    final nextDate = calculateDeliveryDateFromDays(days);
    final nextDateText = intl.DateFormat('yyyy-MM-dd').format(nextDate);
    if (deliveryDateController.text.trim() != nextDateText) {
      deliveryDateController.text = nextDateText;
    }
  }

  void _recalculateOrderTotal() {
    final total = _pricingQuotes.fold<double>(
      0,
      (sum, quote) =>
          sum + (quote?.isReady == true ? quote!.finalPriceTotal : 0),
    );
    totalAmountController.text = total.toStringAsFixed(2);
    totalAmount = totalAmountController.text;
    _updateRemainingAmount('');
  }

  void _invalidatePricing(int pieceIndex) {
    if (pieceIndex >= _pricingVersions.length) return;
    _pricingVersions[pieceIndex]++;
    _pricingQuotes[pieceIndex] = null;
    _recalculateOrderTotal();
    if (mounted) setState(() {});
  }

  Future<void> _schedulePricing(int pieceIndex) async {
    if (pieceIndex >= _pricingVersions.length) return;
    final version = ++_pricingVersions[pieceIndex];
    _pricingQuotes[pieceIndex] = null;
    _recalculateOrderTotal();
    if (mounted) setState(() {});
    await Future<void>.delayed(const Duration(milliseconds: 300));
    if (!mounted || version != _pricingVersions[pieceIndex]) return;
    await _requestPricing(pieceIndex, version);
  }

  Future<void> _requestPricing(int pieceIndex, int version) async {
    final productTypeId =
        _productTypeIdsByType[_normalizePieceTypeKey(pieceTypes[pieceIndex])];
    final fabricCode = fabricCodeControllers[pieceIndex].text.trim();
    final quantity =
        int.tryParse(quantityControllers[pieceIndex].text.trim()) ?? 0;
    final totalConsumption = _calculatedConsumptions[pieceIndex];
    final unit = _consumptionUnits[pieceIndex];
    if (productTypeId == null ||
        fabricCode.isEmpty ||
        quantity <= 0 ||
        totalConsumption == null) return;
    final response = await http.post(
      Uri.parse('$_baseUrl/pricing-engine/calculate'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'productTypeId': productTypeId,
        'fabricCode': fabricCode,
        'consumption': totalConsumption,
        'consumptionUnit': unit,
        'quantity': quantity,
        'pieceProfitPercentage': 0,
        'globalProfitPercentage': 0,
      }),
    );
    if (!mounted || version != _pricingVersions[pieceIndex]) return;
    final decoded = response.body.isEmpty ? null : jsonDecode(response.body);
    setState(() {
      _pricingQuotes[pieceIndex] = response.statusCode >= 200 &&
              response.statusCode < 300 &&
              decoded is Map<String, dynamic>
          ? _SalesPricingQuote.fromJson(decoded)
          : _SalesPricingQuote.notReady('تعذر الاتصال بمحرك التسعير المركزي.');
      _recalculateOrderTotal();
    });
  }

  void _showMessage(String message) {
    if (mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(message)));
    }
  }

  String _apiErrorMessage(String body, String fallback) {
    if (body.trim().isEmpty) return fallback;
    try {
      final decoded = jsonDecode(body);
      if (decoded is String && decoded.trim().isNotEmpty) return decoded;
      if (decoded is Map<String, dynamic>) {
        for (final key in ['message', 'detail', 'title']) {
          final value = decoded[key]?.toString().trim();
          if (value != null && value.isNotEmpty) return value;
        }
      }
    } catch (_) {
      final value = body.trim();
      if (value.isNotEmpty) return value;
    }
    return fallback;
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
    _syncDeliveryDaysFromDate(deliveryDateController.text);
  }

  void _updateCurrentOrderPoints() {
    var total = 0.0;
    for (var index = 0; index < _expectedLoyaltyPoints.length; index++) {
      final points = _expectedLoyaltyPoints[index];
      final quantity = index < quantityControllers.length
          ? int.tryParse(quantityControllers[index].text.trim()) ?? 0
          : 0;
      if (points != null && quantity > 0) {
        total += points * quantity;
      }
    }
    currentPoints = _formatCustomerNumber(total);
    currentPointsController.text = currentPoints;
  }

  Future<void> loadSelectedCustomer() async {
    _fillCustomerControllers();
    await loadMeasurementPreview();
  }

  Future<void> _restorePersistedSession() async {
    final raw = await _sessionStorage.read(key: _activeSessionStorageKey);
    if (raw == null || raw.trim().isEmpty) return;

    try {
      final decoded = jsonDecode(raw);
      if (decoded is! Map<String, dynamic>) return;
      final draft = _SalesDraft.fromJson(decoded);
      if (!mounted) return;
      setState(() {
        _applyDraftToCurrentState(draft);
      });
      await _refreshRestoredSessionData();
    } catch (_) {
      await _sessionStorage.delete(key: _activeSessionStorageKey);
    }
  }

  Future<void> _saveCurrentSessionToStorage() async {
    if (!_hasMeaningfulSessionData()) {
      await _sessionStorage.delete(key: _activeSessionStorageKey);
      return;
    }

    final draft = _captureDraft(_nextSessionNumber);
    final payload = jsonEncode(draft.toJson());
    await _sessionStorage.write(key: _activeSessionStorageKey, value: payload);
  }

  Future<void> _clearPersistedSession() async {
    await _sessionStorage.delete(key: _activeSessionStorageKey);
  }

  void _applyDraftToCurrentState(_SalesDraft draft) {
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
    _setControllerValues(catalogNumberControllers, draft.catalogNumbers);
    _setControllerValues(availableInchesControllers, draft.availableInches);
    _setControllerValues(notes1Controllers, draft.notes1);
    _setControllerValues(notes2Controllers, draft.notes2);
    _setControllerValues(specialRequestsControllers, draft.specialRequests);
    customerNameController.text = draft.customerName;
    customerCodeController.text = draft.customerCode;
    customerIdController.text =
        draft.customerId == 0 ? '' : '${draft.customerId}';
    phoneController.text = draft.phone;
    referrerController.text = draft.referrer;
    relationshipController.text = draft.relationship;
    currentPoints = '0';
    accumulatedPoints = '0';
    treeCustomerCount = '0';
    currentPointsController.text = currentPoints;
    accumulatedPointsController.text = accumulatedPoints;
    treeCustomerCountController.text = treeCustomerCount;
    totalAmountController.text = draft.total;
    paidAmountController.text = draft.paid;
    discountAmountController.text = draft.discount;
    remainingAmountController.text = draft.remaining;
    deliveryDateController.text = draft.deliveryDate;
    _syncDeliveryDaysFromDate(deliveryDateController.text);
    measurementValuesByType
      ..clear()
      ..addAll({
        for (final entry in draft.measurements.entries)
          entry.key: Map<String, String>.from(entry.value),
      });
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
      _calculatedConsumptions.add(null);
      _consumptionUnits.add('');
      _consumptionMessages.add('');
      _pricingQuotes.add(null);
      _pricingVersions.add(0);
      _expectedLoyaltyPoints.add(0);
      _expectedLoyaltyPointVersions.add(0);
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
      _calculatedConsumptions.removeRange(
          piecesCount, _calculatedConsumptions.length);
      _consumptionUnits.removeRange(piecesCount, _consumptionUnits.length);
      _consumptionMessages.removeRange(
          piecesCount, _consumptionMessages.length);
      _pricingQuotes.removeRange(piecesCount, _pricingQuotes.length);
      _pricingVersions.removeRange(piecesCount, _pricingVersions.length);
      _expectedLoyaltyPoints.removeRange(
          piecesCount, _expectedLoyaltyPoints.length);
      _expectedLoyaltyPointVersions.removeRange(
          piecesCount, _expectedLoyaltyPointVersions.length);
    }
  }

  Future<void> _loadExpectedLoyaltyPoints(int pieceIndex) async {
    if (pieceIndex >= pieceTypes.length ||
        pieceIndex >= _expectedLoyaltyPoints.length) return;
    final version = ++_expectedLoyaltyPointVersions[pieceIndex];
    final productTypeId =
        _productTypeIdsByType[_normalizePieceTypeKey(pieceTypes[pieceIndex])];
    if (productTypeId == null) {
      if (mounted) {
        setState(() {
          _expectedLoyaltyPoints[pieceIndex] = 0;
          _updateCurrentOrderPoints();
        });
      }
      return;
    }
    if (mounted) {
      setState(() {
        _expectedLoyaltyPoints[pieceIndex] = null;
        _updateCurrentOrderPoints();
      });
    }
    try {
      final points = await _loyaltyRepository.evaluateOfficialProductPoints(
        productTypeId: productTypeId,
      );
      if (!mounted ||
          pieceIndex >= pieceTypes.length ||
          version != _expectedLoyaltyPointVersions[pieceIndex] ||
          _productTypeIdsByType[
                  _normalizePieceTypeKey(pieceTypes[pieceIndex])] !=
              productTypeId) return;
      setState(() {
        _expectedLoyaltyPoints[pieceIndex] = points;
        _updateCurrentOrderPoints();
      });
    } catch (error) {
      debugPrint('Failed to load expected loyalty points: $error');
      if (mounted &&
          pieceIndex < _expectedLoyaltyPoints.length &&
          version == _expectedLoyaltyPointVersions[pieceIndex]) {
        setState(() {
          _expectedLoyaltyPoints[pieceIndex] = 0;
          _updateCurrentOrderPoints();
        });
      }
    }
  }

  String _formatExpectedLoyaltyPoints(double? points) {
    if (points == null) return '...';
    return points == points.roundToDouble()
        ? points.toStringAsFixed(0)
        : points.toStringAsFixed(2);
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
        final code = (entry['fabricCode'] ??
                entry['FabricCode'] ??
                entry['itemCode'] ??
                entry['ItemCode'])
            ?.toString();
        if (code == null || code.trim().isEmpty) {
          continue;
        }
        final normalizedCode = code.trim().toUpperCase();
        final quantityInchRaw = entry['availableInches'] ??
            entry['AvailableInches'] ??
            entry['quantityInch'] ??
            entry['QuantityInch'] ??
            entry['availableQuantity'] ??
            entry['AvailableQuantity'];
        final quantityInch =
            double.tryParse(quantityInchRaw?.toString() ?? '') ?? 0;
        nextStock[normalizedCode] = _FabricStockSnapshot(
          fabricType: (entry['fabricType'] ??
                      entry['FabricType'] ??
                      entry['fabricName'] ??
                      entry['FabricName'])
                  ?.toString() ??
              '',
          fabricColor: (entry['fabricColor'] ??
                      entry['FabricColor'] ??
                      entry['color'] ??
                      entry['Color'])
                  ?.toString() ??
              '',
          catalogNumber: (entry['catalogNumber'] ??
                      entry['CatalogNumber'] ??
                      entry['barcode'] ??
                      entry['Barcode'])
                  ?.toString() ??
              '',
          availableInches: quantityInch,
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
      availableController.text =
          _formatFabricAvailable(refreshed.availableInches);
      return;
    }

    fabricTypeController.text = snapshot.fabricType;
    fabricColorController.text = snapshot.fabricColor;
    catalogController.text = snapshot.catalogNumber;
    availableController.text = _formatFabricAvailable(snapshot.availableInches);
  }

  double? _calculatedConsumptionForPiece(int pieceIndex) =>
      _calculatedConsumptions[pieceIndex];

  String _pieceMeasurementKey(int pieceIndex) => 'piece_$pieceIndex';

  Future<void> _calculateConsumption(int pieceIndex) async {
    final pieceType = pieceTypes[pieceIndex];
    final productTypeId =
        _productTypeIdsByType[_normalizePieceTypeKey(pieceType)];
    if (pieceType.isEmpty || productTypeId == null) {
      if (mounted)
        setState(() {
          _calculatedConsumptions[pieceIndex] = null;
          _consumptionMessages[pieceIndex] = 'نوع القطعة غير مرتبط بمصدر رسمي.';
        });
      return;
    }
    final values = <String, double>{};
    final names =
        measurementValuesByType[_pieceMeasurementKey(pieceIndex)] ?? const {};
    final codes =
        _measurementCodesByType[_normalizePieceTypeKey(pieceType)] ?? const {};
    for (final entry in names.entries) {
      final value = double.tryParse(entry.value.replaceAll(',', '.'));
      final code = codes[entry.key] ?? entry.key;
      if (value != null && value >= 0) values[code] = value;
    }
    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/consumption-rules/evaluate'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(
            {'productTypeId': productTypeId, 'measurements': values}),
      );
      if (!mounted) return;
      final decoded = response.body.isEmpty ? null : jsonDecode(response.body);
      if (response.statusCode >= 200 &&
          response.statusCode < 300 &&
          decoded is Map<String, dynamic>) {
        final unit = decoded['unit']?.toString() ?? '';
        final result = double.tryParse(decoded['value']?.toString() ?? '');
        setState(() {
          _calculatedConsumptions[pieceIndex] = result;
          _consumptionUnits[pieceIndex] = unit;
          _consumptionMessages[pieceIndex] = '';
        });
        await _schedulePricing(pieceIndex);
      } else {
        final message = decoded is Map<String, dynamic>
            ? decoded['message']?.toString()
            : null;
        setState(() {
          _calculatedConsumptions[pieceIndex] = null;
          _consumptionMessages[pieceIndex] = message ?? 'تعذر حساب الاستهلاك.';
        });
        _invalidatePricing(pieceIndex);
      }
    } catch (_) {
      if (mounted)
        setState(() {
          _calculatedConsumptions[pieceIndex] = null;
          _consumptionMessages[pieceIndex] =
              'تعذر الاتصال بخدمة قواعد الاستهلاك.';
        });
      _invalidatePricing(pieceIndex);
    }
  }

  String _formatFabricAvailable(double value) =>
      value <= 0 ? '0' : value.toStringAsFixed(1);

  List<String> get _pieceTypeOptions => _officialPieceTypeOptions.isNotEmpty
      ? _officialPieceTypeOptions
      : _fallbackPieceTypeOptions;

  Future<void> _loadOfficialMeasurementFields() async {
    final nextFields = <String, List<String>>{};
    final nextCodes = <String, Map<String, String>>{};
    final nextOptions = <String>[];
    final nextProductTypeIds = <String, int>{};

    try {
      final response = await http.get(Uri.parse('$_baseUrl/consumption-rules'));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('HTTP ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) {
        throw Exception('Invalid consumption-rules payload');
      }

      final productTypes = (decoded['productTypes'] as List? ?? const [])
          .whereType<Map<String, dynamic>>()
          .toList();
      final productTypeNames = <int, String>{
        for (final item in productTypes)
          (int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0):
              (item['nameAr']?.toString() ?? '').trim(),
      };

      final seenOptionKeys = <String>{};
      for (final item in productTypes) {
        final name = (item['nameAr']?.toString() ?? '').trim();
        final productTypeId =
            int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0;
        if (name.isEmpty || productTypeId <= 0) continue;

        final key = _normalizePieceTypeKey(name);
        if (key.isEmpty || seenOptionKeys.contains(key)) continue;
        seenOptionKeys.add(key);
        nextOptions.add(name);
        nextProductTypeIds[key] = productTypeId;
      }

      final groupedFields = <int, List<String>>{};
      for (final item in ((decoded['measurementFields'] as List?) ?? const [])
          .whereType<Map<String, dynamic>>()) {
        final productTypeId =
            int.tryParse(item['productTypeId']?.toString() ?? '') ?? 0;
        final name = (item['nameAr']?.toString() ?? '').trim();
        if (productTypeId <= 0 || name.isEmpty) continue;

        groupedFields.putIfAbsent(productTypeId, () => <String>[]).add(name);

        final productTypeName = productTypeNames[productTypeId] ?? '';
        final normalizedKey = _normalizePieceTypeKey(productTypeName);
        if (normalizedKey.isEmpty) continue;
        final codes =
            nextCodes.putIfAbsent(normalizedKey, () => <String, String>{});
        codes[name] = item['code']?.toString() ?? name;
      }

      for (final entry in groupedFields.entries) {
        final productTypeName = productTypeNames[entry.key] ?? '';
        final pieceType = _normalizePieceTypeKey(productTypeName);
        if (pieceType.isEmpty) continue;
        nextFields[pieceType] = <String>[...entry.value.toSet().toList()];
        if (!nextProductTypeIds.containsKey(pieceType)) {
          nextProductTypeIds[pieceType] = entry.key;
        }
      }
    } catch (error) {
      debugPrint('Failed to load official measurement fields: $error');
      if (mounted && _officialPieceTypeOptions.isEmpty) {
        setState(() {
          _officialPieceTypeOptions
            ..clear()
            ..addAll(_fallbackPieceTypeOptions);
        });
      }
      return;
    }

    if (!mounted) return;
    setState(() {
      _officialMeasurementFieldsByType
        ..clear()
        ..addAll(nextFields);
      _measurementCodesByType
        ..clear()
        ..addAll(nextCodes);
      _officialPieceTypeOptions
        ..clear()
        ..addAll(
            nextOptions.isNotEmpty ? nextOptions : _fallbackPieceTypeOptions);
      _productTypeIdsByType
        ..clear()
        ..addAll(nextProductTypeIds);
    });

    for (var index = 0; index < pieceTypes.length; index++) {
      await _loadExpectedLoyaltyPoints(index);
      await _calculateConsumption(index);
    }
  }

  Future<void> _refreshOfficialCatalog() async {
    if (_isRefreshingOfficialCatalog) return;
    setState(() => _isRefreshingOfficialCatalog = true);
    try {
      await _loadOfficialMeasurementFields();
      if (mounted) {
        _showMessage(
            'تم تحديث أنواع القطع والقياسات والقواعد من المصدر الرسمي.');
      }
    } catch (error) {
      debugPrint('Failed to refresh official catalog: $error');
      if (mounted) {
        _showMessage('تعذر تحديث أنواع القطع من المصدر الرسمي.');
      }
    } finally {
      if (mounted) {
        setState(() => _isRefreshingOfficialCatalog = false);
      }
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
      final fields = {
        for (final field in _measurementFieldsForPiece(type)) field: '---',
      };
      for (var index = 0; index < pieceTypes.length; index++) {
        if (pieceTypes[index] == type)
          measurementValuesByType[_pieceMeasurementKey(index)] =
              Map<String, String>.from(fields);
      }
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
              if (type != null && name != null) {
                for (var index = 0; index < pieceTypes.length; index++) {
                  if (pieceTypes[index] == type &&
                      measurementValuesByType[_pieceMeasurementKey(index)]
                              ?[name] ==
                          '---') {
                    measurementValuesByType[_pieceMeasurementKey(index)]![
                        name] = value.isEmpty ? '---' : value;
                  }
                }
              }
            }
          }
        }
      } catch (error) {
        debugPrint('Failed to load measurements: $error');
      }
    }
    if (mounted) setState(() {});
    for (var index = 0; index < pieceTypes.length; index++) {
      await _calculateConsumption(index);
    }
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
    return fieldsByPieceType[normalizedKey] ??
        fieldsByPieceType[pieceType] ??
        const [];
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    unawaited(_saveCurrentSessionToStorage());
    ScreenChromeState.instance.setHideTopChrome(false);
    for (final controller in [
      customerNameController,
      customerCodeController,
      customerIdController,
      phoneController,
      referrerController,
      relationshipController,
      currentPointsController,
      accumulatedPointsController,
      treeCustomerCountController,
      totalAmountController,
      paidAmountController,
      discountAmountController,
      remainingAmountController,
      deliveryDateController,
      deliveryDaysController,
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

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.inactive ||
        state == AppLifecycleState.paused ||
        state == AppLifecycleState.detached) {
      unawaited(_saveCurrentSessionToStorage());
    }
  }

  void _handleSalesViewModeChanged() {
    final nextMode = salesViewModeNotifier.value;
    if (!mounted || _salesViewMode == nextMode) return;
    setState(() {
      _salesViewMode = nextMode;
      ScreenChromeState.instance
          .setHideTopChrome(nextMode == SalesViewMode.fullscreen);
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
        (_salesViewMode == SalesViewMode.tabs ||
            _salesViewMode == SalesViewMode.all);
    final showHeader = _salesViewMode != SalesViewMode.fullscreen &&
        _selectedTab != 2 &&
        (_salesViewMode == SalesViewMode.sessions ||
            _salesViewMode == SalesViewMode.all);

    return Scaffold(
      backgroundColor: screenBackground,
      body: Stack(
        children: [
          Column(
            children: [
              if (showTabs) _buildSalesTabs(context),
              if (showTabs && showHeader) const SizedBox(height: 2),
              if (showHeader) _buildSalesHeader(context),
              Expanded(child: _buildSelectedTab(context)),
            ],
          ),
          if (_isInitialLoading)
            Positioned.fill(
              child: ColoredBox(
                color: Color(0xB30C1420),
                child: Center(
                  child: Container(
                    width: 112,
                    height: 112,
                    decoration: BoxDecoration(
                      color: _surfaceCard,
                      borderRadius: BorderRadius.circular(18),
                      border: Border.all(color: _primaryBlue, width: 1.2),
                      boxShadow: const [
                        BoxShadow(
                          color: Color(0x66000000),
                          blurRadius: 20,
                          offset: Offset(0, 8),
                        ),
                      ],
                    ),
                    child: const Center(
                      child: SizedBox(
                        width: 46,
                        height: 46,
                        child: CircularProgressIndicator(
                          strokeWidth: 4,
                          color: _primaryBlue,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
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
    final chipBackground =
        isLightTheme ? const Color(0xFFEAF1F7) : const Color(0xFF1A2633);
    final chipBorder =
        isLightTheme ? const Color(0xFFD8E1F0) : const Color(0xFF32475E);
    final textColor =
        isLightTheme ? const Color(0xFF0F172A) : const Color(0xFFEAF2FF);

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
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            labelStyle: TextStyle(
              color: selected ? const Color.fromARGB(255, 0, 0, 0) : textColor,
              fontWeight: FontWeight.w700,
              fontSize: _supportingFontSize,
            ),
            side: BorderSide(
              color: selected
                  ? const Color.fromARGB(255, 2, 225, 180)
                  : chipBorder,
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
        ? <Color>[
            const Color(0xFFEAF3FF),
            const Color(0xFFDBF4EF),
            const Color(0xFFDDECF7)
          ]
        : const <Color>[
            Color.fromARGB(255, 10, 16, 22),
            Color.fromARGB(255, 10, 16, 22),
            Color.fromARGB(255, 10, 16, 22)
          ];
    final textColor = isLightTheme ? const Color(0xFF0F172A) : _textMain;
    return Container(
      height: 40,
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
        IconButton(
          tooltip: 'تحديث أنواع القطع والقياسات من المصدر الرسمي',
          onPressed:
              _isRefreshingOfficialCatalog ? null : _refreshOfficialCatalog,
          splashRadius: 18,
          constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
          style: IconButton.styleFrom(
            backgroundColor: const Color.fromARGB(255, 13, 36, 34),
            side: const BorderSide(
              color: Color.fromARGB(255, 2, 225, 180),
              width: 1,
            ),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(5),
            ),
          ),
          icon: _isRefreshingOfficialCatalog
              ? const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Color.fromARGB(255, 2, 225, 180),
                  ),
                )
              : const Icon(
                  Icons.refresh,
                  size: 18,
                  color: Color.fromARGB(255, 2, 225, 180),
                ),
        ),
        const SizedBox(width: 8),
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
          icon: const Icon(Icons.add,
              size: 20, color: Color.fromARGB(255, 23, 22, 37)),
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
                        constraints:
                            const BoxConstraints(minWidth: 18, minHeight: 18),
                        splashRadius: 12,
                        tooltip: 'حذف الجلسة',
                        onPressed: () => _cancelDraft(index),
                        icon:
                            const Icon(Icons.close, size: 12, color: _textSoft),
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
              backgroundColor: selected
                  ? const Color.fromARGB(255, 13, 25, 23)
                  : const Color.fromARGB(255, 12, 26, 23),
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
      return const Padding(
          padding: EdgeInsets.all(8), child: ReadySalesScreen());
    }
    if (_selectedTab == 2) {
      return const Padding(
          padding: EdgeInsets.all(8), child: ReadyMadeProductionScreen());
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
                    child: CircularProgressIndicator(
                        strokeWidth: 2, color: theme.colorScheme.onPrimary),
                  )
                : const Text('حفظ الطلب'),
          ),
        ),
      ]),
    );
  }

  void _startNewSession() {
    FocusScope.of(context).unfocus();
    final draft = _captureDraft(_nextSessionNumber);
    _drafts.add(draft);
    _nextSessionNumber++;
    unawaited(_clearPersistedSession());
    setState(() {
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
        relationship: relationshipController.text,
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
        catalogNumbers:
            catalogNumberControllers.map((item) => item.text).toList(),
        availableInches:
            availableInchesControllers.map((item) => item.text).toList(),
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
      relationshipController,
      currentPointsController,
      accumulatedPointsController,
      treeCustomerCountController,
      totalAmountController,
      paidAmountController,
      discountAmountController,
      remainingAmountController,
      deliveryDateController,
      deliveryDaysController,
    ]) {
      controller.clear();
    }
    quantityControllers[0].text = '1';
    currentPoints = '0';
    accumulatedPoints = '0';
    treeCustomerCount = '0';
    _expectedLoyaltyPoints[0] = 0;
    _updateCurrentOrderPoints();
    for (final controller in [
      fabricCodeControllers[0],
      fabricTypeControllers[0],
      fabricColorControllers[0],
      catalogNumberControllers[0],
      availableInchesControllers[0],
      notes1Controllers[0],
      notes2Controllers[0],
      specialRequestsControllers[0]
    ]) {
      controller.clear();
    }
    measurementValuesByType.clear();
    for (var index = 0; index < _pricingQuotes.length; index++) {
      _pricingVersions[index]++;
      _pricingQuotes[index] = null;
      _calculatedConsumptions[index] = null;
      _consumptionUnits[index] = '';
      _consumptionMessages[index] = '';
    }
    _recalculateOrderTotal();
  }

  Future<void> _restoreDraft(int index) async {
    final draft = _drafts.removeAt(index);
    setState(() {
      _applyDraftToCurrentState(draft);
    });
    await _refreshRestoredSessionData();
  }

  Future<void> _refreshRestoredSessionData() async {
    for (var pieceIndex = 0; pieceIndex < piecesCount; pieceIndex++) {
      await _loadExpectedLoyaltyPoints(pieceIndex);
      await _calculateConsumption(pieceIndex);
    }
    if (currentCustomerId > 0) {
      await _loadCustomerSummary(currentCustomerId);
    } else {
      _updateCurrentOrderPoints();
      _fillCustomerControllers();
    }
  }

  bool _hasMeaningfulSessionData() {
    if (customerNameController.text.trim().isNotEmpty ||
        customerCodeController.text.trim().isNotEmpty ||
        phoneController.text.trim().isNotEmpty ||
        referrerController.text.trim().isNotEmpty ||
        deliveryDateController.text.trim().isNotEmpty ||
        totalAmountController.text.trim() != '0.00' ||
        paidAmountController.text.trim() != '0.00' ||
        discountAmountController.text.trim() != '0.00' ||
        remainingAmountController.text.trim() != '0.00') {
      return true;
    }

    if (pieceTypes.any((pieceType) => pieceType.trim().isNotEmpty)) {
      return true;
    }

    for (final controller in [
      ...quantityControllers,
      ...fabricCodeControllers,
      ...fabricTypeControllers,
      ...fabricColorControllers,
      ...catalogNumberControllers,
      ...availableInchesControllers,
      ...notes1Controllers,
      ...notes2Controllers,
      ...specialRequestsControllers,
    ]) {
      if (controller.text.trim().isNotEmpty) {
        return true;
      }
    }

    for (final values in measurementValuesByType.values) {
      if (values.values
          .any((value) => value.trim().isNotEmpty && value != '---')) {
        return true;
      }
    }

    return false;
  }

  void _cancelDraft(int index) {
    if (index < 0 || index >= _drafts.length) return;
    _drafts.removeAt(index);
    setState(() {});
  }

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
            _statValue(context, 'تاريخ اليوم',
                intl.DateFormat('yyyy/MM/dd').format(now)),
            _statValue(context, 'اسم اليوم', _arabicDayName(now.weekday)),
            _statValue(context, 'إجمالي الطلبات', totalOrdersCount),
            _statValue(context, 'طلبات اليوم', todayOrdersCount),
            _statInput(context, 'LUMAR ERP ', profitAmountController,
                readOnly: true),
            _statInput(context, 'LUMAR ', profitPercentageController,
                suffix: '%', onChanged: (_) => _updateProfitAmount()),
          ];
          final columns = constraints.maxWidth < 900 ? 2 : 6;
          const spacing = 6.0;
          final width =
              (constraints.maxWidth - spacing * (columns - 1)) / columns;
          return Wrap(
            spacing: spacing,
            runSpacing: 6,
            children: cards
                .map((card) => SizedBox(width: width, child: card))
                .toList(),
          );
        },
      );

  Widget _statInput(
      BuildContext context, String label, TextEditingController controller,
      {bool readOnly = false,
      String? suffix,
      ValueChanged<String>? onChanged}) {
    final theme = Theme.of(context);
    return SizedBox(
      height: _dashboardCardHeight,
      child: Card(
        elevation: 0,
        color: theme.colorScheme.surfaceContainer,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(5),
          side: BorderSide(
              color: theme.colorScheme.outlineVariant.withValues(alpha: 0.6)),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
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
          side: BorderSide(
              color: theme.colorScheme.outlineVariant.withValues(alpha: 0.6)),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
          child: InputDecorator(
            decoration: _statDecoration(context, label, null),
            child: Center(
                child: Text(value,
                    style: theme.textTheme.bodyMedium?.copyWith(
                        fontSize: _kpiValueFontSize,
                        fontWeight: FontWeight.bold))),
          ),
        ),
      ),
    );
  }

  InputDecoration _statDecoration(
      BuildContext context, String label, String? suffix) {
    final theme = Theme.of(context);
    return InputDecoration(
      labelText: label,
      suffixText: suffix,
      floatingLabelBehavior: FloatingLabelBehavior.always,
      labelStyle: theme.textTheme.labelSmall
          ?.copyWith(fontSize: _fieldLabelFontSize, color: _textSoft),
      border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(5),
          borderSide:
              const BorderSide(color: Color.fromARGB(255, 207, 211, 211))),
      enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(5),
          borderSide:
              const BorderSide(color: Color.fromARGB(255, 44, 105, 103))),
      focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(5),
          borderSide: const BorderSide(color: _primaryBlue, width: 1.6)),
      isDense: true,
      contentPadding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
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
              Expanded(
                  child: Text('معلومات العميل',
                      style: _sectionTitleStyle(context))),
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
                  onPressed: _openCustomerCreation,
                  icon: const Icon(Icons.person_add_alt_1,
                      size: 15, color: Colors.black),
                  label: const Text('إنشاء عميل',
                      style: TextStyle(
                          fontSize: _buttonFontSize, color: Colors.black)),
                ),
              ),
            ]),
            const SizedBox(height: 8),
            _customerField(context, 'رقم الهاتف', phoneController,
                onChanged: (value) => currentCustomerPhone = value,
                onEditingComplete: () =>
                    _searchCustomerAndNextFocus(phoneController.text.trim())),
            _customerField(context, 'اسم العميل', customerNameController,
                onEditingComplete: () => _searchCustomerAndNextFocus(
                    customerNameController.text.trim())),
            _customerField(context, 'كود العميل', customerCodeController,
                onEditingComplete: () => _searchCustomerAndNextFocus(
                    customerCodeController.text.trim())),
            _customerField(context, 'اسم العميل المحيل', referrerController,
                readOnly: true),
            _customerField(context, 'صلة القرابة', relationshipController,
                readOnly: true),
            _customerField(context, 'النقاط الحالية', currentPointsController,
                readOnly: true),
            _customerField(
                context, 'النقاط التراكمية', accumulatedPointsController,
                readOnly: true),
            _customerField(
                context, 'عدد العملاء في الشجرة', treeCustomerCountController,
                readOnly: true),
            Divider(
                height: 14,
                thickness: 1,
                color: Theme.of(context).colorScheme.outlineVariant),
            _customerField(context, 'القيمة الإجمالية', totalAmountController,
                readOnly: true),
            _customerField(context, 'المدفوع مقدماً', paidAmountController,
                onChanged: _updateRemainingAmount),
            _customerField(context, 'الخصم', discountAmountController,
                onChanged: _updateRemainingAmount),
            _customerField(
                context, 'المتبقي بعد الخصم', remainingAmountController,
                readOnly: true),
            Row(
              children: [
                Expanded(
                  child: _customerField(
                    context,
                    'عدد أيام الاستلام',
                    deliveryDaysController,
                    keyboardType: TextInputType.number,
                    inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                    onChanged: _applyDeliveryDaysShortcut,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _customerField(
                    context,
                    'تاريخ الاستلام YYYY-MM-DD',
                    deliveryDateController,
                    keyboardType: TextInputType.datetime,
                    onChanged: _syncDeliveryDaysFromDate,
                  ),
                ),
              ],
            ),
          ],
        ),
      );

  Widget _customerField(
      BuildContext context, String label, TextEditingController? controller,
      {bool readOnly = false,
      ValueChanged<String>? onChanged,
      VoidCallback? onEditingComplete,
      TextInputType? keyboardType,
      List<TextInputFormatter>? inputFormatters}) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: TextFormField(
        controller: controller,
        readOnly: readOnly,
        onChanged: onChanged,
        onEditingComplete:
            onEditingComplete ?? () => FocusScope.of(context).nextFocus(),
        textInputAction: TextInputAction.next,
        keyboardType: keyboardType ?? TextInputType.text,
        inputFormatters: inputFormatters,
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
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 5, vertical: 8),
          border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(5),
              borderSide:
                  const BorderSide(color: Color.fromARGB(255, 43, 104, 101))),
          enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(5),
              borderSide: const BorderSide(color: _borderSoft)),
          focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(5),
              borderSide: const BorderSide(color: _primaryBlue, width: 1.5)),
        ),
        style: theme.textTheme.bodyMedium
            ?.copyWith(fontSize: _fieldFontSize, color: _textMain),
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
    _updateCurrentOrderPoints();
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
      _calculatedConsumptions.removeAt(pieceIndex);
      _consumptionUnits.removeAt(pieceIndex);
      _consumptionMessages.removeAt(pieceIndex);
      _pricingQuotes.removeAt(pieceIndex);
      _pricingVersions.removeAt(pieceIndex);
      _expectedLoyaltyPoints.removeAt(pieceIndex);
      _expectedLoyaltyPointVersions.removeAt(pieceIndex);
      piecesCount--;
      _selectedPieceIndex = null;
    });
    _updateCurrentOrderPoints();
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
        initialValues:
            measurementValuesByType[_pieceMeasurementKey(pieceIndex)] ??
                const {},
        onSave: currentCustomerId > 0
            ? (values) => MeasurementsApi(baseUrl: _baseUrl)
                .upsert(currentCustomerId, pieceType, values)
            : null,
      ),
    );
    if (values != null && mounted) {
      setState(() =>
          measurementValuesByType[_pieceMeasurementKey(pieceIndex)] = values);
      await _calculateConsumption(pieceIndex);
    }
  }

  Widget _pieceCard(BuildContext context, int index) {
    final pieceIndex = index - 1;
    final pieceType = pieceTypes[pieceIndex];
    final selected = _selectedPieceIndex == pieceIndex;
    final theme = Theme.of(context);
    final consumptionPerPiece = _calculatedConsumptionForPiece(pieceIndex) ?? 0;
    final quantity =
        int.tryParse(quantityControllers[pieceIndex].text.trim()) ?? 1;
    final requiredConsumption = consumptionPerPiece * quantity;
    final availableInches = _fabricAvailableInches(pieceIndex);
    final isFabricShortage =
        availableInches != null && availableInches < requiredConsumption;
    final isMeasurementsHidden = _hiddenMeasurementRows[pieceIndex] ?? false;
    final canToggleMeasurements = pieceType.isNotEmpty;

    return Card(
      elevation: 1.5,
      color: _surfaceCard,
      shadowColor: Colors.black.withValues(alpha: 0.12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(5),
        side: BorderSide(
          color: selected
              ? const Color(0xFF7DE7D1)
              : const Color.fromARGB(255, 102, 203, 197),
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
        onTap: pieceType.isEmpty
            ? null
            : () => setState(() => _selectedPieceIndex = pieceIndex),
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(
                      color: const Color.fromARGB(255, 10, 18, 28),
                      border: Border.all(
                          color: const Color(0xFF7DE7D1), width: 1.2),
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
                  const SizedBox(width: 7),
                  SizedBox(
                    width: 130,
                    height: 40,
                    child: _pricingStatusField(context, pieceIndex),
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
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(5)),
                          ),
                          onPressed: _addPiece,
                          icon: const Icon(Icons.add, size: 16),
                          label: const Text('إضافة قطعة',
                              style: TextStyle(fontSize: 11)),
                        ),
                      ),
                    ),
                  if (index == 1) const SizedBox(width: 8),
                  if (canToggleMeasurements) ...[
                    const SizedBox(width: 8),
                    Container(
                      decoration: BoxDecoration(
                        border: Border.all(
                            color: const Color(0xFF7DE7D1), width: 1.2),
                        borderRadius: BorderRadius.circular(5),
                        color: const Color.fromARGB(255, 10, 18, 28),
                      ),
                      child: IconButton(
                        tooltip: isMeasurementsHidden
                            ? 'إظهار صف المقاسات'
                            : 'إخفاء صف المقاسات',
                        visualDensity: VisualDensity.compact,
                        color: isMeasurementsHidden
                            ? theme.colorScheme.primary
                            : theme.colorScheme.onSurface,
                        onPressed: () => setState(() {
                          _hiddenMeasurementRows[pieceIndex] =
                              !isMeasurementsHidden;
                        }),
                        icon: Icon(
                          isMeasurementsHidden
                              ? Icons.visibility_outlined
                              : Icons.visibility_off_outlined,
                          size: 18,
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(width: 8),
                  SizedBox(
                    width: 130,
                    height: 40,
                    child: InputDecorator(
                      decoration: _pieceDecoration(context, 'معرف القطعة')
                          .copyWith(
                            contentPadding: const EdgeInsets.symmetric(
                                horizontal: 8, vertical: 9),
                          ),
                      child: Text(
                        _productTypeIdsByType[_normalizePieceTypeKey(pieceType)]
                                ?.toString() ??
                            '',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              fontSize: _fieldFontSize,
                              color: Theme.of(context)
                                  .colorScheme
                                  .onSurfaceVariant,
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Container(
                    decoration: BoxDecoration(
                      border: Border.all(
                          color: const Color(0xFF7DE7D1), width: 1.2),
                      borderRadius: BorderRadius.circular(5),
                      color: const Color.fromARGB(255, 10, 18, 28),
                    ),
                    child: IconButton(
                      tooltip: 'حذف القطعة',
                      visualDensity: VisualDensity.compact,
                      color: theme.colorScheme.error,
                      onPressed: piecesCount == 1
                          ? null
                          : () => _removePiece(pieceIndex),
                      icon: const Icon(Icons.delete_outline, size: 18),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 7),
              FocusTraversalGroup(
                policy: OrderedTraversalPolicy(),
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    const spacing = 6.0;
                    final row1Columns = constraints.maxWidth < 700 ? 2 : 7;
                    final row1Width =
                        (constraints.maxWidth - spacing * (row1Columns - 1)) /
                            row1Columns;
                    final row2Columns = constraints.maxWidth < 600 ? 2 : 6;
                    final row2Width =
                        (constraints.maxWidth - spacing * (row2Columns - 1)) /
                            row2Columns;
                    const row1ContentPadding =
                      EdgeInsets.symmetric(horizontal: 6, vertical: 4);

                    final row1Fields = <Widget>[
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(1),
                        child: Focus(
                          onKeyEvent: (_, event) =>
                              _moveToNextFieldOnEnter(context, event),
                          onFocusChange: (hasFocus) =>
                              _setEditableFieldFocusState(
                                  pieceIndex, 'type', hasFocus),
                          child: DropdownButtonFormField<String>(
                            initialValue: pieceType.isEmpty ||
                                    !_pieceTypeOptions.contains(pieceType)
                                ? null
                                : pieceType,
                            hint: const Text('اختر',
                                style:
                                    TextStyle(fontSize: _fieldLabelFontSize)),
                            isExpanded: true,
                            dropdownColor: const Color(0xFF101820),
                            style: theme.textTheme.bodySmall?.copyWith(
                                fontSize: _fieldFontSize, color: _textMain),
                            decoration: _pieceDecoration(context, 'نوع القطعة',
                              selected: _isEditableFieldFocused(
                                pieceIndex, 'type'),
                              contentPadding: row1ContentPadding),
                            items: _pieceTypeOptions
                                .map((type) => DropdownMenuItem(
                                    value: type,
                                    child: Text(type,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis)))
                                .toList(),
                            onChanged: (value) async {
                              if (value == null) return;
                              _invalidatePricing(pieceIndex);
                              setState(() {
                                pieceTypes[pieceIndex] = value;
                                _selectedPieceIndex = pieceIndex;
                              });
                              await _loadExpectedLoyaltyPoints(pieceIndex);
                              await loadMeasurementPreview();
                              await _calculateConsumption(pieceIndex);
                            },
                          ),
                        ),
                      ),
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(2),
                          child: _legacyReadOnlyField(
                              context,
                              'النقاط المتوقعة',
                              _formatExpectedLoyaltyPoints(
                                  _expectedLoyaltyPoints[pieceIndex]),
                                contentPadding: row1ContentPadding)),
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(3),
                        child: _pieceField(
                          context,
                          'عدد القطع',
                          quantityControllers[pieceIndex],
                          number: true,
                          pieceIndex: pieceIndex,
                          fieldKey: 'quantity',
                          contentPadding: row1ContentPadding,
                          onChanged: (_) async {
                            _invalidatePricing(pieceIndex);
                            _updateCurrentOrderPoints();
                            await _calculateConsumption(pieceIndex);
                            if (mounted) setState(() {});
                          },
                        ),
                      ),
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(4),
                        child: _pieceField(
                          context,
                          'كود القماش',
                          fabricCodeControllers[pieceIndex],
                          pieceIndex: pieceIndex,
                          fieldKey: 'fabricCode',
                          contentPadding: row1ContentPadding,
                          onChanged: (_) async {
                            _invalidatePricing(pieceIndex);
                            setState(() {});
                            await _syncFabricDataForPiece(pieceIndex);
                            await _schedulePricing(pieceIndex);
                          },
                        ),
                      ),
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(5),
                          child: _legacyReadOnlyField(context, 'نوع القماش',
                            fabricTypeControllers[pieceIndex].text,
                            contentPadding: row1ContentPadding)),
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(6),
                          child: _legacyReadOnlyField(context, 'لون القماش',
                            fabricColorControllers[pieceIndex].text,
                            contentPadding: row1ContentPadding)),
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(7),
                          child: _legacyReadOnlyField(context, 'رقم الكتالوج',
                            catalogNumberControllers[pieceIndex].text,
                            contentPadding: row1ContentPadding)),
                    ];

                    final row2Fields = <Widget>[
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(8),
                          child: _pieceField(context, 'طلب رقم 1',
                              notes1Controllers[pieceIndex],
                              pieceIndex: pieceIndex, fieldKey: 'notes1')),
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(9),
                          child: _pieceField(context, 'طلب رقم 2',
                              notes2Controllers[pieceIndex],
                              pieceIndex: pieceIndex, fieldKey: 'notes2')),
                      FocusTraversalOrder(
                          order: const NumericFocusOrder(10),
                          child: _pieceField(context, 'طلبات خاصة',
                              specialRequestsControllers[pieceIndex],
                              pieceIndex: pieceIndex,
                              fieldKey: 'specialRequests')),
                      _legacyReadOnlyField(
                        context,
                        'الاستهلاك',
                        requiredConsumption == 0
                            ? '0 '
                            : '${requiredConsumption.toStringAsFixed(2)} ${_consumptionUnits[pieceIndex]}',
                      ),
                      _fabricAvailableField(
                          context,
                          pieceIndex,
                          availableInches,
                          requiredConsumption,
                          isFabricShortage),
                      FocusTraversalOrder(
                        order: const NumericFocusOrder(18),
                        child: SizedBox(
                          height: 40,
                          child: FilledButton.icon(
                            style: FilledButton.styleFrom(
                              backgroundColor:
                                  const Color.fromARGB(255, 2, 225, 180),
                              foregroundColor:
                                  const Color.fromARGB(255, 0, 6, 4),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(5),
                              ),
                            ),
                            onPressed: pieceType.isEmpty
                                ? null
                                : () {
                                    setState(
                                        () => _selectedPieceIndex = pieceIndex);
                                    _openMeasurements(pieceIndex);
                                  },
                            icon: const Icon(Icons.straighten, size: 24),
                            label: const Text('المقاسات',
                                maxLines: 1, overflow: TextOverflow.ellipsis),
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
                          children: row1Fields
                              .map((field) => SizedBox(
                                  width: row1Width, height: 45, child: field))
                              .toList(),
                        ),
                        const SizedBox(height: 7),
                        Wrap(
                          spacing: spacing,
                          runSpacing: 1,
                          children: row2Fields
                              .map((field) => SizedBox(
                                  width: row2Width, height: 50, child: field))
                              .toList(),
                        ),
                      ],
                    );
                  },
                ),
              ),
              if (pieceType.isNotEmpty &&
                  !(_hiddenMeasurementRows[pieceIndex] ?? false)) ...[
                const SizedBox(height: 7),
                _buildInlineMeasurements(context, pieceType, pieceIndex),
                if (_consumptionMessages[pieceIndex].isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      _consumptionMessages[pieceIndex],
                      style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                          fontSize: 12),
                    ),
                  ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  double? _fabricAvailableInches(int pieceIndex) {
    final restoredText = availableInchesControllers[pieceIndex].text.trim();
    if (restoredText.isNotEmpty && restoredText != '---') {
      final parsed = double.tryParse(restoredText.replaceAll(',', '.'));
      if (parsed != null) return parsed;
    }

    final code = fabricCodeControllers[pieceIndex].text.trim();
    if (code.isEmpty) return null;
    final snapshot = _fabricStockByCode[code.toUpperCase()];
    return snapshot?.availableInches;
  }

  Widget _fabricAvailableField(BuildContext context, int pieceIndex,
      double? availableInches, double requiredConsumption, bool isShortage) {
    final value =
        availableInches == null ? '---' : availableInches.toStringAsFixed(1);
    return Container(
      height: 40,
      padding: const EdgeInsets.all(1),
      decoration: BoxDecoration(
        color: isShortage
            ? const Color.fromARGB(255, 160, 11, 0)
            : const Color.fromARGB(255, 101, 224, 165),
        borderRadius: BorderRadius.circular(5),
        border: Border.all(
          color: isShortage
              ? const Color.fromARGB(255, 160, 11, 0)
              : const Color.fromARGB(255, 11, 79, 33),
          width: isShortage ? 1.4 : 1,
        ),
      ),
      child: InputDecorator(
        decoration: _pieceDecoration(context, 'الكمية المتوفرة').copyWith(
          filled: true,
          fillColor: isShortage
              ? const Color.fromARGB(255, 1, 16, 31)
              : const Color.fromARGB(255, 1, 16, 31),
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
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
                    color: isShortage
                        ? const Color(0xFFFFF1C2)
                        : Theme.of(context).colorScheme.onSurfaceVariant,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            if (isShortage)
              Text(
                'القماش لايكفي',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 14,
                  color: Color.fromARGB(255, 160, 11, 0),
                  fontWeight: FontWeight.w600,
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _pricingStatusField(BuildContext context, int pieceIndex) {
    final quote = _pricingQuotes[pieceIndex];
    final text = quote == null
        ? '---'
        : quote.isReady
            ? quote.finalPriceTotal.toStringAsFixed(2)
            : quote.reason;
    return InputDecorator(
      decoration: _pieceDecoration(context, 'سعر البيع').copyWith(
        contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 9),
      ),
      child: Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.center,
        style: TextStyle(
          color: quote?.isReady == true ? _primaryBlue : _textSoft,
          fontSize: _fieldFontSize,
          fontWeight: FontWeight.w700,
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
    EdgeInsets? contentPadding,
    ValueChanged<String>? onChanged,
  }) {
    final key = pieceIndex == null || fieldKey == null
        ? label
        : 'piece_${pieceIndex}_$fieldKey';
    final isFocused = _focusedEditableFieldStates[key] ?? false;
    return Focus(
      onFocusChange: (hasFocus) =>
          _setEditableFieldFocusState(pieceIndex, fieldKey, hasFocus),
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        decoration: _pieceDecoration(context, label,
            selected: isFocused, contentPadding: contentPadding),
        keyboardType: number ? TextInputType.number : null,
        textInputAction: TextInputAction.next,
        onSubmitted: (_) => FocusScope.of(context).nextFocus(),
        style: Theme.of(context)
            .textTheme
            .bodySmall
            ?.copyWith(fontSize: _fieldFontSize, color: _textMain),
      ),
    );
  }

  Widget _legacyReadOnlyField(
          BuildContext context, String label, String value,
          {EdgeInsets? contentPadding}) =>
      InputDecorator(
        decoration: _pieceDecoration(context, label,
            contentPadding: contentPadding),
        child: Text(
          value,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
              fontSize: _fieldFontSize,
              color: Theme.of(context).colorScheme.onSurfaceVariant),
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

  Widget _buildInlineMeasurements(
      BuildContext context, String pieceType, int pieceIndex) {
    final fields = _measurementFieldsForPiece(pieceType);
    if (fields.isEmpty) return const SizedBox.shrink();
    return LayoutBuilder(builder: (context, constraints) {
      const spacing = 4.0;
      final count = fields.length;
      final totalSpacing = spacing * (count > 0 ? count - 1 : 0);
      final itemWidth =
          count == 0 ? 0.0 : (constraints.maxWidth - totalSpacing) / count;
      return Wrap(
        spacing: spacing,
        runSpacing: 1,
        children: fields
            .map((field) => SizedBox(
                  width: itemWidth,
                  height: 40,
                  child: _measurementSmallField(
                      context: context,
                      label: field,
                      value: measurementValuesByType[
                              _pieceMeasurementKey(pieceIndex)]?[field] ??
                          '0'),
                ))
            .toList(),
      );
    });
  }

  Widget _measurementSmallField(
          {required BuildContext context,
          required String label,
          required String value}) =>
      InputDecorator(
        decoration: _pieceDecoration(context, label).copyWith(
            contentPadding:
            const EdgeInsets.symmetric(horizontal: 4, vertical: 4)),
        child: Text(
          _formatInches(value),
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
              fontSize: _fieldFontSize,
              color: Theme.of(context).colorScheme.onSurfaceVariant),
        ),
      );

  String _formatInches(String value) {
    if (value == '0' || value.trim().isEmpty) return '0';
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

  InputDecoration _pieceDecoration(BuildContext context, String label,
      {bool selected = false, EdgeInsets? contentPadding}) {
    final theme = Theme.of(context);
    final isLightTheme = theme.brightness == Brightness.light;
    final borderColor = selected
        ? const Color(0xFF7DE7D1)
        : (isLightTheme
            ? const Color(0xFFB7C9D9)
            : const Color.fromARGB(255, 46, 93, 100));
    final fillColor = isLightTheme
        ? const Color(0xFFF8FAFC)
        : const Color.fromARGB(255, 3, 15, 28);
    final labelColor = isLightTheme ? const Color(0xFF475569) : _textSoft;
    return InputDecoration(
      labelText: label,
      labelStyle: theme.textTheme.labelSmall
          ?.copyWith(fontSize: _fieldLabelFontSize, color: labelColor),
      floatingLabelBehavior: FloatingLabelBehavior.always,
      filled: true,
      fillColor: fillColor,
      border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(5),
          borderSide: BorderSide(color: borderColor)),
      enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(5),
          borderSide: BorderSide(color: borderColor)),
      focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(5),
          borderSide: BorderSide(color: borderColor, width: 1.6)),
      isDense: true,
        contentPadding: contentPadding ??
          const EdgeInsets.symmetric(horizontal: 6, vertical: 12),
    );
  }

  void _setEditableFieldFocusState(
      int? pieceIndex, String? fieldKey, bool hasFocus) {
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
    return _focusedEditableFieldStates['piece_${pieceIndex}_$fieldKey'] ??
        false;
  }

  Widget _darkCard(BuildContext context, Widget child) {
    final isLightTheme = Theme.of(context).brightness == Brightness.light;
    return Card(
      elevation: 1.5,
      color: isLightTheme ? const Color(0xFFFFFFFF) : _surfaceCard,
      shadowColor: Colors.black.withValues(alpha: isLightTheme ? 0.04 : 0.10),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(5),
        side: BorderSide(
            color: (isLightTheme ? const Color(0xFFE2EAF5) : _borderSoft)
                .withValues(alpha: 0.7)),
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

class _SalesPricingQuote {
  const _SalesPricingQuote({
    required this.isReady,
    required this.reason,
    required this.inchPrice,
    required this.finalPriceTotal,
  });

  factory _SalesPricingQuote.fromJson(Map<String, dynamic> json) {
    final reasons =
        (json['reasons'] as List?)?.map((item) => item.toString()).join(' ');
    return _SalesPricingQuote(
      isReady: json['isReady'] == true,
      reason:
          reasons?.isNotEmpty == true ? reasons! : 'تعذر احتساب سعر القطعة.',
      inchPrice: (json['inchPrice'] as num?)?.toDouble() ?? 0,
      finalPriceTotal: (json['finalPriceTotal'] as num?)?.toDouble() ?? 0,
    );
  }

  factory _SalesPricingQuote.notReady(String reason) => _SalesPricingQuote(
        isReady: false,
        reason: reason,
        inchPrice: 0,
        finalPriceTotal: 0,
      );

  final bool isReady;
  final String reason;
  final double inchPrice;
  final double finalPriceTotal;
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
    required this.relationship,
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
    required this.catalogNumbers,
    required this.availableInches,
    required this.notes1,
    required this.notes2,
    required this.specialRequests,
    required this.measurements,
  });

  factory _SalesDraft.fromJson(Map<String, dynamic> json) => _SalesDraft(
        number: (json['number'] as num?)?.toInt() ?? 1,
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerFound: json['customerFound'] as bool? ?? false,
        customerName: json['customerName']?.toString() ?? '',
        customerCode: json['customerCode']?.toString() ?? '',
        phone: json['phone']?.toString() ?? '',
        referrer: json['referrer']?.toString() ?? '',
        relationship: json['relationship']?.toString() ?? '',
        total: json['total']?.toString() ?? '0.00',
        paid: json['paid']?.toString() ?? '0.00',
        discount: json['discount']?.toString() ?? '0.00',
        remaining: json['remaining']?.toString() ?? '0.00',
        deliveryDate: json['deliveryDate']?.toString() ?? '',
        pieceTypes: (json['pieceTypes'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [''],
        quantities: (json['quantities'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const ['1'],
        fabricCodes: (json['fabricCodes'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        fabricTypes: (json['fabricTypes'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        fabricColors: (json['fabricColors'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        catalogNumbers: (json['catalogNumbers'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        availableInches: (json['availableInches'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        notes1: (json['notes1'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        notes2: (json['notes2'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        specialRequests: (json['specialRequests'] as List?)
                ?.map((item) => item.toString())
                .toList() ??
            const [],
        measurements: ((json['measurements'] as Map?) ?? const {})
            .map((key, value) => MapEntry(
                  key.toString(),
                  (value as Map?)?.map(
                        (innerKey, innerValue) => MapEntry(
                            innerKey.toString(), innerValue.toString()),
                      ) ??
                      const <String, String>{},
                )),
      );

  Map<String, dynamic> toJson() => {
        'number': number,
        'customerId': customerId,
        'customerFound': customerFound,
        'customerName': customerName,
        'customerCode': customerCode,
        'phone': phone,
        'referrer': referrer,
        'relationship': relationship,
        'total': total,
        'paid': paid,
        'discount': discount,
        'remaining': remaining,
        'deliveryDate': deliveryDate,
        'pieceTypes': pieceTypes,
        'quantities': quantities,
        'fabricCodes': fabricCodes,
        'fabricTypes': fabricTypes,
        'fabricColors': fabricColors,
        'catalogNumbers': catalogNumbers,
        'availableInches': availableInches,
        'notes1': notes1,
        'notes2': notes2,
        'specialRequests': specialRequests,
        'measurements': {
          for (final entry in measurements.entries)
            entry.key: {
              for (final inner in entry.value.entries) inner.key: inner.value
            }
        },
      };

  final int number;
  final int customerId;
  final bool customerFound;
  final String customerName;
  final String customerCode;
  final String phone;
  final String referrer;
  final String relationship;
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
  final List<String> catalogNumbers;
  final List<String> availableInches;
  final List<String> notes1;
  final List<String> notes2;
  final List<String> specialRequests;
  final Map<String, Map<String, String>> measurements;
}
