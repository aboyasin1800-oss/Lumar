import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';

class CodePrefixSettingItem {
  const CodePrefixSettingItem({
    required this.key,
    required this.displayName,
    required this.currentValue,
    required this.defaultValue,
    required this.example,
  });

  factory CodePrefixSettingItem.fromJson(Map<String, dynamic> json) {
    return CodePrefixSettingItem(
      key: (json['key'] ?? json['SettingName'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      currentValue:
          (json['currentValue'] ?? json['currentValue'] ?? '').toString(),
      defaultValue: (json['defaultValue'] ?? '').toString(),
      example: (json['example'] ?? '').toString(),
    );
  }

  final String key;
  final String displayName;
  final String currentValue;
  final String defaultValue;
  final String example;
}

class CodePrefixSettingsScreen extends StatefulWidget {
  const CodePrefixSettingsScreen({super.key});

  @override
  State<CodePrefixSettingsScreen> createState() =>
      _CodePrefixSettingsScreenState();
}

class _CodePrefixSettingsScreenState extends State<CodePrefixSettingsScreen> {
  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');

  final Map<String, TextEditingController> _controllers = {};
  bool _loading = true;
  bool _saving = false;
  String? _message;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _message = null;
    });

    try {
      final response =
          await http.get(Uri.parse('$_baseUrl/settings/code-prefixes'));
      if (response.statusCode != 200) {
        throw Exception('تعذر تحميل إعدادات التكويد (${response.statusCode})');
      }

      final decoded = jsonDecode(response.body);
      final items = (decoded as List<dynamic>)
          .map((e) => CodePrefixSettingItem.fromJson(e as Map<String, dynamic>))
          .toList();

      for (final item in items) {
        _controllers[item.key] = TextEditingController(
            text: item.currentValue.isNotEmpty
                ? item.currentValue
                : item.defaultValue);
      }

      setState(() {
        _loading = false;
      });
    } catch (error) {
      setState(() {
        _loading = false;
        _error = error.toString();
      });
    }
  }

  Future<void> _save() async {
    final payload = <Map<String, String>>[];
    for (final entry in _controllers.entries) {
      final value = entry.value.text.trim();
      if (value.isEmpty) {
        setState(() => _error = 'لا يمكن ترك أي حقل فارغ.');
        return;
      }
      payload.add({'key': entry.key, 'value': value});
    }

    setState(() {
      _saving = true;
      _error = null;
      _message = null;
    });

    try {
      final response = await http.put(
        Uri.parse('$_baseUrl/settings/code-prefixes'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'settings': payload}),
      );

      if (response.statusCode != 200) {
        final message = jsonDecode(response.body);
        throw Exception(
            (message is Map ? message['title'] ?? message['message'] : message)
                .toString());
      }

      final decoded = jsonDecode(response.body) as List<dynamic>;
      final items = decoded
          .map((e) => CodePrefixSettingItem.fromJson(e as Map<String, dynamic>))
          .toList();

      for (final item in items) {
        final controller = _controllers[item.key];
        if (controller != null) {
          controller.text = item.currentValue.isNotEmpty
              ? item.currentValue
              : item.defaultValue;
        }
      }

      setState(() {
        _saving = false;
        _message = 'تم حفظ بادئات الأكواد بنجاح.';
      });
    } catch (error) {
      setState(() {
        _saving = false;
        _error = error.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('إعدادات التكويد'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded),
          onPressed: () => AppNavigation.back(),
        ),
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
      ),
      backgroundColor: UiPalette.screenBackground,
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    padding: const EdgeInsets.all(18),
                    decoration: BoxDecoration(
                      color: UiPalette.surfaceCard,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: UiPalette.borderSoft),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'بادئات الأكواد داخل System_Settings',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 22,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'يتم حفظ القيم في جدول System_Settings فقط وبدون أي جدول أو ترحيل جديد.',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 13,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                  if (_error != null)
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(12),
                      margin: const EdgeInsets.only(bottom: 12),
                      decoration: BoxDecoration(
                        color: Colors.red.withOpacity(0.12),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.red.shade400),
                      ),
                      child: Text(
                        _error!,
                        style: TextStyle(color: Colors.red.shade200),
                      ),
                    ),
                  if (_message != null)
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(12),
                      margin: const EdgeInsets.only(bottom: 12),
                      decoration: BoxDecoration(
                        color: Colors.green.withOpacity(0.12),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.green.shade400),
                      ),
                      child: Text(
                        _message!,
                        style: TextStyle(color: Colors.green.shade200),
                      ),
                    ),
                  ..._controllers.entries.map((entry) {
                    final key = entry.key;
                    final controller = entry.value;
                    final displayName = _displayName(key);
                    final defaultValue = _defaultValue(key);
                    final example = _example(key, controller.text.trim());
                    return Container(
                      width: double.infinity,
                      margin: const EdgeInsets.only(bottom: 16),
                      padding: const EdgeInsets.all(14),
                      decoration: BoxDecoration(
                        color: UiPalette.surfaceCard,
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(color: UiPalette.borderSoft),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            displayName,
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 16,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'المفتاح: $key',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 12,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                          const SizedBox(height: 12),
                          TextField(
                            controller: controller,
                            textCapitalization: TextCapitalization.characters,
                            autocorrect: false,
                            decoration: InputDecoration(
                              labelText: 'القيمة الحالية',
                              hintText: defaultValue,
                              filled: true,
                              fillColor: UiPalette.softBlue,
                              border: OutlineInputBorder(
                                borderRadius: BorderRadius.circular(12),
                                borderSide:
                                    BorderSide(color: UiPalette.borderSoft),
                              ),
                              focusedBorder: OutlineInputBorder(
                                borderRadius: BorderRadius.circular(12),
                                borderSide:
                                    BorderSide(color: UiPalette.primaryBlue),
                              ),
                              labelStyle: TextStyle(color: UiPalette.textSoft),
                              hintStyle: TextStyle(color: UiPalette.textSoft),
                            ),
                            style: TextStyle(color: UiPalette.textMain),
                          ),
                          const SizedBox(height: 8),
                          Text(
                            'مثال: $example',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                    );
                  }),
                  const SizedBox(height: 24),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: FilledButton.icon(
                      onPressed: _saving ? null : _save,
                      icon: _saving
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2))
                          : const Icon(Icons.save_outlined),
                      label: Text(_saving ? 'جارٍ الحفظ...' : 'حفظ الإعدادات'),
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  String _displayName(String key) => switch (key) {
        'FabricCodePrefix' => 'بادئة كود القماش',
        'CatalogNumberPrefix' => 'بادئة رقم الكتالوج',
        'CustomerCodePrefix' => 'بادئة كود العميل',
        'EmployeeCodePrefix' => 'بادئة كود الموظف',
        'OrderCodePrefix' => 'بادئة رقم الطلب',
        'PieceTrackingPrefix' => 'بادئة تتبع القطعة',
        'ProductionTrackingPrefix' => 'بادئة تتبع الإنتاج',
        'ToolCodePrefix' => 'بادئة كود الأدوات',
        _ => key,
      };

  String _defaultValue(String key) => switch (key) {
        'FabricCodePrefix' => 'FA',
        'CatalogNumberPrefix' => 'CAT',
        'CustomerCodePrefix' => 'CH',
        'EmployeeCodePrefix' => 'MO',
        'OrderCodePrefix' => 'OR',
        'PieceTrackingPrefix' => 'TAR',
        'ProductionTrackingPrefix' => 'TRK',
        'ToolCodePrefix' => 'AT',
        _ => 'N/A',
      };

  String _example(String key, String value) {
    final prefix = value.isNotEmpty ? value : _defaultValue(key);
    return switch (key) {
      'FabricCodePrefix' => '$prefix-0025',
      'CatalogNumberPrefix' => '$prefix-0001',
      'CustomerCodePrefix' => '$prefix-0001',
      'EmployeeCodePrefix' => '$prefix-0001',
      'OrderCodePrefix' => '$prefix-0001',
      'PieceTrackingPrefix' => '$prefix-0001',
      'ProductionTrackingPrefix' => '$prefix-0001',
      'ToolCodePrefix' => '$prefix-0001',
      _ => '$prefix-0001',
    };
  }
}
