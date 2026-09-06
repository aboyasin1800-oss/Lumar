import 'package:flutter/material.dart';

import '../screens/inventory/bulk_fabric_entry_screen.dart';
import '../screens/login_screen.dart';
import '../widgets/main_shell.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/sidebar_state.dart';
import '../widgets/structure_placeholder.dart';

class AppRoutes {
	static const login = '/login';
	static const dashboard = '/dashboard';

	static const destinations = <AppDestination>[
		AppDestination('/dashboard', 'لوحة التحكم', Icons.dashboard_outlined),
		AppDestination('/customers', 'العملاء', Icons.people_outline),
		AppDestination('/sales', 'المبيعات', Icons.point_of_sale_outlined),
		AppDestination('/orders', 'الطلبات', Icons.receipt_long_outlined),
		AppDestination('/measurements', 'القياسات', Icons.straighten_outlined),
		AppDestination('/production', 'الإنتاج', Icons.precision_manufacturing_outlined),
		AppDestination('/printing', 'الطباعة', Icons.print_outlined),
		AppDestination('/inventory', 'المخزون', Icons.inventory_2_outlined),
		AppDestination('/purchasing', 'المشتريات', Icons.shopping_cart_outlined),
		AppDestination('/employees', 'الموظفون', Icons.badge_outlined),
		AppDestination('/payroll', 'الرواتب', Icons.payments_outlined),
		AppDestination('/employee-draws', 'سلف الموظفين', Icons.account_balance_wallet_outlined),
		AppDestination('/finance', 'المالية', Icons.account_balance_outlined),
		AppDestination('/pricing', 'التسعير', Icons.sell_outlined),
		AppDestination('/loyalty', 'الولاء', Icons.workspace_premium_outlined),
		AppDestination('/messages', 'الرسائل', Icons.forum_outlined),
		AppDestination('/reports', 'التقارير', Icons.analytics_outlined),
		AppDestination('/administration', 'الإدارة', Icons.admin_panel_settings_outlined),
		AppDestination('/settings', 'الإعدادات', Icons.settings_outlined),
		AppDestination('/ready-made-production', 'الإنتاج الجاهز من منتجاتنا', Icons.checkroom_outlined),
	];

	static Route<void> onGenerateRoute(RouteSettings settings, AuthState auth, ThemeState themeState, UiScaleState uiScale, SidebarState sidebarState) {
		if (settings.name == login) {
			return MaterialPageRoute(builder: (_) => LoginScreen(auth: auth), settings: settings);
		}
		if (!auth.signedIn) return MaterialPageRoute(builder: (_) => LoginScreen(auth: auth), settings: settings);
		if (settings.name == '/structure/إدخال الأقمشة بالجملة' || settings.name == '/inventory/bulk-entry') {
			sidebarState.setVisible(true);
			return MaterialPageRoute(builder: (_) => const BulkFabricEntryScreen(), settings: settings);
		}
		if (settings.name?.startsWith('/structure/') ?? false) {
			final title = Uri.decodeComponent(settings.name!.substring('/structure/'.length));
			sidebarState.setVisible(true);
			return MaterialPageRoute(builder: (_) => MainShell(auth: auth, themeState: themeState, uiScale: uiScale, sidebarState: sidebarState, content: StructurePlaceholder(title: title, description: 'واجهة منظمة ضمن هيكل وحدة $title.'), title: title), settings: settings);
		}
		final index = destinations.indexWhere((destination) => destination.route == settings.name);
		return MaterialPageRoute(
			builder: (_) => MainShell(auth: auth, themeState: themeState, uiScale: uiScale, sidebarState: sidebarState, initialIndex: index < 0 ? 0 : index),
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