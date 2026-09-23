import 'package:flutter/material.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/loyalty_models.dart';
import '../../repositories/loyalty_repository.dart';

class ImportedProductLoyaltyPointSettingsScreen extends StatefulWidget {
  const ImportedProductLoyaltyPointSettingsScreen({super.key});

  @override
  State<ImportedProductLoyaltyPointSettingsScreen> createState() =>
      _ImportedProductLoyaltyPointSettingsScreenState();
}

class _ImportedProductLoyaltyPointSettingsScreenState
    extends State<ImportedProductLoyaltyPointSettingsScreen> {
  final _repository = LoyaltyRepository();
  List<ImportedProductLoyaltyPointSetting> _items = const [];
  Object? _loadError;
  bool _loading = true;
  final Set<int> _savingIds = <int>{};

  @override
  void initState() {
    super.initState();
    debugPrint('[Loyalty UI] imported screen init: requesting initial list');
    _loadInitial();
  }

  Future<List<ImportedProductLoyaltyPointSetting>> _fetchList() async {
    debugPrint('[Loyalty UI] imported reload: start GET');
    final items = await _repository.getImportedProductLoyaltyPointSettings();
    debugPrint('[Loyalty UI] imported reload: GET parsed count=${items.length}');
    return items;
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
      debugPrint('[Loyalty UI] imported initial state updated');
    } catch (error, stackTrace) {
      debugPrint('[Loyalty UI] imported initial load failed: $error');
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
      debugPrint('[Loyalty UI] imported reload: skipped state update; unmounted');
      return;
    }
    setState(() {
      _items = items;
      _loadError = null;
    });
    debugPrint(
        '[Loyalty UI] imported reload: state replaced successfully; count=${items.length}');
  }

  bool _isSaving(int productId) => _savingIds.contains(productId);

  void _setSaving(int productId, bool saving) {
    if (!mounted) return;
    setState(() {
      if (saving) {
        _savingIds.add(productId);
      } else {
        _savingIds.remove(productId);
      }
    });
  }

  Future<void> _edit(ImportedProductLoyaltyPointSetting item) async {
    final result = await showDialog<_ImportedPointEditResult>(
      context: context,
      builder: (_) => _ImportedPointEditor(
        title: item.productName,
        points: item.points ?? 0,
        isActive: item.isSettingActive ?? true,
      ),
    );
    if (result == null || !mounted) return;
    if (_isSaving(item.importedReadyMadeProductId)) return;
    _setSaving(item.importedReadyMadeProductId, true);
    try {
      await _repository.updateImportedProductLoyaltyPointSetting(
        importedReadyMadeProductId: item.importedReadyMadeProductId,
        points: result.points,
        isActive: result.isActive,
      );
      try {
        await _reload();
      } catch (error, stackTrace) {
        debugPrint('[Loyalty UI] imported settings reload failed: $error');
        debugPrintStack(stackTrace: stackTrace);
        if (mounted) {
          _message('تم حفظ الحالة، لكن تعذر تحديث القائمة. حاول إعادة القراءة.',
              error: true);
        }
        return;
      }
      if (mounted) _message('تم حفظ نقاط الصنف المستورد وإعادة قراءتها بنجاح');
    } catch (error) {
      debugPrint('[Loyalty UI] imported settings save failed: $error');
      if (mounted) _message('تعذر حفظ إعداد نقاط الصنف المستورد.', error: true);
    } finally {
      _setSaving(item.importedReadyMadeProductId, false);
    }
  }

  Future<void> _saveState(
      ImportedProductLoyaltyPointSetting item, bool isActive) async {
    if (_isSaving(item.importedReadyMadeProductId)) return;
    _setSaving(item.importedReadyMadeProductId, true);
    try {
      await _repository.updateImportedProductLoyaltyPointSetting(
        importedReadyMadeProductId: item.importedReadyMadeProductId,
        points: item.points ?? 0,
        isActive: isActive,
      );
      try {
        await _reload();
      } catch (error, stackTrace) {
        debugPrint('[Loyalty UI] imported state reload failed: $error');
        debugPrintStack(stackTrace: stackTrace);
        if (mounted) {
          _message('تم حفظ الحالة، لكن تعذر تحديث القائمة. حاول إعادة القراءة.',
              error: true);
        }
        return;
      }
      if (mounted) {
        _message(isActive
            ? 'تم تفعيل نقاط الصنف المستورد بنجاح.'
            : 'تم تعطيل نقاط الصنف المستورد بنجاح.');
      }
    } catch (error) {
      debugPrint('[Loyalty UI] imported state save failed: $error');
      if (mounted) _message('تعذر حفظ حالة الصنف المستورد.', error: true);
    } finally {
      _setSaving(item.importedReadyMadeProductId, false);
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
    final text = Theme.of(context).colorScheme.onSurface;
    return Scaffold(
      appBar: AppBar(
        title: const Text('نقاط الأصناف المستوردة'),
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
          for (final item in items) {
            debugPrint(
                '[Loyalty UI] imported FutureBuilder before list: id=${item.importedReadyMadeProductId}, settingId=${item.settingId}, points=${item.points}, isActive=${item.isSettingActive}, hasSetting=${item.isConfigured}');
          }
          if (items.isEmpty) {
            return Center(
              child: Text('لا توجد أصناف مستوردة فعالة',
                  style: TextStyle(color: text)),
            );
          }
                  return RefreshIndicator(
                    onRefresh: _reload,
                    child: ListView.separated(
                      padding: const EdgeInsets.all(20),
                      itemCount: items.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (_, index) => _ImportedTile(
                        item: items[index],
                        isSaving: _isSaving(
                            items[index].importedReadyMadeProductId),
                        onEdit: _isSaving(
                                items[index].importedReadyMadeProductId)
                            ? null
                            : () => _edit(items[index]),
                        onToggle: _isSaving(
                                items[index].importedReadyMadeProductId)
                            ? null
                            : (value) => _saveState(items[index], value),
                      ),
                    ),
                  );
                }),
    );
  }
}

class _ImportedTile extends StatelessWidget {
  const _ImportedTile(
      {required this.item,
      required this.isSaving,
      required this.onEdit,
      required this.onToggle});
  final ImportedProductLoyaltyPointSetting item;
  final bool isSaving;
  final VoidCallback? onEdit;
  final ValueChanged<bool>? onToggle;

  @override
  Widget build(BuildContext context) {
    final configured = item.isConfigured && item.isSettingActive == true;
    final pointsText = item.isConfigured
        ? '${item.points ?? 0} نقطة${item.isSettingActive == true ? '' : ' (غير فعال)'}'
        : 'يحتاج تحديد نقاط';
    return Card(
      child: ListTile(
        title: Text(item.productName),
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
          '${item.productType}  |  ${item.productCode}  |  '
          'ImportedReadyMadeProductId: ${item.importedReadyMadeProductId}\n'
          '$pointsText',
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }
}

class _ImportedPointEditResult {
  const _ImportedPointEditResult(this.points, this.isActive);
  final double points;
  final bool isActive;
}

class _ImportedPointEditor extends StatefulWidget {
  const _ImportedPointEditor(
      {required this.title, required this.points, required this.isActive});
  final String title;
  final double points;
  final bool isActive;

  @override
  State<_ImportedPointEditor> createState() => _ImportedPointEditorState();
}

class _ImportedPointEditorState extends State<_ImportedPointEditor> {
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
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
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
            ],
          ),
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
    Navigator.of(context).pop(_ImportedPointEditResult(
        double.parse(_controller.text.trim()), _active));
  }
}
