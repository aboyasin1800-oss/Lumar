import 'package:flutter/material.dart';

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';
import '../../models/supplier_mobile_models.dart';
import '../../repositories/supplier_mobile_repository.dart';
import '../../services/auth_state.dart';
import 'supplier_notification_detail_screen.dart';

class SupplierNotificationsListScreen extends StatefulWidget {
  const SupplierNotificationsListScreen({super.key, this.repository, this.auth});

  final SupplierMobileRepository? repository;
  final AuthState? auth;

  @override
  State<SupplierNotificationsListScreen> createState() => _SupplierNotificationsListScreenState();
}

class _SupplierNotificationsListScreenState extends State<SupplierNotificationsListScreen> {
  late final SupplierMobileRepository _repository =
      widget.repository ?? SupplierMobileRepository(auth: widget.auth);

  late Future<List<SupplierNotification>> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getNotifications(page: 1, pageSize: 20);
  }

  Future<void> _refresh() async {
    setState(() {
      _future = _repository.getNotifications(page: 1, pageSize: 20);
    });
    await _future;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.primary,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
        title: const Text('الإشعارات'),
      ),
      body: FutureBuilder<List<SupplierNotification>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Text(
                'تعذر تحميل الإشعارات.\n${snapshot.error}',
                style: const TextStyle(color: UiPalette.textMain),
              ),
            );
          }

          final items = snapshot.data ?? const <SupplierNotification>[];
          if (items.isEmpty) {
            return const Center(
              child: Text('لا توجد إشعارات.', style: TextStyle(color: UiPalette.textMain)),
            );
          }

          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: items.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final item = items[index];
                return Material(
                  color: UiPalette.surfaceCard,
                  borderRadius: BorderRadius.circular(14),
                  child: ListTile(
                    title: Text(
                      item.title.isEmpty ? 'إشعار' : item.title,
                      style: const TextStyle(color: UiPalette.textMain, fontWeight: FontWeight.bold),
                    ),
                    subtitle: Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        item.body,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(color: UiPalette.textSoft),
                      ),
                    ),
                    trailing: Icon(Icons.chevron_left_rounded, color: UiPalette.primary),
                    onTap: () => AppNavigation.push(
                      context,
                      (_) => SupplierNotificationDetailScreen(
                        repository: _repository,
                        notification: item,
                      ),
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
