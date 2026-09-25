import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';

import '../core/ui_palette.dart';
import '../services/workspace_controller.dart';
import '../services/workspace_registry.dart';

enum WorkspaceModuleType {
  dashboard,
  sales,
  tailoring,
  production,
  inventory,
  customers,
  finance,
  settings,
  employees,
  loyalty,
  referrals,
  reports,
  administration,
  communications,
  delivery,
  unknown,
}

abstract final class WorkspaceIconRegistry {
  static const Map<WorkspaceModuleType, IconData> _icons = {
    WorkspaceModuleType.dashboard: Icons.dashboard_outlined,
    WorkspaceModuleType.sales: Icons.point_of_sale_outlined,
    WorkspaceModuleType.tailoring: Icons.content_cut_outlined,
    WorkspaceModuleType.production: Icons.precision_manufacturing_outlined,
    WorkspaceModuleType.inventory: Icons.inventory_2_outlined,
    WorkspaceModuleType.customers: Icons.people_outline,
    WorkspaceModuleType.finance: Icons.account_balance_outlined,
    WorkspaceModuleType.settings: Icons.settings_outlined,
    WorkspaceModuleType.employees: Icons.badge_outlined,
    WorkspaceModuleType.loyalty: Icons.card_giftcard_outlined,
    WorkspaceModuleType.referrals: Icons.share_outlined,
    WorkspaceModuleType.reports: Icons.analytics_outlined,
    WorkspaceModuleType.administration: Icons.admin_panel_settings_outlined,
    WorkspaceModuleType.communications: Icons.forum_outlined,
    WorkspaceModuleType.delivery: Icons.local_shipping_outlined,
    WorkspaceModuleType.unknown: Icons.apps_outlined,
  };

  static const Map<String, WorkspaceModuleType> _routeTypes = {
    WorkspaceRouteIds.dashboard: WorkspaceModuleType.dashboard,
    '/customers': WorkspaceModuleType.customers,
    '/sales': WorkspaceModuleType.sales,
    '/orders': WorkspaceModuleType.sales,
    '/measurements': WorkspaceModuleType.tailoring,
    '/production': WorkspaceModuleType.production,
    '/printing': WorkspaceModuleType.production,
    '/inventory': WorkspaceModuleType.inventory,
    '/purchasing': WorkspaceModuleType.inventory,
    '/employees': WorkspaceModuleType.employees,
    '/payroll': WorkspaceModuleType.finance,
    '/employee-draws': WorkspaceModuleType.finance,
    '/finance': WorkspaceModuleType.finance,
    '/pricing': WorkspaceModuleType.settings,
    '/loyalty': WorkspaceModuleType.loyalty,
    '/messages': WorkspaceModuleType.communications,
    '/reports': WorkspaceModuleType.reports,
    '/administration': WorkspaceModuleType.administration,
    '/settings': WorkspaceModuleType.settings,
    '/ready-made-production': WorkspaceModuleType.production,
    '/factory-monitoring': WorkspaceModuleType.production,
    '/delivery-dashboard': WorkspaceModuleType.delivery,
    '/design-system-demo': WorkspaceModuleType.settings,
    '/lumar-erp': WorkspaceModuleType.administration,
    WorkspaceRouteIds.referralDashboard: WorkspaceModuleType.referrals,
    WorkspaceRouteIds.referralTree: WorkspaceModuleType.referrals,
    WorkspaceRouteIds.referralHistory: WorkspaceModuleType.referrals,
    WorkspaceRouteIds.referralCodes: WorkspaceModuleType.referrals,
    WorkspaceRouteIds.referralRewards: WorkspaceModuleType.referrals,
    WorkspaceRouteIds.referralAnalytics: WorkspaceModuleType.referrals,
    WorkspaceRouteIds.loyaltyDashboard: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.loyaltyTransactions: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.loyaltyRedemption: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.loyaltyRedemptionsHistory: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.loyaltyRewards: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.loyaltyRules: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.vipLevels: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.pointsSettings: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.piecePointSettings: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.readyMadeProductPointSettings: WorkspaceModuleType.loyalty,
    WorkspaceRouteIds.importedProductPointSettings: WorkspaceModuleType.loyalty,
  };

  static WorkspaceModuleType typeForRoute(String routeId) =>
      _routeTypes[routeId] ?? WorkspaceModuleType.unknown;

  static IconData iconForRoute(String routeId) => _icons[typeForRoute(routeId)]!;
}

class WorkspaceTaskbar extends StatelessWidget {
  const WorkspaceTaskbar({required this.controller, super.key});

  final WorkspaceController controller;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: controller,
      builder: (context, _) {
        final colorScheme = Theme.of(context).colorScheme;
        final taskbarHeight = controller.taskbarSize.height.clamp(48.0, 56.0).toDouble();
        return Container(
          height: taskbarHeight,
          decoration: BoxDecoration(
            color: colorScheme.surface,
            border: Border(top: BorderSide(color: colorScheme.outlineVariant)),
          ),
          child: Row(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  child: Row(
                    children: controller.taskbarTasks
                        .map(
                          (task) => _TaskbarItem(
                            controller: controller,
                            task: task,
                          ),
                        )
                        .toList(growable: false),
                  ),
                ),
              ),
              PopupMenuButton<WorkspaceTaskbarSize>(
                tooltip: 'حجم شريط المهام',
                icon: Icon(Icons.tune_outlined, color: colorScheme.onSurface, size: 19),
                onSelected: controller.setTaskbarSize,
                itemBuilder: (context) => WorkspaceTaskbarSize.values
                    .map(
                      (size) => PopupMenuItem<WorkspaceTaskbarSize>(
                        value: size,
                        child: Row(
                          children: [
                            if (size == controller.taskbarSize)
                              const Icon(Icons.check, size: 17),
                            if (size == controller.taskbarSize)
                              const SizedBox(width: 8),
                            Text(size.arabicLabel),
                          ],
                        ),
                      ),
                    )
                    .toList(growable: false),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _TaskbarItem extends StatelessWidget {
  const _TaskbarItem({required this.controller, required this.task});

  final WorkspaceController controller;
  final WorkspaceTask task;

  bool get isActive => controller.activeRouteId == task.definition.routeId;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final iconColor = isActive ? UiPalette.primary : colorScheme.onSurfaceVariant;
    final isOpenInactive = task.isOpen && !isActive;

    return Padding(
      padding: const EdgeInsetsDirectional.only(end: 4),
      child: Tooltip(
        message: task.definition.title,
        waitDuration: Duration.zero,
        child: Listener(
          onPointerDown: (event) => _handlePointerDown(context, event),
          child: GestureDetector(
            onSecondaryTapDown: (details) => _showContextMenu(context, details),
            child: Material(
              color: Colors.transparent,
              child: InkWell(
                borderRadius: BorderRadius.circular(8),
                onTap: () => controller.toggle(task.definition.routeId),
                child: SizedBox(
                  width: 52,
                  height: 48,
                  child: Stack(
                    alignment: Alignment.center,
                    children: [
                      AnimatedContainer(
                        duration: const Duration(milliseconds: 140),
                        curve: Curves.easeOut,
                        width: 40,
                        height: 38,
                        decoration: BoxDecoration(
                          color: isActive
                              ? UiPalette.primary.withValues(alpha: 0.16)
                              : Colors.transparent,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Icon(
                          WorkspaceIconRegistry.iconForRoute(task.definition.routeId),
                          color: iconColor,
                          size: 22,
                        ),
                      ),
                      if (isActive)
                        Positioned(
                          right: 11,
                          bottom: 2,
                          left: 11,
                          child: Container(
                            height: 3,
                            decoration: BoxDecoration(
                              color: UiPalette.primary,
                              borderRadius: BorderRadius.circular(2),
                            ),
                          ),
                        )
                      else if (isOpenInactive)
                        Positioned(
                          bottom: 3,
                          child: Container(
                            width: 4,
                            height: 4,
                            decoration: BoxDecoration(
                              color: colorScheme.onSurfaceVariant,
                              shape: BoxShape.circle,
                            ),
                          ),
                        ),
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

  void _handlePointerDown(BuildContext context, PointerDownEvent event) {
    if (event.kind == PointerDeviceKind.mouse && event.buttons & kMiddleMouseButton != 0) {
      _closeTask(context);
    }
  }

  Future<void> _showContextMenu(
    BuildContext context,
    TapDownDetails details,
  ) async {
    final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;
    final position = RelativeRect.fromRect(
      Rect.fromLTWH(details.globalPosition.dx, details.globalPosition.dy, 1, 1),
      Offset.zero & overlay.size,
    );
    final action = await showMenu<_TaskbarAction>(
      context: context,
      position: position,
      items: const [
        PopupMenuItem(
          value: _TaskbarAction.activate,
          child: Text('تنشيط الشاشة'),
        ),
        PopupMenuItem(
          value: _TaskbarAction.close,
          child: Text('إغلاق الشاشة'),
        ),
        PopupMenuItem(
          value: _TaskbarAction.closeOthers,
          child: Text('إغلاق جميع الشاشات الأخرى'),
        ),
        PopupMenuItem(
          value: _TaskbarAction.closeAll,
          child: Text('إغلاق جميع الشاشات'),
        ),
      ],
    );

    if (!context.mounted) return;
    switch (action) {
      case _TaskbarAction.activate:
        controller.open(task.definition.routeId);
      case _TaskbarAction.close:
        await _closeTask(context);
      case _TaskbarAction.closeOthers:
        await _closeTasks(
          controller.openTasks
              .where((candidate) => candidate.definition.routeId != task.definition.routeId)
              .toList(growable: false),
          context,
        );
      case _TaskbarAction.closeAll:
        await _closeTasks(controller.openTasks.toList(growable: false), context);
      case null:
        break;
    }
  }

  Future<void> _closeTasks(List<WorkspaceTask> tasks, BuildContext context) async {
    for (final candidate in tasks) {
      if (!context.mounted) return;
      await _closeTask(context, candidate);
    }
  }

  Future<void> _closeTask(BuildContext context, [WorkspaceTask? target]) async {
    final taskToClose = target ?? task;
    if (!taskToClose.isDirty) {
      controller.close(taskToClose.definition.routeId);
      return;
    }

    controller.beginClose(taskToClose.definition.routeId);
    final shouldClose = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('تغييرات غير محفوظة'),
        content: const Text('توجد تغييرات غير محفوظة. هل تريد إغلاق الشاشة؟'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('العودة إلى الشاشة'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('إغلاق دون حفظ'),
          ),
        ],
      ),
    );
    if (shouldClose == true) {
      controller.closeAfterConfirmation(taskToClose.definition.routeId);
    } else {
      controller.cancelClose(taskToClose.definition.routeId);
    }
  }
}

enum _TaskbarAction { activate, close, closeOthers, closeAll }
