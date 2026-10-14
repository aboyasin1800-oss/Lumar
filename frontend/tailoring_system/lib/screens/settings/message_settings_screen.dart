import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';

class MessageSettingsScreen extends StatefulWidget {
  const MessageSettingsScreen({super.key});

  @override
  State<MessageSettingsScreen> createState() => _MessageSettingsScreenState();
}

class _MessageSettingsScreenState extends State<MessageSettingsScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  bool _saving = false;
  String? _error;

  bool _messageMasterEnabled = true;
  bool _directReferralDefaultOn = true;
  bool _otherReferralDefaultOff = false;
  bool _fridayGreetingEnabled = true;
  bool _campaignEnabled = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final response = await http.get(
        Uri.parse('$_baseUrl/settings/message'),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر قراءة إعدادات الرسائل: ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body);
      if (decoded is! Map<String, dynamic>) {
        throw Exception('استجابة غير صالحة من الخادم.');
      }

      if (!mounted) return;
      setState(() {
        _messageMasterEnabled = _toBool(decoded['messageMasterEnabled'], true);
        _directReferralDefaultOn =
            _toBool(decoded['directReferralDefaultOn'], true);
        _otherReferralDefaultOff =
            _toBool(decoded['otherReferralDefaultOff'], false);
        _fridayGreetingEnabled =
            _toBool(decoded['fridayGreetingEnabled'], true);
        _campaignEnabled = _toBool(decoded['campaignEnabled'], true);
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

  Future<void> _save() async {
    setState(() => _saving = true);

    try {
      final response = await http.put(
        Uri.parse('$_baseUrl/settings/message'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'messageMasterEnabled': _messageMasterEnabled,
          'directReferralDefaultOn': _directReferralDefaultOn,
          'otherReferralDefaultOff': _otherReferralDefaultOff,
          'fridayGreetingEnabled': _fridayGreetingEnabled,
          'campaignEnabled': _campaignEnabled,
        }),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر حفظ إعدادات الرسائل: ${response.statusCode}');
      }

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('تم حفظ إعدادات الرسائل بنجاح.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حفظ إعدادات الرسائل: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  bool _toBool(dynamic value, bool fallback) {
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

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('إعدادات الرسائل'),
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
                          const Icon(Icons.error_outline, size: 40),
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
                : _MessageSettingsContent(
                    messageMasterEnabled: _messageMasterEnabled,
                    directReferralDefaultOn: _directReferralDefaultOn,
                    otherReferralDefaultOff: _otherReferralDefaultOff,
                    fridayGreetingEnabled: _fridayGreetingEnabled,
                    campaignEnabled: _campaignEnabled,
                    saving: _saving,
                    onChanged: (field, value) => setState(() {
                      switch (field) {
                        case 'messageMasterEnabled':
                          _messageMasterEnabled = value;
                          break;
                        case 'directReferralDefaultOn':
                          _directReferralDefaultOn = value;
                          break;
                        case 'otherReferralDefaultOff':
                          _otherReferralDefaultOff = value;
                          break;
                        case 'fridayGreetingEnabled':
                          _fridayGreetingEnabled = value;
                          break;
                        case 'campaignEnabled':
                          _campaignEnabled = value;
                          break;
                      }
                    }),
                    onSave: _save,
                  ),
      ),
    );
  }
}

class _MessageSettingsContent extends StatelessWidget {
  const _MessageSettingsContent({
    required this.messageMasterEnabled,
    required this.directReferralDefaultOn,
    required this.otherReferralDefaultOff,
    required this.fridayGreetingEnabled,
    required this.campaignEnabled,
    required this.saving,
    required this.onChanged,
    required this.onSave,
  });

  final bool messageMasterEnabled;
  final bool directReferralDefaultOn;
  final bool otherReferralDefaultOff;
  final bool fridayGreetingEnabled;
  final bool campaignEnabled;
  final bool saving;
  final void Function(String field, bool value) onChanged;
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
                Row(
                  children: [
                    Icon(Icons.message_outlined,
                        color: UiPalette.primaryBlue, size: 30),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'إعدادات الرسائل والإنذار الذكي',
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor: UiPalette.surfaceCard,
                          fontSize: 18,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  value: messageMasterEnabled,
                  onChanged: (value) => onChanged('messageMasterEnabled', value),
                  title: const Text('تفعيل نظام الرسائل'),
                  subtitle: const Text(
                    'عند التعطيل تُحظر الرسائل التلقائية والذكية في النظام.',
                  ),
                ),
                const Divider(height: 1),
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  value: directReferralDefaultOn,
                  onChanged: (value) => onChanged('directReferralDefaultOn', value),
                  title: const Text('الإحالة المباشرة مفعلة افتراضياً'),
                  subtitle: const Text(
                    'يُفترض تفعيل الإحالة المباشرة عند إنشاء رسالة جديدة.',
                  ),
                ),
                const Divider(height: 1),
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  value: otherReferralDefaultOff,
                  onChanged: (value) => onChanged('otherReferralDefaultOff', value),
                  title: const Text('إيقاف الإحالات الأخرى افتراضياً'),
                  subtitle: const Text(
                    'تُعطّل الإحالات غير المباشرة تلقائياً ما لم يحدد المستخدم خلاف ذلك.',
                  ),
                ),
                const Divider(height: 1),
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  value: fridayGreetingEnabled,
                  onChanged: (value) => onChanged('fridayGreetingEnabled', value),
                  title: const Text('تحية الجمعة التلقائية'),
                  subtitle: const Text(
                    'تُنشئ رسالة تحية الجمعة عند وصول اليوم المناسب.',
                  ),
                ),
                const Divider(height: 1),
                SwitchListTile.adaptive(
                  contentPadding: EdgeInsets.zero,
                  value: campaignEnabled,
                  onChanged: (value) => onChanged('campaignEnabled', value),
                  title: const Text('الحملات الترويجية'),
                  subtitle: const Text(
                    'تُسمح قوالب الحملات والترويج داخل المنصة الحالية دون إنشاء نظام جديد.',
                  ),
                ),
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: saving ? null : onSave,
                  icon: saving
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.save_outlined),
                  label: Text(saving ? 'جارٍ الحفظ...' : 'حفظ إعدادات الرسائل'),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
