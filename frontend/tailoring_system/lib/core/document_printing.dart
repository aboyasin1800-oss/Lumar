import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

class DocumentPrintHeader {
  const DocumentPrintHeader({
    required this.useImage,
    required this.imageValue,
    required this.name,
    required this.location,
    required this.phone1,
    required this.phone2,
  });

  factory DocumentPrintHeader.fromSettings(Map<String, String> entries) =>
      DocumentPrintHeader(
        useImage: _toBool(entries['MeasurementCardHeaderUseImage']),
        imageValue: entries['MeasurementCardHeaderImage'] ?? '',
        name: entries['MeasurementCardHeaderName']?.trim().isNotEmpty == true
            ? entries['MeasurementCardHeaderName']!.trim()
            : 'LUMAR',
        location: entries['MeasurementCardLocation']?.trim() ?? '',
        phone1: entries['MeasurementCardPhone1']?.trim() ?? '',
        phone2: entries['MeasurementCardPhone2']?.trim() ?? '',
      );

  final bool useImage;
  final String imageValue;
  final String name;
  final String location;
  final String phone1;
  final String phone2;
}

class MeasurementCardLayoutConfig {
  const MeasurementCardLayoutConfig({
    this.pageWidthInches = 3,
    this.pageHeightInches = 6,
    this.pageMarginMm = 4,
    this.headerImageHeight = 38,
    this.headerTitleSize = 13,
    this.headerMetaSize = 8,
    this.customerTextSize = 12,
    this.metaTextSize = 11,
    this.requestTextSize = 11.2,
    this.sectionTitleSize = 7,
    this.sectionPadding = 6,
    this.sectionSpacing = 4,
    this.outerBorderWidth = 1.3,
    this.innerBorderWidth = 0.8,
    this.dividerHeight = 0.7,
    this.qrSize = 60,
    this.barcodeWidth = 140,
    this.barcodeHeight = 30,
    this.measurementLabelSize = 6.5,
    this.measurementValueSize = 11.5,
    this.measurementCellPaddingY = 1,
    this.measurementCellPaddingX = 2,
    this.measurementTableBorderWidth = 0.3,
  });

  final double pageWidthInches;
  final double pageHeightInches;
  final double pageMarginMm;
  final double headerImageHeight;
  final double headerTitleSize;
  final double headerMetaSize;
  final double customerTextSize;
  final double metaTextSize;
  final double requestTextSize;
  final double sectionTitleSize;
  final double sectionPadding;
  final double sectionSpacing;
  final double outerBorderWidth;
  final double innerBorderWidth;
  final double dividerHeight;
  final double qrSize;
  final double barcodeWidth;
  final double barcodeHeight;
  final double measurementLabelSize;
  final double measurementValueSize;
  final double measurementCellPaddingY;
  final double measurementCellPaddingX;
  final double measurementTableBorderWidth;
}

class DocumentPrintSupport {
  static const MeasurementCardLayoutConfig layout = MeasurementCardLayoutConfig();

  static double a5WidthMm = 148;
  static double a5HeightMm = 210;
  static double a5MarginMm = 8;

  static double measurementCardWidthInches = layout.pageWidthInches;
  static double measurementCardHeightInches = layout.pageHeightInches;
  static double measurementCardMarginMm = layout.pageMarginMm;

  static PdfPageFormat get a5Portrait => PdfPageFormat(
        a5WidthMm * PdfPageFormat.mm,
        a5HeightMm * PdfPageFormat.mm,
        marginAll: a5MarginMm * PdfPageFormat.mm,
      );

  static PdfPageFormat get measurementCardPortrait => PdfPageFormat(
        measurementCardWidthInches * PdfPageFormat.inch,
        measurementCardHeightInches * PdfPageFormat.inch,
        marginAll: measurementCardMarginMm * PdfPageFormat.mm,
      );

  static String firstAvailableString(Iterable<dynamic> values) {
    for (final value in values) {
      final text = value?.toString().trim();
      if (text != null && text.isNotEmpty) {
        return text;
      }
    }
    return '';
  }

  static List<List<MapEntry<String, dynamic>>> buildMeasurementGridRows(
    Map<String, dynamic> measurements,
  ) {
    final entries = measurements.entries
        .where((entry) =>
            entry.value != null && entry.value.toString().trim().isNotEmpty)
        .toList();

    final rows = <List<MapEntry<String, dynamic>>>[];
    for (var index = 0; index < entries.length; index += 6) {
      final end = index + 6;
      rows.add(entries.sublist(index, end > entries.length ? entries.length : end));
    }
    return rows;
  }

  static Future<pw.Font> loadArabicFont() async {
    final fontData = await rootBundle.load('assets/fonts/Tahoma.ttf');
    return pw.Font.ttf(fontData);
  }

  static Future<DocumentPrintHeader> loadHeaderSettings(String baseUrl) async {
    const keys = [
      'MeasurementCardHeaderImage',
      'MeasurementCardHeaderUseImage',
      'MeasurementCardHeaderName',
      'MeasurementCardLocation',
      'MeasurementCardPhone1',
      'MeasurementCardPhone2',
    ];
    final entries = <String, String>{};
    for (final key in keys) {
      try {
        final response = await http.get(
          Uri.parse('$baseUrl/settings/by-key/${Uri.encodeComponent(key)}'),
        );
        if (response.statusCode < 200 || response.statusCode >= 300) continue;
        final decoded = jsonDecode(response.body);
        if (decoded is Map<String, dynamic>) {
          entries[key] = (decoded['settingValue'] ?? '').toString();
        }
      } catch (_) {
        // Printing remains available with the text fallback when settings are unavailable.
      }
    }
    return DocumentPrintHeader.fromSettings(entries);
  }

  static Future<pw.Widget> buildHeader(
    DocumentPrintHeader header,
    pw.Font font, {
    double imageHeight = 44,
  }) async {
    if (header.useImage) {
      final bytes = await _readImageBytes(header.imageValue);
      if (bytes != null && bytes.isNotEmpty) {
        return pw.Directionality(
          textDirection: pw.TextDirection.rtl,
          child: pw.Container(
            height: imageHeight,
            alignment: pw.Alignment.center,
            child: pw.Image(pw.MemoryImage(bytes), fit: pw.BoxFit.contain),
          ),
        );
      }
    }

    final phones = [header.phone1, header.phone2]
        .where((phone) => phone.trim().isNotEmpty)
        .join(' • ');
    return pw.Directionality(
      textDirection: pw.TextDirection.rtl,
      child: pw.Column(
        crossAxisAlignment: pw.CrossAxisAlignment.stretch,
        children: [
          pw.Text(
            header.name,
            textAlign: pw.TextAlign.center,
            style: pw.TextStyle(
              font: font,
              fontSize: 13,
              fontWeight: pw.FontWeight.bold,
            ),
          ),
          if (header.location.isNotEmpty)
            pw.Text(
              header.location,
              textAlign: pw.TextAlign.center,
              style: pw.TextStyle(font: font, fontSize: 8),
            ),
          if (phones.isNotEmpty)
            pw.Text(
              phones,
              textAlign: pw.TextAlign.center,
              style: pw.TextStyle(font: font, fontSize: 8),
            ),
        ],
      ),
    );
  }

  static String paymentTypeLabel(Object? value) {
    final normalized = value?.toString().trim().toLowerCase() ?? '';
    return switch (normalized) {
      'cash' || 'نقداً' || 'نقدا' => 'نقداً',
      'credit' || 'onaccount' || 'آجل' => 'آجل',
      'donation' || 'تبرعاً' || 'تبرعا' => 'تبرعاً',
      _ when normalized.isNotEmpty => value.toString(),
      _ => 'غير محدد',
    };
  }

  static Future<Uint8List?> _readImageBytes(String imageValue) async {
    final safe = imageValue.trim();
    if (safe.isEmpty) return null;
    try {
      if (safe.startsWith('data:image')) {
        return base64Decode(safe.split(',').last);
      }
      if (safe.startsWith('http')) {
        final response = await http.get(Uri.parse(safe));
        if (response.statusCode >= 200 && response.statusCode < 300) {
          return response.bodyBytes;
        }
        return null;
      }
      if (safe.startsWith('assets/')) {
        final data = await rootBundle.load(safe);
        return data.buffer.asUint8List();
      }
    } catch (_) {
      return null;
    }
    return null;
  }
}

bool _toBool(String? value) {
  final normalized = (value ?? '').trim().toLowerCase();
  return normalized == 'true' || normalized == '1' || normalized == 'yes';
}