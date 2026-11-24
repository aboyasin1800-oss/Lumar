import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import '../models/order_models.dart';
import '../repositories/order_repository.dart';
import 'document_printing.dart';
import 'ui_palette.dart';

enum TailoringDocumentTarget { officePrinter, savePdf }

class TailoringOrderPrintData {
  const TailoringOrderPrintData({
    required this.order,
    required this.customer,
    required this.items,
  });

  final OrderDetails order;
  final OrderCustomer customer;
  final List<OrderItem> items;

  int get totalPieces => items.fold<int>(0, (sum, item) => sum + item.quantity);
}

class TailoringOrderDocumentsPrinting {
  const TailoringOrderDocumentsPrinting._();

  static Future<TailoringOrderPrintData> load(int orderId,
      {OrderRepository? repository}) async {
    final data = await (repository ?? OrderRepository()).getDetails(orderId);
    if (data.order.id != orderId) {
      throw StateError('تعذر تحميل بيانات الطلب المحفوظ.');
    }
    return TailoringOrderPrintData(
        order: data.order, customer: data.customer, items: data.items);
  }

  static Future<bool> confirm(BuildContext context, String title,
      {required String approveText}) async {
    final approved = await showDialog<bool>(
      context: context,
      barrierDismissible: true,
      builder: (dialogContext) {
        final isLight = Theme.of(dialogContext).brightness == Brightness.light;
        final background =
            isLight ? const Color(0xFFFFFFFF) : UiPalette.surfaceCard;
        final foreground = UiPalette.adaptiveTextColor(background);
        return CallbackShortcuts(
          bindings: {
            const SingleActivator(LogicalKeyboardKey.escape): () =>
                Navigator.of(dialogContext).pop(false),
          },
          child: Focus(
            autofocus: true,
            child: AlertDialog(
              backgroundColor: background,
              title: Text(title, style: TextStyle(color: foreground)),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(false),
                  child: const Text('لاحقًا'),
                ),
                FilledButton(
                  autofocus: true,
                  onPressed: () => Navigator.of(dialogContext).pop(true),
                  child: Text(approveText),
                ),
              ],
            ),
          ),
        );
      },
    );
    return approved == true;
  }

  static Future<bool> confirmReprint(BuildContext context) =>
      confirm(context, 'هل تريد إعادة طباعة هذا المستند؟',
          approveText: 'نعم، إعادة الطباعة');

  static Future<TailoringDocumentTarget?> chooseTarget(BuildContext context,
      {required bool finalInvoice}) {
    return showModalBottomSheet<TailoringDocumentTarget>(
      context: context,
      backgroundColor: Theme.of(context).brightness == Brightness.light
          ? const Color(0xFFFFFFFF)
          : UiPalette.surfaceCard,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            FilledButton.icon(
              autofocus: true,
              onPressed: () => Navigator.of(sheetContext)
                  .pop(TailoringDocumentTarget.officePrinter),
              icon: const Icon(Icons.print_outlined),
              label: const Text('طباعة على طابعة مكتبية'),
            ),
            ListTile(
              leading: const Icon(Icons.save_alt_outlined),
              title: const Text('حفظ PDF'),
              onTap: () => Navigator.of(sheetContext)
                  .pop(TailoringDocumentTarget.savePdf),
            ),
          ],
        ),
      ),
    );
  }

  static Future<bool> printInitialReceipt(
      BuildContext context, int orderId) async {
    final data = await load(orderId);
    final bytes = await _buildDocument(data, finalInvoice: false);
    return _execute(context, bytes, 'إيصال طلب تفصيل',
        'tailoring_receipt_${data.order.number}');
  }

  static Future<bool> printFinalInvoice(
      BuildContext context, int orderId) async {
    final data = await load(orderId);
    if (data.order.status.toLowerCase() != 'delivered') {
      throw StateError('لا يمكن طباعة الفاتورة قبل نجاح تسليم الطلب.');
    }
    final bytes = await _buildDocument(data, finalInvoice: true);
    return _execute(context, bytes, 'الفاتورة النهائية لطلب تفصيل',
        'tailoring_invoice_${data.order.number}');
  }

  static Future<Uint8List> _buildDocument(TailoringOrderPrintData data,
      {required bool finalInvoice}) async {
    final font = await DocumentPrintSupport.loadArabicFont();
    final header = await DocumentPrintSupport.loadHeaderSettings(
      const String.fromEnvironment('LUMAR_API_URL',
          defaultValue: 'http://127.0.0.1:5093'),
    );
    final pdf = pw.Document();
    final dateFormat = DateFormat('yyyy/MM/dd');
    final headerWidget = await DocumentPrintSupport.buildHeader(header, font);
    final terms = finalInvoice
        ? const [
            'المبالغ المثبتة في الفاتورة النهائية لا تُخفض أو تُعدل إلا في حال ثبت خطأ  .',
            'لا يمكن تغيير القطعة بعد اكتمال تجهيزها وتسليمها، وأي طلب لإعادة التنفيذ أو الاستبدال يكون على حساب العميل.',
            'أي تغيير سبق تنفيذه بعد بدء القص أو الخياطة تُضاف تكلفته إلى الطلب وفق تكلفة القماش والعمل المنفذ.',
          ]
        : const [
            'يرجى التأكد من اختيار القماش قبل بدء القص والخياطة؛ وأي تغيير بعد بدء التنفيذ يتحمل العميل تكلفة القماش والعمل المنفذ.',
            'المبالغ المثبتة في الإيصال لا تُخفض بعد اعتماد الطلب إلا في حال خدث خطأ حسابي مثبت.',
            'يرجى استلام الطلب خلال مدة لا تتجاوز شهرين من تاريخ الجاهزية؛ ولا تتحمل المؤسسة مسؤولية الطلبات المتروكة بعد هذه المدة.',
            'لا يمكن تغيير القطعة بعد اكتمال تجهيزها، وأي طلب لاستبدالها أو إعادة تنفيذها يكون على حساب العميل.',
            'تابع حالة طلبك من تطبيق LUMAR Mobile (لومار موبايل)، وستصلك رسالة عند جاهزية الطلب للاستلام لومار سعداء بخدمتكم تقبلو خالص تحياتنا شكرى لختياركم لومار.',
          ];

    pdf.addPage(
      pw.MultiPage(
        pageFormat: const PdfPageFormat(
            148 * PdfPageFormat.mm, 210 * PdfPageFormat.mm,
            marginAll: 8 * PdfPageFormat.mm),
        textDirection: pw.TextDirection.rtl,
        theme: pw.ThemeData.withFont(base: font, bold: font),
        header: (_) => pw.Directionality(
          textDirection: pw.TextDirection.rtl,
          child: headerWidget,
        ),
        build: (_) => [
          pw.Directionality(
            textDirection: pw.TextDirection.rtl,
            child: pw.Column(
                crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                children: [
                  pw.SizedBox(height: 8),
                  pw.Text(
                      finalInvoice
                          ? 'الفاتورة النهائية لطلب تفصيل'
                          : 'إيصال طلب تفصيل',
                      textAlign: pw.TextAlign.center,
                      style: pw.TextStyle(
                          font: font,
                          fontSize: 16,
                          fontWeight: pw.FontWeight.bold)),
                  pw.Text('رقم الطلب: ${data.order.number}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 11)),
                  if (finalInvoice)
                    pw.Text('حالة الطلب: تم التسليم',
                        textAlign: pw.TextAlign.right,
                        style: pw.TextStyle(font: font, fontSize: 11)),
                  pw.SizedBox(height: 6),
                  pw.Text('بيانات العميل',
                      style: pw.TextStyle(
                          font: font, fontWeight: pw.FontWeight.bold)),
                  pw.Text('الاسم: ${data.customer.name}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.Text('الكود: ${data.customer.code ?? '-'}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.Text('الهاتف: ${data.customer.phoneNumber ?? '-'}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.SizedBox(height: 6),
                  pw.Text(
                      'تاريخ الطلب: ${dateFormat.format(finalInvoice ? data.order.createdDate : data.order.orderDate)}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.Text(
                      'موعد الاستلام: ${data.order.deliveryDate == null ? '-' : dateFormat.format(data.order.deliveryDate!)}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  if (finalInvoice && data.order.revenueRecognizedAt != null)
                    pw.Text(
                        'تاريخ التسليم: ${dateFormat.format(data.order.revenueRecognizedAt!)}',
                        textAlign: pw.TextAlign.right,
                        style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.Text('إجمالي القطع: ${data.totalPieces}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.SizedBox(height: 8),
                  pw.TableHelper.fromTextArray(
                    headers: [
                      'القطعة',
                      'العدد',
                      'الكتالوج',
                      'كود القماش',
                      'نوع القماش',
                      'لون القماش'
                    ],
                    headerStyle: pw.TextStyle(
                        font: font,
                        fontSize: 8,
                        fontWeight: pw.FontWeight.bold),
                    cellStyle: pw.TextStyle(font: font, fontSize: 8),
                    data: data.items
                        .map((item) => [
                              item.pieceType,
                              '${item.quantity}',
                              item.catalogNumber ?? '-',
                              item.fabricCode ?? '-',
                              item.fabricType ?? '-',
                              item.fabricColor ?? '-'
                            ])
                        .toList(),
                  ),
                  pw.SizedBox(height: 8),
                  pw.Text(
                      'القيمة الإجمالية: ${NumberFormat('#,##0.00').format(data.order.totalAmount)}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.Text(
                      'الخصم: ${NumberFormat('#,##0.00').format(data.order.discountAmount)}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  if (finalInvoice)
                    pw.Text(
                        'المدفوع فعليًا: ${NumberFormat('#,##0.00').format(data.order.paidAmount)}',
                        textAlign: pw.TextAlign.right,
                        style: pw.TextStyle(font: font, fontSize: 10)),
                  if (!finalInvoice)
                    pw.Text(
                        'المدفوع مقدمًا: ${NumberFormat('#,##0.00').format(data.order.paidAmount)}',
                        textAlign: pw.TextAlign.right,
                        style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.Text(
                      'المتبقي: ${NumberFormat('#,##0.00').format(data.order.remainingAmount)}',
                      textAlign: pw.TextAlign.right,
                      style: pw.TextStyle(font: font, fontSize: 10)),
                  pw.SizedBox(height: 10),
                  pw.Container(
                    decoration: pw.BoxDecoration(
                        border: pw.Border.all(color: PdfColors.grey400)),
                    padding: const pw.EdgeInsets.all(7),
                    child: pw.Column(
                        crossAxisAlignment: pw.CrossAxisAlignment.stretch,
                        children: [
                          pw.Text(
                              finalInvoice
                                  ? 'شروط الفاتورة'
                                  : ' عزيزي العميل تنبه لهذه الشروط ',
                              textAlign: pw.TextAlign.right,
                              style: pw.TextStyle(
                                  font: font,
                                  fontSize: 11,
                                  fontWeight: pw.FontWeight.bold)),
                          pw.SizedBox(height: 4),
                          for (var i = 0; i < terms.length; i++)
                            pw.Padding(
                              padding: const pw.EdgeInsets.only(bottom: 3),
                              child: pw.Text('${i + 1}. ${terms[i]}',
                                  textAlign: pw.TextAlign.right,
                                  textDirection: pw.TextDirection.rtl,
                                  style:
                                      pw.TextStyle(font: font, fontSize: 8.5)),
                            ),
                        ]),
                  ),
                ]),
          ),
        ],
      ),
    );

    return pdf.save();
  }

  static Future<bool> _execute(BuildContext context, Uint8List bytes,
      String title, String baseName) async {
    final target =
        await chooseTarget(context, finalInvoice: title.contains('الفاتورة'));
    if (target == null) return false;
    if (target == TailoringDocumentTarget.officePrinter) {
      await Printing.layoutPdf(onLayout: (_) => bytes);
      return true;
    }
    final defaultDirectory = Directory(r'D:\تجارب طباعة المقاسات والمستندات');
    if (!await defaultDirectory.exists()) {
      await defaultDirectory.create(recursive: true);
    }
    final fullPath =
        '${defaultDirectory.path}${Platform.pathSeparator}$baseName.pdf';
    await File(fullPath).writeAsBytes(bytes);
    if (!context.mounted) return true;
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(
      SnackBar(
          content: Text(title.contains('الفاتورة')
              ? 'تم حفظ الفاتورة النهائية بصيغة PDF بنجاح.'
              : 'تم حفظ الإيصال بصيغة PDF بنجاح.')),
    );
    return true;
  }
}
