import 'package:flutter/material.dart';

import '../screens/inventory/bulk_fabric_entry_screen.dart';
import '../screens/login_screen.dart';
import '../services/auth_state.dart';
import '../services/theme_state.dart';
import '../services/ui_scale_state.dart';
import '../services/workspace_controller.dart';
import '../services/workspace_registry.dart';
import '../widgets/main_shell.dart';
import '../widgets/structure_placeholder.dart';
import '../screens/module_screens.dart';

class AppRoutes {
  static const login = WorkspaceRouteIds.login;
  static const dashboard = WorkspaceRouteIds.dashboard;
  static const designSystemDemo = WorkspaceRouteIds.designSystemDemo;
  static const referralDashboard = WorkspaceRouteIds.referralDashboard;
  static const referralTree = WorkspaceRouteIds.referralTree;
  static const referralHistory = WorkspaceRouteIds.referralHistory;
  static const referralCodes = WorkspaceRouteIds.referralCodes;
  static const referralRewards = WorkspaceRouteIds.referralRewards;
  static const referralAnalytics = WorkspaceRouteIds.referralAnalytics;
  static const loyaltyDashboard = WorkspaceRouteIds.loyaltyDashboard;
  static const loyaltyTransactions = WorkspaceRouteIds.loyaltyTransactions;
  static const loyaltyRedemption = WorkspaceRouteIds.loyaltyRedemption;
  static const loyaltyRedemptionsHistory = WorkspaceRouteIds.loyaltyRedemptionsHistory;
  static const loyaltyRewards = WorkspaceRouteIds.loyaltyRewards;
  static const loyaltyRules = WorkspaceRouteIds.loyaltyRules;
  static const vipLevels = WorkspaceRouteIds.vipLevels;
  static const pointsSettings = WorkspaceRouteIds.pointsSettings;
  static const piecePointSettings = WorkspaceRouteIds.piecePointSettings;
  static const importedProductPointSettings = WorkspaceRouteIds.importedProductPointSettings;
  static const readyMadeProductPointSettings = WorkspaceRouteIds.readyMadeProductPointSettings;

  static List<WorkspaceRouteDefinition> get destinations => workspaceRegistry;

  static Route<void> onGenerateRoute(
    RouteSettings settings,
    AuthState auth,
    ThemeState themeState,
    UiScaleState uiScale,
    WorkspaceController workspace,
  ) {
    if (settings.name == login) {
      return MaterialPageRoute(
        builder: (_) => LoginScreen(auth: auth),
        settings: settings,
      );
    }

    if (!auth.signedIn) {
      return MaterialPageRoute(
        builder: (_) => LoginScreen(auth: auth),
        settings: settings,
      );
    }

    if (settings.name == dashboard) {
      workspace.open(WorkspaceRouteIds.dashboard);
      return MaterialPageRoute(
        builder: (_) => MainShell(
          auth: auth,
          themeState: themeState,
          uiScale: uiScale,
          workspace: workspace,
        ),
        settings: settings,
      );
    }

    if (settings.name == '/structure/إدخال الأقمشة بالجملة' ||
        settings.name == '/inventory/bulk-entry') {
      return MaterialPageRoute(
        builder: (_) => const BulkFabricEntryScreen(),
        settings: settings,
      );
    }

    if (settings.name?.startsWith('/structure/') ?? false) {
      final title = Uri.decodeComponent(
        settings.name!.substring('/structure/'.length),
      );
      return MaterialPageRoute(
        builder: (_) => StructurePlaceholder(
          title: title,
          description: 'واجهة منظمة ضمن هيكل وحدة $title.',
        ),
        settings: settings,
      );
    }

    final definition = settings.name == null
        ? null
        : workspace.definitionFor(settings.name!);
    if (definition != null) {
      return MaterialPageRoute(
        builder: (_) => _WorkspaceRedirectScreen(
          routeId: definition.routeId,
          workspace: workspace,
        ),
        settings: settings,
      );
    }

    return MaterialPageRoute(
      builder: (_) => MainShell(
        auth: auth,
        themeState: themeState,
        uiScale: uiScale,
        workspace: workspace,
      ),
      settings: settings,
    );
  }
}

class _WorkspaceRedirectScreen extends StatefulWidget {
  const _WorkspaceRedirectScreen({required this.routeId, required this.workspace});

  final String routeId;
  final WorkspaceController workspace;

  @override
  State<_WorkspaceRedirectScreen> createState() => _WorkspaceRedirectScreenState();
}

class _WorkspaceRedirectScreenState extends State<_WorkspaceRedirectScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      widget.workspace.open(widget.routeId);
      if (mounted && Navigator.of(context).canPop()) Navigator.of(context).pop();
    });
  }

  @override
  Widget build(BuildContext context) => const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
}
