import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';

const _apiBaseUrl = String.fromEnvironment(
  'LUMAR_API_URL',
  defaultValue: 'http://127.0.0.1:5093',
);

class MessagesScreen extends StatefulWidget {
  const MessagesScreen({super.key});

  @override
  State<MessagesScreen> createState() => _MessagesScreenState();
}

class _MessagesScreenState extends State<MessagesScreen> {
  late Future<List<CustomerMessageRecord>> _future;
  String _query = '';
  String _selectedFilter = 'الكل';

  @override
  void initState() {
    super.initState();
    _future = MessagesRepository().getCustomerMessages();
  }

  Future<void> _refresh() async {
    setState(() {
      _future = MessagesRepository().getCustomerMessages();
    });
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.primary,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
        title: const Text('مركز إدارة الرسائل'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: FutureBuilder<List<CustomerMessageRecord>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return _ErrorState(message: snapshot.error.toString());
          }

          final items = snapshot.data ?? const <CustomerMessageRecord>[];
          final filtered = _filterItems(items, _query, _selectedFilter);

          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _SummaryCards(messages: items),
                const SizedBox(height: 16),
                _SearchBar(
                  value: _query,
                  onChanged: (value) => setState(() => _query = value),
                ),
                const SizedBox(height: 12),
                _FilterChips(
                  selected: _selectedFilter,
                  onSelected: (value) => setState(() => _selectedFilter = value),
                ),
                const SizedBox(height: 12),
                if (filtered.isEmpty)
                  _EmptyState(query: _query)
                else
                  ...filtered.map(
                    (message) => Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _MessageCard(message: message),
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }

  List<CustomerMessageRecord> _filterItems(
    List<CustomerMessageRecord> items,
    String query,
    String selectedFilter,
  ) {
    final normalizedQuery = query.trim().toLowerCase();
    return items.where((message) {
      final matchesFilter = selectedFilter == 'الكل' ||
          _normalizeStatus(message.deliveryStatus) == selectedFilter;
      final matchesQuery = normalizedQuery.isEmpty ||
          message.title.toLowerCase().contains(normalizedQuery) ||
          message.body.toLowerCase().contains(normalizedQuery) ||
          message.channel.toLowerCase().contains(normalizedQuery) ||
          message.eventType.toLowerCase().contains(normalizedQuery);
      return matchesFilter && matchesQuery;
    }).toList();
  }
}

class MessagesRepository {
  MessagesRepository({http.Client? client}) : _client = client ?? http.Client();

  final http.Client _client;

  Future<List<CustomerMessageRecord>> getCustomerMessages() async {
    final response = await _client.get(
      Uri.parse('$_apiBaseUrl/messages/customer-messages'),
      headers: {'Accept': 'application/json'},
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذرت قراءة الرسائل (${response.statusCode})');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      return const <CustomerMessageRecord>[];
    }

    final items = decoded.whereType<Map<String, dynamic>>();
    return items.map(CustomerMessageRecord.fromJson).toList();
  }
}

class CustomerMessageRecord {
  const CustomerMessageRecord({
    required this.id,
    required this.customerId,
    required this.orderId,
    required this.eventType,
    required this.templateKey,
    required this.channel,
    required this.title,
    required this.body,
    required this.deliveryStatus,
    required this.createdAtUtc,
    required this.sentAtUtc,
    required this.readAtUtc,
    required this.attemptCount,
    required this.deliveredAtUtc,
    required this.processingAtUtc,
    required this.failureReason,
    required this.providerMessageId,
    required this.phoneNumber,
    required this.referenceId,
    required this.referenceType,
    required this.idempotencyKey,
  });

  factory CustomerMessageRecord.fromJson(Map<String, dynamic> json) => CustomerMessageRecord(
        id: json['customerMessageId'] as int? ?? 0,
        customerId: json['customerId'] as int? ?? 0,
        orderId: json['orderId'] as int?,
        eventType: json['eventType'] as String? ?? '',
        templateKey: json['templateKey'] as String? ?? '',
        channel: json['channel'] as String? ?? '',
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
        deliveryStatus: json['deliveryStatus'] as String? ?? '',
        createdAtUtc: _dateFromJson(json['createdAtUtc']),
        sentAtUtc: json['sentAtUtc'] == null ? null : _dateFromJson(json['sentAtUtc']),
        readAtUtc: json['readAtUtc'] == null ? null : _dateFromJson(json['readAtUtc']),
        attemptCount: json['attemptCount'] as int? ?? 0,
        deliveredAtUtc: json['deliveredAtUtc'] == null ? null : _dateFromJson(json['deliveredAtUtc']),
        processingAtUtc: json['processingAtUtc'] == null ? null : _dateFromJson(json['processingAtUtc']),
        failureReason: json['failureReason'] as String?,
        providerMessageId: json['providerMessageId'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        referenceId: json['referenceId'] as String?,
        referenceType: json['referenceType'] as String?,
        idempotencyKey: json['idempotencyKey'] as String?,
      );

  final int id;
  final int customerId;
  final int? orderId;
  final String eventType;
  final String templateKey;
  final String channel;
  final String title;
  final String body;
  final String deliveryStatus;
  final DateTime createdAtUtc;
  final DateTime? sentAtUtc;
  final DateTime? readAtUtc;
  final int attemptCount;
  final DateTime? deliveredAtUtc;
  final DateTime? processingAtUtc;
  final String? failureReason;
  final String? providerMessageId;
  final String? phoneNumber;
  final String? referenceId;
  final String? referenceType;
  final String? idempotencyKey;

  String get displayStatus => _statusLabel(deliveryStatus);
  Color get statusColor => _statusColor(deliveryStatus);
}

DateTime _dateFromJson(Object? value) {
  if (value == null || value == '') return DateTime.fromMillisecondsSinceEpoch(0);
  return DateTime.tryParse(value.toString()) ?? DateTime.fromMillisecondsSinceEpoch(0);
}

String _normalizeStatus(String value) {
  final normalized = value.trim();
  if (normalized.isEmpty) return 'الكل';
  final lower = normalized.toLowerCase();
  if (lower.contains('sent') || lower.contains('مكتمل') || lower.contains('تم الإرسال')) return 'تم الإرسال';
  if (lower.contains('delivered') || lower.contains('تم التسليم') || lower.contains('deliv')) return 'تم التسليم';
  if (lower.contains('read') || lower.contains('مقروء')) return 'مقروء';
  if (lower.contains('queued') || lower.contains('pending') || lower.contains('processing') || lower.contains('waiting')) return 'قيد التنفيذ';
  if (lower.contains('failed') || lower.contains('error') || lower.contains('فشل')) return 'فشل';
  return normalized;
}

String _statusLabel(String value) {
  final normalized = _normalizeStatus(value);
  switch (normalized) {
    case 'تم الإرسال':
      return 'تم الإرسال';
    case 'تم التسليم':
      return 'تم التسليم';
    case 'مقروء':
      return 'مقروء';
    case 'قيد التنفيذ':
      return 'قيد التنفيذ';
    case 'فشل':
      return 'فشل';
    default:
      return normalized.isEmpty ? 'غير محدد' : normalized;
  }
}

Color _statusColor(String value) {
  switch (_normalizeStatus(value)) {
    case 'تم الإرسال':
      return const Color(0xFF2FA5D9);
    case 'تم التسليم':
      return const Color(0xFF2E9D5A);
    case 'مقروء':
      return const Color(0xFF5C6BC0);
    case 'قيد التنفيذ':
      return const Color(0xFFE5B93A);
    case 'فشل':
      return const Color(0xFFD64B4B);
    default:
      return UiPalette.primary;
  }
}

class _SummaryCards extends StatelessWidget {
  const _SummaryCards({required this.messages});

  final List<CustomerMessageRecord> messages;

  @override
  Widget build(BuildContext context) {
    final total = messages.length;
    final sent = messages.where((m) => _normalizeStatus(m.deliveryStatus) == 'تم الإرسال').length;
    final delivered = messages.where((m) => _normalizeStatus(m.deliveryStatus) == 'تم التسليم').length;
    final failed = messages.where((m) => _normalizeStatus(m.deliveryStatus) == 'فشل').length;

    final items = [
      _MetricCard(title: 'إجمالي الرسائل', value: '$total', color: UiPalette.primary),
      _MetricCard(title: 'تم الإرسال', value: '$sent', color: const Color(0xFF2FA5D9)),
      _MetricCard(title: 'تم التسليم', value: '$delivered', color: const Color(0xFF2E9D5A)),
      _MetricCard(title: 'الفشل', value: '$failed', color: const Color(0xFFD64B4B)),
    ];

    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: items.map((item) => SizedBox(width: 150, child: item)).toList(),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({required this.title, required this.value, required this.color});

  final String title;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final textColor = UiPalette.adaptiveTextColor(color);
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: TextStyle(color: textColor, fontSize: 13, fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          Text(value, style: TextStyle(color: textColor, fontSize: 28, fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }
}

class _SearchBar extends StatelessWidget {
  const _SearchBar({required this.value, required this.onChanged});

  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: TextEditingController(text: value)..selection = TextSelection.collapsed(offset: value.length),
      onChanged: onChanged,
      textInputAction: TextInputAction.search,
      decoration: InputDecoration(
        hintText: 'البحث في الرسائل',
        hintStyle: const TextStyle(color: UiPalette.textSoft),
        filled: true,
        fillColor: UiPalette.surfaceCard,
        prefixIcon: const Icon(Icons.search_rounded, color: UiPalette.textSoft),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide.none,
        ),
      ),
      style: const TextStyle(color: UiPalette.textMain),
    );
  }
}

class _FilterChips extends StatelessWidget {
  const _FilterChips({required this.selected, required this.onSelected});

  final String selected;
  final ValueChanged<String> onSelected;

  static const List<String> _labels = ['الكل', 'قيد التنفيذ', 'تم الإرسال', 'تم التسليم', 'مقروء', 'فشل'];

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: _labels.map((label) {
        final isSelected = selected == label;
        return ChoiceChip(
          label: Text(label),
          selected: isSelected,
          onSelected: (_) => onSelected(label),
          selectedColor: UiPalette.primary,
          backgroundColor: UiPalette.surfaceCard,
          labelStyle: TextStyle(
            color: isSelected ? UiPalette.adaptiveTextColor(UiPalette.primary) : UiPalette.textMain,
            fontWeight: FontWeight.w600,
          ),
        );
      }).toList(),
    );
  }
}

class _MessageCard extends StatelessWidget {
  const _MessageCard({required this.message});

  final CustomerMessageRecord message;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: UiPalette.surfaceCard,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => AppNavigation.push(context, (_) => _MessageDetailScreen(message: message)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      message.title.isEmpty ? 'رسالة بدون عنوان' : message.title,
                      style: const TextStyle(color: UiPalette.textMain, fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: message.statusColor.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Text(
                      message.displayStatus,
                      style: TextStyle(color: message.statusColor, fontWeight: FontWeight.w700),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                message.body,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(color: UiPalette.textSoft, height: 1.5),
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  _MetaChip(icon: Icons.schedule_rounded, label: _formatDate(message.createdAtUtc)),
                  _MetaChip(icon: Icons.campaign_rounded, label: message.channel.isEmpty ? 'غير محدد' : message.channel),
                  _MetaChip(icon: Icons.tag_rounded, label: message.eventType.isEmpty ? 'عام' : message.eventType),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _MetaChip extends StatelessWidget {
  const _MetaChip({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      decoration: BoxDecoration(
        color: UiPalette.primary.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: UiPalette.primary),
          const SizedBox(width: 6),
          Text(label, style: const TextStyle(color: UiPalette.textMain, fontSize: 12)),
        ],
      ),
    );
  }
}

class _MessageDetailScreen extends StatelessWidget {
  const _MessageDetailScreen({required this.message});

  final CustomerMessageRecord message;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.primary,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
        title: const Text('تفاصيل الرسالة'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Container(
            padding: const EdgeInsets.all(18),
            decoration: BoxDecoration(
              color: UiPalette.primary,
              borderRadius: BorderRadius.circular(18),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  message.title.isEmpty ? 'رسالة بدون عنوان' : message.title,
                  style: TextStyle(
                    color: UiPalette.adaptiveTextColor(UiPalette.primary),
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'النوع: ${message.eventType.isEmpty ? 'عام' : message.eventType}',
                  style: TextStyle(color: UiPalette.adaptiveTextColor(UiPalette.primary)),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          _DetailSection(title: 'النص', content: message.body),
          const SizedBox(height: 12),
          _DetailSection(title: 'الحالة', content: message.displayStatus),
          const SizedBox(height: 12),
          _DetailSection(title: 'القناة', content: message.channel.isEmpty ? 'غير محدد' : message.channel),
          const SizedBox(height: 12),
          _DetailSection(title: 'الرقم', content: message.phoneNumber ?? 'غير متوفر'),
          const SizedBox(height: 12),
          _DetailSection(title: 'المعرف', content: message.providerMessageId ?? 'غير متوفر'),
          const SizedBox(height: 12),
          _DetailSection(title: 'تاريخ الإنشاء', content: _formatDate(message.createdAtUtc)),
          const SizedBox(height: 12),
          _DetailSection(title: 'تاريخ الإرسال', content: message.sentAtUtc == null ? 'غير مرسلة' : _formatDate(message.sentAtUtc!)),
          const SizedBox(height: 12),
          _DetailSection(title: 'تاريخ التسليم', content: message.deliveredAtUtc == null ? 'غير مسلمة' : _formatDate(message.deliveredAtUtc!)),
          const SizedBox(height: 12),
          if (message.failureReason != null && message.failureReason!.isNotEmpty)
            _DetailSection(title: 'سبب الفشل', content: message.failureReason!),
        ],
      ),
    );
  }
}

class _DetailSection extends StatelessWidget {
  const _DetailSection({required this.title, required this.content});

  final String title;
  final String content;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(color: UiPalette.textSoft, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          Text(content, style: const TextStyle(color: UiPalette.textMain, height: 1.7)),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.query});

  final String query;

  @override
  Widget build(BuildContext context) {
    final text = query.isEmpty ? 'لا توجد رسائل حالياً.' : 'لا توجد نتائج للبحث الحالي.';
    return Container(
      margin: const EdgeInsets.only(top: 12),
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Center(
        child: Text(
          text,
          style: const TextStyle(color: UiPalette.textMain),
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Text(
          'تعذرت قراءة الرسائل.\n$message',
          style: const TextStyle(color: UiPalette.textMain),
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}

String _formatDate(DateTime date) {
  if (date.millisecondsSinceEpoch == 0) return 'غير محدد';
  return '${date.day}/${date.month}/${date.year} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
}
