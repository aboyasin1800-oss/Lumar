import 'package:flutter/material.dart';

import '../../core/currency_formatter.dart';
import '../../core/ui_palette.dart';
import '../../services/currency_settings_service.dart';

class CurrencySettingsScreen extends StatefulWidget {
  const CurrencySettingsScreen({super.key});

  @override
  State<CurrencySettingsScreen> createState() => _CurrencySettingsScreenState();
}

class _CurrencySettingsScreenState extends State<CurrencySettingsScreen> {
  final _service = CurrencySettingsService();
  CurrencySettings? _settings;
  String? _error;
  bool _loading = true;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _service.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final settings = await _service.load();
      if (!mounted) return;
      setState(() {
        _settings = settings;
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
    final current = _settings;
    if (current == null || _saving) return;
    setState(() => _saving = true);
    try {
      final saved = await _service.update(
        currencyName: current.currencyName,
        preferredCurrency: current.preferredCurrency,
      );
      if (!mounted) return;
      setState(() {
        _settings = saved;
        _saving = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('تم حفظ إعدادات العملة وإعادة قراءتها بنجاح.')),
      );
    } catch (error) {
      if (!mounted) return;
      setState(() => _saving = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حفظ إعدادات العملة: $error')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('إعدادات العملة'),
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.textMain,
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? _ErrorState(message: _error!, onRetry: _load)
                : _CurrencyContent(
                    settings: _settings!,
                    saving: _saving,
                    onSave: _save,
                    onCurrencyChanged: (updated) => setState(() {
                      _settings = updated;
                    }),
                  ),
      ),
    );
  }
}

class _CurrencyContent extends StatelessWidget {
  const _CurrencyContent({
    required this.settings,
    required this.saving,
    required this.onSave,
    required this.onCurrencyChanged,
  });

  final CurrencySettings settings;
  final bool saving;
  final VoidCallback onSave;
  final ValueChanged<CurrencySettings> onCurrencyChanged;

  @override
  Widget build(BuildContext context) {
    final formatter = CurrencyFormatter(settings: settings);
    final cardText = UiPalette.adaptiveTextColor(UiPalette.surfaceCard);
    final selectedCode = supportedCurrencyOptions.any(
      (option) => option.code == formatter.code,
    )
        ? formatter.code
        : null;

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Card(
          color: UiPalette.surfaceCard,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: const BorderSide(color: UiPalette.borderSoft),
          ),
          child: Padding(
            padding: const EdgeInsets.all(22),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Icon(Icons.payments_outlined,
                    size: 42, color: UiPalette.primaryBlue),
                const SizedBox(height: 12),
                Text(
                  'العملة الأساسية الحالية',
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: UiPalette.surfaceCard,
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                  ),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 6),
                Text(
                  formatter.name,
                  style: TextStyle(
                    color: cardText,
                    fontSize: 26,
                    fontWeight: FontWeight.w800,
                  ),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 18),
                DropdownButtonFormField<String>(
                  initialValue: selectedCode,
                  decoration: const InputDecoration(
                    labelText: 'عملة العرض',
                    prefixIcon: Icon(Icons.language_outlined),
                    border: OutlineInputBorder(),
                  ),
                  items: supportedCurrencyOptions
                      .map(
                        (option) => DropdownMenuItem<String>(
                          value: option.code,
                          child: Text('${option.name} (${option.code})'),
                        ),
                      )
                      .toList(),
                  onChanged: (code) {
                    if (code == null) return;
                    final option = supportedCurrencyOptions.firstWhere(
                      (item) => item.code == code,
                    );
                    final updated = CurrencySettings.fromValues(
                      currencyName: option.name,
                      preferredCurrency: option.code,
                    );
                    onCurrencyChanged(updated);
                  },
                ),
                const SizedBox(height: 10),
                _InfoRow(label: 'الاختصار الدولي', value: formatter.code),
                _InfoRow(label: 'رمز العرض', value: formatter.symbol),
                _InfoRow(
                    label: 'حالة النظام',
                    value: 'النظام يعمل حالياً بنظام أحادي العملة.'),
                _InfoRow(
                    label: 'معاينة العرض', value: formatter.amount(100000)),
                const SizedBox(height: 18),
                Text(
                  'القيمة المعروضة مقروءة من إعدادات النظام الحقيقية.',
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: UiPalette.softBlue,
                    fontSize: 13,
                  ),
                  textAlign: TextAlign.center,
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
                  label:
                      Text(saving ? 'جارٍ الحفظ...' : 'حفظ الإعدادات الحالية'),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          color: UiPalette.softBlue,
          elevation: 0,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              'هذه القائمة مخصصة لتنسيق العرض فقط ولا تغيّر أي مبلغ محفوظ أو عملية مالية.\nدعم العملات في العمليات وأسعار الصرف يتطلب مشروعاً مستقلاً.',
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.softBlue,
                fontSize: 14,
              ),
              textAlign: TextAlign.right,
            ),
          ),
        ),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 7),
        child: Row(
          textDirection: TextDirection.rtl,
          children: [
            Expanded(
              child: Text(
                label,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const SizedBox(width: 16),
            Flexible(
              child: Text(
                value,
                textAlign: TextAlign.left,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      );
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.cloud_off_outlined,
                  size: 48, color: UiPalette.primaryBlue),
              const SizedBox(height: 12),
              const Text('تعذر تحميل إعدادات العملة',
                  style: TextStyle(color: UiPalette.textMain, fontSize: 18)),
              const SizedBox(height: 8),
              Text(message,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: UiPalette.textSoft)),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('إعادة المحاولة'),
              ),
            ],
          ),
        ),
      );
}
