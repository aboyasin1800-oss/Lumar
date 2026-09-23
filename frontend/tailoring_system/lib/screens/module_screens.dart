import 'package:flutter/material.dart';

import 'design_system_demo_screen.dart';
import 'factory_monitor_screen.dart';
import 'delivery/delivery_dashboard_screen.dart';
import '../widgets/structure_placeholder.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../core/app_navigation.dart';
import '../core/app_routes.dart';
import 'customers_screen.dart';
import 'employees_screen.dart';
import 'financial/finance_views.dart';
import 'inventory_screen.dart';
import 'measurements_screen.dart';
import 'orders_screen.dart';
import 'printing_screen.dart';
import 'pricing_screen.dart';
import 'production_screen.dart';
import 'ready_made_production_screen.dart';
import 'supplier_purchasing_screen.dart';
import 'sales_screen.dart';
import 'settings_screen.dart';
import 'settings/user_guide_screen.dart';
import 'settings/points_settings_screen.dart';
import 'settings/piece_point_settings_screen.dart';
import 'loyalty/loyalty_dashboard_screen.dart';
import 'loyalty/loyalty_redemption_screen.dart';
import 'loyalty/loyalty_redemptions_history_screen.dart';
import 'loyalty/loyalty_rewards_screen.dart';
import 'loyalty/loyalty_rules_screen.dart';
import 'loyalty/loyalty_transactions_screen.dart';
import 'loyalty/vip_levels_screen.dart';
import 'referral/referral_analytics_screen.dart';
import 'referral/referral_codes_screen.dart';
import 'referral/referral_dashboard_screen.dart';
import 'referral/referral_history_screen.dart';
import 'referral/referral_rewards_screen.dart';
import 'referral/referral_tree_screen.dart';
import 'lumar_erp_logo_showcase_screen.dart';

Widget screenForIndex(
    int index, AuthState auth, ThemeState themeState, UiScaleState uiScale) {
  if (index == 0) return const DashboardScreen();
  if (index == 1) return const CustomerPage();
  if (index == 2) return const SalesScreen();
  if (index == 3) return const OrdersScreen();
  if (index == 4) return const MeasurementsScreen();
  if (index == 5) return ProductionScreen(themeState: themeState);
  if (index == 6) return const PrintingScreen();
  if (index == 7) return const InventoryScreen();
  if (index == 8) return const SupplierPurchasingScreen();
  if (index == 9) return const EmployeesScreen(initialTab: 0);
  if (index == 10) return const EmployeesScreen(initialTab: 3);
  if (index == 12) return const FinancialScreen();
  if (index == 13) return const PricingScreen();
  if (index == 14) return const LoyaltyDashboardScreen();
  if (index == 24) return const ReferralDashboardScreen();
  if (index == 25) return const ReferralTreeScreen();
  if (index == 26) return const ReferralHistoryScreen();
  if (index == 27) return const ReferralCodesScreen();
  if (index == 28) return const ReferralRewardsScreen();
  if (index == 29) return const ReferralAnalyticsScreen();
  if (index == 30) return const LoyaltyDashboardScreen();
  if (index == 31) return const LoyaltyTransactionsScreen();
  if (index == 32) return const LoyaltyRedemptionScreen();
  if (index == 33) return const LoyaltyRedemptionsHistoryScreen();
  if (index == 34) return const LoyaltyRewardsScreen();
  if (index == 35) return const LoyaltyRulesScreen();
  if (index == 36) return const VipLevelsScreen();
  if (index == 37) return const PointsSettingsScreen();
  if (index == 38) return const PiecePointSettingsScreen();
  if (index == 18) {
    return SettingsScreen(auth: auth, themeState: themeState, uiScale: uiScale);
  }
  if (index == 19) return const ReadyMadeProductionScreen();
  if (index == 20) return const FactoryMonitorScreen();
  if (index == 21) return const DeliveryDashboardScreen();
  if (index == 22) return const DesignSystemDemoScreen();
  if (index == 23) return const LumarErpLogoShowcaseScreen();
  return ModuleSection(
      title: _titles[index], items: _moduleItems[index] ?? const []);
}

const _titles = [
  'لوحة التحكم',
  'العملاء',
  'المبيعات',
  'الطلبات',
  'القياسات',
  'الإنتاج',
  'الطباعة',
  'المخزون',
  'المشتريات',
  'الموظفون',
  'الرواتب',
  'سلف الموظفين',
  'المالية',
  'التسعير',
  'الولاء',
  'الرسائل',
  'التقارير',
  'الإدارة',
  'الإعدادات',
  'الإنتاج الجاهز من منتجاتنا',
  'مراقبة المصنع',
  'شاشة التسليم',
  'اختبار نظام التصميم',
  'LUMAR ERP',
];

const _moduleItems = <int, List<ModuleSectionItem>>{
  2: [
    ModuleSectionItem('طلبات التفصيل', 'إدارة مبيعات وطلبات التفصيل.', Icons.receipt_long_outlined),
    ModuleSectionItem('المبيعات الجاهزة', 'بيع المنتجات الجاهزة.', Icons.point_of_sale_outlined),
  ],
  3: [
    ModuleSectionItem('قائمة الطلبات', 'متابعة جميع الطلبات.', Icons.list_alt_outlined),
    ModuleSectionItem('تفاصيل الطلب', 'عرض تفاصيل الطلب والدفعات.', Icons.description_outlined),
    ModuleSectionItem('تسليم الطلب', 'متابعة تسليم الطلبات.', Icons.local_shipping_outlined),
  ],
  5: [
    ModuleSectionItem('مسح الإنتاج', 'مسح وتتبع مراحل الإنتاج.', Icons.qr_code_scanner_outlined),
    ModuleSectionItem('تحليل تكلفة المنتج', 'تحليل تكاليف الإنتاج.', Icons.analytics_outlined),
    ModuleSectionItem('تقارير التكاليف', 'تقارير تكاليف الإنتاج.', Icons.bar_chart_outlined),
  ],
  6: [
    ModuleSectionItem('الطباعة', 'طباعة المستندات وبطاقات العمل.', Icons.print_outlined),
  ],
  7: [
    ModuleSectionItem('المخزون', 'إدارة مواد وأصناف المخزون.', Icons.inventory_2_outlined),
    ModuleSectionItem('مخزون الأقمشة', 'متابعة الأقمشة والكميات.', Icons.texture_outlined),
    ModuleSectionItem('إدخال الأقمشة بالجملة', 'تسجيل إدخال الأقمشة.', Icons.add_box_outlined),
  ],
  8: [
    ModuleSectionItem('إدارة الموردين', 'إدارة بيانات الموردين.', Icons.people_outline),
    ModuleSectionItem('أوامر الشراء', 'متابعة أوامر الشراء.', Icons.shopping_cart_outlined),
    ModuleSectionItem('فواتير الموردين', 'إنشاء وتسوية الفواتير.', Icons.receipt_outlined),
    ModuleSectionItem('دفعات الموردين', 'إدارة دفعات الموردين.', Icons.payments_outlined),
    ModuleSectionItem('استلام البضائع', 'تسجيل استلام المشتريات.', Icons.inventory_outlined),
  ],
  9: [
    ModuleSectionItem('إدارة الموظفين', 'إدارة بيانات الموظفين.', Icons.badge_outlined),
    ModuleSectionItem('الحضور', 'الحضور وتقارير الحضور.', Icons.calendar_month_outlined),
    ModuleSectionItem('سلف الموظفين', 'متابعة سلف الموظفين.', Icons.account_balance_wallet_outlined),
  ],
  10: [
    ModuleSectionItem('فترات الرواتب', 'متابعة الرواتب والفترات.', Icons.payments_outlined),
    ModuleSectionItem('تفاصيل الراتب', 'عرض عناصر الراتب.', Icons.description_outlined),
    ModuleSectionItem('أجور القطعة', 'إدارة أجور القطعة.', Icons.precision_manufacturing_outlined),
  ],
  11: [
    ModuleSectionItem('سلف الموظفين', 'واجهة سلف الموظفين.', Icons.account_balance_wallet_outlined),
    ModuleSectionItem('سجل السلف', 'متابعة السلف والتسويات.', Icons.list_alt_outlined),
  ],
  12: [
    ModuleSectionItem('الحركات المالية', 'عرض الحركات المالية.', Icons.swap_horiz_outlined),
    ModuleSectionItem('القوائم المالية', 'عرض القوائم والتقارير.', Icons.assessment_outlined),
    ModuleSectionItem('الميزانية العمومية', 'عرض الميزانية العمومية.', Icons.account_balance_outlined),
    ModuleSectionItem('التدفقات النقدية', 'عرض التدفقات النقدية.', Icons.water_outlined),
    ModuleSectionItem('دليل الحسابات', 'إدارة دليل الحسابات.', Icons.account_tree_outlined),
    ModuleSectionItem('قيود اليومية', 'عرض القيود المحاسبية.', Icons.book_outlined),
    ModuleSectionItem('المصروفات التشغيلية', 'متابعة المصروفات التشغيلية.', Icons.money_off_outlined),
  ],
  13: [
    ModuleSectionItem('حوكمة التسعير', 'إعداد قواعد التسعير.', Icons.sell_outlined),
  ],
  14: [
    ModuleSectionItem('لوحة الولاء', 'متابعة الولاء والمكافآت.', Icons.workspace_premium_outlined),
    ModuleSectionItem('مستويات كبار العملاء', 'إدارة مستويات العملاء.', Icons.star_outline),
  ],
  15: [
    ModuleSectionItem('الرسائل', 'إدارة رسائل العملاء.', Icons.forum_outlined),
  ],
  16: [
    ModuleSectionItem('التقارير المتقدمة', 'واجهة التقارير المتقدمة.', Icons.analytics_outlined),
    ModuleSectionItem('إحصاءات النظام', 'عرض إحصاءات النظام.', Icons.query_stats_outlined),
  ],
  17: [
    ModuleSectionItem('المستخدمون والأدوار', 'إدارة المستخدمين والأدوار.', Icons.admin_panel_settings_outlined),
    ModuleSectionItem('الصلاحيات', 'إدارة الصلاحيات.', Icons.security_outlined),
    ModuleSectionItem('النسخ الاحتياطي', 'متابعة النسخ الاحتياطي.', Icons.backup_outlined),
  ],
  20: [
    ModuleSectionItem('مراقبة المصنع', 'متابعة الطلبات ذات المخاطر والتوقفات.', Icons.factory_outlined),
  ],
  21: [
    ModuleSectionItem('شاشة التسليم', 'التسليم والتحصيل من شاشة مستقلة.', Icons.local_shipping_outlined),
  ],
  22: [
    ModuleSectionItem('LUMAR ERP', 'معاينة تصميم شعار المؤسسة.', Icons.auto_awesome_outlined),
  ],
};

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  static const double _dashboardIconSize = 60;
  static const Color _lightCardBackground = Color(0xFFFFFFFF);
  static const Color _lightCardBorder = Color(0xFFE2E8F0);
  static const Color _lightCardText = Color(0xFF0F172A);
  static const Color _darkCardBackground = Color(0xFF1E293B);
  static const Color _darkCardBorder = Color(0xFF334155);
  static const Color _darkCardText = Color(0xFFE2E8F0);
  static const _moduleIconColors = <int, Color>{
    1: Color(0xFF38BDF8),
    2: Color(0xFFF97316),
    3: Color(0xFF14B8A6),
    4: Color(0xFF4EC9B0),
    5: Color(0xFFF59E0B),
    6: Color(0xFF22C55E),
    7: Color(0xFF06B6D4),
    8: Color(0xFF60A5FA),
    9: Color(0xFFEC4899),
    10: Color(0xFF4EC9B0),
    11: Color(0xFFF43F5E),
    12: Color(0xFF10B981),
    13: Color(0xFFEAB308),
    14: Color(0xFFFBBF24),
    15: Color(0xFF2DD4BF),
    16: Color(0xFF818CF8),
    17: Color(0xFFFB7185),
    18: Color(0xFF94A3B8),
    19: Color(0xFF06B6D4),
    20: Color(0xFF34D399),
    21: Color(0xFF3B82F6),
    22: Color(0xFFD4A63A),
    24: Color(0xFF8B5CF6),
    25: Color(0xFF22C55E),
    26: Color(0xFF60A5FA),
    27: Color(0xFFF59E0B),
    28: Color(0xFFEC4899),
    29: Color(0xFF4EC9B0),
    30: Color(0xFFFBBF24),
    31: Color(0xFF06B6D4),
    32: Color(0xFFFB7185),
    33: Color(0xFF94A3B8),
    34: Color(0xFF2DD4BF),
    35: Color(0xFF818CF8),
    36: Color(0xFF34D399),
  };

  static const _modules = <int>[
    1,
    2,
    3,
    4,
    5,
    19,
    6,
    7,
    8,
    9,
    10,
    11,
    12,
    13,
    24,
    25,
    26,
    27,
    28,
    29,
    30,
    31,
    32,
    33,
    34,
    35,
    36,
    15,
    16,
    17,
    18,
    20,
    21,
    22,
  ];
  @override
  Widget build(BuildContext context) {
    final isLightMode = Theme.of(context).brightness == Brightness.light;
    final cardColor = isLightMode ? _lightCardBackground : _darkCardBackground;
    final cardBorder = isLightMode ? _lightCardBorder : _darkCardBorder;
    final cardTextColor = isLightMode ? _lightCardText : _darkCardText;
    final cardShadows = _dashboardShadows(isLightMode);

    return GridView.builder(
      gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: 164,
        mainAxisExtent: 164,
        mainAxisSpacing: 16,
        crossAxisSpacing: 16,
      ),
      itemCount: _modules.length + 1,
      itemBuilder: (context, index) {
        if (index == _modules.length) {
          return Column(
            children: [
              Expanded(
                child: _NeumorphicDashboardCard(
                  cardColor: cardColor,
                  borderColor: cardBorder,
                  outerShadows: cardShadows,
                  onTap: () => AppNavigation.push(
                    context,
                    (_) => const UserGuideScreen(),
                  ),
                  child: Icon(
                    Icons.menu_book_outlined,
                    size: _dashboardIconSize,
                    color: const Color(0xFFFBBF24),
                  ),
                ),
              ),
              const SizedBox(height: 7),
              Text(
                'دليل المستخدم',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: cardTextColor,
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                    ),
              ),
            ],
          );
        }
        final moduleIndex = _modules[index];
        final destination = AppRoutes.destinations[moduleIndex];
        final cardTitle = switch (moduleIndex) {
          24 => 'الإحالات',
          30 => 'الولاء والنقاط',
          _ => destination.title,
        };

        return Column(
          children: [
            Expanded(
              child: _NeumorphicDashboardCard(
                cardColor: cardColor,
                borderColor: cardBorder,
                outerShadows: cardShadows,
                onTap: () =>
                    AppNavigation.pushNamed(context, destination.route),
                child: Icon(
                  destination.icon,
                  size: _dashboardIconSize,
                  color: _moduleIconColors[moduleIndex] ?? cardTextColor,
                ),
              ),
            ),
            const SizedBox(height: 7),
            Text(
              cardTitle,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: cardTextColor,
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
            ),
          ],
        );
      },
    );
  }

  List<BoxShadow> _dashboardShadows(bool isLightMode) => [
        BoxShadow(
          color: (isLightMode ? Colors.black : Colors.black).withValues(
            alpha: isLightMode ? 0.14 : 0.34,
          ),
          blurRadius: 18,
          offset: const Offset(0, 8),
        ),
        BoxShadow(
          color: (isLightMode ? Colors.white : const Color(0xFF52647D))
              .withValues(alpha: isLightMode ? 0.78 : 0.22),
          blurRadius: 7,
          offset: const Offset(-3, -3),
        ),
      ];
}

class _NeumorphicDashboardCard extends StatefulWidget {
  static const double _centralRecessDiameter = 200;

  const _NeumorphicDashboardCard({
    required this.cardColor,
    required this.borderColor,
    required this.outerShadows,
    required this.onTap,
    required this.child,
  });

  final Color cardColor;
  final Color borderColor;
  final List<BoxShadow> outerShadows;
  final VoidCallback onTap;
  final Widget child;

  @override
  State<_NeumorphicDashboardCard> createState() =>
      _NeumorphicDashboardCardState();
}

class _NeumorphicDashboardCardState extends State<_NeumorphicDashboardCard> {
  bool _isHovered = false;

  @override
  Widget build(BuildContext context) {
    final isLightMode = Theme.of(context).brightness == Brightness.light;
    final recessEdgeColor =
        (isLightMode ? Colors.white : const Color(0xFF52647D))
            .withValues(alpha: isLightMode ? 0.16 : 0.2);
    final recessHighlight =
        (isLightMode ? Colors.white : const Color(0xFF8EA4C2))
            .withValues(alpha: isLightMode ? 0.42 : 0.18);
    final recessShadow = Colors.black.withValues(
      alpha:
          isLightMode ? (_isHovered ? 0.26 : 0.18) : (_isHovered ? 0.54 : 0.42),
    );

    return MouseRegion(
      onEnter: (_) => setState(() => _isHovered = true),
      onExit: (_) => setState(() => _isHovered = false),
      child: RepaintBoundary(
        child: Container(
          decoration: BoxDecoration(
            color: widget.cardColor,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: widget.borderColor),
            boxShadow: widget.outerShadows,
          ),
          child: Padding(
            padding: const EdgeInsets.all(6),
            child: Container(
              decoration: BoxDecoration(
                color: widget.cardColor,
                borderRadius: BorderRadius.circular(5),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(
                      alpha: _isHovered ? 0.18 : 0.12,
                    ),
                    blurRadius: _isHovered ? 4 : 6,
                    spreadRadius: -2,
                    offset:
                        _isHovered ? const Offset(1, 1) : const Offset(2, 2),
                  ),
                  BoxShadow(
                    color: Colors.white.withValues(
                      alpha: _isHovered ? 0.04 : 0.08,
                    ),
                    blurRadius: _isHovered ? 4 : 6,
                    spreadRadius: -2,
                    offset: _isHovered
                        ? const Offset(-1, -1)
                        : const Offset(-2, -2),
                  ),
                ],
              ),
              child: Material(
                color: Colors.transparent,
                child: InkWell(
                  onTap: widget.onTap,
                  borderRadius: BorderRadius.circular(5),
                  hoverColor: Colors.transparent,
                  highlightColor: Colors.transparent,
                  splashColor: Colors.transparent,
                  overlayColor:
                      const WidgetStatePropertyAll(Colors.transparent),
                  child: Stack(
                    alignment: Alignment.center,
                    children: [
                      Container(
                        width: _NeumorphicDashboardCard._centralRecessDiameter,
                        height: _NeumorphicDashboardCard._centralRecessDiameter,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: widget.cardColor,
                          border: Border.all(color: recessEdgeColor),
                          boxShadow: [
                            BoxShadow(
                              color: recessShadow,
                              blurRadius: _isHovered ? 20 : 16,
                              spreadRadius: _isHovered ? -2 : -4,
                              offset: _isHovered
                                  ? const Offset(6, 7)
                                  : const Offset(5, 6),
                            ),
                            BoxShadow(
                              color: recessHighlight,
                              blurRadius: _isHovered ? 9 : 12,
                              spreadRadius: _isHovered ? -4 : -3,
                              offset: _isHovered
                                  ? const Offset(-3, -3)
                                  : const Offset(-4, -4),
                            ),
                          ],
                        ),
                      ),
                      widget.child,
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
