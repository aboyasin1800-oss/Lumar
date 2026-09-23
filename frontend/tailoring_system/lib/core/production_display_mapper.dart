class ProductionDisplayMapper {
  static const Map<String, String> _pieceTypeArabicMap = {
    'KOT': 'كوت',
    'KOT SMALL': 'كوت صغير',
    'KOT MEDIUM': 'كوت متوسط',
    'KOT LARGE': 'كوت كبير',
    'KOT HUGE': 'كوت ضخم',
    'PANTS': 'بنطلون',
    'PANT': 'بنطلون',
    'SHIRT': 'قميص',
    'THOBE': 'ثوب',
    'THOBE_QATARI': 'ثوب قطري',
    'QAMEES': 'قمصان',
    'JACKET': 'جاكيت',
    'BLAZER': 'بليزر',
    'MANTLE': 'بالطو',
    'APRON': 'مريلة',
    'TSHIRT': 'تيشيرت',
    'TROUSERS': 'سروال',
    'CAP': 'طاقية',
    'SPORT': 'ملابس رياضية',
    'UNIFORM': 'يونيفورم',
  };

  static const Map<String, String> _stageArabicMap = {
    'Printing': 'الطباعة',
    'FabricPrep': 'تجهيز القماش',
    'Cutting': 'القص',
    'Sewing': 'الخياطة',
    'Buttons': 'الأزرار',
    'Ironing': 'الكي',
    'Quality': 'الجودة',
    'Assembly': 'التجميع',
    'Ready': 'جاهز',
    'Delivery': 'التسليم',
    'New': 'جديدة',
  };

  static bool isValidPieceTypeCode(String? value) {
    final raw = (value ?? '').trim();
    if (raw.isEmpty || raw == '???' || raw == '?' || raw.contains('?')) {
      return false;
    }

    if (raw.toLowerCase().startsWith('readymade:')) {
      return false;
    }

    if (raw.toLowerCase().contains('fabric') ||
        raw.toLowerCase().contains('sku') ||
        raw.toLowerCase().contains('code') ||
        raw.toLowerCase().contains('item') && raw.contains(':')) {
      return false;
    }

    return true;
  }

  static String pieceTypeLabel(String? value) {
    final raw = (value ?? '').trim();
    if (!isValidPieceTypeCode(raw)) {
      return '';
    }

    final normalizedKey = _normalizePieceTypeKey(raw);
    return _pieceTypeArabicMap[normalizedKey] ??
        _pieceTypeArabicMap[normalizedKey.toUpperCase()] ??
        _normalizeArabicText(raw);
  }

  static String stageLabel(String? stage) {
    final raw = (stage ?? '').trim();
    if (raw.isEmpty) return '';
    return _stageArabicMap[raw] ?? _stageArabicMap[_normalizeStageKey(raw)] ?? raw;
  }

  static String routeLabel(List<String> stages) {
    return stages
        .map(stageLabel)
        .where((value) => value.isNotEmpty)
        .join(' ← ');
  }

  static String _normalizePieceTypeKey(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return '';
    final withoutUnderscore = trimmed.replaceAll('_', ' ').replaceAll('-', ' ');
    final compact = withoutUnderscore.replaceAll(RegExp(r'\s+'), ' ').trim();
    final upper = compact.toUpperCase();

    if (upper == 'THOBE QATARI') return 'THOBE_QATARI';
    if (upper == 'THOBE') return 'THOBE';
    if (upper == 'QAMEES') return 'QAMEES';
    if (upper == 'MANTLE') return 'MANTLE';
    if (upper == 'KOT LARGE') return 'KOT LARGE';
    if (upper == 'KOT HUGE') return 'KOT HUGE';
    if (upper == 'KOT MEDIUM') return 'KOT MEDIUM';
    if (upper == 'KOT SMALL') return 'KOT SMALL';
    return upper;
  }

  static String _normalizeStageKey(String value) {
    final raw = value.trim();
    final upper = raw.toUpperCase();
    switch (upper) {
      case 'PRINTING':
        return 'Printing';
      case 'FABRICPREP':
      case 'FABRIC_PREP':
      case 'FABRIC PREP':
        return 'FabricPrep';
      case 'CUTTING':
        return 'Cutting';
      case 'SEWING':
        return 'Sewing';
      case 'BUTTONS':
        return 'Buttons';
      case 'IRONING':
        return 'Ironing';
      case 'QUALITY':
        return 'Quality';
      case 'ASSEMBLY':
        return 'Assembly';
      default:
        return raw;
    }
  }

  static String _normalizeArabicText(String value) {
    final trimmed = value.trim();
    final lower = trimmed.toLowerCase();
    if (lower.contains('kot')) return 'كوت';
    if (lower.contains('pants')) return 'بنطلون';
    if (lower.contains('shirt')) return 'قميص';
    if (lower.contains('thobe')) return 'ثوب';
    if (lower.contains('qatari')) return 'ثوب قطري';
    if (lower.contains('jacket')) return 'جاكيت';
    if (lower.contains('blazer')) return 'بليزر';
    if (lower.contains('apron')) return 'مريلة';
    if (lower.contains('cap')) return 'طاقية';
    return trimmed;
  }
}
