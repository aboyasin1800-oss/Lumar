import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/app_navigation.dart';
import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';
import 'imported_product_loyalty_point_settings_screen.dart';
import 'product_loyalty_point_settings_screen.dart';
import 'ready_made_product_loyalty_point_settings_screen.dart';

class PointsSettingsScreen extends StatefulWidget {
  const PointsSettingsScreen({super.key});

  @override
  State<PointsSettingsScreen> createState() => _PointsSettingsScreenState();
}

class _PointsSettingsScreenState extends State<PointsSettingsScreen> {
  final _repository = LoyaltyRepository();
  List<LoyaltyProgramSettings> _settings = const [];
  Object? _loadError;
  bool _loading = true;
  bool _saving = false;
  String? _savingSection;
  bool _editingRedemption = false;
  bool _editingLifecycle = false;
  bool _allowRedemptionDraft = true;
  bool _loyaltyFreezeEnabledDraft = false;
  bool _manualReactivationEnabledDraft = true;
  bool _purchaseReactivationEnabledDraft = true;
  final _minimumRedemptionController = TextEditingController();
  final _maximumRedemptionController = TextEditingController();
  final _gracePeriodController = TextEditingController();
  final _warningPeriodController = TextEditingController();
  String? _redemptionMessage;
  bool _redemptionMessageIsError = false;
  String? _lifecycleMessage;
  bool _lifecycleMessageIsError = false;

  @override
  void initState() {
    super.initState();
    unawaited(_loadSettings());
  }

  Future<void> _reload() async {
    await _loadSettings();
  }

  Future<void> _loadSettings() async {
    if (mounted) setState(() => _loading = true);
    try {
      final settings = await _repository.getProgramSettings();
      if (!mounted) return;
      if (settings.isNotEmpty) {
        _syncSectionDrafts(settings.first);
      }
      setState(() {
        _settings = settings;
        _loadError = null;
      });
    } catch (error, stackTrace) {
      debugPrint('Failed to load loyalty program settings: $error');
      debugPrintStack(stackTrace: stackTrace);
      if (mounted) setState(() => _loadError = error);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  void dispose() {
    _minimumRedemptionController.dispose();
    _maximumRedemptionController.dispose();
    _gracePeriodController.dispose();
    _warningPeriodController.dispose();
    super.dispose();
  }

  void _syncSectionDrafts(LoyaltyProgramSettings current) {
    if (!_editingRedemption) {
      _allowRedemptionDraft = current.allowRedemption;
      _minimumRedemptionController.text =
          _formatNumber(current.minimumRedemptionPoints);
      _maximumRedemptionController.text =
          _formatNumber(current.maximumRedemptionPoints);
    }
    if (!_editingLifecycle) {
      _loyaltyFreezeEnabledDraft = current.loyaltyFreezeEnabled;
      _gracePeriodController.text = current.gracePeriodDays.toString();
      _warningPeriodController.text = current.warningPeriodDays.toString();
      _manualReactivationEnabledDraft = current.manualReactivationEnabled;
      _purchaseReactivationEnabledDraft = current.purchaseReactivationEnabled;
    }
  }

  @override
  Widget build(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    final background =
        dark ? UiPalette.darkBackground : UiPalette.lightBackground;
    final surface = dark ? UiPalette.darkSurface : UiPalette.lightCard;
    final text = dark ? UiPalette.darkText : UiPalette.lightText;
    final muted = text.withValues(alpha: .7);

    return Scaffold(
      backgroundColor: background,
      appBar: AppBar(
        title: const Text('إعدادات النقاط'),
        backgroundColor: surface,
        foregroundColor: text,
        actions: [
          IconButton(
            onPressed: _saving ? null : _reload,
            icon: const Icon(Icons.refresh),
            tooltip: 'إعادة قراءة الإعدادات',
          ),
        ],
      ),
      body: _loading && _settings.isEmpty
          ? const Center(child: CircularProgressIndicator())
          : _settings.isEmpty && _loadError != null
              ? _ErrorView(
                  message: RlUiText.friendlyError(_loadError),
                  onRetry: _reload,
                  textColor: text,
                )
              : _settings.isEmpty
                  ? Center(
                      child: Text(
                          'لا توجد إعدادات برنامج نقاط في المصدر الحالي',
                          style: TextStyle(color: text)),
                    )
                  : RefreshIndicator(
                      onRefresh: _reload,
                      child: ListView(
                        padding: const EdgeInsets.all(20),
                        children: [
                          Text('إعدادات برنامج الولاء',
                              style: TextStyle(
                                  color: text,
                                  fontSize: 22,
                                  fontWeight: FontWeight.bold)),
                          const SizedBox(height: 6),
                          Text(
                              'تدار إعدادات البرنامج والاستبدال والنشاط والتجميد من المصدر الفعلي نفسه، مع إبقاء نقاط المنتجات في أقسامها الرسمية.',
                              style: TextStyle(color: muted)),
                          const SizedBox(height: 18),
                          _buildProgramCard(
                              context, _settings.first, surface, text, muted),
                          const SizedBox(height: 12),
                          _buildRedemptionCard(
                              _settings.first, surface, text, muted),
                          const SizedBox(height: 12),
                          _buildLifecycleCard(
                              _settings.first, surface, text, muted),
                          const SizedBox(height: 18),
                          _NavigationCard(
                            icon: Icons.checkroom_outlined,
                            title: 'نقاط المبيعات التفصيل',
                            description:
                                'تحديد نقاط مبيعات التفصيل لكل نوع منتج باستخدام المعرف الرسمي.',
                            onTap: () => AppNavigation.push(
                              context,
                              (_) => const ProductLoyaltyPointSettingsScreen(),
                            ),
                            textColor: text,
                            mutedColor: muted,
                            surface: surface,
                          ),
                          const SizedBox(height: 12),
                          _NavigationCard(
                            icon: Icons.storefront_outlined,
                            title: 'نقاط المبيعات الجاهزة من منتجاتنا',
                            description:
                                'تحديد نقاط المبيعات الجاهزة من منتجاتنا لكل نوع منتج باستخدام ProductTypeId.',
                            onTap: () => AppNavigation.push(
                              context,
                              (_) =>
                                  const ReadyMadeProductLoyaltyPointSettingsScreen(),
                            ),
                            textColor: text,
                            mutedColor: muted,
                            surface: surface,
                          ),
                          const SizedBox(height: 12),
                          _NavigationCard(
                            icon: Icons.inventory_2_outlined,
                            title: 'نقاط الأصناف المستوردة',
                            description:
                                'تحديد نقاط كل صنف مستورد باستخدام المعرف الرسمي للصنف.',
                            onTap: () => AppNavigation.push(
                              context,
                              (_) =>
                                  const ImportedProductLoyaltyPointSettingsScreen(),
                            ),
                            textColor: text,
                            mutedColor: muted,
                            surface: surface,
                          ),
                          const SizedBox(height: 18),
                          Card(
                            color: surface,
                            child: Padding(
                              padding: const EdgeInsets.all(16),
                              child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Icon(Icons.info_outline,
                                        color: UiPalette.primary),
                                    const SizedBox(width: 10),
                                    Expanded(
                                        child: Text(
                                            'القيمة صفر في الحد الأعلى تعني عدم وجود حد أعلى. التجميد يحفظ الرصيد ولا يصفره، ويمنع المكافآت والاستبدال حتى إعادة التنشيط.',
                                            style: TextStyle(color: muted))),
                                  ]),
                            ),
                          ),
                        ],
                      ),
                    ),
    );
  }

  Widget _buildProgramCard(BuildContext context, LoyaltyProgramSettings current,
      Color surface, Color text, Color muted) {
    return Card(
      color: surface,
      shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
          side: BorderSide(color: UiPalette.primaryBorder)),
      child: ExpansionTile(
        initiallyExpanded: true,
        leading: Icon(Icons.settings_outlined, color: UiPalette.primary),
        title: Text('إعدادات البرنامج العامة وقيمة النقطة',
            style: TextStyle(color: text, fontWeight: FontWeight.bold)),
        childrenPadding: const EdgeInsets.fromLTRB(18, 0, 18, 18),
        children: [
          _ValueRow(
              label: 'حالة البرنامج',
              value: current.isEnabled ? 'فعال' : 'غير فعال',
              textColor: text,
              mutedColor: muted),
          _ValueRow(
              label: 'النقاط لكل قطعة',
              value: _formatNumber(current.pointsPerPiece),
              textColor: text,
              mutedColor: muted),
          _ValueRow(
              label: 'قيمة النقطة الواحدة',
              value: _formatNumber(current.pointMonetaryValue),
              textColor: text,
              mutedColor: muted),
          _ValueRow(
              label: 'ساري من',
              value: DateFormat('yyyy-MM-dd HH:mm')
                  .format(current.effectiveFromUtc.toLocal()),
              textColor: text,
              mutedColor: muted),
          Align(
            alignment: Alignment.centerLeft,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Semantics(
                  label: 'تفعيل برنامج الولاء',
                  child: Switch(
                    value: current.isEnabled,
                    onChanged: _saving
                        ? null
                        : (value) => unawaited(
                              _setProgramEnabled(current, value),
                            ),
                    activeTrackColor: UiPalette.primary,
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  current.isEnabled ? 'البرنامج مفعل' : 'البرنامج معطل',
                  style: TextStyle(color: muted),
                ),
              ],
            ),
          ),
          const SizedBox(height: 10),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: FilledButton.icon(
              onPressed: _saving ? null : () => _openEditor(current),
              icon: const Icon(Icons.edit_outlined),
              label: const Text('تعديل إعدادات البرنامج'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildRedemptionCard(
      LoyaltyProgramSettings current, Color surface, Color text, Color muted) {
    return Card(
      color: surface,
      child: ExpansionTile(
        leading: Icon(Icons.redeem_outlined, color: UiPalette.primary),
        title: Text('حدود الاستبدال',
            style: TextStyle(color: text, fontWeight: FontWeight.bold)),
        childrenPadding: const EdgeInsets.fromLTRB(18, 0, 18, 18),
        children: [
          if (_editingRedemption)
            _buildRedemptionEditor(text, muted)
          else ...[
            _ValueRow(
                label: 'السماح بالاستبدال',
                value: current.allowRedemption ? 'مسموح' : 'موقوف',
                textColor: text,
                mutedColor: muted),
            _ValueRow(
                label: 'الحد الأدنى للعملية',
                value: _formatNumber(current.minimumRedemptionPoints),
                textColor: text,
                mutedColor: muted),
            _ValueRow(
                label: 'الحد الأعلى للعملية',
                value: current.maximumRedemptionPoints == 0
                    ? 'لا يوجد حد أعلى'
                    : _formatNumber(current.maximumRedemptionPoints),
                textColor: text,
                mutedColor: muted),
            _buildEditButton(
              label: 'تعديل حدود الاستبدال',
              onPressed: () => setState(() {
                _editingRedemption = true;
                _redemptionMessage = null;
                _syncSectionDrafts(current);
              }),
            ),
          ],
          if (_redemptionMessage != null)
            _SectionMessage(
              message: _redemptionMessage!,
              isError: _redemptionMessageIsError,
            ),
        ],
      ),
    );
  }

  Widget _buildRedemptionEditor(Color text, Color muted) => Column(
        children: [
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            title: Text('السماح بالاستبدال', style: TextStyle(color: text)),
            value: _allowRedemptionDraft,
            onChanged: _saving
                ? null
                : (value) => setState(() => _allowRedemptionDraft = value),
          ),
          TextField(
            controller: _minimumRedemptionController,
            enabled: !_saving,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: const InputDecoration(
              labelText: 'الحد الأدنى للاستبدال',
            ),
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _maximumRedemptionController,
            enabled: !_saving,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: const InputDecoration(
              labelText: 'الحد الأعلى للاستبدال (صفر = بلا حد)',
            ),
          ),
          const SizedBox(height: 14),
          _buildSectionActions(
            section: 'redemption',
            onCancel: () => setState(() => _editingRedemption = false),
            onSave: _saveRedemptionSettings,
            muted: muted,
          ),
        ],
      );

  Widget _buildLifecycleCard(
      LoyaltyProgramSettings current, Color surface, Color text, Color muted) {
    return Card(
      color: surface,
      child: ExpansionTile(
        leading: Icon(Icons.schedule_outlined, color: UiPalette.primary),
        title: Text('النشاط والإنذار والتجميد وإعادة التنشيط',
            style: TextStyle(color: text, fontWeight: FontWeight.bold)),
        childrenPadding: const EdgeInsets.fromLTRB(18, 0, 18, 18),
        children: [
          if (_editingLifecycle)
            _buildLifecycleEditor(text, muted)
          else ...[
            _ValueRow(
                label: 'نظام التجميد',
                value: current.loyaltyFreezeEnabled ? 'مفعل' : 'غير مفعل',
                textColor: text,
                mutedColor: muted),
            _ValueRow(
                label: 'فترة السماح',
                value: '${current.gracePeriodDays} يومًا من آخر شراء مؤهل',
                textColor: text,
                mutedColor: muted),
            _ValueRow(
                label: 'فترة الإنذار',
                value: '${current.warningPeriodDays} يومًا قبل التجميد',
                textColor: text,
                mutedColor: muted),
            _ValueRow(
                label: 'إعادة التنشيط اليدوي',
                value: current.manualReactivationEnabled ? 'مسموح' : 'موقوف',
                textColor: text,
                mutedColor: muted),
            _ValueRow(
                label: 'إعادة التنشيط عند شراء مؤهل',
                value: current.purchaseReactivationEnabled ? 'مسموح' : 'موقوف',
                textColor: text,
                mutedColor: muted),
            _buildEditButton(
              label: 'تعديل النشاط والتجميد وإعادة التنشيط',
              onPressed: () => setState(() {
                _editingLifecycle = true;
                _lifecycleMessage = null;
                _syncSectionDrafts(current);
              }),
            ),
          ],
          if (_lifecycleMessage != null)
            _SectionMessage(
              message: _lifecycleMessage!,
              isError: _lifecycleMessageIsError,
            ),
        ],
      ),
    );
  }

  Widget _buildLifecycleEditor(Color text, Color muted) => Column(
        children: [
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            title: Text('تفعيل نظام التجميد', style: TextStyle(color: text)),
            value: _loyaltyFreezeEnabledDraft,
            onChanged: _saving
                ? null
                : (value) => setState(() => _loyaltyFreezeEnabledDraft = value),
          ),
          TextField(
            controller: _gracePeriodController,
            enabled: !_saving,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(
              labelText: 'فترة السماح بالأيام',
            ),
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _warningPeriodController,
            enabled: !_saving,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(
              labelText: 'فترة الإنذار بالأيام',
              helperText: 'يجب أن تكون أقل من فترة السماح',
            ),
          ),
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            title: Text('السماح بإعادة التنشيط اليدوي',
                style: TextStyle(color: text)),
            value: _manualReactivationEnabledDraft,
            onChanged: _saving
                ? null
                : (value) =>
                    setState(() => _manualReactivationEnabledDraft = value),
          ),
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            title: Text('إعادة التنشيط عند شراء مؤهل',
                style: TextStyle(color: text)),
            value: _purchaseReactivationEnabledDraft,
            onChanged: _saving
                ? null
                : (value) =>
                    setState(() => _purchaseReactivationEnabledDraft = value),
          ),
          const SizedBox(height: 4),
          _buildSectionActions(
            section: 'lifecycle',
            onCancel: () => setState(() => _editingLifecycle = false),
            onSave: _saveLifecycleSettings,
            muted: muted,
          ),
        ],
      );

  Widget _buildEditButton(
          {required String label, required VoidCallback onPressed}) =>
      Align(
        alignment: AlignmentDirectional.centerEnd,
        child: OutlinedButton.icon(
          onPressed: _saving ? null : onPressed,
          icon: const Icon(Icons.edit_outlined),
          label: Text(label),
        ),
      );

  Widget _buildSectionActions({
    required String section,
    required VoidCallback onCancel,
    required VoidCallback onSave,
    required Color muted,
  }) =>
      Row(
        mainAxisAlignment: MainAxisAlignment.end,
        children: [
          TextButton(
              onPressed: _saving ? null : onCancel, child: const Text('إلغاء')),
          const SizedBox(width: 8),
          FilledButton.icon(
            onPressed: _saving ? null : onSave,
            icon: _savingSection == section
                ? const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.save_outlined),
            label: Text(_savingSection == section ? 'جارٍ الحفظ...' : 'حفظ'),
          ),
        ],
      );

  Future<void> _saveRedemptionSettings() async {
    final minimum = double.tryParse(_minimumRedemptionController.text.trim());
    final maximum = double.tryParse(_maximumRedemptionController.text.trim());
    if (minimum == null || minimum < 0) {
      _setRedemptionMessage('أدخل حدًا أدنى غير سالب.', isError: true);
      return;
    }
    if (maximum == null || maximum < 0 || (maximum != 0 && maximum < minimum)) {
      _setRedemptionMessage(
        'يجب أن يكون الحد الأعلى صفرًا أو أكبر من أو يساوي الحد الأدنى.',
        isError: true,
      );
      return;
    }
    final current = _settings.first;
    await _saveProgramSettings(
      current: current,
      isEnabled: current.isEnabled,
      pointsPerPiece: current.pointsPerPiece,
      pointMonetaryValue: current.pointMonetaryValue,
      allowRedemption: _allowRedemptionDraft,
      minimumRedemptionPoints: minimum,
      maximumRedemptionPoints: maximum,
      loyaltyFreezeEnabled: current.loyaltyFreezeEnabled,
      gracePeriodDays: current.gracePeriodDays,
      warningPeriodDays: current.warningPeriodDays,
      manualReactivationEnabled: current.manualReactivationEnabled,
      purchaseReactivationEnabled: current.purchaseReactivationEnabled,
      section: 'redemption',
      successMessage: 'تم حفظ حدود الاستبدال وإعادة قراءتها من الخادم.',
    );
  }

  Future<void> _saveLifecycleSettings() async {
    final grace = int.tryParse(_gracePeriodController.text.trim());
    final warning = int.tryParse(_warningPeriodController.text.trim());
    if (grace == null || grace <= 0) {
      _setLifecycleMessage('أدخل فترة سماح أكبر من صفر.', isError: true);
      return;
    }
    if (warning == null || warning < 0 || warning >= grace) {
      _setLifecycleMessage(
        'يجب أن تكون فترة الإنذار صفرًا أو أكبر، وأقل من فترة السماح.',
        isError: true,
      );
      return;
    }
    final current = _settings.first;
    await _saveProgramSettings(
      current: current,
      isEnabled: current.isEnabled,
      pointsPerPiece: current.pointsPerPiece,
      pointMonetaryValue: current.pointMonetaryValue,
      allowRedemption: current.allowRedemption,
      minimumRedemptionPoints: current.minimumRedemptionPoints,
      maximumRedemptionPoints: current.maximumRedemptionPoints,
      loyaltyFreezeEnabled: _loyaltyFreezeEnabledDraft,
      gracePeriodDays: grace,
      warningPeriodDays: warning,
      manualReactivationEnabled: _manualReactivationEnabledDraft,
      purchaseReactivationEnabled: _purchaseReactivationEnabledDraft,
      section: 'lifecycle',
      successMessage: 'تم حفظ إعدادات التجميد وإعادة قراءتها من الخادم.',
    );
  }

  void _setRedemptionMessage(String message, {required bool isError}) {
    if (!mounted) return;
    setState(() {
      _redemptionMessage = message;
      _redemptionMessageIsError = isError;
    });
  }

  void _setLifecycleMessage(String message, {required bool isError}) {
    if (!mounted) return;
    setState(() {
      _lifecycleMessage = message;
      _lifecycleMessageIsError = isError;
    });
  }

  Future<void> _openEditor(LoyaltyProgramSettings current) async {
    final result = await showDialog<_ProgramEditResult>(
      context: context,
      builder: (_) => _ProgramEditor(current: current),
    );
    if (result == null || !mounted) return;

    await _saveProgramSettings(
      current: current,
      isEnabled: current.isEnabled,
      pointsPerPiece: result.pointsPerPiece,
      pointMonetaryValue: result.pointMonetaryValue,
      allowRedemption: result.allowRedemption,
      minimumRedemptionPoints: result.minimumRedemptionPoints,
      maximumRedemptionPoints: result.maximumRedemptionPoints,
      loyaltyFreezeEnabled: result.loyaltyFreezeEnabled,
      gracePeriodDays: result.gracePeriodDays,
      warningPeriodDays: result.warningPeriodDays,
      manualReactivationEnabled: result.manualReactivationEnabled,
      purchaseReactivationEnabled: result.purchaseReactivationEnabled,
      section: 'program',
      successMessage: 'تم حفظ إعدادات برنامج الولاء بنجاح',
    );
  }

  Future<void> _setProgramEnabled(
      LoyaltyProgramSettings current, bool isEnabled) async {
    if (_saving || isEnabled == current.isEnabled) return;

    await _saveProgramSettings(
      current: current,
      isEnabled: isEnabled,
      pointsPerPiece: current.pointsPerPiece,
      pointMonetaryValue: current.pointMonetaryValue,
      allowRedemption: current.allowRedemption,
      minimumRedemptionPoints: current.minimumRedemptionPoints,
      maximumRedemptionPoints: current.maximumRedemptionPoints,
      loyaltyFreezeEnabled: current.loyaltyFreezeEnabled,
      gracePeriodDays: current.gracePeriodDays,
      warningPeriodDays: current.warningPeriodDays,
      manualReactivationEnabled: current.manualReactivationEnabled,
      purchaseReactivationEnabled: current.purchaseReactivationEnabled,
      section: 'program',
      successMessage:
          isEnabled ? 'تم تفعيل برنامج الولاء' : 'تم تعطيل برنامج الولاء',
    );
  }

  Future<void> _saveProgramSettings({
    required LoyaltyProgramSettings current,
    required bool isEnabled,
    required double pointsPerPiece,
    required double pointMonetaryValue,
    required bool allowRedemption,
    required double minimumRedemptionPoints,
    required double maximumRedemptionPoints,
    required bool loyaltyFreezeEnabled,
    required int gracePeriodDays,
    required int warningPeriodDays,
    required bool manualReactivationEnabled,
    required bool purchaseReactivationEnabled,
    required String section,
    required String successMessage,
  }) async {
    if (!mounted) return;
    setState(() {
      _saving = true;
      _savingSection = section;
      if (section == 'redemption') {
        _redemptionMessage = null;
      } else if (section == 'lifecycle') {
        _lifecycleMessage = null;
      }
    });
    try {
      final updated = await _repository.updateProgramSettings(
        id: current.loyaltyProgramSettingId,
        isEnabled: isEnabled,
        pointsPerPiece: pointsPerPiece,
        pointMonetaryValue: pointMonetaryValue,
        allowRedemption: allowRedemption,
        minimumRedemptionPoints: minimumRedemptionPoints,
        maximumRedemptionPoints: maximumRedemptionPoints,
        loyaltyFreezeEnabled: loyaltyFreezeEnabled,
        gracePeriodDays: gracePeriodDays,
        warningPeriodDays: warningPeriodDays,
        manualReactivationEnabled: manualReactivationEnabled,
        purchaseReactivationEnabled: purchaseReactivationEnabled,
        effectiveFromUtc: current.effectiveFromUtc,
      );
      if (!mounted) return;
      setState(() {
        _settings = [
          for (final setting in _settings)
            if (setting.loyaltyProgramSettingId !=
                updated.loyaltyProgramSettingId)
              setting,
          updated,
        ];
        _loadError = null;
        if (section == 'redemption') {
          _editingRedemption = false;
          _redemptionMessage = successMessage;
          _redemptionMessageIsError = false;
        } else if (section == 'lifecycle') {
          _editingLifecycle = false;
          _lifecycleMessage = successMessage;
          _lifecycleMessageIsError = false;
        }
      });
      try {
        await _reload();
      } catch (error, stackTrace) {
        debugPrint('Reload after saving point value failed: $error');
        debugPrintStack(stackTrace: stackTrace);
      }
    } catch (error) {
      if (!mounted) return;
      final message =
          'تعذر تحديث إعدادات برنامج الولاء: ${RlUiText.friendlyError(error)}';
      setState(() {
        if (section == 'redemption') {
          _redemptionMessage = message;
          _redemptionMessageIsError = true;
        } else if (section == 'lifecycle') {
          _lifecycleMessage = message;
          _lifecycleMessageIsError = true;
        }
      });
    } finally {
      if (mounted) {
        setState(() {
          _saving = false;
          _savingSection = null;
        });
      }
    }
  }
}

class _NavigationCard extends StatelessWidget {
  const _NavigationCard({
    required this.icon,
    required this.title,
    required this.description,
    required this.onTap,
    required this.textColor,
    required this.mutedColor,
    required this.surface,
  });

  final IconData icon;
  final String title;
  final String description;
  final VoidCallback onTap;
  final Color textColor;
  final Color mutedColor;
  final Color surface;

  @override
  Widget build(BuildContext context) => Card(
        color: surface,
        child: ListTile(
          leading: Icon(icon, color: UiPalette.primary),
          title: Text(title, style: TextStyle(color: textColor)),
          subtitle: Text(description, style: TextStyle(color: mutedColor)),
          trailing: const Icon(Icons.chevron_left),
          onTap: onTap,
        ),
      );
}

class _ProgramEditor extends StatefulWidget {
  const _ProgramEditor({required this.current});
  final LoyaltyProgramSettings current;

  @override
  State<_ProgramEditor> createState() => _ProgramEditorState();
}

class _ProgramEditorState extends State<_ProgramEditor> {
  late final TextEditingController _pointsController;
  late final TextEditingController _valueController;
  late final TextEditingController _minimumRedemptionController;
  late final TextEditingController _maximumRedemptionController;
  late final TextEditingController _gracePeriodController;
  late final TextEditingController _warningPeriodController;
  late bool _allowRedemption;
  late bool _loyaltyFreezeEnabled;
  late bool _manualReactivationEnabled;
  late bool _purchaseReactivationEnabled;
  final _formKey = GlobalKey<FormState>();

  @override
  void initState() {
    super.initState();
    _pointsController = TextEditingController(
        text: _formatNumber(widget.current.pointsPerPiece));
    _valueController = TextEditingController(
        text: _formatNumber(widget.current.pointMonetaryValue));
    _minimumRedemptionController = TextEditingController(
        text: _formatNumber(widget.current.minimumRedemptionPoints));
    _maximumRedemptionController = TextEditingController(
        text: _formatNumber(widget.current.maximumRedemptionPoints));
    _gracePeriodController =
        TextEditingController(text: widget.current.gracePeriodDays.toString());
    _warningPeriodController = TextEditingController(
        text: widget.current.warningPeriodDays.toString());
    _allowRedemption = widget.current.allowRedemption;
    _loyaltyFreezeEnabled = widget.current.loyaltyFreezeEnabled;
    _manualReactivationEnabled = widget.current.manualReactivationEnabled;
    _purchaseReactivationEnabled = widget.current.purchaseReactivationEnabled;
  }

  @override
  void dispose() {
    _pointsController.dispose();
    _valueController.dispose();
    _minimumRedemptionController.dispose();
    _maximumRedemptionController.dispose();
    _gracePeriodController.dispose();
    _warningPeriodController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('تعديل إعدادات برنامج الولاء'),
      content: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextFormField(
              controller: _pointsController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'النقاط لكل قطعة'),
              validator: _nonNegativeValidator,
            ),
            TextFormField(
              controller: _valueController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              textInputAction: TextInputAction.done,
              decoration:
                  const InputDecoration(labelText: 'قيمة النقطة الواحدة'),
              validator: _positiveValidator,
              onFieldSubmitted: (_) => _submit(),
            ),
            const SizedBox(height: 12),
            SwitchListTile.adaptive(
              contentPadding: EdgeInsets.zero,
              title: const Text('السماح باستبدال النقاط'),
              value: _allowRedemption,
              onChanged: (value) => setState(() => _allowRedemption = value),
            ),
            TextFormField(
              controller: _minimumRedemptionController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              textInputAction: TextInputAction.next,
              decoration:
                  const InputDecoration(labelText: 'الحد الأدنى للاستبدال'),
              validator: _nonNegativeValidator,
            ),
            TextFormField(
              controller: _maximumRedemptionController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(
                  labelText: 'الحد الأعلى للاستبدال (صفر = بلا حد)'),
              validator: _maximumValidator,
            ),
            const Divider(height: 24),
            SwitchListTile.adaptive(
              contentPadding: EdgeInsets.zero,
              title: const Text('تفعيل تجميد حسابات الولاء'),
              value: _loyaltyFreezeEnabled,
              onChanged: (value) =>
                  setState(() => _loyaltyFreezeEnabled = value),
            ),
            TextFormField(
              controller: _gracePeriodController,
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.next,
              decoration:
                  const InputDecoration(labelText: 'فترة السماح بالأيام'),
              validator: _positiveIntegerValidator,
            ),
            TextFormField(
              controller: _warningPeriodController,
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.next,
              decoration:
                  const InputDecoration(labelText: 'فترة الإنذار بالأيام'),
              validator: _warningValidator,
            ),
            SwitchListTile.adaptive(
              contentPadding: EdgeInsets.zero,
              title: const Text('السماح بإعادة التنشيط اليدوي'),
              value: _manualReactivationEnabled,
              onChanged: (value) =>
                  setState(() => _manualReactivationEnabled = value),
            ),
            SwitchListTile.adaptive(
              contentPadding: EdgeInsets.zero,
              title: const Text('إعادة التنشيط عند شراء مؤهل'),
              value: _purchaseReactivationEnabled,
              onChanged: (value) =>
                  setState(() => _purchaseReactivationEnabled = value),
            ),
          ]),
        ),
      ),
      actions: [
        TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('إلغاء')),
        FilledButton(onPressed: _submit, child: const Text('حفظ')),
      ],
    );
  }

  String? _maximumValidator(String? value) {
    final maximum = double.tryParse(value?.trim() ?? '');
    final minimum =
        double.tryParse(_minimumRedemptionController.text.trim()) ?? 0;
    if (maximum == null || maximum < 0) return 'أدخل صفرًا أو رقمًا غير سالب';
    if (maximum != 0 && maximum < minimum) {
      return 'يجب أن يساوي الحد الأعلى الحد الأدنى أو يتجاوزه';
    }
    return null;
  }

  String? _warningValidator(String? value) {
    final warning = int.tryParse(value?.trim() ?? '');
    final grace = int.tryParse(_gracePeriodController.text.trim()) ?? 0;
    if (warning == null || warning < 0) return 'أدخل صفرًا أو عدد أيام صحيحًا';
    if (warning >= grace) return 'يجب أن تكون فترة الإنذار أقل من فترة السماح';
    return null;
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context).pop(_ProgramEditResult(
      pointsPerPiece: double.parse(_pointsController.text.trim()),
      pointMonetaryValue: double.parse(_valueController.text.trim()),
      allowRedemption: _allowRedemption,
      minimumRedemptionPoints:
          double.parse(_minimumRedemptionController.text.trim()),
      maximumRedemptionPoints:
          double.parse(_maximumRedemptionController.text.trim()),
      loyaltyFreezeEnabled: _loyaltyFreezeEnabled,
      gracePeriodDays: int.parse(_gracePeriodController.text.trim()),
      warningPeriodDays: int.parse(_warningPeriodController.text.trim()),
      manualReactivationEnabled: _manualReactivationEnabled,
      purchaseReactivationEnabled: _purchaseReactivationEnabled,
    ));
  }
}

class _ProgramEditResult {
  const _ProgramEditResult(
      {required this.pointsPerPiece,
      required this.pointMonetaryValue,
      required this.allowRedemption,
      required this.minimumRedemptionPoints,
      required this.maximumRedemptionPoints,
      required this.loyaltyFreezeEnabled,
      required this.gracePeriodDays,
      required this.warningPeriodDays,
      required this.manualReactivationEnabled,
      required this.purchaseReactivationEnabled});
  final double pointsPerPiece;
  final double pointMonetaryValue;
  final bool allowRedemption;
  final double minimumRedemptionPoints;
  final double maximumRedemptionPoints;
  final bool loyaltyFreezeEnabled;
  final int gracePeriodDays;
  final int warningPeriodDays;
  final bool manualReactivationEnabled;
  final bool purchaseReactivationEnabled;
}

class _ValueRow extends StatelessWidget {
  const _ValueRow(
      {required this.label,
      required this.value,
      required this.textColor,
      required this.mutedColor});
  final String label;
  final String value;
  final Color textColor;
  final Color mutedColor;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Row(children: [
          Text('$label: ', style: TextStyle(color: mutedColor)),
          Expanded(
              child: Text(value,
                  style:
                      TextStyle(color: textColor, fontWeight: FontWeight.w600)))
        ]),
      );
}

class _SectionMessage extends StatelessWidget {
  const _SectionMessage({required this.message, required this.isError});

  final String message;
  final bool isError;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              isError ? Icons.error_outline : Icons.check_circle_outline,
              color: isError ? Colors.redAccent : UiPalette.primary,
              size: 20,
            ),
            const SizedBox(width: 8),
            Expanded(child: Text(message)),
          ],
        ),
      );
}

class _ErrorView extends StatelessWidget {
  const _ErrorView(
      {required this.message, required this.onRetry, required this.textColor});
  final String message;
  final Future<void> Function() onRetry;
  final Color textColor;

  @override
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text(message, style: TextStyle(color: textColor)),
        const SizedBox(height: 12),
        FilledButton(onPressed: onRetry, child: const Text('إعادة المحاولة'))
      ]));
}

String? _positiveValidator(String? value) {
  final number = double.tryParse(value?.trim() ?? '');
  return number == null || number <= 0 ? 'أدخل قيمة موجبة صحيحة' : null;
}

String? _nonNegativeValidator(String? value) {
  final number = double.tryParse(value?.trim() ?? '');
  return number == null || number < 0 ? 'أدخل رقمًا صحيحًا غير سالب' : null;
}

String? _positiveIntegerValidator(String? value) {
  final number = int.tryParse(value?.trim() ?? '');
  return number == null || number <= 0 ? 'أدخل عدد أيام موجبًا' : null;
}

String _formatNumber(num value) => value == value.roundToDouble()
    ? value.toInt().toString()
    : value
        .toStringAsFixed(4)
        .replaceFirst(RegExp(r'0+$'), '')
        .replaceFirst(RegExp(r'\.$'), '');
