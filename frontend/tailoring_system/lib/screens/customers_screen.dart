import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';

import '../core/app_navigation.dart';
import '../models/customer_creation_models.dart';
import 'customer_details_screen.dart';
import 'customers/customer_create_screen.dart';
import 'referral/referral_tree_screen.dart';

String _normalizeCustomerSearchText(String value) =>
    value.trim().replaceAll(RegExp(r'\s+'), ' ').toLowerCase();

bool _isDirectCustomerMatch(Map<String, dynamic> customer, String term) {
  final normalizedTerm = _normalizeCustomerSearchText(term);
  final values = [
    customer['customerName']?.toString() ?? '',
    customer['customerCode']?.toString() ?? '',
    customer['phoneNumber']?.toString() ?? '',
  ].map(_normalizeCustomerSearchText).where((value) => value.isNotEmpty);
  final searchableText = values.join(' ');
  if (values.any(
      (value) => value == normalizedTerm || value.startsWith(normalizedTerm))) {
    return true;
  }
  final words = normalizedTerm.split(' ').where((word) => word.isNotEmpty);
  return words.isNotEmpty && words.every(searchableText.contains);
}

bool _hasDirectCustomerMatch(
        List<Map<String, dynamic>> customers, String term) =>
    customers.any((customer) => _isDirectCustomerMatch(customer, term));

class CustomerPage extends StatefulWidget {
  const CustomerPage({super.key});
  @override
  State<CustomerPage> createState() => _CustomerPageState();
}

class _CustomerPageState extends State<CustomerPage> {
  final api = CustomerApi();
  final search = TextEditingController();
  Timer? _searchDebounce;
  int _searchRequestId = 0;
  Future<List<Map<String, dynamic>>>? customers;
  Future<List<Map<String, dynamic>>>? _suggestionsFuture;
  bool _selectingSuggestion = false;
  String? error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    api.cancelSearch();
    search.dispose();
    super.dispose();
  }

  void _load([String? term]) {
    _searchDebounce?.cancel();
    final requestId = ++_searchRequestId;
    final normalizedTerm = term?.trim() ?? '';
    setState(() {
      error = null;
      _suggestionsFuture = null;
      customers = normalizedTerm.isEmpty
          ? api.list(null)
          : _searchForGeneration(normalizedTerm, requestId);
    });
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    final requestId = ++_searchRequestId;
    final term = value.trim();
    if (_selectingSuggestion) return;
    if (term.isEmpty) {
      setState(() {
        _suggestionsFuture = null;
        customers = api.list(null);
      });
      return;
    }

    setState(() {
      error = null;
      _suggestionsFuture = null;
      customers = null;
    });
    _searchDebounce = Timer(const Duration(milliseconds: 250), () {
      if (!mounted || requestId != _searchRequestId) return;
      final request = _searchForGeneration(term, requestId);
      setState(() {
        if (requestId == _searchRequestId) {
          _suggestionsFuture = request;
          customers = request;
        }
      });
    });
  }

  Future<List<Map<String, dynamic>>> _searchForGeneration(
      String term, int requestId) async {
    try {
      final results = await api.search(term);
      if (!mounted || requestId != _searchRequestId) {
        return const <Map<String, dynamic>>[];
      }
      return results;
    } on Object {
      if (!mounted || requestId != _searchRequestId) {
        return const <Map<String, dynamic>>[];
      }
      rethrow;
    }
  }

  void _selectSuggestion(Map<String, dynamic> customer) {
    _searchDebounce?.cancel();
    ++_searchRequestId;
    _selectingSuggestion = true;
    try {
      search.text = customer['customerName']?.toString() ??
          customer['customerCode']?.toString() ??
          '';
    } finally {
      _selectingSuggestion = false;
    }
    setState(() {
      _suggestionsFuture = null;
      customers = Future<List<Map<String, dynamic>>>.value([customer]);
    });
  }

  Future<void> _openCreateCustomer() async {
    final created = await AppNavigation.push<CustomerCreationResult>(
      context,
      (_) => const CustomerCreateScreen(),
    );
    if (!mounted || created == null) return;
    _load();
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('تم إنشاء العميل ${created.customerCode} بنجاح.')),
    );
  }

  Future<void> _form(Map<String, dynamic> customer) async {
    final formKey = GlobalKey<FormState>();
    final code = TextEditingController(text: customer['customerCode']);
    final name = TextEditingController(text: customer['customerName']);
    final phone = TextEditingController(text: customer['phoneNumber']);
    final address = TextEditingController(text: customer['address']);
    final notes = TextEditingController(text: customer['notes']);
    var active = customer['isActive'] ?? true;
    Future<void> save(BuildContext dialogContext) async {
      if (!formKey.currentState!.validate()) return;
      final payload = {
        'customerCode': code.text,
        'customerName': name.text,
        'phoneNumber': phone.text,
        'address': address.text,
        'notes': notes.text,
        'isActive': active,
        'parentCustomerId': customer['parentCustomerId'],
        'relationshipType': customer['relationshipType'],
      };
      try {
        await api.update(customer['customerId'] as int, payload);
        if (dialogContext.mounted) {
          Navigator.pop(dialogContext);
        }
        _load();
      } catch (_) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('تعذر حفظ بيانات العميل.')));
        }
      }
    }

    await showDialog<void>(
        context: context,
        builder: (context) => StatefulBuilder(
            builder: (context, setDialogState) => AlertDialog(
                  title: const Text('تعديل العميل'),
                  content: SizedBox(
                      width: 460,
                      child: Form(
                          key: formKey,
                          child: SingleChildScrollView(
                              child: Column(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                _field(code, 'رمز العميل', required: true),
                                _field(name, 'اسم العميل', required: true),
                                _field(phone, 'رقم الهاتف'),
                                _field(address, 'العنوان'),
                                _field(notes, 'ملاحظات',
                                    lines: 3, onSubmitted: () => save(context)),
                                SwitchListTile(
                                    value: active,
                                    onChanged: (value) =>
                                        setDialogState(() => active = value),
                                    title: const Text('نشط')),
                              ])))),
                  actions: [
                    TextButton(
                        onPressed: () => Navigator.pop(context),
                        child: const Text('إلغاء')),
                    FilledButton(
                        onPressed: () => save(context),
                        child: const Text('حفظ'))
                  ],
                )));
  }

  Widget _field(TextEditingController controller, String label,
          {bool required = false, int lines = 1, VoidCallback? onSubmitted}) =>
      Padding(
          padding: const EdgeInsets.only(bottom: 10),
          child: TextFormField(
              controller: controller,
              maxLines: lines,
              textInputAction: onSubmitted == null
                  ? TextInputAction.next
                  : TextInputAction.done,
              onFieldSubmitted: (_) {
                if (onSubmitted == null) {
                  FocusScope.of(context).nextFocus();
                } else {
                  onSubmitted();
                }
              },
              decoration: InputDecoration(
                  labelText: label, border: const OutlineInputBorder()),
              validator: required
                  ? (value) => value == null || value.trim().isEmpty
                      ? 'حقل $label مطلوب'
                      : null
                  : null));

  @override
  Widget build(BuildContext context) => Column(children: [
        Row(children: [
          Expanded(
              child: TextField(
                  controller: search,
                  onChanged: _onSearchChanged,
                  onSubmitted: (value) => _load(value),
                  decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.search),
                      labelText:
                          'ابحث بالرمز أو الاسم أو الهاتف أو رمز الإحالة',
                      border: OutlineInputBorder()))),
          const SizedBox(width: 8),
          IconButton(
              tooltip: 'إنشاء عميل',
              onPressed: _openCreateCustomer,
              icon: const Icon(Icons.person_add_alt_1)),
          IconButton(
              tooltip: 'مسح البحث',
              onPressed: () {
                search.clear();
                _load(null);
              },
              icon: const Icon(Icons.refresh))
        ]),
        if (_suggestionsFuture != null)
          _CustomerSuggestions(
            future: _suggestionsFuture!,
            searchTerm: search.text.trim(),
            onSelected: _selectSuggestion,
          ),
        const SizedBox(height: 16),
        Expanded(
            child: FutureBuilder<List<Map<String, dynamic>>>(
                future: customers,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    if (snapshot.connectionState == ConnectionState.none) {
                      return const SizedBox.shrink();
                    }
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (snapshot.hasError) {
                    return Center(
                        child: Text(search.text.trim().isEmpty
                            ? 'تعذر تحميل العملاء.'
                            : 'تعذر البحث عن العملاء. حاول مرة أخرى.'));
                  }
                  final items = snapshot.data ?? [];
                  if (items.isEmpty) {
                    if (search.text.trim().isNotEmpty) {
                      return const Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text('لا يوجد تطابق لهذا الاسم'),
                            SizedBox(height: 6),
                            Text('أسماء قريبة من بحثك'),
                            SizedBox(height: 6),
                            Text('لا توجد أسماء قريبة من عبارة البحث'),
                          ],
                        ),
                      );
                    }
                    return const Center(child: Text('لا توجد بيانات عملاء.'));
                  }
                  final hasDirectMatch = search.text.trim().isEmpty ||
                      _hasDirectCustomerMatch(items, search.text);
                  return Column(
                    children: [
                      if (!hasDirectMatch)
                        const Padding(
                          padding: EdgeInsets.only(bottom: 8),
                          child: Align(
                            alignment: AlignmentDirectional.centerStart,
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text('لا يوجد تطابق لهذا الاسم'),
                                Text('أسماء قريبة من بحثك'),
                              ],
                            ),
                          ),
                        ),
                      Expanded(
                        child: ListView.separated(
                            itemCount: items.length,
                            separatorBuilder: (_, __) =>
                                const Divider(height: 1),
                            itemBuilder: (context, index) {
                              final item = items[index];
                              final customerId = item['customerId'] as int;
                              Future<void> editCustomer() async {
                                final details = await api.get(customerId);
                                _form(details);
                              }

                              return ListTile(
                                  onTap: () => AppNavigation.push(
                                      context,
                                      (_) => CustomerDetailsScreen(
                                          customerId: customerId)),
                                  title: Text(
                                      '${item['customerCode'] ?? '-'}  ${item['customerName'] ?? '-'}'),
                                  subtitle: Text(item['phoneNumber'] ?? ''),
                                  trailing: Wrap(spacing: 4, children: [
                                    IconButton(
                                        tooltip: 'شجرة الإحالة',
                                        onPressed: () => _hierarchy(customerId),
                                        icon: const Icon(
                                            Icons.account_tree_outlined)),
                                    IconButton(
                                        tooltip: 'تعديل العميل',
                                        onPressed: editCustomer,
                                        icon: const Icon(Icons.edit_outlined))
                                  ]));
                            }),
                      ),
                    ],
                  );
                }))
      ]);

  Future<void> _hierarchy(int id) async {
    await AppNavigation.push(
      context,
      (_) => ReferralTreeScreen(customerId: id),
    );
  }
}

class _CustomerSuggestions extends StatelessWidget {
  const _CustomerSuggestions(
      {required this.future,
      required this.searchTerm,
      required this.onSelected});

  final Future<List<Map<String, dynamic>>> future;
  final String searchTerm;
  final ValueChanged<Map<String, dynamic>> onSelected;

  @override
  Widget build(BuildContext context) =>
      FutureBuilder<List<Map<String, dynamic>>>(
        future: future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const LinearProgressIndicator();
          }
          if (snapshot.hasError) {
            return const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text('تعذر البحث عن العملاء. حاول مرة أخرى.'),
              ),
            );
          }
          final results = snapshot.data ?? const <Map<String, dynamic>>[];
          if (results.isEmpty) {
            return const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('لا يوجد تطابق لهذا الاسم'),
                    Text('أسماء قريبة من بحثك'),
                    Text('لا توجد أسماء قريبة من عبارة البحث'),
                  ],
                ),
              ),
            );
          }
          final hasDirectMatch = _hasDirectCustomerMatch(results, searchTerm);
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (!hasDirectMatch) ...[
                const Text('لا يوجد تطابق لهذا الاسم'),
                const Text('أسماء قريبة من بحثك'),
              ],
              ConstrainedBox(
                constraints: const BoxConstraints(maxHeight: 220),
                child: Card(
                  margin: const EdgeInsets.only(top: 4),
                  child: ListView.separated(
                    shrinkWrap: true,
                    itemCount: results.take(6).length,
                    separatorBuilder: (_, __) => const Divider(height: 1),
                    itemBuilder: (context, index) {
                      final customer = results[index];
                      final code = customer['customerCode']?.toString() ?? '-';
                      final name =
                          customer['customerName']?.toString() ?? 'عميل';
                      final phone = customer['phoneNumber']?.toString() ?? '';
                      return ListTile(
                        dense: true,
                        leading: const Icon(Icons.person_search_outlined),
                        title: Text('$code  $name'),
                        subtitle: phone.isEmpty ? null : Text(phone),
                        onTap: () => onSelected(customer),
                      );
                    },
                  ),
                ),
              ),
            ],
          );
        },
      );
}

class CustomerApi {
  CustomerApi(
      {this.baseUrl = const String.fromEnvironment('LUMAR_API_URL',
          defaultValue: 'http://127.0.0.1:5093')});
  final String baseUrl;
  HttpClient? _activeSearchClient;

  void cancelSearch() {
    _activeSearchClient?.close(force: true);
    _activeSearchClient = null;
  }

  Future<List<Map<String, dynamic>>> search(String term) async {
    final uri = Uri.parse(
        '$baseUrl/customers/search?term=${Uri.encodeQueryComponent(term)}');
    final client = HttpClient();
    _activeSearchClient = client;
    try {
      final response = await (await client.getUrl(uri)).close();
      final body = await utf8.decoder.bind(response).join();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw HttpException(body, uri: uri);
      }
      return (jsonDecode(body) as List).cast<Map<String, dynamic>>();
    } finally {
      if (identical(_activeSearchClient, client)) {
        _activeSearchClient = null;
      }
      client.close(force: true);
    }
  }

  Future<List<Map<String, dynamic>>> list(String? term) async {
    final uri = Uri.parse(
        '$baseUrl/customers${term == null || term.isEmpty ? '' : '/search?term=${Uri.encodeQueryComponent(term)}'}');
    final client = HttpClient();
    try {
      final response = await (await client.getUrl(uri)).close();
      final body = await utf8.decoder.bind(response).join();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw HttpException(body, uri: uri);
      }
      return (jsonDecode(body) as List).cast<Map<String, dynamic>>();
    } finally {
      client.close();
    }
  }

  Future<Map<String, dynamic>> get(int id) async => jsonDecode(
          (await _request('GET', Uri.parse('$baseUrl/customers/$id'))).body)
      as Map<String, dynamic>;
  Future<Map<String, dynamic>> hierarchy(int id) async =>
      jsonDecode((await _request(
              'GET', Uri.parse('$baseUrl/customers/$id/referral-hierarchy')))
          .body) as Map<String, dynamic>;
  Future<void> update(int id, Map<String, dynamic> payload) async {
    await _request('PUT', Uri.parse('$baseUrl/customers/$id'), payload);
  }

  Future<ApiResponse> _request(String method, Uri uri,
      [Map<String, dynamic>? payload]) async {
    final client = HttpClient();
    try {
      final request = await client.openUrl(method, uri);
      if (payload != null) {
        request.headers.contentType = ContentType.json;
        request.write(jsonEncode(payload));
      }
      final response = await request.close();
      final body = await utf8.decoder.bind(response).join();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw HttpException(
            body.isEmpty ? 'Request failed (${response.statusCode})' : body,
            uri: uri);
      }
      return ApiResponse(response.statusCode, body);
    } finally {
      client.close();
    }
  }
}

class ApiResponse {
  ApiResponse(this.statusCode, this.body);
  final int statusCode;
  final String body;
}
