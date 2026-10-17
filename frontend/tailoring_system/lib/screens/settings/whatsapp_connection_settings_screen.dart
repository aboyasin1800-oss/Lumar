import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

class WhatsAppConnectionSettingsScreen extends StatefulWidget {
  const WhatsAppConnectionSettingsScreen({super.key});

  @override
  State<WhatsAppConnectionSettingsScreen> createState() =>
      _WhatsAppConnectionSettingsScreenState();
}

class _WhatsAppConnectionSettingsScreenState
    extends State<WhatsAppConnectionSettingsScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  bool _saving = false;
  bool _validating = false;
  String? _error;

  final TextEditingController _baseUrlController = TextEditingController();
  final TextEditingController _apiVersionController = TextEditingController();
  final TextEditingController _phoneNumberIdController = TextEditingController();
  final TextEditingController _accessTokenController = TextEditingController();
  final TextEditingController _verifyTokenController = TextEditingController();
  final TextEditingController _appSecretController = TextEditingController();
  final TextEditingController _timeoutController = TextEditingController();

  bool _enabled = false;
  String? _accessTokenMask;
  String? _verifyTokenMask;
  String? _appSecretMask;
  String? _statusMessage;
  bool _isConfigured = false;

  @override
  void dispose() {
    _baseUrlController.dispose();
    _apiVersionController.dispose();
    _phoneNumberIdController.dispose();
    _accessTokenController.dispose();
    _verifyTokenController.dispose();
    _appSecretController.dispose();
    _timeoutController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final response = await http.get(
        Uri.parse('$_baseUrl/settings/whatsapp-connection'),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر قراءة إعدادات واتساب: ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) {
        throw Exception('استجابة غير صالحة من الخادم.');
      }

      if (!mounted) return;
      setState(() {
        _baseUrlController.text = _asString(decoded['baseUrl'], 'https://graph.facebook.com');
        _apiVersionController.text = _asString(decoded['apiVersion'], 'v20.0');
        _phoneNumberIdController.text = _asString(decoded['phoneNumberId'], '');
        _timeoutController.text = _asString(decoded['timeoutSeconds'], '30');
        _enabled = _asBool(decoded['enabled'], false);
        _accessTokenMask = decoded['accessTokenMasked']?.toString();
        _verifyTokenMask = decoded['verifyTokenMasked']?.toString();
        _appSecretMask = decoded['appSecretMasked']?.toString();
        _statusMessage = decoded['message']?.toString();
        _isConfigured = _asBool(decoded['isConfigured'], false);
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = error.toString();
      });
    }
  }

  Future<void> _validateLocalConfig() async {
    setState(() => _validating = true);

    try {
      final response = await http.get(
        Uri.parse('$_baseUrl/settings/whatsapp-connection'),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر التحقق من إعدادات واتساب: ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) {
        throw Exception('استجابة غير صالحة أثناء التحقق.');
      }

      final status = decoded['message']?.toString() ?? 'لم يتم تحديد الحالة.';
      final configured = _asBool(decoded['isConfigured'], false);

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(status),
          backgroundColor: configured ? Colors.green : Colors.orange,
        ),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر التحقق من إعدادات واتساب: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _validating = false);
      }
    }
  }

  Future<void> _save() async {
    setState(() => _saving = true);

    try {
      final accessToken = _accessTokenController.text.trim();
      final verifyToken = _verifyTokenController.text.trim();
      final appSecret = _appSecretController.text.trim();
      final timeout = int.tryParse(_timeoutController.text.trim()) ?? 30;

      final response = await http.put(
        Uri.parse('$_baseUrl/settings/whatsapp-connection'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'enabled': _enabled,
          'baseUrl': _baseUrlController.text.trim(),
          'apiVersion': _apiVersionController.text.trim(),
          'phoneNumberId': _phoneNumberIdController.text.trim(),
          'accessToken': accessToken.isEmpty ? null : accessToken,
          'verifyToken': verifyToken.isEmpty ? null : verifyToken,
          'appSecret': appSecret.isEmpty ? null : appSecret,
          'timeoutSeconds': timeout,
        }),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        final text = response.body.trim();
        throw Exception(text.isNotEmpty ? text : 'تعذر حفظ إعدادات واتساب.');
      }

      final body = jsonDecode(response.body);
      if (body is! Map<String, dynamic>) {
        throw Exception('استجابة غير صالحة من الخادم.');
      }

      if (!mounted) return;
      setState(() {
        _statusMessage = body['message']?.toString();
        _isConfigured = _asBool(body['isConfigured'], false);
        _accessTokenMask = body['accessTokenMasked']?.toString();
        _verifyTokenMask = body['verifyTokenMasked']?.toString();
        _appSecretMask = body['appSecretMasked']?.toString();
        _accessTokenController.clear();
        _verifyTokenController.clear();
        _appSecretController.clear();
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(_statusMessage ?? 'تم حفظ إعدادات واتساب بنجاح.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حفظ إعدادات واتساب: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  String? _maskIfNeeded(String? value) => value == null || value.trim().isEmpty ? null : value;

  bool _asBool(dynamic value, bool fallback) {
    if (value is bool) return value;
    if (value is String) {
      final normalized = value.trim().toLowerCase();
      if (normalized == 'true' || normalized == '1' || normalized == 'yes') {
        return true;
      }
      if (normalized == 'false' || normalized == '0' || normalized == 'no') {
        return false;
      }
    }
    return fallback;
  }

  String _asString(dynamic value, String fallback) => value?.toString() ?? fallback;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('إعدادات اتصال واتساب'),
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(Icons.error_outline_rounded, size: 40),
                          const SizedBox(height: 12),
                          Text(
                            _error!,
                            textAlign: TextAlign.center,
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.screenBackground,
                              fontSize: 14,
                            ),
                          ),
                          const SizedBox(height: 16),
                          FilledButton.icon(
                            onPressed: _load,
                            icon: const Icon(Icons.refresh_outlined),
                            label: const Text('إعادة المحاولة'),
                          ),
                        ],
                      ),
                    ),
                  )
                : SingleChildScrollView(
                    padding: const EdgeInsets.all(18),
                    child: Card(
                      color: UiPalette.surfaceCard,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(18),
                        side: const BorderSide(color: UiPalette.borderSoft),
                      ),
                      child: Padding(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              'إعدادات اتصال واتساب',
                              style: UiPalette.adaptiveTextStyle(
                                context,
                                backgroundColor: UiPalette.surfaceCard,
                                fontSize: 22,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              'تُخزّن القيم الحساسة في مخزن الأسرار المحلي للنظام ولا تُعرض في الاستجابة.',
                              style: UiPalette.adaptiveTextStyle(
                                context,
                                backgroundColor: UiPalette.surfaceCard,
                                fontSize: 14,
                              ),
                            ),
                            const SizedBox(height: 20),
                            SwitchListTile.adaptive(
                              contentPadding: EdgeInsets.zero,
                              value: _enabled,
                              onChanged: (value) => setState(() => _enabled = value),
                              title: Text(
                                'تفعيل مزود واتساب',
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 16,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                              subtitle: Text(
                                _isConfigured
                                    ? 'تمت تهيئة الإعدادات الأساسية.'
                                    : 'لا يزال يتطلب رقم الهاتف ورمز الوصول.',
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 12,
                                ),
                              ),
                            ),
                            const SizedBox(height: 20),
                            TextFormField(
                              controller: _baseUrlController,
                              decoration: const InputDecoration(
                                labelText: 'رابط API الأساسي',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _apiVersionController,
                              decoration: const InputDecoration(
                                labelText: 'إصدار API',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _phoneNumberIdController,
                              decoration: const InputDecoration(
                                labelText: 'رقم الهاتف في واتساب',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _timeoutController,
                              keyboardType: TextInputType.number,
                              decoration: const InputDecoration(
                                labelText: 'مهلة الطلب (ثانية)',
                                border: OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _accessTokenController,
                              obscureText: true,
                              enableSuggestions: false,
                              autocorrect: false,
                              decoration: InputDecoration(
                                labelText: 'رمز الوصول الجديد',
                                hintText: _maskIfNeeded(_accessTokenMask) ?? 'اتركه فارغاً للحفاظ على القيمة الحالية',
                                border: const OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _verifyTokenController,
                              obscureText: true,
                              enableSuggestions: false,
                              autocorrect: false,
                              decoration: InputDecoration(
                                labelText: 'رمز التحقق الجديد',
                                hintText: _maskIfNeeded(_verifyTokenMask) ?? 'اتركه فارغاً للحفاظ على القيمة الحالية',
                                border: const OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 12),
                            TextFormField(
                              controller: _appSecretController,
                              obscureText: true,
                              enableSuggestions: false,
                              autocorrect: false,
                              decoration: InputDecoration(
                                labelText: 'سر التطبيق الجديد',
                                hintText: _maskIfNeeded(_appSecretMask) ?? 'اتركه فارغاً للحفاظ على القيمة الحالية',
                                border: const OutlineInputBorder(),
                              ),
                            ),
                            const SizedBox(height: 18),
                            if (_statusMessage != null)
                              Container(
                                padding: const EdgeInsets.all(12),
                                decoration: BoxDecoration(
                                  color: _isConfigured ? const Color(0xFF143825) : const Color(0xFF3B2D1F),
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: Text(
                                  _statusMessage!,
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: _isConfigured ? const Color(0xFF143825) : const Color(0xFF3B2D1F),
                                    fontSize: 13,
                                  ),
                                ),
                              ),
                            const SizedBox(height: 18),
                            Row(
                              children: [
                                Expanded(
                                  child: FilledButton.icon(
                                    onPressed: _saving ? null : _save,
                                    icon: _saving
                                        ? const SizedBox(
                                            width: 16,
                                            height: 16,
                                            child: CircularProgressIndicator(strokeWidth: 2),
                                          )
                                        : const Icon(Icons.save_outlined),
                                    label: Text(_saving ? 'جارٍ الحفظ...' : 'حفظ الإعدادات'),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: OutlinedButton.icon(
                                    onPressed: _validating ? null : _validateLocalConfig,
                                    icon: _validating
                                        ? const SizedBox(
                                            width: 16,
                                            height: 16,
                                            child: CircularProgressIndicator(strokeWidth: 2),
                                          )
                                        : const Icon(Icons.check_circle_outline),
                                    label: Text(_validating ? 'جارٍ الفحص...' : 'فحص التهيئة'),
                                  ),
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
}
