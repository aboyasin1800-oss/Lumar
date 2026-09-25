import 'package:flutter/material.dart';

import 'workspace_controller.dart';

abstract final class WorkspaceRouteIds {
  static const dashboard = '/dashboard';
  static const login = '/login';
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
  static const importedProductPointSettings = '/imported-product-point-settings';
  static const readyMadeProductPointSettings = '/ready-made-product-point-settings';
}

WorkspaceRouteDefinition workspaceDefinition({
  required String routeId,
  required String title,
  required IconData icon,
  required WorkspaceScreenFactory factory,
  WorkspaceStatePolicy statePolicy = WorkspaceStatePolicy.keepAlive,
  WorkspacePerformanceClass performanceClass = WorkspacePerformanceClass.light,
  bool canOpenMultipleInstances = false,
  bool restorationEnabled = false,
}) {
  return WorkspaceRouteDefinition(
    routeId: routeId,
    title: title,
    icon: icon,
    factory: factory,
    statePolicy: statePolicy,
    performanceClass: performanceClass,
    canOpenMultipleInstances: canOpenMultipleInstances,
    restorationEnabled: restorationEnabled,
  );
}
