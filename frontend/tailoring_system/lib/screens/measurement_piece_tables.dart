import 'package:flutter/material.dart';

import 'measurements_screen.dart';

String formatMeasurementValue(String raw) {
  final value = num.tryParse(raw.trim().replaceAll(',', '.'));
  if (value == null) return raw;
  if (value == value.roundToDouble()) return value.toInt().toString();
  return value
      .toStringAsFixed(12)
      .replaceFirst(RegExp(r'0+$'), '')
      .replaceFirst(RegExp(r'\.$'), '');
}

Map<String, List<CustomerMeasurement>> groupMeasurementsByPieceType(
    Iterable<CustomerMeasurement> source,
    {Map<String, List<String>>? officialFieldOrder}) {
  final latestByPieceAndName = <String, Map<String, CustomerMeasurement>>{};
  for (final measurement in source) {
    final pieceType = measurement.pieceType.trim();
    if (pieceType.isEmpty) continue;
    final byName = latestByPieceAndName.putIfAbsent(pieceType, () => {});
    final current = byName[measurement.measurementName];
    if (current == null ||
        measurement.revisionNumber > current.revisionNumber ||
        (measurement.revisionNumber == current.revisionNumber &&
            measurement.createdAtUtc.isAfter(current.createdAtUtc))) {
      byName[measurement.measurementName] = measurement;
    }
  }
  return {
    for (final entry in latestByPieceAndName.entries)
      entry.key: (() {
        final order = officialFieldOrder?[entry.key] ??
            officialFieldOrder?[_normalizePieceTypeForDisplay(entry.key)] ??
            const <String>[];
        final result = entry.value.values.toList();
        result.sort((a, b) {
          final aIndex = order.indexOf(a.measurementName);
          final bIndex = order.indexOf(b.measurementName);
          if (aIndex >= 0 || bIndex >= 0) {
            if (aIndex < 0) return 1;
            if (bIndex < 0) return -1;
            return aIndex.compareTo(bIndex);
          }
          return a.measurementName.compareTo(b.measurementName);
        });
        return result;
      })(),
  };
}

String _normalizePieceTypeForDisplay(String value) {
  final key = value.trim();
  if (key.contains('قميص')) return 'قميص';
  if (key.contains('بنطلون')) return 'بنطلون';
  if (key.contains('كوت')) return 'كوت';
  return key;
}

DateTime? latestModifiedForPieceType(
    Iterable<CustomerMeasurement> source, String pieceType) {
  DateTime? latest;
  for (final measurement in source) {
    if (measurement.pieceType.trim() != pieceType) continue;
    if (latest == null || measurement.createdAtUtc.isAfter(latest)) {
      latest = measurement.createdAtUtc;
    }
  }
  return latest;
}

String formatArabicLastModified(DateTime? value) {
  if (value == null) return 'آخر تعديل: غير متاح';
  final date = value.toLocal();
  final year = date.year.toString().padLeft(4, '0');
  final month = date.month.toString().padLeft(2, '0');
  final day = date.day.toString().padLeft(2, '0');
  final hour = date.hour.toString().padLeft(2, '0');
  final minute = date.minute.toString().padLeft(2, '0');
  return 'آخر تعديل: $day/$month/$year - $hour:$minute';
}

class MeasurementPieceTables extends StatelessWidget {
  const MeasurementPieceTables({
    required this.measurements,
    required this.onEdit,
    this.officialFieldOrder,
    super.key,
  });

  final List<CustomerMeasurement> measurements;
  final Map<String, List<String>>? officialFieldOrder;
  final void Function(String pieceType) onEdit;

  @override
  Widget build(BuildContext context) {
    final grouped = groupMeasurementsByPieceType(measurements,
        officialFieldOrder: officialFieldOrder);
    if (grouped.isEmpty) {
      return const Center(child: Text('لا توجد قياسات محفوظة لهذا العميل.'));
    }

    return Directionality(
      textDirection: TextDirection.rtl,
      child: ListView.separated(
        padding: const EdgeInsets.symmetric(vertical: 12),
        itemCount: grouped.length,
        separatorBuilder: (_, __) => const SizedBox(height: 16),
        itemBuilder: (context, index) {
          final pieceType = grouped.keys.elementAt(index);
          final pieceMeasurements = grouped[pieceType]!;
          final lastModified =
              latestModifiedForPieceType(measurements, pieceType);
          final theme = Theme.of(context);

          return Card(
            clipBehavior: Clip.antiAlias,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  padding: const EdgeInsets.all(16),
                  color: theme.colorScheme.surfaceContainerHighest,
                  child: Row(
                    children: [
                      const Icon(Icons.straighten_outlined),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(pieceType,
                            style: theme.textTheme.titleLarge
                                ?.copyWith(fontWeight: FontWeight.w800)),
                      ),
                      OutlinedButton.icon(
                        onPressed: () => onEdit(pieceType),
                        icon: const Icon(Icons.edit_outlined, size: 18),
                        label: const Text('تعديل القياسات'),
                      ),
                    ],
                  ),
                ),
                LayoutBuilder(builder: (context, constraints) {
                  Widget measurementRow;
                  if (constraints.maxWidth >= 900) {
                    measurementRow = Row(
                      children: pieceMeasurements
                          .map((measurement) => Expanded(
                                child: Container(
                                  height: 62,
                                  margin:
                                      const EdgeInsets.symmetric(horizontal: 5),
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 10, vertical: 8),
                                  decoration: BoxDecoration(
                                    border: Border.all(
                                        color:
                                            theme.colorScheme.outlineVariant),
                                    borderRadius: BorderRadius.circular(6),
                                  ),
                                  child: Column(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: [
                                      Text(measurement.measurementName,
                                          textAlign: TextAlign.center,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: theme.textTheme.bodySmall),
                                      const SizedBox(height: 3),
                                      Text(
                                          formatMeasurementValue(
                                              measurement.value),
                                          style: theme.textTheme.titleMedium
                                              ?.copyWith(
                                                  fontWeight: FontWeight.w800)),
                                    ],
                                  ),
                                ),
                              ))
                          .toList(),
                    );
                  } else {
                    measurementRow = SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      reverse: true,
                      child: Row(
                        children: pieceMeasurements
                            .map((measurement) => Container(
                                  width: 150,
                                  height: 62,
                                  margin:
                                      const EdgeInsets.symmetric(horizontal: 5),
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 10, vertical: 8),
                                  decoration: BoxDecoration(
                                    border: Border.all(
                                        color:
                                            theme.colorScheme.outlineVariant),
                                    borderRadius: BorderRadius.circular(6),
                                  ),
                                  child: Column(
                                    mainAxisAlignment: MainAxisAlignment.center,
                                    children: [
                                      Text(measurement.measurementName,
                                          textAlign: TextAlign.center,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: theme.textTheme.bodySmall),
                                      const SizedBox(height: 3),
                                      Text(
                                          formatMeasurementValue(
                                              measurement.value),
                                          style: theme.textTheme.titleMedium
                                              ?.copyWith(
                                                  fontWeight: FontWeight.w800)),
                                    ],
                                  ),
                                ))
                            .toList(),
                      ),
                    );
                  }
                  return Padding(
                    padding: const EdgeInsets.all(16),
                    child: measurementRow,
                  );
                }),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                  child: Text(formatArabicLastModified(lastModified),
                      style: theme.textTheme.bodySmall),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
