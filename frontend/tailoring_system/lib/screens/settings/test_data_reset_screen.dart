import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

class TestDataResetScreen extends StatefulWidget {
  const TestDataResetScreen({super.key});

  @override
  State<TestDataResetScreen> createState() => _TestDataResetScreenState();
}

class _TestDataResetScreenState extends State<TestDataResetScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _busy = false;
  String? _statusMessage;

  Future<void> _confirmAndExecute() async {
    if (_busy) {
      return;
    }

    final shouldContinue = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => AlertDialog(
        title: const Text('تأكيد تنظيف بيانات الاختبار'),
        content: const Text(
          'سيتم حذف بيانات الاختبار التشغيلية وإعادة قاعدة التطوير إلى حالتها النظيفة المعتمدة.\n\nلن يتم حذف إعدادات النظام أو التعريفات أو القوالب أو بيانات الأساس المحمية.\n\nهل تريد المتابعة؟',
          textDirection: TextDirection.rtl,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('إلغاء'),
          ),
          FilledButton.icon(
            onPressed: () => Navigator.of(context).pop(true),
            icon: const Icon(Icons.cleaning_services_rounded),
            label: const Text('تنظيف بيانات الاختبار'),
          ),
        ],
      ),
    );

    if (shouldContinue != true) {
      return;
    }

    await _executeReset();
  }

  Future<void> _executeReset() async {
    if (_busy) {
      return;
    }

    setState(() {
      _busy = true;
      _statusMessage = 'جارٍ تنظيف بيانات الاختبار...';
    });

    try {
      final response = await http.post(
        Uri.parse('$_baseUrl/settings/test-data-reset'),
        headers: {'Content-Type': 'application/json'},
      );

      final bodyText = response.body.isEmpty ? '{}' : response.body;
      final payload = jsonDecode(bodyText);
      final map = payload is Map ? Map<String, dynamic>.from(payload) : <String, dynamic>{};
      final success = response.statusCode >= 200 && response.statusCode < 300 && map['success'] == true;

      if (success) {
        final successMessage = map['message']?.toString() ?? 'تم تنظيف بيانات الاختبار بنجاح.';
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(successMessage)),
        );
        setState(() => _statusMessage = 'تم تنظيف بيانات الاختبار بنجاح.');
        return;
      }

      final failureMessage = map['message']?.toString() ??
          'تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.';
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(failureMessage)),
      );
      setState(() => _statusMessage = failureMessage);
    } catch (_) {
      if (!mounted) return;
      const failureMessage = 'تعذر إكمال تنظيف بيانات الاختبار. لم يتم اعتماد العملية كمكتملة.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(failureMessage)),
      );
      setState(() => _statusMessage = failureMessage);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('تنظيف بيانات الاختبار'),
        ),
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 620),
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Card(
                  color: colorScheme.surface,
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.cleaning_services_rounded,
                          size: 56,
                          color: colorScheme.error,
                        ),
                        const SizedBox(height: 18),
                        Text(
                          'تنظيف بيانات الاختبار',
                          textAlign: TextAlign.center,
                          style: theme.textTheme.headlineSmall?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          'إعادة بيانات الاختبار التشغيلية إلى الحالة النظيفة المعتمدة مع الحفاظ على إعدادات وتعريفات النظام.',
                          textAlign: TextAlign.center,
                          style: theme.textTheme.bodyLarge?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                          ),
                        ),
                        const SizedBox(height: 24),
                        FilledButton.icon(
                          onPressed: _busy ? null : _confirmAndExecute,
                          style: FilledButton.styleFrom(
                            backgroundColor: colorScheme.error,
                            foregroundColor: colorScheme.onError,
                            padding: const EdgeInsets.symmetric(vertical: 16),
                          ),
                          icon: const Icon(Icons.delete_forever_rounded),
                          label: Text(
                            _busy ? 'جارٍ تنظيف بيانات الاختبار...' : 'تنظيف بيانات الاختبار',
                          ),
                        ),
                        if (_statusMessage != null) ...[
                          const SizedBox(height: 20),
                          Container(
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: colorScheme.surfaceContainerHighest,
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Text(
                              _statusMessage!,
                              textAlign: TextAlign.center,
                              style: theme.textTheme.bodyMedium,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
