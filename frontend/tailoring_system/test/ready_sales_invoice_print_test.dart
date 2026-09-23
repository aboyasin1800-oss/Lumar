import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pdf/widgets.dart' as pw;

void main() {
  test('invoice PDF creates with the registered Arabic font', () async {
    TestWidgetsFlutterBinding.ensureInitialized();

    final fontData = await rootBundle.load('assets/fonts/Tahoma.ttf');
    expect(fontData.lengthInBytes, greaterThan(0));

    final document = pw.Document();
    final font = pw.Font.ttf(fontData);
    document.addPage(
      pw.Page(
        build: (_) => pw.Directionality(
          textDirection: pw.TextDirection.rtl,
          child: pw.Text(
            'فاتورة بيع منتجات جاهزة',
            style: pw.TextStyle(font: font),
          ),
        ),
      ),
    );

    final bytes = await document.save();
    expect(bytes, isNotEmpty);
  });
}