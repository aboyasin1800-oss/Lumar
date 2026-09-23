import 'package:flutter/material.dart';

import '../../core/ui_palette.dart';

class PrintTemplateCatalogScreen extends StatelessWidget {
  const PrintTemplateCatalogScreen({super.key});

  static const List<_PrintTemplateDefinition> _templates = [
    _PrintTemplateDefinition(
      id: 'measurement-card',
      title: 'تصميم بطاقة المقاسات',
      codeName: 'Measurement Card Template',
      pageWidth: '216',
      pageHeight: '420',
      orientation: 'Portrait',
      margins: '6',
      fontSize: '11',
      fontName: 'Tahoma',
      useHeader: true,
      useHeaderImage: false,
      companyName: 'LUMAR ERP',
      address: 'مركز الطباعة',
      phones: '0555 123 456',
      borderColor: '#000000',
      borderThickness: '1.3',
      notes: 'مستودع تصميم بطاقة المقاسات. غير مرتبط بالطباعة الحالية.',
    ),
    _PrintTemplateDefinition(
      id: 'invoice',
      title: 'تصميم الفاتورة',
      codeName: 'Invoice Template',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: true,
      useHeaderImage: true,
      companyName: 'LUMAR ERP',
      address: 'العنوان التجاري',
      phones: '0555 123 456',
      borderColor: '#1F2937',
      borderThickness: '0.8',
      notes: 'مستودع تصميم الفاتورة. غير مرتبط بعد بتوليد PDF.',
    ),
    _PrintTemplateDefinition(
      id: 'receipt-voucher',
      title: 'تصميم سند القبض',
      codeName: 'Receipt Voucher Template',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: true,
      useHeaderImage: false,
      companyName: 'LUMAR ERP',
      address: 'سند قبض',
      phones: '0555 123 456',
      borderColor: '#374151',
      borderThickness: '1',
      notes: 'مستودع تصميم سند القبض. غير مرتبط بعد.',
    ),
    _PrintTemplateDefinition(
      id: 'payment-voucher',
      title: 'تصميم سند الصرف',
      codeName: 'Payment Voucher Template',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: true,
      useHeaderImage: false,
      companyName: 'LUMAR ERP',
      address: 'سند صرف',
      phones: '0555 123 456',
      borderColor: '#374151',
      borderThickness: '1',
      notes: 'مستودع تصميم سند الصرف. غير مرتبط بعد.',
    ),
    _PrintTemplateDefinition(
      id: 'order-receipt',
      title: 'تصميم إيصال الطلب',
      codeName: 'Order Receipt Template',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: true,
      useHeaderImage: false,
      companyName: 'LUMAR ERP',
      address: 'إيصال الطلب',
      phones: '0555 123 456',
      borderColor: '#111827',
      borderThickness: '1',
      notes: 'مستودع تصميم إيصال الطلب. غير مرتبط بعد.',
    ),
    _PrintTemplateDefinition(
      id: 'template-06',
      title: 'Template 06',
      codeName: 'Template 06',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: false,
      useHeaderImage: false,
      companyName: 'مؤسسة احتياطية',
      address: 'قالب احتياطي 06',
      phones: '',
      borderColor: '#6B7280',
      borderThickness: '0.8',
      notes: 'قالب احتياطي مستقبلية. غير مرتبط بعد.',
    ),
    _PrintTemplateDefinition(
      id: 'template-07',
      title: 'Template 07',
      codeName: 'Template 07',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: false,
      useHeaderImage: false,
      companyName: 'مؤسسة احتياطية',
      address: 'قالب احتياطي 07',
      phones: '',
      borderColor: '#6B7280',
      borderThickness: '0.8',
      notes: 'قالب احتياطي مستقبلية. غير مرتبط بعد.',
    ),
    _PrintTemplateDefinition(
      id: 'template-08',
      title: 'Template 08',
      codeName: 'Template 08',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      useHeader: false,
      useHeaderImage: false,
      companyName: 'مؤسسة احتياطية',
      address: 'قالب احتياطي 08',
      phones: '',
      borderColor: '#6B7280',
      borderThickness: '0.8',
      notes: 'قالب احتياطي مستقبلية. غير مرتبط بعد.',
    ),
  ];

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          title: const Text('إدارة قوالب الطباعة'),
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        ),
        body: ListView(
          padding: const EdgeInsets.all(18),
          children: [
            Text(
              'مستودع التصاميم للطباعة',
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 20,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 12),
            Text(
              'هذه القوالب مجرد تصميمات مسجلة داخل إعدادات LUMAR ERP فقط، ولا ترتبط حاليًا بأي PDF أو طباعة فعليّة.',
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.screenBackground,
                fontSize: 13,
              ),
            ),
            const SizedBox(height: 18),
            ..._templates.map((template) => _TemplateCard(template: template)),
          ],
        ),
      ),
    );
  }
}

class _TemplateCard extends StatelessWidget {
  const _TemplateCard({required this.template});

  final _PrintTemplateDefinition template;

  @override
  Widget build(BuildContext context) {
    final settings = <_SettingRow>[
      _SettingRow('عرض الصفحة', '${template.pageWidth} مم'),
      _SettingRow('ارتفاع الصفحة', '${template.pageHeight} مم'),
      _SettingRow('الاتجاه', template.orientation),
      _SettingRow('الهوامش', '${template.margins} مم'),
      _SettingRow('حجم الخط', template.fontSize),
      _SettingRow('اسم الخط', template.fontName),
      _SettingRow('استخدام رأس المستند', template.useHeader ? 'نعم' : 'لا'),
      _SettingRow('عرض صورة الرأس', template.useHeaderImage ? 'نعم' : 'لا'),
      _SettingRow('اسم المؤسسة', template.companyName),
      _SettingRow('العنوان', template.address),
      _SettingRow('الهواتف', template.phones.isEmpty ? 'غير محدد' : template.phones),
      _SettingRow('لون الحدود', template.borderColor),
      _SettingRow('سمك الحدود', template.borderThickness),
    ];

    return Container(
      margin: const EdgeInsets.only(bottom: 14),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        border: Border.all(color: UiPalette.borderSoft),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    template.title,
                    style: UiPalette.adaptiveTextStyle(
                      context,
                      backgroundColor: UiPalette.surfaceCard,
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: UiPalette.softBlue.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(20),
                    border: Border.all(color: UiPalette.primaryBlue.withValues(alpha: 0.35)),
                  ),
                  child: Text(
                    template.codeName,
                    style: UiPalette.adaptiveTextStyle(
                      context,
                      backgroundColor: UiPalette.surfaceCard,
                      fontSize: 11,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              template.notes,
              style: UiPalette.adaptiveTextStyle(
                context,
                backgroundColor: UiPalette.surfaceCard,
                fontSize: 12,
              ),
            ),
            const SizedBox(height: 14),
            Wrap(
              spacing: 12,
              runSpacing: 10,
              children: settings
                  .map(
                    (item) => Container(
                      constraints: const BoxConstraints(minWidth: 150),
                      child: Text(
                        '${item.label}: ${item.value}',
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor: UiPalette.surfaceCard,
                          fontSize: 12,
                        ),
                      ),
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }
}

class _PrintTemplateDefinition {
  const _PrintTemplateDefinition({
    required this.id,
    required this.title,
    required this.codeName,
    required this.pageWidth,
    required this.pageHeight,
    required this.orientation,
    required this.margins,
    required this.fontSize,
    required this.fontName,
    required this.useHeader,
    required this.useHeaderImage,
    required this.companyName,
    required this.address,
    required this.phones,
    required this.borderColor,
    required this.borderThickness,
    required this.notes,
  });

  final String id;
  final String title;
  final String codeName;
  final String pageWidth;
  final String pageHeight;
  final String orientation;
  final String margins;
  final String fontSize;
  final String fontName;
  final bool useHeader;
  final bool useHeaderImage;
  final String companyName;
  final String address;
  final String phones;
  final String borderColor;
  final String borderThickness;
  final String notes;
}

class _SettingRow {
  const _SettingRow(this.label, this.value);

  final String label;
  final String value;
}
