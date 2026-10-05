import 'dart:convert';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

class InventoryIssuanceScreen extends StatelessWidget {
  const InventoryIssuanceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('إدارة الصرف المخزني والعهد'),
        centerTitle: true,
      ),
      body: DefaultTabController(
        length: 4,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 8),
            Container(
              height: 52,
              decoration: BoxDecoration(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: UiPalette.borderSoft.withValues(alpha: 0.55),
                ),
              ),
              child: TabBar(
                isScrollable: false,
                tabAlignment: TabAlignment.fill,
                labelColor: UiPalette.textMain,
                unselectedLabelColor: UiPalette.textSoft,
                indicatorColor: const Color.fromARGB(255, 18, 247, 216),
                dividerColor: Colors.transparent,
                indicatorSize: TabBarIndicatorSize.tab,
                indicator: const UnderlineTabIndicator(
                  borderSide: BorderSide(
                    width: 3,
                    color: Color.fromARGB(255, 18, 247, 216),
                  ),
                  insets: EdgeInsets.symmetric(horizontal: 6),
                ),
                labelStyle: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                  fontSize: 16,
                ),
                unselectedLabelStyle:
                    Theme.of(context).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w600,
                          fontSize: 12,
                        ),
                tabs: const [
                  Tab(text: 'الصرف التشغيلي'),
                  Tab(text: 'صرف العهدة'),
                  Tab(text: 'العهد المفتوحة'),
                  Tab(text: 'سجل العمليات'),
                ],
              ),
            ),
            const SizedBox(height: 12),
            Expanded(
              child: TabBarView(
                children: [
                  const _OperationalIssueForm(),
                  _buildPlaceholder(
                    context,
                    'سيتم تنفيذ صرف العهدة في المهمة التالية',
                  ),
                  _buildPlaceholder(
                    context,
                    'سيتم عرض العهد المفتوحة في المهمة التالية',
                  ),
                  _buildPlaceholder(
                    context,
                    'سيتم عرض سجل العمليات في المهمة التالية',
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPlaceholder(BuildContext context, String message) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.hourglass_bottom_rounded,
              size: 48,
              color: Theme.of(context).colorScheme.outline,
            ),
            const SizedBox(height: 16),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: Theme.of(context).colorScheme.outline,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _OperationalIssueForm extends StatefulWidget {
  const _OperationalIssueForm();

  @override
  State<_OperationalIssueForm> createState() => _OperationalIssueFormState();
}

class _OperationalIssueFormState extends State<_OperationalIssueForm> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final _formKey = GlobalKey<FormState>();
  final _quantityController = TextEditingController();
  final _reasonController = TextEditingController();
  final _notesController = TextEditingController();

  final List<_ToolOption> _tools = <_ToolOption>[];
  bool _isLoadingTools = true;
  bool _isSubmitting = false;
  _ToolOption? _selectedTool;

  @override
  void initState() {
    super.initState();
    _loadTools();
  }

  @override
  void dispose() {
    _quantityController.dispose();
    _reasonController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _loadTools() async {
    setState(() {
      _isLoadingTools = true;
    });

    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/tools'));
      if (!mounted) return;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is List) {
          final items = decoded
              .whereType<Map<String, dynamic>>()
              .map(_ToolOption.fromJson)
              .toList();

          setState(() {
            _tools
              ..clear()
              ..addAll(items);
            if (_selectedTool == null && _tools.isNotEmpty) {
              _selectedTool = _tools.first;
            }
            _isLoadingTools = false;
          });
          return;
        }
      }

      setState(() => _isLoadingTools = false);
      _showMessage('تعذر تحميل قائمة الأدوات (${response.statusCode})');
    } catch (error) {
      if (!mounted) return;
      setState(() => _isLoadingTools = false);
      _showMessage('تعذر الاتصال بالخادم: ${error.toString()}');
    }
  }

  Future<void> _submitIssue() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (_selectedTool == null) {
      _showMessage('يرجى اختيار أداة قبل التنفيذ.');
      return;
    }

    final quantity = double.tryParse(_quantityController.text.trim().replaceAll(',', '.'));
    if (quantity == null || quantity <= 0) {
      _showMessage('يرجى إدخال كمية صحيحة أكبر من صفر.');
      return;
    }

    setState(() => _isSubmitting = true);
    final payload = <String, dynamic>{
      'inventoryItemId': _selectedTool!.inventoryItemId,
      'quantity': quantity,
      'operationalReason': _reasonController.text.trim(),
      'notes': _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
      'sourceOperationId': _generateSourceOperationId(),
    };

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/tools/issue-operational'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (!mounted) return;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        final message = decoded is Map<String, dynamic>
            ? (decoded['status'] ?? decoded['Status'] ?? 'تم تنفيذ الصرف بنجاح')
            : 'تم تنفيذ الصرف بنجاح';

        _showMessage(message.toString(), backgroundColor: Colors.green);
        _formKey.currentState?.reset();
        setState(() {
          _selectedTool = _tools.isEmpty ? null : _tools.first;
          _quantityController.clear();
          _reasonController.clear();
          _notesController.clear();
        });
        await _loadTools();
        return;
      }

      final rawBody = response.body.trim();
      String friendlyMessage = 'تعذر تنفيذ الصرف.';
      if (rawBody.isNotEmpty) {
        try {
          final decoded = jsonDecode(rawBody);
          if (decoded is Map<String, dynamic>) {
            final detail = decoded['detail'] ?? decoded['message'] ?? decoded['title'] ?? decoded['error'];
            if (detail != null && detail.toString().trim().isNotEmpty) {
              friendlyMessage = detail.toString();
            }
          }
        } catch (_) {
          friendlyMessage = rawBody;
        }
      }

      _showMessage('فشل التنفيذ: $friendlyMessage', backgroundColor: Colors.red);
    } catch (error) {
      if (!mounted) return;
      _showMessage('حدث خطأ أثناء تنفيذ الصرف: ${error.toString()}', backgroundColor: Colors.red);
    } finally {
      if (mounted) {
        setState(() => _isSubmitting = false);
      }
    }
  }

  void _showMessage(String message, {Color backgroundColor = UiPalette.primaryBlue}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: backgroundColor,
      ),
    );
  }

  String _generateSourceOperationId() {
    final random = Random();
    final hex = List<String>.generate(32, (_) => '0123456789abcdef'[random.nextInt(16)]).join();
    final a = hex.substring(0, 8);
    final b = hex.substring(8, 12);
    final c = '4${hex.substring(12, 15)}';
    final d = '${(8 + random.nextInt(2)).toRadixString(16)}${hex.substring(15, 18)}';
    final e = hex.substring(18, 32);
    return '$a-$b-$c-$d-$e';
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: ListView(
          children: [
            DropdownButtonFormField<_ToolOption>(
              initialValue: _selectedTool,
              decoration: InputDecoration(
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              hint: const Text('اختر الأداة'),
              items: _tools
                  .map(
                    (tool) => DropdownMenuItem<_ToolOption>(
                      value: tool,
                      child: Text(tool.label),
                    ),
                  )
                  .toList(),
              onChanged: _isLoadingTools || _isSubmitting
                  ? null
                  : (value) {
                      setState(() => _selectedTool = value);
                    },
              validator: (value) {
                if (value == null) {
                  return 'يرجى اختيار أداة';
                }
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _quantityController,
              decoration: InputDecoration(
                labelText: 'الكمية',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
                prefixIcon: const Icon(Icons.numbers),
              ),
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              enabled: !_isSubmitting,
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'يرجى إدخال الكمية';
                }
                final parsed = double.tryParse(value.trim().replaceAll(',', '.'));
                if (parsed == null || parsed <= 0) {
                  return 'يرجى إدخال رقم صالح أكبر من صفر';
                }
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _reasonController,
              decoration: InputDecoration(
                labelText: 'سبب الصرف',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              maxLines: 2,
              enabled: !_isSubmitting,
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'يرجى إدخال سبب الصرف';
                }
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _notesController,
              decoration: InputDecoration(
                labelText: 'ملاحظات',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              maxLines: 3,
              enabled: !_isSubmitting,
            ),
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: (_isLoadingTools || _isSubmitting) ? null : _submitIssue,
                icon: _isSubmitting
                    ? const SizedBox(
                        height: 18,
                        width: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.check_circle_outline),
                label: Text(_isSubmitting ? 'جارٍ التنفيذ...' : 'تنفيذ الصرف'),
                style: ElevatedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ToolOption {
  const _ToolOption({
    required this.inventoryItemId,
    required this.itemCode,
    required this.itemName,
    required this.unit,
  });

  final int inventoryItemId;
  final String itemCode;
  final String itemName;
  final String unit;

  String get label => '$itemName (${itemCode.isEmpty ? 'بدون كود' : itemCode})';

  static _ToolOption fromJson(Map<String, dynamic> json) {
    final inventoryItemId = (json['inventoryItemId'] ?? json['InventoryItemId'] ?? 0) as num?;
    final itemCode = (json['itemCode'] ?? json['ItemCode'] ?? '').toString();
    final itemName = (json['itemName'] ?? json['ItemName'] ?? 'أداة').toString();
    final unit = (json['unit'] ?? json['Unit'] ?? '').toString();

    return _ToolOption(
      inventoryItemId: inventoryItemId?.toInt() ?? 0,
      itemCode: itemCode,
      itemName: itemName,
      unit: unit,
    );
  }
}
