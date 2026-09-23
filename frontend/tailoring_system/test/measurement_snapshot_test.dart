import 'package:flutter_test/flutter_test.dart';
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
}
