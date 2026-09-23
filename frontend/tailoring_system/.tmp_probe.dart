import 'dart:convert';
import 'package:tailoring_system/core/measurement_snapshot.dart';

void main() {
  const payload = '{"الطول":"26.0","الكتف":"16.0","اليد":"24.0","وسع الصدر":"34.0","وسع البطن":"31.0","فتحة اليد":"5.0","وسع المرفق":"6.0","_catalogNumber":"FA0015","_consumption":"60.0","_consumptionUnit":"Inch","fabricCode":"FA0015","fabricType":"بوليستر","fabricColor":"اسمر","request1":"سليمان","request2":"عبد القادر","specialRequest":"كمالKMAL"}';
  final parsed = parseMeasurementSnapshot(payload);
  final cleaned = extractRealPieceMeasurements(parsed);
  print('PARSED_KEYS=${parsed.keys.toList()}');
  print('CLEANED=${cleaned}');
  print('IS_EMPTY=${cleaned.isEmpty}');
}
