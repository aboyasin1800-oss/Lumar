import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../models/order_models.dart';
import '../../models/payroll_models.dart';
import '../../repositories/order_repository.dart';
import '../../repositories/payroll_repository.dart';

final _productionMoney = NumberFormat('#,##0.00');

class EmployeeProductionScreen extends StatefulWidget {
  const EmployeeProductionScreen({
    super.key,
    this.repository,
    this.orderRepository,
  });

  final PayrollRepository? repository;
  final OrderRepository? orderRepository;

  @override
  State<EmployeeProductionScreen> createState() => _EmployeeProductionScreenState();
}

class _EmployeeProductionScreenState extends State<EmployeeProductionScreen> {
  late final PayrollRepository _repository;
  late final OrderRepository _orderRepository;
  late Future<List<_ProductionRow>> _future;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? PayrollRepository();
    _orderRepository = widget.orderRepository ??
        OrderRepository(client: _repository.client);
    _future = _load();
  }

  Future<List<_ProductionRow>> _load() async {
    final wages = await _repository.getPieceWages();
    if (wages.isEmpty) return const [];

    final enriched = await Future.wait(wages.map(_hydrateRow));
    final grouped = <String, _ProductionRow>{};

    for (final row in enriched) {
      if (row.totalWage <= 0 && row.quantity <= 0) continue;
      final existing = grouped[row.employeeCode];
      if (existing == null) {
        grouped[row.employeeCode] = row;
        continue;
      }

      grouped[row.employeeCode] = _ProductionRow(
        employeeId: row.employeeId,
        employeeCode: row.employeeCode,
        employeeName: row.employeeName,
        customerName: existing.customerName.isNotEmpty ? existing.customerName : row.customerName,
        orderNumber: existing.orderNumber.isNotEmpty ? existing.orderNumber : row.orderNumber,
        trackingCode: existing.trackingCode.isNotEmpty ? existing.trackingCode : row.trackingCode,
        pieceType: row.pieceType,
        stage: row.stage,
        quantity: existing.quantity + row.quantity,
        totalWage: existing.totalWage + row.totalWage,
        totalDraws: existing.totalDraws + row.totalDraws,
        totalSettlements: existing.totalSettlements + row.totalSettlements,
        netDue: existing.netDue + row.netDue,
        records: existing.records + 1,
      );
    }

    final rows = grouped.values.toList();
    rows.sort((a, b) => b.netDue.compareTo(a.netDue));
    return rows;
  }

  Future<_ProductionRow> _hydrateRow(PieceWageRecord wage) async {
    final employeeId = wage.employeeId;
    final employeeCode = wage.employeeCode ?? 'EMP-${employeeId ?? 'غير محدد'}';
    final orderContext = wage.orderId > 0 ? await _resolveOrderContext(wage.orderId, wage.pieceId) : const _OrderContext();
    final drawTotal = employeeId == null
        ? 0.0
        : (await _repository.getDraws(employeeId))
            .fold<double>(0, (sum, draw) => sum + (draw.amount ?? 0));
    final settlementTotal = employeeId == null
        ? 0.0
        : (await _repository.getSettlements(employeeId))
            .fold<double>(0, (sum, item) => sum + item.amount);

    return _ProductionRow(
      employeeId: employeeId ?? 0,
      employeeCode: employeeCode,
      employeeName: wage.employeeCode ?? 'موظف غير محدد',
      customerName: orderContext.customerName,
      orderNumber: orderContext.orderNumber,
      trackingCode: orderContext.trackingCode,
      pieceType: wage.pieceType,
      stage: wage.stage,
      quantity: wage.quantity,
      totalWage: wage.totalWage,
      totalDraws: drawTotal,
      totalSettlements: settlementTotal,
      netDue: wage.totalWage - drawTotal - settlementTotal,
      records: 1,
    );
  }

  Future<_OrderContext> _resolveOrderContext(int orderId, int pieceId) async {
    try {
      final details = await _orderRepository.getDetails(orderId);
      final piece = details.pieces.isEmpty
          ? null
          : details.pieces.firstWhere(
              (item) => item.id == pieceId,
              orElse: () => details.pieces.first,
            );
      return _OrderContext(
        orderNumber: details.order.number,
        customerName: details.customer.name ?? details.customer.code ?? 'غير محدد',
        trackingCode: piece?.trackingCode ?? (pieceId > 0 ? 'PT-$pieceId' : '—'),
      );
    } catch (_) {
      return _OrderContext(
        orderNumber: orderId > 0 ? 'ORD-$orderId' : '—',
        customerName: 'غير محدد',
        trackingCode: pieceId > 0 ? 'PT-$pieceId' : '—',
      );
    }
  }

  void _reload() => setState(() => _future = _load());

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('كشف حساب الإنتاج'),
          actions: [
            IconButton(
              tooltip: 'تحديث',
              onPressed: _reload,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: FutureBuilder<List<_ProductionRow>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.cloud_off_outlined, size: 42),
                    const SizedBox(height: 12),
                    Text('تعذر تحميل كشف حساب الإنتاج:\n${snapshot.error}'),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _reload,
                      icon: const Icon(Icons.refresh),
                      label: const Text('إعادة المحاولة'),
                    ),
                  ],
                ),
              );
            }

            final rows = snapshot.data ?? const <_ProductionRow>[];
            final totalWage = rows.fold<double>(0, (sum, row) => sum + row.totalWage);
            final totalNet = rows.fold<double>(0, (sum, row) => sum + row.netDue);

            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('بطاقة كشف حساب الإنتاج', style: TextStyle(fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            Expanded(
                              child: Text('إجمالي الأجر: ${_productionMoney.format(totalWage)} ر.س'),
                            ),
                            Expanded(
                              child: Text('صافي المستحق: ${_productionMoney.format(totalNet)} ر.س'),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text('${rows.length} موظف مع سجلات إنتاج فعّالة'),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                if (rows.isEmpty)
                  const Center(child: Text('لا توجد سجلات إنتاج مرتبطة بالموظفين.'))
                else
                  ...rows.map(
                    (row) => Card(
                      margin: const EdgeInsets.only(bottom: 8),
                      child: ListTile(
                        leading: const Icon(Icons.precision_manufacturing_outlined),
                        title: Text(row.employeeCode),
                        subtitle: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('العميل: ${row.customerName}'),
                            Row(
                              children: [
                                const Text('الطلب: '),
                                Text(row.orderNumber),
                              ],
                            ),
                            Row(
                              children: [
                                const Text('TrackingCode: '),
                                Text(row.trackingCode),
                              ],
                            ),
                            Text('الكمية: ${row.quantity.toStringAsFixed(2)}'),
                            Text('الأجر: ${_productionMoney.format(row.totalWage)}'),
                            Text('السلف: ${_productionMoney.format(row.totalDraws)}'),
                            Text('صافي المستحق: ${_productionMoney.format(row.netDue)}'),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
      );
}

class _OrderContext {
  const _OrderContext({this.orderNumber = '-', this.customerName = 'غير محدد', this.trackingCode = '—'});

  final String orderNumber;
  final String customerName;
  final String trackingCode;
}

class _ProductionRow {
  const _ProductionRow({
    required this.employeeId,
    required this.employeeCode,
    required this.employeeName,
    required this.customerName,
    required this.orderNumber,
    required this.trackingCode,
    required this.pieceType,
    required this.stage,
    required this.quantity,
    required this.totalWage,
    required this.totalDraws,
    required this.totalSettlements,
    required this.netDue,
    required this.records,
  });

  final int employeeId;
  final String employeeCode;
  final String employeeName;
  final String customerName;
  final String orderNumber;
  final String trackingCode;
  final String pieceType;
  final String stage;
  final double quantity;
  final double totalWage;
  final double totalDraws;
  final double totalSettlements;
  final double netDue;
  final int records;
}
