import 'package:flutter/material.dart';

import '../core/app_navigation.dart';
import '../services/workspace_controller.dart';

class WorkspaceTaskCloseButton extends StatelessWidget {
  const WorkspaceTaskCloseButton({
    required this.controller,
    required this.routeId,
    super.key,
  });

  final WorkspaceController controller;
  final String routeId;

  @override
  Widget build(BuildContext context) => Semantics(
        label: 'إغلاق الشاشة',
        child: IconButton(
          onPressed: () => close(context, controller, routeId),
          icon: Icon(Icons.close_rounded, color: Theme.of(context).colorScheme.error),
        ),
      );

  static Future<void> close(
    BuildContext context,
    WorkspaceController controller,
    String routeId, {
    VoidCallback? beforeClose,
  }
  ) async {
    final task = controller.taskFor(routeId);
    if (task == null) return;
    if (!task.isDirty) {
      beforeClose?.call();
      controller.close(routeId);
      return;
    }

    controller.beginClose(routeId);
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
      beforeClose?.call();
      controller.closeAfterConfirmation(routeId);
    } else {
      controller.cancelClose(routeId);
    }
  }

  static Future<void> closeActiveTask(
    BuildContext context,
    WorkspaceController controller,
  ) {
    final routeId = controller.activeRouteId;
    if (routeId == null) return Future.value();
    return close(
      context,
      controller,
      routeId,
      beforeClose: () => AppNavigation.navigatorKey.currentState
          ?.popUntil((route) => route.isFirst),
    );
  }
}
