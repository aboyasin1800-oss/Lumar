import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';

class ScannerRecord {
  const ScannerRecord({
    required this.id,
    required this.scannerCode,
    required this.scannerName,
    required this.description,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  factory ScannerRecord.fromJson(Map<String, dynamic> json) => ScannerRecord(
        id: (json['scannerId'] ?? json['ScannerId'] ?? 0) as int,
        scannerCode: (json['scannerCode'] ?? json['ScannerCode'] ?? '').toString(),
        scannerName: (json['scannerName'] ?? json['ScannerName'] ?? '').toString(),
        description: (json['description'] ?? json['Description'])?.toString(),
        isActive: (json['isActive'] ?? json['IsActive'] ?? false) as bool,
        createdAt: DateTime.tryParse(
              (json['createdAt'] ?? json['CreatedAt'] ?? '').toString(),
            ) ??
            DateTime.now(),
        updatedAt: DateTime.tryParse(
              (json['updatedAt'] ?? json['UpdatedAt'] ?? '').toString(),
            ),
      );

  final int id;
  final String scannerCode;
  final String scannerName;
  final String? description;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;
}

class ScannerWritePayload {
  const ScannerWritePayload({
    required this.scannerCode,
    required this.scannerName,
    required this.description,
    required this.isActive,
  });

  final String scannerCode;
  final String scannerName;
  final String? description;
  final bool isActive;

  Map<String, dynamic> toJson() => {
        'scannerCode': scannerCode.trim(),
        'scannerName': scannerName.trim(),
        'description': description?.trim().isNotEmpty == true
            ? description!.trim()
            : null,
        'isActive': isActive,
      };
}

class ScannerRepository {
  ScannerRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<List<ScannerRecord>> getScanners() async {
    final response = await _client.get(Uri.parse('$_baseUrl/production/scanners'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ScannerApiException(
        '/production/scanners',
        response.statusCode,
        _extractMessage(response),
      );
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      return const [];
    }

    return decoded
        .map((item) => ScannerRecord.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  Future<ScannerRecord> createScanner(ScannerWritePayload payload) async {
    final response = await _client.post(
      Uri.parse('$_baseUrl/production/scanners'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
      body: jsonEncode(payload.toJson()),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ScannerApiException(
        '/production/scanners',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return ScannerRecord.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<ScannerRecord> updateScanner(int scannerId, ScannerWritePayload payload) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/production/scanners/$scannerId'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
      body: jsonEncode(payload.toJson()),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ScannerApiException(
        '/production/scanners/$scannerId',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return ScannerRecord.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<ScannerRecord> activateScanner(int scannerId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/production/scanners/$scannerId/activate'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ScannerApiException(
        '/production/scanners/$scannerId/activate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return ScannerRecord.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<ScannerRecord> deactivateScanner(int scannerId) async {
    final response = await _client.put(
      Uri.parse('$_baseUrl/production/scanners/$scannerId/deactivate'),
      headers: {'Content-Type': 'application/json; charset=utf-8'},
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ScannerApiException(
        '/production/scanners/$scannerId/deactivate',
        response.statusCode,
        _extractMessage(response),
      );
    }
    return ScannerRecord.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }
}

String _extractMessage(http.Response response) {
  try {
    final decoded = jsonDecode(response.body);
    if (decoded is Map<String, dynamic>) {
      final message = decoded['message'] ?? decoded['error'];
      if (message is String && message.isNotEmpty) return message;
    }
    if (decoded is String && decoded.isNotEmpty) return decoded;
  } catch (_) {}

  return switch (response.statusCode) {
    400 => 'بيانات الماسح غير صحيحة.',
    409 => 'رمز الماسح موجود بالفعل.',
    404 => 'الماسح المطلوب غير موجود.',
    _ => 'تعذر تنفيذ العملية المطلوبة.',
  };
}

class ScannerApiException implements Exception {
  const ScannerApiException(this.path, this.statusCode, this.message);

  final String path;
  final int statusCode;
  final String message;

  @override
  String toString() => 'ScannerApiException(path: $path, statusCode: $statusCode, message: $message)';
}

class ScannerManagementScreen extends StatefulWidget {
  const ScannerManagementScreen({super.key});

  @override
  State<ScannerManagementScreen> createState() => _ScannerManagementScreenState();
}

class _ScannerManagementScreenState extends State<ScannerManagementScreen> {
  final repository = ScannerRepository();
  late Future<List<ScannerRecord>> _scannersFuture;

  @override
  void initState() {
    super.initState();
    _scannersFuture = repository.getScanners();
  }

  void _refresh() {
    if (!mounted) return;
    setState(() => _scannersFuture = repository.getScanners());
  }

  Future<void> _openForm([ScannerRecord? scanner]) async {
    final changed = await AppNavigation.push(
      context,
      (_) => ScannerFormScreen(
        scanner: scanner,
        onSaved: _refresh,
      ),
    );
    if (changed == true && mounted) {
      _refresh();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        title: const Text('إدارة أجهزة المسح'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_rounded),
          ),
          IconButton(
            tooltip: 'إضافة ماسح',
            onPressed: () => _openForm(),
            icon: const Icon(Icons.add_circle_outline_rounded),
          ),
        ],
      ),
      body: FutureBuilder<List<ScannerRecord>>(
        future: _scannersFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline_rounded, size: 42),
                    const SizedBox(height: 12),
                    Text(
                      (snapshot.error as ScannerApiException?)?.message ??
                          'تعذر تحميل أجهزة المسح.',
                      textAlign: TextAlign.center,
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.screenBackground,
                        fontSize: 15,
                      ),
                    ),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      onPressed: _refresh,
                      icon: const Icon(Icons.refresh_rounded),
                      label: const Text('إعادة المحاولة'),
                    ),
                  ],
                ),
              ),
            );
          }

          final scanners = snapshot.data ?? const <ScannerRecord>[];
          if (scanners.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.qr_code_scanner_outlined, size: 52),
                  const SizedBox(height: 12),
                  Text(
                    'لا توجد أجهزة مسح مسجلة بعد.',
                    style: UiPalette.adaptiveTextStyle(
                      context,
                      backgroundColor: UiPalette.screenBackground,
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 24),
                  FilledButton.icon(
                    onPressed: () => _openForm(),
                    icon: const Icon(Icons.add_rounded),
                    label: const Text('إضافة أول ماسح'),
                  ),
                ],
              ),
            );
          }

          return ListView.builder(
            padding: const EdgeInsets.all(16),
            itemCount: scanners.length,
            itemBuilder: (context, index) {
              final scanner = scanners[index];
              return Container(
                margin: const EdgeInsets.only(bottom: 14),
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: UiPalette.surfaceCard,
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(color: UiPalette.borderSoft),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      width: 52,
                      height: 52,
                      decoration: BoxDecoration(
                        color: scanner.isActive
                            ? UiPalette.primaryBlue.withValues(alpha: 0.18)
                            : Colors.orange.withValues(alpha: 0.18),
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: Icon(
                        Icons.qr_code_scanner_rounded,
                        color: scanner.isActive
                            ? UiPalette.primaryBlue
                            : Colors.orange,
                      ),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: [
                              Expanded(
                                child: Text(
                                  scanner.scannerName,
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.surfaceCard,
                                    fontSize: 18,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                              ),
                              Chip(
                                label: Text(
                                  scanner.isActive ? 'نشط' : 'غير نشط',
                                ),
                                backgroundColor: scanner.isActive
                                    ? UiPalette.primaryBlue.withValues(alpha: 0.18)
                                    : Colors.orange.withValues(alpha: 0.18),
                              ),
                            ],
                          ),
                          const SizedBox(height: 6),
                          Text(
                            scanner.scannerCode,
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 14,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          if ((scanner.description ?? '').trim().isNotEmpty) ...[
                            const SizedBox(height: 6),
                            Text(
                              scanner.description ?? '',
                              style: UiPalette.adaptiveTextStyle(
                                context,
                                backgroundColor: UiPalette.surfaceCard,
                                fontSize: 13,
                              ),
                            ),
                          ],
                          const SizedBox(height: 8),
                          Text(
                            'تاريخ الإنشاء: ${_formatDate(scanner.createdAt)}',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontSize: 12,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 10),
                    Column(
                      children: [
                        IconButton(
                          tooltip: 'تعديل',
                          onPressed: () => _openForm(scanner),
                          icon: const Icon(Icons.edit_outlined),
                        ),
                        IconButton(
                          tooltip: scanner.isActive ? 'إيقاف' : 'تفعيل',
                          onPressed: () async {
                            try {
                              if (!mounted) return;
                              if (scanner.isActive) {
                                await repository.deactivateScanner(scanner.id);
                              } else {
                                await repository.activateScanner(scanner.id);
                              }
                              if (!mounted) return;
                              _refresh();
                            } on ScannerApiException catch (error) {
                              if (!mounted || !context.mounted) return;
                              ScaffoldMessenger.maybeOf(context)
                                  ?.showSnackBar(
                                    SnackBar(
                                      backgroundColor: Colors.red.shade700,
                                      content: Text(error.message),
                                    ),
                                  );
                            }
                          },
                          icon: Icon(
                            scanner.isActive
                                ? Icons.toggle_on_rounded
                                : Icons.toggle_off_rounded,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              );
            },
          );
        },
      ),
    );
  }
}

class ScannerFormScreen extends StatefulWidget {
  const ScannerFormScreen({
    super.key,
    this.scanner,
    this.onSaved,
  });

  final ScannerRecord? scanner;
  final VoidCallback? onSaved;

  @override
  State<ScannerFormScreen> createState() => _ScannerFormScreenState();
}

class _ScannerFormScreenState extends State<ScannerFormScreen> {
  final repository = ScannerRepository();
  final _formKey = GlobalKey<FormState>();
  final _scannerCodeController = TextEditingController();
  final _scannerNameController = TextEditingController();
  final _descriptionController = TextEditingController();
  bool _isActive = true;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final scanner = widget.scanner;
    if (scanner != null) {
      _scannerCodeController.text = scanner.scannerCode;
      _scannerNameController.text = scanner.scannerName;
      _descriptionController.text = scanner.description ?? '';
      _isActive = scanner.isActive;
    }
  }

  @override
  void dispose() {
    _scannerCodeController.dispose();
    _scannerNameController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _saving = true);

    final payload = ScannerWritePayload(
      scannerCode: _scannerCodeController.text,
      scannerName: _scannerNameController.text,
      description: _descriptionController.text,
      isActive: _isActive,
    );

    try {
      if (widget.scanner == null) {
        await repository.createScanner(payload);
      } else {
        await repository.updateScanner(widget.scanner!.id, payload);
      }

      if (!mounted) return;
      widget.onSaved?.call();
      Navigator.pop(context, true);
    } on ScannerApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: Colors.red.shade700,
          content: Text(error.message),
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isEdit = widget.scanner != null;

    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        title: Text(isEdit ? 'تعديل ماسح' : 'إضافة ماسح'),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Card(
                  color: UiPalette.surfaceCard,
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      children: [
                        TextFormField(
                          controller: _scannerCodeController,
                          decoration: const InputDecoration(
                            labelText: 'رمز الماسح',
                            border: OutlineInputBorder(),
                          ),
                          validator: (value) => (value == null || value.trim().isEmpty)
                              ? 'رمز الماسح مطلوب.'
                              : null,
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          controller: _scannerNameController,
                          decoration: const InputDecoration(
                            labelText: 'اسم الماسح',
                            border: OutlineInputBorder(),
                          ),
                          validator: (value) => (value == null || value.trim().isEmpty)
                              ? 'اسم الماسح مطلوب.'
                              : null,
                        ),
                        const SizedBox(height: 16),
                        TextFormField(
                          controller: _descriptionController,
                          maxLines: 3,
                          decoration: const InputDecoration(
                            labelText: 'الوصف',
                            border: OutlineInputBorder(),
                          ),
                        ),
                        const SizedBox(height: 18),
                        SwitchListTile.adaptive(
                          value: _isActive,
                          title: const Text('نشط في النظام'),
                          subtitle: const Text('يمكن استخدام هذا الماسح في التنفيذ.'),
                          onChanged: (value) => setState(() => _isActive = value),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: _saving ? null : _save,
                  icon: _saving
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.save_rounded),
                  label: Text(_saving ? 'جاري الحفظ...' : 'حفظ'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String _formatDate(DateTime date) => '${date.year}/${date.month.toString().padLeft(2, '0')}/${date.day.toString().padLeft(2, '0')}';