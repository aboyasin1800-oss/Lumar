import 'package:flutter_test/flutter_test.dart';
import 'package:pdf/widgets.dart' as pw;

import 'package:tailoring_system/core/document_printing.dart';

void main() {
  test('invoice PDF creates as A5 with header modes and payment details', () async {
    TestWidgetsFlutterBinding.ensureInitialized();

    final font = await DocumentPrintSupport.loadArabicFont();
    final textHeader = await DocumentPrintSupport.buildHeader(
      const DocumentPrintHeader(
        useImage: false,
        imageValue: '',
        name: 'مؤسسة لومار',
        location: 'العنوان التجاري',
        phone1: '711111111',
        phone2: '777777777',
      ),
      font,
    );
    const imageData = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';
    final imageHeader = await DocumentPrintSupport.buildHeader(
      const DocumentPrintHeader(
        useImage: true,
        imageValue: imageData,
        name: 'مؤسسة لومار',
        location: '',
        phone1: '',
        phone2: '',
      ),
      font,
    );
    final document = pw.Document();
    document.addPage(
      pw.MultiPage(
        pageFormat: DocumentPrintSupport.a5Portrait,
        build: (_) => [
          textHeader,
          imageHeader,
          pw.Directionality(
            textDirection: pw.TextDirection.rtl,
            child: pw.Column(
              crossAxisAlignment: pw.CrossAxisAlignment.stretch,
              children: [
                pw.Text('فاتورة بيع منتجات جاهزة', style: pw.TextStyle(font: font)),
                pw.Text('إجمالي الفاتورة: 14000', style: pw.TextStyle(font: font)),
                pw.Text('المدفوع: 10000', style: pw.TextStyle(font: font)),
                pw.Text('المتبقي في ذمة العميل: 4000', style: pw.TextStyle(font: font)),
                pw.Text('نوع الدفع: آجل', style: pw.TextStyle(font: font)),
              ],
            ),
          ),
        ],
      ),
    );

    final bytes = await document.save();
    expect(bytes, isNotEmpty);
    expect(DocumentPrintSupport.a5Portrait.width, closeTo(419.53, 0.1));
    expect(DocumentPrintSupport.a5Portrait.height, closeTo(595.28, 0.1));
  });
}