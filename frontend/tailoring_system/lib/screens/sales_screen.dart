import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart' as intl;

import 'measurements_screen.dart';

class SalesScreen extends StatefulWidget {
	const SalesScreen({super.key});

	@override
	State<SalesScreen> createState() => _SalesScreenState();
}

class _SalesScreenState extends State<SalesScreen> {
	static const _baseUrl = String.fromEnvironment(
		'LUMAR_API_URL',
		defaultValue: 'http://127.0.0.1:5092',
	);
	static const _defaultPieceType = 'ثوب';
	static const _pieceTypeOptions = [
		'ثوب', 'قميص', 'يلق', 'بنطلون', 'كوت', 'جاكيت', 'بالطو',
		'مريلة', 'فنيلة', 'طاقية', 'سروال', 'مقطب',
	];
	static const _dashboardCardHeight = 60.0;

	int piecesCount = 1;
	int currentCustomerId = 0;
	String currentCustomerName = '';
	String currentCustomerCode = '';
	String currentCustomerPhone = '';
	bool customerFound = false;
	String currentPoints = '0';
	String accumulatedPoints = '0';
	String treeCustomerCount = '0';
	String totalAmount = '0.00';
	String paidAmount = '0.00';
	String discountAmount = '0.00';
	String remainingAmount = '0.00';
	String deliveryDate = '';
	String profitPercentage = '12';
	String profitAmount = '1200';
	String activeOrdersCount = '3';
	String totalOrdersCount = '1200';
	String todayOrdersCount = '24';
	bool _savingOrder = false;

	final customerNameController = TextEditingController();
	final customerCodeController = TextEditingController();
	final customerIdController = TextEditingController();
	final phoneController = TextEditingController();
	final referrerController = TextEditingController();
	final currentPointsController = TextEditingController();
	final accumulatedPointsController = TextEditingController();
	final treeCustomerCountController = TextEditingController();
	final totalAmountController = TextEditingController();
	final paidAmountController = TextEditingController();
	final discountAmountController = TextEditingController();
	final remainingAmountController = TextEditingController();
	final deliveryDateController = TextEditingController();
	final profitPercentageController = TextEditingController();
	final profitAmountController = TextEditingController();
	final List<String> pieceTypes = [_defaultPieceType];
	final Map<String, Map<String, String>> measurementValuesByType = {};
	final List<TextEditingController> quantityControllers = [TextEditingController(text: '1')];
	final List<TextEditingController> fabricCodeControllers = [TextEditingController()];
	final List<TextEditingController> fabricTypeControllers = [TextEditingController()];
	final List<TextEditingController> fabricColorControllers = [TextEditingController()];
	final List<TextEditingController> notes1Controllers = [TextEditingController()];
	final List<TextEditingController> notes2Controllers = [TextEditingController()];

	@override
	void initState() {
		super.initState();
		profitPercentageController.text = profitPercentage;
		profitAmountController.text = profitAmount;
		_fillCustomerControllers();
		loadMeasurementPreview();
	}

	Future<void> searchCustomerByPhone() =>
			searchCustomerByTerm(phoneController.text.trim());

	Future<void> searchCustomerByTerm(String term) async {
		if (term.isEmpty) {
			_clearCustomer();
			await loadSelectedCustomer();
			return;
		}
		try {
			final uri = Uri.parse('$_baseUrl/customers/search')
					.replace(queryParameters: {'term': term});
			final response = await http.get(uri);
			if (response.statusCode < 200 || response.statusCode >= 300) {
				_clearCustomer();
				await loadSelectedCustomer();
				return;
			}
			final decoded = jsonDecode(response.body);
			final customers = decoded is List
					? decoded.whereType<Map<String, dynamic>>().toList()
					: <Map<String, dynamic>>[];
			if (customers.isEmpty) {
				_clearCustomer();
			} else if (customers.length == 1) {
				_applySelectedCustomer(customers.first);
			} else {
				final selected = await _pickCustomerFromPhoneMatches(customers);
				if (selected == null) return;
				_applySelectedCustomer(selected);
			}
			await loadSelectedCustomer();
		} catch (error) {
			debugPrint('Customer search failed: $error');
			_clearCustomer();
			await loadSelectedCustomer();
		}
	}

	void _clearCustomer() {
		if (!mounted) return;
		setState(() {
			customerFound = false;
			currentCustomerId = 0;
			currentCustomerCode = '';
			currentCustomerName = '';
		});
	}

	void _applySelectedCustomer(Map<String, dynamic> customer) {
		final id = int.tryParse(customer['customerId']?.toString() ?? '') ?? 0;
		setState(() {
			customerFound = id > 0;
			currentCustomerId = id;
			currentCustomerCode = customer['customerCode']?.toString() ?? '';
			currentCustomerName = customer['customerName']?.toString() ?? '';
			currentCustomerPhone =
					customer['phoneNumber']?.toString() ?? currentCustomerPhone;
		});
	}

	Future<Map<String, dynamic>?> _pickCustomerFromPhoneMatches(
		List<Map<String, dynamic>> customers,
	) {
		return showDialog<Map<String, dynamic>>(
			context: context,
			builder: (dialogContext) => AlertDialog(
				title: const Text('اختر العميل'),
				content: SizedBox(
					width: 420,
					child: ListView.separated(
						shrinkWrap: true,
						itemCount: customers.length,
						separatorBuilder: (_, __) => const Divider(height: 1),
						itemBuilder: (_, index) {
							final customer = customers[index];
							return ListTile(
								title: Text(customer['customerName']?.toString() ?? 'عميل بدون اسم'),
								subtitle: Text(
									'الكود: ${customer['customerCode'] ?? '-'} | الجوال: ${customer['phoneNumber'] ?? '-'}',
								),
								onTap: () => Navigator.of(dialogContext).pop(customer),
							);
						},
					),
				),
				actions: [
					TextButton(
						onPressed: () => Navigator.of(dialogContext).pop(),
						child: const Text('إلغاء'),
					),
				],
			),
		);
	}

	Future<void> _searchCustomerAndNextFocus(String term) async {
		await searchCustomerByTerm(term);
		if (mounted) FocusScope.of(context).nextFocus();
	}

	Future<void> saveOrder() async {
		if (_savingOrder) return;
		var customerCreated = false;
		final pendingMeasurements = {
			for (final entry in measurementValuesByType.entries) entry.key: Map<String, String>.from(entry.value),
		};
		currentCustomerPhone = phoneController.text.trim();
		if (currentCustomerPhone.isEmpty) {
			_showMessage('الرجاء إدخال رقم الهاتف');
			return;
		}
		if (!customerFound) {
			final name = customerNameController.text.trim();
			final code = customerCodeController.text.trim();
			if (name.isEmpty) {
				_showMessage('الرجاء إدخال اسم العميل لإنشاء عميل جديد');
				return;
			}
			if (code.isEmpty) {
				_showMessage('الرجاء إدخال كود العميل لإنشاء عميل جديد');
				return;
			}
			try {
				final response = await http.post(
					Uri.parse('$_baseUrl/customers'),
					headers: {'Content-Type': 'application/json'},
					body: jsonEncode({
						'customerCode': code,
						'customerName': name,
						'phoneNumber': currentCustomerPhone,
					}),
				);
				if (response.statusCode != 200 && response.statusCode != 201) {
					_showMessage('تعذر إنشاء العميل. تحقق من البيانات المدخلة.');
					return;
				}
				final decoded = jsonDecode(response.body);
				if (decoded is Map<String, dynamic>) {
					setState(() {
						customerCreated = true;
						customerFound = true;
						currentCustomerId =
								int.tryParse(decoded['customerId']?.toString() ?? '') ?? 0;
						currentCustomerCode = decoded['customerCode']?.toString() ?? code;
						currentCustomerName = name;
					});
					_fillCustomerControllers();
					measurementValuesByType
						..clear()
						..addAll(pendingMeasurements);
				}
			} catch (error) {
				debugPrint('Customer creation failed: $error');
				_showMessage('تعذر الاتصال بالخادم لإنشاء العميل.');
				return;
			}
		}
		final total = double.tryParse(totalAmountController.text.trim().replaceAll(',', '.'));
		final discount = double.tryParse(discountAmountController.text.trim().replaceAll(',', '.')) ?? 0;
		final advance = double.tryParse(paidAmountController.text.trim().replaceAll(',', '.')) ?? 0;
		if (total == null || total < 0 || discount < 0 || advance < 0 || discount + advance > total) {
			_showMessage('تحقق من الإجمالي والخصم والدفعة المقدمة.');
			return;
		}
		final items = <Map<String, dynamic>>[];
		for (var index = 0; index < pieceTypes.length; index++) {
			final quantity = int.tryParse(quantityControllers[index].text.trim());
			if (quantity == null || quantity <= 0) {
				_showMessage('أدخل عددًا صحيحًا للقطعة رقم ${index + 1}.');
				return;
			}
			final fabricCode = fabricCodeControllers[index].text.trim();
			final fabricType = fabricTypeControllers[index].text.trim();
			final fabricColor = fabricColorControllers[index].text.trim();
			final hasFabric = fabricCode.isNotEmpty || fabricType.isNotEmpty || fabricColor.isNotEmpty;
			items.add({
				'pieceType': pieceTypes[index],
				'quantity': quantity,
				'fabricCode': fabricCode.isEmpty ? null : fabricCode,
				'fabricType': fabricType.isEmpty ? null : fabricType,
				'fabricColor': fabricColor.isEmpty ? null : fabricColor,
				'notes1': _nullableText(notes1Controllers[index]),
				'notes2': _nullableText(notes2Controllers[index]),
				'measurementSnapshot': jsonEncode(measurementValuesByType[pieceTypes[index]] ?? const {}),
				if (hasFabric) 'fabric': {
					'fabricCode': fabricCode.isEmpty ? null : fabricCode,
					'fabricType': fabricType.isEmpty ? null : fabricType,
					'fabricColor': fabricColor.isEmpty ? null : fabricColor,
					'quantity': quantity,
					'unit': 'Piece',
					'unitCost': 0,
				},
			});
		}
		setState(() => _savingOrder = true);
		try {
			if (customerCreated) await _persistMeasurements();
			final delivery = deliveryDateController.text.trim();
			final response = await http.post(
				Uri.parse('$_baseUrl/orders'),
				headers: {'Content-Type': 'application/json'},
				body: jsonEncode({
					'customerId': currentCustomerId,
					'orderDate': DateTime.now().toUtc().toIso8601String(),
					'deliveryDate': delivery.isEmpty ? null : DateTime.tryParse(delivery)?.toUtc().toIso8601String(),
					'totalAmount': total,
					'discountAmount': discount,
					'advancePayment': advance,
					'urgencyStatus': 'Normal',
					'saleCategory': 'TailoringOrder',
					'paymentMethod': advance > 0 ? 'Cash' : null,
					'items': items,
				}),
			);
			if (response.statusCode != 201) {
				_showMessage('تعذر حفظ الطلب: ${response.body}');
				return;
			}
			final decoded = jsonDecode(response.body) as Map<String, dynamic>;
			_showMessage('تم حفظ الطلب ${decoded['orderNumber'] ?? ''} بنجاح.');
		} catch (error) {
			debugPrint('Order creation failed: $error');
			_showMessage('تعذر الاتصال بالخادم لحفظ الطلب.');
		} finally {
			if (mounted) setState(() => _savingOrder = false);
		}
	}

	Future<void> _persistMeasurements() async {
		final api = MeasurementsApi(baseUrl: _baseUrl);
		for (final entry in measurementValuesByType.entries) {
			final values = <String, double>{};
			for (final measurement in entry.value.entries) {
				final value = double.tryParse(measurement.value.replaceAll(',', '.'));
				if (value != null && value >= 0) values[measurement.key] = value;
			}
			if (values.isNotEmpty) await api.upsert(currentCustomerId, entry.key, values);
		}
	}

	String? _nullableText(TextEditingController controller) {
		final value = controller.text.trim();
		return value.isEmpty ? null : value;
	}

	void _updateRemainingAmount(String _) {
		final total = double.tryParse(totalAmountController.text.trim().replaceAll(',', '.')) ?? 0;
		final discount = double.tryParse(discountAmountController.text.trim().replaceAll(',', '.')) ?? 0;
		final paid = double.tryParse(paidAmountController.text.trim().replaceAll(',', '.')) ?? 0;
		remainingAmountController.text = (total - discount - paid).clamp(0, double.infinity).toStringAsFixed(2);
	}

	void _showMessage(String message) {
		if (mounted) {
			ScaffoldMessenger.of(context)
					.showSnackBar(SnackBar(content: Text(message)));
		}
	}

	void _fillCustomerControllers() {
		customerNameController.text = currentCustomerName;
		customerCodeController.text = currentCustomerCode;
		customerIdController.text = currentCustomerId > 0 ? '$currentCustomerId' : '';
		phoneController.text = currentCustomerPhone;
		currentPointsController.text = currentPoints;
		accumulatedPointsController.text = accumulatedPoints;
		treeCustomerCountController.text = treeCustomerCount;
		totalAmountController.text = totalAmount;
		paidAmountController.text = paidAmount;
		discountAmountController.text = discountAmount;
		remainingAmountController.text = remainingAmount;
		deliveryDateController.text = deliveryDate;
	}

	Future<void> loadSelectedCustomer() async {
		_fillCustomerControllers();
		await loadMeasurementPreview();
	}

	void _updateProfitAmount() {
		final percent = double.tryParse(profitPercentageController.text) ?? 0;
		final base = double.tryParse(totalAmount) ?? 0;
		profitAmount = (base * percent / 100).toStringAsFixed(2);
		profitAmountController.text = profitAmount;
	}

	List<String> _uniquePieceTypes() => pieceTypes.toSet().toList();

	void _syncPieceTypesWithCount() {
		while (pieceTypes.length < piecesCount) {
			pieceTypes.add(_defaultPieceType);
			quantityControllers.add(TextEditingController(text: '1'));
			fabricCodeControllers.add(TextEditingController());
			fabricTypeControllers.add(TextEditingController());
			fabricColorControllers.add(TextEditingController());
			notes1Controllers.add(TextEditingController());
			notes2Controllers.add(TextEditingController());
		}
		if (pieceTypes.length > piecesCount) {
			for (final controllers in [quantityControllers, fabricCodeControllers, fabricTypeControllers, fabricColorControllers, notes1Controllers, notes2Controllers]) {
				for (final controller in controllers.sublist(piecesCount)) {
					controller.dispose();
				}
				controllers.removeRange(piecesCount, controllers.length);
			}
			pieceTypes.removeRange(piecesCount, pieceTypes.length);
		}
	}

	Future<void> loadMeasurementPreview() async {
		measurementValuesByType.clear();
		for (final type in _uniquePieceTypes()) {
			measurementValuesByType[type] = {
				for (final field in _measurementFieldsForPiece(type)) field: '---',
			};
		}
		if (currentCustomerId > 0) {
			try {
				final response = await http.get(
					Uri.parse('$_baseUrl/customers/$currentCustomerId/measurements'),
				);
				if (response.statusCode >= 200 && response.statusCode < 300) {
					final decoded = jsonDecode(response.body);
					if (decoded is List) {
						for (final item in decoded.whereType<Map<String, dynamic>>()) {
							final type = item['pieceType']?.toString();
							final name = item['measurementName']?.toString();
							final value = item['measurementValue']?.toString() ?? '';
							if (type != null && name != null && measurementValuesByType[type]?[name] == '---') {
								measurementValuesByType[type]![name] = value.isEmpty ? '---' : value;
							}
						}
					}
				}
			} catch (error) {
				debugPrint('Failed to load measurements: $error');
			}
		}
		if (mounted) setState(() {});
	}

	List<String> _measurementFieldsForPiece(String pieceType) {
		const fieldsByPieceType = <String, List<String>>{
			'كوت': [
				'الطول',
				'الكتف',
				'اليد',
				'وسع الصدر',
				'وسع البطن',
				'فتحة اليد',
				'وسع المرفق',
			],
			'ثوب': [
				'الطول',
				'الكتف',
				'اليد',
				'وسع الصدر',
				'وسع البطن',
				'الرقبة',
				'طول الكبك',
				'عرض الكبك',
				'وسع المرفق',
				'فتحة أسفل الثوب',
			],
			'قميص': [
				'الطول',
				'الكتف',
				'اليد',
				'وسع الصدر',
				'وسع البطن',
				'الرقبة',
				'طول الكبك',
				'عرض الكبك',
				'وسع المرفق',
			],
			'يلق': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'],
			'بنطلون': [
				'الطول',
				'الحزام',
				'الأرداف',
				'الفخذ',
				'الركبة',
				'الفتحة',
				'عرض الحزام',
			],
			'مريلة': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن'],
			'بالطو': ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
			'جاكيت': ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
			'سروال': ['الطول', 'الحزام', 'الأرداف'],
			'فنيلة': ['الطول', 'الكتف', 'وسع الصدر', 'وسع البطن', 'الرقبة'],
			'مقطب': ['الطول', 'العرض'],
			'طاقية': ['دوران الرأس', 'ارتفاع الحزام'],
		};
		return fieldsByPieceType[pieceType] ?? const [];
	}

	@override
	void dispose() {
		for (final controller in [
			customerNameController, customerCodeController, customerIdController,
			phoneController, referrerController, currentPointsController,
			accumulatedPointsController, treeCustomerCountController,
			totalAmountController, paidAmountController, discountAmountController,
			remainingAmountController, deliveryDateController,
			profitPercentageController, profitAmountController,
		]) {
			controller.dispose();
		}
		for (final controllers in [quantityControllers, fabricCodeControllers, fabricTypeControllers, fabricColorControllers, notes1Controllers, notes2Controllers]) {
			for (final controller in controllers) {
				controller.dispose();
			}
		}
		super.dispose();
	}

	@override
	Widget build(BuildContext context) => Scaffold(
				appBar: AppBar(title: const Text('المبيعات')),
				body: SingleChildScrollView(
					padding: const EdgeInsets.all(20),
					child: Column(
						children: [
							_buildDashboardCards(),
							const SizedBox(height: 20),
							_buildMainTopSection(),
							const SizedBox(height: 20),
							SizedBox(
								width: double.infinity,
								height: 50,
								child: ElevatedButton(
									onPressed: _savingOrder ? null : saveOrder,
									child: _savingOrder ? const CircularProgressIndicator() : const Text('حفظ الطلب'),
								),
							),
						],
					),
				),
			);

	Widget _buildDashboardCards() => LayoutBuilder(
				builder: (context, constraints) {
					final cards = <Widget>[
						_statInput('L1', profitPercentageController, suffix: '%', onChanged: (_) => _updateProfitAmount()),
						_statInput('L2', profitAmountController, readOnly: true),
						_statValue('الطلبات النشطة', activeOrdersCount),
						_statValue('إجمالي الطلبات', totalOrdersCount),
						_statValue('طلبات اليوم', todayOrdersCount),
						_statValue('تاريخ اليوم', intl.DateFormat('yyyy/MM/dd').format(DateTime.now())),
					];
					final columns = constraints.maxWidth < 900 ? 2 : 6;
					final spacing = constraints.maxWidth < 900 ? 8.0 : 12.0;
					final width = (constraints.maxWidth - spacing * (columns - 1)) / columns;
					return Wrap(
						spacing: spacing,
						runSpacing: 8,
						children: cards.map((card) => SizedBox(width: width, child: card)).toList(),
					);
				},
			);

	Widget _statInput(String label, TextEditingController controller,
			{bool readOnly = false, String? suffix, ValueChanged<String>? onChanged}) {
		return SizedBox(
			height: _dashboardCardHeight,
			child: Card(
				color: const Color(0xFFEFF4F7),
				child: Padding(
					padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
					child: TextFormField(
						controller: controller,
						readOnly: readOnly,
						onChanged: onChanged,
						textAlign: TextAlign.center,
						keyboardType: TextInputType.number,
						style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
						decoration: _statDecoration(label, suffix),
					),
				),
			),
		);
	}

	Widget _statValue(String label, String value) => SizedBox(
				height: _dashboardCardHeight,
				child: Card(
					color: const Color(0xFFEFF4F7),
					child: Padding(
						padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
						child: InputDecorator(
							decoration: _statDecoration(label, null),
							child: Center(child: Text(value, style: const TextStyle(fontWeight: FontWeight.bold))),
						),
					),
				),
			);

	InputDecoration _statDecoration(String label, String? suffix) => InputDecoration(
				labelText: label,
				suffixText: suffix,
				floatingLabelBehavior: FloatingLabelBehavior.always,
				border: const OutlineInputBorder(),
				isDense: true,
				contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 12),
			);

	Widget _buildMainTopSection() => LayoutBuilder(
				builder: (context, constraints) => constraints.maxWidth < 900
						? Column(children: [
								_buildPiecesSection(),
								const SizedBox(height: 20),
								_measurementPreview(),
								const SizedBox(height: 20),
								_customerCard(),
							])
						: Row(
								crossAxisAlignment: CrossAxisAlignment.start,
								children: [
									Expanded(
										flex: 2,
										child: Column(
											crossAxisAlignment: CrossAxisAlignment.stretch,
											children: [
												_buildPiecesSection(),
												const SizedBox(height: 20),
												_measurementPreview(),
											],
										),
									),
									const SizedBox(width: 20),
									Expanded(child: _customerCard()),
								],
							),
			);

	Widget _measurementPreview() => _darkCard(
				Column(
					crossAxisAlignment: CrossAxisAlignment.start,
					children: [
						const Text('عرض المقاسات', style: _darkTitle),
						const SizedBox(height: 12),
						for (final type in _uniquePieceTypes())
							Container(
								width: double.infinity,
								margin: const EdgeInsets.only(bottom: 10),
								padding: const EdgeInsets.all(12),
								decoration: BoxDecoration(color: const Color(0xFF1F2937), borderRadius: BorderRadius.circular(8)),
								child: Column(
									crossAxisAlignment: CrossAxisAlignment.start,
									children: [
										Text(type, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
										const SizedBox(height: 8),
										Wrap(
											spacing: 6,
											runSpacing: 6,
											children: _measurementFieldsForPiece(type).map((field) {
												final value = measurementValuesByType[type]?[field] ?? '---';
												return Chip(label: Text('$field: $value'));
											}).toList(),
										),
									],
								),
							),
					],
				),
			);

	Widget _customerCard() => _darkCard(
				Column(
					crossAxisAlignment: CrossAxisAlignment.end,
					children: [
						const Text('معلومات العميل', style: _darkTitle),
						const SizedBox(height: 16),
						_customerField('رقم الهاتف', phoneController,
								onChanged: (value) => currentCustomerPhone = value,
								onEditingComplete: () => _searchCustomerAndNextFocus(phoneController.text.trim())),
						_customerField('اسم العميل', customerNameController,
								onEditingComplete: () => _searchCustomerAndNextFocus(customerNameController.text.trim())),
						_customerField('كود العميل', customerCodeController,
								onEditingComplete: () => _searchCustomerAndNextFocus(customerCodeController.text.trim())),
						_customerField('العميل المعرف', referrerController),
						_customerField('صلة القرابة', null),
						_customerField('النقاط الحالية', currentPointsController, readOnly: true),
						_customerField('النقاط التراكمية', accumulatedPointsController, readOnly: true),
						_customerField('عدد العملاء في الشجرة', treeCustomerCountController, readOnly: true),
						_customerField('القيمة الإجمالية', totalAmountController, onChanged: _updateRemainingAmount),
						_customerField('المدفوع مقدماً', paidAmountController, onChanged: _updateRemainingAmount),
						_customerField('الخصم', discountAmountController, onChanged: _updateRemainingAmount),
						_customerField('المتبقي بعد الخصم', remainingAmountController, readOnly: true),
						_customerField('تاريخ الاستلام YYYY-MM-DD', deliveryDateController),
					],
				),
			);

	Widget _customerField(String label, TextEditingController? controller,
			{bool readOnly = false, ValueChanged<String>? onChanged, VoidCallback? onEditingComplete}) {
		return Padding(
			padding: const EdgeInsets.only(bottom: 12),
			child: TextFormField(
				controller: controller,
				readOnly: readOnly,
				onChanged: onChanged,
				onEditingComplete: onEditingComplete ?? () => FocusScope.of(context).nextFocus(),
				textInputAction: TextInputAction.next,
				textAlign: TextAlign.right,
				decoration: InputDecoration(labelText: label, filled: true, fillColor: const Color(0xFF171D2A)),
				style: const TextStyle(color: Colors.white),
			),
		);
	}

	Widget _buildPiecesSection() {
		_syncPieceTypesWithCount();
		return _darkCard(
			Column(
				crossAxisAlignment: CrossAxisAlignment.start,
				children: [
					const Text('القطع المطلوبة', style: _darkTitle),
					const SizedBox(height: 6),
					for (int index = 1; index <= piecesCount; index++) ...[
						_pieceCard(index),
						if (index != piecesCount) const SizedBox(height: 10),
					],
				],
			),
		);
	}

	Widget _pieceCard(int index) => Card(
				color: const Color(0xFF1F2937),
				child: Padding(
					padding: const EdgeInsets.all(8),
					child: Column(
						crossAxisAlignment: CrossAxisAlignment.start,
						children: [
							Text('قطعة رقم $index', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
							if (index == 1) ...[
								const SizedBox(height: 8),
								SizedBox(
									width: double.infinity,
									child: ElevatedButton.icon(
										onPressed: () async {
											setState(() => piecesCount++);
											_syncPieceTypesWithCount();
											await loadMeasurementPreview();
										},
										icon: const Icon(Icons.add),
										label: const Text('إضافة قطعة'),
									),
								),
							],
							const SizedBox(height: 8),
							LayoutBuilder(
								builder: (context, constraints) => GridView.count(
									crossAxisCount: constraints.maxWidth < 700 ? 2 : 4,
									shrinkWrap: true,
									physics: const NeverScrollableScrollPhysics(),
									crossAxisSpacing: 6,
									mainAxisSpacing: 6,
									childAspectRatio: 2.9,
									children: [
										DropdownButtonFormField<String>(
											initialValue: pieceTypes[index - 1],
											decoration: _pieceDecoration('نوع القطعة'),
											items: _pieceTypeOptions.map((type) => DropdownMenuItem(value: type, child: Text(type))).toList(),
											onChanged: (value) async {
												if (value == null) return;
												setState(() => pieceTypes[index - 1] = value);
												await loadMeasurementPreview();
											},
										),
										_pieceField('عدد القطع', quantityControllers[index - 1], number: true),
										_pieceField('كود القماش', fabricCodeControllers[index - 1]),
										_pieceField('نوع القماش', fabricTypeControllers[index - 1]),
										_pieceField('لون القماش', fabricColorControllers[index - 1]),
										_disabledChoice('طلب 1'),
										_disabledChoice('طلب 2'),
										_disabledChoice('طلب 3'),
										_pieceField('ملاحظات خاصة 1', notes1Controllers[index - 1]),
										_pieceField('ملاحظات خاصة 2', notes2Controllers[index - 1]),
									],
								),
							),
							const SizedBox(height: 8),
							SizedBox(
								width: double.infinity,
								child: ElevatedButton.icon(
									onPressed: () async {
										final pieceType = pieceTypes[index - 1];
										final fields = _measurementFieldsForPiece(pieceType);
										final values = await Navigator.push<Map<String, String>>(
											context,
											MaterialPageRoute(
												builder: (_) => MeasurementEntryScreen(
													pieceType: pieceType,
													fields: fields,
													initialValues: measurementValuesByType[pieceType] ?? const {},
													onSave: currentCustomerId > 0
															? (values) => MeasurementsApi(baseUrl: _baseUrl).upsert(currentCustomerId, pieceType, values)
															: null,
												),
											),
										);
										if (values != null && mounted) {
											setState(() => measurementValuesByType[pieceType] = values);
										}
									},
									icon: const Icon(Icons.straighten),
									label: const Text('المقاسات'),
								),
							),
						],
					),
				),
			);

	Widget _pieceField(String label, TextEditingController controller, {bool number = false}) => TextField(
				controller: controller,
				decoration: _pieceDecoration(label),
				keyboardType: number ? TextInputType.number : null,
				textInputAction: TextInputAction.next,
				style: const TextStyle(color: Colors.white, fontSize: 12),
			);

	Widget _disabledChoice(String label) => DropdownButtonFormField<String>(
				decoration: _pieceDecoration(label),
				items: const [],
				onChanged: null,
			);

	InputDecoration _pieceDecoration(String label) => InputDecoration(
				labelText: label,
				floatingLabelBehavior: FloatingLabelBehavior.always,
				filled: true,
				fillColor: const Color(0xFF171D2A),
				border: const OutlineInputBorder(),
				isDense: true,
			);

	Widget _darkCard(Widget child) => Card(
				color: const Color(0xFF111827),
				child: Padding(padding: const EdgeInsets.all(16), child: child),
			);

	static const _darkTitle = TextStyle(
		fontSize: 18,
		fontWeight: FontWeight.bold,
		color: Colors.white,
	);
}