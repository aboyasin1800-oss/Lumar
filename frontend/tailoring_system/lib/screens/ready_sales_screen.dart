import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import '../core/document_printing.dart';

class ReadySalesScreen extends StatefulWidget {
	const ReadySalesScreen({super.key});

	@override
	State<ReadySalesScreen> createState() => _ReadySalesScreenState();
}

class _ReadySalesScreenState extends State<ReadySalesScreen> {
	static const _baseUrl = String.fromEnvironment(
		'LUMAR_API_URL',
		defaultValue: 'http://127.0.0.1:5093',
	);
	static const _background = Color(0xFF101414);
	static const _panel = Color(0xFF171B1B);
	static const _field = Color(0xFF202525);
	static const _border = Color.fromARGB(255, 40, 105, 92);
	static const _accent = Color.fromARGB(255, 78, 201, 176);

	final productSearch = TextEditingController();
	final customerSearch = TextEditingController();
	final quantityController = TextEditingController(text: '1');
	final priceController = TextEditingController();
	final discountController = TextEditingController(text: '0');
	final paidController = TextEditingController(text: '0');
	late Future<List<_ReadyProduct>> productsFuture;
	List<_Customer> customerResults = const [];
	List<_Employee> employees = const [];
	final List<_CartLine> cart = [];
	_ReadyProduct? selectedProduct;
	_Customer? selectedCustomer;
	String? selectedEmployeeCode;
	bool searchingCustomers = false;
	bool loadingEmployees = true;
	bool _selling = false;
	String? _saleReference;
	String paymentMethod = 'نقداً';

	@override
	void initState() {
		super.initState();
		productsFuture = _loadProducts();
		_loadEmployees();
	}

	@override
	void dispose() {
		productSearch.dispose();
		customerSearch.dispose();
		quantityController.dispose();
		priceController.dispose();
		discountController.dispose();
		paidController.dispose();
		super.dispose();
	}

	Future<List<_ReadyProduct>> _loadProducts() async {
		final responses = await Future.wait([
			http.get(Uri.parse('$_baseUrl/inventory/readymade')),
			http.get(Uri.parse('$_baseUrl/inventory/imported')),
		]);
		if (responses.any((response) => response.statusCode < 200 || response.statusCode >= 300)) {
			throw Exception();
		}
		final ourProducts = (jsonDecode(responses[0].body) as List)
				.cast<Map<String, dynamic>>()
				.map(_ReadyProduct.fromOurProduction)
				.where((product) => product.availableQuantity > 0);
		final importedProducts = (jsonDecode(responses[1].body) as List)
				.cast<Map<String, dynamic>>()
				.map(_ReadyProduct.fromImported)
				.where((product) => product.availableQuantity > 0);
		return [...ourProducts, ...importedProducts];
	}

	Future<void> _loadEmployees() async {
		try {
			final response = await http.get(Uri.parse('$_baseUrl/employees'));
			if (response.statusCode < 200 || response.statusCode >= 300) throw Exception();
			final values = (jsonDecode(response.body) as List)
					.cast<Map<String, dynamic>>()
					.map(_Employee.fromJson)
					.where((employee) => employee.isActive)
					.toList();
			if (mounted) setState(() => employees = values);
		} catch (error) {
			debugPrint('Ready-sale employee loading failed: $error');
			if (mounted) {
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تعذر تحميل الموظفين الفعالين.')));
			}
		} finally {
			if (mounted) setState(() => loadingEmployees = false);
		}
	}

	Future<void> _searchCustomers() async {
		final term = customerSearch.text.trim();
		if (term.isEmpty) {
			setState(() => customerResults = const []);
			return;
		}
		setState(() => searchingCustomers = true);
		try {
			final uri = Uri.parse('$_baseUrl/customers/search').replace(queryParameters: {'term': term});
			final response = await http.get(uri);
			if (response.statusCode < 200 || response.statusCode >= 300) throw Exception();
			final values = (jsonDecode(response.body) as List)
					.cast<Map<String, dynamic>>()
					.map(_Customer.fromJson)
					.toList();
			if (mounted) setState(() => customerResults = values);
		} catch (error) {
			debugPrint('Ready-sale customer search failed: $error');
			if (mounted) {
				setState(() => customerResults = const []);
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تعذر البحث عن العميل.')));
			}
		} finally {
			if (mounted) setState(() => searchingCustomers = false);
		}
	}

	void _selectProduct(_ReadyProduct product) {
		setState(() {
			selectedProduct = product;
			productSearch.text = '${product.code} - ${product.name}';
			priceController.text = _plainNumber(product.price);
			quantityController.text = '1';
		});
	}

	void _addProduct() {
		final product = selectedProduct;
		final quantity = double.tryParse(quantityController.text.trim()) ?? 0;
		final price = double.tryParse(priceController.text.trim()) ?? 0;
		if (product == null || quantity <= 0 || price < 0) return;
		if (quantity > product.availableQuantity) {
			ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('الكمية المطلوبة أكبر من المتاح.')));
			return;
		}
		setState(() {
			final index = cart.indexWhere((line) => line.product.id == product.id);
			if (index < 0) {
				cart.add(_CartLine(product: product, quantity: quantity, price: price));
			} else {
				cart[index] = _CartLine(product: product, quantity: quantity, price: price);
			}
			selectedProduct = null;
			productSearch.clear();
			priceController.clear();
			quantityController.text = '1';
		});
	}

	double get subtotal => cart.fold(0, (sum, line) => sum + line.quantity * line.price);
	double get discount => double.tryParse(discountController.text.trim()) ?? 0;
	double get total => (subtotal - discount).clamp(0, double.infinity);
	double get paid => double.tryParse(paidController.text.trim()) ?? 0;
	double get remaining => (total - paid).clamp(0, double.infinity);

	Future<void> _sell() async {
		if (_selling) return;
		final customer = selectedCustomer;
		if (customer == null) {
			ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('اختر العميل أولاً.')));
			return;
		}
		if (cart.isEmpty) {
			ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('أضف منتجاً واحداً على الأقل.')));
			return;
		}
		final paymentType = switch (paymentMethod) {
			'آجل' => 'Credit',
			'تبرعاً' => 'Donation',
			_ => 'Cash',
		};
		if (paid < 0 || discount < 0 || discount > subtotal) {
			ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('بيانات المبلغ أو الخصم غير صالحة.')));
			return;
		}
		if (paymentType == 'Cash' && (paid - total).abs() > 0.009) {
			ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('يجب سداد صافي الفاتورة كاملاً في البيع النقدي.')));
			return;
		}
		if (paymentType == 'Donation' && paid.abs() > 0.009) {
			ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('لا يقبل التبرع دفعة نقدية.')));
			return;
		}
		for (final line in cart) {
			if (line.quantity != line.quantity.truncateToDouble() || line.quantity <= 0) {
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('الكمية يجب أن تكون رقماً صحيحاً موجباً.')));
				return;
			}
			if (line.product.localId == null && line.product.importedId == null) {
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('هوية المنتج الرسمية مفقودة.')));
				return;
			}
			if (line.product.localId != null && line.product.productTypeId == null) {
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('نوع المنتج الرسمي مفقود للمنتج المصنع.')));
				return;
			}
		}

		setState(() => _selling = true);
		try {
			final payload = <String, dynamic>{
				'customerId': customer.id,
				if (selectedEmployeeCode != null) 'employeeCode': selectedEmployeeCode,
				'discountAmount': discount,
				'paymentType': paymentType,
				'paidAmount': paid,
				'saleReference': _saleReference ??= 'RMS-${DateTime.now().microsecondsSinceEpoch}',
				'items': cart.map((line) => <String, dynamic>{
					if (line.product.localId != null) 'readyMadeInventoryProductId': line.product.localId,
					if (line.product.importedId != null) 'importedReadyMadeProductId': line.product.importedId,
					if (line.product.productTypeId != null) 'productTypeId': line.product.productTypeId,
					'quantity': line.quantity.toInt(),
					'unitPrice': line.price,
				}).toList(),
			};
			final response = await http.post(
				Uri.parse('$_baseUrl/ready-sales'),
				headers: const {'Content-Type': 'application/json'},
				body: jsonEncode(payload),
			);
			if (response.statusCode < 200 || response.statusCode >= 300) {
				debugPrint('Ready-sale request failed: HTTP ${response.statusCode}; body=${response.body}');
				throw _ReadySaleFailure(response.statusCode);
			}
			final result = (jsonDecode(response.body) as Map).cast<String, dynamic>();
			if (!mounted) return;
			setState(() {
				cart.clear();
				selectedProduct = null;
				selectedCustomer = null;
				selectedEmployeeCode = null;
				customerSearch.clear();
				discountController.text = '0';
				paidController.text = '0';
				_saleReference = null;
				productsFuture = _loadProducts();
			});
			await _showSaleResult(result);
		} catch (error, stackTrace) {
			debugPrint('Ready-sale save failed: $error\n$stackTrace');
			if (mounted) {
				final message = error is http.ClientException
						? 'تعذر الاتصال بالخادم.'
						: error is _ReadySaleFailure && error.statusCode >= 500
							? 'تعذر إنشاء الفاتورة.'
							: 'تعذر حفظ عملية البيع.';
				ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
			}
		} finally {
			if (mounted) setState(() => _selling = false);
		}
	}

	Future<void> _showSaleResult(Map<String, dynamic> result) async {
		await showDialog<void>(
			context: context,
			builder: (dialogContext) => AlertDialog(
				title: const Text('تم حفظ البيع بنجاح'),
				content: Text('رقم الفاتورة: ${result['invoiceNumber'] ?? '-'}\nرقم الطلب: ${result['orderNumber'] ?? '-'}\nالصافي: ${_money((result['netAmount'] as num?)?.toDouble() ?? total)}'),
				actions: [
					TextButton.icon(onPressed: () async {
						Navigator.of(dialogContext).pop();
						await _printInvoice(result);
					}, icon: const Icon(Icons.print_outlined), label: const Text('طباعة الفاتورة')),
					TextButton(onPressed: () => Navigator.of(dialogContext).pop(), child: const Text('إغلاق')),
				],
			),
		);
	}

	Future<void> _printInvoice(Map<String, dynamic> result) async {
		try {
			final font = await DocumentPrintSupport.loadArabicFont();
			final header = await DocumentPrintSupport.loadHeaderSettings(_baseUrl);
			final headerWidget = await DocumentPrintSupport.buildHeader(header, font);
			final lines = (result['items'] as List<dynamic>? ?? const []).cast<Map<String, dynamic>>();
			final totalAmount = (result['totalAmount'] as num?)?.toDouble() ?? 0;
			final discountAmount = (result['discountAmount'] as num?)?.toDouble() ?? 0;
			final netAmount = (result['netAmount'] as num?)?.toDouble() ?? 0;
			final paidAmount = (result['paidAmount'] as num?)?.toDouble() ?? 0;
			final remainingAmount = (result['remainingAmount'] as num?)?.toDouble() ?? 0;
			final pdf = pw.Document();
			pdf.addPage(pw.MultiPage(
				pageFormat: DocumentPrintSupport.a5Portrait,
				build: (_) => [
					headerWidget,
					pw.Divider(),
					pw.Directionality(
						textDirection: pw.TextDirection.rtl,
						child: pw.Column(
							crossAxisAlignment: pw.CrossAxisAlignment.stretch,
							children: [
								pw.Text('فاتورة بيع منتجات جاهزة', style: pw.TextStyle(font: font, fontSize: 20, fontWeight: pw.FontWeight.bold)),
								pw.SizedBox(height: 8),
								pw.Text('رقم الفاتورة: ${result['invoiceNumber'] ?? '-'}', style: pw.TextStyle(font: font)),
								pw.Text('العميل: ${result['customerName'] ?? '-'}', style: pw.TextStyle(font: font)),
								pw.SizedBox(height: 12),
								pw.TableHelper.fromTextArray(
									headerStyle: pw.TextStyle(font: font, fontWeight: pw.FontWeight.bold),
									cellStyle: pw.TextStyle(font: font, fontSize: 10),
									headers: const ['المنتج', 'الكمية', 'السعر', 'الإجمالي'],
									data: lines.map((line) => [line['itemName'] ?? 'منتج جاهز', line['quantity'] ?? 0, line['unitPrice'] ?? 0, line['totalPrice'] ?? 0]).toList(),
								),
								pw.SizedBox(height: 12),
								pw.Text('إجمالي الفاتورة: ${_money(totalAmount)}', style: pw.TextStyle(font: font, fontWeight: pw.FontWeight.bold)),
								if (discountAmount > 0)
									pw.Text('الخصم: ${_money(discountAmount)}', style: pw.TextStyle(font: font)),
								pw.Text('الصافي: ${_money(netAmount)}', style: pw.TextStyle(font: font)),
								pw.Text('المدفوع: ${_money(paidAmount)}', style: pw.TextStyle(font: font)),
								pw.Text('المتبقي في ذمة العميل: ${_money(remainingAmount)}', style: pw.TextStyle(font: font, fontWeight: pw.FontWeight.bold)),
								pw.Text('نوع الدفع: ${DocumentPrintSupport.paymentTypeLabel(result['paymentType'])}', style: pw.TextStyle(font: font)),
							],
						),
					),
				],
			));
			final bytes = await pdf.save();
			final printed = await Printing.layoutPdf(onLayout: (_) async => bytes);
			if (mounted && !printed) {
				ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('لم تكتمل الطباعة.')));
			}
		} catch (error) {
			if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تعذرت طباعة الفاتورة: $error')));
		}
	}

	@override
	Widget build(BuildContext context) => ColoredBox(
			color: _background,
			child: FutureBuilder<List<_ReadyProduct>>(
				future: productsFuture,
				builder: (context, snapshot) {
					if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
					if (snapshot.hasError) return _LoadError(onRetry: () => setState(() => productsFuture = _loadProducts()));
					final products = snapshot.data!;
					return LayoutBuilder(builder: (context, constraints) {
						if (constraints.maxWidth < 900) {
							return ListView(
								padding: const EdgeInsets.all(6),
								children: [
									SizedBox(height: 520, child: _customerAndPaymentPanel()),
									const SizedBox(height: 6),
									SizedBox(height: 600, child: _productsPanel(products)),
								],
							);
						}
						return Padding(
							padding: const EdgeInsets.all(6),
							child: Row(
								crossAxisAlignment: CrossAxisAlignment.stretch,
								children: [
									Flexible(flex: 2, child: _customerAndPaymentPanel()),
									const SizedBox(width: 6),
									Flexible(flex: 3, child: _productsPanel(products)),
								],
							),
						);
					});
				},
			),
		);

	Widget _productsPanel(List<_ReadyProduct> products) {
		final query = productSearch.text.trim().toLowerCase();
		final filtered = query.isEmpty ? products.take(8).toList() : products.where((product) => product.searchText.contains(query)).take(12).toList();
		return Column(children: [
			_section(
				title: 'البحث عن المنتج الجاهز',
				child: Column(children: [
					TextField(controller: productSearch, onChanged: (_) => setState(() {}), decoration: _decoration('ابحث بالكود أو الاسم أو رقم التتبع', prefixIcon: Icons.search)),
					if (filtered.isNotEmpty) ...[
						const SizedBox(height: 4),
						SizedBox(
							height: 96,
							child: ListView.separated(
								itemCount: filtered.length,
								separatorBuilder: (_, __) => const Divider(height: 1),
								itemBuilder: (context, index) {
									final product = filtered[index];
									return ListTile(
										dense: true,
										visualDensity: VisualDensity.compact,
										title: Text(product.trackingCode, maxLines: 1, overflow: TextOverflow.ellipsis),
										subtitle: Text(
											'${product.name}\nكود القماش: ${product.fabricCode}\nنوع القماش: ${product.fabricType}\nلون القماش: ${product.fabricColor}\nسعر البيع المقترح: ${_money(product.price)}',
											maxLines: 5,
											overflow: TextOverflow.ellipsis,
										),
										onTap: () => _selectProduct(product),
									);
								},
							),
						),
					],
					const SizedBox(height: 4),
					Row(children: [
						Expanded(child: TextField(controller: quantityController, keyboardType: TextInputType.number, decoration: _decoration('الكمية'))),
						const SizedBox(width: 4),
						Expanded(child: TextField(controller: priceController, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: _decoration('السعر'))),
					]),
					const SizedBox(height: 4),
					SizedBox(width: double.infinity, height: 34, child: FilledButton.icon(onPressed: selectedProduct == null ? null : _addProduct, icon: const Icon(Icons.add_shopping_cart, size: 16), label: const Text('إضافة منتج'))),
				]),
			),
			const SizedBox(height: 6),
			Expanded(
				child: _section(
					title: 'المنتجات المختارة',
					expand: true,
					child: cart.isEmpty
							? const Center(child: Text('لم تتم إضافة منتجات بعد.'))
							: ListView.separated(
									itemCount: cart.length,
									separatorBuilder: (_, __) => const Divider(height: 1),
									itemBuilder: (context, index) {
										final line = cart[index];
										return ListTile(
											dense: true,
											leading: IconButton(tooltip: 'حذف المنتج', icon: const Icon(Icons.delete_outline, color: Colors.redAccent), onPressed: () => setState(() => cart.removeAt(index))),
											title: Text(line.product.trackingCode),
											subtitle: Text('${line.product.name}\nكود القماش: ${line.product.fabricCode}\nلون القماش: ${line.product.fabricColor}\nسعر القطعة: ${_money(line.price)}'),
											trailing: Text(_money(line.quantity * line.price), style: const TextStyle(fontWeight: FontWeight.bold)),
										);
									},
								),
				),
			),
		]);
	}

	Widget _customerAndPaymentPanel() => Column(children: [
		_section(
			title: 'بيانات العميل',
			child: Column(children: [
				Row(children: [
					Expanded(child: TextField(controller: customerSearch, textInputAction: TextInputAction.search, onSubmitted: (_) => _searchCustomers(), decoration: _decoration('الاسم أو الهاتف أو الكود', prefixIcon: Icons.search))),
					const SizedBox(width: 4),
					IconButton.filled(tooltip: 'بحث', onPressed: searchingCustomers ? null : _searchCustomers, icon: searchingCustomers ? const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.search)),
				]),
				if (customerResults.isNotEmpty)
					SizedBox(
						height: 90,
						child: ListView.builder(
							itemCount: customerResults.length,
							itemBuilder: (context, index) {
								final customer = customerResults[index];
								return ListTile(
									dense: true,
									title: Text(customer.name),
									subtitle: Text('${customer.code} | ${customer.phone}'),
									onTap: () => setState(() {
									selectedCustomer = customer;
									customerResults = const [];
									customerSearch.text = '${customer.code} - ${customer.name}';
								}),
								);
							},
						),
					),
				const SizedBox(height: 4),
				_customerSummary(),
			]),
		),
		const SizedBox(height: 6),
		Expanded(
			child: _section(
				title: 'تفاصيل البيع',
				expand: true,
				child: Column(children: [
					DropdownButtonFormField<String>(
						initialValue: selectedEmployeeCode,
						decoration: _decoration('الموظف المسؤول'),
						hint: const Text('غير محدد'),
						isExpanded: true,
						items: employees.map((employee) => DropdownMenuItem<String>(value: employee.code, child: Text('${employee.code} - ${employee.name}'))).toList(),
						onChanged: loadingEmployees ? null : (value) => setState(() => selectedEmployeeCode = value),
					),
					const SizedBox(height: 5),
					_amountRow('الإجمالي', _money(subtotal)),
					const SizedBox(height: 4),
					TextField(controller: discountController, keyboardType: const TextInputType.numberWithOptions(decimal: true), onChanged: (_) => setState(() {}), decoration: _decoration('الخصم')),
					const SizedBox(height: 4),
					_amountRow('المبلغ المطلوب', _money(total)),
					const SizedBox(height: 4),
					TextField(controller: paidController, keyboardType: const TextInputType.numberWithOptions(decimal: true), onChanged: (_) => setState(() {}), decoration: _decoration('المدفوع')),
					const SizedBox(height: 4),
					_amountRow('المتبقي', _money(remaining)),
					const SizedBox(height: 6),
					Align(alignment: Alignment.centerRight, child: Text('طريقة الدفع', style: Theme.of(context).textTheme.labelMedium)),
					const SizedBox(height: 4),
					Wrap(
						spacing: 4,
						runSpacing: 4,
						children: ['نقداً', 'آجل', 'تبرعاً'].map((method) => ChoiceChip(
							selected: paymentMethod == method,
							label: Text(method),
							onSelected: (_) => setState(() {
								paymentMethod = method;
								if (method != 'نقداً') paidController.text = '0';
							}),
						)).toList(),
					),
					const Spacer(),
					SizedBox(width: double.infinity, height: 36, child: FilledButton.icon(onPressed: _selling ? null : _sell, icon: _selling ? const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.check, size: 17), label: Text(_selling ? 'جارٍ الحفظ' : 'بيع'))),
				]),
			),
		),
	]);

	Widget _customerSummary() {
		final customer = selectedCustomer;
		if (customer == null) {
			return Container(width: double.infinity, padding: const EdgeInsets.all(8), decoration: _boxDecoration(), child: const Text('لم يتم اختيار عميل.'));
		}
		return Container(
			width: double.infinity,
			padding: const EdgeInsets.all(8),
			decoration: _boxDecoration(),
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.start,
				children: [
					Text(customer.name, style: const TextStyle(fontWeight: FontWeight.bold)),
					Text('الكود: ${customer.code}'),
					Text('الهاتف: ${customer.phone}'),
					Text('النقاط: ${customer.points == null ? '-' : _plainNumber(customer.points!)}'),
					Text('المديونية: ${customer.debts == null ? '-' : _money(customer.debts!)}'),
				],
			),
		);
	}

	Widget _amountRow(String label, String value) => Container(
			width: double.infinity,
			padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
			decoration: _boxDecoration(),
			child: Row(children: [
				Expanded(child: Text(label, style: const TextStyle(fontSize: 11))),
				Text(value, style: const TextStyle(fontWeight: FontWeight.bold)),
			]),
		);

	Widget _section({required String title, required Widget child, bool expand = false}) {
		final content = Container(
			width: double.infinity,
			padding: const EdgeInsets.all(7),
			decoration: BoxDecoration(color: _panel, borderRadius: BorderRadius.circular(5), border: Border.all(color: _border)),
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.stretch,
				children: [
					Text(title, style: const TextStyle(color: _accent, fontSize: 12, fontWeight: FontWeight.bold)),
					const SizedBox(height: 5),
					if (expand) Expanded(child: child) else child,
				],
			),
		);
		return expand ? content : IntrinsicHeight(child: content);
	}

	InputDecoration _decoration(String label, {IconData? prefixIcon}) => InputDecoration(
		labelText: label,
		labelStyle: const TextStyle(fontSize: 10),
		prefixIcon: prefixIcon == null ? null : Icon(prefixIcon, size: 17),
		filled: true,
		fillColor: _field,
		isDense: true,
		contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 9),
		border: OutlineInputBorder(borderRadius: BorderRadius.circular(5)),
		enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: _border)),
		focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(5), borderSide: const BorderSide(color: _accent)),
	);

	BoxDecoration _boxDecoration() => BoxDecoration(color: _field, borderRadius: BorderRadius.circular(5), border: Border.all(color: _border));
}

class _ReadyProduct {
	const _ReadyProduct({required this.id, required this.localId, required this.importedId, required this.productTypeId, required this.trackingCode, required this.name, required this.productTypeName, required this.fabricCode, required this.fabricType, required this.fabricColor, required this.status, required this.source, required this.availableQuantity, required this.price});

	factory _ReadyProduct.fromOurProduction(Map<String, dynamic> json) => _ReadyProduct(
		id: 'our-${json['readyMadeInventoryProductId']}',
		localId: (json['readyMadeInventoryProductId'] as num?)?.toInt(),
		importedId: null,
		productTypeId: (json['productTypeId'] as num?)?.toInt(),
		trackingCode: json['trackingCode']?.toString() ?? '-',
		name: json['pieceType']?.toString() ?? json['productionName']?.toString() ?? '-',
		productTypeName: json['productTypeName']?.toString() ?? json['pieceType']?.toString() ?? '-',
		fabricCode: json['fabricCode']?.toString() ?? '-',
		fabricType: json['fabricType']?.toString() ?? '-',
		fabricColor: json['fabricColor']?.toString() ?? '-',
		status: json['status']?.toString() ?? '-',
		source: 'إنتاجنا',
		availableQuantity: json['status'] == 'AvailableForSale' && json['isActive'] == true ? 1 : 0,
		price: (json['suggestedSellingPrice'] as num?)?.toDouble() ?? 0,
	);

	factory _ReadyProduct.fromImported(Map<String, dynamic> json) => _ReadyProduct(
		id: 'imported-${json['importedReadyMadeProductId']}',
		localId: null,
		importedId: (json['importedReadyMadeProductId'] as num?)?.toInt(),
		productTypeId: null,
		trackingCode: json['productCode']?.toString() ?? '-',
		name: json['productName']?.toString() ?? '-',
		productTypeName: json['productType']?.toString() ?? '-',
		fabricCode: '-',
		fabricType: json['productType']?.toString() ?? '-',
		fabricColor: '-',
		status: json['isActive'] == true ? 'AvailableForSale' : 'Sold',
		source: 'مستورد',
		availableQuantity: json['isActive'] == true ? (json['quantity'] as num?)?.toDouble() ?? 0 : 0,
		price: (json['sellingPrice'] as num?)?.toDouble() ?? 0,
	);

	final String id;
	final int? localId;
	final int? importedId;
	final int? productTypeId;
	final String trackingCode;
	final String name;
	final String productTypeName;
	final String fabricCode;
	final String fabricType;
	final String fabricColor;
	final String status;
	final String source;
	final double availableQuantity;
	final double price;

	String get code => trackingCode;
	String get searchText => '$trackingCode $name $productTypeName $fabricCode $fabricType $fabricColor $source'.toLowerCase();
}

class _Customer {
	const _Customer({required this.id, required this.code, required this.name, required this.phone, required this.points, required this.debts});

	factory _Customer.fromJson(Map<String, dynamic> json) => _Customer(
		id: (json['customerId'] as num).toInt(),
		code: json['customerCode']?.toString() ?? '-',
		name: json['customerName']?.toString() ?? '-',
		phone: json['phoneNumber']?.toString() ?? '-',
		points: (json['totalPoints'] as num?)?.toDouble(),
		debts: (json['totalDebts'] as num?)?.toDouble(),
	);

	final int id;
	final String code;
	final String name;
	final String phone;
	final double? points;
	final double? debts;
}

class _ReadySaleFailure implements Exception {
	const _ReadySaleFailure(this.statusCode);

	final int statusCode;
}

class _Employee {
	const _Employee({required this.code, required this.name, required this.isActive});

	factory _Employee.fromJson(Map<String, dynamic> json) => _Employee(
		code: json['employeeCode']?.toString() ?? '-',
		name: json['employeeName']?.toString() ?? '-',
		isActive: json['isActive'] == true || json['status']?.toString().toLowerCase() == 'active',
	);

	final String code;
	final String name;
	final bool isActive;
}

class _CartLine {
	const _CartLine({required this.product, required this.quantity, required this.price});

	final _ReadyProduct product;
	final double quantity;
	final double price;
}

class _LoadError extends StatelessWidget {
	const _LoadError({required this.onRetry});

	final VoidCallback onRetry;

	@override
	Widget build(BuildContext context) => Center(
			child: Column(
				mainAxisSize: MainAxisSize.min,
				children: [
					const Icon(Icons.cloud_off_outlined, size: 42),
					const SizedBox(height: 8),
					const Text('تعذر تحميل المنتجات الجاهزة.'),
					const SizedBox(height: 8),
					FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة')),
				],
			),
		);
}

final _moneyFormat = NumberFormat('#,##0.##');
String _money(double value) => _moneyFormat.format(value);
String _plainNumber(double value) => value == value.truncateToDouble() ? value.toInt().toString() : '$value';