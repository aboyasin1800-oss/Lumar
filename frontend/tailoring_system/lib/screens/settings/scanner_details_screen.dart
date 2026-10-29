import 'package:flutter/material.dart';

import '../../core/ui_palette.dart';
import 'scanner_management_screen.dart';

class ScannerDetailsScreen extends StatefulWidget {
  const ScannerDetailsScreen({super.key, required this.scanner});

  final ScannerRecord scanner;

  @override
  State<ScannerDetailsScreen> createState() => _ScannerDetailsScreenState();
}

class _ScannerDetailsScreenState extends State<ScannerDetailsScreen> {
  final repository = ScannerRepository();
  bool _loading = true;
  late ScannerRecord _scanner;
  List<ScannerPermissionRecord> _permissions = const <ScannerPermissionRecord>[];

  @override
  void initState() {
    super.initState();
    _scanner = widget.scanner;
    _load();
  }

  Future<void> _load() async {
    try {
      final permissions = await repository.getPermissions(_scanner.id);
      if (!mounted) return;
      setState(() {
        _permissions = permissions;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        title: const Text('تفاصيل الماسح'),
        actions: [
          IconButton(
            tooltip: _scanner.isActive ? 'إيقاف' : 'تفعيل',
            onPressed: () async {
              try {
                if (_scanner.isActive) {
                  final updated = await repository.deactivateScanner(_scanner.id);
                  if (!mounted) return;
                  setState(() => _scanner = updated);
                } else {
                  final updated = await repository.activateScanner(_scanner.id);
                  if (!mounted) return;
                  setState(() => _scanner = updated);
                }
              } on ScannerApiException catch (error) {
                if (!context.mounted) return;
                ScaffoldMessenger.maybeOf(context)?.showSnackBar(
                  SnackBar(
                    backgroundColor: Colors.red.shade700,
                    content: Text(error.message),
                  ),
                );
              }
            },
            icon: Icon(
              _scanner.isActive ? Icons.toggle_on_rounded : Icons.toggle_off_rounded,
            ),
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Card(
                    color: UiPalette.surfaceCard,
                    child: Padding(
                      padding: const EdgeInsets.all(18),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _detailRow('اسم الماسح', _scanner.scannerName),
                          _detailRow('كود الماسح', _scanner.scannerCode),
                          _detailRow('الوصف', _scanner.description ?? '—'),
                          _detailRow('الحالة', _scanner.isActive ? 'نشط' : 'معطل'),
                          _detailRow('الموظف المرتبط', _scanner.employeeName ?? 'بدون موظف'),
                          _detailRow('EmployeeCode', _scanner.employeeCode ?? '—'),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Card(
                    color: UiPalette.surfaceCard,
                    child: Padding(
                      padding: const EdgeInsets.all(18),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'صلاحيات الماسح',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 18,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          const SizedBox(height: 12),
                          if (_permissions.isEmpty)
                            const Text('لا توجد صلاحيات مسجلة لهذا الماسح.')
                          else ...[
                            for (final permission in _permissions)
                              Container(
                                width: double.infinity,
                                margin: const EdgeInsets.only(bottom: 8),
                                padding: const EdgeInsets.all(12),
                                decoration: BoxDecoration(
                                  color: UiPalette.screenBackground,
                                  borderRadius: BorderRadius.circular(12),
                                  border: Border.all(color: UiPalette.borderSoft),
                                ),
                                child: Text(
                                  '${permission.productTypeId} • ${localizedScannerStage(permission.stage)}',
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.screenBackground,
                                    fontSize: 14,
                                  ),
                                ),
                              ),
                          ],
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
    );
  }

  Widget _detailRow(String label, String value) => Padding(
        padding: const EdgeInsets.only(bottom: 10),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 120,
              child: Text(
                label,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            Expanded(
              child: Text(
                value,
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.surfaceCard,
                  fontSize: 13,
                ),
              ),
            ),
          ],
        ),
      );
}
