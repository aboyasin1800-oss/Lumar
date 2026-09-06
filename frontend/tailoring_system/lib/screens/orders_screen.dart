import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/app_navigation.dart';
import 'order_details_screen.dart';

class OrdersScreen extends StatefulWidget {
	const OrdersScreen({super.key});

	@override
	State<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends State<OrdersScreen> with SingleTickerProviderStateMixin {
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5093');
	late final TabController _tabController;
	late Future<List<OrderSummary>> _orders;

	static const _tabs = <OrderTab>[
		OrderTab('الطلبات الجديدة', 'New', Icons.fiber_new_outlined),
		OrderTab('قيد الإنتاج', 'InProduction', Icons.precision_manufacturing_outlined),
		OrderTab('جاهزة للتسليم', 'ReadyForDelivery', Icons.inventory_2_outlined),
		OrderTab('تم التسليم', 'Delivered', Icons.task_alt_outlined),
	];

	@override
	void initState() {
		super.initState();
		_tabController = TabController(length: _tabs.length, vsync: this);
		_orders = _loadOrders();
	}

	@override
	void dispose() {
		_tabController.dispose();
		super.dispose();
	}

	Future<List<OrderSummary>> _loadOrders() async {
		final response = await http.get(Uri.parse('$_baseUrl/orders'));
		if (response.statusCode < 200 || response.statusCode >= 300) {
			throw Exception('تعذر تحميل الطلبات.');
		}
		final decoded = jsonDecode(response.body) as List;
		return decoded.cast<Map<String, dynamic>>().map(OrderSummary.fromJson).toList();
	}

	Future<void> _refresh() async {
		setState(() => _orders = _loadOrders());
		await _orders;
	}

	void _openDetails(OrderSummary order) {
		AppNavigation.push(context, (_) => OrderDetailsScreen(orderId: order.orderId));
	}

	Future<void> _showDelivery(OrderSummary order) async {
		try {
			final response = await http.get(Uri.parse('$_baseUrl/orders/${order.orderId}/delivery'));
			if (response.statusCode < 200 || response.statusCode >= 300) throw Exception();
			final delivery = jsonDecode(response.body) as Map<String, dynamic>;
			if (!mounted) return;
			showModalBottomSheet<void>(
				context: context,
				showDragHandle: true,
				builder: (context) => Padding(
					padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
					child: Column(
						mainAxisSize: MainAxisSize.min,
						crossAxisAlignment: CrossAxisAlignment.stretch,
						children: [
							Text('تسليم الطلب ${order.orderNumber}', style: Theme.of(context).textTheme.titleLarge),
							const SizedBox(height: 16),
							ListTile(
								leading: const Icon(Icons.local_shipping_outlined),
								title: Text(_statusLabel(delivery['orderStatus']?.toString() ?? '')),
								subtitle: Text(_dateText(delivery['deliveryDate']?.toString())),
							),
							const Text('تنفيذ التسليم غير متاح حاليًا لأن API يسمح بقراءة حالة التسليم فقط.'),
						],
					),
				),
			);
		} catch (_) {
			if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تعذر تحميل حالة التسليم.')));
		}
	}

	@override
	Widget build(BuildContext context) => Column(
		crossAxisAlignment: CrossAxisAlignment.stretch,
		children: [
			Row(children: [
				Expanded(child: Text('الطلبات', style: Theme.of(context).textTheme.headlineSmall)),
				IconButton(tooltip: 'تحديث', onPressed: _refresh, icon: const Icon(Icons.refresh)),
			]),
			const SizedBox(height: 12),
			TabBar(
				controller: _tabController,
				isScrollable: true,
				tabs: _tabs.map((tab) => Tab(icon: Icon(tab.icon), text: tab.title)).toList(),
			),
			const SizedBox(height: 12),
			Expanded(
				child: FutureBuilder<List<OrderSummary>>(
					future: _orders,
					builder: (context, snapshot) {
						if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
						if (snapshot.hasError) return _OrdersError(onRetry: _refresh);
						final orders = snapshot.data ?? const [];
						return TabBarView(
							controller: _tabController,
							children: _tabs.map((tab) {
								final filtered = orders.where((order) => order.status == tab.status).toList();
								return _OrderList(
									orders: filtered,
									onRefresh: _refresh,
									onOpen: _openDetails,
									onDelivery: tab.status == 'ReadyForDelivery' ? _showDelivery : null,
								);
							}).toList(),
						);
					},
				),
			),
		],
	);

	static String _dateText(String? value) {
		final date = DateTime.tryParse(value ?? '');
		return date == null ? 'لم يحدد تاريخ التسليم' : '${date.year}/${date.month.toString().padLeft(2, '0')}/${date.day.toString().padLeft(2, '0')}';
	}

	static String _statusLabel(String status) => switch (status) {
		'New' => 'طلب جديد',
		'InProduction' => 'قيد الإنتاج',
		'ReadyForDelivery' => 'جاهز للتسليم',
		'Delivered' => 'تم التسليم',
		_ => status,
	};
}

class OrderSummary {
	const OrderSummary({required this.orderId, required this.orderNumber, required this.customerId, required this.orderDate, required this.deliveryDate, required this.totalAmount, required this.paidAmount, required this.remainingAmount, required this.status, required this.urgencyStatus});

	factory OrderSummary.fromJson(Map<String, dynamic> json) => OrderSummary(
		orderId: json['orderId'] as int? ?? 0,
		orderNumber: json['orderNumber']?.toString() ?? '-',
		customerId: json['customerId'] as int? ?? 0,
		orderDate: DateTime.tryParse(json['orderDate']?.toString() ?? ''),
		deliveryDate: DateTime.tryParse(json['deliveryDate']?.toString() ?? ''),
		totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0,
		paidAmount: (json['paidAmount'] as num?)?.toDouble() ?? 0,
		remainingAmount: (json['remainingAmount'] as num?)?.toDouble() ?? 0,
		status: json['orderStatus']?.toString() ?? '',
		urgencyStatus: json['urgencyStatus']?.toString() ?? '',
	);

	final int orderId;
	final String orderNumber;
	final int customerId;
	final DateTime? orderDate;
	final DateTime? deliveryDate;
	final double totalAmount;
	final double paidAmount;
	final double remainingAmount;
	final String status;
	final String urgencyStatus;
}

class OrderTab {
	const OrderTab(this.title, this.status, this.icon);
	final String title;
	final String status;
	final IconData icon;
}

class _OrderList extends StatelessWidget {
	const _OrderList({required this.orders, required this.onRefresh, required this.onOpen, this.onDelivery});
	final List<OrderSummary> orders;
	final Future<void> Function() onRefresh;
	final ValueChanged<OrderSummary> onOpen;
	final ValueChanged<OrderSummary>? onDelivery;

	@override
	Widget build(BuildContext context) => RefreshIndicator(
		onRefresh: onRefresh,
		child: orders.isEmpty
				? ListView(children: const [SizedBox(height: 180), Center(child: Text('لا توجد طلبات في هذا التبويب.'))])
				: ListView.separated(
					itemCount: orders.length,
					separatorBuilder: (_, __) => const SizedBox(height: 8),
					itemBuilder: (context, index) {
						final order = orders[index];
						return Card(
							child: ListTile(
								onTap: () => onOpen(order),
								leading: CircleAvatar(child: Text('${order.orderId}')),
								title: Text('الطلب ${order.orderNumber}'),
								subtitle: Text('العميل: ${order.customerId}  •  الإجمالي: ${order.totalAmount.toStringAsFixed(2)}  •  المتبقي: ${order.remainingAmount.toStringAsFixed(2)}'),
								trailing: onDelivery == null
										? const Icon(Icons.chevron_left)
										: FilledButton.icon(onPressed: () => onDelivery!(order), icon: const Icon(Icons.local_shipping_outlined), label: const Text('التسليم')),
							),
						);
					},
				),
	);
}

class _OrdersError extends StatelessWidget {
	const _OrdersError({required this.onRetry});
	final VoidCallback onRetry;
	@override
	Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Text('تعذر تحميل الطلبات.'), const SizedBox(height: 12), FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة'))]));
}