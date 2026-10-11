import 'package:flutter/material.dart';

import '../core/app_routes.dart';
import '../core/ui_palette.dart';
import '../services/auth_state.dart';

enum _AccountTypeSelection { customer, employee, supplier }

class LoginScreen extends StatefulWidget {
  const LoginScreen({required this.auth, super.key});

  final AuthState auth;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final username = TextEditingController();
  final password = TextEditingController();
  final passwordFocus = FocusNode();
  bool rememberMe = false;
  bool loading = false;
  String? error;

  @override
  void initState() {
    super.initState();
    username.text = widget.auth.rememberedUsername ?? '';
  }

  @override
  void dispose() {
    username.dispose();
    password.dispose();
    passwordFocus.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (loading) return;
    final trimmedUsername = username.text.trim();
    final trimmedPassword = password.text.trim();
    final routeContext = context;
    if (trimmedUsername.isEmpty || trimmedPassword.isEmpty) {
      setState(() => error = 'يرجى إدخال اسم المستخدم وكلمة المرور.');
      return;
    }

    setState(() => loading = true);
    final message = await widget.auth.login(trimmedUsername, trimmedPassword, rememberMe);
    if (!mounted) return;
    setState(() {
      loading = false;
      error = message;
    });
    if (message == null) {
      Navigator.of(routeContext).pushReplacementNamed(AppRoutes.dashboard);
    }
  }

  Future<void> _openCreateAccountDialog() async {
    final rootContext = context;
    final accountType = await showDialog<_AccountTypeSelection>(
      context: rootContext,
      builder: (dialogContext) => AlertDialog(
        backgroundColor: UiPalette.surfaceCard,
        title: Text(
          'اختيار نوع الحساب',
          style: UiPalette.adaptiveTextStyle(dialogContext, backgroundColor: UiPalette.surfaceCard, fontSize: 20, fontWeight: FontWeight.bold),
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            _AccountTypeTile(
              title: 'عميل',
              subtitle: 'إنشاء حساب عميل متاح حاليًا.',
              icon: Icons.person,
              value: _AccountTypeSelection.customer,
            ),
            const SizedBox(height: 8),
            _AccountTypeTile(
              title: 'موظف',
              subtitle: 'يرتبط بالحساب الرسمي للموظف وربطه بـ EmployeeId.',
              icon: Icons.badge,
              value: _AccountTypeSelection.employee,
            ),
            const SizedBox(height: 8),
            _AccountTypeTile(
              title: 'مورد',
              subtitle: 'غير متاح حاليًا — قريبًا.',
              icon: Icons.storefront,
              value: _AccountTypeSelection.supplier,
              enabled: false,
            ),
          ],
        ),
      ),
    );

    if (accountType == null) return;
    if (accountType == _AccountTypeSelection.supplier) {
      if (!mounted) return;
      ScaffoldMessenger.maybeOf(rootContext)?.showSnackBar(
        const SnackBar(content: Text('خيار المورد غير متاح حاليًا.')),
      );
      return;
    }

    await _showRegistrationForm(accountType);
  }

  Future<void> _showRegistrationForm(_AccountTypeSelection accountType) async {
    final formKey = GlobalKey<FormState>();
    final usernameController = TextEditingController();
    final fullNameController = TextEditingController();
    final customerNameController = TextEditingController();
    final phoneController = TextEditingController();
    final employeeIdController = TextEditingController();
    final preferredUsernameController = TextEditingController();
    final passwordController = TextEditingController();
    final confirmPasswordController = TextEditingController();
    final isEmployee = accountType == _AccountTypeSelection.employee;

    await showDialog<void>(
      context: context,
      builder: (dialogContext) {
        final dialogNavContext = dialogContext;
        return StatefulBuilder(
          builder: (context, setDialogState) {
            bool busy = false;
            String? localError;

            Future<void> submit() async {
              if (busy) return;
              if (!(formKey.currentState?.validate() ?? false)) {
                return;
              }

              final trimmedUsername = usernameController.text.trim();
              final trimmedPassword = passwordController.text.trim();
              final trimmedConfirmPassword = confirmPasswordController.text.trim();
              final trimmedFullName = fullNameController.text.trim();

              if (!isEmployee && (trimmedUsername.isEmpty || trimmedPassword.isEmpty || trimmedConfirmPassword.isEmpty || trimmedFullName.isEmpty)) {
                setDialogState(() => localError = 'يرجى إدخال جميع بيانات العميل المطلوبة.');
                return;
              }

              if (isEmployee) {
                final employeeId = int.tryParse(employeeIdController.text.trim());
                if (employeeId == null || employeeId <= 0) {
                  setDialogState(() => localError = 'رقم الموظف الرسمي غير صالح.');
                  return;
                }
                if (trimmedPassword.length < 8 || trimmedPassword != trimmedConfirmPassword) {
                  setDialogState(() => localError = 'كلمة المرور يجب أن تكون 8 أحرف فأكثر ومتطابقة.');
                  return;
                }
              }

              setDialogState(() {
                busy = true;
                localError = null;
              });

              if (isEmployee) {
                final employeeId = int.tryParse(employeeIdController.text.trim());
                if (employeeId == null) {
                  setDialogState(() {
                    busy = false;
                    localError = 'رقم الموظف الرسمي غير صالح.';
                  });
                  return;
                }

                final created = await widget.auth.provisionEmployeeAccount(
                  employeeId: employeeId,
                  fullName: trimmedFullName,
                  preferredUsername: preferredUsernameController.text.trim().isEmpty ? null : preferredUsernameController.text.trim(),
                  rememberMe: rememberMe,
                );

                if (created == null) {
                  setDialogState(() {
                    busy = false;
                    localError = 'تعذر إنشاء حساب الموظف الرسمي. تأكد من صلاحية رقم الموظف والاسم.';
                  });
                  return;
                }

                final setupToken = created['setupToken'] as String?;
                final resolvedUsername = (created['username'] as String?) ?? preferredUsernameController.text.trim();
                if (setupToken == null || setupToken.isEmpty) {
                  setDialogState(() {
                    busy = false;
                    localError = 'رمز إعداد كلمة المرور غير موجود لهذا الموظف.';
                  });
                  return;
                }

                final passwordSetupError = await widget.auth.completeEmployeePasswordSetup(
                  setupToken: setupToken,
                  newPassword: trimmedPassword,
                  confirmPassword: trimmedConfirmPassword,
                );

                if (passwordSetupError != null) {
                  setDialogState(() {
                    busy = false;
                    localError = passwordSetupError;
                  });
                  return;
                }

                final loginError = await widget.auth.login(resolvedUsername, trimmedPassword, rememberMe);
                if (!mounted) return;
                if (loginError == null) {
                  if (Navigator.of(dialogNavContext).canPop()) {
                    Navigator.of(dialogNavContext).pop();
                  }
                  Navigator.of(context).pushReplacementNamed(AppRoutes.dashboard);
                  return;
                }

                setDialogState(() {
                  busy = false;
                  localError = loginError;
                });
                return;
              }

              final registerError = await widget.auth.registerCustomer(
                username: trimmedUsername,
                password: trimmedPassword,
                confirmPassword: trimmedConfirmPassword,
                fullName: trimmedFullName,
                customerName: customerNameController.text.trim(),
                phoneNumber: phoneController.text.trim(),
                rememberMe: rememberMe,
              );

              if (!mounted) return;
              if (registerError == null) {
                if (Navigator.of(dialogNavContext).canPop()) {
                  Navigator.of(dialogNavContext).pop();
                }
                Navigator.of(context).pushReplacementNamed(AppRoutes.dashboard);
                return;
              }

              setDialogState(() {
                busy = false;
                localError = registerError;
              });
            }

            return AlertDialog(
              backgroundColor: UiPalette.surfaceCard,
              title: Text(
                isEmployee ? 'إنشاء حساب موظف' : 'إنشاء حساب عميل',
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
              content: SizedBox(
                width: 420,
                child: SingleChildScrollView(
                  child: Form(
                    key: formKey,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        if (!isEmployee) ...[
                          TextFormField(
                            controller: usernameController,
                            decoration: const InputDecoration(labelText: 'اسم المستخدم', border: OutlineInputBorder()),
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'اسم المستخدم مطلوب.' : null,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: fullNameController,
                            decoration: const InputDecoration(labelText: 'الاسم الكامل', border: OutlineInputBorder()),
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'الاسم الكامل مطلوب.' : null,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: customerNameController,
                            decoration: const InputDecoration(labelText: 'اسم العميل/الجهة', border: OutlineInputBorder()),
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'اسم العميل مطلوب.' : null,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: phoneController,
                            keyboardType: TextInputType.phone,
                            decoration: const InputDecoration(labelText: 'رقم الهاتف', border: OutlineInputBorder()),
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'رقم الهاتف مطلوب.' : null,
                          ),
                        ] else ...[
                          TextFormField(
                            controller: employeeIdController,
                            keyboardType: const TextInputType.numberWithOptions(signed: false),
                            decoration: const InputDecoration(labelText: 'رقم الموظف الرسمي', border: OutlineInputBorder()),
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'رقم الموظف الرسمي مطلوب.' : null,
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: preferredUsernameController,
                            decoration: const InputDecoration(labelText: 'اسم المستخدم المفضل (اختياري)', border: OutlineInputBorder()),
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: fullNameController,
                            decoration: const InputDecoration(labelText: 'الاسم الكامل', border: OutlineInputBorder()),
                            validator: (value) => (value == null || value.trim().isEmpty) ? 'الاسم الكامل مطلوب.' : null,
                          ),
                        ],
                        const SizedBox(height: 12),
                        TextFormField(
                          controller: passwordController,
                          obscureText: true,
                          decoration: const InputDecoration(labelText: 'كلمة المرور', border: OutlineInputBorder()),
                          validator: (value) => (value == null || value.trim().length < 8) ? 'كلمة المرور يجب أن تكون 8 أحرف أو أكثر.' : null,
                        ),
                        const SizedBox(height: 12),
                        TextFormField(
                          controller: confirmPasswordController,
                          obscureText: true,
                          decoration: const InputDecoration(labelText: 'تأكيد كلمة المرور', border: OutlineInputBorder()),
                          validator: (value) => (value == null || value.trim() != passwordController.text.trim()) ? 'تأكيد كلمة المرور غير مطابق.' : null,
                        ),
                        if (localError != null) ...[
                          const SizedBox(height: 12),
                          Text(localError!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: busy ? null : () => Navigator.of(dialogContext).pop(),
                  child: const Text('إلغاء'),
                ),
                FilledButton(
                  onPressed: busy ? null : submit,
                  child: Text(busy ? 'جارٍ التنفيذ...' : 'إنشاء الحساب'),
                ),
              ],
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final background = isDark ? UiPalette.darkBackground : UiPalette.lightBackground;
    final cardColor = isDark ? UiPalette.surfaceCard : UiPalette.lightCard;

    return Scaffold(
      backgroundColor: background,
      body: Directionality(
        textDirection: TextDirection.rtl,
        child: Center(
          child: SizedBox(
            width: 360,
            child: Card(
              color: cardColor,
              elevation: 0,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(22),
                side: BorderSide(color: UiPalette.primaryBorder.withAlpha(140)),
              ),
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(
                      child: Text(
                        'لومار لإدارة الأعمال',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          color: UiPalette.adaptiveTextColor(cardColor),
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    const SizedBox(height: 24),
                    TextField(
                      controller: username,
                      textInputAction: TextInputAction.next,
                      onSubmitted: (_) => passwordFocus.requestFocus(),
                      decoration: const InputDecoration(
                        labelText: 'اسم المستخدم',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: password,
                      focusNode: passwordFocus,
                      obscureText: true,
                      textInputAction: TextInputAction.done,
                      onSubmitted: (_) => _login(),
                      decoration: const InputDecoration(
                        labelText: 'كلمة المرور',
                        border: OutlineInputBorder(),
                      ),
                    ),
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      value: rememberMe,
                      onChanged: (value) => setState(() => rememberMe = value ?? false),
                      title: const Text('تذكرني'),
                    ),
                    if (error != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        error!,
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                      ),
                    ],
                    const SizedBox(height: 20),
                    FilledButton(
                      onPressed: loading ? null : _login,
                      style: FilledButton.styleFrom(
                        backgroundColor: UiPalette.primary,
                        foregroundColor: Colors.black,
                        padding: const EdgeInsets.symmetric(vertical: 14),
                      ),
                      child: Text(loading ? 'جارٍ تسجيل الدخول...' : 'تسجيل الدخول'),
                    ),
                    const SizedBox(height: 12),
                    OutlinedButton(
                      onPressed: loading ? null : _openCreateAccountDialog,
                      style: OutlinedButton.styleFrom(
                        foregroundColor: UiPalette.adaptiveTextColor(cardColor),
                        side: const BorderSide(color: UiPalette.primaryBorder),
                        padding: const EdgeInsets.symmetric(vertical: 14),
                      ),
                      child: const Text('إنشاء حساب'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _AccountTypeTile extends StatelessWidget {
  const _AccountTypeTile({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.value,
    this.enabled = true,
  });

  final String title;
  final String subtitle;
  final IconData icon;
  final _AccountTypeSelection value;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final baseColor = enabled ? UiPalette.primary : UiPalette.borderSoft;
    return ListTile(
      enabled: enabled,
      leading: Icon(icon, color: baseColor),
      title: Text(title, style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.surfaceCard, fontWeight: FontWeight.w600)),
      subtitle: Text(subtitle, style: UiPalette.adaptiveTextStyle(context, backgroundColor: UiPalette.surfaceCard, fontSize: 12).copyWith(color: UiPalette.textSoft.withAlpha(190))),
      trailing: const Icon(Icons.arrow_forward_ios_rounded, size: 16),
      tileColor: UiPalette.softBlue,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: enabled ? UiPalette.primaryBorder : UiPalette.borderSoft),
      ),
      onTap: enabled ? () => Navigator.of(context).pop(value) : null,
    );
  }
}