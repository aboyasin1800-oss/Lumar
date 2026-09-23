import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/theme/app_theme.dart';
import '../core/theme/app_typography.dart';
import '../core/ui_palette.dart';

class FactoryMonitorScreen extends StatefulWidget {
  const FactoryMonitorScreen({
    super.key,
    this.dataSource,
  });

  final Future<Map<String, dynamic>> Function()? dataSource;

  @override
  State<FactoryMonitorScreen> createState() => _FactoryMonitorScreenState();
}

class _FactoryMonitorScreenState extends State<FactoryMonitorScreen> {
  static const String _apiBaseUrl = String.fromEnvironment(
    'LUMAR_API_URL',
    defaultValue: 'http://127.0.0.1:5093',
  );

  final ScrollController _riskController = ScrollController();
  final ScrollController _stalledController = ScrollController();
  final ScrollController _blockedController = ScrollController();
  final ScrollController _readyController = ScrollController();

  Timer? _timer;
  bool _loading = true;
  String? _errorMessage;
  Map<String, dynamic> _dashboard = const {};

  @override
  void initState() {
    super.initState();
    _refresh();
    _timer = Timer.periodic(const Duration(seconds: 15), (_) => _refresh());
  }

  @override
  void dispose() {
    _timer?.cancel();
    _riskController.dispose();
    _stalledController.dispose();
    _blockedController.dispose();
    _readyController.dispose();
    super.dispose();
  }

  Future<Map<String, dynamic>> _loadDashboard() async {
    if (widget.dataSource != null) {
      return widget.dataSource!();
    }

    final response = await http.get(
      Uri.parse('$_apiBaseUrl/production/factory-monitoring'),
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw StateError('استجابة الخادم غير صحيحة (${response.statusCode}).');
    }

    final decoded = jsonDecode(response.body);
    return decoded is Map<String, dynamic>
        ? decoded
        : Map<String, dynamic>.from(decoded as Map);
  }

  Future<void> _refresh() async {
    try {
      final dashboard = await _loadDashboard();
      if (!mounted) return;

      setState(() {
        _dashboard = dashboard;
        _errorMessage = null;
        _loading = false;
      });
      _scheduleAutoScroll();
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _errorMessage = error.toString();
        _loading = false;
      });
    }
  }

  void _scheduleAutoScroll() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;

      final controllers = <MapEntry<ScrollController, List<Map<String, dynamic>>>>[
        MapEntry(_riskController, _listOf('atRiskOrders')),
        MapEntry(_stalledController, _listOf('stalledOrders')),
        MapEntry(_blockedController, _listOf('blockedOrders')),
        MapEntry(_readyController, _listOf('readyOrders')),
      ];

      for (final entry in controllers) {
        final controller = entry.key;
        if (!controller.hasClients) continue;
        if (entry.value.length < 5) {
          if (controller.offset != 0) {
            controller.jumpTo(0);
          }
          continue;
        }

        final max = controller.position.maxScrollExtent;
        if (max <= 0) continue;

        if (controller.offset > max * 0.98) {
          controller.jumpTo(0);
        }
      }
    });
  }

  List<Map<String, dynamic>> _listOf(String key) {
    final value = _dashboard[key];
    if (value is! List) return const [];
    return value
        .whereType<Map>()
        .map((item) => Map<String, dynamic>.from(item.cast<String, dynamic>()))
        .toList();
  }

  @override
  Widget build(BuildContext context) {
    final riskCount = (_dashboard['totalAtRisk'] as num?)?.toInt() ?? 0;
    final stalledCount = (_dashboard['totalStalled'] as num?)?.toInt() ?? 0;
    final blockedCount = (_dashboard['totalBlocked'] as num?)?.toInt() ?? 0;
    final readyCount = (_dashboard['totalReadyForDelivery'] as num?)?.toInt() ?? 0;
    final overallProgress = (_dashboard['overallProgressPercent'] as num?)?.toDouble() ?? 0;
    final lastUpdated = _dashboard['lastUpdatedAt'];

    final columns = [
      _StatusColumn(
        color: const Color(0xFF3B1A1A).withValues(alpha: 0.52),
        accent: const Color(0xFFFF5A5A),
        items: _listOf('atRiskOrders'),
        controller: _riskController,
        title: 'طلبات في خطر',
        count: riskCount,
        autoScrollRatio: 1.0,
        autoScrollDurationSeconds: 90,
      ),
      _StatusColumn(
        color: const Color(0xFF4A2B1A).withValues(alpha: 0.52),
        accent: const Color(0xFFFFA35D),
        items: _listOf('stalledOrders'),
        controller: _stalledController,
        title: 'طلبات متعثرة',
        count: stalledCount,
        autoScrollRatio: 1.0,
        autoScrollDurationSeconds: 200,
      ),
      _StatusColumn(
        color: const Color(0xFF3E3117).withValues(alpha: 0.56),
        accent: const Color(0xFFE7C86D),
        items: _listOf('blockedOrders'),
        controller: _blockedController,
        title: 'طلبات عالقة',
        count: blockedCount,
        autoScrollRatio: 1.0,
        autoScrollDurationSeconds: 220,
      ),
      _StatusColumn(
        color: const Color(0xFF1E3D2B).withValues(alpha: 0.52),
        accent: const Color(0xFF84DDA4),
        items: _listOf('readyForDeliveryOrders'),
        controller: _readyController,
        title: 'جاهز للتسليم',
        count: readyCount,
        autoScrollRatio: 1.0,
        autoScrollDurationSeconds: 150,
      ),
    ];

    final isLoading = _loading && _dashboard.isEmpty;
    final lastUpdatedText = lastUpdated is String
        ? lastUpdated
        : lastUpdated != null
            ? lastUpdated.toString()
            : '—';

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Theme(
        data: AppTheme.dark(),
        child: Scaffold(
          backgroundColor: const Color(0xFF07131E),
          body: SafeArea(
            child: isLoading
                ? const Center(
                    child: CircularProgressIndicator(color: UiPalette.primaryBlue),
                  )
                : Column(
                    children: [
                      Container(
                        padding: const EdgeInsets.fromLTRB(14, 10, 14, 10),
                        decoration: const BoxDecoration(
                          color: Color(0xFF0B1720),
                          border: Border(
                            bottom: BorderSide(
                              color: Color(0xFF1E2D39),
                              width: 1,
                            ),
                          ),
                        ),
                        child: Row(
                          children: [
                            _StatChip(
                              label: 'في خطر',
                              value: riskCount,
                              accent: const Color(0xFFFF5A5A),
                            ),
                            const SizedBox(width: 8),
                            _StatChip(
                              label: 'متعثر',
                              value: stalledCount,
                              accent: const Color(0xFFFFA35D),
                            ),
                            const SizedBox(width: 8),
                            _StatChip(
                              label: 'عالقة',
                              value: blockedCount,
                              accent: const Color(0xFFE7C86D),
                            ),
                            const SizedBox(width: 8),
                            _StatChip(
                              label: 'جاهز',
                              value: readyCount,
                              accent: const Color(0xFF84DDA4),
                            ),
                            const Spacer(),
                            _StatChip(
                              label: 'نسبة الإنجاز',
                              value: '${overallProgress.toStringAsFixed(0)}%',
                              accent: const Color(0xFF8BD0FF),
                              compact: false,
                            ),
                          ],
                        ),
                      ),
                      Expanded(
                        child: Stack(
                          children: [
                            Row(
                              children: [
                                for (int i = 0; i < columns.length; i++) ...[
                                  Expanded(child: columns[i]),
                                  if (i < columns.length - 1)
                                    const SizedBox(width: 0),
                                ],
                              ],
                            ),
                            if (_errorMessage != null)
                              Positioned(
                                top: 12,
                                left: 12,
                                child: Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 10,
                                    vertical: 6,
                                  ),
                                  decoration: BoxDecoration(
                                    color: Colors.black.withValues(alpha: 0.25),
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Text(
                                    _errorMessage!,
                                    style: AppTypography.caption.copyWith(
                                      color: Colors.white,
                                    ),
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                      Container(
                        height: 32,
                        alignment: Alignment.center,
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        decoration: const BoxDecoration(
                          color: Color(0xFF0B1720),
                          border: Border(
                            top: BorderSide(
                              color: Color(0xFF1E2D39),
                              width: 1,
                            ),
                          ),
                        ),
                        child: Text(
                          'آخر تحديث: $lastUpdatedText',
                          style: AppTypography.caption.copyWith(
                            color: const Color(0xFFB7C7DA),
                          ),
                        ),
                      ),
                    ],
                  ),
          ),
        ),
      ),
    );
  }
}

class _StatChip extends StatelessWidget {
  const _StatChip({
    required this.label,
    required this.value,
    required this.accent,
    this.compact = true,
  });

  final String label;
  final dynamic value;
  final Color accent;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: compact ? 10 : 12,
        vertical: compact ? 7 : 8,
      ),
      decoration: BoxDecoration(
        color: accent.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: accent.withValues(alpha: 0.25), width: 1),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            '$label:',
            style: AppTypography.caption.copyWith(
              color: Colors.white.withValues(alpha: 0.82),
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(width: 6),
          Text(
            value.toString(),
            style: AppTypography.caption.copyWith(
              color: accent,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusColumn extends StatefulWidget {
  const _StatusColumn({
    required this.title,
    required this.color,
    required this.accent,
    required this.items,
    required this.controller,
    required this.count,
    required this.autoScrollRatio,
    required this.autoScrollDurationSeconds,
  });

  final String title;
  final Color color;
  final Color accent;
  final List<Map<String, dynamic>> items;
  final ScrollController controller;
  final int count;
  final double autoScrollRatio;
  final int autoScrollDurationSeconds;

  @override
  State<_StatusColumn> createState() => _StatusColumnState();
}

class _StatusColumnState extends State<_StatusColumn> {
  late final List<Map<String, dynamic>> _repeatedItems;
  Timer? _resumeTimer;
  bool _isUserDragging = false;

  @override
  void initState() {
    super.initState();
    _repeatedItems = [...widget.items, ...widget.items];
    WidgetsBinding.instance.addPostFrameCallback((_) => _startContinuousScroll());
  }

  @override
  void dispose() {
    _resumeTimer?.cancel();
    super.dispose();
  }

  void _scheduleResume() {
    _resumeTimer?.cancel();
    _resumeTimer = Timer(const Duration(milliseconds: 1400), () {
      if (!mounted || _isUserDragging) return;
      _startContinuousScroll();
    });
  }

  void _startContinuousScroll() {
    if (!mounted || !widget.controller.hasClients || _isUserDragging) return;

    final max = widget.controller.position.maxScrollExtent;
    if (max <= 0) return;

    final target = max * 1.0;
    widget.controller.animateTo(
      target,
      duration: Duration(seconds: widget.autoScrollDurationSeconds),
      curve: Curves.linear,
    ).then((_) {
      if (!mounted || !widget.controller.hasClients || _isUserDragging) return;
      widget.controller.jumpTo(0);
      _scheduleResume();
    });
  }

  bool _isManualScrollNotification(ScrollNotification notification) {
    if (notification is ScrollStartNotification) {
      return notification.dragDetails != null;
    }
    if (notification is ScrollUpdateNotification) {
      return notification.dragDetails != null;
    }
    return false;
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: widget.color,
      ),
      child: Column(
        children: [
          SizedBox(
            height: 66,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(
                  widget.title,
                  style: AppTypography.title.copyWith(
                    color: widget.accent,
                    fontSize: 23,
                    letterSpacing: 0.8,
                  ),
                ),
                const SizedBox(width: 8),
                Container(
                  width: 32,
                  height: 32,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: Colors.black.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(9),
                  ),
                  child: Text(
                    widget.count.toString(),
                    style: AppTypography.caption.copyWith(
                      color: widget.accent,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: NotificationListener<ScrollNotification>(
              onNotification: (notification) {
                if (_isManualScrollNotification(notification)) {
                  _isUserDragging = true;
                  _resumeTimer?.cancel();
                } else if (notification is ScrollEndNotification) {
                  _isUserDragging = false;
                  _scheduleResume();
                }
                return false;
              },
              child: _repeatedItems.isEmpty
                  ? const Center(
                      child: Icon(Icons.inbox_rounded, color: Colors.white54, size: 34),
                    )
                  : ListView.builder(
                      controller: widget.controller,
                      physics: const BouncingScrollPhysics(
                        parent: AlwaysScrollableScrollPhysics(),
                      ),
                      padding: const EdgeInsets.fromLTRB(10, 0, 10, 10),
                      itemCount: _repeatedItems.length,
                      itemBuilder: (context, index) {
                        final order = _repeatedItems[index];
                        return _OrderCard(order: order, accent: widget.accent);
                      },
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

class _OrderCard extends StatelessWidget {
  const _OrderCard({required this.order, required this.accent});

  final Map<String, dynamic> order;
  final Color accent;

  String _value(String field, {String fallback = '-'}) {
    final value = order[field];
    if (value == null || value.toString().trim().isEmpty) return fallback;
    return value.toString();
  }

  String _formatDate(dynamic value) {
    if (value == null) return '—';
    try {
      final date = DateTime.tryParse(value.toString());
      if (date == null) return value.toString();
      return '${date.day}/${date.month}/${date.year}';
    } catch (_) {
      return value.toString();
    }
  }

  @override
  Widget build(BuildContext context) {
    final orderNumber = _value('orderNumber', fallback: 'طلب #${order['orderId']}');
    final customer = _value('customerName', fallback: _value('customerCode', fallback: 'غير محدد'));
    final deliveryDate = _formatDate(order['deliveryDate']);
    final daysRemaining = _value('daysRemaining', fallback: '0');
    final totalPieces = _value('totalPieces', fallback: '0');
    final completedPieces = _value('completedPieces', fallback: '0');
    final incompletePieces = _value('incompletePieces', fallback: '0');
    final delayPiece = _value('delayedPieceCode', fallback: '—');
    final delayStage = _value('delayedCurrentStage', fallback: '—');
    final lastEmployee = _value('lastEmployeeCode', fallback: '—');
    final lastUpdated = _formatDate(order['lastUpdatedAt']);
    final progress = (order['progressPercent'] as num?)?.toInt() ?? 0;

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: Colors.black.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: accent.withValues(alpha: 0.18), width: 1),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                child: Text(
                  orderNumber,
                  overflow: TextOverflow.ellipsis,
                  style: AppTypography.title.copyWith(
                    color: Colors.white,
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: accent.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  '$daysRemaining يوم',
                  style: AppTypography.caption.copyWith(
                    color: accent,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            customer,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTypography.body.copyWith(
              color: Colors.white.withValues(alpha: 0.8),
              fontSize: 11,
            ),
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              Expanded(
                child: Text(
                  'تسليم: $deliveryDate',
                  style: AppTypography.caption.copyWith(
                    color: Colors.white.withValues(alpha: 0.7),
                    fontSize: 9,
                  ),
                ),
              ),
              Text(
                '$progress%',
                style: AppTypography.caption.copyWith(
                  color: accent,
                  fontWeight: FontWeight.w800,
                  fontSize: 9,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              _MiniStat(label: 'قطع', value: totalPieces),
              const SizedBox(width: 6),
              _MiniStat(label: 'مكتمل', value: completedPieces),
              const SizedBox(width: 6),
              _MiniStat(label: 'غير مكتمل', value: incompletePieces),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            'القطعة: $delayPiece • المرحلة: $delayStage',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTypography.caption.copyWith(
              color: Colors.white.withValues(alpha: 0.72),
              fontSize: 9,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'آخر موظف: $lastEmployee • آخر تحديث: $lastUpdated',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppTypography.caption.copyWith(
              color: Colors.white.withValues(alpha: 0.65),
              fontSize: 9,
            ),
          ),
          if (progress > 0) ...[
            const SizedBox(height: 8),
            ClipRRect(
              borderRadius: BorderRadius.circular(999),
              child: LinearProgressIndicator(
                minHeight: 4,
                value: (progress / 100).clamp(0.0, 1.0),
                backgroundColor: Colors.white.withValues(alpha: 0.07),
                valueColor: AlwaysStoppedAnimation<Color>(accent),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.04),
          borderRadius: BorderRadius.circular(6),
        ),
        child: Text(
          '$label: $value',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: AppTypography.caption.copyWith(
            color: Colors.white.withValues(alpha: 0.7),
            fontSize: 8,
          ),
        ),
      ),
    );
  }
}
