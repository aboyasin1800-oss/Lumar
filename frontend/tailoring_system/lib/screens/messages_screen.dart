import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/app_navigation.dart';
import '../core/ui_palette.dart';

const _apiBaseUrl = String.fromEnvironment(
  'LUMAR_API_URL',
  defaultValue: 'http://127.0.0.1:5093',
);

const Map<String, String> _templateDescriptions = {
  'order-created-customer-smart': 'يُستخدم عند تسجيل طلب جديد، ويرسل للعميل بيانات طلبه والنقاط والمعلومات المرتبطة بالعملية.',
  'referrer-points-earned-smart': 'يُستخدم لإبلاغ المعرّف بالنقاط التي حصل عليها عندما يستحق نقاطاً نتيجة تعريف عميل على المؤسسة.',
  'order-referrer-combined-smart': 'يُستخدم عندما يكون رقم هاتف العميل ورقم هاتف المعرّف متطابقين، ويجمع بيانات العميل والمعرّف في رسالة واحدة.',
  'friday-greeting': 'يُرسل تلقائياً يوم الجمعة للعملاء المؤهلين في النظام لإرسال تحية الجمعة الأسبوعية من مؤسسة لومــــــVIPـــــار.',
  'general-campaign-smart': 'يُستخدم لإنشاء رسائل الإعلانات والعروض والتهاني والمناسبات وإرسالها يدوياً إلى العملاء المستهدفين، مع دعم النص والصورة.',
  'order-ready': 'يُرسل للعميل عندما يصبح طلبه جاهزاً للاستلام.',
  'order-delivered': 'يُرسل للعميل بعد تسجيل تسليم الطلب بنجاح.',
  'order-started': 'يُرسل للعميل عند بدء العمل الفعلي على طلبه.',
  'order-created': 'قالب تسجيل طلب تفصيل، ويُستخدم لإبلاغ العميل بإنشاء طلبه وفق المسار المرتبط به.',
  'OrderCreated': 'قالب تشغيلي مرتبط بحدث إنشاء الطلب، ويجب مراجعة ارتباطه قبل تعطيله أو استبداله.',
  'ready-made-sale': 'يُرسل للعميل عند إتمام عملية بيع منتج جاهز.',
  'loyalty-welcome': 'يُرسل للعميل عند تفعيل حسابه في نظام النقاط والولاء.',
  'loyalty-earned': 'يُرسل للعميل عندما تضاف نقاط ولاء جديدة إلى حسابه.',
  'referral-earned': 'قالب عربي سابق لاكتساب نقاط التعريف، ويُعرض للتوثيق مع بيان حالته التشغيلية الفعلية.',
  'ReferralPointsEarned': 'قالب تشغيلي مرتبط بحدث اكتساب نقاط التعريف، ويجب توضيح ارتباطه الحالي قبل تعطيله.',
  'welcome': 'قالب ترحيب تاريخي باللغة الإنجليزية. لا يُستخدم كنص عربي نهائي إلا إذا بقي مرتبطاً بمسار تشغيلي قديم.',
  'referral': 'قالب تاريخي باللغة الإنجليزية لرسائل التعريف، ويجب الحفاظ عليه للمرجعية حتى اكتمال الانتقال الآمن.',
  'loyalty': 'قالب تاريخي باللغة الإنجليزية لرسائل الولاء، ويُعرض للمرجعية ولا يُعتمد كنص عربي نهائي.',
  'reward': 'قالب تاريخي باللغة الإنجليزية للمكافآت، ويُحافظ عليه حتى إثبات عدم وجود استخدام تشغيلي له.',
  'ready': 'قالب تاريخي باللغة الإنجليزية لجاهزية الطلب، ويُحافظ عليه للمرجعية حتى اكتمال الانتقال الآمن.',
  'delivery': 'قالب تاريخي باللغة الإنجليزية لتسليم الطلب، ويُحافظ عليه للمرجعية حتى اكتمال الانتقال الآمن.',
};

String _templateDescriptionFor(String? key) {
  final normalized = (key ?? '').trim();
  if (normalized.isEmpty) {
    return 'لا يتوفر تعريف تشغيلي موثق لهذا القالب حالياً. راجع مفتاح القالب وارتباطه بالأحداث قبل تعديله أو تعطيله.';
  }

  return _templateDescriptions[normalized] ??
      'لا يتوفر تعريف تشغيلي موثق لهذا القالب حالياً. راجع مفتاح القالب وارتباطه بالأحداث قبل تعديله أو تعطيله.';
}

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

  void _showManualCampaignSheet(BuildContext context) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (sheetContext) => SafeArea(
        child: Container(
          constraints: BoxConstraints(
            maxHeight: MediaQuery.of(sheetContext).size.height * 0.9,
          ),
          decoration: const BoxDecoration(
            color: UiPalette.screenBackground,
            borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
          ),
          child: const _ManualCampaignSheet(),
        ),
      ),
    );
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
            tooltip: 'إنشاء حملة',
            onPressed: () => _showManualCampaignSheet(context),
            icon: const Icon(Icons.campaign_rounded),
          ),
          IconButton(
            tooltip: 'قوالب الرسائل',
            onPressed: () => AppNavigation.push(context, (_) => const MessageTemplatesScreen()),
            icon: const Icon(Icons.library_books_rounded),
          ),
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

  Future<ManualCampaignResult> createManualCampaign({
    required ManualCampaignRequest request,
  }) async {
    final response = await _client.post(
      Uri.parse('$_apiBaseUrl/messages/manual-campaigns'),
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
      body: jsonEncode(request.toJson()),
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      final payload = jsonDecode(response.body);
      String? extractSafeMessage(Object? value) {
        if (value is Map<String, dynamic>) {
          final candidates = [
            value['message'],
            value['detail'],
            value['title'],
            value['errors'],
          ];
          for (final candidate in candidates) {
            if (candidate is String && candidate.trim().isNotEmpty) return candidate.trim();
            if (candidate is Map && candidate.values.isNotEmpty) {
              for (final nested in candidate.values) {
                if (nested is String && nested.trim().isNotEmpty) return nested.trim();
                if (nested is List) {
                  for (final item in nested) {
                    if (item is String && item.trim().isNotEmpty) return item.trim();
                  }
                }
              }
            }
            if (candidate is List) {
              for (final item in candidate) {
                if (item is String && item.trim().isNotEmpty) return item.trim();
                if (item is Map && item.values.isNotEmpty) {
                  for (final nested in item.values) {
                    if (nested is String && nested.trim().isNotEmpty) return nested.trim();
                  }
                }
              }
            }
          }
        }
        return null;
      }

      final safeMessage = extractSafeMessage(payload);
      throw Exception(
        safeMessage ?? 'فشل طلب الحملة (HTTP ${response.statusCode})',
      );
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('استجابة الحملة غير صالحة.');
    }

    return ManualCampaignResult.fromJson(decoded);
  }
}

enum ManualCampaignRecipientMode {
  singleCustomer(0),
  allEligibleCustomers(1);

  const ManualCampaignRecipientMode(this.value);

  final int value;

  static ManualCampaignRecipientMode fromValue(int value) {
    switch (value) {
      case 0:
        return ManualCampaignRecipientMode.singleCustomer;
      case 1:
        return ManualCampaignRecipientMode.allEligibleCustomers;
      default:
        throw ArgumentError.value(value, 'value', 'RecipientMode must be 0 or 1.');
    }
  }
}

class ManualCampaignRequest {
  const ManualCampaignRequest({
    required this.campaignReference,
    required this.recipientMode,
    this.customerId,
    required this.messageText,
    required this.templateKey,
    required this.title,
  });

  final String campaignReference;
  final ManualCampaignRecipientMode recipientMode;
  final int? customerId;
  final String messageText;
  final String templateKey;
  final String title;

  Map<String, dynamic> toJson() => {
        'campaignReference': campaignReference,
        'recipientMode': recipientMode.value,
        if (customerId != null) 'customerId': customerId,
        'messageText': messageText,
        'templateKey': templateKey,
        'title': title,
      };
}

class ManualCampaignResult {
  const ManualCampaignResult({
    required this.createdCount,
    required this.skippedCount,
    required this.duplicateCount,
    required this.customerIds,
  });

  factory ManualCampaignResult.fromJson(Map<String, dynamic> json) {
    final ids = (json['customerIds'] as List? ?? const [])
        .whereType<num>()
        .map((item) => item.toInt())
        .toList();

    return ManualCampaignResult(
      createdCount: (json['createdCount'] as num?)?.toInt() ?? 0,
      skippedCount: (json['skippedCount'] as num?)?.toInt() ?? 0,
      duplicateCount: (json['duplicateCount'] as num?)?.toInt() ?? 0,
      customerIds: ids,
    );
  }

  final int createdCount;
  final int skippedCount;
  final int duplicateCount;
  final List<int> customerIds;
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

class _ManualCampaignSheet extends StatefulWidget {
  const _ManualCampaignSheet();

  @override
  State<_ManualCampaignSheet> createState() => _ManualCampaignSheetState();
}

class _ManualCampaignSheetState extends State<_ManualCampaignSheet> {
  static const _campaignTemplateKey = 'general-campaign-smart';

  final TextEditingController _titleController = TextEditingController(text: 'حملة إعلانية');
  final TextEditingController _messageController = TextEditingController();
  final TextEditingController _customerSearchController = TextEditingController();

  String _recipientMode = 'SingleCustomer';
  bool _confirmation = false;
  bool _isSubmitting = false;
  bool _isSearchingCustomers = false;
  List<_CustomerSearchResult> _customerResults = const [];
  _CustomerSearchResult? _selectedCustomer;

  void _refreshCampaignUiState() {
    if (mounted) {
      setState(() {});
    }
  }

  @override
  void initState() {
    super.initState();
    _titleController.addListener(_refreshCampaignUiState);
    _messageController.addListener(_refreshCampaignUiState);
  }

  @override
  void dispose() {
    _titleController.removeListener(_refreshCampaignUiState);
    _messageController.removeListener(_refreshCampaignUiState);
    _titleController.dispose();
    _messageController.dispose();
    _customerSearchController.dispose();
    super.dispose();
  }

  Future<void> _searchCustomers() async {
    final term = _customerSearchController.text.trim();
    if (term.isEmpty) {
      setState(() => _customerResults = const []);
      return;
    }

    setState(() => _isSearchingCustomers = true);
    try {
      final uri = Uri.parse('$_apiBaseUrl/customers/search')
          .replace(queryParameters: {'term': term});
      final response = await http.get(uri, headers: {'Accept': 'application/json'});
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('تعذر البحث عن العملاء.');
      }

      final decoded = jsonDecode(response.body);
      final values = decoded is List
          ? decoded.whereType<Map<String, dynamic>>().toList()
          : <Map<String, dynamic>>[];
      setState(() {
        _customerResults = values
            .map(_CustomerSearchResult.fromJson)
            .where((item) => item.phoneNumber.trim().isNotEmpty)
            .toList();
      });
    } catch (_) {
      setState(() => _customerResults = const []);
    } finally {
      if (mounted) {
        setState(() => _isSearchingCustomers = false);
      }
    }
  }

  Future<void> _submitCampaign() async {
    if (_isSubmitting) return;

    if (_recipientMode == 'SingleCustomer' && _selectedCustomer == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اختر عميل واحد أولاً من المصدر الرسمي.')),
      );
      return;
    }

    if (_recipientMode == 'AllEligibleCustomers' && !_confirmation) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('يجب تأكيد الحملة على جميع العملاء المؤهلين أولاً.')),
      );
      return;
    }

    final messageText = _messageController.text.trim();
    if (messageText.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('اكتب نص الحملة قبل التنفيذ.')),
      );
      return;
    }

    setState(() => _isSubmitting = true);
    try {
      final result = await MessagesRepository().createManualCampaign(
        request: ManualCampaignRequest(
          campaignReference: 'manual-campaign-${DateTime.now().millisecondsSinceEpoch}',
          recipientMode: _recipientMode == 'SingleCustomer'
              ? ManualCampaignRecipientMode.singleCustomer
              : ManualCampaignRecipientMode.allEligibleCustomers,
          customerId: _recipientMode == 'SingleCustomer' ? _selectedCustomer!.customerId : null,
          messageText: messageText,
          templateKey: _campaignTemplateKey,
          title: _titleController.text.trim().isEmpty ? 'حملة إعلانية' : _titleController.text.trim(),
        ),
      );

      if (!mounted) return;
      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            result.createdCount > 0
                ? 'تم إنشاء الحملة بنجاح. تم إرسال ${result.createdCount} عميل.'
                : result.skippedCount > 0
                    ? 'تمت معالجة الحملة، لكن لا يوجد عملاء مؤهلون للتمرير.'
                    : 'تمت معالجة الحملة بنجاح.',
          ),
          backgroundColor: const Color(0xFF2E9D5A),
        ),
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(error.toString().replaceFirst('Exception: ', '')),
            backgroundColor: const Color(0xFFD64B4B),
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isSubmitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final previewText = _messageController.text.trim().isEmpty
        ? 'سيظهر نص الحملة هنا قبل التنفيذ.'
        : _messageController.text.trim();

    return Padding(
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'إنشاء حملة',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w800),
                  ),
                ),
                IconButton(
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.close_rounded),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              'نوع المستهدف',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ChoiceChip(
                  label: const Text('عميل واحد'),
                  selected: _recipientMode == 'SingleCustomer',
                  onSelected: (_) => setState(() => _recipientMode = 'SingleCustomer'),
                ),
                ChoiceChip(
                  label: const Text('جميع العملاء المؤهلين'),
                  selected: _recipientMode == 'AllEligibleCustomers',
                  onSelected: (_) => setState(() => _recipientMode = 'AllEligibleCustomers'),
                ),
              ],
            ),
            const SizedBox(height: 18),
            if (_recipientMode == 'SingleCustomer') ...[
              Text(
                'بحث العميل الرسمي',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _customerSearchController,
                textInputAction: TextInputAction.search,
                onChanged: (_) {
                  _searchCustomers();
                  if (_customerSearchController.text.trim().isEmpty) {
                    setState(() => _selectedCustomer = null);
                  }
                },
                decoration: InputDecoration(
                  hintText: 'ابحث باسم العميل أو رقم الهاتف أو الكود',
                  filled: true,
                  fillColor: UiPalette.surfaceCard,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: BorderSide.none,
                  ),
                  suffixIcon: _isSearchingCustomers
                      ? const Padding(
                          padding: EdgeInsets.all(12),
                          child: SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          ),
                        )
                      : const Icon(Icons.search_rounded),
                ),
              ),
              const SizedBox(height: 12),
              if (_selectedCustomer != null)
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: UiPalette.primary.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.person_rounded, color: UiPalette.primary),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              _selectedCustomer!.customerName,
                              style: const TextStyle(fontWeight: FontWeight.w700),
                            ),
                            const SizedBox(height: 4),
                            Text(_selectedCustomer!.phoneNumber),
                          ],
                        ),
                      ),
                    ],
                  ),
                )
              else if (_customerResults.isNotEmpty)
                ..._customerResults.map((customer) {
                  final selected = _selectedCustomer?.customerId == customer.customerId;
                  return GestureDetector(
                    onTap: () {
                      setState(() {
                        _selectedCustomer = customer;
                        _customerResults = const [];
                        _customerSearchController.text = '${customer.customerName} • ${customer.phoneNumber}';
                      });
                    },
                    child: Container(
                      width: double.infinity,
                      margin: const EdgeInsets.only(top: 8),
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: selected ? UiPalette.primary.withValues(alpha: 0.16) : UiPalette.surfaceCard,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: selected ? UiPalette.primary : UiPalette.borderSoft),
                      ),
                      child: Row(
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(customer.customerName, style: const TextStyle(fontWeight: FontWeight.w700)),
                                const SizedBox(height: 4),
                                Text(customer.phoneNumber),
                              ],
                            ),
                          ),
                          if (selected) const Icon(Icons.check_circle_rounded, color: UiPalette.primary),
                        ],
                      ),
                    ),
                  );
                }),
            ] else ...[
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFFFFF5D6),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0xFFE5B93A)),
                ),
                child: const Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.warning_amber_rounded, color: Color(0xFFE5B93A)),
                    SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'سيتم استهداف جميع العملاء المؤهلين في النظام. تأكد من التحقق قبل التنفيذ.',
                        style: TextStyle(color: Color(0xFF7C5D00), height: 1.5),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              CheckboxListTile(
                controlAffinity: ListTileControlAffinity.leading,
                contentPadding: EdgeInsets.zero,
                title: const Text('تأكيد التنفيذ للحملة على جميع العملاء المؤهلين'),
                value: _confirmation,
                onChanged: (value) => setState(() => _confirmation = value ?? false),
              ),
            ],
            const SizedBox(height: 18),
            TextField(
              controller: _titleController,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                labelText: 'عنوان الحملة',
                filled: true,
                fillColor: UiPalette.surfaceCard,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _messageController,
              minLines: 5,
              maxLines: 8,
              textInputAction: TextInputAction.newline,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                labelText: 'نص الحملة',
                filled: true,
                fillColor: UiPalette.surfaceCard,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
            const SizedBox(height: 16),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: UiPalette.borderSoft),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'معاينة',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    _titleController.text.trim().isEmpty ? 'حملة إعلانية' : _titleController.text.trim(),
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    previewText,
                    style: const TextStyle(height: 1.6),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: (_isSubmitting || !_canSubmit) ? null : _submitCampaign,
                icon: _isSubmitting
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Icon(Icons.send_rounded),
                label: Text(_isSubmitting ? 'جارٍ التنفيذ...' : 'تنفيذ الحملة'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  bool get _canSubmit {
    if (_messageController.text.trim().isEmpty) return false;
    if (_recipientMode == 'SingleCustomer') return _selectedCustomer != null;
    return _confirmation;
  }
}

class _CustomerSearchResult {
  const _CustomerSearchResult({
    required this.customerId,
    required this.customerName,
    required this.phoneNumber,
  });

  factory _CustomerSearchResult.fromJson(Map<String, dynamic> json) {
    final customerId = json['customerId'] as num? ?? 0;
    return _CustomerSearchResult(
      customerId: customerId.toInt(),
      customerName: (json['customerName'] as String?)?.trim().isNotEmpty == true
          ? json['customerName'] as String
          : 'عميل غير مسمى',
      phoneNumber: (json['phoneNumber'] as String?)?.trim() ?? 'غير متوفر',
    );
  }

  final int customerId;
  final String customerName;
  final String phoneNumber;
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

class MessageTemplatesScreen extends StatefulWidget {
  const MessageTemplatesScreen({super.key});

  @override
  State<MessageTemplatesScreen> createState() => _MessageTemplatesScreenState();
}

class _MessageTemplatesScreenState extends State<MessageTemplatesScreen> {
  late Future<List<MessageTemplateRecord>> _future;
  String _query = '';

  @override
  void initState() {
    super.initState();
    _future = MessageTemplatesRepository().getTemplates();
  }

  Future<void> _refresh() async {
    setState(() {
      _future = MessageTemplatesRepository().getTemplates();
    });
    await _future;
  }

  List<MessageTemplateRecord> _filterItems(List<MessageTemplateRecord> items, String query) {
    final normalizedQuery = query.trim().toLowerCase();
    if (normalizedQuery.isEmpty) return items;

    return items.where((template) {
      final haystack = [
        template.templateKey,
        template.templateName,
        template.title,
        template.channel,
        template.body,
        template.sourceTable,
        _templateDescriptionFor(template.templateKey),
      ].join(' ').toLowerCase();
      return haystack.contains(normalizedQuery);
    }).toList();
  }

  Future<void> _showEditor({MessageTemplateRecord? template}) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (_) => _TemplateEditorDialog(template: template),
    );

    if (result != true || !mounted) return;
    await _refresh();
  }

  Future<void> _toggleStatus(MessageTemplateRecord template) async {
    try {
      final repository = MessageTemplatesRepository();
      await (template.isActive ? repository.deactivateTemplate(template.id) : repository.activateTemplate(template.id));
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(template.isActive ? 'تم إلغاء تفعيل القالب.' : 'تم تفعيل القالب.'),
          behavior: SnackBarBehavior.floating,
        ),
      );
      await _refresh();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذرت عملية تحديث القالب: $error'), behavior: SnackBarBehavior.floating),
      );
    }
  }

  void _showPreview(MessageTemplateRecord template) {
    showDialog<void>(
      context: context,
      builder: (_) => AlertDialog(
        backgroundColor: UiPalette.surfaceCard,
        title: Text(template.titleText, style: const TextStyle(color: UiPalette.textMain)),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              if (template.templateKey != null && template.templateKey!.isNotEmpty)
                _DetailRow(label: 'المفتاح', value: template.templateKey!),
              if (template.channel != null && template.channel!.isNotEmpty)
                _DetailRow(label: 'القناة', value: template.channel!),
              const SizedBox(height: 12),
              Text(
                template.bodyText,
                style: const TextStyle(color: UiPalette.textMain, height: 1.7),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('إغلاق'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.primary,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
        title: const Text('قوالب الرسائل'),
        actions: [
          IconButton(
            tooltip: 'تحديث',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _showEditor(),
        icon: const Icon(Icons.add_rounded),
        label: const Text('إضافة قالب'),
      ),
      body: FutureBuilder<List<MessageTemplateRecord>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return _ErrorState(message: snapshot.error.toString());
          }

          final items = snapshot.data ?? const <MessageTemplateRecord>[];
          final filtered = _filterItems(items, _query);

          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        onChanged: (value) => setState(() => _query = value),
                        decoration: InputDecoration(
                          hintText: 'بحث في القوالب',
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
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                if (filtered.isEmpty)
                  _EmptyState(query: _query, emptyText: 'لا توجد قوالب رسائل حالياً.')
                else
                  ...filtered.map(
                    (template) => Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: _TemplateCard(
                        template: template,
                        onEdit: () => _showEditor(template: template),
                        onPreview: () => _showPreview(template),
                        onToggle: () => _toggleStatus(template),
                      ),
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _TemplateCard extends StatelessWidget {
  const _TemplateCard({
    required this.template,
    required this.onEdit,
    required this.onPreview,
    required this.onToggle,
  });

  final MessageTemplateRecord template;
  final VoidCallback onEdit;
  final VoidCallback onPreview;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: UiPalette.surfaceCard,
      borderRadius: BorderRadius.circular(16),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        template.titleText,
                        style: const TextStyle(color: UiPalette.textMain, fontSize: 16, fontWeight: FontWeight.bold),
                      ),
                      if (template.templateKey != null && template.templateKey!.isNotEmpty)
                        Padding(
                          padding: const EdgeInsets.only(top: 6),
                          child: Text(
                            template.templateKey!,
                            style: const TextStyle(color: UiPalette.textSoft, fontSize: 12),
                          ),
                        ),
                      const SizedBox(height: 10),
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: UiPalette.softBlue.withValues(alpha: 0.7),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: UiPalette.borderSoft),
                        ),
                        child: Text(
                          _templateDescriptionFor(template.templateKey),
                          style: const TextStyle(color: UiPalette.textMain, fontSize: 12.5, height: 1.6),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 10),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: (template.isActive ? const Color(0xFF2E9D5A) : const Color(0xFFD64B4B)).withValues(alpha: 0.18),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    template.isActive ? 'مفعل' : 'غير مفعل',
                    style: TextStyle(
                      color: template.isActive ? const Color(0xFF2E9D5A) : const Color(0xFFD64B4B),
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              template.bodyText,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(color: UiPalette.textSoft, height: 1.6),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 10,
              runSpacing: 8,
              children: [
                if (template.channel != null && template.channel!.isNotEmpty)
                  _MetaChip(icon: Icons.campaign_rounded, label: template.channel!),
                if (template.sourceTable.isNotEmpty)
                  _MetaChip(icon: Icons.storage_rounded, label: template.sourceTable),
                _MetaChip(icon: Icons.update_rounded, label: template.updatedAtLabel),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      OutlinedButton.icon(
                        onPressed: onPreview,
                        icon: const Icon(Icons.preview_rounded, size: 18),
                        label: const Text('معاينة'),
                      ),
                      OutlinedButton.icon(
                        onPressed: onEdit,
                        icon: const Icon(Icons.edit_rounded, size: 18),
                        label: const Text('تعديل'),
                      ),
                      FilledButton.tonal(
                        onPressed: onToggle,
                        style: FilledButton.styleFrom(
                          backgroundColor: template.isActive ? const Color(0xFFD64B4B) : const Color(0xFF2E9D5A),
                        ),
                        child: Text(template.isActive ? 'إلغاء التفعيل' : 'تفعيل'),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _TemplateEditorDialog extends StatefulWidget {
  const _TemplateEditorDialog({this.template});

  final MessageTemplateRecord? template;

  @override
  State<_TemplateEditorDialog> createState() => _TemplateEditorDialogState();
}

class _TemplateEditorDialogState extends State<_TemplateEditorDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _keyController;
  late final TextEditingController _titleController;
  late final TextEditingController _channelController;
  late final TextEditingController _bodyController;
  bool _isActive = true;

  @override
  void initState() {
    super.initState();
    final template = widget.template;
    _keyController = TextEditingController(text: template?.templateKey ?? '');
    _titleController = TextEditingController(text: template?.title ?? '');
    _channelController = TextEditingController(text: template?.channel ?? '');
    _bodyController = TextEditingController(text: template?.body ?? '');
    _isActive = template?.isActive ?? true;
  }

  @override
  void dispose() {
    _keyController.dispose();
    _titleController.dispose();
    _channelController.dispose();
    _bodyController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final repository = MessageTemplatesRepository();
    try {
      if (widget.template == null) {
        await repository.createTemplate(
          templateKey: _keyController.text.trim(),
          title: _titleController.text.trim(),
          channel: _channelController.text.trim(),
          body: _bodyController.text.trim(),
          isActive: _isActive,
        );
      } else {
        await repository.updateTemplate(
          widget.template!.id,
          templateKey: _keyController.text.trim(),
          title: _titleController.text.trim(),
          channel: _channelController.text.trim(),
          body: _bodyController.text.trim(),
          isActive: _isActive,
        );
      }
      if (mounted) Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('تعذرت حفظ القالب: $error'), behavior: SnackBarBehavior.floating),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final screenSize = MediaQuery.of(context).size;
    final dialogWidth = (screenSize.width * 0.75).clamp(540.0, 960.0);
    final dialogHeight = (screenSize.height * 0.82).clamp(500.0, 900.0);
    final descriptionText = _templateDescriptionFor(_keyController.text);

    return AlertDialog(
      backgroundColor: UiPalette.surfaceCard,
      insetPadding: const EdgeInsets.symmetric(horizontal: 18, vertical: 20),
      title: Text(widget.template == null ? 'إضافة قالب رسالة' : 'تعديل قالب الرسالة', style: const TextStyle(color: UiPalette.textMain)),
      content: SizedBox(
        width: dialogWidth,
        height: dialogHeight,
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  SizedBox(
                    width: 220,
                    child: TextFormField(
                      controller: _keyController,
                      readOnly: true,
                      validator: (value) => (value == null || value.trim().isEmpty) ? 'أدخل مفتاح القالب' : null,
                      decoration: const InputDecoration(labelText: 'مفتاح القالب'),
                    ),
                  ),
                  SizedBox(
                    width: 220,
                    child: TextFormField(
                      controller: _titleController,
                      validator: (value) => (value == null || value.trim().isEmpty) ? 'أدخل عنوان القالب' : null,
                      decoration: const InputDecoration(labelText: 'عنوان القالب'),
                    ),
                  ),
                  SizedBox(
                    width: 180,
                    child: TextFormField(
                      controller: _channelController,
                      validator: (value) => (value == null || value.trim().isEmpty) ? 'أدخل القناة' : null,
                      decoration: const InputDecoration(labelText: 'القناة'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: UiPalette.softBlue.withValues(alpha: 0.9),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: UiPalette.borderSoft),
                ),
                child: Text(
                  descriptionText,
                  style: const TextStyle(color: UiPalette.textMain, height: 1.6),
                ),
              ),
              const SizedBox(height: 12),
              SwitchListTile.adaptive(
                contentPadding: EdgeInsets.zero,
                title: const Text('مفعل'),
                value: _isActive,
                onChanged: (value) => setState(() => _isActive = value),
              ),
              const SizedBox(height: 12),
              Expanded(
                child: Scrollbar(
                  thumbVisibility: true,
                  child: SingleChildScrollView(
                    child: TextFormField(
                      controller: _bodyController,
                      minLines: 20,
                      maxLines: null,
                      keyboardType: TextInputType.multiline,
                      textInputAction: TextInputAction.newline,
                      textAlign: TextAlign.right,
                      validator: (value) => (value == null || value.trim().isEmpty) ? 'أدخل نص القالب' : null,
                      decoration: const InputDecoration(
                        labelText: 'نص القالب',
                        alignLabelWithHint: true,
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('إلغاء')),
        FilledButton(onPressed: _submit, child: const Text('حفظ')),
      ],
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: RichText(
        text: TextSpan(
          style: const TextStyle(color: UiPalette.textMain, height: 1.7),
          children: [
            TextSpan(text: '$label: ', style: const TextStyle(fontWeight: FontWeight.bold)),
            TextSpan(text: value),
          ],
        ),
      ),
    );
  }
}

class MessageTemplatesRepository {
  MessageTemplatesRepository({http.Client? client}) : _client = client ?? http.Client();

  final http.Client _client;

  Future<List<MessageTemplateRecord>> getTemplates() async {
    final response = await _client.get(
      Uri.parse('$_apiBaseUrl/messages/templates'),
      headers: {'Accept': 'application/json'},
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذرت قراءة قوالب الرسائل (${response.statusCode})');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! List) return const <MessageTemplateRecord>[];

    final items = decoded.whereType<Map<String, dynamic>>();
    return items.map(MessageTemplateRecord.fromJson).toList();
  }

  Future<MessageTemplateRecord> createTemplate({
    required String templateKey,
    required String title,
    required String channel,
    required String body,
    required bool isActive,
  }) async {
    final response = await _client.post(
      Uri.parse('$_apiBaseUrl/messages/templates'),
      headers: {'Content-Type': 'application/json', 'Accept': 'application/json'},
      body: jsonEncode({
        'templateKey': templateKey,
        'title': title,
        'channel': channel,
        'body': body,
        'isActive': isActive,
      }),
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذرت إنشاء القالب (${response.statusCode})');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('استجابة القالب غير صالحة.');
    }

    return MessageTemplateRecord.fromJson(decoded);
  }

  Future<MessageTemplateRecord> updateTemplate(
    int id, {
    required String templateKey,
    required String title,
    required String channel,
    required String body,
    required bool isActive,
  }) async {
    final response = await _client.put(
      Uri.parse('$_apiBaseUrl/messages/templates/$id'),
      headers: {'Content-Type': 'application/json', 'Accept': 'application/json'},
      body: jsonEncode({
        'templateKey': templateKey,
        'title': title,
        'channel': channel,
        'body': body,
        'isActive': isActive,
      }),
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذرت تحديث القالب (${response.statusCode})');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('استجابة تحديث القالب غير صالحة.');
    }

    return MessageTemplateRecord.fromJson(decoded);
  }

  Future<MessageTemplateRecord> activateTemplate(int id) async {
    final response = await _client.put(
      Uri.parse('$_apiBaseUrl/messages/templates/$id/activate'),
      headers: {'Accept': 'application/json'},
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذرت تفعيل القالب (${response.statusCode})');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('استجابة التفعيل غير صالحة.');
    }

    return MessageTemplateRecord.fromJson(decoded);
  }

  Future<MessageTemplateRecord> deactivateTemplate(int id) async {
    final response = await _client.put(
      Uri.parse('$_apiBaseUrl/messages/templates/$id/deactivate'),
      headers: {'Accept': 'application/json'},
    );

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذرت إلغاء تفعيل القالب (${response.statusCode})');
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('استجابة إلغاء التفعيل غير صالحة.');
    }

    return MessageTemplateRecord.fromJson(decoded);
  }
}

class MessageTemplateRecord {
  const MessageTemplateRecord({
    required this.id,
    required this.sourceTable,
    this.templateKey,
    this.templateName,
    this.title,
    this.channel,
    this.body,
    required this.isActive,
    this.createdAtUtc,
    this.updatedAtUtc,
  });

  factory MessageTemplateRecord.fromJson(Map<String, dynamic> json) {
    return MessageTemplateRecord(
      id: json['templateId'] as int? ?? 0,
      sourceTable: json['sourceTable'] as String? ?? '',
      templateKey: json['templateKey'] as String?,
      templateName: json['templateName'] as String?,
      title: json['title'] as String?,
      channel: json['channel'] as String?,
      body: json['body'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      createdAtUtc: _optionalDateFromJson(json['createdAtUtc']),
      updatedAtUtc: _optionalDateFromJson(json['updatedAtUtc']),
    );
  }

  final int id;
  final String sourceTable;
  final String? templateKey;
  final String? templateName;
  final String? title;
  final String? channel;
  final String? body;
  final bool isActive;
  final DateTime? createdAtUtc;
  final DateTime? updatedAtUtc;

  String get titleText => (title ?? '').trim().isNotEmpty ? title!.trim() : (templateName ?? templateKey ?? 'قالب بدون عنوان');
  String get bodyText => body ?? '';
  String get updatedAtLabel => updatedAtUtc != null ? _formatDate(updatedAtUtc!) : 'غير محدد';
}

DateTime? _optionalDateFromJson(Object? value) {
  if (value == null || value == '') return null;
  final parsed = DateTime.tryParse(value.toString());
  return parsed;
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.query, this.emptyText = 'لا توجد رسائل حالياً.'});

  final String query;
  final String emptyText;

  @override
  Widget build(BuildContext context) {
    final text = query.isEmpty ? emptyText : 'لا توجد نتائج للبحث الحالي.';
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
