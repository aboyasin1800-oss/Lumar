import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';
import 'order_delivery_screen.dart';

class DeliveryDashboardScreen extends StatefulWidget {
  const DeliveryDashboardScreen({super.key});

  @override
  State<DeliveryDashboardScreen> createState() =>
      _DeliveryDashboardScreenState();
}

class _DeliveryDashboardScreenState extends State<DeliveryDashboardScreen>
    with SingleTickerProviderStateMixin {
  final TextEditingController _searchController = TextEditingController();
  late final TabController _tabController;
  late Future<List<_DeliveryOrder>> _readyOrdersFuture;
  late Future<List<_DeliveryOrder>> _deliveredOrdersFuture;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _readyOrdersFuture = _loadOrdersByStatus('ReadyForDelivery');
    _deliveredOrdersFuture = _loadOrdersByStatus('Delivered');
  }

  @override
  void dispose() {
    _searchController.dispose();
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    if (!mounted) return;
    setState(() {
      _readyOrdersFuture = _loadOrdersByStatus('ReadyForDelivery');
      _deliveredOrdersFuture = _loadOrdersByStatus('Delivered');
    });
  }

  Future<List<_DeliveryOrder>> _loadOrdersByStatus(String status) async {
    const baseUrl = String.fromEnvironment(
      'LUMAR_API_URL',
      defaultValue: 'http://127.0.0.1:5093',
    );

    final response = await http.get(Uri.parse('$baseUrl/orders'));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('تعذر تحميل الطلبات.');
    }

    final ordersJson = jsonDecode(response.body) as List<dynamic>;
    final filtered = ordersJson.where((item) {
      final map = item as Map<String, dynamic>;
      final current = (map['orderStatus'] ?? '').toString();
      return _matchesStatus(current, status);
    }).toList();

    final result = <_DeliveryOrder>[];
    for (final item in filtered) {
      final map = item as Map<String, dynamic>;
      final customerId = (map['customerId'] as num?)?.toInt() ?? 0;
      String customerName = '';
      String phoneNumber = '';

      if (customerId > 0) {
        final customerResponse =
            await http.get(Uri.parse('$baseUrl/customers/$customerId'));
        if (customerResponse.statusCode >= 200 &&
            customerResponse.statusCode < 300) {
          final customerMap =
              jsonDecode(customerResponse.body) as Map<String, dynamic>;
          customerName = (customerMap['customerName'] ?? '').toString();
          phoneNumber = (customerMap['phoneNumber'] ?? '').toString();
        }
      }

      result.add(_DeliveryOrder(
        orderId: (map['orderId'] as num?)?.toInt() ?? 0,
        orderNumber: (map['orderNumber'] ?? '').toString(),
        customerName: customerName,
        phoneNumber: phoneNumber,
        status: (map['orderStatus'] ?? '').toString(),
        deliveryDate: map['deliveryDate']?.toString(),
      ));
    }

    return result;
  }

  static bool _matchesStatus(String incoming, String target) {
    final current = incoming.trim();
    final normalized = current.toLowerCase();
    switch (target) {
      case 'ReadyForDelivery':
        return normalized == 'readyfordelivery' ||
            normalized == 'ready for delivery' ||
            normalized == 'ready' ||
            normalized == 'جاهز للتسليم';
      case 'Delivered':
        return normalized == 'delivered' || normalized == 'تم التسليم';
      default:
        return false;
    }
  }

  List<_DeliveryOrder> _filteredOrders(List<_DeliveryOrder> orders) {
    final query = _searchController.text.trim().toLowerCase();
    if (query.isEmpty) return orders;

    return orders.where((order) {
      final customer = order.customerName.toLowerCase();
      final phone = order.phoneNumber.toLowerCase();
      final orderNumber = order.orderNumber.toLowerCase();
      return customer.contains(query) ||
          phone.contains(query) ||
          orderNumber.contains(query);
    }).toList();
  }

  Widget _buildOrdersList({
    required Future<List<_DeliveryOrder>> future,
    required String emptyText,
    required bool isArchive,
  }) {
    return FutureBuilder<List<_DeliveryOrder>>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }

        if (snapshot.hasError) {
          return Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline_rounded,
                    size: 48, color: UiPalette.primaryBlue),
                const SizedBox(height: 12),
                Text(
                  'تعذر تحميل الطلبات.',
                  style: UiPalette.adaptiveTextStyle(
                    context,
                    backgroundColor: UiPalette.screenBackground,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: _refresh,
                  icon: const Icon(Icons.refresh_rounded),
                  label: const Text('إعادة المحاولة'),
                ),
              ],
            ),
          );
        }

        final orders = _filteredOrders(snapshot.data ?? const []);
        if (orders.isEmpty) {
          return _EmptyDeliveryState(
            onRefresh: _refresh,
            query: _searchController.text.trim(),
            message: emptyText,
          );
        }

        return RefreshIndicator(
          onRefresh: _refresh,
          child: ListView.separated(
            itemCount: orders.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (context, index) {
              final order = orders[index];
              return Material(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(16),
                child: InkWell(
                  onTap: () async {
                    await AppNavigation.push(
                      context,
                      (_) => OrderDeliveryScreen(orderId: order.orderId),
                    );
                    if (mounted) _refresh();
                  },
                  borderRadius: BorderRadius.circular(16),
                  child: Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: UiPalette.borderSoft),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.center,
                      children: [
                        Container(
                          width: 52,
                          height: 52,
                          decoration: BoxDecoration(
                            color: isArchive
                                ? UiPalette.primaryDark.withValues(alpha: 0.12)
                                : UiPalette.primaryBlue.withValues(alpha: 0.14),
                            borderRadius: BorderRadius.circular(14),
                          ),
                          child: Icon(
                            isArchive
                                ? Icons.archive_outlined
                                : Icons.local_shipping_outlined,
                            color: isArchive
                                ? UiPalette.primaryDark
                                : UiPalette.primaryBlue,
                          ),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'رقم الطلب: ${order.orderNumber}',
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 16,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                'العميل: ${order.customerName.isEmpty ? 'غير محدد' : order.customerName}',
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 13,
                                ).copyWith(color: UiPalette.textSoft),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                'الهاتف: ${order.phoneNumber.isEmpty ? 'غير محدد' : order.phoneNumber}',
                                style: UiPalette.adaptiveTextStyle(
                                  context,
                                  backgroundColor: UiPalette.surfaceCard,
                                  fontSize: 13,
                                ).copyWith(color: UiPalette.textSoft),
                              ),
                              if (order.deliveryDate != null) ...[
                                const SizedBox(height: 4),
                                Text(
                                  'تاريخ التسليم: ${order.deliveryDate!}',
                                  style: UiPalette.adaptiveTextStyle(
                                    context,
                                    backgroundColor: UiPalette.surfaceCard,
                                    fontSize: 12,
                                  ).copyWith(color: UiPalette.textSoft),
                                ),
                              ],
                            ],
                          ),
                        ),
                        const SizedBox(width: 12),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 8),
                          decoration: BoxDecoration(
                            color: isArchive
                                ? UiPalette.softBlue
                                : UiPalette.softBlue,
                            borderRadius: BorderRadius.circular(999),
                          ),
                          child: Text(
                            isArchive ? 'مسلم' : 'جاهز للتسليم',
                            style: UiPalette.adaptiveTextStyle(
                              context,
                              backgroundColor: UiPalette.softBlue,
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.screenBackground,
        foregroundColor:
            UiPalette.adaptiveTextColor(UiPalette.screenBackground),
        title: const Text('شاشة التسليم'),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(18),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(
                  color: UiPalette.surfaceCard,
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(color: UiPalette.borderSoft),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'لوحة تسليم الطلبات',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.surfaceCard,
                        fontSize: 26,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'ابحث باسم العميل أو الهاتف أو رقم الطلب، ثم اضغط على أي طلب لفتح بطاقة التسليم مباشرة.',
                      style: UiPalette.adaptiveTextStyle(
                        context,
                        backgroundColor: UiPalette.surfaceCard,
                        fontSize: 13,
                      ).copyWith(color: UiPalette.textSoft),
                    ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: _searchController,
                      onChanged: (_) => setState(() {}),
                      textDirection: TextDirection.rtl,
                      decoration: InputDecoration(
                        prefixIcon: const Icon(Icons.search_rounded),
                        hintText: 'ابحث بالاسم، الهاتف أو رقم الطلب',
                        filled: true,
                        fillColor: UiPalette.softBlue,
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: BorderSide(color: UiPalette.borderSoft),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Container(
                decoration: BoxDecoration(
                  color: UiPalette.surfaceCard,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: UiPalette.borderSoft),
                ),
                child: TabBar(
                  controller: _tabController,
                  labelColor: UiPalette.primaryBlue,
                  unselectedLabelColor: UiPalette.textSoft,
                  indicatorColor: UiPalette.primaryBlue,
                  tabs: const [
                    Tab(text: 'جاهز للتسليم'),
                    Tab(text: 'أرشيف الطلبات المسلمة'),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Expanded(
                child: TabBarView(
                  controller: _tabController,
                  children: [
                    _buildOrdersList(
                      future: _readyOrdersFuture,
                      emptyText: 'لا توجد طلبات جاهزة للتسليم حاليًا.',
                      isArchive: false,
                    ),
                    _buildOrdersList(
                      future: _deliveredOrdersFuture,
                      emptyText: 'لا توجد طلبات مسلمة في الأرشيف.',
                      isArchive: true,
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DeliveryOrder {
  const _DeliveryOrder({
    required this.orderId,
    required this.orderNumber,
    required this.customerName,
    required this.phoneNumber,
    required this.status,
    required this.deliveryDate,
  });

  final int orderId;
  final String orderNumber;
  final String customerName;
  final String phoneNumber;
  final String status;
  final String? deliveryDate;
}

class _EmptyDeliveryState extends StatelessWidget {
  const _EmptyDeliveryState({
    required this.onRefresh,
    required this.query,
    required this.message,
  });

  final Future<void> Function() onRefresh;
  final String query;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.search_off_rounded,
                size: 52, color: UiPalette.primaryBlue),
            const SizedBox(height: 14),
            Text(
              query.isEmpty ? message : 'لا يوجد طلب يطابق بحثك.',
              textAlign: TextAlign.center,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRefresh,
              icon: const Icon(Icons.refresh_rounded),
              label: const Text('تحديث القائمة'),
            ),
          ],
        ),
      ),
    );
  }
}
