import 'dart:async';

import 'package:flutter/material.dart';

import 'auth_state.dart';

enum ProtectedAreasKeyState { locked, unlocked }

class ProtectedAreasKeyController extends ChangeNotifier {
  ProtectedAreasKeyController({required this.auth});

  final AuthState auth;
  ProtectedAreasKeyState state = ProtectedAreasKeyState.locked;
  bool passwordConfigured = false;
  bool manualOnly = false;
  int durationMinutes = 30;
  DateTime? expiresAtUtc;
  Timer? _timer;
  bool _disposed = false;

  bool get unlocked => state == ProtectedAreasKeyState.unlocked;

  Future<void> loadStatus() async {
    final status = await auth.getProtectedAreasStatus();
    if (status == null || _disposed) return;
    passwordConfigured = status.passwordConfigured;
    manualOnly = status.manualOnly;
    durationMinutes = status.durationMinutes;
    notifyListeners();
  }

  Future<bool> requestInitialSetup(BuildContext context) async {
    final passwordController = TextEditingController();
    final confirmController = TextEditingController();
    var busy = false;

    final result = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: const Text('تهيئة حماية المناطق الحساسة'),
          content: SizedBox(
            width: 420,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                    'اختر كلمة حماية مستقلة للمناطق الحساسة. التهيئة مصادقة بحساب المدير الحالي.'),
                const SizedBox(height: 12),
                TextField(
                    controller: passwordController,
                    enabled: !busy,
                    obscureText: true,
                    enableSuggestions: false,
                    autocorrect: false,
                    decoration: const InputDecoration(
                        labelText: 'كلمة حماية المناطق الجديدة')),
                const SizedBox(height: 10),
                TextField(
                    controller: confirmController,
                    enabled: !busy,
                    obscureText: true,
                    enableSuggestions: false,
                    autocorrect: false,
                    decoration: const InputDecoration(
                        labelText: 'تأكيد كلمة حماية المناطق')),
              ],
            ),
          ),
          actions: [
            TextButton(
                onPressed:
                    busy ? null : () => Navigator.of(dialogContext).pop(false),
                child: const Text('إلغاء')),
            FilledButton(
              onPressed: busy
                  ? null
                  : () async {
                      if (passwordController.text.length < 4) {
                        ScaffoldMessenger.of(dialogContext).showSnackBar(
                            const SnackBar(
                                content: Text(
                                    'كلمة الحماية يجب أن تكون 4 رموز على الأقل.')));
                        return;
                      }
                      if (passwordController.text != confirmController.text) {
                        ScaffoldMessenger.of(dialogContext).showSnackBar(
                            const SnackBar(
                                content: Text(
                                    'كلمة الحماية وتأكيدها غير متطابقين.')));
                        return;
                      }
                      setState(() => busy = true);
                      final ok = await auth.setProtectedAreasPassword(
                        password: passwordController.text,
                        confirmPassword: confirmController.text,
                      );
                      if (!dialogContext.mounted) return;
                      if (ok) {
                        passwordConfigured = true;
                        Navigator.of(dialogContext).pop(true);
                      } else {
                        setState(() => busy = false);
                        ScaffoldMessenger.of(dialogContext).showSnackBar(
                            const SnackBar(
                                content: Text(
                                    'تعذر التهيئة. تحقق من بيانات المدير وكلمة الحماية.')));
                      }
                    },
              child: busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('حفظ وفتح'),
            ),
          ],
        ),
      ),
    );

    passwordController.dispose();
    confirmController.dispose();

    if (result == true) {
      unlock();
      return true;
    }
    return false;
  }

  Future<bool> requestUnlock(BuildContext context) async {
    final passwordController = TextEditingController();
    var busy = false;

    final result = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: const Text('فتح المناطق المحمية'),
          content: TextField(
            controller: passwordController,
            obscureText: true,
            enableSuggestions: false,
            autocorrect: false,
            decoration:
                const InputDecoration(labelText: 'كلمة مرور حماية المناطق'),
            enabled: !busy,
          ),
          actions: [
            TextButton(
              onPressed:
                  busy ? null : () => Navigator.of(dialogContext).pop(false),
              child: const Text('إلغاء'),
            ),
            FilledButton(
              onPressed: busy
                  ? null
                  : () async {
                      setState(() => busy = true);
                      final verified = await auth.verifyProtectedAreasPassword(
                          passwordController.text);
                      if (!dialogContext.mounted) return;
                      if (verified) {
                        Navigator.of(dialogContext).pop(true);
                      } else {
                        setState(() => busy = false);
                        ScaffoldMessenger.of(dialogContext).showSnackBar(
                          const SnackBar(
                              content: Text('كلمة مرور الحماية غير صحيحة.')),
                        );
                      }
                    },
              child: busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('فتح'),
            ),
          ],
        ),
      ),
    );

    passwordController.dispose();
    if (result == true) {
      unlock();
      return true;
    }
    return false;
  }

  Future<void> unlock() async {
    await loadStatus();
    state = ProtectedAreasKeyState.unlocked;
    _timer?.cancel();
    if (manualOnly) {
      expiresAtUtc = null;
    } else {
      expiresAtUtc = DateTime.now().add(Duration(minutes: durationMinutes));
      _timer = Timer(expiresAtUtc!.difference(DateTime.now()), close);
    }
    notifyListeners();
  }

  void close() {
    _timer?.cancel();
    _timer = null;
    expiresAtUtc = null;
    state = ProtectedAreasKeyState.locked;
    notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _timer?.cancel();
    super.dispose();
  }
}
