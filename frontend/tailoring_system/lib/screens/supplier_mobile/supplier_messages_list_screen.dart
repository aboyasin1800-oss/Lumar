import 'package:flutter/material.dart';

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';
import '../../models/supplier_mobile_models.dart';
import '../../repositories/supplier_mobile_repository.dart';
import '../../services/auth_state.dart';
import 'supplier_message_detail_screen.dart';

class SupplierMessagesListScreen extends StatefulWidget {
  const SupplierMessagesListScreen({super.key, this.repository, this.auth});

  final SupplierMobileRepository? repository;
  final AuthState? auth;

  @override
  State<SupplierMessagesListScreen> createState() => _SupplierMessagesListScreenState();
}

class _SupplierMessagesListScreenState extends State<SupplierMessagesListScreen> {
  late final SupplierMobileRepository _repository =
      widget.repository ?? SupplierMobileRepository(auth: widget.auth);

  late Future<List<SupplierMessage>> _future;

  @override
  void initState() {
    super.initState();
    _future = _repository.getMessages(page: 1, pageSize: 20);
  }

  Future<void> _refresh() async {
    setState(() {
      _future = _repository.getMessages(page: 1, pageSize: 20);
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
        title: const Text('الرسائل'),
      ),
      body: FutureBuilder<List<SupplierMessage>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Text(
                'تعذر تحميل الرسائل.\n${snapshot.error}',
                style: const TextStyle(color: UiPalette.textMain),
              ),
            );
          }

          final messages = snapshot.data ?? const <SupplierMessage>[];

          if (messages.isEmpty) {
            return const Center(
              child: Text('لا توجد رسائل.', style: TextStyle(color: UiPalette.textMain)),
            );
          }

          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: messages.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final item = messages[index];
                return Material(
                  color: UiPalette.surfaceCard,
                  borderRadius: BorderRadius.circular(14),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    title: Text(
                      item.subject.isEmpty ? 'رسالة بدون عنوان' : item.subject,
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
                      (_) => SupplierMessageDetailScreen(
                        repository: _repository,
                        message: item,
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
