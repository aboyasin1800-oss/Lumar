import 'package:flutter/material.dart';

import '../../services/auth_state.dart';

class ProtectedAreasSettingsScreen extends StatefulWidget {
  const ProtectedAreasSettingsScreen({required this.auth, super.key});
  final AuthState auth;

  @override
  State<ProtectedAreasSettingsScreen> createState() =>
      _ProtectedAreasSettingsScreenState();
}

class _ProtectedAreasSettingsScreenState
    extends State<ProtectedAreasSettingsScreen> {
  final _newPassword = TextEditingController();
  final _confirmPassword = TextEditingController();
  final _currentProtectionPassword = TextEditingController();
  final _minutes = TextEditingController(text: '30');
  bool _manualOnly = false;
  bool _configured = false;
  bool _busy = false;
  String _message = '';

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final status = await widget.auth.getProtectedAreasStatus();
    if (!mounted || status == null) return;
    setState(() {
      _configured = status.passwordConfigured;
      _manualOnly = status.manualOnly;
      _minutes.text =
          status.manualOnly ? '30' : status.durationMinutes.toString();
    });
  }

  Future<void> _savePassword() async {
    if (_newPassword.text.length < 4) {
      setState(() => _message =
          'يجب أن تتكون كلمة الحماية من 4 أحرف أو أرقام أو رموز على الأقل.');
      return;
    }
    setState(() => _busy = true);
    final ok = await widget.auth.setProtectedAreasPassword(
      password: _newPassword.text,
      confirmPassword: _confirmPassword.text,
    );
    if (!mounted) return;
    setState(() {
      _busy = false;
      _configured = ok;
      _message = ok
          ? 'تم حفظ حماية المناطق بنجاح.'
          : 'تعذر حفظ الحماية. راجع بيانات المدير وكلمة الحماية.';
      if (ok) {
        _newPassword.clear();
        _confirmPassword.clear();
      }
    });
  }

  Future<void> _saveDuration() async {
    if (_currentProtectionPassword.text.isEmpty) {
      setState(() => _message = 'أدخل كلمة مرور حماية المناطق الحالية.');
      return;
    }
    final minutes = int.tryParse(_minutes.text.trim());
    if (!_manualOnly && (minutes == null || minutes < 1 || minutes > 1440)) {
      setState(() => _message = 'أدخل مدة صحيحة من 1 إلى 1440 دقيقة.');
      return;
    }
    setState(() => _busy = true);
    final ok = await widget.auth.updateProtectedAreasDuration(
      manualOnly: _manualOnly,
      durationMinutes: _manualOnly ? 0 : minutes!,
      currentProtectionPassword: _currentProtectionPassword.text,
    );
    if (!mounted) return;
    setState(() {
      _busy = false;
      _message = ok
          ? 'تم حفظ مدة فتح المناطق بنجاح.'
          : 'تعذر تحديث المدة. يلزم مصادقة مسؤول فعلي.';
      if (ok) {
        _currentProtectionPassword.clear();
      }
    });
    if (ok) await _load();
  }

  @override
  void dispose() {
    _newPassword.dispose();
    _confirmPassword.dispose();
    _currentProtectionPassword.dispose();
    _minutes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('حماية المناطق الحساسة')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text(
              _configured
                  ? 'حالة التهيئة: كلمة الحماية مهيأة'
                  : 'حالة التهيئة: غير مهيأة',
              style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 16),
          TextField(
              controller: _newPassword,
              obscureText: true,
              enableSuggestions: false,
              autocorrect: false,
              decoration: const InputDecoration(
                  labelText: 'كلمة مرور حماية المناطق الجديدة')),
          const SizedBox(height: 12),
          TextField(
              controller: _confirmPassword,
              obscureText: true,
              enableSuggestions: false,
              autocorrect: false,
              decoration:
                  const InputDecoration(labelText: 'تأكيد كلمة مرور الحماية')),
          const SizedBox(height: 12),
          FilledButton(
              onPressed: _busy ? null : _savePassword,
              child: Text(_configured
                  ? 'تغيير كلمة مرور الحماية'
                  : 'حفظ كلمة مرور الحماية')),
          const Divider(height: 32),
          const Text('مدة فتح المناطق المحمية',
              style: TextStyle(fontWeight: FontWeight.bold)),
          Row(children: [
            Expanded(
                child: TextField(
                    controller: _minutes,
                    keyboardType: TextInputType.number,
                    enabled: !_manualOnly,
                    decoration:
                        const InputDecoration(labelText: 'المدة بالدقائق'))),
            const SizedBox(width: 12),
            Expanded(
                child: CheckboxListTile(
                    value: _manualOnly,
                    onChanged: (value) =>
                        setState(() => _manualOnly = value ?? false),
                    title: const Text('يبقى مفتوحًا حتى الإغلاق اليدوي'))),
          ]),
          const SizedBox(height: 12),
          TextField(
              controller: _currentProtectionPassword,
              obscureText: true,
              enableSuggestions: false,
              autocorrect: false,
              decoration: const InputDecoration(
                  labelText: 'كلمة مرور حماية المناطق الحالية')),
          const SizedBox(height: 12),
          FilledButton(
              onPressed: _busy ? null : _saveDuration,
              child: const Text('حفظ مدة فتح المناطق')),
          if (_message.isNotEmpty)
            Padding(
                padding: const EdgeInsets.only(top: 16), child: Text(_message)),
        ],
      ),
    );
  }
}
