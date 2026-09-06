import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

class ImportedProductEntryScreen extends StatefulWidget {
  const ImportedProductEntryScreen({super.key});

  @override
  State<ImportedProductEntryScreen> createState() => _ImportedProductEntryScreenState();
}

class _ImportedProductEntryScreenState extends State<ImportedProductEntryScreen> {
  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');

  final _formKey = GlobalKey<FormState>();
  final _productNameController = TextEditingController();
  final _productTypeController = TextEditingController();
  final _productCodeController = TextEditingController();
  final _quantityController = TextEditingController();
  final _purchasePriceController = TextEditingController();
  final _sellingPriceController = TextEditingController();
  final _supplierController = TextEditingController();
  final _notesController = TextEditingController();
  final _searchController = TextEditingController();
  final _totalController = TextEditingController(text: '0.00');

  bool _isSaving = false;
  bool _renewExisting = false;
  List<_ImportedProductRow> _products = [];
  _ImportedProductRow? _selectedProduct;

  @override
  void initState() {
    super.initState();
    _quantityController.addListener(_syncTotal);
    _purchasePriceController.addListener(_syncTotal);
    _loadSuppliers();
    _loadExistingProducts();
  }

  @override
  void dispose() {
    _productNameController.dispose();
    _productTypeController.dispose();
    _productCodeController.dispose();
    _quantityController.dispose();
    _purchasePriceController.dispose();
    _sellingPriceController.dispose();
    _supplierController.dispose();
    _notesController.dispose();
    _searchController.dispose();
    _totalController.dispose();
    super.dispose();
  }

  void _syncTotal() {
    final quantity = double.tryParse(_quantityController.text.trim().replaceAll(',', '.')) ?? 0;
    final purchase = double.tryParse(_purchasePriceController.text.trim().replaceAll(',', '.')) ?? 0;
    _totalController.text = (quantity * purchase).toStringAsFixed(2);
  }

  Future<void> _loadSuppliers() async {
    try {
      final response = await http.get(Uri.parse('$_baseUrl/suppliers'));
      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is List) {
          final list = decoded.whereType<Map<String, dynamic>>();
          if (mounted && list.isNotEmpty) {
            final supplierId = int.tryParse(_supplierController.text.trim());
            if (supplierId == null || supplierId <= 0) {
              _supplierController.text = (list.first['supplierId'] as num?)?.toString() ?? '';
            }
          }
        }
      }
    } catch (_) {
      // Ignore supplier loading issues; the field stays optional.
    }
  }

  Future<void> _loadExistingProducts() async {
    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/imported'));
      if (response.statusCode < 200 || response.statusCode >= 300) return;
      final decoded = jsonDecode(response.body);
      if (decoded is! List) return;
      final products = decoded.whereType<Map<String, dynamic>>().map(_ImportedProductRow.fromJson).toList();
      if (mounted) setState(() => _products = products);
    } catch (_) {
      // ignore load errors; user can still add a new product
    }
  }

  double get _totalCost {
    final quantity = double.tryParse(_quantityController.text.trim().replaceAll(',', '.')) ?? 0;
    final purchase = double.tryParse(_purchasePriceController.text.trim().replaceAll(',', '.')) ?? 0;
    return quantity * purchase;
  }

  List<_ImportedProductRow> get _filteredProducts {
    final query = _searchController.text.trim().toLowerCase();
    if (query.isEmpty) return _products;
    return _products.where((product) {
      final haystack = [product.name, product.productType, product.code].join(' ').toLowerCase();
      return haystack.contains(query);
    }).toList();
  }

  void _applySelectedProduct(_ImportedProductRow product) {
    setState(() {
      _selectedProduct = product;
      _productNameController.text = product.name;
      _productTypeController.text = product.productType;
      _productCodeController.text = product.code;
      _quantityController.text = '1';
      _purchasePriceController.text = product.purchasePrice > 0 ? product.purchasePrice.toStringAsFixed(2) : '';
      _sellingPriceController.text = product.sellingPrice > 0 ? product.sellingPrice.toStringAsFixed(2) : '';
    });
  }

  Future<void> _save() async {
    if (_isSaving) return;
    if (!_formKey.currentState!.validate()) return;

    final quantity = double.tryParse(_quantityController.text.trim().replaceAll(',', '.')) ?? 0;
    final purchasePrice = double.tryParse(_purchasePriceController.text.trim().replaceAll(',', '.')) ?? 0;
    final sellingPrice = double.tryParse(_sellingPriceController.text.trim().replaceAll(',', '.')) ?? 0;

    if (quantity <= 0) {
      _showMessage('الكمية يجب أن تكون أكبر من صفر.');
      return;
    }
    if (purchasePrice <= 0) {
      _showMessage('سعر الشراء للحبة يجب أن يكون أكبر من صفر.');
      return;
    }
    if (sellingPrice <= 0) {
      _showMessage('سعر البيع المقترح يجب أن يكون أكبر من صفر.');
      return;
    }

    final supplierId = int.tryParse(_supplierController.text.trim());
    final payload = {
      'productName': _productNameController.text.trim(),
      'productType': _productTypeController.text.trim(),
      'productCode': _productCodeController.text.trim(),
      'unit': 'حبة',
      'quantity': quantity,
      'purchasePrice': purchasePrice,
      'sellingPrice': sellingPrice,
      'supplierId': supplierId != null && supplierId > 0 ? supplierId : null,
      'notes': _notesController.text.trim(),
      'category': 'Imported',
      'renewExisting': _renewExisting,
    };

    setState(() => _isSaving = true);

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/imported'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (response.statusCode >= 200 && response.statusCode < 300) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            backgroundColor: UiPalette.primaryBlue,
            content: Text(_renewExisting ? 'تم تجديد المنتج بنجاح.' : 'تمت إضافة المنتج المستورد بنجاح.'),
          ),
        );
        Navigator.of(context).pop(true);
      } else {
        final msg = response.body;
        _showMessage('تعذر حفظ المنتج: ${msg.isNotEmpty ? msg : response.statusCode}');
      }
    } catch (e) {
      _showMessage('حدث خطأ أثناء حفظ المنتج: $e');
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
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.softBlue,
        foregroundColor: UiPalette.textMain,
        title: const Text('مخزن المنتجات المستوردة'),
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Card(
                color: UiPalette.surfaceCard,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SegmentedButton<bool>(
                        segments: const [
                          ButtonSegment<bool>(value: false, label: Text('منتج جديد')),
                          ButtonSegment<bool>(value: true, label: Text('تجديد منتج موجود')),
                        ],
                        selected: {_renewExisting},
                        onSelectionChanged: (selection) {
                          setState(() {
                            _renewExisting = selection.first;
                            if (!_renewExisting) {
                              _selectedProduct = null;
                              _searchController.clear();
                            }
                          });
                        },
                      ),
                      const SizedBox(height: 12),
                      if (_renewExisting) ...[
                        TextField(
                          controller: _searchController,
                          decoration: InputDecoration(
                            fillColor: UiPalette.softBlue,
                            filled: true,
                            hintText: 'ابحث باسم المنتج أو الكود أو النوع',
                            hintStyle: TextStyle(color: UiPalette.textSoft),
                            border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.borderSoft)),
                          ),
                          style: TextStyle(color: UiPalette.textMain),
                        ),
                        const SizedBox(height: 12),
                        if (_filteredProducts.isEmpty)
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: UiPalette.softBlue,
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: Text('لا توجد منتجات مستوردة حالياً.', style: TextStyle(color: UiPalette.textMain)),
                          )
                        else
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: _filteredProducts.map((product) {
                              final selected = _selectedProduct?.code == product.code;
                              return ChoiceChip(
                                label: Text(product.name),
                                selected: selected,
                                selectedColor: UiPalette.primaryBlue,
                                backgroundColor: UiPalette.softBlue,
                                labelStyle: TextStyle(color: selected ? Colors.black : UiPalette.textMain),
                                onSelected: (_) => _applySelectedProduct(product),
                              );
                            }).toList(),
                          ),
                        const SizedBox(height: 12),
                      ],
                      Wrap(
                        spacing: 12,
                        runSpacing: 12,
                        children: [
                          _field(width: 220, label: 'اسم المنتج *', child: TextFormField(
                            controller: _productNameController,
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'اسم المنتج مطلوب' : null,
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('اسم المنتج *'),
                          )),
                          _field(width: 220, label: 'نوع المنتج', child: TextFormField(
                            controller: _productTypeController,
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('نوع المنتج'),
                          )),
                          _field(width: 220, label: 'كود المنتج *', child: TextFormField(
                            controller: _productCodeController,
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'كود المنتج مطلوب' : null,
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('كود المنتج *'),
                          )),
                          _field(width: 220, label: 'الكمية *', child: TextFormField(
                            controller: _quantityController,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            validator: (value) {
                              final qty = double.tryParse((value ?? '').replaceAll(',', '.')) ?? 0;
                              return qty > 0 ? null : 'أدخل كمية أكبر من صفر';
                            },
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('الكمية *'),
                          )),
                          _field(width: 220, label: 'سعر الشراء للحبة *', child: TextFormField(
                            controller: _purchasePriceController,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            validator: (value) {
                              final price = double.tryParse((value ?? '').replaceAll(',', '.')) ?? 0;
                              return price > 0 ? null : 'أدخل سعر شراء أكبر من صفر';
                            },
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('سعر الشراء للحبة *'),
                          )),
                          _field(width: 220, label: 'السعر الإجمالي', child: TextFormField(
                            controller: _totalController,
                            readOnly: true,
                            style: TextStyle(color: UiPalette.textMain, fontWeight: FontWeight.bold),
                            decoration: _inputDecoration('السعر الإجمالي').copyWith(fillColor: UiPalette.softBlue),
                          )),
                          _field(width: 220, label: 'سعر البيع المقترح *', child: TextFormField(
                            controller: _sellingPriceController,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            validator: (value) {
                              final price = double.tryParse((value ?? '').replaceAll(',', '.')) ?? 0;
                              return price > 0 ? null : 'أدخل سعر بيع أكبر من صفر';
                            },
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('سعر البيع المقترح *'),
                          )),
                          _field(width: 220, label: 'المورد', child: TextFormField(
                            controller: _supplierController,
                            keyboardType: TextInputType.number,
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('المورد'),
                          )),
                          _field(width: 420, label: 'ملاحظات', child: TextFormField(
                            controller: _notesController,
                            maxLines: 2,
                            style: TextStyle(color: UiPalette.textMain),
                            decoration: _inputDecoration('ملاحظات'),
                          )),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),
              Card(
                color: UiPalette.surfaceCard,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('ملخص العملية', style: Theme.of(context).textTheme.titleMedium?.copyWith(color: UiPalette.textMain, fontWeight: FontWeight.bold)),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 10,
                        runSpacing: 10,
                        children: [
                          _summaryChip('اسم المنتج', _productNameController.text.trim().isEmpty ? 'غير محدد' : _productNameController.text.trim()),
                          _summaryChip('النوع', _productTypeController.text.trim().isEmpty ? 'غير محدد' : _productTypeController.text.trim()),
                          _summaryChip('الكمية', _quantityController.text.trim().isEmpty ? '0' : _quantityController.text.trim()),
                          _summaryChip('سعر الشراء', _purchasePriceController.text.trim().isEmpty ? '0.00' : _purchasePriceController.text.trim()),
                          _summaryChip('السعر الإجمالي', _totalCost.toStringAsFixed(2)),
                          _summaryChip('سعر البيع', _sellingPriceController.text.trim().isEmpty ? '0.00' : _sellingPriceController.text.trim()),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: _isSaving ? null : _save,
                icon: _isSaving ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.save_outlined),
                label: Text(_isSaving ? 'جارٍ الحفظ...' : 'حفظ'),
                style: FilledButton.styleFrom(
                  backgroundColor: UiPalette.primaryBlue,
                  foregroundColor: Colors.black,
                  minimumSize: const Size.fromHeight(50),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _field({required double width, required String label, required Widget child}) {
    return SizedBox(
      width: width,
      child: child,
    );
  }

  InputDecoration _inputDecoration(String label) {
    return InputDecoration(
      labelText: label,
      labelStyle: TextStyle(color: UiPalette.textSoft),
      filled: true,
      fillColor: UiPalette.softBlue,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.borderSoft)),
      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.borderSoft)),
      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.primaryBlue, width: 1.4)),
      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
    );
  }

  Widget _summaryChip(String title, String value) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: UiPalette.softBlue,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: TextStyle(fontSize: 11, color: UiPalette.textSoft)),
          const SizedBox(height: 4),
          Text(value, style: TextStyle(color: UiPalette.textMain, fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }
}

class _ImportedProductRow {
  const _ImportedProductRow({
    required this.id,
    required this.name,
    required this.productType,
    required this.code,
    required this.quantity,
    required this.purchasePrice,
    required this.sellingPrice,
  });

  factory _ImportedProductRow.fromJson(Map<String, dynamic> json) => _ImportedProductRow(
        id: (json['importedReadyMadeProductId'] as num?)?.toInt() ?? 0,
        name: (json['productName'] ?? '').toString(),
        productType: (json['productType'] ?? '').toString(),
        code: (json['productCode'] ?? '').toString(),
        quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
        purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
        sellingPrice: (json['sellingPrice'] as num?)?.toDouble() ?? 0,
      );

  final int id;
  final String name;
  final String productType;
  final String code;
  final double quantity;
  final double purchasePrice;
  final double sellingPrice;
}
