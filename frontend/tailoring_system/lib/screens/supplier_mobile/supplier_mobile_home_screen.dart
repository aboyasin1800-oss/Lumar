import 'package:flutter/material.dart';

import '../../core/app_navigation.dart';
import '../../core/ui_palette.dart';
import '../../repositories/supplier_mobile_repository.dart';
import '../../services/auth_state.dart';
import 'supplier_announcements_list_screen.dart';
import 'supplier_messages_list_screen.dart';
import 'supplier_notifications_list_screen.dart';

class SupplierMobileHomeScreen extends StatefulWidget {
  const SupplierMobileHomeScreen({super.key, this.repository, this.auth});

  final SupplierMobileRepository? repository;
  final AuthState? auth;

  @override
  State<SupplierMobileHomeScreen> createState() => _SupplierMobileHomeScreenState();
}

class _SupplierMobileHomeScreenState extends State<SupplierMobileHomeScreen> {
  late final SupplierMobileRepository _repository =
      widget.repository ?? SupplierMobileRepository(auth: widget.auth);

  late Future<List<dynamic>> _future;

  @override
  void initState() {
    super.initState();
    _future = Future.wait([
      _repository.getProfile(),
      _repository.getHomeSummary(),
    ]);
  }

  Future<void> _refresh() async {
    setState(() {
      _future = Future.wait([
        _repository.getProfile(),
        _repository.getHomeSummary(),
      ]);
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
        title: const Text('لوحة المورد'),
      ),
      body: FutureBuilder<List<dynamic>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }

          if (snapshot.hasError) {
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Text(
                  'تعذر تحميل بيانات المورد.\n${snapshot.error}',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: UiPalette.textMain),
                ),
              ),
            );
          }

          final profile = snapshot.data?[0];
          final summary = snapshot.data?[1];

          if (profile == null || summary == null) {
            return const Center(child: Text('لا توجد بيانات متاحة حالياً.', style: TextStyle(color: UiPalette.textMain)));
          }

          return RefreshIndicator(
            onRefresh: _refresh,
            child: ListView(
              padding: const EdgeInsets.all(20),
              children: [
                _ProfileHeader(profile: profile),
                const SizedBox(height: 16),
                _MetricGrid(summary: summary),
                const SizedBox(height: 16),
                _QuickActionGrid(repository: _repository),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({required this.profile});

  final dynamic profile;

  @override
  Widget build(BuildContext context) {
    final name = profile.supplierName;
    final code = profile.supplierCode;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 30,
            backgroundColor: UiPalette.primary,
            child: Icon(
              Icons.business_center_rounded,
              color: UiPalette.adaptiveTextColor(UiPalette.primary),
              size: 26,
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: UiPalette.textMain,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  'رمز المورد: $code',
                  style: const TextStyle(color: UiPalette.textSoft),
                ),
                const SizedBox(height: 8),
                Text(
                  profile.isActive ? 'نشط' : 'غير نشط',
                  style: TextStyle(
                    color: profile.isActive ? UiPalette.primary : Colors.orangeAccent,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _MetricGrid extends StatelessWidget {
  const _MetricGrid({required this.summary});

  final dynamic summary;

  @override
  Widget build(BuildContext context) {
    final items = [
      _MetricCard(
        label: 'الرصيد الحالي',
        value: '${summary.currentBalance.toStringAsFixed(2)} ر.س',
        color: UiPalette.primary,
      ),
      _MetricCard(
        label: 'الفواتير',
        value: '${summary.invoiceCount}',
        color: Colors.blueAccent,
      ),
      _MetricCard(
        label: 'الدفعات',
        value: '${summary.paymentCount}',
        color: Colors.green,
      ),
      _MetricCard(
        label: 'إيصالات الاستلام',
        value: '${summary.goodsReceiptCount}',
        color: Colors.orangeAccent,
      ),
      _MetricCard(
        label: 'الموافقات المعلقة',
        value: '${summary.pendingAcknowledgements}',
        color: Colors.deepPurpleAccent,
      ),
      _MetricCard(
        label: 'المنازعات المفتوحة',
        value: '${summary.openDisputes}',
        color: Colors.redAccent,
      ),
    ];

    return GridView.builder(
      physics: const NeverScrollableScrollPhysics(),
      shrinkWrap: true,
      itemCount: items.length,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        crossAxisSpacing: 12,
        mainAxisSpacing: 12,
        childAspectRatio: 1.7,
      ),
      itemBuilder: (_, index) => items[index],
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.label,
    required this.value,
    required this.color,
  });

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.circle, color: color, size: 10),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  label,
                  style: const TextStyle(color: UiPalette.textSoft, fontSize: 12),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            value,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: UiPalette.textMain,
              fontWeight: FontWeight.bold,
            ),
          ),
        ],
      ),
    );
  }
}

class _QuickActionGrid extends StatelessWidget {
  const _QuickActionGrid({required this.repository});

  final SupplierMobileRepository repository;

  @override
  Widget build(BuildContext context) {
    final actions = [
      _ActionTile(
        icon: Icons.message_outlined,
        label: 'الرسائل',
        onTap: () => AppNavigation.push(
          context,
          (_) => SupplierMessagesListScreen(repository: repository),
        ),
      ),
      _ActionTile(
        icon: Icons.notifications_none_rounded,
        label: 'الإشعارات',
        onTap: () => AppNavigation.push(
          context,
          (_) => SupplierNotificationsListScreen(repository: repository),
        ),
      ),
      _ActionTile(
        icon: Icons.campaign_outlined,
        label: 'الإعلانات',
        onTap: () => AppNavigation.push(
          context,
          (_) => SupplierAnnouncementsListScreen(repository: repository),
        ),
      ),
      _ActionTile(
        icon: Icons.receipt_long_outlined,
        label: 'الفواتير',
        onTap: () => AppNavigation.push(
          context,
          (_) => SupplierMessagesListScreen(repository: repository),
        ),
      ),
    ];

    return GridView.builder(
      physics: const NeverScrollableScrollPhysics(),
      shrinkWrap: true,
      itemCount: actions.length,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        crossAxisSpacing: 12,
        mainAxisSpacing: 12,
        childAspectRatio: 1.25,
      ),
      itemBuilder: (_, index) => actions[index],
    );
  }
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: UiPalette.surfaceCard,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: UiPalette.primary, size: 26),
              const SizedBox(height: 10),
              Text(
                label,
                style: const TextStyle(
                  color: UiPalette.textMain,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
