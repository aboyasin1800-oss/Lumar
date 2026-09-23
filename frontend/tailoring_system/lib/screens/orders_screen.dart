import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../core/app_navigation.dart';
import 'order_details_screen.dart';

class OrdersScreen extends StatefulWidget {
  const OrdersScreen({super.key});

  static const List<OrderTab> orderTabs = <OrderTab>[
    OrderTab('الطلبات الجديدة', 'New', Icons.fiber_new_outlined),
    OrderTab(
        'قيد الإنتاج', 'InProduction', Icons.precision_manufacturing_outlined),
    OrderTab('جاهزة للتسليم', 'ReadyForDelivery', Icons.inventory_2_outlined),
    OrderTab('تم التسليم', 'Delivered', Icons.task_alt_outlined),
    OrderTab('ملغاة', 'Cancelled', Icons.cancel_outlined),
  ];

  static List<OrderSummary> filterOrdersForTab(
          List<OrderSummary> orders, String status) =>
      orders.where((order) => order.status == status).toList();

  @override
  State<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends State<OrdersScreen>
    with SingleTickerProviderStateMixin {
  static const _baseUrl = String.fromEnvironment('LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093');
  late final TabController _tabController;
  late Future<List<OrderSummary>> _orders;
  final _searchController = TextEditingController();
  Timer? _searchTimer;
  Future<List<_CustomerSuggestion>>? _suggestions;
  int? _selectedCustomerId;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(
        length: OrdersScreen.orderTabs.length, vsync: this);
    _orders = _loadOrders();
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _searchController.dispose();
    _tabController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _searchTimer?.cancel();
    setState(() {
      _selectedCustomerId = null;
      _suggestions = null;
    });
    if (value.trim().length < 2) return;

    _searchTimer = Timer(const Duration(milliseconds: 250), () {
      if (!mounted) return;
      setState(() => _suggestions = _searchCustomers(value.trim()));
    });
  }

  Future<List<_CustomerSuggestion>> _searchCustomers(String term) async {
    final uri = Uri.parse('$_baseUrl/customers/search')
        .replace(queryParameters: {'term': term});
    final response = await http.get(uri);
    if (response.statusCode < 200 || response.statusCode >= 300) return const [];
    final decoded = jsonDecode(response.body);
    if (decoded is! List) return const [];
    return decoded
        .whereType<Map<String, dynamic>>()
        .map(_CustomerSuggestion.fromJson)
        .toList();
  }

  void _selectCustomer(_CustomerSuggestion customer) {
    _searchTimer?.cancel();
    _searchController.text = customer.customerName ?? customer.customerCode ?? '';
    setState(() {
      _selectedCustomerId = customer.customerId;
      _suggestions = null;
    });
  }

  List<OrderSummary> _filterOrders(List<OrderSummary> orders) {
    final customerId = _selectedCustomerId;
    if (customerId != null) {
      return orders.where((order) => order.customerId == customerId).toList();
    }
    final query = _searchController.text.trim().toLowerCase();
    if (query.isEmpty) return orders;
    return orders.where((order) {
      return order.customerName.toLowerCase().contains(query) ||
          order.phoneNumber.toLowerCase().contains(query) ||
          order.customerCode.toLowerCase().contains(query);
    }).toList();
  }

  Future<List<OrderSummary>> _loadOrders() async {
    final response = await http.get(Uri.parse('$_baseUrl/orders'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تحميل الطلبات.');
    }
    final decoded = jsonDecode(response.body) as List;
    return decoded
        .cast<Map<String, dynamic>>()
        .map(OrderSummary.fromJson)
        .toList();
  }

  Future<void> _refresh() async {
    setState(() => _orders = _loadOrders());
    await _orders;
  }

  void _openDetails(OrderSummary order) {
    AppNavigation.push(
        context, (_) => OrderDetailsScreen(orderId: order.orderId));
  }

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(children: [
            Expanded(
                child: Text('الطلبات',
                    style: Theme.of(context).textTheme.headlineSmall)),
            IconButton(
                tooltip: 'تحديث',
                onPressed: _refresh,
                icon: const Icon(Icons.refresh)),
          ]),
          const SizedBox(height: 12),
          _buildSearch(),
          const SizedBox(height: 12),
          TabBar(
            controller: _tabController,
            isScrollable: true,
            tabs: OrdersScreen.orderTabs
                .map((tab) => Tab(icon: Icon(tab.icon), text: tab.title))
                .toList(),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: FutureBuilder<List<OrderSummary>>(
              future: _orders,
              builder: (context, snapshot) {
                if (snapshot.connectionState != ConnectionState.done)
                  return const Center(child: CircularProgressIndicator());
                if (snapshot.hasError) return _OrdersError(onRetry: _refresh);
                final orders = _filterOrders(snapshot.data ?? const []);
                return TabBarView(
                  controller: _tabController,
                  children: OrdersScreen.orderTabs.map((tab) {
                    final filtered = OrdersScreen.filterOrdersForTab(orders, tab.status);
                    return _OrderList(
                      orders: filtered,
                      onRefresh: _refresh,
                      onOpen: _openDetails,
                      onDelivery: null,
                    );
                  }).toList(),
                );
              },
            ),
          ),
        ],
      );

  Widget _buildSearch() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _searchController,
            onChanged: _onSearchChanged,
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              labelText: 'بحث باسم العميل أو رقم الهاتف أو كود العميل',
              hintText: 'اكتب اسم العميل أو الهاتف أو الكود',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _searchController.text.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'مسح البحث',
                      onPressed: () {
                        _searchController.clear();
                        _onSearchChanged('');
                      },
                      icon: const Icon(Icons.clear),
                    ),
            ),
          ),
          if (_suggestions != null)
            _CustomerSuggestions(
              future: _suggestions!,
              onSelected: _selectCustomer,
            ),
        ],
      );

  static String _dateText(String? value) {
    final date = DateTime.tryParse(value ?? '');
    return date == null
        ? 'لم يحدد تاريخ التسليم'
        : '${date.year}/${date.month.toString().padLeft(2, '0')}/${date.day.toString().padLeft(2, '0')}';
  }

  static String _statusLabel(String status) => switch (status) {
        'New' => 'طلب جديد',
        'InProduction' => 'قيد الإنتاج',
        'ReadyForDelivery' => 'جاهز للتسليم',
        'Delivered' => 'تم التسليم',
        'Cancelled' => 'ملغاة',
        _ => status,
      };
}

class OrderSummary {
  const OrderSummary(
      {required this.orderId,
      required this.orderNumber,
      required this.customerId,
      this.customerCode = '',
      this.customerName = '',
      this.phoneNumber = '',
      required this.orderDate,
      required this.deliveryDate,
      required this.totalAmount,
      required this.paidAmount,
      required this.remainingAmount,
      required this.status,
      required this.urgencyStatus});

  factory OrderSummary.fromJson(Map<String, dynamic> json) => OrderSummary(
        orderId: json['orderId'] as int? ?? 0,
        orderNumber: json['orderNumber']?.toString() ?? '-',
        customerId: json['customerId'] as int? ?? 0,
        customerCode: json['customerCode']?.toString() ?? '',
        customerName: json['customerName']?.toString() ?? '',
        phoneNumber: json['phoneNumber']?.toString() ?? '',
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
  final String customerCode;
  final String customerName;
  final String phoneNumber;
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

class _CustomerSuggestion {
  const _CustomerSuggestion({
    required this.customerId,
    required this.customerCode,
    required this.customerName,
    required this.phoneNumber,
  });

  factory _CustomerSuggestion.fromJson(Map<String, dynamic> json) =>
      _CustomerSuggestion(
        customerId: (json['customerId'] as num?)?.toInt() ?? 0,
        customerCode: json['customerCode']?.toString(),
        customerName: json['customerName']?.toString(),
        phoneNumber: json['phoneNumber']?.toString(),
      );

  final int customerId;
  final String? customerCode;
  final String? customerName;
  final String? phoneNumber;
}

class _CustomerSuggestions extends StatelessWidget {
  const _CustomerSuggestions({required this.future, required this.onSelected});

  final Future<List<_CustomerSuggestion>> future;
  final ValueChanged<_CustomerSuggestion> onSelected;

  @override
  Widget build(BuildContext context) => Card(
        child: FutureBuilder<List<_CustomerSuggestion>>(
          future: future,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Padding(
                padding: EdgeInsets.all(12),
                child: LinearProgressIndicator(),
              );
            }
            final customers = snapshot.data ?? const <_CustomerSuggestion>[];
            if (customers.isEmpty) {
              return const ListTile(title: Text('لا توجد اقتراحات مطابقة.'));
            }
            return Column(
              children: customers
                  .map((customer) => ListTile(
                        dense: true,
                        leading: const Icon(Icons.person_search_outlined),
                        title: Text(customer.customerName ?? 'عميل ${customer.customerId}'),
                        subtitle: Text([
                          customer.customerCode,
                          customer.phoneNumber,
                        ].where((value) => value?.trim().isNotEmpty == true).join(' • ')),
                        onTap: () => onSelected(customer),
                      ))
                  .toList(),
            );
          },
        ),
      );
}

class _OrderList extends StatelessWidget {
  const _OrderList(
      {required this.orders,
      required this.onRefresh,
      required this.onOpen,
      this.onDelivery});
  final List<OrderSummary> orders;
  final Future<void> Function() onRefresh;
  final ValueChanged<OrderSummary> onOpen;
  final ValueChanged<OrderSummary>? onDelivery;

  @override
  Widget build(BuildContext context) => RefreshIndicator(
        onRefresh: onRefresh,
        child: orders.isEmpty
            ? ListView(children: const [
                SizedBox(height: 180),
                Center(child: Text('لا توجد طلبات في هذا التبويب.'))
              ])
            : ListView.separated(
                itemCount: orders.length,
                separatorBuilder: (_, __) => const SizedBox(height: 8),
                itemBuilder: (context, index) {
                  final order = orders[index];
                  return Card(
                    child: ListTile(
                      onTap: onDelivery == null
                          ? () => onOpen(order)
                          : () => onDelivery!(order),
                      leading: CircleAvatar(child: Text('${order.orderId}')),
                      title: Text('الطلب ${order.orderNumber}'),
                      subtitle: Text(
                          'العميل: ${order.customerName.isEmpty ? order.customerId : order.customerName}  •  الإجمالي: ${order.totalAmount.toStringAsFixed(2)}  •  المتبقي: ${order.remainingAmount.toStringAsFixed(2)}'),
                      trailing: onDelivery == null
                          ? const Icon(Icons.chevron_left)
                          : FilledButton.icon(
                              onPressed: () => onDelivery!(order),
                              icon: const Icon(Icons.local_shipping_outlined),
                              label: const Text('التسليم')),
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
  Widget build(BuildContext context) => Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Text('تعذر تحميل الطلبات.'),
        const SizedBox(height: 12),
        FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('إعادة المحاولة'))
      ]));
}
