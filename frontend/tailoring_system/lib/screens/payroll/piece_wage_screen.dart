import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../models/order_models.dart';
import '../../models/payroll_models.dart';
import '../../repositories/order_repository.dart';
import '../../repositories/payroll_repository.dart';

class PieceWageScreen extends StatefulWidget {
  const PieceWageScreen({
    super.key,
    this.repository,
    this.orderRepository,
  });

  final PayrollRepository? repository;
  final OrderRepository? orderRepository;

  @override
  State<PieceWageScreen> createState() => _PieceWageScreenState();
}

class _PieceWageScreenState extends State<PieceWageScreen> {
  late final PayrollRepository _repository;
  late final OrderRepository _orderRepository;
  late Future<_PieceWageData> _future;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? PayrollRepository();
    _orderRepository = widget.orderRepository ??
        OrderRepository(client: _repository.client);
    _future = _load();
  }

  Future<_PieceWageData> _load() async {
    final results = await Future.wait([
      _repository.getPieceWages(),
      _repository.getPieceWageRates(),
    ]);
    final wages = (results[0] as List<PieceWageRecord>)
        .where((item) => item.totalWage > 0 || item.quantity > 0)
        .toList();
    final rates = (results[1] as List<PieceWageRate>)
        .where((item) => item.isActive)
        .toList();

    final hydrated = await Future.wait(wages.map(_hydrateRow));
    return _PieceWageData(wages: hydrated, rates: rates);
  }

  Future<_PieceWageRow> _hydrateRow(PieceWageRecord wage) async {
    final employeeId = wage.employeeId;
    final orderContext = wage.orderId > 0 ? await _resolveOrderContext(wage.orderId, wage.pieceId) : const _OrderContext();
    final drawTotal = employeeId == null
        ? 0.0
        : (await _repository.getDraws(employeeId))
            .fold<double>(0, (sum, draw) => sum + (draw.amount ?? 0));
    final settlementTotal = employeeId == null
        ? 0.0
        : (await _repository.getSettlements(employeeId))
            .fold<double>(0, (sum, item) => sum + item.amount);

    return _PieceWageRow(
      record: wage,
      customerName: orderContext.customerName,
      orderNumber: orderContext.orderNumber,
      trackingCode: orderContext.trackingCode,
      drawTotal: drawTotal,
      settlementTotal: settlementTotal,
      netDue: wage.totalWage - drawTotal - settlementTotal,
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
          title: const Text('أجور القطعة'),
          actions: [
            IconButton(
              tooltip: 'تحديث',
              onPressed: _reload,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        body: FutureBuilder<_PieceWageData>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.cloud_off_outlined, size: 42),
                      const SizedBox(height: 12),
                      Text('تعذر تحميل بيانات أجور القطعة:\n${snapshot.error}',
                          textAlign: TextAlign.center),
                      const SizedBox(height: 12),
                      FilledButton.icon(
                        onPressed: _reload,
                        icon: const Icon(Icons.refresh),
                        label: const Text('إعادة المحاولة'),
                      ),
                    ],
                  ),
                ),
              );
            }

            final data = snapshot.data!;
            final rows = data.wages;
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _SummaryCard(title: 'أجور القطعة', value: '${rows.length}', icon: Icons.precision_manufacturing_outlined),
                const SizedBox(height: 12),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.list_alt_outlined),
                            const SizedBox(width: 8),
                            Text('سجلات الأجر', style: Theme.of(context).textTheme.titleMedium),
                          ],
                        ),
                        const SizedBox(height: 12),
                        if (rows.isEmpty)
                          const Center(child: Text('لا توجد سجلات أجور قطعة فعالة في النظام الحالي.'))
                        else
                          SingleChildScrollView(
                            scrollDirection: Axis.horizontal,
                            child: DataTable(
                              columns: const [
                                DataColumn(label: Text('الموظف')),
                                DataColumn(label: Text('العميل')),
                                DataColumn(label: Text('الطلب')),
                                DataColumn(label: Text('TrackingCode')),
                                DataColumn(label: Text('النوع')),
                                DataColumn(label: Text('المرحلة')),
                                DataColumn(label: Text('الكمية')),
                                DataColumn(label: Text('السعر')),
                                DataColumn(label: Text('الإجمالي')),
                                DataColumn(label: Text('السلف')),
                                DataColumn(label: Text('صافي المستحق')),
                                DataColumn(label: Text('الحالة')),
                              ],
                              rows: rows.map((item) {
                                final employeeCode = item.record.employeeCode ?? 'EMP-${item.record.employeeId ?? 'غير محدد'}';
                                return DataRow(cells: [
                                  DataCell(Text(employeeCode)),
                                  DataCell(Text(item.customerName)),
                                  DataCell(Text(item.orderNumber)),
                                  DataCell(Text(item.trackingCode)),
                                  DataCell(Text(item.record.pieceType)),
                                  DataCell(Text(item.record.stage)),
                                  DataCell(Text(NumberFormat('#,##0.##').format(item.record.quantity))),
                                  DataCell(Text(NumberFormat('#,##0.00').format(item.record.wageRate))),
                                  DataCell(Text(NumberFormat('#,##0.00').format(item.record.totalWage))),
                                  DataCell(Text(NumberFormat('#,##0.00').format(item.drawTotal))),
                                  DataCell(Text(NumberFormat('#,##0.00').format(item.netDue))),
                                  DataCell(Text(item.record.status)),
                                ]);
                              }).toList(),
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Row(
                          children: [
                            const Icon(Icons.attach_money_outlined),
                            const SizedBox(width: 8),
                            Text('أسعار القطعة', style: Theme.of(context).textTheme.titleMedium),
                          ],
                        ),
                        const SizedBox(height: 12),
                        if (data.rates.isEmpty)
                          const Text('لا توجد أسعار فعالة مسجلة حاليًا.')
                        else
                          ...data.rates.map((item) => ListTile(
                                dense: true,
                                leading: const Icon(Icons.price_change_outlined),
                                title: Text('${item.pieceType} • ${item.stage}'),
                                subtitle: item.notes == null || item.notes!.trim().isEmpty
                                    ? null
                                    : Text(item.notes!),
                                trailing: Text('${NumberFormat('#,##0.00').format(item.wageRate)} ر.س'),
                              )),
                      ],
                    ),
                  ),
                ),
              ],
            );
          },
        ),
      );
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.title, required this.value, required this.icon});

  final String title;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              CircleAvatar(
                backgroundColor: Theme.of(context).colorScheme.primaryContainer,
                child: Icon(icon),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: Theme.of(context).textTheme.titleMedium),
                    Text(value, style: Theme.of(context).textTheme.headlineSmall),
                  ],
                ),
              ),
            ],
          ),
        ),
      );
}

class _OrderContext {
  const _OrderContext({this.orderNumber = '-', this.customerName = 'غير محدد', this.trackingCode = '—'});

  final String orderNumber;
  final String customerName;
  final String trackingCode;
}

class _PieceWageRow {
  const _PieceWageRow({
    required this.record,
    required this.customerName,
    required this.orderNumber,
    required this.trackingCode,
    required this.drawTotal,
    required this.settlementTotal,
    required this.netDue,
  });

  final PieceWageRecord record;
  final String customerName;
  final String orderNumber;
  final String trackingCode;
  final double drawTotal;
  final double settlementTotal;
  final double netDue;
}

class _PieceWageData {
  const _PieceWageData({required this.wages, required this.rates});

  final List<_PieceWageRow> wages;
  final List<PieceWageRate> rates;
}
