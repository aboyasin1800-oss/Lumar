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
                  const _CustodyIssueForm(),
                  const _OpenCustodyTab(),
                  const _ToolHistoryTab(),
                ],
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
    setState(() => _isLoadingTools = true);

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

class _CustodyIssueForm extends StatefulWidget {
  const _CustodyIssueForm();

  @override
  State<_CustodyIssueForm> createState() => _CustodyIssueFormState();
}

class _CustodyIssueFormState extends State<_CustodyIssueForm> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final _formKey = GlobalKey<FormState>();
  final _quantityController = TextEditingController();
  final _beneficiaryController = TextEditingController();
  final _destinationController = TextEditingController();
  final _loanReasonController = TextEditingController();
  final _notesController = TextEditingController();
  final _officialUnitCostController = TextEditingController();

  final List<_ToolOption> _tools = <_ToolOption>[];
  _ToolOption? _selectedTool;
  bool _isLoadingTools = true;
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    _loadTools();
  }

  @override
  void dispose() {
    _quantityController.dispose();
    _beneficiaryController.dispose();
    _destinationController.dispose();
    _loanReasonController.dispose();
    _notesController.dispose();
    _officialUnitCostController.dispose();
    super.dispose();
  }

  Future<void> _loadTools() async {
    setState(() => _isLoadingTools = true);
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
    final unitCost = double.tryParse(_officialUnitCostController.text.trim().replaceAll(',', '.'));
    if (quantity == null || quantity <= 0) {
      _showMessage('يرجى إدخال كمية صحيحة أكبر من صفر.');
      return;
    }
    if (unitCost == null || unitCost <= 0) {
      _showMessage('يرجى إدخال سعر وحدة رسمي صحيح.');
      return;
    }

    setState(() => _isSubmitting = true);

    final payload = <String, dynamic>{
      'inventoryItemId': _selectedTool!.inventoryItemId,
      'quantity': quantity,
      'officialUnitCost': unitCost,
      'beneficiaryName': _beneficiaryController.text.trim(),
      'destinationType': _destinationController.text.trim(),
      'destinationName': _destinationController.text.trim(),
      'loanReason': _loanReasonController.text.trim(),
      'notes': _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
      'sourceOperationId': _generateSourceOperationId(),
    };

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/tools/issue-custody'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (!mounted) return;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        _showMessage('تم تنفيذ صرف العهدة بنجاح.', backgroundColor: Colors.green);
        _formKey.currentState?.reset();
        setState(() {
          _selectedTool = _tools.isEmpty ? null : _tools.first;
          _quantityController.clear();
          _beneficiaryController.clear();
          _destinationController.clear();
          _loanReasonController.clear();
          _notesController.clear();
          _officialUnitCostController.clear();
        });
        return;
      }

      final rawBody = response.body.trim();
      String message = 'تعذر تنفيذ صرف العهدة.';
      if (rawBody.isNotEmpty) {
        try {
          final decoded = jsonDecode(rawBody);
          if (decoded is Map<String, dynamic>) {
            final detail = decoded['detail'] ?? decoded['message'] ?? decoded['title'] ?? decoded['error'];
            if (detail != null && detail.toString().trim().isNotEmpty) {
              message = detail.toString();
            }
          }
        } catch (_) {
          message = rawBody;
        }
      }
      _showMessage('فشل التنفيذ: $message', backgroundColor: Colors.red);
    } catch (error) {
      if (!mounted) return;
      _showMessage('حدث خطأ أثناء تنفيذ صرف العهدة: ${error.toString()}', backgroundColor: Colors.red);
    } finally {
      if (mounted) {
        setState(() => _isSubmitting = false);
      }
    }
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

  void _showMessage(String message, {Color backgroundColor = UiPalette.primaryBlue}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: backgroundColor,
      ),
    );
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
              isExpanded: true,
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
              enabled: !_isSubmitting,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'الكمية',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) return 'يرجى إدخال الكمية';
                final parsed = double.tryParse(value.trim().replaceAll(',', '.'));
                if (parsed == null || parsed <= 0) return 'يرجى إدخال كمية صحيحة';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _officialUnitCostController,
              enabled: !_isSubmitting,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: InputDecoration(
                labelText: 'سعر الوحدة الرسمي',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) return 'يرجى إدخال سعر الوحدة الرسمي';
                final parsed = double.tryParse(value.trim().replaceAll(',', '.'));
                if (parsed == null || parsed <= 0) return 'يرجى إدخال سعر وحدة صحيح';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _beneficiaryController,
              enabled: !_isSubmitting,
              decoration: InputDecoration(
                labelText: 'المستفيد',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) return 'يرجى إدخال اسم المستفيد';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _destinationController,
              enabled: !_isSubmitting,
              decoration: InputDecoration(
                labelText: 'الجهة / المكان',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) return 'يرجى إدخال الجهة';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _loanReasonController,
              enabled: !_isSubmitting,
              maxLines: 2,
              decoration: InputDecoration(
                labelText: 'سبب العهدة',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) return 'يرجى إدخال سبب العهدة';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _notesController,
              enabled: !_isSubmitting,
              maxLines: 3,
              decoration: InputDecoration(
                labelText: 'ملاحظات',
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
            ),
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: (_isLoadingTools || _isSubmitting) ? null : _submitIssue,
                icon: _isSubmitting
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.check_circle_outline),
                label: Text(_isSubmitting ? 'جارٍ التنفيذ...' : 'تنفيذ صرف العهدة'),
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

class _OpenCustodyTab extends StatefulWidget {
  const _OpenCustodyTab();

  @override
  State<_OpenCustodyTab> createState() => _OpenCustodyTabState();
}

class _OpenCustodyTabState extends State<_OpenCustodyTab> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  bool _submitting = false;
  String _errorMessage = '';
  List<_OpenCustodyRow> _rows = <_OpenCustodyRow>[];

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() {
      _loading = true;
      _errorMessage = '';
    });

    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/tools/custody/open'));
      if (!mounted) return;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is List) {
          final items = decoded.whereType<Map<String, dynamic>>().map(_OpenCustodyRow.fromJson).toList();
          setState(() {
            _rows = items;
            _loading = false;
          });
          return;
        }
      }
      setState(() {
        _loading = false;
        _errorMessage = 'تعذر تحميل العهد المفتوحة (${response.statusCode})';
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _errorMessage = 'تعذر الاتصال بالخادم: ${error.toString()}';
      });
    }
  }

  Future<void> _returnCustody(_OpenCustodyRow row, {required bool full}) async {
    final rawValue = row.returnController.text.trim();
    final quantity = double.tryParse(rawValue.replaceAll(',', '.')) ?? (full ? row.outstandingQuantity : 0);

    if (quantity <= 0 || quantity > row.outstandingQuantity) {
      _showMessage('يرجى إدخال كمية مسترجعة صالحة لا تتجاوز المبلغ المتبقي.', backgroundColor: Colors.red);
      return;
    }

    setState(() => _submitting = true);
    final payload = <String, dynamic>{
      'toolIssuanceId': row.toolIssuanceId,
      'returnedQuantity': quantity,
      'returnNotes': row.notesController.text.trim().isEmpty ? null : row.notesController.text.trim(),
      'sourceOperationId': _generateSourceOperationId(),
    };

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/tools/returns/custody'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (!mounted) return;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        _showMessage('تم إرجاع العهدة بنجاح.', backgroundColor: Colors.green);
        await _loadData();
        return;
      }

      final rawBody = response.body.trim();
      String message = 'تعذر إرجاع العهدة.';
      if (rawBody.isNotEmpty) {
        try {
          final decoded = jsonDecode(rawBody);
          if (decoded is Map<String, dynamic>) {
            final detail = decoded['detail'] ?? decoded['message'] ?? decoded['title'] ?? decoded['error'];
            if (detail != null && detail.toString().trim().isNotEmpty) {
              message = detail.toString();
            }
          }
        } catch (_) {
          message = rawBody;
        }
      }
      _showMessage('فشل الإرجاع: $message', backgroundColor: Colors.red);
    } catch (error) {
      if (!mounted) return;
      _showMessage('حدث خطأ أثناء الإرجاع: ${error.toString()}', backgroundColor: Colors.red);
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
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

  void _showMessage(String message, {Color backgroundColor = UiPalette.primaryBlue}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: backgroundColor,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_errorMessage.isNotEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 48),
              const SizedBox(height: 12),
              Text(_errorMessage, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              ElevatedButton.icon(
                onPressed: _loadData,
                icon: const Icon(Icons.refresh),
                label: const Text('إعادة التحميل'),
              ),
            ],
          ),
        ),
      );
    }
    if (_rows.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: const [
              Icon(Icons.inbox_outlined, size: 48),
              SizedBox(height: 12),
              Text('لا توجد عهد مفتوحة حالياً.'),
            ],
          ),
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _loadData,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: _rows.length,
        itemBuilder: (context, index) {
          final row = _rows[index];
          return Card(
            margin: const EdgeInsets.only(bottom: 16),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          row.itemName,
                          style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: UiPalette.softBlue,
                          borderRadius: BorderRadius.circular(10),
                        ),
                        child: Text(row.itemCode),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text('المستفيد: ${row.beneficiaryName}'),
                  Text('الجهة: ${row.destinationType} / ${row.destinationName}'),
                  Text('الكمية الأصلية: ${row.quantity}'),
                  Text('المسترجع: ${row.returnedQuantity}'),
                  Text('المتبقي: ${row.outstandingQuantity}'),
                  Text('تاريخ الإعارة: ${row.createdAt.toString().substring(0, 16)}'),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: row.returnController,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    decoration: InputDecoration(
                      labelText: 'الكمية المرتجعة',
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(10),
                      ),
                    ),
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: row.notesController,
                    maxLines: 2,
                    decoration: InputDecoration(
                      labelText: 'ملاحظات الإرجاع',
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(10),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: _submitting ? null : () => _returnCustody(row, full: false),
                          icon: const Icon(Icons.keyboard_return),
                          label: const Text('إرجاع جزئي'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: _submitting ? null : () => _returnCustody(row, full: true),
                          icon: const Icon(Icons.done_all),
                          label: const Text('إرجاع كامل'),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _ToolHistoryTab extends StatefulWidget {
  const _ToolHistoryTab();

  @override
  State<_ToolHistoryTab> createState() => _ToolHistoryTabState();
}

class _ToolHistoryTabState extends State<_ToolHistoryTab> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  String _errorMessage = '';
  String _searchText = '';
  List<_HistoryRow> _rows = <_HistoryRow>[];

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() {
      _loading = true;
      _errorMessage = '';
    });

    try {
      final response = await http.get(Uri.parse('$_baseUrl/inventory/tools/history'));
      if (!mounted) return;
      if (response.statusCode >= 200 && response.statusCode < 300) {
        final decoded = jsonDecode(response.body);
        if (decoded is List) {
          final rows = decoded.whereType<Map<String, dynamic>>().map(_HistoryRow.fromJson).toList();
          setState(() {
            _rows = rows;
            _loading = false;
          });
          return;
        }
      }
      setState(() {
        _loading = false;
        _errorMessage = 'تعذر تحميل سجل العمليات (${response.statusCode})';
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _errorMessage = 'تعذر الاتصال بالخادم: ${error.toString()}';
      });
    }
  }

  List<_HistoryRow> get _filteredRows {
    final query = _searchText.trim().toLowerCase();
    if (query.isEmpty) return _rows;
    return _rows.where((row) {
      final haystack = [
        row.itemName,
        row.itemCode,
        row.issueType,
        row.status,
        row.beneficiaryName ?? '',
        row.destinationName ?? '',
      ].join(' ').toLowerCase();
      return haystack.contains(query);
    }).toList();
  }

  Future<void> _reverseOperational(_HistoryRow row) async {
    final reasonController = TextEditingController();
    final notesController = TextEditingController();
    final shouldReverse = await showDialog<bool>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: const Text('إلغاء الصرف التشغيلي'),
          content: SizedBox(
            width: 420,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: reasonController,
                  maxLines: 2,
                  decoration: const InputDecoration(
                    labelText: 'سبب الإلغاء',
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: notesController,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    labelText: 'ملاحظات',
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('إلغاء'),
            ),
            ElevatedButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('تأكيد الإلغاء'),
            ),
          ],
        );
      },
    );

    if (shouldReverse != true) return;

    final reason = reasonController.text.trim();
    final notes = notesController.text.trim();
    if (reason.isEmpty || notes.isEmpty) {
      _showMessage('يرجى إدخال سبب الإلغاء وملاحظاته.', backgroundColor: Colors.red);
      return;
    }

    final payload = <String, dynamic>{
      'toolIssuanceId': row.toolIssuanceId,
      'reversalReason': reason,
      'notes': notes,
      'sourceOperationId': _generateSourceOperationId(),
    };

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/inventory/tools/reversals/operational'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (!mounted) return;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        _showMessage('تم إلغاء الصرف التشغيلي بنجاح.', backgroundColor: Colors.green);
        await _loadData();
        return;
      }

      final rawBody = response.body.trim();
      String message = 'تعذر إلغاء الصرف.';
      if (rawBody.isNotEmpty) {
        try {
          final decoded = jsonDecode(rawBody);
          if (decoded is Map<String, dynamic>) {
            final detail = decoded['detail'] ?? decoded['message'] ?? decoded['title'] ?? decoded['error'];
            if (detail != null && detail.toString().trim().isNotEmpty) {
              message = detail.toString();
            }
          }
        } catch (_) {
          message = rawBody;
        }
      }
      _showMessage('فشل الإلغاء: $message', backgroundColor: Colors.red);
    } catch (error) {
      if (!mounted) return;
      _showMessage('حدث خطأ أثناء إلغاء الصرف: ${error.toString()}', backgroundColor: Colors.red);
    }
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

  void _showMessage(String message, {Color backgroundColor = UiPalette.primaryBlue}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: backgroundColor,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_errorMessage.isNotEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 48),
              const SizedBox(height: 12),
              Text(_errorMessage, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              ElevatedButton.icon(
                onPressed: _loadData,
                icon: const Icon(Icons.refresh),
                label: const Text('إعادة التحميل'),
              ),
            ],
          ),
        ),
      );
    }

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        children: [
          TextField(
            decoration: const InputDecoration(
              labelText: 'بحث في السجل',
              prefixIcon: Icon(Icons.search),
            ),
            onChanged: (value) => setState(() => _searchText = value),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: _filteredRows.isEmpty
                ? const Center(child: Text('لا توجد عمليات لعرضها.'))
                : ListView.builder(
                    itemCount: _filteredRows.length,
                    itemBuilder: (context, index) {
                      final row = _filteredRows[index];
                      final canReverse = row.issueType.toLowerCase().contains('operational');
                      return Card(
                        margin: const EdgeInsets.only(bottom: 12),
                        child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                row.itemName,
                                style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                              ),
                              const SizedBox(height: 6),
                              Text('الكود: ${row.itemCode}'),
                              Text('النوع: ${row.issueType}'),
                              Text('الكمية: ${row.quantity}'),
                              Text('السعر الرسمي: ${row.officialUnitCost}'),
                              Text('الحالة: ${row.status}'),
                              Text('المصدر: ${row.sourceOperationId}'),
                              Text('التاريخ: ${row.createdAt.toString().substring(0, 16)}'),
                              if (row.beneficiaryName != null && row.beneficiaryName!.trim().isNotEmpty) Text('المستفيد: ${row.beneficiaryName}'),
                              if (row.destinationName != null && row.destinationName!.trim().isNotEmpty) Text('الجهة: ${row.destinationName}'),
                              if (row.operationalReason != null && row.operationalReason!.trim().isNotEmpty) Text('سبب التشغيل: ${row.operationalReason}'),
                              if (row.loanReason != null && row.loanReason!.trim().isNotEmpty) Text('سبب العهدة: ${row.loanReason}'),
                              if (canReverse)
                                Align(
                                  alignment: Alignment.centerRight,
                                  child: ElevatedButton.icon(
                                    onPressed: () => _reverseOperational(row),
                                    icon: const Icon(Icons.undo),
                                    label: const Text('إلغاء التشغيل'),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}

class _OpenCustodyRow {
  _OpenCustodyRow({
    required this.toolIssuanceId,
    required this.inventoryItemId,
    required this.itemCode,
    required this.itemName,
    required this.quantity,
    required this.returnedQuantity,
    required this.outstandingQuantity,
    required this.beneficiaryName,
    required this.destinationType,
    required this.destinationName,
    required this.loanReason,
    required this.createdAt,
    required this.returnController,
    required this.notesController,
  });

  final int toolIssuanceId;
  final int inventoryItemId;
  final String itemCode;
  final String itemName;
  final double quantity;
  final double returnedQuantity;
  final double outstandingQuantity;
  final String beneficiaryName;
  final String destinationType;
  final String destinationName;
  final String loanReason;
  final DateTime createdAt;
  final TextEditingController returnController;
  final TextEditingController notesController;

  static _OpenCustodyRow fromJson(Map<String, dynamic> json) {
    final toolIssuanceId = (json['toolIssuanceId'] ?? json['ToolIssuanceId'] ?? 0) as num?;
    final inventoryItemId = (json['inventoryItemId'] ?? json['InventoryItemId'] ?? 0) as num?;
    final quantity = (json['quantity'] ?? json['Quantity'] ?? 0) as num? ?? 0;
    final returnedQuantity = (json['returnedQuantity'] ?? json['ReturnedQuantity'] ?? 0) as num? ?? 0;
    final outstandingQuantity = (json['outstandingQuantity'] ?? json['OutstandingQuantity'] ?? 0) as num? ?? 0;

    return _OpenCustodyRow(
      toolIssuanceId: toolIssuanceId?.toInt() ?? 0,
      inventoryItemId: inventoryItemId?.toInt() ?? 0,
      itemCode: (json['itemCode'] ?? json['ItemCode'] ?? '').toString(),
      itemName: (json['itemName'] ?? json['ItemName'] ?? '').toString(),
      quantity: quantity.toDouble(),
      returnedQuantity: returnedQuantity.toDouble(),
      outstandingQuantity: outstandingQuantity.toDouble(),
      beneficiaryName: (json['beneficiaryName'] ?? json['BeneficiaryName'] ?? '').toString(),
      destinationType: (json['destinationType'] ?? json['DestinationType'] ?? '').toString(),
      destinationName: (json['destinationName'] ?? json['DestinationName'] ?? '').toString(),
      loanReason: (json['loanReason'] ?? json['LoanReason'] ?? '').toString(),
      createdAt: DateTime.tryParse((json['createdAt'] ?? json['CreatedAt'] ?? '').toString()) ?? DateTime.now(),
      returnController: TextEditingController(text: outstandingQuantity.toStringAsFixed(2)),
      notesController: TextEditingController(),
    );
  }
}

class _HistoryRow {
  _HistoryRow({
    required this.toolIssuanceId,
    required this.inventoryItemId,
    required this.itemCode,
    required this.itemName,
    required this.issueType,
    required this.quantity,
    required this.officialUnitCost,
    required this.operationalAmount,
    required this.postingAmount,
    required this.status,
    required this.accountingEventId,
    required this.inventoryTransactionId,
    required this.sourceOperationId,
    required this.createdAt,
    this.beneficiaryName,
    this.destinationType,
    this.destinationName,
    this.operationalReason,
    this.loanReason,
  });

  final int toolIssuanceId;
  final int inventoryItemId;
  final String itemCode;
  final String itemName;
  final String issueType;
  final double quantity;
  final double officialUnitCost;
  final double operationalAmount;
  final double postingAmount;
  final String status;
  final int? accountingEventId;
  final int? inventoryTransactionId;
  final String sourceOperationId;
  final DateTime createdAt;
  final String? beneficiaryName;
  final String? destinationType;
  final String? destinationName;
  final String? operationalReason;
  final String? loanReason;

  static _HistoryRow fromJson(Map<String, dynamic> json) {
    final quantity = (json['quantity'] ?? json['Quantity'] ?? 0) as num? ?? 0;
    final officialUnitCost = (json['officialUnitCost'] ?? json['OfficialUnitCost'] ?? 0) as num? ?? 0;
    final operationalAmount = (json['operationalAmount'] ?? json['OperationalAmount'] ?? 0) as num? ?? 0;
    final postingAmount = (json['postingAmount'] ?? json['PostingAmount'] ?? 0) as num? ?? 0;

    return _HistoryRow(
      toolIssuanceId: ((json['toolIssuanceId'] ?? json['ToolIssuanceId'] ?? 0) as num).toInt(),
      inventoryItemId: ((json['inventoryItemId'] ?? json['InventoryItemId'] ?? 0) as num).toInt(),
      itemCode: (json['itemCode'] ?? json['ItemCode'] ?? '').toString(),
      itemName: (json['itemName'] ?? json['ItemName'] ?? '').toString(),
      issueType: (json['issueType'] ?? json['IssueType'] ?? '').toString(),
      quantity: quantity.toDouble(),
      officialUnitCost: officialUnitCost.toDouble(),
      operationalAmount: operationalAmount.toDouble(),
      postingAmount: postingAmount.toDouble(),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      accountingEventId: (json['accountingEventId'] ?? json['AccountingEventId']) is num ? (json['accountingEventId'] ?? json['AccountingEventId']) as num? : null,
      inventoryTransactionId: (json['inventoryTransactionId'] ?? json['InventoryTransactionId']) is num ? (json['inventoryTransactionId'] ?? json['InventoryTransactionId']) as num? : null,
      sourceOperationId: (json['sourceOperationId'] ?? json['SourceOperationId'] ?? '').toString(),
      createdAt: DateTime.tryParse((json['createdAt'] ?? json['CreatedAt'] ?? '').toString()) ?? DateTime.now(),
      beneficiaryName: (json['beneficiaryName'] ?? json['BeneficiaryName'])?.toString(),
      destinationType: (json['destinationType'] ?? json['DestinationType'])?.toString(),
      destinationName: (json['destinationName'] ?? json['DestinationName'])?.toString(),
      operationalReason: (json['operationalReason'] ?? json['OperationalReason'])?.toString(),
      loanReason: (json['loanReason'] ?? json['LoanReason'])?.toString(),
    );
  }
}
