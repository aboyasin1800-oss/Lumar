import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';
import '../../models/employee_models.dart';
import '../../repositories/employee_repository.dart';
import 'scanner_details_screen.dart';

const Map<String, String> _scannerStageLabels = {
  'Printing': 'الطباعة',
  'FabricPrep': 'تجهيز القماش',
  'Cutting': 'القص',
  'Sewing': 'الخياطة',
  'Buttons': 'الأزرار',
  'Ironing': 'الكَي',
  'Quality': 'الجودة',
  'Assembly': 'التجميع',
};

const List<String> _scannerPermissionStages = [
  'Printing',
  'FabricPrep',
  'Cutting',
  'Sewing',
  'Buttons',
  'Ironing',
  'Quality',
  'Assembly',
];

String localizedScannerStage(String stage) =>
    _scannerStageLabels[stage.trim()] ?? stage.trim();

class ScannerRecord {
  const ScannerRecord({
    required this.id,
    required this.scannerCode,
    required this.scannerName,
    required this.description,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
    this.employeeId,
    this.employeeCode,
    this.employeeName,
  });

  factory ScannerRecord.fromJson(Map<String, dynamic> json) => ScannerRecord(
        id: (json['scannerId'] ?? json['ScannerId'] ?? 0) as int,
        scannerCode:
            (json['scannerCode'] ?? json['ScannerCode'] ?? '').toString(),
        scannerName:
            (json['scannerName'] ?? json['ScannerName'] ?? '').toString(),
        description: (json['description'] ?? json['Description'])?.toString(),
        isActive: (json['isActive'] ?? json['IsActive'] ?? false) as bool,
        createdAt: DateTime.tryParse(
              (json['createdAt'] ?? json['CreatedAt'] ?? '').toString(),
            ) ??
            DateTime.now(),
        updatedAt: DateTime.tryParse(
          (json['updatedAt'] ?? json['UpdatedAt'] ?? '').toString(),
        ),
        employeeId: int.tryParse(
          (json['employeeId'] ?? json['EmployeeId'] ?? '').toString(),
        ),
        employeeCode: (json['employeeCode'] ?? json['EmployeeCode'])?.toString(),
        employeeName: (json['employeeName'] ?? json['EmployeeName'])?.toString(),
      );

  final int id;
  final String scannerCode;
  final String scannerName;
  final String? description;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final int? employeeId;
  final String? employeeCode;
  final String? employeeName;
}

class ScannerPermissionWritePayload {
  const ScannerPermissionWritePayload({
    required this.productTypeId,
    required this.stage,
    this.isActive = true,
  });

  final int productTypeId;
  final String stage;
  final bool isActive;

  Map<String, dynamic> toJson() => {
        'productTypeId': productTypeId,
        'stage': stage,
        'isActive': isActive,
      };
}

class ScannerPermissionRecord {
  const ScannerPermissionRecord({
    required this.scannerPermissionId,
    required this.scannerId,
    required this.productTypeId,
    required this.stage,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
  });

  factory ScannerPermissionRecord.fromJson(Map<String, dynamic> json) =>
      ScannerPermissionRecord(
        scannerPermissionId: (json['scannerPermissionId'] ??
            json['ScannerPermissionId'] ??
            0) as int,
        scannerId: (json['scannerId'] ?? json['ScannerId'] ?? 0) as int,
        productTypeId:
            (json['productTypeId'] ?? json['ProductTypeId'] ?? 0) as int,
        stage: (json['stage'] ?? json['Stage'] ?? '').toString(),
        isActive: (json['isActive'] ?? json['IsActive'] ?? true) as bool,
        createdAt: DateTime.tryParse(
              (json['createdAt'] ?? json['CreatedAt'] ?? '').toString(),
            ) ??
            DateTime.now(),
        updatedAt: DateTime.tryParse(
          (json['updatedAt'] ?? json['UpdatedAt'] ?? '').toString(),
        ),
      );

  final int scannerPermissionId;
  final int scannerId;
  final int productTypeId;
  final String stage;
  final bool isActive;
  final DateTime createdAt;
  final DateTime? updatedAt;
}

class ProductTypeOption {
  const ProductTypeOption({
    required this.productTypeId,
    required this.nameAr,
  });

  factory ProductTypeOption.fromJson(Map<String, dynamic> json) {
    final productTypeId = int.tryParse(
          (json['productTypeId'] ??
                  json['ProductTypeId'] ??
                  json['id'] ??
                  json['Id'] ??
                  0)
              .toString(),
        ) ??
        0;
    final nameAr =
        (json['nameAr'] ?? json['NameAr'] ?? json['name'] ?? json['Name'] ?? '')
            .toString()
            .trim();

    return ProductTypeOption(
      productTypeId: productTypeId,
      nameAr: nameAr,
    );
  }

  final int productTypeId;
  final String nameAr;
}

class ScannerWritePayload {
  const ScannerWritePayload({
    required this.scannerCode,
    required this.scannerName,
    required this.description,
    required this.isActive,
    this.employeeId,
    this.permissions,
  });

  final String scannerCode;
  final String scannerName;
  final String? description;
  final bool isActive;
  final int? employeeId;
  final List<ScannerPermissionWritePayload>? permissions;

  Map<String, dynamic> toJson() => {
        'scannerCode': scannerCode.trim(),
        'scannerName': scannerName.trim(),
        'description':
            description?.trim().isNotEmpty == true ? description!.trim() : null,
        'isActive': isActive,
        'employeeId': employeeId,
        'permissions': (permissions ?? const <ScannerPermissionWritePayload>[])
            .map((permission) => permission.toJson())
            .toList(),
      };
}

class ScannerRepository {
  ScannerRepository({http.Client? client}) : _client = client ?? http.Client();

  static const _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final http.Client _client;

  Future<T> _guardRequest<T>(String path, Future<T> Function() action) async {
    try {
      return await action();
    } on ScannerApiException {
      rethrow;
    } on TimeoutException {
      throw const ScannerApiException(
        '',
        504,
        'انتهت مهلة الاتصال بالخادم. حاول مرة أخرى.',
      );
    } on SocketException {
      throw const ScannerApiException(
        '',
        0,
        'تعذر الاتصال بالخادم. تأكد من تشغيل الخدمة ثم حاول مرة أخرى.',
      );
    } on http.ClientException {
      throw const ScannerApiException(
        '',
        0,
        'تعذر الاتصال بالخادم. تأكد من تشغيل الخدمة ثم حاول مرة أخرى.',
      );
    } catch (_) {
      throw ScannerApiException(
        path,
        500,
        'حدث خطأ غير متوقع أثناء الاتصال بالخادم. حاول مرة أخرى.',
      );
    }
  }

  Future<List<ScannerRecord>> getScanners() => _guardRequest(
        '/production/scanners',
        () async {
          final response =
              await _client.get(Uri.parse('$_baseUrl/production/scanners'));
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
        },
      );

  Future<ScannerRecord> createScanner(ScannerWritePayload payload) => _guardRequest(
        '/production/scanners',
        () async {
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
          return ScannerRecord.fromJson(
              jsonDecode(response.body) as Map<String, dynamic>);
        },
      );

  Future<ScannerRecord> updateScanner(
      int scannerId, ScannerWritePayload payload) => _guardRequest(
        '/production/scanners/$scannerId',
        () async {
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
          return ScannerRecord.fromJson(
              jsonDecode(response.body) as Map<String, dynamic>);
        },
      );

  Future<ScannerRecord> activateScanner(int scannerId) => _guardRequest(
        '/production/scanners/$scannerId/activate',
        () async {
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
          return ScannerRecord.fromJson(
              jsonDecode(response.body) as Map<String, dynamic>);
        },
      );

  Future<ScannerRecord> deactivateScanner(int scannerId) => _guardRequest(
        '/production/scanners/$scannerId/deactivate',
        () async {
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
          return ScannerRecord.fromJson(
              jsonDecode(response.body) as Map<String, dynamic>);
        },
      );

  Future<List<ScannerPermissionRecord>> getPermissions(int scannerId) =>
      _guardRequest(
        '/production/scanners/$scannerId/permissions',
        () async {
          final response = await _client.get(
            Uri.parse('$_baseUrl/production/scanners/$scannerId/permissions'),
          );
          if (response.statusCode < 200 || response.statusCode >= 300) {
            throw ScannerApiException(
              '/production/scanners/$scannerId/permissions',
              response.statusCode,
              _extractMessage(response),
            );
          }

          final decoded = jsonDecode(response.body);
          if (decoded is! List) {
            return const <ScannerPermissionRecord>[];
          }

          return decoded
              .map((item) =>
                  ScannerPermissionRecord.fromJson(item as Map<String, dynamic>))
              .toList();
        },
      );

  Future<List<ScannerPermissionRecord>> upsertPermissions(
    int scannerId,
    List<ScannerPermissionWritePayload> permissions,
  ) => _guardRequest(
        '/production/scanners/$scannerId/permissions',
        () async {
          final response = await _client.put(
            Uri.parse('$_baseUrl/production/scanners/$scannerId/permissions'),
            headers: {'Content-Type': 'application/json; charset=utf-8'},
            body: jsonEncode(
                permissions.map((permission) => permission.toJson()).toList()),
          );
          if (response.statusCode < 200 || response.statusCode >= 300) {
            throw ScannerApiException(
              '/production/scanners/$scannerId/permissions',
              response.statusCode,
              _extractMessage(response),
            );
          }

          final decoded = jsonDecode(response.body);
          if (decoded is! List) {
            return const <ScannerPermissionRecord>[];
          }

          return decoded
              .map((item) =>
                  ScannerPermissionRecord.fromJson(item as Map<String, dynamic>))
              .toList();
        },
      );

  Future<List<ProductTypeOption>> getProductTypes() => _guardRequest(
        '/consumption-rules',
        () async {
          final response =
              await _client.get(Uri.parse('$_baseUrl/consumption-rules'));
          if (response.statusCode < 200 || response.statusCode >= 300) {
            throw ScannerApiException(
              '/consumption-rules',
              response.statusCode,
              _extractMessage(response),
            );
          }

          final decoded = jsonDecode(response.body);
          if (decoded is! Map<String, dynamic>) {
            return const <ProductTypeOption>[];
          }

          final productTypes = (decoded['productTypes'] as List<dynamic>? ??
                  const <dynamic>[])
              .whereType<Map<String, dynamic>>()
              .map(ProductTypeOption.fromJson)
              .where((option) => option.productTypeId > 0 && option.nameAr.isNotEmpty)
              .toList()
            ..sort((a, b) => a.nameAr.compareTo(b.nameAr));

          return productTypes;
        },
      );
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

String _safeScannerErrorMessage(Object? error) {
  if (error is ScannerApiException) return error.message;
  if (error is TimeoutException) {
    return 'انتهت مهلة الاتصال بالخادم. حاول مرة أخرى.';
  }
  if (error is SocketException || error is http.ClientException) {
    return 'تعذر الاتصال بالخادم. تأكد من تشغيل الخدمة ثم حاول مرة أخرى.';
  }
  if (error != null) {
    return 'تعذر تحميل أجهزة المسح. حاول مرة أخرى.';
  }
  return 'تعذر تحميل أجهزة المسح.';
}

class ScannerApiException implements Exception {
  const ScannerApiException(this.path, this.statusCode, this.message);

  final String path;
  final int statusCode;
  final String message;

  @override
  String toString() =>
      'ScannerApiException(path: $path, statusCode: $statusCode, message: $message)';
}

class ScannerManagementScreen extends StatefulWidget {
  const ScannerManagementScreen({super.key});

  @override
  State<ScannerManagementScreen> createState() =>
      _ScannerManagementScreenState();
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

  Future<void> _openDetails(ScannerRecord scanner) async {
    await AppNavigation.push(
      context,
      (_) => ScannerDetailsScreen(scanner: scanner),
    );
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
            final message = _safeScannerErrorMessage(snapshot.error);
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline_rounded, size: 42),
                    const SizedBox(height: 12),
                    Text(
                      message,
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
                                    ? UiPalette.primaryBlue
                                        .withValues(alpha: 0.18)
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
                          if ((scanner.description ?? '')
                              .trim()
                              .isNotEmpty) ...[
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
                          tooltip: 'تفاصيل',
                          onPressed: () => _openDetails(scanner),
                          icon: const Icon(Icons.info_outline_rounded),
                        ),
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
                              ScaffoldMessenger.maybeOf(context)?.showSnackBar(
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
  final _employeeRepository = EmployeeRepository();
  final _formKey = GlobalKey<FormState>();
  final _scannerCodeController = TextEditingController();
  final _scannerNameController = TextEditingController();
  final _descriptionController = TextEditingController();
  bool _isActive = true;
  int? _selectedEmployeeId;
  List<EmployeeSummary> _employees = const <EmployeeSummary>[];
  bool _saving = false;
  bool _loadingPermissions = true;
  List<ProductTypeOption> _productTypes = const <ProductTypeOption>[];
  final Map<int, Set<String>> _selectedStagesByProductType =
      <int, Set<String>>{};

  @override
  void initState() {
    super.initState();
    final scanner = widget.scanner;
    if (scanner != null) {
      _scannerCodeController.text = scanner.scannerCode;
      _scannerNameController.text = scanner.scannerName;
      _descriptionController.text = scanner.description ?? '';
      _isActive = scanner.isActive;
      _selectedEmployeeId = scanner.employeeId;
    }

    _loadEmployees();
    _loadProductTypeCatalog();
  }

  Future<void> _loadEmployees() async {
    try {
      final employees = await _employeeRepository.getEmployees();
      if (!mounted) return;
      setState(() {
        _employees = employees
            .where((employee) => employee.isActive != false)
            .toList()
          ..sort((a, b) => a.name.compareTo(b.name));
        if (_selectedEmployeeId != null &&
            !_employees.any((employee) => employee.id == _selectedEmployeeId)) {
          _selectedEmployeeId = null;
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _employees = const <EmployeeSummary>[]);
    }
  }

  Future<void> _loadProductTypeCatalog() async {
    try {
      final productTypes = await repository.getProductTypes();
      final selectedStagesByProductType = <int, Set<String>>{};

      if (widget.scanner != null) {
        final permissions = await repository.getPermissions(widget.scanner!.id);
        for (final permission in permissions) {
          final typeId = permission.productTypeId;
          selectedStagesByProductType.putIfAbsent(typeId, () => <String>{});
          if (permission.isActive && permission.stage.trim().isNotEmpty) {
            selectedStagesByProductType[typeId]!.add(permission.stage.trim());
          }
        }
      }

      if (!mounted) return;
      setState(() {
        _productTypes = productTypes;
        _selectedStagesByProductType
          ..clear()
          ..addAll(selectedStagesByProductType);
        _loadingPermissions = false;
      });
    } on ScannerApiException catch (error) {
      if (!mounted) return;
      setState(() => _loadingPermissions = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: Colors.red.shade700,
          content: Text(error.message),
        ),
      );
    }
  }

  List<ScannerPermissionWritePayload> _buildPermissions() {
    final entries = <ScannerPermissionWritePayload>[];
    for (final entry in _selectedStagesByProductType.entries) {
      final productTypeId = entry.key;
      final stages = entry.value;
      for (final stage in stages) {
        entries.add(
          ScannerPermissionWritePayload(
            productTypeId: productTypeId,
            stage: stage,
            isActive: true,
          ),
        );
      }
    }
    entries.sort((a, b) {
      final byProduct = a.productTypeId.compareTo(b.productTypeId);
      if (byProduct != 0) return byProduct;
      return a.stage.compareTo(b.stage);
    });
    return entries;
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
      employeeId: _selectedEmployeeId,
      permissions: _buildPermissions(),
    );

    try {
      final saved = widget.scanner == null
          ? await repository.createScanner(payload)
          : await repository.updateScanner(widget.scanner!.id, payload);

      if (saved.id <= 0) {
        throw const ScannerApiException(
          '/production/scanners',
          500,
          'تعذر حفظ بيانات الماسح: لم يتم استلام هوية الماسح بعد الحفظ.',
        );
      }

      final reloaded = await repository.getScanners();
      final persisted = reloaded.firstWhere(
        (item) => item.id == saved.id || item.scannerCode == payload.scannerCode,
        orElse: () => saved,
      );

      final expectedEmployeeId = _selectedEmployeeId;
      final matchesEmployee = expectedEmployeeId == null
          ? persisted.employeeId == null
          : persisted.employeeId == expectedEmployeeId;

      if (!matchesEmployee) {
        throw const ScannerApiException(
          '/production/scanners',
          500,
          'تعذر التحقق من حفظ بيانات الماسح. لم يكتمل ربط الموظف بالماسح.',
        );
      }

      if (!mounted) return;
      widget.onSaved?.call();
      ScaffoldMessenger.maybeOf(context)?.showSnackBar(
        const SnackBar(
          backgroundColor: Colors.green,
          content: Text('تم حفظ بيانات الماسح بنجاح.'),
        ),
      );
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
                          validator: (value) =>
                              (value == null || value.trim().isEmpty)
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
                          validator: (value) =>
                              (value == null || value.trim().isEmpty)
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
                        if (_employees.isNotEmpty)
                          DropdownButtonFormField<int?>(
                            initialValue: _selectedEmployeeId,
                            decoration: const InputDecoration(
                              labelText: 'الموظف المرتبط بالماسح',
                              hintText: 'بدون موظف',
                              border: OutlineInputBorder(),
                            ),
                            items: [
                              const DropdownMenuItem<int?>(
                                value: null,
                                child: Text('بدون موظف'),
                              ),
                              ..._employees.map(
                                (employee) => DropdownMenuItem<int?>(
                                  value: employee.id,
                                  child: Text(
                                    '${employee.name} (${employee.code})',
                                  ),
                                ),
                              ),
                            ],
                            onChanged: (value) =>
                                setState(() => _selectedEmployeeId = value),
                          ),
                        if (_employees.isNotEmpty) const SizedBox(height: 18),
                        SwitchListTile.adaptive(
                          value: _isActive,
                          title: const Text('نشط في النظام'),
                          subtitle:
                              const Text('يمكن استخدام هذا الماسح في التنفيذ.'),
                          onChanged: (value) =>
                              setState(() => _isActive = value),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 20),
                Card(
                  color: UiPalette.surfaceCard,
                  child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.rule_rounded),
                            const SizedBox(width: 10),
                            Expanded(
                              child: Text(
                                'صلاحيات المسح حسب النوع والمرحلة',
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 16,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        if (_loadingPermissions)
                          const Center(child: CircularProgressIndicator())
                        else if (_productTypes.isEmpty)
                          const Text('لا توجد أنواع قطع متاحة في النظام.')
                        else
                          ..._productTypes.map((productType) {
                            final selectedStages = _selectedStagesByProductType[
                                    productType.productTypeId] ??
                                <String>{};
                            return Container(
                              margin: const EdgeInsets.only(bottom: 12),
                              padding: const EdgeInsets.all(12),
                              decoration: BoxDecoration(
                                color: UiPalette.screenBackground,
                                borderRadius: BorderRadius.circular(12),
                                border: Border.all(color: UiPalette.borderSoft),
                              ),
                              child: Theme(
                                data: Theme.of(context)
                                    .copyWith(dividerColor: Colors.transparent),
                                child: ExpansionTile(
                                  tilePadding: EdgeInsets.zero,
                                  childrenPadding:
                                      const EdgeInsets.only(top: 8),
                                  title: Text(
                                    productType.nameAr,
                                    style: UiPalette.adaptiveTextStyle(
                                      context,
                                      backgroundColor:
                                          UiPalette.screenBackground,
                                      fontSize: 14,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  subtitle: Text(
                                    selectedStages.isEmpty
                                        ? 'لا توجد مراحل محددة'
                                        : '${selectedStages.length} مرحلة محددة',
                                    style: UiPalette.adaptiveTextStyle(
                                      context,
                                      backgroundColor:
                                          UiPalette.screenBackground,
                                      fontSize: 12,
                                    ),
                                  ),
                                  children: [
                                    Wrap(
                                      spacing: 8,
                                      runSpacing: 8,
                                      children:
                                          _scannerPermissionStages.map((stage) {
                                        final isSelected =
                                            selectedStages.contains(stage);
                                        return FilterChip(
                                          label: Text(localizedScannerStage(stage)),
                                          selected: isSelected,
                                          onSelected: (value) {
                                            setState(() {
                                              final productStages =
                                                  _selectedStagesByProductType
                                                      .putIfAbsent(
                                                productType.productTypeId,
                                                () => <String>{},
                                              );
                                              if (value) {
                                                productStages.add(stage);
                                              } else {
                                                productStages.remove(stage);
                                              }
                                              if (productStages.isEmpty) {
                                                _selectedStagesByProductType
                                                    .remove(productType
                                                        .productTypeId);
                                              }
                                            });
                                          },
                                        );
                                      }).toList(),
                                    ),
                                  ],
                                ),
                              ),
                            );
                          }),
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

String _formatDate(DateTime date) =>
    '${date.year}/${date.month.toString().padLeft(2, '0')}/${date.day.toString().padLeft(2, '0')}';
