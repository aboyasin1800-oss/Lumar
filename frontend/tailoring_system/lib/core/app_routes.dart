import 'package:flutter/material.dart';

import '../screens/design_system_demo_screen.dart';
import '../screens/inventory/bulk_fabric_entry_screen.dart';
import '../screens/login_screen.dart';
import '../screens/loyalty/loyalty_dashboard_screen.dart';
import '../screens/loyalty/loyalty_redemption_screen.dart';
import '../screens/loyalty/loyalty_redemptions_history_screen.dart';
import '../screens/loyalty/loyalty_rewards_screen.dart';
import '../screens/loyalty/loyalty_rules_screen.dart';
import '../screens/loyalty/loyalty_transactions_screen.dart';
import '../screens/loyalty/vip_levels_screen.dart';
import '../screens/referral/referral_analytics_screen.dart';
import '../screens/referral/referral_codes_screen.dart';
import '../screens/referral/referral_dashboard_screen.dart';
import '../screens/referral/referral_history_screen.dart';
import '../screens/referral/referral_rewards_screen.dart';
import '../screens/referral/referral_tree_screen.dart';
import '../screens/settings/imported_product_loyalty_point_settings_screen.dart';
import '../screens/settings/product_loyalty_point_settings_screen.dart';
import '../screens/settings/points_settings_screen.dart';
import '../screens/settings/ready_made_product_loyalty_point_settings_screen.dart';
import '../widgets/main_shell.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/sidebar_state.dart';
import '../widgets/structure_placeholder.dart';

class AppRoutes {
  static const login = '/login';
  static const dashboard = '/dashboard';
  static const designSystemDemo = '/design-system-demo';
  static const referralDashboard = '/referral-dashboard';
  static const referralTree = '/referral-tree';
  static const referralHistory = '/referral-history';
  static const referralCodes = '/referral-codes';
  static const referralRewards = '/referral-rewards';
  static const referralAnalytics = '/referral-analytics';
  static const loyaltyDashboard = '/loyalty-dashboard';
  static const loyaltyTransactions = '/loyalty-transactions';
  static const loyaltyRedemption = '/loyalty-redemption';
  static const loyaltyRedemptionsHistory = '/loyalty-redemptions-history';
  static const loyaltyRewards = '/loyalty-rewards';
  static const loyaltyRules = '/loyalty-rules';
  static const vipLevels = '/vip-levels';
  static const pointsSettings = '/points-settings';
  static const piecePointSettings = '/piece-point-settings';
  static const importedProductPointSettings =
      '/imported-product-point-settings';
  static const readyMadeProductPointSettings =
      '/ready-made-product-point-settings';

  static const destinations = <AppDestination>[
    AppDestination('/dashboard', 'لوحة التحكم', Icons.dashboard_outlined),
    AppDestination('/customers', 'العملاء', Icons.people_outline),
    AppDestination('/sales', 'المبيعات', Icons.point_of_sale_outlined),
    AppDestination('/orders', 'الطلبات', Icons.receipt_long_outlined),
    AppDestination('/measurements', 'القياسات', Icons.straighten_outlined),
    AppDestination(
        '/production', 'الإنتاج', Icons.precision_manufacturing_outlined),
    AppDestination('/printing', 'الطباعة', Icons.print_outlined),
    AppDestination('/inventory', 'المخزون', Icons.inventory_2_outlined),
    AppDestination('/purchasing', 'المشتريات', Icons.shopping_cart_outlined),
    AppDestination('/employees', 'الموظفون', Icons.badge_outlined),
    AppDestination('/payroll', 'الرواتب', Icons.payments_outlined),
    AppDestination('/employee-draws', 'سلف الموظفين',
        Icons.account_balance_wallet_outlined),
    AppDestination('/finance', 'المالية', Icons.account_balance_outlined),
    AppDestination('/pricing', 'التسعير', Icons.sell_outlined),
    AppDestination('/loyalty', 'الولاء', Icons.workspace_premium_outlined),
    AppDestination('/messages', 'الرسائل', Icons.forum_outlined),
    AppDestination('/reports', 'التقارير', Icons.analytics_outlined),
    AppDestination(
        '/administration', 'الإدارة', Icons.admin_panel_settings_outlined),
    AppDestination('/settings', 'الإعدادات', Icons.settings_outlined),
    AppDestination('/ready-made-production', 'الإنتاج الجاهز من منتجاتنا',
        Icons.checkroom_outlined),
    AppDestination(
        '/factory-monitoring', 'مراقبة المصنع', Icons.factory_outlined),
    AppDestination(
        '/delivery-dashboard', 'شاشة التسليم', Icons.local_shipping_outlined),
    AppDestination(
        '/design-system-demo', 'اختبار نظام التصميم', Icons.palette_outlined),
    AppDestination('/lumar-erp', 'LUMAR ERP', Icons.auto_awesome_outlined),
    AppDestination(
        referralDashboard, 'لوحة الإحالات', Icons.group_add_outlined),
    AppDestination(referralTree, 'شجرة الإحالة', Icons.account_tree_outlined),
    AppDestination(referralHistory, 'سجل الإحالات', Icons.history_outlined),
    AppDestination(referralCodes, 'أكواد الإحالة', Icons.qr_code_2_outlined),
    AppDestination(
        referralRewards, 'مكافآت الإحالات', Icons.card_giftcard_outlined),
    AppDestination(
        referralAnalytics, 'تحليلات الإحالات', Icons.analytics_outlined),
    AppDestination(
        loyaltyDashboard, 'لوحة الولاء', Icons.workspace_premium_outlined),
    AppDestination(
        loyaltyTransactions, 'حركات الولاء', Icons.receipt_long_outlined),
    AppDestination(loyaltyRedemption, 'استبدال النقاط', Icons.redeem_outlined),
    AppDestination(loyaltyRedemptionsHistory, 'سجل استبدالات النقاط',
        Icons.history_edu_outlined),
    AppDestination(
        loyaltyRewards, 'مكافآت الولاء', Icons.card_giftcard_outlined),
    AppDestination(loyaltyRules, 'قواعد الولاء', Icons.rule_outlined),
    AppDestination(vipLevels, 'مستويات كبار العملاء', Icons.stars_outlined),
    AppDestination(pointsSettings, 'إعدادات النقاط', Icons.tune_outlined),
    AppDestination(piecePointSettings, 'نقاط المبيعات التفصيل',
        Icons.checkroom_outlined),
    AppDestination(readyMadeProductPointSettings,
        'نقاط المبيعات الجاهزة من منتجاتنا', Icons.storefront_outlined),
    AppDestination(importedProductPointSettings, 'نقاط الأصناف المستوردة',
        Icons.inventory_2_outlined),
  ];

  static Route<void> onGenerateRoute(RouteSettings settings, AuthState auth,
      ThemeState themeState, UiScaleState uiScale, SidebarState sidebarState) {
    if (settings.name == login) {
      return MaterialPageRoute(
          builder: (_) => LoginScreen(auth: auth), settings: settings);
    }
    if (settings.name == designSystemDemo) {
      return MaterialPageRoute(
          builder: (_) => const DesignSystemDemoScreen(), settings: settings);
    }
    if (settings.name == referralDashboard) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ReferralDashboardScreen(), settings: settings);
    }
    if (settings.name == referralTree) {
      sidebarState.setVisible(true);
      final customerId =
          settings.arguments is int ? settings.arguments as int : null;
      return MaterialPageRoute(
          builder: (_) => ReferralTreeScreen(customerId: customerId),
          settings: settings);
    }
    if (settings.name == referralHistory) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ReferralHistoryScreen(), settings: settings);
    }
    if (settings.name == referralCodes) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ReferralCodesScreen(), settings: settings);
    }
    if (settings.name == referralRewards) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ReferralRewardsScreen(), settings: settings);
    }
    if (settings.name == referralAnalytics) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ReferralAnalyticsScreen(), settings: settings);
    }
    if (settings.name == loyaltyDashboard) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const LoyaltyDashboardScreen(), settings: settings);
    }
    if (settings.name == loyaltyTransactions) {
      sidebarState.setVisible(true);
      final customerId =
          settings.arguments is int && (settings.arguments as int) > 0
              ? settings.arguments as int
              : null;
      return MaterialPageRoute(
          builder: (_) => LoyaltyTransactionsScreen(customerId: customerId),
          settings: settings);
    }
    if (settings.name == loyaltyRedemption) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const LoyaltyRedemptionScreen(), settings: settings);
    }
    if (settings.name == loyaltyRedemptionsHistory) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const LoyaltyRedemptionsHistoryScreen(),
          settings: settings);
    }
    if (settings.name == loyaltyRewards) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const LoyaltyRewardsScreen(), settings: settings);
    }
    if (settings.name == loyaltyRules) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const LoyaltyRulesScreen(), settings: settings);
    }
    if (settings.name == vipLevels) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const VipLevelsScreen(), settings: settings);
    }
    if (settings.name == pointsSettings) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const PointsSettingsScreen(), settings: settings);
    }
    if (settings.name == piecePointSettings) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ProductLoyaltyPointSettingsScreen(),
          settings: settings);
    }
    if (settings.name == importedProductPointSettings) {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const ImportedProductLoyaltyPointSettingsScreen(),
          settings: settings);
    }
        if (settings.name == readyMadeProductPointSettings) {
        sidebarState.setVisible(true);
        return MaterialPageRoute(
            builder: (_) => const ReadyMadeProductLoyaltyPointSettingsScreen(),
            settings: settings);
        }
    if (!auth.signedIn) {
      return MaterialPageRoute(
          builder: (_) => LoginScreen(auth: auth), settings: settings);
    }
    if (settings.name == '/structure/إدخال الأقمشة بالجملة' ||
        settings.name == '/inventory/bulk-entry') {
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => const BulkFabricEntryScreen(), settings: settings);
    }
    if (settings.name?.startsWith('/structure/') ?? false) {
      final title =
          Uri.decodeComponent(settings.name!.substring('/structure/'.length));
      sidebarState.setVisible(true);
      return MaterialPageRoute(
          builder: (_) => MainShell(
              auth: auth,
              themeState: themeState,
              uiScale: uiScale,
              sidebarState: sidebarState,
              content: StructurePlaceholder(
                  title: title,
                  description: 'واجهة منظمة ضمن هيكل وحدة $title.'),
              title: title),
          settings: settings);
    }
    final index = destinations
        .indexWhere((destination) => destination.route == settings.name);
    return MaterialPageRoute(
      builder: (_) => MainShell(
          auth: auth,
          themeState: themeState,
          uiScale: uiScale,
          sidebarState: sidebarState,
          initialIndex: index < 0 ? 0 : index),
      settings: settings,
    );
  }
}

class AppDestination {
  const AppDestination(this.route, this.title, this.icon);
  final String route;
  final String title;
  final IconData icon;
}
