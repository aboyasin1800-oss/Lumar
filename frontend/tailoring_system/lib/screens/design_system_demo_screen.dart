import 'package:flutter/material.dart';

import '../core/theme/app_breakpoints.dart';
import '../core/theme/app_dimensions.dart';
import '../core/theme/app_icons.dart';
import '../core/theme/app_spacing.dart';
import '../core/theme/app_surface.dart';
import '../core/theme/app_theme.dart';
import '../core/theme/app_typography.dart';
import '../core/ui_palette.dart';

class DesignSystemDemoScreen extends StatelessWidget {
  const DesignSystemDemoScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final isDesktop =
        AppBreakpoints.isDesktop(MediaQuery.sizeOf(context).width);
    final contentWidth =
        isDesktop ? 1200.0 : MediaQuery.sizeOf(context).width - 24;

    return Directionality(
      textDirection: TextDirection.rtl,
      child: Theme(
        data: AppTheme.dark(),
        child: Scaffold(
          backgroundColor: UiPalette.screenBackground,
          body: SafeArea(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Center(
                child: SizedBox(
                  width: contentWidth,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      AppSurface(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: [
                                AppIcons.svg('design_demo_icon',
                                    size: 32, color: UiPalette.primaryBlue),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Text(
                                    'واجهة اختبار نظام التصميم',
                                    style: AppTypography.display
                                        .copyWith(color: UiPalette.textMain),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 12),
                            Text(
                              'مستوى التصميم المركزي مع Material 3 والهوية الحالية لواجهة LUMAR ERP.',
                              style: AppTypography.body
                                  .copyWith(color: UiPalette.textSoft),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: AppSpacing.lg),
                      Wrap(
                        spacing: AppSpacing.md,
                        runSpacing: AppSpacing.md,
                        children: [
                          _InfoTile(label: 'Light Theme', value: 'مفعّل'),
                          _InfoTile(label: 'Dark Theme', value: 'مفعّل'),
                          _InfoTile(label: 'RTL', value: 'مفعّل'),
                          _InfoTile(label: 'SVG', value: 'مفعّل'),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xl),
                      AppSurface(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('خريطة نظام الإحالات والنقاط', style: AppTypography.headline),
                            const SizedBox(height: AppSpacing.md),
                            Text(
                              'العرض التوضيحي التالي يوضح العلاقة التشغيلية بين الشجرة الإحالية ومعدل توزيع المكافآت والنقاط دون أي تعديل على المنطق الحالي.',
                              style: AppTypography.body.copyWith(color: UiPalette.textSoft),
                            ),
                            const SizedBox(height: AppSpacing.lg),
                            _ReferralTreeMap(),
                            const SizedBox(height: AppSpacing.lg),
                            Text('توزيع المكافآت', style: AppTypography.title),
                            const SizedBox(height: AppSpacing.sm),
                            Wrap(
                              spacing: AppSpacing.sm,
                              runSpacing: AppSpacing.sm,
                              children: const [
                                _RewardChip(label: 'المستوى 1', value: '50%', color: UiPalette.primaryBlue),
                                _RewardChip(label: 'المستوى 2', value: '25%', color: UiPalette.purpleAccent),
                                _RewardChip(label: 'المستوى 3', value: '12.5%', color: UiPalette.softBlue),
                                _RewardChip(label: 'المستوى 4', value: '6.25%', color: UiPalette.primaryDark),
                              ],
                            ),
                            const SizedBox(height: AppSpacing.md),
                            Wrap(
                              spacing: AppSpacing.sm,
                              runSpacing: AppSpacing.sm,
                              children: const [
                                _RewardChip(label: 'نقاط المشتري', value: '+100', color: UiPalette.primaryBlue),
                                _RewardChip(label: 'مكافأة المحيل', value: '+50', color: UiPalette.primaryDark),
                                _RewardChip(label: 'عكس', value: '-25', color: UiPalette.softBlue),
                                _RewardChip(label: 'استبدال', value: '-200', color: UiPalette.purpleAccent),
                              ],
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: AppSpacing.xl),
                      Text('أنواع النصوص', style: AppTypography.headline),
                      const SizedBox(height: AppSpacing.md),
                      Text('Display', style: AppTypography.display),
                      const SizedBox(height: AppSpacing.sm),
                      Text('Headline', style: AppTypography.headline),
                      const SizedBox(height: AppSpacing.sm),
                      Text('Title', style: AppTypography.title),
                      const SizedBox(height: AppSpacing.sm),
                      Text('Body', style: AppTypography.body),
                      const SizedBox(height: AppSpacing.sm),
                      Text('Label', style: AppTypography.label),
                      const SizedBox(height: AppSpacing.sm),
                      Text('Caption', style: AppTypography.caption),
                      const SizedBox(height: AppSpacing.sm),
                      Text('123456.78', style: AppTypography.numeric),
                      const SizedBox(height: AppSpacing.xl),
                      Text('المسافات المعرفة', style: AppTypography.headline),
                      const SizedBox(height: AppSpacing.md),
                      Wrap(
                        spacing: AppSpacing.xs,
                        runSpacing: AppSpacing.xs,
                        children: [
                          _SpacingChip('xxs', AppSpacing.xxs),
                          _SpacingChip('xs', AppSpacing.xs),
                          _SpacingChip('sm', AppSpacing.sm),
                          _SpacingChip('md', AppSpacing.md),
                          _SpacingChip('lg', AppSpacing.lg),
                          _SpacingChip('xl', AppSpacing.xl),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xl),
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: AppSurface(
                              padding: const EdgeInsets.all(20),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text('نموذج', style: AppTypography.title),
                                  const SizedBox(height: AppSpacing.md),
                                  TextField(
                                    decoration: InputDecoration(
                                      labelText: 'اسم المستخدم',
                                      hintText: 'أدخل الاسم',
                                    ),
                                  ),
                                  const SizedBox(height: AppSpacing.md),
                                  TextField(
                                    decoration: InputDecoration(
                                      labelText: 'البريد الإلكتروني',
                                      hintText: 'example@domain.com',
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                          const SizedBox(width: AppSpacing.md),
                          Expanded(
                            child: AppSurface(
                              padding: const EdgeInsets.all(20),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text('الأزرار', style: AppTypography.title),
                                  const SizedBox(height: AppSpacing.md),
                                  FilledButton(
                                    onPressed: () {},
                                    child: const Text('زر تعبئة'),
                                  ),
                                  const SizedBox(height: AppSpacing.sm),
                                  ElevatedButton(
                                    onPressed: () {},
                                    child: const Text('زر مرتفع'),
                                  ),
                                  const SizedBox(height: AppSpacing.sm),
                                  OutlinedButton(
                                    onPressed: () {},
                                    child: const Text('زر حدودي'),
                                  ),
                                  const SizedBox(height: AppSpacing.sm),
                                  TextButton(
                                    onPressed: () {},
                                    child: const Text('رابط نصي'),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.xl),
                      Text('مثال SVG', style: AppTypography.headline),
                      const SizedBox(height: AppSpacing.md),
                      AppSurface(
                        padding: const EdgeInsets.all(20),
                        child: Row(
                          children: [
                            AppIcons.svg('design_demo_icon',
                                size: 64, color: UiPalette.primaryBlue),
                            const SizedBox(width: AppSpacing.md),
                            Expanded(
                              child: Text(
                                'تم تحميل SVG بنجاح من الأصول داخل الطبقة المركزية. هذا مثال يثبت أن الخطوط والأيقونات واللون تعمل دون كسر في التطبيق.',
                                style: AppTypography.body,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: AppSpacing.xl),
                      Text('مثال Responsive', style: AppTypography.headline),
                      const SizedBox(height: AppSpacing.md),
                      AppSurface(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                                'العرض الحالي: ${MediaQuery.sizeOf(context).width.round()}px',
                                style: AppTypography.numeric),
                            const SizedBox(height: AppSpacing.sm),
                            Text(
                                'نقطة التوقف: ${_breakpointLabel(MediaQuery.sizeOf(context).width)}',
                                style: AppTypography.body),
                            const SizedBox(height: AppSpacing.sm),
                            if (isDesktop)
                              Row(
                                children: [
                                  _ResponsiveStatCard(
                                      label: 'Desktop', value: 'متاح'),
                                  const SizedBox(width: AppSpacing.sm),
                                  _ResponsiveStatCard(
                                      label: 'Tablet', value: 'مؤقت'),
                                ],
                              )
                            else
                              Column(
                                children: [
                                  _ResponsiveStatCard(
                                      label: 'Mobile', value: 'مفعّل'),
                                  const SizedBox(height: AppSpacing.sm),
                                  _ResponsiveStatCard(
                                      label: 'Tablet', value: 'جاري التكيف'),
                                ],
                              ),
                          ],
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

  static String _breakpointLabel(double width) {
    if (width >= AppBreakpoints.largeDesktop) return 'largeDesktop';
    if (width >= AppBreakpoints.desktop) return 'desktop';
    if (width >= AppBreakpoints.tablet) return 'tablet';
    return 'mobile';
  }
}

class _ReferralTreeMap extends StatelessWidget {
  const _ReferralTreeMap();

  @override
  Widget build(BuildContext context) {
    final nodes = [
      _TreeNodeData('50', 'الجذر', '12.5', true),
      _TreeNodeData('51', 'مستوى 1', '25', false),
      _TreeNodeData('52', 'مستوى 2', '50', false),
      _TreeNodeData('53', 'مستوى 3', '100', false),
      _TreeNodeData('54', 'مستوى 4', '200', false),
    ];

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: UiPalette.softBlue.withOpacity(0.25),
        borderRadius: BorderRadius.circular(AppDimensions.cardRadius),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _ReferralTreeNode(node: nodes[0]),
            ],
          ),
          const SizedBox(height: 16),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: nodes.sublist(1).map((node) => _ReferralTreeNode(node: node)).toList(),
          ),
          const SizedBox(height: 16),
          Text(
            'السلسلة: 50 → 51 → 52 → 53 → 54',
            style: AppTypography.label.copyWith(color: UiPalette.textSoft),
          ),
        ],
      ),
    );
  }
}

class _ReferralTreeNode extends StatelessWidget {
  const _ReferralTreeNode({required this.node});

  final _TreeNodeData node;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          width: 92,
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
          decoration: BoxDecoration(
            color: node.isRoot ? UiPalette.primaryBlue : UiPalette.surfaceCard,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: UiPalette.borderSoft),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.12),
                blurRadius: 8,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Column(
            children: [
              Text(
                node.id,
                style: AppTypography.title.copyWith(
                  color: node.isRoot ? UiPalette.textMain : UiPalette.primaryBlue,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                node.label,
                style: AppTypography.label.copyWith(
                  color: node.isRoot ? UiPalette.textMain : UiPalette.textSoft,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                '${node.points} نقطة',
                style: AppTypography.caption.copyWith(
                  color: UiPalette.textSoft,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _TreeNodeData {
  const _TreeNodeData(this.id, this.label, this.points, this.isRoot);

  final String id;
  final String label;
  final String points;
  final bool isRoot;
}

class _RewardChip extends StatelessWidget {
  const _RewardChip({required this.label, required this.value, required this.color});

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Text(
        '$label: $value',
        style: AppTypography.label.copyWith(color: UiPalette.textMain),
      ),
    );
  }
}

class _InfoTile extends StatelessWidget {
  const _InfoTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final color = UiPalette.surfaceCard;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(AppDimensions.cardRadius),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: AppTypography.label),
          const SizedBox(height: 4),
          Text(value,
              style:
                  AppTypography.title.copyWith(color: UiPalette.primaryBlue)),
        ],
      ),
    );
  }
}

class _SpacingChip extends StatelessWidget {
  const _SpacingChip(this.label, this.value);

  final String label;
  final double value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: UiPalette.softBlue,
        borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Text('$label: $value',
          style: AppTypography.label.copyWith(color: UiPalette.textMain)),
    );
  }
}

class _ResponsiveStatCard extends StatelessWidget {
  const _ResponsiveStatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: UiPalette.softBlue,
          borderRadius: BorderRadius.circular(AppDimensions.smallRadius),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: AppTypography.label),
            const SizedBox(height: 8),
            Text(value,
                style:
                    AppTypography.title.copyWith(color: UiPalette.primaryBlue)),
          ],
        ),
      ),
    );
  }
}
