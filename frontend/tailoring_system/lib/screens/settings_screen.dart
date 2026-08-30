import 'package:flutter/material.dart';

import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';

class SettingsScreen extends StatefulWidget {
	const SettingsScreen({required this.auth, required this.themeState, required this.uiScale, super.key});
	final AuthState auth;
	final ThemeState themeState;
	final UiScaleState uiScale;
	@override
	State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
	final currentPassword = TextEditingController();
	final username = TextEditingController();
	final newPassword = TextEditingController();
	final confirmPassword = TextEditingController();
	final currentPasswordFocus = FocusNode();
	final newPasswordFocus = FocusNode();
	final confirmPasswordFocus = FocusNode();
	String? message;

	@override
	void initState() { super.initState(); username.text = widget.auth.user!.username; }
	@override
	void dispose() { currentPassword.dispose(); username.dispose(); newPassword.dispose(); confirmPassword.dispose(); currentPasswordFocus.dispose(); newPasswordFocus.dispose(); confirmPasswordFocus.dispose(); super.dispose(); }
	Future<void> _changeUsername() async { final result = await widget.auth.changeUsername(currentPassword.text, username.text); if (!mounted) return; setState(() => message = result ?? 'تم تحديث اسم المستخدم.'); if (result == null) currentPassword.clear(); }
	Future<void> _changePassword() async { final result = await widget.auth.changePassword(currentPassword.text, newPassword.text, confirmPassword.text); if (!mounted) return; setState(() => message = result ?? 'تم تحديث كلمة المرور. يرجى تسجيل الدخول مجددًا.'); if (result == null) Navigator.of(context).pushNamedAndRemoveUntil('/login', (_) => false); }
	String _roleLabel(String? role) => switch (role?.toLowerCase()) { 'admin' => 'مدير', 'viewer' => 'مستعرض', _ => 'غير محدد' };
	String _lastLoginLabel(BuildContext context, String value) { final date = DateTime.tryParse(value)?.toLocal(); return date == null ? 'غير محدد' : MaterialLocalizations.of(context).formatFullDate(date); }
	String _themeLabel(AppThemePreference preference) => switch (preference) { AppThemePreference.light => 'الوضع الفاتح', AppThemePreference.dark => 'الوضع الداكن', AppThemePreference.system => 'اتباع إعدادات النظام' };

	@override
	Widget build(BuildContext context) {
		final user = widget.auth.user!;
		return ListView(children: [
			Text('إعدادات تسجيل الدخول', style: Theme.of(context).textTheme.titleLarge),
			const SizedBox(height: 16),
			ListTile(title: const Text('اسم المستخدم'), subtitle: Text(user.username)),
			ListTile(title: const Text('الاسم الكامل'), subtitle: Text(user.fullName)),
			ListTile(title: const Text('الدور'), subtitle: Text(_roleLabel(user.role))),
			ListTile(title: const Text('حالة الحساب'), subtitle: Text(user.isActive ? 'نشط' : 'غير نشط')),
			if (user.lastLoginUtc != null) ListTile(title: const Text('آخر تسجيل دخول'), subtitle: Text(_lastLoginLabel(context, user.lastLoginUtc!))),
			const Divider(),
			Text('المظهر', style: Theme.of(context).textTheme.titleLarge),
			RadioGroup<AppThemePreference>(groupValue: widget.themeState.preference, onChanged: (value) { if (value != null) widget.themeState.setPreference(value); }, child: Column(children: AppThemePreference.values.map((preference) => RadioListTile<AppThemePreference>(value: preference, title: Text(_themeLabel(preference)))).toList())),
			const SizedBox(height: 12),
			Text('حجم المحتوى', style: Theme.of(context).textTheme.titleMedium),
			DropdownButtonFormField<double>(initialValue: widget.uiScale.scale, decoration: const InputDecoration(border: OutlineInputBorder()), items: UiScaleState.levels.map((scale) => DropdownMenuItem(value: scale, child: Text('${(scale * 100).round()}%'))).toList(), onChanged: (value) { if (value != null) widget.uiScale.setScale(value); }),
			const SizedBox(height: 8),
			Align(alignment: Alignment.centerRight, child: OutlinedButton(onPressed: widget.uiScale.reset, child: const Text('إعادة الافتراضي'))),
			const Divider(),
			TextField(controller: username, textInputAction: TextInputAction.next, onSubmitted: (_) => currentPasswordFocus.requestFocus(), decoration: const InputDecoration(labelText: 'اسم المستخدم الجديد', border: OutlineInputBorder())),
			const SizedBox(height: 12),
			TextField(controller: currentPassword, focusNode: currentPasswordFocus, obscureText: true, textInputAction: TextInputAction.done, onSubmitted: (_) => _changeUsername(), decoration: const InputDecoration(labelText: 'كلمة المرور الحالية', border: OutlineInputBorder())),
			const SizedBox(height: 12),
			Align(alignment: Alignment.centerRight, child: FilledButton(onPressed: _changeUsername, child: const Text('تغيير اسم المستخدم'))),
			const Divider(height: 40),
			TextField(controller: newPassword, focusNode: newPasswordFocus, obscureText: true, textInputAction: TextInputAction.next, onSubmitted: (_) => confirmPasswordFocus.requestFocus(), decoration: const InputDecoration(labelText: 'كلمة المرور الجديدة', border: OutlineInputBorder())),
			const SizedBox(height: 12),
			TextField(controller: confirmPassword, focusNode: confirmPasswordFocus, obscureText: true, textInputAction: TextInputAction.done, onSubmitted: (_) => _changePassword(), decoration: const InputDecoration(labelText: 'تأكيد كلمة المرور الجديدة', border: OutlineInputBorder())),
			const SizedBox(height: 12),
			Align(alignment: Alignment.centerRight, child: FilledButton(onPressed: _changePassword, child: const Text('تغيير كلمة المرور'))),
			if (message != null) Padding(padding: const EdgeInsets.only(top: 16), child: Text(message!)),
		]);
	}
}