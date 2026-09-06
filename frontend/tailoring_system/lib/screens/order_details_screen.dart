import 'package:flutter/material.dart';

import 'order_details_content.dart';

class OrderDetailsScreen extends StatefulWidget {
  const OrderDetailsScreen({required this.orderId, super.key});
  final int orderId;

  @override
  State<OrderDetailsScreen> createState() => _OrderDetailsScreenState();
}

class _OrderDetailsScreenState extends State<OrderDetailsScreen> {
  @override
  Widget build(BuildContext context) =>
      OrderDetailsContent(orderId: widget.orderId);
}
