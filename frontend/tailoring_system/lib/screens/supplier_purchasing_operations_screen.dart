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
  List<SupplierPurchasingCashAccount> _cashAccounts = const [];
  List<SupplierPurchasingInventoryItem> _inventoryItems = const [];
  List<SupplierPurchasingWarehouse> _warehouses = const [];
  String _supplierQuery = '';

  AuthUser? get _user => widget.auth.user;
  String get _normalizedRole =>
      (_user?.role ?? '').trim().split(RegExp(r'\s+')).join(' ').toLowerCase();
  bool get _isAdministrator => _normalizedRole == 'system administrator';
  bool get _isFinancialManager =>
      _normalizedRole == 'authorized financial manager';
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

  Future<void> _loadOperationalReferences() async {
    final values = await Future.wait([
      _repository.getSuppliers(),
      _repository.getOrders(),
      _repository.getInvoices(),
      _repository.getPayments(),
      _repository.getReceipts(),
      _repository.getCashAccounts(),
      _repository.getInventoryItems(),
      _repository.getWarehouses(),
    ]);
    if (!mounted) return;
    setState(() {
      _suppliers = values[0] as List<SupplierPurchasingSupplier>;
      _orders = values[1] as List<SupplierPurchasingOrder>;
      _invoices = values[2] as List<SupplierPurchasingInvoice>;
      _payments = values[3] as List<SupplierPurchasingPayment>;
      _receipts = values[4] as List<SupplierPurchasingReceipt>;
      _cashAccounts = values[5] as List<SupplierPurchasingCashAccount>;
      _inventoryItems = values[6] as List<SupplierPurchasingInventoryItem>;
      _warehouses = values[7] as List<SupplierPurchasingWarehouse>;
      _loadedTabs.addAll([0, 1, 2, 3, 4]);
    });
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

  Widget _suppliersView() {
    final query = _supplierQuery.trim().toLowerCase();
    final suppliers = query.isEmpty
        ? _suppliers
        : _suppliers
            .where((supplier) =>
                supplier.name.toLowerCase().contains(query) ||
                supplier.code.toLowerCase().contains(query) ||
                (supplier.phone ?? '').toLowerCase().contains(query))
            .toList();
    return Column(children: [
      Padding(
        padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
        child: Row(children: [
          Expanded(
            child: TextField(
              decoration: const InputDecoration(
                  labelText: 'البحث عن مورد', prefixIcon: Icon(Icons.search)),
              onChanged: (value) => setState(() => _supplierQuery = value),
            ),
          ),
          if (_isAdministrator) ...[
            const SizedBox(width: 12),
            FilledButton.icon(
                onPressed: _busy ? null : _createSupplier,
                icon: const Icon(Icons.person_add_alt_1),
                label: const Text('إضافة مورد'))
          ]
        ]),
      ),
      Expanded(
        child: _listOrEmpty(
          suppliers,
          (supplier) => ListTile(
            leading: const CircleAvatar(child: Icon(Icons.person_outline)),
            title: Text(supplier.name),
            subtitle: Text(
                '${supplier.code}  •  ${supplier.phone ?? 'لا يوجد هاتف'}'),
          ),
          empty: query.isEmpty
              ? 'لا يوجد موردون مسجلون.'
              : 'لا توجد نتائج مطابقة.',
        ),
      )
    ]);
  }

  Widget _ordersView() => _listOrEmpty(
        _orders,
        (order) => ListTile(
          leading: const Icon(Icons.shopping_cart_outlined),
          title: Text(order.number),
          subtitle: Text(
              '${_supplierName(order.supplierId)}  •  ${_date.format(order.date)}'),
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
                    '${_supplierName(invoice.supplierId)}  •  المتبقي ${invoice.outstanding.toStringAsFixed(2)}'),
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
                    '${_supplierName(payment.supplierId)}  •  ${_date.format(payment.date)}'),
                trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                  Text(payment.amount.toStringAsFixed(2)),
                  if (_canFinancial)
                    IconButton(
                        onPressed:
                            _busy ? null : () => _manageAllocations(payment),
                        tooltip: 'إدارة تخصيصات الدفعة',
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
                    '${_supplierName(receipt.supplierId)}  •  ${receipt.purchaseOrderId == null ? 'استلام مباشر' : 'مرتبط بأمر شراء'}'),
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

  Future<void> _createSupplier() async {
    final formKey = GlobalKey<FormState>();
    final code = TextEditingController();
    final name = TextEditingController();
    final phone = TextEditingController();
    final email = TextEditingController();
    final address = TextEditingController();
    final save = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('إضافة مورد'),
        content: SizedBox(
          width: 480,
          child: Form(
            key: formKey,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                _field(code, 'رمز المورد',
                    required: true, autofocus: true, maxLength: 50),
                _field(name, 'اسم المورد', required: true, maxLength: 200),
                _field(phone, 'رقم الهاتف', maxLength: 50),
                _field(email, 'البريد الإلكتروني', email: true, maxLength: 200),
                _field(address, 'العنوان', last: true, maxLength: 500,
                    onSubmitted: () {
                  if (formKey.currentState!.validate()) {
                    Navigator.pop(dialogContext, true);
                  }
                }),
              ]),
            ),
          ),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('إلغاء')),
          FilledButton.icon(
              onPressed: () {
                if (formKey.currentState!.validate()) {
                  Navigator.pop(dialogContext, true);
                }
              },
              icon: const Icon(Icons.save),
              label: const Text('حفظ المورد'))
        ],
      ),
    );
    if (save == true) {
      await _run(() async {
        await _repository.createSupplier(
            code: code.text.trim(),
            name: name.text.trim(),
            phone: _nullable(phone.text),
            email: _nullable(email.text),
            address: _nullable(address.text));
        _loadedTabs.remove(0);
      }, success: 'تم إنشاء المورد وتحديث القائمة.');
    }
    for (final controller in [code, name, phone, email, address]) {
      controller.dispose();
    }
  }

  Future<void> _createInvoice() async {
    await _loadOperationalReferences();
    if (!mounted || _suppliers.isEmpty) {
      return _showMessage('لا يوجد مورد فعال.');
    }
    final formKey = GlobalKey<FormState>();
    final number = TextEditingController();
    final amount = TextEditingController();
    final notes = TextEditingController();
    SupplierPurchasingSupplier? supplier;
    SupplierPurchasingOrder? order;
    DateTime invoiceDate = DateTime.now();
    DateTime dueDate = DateTime.now();
    String currency = 'YER';
    final currencies =
        <String>{'YER', ..._cashAccounts.map((e) => e.currency)}.toList();
    final save = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('فاتورة مورد جديدة'),
          content: SizedBox(
            width: 520,
            child: Form(
              key: formKey,
              child: SingleChildScrollView(
                child: Column(mainAxisSize: MainAxisSize.min, children: [
                  DropdownButtonFormField<SupplierPurchasingSupplier>(
                    initialValue: supplier,
                    decoration: const InputDecoration(labelText: 'المورد'),
                    items: _suppliers
                        .map((item) => DropdownMenuItem(
                            value: item,
                            child: Text('${item.name} (${item.code})')))
                        .toList(),
                    onChanged: (value) => setDialogState(() {
                      supplier = value;
                      order = null;
                    }),
                    validator: (value) => value == null ? 'اختر المورد.' : null,
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<SupplierPurchasingOrder?>(
                    key: ValueKey('invoice-order-${supplier?.id}'),
                    initialValue: order,
                    decoration: const InputDecoration(
                        labelText: 'أمر الشراء (اختياري)'),
                    items: [
                      const DropdownMenuItem(
                          value: null,
                          child: Text('فاتورة مباشرة دون أمر شراء')),
                      ..._orders
                          .where((item) => item.supplierId == supplier?.id)
                          .map((item) => DropdownMenuItem(
                              value: item, child: Text(item.number)))
                    ],
                    onChanged: (value) => setDialogState(() => order = value),
                  ),
                  _field(number, 'رقم الفاتورة', required: true),
                  _dateRow('تاريخ الفاتورة', invoiceDate, () async {
                    final value = await _pickDate(invoiceDate);
                    if (value != null) {
                      setDialogState(() => invoiceDate = value);
                    }
                  }),
                  _dateRow('تاريخ الاستحقاق', dueDate, () async {
                    final value = await _pickDate(dueDate);
                    if (value != null) setDialogState(() => dueDate = value);
                  }),
                  _field(amount, 'المبلغ', required: true, number: true),
                  DropdownButtonFormField<String>(
                      initialValue: currency,
                      decoration: const InputDecoration(labelText: 'العملة'),
                      items: currencies
                          .map((item) =>
                              DropdownMenuItem(value: item, child: Text(item)))
                          .toList(),
                      onChanged: (value) =>
                          setDialogState(() => currency = value ?? 'YER')),
                  _field(notes, 'الملاحظات', last: true, onSubmitted: () {
                    final parsed = double.tryParse(amount.text.trim());
                    if (formKey.currentState!.validate() &&
                        parsed != null &&
                        parsed > 0 &&
                        !dueDate.isBefore(invoiceDate)) {
                      Navigator.pop(dialogContext, true);
                    }
                  }),
                ]),
              ),
            ),
          ),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(dialogContext, false),
                child: const Text('إلغاء')),
            FilledButton(
                onPressed: () {
                  final parsed = double.tryParse(amount.text.trim());
                  if (formKey.currentState!.validate() &&
                      parsed != null &&
                      parsed > 0 &&
                      dueDate.isBefore(invoiceDate)) {
                    _showMessage('تاريخ الاستحقاق لا يسبق تاريخ الفاتورة.');
                  } else if (formKey.currentState!.validate() &&
                      parsed != null &&
                      parsed > 0) {
                    Navigator.pop(dialogContext, true);
                  }
                },
                child: const Text('حفظ الفاتورة'))
          ],
        ),
      ),
    );
    if (save == true && supplier != null) {
      final duplicate = _invoices.any((item) =>
          item.supplierId == supplier!.id &&
          item.number.trim().toLowerCase() == number.text.trim().toLowerCase());
      if (duplicate) {
        _showMessage('توجد فاتورة للمورد بالرقم نفسه.');
      } else {
        await _run(
            () => _repository.createInvoice(
                supplierId: supplier!.id,
                invoiceNumber: number.text.trim(),
                invoiceDate: invoiceDate,
                dueDate: dueDate,
                amount: double.parse(amount.text.trim()),
                purchaseOrderId: order?.id,
                currencyCode: currency,
                notes: _nullable(notes.text)),
            success: 'تم إنشاء فاتورة المورد.');
      }
    }
    for (final controller in [number, amount, notes]) {
      controller.dispose();
    }
  }

  Future<void> _createPayment() async {
    await _loadOperationalReferences();
    if (!mounted || _suppliers.isEmpty || _cashAccounts.isEmpty) {
      return _showMessage('بيانات الموردين أو الحسابات النقدية غير متاحة.');
    }
    final formKey = GlobalKey<FormState>();
    final amount = TextEditingController();
    final method = TextEditingController();
    final reference = TextEditingController();
    final notes = TextEditingController();
    SupplierPurchasingSupplier? supplier;
    SupplierPurchasingInvoice? invoice;
    SupplierPurchasingCashAccount? account;
    DateTime paymentDate = DateTime.now();
    int kind = 0;
    final save = await showDialog<bool>(
        context: context,
        builder: (dialogContext) =>
            StatefulBuilder(builder: (context, setDialogState) {
              final invoices = _invoices
                  .where((item) =>
                      item.supplierId == supplier?.id &&
                      item.outstanding > 0 &&
                      item.status != 'Reversed')
                  .toList();
              return AlertDialog(
                  title: const Text('دفعة مورد جديدة'),
                  content: SizedBox(
                      width: 520,
                      child: Form(
                          key: formKey,
                          child: SingleChildScrollView(
                              child: Column(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                DropdownButtonFormField<
                                        SupplierPurchasingSupplier>(
                                    initialValue: supplier,
                                    decoration: const InputDecoration(
                                        labelText: 'المورد'),
                                    items: _suppliers
                                        .map((item) => DropdownMenuItem(
                                            value: item,
                                            child: Text(
                                                '${item.name} (${item.code})')))
                                        .toList(),
                                    onChanged: (value) => setDialogState(() {
                                          supplier = value;
                                          invoice = null;
                                        }),
                                    validator: (value) =>
                                        value == null ? 'اختر المورد.' : null),
                                const SizedBox(height: 10),
                                DropdownButtonFormField<int>(
                                    initialValue: kind,
                                    decoration: const InputDecoration(
                                        labelText: 'نوع الدفعة'),
                                    items: const [
                                      DropdownMenuItem(
                                          value: 0, child: Text('دفعة فورية')),
                                      DropdownMenuItem(
                                          value: 1, child: Text('دفعة لاحقة')),
                                      DropdownMenuItem(
                                          value: 2, child: Text('دفعة مقدمة'))
                                    ],
                                    onChanged: (value) => setDialogState(() {
                                          kind = value ?? 0;
                                          if (kind == 2) invoice = null;
                                        })),
                                const SizedBox(height: 10),
                                if (kind != 2)
                                  DropdownButtonFormField<
                                          SupplierPurchasingInvoice>(
                                      key: ValueKey(
                                          'payment-invoice-${supplier?.id}-$kind'),
                                      initialValue: invoice,
                                      decoration: const InputDecoration(
                                          labelText: 'الفاتورة'),
                                      items: invoices
                                          .map((item) => DropdownMenuItem(
                                              value: item,
                                              child: Text(
                                                  '${item.number} - المتبقي ${item.outstanding.toStringAsFixed(2)}')))
                                          .toList(),
                                      onChanged: (value) =>
                                          setDialogState(() => invoice = value),
                                      validator: (value) => value == null
                                          ? 'اختر الفاتورة.'
                                          : null),
                                _field(amount, 'المبلغ',
                                    required: true, number: true),
                                DropdownButtonFormField<
                                        SupplierPurchasingCashAccount>(
                                    initialValue: account,
                                    decoration: const InputDecoration(
                                        labelText: 'الحساب النقدي'),
                                    items: _cashAccounts
                                        .map((item) => DropdownMenuItem(
                                            value: item,
                                            child: Text(
                                                '${item.name} - الرصيد ${item.balance.toStringAsFixed(2)} ${item.currency}')))
                                        .toList(),
                                    onChanged: (value) =>
                                        setDialogState(() => account = value),
                                    validator: (value) => value == null
                                        ? 'اختر الحساب النقدي.'
                                        : null),
                                _field(method, 'طريقة الدفع', required: true),
                                _field(reference, 'الرقم المرجعي',
                                    required: true),
                                _dateRow('تاريخ الدفعة', paymentDate, () async {
                                  final value = await _pickDate(paymentDate);
                                  if (value != null) {
                                    setDialogState(() => paymentDate = value);
                                  }
                                }),
                                _field(notes, 'الملاحظات', last: true,
                                    onSubmitted: () {
                                  final value =
                                      double.tryParse(amount.text.trim());
                                  if (formKey.currentState!.validate() &&
                                      value != null &&
                                      value > 0 &&
                                      (invoice == null ||
                                          value <= invoice!.outstanding) &&
                                      (account == null ||
                                          value <= account!.balance)) {
                                    Navigator.pop(dialogContext, true);
                                  }
                                }),
                              ])))),
                  actions: [
                    TextButton(
                        onPressed: () => Navigator.pop(dialogContext, false),
                        child: const Text('إلغاء')),
                    FilledButton(
                        onPressed: () {
                          final value = double.tryParse(amount.text.trim());
                          if (!formKey.currentState!.validate() ||
                              value == null ||
                              value <= 0) {
                            return;
                          }
                          if (invoice != null && value > invoice!.outstanding) {
                            _showMessage('المبلغ يتجاوز المتبقي على الفاتورة.');
                            return;
                          }
                          if (account != null && value > account!.balance) {
                            _showMessage('المبلغ يتجاوز رصيد الحساب النقدي.');
                            return;
                          }
                          Navigator.pop(dialogContext, true);
                        },
                        child: const Text('حفظ الدفعة'))
                  ]);
            }));
    if (save == true && supplier != null && account != null) {
      final duplicate = _payments.any((item) =>
          item.reference?.trim().toLowerCase() ==
          reference.text.trim().toLowerCase());
      if (duplicate) {
        _showMessage('توجد دفعة بالرقم المرجعي نفسه.');
      } else {
        await _run(
            () => _repository.createPayment(
                supplierId: supplier!.id,
                amount: double.parse(amount.text.trim()),
                paymentDate: paymentDate,
                cashAccountId: account!.id,
                paymentKind: kind,
                paymentMethod: method.text.trim(),
                referenceNumber: reference.text.trim(),
                supplierInvoiceId: invoice?.id,
                currencyCode: account!.currency,
                notes: _nullable(notes.text)),
            success: 'تم إنشاء دفعة المورد وتحديث الأرصدة.');
      }
    }
    for (final controller in [amount, method, reference, notes]) {
      controller.dispose();
    }
  }

  Future<void> _manageAllocations(SupplierPurchasingPayment payment) async {
    final allocations =
        await _repository.getSupplierAllocations(payment.supplierId);
    if (!mounted) return;
    final paymentAllocations =
        allocations.where((item) => item.paymentId == payment.id).toList();
    final allocated =
        paymentAllocations.fold<double>(0, (sum, item) => sum + item.amount);
    final unallocated = payment.amount - allocated;
    final action = await showDialog<String>(
        context: context,
        builder: (dialogContext) => AlertDialog(
              title: Text('تخصيصات ${payment.number}'),
              content: SizedBox(
                  width: 520,
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                    ListTile(
                        title: const Text('الرصيد غير المخصص'),
                        trailing: Text(unallocated.toStringAsFixed(2))),
                    if (paymentAllocations.isEmpty)
                      const Padding(
                          padding: EdgeInsets.all(16),
                          child: Text('لا توجد تخصيصات لهذه الدفعة.')),
                    ...paymentAllocations.map((item) => ListTile(
                        title: Text('فاتورة ${_invoiceNumber(item.invoiceId)}'),
                        subtitle: Text(item.amount.toStringAsFixed(2)),
                        trailing: _canReverse
                            ? IconButton(
                                tooltip: 'عكس التخصيص',
                                icon: const Icon(Icons.undo),
                                onPressed: () => Navigator.pop(
                                    dialogContext, 'reverse:${item.id}'))
                            : null))
                  ])),
              actions: [
                TextButton(
                    onPressed: () => Navigator.pop(dialogContext),
                    child: const Text('إغلاق')),
                if (unallocated > 0)
                  FilledButton.icon(
                      onPressed: () => Navigator.pop(dialogContext, 'add'),
                      icon: const Icon(Icons.add_link),
                      label: const Text('تخصيص جديد'))
              ],
            ));
    if (action == 'add') await _createAllocation(payment, unallocated);
    if (action?.startsWith('reverse:') == true) {
      await _reverseFinancial('Allocation', int.parse(action!.split(':').last));
    }
  }

  Future<void> _createAllocation(
      SupplierPurchasingPayment payment, double unallocated) async {
    await _loadOperationalReferences();
    if (!mounted) return;
    final candidates = _invoices
        .where((item) =>
            item.supplierId == payment.supplierId &&
            item.outstanding > 0 &&
            item.status != 'Reversed')
        .toList();
    if (candidates.isEmpty) {
      return _showMessage('لا توجد فواتير مفتوحة لهذا المورد.');
    }
    SupplierPurchasingInvoice? invoice;
    final amount = TextEditingController();
    final reference = TextEditingController();
    final formKey = GlobalKey<FormState>();
    final save = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => AlertDialog(
              title: const Text('تخصيص دفعة'),
              content: SizedBox(
                  width: 460,
                  child: Form(
                      key: formKey,
                      child: Column(mainAxisSize: MainAxisSize.min, children: [
                        Text(
                            'الرصيد غير المخصص: ${unallocated.toStringAsFixed(2)}'),
                        DropdownButtonFormField<SupplierPurchasingInvoice>(
                            initialValue: invoice,
                            decoration:
                                const InputDecoration(labelText: 'الفاتورة'),
                            items: candidates
                                .map((item) => DropdownMenuItem(
                                    value: item,
                                    child: Text(
                                        '${item.number} - ${item.outstanding.toStringAsFixed(2)}')))
                                .toList(),
                            onChanged: (value) => invoice = value,
                            validator: (value) =>
                                value == null ? 'اختر الفاتورة.' : null),
                        _field(amount, 'مبلغ التخصيص',
                            required: true, number: true),
                        _field(reference, 'الرقم المرجعي',
                            required: true, last: true, onSubmitted: () {
                          final value = double.tryParse(amount.text.trim());
                          if (formKey.currentState!.validate() &&
                              value != null &&
                              value > 0 &&
                              value <= unallocated &&
                              invoice != null &&
                              value <= invoice!.outstanding) {
                            Navigator.pop(dialogContext, true);
                          }
                        }),
                      ]))),
              actions: [
                TextButton(
                    onPressed: () => Navigator.pop(dialogContext, false),
                    child: const Text('إلغاء')),
                FilledButton(
                    onPressed: () {
                      final value = double.tryParse(amount.text.trim());
                      if (!formKey.currentState!.validate() ||
                          value == null ||
                          value <= 0 ||
                          value > unallocated ||
                          (invoice != null && value > invoice!.outstanding)) {
                        _showMessage('مبلغ التخصيص يتجاوز الرصيد المتاح.');
                        return;
                      }
                      Navigator.pop(dialogContext, true);
                    },
                    child: const Text('حفظ التخصيص'))
              ],
            ));
    if (save == true && invoice != null) {
      await _run(
          () => _repository.createAllocation(
              paymentId: payment.id,
              invoiceId: invoice!.id,
              amount: double.parse(amount.text.trim()),
              referenceNumber: reference.text.trim()),
          success: 'تم تخصيص الدفعة على الفاتورة.');
    }
    amount.dispose();
    reference.dispose();
  }

  Future<void> _createReceipt() async {
    await _loadOperationalReferences();
    if (!mounted ||
        _suppliers.isEmpty ||
        _warehouses.isEmpty ||
        _inventoryItems.isEmpty) {
      return _showMessage(
          'بيانات الموردين أو المستودعات أو الأصناف غير متاحة.');
    }
    final formKey = GlobalKey<FormState>();
    final number = TextEditingController();
    final notes = TextEditingController();
    final lines = <_ReceiptDraftLine>[_ReceiptDraftLine()];
    SupplierPurchasingSupplier? supplier;
    SupplierPurchasingWarehouse? warehouse;
    SupplierPurchasingOrder? order;
    DateTime receiptDate = DateTime.now();
    final save = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => StatefulBuilder(
            builder: (context, setDialogState) => AlertDialog(
                    title: const Text('استلام بضاعة جديد'),
                    content: SizedBox(
                        width: 680,
                        child: Form(
                            key: formKey,
                            child: SingleChildScrollView(
                                child: Column(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                  DropdownButtonFormField<
                                          SupplierPurchasingSupplier>(
                                      initialValue: supplier,
                                      decoration: const InputDecoration(
                                          labelText: 'المورد'),
                                      items: _suppliers
                                          .map((item) => DropdownMenuItem(
                                              value: item,
                                              child: Text(
                                                  '${item.name} (${item.code})')))
                                          .toList(),
                                      onChanged: (value) => setDialogState(() {
                                            supplier = value;
                                            order = null;
                                          }),
                                      validator: (value) => value == null
                                          ? 'اختر المورد.'
                                          : null),
                                  const SizedBox(height: 10),
                                  DropdownButtonFormField<
                                          SupplierPurchasingOrder?>(
                                      key: ValueKey(
                                          'receipt-order-${supplier?.id}'),
                                      initialValue: order,
                                      decoration: const InputDecoration(
                                          labelText: 'أمر الشراء (اختياري)'),
                                      items: [
                                        const DropdownMenuItem(
                                            value: null,
                                            child: Text(
                                                'استلام مباشر دون أمر شراء')),
                                        ..._orders
                                            .where((item) =>
                                                item.supplierId == supplier?.id)
                                            .map((item) => DropdownMenuItem(
                                                value: item,
                                                child: Text(item.number)))
                                      ],
                                      onChanged: (value) =>
                                          setDialogState(() => order = value)),
                                  const SizedBox(height: 10),
                                  DropdownButtonFormField<
                                          SupplierPurchasingWarehouse>(
                                      initialValue: warehouse,
                                      decoration: const InputDecoration(
                                          labelText: 'المستودع'),
                                      items: _warehouses
                                          .map((item) => DropdownMenuItem(
                                              value: item,
                                              child: Text(
                                                  '${item.name} (${item.code})')))
                                          .toList(),
                                      onChanged: (value) => setDialogState(
                                          () => warehouse = value),
                                      validator: (value) => value == null
                                          ? 'اختر المستودع.'
                                          : null),
                                  _field(number, 'رقم الاستلام',
                                      required: true),
                                  _dateRow('تاريخ الاستلام', receiptDate,
                                      () async {
                                    final value = await _pickDate(receiptDate);
                                    if (value != null) {
                                      setDialogState(() => receiptDate = value);
                                    }
                                  }),
                                  const Divider(),
                                  for (var index = 0;
                                      index < lines.length;
                                      index++)
                                    _receiptLine(
                                        lines[index],
                                        index,
                                        () => setDialogState(() {
                                              lines[index].dispose();
                                              lines.removeAt(index);
                                            })),
                                  Align(
                                      alignment: Alignment.centerRight,
                                      child: TextButton.icon(
                                          onPressed: () => setDialogState(() =>
                                              lines.add(_ReceiptDraftLine())),
                                          icon: const Icon(Icons.add),
                                          label: const Text('إضافة صنف'))),
                                  _field(notes, 'الملاحظات', last: true,
                                      onSubmitted: () {
                                    if (formKey.currentState!.validate() &&
                                        lines.isNotEmpty &&
                                        lines.every((line) =>
                                            line.item != null &&
                                            (double.tryParse(
                                                        line.quantity.text) ??
                                                    0) >
                                                0 &&
                                            (double.tryParse(line.cost.text) ??
                                                    0) >
                                                0) &&
                                        lines
                                                .map((line) => line.item?.id)
                                                .toSet()
                                                .length ==
                                            lines.length) {
                                      Navigator.pop(dialogContext, true);
                                    }
                                  }),
                                ])))),
                    actions: [
                      TextButton(
                          onPressed: () => Navigator.pop(dialogContext, false),
                          child: const Text('إلغاء')),
                      FilledButton(
                          onPressed: () {
                            if (!formKey.currentState!.validate() ||
                                lines.isEmpty ||
                                lines.any((line) =>
                                    line.item == null ||
                                    (double.tryParse(line.quantity.text) ??
                                            0) <=
                                        0 ||
                                    (double.tryParse(line.cost.text) ?? 0) <=
                                        0) ||
                                lines
                                        .map((line) => line.item?.id)
                                        .toSet()
                                        .length !=
                                    lines.length) {
                              _showMessage(
                                  'راجع البنود؛ يجب اختيار أصناف مختلفة وكميات وتكاليف صحيحة.');
                              return;
                            }
                            Navigator.pop(dialogContext, true);
                          },
                          child: const Text('حفظ الاستلام'))
                    ])));
    if (save == true && supplier != null && warehouse != null) {
      final duplicate = _receipts.any((item) =>
          item.number.trim().toLowerCase() == number.text.trim().toLowerCase());
      if (duplicate) {
        _showMessage('يوجد استلام بالرقم نفسه.');
      } else {
        await _run(
            () => _repository.createReceipt(
                supplierId: supplier!.id,
                purchaseOrderId: order?.id,
                warehouseId: warehouse!.id,
                receiptNumber: number.text.trim(),
                receiptDate: receiptDate,
                notes: _nullable(notes.text),
                items: lines
                    .map((line) => {
                          'inventoryItemId': line.item!.id,
                          'quantity': double.parse(line.quantity.text),
                          'unitCost': double.parse(line.cost.text),
                          'rollCode': _nullable(line.rollCode.text)
                        })
                    .toList()),
            success: 'تم تسجيل الاستلام وتحديث المخزون.');
      }
    }
    number.dispose();
    notes.dispose();
    for (final line in lines) {
      line.dispose();
    }
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
      if (matching.differences.isNotEmpty && mounted) {
        await showDialog<void>(
          context: context,
          builder: (dialogContext) => AlertDialog(
            title: const Text('فروقات المطابقة'),
            content: SizedBox(
              width: 520,
              child: ListView(
                shrinkWrap: true,
                children: matching.differences
                    .map((difference) => ListTile(
                          leading: const Icon(Icons.warning_amber),
                          title: Text(_differenceType(difference.type)),
                          subtitle: Text(_differenceDetails(difference)),
                        ))
                    .toList(),
              ),
            ),
            actions: [
              TextButton(
                  onPressed: () => Navigator.pop(dialogContext),
                  child: const Text('إغلاق'))
            ],
          ),
        );
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.toString())));
      }
    }
  }

  Widget _field(TextEditingController controller, String label,
      {bool required = false,
      bool number = false,
      bool email = false,
      bool autofocus = false,
      bool last = false,
      int? maxLength,
      VoidCallback? onSubmitted}) {
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: TextFormField(
        controller: controller,
        autofocus: autofocus,
        keyboardType: number
            ? const TextInputType.numberWithOptions(decimal: true)
            : email
                ? TextInputType.emailAddress
                : TextInputType.text,
        textInputAction: last ? TextInputAction.done : TextInputAction.next,
        maxLength: maxLength,
        decoration: InputDecoration(labelText: label),
        onFieldSubmitted:
            last && onSubmitted != null ? (_) => onSubmitted() : null,
        validator: (value) {
          final text = value?.trim() ?? '';
          if (required && text.isEmpty) return 'هذا الحقل مطلوب.';
          if (number &&
              text.isNotEmpty &&
              (double.tryParse(text) == null || double.parse(text) <= 0)) {
            return 'أدخل رقمًا أكبر من صفر.';
          }
          if (email &&
              text.isNotEmpty &&
              !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(text)) {
            return 'أدخل بريدًا إلكترونيًا صحيحًا.';
          }
          return null;
        },
      ),
    );
  }

  Widget _dateRow(String label, DateTime value, VoidCallback onPressed) =>
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text(label),
        subtitle: Text(_date.format(value)),
        trailing: IconButton(
            onPressed: onPressed,
            tooltip: 'اختيار التاريخ',
            icon: const Icon(Icons.calendar_month)),
      );

  Widget _receiptLine(
          _ReceiptDraftLine line, int index, VoidCallback onRemove) =>
      Card(
        margin: const EdgeInsets.only(bottom: 8),
        child: Padding(
          padding: const EdgeInsets.all(10),
          child: Column(children: [
            Row(children: [
              Expanded(child: Text('البند ${index + 1}')),
              IconButton(
                  onPressed: onRemove,
                  tooltip: 'حذف البند',
                  icon: const Icon(Icons.delete_outline))
            ]),
            DropdownButtonFormField<SupplierPurchasingInventoryItem>(
              initialValue: line.item,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'الصنف'),
              items: _inventoryItems
                  .map((item) => DropdownMenuItem(
                      value: item, child: Text('${item.name} (${item.code})')))
                  .toList(),
              onChanged: (value) => line.item = value,
              validator: (value) => value == null ? 'اختر الصنف.' : null,
            ),
            Row(children: [
              Expanded(
                  child: _field(line.quantity, 'الكمية',
                      required: true, number: true)),
              const SizedBox(width: 10),
              Expanded(
                  child: _field(line.cost, 'تكلفة الوحدة',
                      required: true, number: true)),
            ]),
            _field(line.rollCode, 'رمز اللفة (عند استلام القماش)'),
          ]),
        ),
      );

  Future<DateTime?> _pickDate(DateTime initial) => showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 3650)));

  String _supplierName(int supplierId) {
    for (final supplier in _suppliers) {
      if (supplier.id == supplierId) return supplier.name;
    }
    return 'مورد غير متاح';
  }

  String _invoiceNumber(int invoiceId) {
    for (final invoice in _invoices) {
      if (invoice.id == invoiceId) return invoice.number;
    }
    return 'غير متاحة';
  }

  String _differenceType(String value) => switch (value) {
        'PurchaseOrderItemMissing' => 'الصنف غير موجود في أمر الشراء',
        'PurchaseOrderQuantityVariance' => 'فرق في الكمية',
        'PurchaseOrderCostVariance' => 'فرق في تكلفة الوحدة',
        'SupplierInvoiceLineMissing' => 'بند الفاتورة غير موجود',
        'SupplierInvoiceQuantityVariance' => 'فرق كمية مع الفاتورة',
        'SupplierInvoiceCostVariance' => 'فرق تكلفة مع الفاتورة',
        _ => 'فرق في المطابقة'
      };

  String _differenceDetails(SupplierPurchasingDifference difference) {
    final parts = <String>[];
    if (difference.expectedQuantity != null) {
      parts.add('الكمية المتوقعة ${difference.expectedQuantity}');
    }
    if (difference.actualQuantity != null) {
      parts.add('الكمية الفعلية ${difference.actualQuantity}');
    }
    if (difference.expectedUnitCost != null) {
      parts.add('التكلفة المتوقعة ${difference.expectedUnitCost}');
    }
    if (difference.actualUnitCost != null) {
      parts.add('التكلفة الفعلية ${difference.actualUnitCost}');
    }
    return parts.isEmpty ? 'راجع بيانات البند.' : parts.join('  •  ');
  }

  String? _nullable(String value) => value.trim().isEmpty ? null : value.trim();

  void _showMessage(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
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

class _ReceiptDraftLine {
  SupplierPurchasingInventoryItem? item;
  final quantity = TextEditingController();
  final cost = TextEditingController();
  final rollCode = TextEditingController();

  void dispose() {
    quantity.dispose();
    cost.dispose();
    rollCode.dispose();
  }
}
