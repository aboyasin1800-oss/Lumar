import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import 'printing/work_card_screen.dart';

class OrderDetailsScreen extends StatefulWidget {
	const OrderDetailsScreen({required this.orderId, super.key});
	final int orderId;

	@override
	State<OrderDetailsScreen> createState() => _OrderDetailsScreenState();
}

class _OrderDetailsScreenState extends State<OrderDetailsScreen> {
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5092');
	late final Future<_OrderDetailsData> _details = _load();

	Future<_OrderDetailsData> _load() async {
		final responses = await Future.wait([
			http.get(Uri.parse('$_baseUrl/orders/${widget.orderId}')),
			http.get(Uri.parse('$_baseUrl/orders/${widget.orderId}/items')),
			http.get(Uri.parse('$_baseUrl/orders/${widget.orderId}/pieces')),
		]);
		if (responses.any((response) => response.statusCode < 200 || response.statusCode >= 300)) throw Exception();
		return _OrderDetailsData(
			order: jsonDecode(responses[0].body) as Map<String, dynamic>,
			items: (jsonDecode(responses[1].body) as List).cast<Map<String, dynamic>>(),
			pieces: (jsonDecode(responses[2].body) as List).cast<Map<String, dynamic>>(),
		);
	}

	@override
	Widget build(BuildContext context) => Scaffold(
		appBar: AppBar(title: Text('تفاصيل الطلب ${widget.orderId}')),
		body: FutureBuilder<_OrderDetailsData>(
			future: _details,
			builder: (context, snapshot) {
				if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
				if (snapshot.hasError || snapshot.data == null) return const Center(child: Text('تعذر تحميل تفاصيل الطلب.'));
				final data = snapshot.data!;
				final order = data.order;
				return ListView(
					padding: const EdgeInsets.all(24),
					children: [
						Wrap(spacing: 12, runSpacing: 12, children: [
							_Detail('رقم الطلب', order['orderNumber']),
							_Detail('معرف العميل', order['customerId']),
							_Detail('الحالة', order['orderStatus']),
							_Detail('الإجمالي', order['totalAmount']),
							_Detail('المدفوع', order['paidAmount']),
							_Detail('المتبقي', order['remainingAmount']),
							_Detail('تاريخ التسليم', order['deliveryDate']),
						]),
						const SizedBox(height: 24),
						Text('بنود الطلب', style: Theme.of(context).textTheme.titleLarge),
						const SizedBox(height: 8),
						if (data.items.isEmpty) const Text('لا توجد بنود.') else ...data.items.map((item) => Card(child: ListTile(title: Text(item['pieceType']?.toString() ?? '-'), subtitle: Text('الكمية: ${item['quantity'] ?? 0}  •  القماش: ${item['fabricType'] ?? '-'}')))),
						const SizedBox(height: 24),
						Text('القطع', style: Theme.of(context).textTheme.titleLarge),
						const SizedBox(height: 8),
						if (data.pieces.isEmpty) const Text('لا توجد قطع.') else ...data.pieces.map((piece) => Card(child: ListTile(leading: const Icon(Icons.content_cut), title: Text('قطعة ${piece['pieceNumber'] ?? '-'}'), subtitle: Text('الحالة: ${piece['pieceStatus'] ?? '-'}  •  التتبع: ${piece['trackingCode'] ?? '-'}'), trailing: FilledButton.icon(icon: const Icon(Icons.visibility_outlined), label: const Text('معاينة البطاقة'), onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => WorkCardPreviewScreen(pieceId: piece['pieceId'] as int))))))),
					],
				);
			},
		),
	);
}

class _OrderDetailsData {
	const _OrderDetailsData({required this.order, required this.items, required this.pieces});
	final Map<String, dynamic> order;
	final List<Map<String, dynamic>> items;
	final List<Map<String, dynamic>> pieces;
}

class _Detail extends StatelessWidget {
	const _Detail(this.label, this.value);
	final String label;
	final Object? value;
	@override
	Widget build(BuildContext context) => SizedBox(width: 220, child: Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label, style: Theme.of(context).textTheme.labelLarge), const SizedBox(height: 8), Text(value?.toString() ?? '-', style: Theme.of(context).textTheme.titleMedium)]))));
}