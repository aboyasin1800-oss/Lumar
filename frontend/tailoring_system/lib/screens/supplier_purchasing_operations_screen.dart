import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../core/authenticated_api_client.dart';
import '../models/supplier_purchasing_models.dart';
import '../repositories/supplier_purchasing_repository.dart';
import '../services/auth_state.dart';

class SupplierPurchasingOperationsScreen extends StatefulWidget {
  const SupplierPurchasingOperationsScreen({super.key, required this.auth});

  final AuthState auth;

  @override
  State<SupplierPurchasingOperationsScreen> createState() =>
      _SupplierPurchasingOperationsScreenState();
}

class _SupplierPurchasingOperationsScreenState
    extends State<SupplierPurchasingOperationsScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  late final SupplierPurchasingRepository _repository;
  final _date = DateFormat('dd/MM/yyyy');
  bool _loading = true;
  bool _busy = false;
  String? _error;
  final Set<int> _loadedTabs = <int>{};
  List<SupplierPurchasingSupplier> _suppliers = const [];
  List<SupplierPurchasingOrder> _orders = const [];
  List<SupplierPurchasingInvoice> _invoices = const [];
  List<SupplierPurchasingPayment> _payments = const [];
  List<SupplierPurchasingReceipt> _receipts = const [];

  AuthUser? get _user => widget.auth.user;
  bool get _isAdministrator =>
      _user?.role?.trim().toLowerCase() == 'system administrator';
  bool get _isFinancialManager =>
      _user?.role?.trim().toLowerCase() == 'authorized financial manager';
  bool get _canFinancial => _isAdministrator || _isFinancialManager;
  bool get _canReceive => _isAdministrator;
  bool get _canReverse => _isAdministrator || _isFinancialManager;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 5, vsync: this);
    _tabs.addListener(_onTabChanged);
    _repository =
        SupplierPurchasingRepository(AuthenticatedApiClient(auth: widget.auth));
    WidgetsBinding.instance
        .addPostFrameCallback((_) => _loadTab(0, force: true));
  }

  @override
  void dispose() {
    _tabs.removeListener(_onTabChanged);
    _tabs.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    _loadedTabs.clear();
    await _loadTab(_tabs.index, force: true);
  }

  void _onTabChanged() {
    if (!_tabs.indexIsChanging) _loadTab(_tabs.index);
  }

  Future<void> _loadTab(int tab, {bool force = false}) async {
    if (!force && _loadedTabs.contains(tab)) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = switch (tab) {
        0 => await _repository.getSuppliers(),
        1 => await _repository.getOrders(),
        2 => await _repository.getInvoices(),
        3 => await _repository.getPayments(),
        _ => await _repository.getReceipts(),
      };
      if (!mounted) return;
      setState(() {
        if (tab == 0) _suppliers = data as List<SupplierPurchasingSupplier>;
        if (tab == 1) _orders = data as List<SupplierPurchasingOrder>;
        if (tab == 2) _invoices = data as List<SupplierPurchasingInvoice>;
        if (tab == 3) _payments = data as List<SupplierPurchasingPayment>;
        if (tab == 4) _receipts = data as List<SupplierPurchasingReceipt>;
        _loadedTabs.add(tab);
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = error.toString();
      });
    }
  }

  Future<void> _run(Future<void> Function() action,
      {String success = 'تم تنفيذ العملية بنجاح.'}) async {
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(success)));
      await _load();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('الموردون والمشتريات'),
        actions: [
          IconButton(
              onPressed: _loading ? null : _load,
              tooltip: 'تحديث البيانات',
              icon: const Icon(Icons.refresh))
        ],
        bottom: TabBar(
          controller: _tabs,
          isScrollable: true,
          tabs: const [
            Tab(text: 'الموردون', icon: Icon(Icons.people_outline)),
            Tab(text: 'أوامر الشراء', icon: Icon(Icons.shopping_cart_outlined)),
            Tab(text: 'الفواتير', icon: Icon(Icons.receipt_long_outlined)),
            Tab(text: 'الدفعات', icon: Icon(Icons.payments_outlined)),
            Tab(text: 'الاستلام', icon: Icon(Icons.inventory_2_outlined)),
          ],
        ),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? _ErrorState(message: _error!, onRetry: _load)
              : TabBarView(controller: _tabs, children: [
                  _suppliersView(),
                  _ordersView(),
                  _invoicesView(),
                  _paymentsView(),
                  _receiptsView()
                ]),
    );
  }

  Widget _suppliersView() => _listOrEmpty(
        _suppliers,
        (supplier) => ListTile(
          leading: const CircleAvatar(child: Icon(Icons.person_outline)),
          title: Text(supplier.name),
          subtitle:
              Text('${supplier.code}  ${supplier.phone ?? 'لا يوجد هاتف'}'),
        ),
        empty: 'لا يوجد موردون مسجلون.',
      );

  Widget _ordersView() => _listOrEmpty(
        _orders,
        (order) => ListTile(
          leading: const Icon(Icons.shopping_cart_outlined),
          title: Text(order.number),
          subtitle: Text(
              'المورد رقم ${order.supplierId}  •  ${_date.format(order.date)}'),
          trailing: Text(_status(order.status)),
        ),
        empty: 'لا توجد أوامر شراء.',
      );

  Widget _invoicesView() => Column(
        children: [
          if (_canFinancial)
            _ActionBar(
                label: 'فاتورة مورد جديدة',
                icon: Icons.add,
                onPressed: _busy ? null : _createInvoice),
          Expanded(
            child: _listOrEmpty(
              _invoices,
              (invoice) => ListTile(
                leading: const Icon(Icons.receipt_long_outlined),
                title: Text(invoice.number),
                subtitle: Text(
                    'المورد رقم ${invoice.supplierId}  •  ${_date.format(invoice.date)}'),
                trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                  Text(invoice.total.toStringAsFixed(2)),
                  if (_canReverse)
                    IconButton(
                        onPressed: _busy
                            ? null
                            : () => _reverseFinancial('Invoice', invoice.id),
                        tooltip: 'عكس الفاتورة',
                        icon: const Icon(Icons.undo))
                ]),
              ),
              empty: 'لا توجد فواتير موردين.',
            ),
          ),
        ],
      );

  Widget _paymentsView() => Column(
        children: [
          if (_canFinancial)
            _ActionBar(
                label: 'دفعة مورد جديدة',
                icon: Icons.add,
                onPressed: _busy ? null : _createPayment),
          Expanded(
            child: _listOrEmpty(
              _payments,
              (payment) => ListTile(
                leading: const Icon(Icons.payments_outlined),
                title: Text(payment.number),
                subtitle: Text(
                    'المورد رقم ${payment.supplierId}  •  ${_date.format(payment.date)}'),
                trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                  Text(payment.amount.toStringAsFixed(2)),
                  if (_canFinancial)
                    IconButton(
                        onPressed: _busy ? null : _createAllocation,
                        tooltip: 'تخصيص دفعة',
                        icon: const Icon(Icons.link)),
                  if (_canReverse)
                    IconButton(
                        onPressed: _busy
                            ? null
                            : () => _reverseFinancial('Payment', payment.id),
                        tooltip: 'عكس الدفعة',
                        icon: const Icon(Icons.undo))
                ]),
              ),
              empty: 'لا توجد دفعات موردين.',
            ),
          ),
        ],
      );

  Widget _receiptsView() => Column(
        children: [
          if (_canReceive)
            _ActionBar(
                label: 'استلام بضاعة جديد',
                icon: Icons.add,
                onPressed: _busy ? null : _createReceipt),
          Expanded(
            child: _listOrEmpty(
              _receipts,
              (receipt) => ListTile(
                leading: const Icon(Icons.inventory_2_outlined),
                title: Text(receipt.number),
                subtitle: Text(
                    'المورد رقم ${receipt.supplierId}  •  ${receipt.purchaseOrderId == null ? 'استلام مباشر' : 'مرتبط بأمر شراء'}'),
                trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                  IconButton(
                      onPressed: _busy ? null : () => _showMatching(receipt.id),
                      tooltip: 'عرض المطابقة',
                      icon: const Icon(Icons.compare_arrows)),
                  if (_canReverse)
                    IconButton(
                        onPressed:
                            _busy ? null : () => _reverseReceipt(receipt.id),
                        tooltip: 'عكس الاستلام',
                        icon: const Icon(Icons.undo))
                ]),
              ),
              empty: 'لا توجد عمليات استلام.',
            ),
          ),
        ],
      );

  Widget _listOrEmpty<T>(List<T> items, Widget Function(T item) builder,
      {required String empty}) {
    if (items.isEmpty) return Center(child: Text(empty));
    return RefreshIndicator(
        onRefresh: _load,
        child: ListView.builder(
            itemCount: items.length,
            itemBuilder: (_, index) => Card(
                margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                child: builder(items[index]))));
  }

  Future<void> _createInvoice() async {
    final values = await _formDialog('فاتورة مورد جديدة',
        ['رقم الفاتورة', 'رقم المورد', 'المبلغ', 'رقم أمر الشراء اختياري']);
    if (values == null) return;
    await _run(() => _repository.createInvoice(
        supplierId: int.parse(values[1]),
        invoiceNumber: values[0],
        invoiceDate: DateTime.now(),
        dueDate: DateTime.now(),
        amount: double.parse(values[2]),
        purchaseOrderId: values[3].isEmpty ? null : int.parse(values[3])));
  }

  Future<void> _createPayment() async {
    final values = await _formDialog('دفعة مورد جديدة', [
      'رقم المورد',
      'المبلغ',
      'رقم الحساب النقدي',
      'طريقة الدفع',
      'المرجع',
      'رقم الفاتورة اختياري'
    ]);
    if (values == null) return;
    await _run(() => _repository.createPayment(
        supplierId: int.parse(values[0]),
        amount: double.parse(values[1]),
        paymentDate: DateTime.now(),
        cashAccountId: int.parse(values[2]),
        paymentKind: 0,
        paymentMethod: values[3],
        referenceNumber: values[4],
        supplierInvoiceId: values[5].isEmpty ? null : int.parse(values[5])));
  }

  Future<void> _createAllocation() async {
    final values = await _formDialog(
        'تخصيص دفعة', ['رقم الدفعة', 'رقم الفاتورة', 'المبلغ', 'المرجع']);
    if (values == null) return;
    await _run(() => _repository.createAllocation(
        paymentId: int.parse(values[0]),
        invoiceId: int.parse(values[1]),
        amount: double.parse(values[2]),
        referenceNumber: values[3]));
  }

  Future<void> _createReceipt() async {
    final values = await _formDialog('استلام بضاعة جديد', [
      'رقم المورد',
      'رقم المستودع',
      'رقم الاستلام',
      'رقم أمر الشراء اختياري',
      'رقم الصنف',
      'الكمية',
      'تكلفة الوحدة'
    ]);
    if (values == null) return;
    await _run(() => _repository.createReceipt(
            supplierId: int.parse(values[0]),
            warehouseId: int.parse(values[1]),
            receiptNumber: values[2],
            purchaseOrderId: values[3].isEmpty ? null : int.parse(values[3]),
            items: [
              {
                'inventoryItemId': int.parse(values[4]),
                'quantity': double.parse(values[5]),
                'unitCost': double.parse(values[6])
              }
            ]));
  }

  Future<void> _reverseFinancial(String type, int id) async {
    final reason = await _reasonDialog('سبب عكس العملية');
    if (reason == null) return;
    await _run(
        () => _repository.reverseFinancial(
            documentType: type, documentId: id, reason: reason),
        success: 'تم إرسال طلب العكس بنجاح.');
  }

  Future<void> _reverseReceipt(int id) async {
    final reason = await _reasonDialog('سبب عكس الاستلام');
    if (reason == null) return;
    await _run(() => _repository.reverseReceipt(receiptId: id, reason: reason),
        success: 'تم عكس الاستلام بنجاح.');
  }

  Future<void> _showMatching(int id) async {
    try {
      final matching = await _repository.getReceiptMatching(id);
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (_) => AlertDialog(
          title: Text('مطابقة ${matching.receipt.number}'),
          content: SizedBox(
            width: 420,
            child: ListView(
              shrinkWrap: true,
              children: matching.items
                  .map((item) => ListTile(
                        title: Text(item.itemName),
                        subtitle: Text(
                            'الكمية ${item.quantity}  •  تكلفة الوحدة ${item.unitCost}'),
                      ))
                  .toList(),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('إغلاق'),
            ),
          ],
        ),
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  Future<List<String>?> _formDialog(String title, List<String> labels) async {
    final controllers = labels.map((_) => TextEditingController()).toList();
    final formKey = GlobalKey<FormState>();
    final result = await showDialog<List<String>>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: SizedBox(
          width: 430,
          child: Form(
            key: formKey,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  for (var index = 0; index < labels.length; index++)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: TextFormField(
                        controller: controllers[index],
                        autofocus: index == 0,
                        textInputAction: index == labels.length - 1
                            ? TextInputAction.done
                            : TextInputAction.next,
                        keyboardType: index == 0 || index == 3
                            ? TextInputType.text
                            : TextInputType.number,
                        decoration: InputDecoration(labelText: labels[index]),
                        validator: (value) => index == 3 && labels.length > 3
                            ? null
                            : (value == null || value.trim().isEmpty
                                ? 'هذا الحقل مطلوب.'
                                : null),
                        onFieldSubmitted: (_) {
                          if (index == labels.length - 1 &&
                              formKey.currentState!.validate()) {
                            Navigator.pop(
                              dialogContext,
                              controllers
                                  .map((controller) => controller.text.trim())
                                  .toList(),
                            );
                          }
                        },
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('إلغاء'),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.pop(
                  dialogContext,
                  controllers
                      .map((controller) => controller.text.trim())
                      .toList(),
                );
              }
            },
            child: const Text('تنفيذ'),
          ),
        ],
      ),
    );
    for (final controller in controllers) {
      controller.dispose();
    }
    return result;
  }

  Future<String?> _reasonDialog(String title) async {
    final controller = TextEditingController();
    final result = await showDialog<String>(
        context: context,
        builder: (dialogContext) => AlertDialog(
                title: Text(title),
                content: TextField(
                    controller: controller,
                    autofocus: true,
                    decoration: const InputDecoration(labelText: 'السبب')),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(dialogContext),
                      child: const Text('إلغاء')),
                  FilledButton(
                      onPressed: () => Navigator.pop(
                          dialogContext,
                          controller.text.trim().isEmpty
                              ? null
                              : controller.text.trim()),
                      child: const Text('تأكيد'))
                ]));
    controller.dispose();
    return result;
  }

  String _status(String value) => switch (value) {
        'New' => 'جديد',
        'Open' => 'مفتوح',
        'Posted' => 'مرحل',
        'Reversed' => 'معكوس',
        'Completed' => 'مكتمل',
        _ => value.isEmpty ? 'غير محدد' : value
      };
}

class _ActionBar extends StatelessWidget {
  const _ActionBar(
      {required this.label, required this.icon, required this.onPressed});
  final String label;
  final IconData icon;
  final VoidCallback? onPressed;
  @override
  Widget build(BuildContext context) => Padding(
      padding: const EdgeInsets.all(12),
      child: Align(
          alignment: Alignment.centerRight,
          child: FilledButton.icon(
              onPressed: onPressed, icon: Icon(icon), label: Text(label))));
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Center(
      child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            const Icon(Icons.error_outline, size: 48, color: Colors.redAccent),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
                label: const Text('إعادة المحاولة'))
          ])));
}
