class FinanceUiText {
  const FinanceUiText._();

  static String transactionType(String value) => switch (value.trim()) {
        'RevenueRecognized' => 'إثبات الإيراد',
        'CustomerAdvance' => 'عربون عميل',
        'CustomerPayment' => 'تحصيل من عميل',
        'ReadyMadeCost' => 'تكلفة مبيعات الجاهز',
        'RevenueReversal' => 'عكس الإيراد',
        'OrderCancellationRefund' => 'استرداد إلغاء طلب',
        'CustomerBalanceWaiver' => 'إعفاء رصيد العميل',
        'CustomerSettlementDiscount' => 'خصم تسوية العميل',
        'DeliveryCost' => 'تكلفة التوصيل',
        'CostReversal' => 'عكس التكلفة',
        'InventoryAdjustment' => 'تسوية المخزون',
        'OperatingExpense' => 'مصروف تشغيلي',
        _ => 'حركة مالية مسجلة',
      };

  static String accountType(String value) => switch (value.trim().toLowerCase()) {
        'asset' || 'اصل' || 'أصل' => 'أصل',
        'liability' || 'التزام' => 'التزام',
        'revenue' || 'ايراد' || 'إيراد' => 'إيراد',
        'expense' || 'مصروف' => 'مصروف',
        'equity' || 'حقوق ملكية' => 'حقوق ملكية',
        _ => 'نوع حساب غير مصنف',
      };

  static String accountName(String code, String value) => switch (code) {
        '1000' => 'النقد المتاح في المنشأة',
        '1110' => 'مخزون المنتجات الجاهزة',
        '1160' => 'عربون العملاء',
        '1200' => 'الذمم المدينة',
        '4200' => 'إيراد المبيعات',
        '5200' => 'تكلفة المبيعات',
        _ => _isArabic(value) ? value : 'حساب رقم $code',
      };

  static String description(String? value) => value == null || value.trim().isEmpty
      ? '-'
      : _isArabic(value) ? value.trim() : 'تفاصيل محاسبية مسجلة';
  
  static String label(String? value, String fallback) => value == null || value.trim().isEmpty || !_isArabic(value) ? fallback : value.trim();

  static bool _isArabic(String value) => RegExp(r'[\u0600-\u06FF]').hasMatch(value);
}