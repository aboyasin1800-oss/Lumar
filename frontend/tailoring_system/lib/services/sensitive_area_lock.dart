import 'package:flutter/material.dart';

typedef AdministrativePasswordVerifier = Future<bool> Function(
    String username, String password);

abstract final class SensitiveAreaLock {
  static const openRouteIds = <String>{
    '/customers',
    '/sales',
    '/orders',
    '/measurements',
    '/production',
    '/printing',
    '/ready-made-production',
    '/delivery-dashboard',
    '/messages',
    '/factory-monitoring',
    '/referral-dashboard',
    '/referral-tree',
    '/referral-history',
    '/referral-codes',
    '/referral-rewards',
    '/referral-analytics',
    '/loyalty',
    '/loyalty-dashboard',
    '/loyalty-transactions',
    '/loyalty-redemption',
    '/loyalty-redemptions-history',
    '/loyalty-rewards',
    '/loyalty-rules',
    '/vip-levels',
  };

  static bool isLockedRoute(String routeId) => !openRouteIds.contains(routeId);

  static Future<bool> requestUnlock(
      BuildContext context, AdministrativePasswordVerifier verifier) async {
    final usernameController = TextEditingController();
    final passwordController = TextEditingController();
    var busy = false;

    final result = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: const Text('مصادقة المدير'),
          content: SizedBox(
            width: 380,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                    'أدخل حساب إداري فعلي وكلمة المرور لفتح هذه المنطقة.'),
                const SizedBox(height: 16),
                TextField(
                  controller: usernameController,
                  enabled: !busy,
                  autofillHints: const [AutofillHints.username],
                  decoration:
                      const InputDecoration(labelText: 'اسم حساب المدير'),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: passwordController,
                  enabled: !busy,
                  obscureText: true,
                  enableSuggestions: false,
                  autocorrect: false,
                  autofillHints: const [AutofillHints.password],
                  decoration:
                      const InputDecoration(labelText: 'كلمة مرور المدير'),
                ),
              ],
            ),
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
                      final verified = await verifier(
                        usernameController.text.trim(),
                        passwordController.text,
                      );
                      if (!dialogContext.mounted) return;
                      if (verified) {
                        Navigator.of(dialogContext).pop(true);
                      } else {
                        setState(() => busy = false);
                        ScaffoldMessenger.of(dialogContext).showSnackBar(
                          const SnackBar(
                              content: Text(
                                  'كلمة مرور المدير غير صحيحة أو الحساب غير إداري فعّال.')),
                        );
                      }
                    },
              child: busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('فتح المنطقة'),
            ),
          ],
        ),
      ),
    );

    usernameController.dispose();
    passwordController.dispose();
    return result == true;
  }
}
