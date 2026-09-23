import 'package:flutter/material.dart';

import '../core/ui_palette.dart';

class LumarErpLogoShowcaseScreen extends StatefulWidget {
  const LumarErpLogoShowcaseScreen({super.key});

  @override
  State<LumarErpLogoShowcaseScreen> createState() =>
      _LumarErpLogoShowcaseScreenState();
}

class _LumarErpLogoShowcaseScreenState
    extends State<LumarErpLogoShowcaseScreen> {
  final List<_DesignTemplate> _templates = [
    _DesignTemplate(
      title: 'بطاقة المقاسات',
      codeName: 'Measurement Card',
      pageWidth: '216',
      pageHeight: '420',
      orientation: 'Portrait',
      margins: '6',
      fontSize: '11',
      fontName: 'Tahoma',
      companyName: 'LUMAR ERP',
      address: 'مؤسسة لومار',
      phones: '966555123456',
      borderColor: '#D7AF58',
      borderThickness: '1.3',
      notes: 'قالب بطاقة المقاسات الرئيسية.',
    ),
    _DesignTemplate(
      title: 'الفاتورة',
      codeName: 'Invoice',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'LUMAR ERP',
      address: 'العنوان الرئيسي',
      phones: '966555123456',
      borderColor: '#1F2937',
      borderThickness: '0.8',
      notes: 'قالب الفاتورة التجاري.',
    ),
    _DesignTemplate(
      title: 'سند القبض',
      codeName: 'Receipt Voucher',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'LUMAR ERP',
      address: 'سند قبض',
      phones: '966555123456',
      borderColor: '#374151',
      borderThickness: '1',
      notes: 'قالب سند القبض.',
    ),
    _DesignTemplate(
      title: 'سند الصرف',
      codeName: 'Payment Voucher',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'LUMAR ERP',
      address: 'سند صرف',
      phones: '966555123456',
      borderColor: '#374151',
      borderThickness: '1',
      notes: 'قالب سند الصرف.',
    ),
    _DesignTemplate(
      title: 'إيصال الطلب',
      codeName: 'Order Receipt',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'LUMAR ERP',
      address: 'إيصال الطلب',
      phones: '966555123456',
      borderColor: '#111827',
      borderThickness: '1',
      notes: 'قالب إيصال الطلب.',
    ),
    _DesignTemplate(
      title: 'قالب احتياطي 01',
      codeName: 'Backup 01',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'قالب احتياطي',
      address: 'احتياطي 01',
      phones: '',
      borderColor: '#6B7280',
      borderThickness: '0.8',
      notes: 'قالب احتياطي للمستقبل.',
    ),
    _DesignTemplate(
      title: 'قالب احتياطي 02',
      codeName: 'Backup 02',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'قالب احتياطي',
      address: 'احتياطي 02',
      phones: '',
      borderColor: '#6B7280',
      borderThickness: '0.8',
      notes: 'قالب احتياطي للمستقبل.',
    ),
    _DesignTemplate(
      title: 'قالب احتياطي 03',
      codeName: 'Backup 03',
      pageWidth: '210',
      pageHeight: '297',
      orientation: 'Portrait',
      margins: '8',
      fontSize: '10',
      fontName: 'Tahoma',
      companyName: 'قالب احتياطي',
      address: 'احتياطي 03',
      phones: '',
      borderColor: '#6B7280',
      borderThickness: '0.8',
      notes: 'قالب احتياطي للمستقبل.',
    ),
  ];

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(
          backgroundColor: UiPalette.surfaceCard,
          foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
          title: const Text('LUMAR ERP'),
        ),
        body: ListView(
          padding: const EdgeInsets.all(18),
          children: [
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(18),
                border: Border.all(color: UiPalette.borderSoft),
              ),
              child: Row(
                children: [
                  Container(
                    width: 82,
                    height: 82,
                    decoration: BoxDecoration(
                      color: const Color(0xFF10314C),
                      borderRadius: BorderRadius.circular(18),
                      border: Border.all(
                        color: const Color(0xFFD7AF58),
                        width: 2,
                      ),
                    ),
                    child: const Icon(
                      Icons.auto_awesome_outlined,
                      size: 42,
                      color: Color(0xFFD7AF58),
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'مصدر تصميمات الطباعة',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 22,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'كل قالب مستقل ويمكن تعديله مباشرة هنا دون الربط الحالي بأي طباعة.',
                          style: UiPalette.adaptiveTextStyle(
                            context,
                            backgroundColor: UiPalette.surfaceCard,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            ..._templates.map((template) => _TemplateEditorCard(template: template)),
          ],
        ),
      ),
    );
  }
}

class _TemplateEditorCard extends StatefulWidget {
  const _TemplateEditorCard({required this.template});

  final _DesignTemplate template;

  @override
  State<_TemplateEditorCard> createState() => _TemplateEditorCardState();
}

class _TemplateEditorCardState extends State<_TemplateEditorCard> {
  late final TextEditingController _titleController;
  late final TextEditingController _codeNameController;
  late final TextEditingController _pageWidthController;
  late final TextEditingController _pageHeightController;
  late final TextEditingController _marginsController;
  late final TextEditingController _fontSizeController;
  late final TextEditingController _companyNameController;
  late final TextEditingController _addressController;
  late final TextEditingController _phonesController;
  late final TextEditingController _borderColorController;
  late final TextEditingController _borderThicknessController;
  late final TextEditingController _notesController;

  final List<String> _fontOptions = const [
    'Tahoma',
    'Arial',
    'Calibri',
    'Segoe UI',
    'Times New Roman',
    'Helvetica',
  ];

  final List<String> _orientationOptions = const ['Portrait', 'Landscape'];

  @override
  void initState() {
    super.initState();
    final template = widget.template;
    _titleController = TextEditingController(text: template.title);
    _codeNameController = TextEditingController(text: template.codeName);
    _pageWidthController = TextEditingController(text: template.pageWidth);
    _pageHeightController = TextEditingController(text: template.pageHeight);
    _marginsController = TextEditingController(text: template.margins);
    _fontSizeController = TextEditingController(text: template.fontSize);
    _companyNameController = TextEditingController(text: template.companyName);
    _addressController = TextEditingController(text: template.address);
    _phonesController = TextEditingController(text: template.phones);
    _borderColorController = TextEditingController(text: template.borderColor);
    _borderThicknessController =
        TextEditingController(text: template.borderThickness);
    _notesController = TextEditingController(text: template.notes);
  }

  @override
  void dispose() {
    _titleController.dispose();
    _codeNameController.dispose();
    _pageWidthController.dispose();
    _pageHeightController.dispose();
    _marginsController.dispose();
    _fontSizeController.dispose();
    _companyNameController.dispose();
    _addressController.dispose();
    _phonesController.dispose();
    _borderColorController.dispose();
    _borderThicknessController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final fields = <_FormFieldConfig>[
      _FormFieldConfig(
        label: 'اسم القالب',
        controller: _titleController,
        onChanged: (value) => widget.template.title = value,
      ),
      _FormFieldConfig(
        label: 'اسم التصميم',
        controller: _codeNameController,
        onChanged: (value) => widget.template.codeName = value,
      ),
      _FormFieldConfig(
        label: 'عرض الصفحة',
        controller: _pageWidthController,
        onChanged: (value) => widget.template.pageWidth = value,
      ),
      _FormFieldConfig(
        label: 'ارتفاع الصفحة',
        controller: _pageHeightController,
        onChanged: (value) => widget.template.pageHeight = value,
      ),
      _FormFieldConfig(
        label: 'الإتجاه',
        controller: null,
        value: widget.template.orientation,
        type: _FieldType.dropdown,
        options: _orientationOptions,
        onChanged: (value) => widget.template.orientation = value,
      ),
      _FormFieldConfig(
        label: 'الهامش',
        controller: _marginsController,
        onChanged: (value) => widget.template.margins = value,
      ),
      _FormFieldConfig(
        label: 'حجم الخط',
        controller: _fontSizeController,
        onChanged: (value) => widget.template.fontSize = value,
      ),
      _FormFieldConfig(
        label: 'نوع الخط',
        controller: null,
        value: widget.template.fontName,
        type: _FieldType.dropdown,
        options: _fontOptions,
        onChanged: (value) => widget.template.fontName = value,
      ),
      _FormFieldConfig(
        label: 'اسم المؤسسة',
        controller: _companyNameController,
        onChanged: (value) => widget.template.companyName = value,
      ),
      _FormFieldConfig(
        label: 'العنوان',
        controller: _addressController,
        onChanged: (value) => widget.template.address = value,
      ),
      _FormFieldConfig(
        label: 'الهواتف',
        controller: _phonesController,
        onChanged: (value) => widget.template.phones = value,
      ),
      _FormFieldConfig(
        label: 'لون الحدود',
        controller: _borderColorController,
        onChanged: (value) => widget.template.borderColor = value,
      ),
      _FormFieldConfig(
        label: 'سمك الحدود',
        controller: _borderThicknessController,
        onChanged: (value) => widget.template.borderThickness = value,
      ),
      _FormFieldConfig(
        label: 'ملاحظات',
        controller: _notesController,
        onChanged: (value) => widget.template.notes = value,
      ),
    ];

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            flex: 1,
            child: Padding(
              padding: const EdgeInsets.only(left: 12, right: 6),
              child: _DocumentPreview(template: widget.template),
            ),
          ),
          const SizedBox(width: 18),
          Expanded(
            flex: 1,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Container(
                      width: 42,
                      height: 42,
                      decoration: BoxDecoration(
                        color: UiPalette.softBlue.withValues(alpha: 0.18),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Icon(
                        Icons.design_services_outlined,
                        color: UiPalette.primaryBlue,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        widget.template.title,
                        style: UiPalette.adaptiveTextStyle(
                          context,
                          backgroundColor: UiPalette.surfaceCard,
                          fontSize: 20,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                GridView.builder(
                  shrinkWrap: true,
                  physics: const NeverScrollableScrollPhysics(),
                  gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: 2,
                    crossAxisSpacing: 12,
                    mainAxisSpacing: 12,
                    childAspectRatio: 2.6,
                  ),
                  itemCount: fields.length,
                  itemBuilder: (context, index) {
                    final field = fields[index];
                    if (field.type == _FieldType.dropdown) {
                      return DropdownButtonFormField<String>(
                        value: field.options.contains(field.value) ? field.value : field.options.first,
                        decoration: InputDecoration(
                          labelText: field.label,
                          border: const OutlineInputBorder(),
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 10,
                          ),
                        ),
                        items: field.options
                            .map(
                              (option) => DropdownMenuItem(
                                value: option,
                                child: Text(option),
                              ),
                            )
                            .toList(),
                        onChanged: (value) {
                          if (value != null) {
                            field.onChanged(value);
                            setState(() {});
                          }
                        },
                      );
                    }

                    return TextFormField(
                      controller: field.controller,
                      textDirection: TextDirection.rtl,
                      decoration: InputDecoration(
                        labelText: field.label,
                        border: const OutlineInputBorder(),
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 10,
                        ),
                      ),
                      onChanged: field.onChanged,
                    );
                  },
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _DocumentPreview extends StatelessWidget {
  const _DocumentPreview({required this.template});

  final _DesignTemplate template;

  @override
  Widget build(BuildContext context) {
    final borderColor = _parseColor(template.borderColor) ?? const Color(0xFFD7AF58);
    final isMeasurement = template.title.contains('بطاقة المقاسات') ||
        template.codeName.contains('Measurement');
    final isInvoice = template.title.contains('الفاتورة') ||
        template.codeName.contains('Invoice');
    final isReceipt = template.title.contains('سند القبض') ||
        template.codeName.contains('Receipt');
    final isPayment = template.title.contains('سند الصرف') ||
        template.codeName.contains('Payment');
    final isOrder = template.title.contains('إيصال الطلب') ||
        template.codeName.contains('Order');

    if (isMeasurement) {
      return _MeasurementCardPreview(template: template, borderColor: borderColor);
    }
    if (isInvoice) {
      return _InvoicePreview(template: template, borderColor: borderColor);
    }
    if (isReceipt || isPayment || isOrder) {
      return _VoucherPreview(
        template: template,
        borderColor: borderColor,
        title: template.title,
      );
    }
    return _GenericDocumentPreview(template: template, borderColor: borderColor);
  }

  Color? _parseColor(String value) {
    final clean = value.trim();
    if (clean.isEmpty) return null;
    if (clean.startsWith('#')) {
      final hex = clean.replaceAll('#', '');
      if (hex.length == 6) {
        return Color(int.parse('FF$hex', radix: 16));
      }
    }
    return null;
  }
}

class _MeasurementCardPreview extends StatelessWidget {
  const _MeasurementCardPreview({
    required this.template,
    required this.borderColor,
  });

  final _DesignTemplate template;
  final Color borderColor;

  @override
  Widget build(BuildContext context) {
    final fontScale = double.tryParse(template.fontSize) ?? 11;
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF0E1C2A),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: borderColor.withValues(alpha: 0.8), width: 2),
      ),
      child: Center(
        child: Container(
          width: 500,
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: borderColor, width: 2),
          ),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Row(
                    children: [
                      Container(
                        width: 52,
                        height: 52,
                        decoration: BoxDecoration(
                          color: const Color(0xFFF3E8C7),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Icon(
                          Icons.auto_awesome_outlined,
                          color: Color(0xFFC7922D),
                          size: 28,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          template.companyName,
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: fontScale + 7,
                            fontWeight: FontWeight.bold,
                            color: Colors.black87,
                            fontFamily: template.fontName,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: _MiniInfoCell(label: 'اسم القالب', value: template.title),
                    ),
                    Expanded(
                      child: _MiniInfoCell(label: 'اسم التصميم', value: template.codeName),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _MiniInfoCell(label: 'عرض الصفحة', value: template.pageWidth),
                    ),
                    Expanded(
                      child: _MiniInfoCell(label: 'ارتفاع الصفحة', value: template.pageHeight),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _MiniInfoCell(label: 'الإتجاه', value: template.orientation),
                    ),
                    Expanded(
                      child: _MiniInfoCell(label: 'حجم الخط', value: template.fontSize),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _MiniInfoCell(label: 'اسم المؤسسة', value: template.companyName),
                    ),
                    Expanded(
                      child: _MiniInfoCell(label: 'اسم الخط', value: template.fontName),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: _MiniInfoCell(label: 'الهواتف', value: template.phones),
                    ),
                    Expanded(
                      child: _MiniInfoCell(label: 'لون الحدود', value: template.borderColor),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                _MiniInfoCell(label: 'ملاحظات', value: template.notes),
                const SizedBox(height: 16),
                Container(
                  height: 58,
                  decoration: BoxDecoration(
                    color: const Color(0xFFF5F5F5),
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: borderColor.withValues(alpha: 0.4)),
                  ),
                  alignment: Alignment.center,
                  child: Text(
                    'معاينة مستند قياسات',
                    style: TextStyle(
                      fontSize: fontScale + 2,
                      fontWeight: FontWeight.w700,
                      color: Colors.black87,
                      fontFamily: template.fontName,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _InvoicePreview extends StatelessWidget {
  const _InvoicePreview({required this.template, required this.borderColor});

  final _DesignTemplate template;
  final Color borderColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF0E1C2A),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: borderColor.withValues(alpha: 0.8), width: 2),
      ),
      child: Center(
        child: Container(
          width: 520,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: Colors.white,
            border: Border.all(color: borderColor, width: 2),
            borderRadius: BorderRadius.circular(14),
          ),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Container(
                      width: 48,
                      height: 48,
                      decoration: BoxDecoration(
                        color: const Color(0xFFE7F4EC),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Icon(Icons.receipt_long_rounded, color: Colors.green),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        template.companyName,
                        style: TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                          fontFamily: template.fontName,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text('الفاتورة', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, fontFamily: template.fontName)),
                    Text('INV-001', style: TextStyle(fontSize: 14, fontFamily: template.fontName)),
                  ],
                ),
                const SizedBox(height: 8),
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF8FAFC),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Column(
                    children: [
                      _InvoiceRow(label: 'العميل', value: 'اسم العميل'),
                      _InvoiceRow(label: 'التاريخ', value: '12/09/2026'),
                      _InvoiceRow(label: 'الموقع', value: template.address),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: const [
                    Expanded(child: _InvoiceTableCell('الصنف')),
                    Expanded(child: _InvoiceTableCell('الكمية')),
                    Expanded(child: _InvoiceTableCell('السعر')),
                    Expanded(child: _InvoiceTableCell('الإجمالي')),
                  ],
                ),
                ...List.generate(
                  3,
                  (index) => Row(
                    children: [
                      Expanded(child: _InvoiceTableCell('قماش ${index + 1}')),
                      Expanded(child: _InvoiceTableCell('2')),
                      Expanded(child: _InvoiceTableCell('150')),
                      Expanded(child: _InvoiceTableCell('300')),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    'الإجمالي: 900 ريال',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      fontFamily: template.fontName,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _VoucherPreview extends StatelessWidget {
  const _VoucherPreview({
    required this.template,
    required this.borderColor,
    required this.title,
  });

  final _DesignTemplate template;
  final Color borderColor;
  final String title;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF0E1C2A),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: borderColor.withValues(alpha: 0.8), width: 2),
      ),
      child: Center(
        child: Container(
          width: 520,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: Colors.white,
            border: Border.all(color: borderColor, width: 2),
            borderRadius: BorderRadius.circular(14),
          ),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: Column(
              children: [
                Text(
                  title,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                    fontFamily: template.fontName,
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(template.companyName, style: TextStyle(fontFamily: template.fontName)),
                    Text('رقم السند: 0001', style: TextStyle(fontFamily: template.fontName)),
                  ],
                ),
                const SizedBox(height: 14),
                _VoucherBox(label: 'الاسم', value: 'اسم العميل'),
                const SizedBox(height: 8),
                _VoucherBox(label: 'المبلغ', value: '1200 ريال'),
                const SizedBox(height: 8),
                _VoucherBox(label: 'التاريخ', value: '12/09/2026'),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _GenericDocumentPreview extends StatelessWidget {
  const _GenericDocumentPreview({
    required this.template,
    required this.borderColor,
  });

  final _DesignTemplate template;
  final Color borderColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF0E1C2A),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: borderColor.withValues(alpha: 0.8), width: 2),
      ),
      child: Center(
        child: Container(
          width: 500,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: borderColor, width: 2),
          ),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  template.companyName,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                    fontFamily: template.fontName,
                  ),
                ),
                const SizedBox(height: 12),
                _MiniInfoCell(label: 'اسم القالب', value: template.title),
                const SizedBox(height: 8),
                _MiniInfoCell(label: 'اسم التصميم', value: template.codeName),
                const SizedBox(height: 8),
                _MiniInfoCell(label: 'العنوان', value: template.address),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _MiniInfoCell extends StatelessWidget {
  const _MiniInfoCell({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFFCBD5E1)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 10, color: Colors.black54)),
          const SizedBox(height: 4),
          Text(value, style: const TextStyle(fontSize: 12, color: Colors.black87, fontWeight: FontWeight.w600)),
        ],
      ),
    );
  }
}

class _InvoiceRow extends StatelessWidget {
  const _InvoiceRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
        Text(value),
      ],
    );
  }
}

class _InvoiceTableCell extends StatelessWidget {
  const _InvoiceTableCell(this.value);

  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(vertical: 10),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFCBD5E1)),
      ),
      child: Text(value),
    );
  }
}

class _VoucherBox extends StatelessWidget {
  const _VoucherBox({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        border: Border.all(color: const Color(0xFFCBD5E1)),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
          Text(value),
        ],
      ),
    );
  }
}

class _FormFieldConfig {
  const _FormFieldConfig({
    required this.label,
    required this.onChanged,
    this.controller,
    this.value,
    this.type = _FieldType.text,
    this.options = const [],
  });

  final String label;
  final TextEditingController? controller;
  final ValueChanged<String> onChanged;
  final String? value;
  final _FieldType type;
  final List<String> options;
}

enum _FieldType { text, dropdown }

class _DesignTemplate {
  _DesignTemplate({
    required this.title,
    required this.codeName,
    required this.pageWidth,
    required this.pageHeight,
    required this.orientation,
    required this.margins,
    required this.fontSize,
    required this.fontName,
    required this.companyName,
    required this.address,
    required this.phones,
    required this.borderColor,
    required this.borderThickness,
    required this.notes,
  });

  String title;
  String codeName;
  String pageWidth;
  String pageHeight;
  String orientation;
  String margins;
  String fontSize;
  String fontName;
  String companyName;
  String address;
  String phones;
  String borderColor;
  String borderThickness;
  String notes;
}
