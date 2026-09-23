import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/ui_palette.dart';
import '../../models/payroll_models.dart';
import '../../repositories/payroll_repository.dart';

class PieceRateScreen extends StatefulWidget {
  const PieceRateScreen({super.key, this.repository});

  final PayrollRepository? repository;

  @override
  State<PieceRateScreen> createState() => _PieceRateScreenState();
}

class _PieceRateScreenState extends State<PieceRateScreen> {
  late final PayrollRepository _repository;
  late Future<List<PieceWageRate>> _future;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? PayrollRepository();
    _future = _repository.getPieceWageRates();
  }

  void _reload() => setState(() => _future = _repository.getPieceWageRates());

  Future<void> _create() async {
    final result = await showDialog<_RateDraft>(
      context: context,
      builder: (_) => const _RateEditorDialog(),
    );
    if (result == null || !mounted) return;
    try {
      await _repository.createPieceWageRate(
        pieceType: result.pieceType,
        stage: result.stage,
        wageRate: result.wageRate,
        isActive: result.isActive,
        notes: result.notes,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم حفظ سعر القطعة بنجاح.')),
        );
      }
      _reload();
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تعذر حفظ سعر القطعة: $error')),
        );
      }
    }
  }

  Future<void> _edit(PieceWageRate rate) async {
    final result = await showDialog<_RateDraft>(
      context: context,
      builder: (_) => _RateEditorDialog(initial: rate),
    );
    if (result == null || !mounted) return;
    try {
      await _repository.updatePieceWageRate(
        rate.id,
        pieceType: result.pieceType,
        stage: result.stage,
        wageRate: result.wageRate,
        isActive: result.isActive,
        notes: result.notes,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم تحديث سعر القطعة بنجاح.')),
        );
      }
      _reload();
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تعذر تحديث سعر القطعة: $error')),
        );
      }
    }
  }

  Future<void> _delete(PieceWageRate rate) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        backgroundColor: UiPalette.surfaceCard,
        title: const Text('حذف سعر القطعة'),
        content: Text('هل تريد حذف ${rate.pieceType} • ${rate.stage}؟'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('إلغاء')),
          FilledButton.icon(
            onPressed: () => Navigator.pop(context, true),
            icon: const Icon(Icons.delete_outline_rounded),
            label: const Text('حذف'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await _repository.deletePieceWageRate(rate.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('تم حذف سعر القطعة.')),
        );
      }
      _reload();
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('تعذر حذف سعر القطعة: $error')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.textMain,
          title: const Text('أسعار القطعة'),
          actions: [
            IconButton(
              tooltip: 'إضافة سعر جديد',
              onPressed: _create,
              icon: const Icon(Icons.add_circle_outline_rounded),
            ),
            IconButton(
              tooltip: 'تحديث',
              onPressed: _reload,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: FutureBuilder<List<PieceWageRate>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.cloud_off_outlined, size: 42),
                    const SizedBox(height: 12),
                    Text('تعذر تحميل أسعار القطعة:\n${snapshot.error}'),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _reload,
                      icon: const Icon(Icons.refresh),
                      label: const Text('إعادة المحاولة'),
                    ),
                  ],
                ),
              );
            }

            final rates = snapshot.data ?? const <PieceWageRate>[];
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  color: UiPalette.surfaceCard,
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        const Icon(Icons.attach_money_outlined, color: UiPalette.primaryBlue),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('أسعار القطعة', style: Theme.of(context).textTheme.titleMedium?.copyWith(color: UiPalette.textMain)),
                              Text('${rates.length} سعر مسجل • ${rates.where((item) => item.isActive).length} نشط', style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: UiPalette.textSoft)),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                if (rates.isEmpty)
                  const Center(child: Text('لا توجد أسعار مسجلة حاليًا.'))
                else
                  ...rates.map((item) => Card(
                        color: UiPalette.surfaceCard,
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          leading: Icon(item.isActive ? Icons.check_circle_outline_rounded : Icons.pause_circle_outline_rounded, color: item.isActive ? UiPalette.primaryBlue : UiPalette.textSoft),
                          title: Text('${item.pieceType} • ${item.stage}', style: TextStyle(color: UiPalette.textMain)),
                          subtitle: item.notes == null || item.notes!.trim().isEmpty
                              ? Text(item.isActive ? 'نشط' : 'متوقف', style: TextStyle(color: UiPalette.textSoft))
                              : Text(item.notes!, style: TextStyle(color: UiPalette.textSoft)),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text('${NumberFormat('#,##0.00').format(item.wageRate)} ر.س', style: TextStyle(color: item.wageRate == 0 ? UiPalette.primaryBlue : UiPalette.textMain)),
                              const SizedBox(width: 8),
                              IconButton(onPressed: () => _edit(item), icon: const Icon(Icons.edit_outlined), tooltip: 'تعديل'),
                              IconButton(onPressed: () => _delete(item), icon: const Icon(Icons.delete_outline_rounded), tooltip: 'حذف'),
                            ],
                          ),
                        ),
                      )),
              ],
            );
          },
        ),
      );
}

class _RateDraft {
  const _RateDraft({required this.pieceType, required this.stage, required this.wageRate, required this.isActive, this.notes});
  final String pieceType;
  final String stage;
  final double wageRate;
  final bool isActive;
  final String? notes;
}

class _RateEditorDialog extends StatefulWidget {
  const _RateEditorDialog({this.initial});

  final PieceWageRate? initial;

  @override
  State<_RateEditorDialog> createState() => _RateEditorDialogState();
}

class _RateEditorDialogState extends State<_RateEditorDialog> {
  final _pieceTypeController = TextEditingController();
  final _stageController = TextEditingController();
  final _rateController = TextEditingController();
  final _notesController = TextEditingController();
  bool _isActive = true;

  @override
  void initState() {
    super.initState();
    final initial = widget.initial;
    if (initial != null) {
      _pieceTypeController.text = initial.pieceType;
      _stageController.text = initial.stage;
      _rateController.text = initial.wageRate.toStringAsFixed(2);
      _notesController.text = initial.notes ?? '';
      _isActive = initial.isActive;
    }
  }

  @override
  void dispose() {
    _pieceTypeController.dispose();
    _stageController.dispose();
    _rateController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  void _save() {
    final pieceType = _pieceTypeController.text.trim();
    final stage = _stageController.text.trim();
    final wageRate = double.tryParse(_rateController.text.trim());
    if (pieceType.isEmpty || stage.isEmpty || wageRate == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('أدخل نوع القطعة، المرحلة، وسعرًا صحيحًا.')),
      );
      return;
    }
    if (wageRate < 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('السعر لا يمكن أن يكون سالبًا، والقيمة صفر مسموح بها للمرحلة غير المدفوعة.')),
      );
      return;
    }
    Navigator.pop(
      context,
      _RateDraft(
        pieceType: pieceType,
        stage: stage,
        wageRate: wageRate,
        isActive: _isActive,
        notes: _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
        backgroundColor: UiPalette.surfaceCard,
        title: Text(widget.initial == null ? 'إضافة سعر جديد' : 'تعديل سعر القطعة'),
        content: SizedBox(
          width: 420,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: _pieceTypeController,
                  decoration: const InputDecoration(labelText: 'نوع القطعة'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _stageController,
                  decoration: const InputDecoration(labelText: 'المرحلة'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _rateController,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  decoration: const InputDecoration(labelText: 'السعر (ر.س)'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _notesController,
                  maxLines: 3,
                  decoration: const InputDecoration(labelText: 'ملاحظات (اختياري)'),
                ),
                const SizedBox(height: 12),
                CheckboxListTile(
                  value: _isActive,
                  onChanged: (value) => setState(() => _isActive = value ?? true),
                  title: const Text('مفعل'),
                  controlAffinity: ListTileControlAffinity.leading,
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
          FilledButton.icon(
            onPressed: _save,
            icon: const Icon(Icons.save_outlined),
            label: const Text('حفظ'),
          ),
        ],
      );
}
