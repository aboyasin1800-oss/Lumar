import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/app_navigation.dart';
import 'core/app_routes.dart';
import 'core/ui_palette.dart';
import 'screens/login_screen.dart';
import 'services/auth_state.dart';
import 'services/theme_state.dart';
import 'services/ui_scale_state.dart';
import 'services/sidebar_state.dart';
import 'widgets/keyboard_policy.dart';
import 'widgets/main_shell.dart';

void main() => runApp(const LumarApp());

class LumarApp extends StatefulWidget {
	const LumarApp({super.key});
	@override State<LumarApp> createState()=>_LumarAppState();
}
class _LumarAppState extends State<LumarApp> {
	final auth=AuthState();
	final themeState=ThemeState();
	final uiScale=UiScaleState();
	final sidebarState=SidebarState();
	@override void initState(){super.initState();auth.initialize();themeState.initialize();uiScale.initialize();sidebarState.initialize();}

	@override
	Widget build(BuildContext context) => ListenableBuilder(listenable:Listenable.merge([auth, themeState, uiScale, sidebarState]),builder:(context,_)=>MaterialApp(
		title: 'لومار لإدارة الأعمال',
		navigatorKey: AppNavigation.navigatorKey,
		navigatorObservers: [AppNavigation.observer],
		locale: const Locale('ar'),
		supportedLocales: const [Locale('ar')],
		localizationsDelegates: const [GlobalMaterialLocalizations.delegate, GlobalWidgetsLocalizations.delegate, GlobalCupertinoLocalizations.delegate],
		builder: (context, child) => AppNavigationRegion(child: KeyboardPolicy(uiScale: uiScale, child: Directionality(textDirection: TextDirection.rtl, child: MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(uiScale.scale)), child: child!)))),
		themeMode: themeState.themeMode,
		theme: _appTheme(Brightness.light, uiScale.scale),
		darkTheme: _appTheme(Brightness.dark, uiScale.scale),
		home: !auth.initialized || !themeState.initialized || !uiScale.initialized || !sidebarState.initialized?const Scaffold(body:Center(child:CircularProgressIndicator())):auth.signedIn?MainShell(auth:auth, themeState:themeState, uiScale:uiScale, sidebarState: sidebarState):LoginScreen(auth:auth),
		onGenerateRoute: (settings)=>AppRoutes.onGenerateRoute(settings,auth,themeState,uiScale,sidebarState),
	));

	ThemeData _appTheme(Brightness brightness, double scale) {
		final isDark = brightness == Brightness.dark;
		final scheme = ColorScheme.fromSeed(
			seedColor: UiPalette.primaryBlue,
			brightness: brightness,
		).copyWith(
			primary: UiPalette.primaryBlue,
			onPrimary: UiPalette.textMain,
			secondary: UiPalette.purpleAccent,
			onSecondary: UiPalette.textMain,
			surface: UiPalette.screenBackground,
			onSurface: UiPalette.textMain,
			surfaceContainerLowest: UiPalette.screenBackground,
			surfaceContainerLow: UiPalette.softBlue,
			surfaceContainer: UiPalette.softBlue,
			surfaceContainerHigh: UiPalette.surfaceCard,
			surfaceContainerHighest: UiPalette.surfaceCard,
			outline: UiPalette.borderSoft,
			outlineVariant: UiPalette.borderSoft,
		);
		final density = (scale - 1) * 2;
		return ThemeData(
			useMaterial3: true,
			brightness: brightness,
			colorScheme: scheme,
			visualDensity: VisualDensity(horizontal: density, vertical: density),
			scaffoldBackgroundColor: isDark ? UiPalette.screenBackground : const Color(0xFFF5F7FB),
			cardTheme: CardThemeData(
				color: isDark ? UiPalette.surfaceCard : const Color(0xFFFFFFFF),
				surfaceTintColor: Colors.transparent,
				margin: EdgeInsets.all(4 * scale),
			),
			navigationRailTheme: NavigationRailThemeData(
				backgroundColor: isDark ? UiPalette.softBlue : const Color(0xFFEAF2FF),
				minWidth: 72 * scale,
				groupAlignment: -1,
			),
			textTheme: ThemeData(brightness: brightness).textTheme.apply(
				bodyColor: UiPalette.textMain,
				displayColor: UiPalette.textMain,
			),
		);
	}
}

