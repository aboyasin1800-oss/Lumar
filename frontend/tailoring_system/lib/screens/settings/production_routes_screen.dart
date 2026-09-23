import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/app_navigation.dart';
import '../../core/production_display_mapper.dart';
import '../../core/ui_palette.dart';

const List<String> _canonicalProductionStages = [
  'Printing',
  'FabricPrep',
  'Cutting',
  'Sewing',
  'Buttons',
  'Ironing',
  'Quality',
  'Assembly',
];

List<Map<String, dynamic>> buildUniqueProductionRouteItems({
  required List<dynamic> officialProductTypes,
  required List<dynamic> routeEntries,
}) {
  final routeStagesByProductTypeId = <int, List<String>>{};

  for (final entry in routeEntries) {
    final map = entry is Map ? Map<String, dynamic>.from(entry as Map) : <String, dynamic>{};
    final productTypeId = int.tryParse(
          (map['productTypeId'] ?? map['ProductTypeId'] ?? 0).toString(),
        ) ??
        0;
    if (productTypeId <= 0) continue;

    final rawStages = (map['stages'] ?? map['Stages'] ?? map['route'] ?? map['Route'] ?? const <dynamic>[])
        as List<dynamic>? ?? const <dynamic>[];
    final isEnabled = map['isEnabled'] != false;
    final stages = rawStages
      .map((value) => value.toString().trim())
        .where((value) => value.isNotEmpty)
        .toList();

    routeStagesByProductTypeId.putIfAbsent(
      productTypeId,
      () => isEnabled ? stages : <String>[],
    );
  }

  final deduped = <Map<String, dynamic>>[];
  final seenProductTypeIds = <int>{};

  for (final item in officialProductTypes) {
    final map = item is Map ? Map<String, dynamic>.from(item as Map) : <String, dynamic>{};
    final productTypeId = int.tryParse((map['productTypeId'] ?? map['ProductTypeId'] ?? 0).toString()) ?? 0;
    final code = (map['code'] ?? map['Code'] ?? '').toString().trim();
    final nameAr = (map['nameAr'] ?? map['NameAr'] ?? map['name'] ?? map['Name'] ?? '').toString().trim();
    final fallbackName = nameAr.isNotEmpty ? nameAr : code;
    if (productTypeId <= 0 || !seenProductTypeIds.add(productTypeId)) continue;

    final route = routeStagesByProductTypeId[productTypeId] ?? const <String>[];

    deduped.add({
      'productTypeId': productTypeId,
      'code': code,
      'nameAr': fallbackName,
      'route': route,
      'source': 'official',
    });
  }

  return deduped;
}

class ProductionRouteEntry {
  const ProductionRouteEntry({
    required this.productTypeId,
    required this.productTypeCode,
    required this.productTypeNameAr,
    required this.stages,
    required this.isEnabled,
  });

  final int productTypeId;
  final String productTypeCode;
  final String productTypeNameAr;
  final List<String> stages;
  final bool isEnabled;

  factory ProductionRouteEntry.fromJson(Map<String, dynamic> json) {
    final productTypeId = int.tryParse(
          (json['productTypeId'] ?? json['ProductTypeId'] ?? 0).toString(),
        ) ??
        0;
    final productTypeCode = (json['productTypeCode'] ?? json['ProductTypeCode'] ?? json['code'] ?? json['Code'] ?? '').toString().trim();
    final productTypeNameAr = (json['productTypeNameAr'] ?? json['ProductTypeNameAr'] ?? json['nameAr'] ?? json['NameAr'] ?? '').toString().trim();
    final rawStages = (json['stages'] ?? json['Stages'] ?? json['route'] ?? json['Route'] ?? const <dynamic>[])
        as List<dynamic>? ?? const <dynamic>[];

    final stages = rawStages
        .map((value) => value.toString())
        .where((value) => value.trim().isNotEmpty)
        .toList();

    return ProductionRouteEntry(
      productTypeId: productTypeId,
      productTypeCode: productTypeCode,
      productTypeNameAr: productTypeNameAr,
      stages: _normalizeStages(stages),
      isEnabled: json['isEnabled'] != false,
    );
  }

  Map<String, dynamic> toJson() => {
        'productTypeId': productTypeId,
        'productTypeCode': productTypeCode,
        'productTypeNameAr': productTypeNameAr,
        'stages': stages,
        'isEnabled': isEnabled,
      };

  static List<String> _normalizeStages(List<String> values) {
    final seen = <String>{};
    final ordered = <String>[];
    for (final stage in values
        .map((value) => value.trim())
        .where((value) => value.isNotEmpty)
        .where((value) => _canonicalProductionStages.contains(value))) {
      if (seen.add(stage)) {
        ordered.add(stage);
      }
    }

    final finalOrdered = <String>[];
    for (final stage in _canonicalProductionStages) {
      if (ordered.contains(stage)) {
        finalOrdered.add(stage);
      }
    }
    return finalOrdered;
  }
}

class ProductionRoutesConfigModel {
  const ProductionRoutesConfigModel({required this.routes});

  final List<ProductionRouteEntry> routes;

  factory ProductionRoutesConfigModel.fromJson(Map<String, dynamic> json) {
    final entries = (json['routes'] as List<dynamic>? ?? const <dynamic>[])
        .map((value) => ProductionRouteEntry.fromJson(value as Map<String, dynamic>))
        .toList();
    return ProductionRoutesConfigModel(routes: entries);
  }

  Map<String, dynamic> toJson() => {
        'routes': routes.map((e) => e.toJson()).toList(),
      };
}

class ProductionRoutesScreen extends StatefulWidget {
  const ProductionRoutesScreen({super.key});

  @override
  State<ProductionRoutesScreen> createState() => _ProductionRoutesScreenState();
}

class _ProductionRoutesScreenState extends State<ProductionRoutesScreen> {
  static const String _baseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  bool _loading = true;
  bool _saving = false;
  String? _error;
  String? _message;
  final TextEditingController _searchController = TextEditingController();
  final List<Map<String, dynamic>> _products = <Map<String, dynamic>>[];
  final Map<int, List<String>> _routeSelections = <int, List<String>>{};

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _message = null;
    });

    try {
      final settingsResponse = await http.get(Uri.parse('$_baseUrl/settings/production-routes'));
      final officialTypesResponse = await http.get(Uri.parse('$_baseUrl/consumption-rules'));

      if (settingsResponse.statusCode < 200 || settingsResponse.statusCode >= 300) {
        throw Exception('تعذر تحميل إعدادات المسارات (${settingsResponse.statusCode})');
      }
      if (officialTypesResponse.statusCode < 200 || officialTypesResponse.statusCode >= 300) {
        throw Exception('تعذر تحميل أنواع القطع الرسمية (${officialTypesResponse.statusCode})');
      }

      final routeConfig = jsonDecode(settingsResponse.body) as Map<String, dynamic>;
      final entries = (routeConfig['routes'] as List<dynamic>? ?? const <dynamic>[])
          .map((item) => ProductionRouteEntry.fromJson(item as Map<String, dynamic>))
          .toList();

      final officialPayload = jsonDecode(officialTypesResponse.body) as Map<String, dynamic>;
      final officialProductTypes = (officialPayload['productTypes'] as List<dynamic>? ?? const <dynamic>[]);
      final uniqueItems = buildUniqueProductionRouteItems(
        officialProductTypes: officialProductTypes,
        routeEntries: entries.map((entry) => entry.toJson()).toList(),
      );

      final products = uniqueItems.where((item) {
        final id = int.tryParse(item['productTypeId'].toString()) ?? 0;
        return id > 0 && item['nameAr'].toString().trim().isNotEmpty;
      }).toList()
        ..sort((a, b) => a['nameAr'].toString().compareTo(b['nameAr'].toString()));
      final routeSelections = <int, List<String>>{};
      for (final item in uniqueItems) {
        final productTypeId = int.tryParse(item['productTypeId'].toString()) ?? 0;
        if (productTypeId <= 0) continue;
        routeSelections[productTypeId] = (item['route'] as List<dynamic>? ?? const <dynamic>[])
            .map((stage) => stage.toString())
            .where((stage) => stage.isNotEmpty)
            .toList();
      }

      setState(() {
        _products
          ..clear()
          ..addAll(products);
        _routeSelections
          ..clear()
          ..addAll(routeSelections);
        _loading = false;
      });
    } catch (error) {
      setState(() {
        _loading = false;
        _error = error.toString();
      });
    }
  }

  Future<void> _save() async {
    final payloadRoutes = <Map<String, dynamic>>[];
    for (final product in _products) {
      final productTypeId = int.tryParse(product['productTypeId'].toString()) ?? 0;
      final stages = _routeSelections[productTypeId] ?? const <String>[];
      final orderedStages = _canonicalProductionStages.where((stage) => stages.contains(stage)).toList();
      payloadRoutes.add({
        'productTypeId': productTypeId,
        'productTypeCode': product['code'].toString(),
        'productTypeNameAr': product['nameAr'].toString(),
        'stages': orderedStages,
        'isEnabled': true,
      });
    }

    setState(() {
      _saving = true;
      _error = null;
      _message = null;
    });

    try {
      final response = await http.put(
        Uri.parse('$_baseUrl/settings/production-routes'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'routes': payloadRoutes}),
      );

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر حفظ تكوين المسارات (${response.statusCode})');
      }

      final decoded = jsonDecode(response.body) as Map<String, dynamic>;
      final updated = (decoded['routes'] as List<dynamic>? ?? const <dynamic>[])
          .map((item) => ProductionRouteEntry.fromJson(item as Map<String, dynamic>))
          .where((entry) => entry.productTypeId > 0)
          .toList();
      final updatedById = <int, List<String>>{
        for (final route in updated) route.productTypeId: route.stages,
      };
      if (updatedById.length != _products.length ||
          _products.any((product) => !updatedById.containsKey(int.tryParse(product['productTypeId'].toString()) ?? 0))) {
        throw Exception('لم تتطابق إعادة القراءة من الخادم مع المسارات المرسلة.');
      }

      setState(() {
        _routeSelections
          ..clear()
          ..addAll(updatedById);
        _message = 'تم حفظ مسارات الإنتاج وإعادة قراءتها بنجاح.';
      });
    } catch (error) {
      setState(() {
        _error = error.toString();
      });
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final searchQuery = _searchController.text.trim().toLowerCase();
    final visibleProducts = _products.where((product) {
      final code = product['code'].toString().toLowerCase();
      final nameAr = product['nameAr'].toString().toLowerCase();
      if (searchQuery.isEmpty) return true;
      return code.contains(searchQuery) || nameAr.contains(searchQuery);
    }).toList();

    final suggestions = searchQuery.isEmpty
        ? const <Map<String, dynamic>>[]
        : _products
            .where((product) {
              final code = product['code'].toString().toLowerCase();
              final nameAr = product['nameAr'].toString().toLowerCase();
              return code.contains(searchQuery) || nameAr.contains(searchQuery);
            })
            .take(6)
            .toList();

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('مسارات الإنتاج'),
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
          automaticallyImplyLeading: false,
          actions: [
            IconButton(
              tooltip: 'تحديث المسارات',
              onPressed: _load,
              icon: const Icon(Icons.refresh_rounded),
            ),
            FilledButton.icon(
              onPressed: _saving ? null : _save,
              icon: _saving ? const SizedBox.shrink() : const Icon(Icons.save_outlined),
              label: Text(_saving ? 'جارٍ الحفظ...' : 'حفظ التغييرات'),
            ),
            const SizedBox(width: 8),
          ],
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? _ErrorState(message: _error!, onRetry: _load)
                : ListView(
                    padding: const EdgeInsets.all(20),
                    children: [
                      _InfoCard(
                        title: 'القاعدة المعيارية',
                        body: ProductionDisplayMapper.routeLabel(_canonicalProductionStages),
                      ),
                      const SizedBox(height: 16),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                        decoration: BoxDecoration(
                          color: UiPalette.surfaceCard,
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(color: UiPalette.borderSoft),
                        ),
                        child: TextField(
                          controller: _searchController,
                          textDirection: TextDirection.rtl,
                          onChanged: (_) => setState(() {}),
                          decoration: InputDecoration(
                            border: InputBorder.none,
                            hintText: 'ابحث عن نوع القطعة',
                            hintStyle: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                            ).copyWith(color: UiPalette.textSoft),
                            prefixIcon: const Icon(Icons.search_rounded),
                            suffixIcon: _searchController.text.isEmpty
                                ? null
                                : IconButton(
                                    icon: const Icon(Icons.close_rounded),
                                    onPressed: () {
                                      _searchController.clear();
                                      setState(() {});
                                    },
                                  ),
                          ),
                        ),
                      ),
                      if (suggestions.isNotEmpty) ...[
                        const SizedBox(height: 12),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: suggestions.map((product) {
                            final label = product['nameAr'].toString();
                            return InkWell(
                              borderRadius: BorderRadius.circular(20),
                              onTap: () {
                                _searchController.text = product['nameAr'].toString();
                                setState(() {});
                              },
                              child: Container(
                                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                                decoration: BoxDecoration(
                                  color: UiPalette.primaryBlue.withValues(alpha: 0.10),
                                  borderRadius: BorderRadius.circular(20),
                                  border: Border.all(color: UiPalette.primaryDark),
                                ),
                                child: Text(
                                  label.isEmpty ? product['code'].toString() : label,
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.surfaceCard,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            );
                          }).toList(),
                        ),
                      ],
                      const SizedBox(height: 16),
                      if (_message != null)
                        Container(
                          margin: const EdgeInsets.only(bottom: 14),
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: UiPalette.primaryBlue.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(color: UiPalette.primaryDark),
                          ),
                          child: Text(
                            _message!,
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      if (visibleProducts.isEmpty)
                        Container(
                          padding: const EdgeInsets.all(18),
                          decoration: BoxDecoration(
                            color: UiPalette.surfaceCard,
                            borderRadius: BorderRadius.circular(16),
                            border: Border.all(color: UiPalette.borderSoft),
                          ),
                          child: Text(
                            'لا توجد نتائج تطابق البحث.',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.surfaceCard,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        )
                      else
                        ...visibleProducts.map((product) {
                          final productTypeId = int.tryParse(product['productTypeId'].toString()) ?? 0;
                          return _RouteCard(
                              productTypeId: productTypeId,
                              productTypeCode: product['code'].toString(),
                              productTypeNameAr: product['nameAr'].toString(),
                              selectedStages: _routeSelections[productTypeId] ?? const <String>[],
                              allStages: _canonicalProductionStages,
                              onChanged: (stages) {
                                setState(() {
                                  _routeSelections[productTypeId] = stages;
                                });
                              },
                            );
                        }),
                      const SizedBox(height: 18),
                    ],
                  ),
      ),
    );
  }
}

class _RouteCard extends StatelessWidget {
  const _RouteCard({
    required this.productTypeId,
    required this.productTypeCode,
    required this.productTypeNameAr,
    required this.selectedStages,
    required this.allStages,
    required this.onChanged,
  });

  final int productTypeId;
  final String productTypeCode;
  final String productTypeNameAr;
  final List<String> selectedStages;
  final List<String> allStages;
  final ValueChanged<List<String>> onChanged;

  @override
  Widget build(BuildContext context) {
    final routeDisplay = selectedStages;

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            productTypeNameAr,
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.surfaceCard,
              fontSize: 20,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            '$productTypeCode  |  ProductTypeId: $productTypeId',
            style: UiPalette.adaptiveTextStyle(
              context,
              backgroundColor: UiPalette.surfaceCard,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: allStages.map((stage) {
              final active = routeDisplay.contains(stage);
              return ChoiceChip(
                label: Text(ProductionDisplayMapper.stageLabel(stage)),
                selected: active,
                onSelected: (_) {
                  final next = List<String>.from(routeDisplay);
                  if (active) {
                    next.remove(stage);
                  } else {
                    next.add(stage);
                  }
                  final ordered = <String>[];
                  for (final canonical in allStages) {
                    if (next.contains(canonical)) {
                      ordered.add(canonical);
                    }
                  }
                  onChanged(ordered);
                },
                showCheckmark: true,
                selectedColor: UiPalette.primaryBlue,
                backgroundColor: const Color(0xFF3B4A57),
                side: BorderSide(
                  color: active ? UiPalette.primaryDark : UiPalette.borderSoft,
                  width: 1.2,
                ),
                labelStyle: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: active ? UiPalette.primaryBlue : const Color(0xFF3B4A57),
                  fontWeight: FontWeight.w700,
                ),
              );
            }).toList(),
          ),
        ],
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: UiPalette.surfaceCard,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 16,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              body,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 14,
              ),
            ),
          ],
        ),
      );
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                'تعذر تحميل إعدادات المسارات',
                style: UiPalette.adaptiveTextStyle(
                  context,
                  backgroundColor: UiPalette.screenBackground,
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 8),
              Text(message),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('إعادة المحاولة'),
              ),
            ],
          ),
        ),
      );
}
