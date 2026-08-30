import 'package:flutter/material.dart';

import '../widgets/structure_placeholder.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../core/app_routes.dart';
import 'customers_screen.dart';
import 'financial/finance_views.dart';
import 'inventory_screen.dart';
import 'measurements_screen.dart';
import 'orders_screen.dart';
import 'printing_screen.dart';
import 'production_screen.dart';
import 'sales_screen.dart';
import 'settings_screen.dart';

Widget screenForIndex(int index, AuthState auth, ThemeState themeState, UiScaleState uiScale) {
	if (index == 0) return const DashboardScreen();
	if (index == 1) return const CustomerPage();
	if (index == 2) return const SalesScreen();
	if (index == 3) return const OrdersScreen();
	if (index == 4) return const MeasurementsScreen();
	if (index == 5) return const ProductionScreen();
	if (index == 6) return const PrintingScreen();
	if (index == 7) return const InventoryScreen();
	if (index == 12) return const FinancialScreen();
	if (index == 18) return SettingsScreen(auth: auth, themeState: themeState, uiScale: uiScale);
	return ModuleSection(title: _titles[index], items: _moduleItems[index] ?? const []);
}

const _titles = [
	'لوحة التحكم', 'العملاء', 'المبيعات', 'الطلبات', 'القياسات', 'الإنتاج', 'الطباعة', 'المخزون', 'المشتريات', 'الموظفون', 'الرواتب', 'سلف الموظفين', 'المالية', 'التسعير', 'الولاء', 'الرسائل', 'التقارير', 'الإدارة', 'الإعدادات',
];

const _moduleItems = <int, List<ModuleSectionItem>>{
	2: [ModuleSectionItem('طلبات التفصيل', 'إدارة مبيعات وطلبات التفصيل.', Icons.receipt_long_outlined), ModuleSectionItem('المبيعات الجاهزة', 'بيع المنتجات الجاهزة.', Icons.point_of_sale_outlined)],
	3: [ModuleSectionItem('قائمة الطلبات', 'متابعة جميع الطلبات.', Icons.list_alt_outlined), ModuleSectionItem('تفاصيل الطلب', 'عرض تفاصيل الطلب والدفعات.', Icons.description_outlined), ModuleSectionItem('تسليم الطلب', 'متابعة تسليم الطلبات.', Icons.local_shipping_outlined)],
	5: [ModuleSectionItem('مسح الإنتاج', 'مسح وتتبع مراحل الإنتاج.', Icons.qr_code_scanner_outlined), ModuleSectionItem('تحليل تكلفة المنتج', 'تحليل تكاليف الإنتاج.', Icons.analytics_outlined), ModuleSectionItem('تقارير التكاليف', 'تقارير تكاليف الإنتاج.', Icons.bar_chart_outlined)],
	6: [ModuleSectionItem('الطباعة', 'طباعة المستندات وبطاقات العمل.', Icons.print_outlined)],
	7: [ModuleSectionItem('المخزون', 'إدارة مواد وأصناف المخزون.', Icons.inventory_2_outlined), ModuleSectionItem('مخزون الأقمشة', 'متابعة الأقمشة والكميات.', Icons.texture_outlined), ModuleSectionItem('إدخال الأقمشة بالجملة', 'تسجيل إدخال الأقمشة.', Icons.add_box_outlined)],
	8: [ModuleSectionItem('إدارة الموردين', 'إدارة بيانات الموردين.', Icons.people_outline), ModuleSectionItem('أوامر الشراء', 'متابعة أوامر الشراء.', Icons.shopping_cart_outlined), ModuleSectionItem('فواتير الموردين', 'إنشاء وتسوية الفواتير.', Icons.receipt_outlined), ModuleSectionItem('دفعات الموردين', 'إدارة دفعات الموردين.', Icons.payments_outlined), ModuleSectionItem('استلام البضائع', 'تسجيل استلام المشتريات.', Icons.inventory_outlined)],
	9: [ModuleSectionItem('إدارة الموظفين', 'إدارة بيانات الموظفين.', Icons.badge_outlined), ModuleSectionItem('الحضور', 'الحضور وتقارير الحضور.', Icons.calendar_month_outlined), ModuleSectionItem('سلف الموظفين', 'متابعة سلف الموظفين.', Icons.account_balance_wallet_outlined)],
	10: [ModuleSectionItem('فترات الرواتب', 'متابعة الرواتب والفترات.', Icons.payments_outlined), ModuleSectionItem('تفاصيل الراتب', 'عرض عناصر الراتب.', Icons.description_outlined), ModuleSectionItem('أجور القطعة', 'إدارة أجور القطعة.', Icons.precision_manufacturing_outlined)],
	11: [ModuleSectionItem('سلف الموظفين', 'واجهة سلف الموظفين.', Icons.account_balance_wallet_outlined), ModuleSectionItem('سجل السلف', 'متابعة السلف والتسويات.', Icons.list_alt_outlined)],
	12: [ModuleSectionItem('الحركات المالية', 'عرض الحركات المالية.', Icons.swap_horiz_outlined), ModuleSectionItem('القوائم المالية', 'عرض القوائم والتقارير.', Icons.assessment_outlined), ModuleSectionItem('الميزانية العمومية', 'عرض الميزانية العمومية.', Icons.account_balance_outlined), ModuleSectionItem('التدفقات النقدية', 'عرض التدفقات النقدية.', Icons.water_outlined), ModuleSectionItem('دليل الحسابات', 'إدارة دليل الحسابات.', Icons.account_tree_outlined), ModuleSectionItem('قيود اليومية', 'عرض القيود المحاسبية.', Icons.book_outlined), ModuleSectionItem('المصروفات التشغيلية', 'متابعة المصروفات التشغيلية.', Icons.money_off_outlined)],
	13: [ModuleSectionItem('حوكمة التسعير', 'إعداد قواعد التسعير.', Icons.sell_outlined)],
	14: [ModuleSectionItem('لوحة الولاء', 'متابعة الولاء والمكافآت.', Icons.workspace_premium_outlined), ModuleSectionItem('مستويات كبار العملاء', 'إدارة مستويات العملاء.', Icons.star_outline)],
	15: [ModuleSectionItem('الرسائل', 'إدارة رسائل العملاء.', Icons.forum_outlined)],
	16: [ModuleSectionItem('التقارير المتقدمة', 'واجهة التقارير المتقدمة.', Icons.analytics_outlined), ModuleSectionItem('إحصاءات النظام', 'عرض إحصاءات النظام.', Icons.query_stats_outlined)],
	17: [ModuleSectionItem('المستخدمون والأدوار', 'إدارة المستخدمين والأدوار.', Icons.admin_panel_settings_outlined), ModuleSectionItem('الصلاحيات', 'إدارة الصلاحيات.', Icons.security_outlined), ModuleSectionItem('النسخ الاحتياطي', 'متابعة النسخ الاحتياطي.', Icons.backup_outlined)],
};

class DashboardScreen extends StatelessWidget {
	const DashboardScreen({super.key});
	static const _modules = <int>[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18];
	@override
	Widget build(BuildContext context) => GridView.builder(
		gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(maxCrossAxisExtent: 220, mainAxisExtent: 132, mainAxisSpacing: 16, crossAxisSpacing: 16),
		itemCount: _modules.length,
		itemBuilder: (context, index) {
			final moduleIndex = _modules[index];
			final destination = AppRoutes.destinations[moduleIndex];
			return Card(child: InkWell(borderRadius: BorderRadius.circular(8), onTap: () => Navigator.of(context).pushNamed(destination.route), child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Icon(destination.icon), const Spacer(), Text(destination.title, style: Theme.of(context).textTheme.titleMedium)]))));
		},
	);
}