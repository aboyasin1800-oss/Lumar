import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/app_navigation.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../widgets/structure_placeholder.dart';
import 'settings/code_prefix_settings_screen.dart';
import 'settings/consumption_rules_settings_screen.dart';

const Color _settingsAccent = Color.fromARGB(255, 78, 201, 176);

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({
    required this.auth,
    required this.themeState,
    required this.uiScale,
    super.key,
  });

  final AuthState auth;
  final ThemeState themeState;
  final UiScaleState uiScale;

  @override
  Widget build(BuildContext context) => SettingsHomeScreen(
        auth: auth,
        themeState: themeState,
        uiScale: uiScale,
      );
}

class SettingsHomeScreen extends StatefulWidget {
  const SettingsHomeScreen({
    required this.auth,
    required this.themeState,
    required this.uiScale,
    super.key,
  });

  final AuthState auth;
  final ThemeState themeState;
  final UiScaleState uiScale;

  @override
  State<SettingsHomeScreen> createState() => _SettingsHomeScreenState();
}

class _SettingsHomeScreenState extends State<SettingsHomeScreen> {
  List<_SettingsSection> get _sections => [
        _SettingsSection(
          title: 'الحساب والمستخدم',
          description: 'اسم المستخدم\nكلمة المرور\nالبيانات الشخصية',
          icon: Icons.person_outline_rounded,
          builder: (_) => AccountSettingsScreen(
            auth: widget.auth,
          ),
        ),
        _SettingsSection(
          title: 'إعدادات المظهر',
          description: 'الوضع الداكن\nالوضع الفاتح\nحجم المحتوى',
          icon: Icons.palette_outlined,
          builder: (_) => AppearanceSettingsScreen(
            themeState: widget.themeState,
            uiScale: widget.uiScale,
          ),
        ),
        _SettingsSection(
          title: 'إدارة الأكواد',
          description: 'الأكواد التسلسلية\nالبادئات\nالتوليد التلقائي',
          icon: Icons.code_outlined,
          builder: (_) => const CodePrefixSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات المبيعات',
          description: 'كل ما يخص المبيعات\nحتى إذا كانت الشاشة مؤقتاً Placeholder',
          icon: Icons.shopping_cart_outlined,
          builder: (_) => const SalesSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات المخزون',
          description: 'إعدادات إدارة المخزون',
          icon: Icons.inventory_2_outlined,
          builder: (_) => const InventorySettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات الإنتاج',
          description: 'إعدادات الإنتاج',
          icon: Icons.factory_outlined,
          builder: (_) => const ProductionSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات القياسات',
          description: 'ملفات القياسات',
          icon: Icons.straighten_rounded,
          builder: (_) => const MeasurementsSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات أنواع القطع',
          description: 'أنواع القطع',
          icon: Icons.cut_outlined,
          builder: (_) => const PieceTypesSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات قواعد الاستهلاك',
          description: 'قواعد استهلاك القماش',
          icon: Icons.texture_outlined,
          builder: (_) => const ConsumptionRulesSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات التسعير',
          description: 'قواعد التسعير',
          icon: Icons.price_change_outlined,
          builder: (_) => const PricingSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات الأقمشة',
          description: 'تصنيفات الأقمشة',
          icon: Icons.checkroom_outlined,
          builder: (_) => const FabricSettingsScreen(),
        ),
        _SettingsSection(
          title: 'إعدادات النظام',
          description: 'الإعدادات العامة\nواعدادات الطباعة',
          icon: Icons.settings_applications_outlined,
          builder: (_) => const SystemSettingsScreen(),
        ),
      ];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('الإعدادات'),
        centerTitle: false,
      ),
      body: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: _sections.length,
        separatorBuilder: (_, __) => const SizedBox(height: 12),
        itemBuilder: (context, index) {
          final section = _sections[index];
          return Card(
            color: theme.colorScheme.surface,
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(18),
              side: BorderSide(
                color: _settingsAccent.withOpacity(0.7),
                width: 1.2,
              ),
            ),
            child: InkWell(
              onTap: () => AppNavigation.push(context, section.builder),
              borderRadius: BorderRadius.circular(18),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  textDirection: TextDirection.rtl,
                  children: [
                    Container(
                      width: 52,
                      height: 52,
                      decoration: BoxDecoration(
                        color: _settingsAccent.withOpacity(0.14),
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(
                          color: _settingsAccent.withOpacity(0.72),
                          width: 1,
                        ),
                      ),
                      child: Icon(
                        section.icon,
                        color: _settingsAccent,
                        size: 26,
                      ),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            section.title,
                            style: theme.textTheme.titleLarge?.copyWith(
                                  fontWeight: FontWeight.w700,
                                ) ??
                                const TextStyle(
                                  fontWeight: FontWeight.w700,
                                  fontSize: 18,
                                ),
                            textAlign: TextAlign.right,
                          ),
                          const SizedBox(height: 6),
                          Text(
                            section.description,
                            style: theme.textTheme.bodyMedium?.copyWith(
                                  color: theme.colorScheme.onSurfaceVariant,
                                ) ??
                                TextStyle(
                                  color: theme.colorScheme.onSurfaceVariant,
                                ),
                            textAlign: TextAlign.right,
                          ),
                        ],
                      ),
                    ),
                    Icon(
                      Icons.arrow_forward_ios_rounded,
                      color: _settingsAccent,
                      size: 18,
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _SettingsSection {
  const _SettingsSection({
    required this.title,
    required this.description,
    required this.icon,
    required this.builder,
  });

  final String title;
  final String description;
  final IconData icon;
  final WidgetBuilder builder;
}

class AccountSettingsScreen extends StatefulWidget {
  const AccountSettingsScreen({required this.auth, super.key});

  final AuthState auth;

  @override
  State<AccountSettingsScreen> createState() => _AccountSettingsScreenState();
}

class _AccountSettingsScreenState extends State<AccountSettingsScreen> {
  final currentPassword = TextEditingController();
  final username = TextEditingController();
  final newPassword = TextEditingController();
  final confirmPassword = TextEditingController();
  final currentPasswordFocus = FocusNode();
  final newPasswordFocus = FocusNode();
  final confirmPasswordFocus = FocusNode();
  String? message;

  @override
  void initState() {
    super.initState();
    username.text = widget.auth.user!.username;
  }

  @override
  void dispose() {
    currentPassword.dispose();
    username.dispose();
    newPassword.dispose();
    confirmPassword.dispose();
    currentPasswordFocus.dispose();
    newPasswordFocus.dispose();
    confirmPasswordFocus.dispose();
    super.dispose();
  }

  Future<void> _changeUsername() async {
    final result = await widget.auth.changeUsername(currentPassword.text, username.text);
    if (!mounted) return;
    setState(() => message = result ?? 'تم تحديث اسم المستخدم.');
    if (result == null) currentPassword.clear();
  }

  Future<void> _changePassword() async {
    final result = await widget.auth.changePassword(
      currentPassword.text,
      newPassword.text,
      confirmPassword.text,
    );
    if (!mounted) return;
    setState(() => message = result ?? 'تم تحديث كلمة المرور. يرجى تسجيل الدخول مجددًا.');
    if (result == null) {
      Navigator.of(context).pushNamedAndRemoveUntil('/login', (_) => false);
    }
  }

  String _roleLabel(String? role) => switch (role?.toLowerCase()) {
        'admin' => 'مدير',
        'viewer' => 'مستعرض',
        _ => 'غير محدد',
      };

  String _lastLoginLabel(BuildContext context, String value) {
    final date = DateTime.tryParse(value)?.toLocal();
    return date == null
        ? 'غير محدد'
        : MaterialLocalizations.of(context).formatFullDate(date);
  }

  @override
  Widget build(BuildContext context) {
    final user = widget.auth.user!;
    return Scaffold(
      appBar: AppBar(title: const Text('الحساب والمستخدم')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('إعدادات الحساب', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          ListTile(
            title: const Text('اسم المستخدم'),
            subtitle: Text(user.username),
          ),
          ListTile(
            title: const Text('الاسم الكامل'),
            subtitle: Text(user.fullName),
          ),
          ListTile(
            title: const Text('الدور'),
            subtitle: Text(_roleLabel(user.role)),
          ),
          ListTile(
            title: const Text('حالة الحساب'),
            subtitle: Text(user.isActive ? 'نشط' : 'غير نشط'),
          ),
          if (user.lastLoginUtc != null)
            ListTile(
              title: const Text('آخر تسجيل دخول'),
              subtitle: Text(_lastLoginLabel(context, user.lastLoginUtc!)),
            ),
          const SizedBox(height: 20),
          Text('تغيير اسم المستخدم', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 10),
          TextField(
            controller: username,
            textInputAction: TextInputAction.next,
            onSubmitted: (_) => currentPasswordFocus.requestFocus(),
            decoration: const InputDecoration(
              labelText: 'اسم المستخدم الجديد',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: currentPassword,
            focusNode: currentPasswordFocus,
            obscureText: true,
            textInputAction: TextInputAction.done,
            onSubmitted: (_) => _changeUsername(),
            decoration: const InputDecoration(
              labelText: 'كلمة المرور الحالية',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton(
              onPressed: _changeUsername,
              child: const Text('تغيير اسم المستخدم'),
            ),
          ),
          const SizedBox(height: 24),
          Text('تغيير كلمة المرور', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 10),
          TextField(
            controller: newPassword,
            focusNode: newPasswordFocus,
            obscureText: true,
            textInputAction: TextInputAction.next,
            onSubmitted: (_) => confirmPasswordFocus.requestFocus(),
            decoration: const InputDecoration(
              labelText: 'كلمة المرور الجديدة',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: confirmPassword,
            focusNode: confirmPasswordFocus,
            obscureText: true,
            textInputAction: TextInputAction.done,
            onSubmitted: (_) => _changePassword(),
            decoration: const InputDecoration(
              labelText: 'تأكيد كلمة المرور الجديدة',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton(
              onPressed: _changePassword,
              child: const Text('تغيير كلمة المرور'),
            ),
          ),
          if (message != null)
            Padding(
              padding: const EdgeInsets.only(top: 16),
              child: Text(message!),
            ),
        ],
      ),
    );
  }
}

class AppearanceSettingsScreen extends StatelessWidget {
  const AppearanceSettingsScreen({
    required this.themeState,
    required this.uiScale,
    super.key,
  });

  final ThemeState themeState;
  final UiScaleState uiScale;

  String _themeLabel(AppThemePreference preference) => switch (preference) {
        AppThemePreference.light => 'الوضع الفاتح',
        AppThemePreference.dark => 'الوضع الداكن',
        AppThemePreference.system => 'اتباع إعدادات النظام',
      };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('إعدادات المظهر')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('المظهر', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          RadioGroup<AppThemePreference>(
            groupValue: themeState.preference,
            onChanged: (value) {
              if (value != null) themeState.setPreference(value);
            },
            child: Column(
              children: AppThemePreference.values
                  .map(
                    (preference) => RadioListTile<AppThemePreference>(
                      value: preference,
                      title: Text(_themeLabel(preference)),
                    ),
                  )
                  .toList(),
            ),
          ),
          const SizedBox(height: 16),
          Text('حجم المحتوى', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          DropdownButtonFormField<double>(
            initialValue: uiScale.scale,
            decoration: const InputDecoration(
              border: OutlineInputBorder(),
            ),
            items: UiScaleState.levels
                .map(
                  (scale) => DropdownMenuItem(
                    value: scale,
                    child: Text('${(scale * 100).round()}%'),
                  ),
                )
                .toList(),
            onChanged: (value) {
              if (value != null) uiScale.setScale(value);
            },
          ),
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerRight,
            child: OutlinedButton(
              onPressed: uiScale.reset,
              child: const Text('إعادة الافتراضي'),
            ),
          ),
        ],
      ),
    );
  }
}

class SalesSettingsScreen extends StatelessWidget {
  const SalesSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات المبيعات',
        description: 'كل ما يخص المبيعات\nالصفحة جاهزة للتنظيم لاحقاً دون تعديل قاعدة البيانات.',
      );
}

class InventorySettingsScreen extends StatelessWidget {
  const InventorySettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات المخزون',
        description: 'إعدادات إدارة المخزون\nالهيكل جاهز لاستقبال التعديلات المستقبلية.',
      );
}

class ProductionSettingsScreen extends StatelessWidget {
  const ProductionSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات الإنتاج',
        description: 'إعدادات الإنتاج\nلا توجد تغييرات على قاعدة البيانات في هذا الإصدار.',
      );
}

class MeasurementsSettingsScreen extends StatefulWidget {
  const MeasurementsSettingsScreen({super.key});

  @override
  State<MeasurementsSettingsScreen> createState() => _MeasurementsSettingsScreenState();
}

class _MeasurementsSettingsScreenState extends State<MeasurementsSettingsScreen> {
  static const String _baseUrl = 'http://127.0.0.1:5093';

  bool _loading = true;
  bool _saving = false;
  List<_MeasurementTypeRow> _items = const [];
  final TextEditingController _newNameController = TextEditingController();
  final TextEditingController _newCountController = TextEditingController(text: '5');
  final List<TextEditingController> _newFieldControllers = [];
  final List<FocusNode> _newFieldFocusNodes = [];
  final FocusNode _newNameFocusNode = FocusNode();
  final FocusNode _newCountFocusNode = FocusNode();
  bool _isAddingNew = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _newNameFocusNode.dispose();
    _newCountFocusNode.dispose();
    _newNameController.dispose();
    _newCountController.dispose();
    for (final controller in _newFieldControllers) {
      controller.dispose();
    }
    for (final focusNode in _newFieldFocusNodes) {
      focusNode.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final response = await http.get(Uri.parse('$_baseUrl/consumption-rules'));
      if (!mounted) return;
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('HTTP ${response.statusCode}');
      }

      final decoded = jsonDecode(response.body) as Map<String, dynamic>;
      final productTypes = ((decoded['productTypes'] as List?) ?? const [])
          .map((item) => _MeasurementTypeRow.fromJson(item as Map<String, dynamic>))
          .toList();
      final fields = ((decoded['measurementFields'] as List?) ?? const []);
      final grouped = <int, List<String>>{};
      for (final item in fields) {
        final map = item as Map<String, dynamic>;
        final productTypeId = (map['productTypeId'] as int?) ?? 0;
        final name = (map['nameAr'] as String?) ?? '';
        if (productTypeId > 0 && name.trim().isNotEmpty) {
          grouped.putIfAbsent(productTypeId, () => <String>[]);
          grouped[productTypeId]!.add(name.trim());
        }
      }

      setState(() {
        _items = productTypes.map((row) {
          final names = grouped[row.productTypeId] ?? const <String>[];
          return row.copyWith(fieldNames: names);
        }).toList();
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() => _loading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر تحميل البيانات: $error')),
      );
    }
  }

  String _friendlyErrorMessage(Object error, {String fallback = 'حدث خطأ غير متوقع.'}) {
    final raw = error.toString();
    final normalized = raw
        .replaceFirst(RegExp(r'^(Exception|Error):\s*', caseSensitive: false), '')
        .replaceAll(RegExp(r'\s+'), ' ')
        .trim();
    final lower = normalized.toLowerCase();

    if (lower.contains('duplicate') || lower.contains('مكرر') || lower.contains('unique index') || lower.contains('ix_') || lower.contains('هذه القاعدة موجودة') || lower.contains('القاعدة موجودة') || lower.contains('already exists')) {
      return 'هذه القاعدة موجودة فعليًا لنفس القطعة بنفس المعطيات. استخدم اسمًا أو صيغة مختلفة.';
    }

    if (lower.contains('not found') || lower.contains('غير موجود')) {
      return 'القطعة غير موجودة أو تم حذفها.';
    }

    if (lower.contains('required') || lower.contains('مطلوب') || lower.contains('validation')) {
      return 'بيانات القطعة غير مكتملة. تحقق من الاسم وأسماء الحقول.';
    }

    if (normalized.isNotEmpty && normalized != 'null') {
      return normalized;
    }

    return fallback;
  }

  Future<void> _saveNewItem() async {
    final pieceName = _newNameController.text.trim();
    if (pieceName.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يجب إدخال اسم القطعة.')),
      );
      return;
    }

    final count = int.tryParse(_newCountController.text.trim()) ?? 0;
    final list = _newFieldControllers
        .map((controller) => controller.text.trim())
        .where((value) => value.isNotEmpty)
        .toList();

    if (count <= 0 && list.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يجب تحديد عدد الحقول أو إدخال أسماء الحقول.')),
      );
      return;
    }

    final payload = {
      'nameAr': pieceName,
      'fieldCount': count > 0 ? count : list.length,
      'fieldNames': list.isNotEmpty ? list : List.generate(count, (index) => 'حقل ${index + 1}'),
    };

    try {
      setState(() => _saving = true);
      final response = await http.post(
        Uri.parse('$_baseUrl/consumption-rules/measurement-types'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(payload),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(response.body);
      }

      if (!mounted) return;
      _newNameController.clear();
      _newCountController.text = '5';
      for (final controller in _newFieldControllers) {
        controller.dispose();
      }
      for (final focusNode in _newFieldFocusNodes) {
        focusNode.dispose();
      }
      _newFieldControllers.clear();
      _newFieldFocusNodes.clear();
      setState(() => _isAddingNew = false);
      await _load();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر إنشاء القطعة: ${_friendlyErrorMessage(error)}')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _updateRow(_MeasurementTypeRow row) async {
    final fieldNames = row.fieldNames
        .map((name) => name.trim())
        .where((name) => name.isNotEmpty)
        .toList();

    if (fieldNames.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يجب وجود حقل قياس واحد على الأقل.')),
      );
      return;
    }

    try {
      setState(() => _saving = true);
      final response = await http.put(
        Uri.parse('$_baseUrl/consumption-rules/measurement-types/${row.productTypeId}'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'nameAr': row.nameAr,
          'fieldNames': fieldNames,
        }),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(response.body);
      }

      await _load();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر تحديث قياسات القطعة: ${_friendlyErrorMessage(error)}')),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _deleteRow(_MeasurementTypeRow row) async {
    final proceed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('تأكيد الحذف'),
        content: Text('هل تريد حذف قطعة ${row.nameAr}؟\nإذا كانت مرتبطة بقواعد الاستهلاك سيتم منع الحذف.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('إلغاء')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('حذف')),
        ],
      ),
    );

    if (proceed != true) return;

    try {
      final response = await http.delete(Uri.parse('$_baseUrl/consumption-rules/measurement-types/${row.productTypeId}'));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception(response.body);
      }
      await _load();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذر حذف القطعة: ${_friendlyErrorMessage(error)}')),
      );
    }
  }

  void _generateNewFieldControllers(int count) {
    final previousValues = <String>[];
    for (var i = 0; i < _newFieldControllers.length && i < count; i++) {
      previousValues.add(_newFieldControllers[i].text.trim());
    }

    for (final controller in _newFieldControllers) {
      controller.dispose();
    }
    for (final focusNode in _newFieldFocusNodes) {
      focusNode.dispose();
    }

    _newFieldControllers.clear();
    _newFieldFocusNodes.clear();

    for (var i = 0; i < count; i++) {
      final value = i < previousValues.length && previousValues[i].isNotEmpty ? previousValues[i] : (i == 0 ? 'الطول' : '');
      _newFieldControllers.add(TextEditingController(text: value));
      _newFieldFocusNodes.add(FocusNode());
    }
  }

  void _refreshFieldEditorsFromCount() {
    final count = int.tryParse(_newCountController.text.trim()) ?? 0;
    if (count <= 0) {
      return;
    }

    if (count == _newFieldControllers.length) {
      return;
    }

    FocusScope.of(context).unfocus();
    setState(() {
      _generateNewFieldControllers(count);
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || _newFieldFocusNodes.isEmpty) {
        return;
      }
      FocusScope.of(context).requestFocus(_newFieldFocusNodes.first);
    });
  }

  void _moveToNextNewField(int index) {
    if (index < _newFieldFocusNodes.length - 1) {
      FocusScope.of(context).requestFocus(_newFieldFocusNodes[index + 1]);
      return;
    }
    FocusScope.of(context).unfocus();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('إدارة قياسات القطع'),
        actions: [
          IconButton(
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'تحديث',
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  textDirection: TextDirection.rtl,
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text('أنواع القطع', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                    FilledButton.icon(
                      onPressed: () => setState(() {
                        _isAddingNew = !_isAddingNew;
                        if (_isAddingNew) {
                          _generateNewFieldControllers(int.tryParse(_newCountController.text.trim()) ?? 5);
                        }
                      }),
                      icon: const Icon(Icons.add_rounded),
                      label: const Text('إضافة قطعة'),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                if (_isAddingNew)
                  Container(
                    padding: const EdgeInsets.all(16),
                    margin: const EdgeInsets.only(bottom: 16),
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.surface,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: const Color(0xFF2B6869)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          textDirection: TextDirection.rtl,
                          children: [
                            Expanded(
                              child: TextField(
                                controller: _newNameController,
                                focusNode: _newNameFocusNode,
                                textInputAction: TextInputAction.next,
                                textAlign: TextAlign.right,
                                onSubmitted: (_) => FocusScope.of(context).requestFocus(_newCountFocusNode),
                                decoration: const InputDecoration(
                                  labelText: 'نوع القطعة',
                                  border: OutlineInputBorder(),
                                ),
                              ),
                            ),
                            const SizedBox(width: 12),
                            SizedBox(
                              width: 120,
                              child: TextField(
                                controller: _newCountController,
                                focusNode: _newCountFocusNode,
                                keyboardType: TextInputType.number,
                                textInputAction: TextInputAction.next,
                                textAlign: TextAlign.right,
                                onEditingComplete: () => _refreshFieldEditorsFromCount(),
                                onSubmitted: (_) => _refreshFieldEditorsFromCount(),
                                decoration: const InputDecoration(
                                  labelText: 'عدد الحقول',
                                  border: OutlineInputBorder(),
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Wrap(
                          spacing: 10,
                          runSpacing: 10,
                          children: List.generate(_newFieldControllers.length, (index) {
                            return SizedBox(
                              width: 170,
                              child: TextField(
                                controller: _newFieldControllers[index],
                                focusNode: _newFieldFocusNodes[index],
                                textInputAction: index == _newFieldControllers.length - 1 ? TextInputAction.done : TextInputAction.next,
                                textAlign: TextAlign.right,
                                onSubmitted: (_) => _moveToNextNewField(index),
                                decoration: InputDecoration(
                                  labelText: 'الحقل ${index + 1}',
                                  border: const OutlineInputBorder(),
                                ),
                              ),
                            );
                          }),
                        ),
                        const SizedBox(height: 12),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: FilledButton.icon(
                            onPressed: _saving ? null : _saveNewItem,
                            icon: const Icon(Icons.save_rounded),
                            label: const Text('حفظ'),
                          ),
                        ),
                      ],
                    ),
                  ),
                if (_items.isEmpty)
                  Container(
                    padding: const EdgeInsets.all(20),
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.surface,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: const Text('لا توجد قطع مسجلة حالياً.', textAlign: TextAlign.center),
                  )
                else
                  ..._items.map((item) => _MeasurementTypeCard(
                        item: item,
                        onChanged: (updated) {
                          setState(() {
                            final index = _items.indexWhere((entry) => entry.productTypeId == updated.productTypeId);
                            if (index >= 0) {
                              _items[index] = updated;
                            }
                          });
                        },
                        onSave: _updateRow,
                        onDelete: _deleteRow,
                      )),
              ],
            ),
    );
  }
}

class _MeasurementTypeCard extends StatefulWidget {
  const _MeasurementTypeCard({
    required this.item,
    required this.onChanged,
    required this.onSave,
    required this.onDelete,
  });

  final _MeasurementTypeRow item;
  final ValueChanged<_MeasurementTypeRow> onChanged;
  final Future<void> Function(_MeasurementTypeRow) onSave;
  final Future<void> Function(_MeasurementTypeRow) onDelete;

  @override
  State<_MeasurementTypeCard> createState() => _MeasurementTypeCardState();
}

class _MeasurementTypeCardState extends State<_MeasurementTypeCard> {
  late final List<TextEditingController> _controllers;
  late final List<FocusNode> _focusNodes;
  bool _expanded = false;

  @override
  void initState() {
    super.initState();
    _controllers = [
      for (final name in widget.item.fieldNames)
        TextEditingController(text: name),
    ];
    _focusNodes = [
      for (var i = 0; i < _controllers.length; i++) FocusNode(),
    ];
  }

  @override
  void dispose() {
    for (final controller in _controllers) {
      controller.dispose();
    }
    for (final focusNode in _focusNodes) {
      focusNode.dispose();
    }
    super.dispose();
  }

  void _addField() {
    setState(() {
      _controllers.add(TextEditingController(text: 'قياس جديد'));
      _focusNodes.add(FocusNode());
    });
  }

  Future<void> _saveCurrent() async {
    final names = _controllers.map((controller) => controller.text.trim()).where((value) => value.isNotEmpty).toList();
    final updated = widget.item.copyWith(fieldNames: names, nameAr: widget.item.nameAr);
    widget.onChanged(updated);
    await widget.onSave(updated);
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFF2B6869)),
      ),
      child: Column(
        children: [
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            borderRadius: BorderRadius.circular(16),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              child: Row(
                textDirection: TextDirection.rtl,
                children: [
                  Expanded(
                    child: Row(
                      textDirection: TextDirection.rtl,
                      children: [
                        Text(widget.item.nameAr, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                        const SizedBox(width: 12),
                        Text('عدد القياسات: ${widget.item.fieldNames.length}', style: const TextStyle(color: Colors.grey, fontSize: 12)),
                      ],
                    ),
                  ),
                  Icon(_expanded ? Icons.keyboard_arrow_up_rounded : Icons.keyboard_arrow_down_rounded),
                ],
              ),
            ),
          ),
          if (_expanded)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
              child: Column(
                children: [
                  Text('عدد حقول القياسات: ${_controllers.length}', textAlign: TextAlign.right),
                  const SizedBox(height: 12),
                  if (_controllers.isNotEmpty)
                    Wrap(
                      spacing: 10,
                      runSpacing: 10,
                      children: List.generate(_controllers.length, (index) {
                        return SizedBox(
                          width: 180,
                          child: TextField(
                            controller: _controllers[index],
                            focusNode: _focusNodes[index],
                            textInputAction: index == _controllers.length - 1 ? TextInputAction.done : TextInputAction.next,
                            textAlign: TextAlign.right,
                            onSubmitted: (_) {
                              if (index < _controllers.length - 1) {
                                FocusScope.of(context).requestFocus(_focusNodes[index + 1]);
                              } else {
                                FocusScope.of(context).unfocus();
                              }
                            },
                            decoration: InputDecoration(
                              labelText: 'قياس ${index + 1}',
                              border: const OutlineInputBorder(),
                            ),
                          ),
                        );
                      }),
                    ),
                  const SizedBox(height: 14),
                  Row(
                    textDirection: TextDirection.rtl,
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      TextButton.icon(
                        onPressed: _addField,
                        icon: const Icon(Icons.add_rounded),
                        label: const Text('إضافة حقل قياس'),
                      ),
                      const SizedBox(width: 8),
                      TextButton.icon(
                        onPressed: () => _saveCurrent(),
                        icon: const Icon(Icons.save_rounded),
                        label: const Text('حفظ'),
                      ),
                      const SizedBox(width: 8),
                      TextButton.icon(
                        onPressed: () => widget.onDelete(widget.item),
                        icon: const Icon(Icons.delete_rounded),
                        label: const Text('حذف القطعة'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _MeasurementTypeRow {
  const _MeasurementTypeRow({
    required this.productTypeId,
    required this.nameAr,
    required this.fieldNames,
  });

  final int productTypeId;
  final String nameAr;
  final List<String> fieldNames;

  _MeasurementTypeRow copyWith({String? nameAr, List<String>? fieldNames}) => _MeasurementTypeRow(
        productTypeId: productTypeId,
        nameAr: nameAr ?? this.nameAr,
        fieldNames: fieldNames ?? this.fieldNames,
      );

  factory _MeasurementTypeRow.fromJson(Map<String, dynamic> json) {
    final productTypeId = (json['productTypeId'] as int?) ?? 0;
    final name = (json['nameAr'] as String?) ?? '';
    return _MeasurementTypeRow(
      productTypeId: productTypeId,
      nameAr: name,
      fieldNames: const [],
    );
  }
}

class PieceTypesSettingsScreen extends StatelessWidget {
  const PieceTypesSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات أنواع القطع',
        description: 'أنواع القطع\nتجهيز الهيكل فقط دون تعديل البيانات.',
      );
}

class FabricConsumptionRulesSettingsScreen extends StatelessWidget {
  const FabricConsumptionRulesSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const ConsumptionRulesSettingsScreen();
}

class PricingSettingsScreen extends StatelessWidget {
  const PricingSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات التسعير',
        description: 'قواعد التسعير\nتم تجهيز هيكل الصفحة فقط.',
      );
}

class FabricSettingsScreen extends StatelessWidget {
  const FabricSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات الأقمشة',
        description: 'تصنيفات الأقمشة\nلا توجد تغييرات على قاعدة البيانات.',
      );
}

class SystemSettingsScreen extends StatelessWidget {
  const SystemSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) => const StructurePlaceholder(
        title: 'إعدادات النظام',
        description: 'الإعدادات العامة\nإعدادات الطباعة\nالهيكل جاهز للمرحلة التالية.',
      );
}
