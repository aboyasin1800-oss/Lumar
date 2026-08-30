import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

class WorkCardScreen extends StatefulWidget {
	const WorkCardScreen({super.key});
	@override State<WorkCardScreen> createState() => _WorkCardScreenState();
}

class _WorkCardScreenState extends State<WorkCardScreen> {
	final api = WorkCardApi(); final search = TextEditingController(); late Future<List<WorkCardPiece>> future;
	@override void initState() { super.initState(); future = api.getPieces(); }
	@override void dispose() { search.dispose(); super.dispose(); }
	void reload() => setState(() => future = api.getPieces());
	@override Widget build(BuildContext context) => Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
		Row(children: [Expanded(child: Text('بطاقات التشغيل', style: Theme.of(context).textTheme.headlineSmall)), IconButton(tooltip: 'تحديث', onPressed: reload, icon: const Icon(Icons.refresh))]),
		const SizedBox(height: 12),
		TextField(controller: search, onChanged: (_) => setState(() {}), decoration: const InputDecoration(prefixIcon: Icon(Icons.search), labelText: 'بحث برقم القطعة أو كود التتبع أو الحالة', border: OutlineInputBorder())),
		const SizedBox(height: 12),
		Expanded(child: FutureBuilder<List<WorkCardPiece>>(
			future: future,
			builder: (context, snapshot) {
				if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
				if (snapshot.hasError) return _WorkCardError(onRetry: reload);
				final query = search.text.trim().toLowerCase();
				final pieces = snapshot.data!.where((piece) => query.isEmpty || '${piece.pieceNumber}'.contains(query) || piece.trackingCode.toLowerCase().contains(query) || piece.pieceType.toLowerCase().contains(query) || piece.status.toLowerCase().contains(query)).toList();
				if (pieces.isEmpty) return const Center(child: Text('لا توجد قطع مطابقة.'));
				return ListView.separated(
					itemCount: pieces.length,
					separatorBuilder: (_, __) => const SizedBox(height: 6),
					itemBuilder: (context, index) {
						final piece = pieces[index];
						return Card(child: ListTile(
							leading: CircleAvatar(child: Text('${piece.pieceNumber}')),
							title: Text('${piece.pieceType} - ${piece.trackingCode}'),
							subtitle: Text('الحالة: ${piece.status}'),
							trailing: IconButton(tooltip: 'معاينة البطاقة', icon: const Icon(Icons.visibility_outlined), onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => WorkCardPreviewScreen(pieceId: piece.id)))),
						));
					},
				);
			},
		)),
	]);
}

class WorkCardPreviewScreen extends StatefulWidget {
	const WorkCardPreviewScreen({required this.pieceId, super.key}); final int pieceId;
	@override State<WorkCardPreviewScreen> createState() => _WorkCardPreviewScreenState();
}

class _WorkCardPreviewScreenState extends State<WorkCardPreviewScreen> {
	final api = WorkCardApi(); late Future<WorkCard> future;
	@override void initState() { super.initState(); future = api.getWorkCard(widget.pieceId); }
	void reload() { setState(() { future = api.getWorkCard(widget.pieceId); }); }
	void printPlaceholder() => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('الطباعة الفعلية ستكون متاحة في مرحلة PDF والطباعة.')));
	@override Widget build(BuildContext context) => Scaffold(
		appBar: AppBar(title: const Text('معاينة بطاقة التشغيل'), actions: [IconButton(tooltip: 'تحديث', onPressed: reload, icon: const Icon(Icons.refresh)), IconButton(tooltip: 'طباعة', onPressed: printPlaceholder, icon: const Icon(Icons.print_outlined))]),
		body: FutureBuilder<WorkCard>(future: future, builder: (context, snapshot) {
			if (snapshot.connectionState != ConnectionState.done) return const Center(child: CircularProgressIndicator());
			if (snapshot.hasError) return _WorkCardError(onRetry: reload, message: snapshot.error is WorkCardApiException && (snapshot.error as WorkCardApiException).statusCode == 404 ? 'خادم الإنتاج الحالي لا يوفر بطاقة التشغيل. يلزم تشغيل إصدار Backend المطابق للكود الحالي.' : 'تعذر تحميل بطاقة التشغيل.');
			final card = snapshot.data!;
			final notes = [card.notes1, card.notes2].whereType<String>().where((value) => value.trim().isNotEmpty).join('\n');
			return ListView(padding: const EdgeInsets.all(24), children: [
				Center(child: ConstrainedBox(
					constraints: const BoxConstraints(maxWidth: 900),
					child: Card(
						elevation: 0,
						shape: RoundedRectangleBorder(side: BorderSide(color: Theme.of(context).colorScheme.outlineVariant)),
						child: Padding(
							padding: const EdgeInsets.all(24),
							child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
								Row(children: [const Icon(Icons.content_cut, size: 34), const SizedBox(width: 12), Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('بطاقة تشغيل', style: Theme.of(context).textTheme.headlineSmall), Text(card.trackingCode, style: Theme.of(context).textTheme.titleMedium)])), Chip(label: Text(card.status))]),
								const Divider(height: 30),
								Wrap(spacing: 12, runSpacing: 12, children: [_CardField('رقم الطلب', card.orderNumber), _CardField('رقم القطعة', '${card.pieceNumber}'), _CardField('كود العميل', card.customerCode), _CardField('اسم العميل', card.customerName), _CardField('الهاتف', card.phoneNumber), _CardField('نوع القطعة', card.pieceType), _CardField('الكمية', '${card.quantity}'), _CardField('نوع القماش', card.fabricType), _CardField('لون القماش', card.fabricColor), _CardField('تاريخ التسليم', card.deliveryDate == null ? '-' : DateFormat('yyyy/MM/dd').format(card.deliveryDate!)), _CardField('الحالة الحالية', card.status)]),
								const SizedBox(height: 18), Text('الملاحظات', style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 6), Text(notes.isEmpty ? '-' : notes),
								const SizedBox(height: 18), Text('القياسات', style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 8), _Measurements(values: card.measurements),
								const SizedBox(height: 18), Text('تاريخ التتبع', style: Theme.of(context).textTheme.titleMedium), const SizedBox(height: 8),
								if (card.history.isEmpty) const Text('لا يوجد تاريخ تتبع.') else ...card.history.map((event) => ListTile(contentPadding: EdgeInsets.zero, leading: Icon(event.isReverted ? Icons.undo : Icons.check_circle_outline), title: Text('${event.stage} - ${event.status}'), subtitle: Text('${DateFormat('yyyy/MM/dd HH:mm').format(event.eventTime)}${event.employeeCode == null ? '' : '  •  ${event.employeeCode}'}${event.notes == null ? '' : '\n${event.notes}'}'))),
							]),
						),
					),
				)),
				const SizedBox(height: 14), Align(alignment: Alignment.center, child: FilledButton.icon(onPressed: printPlaceholder, icon: const Icon(Icons.print_outlined), label: const Text('طباعة'))),
			]);
		}),
	);
}

class _CardField extends StatelessWidget {
	const _CardField(this.label, this.value); final String label; final String? value;
	@override Widget build(BuildContext context) => SizedBox(width: 190, child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(label, style: Theme.of(context).textTheme.labelMedium), const SizedBox(height: 3), SelectableText(value == null || value!.trim().isEmpty ? '-' : value!, style: Theme.of(context).textTheme.titleSmall)]));
}
class _Measurements extends StatelessWidget {
	const _Measurements({required this.values}); final Map<String, dynamic> values;
	@override Widget build(BuildContext context) => values.isEmpty ? const Text('لا توجد قياسات محفوظة.') : Wrap(spacing: 10, runSpacing: 10, children: values.entries.where((entry) => !entry.key.startsWith('_')).map((entry) => Container(width: 150, padding: const EdgeInsets.all(10), decoration: BoxDecoration(border: Border.all(color: Theme.of(context).colorScheme.outlineVariant), borderRadius: BorderRadius.circular(6)), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(entry.key, style: Theme.of(context).textTheme.labelMedium), Text(entry.value.toString(), style: Theme.of(context).textTheme.titleMedium)]))).toList());
}
class _WorkCardError extends StatelessWidget {
	const _WorkCardError({required this.onRetry, this.message = 'تعذر تحميل بطاقة التشغيل.'}); final VoidCallback onRetry; final String message;
	@override Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [const Icon(Icons.cloud_off_outlined, size: 42), const SizedBox(height: 10), Text(message, textAlign: TextAlign.center), const SizedBox(height: 10), FilledButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('إعادة المحاولة'))]));
}

class WorkCardApi {
	static const _baseUrl = String.fromEnvironment('LUMAR_API_URL', defaultValue: 'http://127.0.0.1:5092');
	Future<dynamic> _get(String path) async { final response = await http.get(Uri.parse('$_baseUrl$path')); if (response.statusCode < 200 || response.statusCode >= 300) throw WorkCardApiException(response.statusCode); return jsonDecode(response.body); }
	Future<List<WorkCardPiece>> getPieces() async => ((await _get('/production/pieces')) as List).cast<Map<String, dynamic>>().map(WorkCardPiece.fromJson).toList();
	Future<WorkCard> getWorkCard(int pieceId) async => WorkCard.fromJson((await _get('/production/pieces/$pieceId/work-card')) as Map<String, dynamic>);
}
class WorkCardPiece {
	const WorkCardPiece({required this.id, required this.pieceNumber, required this.trackingCode, required this.status, required this.pieceType});
	factory WorkCardPiece.fromJson(Map<String, dynamic> json) => WorkCardPiece(id: json['pieceId'] as int, pieceNumber: json['pieceNumber'] as int, trackingCode: json['trackingCode'] as String, status: json['pieceStatus'] as String, pieceType: json['pieceType']?.toString() ?? '-');
	final int id; final int pieceNumber; final String trackingCode; final String status; final String pieceType;
}
class WorkCard {
	const WorkCard({required this.orderNumber, required this.pieceNumber, required this.trackingCode, required this.customerCode, required this.customerName, required this.phoneNumber, required this.pieceType, required this.quantity, required this.fabricType, required this.fabricColor, required this.notes1, required this.notes2, required this.measurements, required this.deliveryDate, required this.status, required this.history});
	factory WorkCard.fromJson(Map<String, dynamic> json) { final snapshot = json['measurementSnapshot'] as String?; Map<String, dynamic> measurements = {}; if (snapshot != null && snapshot.trim().isNotEmpty) { try { measurements = jsonDecode(snapshot) as Map<String, dynamic>; } catch (_) {} } return WorkCard(orderNumber: json['orderNumber'] as String, pieceNumber: json['pieceNumber'] as int, trackingCode: json['trackingCode'] as String, customerCode: json['customerCode'] as String?, customerName: json['customerName'] as String?, phoneNumber: json['phoneNumber'] as String?, pieceType: json['pieceType'] as String, quantity: json['quantity'] as int, fabricType: json['fabricType'] as String?, fabricColor: json['fabricColor'] as String?, notes1: json['notes1'] as String?, notes2: json['notes2'] as String?, measurements: measurements, deliveryDate: json['deliveryDate'] == null ? null : DateTime.parse(json['deliveryDate'] as String), status: json['pieceStatus'] as String, history: (json['trackingHistory'] as List).cast<Map<String, dynamic>>().map(WorkCardTrackingEvent.fromJson).toList()); }
	final String orderNumber; final int pieceNumber; final String trackingCode; final String? customerCode; final String? customerName; final String? phoneNumber; final String pieceType; final int quantity; final String? fabricType; final String? fabricColor; final String? notes1; final String? notes2; final Map<String, dynamic> measurements; final DateTime? deliveryDate; final String status; final List<WorkCardTrackingEvent> history;
}
class WorkCardApiException implements Exception { const WorkCardApiException(this.statusCode); final int statusCode; }
class WorkCardTrackingEvent {
	const WorkCardTrackingEvent({required this.stage, required this.status, required this.eventTime, required this.employeeCode, required this.notes, required this.isReverted});
	factory WorkCardTrackingEvent.fromJson(Map<String, dynamic> json) => WorkCardTrackingEvent(stage: json['stage'] as String, status: json['status'] as String, eventTime: DateTime.parse(json['eventTime'] as String), employeeCode: json['employeeCode'] as String?, notes: json['notes'] as String?, isReverted: json['isReverted'] as bool);
	final String stage; final String status; final DateTime eventTime; final String? employeeCode; final String? notes; final bool isReverted;
}