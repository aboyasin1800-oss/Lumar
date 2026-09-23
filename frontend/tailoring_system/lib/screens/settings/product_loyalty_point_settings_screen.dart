import 'package:flutter/material.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

enum ProductLoyaltyPointSettingsMode { tailoring, readyMade }

class ProductLoyaltyPointSettingsScreen extends StatefulWidget {
  const ProductLoyaltyPointSettingsScreen({
    this.mode = ProductLoyaltyPointSettingsMode.tailoring,
    super.key,
  });

  final ProductLoyaltyPointSettingsMode mode;

  @override
  State<ProductLoyaltyPointSettingsScreen> createState() =>
      _ProductLoyaltyPointSettingsScreenState();
}

class _ProductLoyaltyPointSettingsScreenState
    extends State<ProductLoyaltyPointSettingsScreen> {
  final _repository = LoyaltyRepository();
  List<ProductLoyaltyPointSetting> _items = const [];
  Object? _loadError;
  bool _loading = true;
  final Set<int> _savingIds = <int>{};

  @override
  void initState() {
    super.initState();
    debugPrint('[Loyalty UI] product screen init: requesting initial list');
    _loadInitial();
  }

  Future<List<ProductLoyaltyPointSetting>> _fetchList() async {
    debugPrint('[Loyalty UI] product reload: start GET');
    final items = widget.mode == ProductLoyaltyPointSettingsMode.readyMade
        ? await _repository.getReadyMadeProductLoyaltyPointSettings()
        : await _repository.getProductLoyaltyPointSettings();
    final pants = items.where((item) => item.productTypeId == 7).toList();
    debugPrint(
        '[Loyalty UI] product reload: GET parsed count=${items.length}, pants=${pants.length}');
    for (final item in pants) {
      debugPrint(
          '[Loyalty UI] product reload before setState: ProductTypeId=${item.productTypeId}, settingId=${item.settingId}, points=${item.points}, isActive=${item.isSettingActive}, hasSetting=${item.isConfigured}');
    }
    return items;
  }

  Future<ProductLoyaltyPointSetting> _updateSetting({
    required int productTypeId,
    required double points,
    required bool isActive,
  }) {
    return widget.mode == ProductLoyaltyPointSettingsMode.readyMade
        ? _repository.updateReadyMadeProductLoyaltyPointSetting(
            productTypeId: productTypeId,
            points: points,
            isActive: isActive,
          )
        : _repository.updateProductLoyaltyPointSetting(
            productTypeId: productTypeId,
            points: points,
            isActive: isActive,
          );
  }

  Future<void> _loadInitial() async {
    try {
      final items = await _fetchList();
      if (!mounted) return;
      setState(() {
        _items = items;
        _loadError = null;
        _loading = false;
      });
      debugPrint('[Loyalty UI] product initial state updated');
    } catch (error, stackTrace) {
      debugPrint('[Loyalty UI] product initial load failed: $error');
      debugPrintStack(stackTrace: stackTrace);
      if (mounted) {
        setState(() {
          _loadError = error;
          _loading = false;
        });
      }
    }
  }

  Future<void> _reload() async {
    final items = await _fetchList();
    if (!mounted) {
      debugPrint('[Loyalty UI] product reload: skipped state update; unmounted');
      return;
    }
    try {
      setState(() {
        _items = items;
        _loadError = null;
      });
      debugPrint(
          '[Loyalty UI] product reload: state replaced successfully; count=${items.length}');
    } catch (error, stackTrace) {
      debugPrint('[Loyalty UI] product reload: setState failed: $error');
      debugPrintStack(stackTrace: stackTrace);
      rethrow;
    }
  }

  bool _isSaving(int productTypeId) => _savingIds.contains(productTypeId);

  void _setSaving(int productTypeId, bool saving) {
    if (!mounted) return;
    setState(() {
      if (saving) {
        _savingIds.add(productTypeId);
      } else {
        _savingIds.remove(productTypeId);
      }
    });
  }

  Future<void> _edit(ProductLoyaltyPointSetting item) async {
    final result = await showDialog<_PointEditResult>(
      context: context,
      builder: (_) => _PointEditor(
        title: item.nameAr,
        points: item.points ?? 0,
        isActive: item.isSettingActive ?? true,
      ),
    );
    if (result == null || !mounted) return;
    if (_isSaving(item.productTypeId)) return;
    _setSaving(item.productTypeId, true);
    try {
      await _updateSetting(
        productTypeId: item.productTypeId,
        points: result.points,
        isActive: result.isActive,
      );
      try {
        await _reload();
      } catch (error, stackTrace) {
        debugPrint('[Loyalty UI] product settings reload failed: $error');
        debugPrintStack(stackTrace: stackTrace);
        if (mounted) {
          _message('تم حفظ الحالة، لكن تعذر تحديث القائمة. حاول إعادة القراءة.',
              error: true);
        }
        return;
      }
      if (mounted) _message('تم حفظ نقاط نوع المنتج وإعادة قراءتها بنجاح');
    } catch (error, stackTrace) {
      debugPrint('[Loyalty UI] product settings save failed: $error');
      debugPrintStack(stackTrace: stackTrace);
      if (mounted) _message('تعذر حفظ إعداد نقاط النوع.', error: true);
    } finally {
      _setSaving(item.productTypeId, false);
    }
  }

  Future<void> _saveState(
      ProductLoyaltyPointSetting item, bool isActive) async {
    if (_isSaving(item.productTypeId)) return;
    _setSaving(item.productTypeId, true);
    try {
      await _updateSetting(
        productTypeId: item.productTypeId,
        points: item.points ?? 0,
        isActive: isActive,
      );
      try {
        await _reload();
      } catch (error, stackTrace) {
        debugPrint('[Loyalty UI] product state reload failed: $error');
        debugPrintStack(stackTrace: stackTrace);
        if (mounted) {
          _message('تم حفظ الحالة، لكن تعذر تحديث القائمة. حاول إعادة القراءة.',
              error: true);
        }
        return;
      }
      if (mounted) {
        _message(isActive
            ? 'تم تفعيل نقاط النوع بنجاح.'
            : 'تم تعطيل نقاط النوع بنجاح.');
      }
    } catch (error, stackTrace) {
      debugPrint('[Loyalty UI] product state save failed: $error');
      debugPrintStack(stackTrace: stackTrace);
      if (mounted) _message('تعذر حفظ حالة النوع.', error: true);
    } finally {
      _setSaving(item.productTypeId, false);
    }
  }

  void _message(String message, {bool error = false}) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      backgroundColor: error ? Colors.red.shade700 : null,
      content: Text(message),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final text = theme.colorScheme.onSurface;
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.mode == ProductLoyaltyPointSettingsMode.readyMade
            ? 'نقاط المبيعات الجاهزة من منتجاتنا'
            : 'نقاط المبيعات التفصيل'),
        actions: [
          IconButton(
            onPressed: _savingIds.isNotEmpty ? null : _reload,
            icon: const Icon(Icons.refresh),
            tooltip: 'إعادة القراءة',
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
              ? Center(
                  child: Text(RlUiText.friendlyError(_loadError),
                      style: TextStyle(color: text)),
                )
              : Builder(builder: (context) {
                  final items = _items;
          final pants = items.where((item) => item.productTypeId == 7).toList();
          for (final item in pants) {
            debugPrint(
                '[Loyalty UI] product FutureBuilder before list: ProductTypeId=${item.productTypeId}, settingId=${item.settingId}, points=${item.points}, isActive=${item.isSettingActive}, hasSetting=${item.isConfigured}');
          }
          if (items.isEmpty) {
            return Center(
              child: Text('لا توجد أنواع منتجات فعالة',
                  style: TextStyle(color: text)),
            );
          }
                  return RefreshIndicator(
                    onRefresh: _reload,
                    child: ListView.separated(
                      padding: const EdgeInsets.all(20),
                      itemCount: items.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (_, index) => _ProductTile(
                        item: items[index],
                        isSaving: _isSaving(items[index].productTypeId),
                        onEdit: _isSaving(items[index].productTypeId)
                            ? null
                            : () => _edit(items[index]),
                        onToggle: _isSaving(items[index].productTypeId)
                            ? null
                            : (value) => _saveState(items[index], value),
                      ),
                    ),
                  );
                }),
    );
  }
}

class _ProductTile extends StatelessWidget {
  const _ProductTile(
      {required this.item,
      required this.isSaving,
      required this.onEdit,
      required this.onToggle});
  final ProductLoyaltyPointSetting item;
  final bool isSaving;
  final VoidCallback? onEdit;
  final ValueChanged<bool>? onToggle;

  @override
  Widget build(BuildContext context) {
    final configured = item.isConfigured && item.isSettingActive == true;
    final pointsText = item.isConfigured
        ? '${item.points ?? 0} نقطة${item.isSettingActive == true ? '' : ' (غير فعال)'}'
        : 'يحتاج تحديد نقاط';
    if (item.productTypeId == 7) {
      debugPrint(
          '[Loyalty UI] product tile build: ProductTypeId=${item.productTypeId}, settingId=${item.settingId}, points=${item.points}, isActive=${item.isSettingActive}, hasSetting=${item.isConfigured}, configured=$configured');
    }
    final surface = Theme.of(context).colorScheme.surface;
    return Card(
      color: surface,
      child: ListTile(
        title: Text(item.nameAr),
        leading: Icon(
          configured
              ? Icons.check_circle_outline
              : Icons.warning_amber_outlined,
          color: configured ? UiPalette.primary : Colors.orange,
        ),
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            isSaving
                ? const SizedBox(
                    width: 24,
                    height: 24,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Switch(
                    value: item.isSettingActive == true,
                    onChanged: item.isConfigured ? onToggle : null,
                  ),
            IconButton(
              onPressed: onEdit,
              icon: const Icon(Icons.edit_outlined),
              tooltip: 'تعديل النقاط',
            ),
          ],
        ),
        isThreeLine: true,
        subtitle: Text(
          '${item.code}  |  ProductTypeId: ${item.productTypeId}\n'
          '$pointsText',
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }
}

class _PointEditResult {
  const _PointEditResult(this.points, this.isActive);
  final double points;
  final bool isActive;
}

class _PointEditor extends StatefulWidget {
  const _PointEditor(
      {required this.title, required this.points, required this.isActive});
  final String title;
  final double points;
  final bool isActive;

  @override
  State<_PointEditor> createState() => _PointEditorState();
}

class _PointEditorState extends State<_PointEditor> {
  late final TextEditingController _controller;
  late bool _active;
  final _formKey = GlobalKey<FormState>();

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.points.toString());
    _active = widget.isActive;
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        title: Text('نقاط ${widget.title}'),
        content: Form(
          key: _formKey,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('الإعداد فعال'),
              value: _active,
              onChanged: (value) => setState(() => _active = value),
            ),
            TextFormField(
              controller: _controller,
              autofocus: true,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              textInputAction: TextInputAction.done,
              decoration: const InputDecoration(labelText: 'النقاط'),
              validator: (value) {
                final parsed = double.tryParse(value?.trim() ?? '');
                return parsed == null || parsed < 0
                    ? 'أدخل رقمًا غير سالب'
                    : null;
              },
              onFieldSubmitted: (_) => _submit(),
            ),
          ]),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('إلغاء')),
          FilledButton(onPressed: _submit, child: const Text('حفظ')),
        ],
      );

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.of(context)
        .pop(_PointEditResult(double.parse(_controller.text.trim()), _active));
  }
}
