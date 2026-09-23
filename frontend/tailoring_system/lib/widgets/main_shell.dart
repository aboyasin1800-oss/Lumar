import 'package:flutter/material.dart';

import '../core/app_navigation.dart';
import '../core/app_routes.dart';
import '../screens/module_screens.dart';
import '../screens/sales_screen.dart';
import 'app_sidebar.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/sidebar_state.dart';

class MainShell extends StatefulWidget {
	const MainShell({required this.auth, required this.themeState, required this.uiScale, required this.sidebarState, this.initialIndex = 0, this.content, this.title, super.key});
	final AuthState auth;
	final ThemeState themeState;
	final UiScaleState uiScale;
	final SidebarState sidebarState;
	final int initialIndex;
	final Widget? content;
	final String? title;

	@override
	State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
	late int selectedIndex;

	@override
	void initState() {
		super.initState();
		selectedIndex = widget.initialIndex;
		if (selectedIndex != 0) widget.sidebarState.setVisible(true);
	}

	void _navigate(int index) {
		widget.sidebarState.setVisible(true);
		if (index == selectedIndex) return;
		Navigator.of(context).pushReplacementNamed(AppRoutes.destinations[index].route);
	}

	Widget _buildSalesViewSelector() {
		return ValueListenableBuilder<SalesViewMode>(
			valueListenable: salesViewModeNotifier,
			builder: (context, mode, _) {
				final buttons = <SalesViewMode, String>{
					SalesViewMode.tabs: 'التبويبات',
					SalesViewMode.sessions: 'الشريط',
					SalesViewMode.all: 'الكل',
					SalesViewMode.fullscreen: 'ملء الشاشة',
				};
				final isLightTheme = Theme.of(context).brightness == Brightness.light;
				final chipBackground = isLightTheme ? const Color(0xFFEAF1F7) : const Color(0xFF1A2633);
				final chipBorder = isLightTheme ? const Color(0xFFD8E1F0) : const Color(0xFF32475E);
				final textColor = isLightTheme ? const Color(0xFF0F172A) : const Color(0xFFEAF2FF);

				return Wrap(
					alignment: WrapAlignment.center,
					spacing: 8,
					runSpacing: 8,
					children: buttons.entries.map((entry) {
						final selected = mode == entry.key;
						return ChoiceChip(
							label: Text(entry.value),
							selected: selected,
							showCheckmark: false,
							onSelected: (_) => salesViewModeNotifier.value = entry.key,
							selectedColor: const Color.fromARGB(255, 2, 225, 180),
							backgroundColor: chipBackground,
							padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
							labelStyle: TextStyle(
								color: selected ? const Color.fromARGB(255, 0, 0, 0) : textColor,
								fontWeight: FontWeight.w700,
								fontSize: 13,
							),
							side: BorderSide(
								color: selected ? const Color.fromARGB(255, 2, 225, 180) : chipBorder,
							),
							shape: RoundedRectangleBorder(
								borderRadius: BorderRadius.circular(5),
							),
						);
					}).toList(),
				);
			},
		);
	}

	@override
	Widget build(BuildContext context) {
		final destination = AppRoutes.destinations[selectedIndex];
		final title = widget.title ?? destination.title;
		return Scaffold(
			body: Row(children: [
				if (widget.sidebarState.isVisible) ...[
					ExcludeFocus(child: AppSidebar(selectedIndex: selectedIndex, onDestinationSelected: _navigate)),
					const VerticalDivider(width: 3),
				],
				Expanded(child: Column(children: [
					Container(
						height: 40,
						padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 6),
						alignment: Alignment.centerLeft,
						child: selectedIndex == 2
							? Row(
									children: [
										if (Navigator.of(context).canPop()) BackButton(onPressed: AppNavigation.back),
										if (Navigator.of(context).canPop()) const SizedBox(width: 4),
										IconButton(
											tooltip: widget.sidebarState.isVisible ? 'إخفاء القائمة الجانبية' : 'إظهار القائمة الجانبية',
											onPressed: widget.sidebarState.toggle,
											icon: Icon(widget.sidebarState.isVisible ? Icons.menu_open : Icons.menu),
										),
										const SizedBox(width: 8),
										Text(title, style: Theme.of(context).textTheme.headlineSmall),
										const Spacer(),
										SizedBox(width: 420, child: _buildSalesViewSelector()),
										const SizedBox(width: 18),
										Row(
											children: [
												const CircleAvatar(child: Icon(Icons.person_outline)),
												const SizedBox(width: 8),
												Text(widget.auth.user!.username),
											],
										),
										const SizedBox(width: 8),
										IconButton(
											tooltip: 'تسجيل الخروج',
											onPressed: () async {
												await widget.auth.logout();
												if (mounted) {
													Navigator.of(context).pushNamedAndRemoveUntil(AppRoutes.login, (_) => false);
												}
											},
											icon: const Icon(Icons.logout),
										),
									],
								)
								: Row(children: [
										if (Navigator.of(context).canPop()) BackButton(onPressed: AppNavigation.back),
										if (Navigator.of(context).canPop()) const SizedBox(width: 4),
										IconButton(
											tooltip: widget.sidebarState.isVisible ? 'إخفاء القائمة الجانبية' : 'إظهار القائمة الجانبية',
											onPressed: widget.sidebarState.toggle,
											icon: Icon(widget.sidebarState.isVisible ? Icons.menu_open : Icons.menu),
										),
										const SizedBox(width: 8),
										Expanded(child: Text(title, style: Theme.of(context).textTheme.headlineSmall)),
										const CircleAvatar(child: Icon(Icons.person_outline)),
										const SizedBox(width: 10),
										Text(widget.auth.user!.username),
										IconButton(
											tooltip: 'تسجيل الخروج',
											onPressed: () async {
												await widget.auth.logout();
												if (mounted) {
													Navigator.of(context).pushNamedAndRemoveUntil(AppRoutes.login, (_) => false);
												}
											},
											icon: const Icon(Icons.logout),
										),
									]),
						),
					const Divider(height: 1),
					Expanded(child: Padding(padding: const EdgeInsets.all(28), child: widget.content ?? screenForIndex(selectedIndex, widget.auth, widget.themeState, widget.uiScale))),
				])),
			]),
		);
	}
}