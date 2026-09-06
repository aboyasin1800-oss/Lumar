import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';

import '../core/app_navigation.dart';
import 'customer_details_screen.dart';

class CustomerPage extends StatefulWidget {
  const CustomerPage({super.key});
  @override
  State<CustomerPage> createState() => _CustomerPageState();
}

class _CustomerPageState extends State<CustomerPage> {
  final api = CustomerApi();
  final search = TextEditingController();
  Future<List<Map<String, dynamic>>>? customers;
  String? error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load([String? term]) => setState(() {
        error = null;
        customers = api.list(term);
      });

  Future<void> _form([Map<String, dynamic>? customer]) async {
    final formKey = GlobalKey<FormState>();
    final code = TextEditingController(text: customer?['customerCode']);
    final name = TextEditingController(text: customer?['customerName']);
    final phone = TextEditingController(text: customer?['phoneNumber']);
    final address = TextEditingController(text: customer?['address']);
    final notes = TextEditingController(text: customer?['notes']);
    final parent =
        TextEditingController(text: customer?['parentCustomerId']?.toString());
    var active = customer?['isActive'] ?? true;
    Future<void> save(BuildContext dialogContext) async {
      if (!formKey.currentState!.validate()) return;
      final payload = {
        'customerCode': code.text,
        'customerName': name.text,
        'phoneNumber': phone.text,
        'address': address.text,
        'notes': notes.text,
        'isActive': active,
        'parentCustomerId': int.tryParse(parent.text)
      };
      try {
        if (customer == null) {
          await api.create(payload);
        } else {
          await api.update(customer['customerId'] as int, payload);
        }
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
                  title: Text(customer == null ? 'عميل جديد' : 'تعديل العميل'),
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
                                _field(notes, 'ملاحظات', lines: 3),
                                _field(parent, 'معرف العميل المحيل',
                                    onSubmitted: () => save(context)),
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
                  onSubmitted: (value) => _load(value),
                  decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.search),
                      labelText:
                          'ابحث بالرمز أو الاسم أو الهاتف أو رمز الإحالة',
                      border: OutlineInputBorder()))),
          const SizedBox(width: 8),
          IconButton(
              tooltip: 'مسح البحث',
              onPressed: () {
                search.clear();
                _load();
              },
              icon: const Icon(Icons.refresh))
        ]),
        const SizedBox(height: 16),
        Expanded(
            child: FutureBuilder<List<Map<String, dynamic>>>(
                future: customers,
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const Center(child: CircularProgressIndicator());
                  }
                  if (snapshot.hasError) {
                    return const Center(child: Text('تعذر تحميل العملاء.'));
                  }
                  final items = snapshot.data ?? [];
                  if (items.isEmpty) {
                    return const Center(child: Text('لا توجد بيانات عملاء.'));
                  }
                  return ListView.separated(
                      itemCount: items.length,
                      separatorBuilder: (_, __) => const Divider(height: 1),
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
                                  icon:
                                      const Icon(Icons.account_tree_outlined)),
                              IconButton(
                                  tooltip: 'تعديل العميل',
                                  onPressed: editCustomer,
                                  icon: const Icon(Icons.edit_outlined))
                            ]));
                      });
                }))
      ]);

  Future<void> _hierarchy(int id) async {
    try {
      await api.hierarchy(id);
      if (!mounted) return;
      showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
                  title: const Text('شجرة الإحالة'),
                  content: const SizedBox(
                      width: 500,
                      child: Text('تم تحميل بيانات شجرة الإحالة بنجاح.')),
                  actions: [
                    TextButton(
                        onPressed: () => Navigator.pop(context),
                        child: const Text('إغلاق'))
                  ]));
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('تعذر تحميل شجرة الإحالة.')));
      }
    }
  }
}

class CustomerApi {
  CustomerApi(
      {this.baseUrl = const String.fromEnvironment('LUMAR_API_URL',
          defaultValue: 'http://127.0.0.1:5093')});
  final String baseUrl;
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
  Future<void> create(Map<String, dynamic> payload) async {
    await _request('POST', Uri.parse('$baseUrl/customers'), payload);
  }

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
