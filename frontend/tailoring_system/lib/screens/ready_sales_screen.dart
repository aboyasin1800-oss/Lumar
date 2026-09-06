import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

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
	final List<_CartLine> cart = [];
	_ReadyProduct? selectedProduct;
	_Customer? selectedCustomer;
	bool searchingCustomers = false;
	String paymentMethod = 'نقداً';

	@override
	void initState() {
		super.initState();
		productsFuture = _loadProducts();
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
		if (responses.any((response) =>
				response.statusCode < 200 || response.statusCode >= 300)) {
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

	Future<void> _searchCustomers() async {
		final term = customerSearch.text.trim();
		if (term.isEmpty) {
			setState(() => customerResults = const []);
			return;
		}
		setState(() => searchingCustomers = true);
		try {
			final uri = Uri.parse('$_baseUrl/customers/search')
					.replace(queryParameters: {'term': term});
			final response = await http.get(uri);
			if (response.statusCode < 200 || response.statusCode >= 300) {
				throw Exception();
			}
			final values = (jsonDecode(response.body) as List)
					.cast<Map<String, dynamic>>()
					.map(_Customer.fromJson)
					.toList();
			if (mounted) setState(() => customerResults = values);
		} catch (error) {
			debugPrint('Ready-sale customer search failed: $error');
			if (mounted) {
				setState(() => customerResults = const []);
				ScaffoldMessenger.of(context).showSnackBar(
					const SnackBar(content: Text('تعذر البحث عن العميل.')),
				);
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
			ScaffoldMessenger.of(context).showSnackBar(
				const SnackBar(content: Text('الكمية المطلوبة أكبر من المتاح.')),
			);
			return;
		}
		setState(() {
			final index = cart.indexWhere((line) => line.product.id == product.id);
			if (index < 0) {
				cart.add(_CartLine(product: product, quantity: quantity, price: price));
			} else {
				cart[index] = _CartLine(
					product: product,
					quantity: quantity,
					price: price,
				);
			}
			selectedProduct = null;
			productSearch.clear();
			priceController.clear();
			quantityController.text = '1';
		});
	}

	double get subtotal =>
			cart.fold(0, (sum, line) => sum + line.quantity * line.price);
	double get discount =>
			double.tryParse(discountController.text.trim()) ?? 0;
	double get total => (subtotal - discount).clamp(0, double.infinity);
	double get paid => double.tryParse(paidController.text.trim()) ?? 0;
	double get remaining => (total - paid).clamp(0, double.infinity);

	@override
	Widget build(BuildContext context) => ColoredBox(
				color: _background,
				child: FutureBuilder<List<_ReadyProduct>>(
					future: productsFuture,
					builder: (context, snapshot) {
						if (snapshot.connectionState != ConnectionState.done) {
							return const Center(child: CircularProgressIndicator());
						}
						if (snapshot.hasError) {
							return _LoadError(
								onRetry: () => setState(() => productsFuture = _loadProducts()),
							);
						}
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
										Flexible(
											flex: 2,
											child: _customerAndPaymentPanel(),
										),
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
		final filtered = query.isEmpty
				? products.take(8).toList()
				: products
						.where((product) => product.searchText.contains(query))
						.take(12)
						.toList();
		return Column(children: [
			_section(
				title: 'البحث عن المنتج الجاهز',
				child: Column(children: [
					TextField(
						controller: productSearch,
						onChanged: (_) => setState(() {}),
						decoration: _decoration(
							'ابحث بالكود أو الاسم أو رقم التتبع',
							prefixIcon: Icons.search,
						),
					),
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
										title: Text('${product.code} - ${product.name}',
												maxLines: 1, overflow: TextOverflow.ellipsis),
										subtitle: Text(
												'${product.source} | المتاح ${_plainNumber(product.availableQuantity)}'),
										onTap: () => _selectProduct(product),
									);
								},
							),
						),
					],
					const SizedBox(height: 4),
					Row(children: [
						Expanded(
							child: TextField(
								controller: quantityController,
								keyboardType: TextInputType.number,
								decoration: _decoration('الكمية'),
							),
						),
						const SizedBox(width: 4),
						Expanded(
							child: TextField(
								controller: priceController,
								keyboardType: const TextInputType.numberWithOptions(
										decimal: true),
								decoration: _decoration('السعر'),
							),
						),
					]),
					const SizedBox(height: 4),
					SizedBox(
						width: double.infinity,
						height: 34,
						child: FilledButton.icon(
							style: FilledButton.styleFrom(
								backgroundColor: _accent,
								foregroundColor: Colors.black,
							),
							onPressed: selectedProduct == null ? null : _addProduct,
							icon: const Icon(Icons.add_shopping_cart, size: 16),
							label: const Text('إضافة منتج'),
						),
					),
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
											leading: IconButton(
												tooltip: 'حذف المنتج',
												icon: const Icon(Icons.delete_outline,
														color: Colors.redAccent),
												onPressed: () => setState(() => cart.removeAt(index)),
											),
											title: Text('${line.product.code} - ${line.product.name}'),
											subtitle: Text(
													'${_plainNumber(line.quantity)} × ${_money(line.price)}'),
											trailing: Text(_money(line.quantity * line.price),
													style: const TextStyle(fontWeight: FontWeight.bold)),
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
							Expanded(
								child: TextField(
									controller: customerSearch,
									textInputAction: TextInputAction.search,
									onSubmitted: (_) => _searchCustomers(),
									decoration: _decoration(
										'الاسم أو الهاتف أو الكود',
										prefixIcon: Icons.search,
									),
								),
							),
							const SizedBox(width: 4),
							IconButton.filled(
								tooltip: 'بحث',
								onPressed: searchingCustomers ? null : _searchCustomers,
								icon: searchingCustomers
										? const SizedBox.square(
												dimension: 16,
												child: CircularProgressIndicator(strokeWidth: 2))
										: const Icon(Icons.search),
							),
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
												customerSearch.text =
														'${customer.code} - ${customer.name}';
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
								decoration: _decoration('الموظف المسؤول'),
								hint: const Text('غير محدد'),
								items: const [],
								onChanged: null,
							),
							const SizedBox(height: 5),
							_amountRow('الإجمالي', _money(subtotal)),
							const SizedBox(height: 4),
							TextField(
								controller: discountController,
								keyboardType:
										const TextInputType.numberWithOptions(decimal: true),
								onChanged: (_) => setState(() {}),
								decoration: _decoration('الخصم'),
							),
							const SizedBox(height: 4),
							_amountRow('المبلغ المطلوب', _money(total)),
							const SizedBox(height: 4),
							TextField(
								controller: paidController,
								keyboardType:
										const TextInputType.numberWithOptions(decimal: true),
								onChanged: (_) => setState(() {}),
								decoration: _decoration('المدفوع'),
							),
							const SizedBox(height: 4),
							_amountRow('المتبقي', _money(remaining)),
							const SizedBox(height: 6),
							Align(
								alignment: Alignment.centerRight,
								child: Text('طريقة الدفع',
										style: Theme.of(context).textTheme.labelMedium),
							),
							const SizedBox(height: 4),
							Wrap(
								spacing: 4,
								runSpacing: 4,
								children: ['نقداً', 'عن طريق العميل', 'آجل']
										.map((method) => ChoiceChip(
													selected: paymentMethod == method,
													label: Text(method),
													onSelected: (_) =>
															setState(() => paymentMethod = method),
												))
										.toList(),
							),
							const Spacer(),
							Tooltip(
								message: 'لا تتوفر خدمة حفظ المبيعات الجاهزة في API الحالي',
								child: SizedBox(
									width: double.infinity,
									height: 36,
									child: FilledButton.icon(
										onPressed: null,
										icon: const Icon(Icons.check, size: 17),
										label: const Text('بيع'),
									),
								),
							),
						]),
					),
				),
			]);

	Widget _customerSummary() {
		final customer = selectedCustomer;
		if (customer == null) {
			return Container(
				width: double.infinity,
				padding: const EdgeInsets.all(8),
				decoration: _boxDecoration(),
				child: const Text('لم يتم اختيار عميل.'),
			);
		}
		return Container(
			width: double.infinity,
			padding: const EdgeInsets.all(8),
			decoration: _boxDecoration(),
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.start,
				children: [
					Text(customer.name,
							style: const TextStyle(fontWeight: FontWeight.bold)),
					Text('الكود: ${customer.code}'),
					Text('الهاتف: ${customer.phone}'),
					Text('النقاط: ${_plainNumber(customer.points)}'),
					Text('المديونية: ${_money(customer.debts)}'),
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

	Widget _section({
		required String title,
		required Widget child,
		bool expand = false,
	}) {
		final content = Container(
			width: double.infinity,
			padding: const EdgeInsets.all(7),
			decoration: BoxDecoration(
				color: _panel,
				borderRadius: BorderRadius.circular(5),
				border: Border.all(color: _border),
			),
			child: Column(
				crossAxisAlignment: CrossAxisAlignment.stretch,
				children: [
					Text(title,
							style: const TextStyle(
									color: _accent, fontSize: 12, fontWeight: FontWeight.bold)),
					const SizedBox(height: 5),
					if (expand) Expanded(child: child) else child,
				],
			),
		);
		return expand ? content : IntrinsicHeight(child: content);
	}

	InputDecoration _decoration(String label, {IconData? prefixIcon}) =>
			InputDecoration(
				labelText: label,
				labelStyle: const TextStyle(fontSize: 10),
				prefixIcon: prefixIcon == null ? null : Icon(prefixIcon, size: 17),
				filled: true,
				fillColor: _field,
				isDense: true,
				contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 9),
				border: OutlineInputBorder(borderRadius: BorderRadius.circular(5)),
				enabledBorder: OutlineInputBorder(
					borderRadius: BorderRadius.circular(5),
					borderSide: const BorderSide(color: _border),
				),
				focusedBorder: OutlineInputBorder(
					borderRadius: BorderRadius.circular(5),
					borderSide: const BorderSide(color: _accent),
				),
			);

	BoxDecoration _boxDecoration() => BoxDecoration(
				color: _field,
				borderRadius: BorderRadius.circular(5),
				border: Border.all(color: _border),
			);
}

class _ReadyProduct {
	const _ReadyProduct({
		required this.id,
		required this.code,
		required this.name,
		required this.source,
		required this.availableQuantity,
		required this.price,
	});

	factory _ReadyProduct.fromOurProduction(Map<String, dynamic> json) =>
			_ReadyProduct(
				id: 'our-${json['readyMadeInventoryProductId']}',
				code: json['trackingCode']?.toString() ?? '-',
				name: json['productionName']?.toString() ??
						json['pieceType']?.toString() ??
						'-',
				source: 'إنتاجنا',
				availableQuantity:
						json['status'] == 'AvailableForSale' && json['isActive'] == true
								? 1
								: 0,
				price: (json['suggestedSellingPrice'] as num?)?.toDouble() ?? 0,
			);

	factory _ReadyProduct.fromImported(Map<String, dynamic> json) =>
			_ReadyProduct(
				id: 'imported-${json['importedReadyMadeProductId']}',
				code: json['productCode']?.toString() ?? '-',
				name: json['productName']?.toString() ?? '-',
				source: 'مستورد',
				availableQuantity: json['isActive'] == true
						? (json['quantity'] as num?)?.toDouble() ?? 0
						: 0,
				price: (json['sellingPrice'] as num?)?.toDouble() ?? 0,
			);

	final String id;
	final String code;
	final String name;
	final String source;
	final double availableQuantity;
	final double price;

	String get searchText => '$code $name $source'.toLowerCase();
}

class _Customer {
	const _Customer({
		required this.id,
		required this.code,
		required this.name,
		required this.phone,
		required this.points,
		required this.debts,
	});

	factory _Customer.fromJson(Map<String, dynamic> json) => _Customer(
				id: json['customerId'] as int,
				code: json['customerCode']?.toString() ?? '-',
				name: json['customerName']?.toString() ?? '-',
				phone: json['phoneNumber']?.toString() ?? '-',
				points: (json['totalPoints'] as num?)?.toDouble() ?? 0,
				debts: (json['totalDebts'] as num?)?.toDouble() ?? 0,
			);

	final int id;
	final String code;
	final String name;
	final String phone;
	final double points;
	final double debts;
}

class _CartLine {
	const _CartLine({
		required this.product,
		required this.quantity,
		required this.price,
	});

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
						FilledButton.icon(
							onPressed: onRetry,
							icon: const Icon(Icons.refresh),
							label: const Text('إعادة المحاولة'),
						),
					],
				),
			);
}

final _moneyFormat = NumberFormat('#,##0.##');
String _money(double value) => _moneyFormat.format(value);
String _plainNumber(double value) =>
		value == value.truncateToDouble() ? value.toInt().toString() : '$value';