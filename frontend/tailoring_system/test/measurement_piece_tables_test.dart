import 'package:flutter_test/flutter_test.dart';

import 'package:tailoring_system/screens/measurement_piece_tables.dart';
import 'package:tailoring_system/screens/measurements_screen.dart';

void main() {
  test('formats integer and decimal values without fake units', () {
    expect(formatMeasurementValue('7.0'), '7');
    expect(formatMeasurementValue('30.00'), '30');
    expect(formatMeasurementValue('7.5'), '7.5');
    expect(formatMeasurementValue('30.25'), '30.25');
  });

  test('groups measurements independently by piece type', () {
    final source = <CustomerMeasurement>[
      CustomerMeasurement(
          pieceType: 'بنطلون',
          measurementName: 'الحزام',
          value: '30',
          createdAtUtc: DateTime.utc(2026, 1, 2),
          revisionNumber: 1),
      CustomerMeasurement(
          pieceType: 'كوت',
          measurementName: 'الطول',
          value: '18',
          createdAtUtc: DateTime.utc(2026, 1, 3),
          revisionNumber: 1),
      CustomerMeasurement(
          pieceType: 'بنطلون',
          measurementName: 'الفخذ',
          value: '18.5',
          createdAtUtc: DateTime.utc(2026, 1, 4),
          revisionNumber: 2),
    ];

    final grouped = groupMeasurementsByPieceType(source);

    expect(grouped.keys, containsAll(<String>['بنطلون', 'كوت']));
    expect(grouped['بنطلون']!.map((item) => item.measurementName),
        isNot(contains('الطول')));
    expect(grouped['كوت']!.map((item) => item.measurementName), ['الطول']);
  });

  test('uses official sequence rather than alphabetical ordering', () {
    final source = <CustomerMeasurement>[
      CustomerMeasurement(
          pieceType: 'قميص',
          measurementName: 'وسع المرفق',
          value: '4',
          createdAtUtc: DateTime.utc(2026, 1, 2),
          revisionNumber: 1),
      CustomerMeasurement(
          pieceType: 'قميص',
          measurementName: 'الطول',
          value: '20',
          createdAtUtc: DateTime.utc(2026, 1, 2),
          revisionNumber: 1),
      CustomerMeasurement(
          pieceType: 'قميص',
          measurementName: 'الكتف',
          value: '11',
          createdAtUtc: DateTime.utc(2026, 1, 2),
          revisionNumber: 1),
    ];

    final grouped = groupMeasurementsByPieceType(source, officialFieldOrder: {
      'قميص': [
        'الطول',
        'الكتف',
        'اليد',
        'وسع الصدر',
        'وسع البطن',
        'الرقبة',
        'طول الكبك',
        'عرض الكبك',
        'وسع المرفق'
      ],
    });

    expect(grouped['قميص']!.map((item) => item.measurementName),
        ['الطول', 'الكتف', 'وسع المرفق']);
  });

  test('latest modified date is per piece type', () {
    final source = <CustomerMeasurement>[
      CustomerMeasurement(
          pieceType: 'بنطلون',
          measurementName: 'الحزام',
          value: '30',
          createdAtUtc: DateTime.utc(2026, 1, 2),
          revisionNumber: 1),
      CustomerMeasurement(
          pieceType: 'كوت',
          measurementName: 'الطول',
          value: '18',
          createdAtUtc: DateTime.utc(2026, 2, 5),
          revisionNumber: 1),
      CustomerMeasurement(
          pieceType: 'بنطلون',
          measurementName: 'الفخذ',
          value: '18.5',
          createdAtUtc: DateTime.utc(2026, 3, 7),
          revisionNumber: 2),
    ];

    expect(
        formatArabicLastModified(latestModifiedForPieceType(source, 'بنطلون')),
        allOf(contains('07/03/2026'), startsWith('آخر تعديل:')));
    expect(formatArabicLastModified(latestModifiedForPieceType(source, 'كوت')),
        allOf(contains('05/02/2026'), startsWith('آخر تعديل:')));
  });
}
