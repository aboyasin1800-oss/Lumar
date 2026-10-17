import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/ui_palette.dart';
import '../../models/finance_models.dart';

class GeneralCashAccountSettingsScreen extends StatefulWidget {
  const GeneralCashAccountSettingsScreen({super.key});

  @override
  State<GeneralCashAccountSettingsScreen> createState() =>
      _GeneralCashAccountSettingsScreenState();
}

class _GeneralCashAccountSettingsScreenState
    extends State<GeneralCashAccountSettingsScreen> {
  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  bool _saving = false;
  String? _error;
  List<CashAccount> _eligibleAccounts = const [];
  int? _selectedCashAccountId;
  String _message = 'لم يتم تحديد صندوق نقدي عام افتراضي بعد.';

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
      final settingResponse = await http.get(
        Uri.parse('$_baseUrl/settings/general-cash-account'),
      );
      final cashAccountsResponse = await http.get(
        Uri.parse('$_baseUrl/finance/cash-accounts'),
      );

      if (settingResponse.statusCode < 200 || settingResponse.statusCode >= 300) {
        throw Exception('تعذر قراءة إعداد الصندوق النقدي العام: ${settingResponse.statusCode}');
      }

      if (cashAccountsResponse.statusCode < 200 || cashAccountsResponse.statusCode >= 300) {
        throw Exception('تعذر قراءة الحسابات النقدية: ${cashAccountsResponse.statusCode}');
      }

      final settingsJson = jsonDecode(settingResponse.body);
      final accountsJson = jsonDecode(cashAccountsResponse.body);
      final accounts = switch (accountsJson) {
        List list => list
            .map((item) => CashAccount.fromJson(Map<String, dynamic>.from(item as Map)))
            .toList(),
        _ => <CashAccount>[],
      };

      final eligible = accounts.where((account) {
        final currency = account.currencyCode?.trim().toUpperCase();
        return account.isActive &&
            account.isReceiptEnabled &&
            account.isDisbursementEnabled &&
            currency == 'YER';
      }).toList();

      final selectedValue = settingsJson is Map && settingsJson['cashAccountId'] != null
          ? (settingsJson['cashAccountId'] as num?)?.toInt()
          : null;

      final isSelectedStillEligible = selectedValue != null &&
          eligible.any((account) => account.id == selectedValue);

      if (!mounted) return;
      setState(() {
        _eligibleAccounts = eligible;
        _selectedCashAccountId = isSelectedStillEligible ? selectedValue : null;
        _message = (settingsJson is Map ? settingsJson['message'] : '')?.toString() ??
            'لم يتم تحديد صندوق نقدي عام افتراضي بعد.';
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
    if (_saving) {
      return;
    }

    setState(() => _saving = true);

    try {
      final response = await http.put(
        Uri.parse('$_baseUrl/settings/general-cash-account'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'cashAccountId': _selectedCashAccountId}),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        final text = response.body.trim();
        throw Exception(text.isNotEmpty ? text : 'تعذر حفظ الصندوق النقدي العام.');
      }

      final body = jsonDecode(response.body);
      final message = body is Map ? body['message']?.toString() : null;
      if (!mounted) return;
      setState(() {
        _message = message ?? 'تم حفظ الصندوق النقدي العام الافتراضي.';
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(_message)),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حفظ الصندوق النقدي العام: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('الصندوق النقدي العام الافتراضي'),
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
                              'تحديد الصندوق النقدي العام الافتراضي',
                              style: UiPalette.adaptiveTextStyle(
                                context,
                                backgroundColor: UiPalette.surfaceCard,
                                fontSize: 22,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              'هذا التحديد يكون من شاشة الإعدادات فقط، ولا يتم اختيار أول حساب مؤهل تلقائياً.',
                              style: UiPalette.adaptiveTextStyle(
                                context,
                                backgroundColor: UiPalette.surfaceCard,
                                fontSize: 14,
                              ),
                            ),
                            const SizedBox(height: 20),
                            if (_eligibleAccounts.isEmpty)
                              Container(
                                padding: const EdgeInsets.all(14),
                                decoration: BoxDecoration(
                                  color: const Color(0xFF3B2D1F),
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: Text(
                                  'لا يوجد صندوق نقدي مؤهل حالياً. يجب أن يكون الحساب نشطاً، بالعملة YER، وقابلاً للإيراد والمصروفات.',
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: const Color(0xFF3B2D1F),
                                    fontSize: 14,
                                  ),
                                ),
                              )
                            else
                              DropdownButtonFormField<int>(
                                initialValue: _selectedCashAccountId,
                                isExpanded: true,
                                decoration: InputDecoration(
                                  labelText: 'اختر الصندوق النقدي العام',
                                  border: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                ),
                                items: _eligibleAccounts
                                    .map(
                                      (account) => DropdownMenuItem<int>(
                                        value: account.id,
                                        child: Text(
                                          '${account.accountName} • ${account.currencyCode ?? 'YER'}',
                                        ),
                                      ),
                                    )
                                    .toList(),
                                onChanged: (value) => setState(() {
                                  _selectedCashAccountId = value;
                                }),
                                hint: const Text('اختر صندوقاً نقدياً من القائمة'),
                              ),
                            const SizedBox(height: 20),
                            if (_message.isNotEmpty)
                              Container(
                                padding: const EdgeInsets.all(14),
                                decoration: BoxDecoration(
                                  color: const Color(0xFF1D2C3F),
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: Text(
                                  _message,
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: const Color(0xFF1D2C3F),
                                    fontSize: 14,
                                  ),
                                ),
                              ),
                            const SizedBox(height: 20),
                            FilledButton.icon(
                              onPressed: _saving || _eligibleAccounts.isEmpty ? null : _save,
                              icon: _saving
                                  ? const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    )
                                  : const Icon(Icons.save_outlined),
                              label: Text(_saving ? 'جارٍ الحفظ...' : 'حفظ التحديد'),
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
