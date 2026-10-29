import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/core/document_printing.dart';
import 'package:tailoring_system/core/measurement_snapshot.dart';

void main() {
  test('extractRealPieceMeasurements keeps only actual measurement fields', () {
    final snapshot = {
      'الطول': '26',
      'الكتف': '17',
      'request1': 'تخصيص 1',
      'specialRequest': 'طلب خاص',
      'notes1': 'ملاحظة 1',
      'fabricCode': 'FAB-01',
      'fabricType': 'قماش',
      'fabricColor': 'أبيض',
      'consumption': '7.5',
      'catalogNumber': 'CAT-100',
      'fullCost': '150.0',
      'source': 'sales',
    };

    expect(extractRealPieceMeasurements(snapshot), {
      'الطول': '26',
      'الكتف': '17',
    });
  });

  test(
      'parseMeasurementSnapshot and cleanup match the production details source',
      () {
    const source =
        '{"الطول":"26","الكتف":"17","request1":"تخصيص","fabricCode":"FAB-01","catalogNumber":"CAT-100","specialRequest":"طلب خاص"}';

    final parsed = parseMeasurementSnapshot(source);
    final cleaned = extractRealPieceMeasurements(parsed);

    expect(parsed['الطول'], '26');
    expect(cleaned, {
      'الطول': '26',
      'الكتف': '17',
    });
  });

  test('JSON string snapshot keeps only real measurement keys', () {
    const source =
        '{"الطول":"26","الكتف":"17","request1":"تخصيص","specialRequest":"طلب خاص","fabricCode":"FAB-01"}';

    final parsed = parseMeasurementSnapshot(source);

    expect(parsed['الطول'], '26');
    expect(extractRealPieceMeasurements(parsed), {
      'الطول': '26',
      'الكتف': '17',
    });
  });

  test('direct map snapshot keeps only real measurement keys', () {
    final source = {
      'الطول': '26',
      'الكتف': '17',
      'request1': 'تخصيص',
      'specialRequest': 'طلب خاص',
      'fabricCode': 'FAB-01',
    };

    expect(extractRealPieceMeasurements(source), {
      'الطول': '26',
      'الكتف': '17',
    });
  });

  test('nested measurementSnapshot key unwraps the contained map', () {
    final source = {
      'measurementSnapshot': {
        'الطول': '26',
        'الكتف': '17',
        'request1': 'تخصيص',
        'fabricCode': 'FAB-01',
      },
    };

    final parsed = parseMeasurementSnapshot(source);

    expect(parsed['الطول'], '26');
    expect(extractRealPieceMeasurements(parsed), {
      'الطول': '26',
      'الكتف': '17',
    });
  });

  test('MeasurementSnapshot key with nested JSON string is also supported', () {
    final source = {
      'MeasurementSnapshot':
          '{"الطول":"26","الكتف":"17","request1":"تخصيص","fabricCode":"FAB-01"}',
      'specialRequest': 'طلب خاص',
    };

    final parsed = parseMeasurementSnapshot(source);

    expect(parsed['الطول'], '26');
    expect(extractRealPieceMeasurements(parsed), {
      'الطول': '26',
      'الكتف': '17',
    });
  });

  test('snapshot with descriptive fields still keeps only real measurements',
      () {
    final source = {
      'MeasurementSnapshot': {
        'الطول': '26',
        'الكتف': '17',
        'اليد': '18',
        'وسع الصدر': '100',
        'وسع البطن': '96',
        'فتحة اليد': '22',
        'وسع المرفق': '30',
        'request1': 'تخصيص',
        'notes1': 'ملاحظة',
        'fabricCode': 'FAB-01',
        'consumption': '7.5',
      },
      'fabricType': 'قماش',
      'catalogNumber': 'CAT-100',
    };

    final parsed = parseMeasurementSnapshot(source);
    final cleaned = extractRealPieceMeasurements(parsed);

    expect(cleaned, {
      'الطول': '26',
      'الكتف': '17',
      'اليد': '18',
      'وسع الصدر': '100',
      'وسع البطن': '96',
      'فتحة اليد': '22',
      'وسع المرفق': '30',
    });
  });

  test('measurement grid batches into rows of six and preserves order', () {
    final cases = <int, List<String>>{
      5: ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن'],
      6: ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن', 'فتحة اليد'],
      7: ['الطول', 'الكتف', 'اليد', 'وسع الصدر', 'وسع البطن', 'فتحة اليد', 'وسع المرفق'],
      10: [
        'الطول',
        'الكتف',
        'اليد',
        'وسع الصدر',
        'وسع البطن',
        'فتحة اليد',
        'وسع المرفق',
        'عرض الكتف',
        'طول الجيب',
        'عرض الخاصرة',
      ],
      12: [
        'الطول',
        'الكتف',
        'اليد',
        'وسع الصدر',
        'وسع البطن',
        'فتحة اليد',
        'وسع المرفق',
        'عرض الكتف',
        'طول الجيب',
        'عرض الخاصرة',
        'محيط الرقبة',
        'محيط المعصم',
      ],
      13: [
        'الطول',
        'الكتف',
        'اليد',
        'وسع الصدر',
        'وسع البطن',
        'فتحة اليد',
        'وسع المرفق',
        'عرض الكتف',
        'طول الجيب',
        'عرض الخاصرة',
        'محيط الرقبة',
        'محيط المعصم',
        'العرض الكلي',
      ],
    };

    final expectedRows = {
      5: [5],
      6: [6],
      7: [6, 1],
      10: [6, 4],
      12: [6, 6],
      13: [6, 6, 1],
    };

    for (final entry in cases.entries) {
      final size = entry.key;
      final measurements = <String, dynamic>{
        for (var i = 0; i < size; i++) entry.value[i]: i + 1,
      };
      final rows = DocumentPrintSupport.buildMeasurementGridRows(measurements);
      expect(rows.length, expectedRows[size]!.length);
      expect(rows.map((row) => row.length).toList(), expectedRows[size]!);
      expect(rows.every((row) => row.length <= 6), isTrue);
      expect(
        rows.expand((row) => row).map((entry) => entry.key).toList(),
        entry.value,
      );
      expect(
        rows.expand((row) => row).map((entry) => entry.key).toList().length,
        size,
      );
    }
  });

  test('measurement card keeps the approved 3x8 inch portrait sizing', () {
    expect(DocumentPrintSupport.measurementCardPortrait.width,
        closeTo(3 * 72, 0.1));
    expect(DocumentPrintSupport.measurementCardPortrait.height,
        closeTo(8 * 72, 0.1));
    expect(DocumentPrintSupport.measurementCardPortrait.width,
        lessThan(DocumentPrintSupport.measurementCardPortrait.height));
  });

  test('requests section remains active when request values exist', () {
    final source = {
      'request1': 'تخصيص داخلي',
      'request2': 'خياطة خاصة',
      'specialRequest': 'حزام مزخرف',
      'الطول': '26',
      'الكتف': '17',
      'fullCost': '150.0',
    };

    final cleaned = extractRealPieceMeasurements(parseMeasurementSnapshot(source));
    expect(cleaned.containsKey('الطول'), isTrue);
    expect(cleaned.containsKey('الكتف'), isTrue);
    expect(cleaned.containsKey('fullCost'), isFalse);
    expect(cleaned.length, 2);
  });

  test('print metadata accepts PascalCase JSON keys for fabric and requests', () {
    final json = {
      'FabricCode': 'FAB-77',
      'FabricType': 'قماش',
      'FabricColor': 'أبيض',
      'CatalogNumber': 'CAT-77',
      'Request1': 'تخصيص داخلي',
      'Request2': 'خياطة خاصة',
      'SpecialRequest': 'حزام مزخرف',
      'MeasurementSnapshot': '{"الطول":"26","الكتف":"17"}',
      'deliveryDate': '2026-02-15T00:00:00Z',
    };

    final detail = _buildDetailFromJsonForTest(json);
    expect(detail.fabricCode, 'FAB-77');
    expect(detail.fabricType, 'قماش');
    expect(detail.request1, 'تخصيص داخلي');
    expect(detail.request2, 'خياطة خاصة');
    expect(detail.specialRequest, 'حزام مزخرف');
    expect(detail.deliveryDate, DateTime.utc(2026, 2, 15));
    expect(detail.measurements, {'الطول': '26', 'الكتف': '17'});
  });
}

_OrderPieceDetailForTest _buildDetailFromJsonForTest(Map<String, dynamic> json) {
  final snapshotValue = json['MeasurementSnapshot'];
  final measurementMap = parseMeasurementSnapshot(snapshotValue);

  final fabricCode = DocumentPrintSupport.firstAvailableString([
    json['fabricCode'],
    json['FabricCode'],
    measurementMap['fabricCode'],
    measurementMap['FabricCode'],
  ]);
  final fabricType = DocumentPrintSupport.firstAvailableString([
    json['fabricType'],
    json['FabricType'],
    measurementMap['fabricType'],
    measurementMap['FabricType'],
  ]);
  final request1 = DocumentPrintSupport.firstAvailableString([
    json['request1'],
    json['Request1'],
    measurementMap['request1'],
    measurementMap['Request1'],
  ]);
  final request2 = DocumentPrintSupport.firstAvailableString([
    json['request2'],
    json['Request2'],
    measurementMap['request2'],
    measurementMap['Request2'],
  ]);
  final specialRequest = DocumentPrintSupport.firstAvailableString([
    json['specialRequest'],
    json['SpecialRequest'],
    measurementMap['specialRequest'],
    measurementMap['SpecialRequest'],
  ]);

  final deliveryDate = DateTime.tryParse((json['deliveryDate'] ?? '').toString());

  return _OrderPieceDetailForTest(
    fabricCode: fabricCode,
    fabricType: fabricType,
    request1: request1,
    request2: request2,
    specialRequest: specialRequest,
    deliveryDate: deliveryDate,
    measurements: extractRealPieceMeasurements(measurementMap),
  );
}

class _OrderPieceDetailForTest {
  const _OrderPieceDetailForTest({
    required this.fabricCode,
    required this.fabricType,
    required this.request1,
    required this.request2,
    required this.specialRequest,
    required this.deliveryDate,
    required this.measurements,
  });

  final String fabricCode;
  final String fabricType;
  final String request1;
  final String request2;
  final String specialRequest;
  final DateTime? deliveryDate;
  final Map<String, dynamic> measurements;
}
