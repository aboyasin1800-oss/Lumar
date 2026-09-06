import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

class ToolEntryScreen extends StatefulWidget {
  const ToolEntryScreen({super.key});

  @override
  State<ToolEntryScreen> createState() => _ToolEntryScreenState();
}

class _ToolEntryScreenState extends State<ToolEntryScreen> {
  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');

  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _typeController = TextEditingController();
  final _unitController = TextEditingController(text: 'قطعة');
  final _quantityController = TextEditingController();
  final _unitPriceController = TextEditingController();
  final _supplierController = TextEditingController();
  final _invoiceController = TextEditingController();
  final _notesController = TextEditingController();
  final _searchController = TextEditingController();
  final _totalController = TextEditingController(text: '0.00');

  bool _renewExisting = false;
  bool _isSaving = false;
  List<_ToolItemRow> _items = [];
  _ToolItemRow? _selectedItem;

  static const List<String> _unitOptions = ['قطعة', 'بكرة', 'كرتون', 'علبة', 'متر', 'كيلو'];

  @override
  void initState() {
    super.initState();
    _quantityController.addListener(_syncTotal);
    _unitPriceController.addListener(_syncTotal);
    _loadExistingItems();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _typeController.dispose();
    _unitController.dispose();
    _quantityController.dispose();
    _unitPriceController.dispose();
    _supplierController.dispose();
    _invoiceController.dispose();
    _notesController.dispose();
    _searchController.dispose();
    _totalController.dispose();
    super.dispose();
  }

  Future<void> _loadExistingItems() async {
    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/tools'));
      if (response.statusCode < 200 || response.statusCode >= 300) return;
      final decoded = jsonDecode(response.body);
      if (decoded is! List) return;
      if (!mounted) return;
      setState(() {
        _items = decoded.whereType<Map<String, dynamic>>().map(_ToolItemRow.fromJson).toList();
      });
    } catch (_) {
      // ignore load errors
    }
  }

  void _syncTotal() {
    final quantity = double.tryParse(_quantityController.text.trim().replaceAll(',', '.')) ?? 0;
    final unitPrice = double.tryParse(_unitPriceController.text.trim().replaceAll(',', '.')) ?? 0;
    _totalController.text = (quantity * unitPrice).toStringAsFixed(2);
  }

  List<_ToolItemRow> get _filteredItems {
    final query = _searchController.text.trim().toLowerCase();
    if (query.isEmpty) return _items;
    return _items.where((item) {
      final haystack = [item.name, item.code, item.type].join(' ').toLowerCase();
      return haystack.contains(query);
    }).toList();
  }

  void _applySelected(_ToolItemRow item) {
    setState(() {
      _selectedItem = item;
      _nameController.text = item.name;
      _typeController.text = item.type;
      _unitController.text = item.unit.isEmpty ? 'قطعة' : item.unit;
      _quantityController.text = '1';
      _unitPriceController.text = item.unitPrice > 0 ? item.unitPrice.toStringAsFixed(2) : '';
      _supplierController.text = '';
      _invoiceController.text = '';
    });
  }

  Future<void> _save() async {
    if (_isSaving) return;
    if (!_formKey.currentState!.validate()) return;

    final quantity = double.tryParse(_quantityController.text.trim().replaceAll(',', '.')) ?? 0;
    final unitPrice = double.tryParse(_unitPriceController.text.trim().replaceAll(',', '.')) ?? 0;
    final supplierId = int.tryParse(_supplierController.text.trim());

    if (quantity <= 0) {
      _showMessage('الكمية يجب أن تكون أكبر من صفر.');
      return;
    }
    if (unitPrice <= 0) {
      _showMessage('سعر الوحدة يجب أن يكون أكبر من صفر.');
      return;
    }

    final payload = {
      'productName': _nameController.text.trim(),
      'productType': _typeController.text.trim(),
      'productCode': _selectedItem?.code ?? '',
      'unit': _unitController.text.trim(),
      'quantity': quantity,
      'unitPrice': unitPrice,
      'supplierId': supplierId != null && supplierId > 0 ? supplierId : null,
      'invoiceNumber': _invoiceController.text.trim(),
      'notes': _notesController.text.trim(),
      'renewExisting': _renewExisting,
    };

    setState(() => _isSaving = true);

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/tools'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (response.statusCode >= 200 && response.statusCode < 300) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            backgroundColor: UiPalette.primaryBlue,
            content: Text(_renewExisting ? 'تم تجديد المخزون بنجاح.' : 'تمت إضافة الأداة بنجاح.'),
          ),
        );
        Navigator.of(context).pop(true);
      } else {
        final body = response.body;
        _showMessage('تعذر حفظ الأداة: ${body.isNotEmpty ? body : response.statusCode}');
      }
    } catch (e) {
      _showMessage('حدث خطأ أثناء الحفظ: $e');
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
        title: const Text('مخزن الأدوات المستخدمة'),
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
                          ButtonSegment<bool>(value: false, label: Text('إضافة أداة جديدة')),
                          ButtonSegment<bool>(value: true, label: Text('تجديد أداة موجودة')),
                        ],
                        selected: {_renewExisting},
                        onSelectionChanged: (selection) {
                          setState(() {
                            _renewExisting = selection.first;
                            if (!_renewExisting) {
                              _selectedItem = null;
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
                            filled: true,
                            fillColor: UiPalette.softBlue,
                            hintText: 'ابحث باسم المنتج أو الكود أو النوع',
                            hintStyle: TextStyle(color: UiPalette.textSoft),
                            border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.borderSoft)),
                          ),
                          style: TextStyle(color: UiPalette.textMain),
                        ),
                        const SizedBox(height: 12),
                        if (_filteredItems.isEmpty)
                          Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(color: UiPalette.softBlue, borderRadius: BorderRadius.circular(10)),
                            child: Text('لا توجد أدوات حالياً.', style: TextStyle(color: UiPalette.textMain)),
                          )
                        else
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: _filteredItems.map((item) {
                              final selected = _selectedItem?.code == item.code;
                              return ChoiceChip(
                                label: Text(item.name),
                                selected: selected,
                                selectedColor: UiPalette.primaryBlue,
                                backgroundColor: UiPalette.softBlue,
                                labelStyle: TextStyle(color: selected ? Colors.black : UiPalette.textMain),
                                onSelected: (_) => _applySelected(item),
                              );
                            }).toList(),
                          ),
                        const SizedBox(height: 12),
                      ],
                      Wrap(
                        spacing: 12,
                        runSpacing: 12,
                        children: [
                          _field(width: 220, child: TextFormField(
                            controller: _nameController,
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'اسم المنتج مطلوب' : null,
                            decoration: _inputDecoration('اسم المنتج *'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: TextFormField(
                            controller: _typeController,
                            decoration: _inputDecoration('نوع المنتج'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: TextFormField(
                            readOnly: true,
                            controller: TextEditingController(text: _selectedItem?.code ?? 'سيُولّد تلقائيًا'),
                            decoration: _inputDecoration('كود المنتج'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: DropdownButtonFormField<String>(
                            initialValue: _unitOptions.contains(_unitController.text) ? _unitController.text : _unitOptions.first,
                            items: _unitOptions.map((unit) => DropdownMenuItem(value: unit, child: Text(unit))).toList(),
                            onChanged: (value) {
                              if (value != null) {
                                _unitController.text = value;
                              }
                            },
                            decoration: _inputDecoration('الوحدة'),
                            dropdownColor: UiPalette.surfaceCard,
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: TextFormField(
                            controller: _quantityController,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            validator: (value) {
                              final qty = double.tryParse((value ?? '').replaceAll(',', '.')) ?? 0;
                              return qty > 0 ? null : 'الكمية يجب أن تكون أكبر من صفر';
                            },
                            decoration: _inputDecoration('الكمية *'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: TextFormField(
                            controller: _unitPriceController,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            validator: (value) {
                              final price = double.tryParse((value ?? '').replaceAll(',', '.')) ?? 0;
                              return price > 0 ? null : 'سعر الوحدة يجب أن يكون أكبر من صفر';
                            },
                            decoration: _inputDecoration('سعر الوحدة *'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: TextFormField(
                            controller: _totalController,
                            readOnly: true,
                            decoration: _inputDecoration('السعر الإجمالي'),
                            style: TextStyle(color: UiPalette.textMain, fontWeight: FontWeight.bold),
                          )),
                          _field(width: 220, child: TextFormField(
                            controller: _supplierController,
                            keyboardType: TextInputType.number,
                            decoration: _inputDecoration('المورد'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 220, child: TextFormField(
                            controller: _invoiceController,
                            decoration: _inputDecoration('رقم الفاتورة'),
                            style: TextStyle(color: UiPalette.textMain),
                          )),
                          _field(width: 420, child: TextFormField(
                            controller: _notesController,
                            maxLines: 2,
                            decoration: _inputDecoration('ملاحظات'),
                            style: TextStyle(color: UiPalette.textMain),
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
                          _summaryChip('الكمية', _quantityController.text.trim().isEmpty ? '0' : _quantityController.text.trim()),
                          _summaryChip('سعر الوحدة', _unitPriceController.text.trim().isEmpty ? '0.00' : _unitPriceController.text.trim()),
                          _summaryChip('السعر الإجمالي', _totalController.text.trim().isEmpty ? '0.00' : _totalController.text.trim()),
                          _summaryChip('المورد', _supplierController.text.trim().isEmpty ? 'غير محدد' : _supplierController.text.trim()),
                          _summaryChip('رقم الفاتورة', _invoiceController.text.trim().isEmpty ? 'غير محدد' : _invoiceController.text.trim()),
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
                style: FilledButton.styleFrom(backgroundColor: UiPalette.primaryBlue, foregroundColor: Colors.black, minimumSize: const Size.fromHeight(50)),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _field({required double width, required Widget child}) => SizedBox(width: width, child: child);

  InputDecoration _inputDecoration(String label) => InputDecoration(
        labelText: label,
        labelStyle: TextStyle(color: UiPalette.textSoft),
        filled: true,
        fillColor: UiPalette.softBlue,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.borderSoft)),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.borderSoft)),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide(color: UiPalette.primaryBlue, width: 1.4)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
      );

  Widget _summaryChip(String title, String value) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(color: UiPalette.softBlue, borderRadius: BorderRadius.circular(10), border: Border.all(color: UiPalette.borderSoft)),
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

class _ToolItemRow {
  const _ToolItemRow({required this.id, required this.code, required this.name, required this.type, required this.quantity, required this.available, required this.used, required this.unit, required this.unitPrice});

  factory _ToolItemRow.fromJson(Map<String, dynamic> json) => _ToolItemRow(
        id: (json['inventoryItemId'] as num?)?.toInt() ?? 0,
        code: (json['itemCode'] ?? '').toString(),
        name: (json['itemName'] ?? '').toString(),
        type: (json['category'] ?? '').toString(),
        quantity: (json['currentQuantity'] as num?)?.toDouble() ?? 0,
        available: (json['availableQuantity'] as num?)?.toDouble() ?? 0,
        used: ((json['currentQuantity'] as num?)?.toDouble() ?? 0) - ((json['availableQuantity'] as num?)?.toDouble() ?? 0),
        unit: (json['unit'] ?? '').toString(),
        unitPrice: (json['yardPrice'] as num?)?.toDouble() ?? (json['inchPrice'] as num?)?.toDouble() ?? 0,
      );

  final int id;
  final String code;
  final String name;
  final String type;
  final double quantity;
  final double available;
  final double used;
  final String unit;
  final double unitPrice;
}
