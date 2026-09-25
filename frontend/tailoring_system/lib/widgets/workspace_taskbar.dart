import 'package:flutter/material.dart';

import '../core/ui_palette.dart';
import '../services/workspace_controller.dart';

class WorkspaceTaskbar extends StatelessWidget {
  const WorkspaceTaskbar({required this.controller, super.key});

  final WorkspaceController controller;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: controller,
      builder: (context, _) {
        final background = UiPalette.surfaceCard;
        final foreground = UiPalette.adaptiveTextColor(background);
        return Container(
          height: controller.taskbarSize.height,
          decoration: BoxDecoration(
            color: background,
            border: Border(top: BorderSide(color: UiPalette.borderSoft)),
          ),
          child: Row(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
                  child: Row(
                    children: controller.taskbarTasks
                        .map((task) => _TaskbarItem(
                              controller: controller,
                              task: task,
                            ))
                        .toList(),
                  ),
                ),
              ),
              PopupMenuButton<WorkspaceTaskbarSize>(
                tooltip: 'حجم شريط المهام',
                icon: Icon(Icons.tune_outlined, color: foreground, size: 19),
                onSelected: controller.setTaskbarSize,
                itemBuilder: (context) => WorkspaceTaskbarSize.values
                    .map((size) => PopupMenuItem<WorkspaceTaskbarSize>(
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
                        ))
                    .toList(),
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
    final background = isActive ? UiPalette.primaryDark : UiPalette.softBlue;
    final foreground = UiPalette.adaptiveTextColor(background);
    final isPending = task.state == WorkspaceTaskState.closingPending;

    return Padding(
      padding: const EdgeInsetsDirectional.only(end: 6),
      child: Tooltip(
        message: task.definition.title,
        child: GestureDetector(
          onSecondaryTapDown: (details) => _showContextMenu(context, details),
          child: Material(
            color: background,
            borderRadius: BorderRadius.circular(7),
            child: InkWell(
              borderRadius: BorderRadius.circular(7),
              onTap: () => controller.toggle(task.definition.routeId),
              child: ConstrainedBox(
                constraints: const BoxConstraints(minWidth: 112, maxWidth: 190),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(task.definition.icon, size: 17, color: foreground),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Text(
                          task.definition.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            color: foreground,
                            fontSize: 12,
                            fontWeight: isActive ? FontWeight.bold : FontWeight.w500,
                          ),
                        ),
                      ),
                      if (task.isPinned) ...[
                        const SizedBox(width: 5),
                        Icon(Icons.push_pin_outlined, size: 13, color: foreground),
                      ],
                      if (isPending) ...[
                        const SizedBox(width: 5),
                        Icon(Icons.hourglass_top, size: 13, color: foreground),
                      ],
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
      items: [
        const PopupMenuItem(
          value: _TaskbarAction.open,
          child: Text('فتح أو استعادة'),
        ),
        if (isActive)
          const PopupMenuItem(
            value: _TaskbarAction.minimize,
            child: Text('تصغير'),
          ),
        const PopupMenuItem(
          value: _TaskbarAction.close,
          child: Text('إغلاق'),
        ),
        PopupMenuItem(
          value: task.isPinned ? _TaskbarAction.unpin : _TaskbarAction.pin,
          child: Text(task.isPinned ? 'إلغاء التثبيت من شريط المهام' : 'تثبيت في شريط المهام'),
        ),
      ],
    );

    if (!context.mounted) return;
    switch (action) {
      case _TaskbarAction.open:
        controller.open(task.definition.routeId);
      case _TaskbarAction.minimize:
        controller.minimize(task.definition.routeId);
      case _TaskbarAction.close:
        await _closeTask(context);
      case _TaskbarAction.pin:
        await controller.pin(task.definition.routeId);
      case _TaskbarAction.unpin:
        await controller.unpin(task.definition.routeId);
      case null:
        break;
    }
  }

  Future<void> _closeTask(BuildContext context) async {
    if (!task.isDirty) {
      controller.close(task.definition.routeId);
      return;
    }

    controller.beginClose(task.definition.routeId);
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
      controller.closeAfterConfirmation(task.definition.routeId);
    } else {
      controller.cancelClose(task.definition.routeId);
    }
  }
}

enum _TaskbarAction { open, minimize, close, pin, unpin }
