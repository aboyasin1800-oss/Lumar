import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/app_navigation.dart';
import 'core/app_routes.dart';
import 'core/theme/app_theme.dart';
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
  @override
  State<LumarApp> createState() => _LumarAppState();
}

class _LumarAppState extends State<LumarApp> {
  final auth = AuthState();
  final themeState = ThemeState();
  final uiScale = UiScaleState();
  final sidebarState = SidebarState();
  @override
  void initState() {
    super.initState();
    auth.initialize();
    themeState.initialize();
    uiScale.initialize();
    sidebarState.initialize();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
      listenable: Listenable.merge([auth, themeState, uiScale, sidebarState]),
      builder: (context, _) => MaterialApp(
            title: 'لومار لإدارة الأعمال',
            navigatorKey: AppNavigation.navigatorKey,
            navigatorObservers: [AppNavigation.observer],
            locale: const Locale('ar'),
            supportedLocales: const [Locale('ar')],
            localizationsDelegates: const [
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate
            ],
            builder: (context, child) => AppNavigationRegion(
                child: KeyboardPolicy(
                    uiScale: uiScale,
                    child: Directionality(
                        textDirection: TextDirection.rtl,
                        child: _ScaledAppContent(
                          scale: uiScale.scale,
                          child: child!,
                        )))),
            themeMode: themeState.themeMode,
            theme: AppTheme.light(scale: uiScale.scale),
            darkTheme: AppTheme.dark(scale: uiScale.scale),
            home: !auth.initialized ||
                    !themeState.initialized ||
                    !uiScale.initialized ||
                    !sidebarState.initialized
                ? const Scaffold(
                    body: Center(child: CircularProgressIndicator()))
                : auth.signedIn
                    ? MainShell(
                        auth: auth,
                        themeState: themeState,
                        uiScale: uiScale,
                        sidebarState: sidebarState)
                    : LoginScreen(auth: auth),
            onGenerateRoute: (settings) => AppRoutes.onGenerateRoute(
                settings, auth, themeState, uiScale, sidebarState),
          ));
}

class _ScaledAppContent extends StatelessWidget {
  const _ScaledAppContent({required this.scale, required this.child});

  final double scale;
  final Widget child;

  EdgeInsets _scaledInsets(EdgeInsets insets, double factor) =>
      EdgeInsets.fromLTRB(
        insets.left / factor,
        insets.top / factor,
        insets.right / factor,
        insets.bottom / factor,
      );

  @override
  Widget build(BuildContext context) {
    final mediaQuery = MediaQuery.of(context);
    final factor = scale.clamp(0.2, 1.5).toDouble();
    if (factor == 1) return child;

    final viewport = mediaQuery.size;
    final contentSize = Size(
      viewport.width / factor,
      viewport.height / factor,
    );
    final contentMediaQuery = mediaQuery.copyWith(
      size: contentSize,
      padding: _scaledInsets(mediaQuery.padding, factor),
      viewPadding: _scaledInsets(mediaQuery.viewPadding, factor),
      viewInsets: _scaledInsets(mediaQuery.viewInsets, factor),
    );

    return SizedBox(
      width: viewport.width,
      height: viewport.height,
      child: ClipRect(
        child: OverflowBox(
          alignment: Alignment.topLeft,
          minWidth: 0,
          maxWidth: double.infinity,
          minHeight: 0,
          maxHeight: double.infinity,
          child: Transform.scale(
            scale: factor,
            alignment: Alignment.topLeft,
            child: SizedBox(
              width: contentSize.width,
              height: contentSize.height,
              child: MediaQuery(
                data: contentMediaQuery,
                child: child,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
