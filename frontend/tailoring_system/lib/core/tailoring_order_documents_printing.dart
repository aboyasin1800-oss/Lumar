import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import '../models/order_models.dart';
import '../repositories/order_repository.dart';
import 'document_printing.dart';

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

  static Future<TailoringDocumentTarget?> chooseTarget(BuildContext context,
      {required bool finalInvoice}) {
    return showModalBottomSheet<TailoringDocumentTarget>(
      context: context,
      backgroundColor: const Color(0xFFFFFFFF),
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.print_outlined),
              title: const Text('طباعة على طابعة مكتبية'),
              onTap: () => Navigator.of(sheetContext)
                  .pop(TailoringDocumentTarget.officePrinter),
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

  static Future<void> printInitialReceipt(
      BuildContext context, int orderId) async {
    final data = await load(orderId);
    final bytes = await _buildDocument(data, finalInvoice: false);
    await _execute(context, bytes, 'إيصال طلب تفصيل',
        'tailoring_receipt_${data.order.number}');
  }

  static Future<void> printFinalInvoice(
      BuildContext context, int orderId) async {
    final data = await load(orderId);
    if (data.order.status.toLowerCase() != 'delivered') {
      throw StateError('لا يمكن طباعة الفاتورة قبل نجاح تسليم الطلب.');
    }
    final bytes = await _buildDocument(data, finalInvoice: true);
    await _execute(context, bytes, 'الفاتورة النهائية لطلب تفصيل',
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

    pdf.addPage(
      pw.MultiPage(
        pageFormat: DocumentPrintSupport.a5Portrait,
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
                ]),
          ),
        ],
      ),
    );

    return pdf.save();
  }

  static Future<void> _execute(BuildContext context, Uint8List bytes,
      String title, String baseName) async {
    final target =
        await chooseTarget(context, finalInvoice: title.contains('الفاتورة'));
    if (target == null) return;
    if (target == TailoringDocumentTarget.officePrinter) {
      await Printing.layoutPdf(onLayout: (_) => bytes);
      return;
    }
    final defaultDirectory = Directory(r'D:\تجارب طباعة المقاسات والمستندات');
    if (!await defaultDirectory.exists()) {
      await defaultDirectory.create(recursive: true);
    }
    final fullPath =
        '${defaultDirectory.path}${Platform.pathSeparator}$baseName.pdf';
    await File(fullPath).writeAsBytes(bytes);
    if (!context.mounted) return;
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(
      SnackBar(content: Text('تم حفظ الإيصال بصيغة PDF بنجاح.')),
    );
  }
}
