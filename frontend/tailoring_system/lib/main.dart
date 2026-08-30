import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/app_routes.dart';
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
		locale: const Locale('ar'),
		supportedLocales: const [Locale('ar')],
		localizationsDelegates: const [GlobalMaterialLocalizations.delegate, GlobalWidgetsLocalizations.delegate, GlobalCupertinoLocalizations.delegate],
		builder: (context, child) => KeyboardPolicy(uiScale: uiScale, child: Directionality(textDirection: TextDirection.rtl, child: MediaQuery(data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(uiScale.scale)), child: child!))),
		themeMode: themeState.themeMode,
		theme: _appTheme(Brightness.light, uiScale.scale),
		darkTheme: _appTheme(Brightness.dark, uiScale.scale),
		home: !auth.initialized || !themeState.initialized || !uiScale.initialized || !sidebarState.initialized?const Scaffold(body:Center(child:CircularProgressIndicator())):auth.signedIn?MainShell(auth:auth, themeState:themeState, uiScale:uiScale, sidebarState: sidebarState):LoginScreen(auth:auth),
		onGenerateRoute: (settings)=>AppRoutes.onGenerateRoute(settings,auth,themeState,uiScale,sidebarState),
	));

	ThemeData _appTheme(Brightness brightness, double scale) {
		final isDark = brightness == Brightness.dark;
		final scheme = ColorScheme.fromSeed(seedColor: const Color(0xff2f6f6d), brightness: brightness);
		final density = (scale - 1) * 2;
		return ThemeData(
			useMaterial3: true,
			brightness: brightness,
			colorScheme: scheme,
			visualDensity: VisualDensity(horizontal: density, vertical: density),
			scaffoldBackgroundColor: isDark ? const Color(0xff171c1c) : const Color(0xfff4f7f6),
			cardTheme: CardThemeData(color: isDark ? const Color(0xff242b2b) : Colors.white, surfaceTintColor: Colors.transparent, margin: EdgeInsets.all(4 * scale)),
			navigationRailTheme: NavigationRailThemeData(backgroundColor: isDark ? const Color(0xff202727) : const Color(0xffeaf1f0), minWidth: 72 * scale, groupAlignment: -1),
		);
	}
}

