import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

class PrintSettingsScreen extends StatefulWidget {
  const PrintSettingsScreen({super.key});

  @override
  State<PrintSettingsScreen> createState() => _PrintSettingsScreenState();
}

class _PrintSettingsScreenState extends State<PrintSettingsScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  bool _saving = false;
  String? _error;
  final Map<String, TextEditingController> _controllers = {};
  bool _useImage = false;

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
    });

    try {
      final keys = [
        'MeasurementCardHeaderImage',
        'MeasurementCardHeaderUseImage',
        'MeasurementCardHeaderName',
        'MeasurementCardLocation',
        'MeasurementCardPhone1',
        'MeasurementCardPhone2',
      ];

      final entries = <String, String>{};
      for (final key in keys) {
        final response = await http.get(
          Uri.parse('$_baseUrl/settings/by-key/${Uri.encodeComponent(key)}'),
        );
        if (response.statusCode >= 200 && response.statusCode < 300) {
          final decoded = jsonDecode(response.body);
          if (decoded is Map<String, dynamic>) {
            entries[key] = (decoded['settingValue'] ?? '').toString();
          }
        }
      }

      final controllerMap = {
        'MeasurementCardHeaderImage': TextEditingController(
          text: entries['MeasurementCardHeaderImage'] ?? '',
        ),
        'MeasurementCardHeaderName': TextEditingController(
          text: entries['MeasurementCardHeaderName'] ?? '',
        ),
        'MeasurementCardLocation': TextEditingController(
          text: entries['MeasurementCardLocation'] ?? '',
        ),
        'MeasurementCardPhone1': TextEditingController(
          text: entries['MeasurementCardPhone1'] ?? '',
        ),
        'MeasurementCardPhone2': TextEditingController(
          text: entries['MeasurementCardPhone2'] ?? '',
        ),
      };

      for (final entry in controllerMap.entries) {
        _controllers[entry.key] = entry.value;
      }

      _useImage = _toBool(entries['MeasurementCardHeaderUseImage']);
      if (!mounted) return;
      setState(() => _loading = false);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = error.toString();
      });
    }
  }

  bool _toBool(String? value) {
    final normalized = (value ?? '').trim().toLowerCase();
    return normalized == 'true' || normalized == '1' || normalized == 'yes';
  }

  Future<void> _save() async {
    final payload = <String, String>{};
    payload['MeasurementCardHeaderUseImage'] = _useImage ? 'true' : 'false';
    payload['MeasurementCardHeaderImage'] =
        _controllers['MeasurementCardHeaderImage']?.text.trim() ?? '';
    payload['MeasurementCardHeaderName'] =
        _controllers['MeasurementCardHeaderName']?.text.trim() ?? '';
    payload['MeasurementCardLocation'] =
        _controllers['MeasurementCardLocation']?.text.trim() ?? '';
    payload['MeasurementCardPhone1'] =
        _controllers['MeasurementCardPhone1']?.text.trim() ?? '';
    payload['MeasurementCardPhone2'] =
        _controllers['MeasurementCardPhone2']?.text.trim() ?? '';

    setState(() => _saving = true);

    try {
      final response = await http.put(
        Uri.parse('$_baseUrl/settings/print-config'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر حفظ إعدادات الطباعة: ${response.statusCode}');
      }

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم حفظ إعدادات الطباعة بنجاح.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حفظ إعدادات الطباعة: $error')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('إعدادات الطباعة'),
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? _ErrorState(message: _error!, onRetry: _load)
                : _PrintSettingsContent(
                    useImage: _useImage,
                    controllers: _controllers,
                    saving: _saving,
                    onUseImageChanged: (value) => setState(() => _useImage = value),
                    onSave: _save,
                  ),
      ),
    );
  }
}

class _PrintSettingsContent extends StatelessWidget {
  const _PrintSettingsContent({
    required this.useImage,
    required this.controllers,
    required this.saving,
    required this.onUseImageChanged,
    required this.onSave,
  });

  final bool useImage;
  final Map<String, TextEditingController> controllers;
  final bool saving;
  final ValueChanged<bool> onUseImageChanged;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(18),
      children: [
        Card(
          color: UiPalette.surfaceCard,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(18),
            side: const BorderSide(color: UiPalette.borderSoft),
          ),
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'رأس المستندات المطبوعة',
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: UiPalette.surfaceCard,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 18),
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  value: useImage,
                  onChanged: onUseImageChanged,
                  title: const Text('استخدام صورة رأس المستند'),
                  subtitle: const Text('عند التفعيل تظهر الصورة في المستندات، وإلا يستخدم النظام الرأس النصي.'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: controllers['MeasurementCardHeaderImage'],
                  decoration: const InputDecoration(
                    labelText: 'رابط أو مسار صورة الرأس',
                    prefixIcon: Icon(Icons.image_outlined),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: controllers['MeasurementCardHeaderName'],
                  decoration: const InputDecoration(
                    labelText: 'اسم الشركة / المؤسسة',
                    prefixIcon: Icon(Icons.business_outlined),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: controllers['MeasurementCardLocation'],
                  decoration: const InputDecoration(
                    labelText: 'الموقع',
                    prefixIcon: Icon(Icons.location_on_outlined),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: controllers['MeasurementCardPhone1'],
                  decoration: const InputDecoration(
                    labelText: 'الهاتف الأول',
                    prefixIcon: Icon(Icons.phone_outlined),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: controllers['MeasurementCardPhone2'],
                  decoration: const InputDecoration(
                    labelText: 'الهاتف الثاني',
                    prefixIcon: Icon(Icons.phone_android_outlined),
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 18),
                FilledButton.icon(
                  onPressed: saving ? null : onSave,
                  icon: saving
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.save_outlined),
                  label: Text(saving ? 'جارٍ الحفظ...' : 'حفظ إعدادات الطباعة'),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 18),
        Card(
          color: UiPalette.softBlue,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              'تتعلق هذه الإعدادات برأس بطاقة المقاسات التي تظهر عند الطباعة فقط، ولا تُنشئ أي جدول أو عمود جديد في قاعدة البيانات.',
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.softBlue,
                fontSize: 13,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(22),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 42),
            const SizedBox(height: 12),
            Text(
              message,
              textAlign: TextAlign.center,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 14,
              ),
            ),
            const SizedBox(height: 14),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh_outlined),
              label: const Text('إعادة المحاولة'),
            ),
          ],
        ),
      ),
    );
  }
}
