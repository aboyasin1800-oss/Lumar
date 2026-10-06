import 'package:flutter/material.dart';

class CustomerProfileScreen extends StatelessWidget {
  const CustomerProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final profile = const _CustomerProfile(
      fullName: 'عبد الرحمن محمد',
      customerCode: 'CUST-2048',
      status: 'نشط',
      lastLogin: 'آخر تسجيل دخول: اليوم, 09:45',
      mobile: '+966 50 123 4567',
      email: 'abdulrahman.mohammed@example.com',
      city: 'الرياض',
      address: 'شارع الملك فهد، حي النخيل، الرياض 12273',
      loyaltyTier: 'عضو فضي',
      points: '2,480 نقطة',
      preferredLanguage: 'العربية',
      joinedAt: 'انضم في أكتوبر 2024',
      preferences: [
        'تجربة تسليم سريعة',
        'تأكيد الطلب عبر الرسائل',
        'مراجعة الطلب قبل الشحن',
      ],
      supportOptions: [
        'الدعم المتاح 24/7',
        'متوسط الرد خلال 10 دقائق',
        'المساعدة عبر الهاتف والواتساب',
      ],
    );

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _ProfileHeader(profile: profile),
        const SizedBox(height: 16),
        _InfoSection(
          title: 'البيانات الشخصية',
          icon: Icons.person_outline,
          items: [
            _InfoRow(label: 'اسم العميل', value: profile.fullName),
            _InfoRow(label: 'رمز العميل', value: profile.customerCode),
            _InfoRow(label: 'حالة الحساب', value: profile.status),
            _InfoRow(label: 'تاريخ الانضمام', value: profile.joinedAt),
          ],
        ),
        const SizedBox(height: 16),
        _InfoSection(
          title: 'معلومات التواصل',
          icon: Icons.contact_phone_outlined,
          items: [
            _InfoRow(label: 'الجوال', value: profile.mobile),
            _InfoRow(label: 'البريد الإلكتروني', value: profile.email),
            _InfoRow(label: 'المدينة', value: profile.city),
            _InfoRow(label: 'العنوان', value: profile.address),
          ],
        ),
        const SizedBox(height: 16),
        _ActionSummaryCard(
          title: 'ملف الحساب',
          points: profile.points,
          level: profile.loyaltyTier,
          language: profile.preferredLanguage,
        ),
        const SizedBox(height: 16),
        _PreferenceSection(
          title: 'التفضيلات',
          items: profile.preferences,
        ),
        const SizedBox(height: 16),
        _PreferenceSection(
          title: 'الدعم',
          items: profile.supportOptions,
          accentColor: Colors.green,
        ),
      ],
    );
  }
}

class _CustomerProfile {
  const _CustomerProfile({
    required this.fullName,
    required this.customerCode,
    required this.status,
    required this.lastLogin,
    required this.mobile,
    required this.email,
    required this.city,
    required this.address,
    required this.loyaltyTier,
    required this.points,
    required this.preferredLanguage,
    required this.joinedAt,
    required this.preferences,
    required this.supportOptions,
  });

  final String fullName;
  final String customerCode;
  final String status;
  final String lastLogin;
  final String mobile;
  final String email;
  final String city;
  final String address;
  final String loyaltyTier;
  final String points;
  final String preferredLanguage;
  final String joinedAt;
  final List<String> preferences;
  final List<String> supportOptions;
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({required this.profile});

  final _CustomerProfile profile;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Card(
      color: colorScheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            CircleAvatar(
              radius: 32,
              backgroundColor:
                  colorScheme.onPrimaryContainer.withValues(alpha: 0.08),
              child: Icon(
                Icons.person,
                size: 30,
                color: colorScheme.onPrimaryContainer,
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    profile.fullName,
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.bold,
                        ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'رمز العميل: ${profile.customerCode}',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 8),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: Colors.green.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Text(
                      profile.status,
                      style: const TextStyle(
                        color: Colors.green,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Text(
                    profile.lastLogin,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: colorScheme.onPrimaryContainer
                              .withValues(alpha: 0.8),
                        ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _InfoSection extends StatelessWidget {
  const _InfoSection({
    required this.title,
    required this.icon,
    required this.items,
  });

  final String title;
  final IconData icon;
  final List<_InfoRow> items;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            ...items.map((item) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(
                        width: 110,
                        child: Text(
                          item.label,
                          style: TextStyle(
                            color: colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          item.value,
                          textAlign: TextAlign.start,
                        ),
                      ),
                    ],
                  ),
                )),
          ],
        ),
      ),
    );
  }
}

class _InfoRow {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;
}

class _ActionSummaryCard extends StatelessWidget {
  const _ActionSummaryCard({
    required this.title,
    required this.points,
    required this.level,
    required this.language,
  });

  final String title;
  final String points;
  final String level;
  final String language;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: _StatMiniCard(
                    label: 'المستوى',
                    value: level,
                    icon: Icons.stars_rounded,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _StatMiniCard(
                    label: 'النقاط',
                    value: points,
                    icon: Icons.wallet_giftcard_rounded,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            _StatMiniCard(
              label: 'لغة العرض',
              value: language,
              icon: Icons.language_rounded,
              fullWidth: true,
            ),
          ],
        ),
      ),
    );
  }
}

class _StatMiniCard extends StatelessWidget {
  const _StatMiniCard({
    required this.label,
    required this.value,
    required this.icon,
    this.fullWidth = false,
  });

  final String label;
  final String value;
  final IconData icon;
  final bool fullWidth;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: fullWidth ? double.infinity : null,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Icon(icon, color: colorScheme.primary),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 12,
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  value,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PreferenceSection extends StatelessWidget {
  const _PreferenceSection({
    required this.title,
    required this.items,
    this.accentColor = Colors.blue,
  });

  final String title;
  final List<String> items;
  final Color accentColor;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 12),
            ...items.map((item) => Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(Icons.check_circle_rounded,
                          color: accentColor, size: 20),
                      const SizedBox(width: 10),
                      Expanded(child: Text(item)),
                    ],
                  ),
                )),
          ],
        ),
      ),
    );
  }
}
