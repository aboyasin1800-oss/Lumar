class RlUiText {
  static const Map<String, String> _translations = {
    'Registration': 'تسجيل إحالة',
    'RewardGranted': 'منح مكافأة إحالة',
    'RewardReversal': 'عكس مكافأة إحالة',
    'Earn': 'اكتساب نقاط',
    'Adjust': 'تعديل نقاط',
    'Redeem': 'استبدال نقاط',
    'Reversal': 'عكس نقاط',
    'PiecePurchase': 'شراء قطع',
    'OrderLoyaltyCredit': 'رصيد ولاء للطلب',
    'PieceOrderCancellation': 'إلغاء نقاط طلب',
    'FixedAmount': 'مبلغ ثابت',
    'LoyaltyPoints': 'نقاط ولاء',
    'Bonus': 'مكافأة إضافية',
    'Active': 'نشط',
    'Inactive': 'غير نشط',
    'Applied': 'مطبق',
    'Reversed': 'معكوس',
    'Pending': 'قيد الانتظار',
    'Enabled': 'مفعّل',
    'Disabled': 'معطّل',
    'Reward': 'مكافأة',
    'Referral': 'إحالة',
    'Loyalty': 'ولاء',
    'VIP': 'كبار العملاء',
    'Points': 'النقاط',
    'Point': 'نقطة',
    'Success': 'نجاح',
    'Failed': 'فشل',
    'Error': 'خطأ',
    'Loading': 'جارٍ التحميل',
    'No data': 'لا توجد بيانات',
    'Unknown': 'غير معروف',
    'Default Points Per Order': 'نقاط افتراضية لكل طلب',
    'Default Points Per Spending': 'نقاط افتراضية حسب الإنفاق',
    'Default Bonus Rule': 'قاعدة المكافأة الافتراضية',
    'PointsPerOrder': 'نقاط لكل طلب',
    'PointsPerSpending': 'نقاط حسب الإنفاق',
  };

  static String translate(String? value,
      {String fallback = 'حالة غير معروفة'}) {
    if (value == null || value.trim().isEmpty) return fallback;

    final normalized = value.trim();
    final direct = _translations[normalized];
    if (direct != null) return direct;

    final lower = normalized.toLowerCase();
    for (final entry in _translations.entries) {
      if (entry.key.toLowerCase() == lower) return entry.value;
    }

    return fallback;
  }

  static String friendlyError(Object? error,
      {String fallback = 'تعذر تحميل البيانات. حاول مرة أخرى.'}) {
    if (error == null) return fallback;

    final message = error.toString();
    final lower = message.toLowerCase();

    if (lower.contains('not found')) {
      return 'البيانات المطلوبة غير موجودة.';
    }
    if (lower.contains('unauthorized') || lower.contains('forbidden')) {
      return 'ليس لديك صلاحية لتنفيذ هذه العملية.';
    }
    if (lower.contains('invalid') || lower.contains('bad request')) {
      return 'البيانات المدخلة غير صحيحة.';
    }
    if (lower.contains('already') || lower.contains('duplicate')) {
      return 'تم تنفيذ هذه العملية مسبقاً.';
    }
    if (lower.contains('insufficient') || lower.contains('balance')) {
      return 'رصيد النقاط غير كافٍ.';
    }
    if (lower.contains('status code') || lower.contains('exception')) {
      return fallback;
    }

    return fallback;
  }
}
