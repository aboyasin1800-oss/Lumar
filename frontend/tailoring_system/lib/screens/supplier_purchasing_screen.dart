import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:intl/intl.dart';

import '../core/ui_palette.dart';
import '../repositories/supplier_repository.dart';

class SupplierPurchasingScreen extends StatefulWidget {
  const SupplierPurchasingScreen({super.key});

  @override
  State<SupplierPurchasingScreen> createState() => _SupplierPurchasingScreenState();
}

class _SupplierPurchasingScreenState extends State<SupplierPurchasingScreen>
    with SingleTickerProviderStateMixin {
  static const _themeStorageKey = 'supplier_purchasing_screen_dark_mode';
  final _storage = const FlutterSecureStorage();

  late final TabController _tabController;
  bool _isDarkMode = false;
  bool _loading = true;
  String? _loadError;
  final SupplierRepository _repository = SupplierRepository();

  List<_SupplierRecord> _suppliers = const [];
  List<_InvoiceRecord> _invoices = const [];
  List<_PaymentRecord> _payments = const [];

  int _selectedSupplierId = 0;
  final NumberFormat _money = NumberFormat('#,##0.00');
  final DateFormat _dateFormat = DateFormat('dd/MM/yyyy');

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
    _loadData();
    _loadThemePreference();
  }

  Future<void> _loadData() async {
    try {
      final suppliers = await _repository.getSuppliers();
      final invoices = await _repository.getInvoices();
      final payments = await _repository.getPayments();

      if (!mounted) return;

      final mappedSuppliers = suppliers
          .map((supplier) => _SupplierRecord(
                id: supplier.id,
                name: supplier.name,
                phone: supplier.phone ?? '-',
                code: supplier.code,
                location: 'غير محدد',
                notes: supplier.email ?? 'لا توجد ملاحظات',
              ))
          .toList();

      final mappedInvoices = invoices
          .map((invoice) => _InvoiceRecord(
                id: invoice.id,
                supplierId: invoice.supplierId,
                invoiceNumber: invoice.invoiceNumber,
                invoiceDate: invoice.invoiceDate.toIso8601String().split('T').first,
                title: 'فاتورة مورد',
                totalAmount: invoice.totalAmount,
                paidAmount: invoice.amountPaid,
                previousBalance: 0,
                finalBalance: invoice.totalAmount - invoice.amountPaid,
                lineItems: const [],
              ))
          .toList();

      final mappedPayments = payments
          .map((payment) => _PaymentRecord(
                id: payment.id,
                supplierId: payment.supplierId,
                amount: payment.amount,
                against: payment.referenceNumber ?? 'من الحساب',
                date: payment.paymentDate.toIso8601String().split('T').first,
                receiptNumber: payment.paymentNumber,
                method: payment.paymentMethod ?? 'غير محدد',
                collector: payment.notes ?? '-',
                finalBalance: payment.amount,
              ))
          .toList();

      setState(() {
        _suppliers = mappedSuppliers;
        _invoices = mappedInvoices;
        _payments = mappedPayments;
        _selectedSupplierId = mappedSuppliers.isNotEmpty ? mappedSuppliers.first.id : 0;
        _loading = false;
        _loadError = null;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _loadError = error.toString();
      });
    }
  }

  Future<void> _loadThemePreference() async {
    final saved = await _storage.read(key: _themeStorageKey);
    if (!mounted) return;
    if (saved == 'dark') {
      setState(() => _isDarkMode = true);
    } else if (saved == 'light') {
      setState(() => _isDarkMode = false);
    }
  }

  Future<void> _toggleTheme() async {
    setState(() => _isDarkMode = !_isDarkMode);
    await _storage.write(key: _themeStorageKey, value: _isDarkMode ? 'dark' : 'light');
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  _SupplierRecord get selectedSupplier =>
      _suppliers.firstWhere((supplier) => supplier.id == _selectedSupplierId, orElse: () => const _SupplierRecord(id: 0, name: '-', phone: '-', code: '-', location: '-', notes: '-'));

  double _totalPurchasesForSupplier(int supplierId) => _invoices
      .where((invoice) => invoice.supplierId == supplierId)
      .fold<double>(0, (total, invoice) => total + invoice.totalAmount);

  double _totalPaymentsForSupplier(int supplierId) => _payments
      .where((payment) => payment.supplierId == supplierId)
      .fold<double>(0, (total, payment) => total + payment.amount);

  double _finalBalanceForSupplier(int supplierId) =>
      _totalPurchasesForSupplier(supplierId) - _totalPaymentsForSupplier(supplierId);

  List<_SettlementEntry> _settlementEntriesForSupplier(int supplierId) {
    final entries = <_SettlementEntry>[];

    for (final invoice in _invoices.where((item) => item.supplierId == supplierId)) {
      entries.add(_SettlementEntry(
        type: 'فاتورة',
        date: DateTime.parse(invoice.invoiceDate),
        title: invoice.invoiceNumber,
        amount: invoice.totalAmount,
        reference: invoice.invoiceNumber,
        previousBalance: invoice.previousBalance,
        finalBalance: invoice.finalBalance,
      ));
    }

    for (final payment in _payments.where((item) => item.supplierId == supplierId)) {
      entries.add(_SettlementEntry(
        type: 'دفعة',
        date: DateTime.parse(payment.date),
        title: payment.receiptNumber,
        amount: payment.amount,
        reference: payment.receiptNumber,
        previousBalance: payment.finalBalance,
        finalBalance: payment.finalBalance,
      ));
    }

    entries.sort((a, b) => a.date.compareTo(b.date));
    return entries;
  }

  ThemeData get _screenTheme => _isDarkMode ? _darkTheme : _lightTheme;

  ThemeData get _lightTheme => ThemeData(
        brightness: Brightness.light,
        colorScheme: ColorScheme.fromSeed(
          seedColor: UiPalette.primaryBlue,
          brightness: Brightness.light,
        ),
        scaffoldBackgroundColor: const Color(0xFFF3F7FA),
        cardColor: Colors.white,
        appBarTheme: const AppBarTheme(
          backgroundColor: Colors.white,
          foregroundColor: Color(0xFF10212B),
          elevation: 0,
        ),
        tabBarTheme: const TabBarThemeData(
          labelColor: Color(0xFF10212B),
          unselectedLabelColor: Color(0xFF607181),
        ),
        inputDecorationTheme: const InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          border: OutlineInputBorder(),
        ),
      );

  ThemeData get _darkTheme => ThemeData(
        brightness: Brightness.dark,
        colorScheme: ColorScheme.fromSeed(
          seedColor: UiPalette.primaryBlue,
          brightness: Brightness.dark,
        ),
        scaffoldBackgroundColor: UiPalette.screenBackground,
        cardColor: UiPalette.surfaceCard,
        appBarTheme: const AppBarTheme(
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.textMain,
          elevation: 0,
        ),
        tabBarTheme: const TabBarThemeData(
          labelColor: UiPalette.textMain,
          unselectedLabelColor: UiPalette.textSoft,
        ),
        inputDecorationTheme: const InputDecorationTheme(
          filled: true,
          fillColor: UiPalette.softBlue,
          border: OutlineInputBorder(
            borderSide: BorderSide(color: UiPalette.borderSoft),
          ),
          labelStyle: TextStyle(color: UiPalette.textSoft),
          hintStyle: TextStyle(color: UiPalette.textSoft),
        ),
      );

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return Theme(
        data: _screenTheme,
        child: Scaffold(
          appBar: AppBar(
            title: const Text('الموردون والمشتريات'),
            actions: [
              IconButton(
                tooltip: _isDarkMode ? 'الوضع الفاتح' : 'الوضع الداكن',
                onPressed: _toggleTheme,
                icon: Icon(_isDarkMode ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
              ),
            ],
          ),
          body: const Center(child: CircularProgressIndicator()),
        ),
      );
    }

    if (_loadError != null || _suppliers.isEmpty) {
      return Theme(
        data: _screenTheme,
        child: Scaffold(
          appBar: AppBar(
            title: const Text('الموردون والمشتريات'),
            actions: [
              IconButton(
                tooltip: _isDarkMode ? 'الوضع الفاتح' : 'الوضع الداكن',
                onPressed: _toggleTheme,
                icon: Icon(_isDarkMode ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
              ),
            ],
          ),
          body: Center(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                _loadError ?? 'لا توجد بيانات موردين في قاعدة البيانات',
                textAlign: TextAlign.center,
              ),
            ),
          ),
        ),
      );
    }

    final summary = _summaryForSupplier(selectedSupplier.id);

    return Theme(
      data: _screenTheme,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('الموردون والمشتريات'),
          actions: [
            IconButton(
              tooltip: _isDarkMode ? 'الوضع الفاتح' : 'الوضع الداكن',
              onPressed: _toggleTheme,
              icon: Icon(_isDarkMode ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
            ),
          ],
        ),
        body: Column(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: _isDarkMode ? UiPalette.surfaceCard : Colors.white,
                border: Border(
                  bottom: BorderSide(
                    color: _isDarkMode ? UiPalette.borderSoft : const Color(0xFFE4E8EE),
                  ),
                ),
              ),
              child: TabBar(
                controller: _tabController,
                isScrollable: true,
                tabAlignment: TabAlignment.start,
                labelPadding: const EdgeInsets.symmetric(horizontal: 18),
                indicatorColor: UiPalette.primaryBlue,
                tabs: const [
                  Tab(text: 'الموردون'),
                  Tab(text: 'فواتير المشتريات'),
                  Tab(text: 'دفعات الموردين'),
                  Tab(text: 'التسوية'),
                ],
              ),
            ),
            Expanded(
              child: TabBarView(
                controller: _tabController,
                children: [
                  _SuppliersTab(
                    suppliers: _suppliers,
                    selectedSupplierId: _selectedSupplierId,
                    onSelect: (supplierId) => setState(() => _selectedSupplierId = supplierId),
                    onAddSupplier: _showAddSupplierDialog,
                    summary: summary,
                    supplierTransactions: _supplierTransactionsFor(selectedSupplier.id),
                    onAddInvoice: _showAddInvoiceDialog,
                  ),
                  _PurchasesTab(
                    suppliers: _suppliers,
                    invoices: _invoices,
                    onAddInvoice: _showAddInvoiceDialog,
                    onViewInvoice: _showInvoicePreview,
                  ),
                  _PaymentsTab(
                    suppliers: _suppliers,
                    payments: _payments,
                    onAddPayment: _showAddPaymentDialog,
                  ),
                  _SettlementTab(
                    suppliers: _suppliers,
                    selectedSupplierId: _selectedSupplierId,
                    onSupplierChanged: (supplierId) => setState(() => _selectedSupplierId = supplierId),
                    entries: _settlementEntriesForSupplier(_selectedSupplierId),
                    totalPurchases: _totalPurchasesForSupplier(_selectedSupplierId),
                    totalPayments: _totalPaymentsForSupplier(_selectedSupplierId),
                    finalBalance: _finalBalanceForSupplier(_selectedSupplierId),
                    lastPaymentDate: _lastPaymentDate(_selectedSupplierId),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  String? _lastPaymentDate(int supplierId) {
    final paymentDates = _payments
        .where((payment) => payment.supplierId == supplierId)
        .map((payment) => DateTime.parse(payment.date))
        .toList();
    if (paymentDates.isEmpty) return null;
    paymentDates.sort();
    return _dateFormat.format(paymentDates.last);
  }

  _SupplierSummary _summaryForSupplier(int supplierId) {
    final totalPurchases = _totalPurchasesForSupplier(supplierId);
    final totalPayments = _totalPaymentsForSupplier(supplierId);
    final finalBalance = totalPurchases - totalPayments;
    return _SupplierSummary(
      totalPurchases: totalPurchases,
      creditor: totalPurchases,
      debtor: totalPayments,
      finalBalance: finalBalance,
    );
  }

  List<_SupplierTransaction> _supplierTransactionsFor(int supplierId) {
    final list = <_SupplierTransaction>[];
    for (final invoice in _invoices.where((item) => item.supplierId == supplierId)) {
      list.add(_SupplierTransaction(
        type: 'فاتورة',
        title: invoice.invoiceNumber,
        amount: invoice.totalAmount,
        date: DateTime.parse(invoice.invoiceDate),
        details: 'قيمة الفاتورة: ${_money.format(invoice.totalAmount)}',
      ));
    }
    for (final payment in _payments.where((item) => item.supplierId == supplierId)) {
      list.add(_SupplierTransaction(
        type: 'دفعة',
        title: payment.receiptNumber,
        amount: payment.amount,
        date: DateTime.parse(payment.date),
        details: 'المتحصل: ${payment.collector} • ${payment.method}',
      ));
    }
    list.sort((a, b) => b.date.compareTo(a.date));
    return list;
  }

  Future<void> _showAddSupplierDialog() async {
    final controllerName = TextEditingController();
    final controllerPhone = TextEditingController();
    final controllerCode = TextEditingController();
    final controllerLocation = TextEditingController();
    final controllerNotes = TextEditingController();

    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('إضافة مورد'),
        content: SizedBox(
          width: 520,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(controller: controllerName, decoration: const InputDecoration(labelText: 'اسم المورد')),
                const SizedBox(height: 12),
                TextField(controller: controllerPhone, decoration: const InputDecoration(labelText: 'الهاتف')),
                const SizedBox(height: 12),
                TextField(controller: controllerCode, decoration: const InputDecoration(labelText: 'الكود')),
                const SizedBox(height: 12),
                TextField(controller: controllerLocation, decoration: const InputDecoration(labelText: 'الموقع')),
                const SizedBox(height: 12),
                TextField(controller: controllerNotes, maxLines: 3, decoration: const InputDecoration(labelText: 'الملاحظات')),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
          FilledButton(
            onPressed: () {
              final name = controllerName.text.trim();
              final phone = controllerPhone.text.trim();
              final code = controllerCode.text.trim();
              final location = controllerLocation.text.trim();
              final notes = controllerNotes.text.trim();
              if (name.isEmpty || code.isEmpty) {
                return;
              }

              setState(() {
                _suppliers.add(_SupplierRecord(
                  id: (_suppliers.map((item) => item.id).fold<int>(0, (max, value) => value > max ? value : max) + 1),
                  name: name,
                  phone: phone.isEmpty ? '-' : phone,
                  code: code,
                  location: location.isEmpty ? '-' : location,
                  notes: notes.isEmpty ? 'لا توجد ملاحظات' : notes,
                ));
                _selectedSupplierId = _suppliers.last.id;
              });
              Navigator.pop(context);
            },
            child: const Text('حفظ'),
          ),
        ],
      ),
    );
  }

  Future<void> _showAddInvoiceDialog() async {
    var supplierId = _selectedSupplierId;
    final invoiceNumberController = TextEditingController(
      text: 'INV-${DateTime.now().millisecondsSinceEpoch % 100000}',
    );
    final dateController = TextEditingController(text: _dateFormat.format(DateTime.now()));
    final addressController = TextEditingController(text: selectedSupplier.location);
    final itemCountController = TextEditingController(text: '1');
    final lines = <_InvoiceLineDraft>[
      const _InvoiceLineDraft(name: '', unit: 'ياردة', quantity: 0, unitPrice: 0, note: ''),
    ];

    await showDialog<void>(
      context: context,
      builder: (context) => Theme(
        data: _screenTheme,
        child: StatefulBuilder(
          builder: (context, setStateDialog) {
            final totalAmount = lines.fold<double>(0, (sum, line) => sum + (line.quantity * line.unitPrice));
            final previousBalance = _finalBalanceForSupplier(supplierId);
            final finalBalance = previousBalance + totalAmount;

            return AlertDialog(
              insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
              title: const Text('إضافة فاتورة جديدة'),
              content: SizedBox(
                width: 980,
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      FocusTraversalGroup(
                        policy: OrderedTraversalPolicy(),
                        child: Wrap(
                          spacing: 12,
                          runSpacing: 12,
                          children: [
                            SizedBox(
                              width: 220,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(1),
                                child: DropdownButtonFormField<int>(
                                  value: supplierId,
                                  isExpanded: true,
                                  decoration: const InputDecoration(labelText: 'المورد'),
                                  items: _suppliers
                                      .map((supplier) => DropdownMenuItem(
                                          value: supplier.id,
                                          child: Text(supplier.name, overflow: TextOverflow.ellipsis)))
                                      .toList(),
                                  onChanged: (value) {
                                    if (value == null) return;
                                    setStateDialog(() {
                                      supplierId = value;
                                      final selected = _suppliers.firstWhere((supplier) => supplier.id == value);
                                      addressController.text = selected.location;
                                    });
                                  },
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 220,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(2),
                                child: TextField(
                                  controller: invoiceNumberController,
                                  decoration: const InputDecoration(labelText: 'رقم الفاتورة'),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 220,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(3),
                                child: TextField(
                                  controller: dateController,
                                  decoration: const InputDecoration(labelText: 'تاريخ الفاتورة'),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(4),
                                child: TextField(
                                  controller: addressController,
                                  decoration: const InputDecoration(labelText: 'عنوان المورد'),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          const Expanded(child: Text('بنود الفاتورة', style: TextStyle(fontWeight: FontWeight.bold))),
                          SizedBox(
                            width: 180,
                            child: TextField(
                              controller: itemCountController,
                              keyboardType: TextInputType.number,
                              decoration: const InputDecoration(labelText: 'عدد البنود'),
                              onChanged: (value) {
                                final count = int.tryParse(value) ?? 0;
                                if (count <= 0) {
                                  if (lines.isNotEmpty) {
                                    setStateDialog(() => lines.clear());
                                  }
                                  return;
                                }
                                setStateDialog(() {
                                  final nextLines = <_InvoiceLineDraft>[];
                                  for (int index = 0; index < count; index++) {
                                    nextLines.add(index < lines.length
                                        ? lines[index]
                                        : const _InvoiceLineDraft(name: '', unit: 'ياردة', quantity: 1, unitPrice: 0, note: ''));
                                  }
                                  lines.clear();
                                  lines.addAll(nextLines);
                                });
                              },
                              textAlign: TextAlign.center,
                              style: const TextStyle(fontSize: 14),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      ...List.generate(lines.length, (index) {
                        final line = lines[index];
                        final lineTotal = line.quantity * line.unitPrice;
                        final lineControllerName = TextEditingController(text: line.name);
                        final lineControllerQty = TextEditingController(text: line.quantity == 0 ? '' : line.quantity.toString());
                        final lineControllerPrice = TextEditingController(text: line.unitPrice == 0 ? '' : line.unitPrice.toString());
                        final lineControllerNote = TextEditingController(text: line.note);

                        return Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: FocusTraversalGroup(
                            policy: OrderedTraversalPolicy(),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(
                                  flex: 2,
                                  child: FocusTraversalOrder(
                                    order: const NumericFocusOrder(1),
                                    child: SizedBox(
                                      height: 40,
                                      child: TextField(
                                        controller: lineControllerName,
                                        decoration: const InputDecoration(labelText: 'البند', contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10)),
                                        onChanged: (value) => setStateDialog(() => lines[index] = line.copyWith(name: value)),
                                        textInputAction: TextInputAction.next,
                                        onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: FocusTraversalOrder(
                                    order: const NumericFocusOrder(2),
                                    child: SizedBox(
                                      height: 40,
                                      child: DropdownButtonFormField<String>(
                                        value: line.unit.isEmpty ? 'ياردة' : line.unit,
                                        isExpanded: true,
                                        decoration: const InputDecoration(labelText: 'الوحدة', contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10)),
                                        items: const [
                                          DropdownMenuItem(value: 'ياردة', child: Text('ياردة')),
                                          DropdownMenuItem(value: 'متر', child: Text('متر')),
                                        ],
                                        onChanged: (value) => setStateDialog(() => lines[index] = line.copyWith(unit: value ?? 'ياردة')),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: FocusTraversalOrder(
                                    order: const NumericFocusOrder(3),
                                    child: SizedBox(
                                      height: 40,
                                      child: TextField(
                                        controller: lineControllerQty,
                                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                        decoration: const InputDecoration(labelText: 'الكمية', contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10)),
                                        onChanged: (value) => setStateDialog(() => lines[index] = line.copyWith(quantity: double.tryParse(value) ?? 0)),
                                        textInputAction: TextInputAction.next,
                                        onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: FocusTraversalOrder(
                                    order: const NumericFocusOrder(4),
                                    child: SizedBox(
                                      height: 40,
                                      child: TextField(
                                        controller: lineControllerPrice,
                                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                        decoration: const InputDecoration(labelText: 'سعر الوحدة', contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10)),
                                        onChanged: (value) => setStateDialog(() => lines[index] = line.copyWith(unitPrice: double.tryParse(value) ?? 0)),
                                        textInputAction: TextInputAction.next,
                                        onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                SizedBox(
                                  width: 120,
                                  child: FocusTraversalOrder(
                                    order: const NumericFocusOrder(5),
                                    child: Container(
                                      height: 40,
                                      alignment: Alignment.center,
                                      padding: const EdgeInsets.symmetric(horizontal: 8),
                                      decoration: BoxDecoration(
                                        color: _screenTheme.cardColor,
                                        borderRadius: BorderRadius.circular(8),
                                        border: Border.all(color: UiPalette.borderSoft.withOpacity(0.5)),
                                      ),
                                      child: Text(
                                        _money.format(lineTotal),
                                        textAlign: TextAlign.center,
                                        style: const TextStyle(fontWeight: FontWeight.bold),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: FocusTraversalOrder(
                                    order: const NumericFocusOrder(6),
                                    child: SizedBox(
                                      height: 40,
                                      child: TextField(
                                        controller: lineControllerNote,
                                        decoration: const InputDecoration(labelText: 'ملاحظة', contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 10)),
                                        onChanged: (value) => setStateDialog(() => lines[index] = line.copyWith(note: value)),
                                        textInputAction: TextInputAction.next,
                                        onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                      ),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        );
                      }),
                      const SizedBox(height: 12),
                      Wrap(
                        alignment: WrapAlignment.spaceBetween,
                        spacing: 12,
                        runSpacing: 8,
                        children: [
                          const Text('إجمالي الفاتورة', style: TextStyle(fontWeight: FontWeight.bold)),
                          Text(_money.format(totalAmount), style: const TextStyle(fontWeight: FontWeight.bold)),
                          const Text('المدفوع', style: TextStyle(fontWeight: FontWeight.normal)),
                          Text(_money.format(0), style: const TextStyle(fontWeight: FontWeight.normal)),
                          const Text('المتبقي', style: TextStyle(fontWeight: FontWeight.normal)),
                          Text(_money.format(totalAmount), style: const TextStyle(fontWeight: FontWeight.normal)),
                          const Text('الرصيد السابق', style: TextStyle(fontWeight: FontWeight.normal)),
                          Text(_money.format(previousBalance), style: const TextStyle(fontWeight: FontWeight.normal)),
                          const Text('الرصيد النهائي', style: TextStyle(fontWeight: FontWeight.normal)),
                          Text(_money.format(finalBalance), style: const TextStyle(fontWeight: FontWeight.normal)),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
                FilledButton(
                  onPressed: () {
                    final selectedSupplierId = supplierId;
                    if (selectedSupplierId <= 0 || lines.isEmpty) {
                      return;
                    }

                    final invoiceLines = lines
                        .where((line) => line.name.trim().isNotEmpty)
                        .map((line) => _InvoiceLine(
                            name: line.name,
                            unit: line.unit,
                            quantity: line.quantity,
                            unitPrice: line.unitPrice,
                            note: line.note,
                          ))
                        .toList();

                    if (invoiceLines.isEmpty) {
                      return;
                    }

                    final invoiceTotal = invoiceLines.fold<double>(0, (sum, line) => sum + (line.quantity * line.unitPrice));
                    final previousBalanceValue = _finalBalanceForSupplier(selectedSupplierId);
                    final finalBalanceValue = previousBalanceValue + invoiceTotal;

                    setState(() {
                      _invoices.add(_InvoiceRecord(
                        id: _invoices.length + 1,
                        supplierId: selectedSupplierId,
                        invoiceNumber: invoiceNumberController.text.trim().isEmpty
                            ? 'INV-${DateTime.now().millisecondsSinceEpoch}'
                            : invoiceNumberController.text.trim(),
                        invoiceDate: dateController.text.trim().isEmpty
                            ? DateTime.now().toIso8601String().split('T').first
                            : DateTime.parse(_parseArabicDate(dateController.text.trim())).toIso8601String().split('T').first,
                        title: 'فاتورة مورد',
                        totalAmount: invoiceTotal,
                        paidAmount: 0,
                        previousBalance: previousBalanceValue,
                        finalBalance: finalBalanceValue,
                        lineItems: invoiceLines,
                      ));
                    });
                    Navigator.pop(context);
                  },
                  child: const Text('حفظ الفاتورة'),
                ),
              ],
            );
          },
        ),
      ),
    );
  }

  Future<void> _showAddPaymentDialog() async {
    var supplierId = _selectedSupplierId;
    final previousBalance = _finalBalanceForSupplier(supplierId);
    final amountController = TextEditingController();
    final dateController = TextEditingController(text: _dateFormat.format(DateTime.now()));
    final receiptController = TextEditingController();
    final methodController = TextEditingController(text: 'كاش');
    final collectorController = TextEditingController();
    final againstController = TextEditingController(text: 'من الحساب');

    await showDialog<void>(
      context: context,
      builder: (context) => Theme(
        data: _screenTheme,
        child: StatefulBuilder(
          builder: (context, setStateDialog) {
            final enteredAmount = double.tryParse(amountController.text.trim()) ?? 0;
            final currentFinalBalance = previousBalance - enteredAmount;

            return AlertDialog(
              insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
              title: const Text('إضافة دفعة مورد'),
              content: SizedBox(
                width: 900,
                child: SingleChildScrollView(
                  child: Column(
                    children: [
                      FocusTraversalGroup(
                        policy: OrderedTraversalPolicy(),
                        child: Wrap(
                          spacing: 16,
                          runSpacing: 16,
                          children: [
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(1),
                                child: DropdownButtonFormField<int>(
                                  value: supplierId,
                                  isExpanded: true,
                                  decoration: const InputDecoration(labelText: 'المورد'),
                                  items: _suppliers
                                      .map((supplier) => DropdownMenuItem(value: supplier.id, child: Text(supplier.name, overflow: TextOverflow.ellipsis)))
                                      .toList(),
                                  onChanged: (value) {
                                    if (value == null) return;
                                    setStateDialog(() {
                                      supplierId = value;
                                      amountController.clear();
                                    });
                                  },
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(2),
                                child: TextField(
                                  readOnly: true,
                                  decoration: const InputDecoration(labelText: 'الرصيد السابق'),
                                  controller: TextEditingController(text: _money.format(_finalBalanceForSupplier(supplierId))),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(3),
                                child: TextField(
                                  controller: amountController,
                                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                  decoration: const InputDecoration(labelText: 'المبلغ'),
                                  onChanged: (_) => setStateDialog(() {}),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(4),
                                child: TextField(
                                  readOnly: true,
                                  decoration: const InputDecoration(labelText: 'الرصيد النهائي'),
                                  controller: TextEditingController(text: _money.format(_finalBalanceForSupplier(supplierId) - (double.tryParse(amountController.text.trim()) ?? 0))),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(5),
                                child: TextField(
                                  controller: methodController,
                                  decoration: const InputDecoration(labelText: 'طريقة الدفع'),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(6),
                                child: TextField(
                                  controller: receiptController,
                                  decoration: const InputDecoration(labelText: 'رقم السند'),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(7),
                                child: TextField(
                                  controller: collectorController,
                                  decoration: const InputDecoration(labelText: 'المتحصل'),
                                  textInputAction: TextInputAction.next,
                                  onSubmitted: (_) => FocusScope.of(context).nextFocus(),
                                ),
                              ),
                            ),
                            SizedBox(
                              width: 260,
                              child: FocusTraversalOrder(
                                order: const NumericFocusOrder(8),
                                child: TextField(
                                  controller: dateController,
                                  decoration: const InputDecoration(labelText: 'التاريخ'),
                                  textInputAction: TextInputAction.done,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 12,
                        runSpacing: 8,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          const Text('رؤية مباشرة', style: TextStyle(fontWeight: FontWeight.bold)),
                          Text(
                            'الرصيد السابق: ${_money.format(_finalBalanceForSupplier(supplierId))} | الرصيد النهائي: ${_money.format(currentFinalBalance)}',
                            style: const TextStyle(fontWeight: FontWeight.bold),
                            softWrap: true,
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(onPressed: () => Navigator.pop(context), child: const Text('إلغاء')),
                FilledButton(
                  onPressed: () {
                    final parsedAmount = double.tryParse(amountController.text.trim()) ?? 0;
                    final selectedSupplierId = supplierId;
                    if (parsedAmount <= 0 || selectedSupplierId <= 0) {
                      return;
                    }

                    final finalBalance = _finalBalanceForSupplier(selectedSupplierId) - parsedAmount;
                    setState(() {
                      _payments.add(_PaymentRecord(
                        id: _payments.length + 1,
                        supplierId: selectedSupplierId,
                        amount: parsedAmount,
                        against: againstController.text.trim().isEmpty ? 'من الحساب' : againstController.text.trim(),
                        date: dateController.text.trim().isEmpty ? DateTime.now().toIso8601String().split('T').first : DateTime.parse(_parseArabicDate(dateController.text.trim())).toIso8601String().split('T').first,
                        receiptNumber: receiptController.text.trim().isEmpty ? 'AUTO-${_payments.length + 1}' : receiptController.text.trim(),
                        method: methodController.text.trim().isEmpty ? 'كاش' : methodController.text.trim(),
                        collector: collectorController.text.trim().isEmpty ? '-' : collectorController.text.trim(),
                        finalBalance: finalBalance,
                      ));
                    });
                    Navigator.pop(context);
                  },
                  child: const Text('حفظ الدفعة'),
                ),
              ],
            );
          },
        ),
      ),
    );
  }

  Future<void> _showInvoicePreview(_InvoiceRecord invoice) async {
    final supplierName = _suppliers.firstWhere((supplier) => supplier.id == invoice.supplierId).name;
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('عرض الفاتورة'),
        content: SizedBox(
          width: 640,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('المورد: $supplierName'),
                const SizedBox(height: 8),
                Text('رقم الفاتورة: ${invoice.invoiceNumber}'),
                Text('تاريخ الفاتورة: ${_dateFormat.format(DateTime.parse(invoice.invoiceDate))}'),
                const SizedBox(height: 12),
                ...invoice.lineItems.map((line) => ListTile(
                      dense: true,
                      title: Text('${line.name}'),
                      subtitle: Text('${line.unit} • ${line.quantity} • ${_money.format(line.unitPrice)}'),
                      trailing: Text(_money.format(line.quantity * line.unitPrice)),
                    )),
                const Divider(),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text('إجمالي الفاتورة'),
                    Text(_money.format(invoice.totalAmount)),
                  ],
                ),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text('المدفوع'),
                    Text(_money.format(invoice.paidAmount)),
                  ],
                ),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text('المتبقي'),
                    Text(_money.format(invoice.totalAmount - invoice.paidAmount)),
                  ],
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('إغلاق')),
        ],
      ),
    );
  }

  String _parseArabicDate(String raw) {
    final trimmed = raw.trim();
    final parts = trimmed.split('/');
    if (parts.length == 3) {
      return '${parts[2]}-${parts[1].padLeft(2, '0')}-${parts[0].padLeft(2, '0')}';
    }
    return trimmed;
  }
}

class _SuppliersTab extends StatelessWidget {
  const _SuppliersTab({
    required this.suppliers,
    required this.selectedSupplierId,
    required this.onSelect,
    required this.onAddSupplier,
    required this.summary,
    required this.supplierTransactions,
    required this.onAddInvoice,
  });

  final List<_SupplierRecord> suppliers;
  final int selectedSupplierId;
  final ValueChanged<int> onSelect;
  final VoidCallback onAddSupplier;
  final _SupplierSummary summary;
  final List<_SupplierTransaction> supplierTransactions;
  final VoidCallback onAddInvoice;

  @override
  Widget build(BuildContext context) {
    final selected = suppliers.firstWhere((supplier) => supplier.id == selectedSupplierId);
    final format = NumberFormat('#,##0.00');
    final dateFormat = DateFormat('dd/MM/yyyy');
    final screenDark = Theme.of(context).brightness == Brightness.dark;

    return LayoutBuilder(
      builder: (context, constraints) {
        final isWide = constraints.maxWidth > 960;
        final listWidget = Card(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    const Expanded(child: Text('قائمة الموردين', style: TextStyle(fontWeight: FontWeight.bold))),
                    FilledButton.icon(
                      onPressed: onAddSupplier,
                      icon: const Icon(Icons.add),
                      label: const Text('إضافة مورد'),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: ListView(
                  children: suppliers.map((supplier) => ListTile(
                        selected: supplier.id == selectedSupplierId,
                        selectedTileColor: screenDark ? const Color(0xFF1C2F39) : const Color(0xFFEAF5F2),
                        onTap: () => onSelect(supplier.id),
                        leading: CircleAvatar(
                          backgroundColor: UiPalette.primaryBlue.withOpacity(0.15),
                          child: Text(supplier.name.substring(0, 1), style: const TextStyle(color: UiPalette.primaryBlue, fontWeight: FontWeight.bold)),
                        ),
                        title: Text(supplier.name),
                        subtitle: Text('${supplier.phone} • ${supplier.code}'),
                        trailing: Text(supplier.location),
                      )).toList(),
                ),
              ),
            ],
          ),
        );

        final detailWidget = Card(
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Row(
                children: [
                  const Expanded(child: Text('تفاصيل المورد', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18))),
                  FilledButton.icon(
                    onPressed: onAddInvoice,
                    icon: const Icon(Icons.receipt_long_outlined),
                    label: const Text('إضافة فاتورة'),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  _SummaryBox(title: 'الاسم', value: selected.name),
                  _SummaryBox(title: 'الهاتف', value: selected.phone),
                  _SummaryBox(title: 'الكود', value: selected.code),
                  _SummaryBox(title: 'الموقع', value: selected.location),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(child: _SummaryMetric(label: 'إجمالي المشتريات', value: format.format(summary.totalPurchases))),
                  Expanded(child: _SummaryMetric(label: 'الدائن', value: format.format(summary.creditor))),
                  Expanded(child: _SummaryMetric(label: 'المدين', value: format.format(summary.debtor))),
                  Expanded(child: _SummaryMetric(label: 'الرصيد النهائي', value: format.format(summary.finalBalance))),
                ],
              ),
              const SizedBox(height: 16),
              const Text('كشف حركة المورد', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
              const SizedBox(height: 8),
              ...supplierTransactions.map((transaction) => Card(
                    margin: const EdgeInsets.only(bottom: 8),
                    child: ListTile(
                      leading: Icon(
                        transaction.type == 'فاتورة' ? Icons.receipt_long_outlined : Icons.payments_outlined,
                        color: transaction.type == 'فاتورة' ? UiPalette.primaryBlue : Colors.orange,
                      ),
                      title: Text('${transaction.type} • ${transaction.title}'),
                      subtitle: Text('${dateFormat.format(transaction.date)} • ${transaction.details}'),
                      trailing: Text('${format.format(transaction.amount)}', style: const TextStyle(fontWeight: FontWeight.bold)),
                    ),
                  )),
            ],
          ),
        );

        if (isWide) {
          return Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(flex: 3, child: listWidget),
                const SizedBox(width: 12),
                Expanded(flex: 6, child: detailWidget),
              ],
            ),
          );
        }

        return Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(flex: 2, child: listWidget),
              const SizedBox(height: 12),
              Expanded(flex: 5, child: detailWidget),
            ],
          ),
        );
      },
    );
  }
}

class _PurchasesTab extends StatelessWidget {
  const _PurchasesTab({
    required this.suppliers,
    required this.invoices,
    required this.onAddInvoice,
    required this.onViewInvoice,
  });

  final List<_SupplierRecord> suppliers;
  final List<_InvoiceRecord> invoices;
  final VoidCallback onAddInvoice;
  final ValueChanged<_InvoiceRecord> onViewInvoice;

  @override
  Widget build(BuildContext context) {
    final format = NumberFormat('#,##0.00');
    return Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              const Expanded(child: Text('فواتير المشتريات', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18))),
              FilledButton.icon(
                onPressed: onAddInvoice,
                icon: const Icon(Icons.add),
                label: const Text('إضافة فاتورة'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Expanded(
            child: ListView(
              children: invoices.map((invoice) {
                final supplier = suppliers.firstWhere((item) => item.id == invoice.supplierId);
                return Card(
                  margin: const EdgeInsets.only(bottom: 12),
                  child: ListTile(
                    title: Text('${supplier.name} • ${invoice.invoiceNumber}'),
                    subtitle: Text('تاريخ: ${_parseDate(invoice.invoiceDate)} • القيمة: ${format.format(invoice.totalAmount)} • المدفوع: ${format.format(invoice.paidAmount)}'),
                    trailing: FilledButton(
                      onPressed: () => onViewInvoice(invoice),
                      child: const Text('عرض الفاتورة'),
                    ),
                  ),
                );
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }

  String _parseDate(String value) => DateFormat('dd/MM/yyyy').format(DateTime.parse(value));
}

class _PaymentsTab extends StatelessWidget {
  const _PaymentsTab({required this.suppliers, required this.payments, required this.onAddPayment});

  final List<_SupplierRecord> suppliers;
  final List<_PaymentRecord> payments;
  final VoidCallback onAddPayment;

  @override
  Widget build(BuildContext context) {
    final format = NumberFormat('#,##0.00');
    return Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              const Expanded(child: Text('دفعات الموردين', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18))),
              FilledButton.icon(
                onPressed: onAddPayment,
                icon: const Icon(Icons.payments_outlined),
                label: const Text('إنشاء دفعة جديدة'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Expanded(
            child: ListView(
              children: payments.map((payment) {
                final supplier = suppliers.firstWhere((item) => item.id == payment.supplierId);
                return Card(
                  margin: const EdgeInsets.only(bottom: 12),
                  child: ListTile(
                    title: Text('${supplier.name} • ${payment.amount}'),
                    subtitle: Text('التاريخ: ${_parseDate(payment.date)} • رقم السند: ${payment.receiptNumber} • الطريقة: ${payment.method} • المتحصل: ${payment.collector}'),
                    trailing: Text('${format.format(payment.finalBalance)}', style: const TextStyle(fontWeight: FontWeight.bold)),
                  ),
                );
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }

  String _parseDate(String value) => DateFormat('dd/MM/yyyy').format(DateTime.parse(value));
}

class _SettlementTab extends StatelessWidget {
  const _SettlementTab({
    required this.suppliers,
    required this.selectedSupplierId,
    required this.onSupplierChanged,
    required this.entries,
    required this.totalPurchases,
    required this.totalPayments,
    required this.finalBalance,
    required this.lastPaymentDate,
  });

  final List<_SupplierRecord> suppliers;
  final int selectedSupplierId;
  final ValueChanged<int> onSupplierChanged;
  final List<_SettlementEntry> entries;
  final double totalPurchases;
  final double totalPayments;
  final double finalBalance;
  final String? lastPaymentDate;

  @override
  Widget build(BuildContext context) {
    final format = NumberFormat('#,##0.00');
    return Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          DropdownButtonFormField<int>(
            value: selectedSupplierId,
            decoration: const InputDecoration(labelText: 'اختر المورد'),
            items: suppliers.map((supplier) => DropdownMenuItem(value: supplier.id, child: Text(supplier.name))).toList(),
            onChanged: (value) {
              if (value != null) onSupplierChanged(value);
            },
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              _SummaryMetric(label: 'إجمالي قيمة الاستيراد', value: format.format(totalPurchases)),
              _SummaryMetric(label: 'الدائن', value: format.format(totalPurchases)),
              _SummaryMetric(label: 'المدين', value: format.format(totalPayments)),
              _SummaryMetric(label: 'آخر دفعة', value: lastPaymentDate ?? '-'),
              _SummaryMetric(label: 'الرصيد النهائي', value: format.format(finalBalance)),
            ],
          ),
          const SizedBox(height: 16),
          const Text('كشف التسوية', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
          const SizedBox(height: 8),
          Expanded(
            child: ListView(
              children: entries.map((entry) {
                return Card(
                  margin: const EdgeInsets.only(bottom: 10),
                  child: ListTile(
                    leading: Icon(
                      entry.type == 'فاتورة' ? Icons.receipt_long_outlined : Icons.payments_outlined,
                      color: entry.type == 'فاتورة' ? UiPalette.primaryBlue : Colors.orange,
                    ),
                    title: Text('${entry.type} • ${entry.reference}'),
                    subtitle: Text('تاريخ: ${DateFormat('dd/MM/yyyy').format(entry.date)} • القيمة: ${format.format(entry.amount)} • الرصيد السابق: ${format.format(entry.previousBalance)} • الرصيد النهائي: ${format.format(entry.finalBalance)}'),
                  ),
                );
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }
}

class _SummaryBox extends StatelessWidget {
  const _SummaryBox({required this.title, required this.value});

  final String title;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 170,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft.withOpacity(0.4)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.bold)),
          const SizedBox(height: 6),
          Text(value),
        ],
      ),
    );
  }
}

class _SummaryMetric extends StatelessWidget {
  const _SummaryMetric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      constraints: const BoxConstraints(minWidth: 140, minHeight: 90),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft.withOpacity(0.4)),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 12)),
          const SizedBox(height: 8),
          Text(value, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18)),
        ],
      ),
    );
  }
}

class _SupplierRecord {
  const _SupplierRecord({
    required this.id,
    required this.name,
    required this.phone,
    required this.code,
    required this.location,
    required this.notes,
  });

  final int id;
  final String name;
  final String phone;
  final String code;
  final String location;
  final String notes;
}

class _SupplierSummary {
  const _SupplierSummary({
    required this.totalPurchases,
    required this.creditor,
    required this.debtor,
    required this.finalBalance,
  });

  final double totalPurchases;
  final double creditor;
  final double debtor;
  final double finalBalance;
}

class _SupplierTransaction {
  const _SupplierTransaction({
    required this.type,
    required this.title,
    required this.amount,
    required this.date,
    required this.details,
  });

  final String type;
  final String title;
  final double amount;
  final DateTime date;
  final String details;
}

class _InvoiceRecord {
  const _InvoiceRecord({
    required this.id,
    required this.supplierId,
    required this.invoiceNumber,
    required this.invoiceDate,
    required this.title,
    required this.totalAmount,
    required this.paidAmount,
    required this.previousBalance,
    required this.finalBalance,
    required this.lineItems,
  });

  final int id;
  final int supplierId;
  final String invoiceNumber;
  final String invoiceDate;
  final String title;
  final double totalAmount;
  final double paidAmount;
  final double previousBalance;
  final double finalBalance;
  final List<_InvoiceLine> lineItems;
}

class _InvoiceLine {
  const _InvoiceLine({
    required this.name,
    required this.unit,
    required this.quantity,
    required this.unitPrice,
    this.note = '',
  });

  final String name;
  final String unit;
  final double quantity;
  final double unitPrice;
  final String note;
}

class _InvoiceLineDraft {
  const _InvoiceLineDraft({
    required this.name,
    required this.unit,
    required this.quantity,
    required this.unitPrice,
    this.note = '',
  });

  final String name;
  final String unit;
  final double quantity;
  final double unitPrice;
  final String note;

  _InvoiceLineDraft copyWith({String? name, String? unit, double? quantity, double? unitPrice, String? note}) {
    return _InvoiceLineDraft(
      name: name ?? this.name,
      unit: unit ?? this.unit,
      quantity: quantity ?? this.quantity,
      unitPrice: unitPrice ?? this.unitPrice,
      note: note ?? this.note,
    );
  }
}

class _PaymentRecord {
  const _PaymentRecord({
    required this.id,
    required this.supplierId,
    required this.amount,
    required this.against,
    required this.date,
    required this.receiptNumber,
    required this.method,
    required this.collector,
    required this.finalBalance,
  });

  final int id;
  final int supplierId;
  final double amount;
  final String against;
  final String date;
  final String receiptNumber;
  final String method;
  final String collector;
  final double finalBalance;
}

class _SettlementEntry {
  const _SettlementEntry({
    required this.type,
    required this.date,
    required this.title,
    required this.amount,
    required this.reference,
    required this.previousBalance,
    required this.finalBalance,
  });

  final String type;
  final DateTime date;
  final String title;
  final double amount;
  final String reference;
  final double previousBalance;
  final double finalBalance;
}
