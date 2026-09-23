import 'package:flutter/material.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class LoyaltyRulesScreen extends StatefulWidget {
  const LoyaltyRulesScreen({super.key, this.repository});

  final LoyaltyRepository? repository;

  @override
  State<LoyaltyRulesScreen> createState() => _LoyaltyRulesScreenState();
}

class _LoyaltyRulesScreenState extends State<LoyaltyRulesScreen> {
  late final LoyaltyRepository _repository =
      widget.repository ?? LoyaltyRepository();
  late Future<_LoyaltyRulesData> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<_LoyaltyRulesData> _load() async {
    final values = await Future.wait<dynamic>([
      _repository.getProgramSettings(),
      _repository.getProductLoyaltyPointSettings(),
      _repository.getImportedProductLoyaltyPointSettings(),
      _repository.getRules(),
    ]);
    final programSettings = values[0] as List<LoyaltyProgramSettings>;
    final productSettings = values[1] as List<ProductLoyaltyPointSetting>;
    final importedSettings =
        values[2] as List<ImportedProductLoyaltyPointSetting>;
    final activeProductSettings = productSettings.where(
      (item) => item.isConfigured && item.isSettingActive == true,
    );
    final activeImportedSettings = importedSettings.where(
      (item) => item.isConfigured && item.isSettingActive == true,
    );
    ({double basePoints, List<double> referralLevels})? referralEvaluation;
    try {
      final activeProduct = activeProductSettings.firstOrNull;
      final activeImported = activeImportedSettings.firstOrNull;
      if (activeProduct != null) {
        referralEvaluation = await _repository.evaluateOfficialReferralLevels(
          productTypeId: activeProduct.productTypeId,
        );
      } else if (activeImported != null) {
        referralEvaluation = await _repository.evaluateOfficialReferralLevels(
          importedReadyMadeProductId: activeImported.importedReadyMadeProductId,
        );
      }
    } catch (_) {
      referralEvaluation = null;
    }
    return _LoyaltyRulesData(
      program: programSettings.isEmpty ? null : programSettings.first,
      productSettings: productSettings,
      importedSettings: importedSettings,
      registeredRules: values[3] as List<LoyaltyRule>,
      referralEvaluation: referralEvaluation,
    );
  }

  Future<void> _refresh() async {
    setState(() => _future = _load());
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        title: const Text('قواعد الولاء'),
        actions: [
          IconButton(
            tooltip: 'تحديث القواعد',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: FutureBuilder<_LoyaltyRulesData>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return _ErrorState(
              message: RlUiText.friendlyError(snapshot.error),
              onRetry: _refresh,
            );
          }
          final data = snapshot.data;
          if (data == null) {
            return _ErrorState(
              message: 'تعذر قراءة قواعد الولاء.',
              onRetry: _refresh,
            );
          }
          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(20),
              children: [
                _buildIntro(context, data),
                const SizedBox(height: 16),
                _buildProgramStatus(context, data),
                const SizedBox(height: 16),
                _buildEarningRules(context, data),
                const SizedBox(height: 16),
                _buildRedemptionRules(context, data),
                const SizedBox(height: 16),
                _buildFreezeRules(context, data),
                const SizedBox(height: 16),
                if (data.referralEvaluation != null)
                  _buildReferralRules(context, data),
                if (data.referralEvaluation != null) const SizedBox(height: 16),
                _buildRegisteredRules(context, data),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _buildIntro(BuildContext context, _LoyaltyRulesData data) {
    final updatedAt = data.latestUpdate;
    return _section(
      context,
      icon: Icons.rule_outlined,
      title: 'المرجع التشغيلي',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'تعرض هذه الشاشة القواعد التي يقرأها الخادم وقت التشغيل، ولا تنشئ سياسة مستقلة داخل التطبيق.',
            style: TextStyle(color: UiPalette.textSoft, height: 1.5),
          ),
          if (updatedAt != null) ...[
            const SizedBox(height: 10),
            Text(
              'آخر تحديث متاح من المصدر: ${_formatDate(updatedAt)}',
              style: TextStyle(color: UiPalette.textSoft, fontSize: 12),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildProgramStatus(BuildContext context, _LoyaltyRulesData data) {
    final program = data.program;
    final activeRules =
        data.registeredRules.where((rule) => rule.isActive).length;
    final inactiveRules = data.registeredRules.length - activeRules;
    return _section(
      context,
      icon: Icons.toggle_on_outlined,
      title: 'حالة البرنامج',
      child: Column(
        children: [
          _statusLine(
            context,
            'برنامج الولاء',
            program == null
                ? 'غير متاح من المصدر'
                : program.isEnabled
                    ? 'مفعّل'
                    : 'غير مفعّل',
            program?.isEnabled == true,
          ),
          _statusLine(
            context,
            'كسب النقاط',
            program == null
                ? 'غير متاح من المصدر'
                : data.activeEarningSources == 0
                    ? 'غير متاح حاليًا'
                    : program.isEnabled
                        ? 'مفعّل'
                        : 'متوقف مع البرنامج',
            program?.isEnabled == true && data.activeEarningSources > 0,
          ),
          _statusLine(
            context,
            'استبدال النقاط',
            program == null
                ? 'غير متاح من المصدر'
                : program.allowRedemption
                    ? 'مسموح'
                    : 'غير مسموح',
            program?.allowRedemption == true,
          ),
          const Divider(height: 24),
          _valueLine('سجلات قواعد الولاء النشطة', '$activeRules'),
          _valueLine('سجلات قواعد الولاء المعطلة', '$inactiveRules'),
        ],
      ),
    );
  }

  Widget _buildEarningRules(BuildContext context, _LoyaltyRulesData data) {
    final activeProducts = data.productSettings
        .where((item) => item.isConfigured && item.isSettingActive == true)
        .toList();
    final activeImported = data.importedSettings
        .where((item) => item.isConfigured && item.isSettingActive == true)
        .toList();
    return _section(
      context,
      icon: Icons.add_circle_outline,
      title: 'قواعد منح النقاط',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'يُحسب الكسب من إعداد النقاط الفعال المرتبط بالمعرف الرسمي للمنتج أو الصنف المستورد.',
          ),
          const SizedBox(height: 12),
          _valueLine(
              'مصادر أنواع المنتجات الفعالة', '${activeProducts.length}'),
          _valueLine(
            'مصادر الأصناف المستوردة الفعالة',
            '${activeImported.length}',
          ),
          if (activeProducts.isNotEmpty) ...[
            const Divider(height: 24),
            const Text('أنواع المنتجات الفعالة'),
            const SizedBox(height: 6),
            ...activeProducts.map(
              (item) => _sourceLine(
                item.nameAr,
                '${_number(item.points ?? 0)} نقطة لكل قطعة',
              ),
            ),
          ],
          if (activeImported.isNotEmpty) ...[
            const Divider(height: 24),
            const Text('الأصناف المستوردة الفعالة'),
            const SizedBox(height: 6),
            ...activeImported.map(
              (item) => _sourceLine(
                item.productName,
                '${_number(item.points ?? 0)} نقطة لكل قطعة',
              ),
            ),
          ],
          if (activeProducts.isEmpty && activeImported.isEmpty) ...[
            const SizedBox(height: 12),
            Text(
              'لا يوجد مصدر نقاط فعال حاليًا.',
              style: TextStyle(color: UiPalette.textSoft),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildRedemptionRules(BuildContext context, _LoyaltyRulesData data) {
    final program = data.program;
    return _section(
      context,
      icon: Icons.redeem_outlined,
      title: 'قواعد الاستبدال',
      child: program == null
          ? const Text('لا توجد إعدادات برنامج متاحة من المصدر.')
          : Column(
              children: [
                _valueLine(
                  'الحد الأدنى للاستبدال',
                  '${_number(program.minimumRedemptionPoints)} نقطة',
                ),
                _valueLine(
                  'الحد الأقصى للاستبدال',
                  program.maximumRedemptionPoints == 0
                      ? 'بلا حد أعلى'
                      : '${_number(program.maximumRedemptionPoints)} نقطة',
                ),
                _valueLine(
                  'قيمة النقطة النقدية',
                  _number(program.pointMonetaryValue),
                ),
                _valueLine(
                  'السماح العام بالاستبدال',
                  program.allowRedemption ? 'مسموح' : 'غير مسموح',
                ),
                const Divider(height: 24),
                Text(
                  'تُراجع بقية شروط العملية في الخادم عند المعاينة والتنفيذ وفق الرصيد والحساب والطلب.',
                  style: TextStyle(color: UiPalette.textSoft, height: 1.5),
                ),
              ],
            ),
    );
  }

  Widget _buildFreezeRules(BuildContext context, _LoyaltyRulesData data) {
    final program = data.program;
    return _section(
      context,
      icon: Icons.lock_clock_outlined,
      title: 'قواعد النشاط والتجميد وإعادة التنشيط',
      child: program == null
          ? const Text('لا توجد إعدادات برنامج متاحة من المصدر.')
          : Column(
              children: [
                _valueLine(
                  'تطبيق التجميد',
                  program.loyaltyFreezeEnabled ? 'مفعّل' : 'غير مفعّل',
                ),
                _valueLine(
                  'فترة السماح',
                  '${program.gracePeriodDays} يومًا',
                ),
                _valueLine(
                  'فترة الإنذار',
                  '${program.warningPeriodDays} يومًا',
                ),
                _valueLine(
                  'إعادة التنشيط اليدوية',
                  program.manualReactivationEnabled ? 'مسموحة' : 'غير مسموحة',
                ),
                _valueLine(
                  'إعادة التنشيط عند شراء مؤهل',
                  program.purchaseReactivationEnabled ? 'مسموحة' : 'غير مسموحة',
                ),
                const Divider(height: 24),
                Text(
                  'يستخدم الخادم آخر نشاط مؤهل لتقييم حالة الحساب. لا تعرض هذه الشاشة سياسة انتهاء نقاط مستقلة لعدم وجود مصدر تنفيذي لها.',
                  style: TextStyle(color: UiPalette.textSoft, height: 1.5),
                ),
              ],
            ),
    );
  }

  Widget _buildRegisteredRules(BuildContext context, _LoyaltyRulesData data) {
    final rules = data.registeredRules;
    return _section(
      context,
      icon: Icons.fact_check_outlined,
      title: 'سجلات إدارية محفوظة غير مطبقة في محرك المنح',
      child: rules.isEmpty
          ? const Text('لا توجد سجلات قواعد في المصدر.')
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'هذه السجلات مقروءة من واجهة القواعد الإدارية. لم يثبت في مسار التشغيل الحالي أنها تدخل محرك منح النقاط، لذلك لا تُستخدم هنا لوصف قاعدة تنفيذية.',
                  style: TextStyle(color: UiPalette.textSoft, height: 1.5),
                ),
                const SizedBox(height: 12),
                ...rules.map(_registeredRule),
              ],
            ),
    );
  }

  Widget _buildReferralRules(BuildContext context, _LoyaltyRulesData data) {
    final evaluation = data.referralEvaluation!;
    if (evaluation.basePoints <= 0 || evaluation.referralLevels.isEmpty) {
      return const SizedBox.shrink();
    }
    return _section(
      context,
      icon: Icons.account_tree_outlined,
      title: 'قواعد الإحالات المرتبطة بالولاء',
      child: Column(
        children: [
          const Text(
            'تعرض النسب المستخرجة من محرك الإحالة عبر مصدر نقاط رسمي فعال.',
          ),
          const SizedBox(height: 12),
          ...evaluation.referralLevels.asMap().entries.map(
                (entry) => _valueLine(
                  'المستوى ${entry.key + 1}',
                  '${_percentage(entry.value / evaluation.basePoints)} من نقاط الأساس',
                ),
              ),
        ],
      ),
    );
  }

  Widget _registeredRule(LoyaltyRule rule) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  rule.ruleName.isEmpty
                      ? 'قاعدة مسجلة'
                      : RlUiText.translate(rule.ruleName,
                          fallback: rule.ruleName),
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
              Text(rule.isActive ? 'نشط' : 'معطل'),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'نوع القاعدة: ${RlUiText.translate(rule.ruleType)} | الأولوية: ${rule.priority}',
            style: TextStyle(color: UiPalette.textSoft, fontSize: 12),
          ),
          Text(
            'القيمة: ${_number(rule.pointsValue)} | الإنفاق: ${_number(rule.spendingAmount)} | المضاعف: ${_number(rule.multiplier)} | المكافأة: ${_number(rule.bonusPoints)}',
            style: TextStyle(color: UiPalette.textSoft, fontSize: 12),
          ),
          Text(
            'آخر تحديث: ${_formatDate(rule.updatedAt)}',
            style: TextStyle(color: UiPalette.textSoft, fontSize: 12),
          ),
        ],
      ),
    );
  }

  Widget _section(
    BuildContext context, {
    required IconData icon,
    required String title,
    required Widget child,
  }) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: UiPalette.primaryBlue),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                    color: UiPalette.textMain,
                    fontSize: 17,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          DefaultTextStyle(
            style: const TextStyle(color: UiPalette.textMain, height: 1.4),
            child: child,
          ),
        ],
      ),
    );
  }

  Widget _statusLine(
    BuildContext context,
    String label,
    String value,
    bool enabled,
  ) {
    final color = enabled ? UiPalette.primaryBlue : UiPalette.textSoft;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        children: [
          Icon(
            enabled ? Icons.check_circle_outline : Icons.remove_circle_outline,
            color: color,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(child: Text(label)),
          Text(
            value,
            style: TextStyle(color: color, fontWeight: FontWeight.w700),
          ),
        ],
      ),
    );
  }

  Widget _valueLine(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        children: [
          Expanded(child: Text(label)),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }

  Widget _sourceLine(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          const Icon(Icons.chevron_left,
              color: UiPalette.primaryBlue, size: 18),
          Expanded(child: Text(label)),
          Text(
            value,
            style: TextStyle(color: UiPalette.textSoft, fontSize: 12),
          ),
        ],
      ),
    );
  }

  static String _number(double value) {
    if (value == value.roundToDouble()) return value.toInt().toString();
    return value
        .toStringAsFixed(2)
        .replaceFirst(RegExp(r'0+$'), '')
        .replaceFirst(RegExp(r'\.$'), '');
  }

  static String _percentage(double value) {
    final percentage = value * 100;
    return '${_number(percentage)}%';
  }

  static String _formatDate(DateTime value) {
    final local = value.toLocal();
    final date = '${local.year.toString().padLeft(4, '0')}/'
        '${local.month.toString().padLeft(2, '0')}/'
        '${local.day.toString().padLeft(2, '0')}';
    final time = '${local.hour.toString().padLeft(2, '0')}:'
        '${local.minute.toString().padLeft(2, '0')}';
    return '$date $time';
  }
}

class _LoyaltyRulesData {
  const _LoyaltyRulesData({
    required this.program,
    required this.productSettings,
    required this.importedSettings,
    required this.registeredRules,
    required this.referralEvaluation,
  });

  final LoyaltyProgramSettings? program;
  final List<ProductLoyaltyPointSetting> productSettings;
  final List<ImportedProductLoyaltyPointSetting> importedSettings;
  final List<LoyaltyRule> registeredRules;
  final ({double basePoints, List<double> referralLevels})? referralEvaluation;

  int get activeEarningSources =>
      productSettings
          .where((item) => item.isConfigured && item.isSettingActive == true)
          .length +
      importedSettings
          .where((item) => item.isConfigured && item.isSettingActive == true)
          .length;

  DateTime? get latestUpdate {
    final dates = <DateTime>[
      if (program != null) program!.updatedAt,
      ...registeredRules.map((rule) => rule.updatedAt),
    ];
    if (dates.isEmpty) return null;
    return dates
        .reduce((first, second) => first.isAfter(second) ? first : second);
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(message),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة'),
          ),
        ],
      ),
    );
  }
}
