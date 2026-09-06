import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

import '../../core/ui_palette.dart';

class _FabricBatchUtils {
  static String normalizeCode(String value) => value.trim().toUpperCase();

  static String nextFabricCode(
    List<String> existingCodes, {
    String prefix = 'FA',
    int width = 4,
  }) {
    final cleaned = existingCodes
        .map(normalizeCode)
        .where((element) => element.isNotEmpty && element.startsWith(prefix))
        .toList();

    int maxNumber = 0;
    for (final code in cleaned) {
      final numeric = code.substring(prefix.length);
      final parsed = int.tryParse(numeric);
      if (parsed != null && parsed > maxNumber) {
        maxNumber = parsed;
      }
    }

    return '$prefix${(maxNumber + 1).toString().padLeft(width, '0')}';
  }

  static String nextCatalogNumber(
    List<String> existingCatalogs, {
    String prefix = 'CAT',
    int width = 4,
  }) {
    final cleaned = existingCatalogs
        .map((value) => value.trim())
        .where((element) => element.isNotEmpty && element.toUpperCase().startsWith(prefix))
        .toList();

    int maxNumber = 0;
    for (final catalog in cleaned) {
      final numeric = catalog.substring(prefix.length);
      final parsed = int.tryParse(numeric);
      if (parsed != null && parsed > maxNumber) {
        maxNumber = parsed;
      }
    }

    return '$prefix${(maxNumber + 1).toString().padLeft(width, '0')}';
  }

  static double inchPriceFromYardPrice(double yardPrice) => yardPrice > 0 ? yardPrice / 36.0 : 0.0;

  static double batchTotalYards(List<_RollEntryItem> rolls) => rolls.fold(0.0, (sum, roll) {
        final yards = double.tryParse(roll.quantityYardsController.text.trim().replaceAll(',', '.')) ?? 0;
        return sum + yards;
      });

  static double batchTotalCost(List<_RollEntryItem> rolls) => rolls.fold(0.0, (sum, roll) {
        final yards = double.tryParse(roll.quantityYardsController.text.trim().replaceAll(',', '.')) ?? 0;
        final price = double.tryParse(roll.yardPriceController.text.trim().replaceAll(',', '.')) ?? 0;
        return sum + (yards * price);
      });
}

class FabricEntryScreenPalette {
  const FabricEntryScreenPalette({
    required this.screenBackground,
    required this.surfaceCard,
    required this.surfaceSoft,
    required this.primary,
    required this.primaryStrong,
    required this.accent,
    required this.textMain,
    required this.textSoft,
    required this.border,
  });

  final Color screenBackground;
  final Color surfaceCard;
  final Color surfaceSoft;
  final Color primary;
  final Color primaryStrong;
  final Color accent;
  final Color textMain;
  final Color textSoft;
  final Color border;

  static const defaults = FabricEntryScreenPalette(
    screenBackground: UiPalette.screenBackground,
    surfaceCard: Color.fromARGB(255, 18, 31, 41),
    surfaceSoft: Color.fromARGB(255, 18, 31, 41),
    primary: Color.fromARGB(255, 29, 46, 66),
    primaryStrong: UiPalette.primaryDark,
    accent: UiPalette.purpleAccent,
    textMain: UiPalette.textMain,
    textSoft: UiPalette.textSoft,
    border: Color.fromARGB(255, 3, 115, 243),
  );
}

class BulkFabricEntryScreen extends StatefulWidget {
  const BulkFabricEntryScreen({
    super.key,
    this.palette = FabricEntryScreenPalette.defaults,
  });

  final FabricEntryScreenPalette palette;

  @override
  State<BulkFabricEntryScreen> createState() => _BulkFabricEntryScreenState();
}

class _InventoryFabricItem {
  const _InventoryFabricItem({
    required this.id,
    required this.code,
    required this.name,
    required this.color,
    required this.width,
    required this.unitPrice,
    required this.currentQuantity,
    required this.availableQuantity,
    required this.consumedQuantity,
    required this.catalogNumber,
  });

  final int id;
  final String code;
  final String name;
  final String color;
  final double width;
  final double unitPrice;
  final double currentQuantity;
  final double availableQuantity;
  final double consumedQuantity;
  final String catalogNumber;
}

class _RollEntryItem {
  _RollEntryItem({
    String fabricCode = '',
    String catalogNumber = '',
    String fabricType = 'قماش تفصيل',
    String fabricColor = '',
    String fabricWidth = '58',
    String quantityYards = '',
    String yardPrice = '',
  })  : fabricCodeController = TextEditingController(text: fabricCode),
        catalogNumberController = TextEditingController(text: catalogNumber),
        fabricTypeController = TextEditingController(text: fabricType),
        fabricColorController = TextEditingController(text: fabricColor),
        fabricWidthController = TextEditingController(text: fabricWidth),
        quantityYardsController = TextEditingController(text: quantityYards),
        yardPriceController = TextEditingController(text: yardPrice),
        inchPriceController = TextEditingController(),
        totalRollCostController = TextEditingController();

  final TextEditingController fabricCodeController;
  final TextEditingController catalogNumberController;
  final TextEditingController fabricTypeController;
  final TextEditingController fabricColorController;
  final TextEditingController fabricWidthController;
  final TextEditingController quantityYardsController;
  final TextEditingController yardPriceController;
  final TextEditingController inchPriceController;
  final TextEditingController totalRollCostController;

  void updateCalculations() {
    final yards = double.tryParse(quantityYardsController.text.trim().replaceAll(',', '.')) ?? 0;
    final yardPrice = double.tryParse(yardPriceController.text.trim().replaceAll(',', '.')) ?? 0;

    final inchPrice = _FabricBatchUtils.inchPriceFromYardPrice(yardPrice);
    final totalCost = yards * yardPrice;

    inchPriceController.text = inchPrice > 0 ? inchPrice.toStringAsFixed(3) : '0.000';
    totalRollCostController.text = totalCost > 0 ? totalCost.toStringAsFixed(2) : '0.00';
  }

  void dispose() {
    fabricCodeController.dispose();
    catalogNumberController.dispose();
    fabricTypeController.dispose();
    fabricColorController.dispose();
    fabricWidthController.dispose();
    quantityYardsController.dispose();
    yardPriceController.dispose();
    inchPriceController.dispose();
    totalRollCostController.dispose();
  }
}

class _SupplierItem {
  const _SupplierItem({required this.id, required this.name, required this.code});
  final int id;
  final String name;
  final String code;
}

enum _FabricEntryMode { renewExisting, newFabric }

class _BulkFabricEntryScreenState extends State<BulkFabricEntryScreen> {
  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');

  final _formKey = GlobalKey<FormState>();
  final _invoiceNumberController = TextEditingController();
  final _purchaseDateController = TextEditingController(text: DateFormat('yyyy/MM/dd').format(DateTime.now()));
  final _notesController = TextEditingController();
  final _fabricTypeController = TextEditingController(text: 'قماش تفصيل');
  final _catalogNumberController = TextEditingController();
  final _fabricWidthController = TextEditingController(text: '58');
  final _yardPriceController = TextEditingController();
  final _inchPriceController = TextEditingController();
  final _rollCountController = TextEditingController(text: '1');

  DateTime _selectedDate = DateTime.now();
  int? _selectedSupplierId;
  List<_SupplierItem> _suppliers = [];
  bool _loadingSuppliers = false;
  bool _isSaving = false;
  _FabricEntryMode _mode = _FabricEntryMode.newFabric;
  List<_InventoryFabricItem> _availableInventory = [];
  final List<String> _allKnownFabricCodes = [];
  _InventoryFabricItem? _selectedExistingFabric;

  final List<_RollEntryItem> _rolls = [];

  static const _commonColors = [
    'أبيض',
    'سكري',
    'كريمي',
    'أوف وايت',
    'أسود',
    'كحلي',
    'رمادي',
    'بني',
    'بيج',
  ];

  @override
  void initState() {
    super.initState();
    _loadSuppliers();
    _loadInventoryItems();
    _updateRollsFromCount();
  }

  @override
  void dispose() {
    _invoiceNumberController.dispose();
    _purchaseDateController.dispose();
    _notesController.dispose();
    _fabricTypeController.dispose();
    _catalogNumberController.dispose();
    _fabricWidthController.dispose();
    _yardPriceController.dispose();
    _inchPriceController.dispose();
    _rollCountController.dispose();
    for (final roll in _rolls) {
      roll.dispose();
    }
    super.dispose();
  }

  Future<void> _loadSuppliers() async {
    setState(() => _loadingSuppliers = true);
    try {
      final response = await http.get(Uri.parse('$_baseUrl/suppliers'));
      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is List) {
          final list = decoded.whereType<Map<String, dynamic>>().map((item) {
            return _SupplierItem(
              id: item['supplierId'] as int,
              name: item['supplierName']?.toString() ?? '-',
              code: item['supplierCode']?.toString() ?? '',
            );
          }).toList();
          setState(() {
            _suppliers = list;
            if (_suppliers.isNotEmpty && _selectedSupplierId == null) {
              _selectedSupplierId = _suppliers.first.id;
            }
          });
        }
      }
    } catch (_) {
      // Ignore supplier loading issues and keep the form usable.
    } finally {
      if (mounted) setState(() => _loadingSuppliers = false);
    }
  }

  Future<void> _loadInventoryItems() async {
    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/items'));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        return;
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! List) {
        return;
      }

      final inventory = decoded.whereType<Map<String, dynamic>>().map((item) {
        final category = (item['category'] ?? '').toString();
        final fabricCategory = (item['fabricCategory'] ?? '').toString();
        final isFabric = category.toLowerCase().contains('fabric') ||
            fabricCategory.toLowerCase().contains('fabric') ||
            category.toLowerCase().contains('قماش') ||
            fabricCategory.toLowerCase().contains('قماش');

        final code = (item['itemCode'] ?? '').toString();
        final normalized = code.trim();
        if (normalized.toUpperCase().startsWith('FA') && RegExp(r'^FA\d+$').hasMatch(normalized.toUpperCase())) {
          _allKnownFabricCodes.add(normalized.toUpperCase());
        }

        if (!isFabric) {
          return null;
        }

        final name = (item['itemName'] ?? '').toString();
        final color = (item['fabricColor'] ?? '').toString();
        final width = (item['fabricWidth'] as num?)?.toDouble() ?? 0;
        final unitPrice = (item['yardPrice'] as num?)?.toDouble() ?? 0;
        final currentQuantity = (item['currentQuantity'] as num?)?.toDouble() ?? 0;
        final availableQuantity = (item['availableQuantity'] as num?)?.toDouble() ?? 0;
        final consumedQuantity = (item['reservedQuantity'] as num?)?.toDouble() ?? 0;
        final catalog = (item['barcode'] ?? item['itemCode'] ?? '').toString();

        return _InventoryFabricItem(
          id: item['inventoryItemId'] as int? ?? 0,
          code: code,
          name: name,
          color: color,
          width: width,
          unitPrice: unitPrice,
          currentQuantity: currentQuantity,
          availableQuantity: availableQuantity,
          consumedQuantity: consumedQuantity,
          catalogNumber: catalog,
        );
      }).whereType<_InventoryFabricItem>().toList();

      if (mounted) {
        setState(() {
          _availableInventory = inventory;
          if (_availableInventory.isNotEmpty && _selectedExistingFabric == null) {
            _selectedExistingFabric = _availableInventory.first;
            _applySelectedFabric();
          }
        });
      }
    } catch (_) {
      // Ignore inventory loading issues; user can still add new fabric.
    } finally {
      if (mounted) setState(() {});
    }
  }

  void _applySelectedFabric() {
    final item = _selectedExistingFabric;
    if (item == null) return;

    setState(() {
      _fabricTypeController.text = item.name;
      _catalogNumberController.text = item.catalogNumber;
      _fabricWidthController.text = item.width > 0 ? item.width.toStringAsFixed(0) : '58';
      _yardPriceController.text = item.unitPrice > 0 ? item.unitPrice.toStringAsFixed(2) : '';
      _inchPriceController.text = item.unitPrice > 0 ? _FabricBatchUtils.inchPriceFromYardPrice(item.unitPrice).toStringAsFixed(3) : '0.000';
      if (_rolls.isNotEmpty) {
        for (final roll in _rolls) {
          roll.fabricCodeController.text = item.code;
          roll.fabricTypeController.text = item.name;
          roll.catalogNumberController.text = item.catalogNumber;
          roll.fabricWidthController.text = item.width > 0 ? item.width.toStringAsFixed(0) : '58';
          roll.yardPriceController.text = item.unitPrice > 0 ? item.unitPrice.toStringAsFixed(2) : '';
          roll.quantityYardsController.text = '';
          roll.fabricColorController.text = item.color;
          roll.updateCalculations();
        }
      }
    });
  }

  void _updateRollsFromCount() {
    final count = int.tryParse(_rollCountController.text) ?? 1;
    final safeCount = count <= 0 ? 1 : count;

    setState(() {
      final oldLength = _rolls.length;
      final batchCatalog = _catalogNumberController.text.trim();
      final activeCatalog = batchCatalog.isNotEmpty ? batchCatalog : _FabricBatchUtils.nextCatalogNumber(
        _availableInventory.map((item) => item.catalogNumber).toList(),
        prefix: 'CAT',
      );
      if (oldLength < safeCount) {
        final existingCodes = <String>{..._allKnownFabricCodes, ..._availableInventory.map((item) => item.code)};
        final generatedCodes = <String>[];
        final usedCodes = <String>{...existingCodes};

        for (var i = 0; i < safeCount - oldLength; i++) {
          final nextCode = _FabricBatchUtils.nextFabricCode(usedCodes.toList(), prefix: 'FA');
          usedCodes.add(nextCode);
          generatedCodes.add(nextCode);
        }

        for (var i = 0; i < safeCount - oldLength; i++) {
          final newRoll = _RollEntryItem(
            fabricCode: generatedCodes[i],
            catalogNumber: activeCatalog,
            fabricType: _fabricTypeController.text.trim().isNotEmpty ? _fabricTypeController.text.trim() : 'قماش تفصيل',
            fabricColor: '',
            fabricWidth: _fabricWidthController.text.trim().isNotEmpty ? _fabricWidthController.text.trim() : '58',
            quantityYards: '',
            yardPrice: _yardPriceController.text.trim().isNotEmpty ? _yardPriceController.text.trim() : '',
          );
          newRoll.updateCalculations();
          _rolls.add(newRoll);
        }
      } else if (oldLength > safeCount) {
        while (_rolls.length > safeCount) {
          final last = _rolls.removeLast();
          last.dispose();
        }
      }

      if (_mode == _FabricEntryMode.renewExisting && _selectedExistingFabric != null) {
        _catalogNumberController.text = _selectedExistingFabric!.catalogNumber;
      } else if (_catalogNumberController.text.trim().isEmpty) {
        _catalogNumberController.text = activeCatalog;
      }

      for (var i = 0; i < _rolls.length; i++) {
        final roll = _rolls[i];
        if (_mode == _FabricEntryMode.renewExisting && _selectedExistingFabric != null) {
          roll.fabricCodeController.text = _selectedExistingFabric!.code;
          roll.fabricTypeController.text = _selectedExistingFabric!.name;
          roll.catalogNumberController.text = _catalogNumberController.text.trim();
          roll.fabricWidthController.text = _selectedExistingFabric!.width > 0 ? _selectedExistingFabric!.width.toStringAsFixed(0) : '58';
          roll.yardPriceController.text = _selectedExistingFabric!.unitPrice > 0 ? _selectedExistingFabric!.unitPrice.toStringAsFixed(2) : '';
          roll.quantityYardsController.text = i == 0 ? _rolls.first.quantityYardsController.text : roll.quantityYardsController.text;
        } else {
          roll.fabricCodeController.text = _FabricBatchUtils.nextFabricCode(
            [..._availableInventory.map((item) => item.code), ..._rolls.map((entry) => entry.fabricCodeController.text.trim())],
            prefix: 'FA',
          );
          roll.catalogNumberController.text = _catalogNumberController.text.trim();
          roll.fabricTypeController.text = _fabricTypeController.text.trim().isEmpty ? 'قماش تفصيل' : _fabricTypeController.text.trim();
          roll.fabricWidthController.text = _fabricWidthController.text.trim().isEmpty ? '58' : _fabricWidthController.text.trim();
          roll.yardPriceController.text = _yardPriceController.text.trim();
        }
        roll.updateCalculations();
      }
    });
  }

  void _addRoll() {
    final nextCount = (_rolls.length + 1);
    _rollCountController.text = nextCount.toString();
    _updateRollsFromCount();
  }

  void _removeRoll(int index) {
    if (_rolls.length <= 1) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يجب أن تحتوي الدفعة على رول واحد على الأقل.')),
      );
      return;
    }

    setState(() {
      final removed = _rolls.removeAt(index);
      removed.dispose();
      _rollCountController.text = _rolls.length.toString();
    });
  }

  double get _totalYards => _FabricBatchUtils.batchTotalYards(_rolls);

  double get _totalBatchCost => _FabricBatchUtils.batchTotalCost(_rolls);

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2035),
    );
    if (picked != null) {
      setState(() {
        _selectedDate = picked;
        _purchaseDateController.text = DateFormat('yyyy/MM/dd').format(picked);
      });
    }
  }

  void _validateUniqueInputs() {
    final seenCodes = <String>{};

    for (final roll in _rolls) {
      final code = roll.fabricCodeController.text.trim();
      if (_mode == _FabricEntryMode.newFabric && code.isNotEmpty && !seenCodes.add(code)) {
        throw StateError('كود القماش مكرر داخل نفس الدفعة: $code');
      }
    }

    if (_mode == _FabricEntryMode.newFabric) {
      final existingCodes = _availableInventory.map((item) => item.code.trim()).toSet();
      for (final roll in _rolls) {
        final code = roll.fabricCodeController.text.trim();
        if (code.isNotEmpty && existingCodes.contains(code)) {
          throw StateError('كود القماش موجود بالفعل في المخزون: $code');
        }
      }
    }
  }

  Future<void> _saveBatch() async {
    if (_isSaving) return;

    if (_selectedSupplierId == null || _selectedSupplierId! <= 0) {
      _showMessage('الرجاء اختيار المورد.');
      return;
    }

    final invoice = _invoiceNumberController.text.trim();
    if (invoice.isEmpty) {
      _showMessage('الرجاء إدخال رقم فاتورة المورد.');
      return;
    }

    if (_rolls.isEmpty) {
      _showMessage('الرجاء إضافة رول قماش واحد على الأقل.');
      return;
    }

    try {
      _validateUniqueInputs();
    } catch (error) {
      _showMessage(error.toString().replaceFirst('Exception: ', '').replaceFirst('StateError: ', ''));
      return;
    }

    for (var i = 0; i < _rolls.length; i++) {
      final roll = _rolls[i];
      final code = roll.fabricCodeController.text.trim();
      final type = roll.fabricTypeController.text.trim();
      final width = double.tryParse(roll.fabricWidthController.text.trim().replaceAll(',', '.')) ?? 0;
      final yards = double.tryParse(roll.quantityYardsController.text.trim().replaceAll(',', '.')) ?? 0;
      final price = double.tryParse(roll.yardPriceController.text.trim().replaceAll(',', '.')) ?? 0;
      final color = roll.fabricColorController.text.trim();

      if (code.isEmpty) {
        _showMessage('الرجاء إدخال كود القماش للرول رقم ${i + 1}.');
        return;
      }
      if (type.isEmpty) {
        _showMessage('الرجاء إدخال نوع القماش للرول رقم ${i + 1}.');
        return;
      }
      if (width <= 0) {
        _showMessage('عرض القماش يجب أن يكون أكبر من الصفر للرول رقم ${i + 1}.');
        return;
      }
      if (yards <= 0) {
        _showMessage('عدد الياردات يجب أن يكون أكبر من الصفر للرول رقم ${i + 1}.');
        return;
      }
      if (price <= 0) {
        _showMessage('سعر الياردة يجب أن يكون أكبر من الصفر للرول رقم ${i + 1}.');
        return;
      }
      if (color.isEmpty) {
        _showMessage('الرجاء إدخال لون القماش للرول رقم ${i + 1}.');
        return;
      }
    }

    setState(() => _isSaving = true);

    final batchCatalogNumber = _catalogNumberController.text.trim();
    final payload = {
      'supplierId': _selectedSupplierId,
      'invoiceNumber': invoice,
      'purchaseDate': _selectedDate.toIso8601String(),
      'notes': _notesController.text.trim(),
      'rolls': _rolls.map((roll) {
        return {
          'fabricCode': _mode == _FabricEntryMode.renewExisting && _selectedExistingFabric != null
              ? _selectedExistingFabric!.code
              : roll.fabricCodeController.text.trim(),
          'catalogNumber': batchCatalogNumber.isNotEmpty ? batchCatalogNumber : roll.catalogNumberController.text.trim(),
          'fabricType': _mode == _FabricEntryMode.renewExisting && _selectedExistingFabric != null
              ? _selectedExistingFabric!.name
              : roll.fabricTypeController.text.trim(),
          'fabricColor': roll.fabricColorController.text.trim(),
          'fabricWidth': double.tryParse(roll.fabricWidthController.text.trim().replaceAll(',', '.')) ?? 58,
          'quantityYards': double.tryParse(roll.quantityYardsController.text.trim().replaceAll(',', '.')) ?? 0,
          'yardPrice': double.tryParse(roll.yardPriceController.text.trim().replaceAll(',', '.')) ?? 0,
        };
      }).toList(),
    };

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/fabric-batches'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (response.statusCode >= 200 && response.statusCode < 300) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            backgroundColor: Theme.of(context).colorScheme.primary,
            content: Text('تم استلام دفعة الأقمشة بنجاح وإضافتها للمخزون (${_rolls.length} رولات).'),
          ),
        );
        Navigator.of(context).pop(true);
      } else {
        final body = response.body;
        _showMessage('تعذر حفظ الدفعة: ${body.isNotEmpty ? body : response.statusCode}');
      }
    } catch (e) {
      _showMessage('حدث خطأ أثناء الاتصال بالخادم: $e');
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  void _showMessage(String text) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final money = NumberFormat('#,##0.00');
    final quantity = NumberFormat('#,##0.##');

    return Scaffold(
      appBar: AppBar(
        title: const Text('إدارة الأقمشة - إضافة دفعة قماش'),
        actions: [
          IconButton(
            tooltip: 'إضافة رول جديد',
            onPressed: _addRoll,
            icon: const Icon(Icons.add),
          ),
        ],
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _buildModeSwitcher(theme),
              const SizedBox(height: 12),
              if (_mode == _FabricEntryMode.renewExisting) _buildRenewSection(theme) else _buildNewFabricSection(theme),
              const SizedBox(height: 12),
              _buildSummaryHeader(theme, money, quantity),
              const SizedBox(height: 12),
              _buildBatchHeaderCard(theme),
              const SizedBox(height: 14),
              Row(
                children: [
                  const Icon(Icons.texture_outlined, size: 22),
                  const SizedBox(width: 8),
                  Text(
                    'جدول إدخال رولات الأقمشة',
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const Spacer(),
                  FilledButton.tonalIcon(
                    style: FilledButton.styleFrom(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                    ),
                    onPressed: _addRoll,
                    icon: const Icon(Icons.add, size: 16),
                    label: const Text('إضافة رول جديد'),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              for (var i = 0; i < _rolls.length; i++) ...[
                _buildRollCard(theme, i),
                const SizedBox(height: 10),
              ],
              const SizedBox(height: 14),
              SizedBox(
                height: 48,
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: theme.colorScheme.primary,
                    foregroundColor: theme.colorScheme.onPrimary,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  onPressed: _isSaving ? null : _saveBatch,
                  icon: _isSaving
                      ? SizedBox.square(
                          dimension: 20,
                          child: CircularProgressIndicator(strokeWidth: 2, color: theme.colorScheme.onPrimary),
                        )
                      : const Icon(Icons.inventory_2_outlined),
                  label: Text(_isSaving ? 'جارٍ حفظ واستلام الدفعة...' : 'حفظ دفعة القماش',
                      style: const TextStyle(fontSize: 15, fontWeight: FontWeight.bold)),
                ),
              ),
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildModeSwitcher(ThemeData theme) {
    return SegmentedButton<_FabricEntryMode>(
      segments: const [
        ButtonSegment<_FabricEntryMode>(value: _FabricEntryMode.renewExisting, label: Text('تجديد قماش موجود')),
        ButtonSegment<_FabricEntryMode>(value: _FabricEntryMode.newFabric, label: Text('إضافة قماش جديد')),
      ],
      selected: {_mode},
      onSelectionChanged: (selection) {
        setState(() {
          _mode = selection.first;
          if (_mode == _FabricEntryMode.renewExisting && _selectedExistingFabric != null) {
            _applySelectedFabric();
          } else {
            _catalogNumberController.text = _FabricBatchUtils.nextCatalogNumber(
              _availableInventory.map((item) => item.catalogNumber).toList(),
              prefix: 'CAT',
            );
            _fabricTypeController.text = 'قماش تفصيل';
            _fabricWidthController.text = '58';
            _yardPriceController.text = '';
            _inchPriceController.text = '0.000';
            _rollCountController.text = '1';
            _updateRollsFromCount();
          }
        });
      },
    );
  }

  Widget _buildRenewSection(ThemeData theme) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('تجديد قماش موجود', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            DropdownButtonFormField<_InventoryFabricItem>(
              initialValue: _selectedExistingFabric,
              isExpanded: true,
              decoration: _inputDecoration(theme, 'اختر القماش الحالي *'),
              items: _availableInventory.map((item) {
                return DropdownMenuItem<_InventoryFabricItem>(
                  value: item,
                  child: Text('${item.code} • ${item.name} • ${item.color}', overflow: TextOverflow.ellipsis),
                );
              }).toList(),
              onChanged: (value) {
                setState(() {
                  _selectedExistingFabric = value;
                  _applySelectedFabric();
                });
              },
            ),
            const SizedBox(height: 12),
            if (_selectedExistingFabric != null)
              Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  _miniInfo(theme, 'الكود', _selectedExistingFabric!.code),
                  _miniInfo(theme, 'اللون', _selectedExistingFabric!.color.isEmpty ? 'غير محدد' : _selectedExistingFabric!.color),
                  _miniInfo(theme, 'المتاح', '${_selectedExistingFabric!.availableQuantity} ياردة'),
                  _miniInfo(theme, 'المستهلك', '${_selectedExistingFabric!.consumedQuantity} ياردة'),
                  _miniInfo(theme, 'السعر', '${_selectedExistingFabric!.unitPrice} ريال/ياردة'),
                ],
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildNewFabricSection(ThemeData theme) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('إضافة قماش جديد', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                SizedBox(
                  width: 220,
                  child: TextFormField(
                    controller: _fabricTypeController,
                    textInputAction: TextInputAction.next,
                    onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                    decoration: _inputDecoration(theme, 'نوع القماش *'),
                  ),
                ),
                SizedBox(
                  width: 220,
                  child: TextFormField(
                    controller: _catalogNumberController,
                    readOnly: true,
                    decoration: _inputDecoration(theme, 'رقم الكتالوج التلقائي'),
                  ),
                ),
                SizedBox(
                  width: 220,
                  child: TextFormField(
                    controller: _fabricWidthController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    textInputAction: TextInputAction.next,
                    onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                    decoration: _inputDecoration(theme, 'عرض القماش (بوصة) *'),
                    onChanged: (_) => _updateRollsFromCount(),
                  ),
                ),
                SizedBox(
                  width: 220,
                  child: TextFormField(
                    controller: _yardPriceController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    textInputAction: TextInputAction.next,
                    onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                    decoration: _inputDecoration(theme, 'سعر الياردة (ريال) *'),
                    onChanged: (_) {
                      final value = double.tryParse(_yardPriceController.text.trim().replaceAll(',', '.')) ?? 0;
                      _inchPriceController.text = value > 0 ? _FabricBatchUtils.inchPriceFromYardPrice(value).toStringAsFixed(3) : '0.000';
                      _updateRollsFromCount();
                    },
                  ),
                ),
                SizedBox(
                  width: 220,
                  child: TextFormField(
                    controller: _inchPriceController,
                    readOnly: true,
                    decoration: _inputDecoration(theme, 'سعر البوصة (تلقائي)'),
                  ),
                ),
                SizedBox(
                  width: 220,
                  child: TextFormField(
                    controller: _rollCountController,
                    keyboardType: TextInputType.number,
                    textInputAction: TextInputAction.next,
                    onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                    decoration: _inputDecoration(theme, 'عدد الرولات *'),
                    onChanged: (_) => _updateRollsFromCount(),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _miniInfo(ThemeData theme, String label, String value) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        border: Border.all(color: theme.colorScheme.outlineVariant),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.onSurfaceVariant)),
          const SizedBox(height: 2),
          Text(value, style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }

  Widget _buildSummaryHeader(ThemeData theme, NumberFormat money, NumberFormat quantity) {
    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth < 600 ? constraints.maxWidth : (constraints.maxWidth - 20) / 3;
      return Wrap(
        spacing: 10,
        runSpacing: 10,
        children: [
          _statBox(
            width: width,
            icon: Icons.numbers_outlined,
            title: 'إجمالي الرولات',
            value: '${_rolls.length} رول',
            theme: theme,
          ),
          _statBox(
            width: width,
            icon: Icons.straighten_outlined,
            title: 'إجمالي الياردات',
            value: '${quantity.format(_totalYards)} ياردة',
            theme: theme,
          ),
          _statBox(
            width: width,
            icon: Icons.payments_outlined,
            title: 'إجمالي قيمة الدفعة',
            value: '${money.format(_totalBatchCost)} ريال',
            theme: theme,
            emphasized: true,
          ),
        ],
      );
    });
  }

  Widget _statBox({
    required double width,
    required IconData icon,
    required String title,
    required String value,
    required ThemeData theme,
    bool emphasized = false,
  }) {
    return SizedBox(
      width: width,
      height: 76,
      child: Card(
        elevation: 0,
        color: emphasized ? theme.colorScheme.primaryContainer : theme.colorScheme.surfaceContainerLow,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
          side: BorderSide(
            color: emphasized ? theme.colorScheme.primary : theme.colorScheme.outlineVariant.withValues(alpha: 0.6),
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          child: Row(
            children: [
              CircleAvatar(
                radius: 20,
                backgroundColor: emphasized ? theme.colorScheme.primary : theme.colorScheme.secondaryContainer,
                foregroundColor: emphasized ? theme.colorScheme.onPrimary : theme.colorScheme.onSecondaryContainer,
                child: Icon(icon, size: 20),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: theme.textTheme.labelMedium?.copyWith(
                        fontSize: 14,
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                    Text(
                      value,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: emphasized ? theme.colorScheme.onPrimaryContainer : theme.colorScheme.onSurface,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildBatchHeaderCard(ThemeData theme) {
    final palette = widget.palette;
    return Card(
      elevation: 0,
      color: palette.surfaceSoft,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: palette.border.withValues(alpha: 0.7)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.receipt_long_outlined, size: 20),
                const SizedBox(width: 8),
                Text(
                  'بيانات رأس الدفعة',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            LayoutBuilder(builder: (context, constraints) {
              final isWide = constraints.maxWidth > 700;
              final halfWidth = isWide ? (constraints.maxWidth - 12) / 2 : constraints.maxWidth;
              return Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  SizedBox(
                    width: halfWidth,
                    child: _loadingSuppliers
                        ? const Center(child: Padding(padding: EdgeInsets.all(8), child: CircularProgressIndicator()))
                        : DropdownButtonFormField<int>(
                            initialValue: _selectedSupplierId,
                            isExpanded: true,
                            decoration: _inputDecoration(theme, 'المورد *'),
                            dropdownColor: theme.colorScheme.surfaceContainerHighest,
                            items: _suppliers.map((s) {
                              return DropdownMenuItem<int>(
                                value: s.id,
                                child: Text('${s.name} (${s.code.isNotEmpty ? s.code : s.id})', maxLines: 1, overflow: TextOverflow.ellipsis),
                              );
                            }).toList(),
                            onChanged: (val) => setState(() => _selectedSupplierId = val),
                          ),
                  ),
                  SizedBox(
                    width: halfWidth,
                    child: TextFormField(
                      controller: _invoiceNumberController,
                      textInputAction: TextInputAction.next,
                      onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                      decoration: _inputDecoration(theme, 'رقم فاتورة المورد *'),
                    ),
                  ),
                  SizedBox(
                    width: halfWidth,
                    child: TextFormField(
                      controller: _purchaseDateController,
                      readOnly: true,
                      onTap: _pickDate,
                      onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                      decoration: _inputDecoration(theme, 'تاريخ الشراء').copyWith(
                        suffixIcon: IconButton(
                          icon: const Icon(Icons.calendar_today_outlined, size: 18),
                          onPressed: _pickDate,
                        ),
                      ),
                    ),
                  ),
                  SizedBox(
                    width: halfWidth,
                    child: TextFormField(
                      controller: _notesController,
                      textInputAction: TextInputAction.next,
                      onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                      decoration: _inputDecoration(theme, 'ملاحظات عامة'),
                    ),
                  ),
                ],
              );
            }),
          ],
        ),
      ),
    );
  }

  Widget _buildRollCard(ThemeData theme, int index) {
    final roll = _rolls[index];
    final palette = widget.palette;
    return Card(
      elevation: 0,
      color: palette.surfaceCard,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: palette.border),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 14,
                  backgroundColor: theme.colorScheme.primaryContainer,
                  child: Text('${index + 1}', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold, color: theme.colorScheme.onPrimaryContainer)),
                ),
                const SizedBox(width: 8),
                Text('رول رقم ${index + 1}', style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold)),
                const Spacer(),
                if (_rolls.length > 1)
                  IconButton(
                    tooltip: 'حذف الرول',
                    visualDensity: VisualDensity.compact,
                    color: theme.colorScheme.error,
                    onPressed: () => _removeRoll(index),
                    icon: const Icon(Icons.delete_outline, size: 18),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            LayoutBuilder(
              builder: (context, constraints) {
                const spacing = 8.0;
                final width = constraints.maxWidth < 600 ? constraints.maxWidth : (constraints.maxWidth - spacing) / 2;

                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      spacing: spacing,
                      runSpacing: spacing,
                      children: [
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.fabricCodeController,
                            readOnly: true,
                            canRequestFocus: false,
                            decoration: _inputDecoration(theme, 'كود القماش').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontWeight: FontWeight.w600),
                          ),
                        ),
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.catalogNumberController,
                            readOnly: true,
                            canRequestFocus: false,
                            decoration: _inputDecoration(theme, 'رقم الكتالوج').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontWeight: FontWeight.w600),
                          ),
                        ),
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.fabricTypeController,
                            readOnly: true,
                            canRequestFocus: false,
                            decoration: _inputDecoration(theme, 'نوع القماش').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontWeight: FontWeight.w600),
                          ),
                        ),
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.fabricWidthController,
                            readOnly: true,
                            canRequestFocus: false,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            decoration: _inputDecoration(theme, 'عرض القماش (بوصة)').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontWeight: FontWeight.w600),
                          ),
                        ),
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.yardPriceController,
                            readOnly: true,
                            canRequestFocus: false,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            decoration: _inputDecoration(theme, 'سعر الياردة (ريال)').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontWeight: FontWeight.w600),
                          ),
                        ),
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.inchPriceController,
                            readOnly: true,
                            canRequestFocus: false,
                            decoration: _inputDecoration(theme, 'سعر البوصة').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.primary, fontWeight: FontWeight.bold),
                          ),
                        ),
                        SizedBox(
                          width: width,
                          child: TextFormField(
                            controller: roll.totalRollCostController,
                            readOnly: true,
                            canRequestFocus: false,
                            decoration: _inputDecoration(theme, 'إجمالي الرول').copyWith(
                              fillColor: theme.colorScheme.surfaceContainerLow,
                            ),
                            style: TextStyle(color: theme.colorScheme.primary, fontWeight: FontWeight.bold),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Expanded(
                          child: Autocomplete<String>(
                            initialValue: TextEditingValue(text: roll.fabricColorController.text),
                            optionsBuilder: (textVal) {
                              if (textVal.text.isEmpty) return _commonColors;
                              return _commonColors.where((c) => c.contains(textVal.text));
                            },
                            onSelected: (val) {
                              roll.fabricColorController.text = val;
                              setState(() {});
                            },
                            fieldViewBuilder: (context, textController, focusNode, onFieldSubmitted) {
                              return TextFormField(
                                controller: textController,
                                focusNode: focusNode,
                                textInputAction: TextInputAction.next,
                                onChanged: (val) => roll.fabricColorController.text = val,
                                onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                decoration: _inputDecoration(theme, 'لون القماش *'),
                              );
                            },
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: TextFormField(
                            controller: roll.quantityYardsController,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            textInputAction: TextInputAction.next,
                            onChanged: (_) {
                              setState(() => roll.updateCalculations());
                            },
                            onFieldSubmitted: (_) => FocusScope.of(context).nextFocus(),
                            decoration: _inputDecoration(theme, 'كمية القماش (ياردة) *'),
                          ),
                        ),
                      ],
                    ),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(ThemeData theme, String label) {
    final palette = widget.palette;
    return InputDecoration(
      labelText: label,
      labelStyle: theme.textTheme.bodyLarge?.copyWith(
        fontSize: 16,
        fontWeight: FontWeight.w600,
        color: palette.textSoft,
      ),
      floatingLabelBehavior: FloatingLabelBehavior.always,
      filled: true,
      fillColor: palette.surfaceSoft,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: palette.border)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: palette.border)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: palette.primary, width: 1.4)),
      isDense: true,
      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
    );
  }
}
