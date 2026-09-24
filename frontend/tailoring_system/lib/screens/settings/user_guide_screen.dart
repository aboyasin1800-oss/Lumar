import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_markdown_plus/flutter_markdown_plus.dart';
import 'package:flutter/services.dart';

import '../../core/ui_palette.dart';

class UserGuideScreen extends StatefulWidget {
  const UserGuideScreen({super.key});

  @override
  State<UserGuideScreen> createState() => _UserGuideScreenState();
}

class _UserGuideScreenState extends State<UserGuideScreen> {
  static const _categories = <_GuideCategory>[
    _GuideCategory('قواعد استهلاك القماش', Icons.texture_outlined, true,
        'CONSUMPTION_RULE_DOCUMENTATION_AR.md'),
    _GuideCategory('إعدادات العملة', Icons.payments_outlined, true,
        'CURRENCY_SETTINGS_DOCUMENTATION_AR.md'),
    _GuideCategory('التسعير', Icons.calculate_outlined, true,
        'PRICING_ENGINE_DOCUMENTATION_AR.md'),
    _GuideCategory('إعدادات الطباعة', Icons.print_outlined, true,
        'PRINT_SETTINGS_DOCUMENTATION_AR.md'),
    _GuideCategory('قوالب الطباعة', Icons.design_services_outlined, true,
        'PRINT_TEMPLATE_CATALOG_DOCUMENTATION_AR.md'),
    _GuideCategory('إدارة أجهزة المسح', Icons.qr_code_scanner_outlined, true,
        'SCANNER_MANAGEMENT_GUIDE_AR.md'),
    _GuideCategory('القياسات', Icons.straighten_rounded, false, ''),
    _GuideCategory('القطع والأنواع', Icons.content_cut_outlined, false, ''),
    _GuideCategory('المخزون', Icons.inventory_2_outlined, false, ''),
    _GuideCategory('إنشاء العملاء والإحالات', Icons.person_add_alt_1_outlined,
        true, 'CUSTOMER_CREATION_USER_GUIDE_AR.md'),
    _GuideCategory('المبيعات', Icons.shopping_cart_outlined, true,
        'SALES_PRICING_INTEGRATION_DOCUMENTATION_AR.md'),
    _GuideCategory('مبيعات المنتجات الجاهزة', Icons.point_of_sale_outlined, true,
      'READY_MADE_SALES_SCREEN_GUIDE_AR.md'),
    _GuideCategory('إنشاء أوامر الإنتاج الجاهز', Icons.factory_outlined, true,
      'READY_MADE_PRODUCTION_ORDER_CREATE_GUIDE_AR.md'),
    _GuideCategory('الطلبات', Icons.receipt_long_outlined, true,
        'ORDERS_SCREEN_USER_GUIDE_AR.md'),
    _GuideCategory('الإنتاج', Icons.factory_outlined, true,
        'PRODUCTION_SCANNING_GUIDE_AR.md'),
    _GuideCategory('طلبات الإنتاج', Icons.assignment_outlined, true,
        'PRODUCTION_SCREEN_USER_GUIDE_AR.md'),
    _GuideCategory('إعدادات مسارات الإنتاج', Icons.route_outlined, true,
        'PRODUCTION_ROUTES_SETTINGS_GUIDE_AR.md'),
    _GuideCategory('مراقبة المصنع', Icons.factory_outlined, true,
        'FACTORY_MONITORING_GUIDE_AR.md'),
    _GuideCategory('شاشة التسليم', Icons.local_shipping_outlined, true,
        'DELIVERY_SCREEN_GUIDE_AR.md'),
    _GuideCategory('الرواتب والموارد البشرية', Icons.payments_outlined, true,
        'HR_PAYROLL_GUIDE_AR.md'),
    _GuideCategory('المشتريات', Icons.shopping_cart_outlined, true,
        'SUPPLIER_PURCHASING_SCREEN_GUIDE_AR.md'),
    _GuideCategory('شجرة الإحالة', Icons.account_tree_outlined, true,
        'REFERRAL_TREE_USER_GUIDE_AR.md'),
    _GuideCategory('الإحالات', Icons.account_tree_outlined, true,
        'REFERRAL_EVENTS_USER_GUIDE_AR.md'),
    _GuideCategory('لوحة الإحالات', Icons.dashboard_customize_outlined, true,
        'REFERRAL_DASHBOARD_USER_GUIDE_AR.md'),
    _GuideCategory('لوحة الولاء', Icons.loyalty_outlined, true,
        'LOYALTY_DASHBOARD_USER_GUIDE_AR.md'),
    _GuideCategory('حركات الولاء', Icons.receipt_long_outlined, true,
        'LOYALTY_TRANSACTIONS_USER_GUIDE_AR.md'),
    _GuideCategory('مكافآت الولاء', Icons.card_giftcard_outlined, true,
        'LOYALTY_REWARDS_USER_GUIDE_AR.md'),
    _GuideCategory('استبدال نقاط الولاء', Icons.redeem_outlined, true,
        'LOYALTY_REDEMPTION_USER_GUIDE_AR.md'),
    _GuideCategory('إعدادات النقاط', Icons.settings_outlined, true,
        'LOYALTY_POINTS_SETTINGS_USER_GUIDE_AR.md'),
    _GuideCategory('مركز الإعدادات', Icons.manage_search_outlined, true,
        'SETTINGS_CENTER_USER_GUIDE_AR.md'),
    _GuideCategory('قواعد الولاء', Icons.rule_outlined, true,
        'LOYALTY_RULES_USER_GUIDE_AR.md'),
    _GuideCategory('تقييم مستويات كبار العملاء', Icons.stars_outlined, true,
        'VIP_LEVELS_USER_GUIDE_AR.md'),
    _GuideCategory('سجل استبدالات النقاط', Icons.history_outlined, true,
        'LOYALTY_REDEMPTION_HISTORY_USER_GUIDE_AR.md'),
    _GuideCategory('تحليلات الإحالات', Icons.analytics_outlined, true,
        'REFERRAL_ANALYTICS_USER_GUIDE_AR.md'),
    _GuideCategory('مكافآت الإحالات', Icons.card_giftcard_outlined, true,
        'REFERRAL_REWARDS_USER_GUIDE_AR.md'),
    _GuideCategory('أكواد الإحالة', Icons.confirmation_number_outlined, true,
        'REFERRAL_CODES_USER_GUIDE_AR.md'),
    _GuideCategory('المالية', Icons.account_balance_outlined, true,
      'FINANCIAL_MANAGEMENT_USER_GUIDE_AR.md'),
    _GuideCategory('التقارير', Icons.bar_chart_outlined, false, ''),
  ];

  final _searchController = TextEditingController();
  final _contentScrollController = ScrollController();
  final _sectionKeys = <String, GlobalKey>{};
  String _searchText = '';
  String? _selectedCategory;
  late Future<String> _guideFuture;

  @override
  void initState() {
    super.initState();
    _selectedCategory = _categories.first.title;
    _guideFuture = _readGuide();
    _searchController.addListener(_onSearchChanged);
  }

  @override
  void dispose() {
    _searchController
      ..removeListener(_onSearchChanged)
      ..dispose();
    _contentScrollController.dispose();
    super.dispose();
  }

  void _onSearchChanged() {
    setState(() {
      _searchText = _searchController.text.trim();
    });
  }

  Future<String> _readGuide() async {
    final category =
        _categories.firstWhere((item) => item.title == _selectedCategory);
    final file = _findGuideFile(category.fileName);
    if (file == null) {
      throw FileSystemException(
        'لم يتم العثور على ملف الدليل المحدد.',
        category.fileName,
      );
    }
    return file.readAsString();
  }

  File? _findGuideFile(String guideFileName) {
    final candidates = <String>[];
    final configuredRoot = Platform.environment['LUMAR_WORKSPACE_ROOT'];
    if (configuredRoot != null && configuredRoot.trim().isNotEmpty) {
      candidates.add(_join(configuredRoot, 'Docs', guideFileName));
    }

    for (final start in [
      Directory.current.path,
      File(Platform.resolvedExecutable).parent.path
    ]) {
      var directory = Directory(start);
      for (var level = 0; level < 8; level++) {
        candidates.add(_join(directory.path, 'Docs', guideFileName));
        final parent = directory.parent;
        if (parent.path == directory.path) break;
        directory = parent;
      }
    }

    for (final path in candidates.toSet()) {
      final file = File(path);
      if (file.existsSync()) return file;
    }
    return null;
  }

  String _join(String first, String second, String third) =>
      '$first${Platform.pathSeparator}$second${Platform.pathSeparator}$third';

  Future<void> _copyGuide(String content) async {
    await Clipboard.setData(ClipboardData(text: content));
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('تم نسخ نص الدليل.')),
    );
  }

  void _selectCategory(String category) {
    setState(() {
      _selectedCategory = category;
      _searchController.clear();
      _guideFuture = _readGuide();
    });
  }

  void _scrollToSection(String title) {
    final key = _sectionKeys[title];
    final context = key?.currentContext;
    if (context == null) return;
    Scrollable.ensureVisible(
      context,
      duration: const Duration(milliseconds: 350),
      curve: Curves.easeOutCubic,
      alignment: 0.04,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        title: const Text('دليل المستخدم'),
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.textMain,
        actions: [
          IconButton(
            tooltip: 'تحديث الدليل',
            onPressed: () => setState(() {
              _guideFuture = _readGuide();
            }),
            icon: const Icon(Icons.refresh_rounded),
          ),
          IconButton(
            tooltip: 'الطباعة لاحقاً',
            onPressed: null,
            icon: const Icon(Icons.print_outlined),
          ),
        ],
      ),
      body: FutureBuilder<String>(
        future: _guideFuture,
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return _GuideError(
              message: snapshot.error.toString(),
              onRetry: () => setState(() {
                _guideFuture = _readGuide();
              }),
            );
          }
          return _GuideWorkspace(
            categories: _categories,
            selectedCategory: _selectedCategory!,
            searchController: _searchController,
            searchText: _searchText,
            content: snapshot.data ?? '',
            sectionKeys: _sectionKeys,
            contentScrollController: _contentScrollController,
            onCategorySelected: _selectCategory,
            onSectionSelected: _scrollToSection,
            onCopy: _copyGuide,
          );
        },
      ),
    );
  }
}

class _GuideWorkspace extends StatelessWidget {
  const _GuideWorkspace({
    required this.categories,
    required this.selectedCategory,
    required this.searchController,
    required this.searchText,
    required this.content,
    required this.sectionKeys,
    required this.contentScrollController,
    required this.onCategorySelected,
    required this.onSectionSelected,
    required this.onCopy,
  });

  final List<_GuideCategory> categories;
  final String selectedCategory;
  final TextEditingController searchController;
  final String searchText;
  final String content;
  final Map<String, GlobalKey> sectionKeys;
  final ScrollController contentScrollController;
  final ValueChanged<String> onCategorySelected;
  final ValueChanged<String> onSectionSelected;
  final Future<void> Function(String content) onCopy;

  @override
  Widget build(BuildContext context) {
    final sections = _parseSections(content);
    final visibleSections = searchText.isEmpty
        ? sections
        : sections
            .where((section) =>
                section.text.toLowerCase().contains(searchText.toLowerCase()))
            .toList();
    final matches = searchText.isEmpty
        ? 0
        : sections
            .where((section) =>
                section.text.toLowerCase().contains(searchText.toLowerCase()))
            .length;

    return Padding(
      padding: const EdgeInsets.all(16),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final isWide = constraints.maxWidth >= 1050;
          final contentPanel = _GuideContent(
            content: content,
            sections: visibleSections,
            allSections: sections,
            selectedCategory: selectedCategory,
            searchController: searchController,
            searchText: searchText,
            matches: matches,
            sectionKeys: sectionKeys,
            contentScrollController: contentScrollController,
            onSectionSelected: onSectionSelected,
            onCopy: onCopy,
          );
          final sidebar = _GuideSidebar(
            categories: categories,
            selectedCategory: selectedCategory,
            onCategorySelected: onCategorySelected,
          );

          if (isWide) {
            return Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                SizedBox(width: 240, child: sidebar),
                const SizedBox(width: 16),
                Expanded(child: contentPanel),
              ],
            );
          }
          return Column(
            children: [
              SizedBox(height: 150, child: sidebar),
              const SizedBox(height: 12),
              Expanded(child: contentPanel),
            ],
          );
        },
      ),
    );
  }
}

class _GuideSidebar extends StatelessWidget {
  const _GuideSidebar({
    required this.categories,
    required this.selectedCategory,
    required this.onCategorySelected,
  });

  final List<_GuideCategory> categories;
  final String selectedCategory;
  final ValueChanged<String> onCategorySelected;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: ListView(
        children: [
          const Text(
            'الأدلة',
            style: TextStyle(
              color: UiPalette.textMain,
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 8),
          ...categories.map(
            (category) => ListTile(
              dense: true,
              selected: category.title == selectedCategory,
              selectedTileColor: UiPalette.softBlue,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
              leading: Icon(category.icon, color: UiPalette.primaryBlue),
              title: Text(category.title),
              subtitle: category.available ? null : const Text('قريباً'),
              onTap: category.available
                  ? () => onCategorySelected(category.title)
                  : null,
            ),
          ),
        ],
      ),
    );
  }
}

class _GuideContent extends StatelessWidget {
  const _GuideContent({
    required this.content,
    required this.sections,
    required this.allSections,
    required this.selectedCategory,
    required this.searchController,
    required this.searchText,
    required this.matches,
    required this.sectionKeys,
    required this.contentScrollController,
    required this.onSectionSelected,
    required this.onCopy,
  });

  final String content;
  final List<_GuideSection> sections;
  final List<_GuideSection> allSections;
  final String selectedCategory;
  final TextEditingController searchController;
  final String searchText;
  final int matches;
  final Map<String, GlobalKey> sectionKeys;
  final ScrollController contentScrollController;
  final ValueChanged<String> onSectionSelected;
  final Future<void> Function(String content) onCopy;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        selectedCategory,
                        style: const TextStyle(
                          color: UiPalette.textMain,
                          fontSize: 22,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    IconButton(
                      tooltip: 'نسخ الدليل كاملاً',
                      onPressed: () => onCopy(content),
                      icon: const Icon(Icons.copy_all_outlined),
                    ),
                  ],
                ),
                Text(
                  'المحتوى الرسمي المعتمد لدليل المستخدم',
                  style: TextStyle(color: UiPalette.textSoft),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: searchController,
                  textDirection: TextDirection.rtl,
                  decoration: InputDecoration(
                    labelText: 'ابحث داخل الدليل',
                    prefixIcon: const Icon(Icons.search_rounded),
                    suffixIcon: searchText.isEmpty
                        ? null
                        : IconButton(
                            tooltip: 'مسح البحث',
                            onPressed: searchController.clear,
                            icon: const Icon(Icons.clear_rounded),
                          ),
                    border: const OutlineInputBorder(),
                  ),
                ),
                if (searchText.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      matches == 0
                          ? 'لا توجد أقسام مطابقة للبحث.'
                          : 'عدد المواضع المطابقة: $matches',
                      style: const TextStyle(color: UiPalette.textSoft),
                    ),
                  ),
              ],
            ),
          ),
          const Divider(height: 1, color: UiPalette.borderSoft),
          Expanded(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                SizedBox(
                  width: 220,
                  child: _GuideIndex(
                    sections: allSections,
                    visibleSections: sections,
                    sectionKeys: sectionKeys,
                    onSectionSelected: onSectionSelected,
                  ),
                ),
                const VerticalDivider(width: 1, color: UiPalette.borderSoft),
                Expanded(
                  child: ListView(
                    controller: contentScrollController,
                    padding: const EdgeInsets.fromLTRB(24, 18, 24, 40),
                    children: [
                      if (sections.isEmpty)
                        const Padding(
                          padding: EdgeInsets.all(24),
                          child: Text(
                            'لا يوجد محتوى مطابق للبحث.',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: UiPalette.textSoft),
                          ),
                        ),
                      ...sections.map(
                        (section) => Container(
                          key: sectionKeys.putIfAbsent(
                            section.title,
                            GlobalKey.new,
                          ),
                          margin: const EdgeInsets.only(bottom: 18),
                          child: Directionality(
                            textDirection: TextDirection.rtl,
                            child: MarkdownBody(
                              data: section.markdown,
                              selectable: true,
                              styleSheet: _markdownStyle(context),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  MarkdownStyleSheet _markdownStyle(BuildContext context) {
    return MarkdownStyleSheet.fromTheme(Theme.of(context)).copyWith(
      p: const TextStyle(
        color: UiPalette.textMain,
        fontSize: 15,
        height: 1.7,
      ),
      h1: const TextStyle(
        color: UiPalette.primaryBlue,
        fontSize: 26,
        fontWeight: FontWeight.bold,
        height: 1.4,
      ),
      h2: const TextStyle(
        color: UiPalette.primaryBlue,
        fontSize: 21,
        fontWeight: FontWeight.bold,
        height: 1.5,
      ),
      h3: const TextStyle(
        color: UiPalette.textMain,
        fontSize: 18,
        fontWeight: FontWeight.bold,
        height: 1.5,
      ),
      listBullet: const TextStyle(color: UiPalette.primaryBlue, fontSize: 16),
      tableHead: const TextStyle(
        color: UiPalette.textMain,
        fontWeight: FontWeight.bold,
      ),
      tableBody: const TextStyle(color: UiPalette.textSoft, height: 1.5),
      blockquote: const TextStyle(color: UiPalette.textSoft, height: 1.6),
      code: const TextStyle(color: UiPalette.primaryBlue),
      horizontalRuleDecoration: const BoxDecoration(
        border: Border(top: BorderSide(color: UiPalette.borderSoft)),
      ),
    );
  }
}

class _GuideIndex extends StatelessWidget {
  const _GuideIndex({
    required this.sections,
    required this.visibleSections,
    required this.sectionKeys,
    required this.onSectionSelected,
  });

  final List<_GuideSection> sections;
  final List<_GuideSection> visibleSections;
  final Map<String, GlobalKey> sectionKeys;
  final ValueChanged<String> onSectionSelected;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: UiPalette.softBlue.withValues(alpha: 0.35),
      padding: const EdgeInsets.all(10),
      child: ListView(
        children: [
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 8, vertical: 6),
            child: Text(
              'فهرس الدليل',
              style: TextStyle(
                color: UiPalette.textMain,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
          ...sections.map(
            (section) => ListTile(
              dense: true,
              enabled: visibleSections.contains(section),
              title: Text(
                section.title,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              onTap: () => onSectionSelected(section.title),
            ),
          ),
        ],
      ),
    );
  }
}

class _GuideError extends StatelessWidget {
  const _GuideError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        constraints: const BoxConstraints(maxWidth: 560),
        margin: const EdgeInsets.all(24),
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          color: UiPalette.surfaceCard,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: UiPalette.borderSoft),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.menu_book_outlined,
                size: 48, color: UiPalette.primaryBlue),
            const SizedBox(height: 12),
            const Text(
              'تعذر تحميل دليل المستخدم',
              style: TextStyle(
                color: UiPalette.textMain,
                fontSize: 18,
                fontWeight: FontWeight.bold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              message.replaceFirst('FileSystemException: ', ''),
              textAlign: TextAlign.center,
              style: const TextStyle(color: UiPalette.textSoft),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh_rounded),
              label: const Text('إعادة المحاولة'),
            ),
          ],
        ),
      ),
    );
  }
}

class _GuideCategory {
  const _GuideCategory(this.title, this.icon, this.available, this.fileName);

  final String title;
  final IconData icon;
  final bool available;
  final String fileName;
}

class _GuideSection {
  const _GuideSection({required this.title, required this.markdown});

  final String title;
  final String markdown;

  String get text => markdown;
}

List<_GuideSection> _parseSections(String markdown) {
  final lines = markdown.replaceAll('\r\n', '\n').split('\n');
  final sections = <_GuideSection>[];
  var currentTitle = 'مقدمة';
  var currentLines = <String>[];

  void save() {
    final body = currentLines.join('\n').trim();
    if (body.isEmpty && sections.isNotEmpty) return;
    sections.add(_GuideSection(
      title: currentTitle,
      markdown: body.isEmpty ? currentTitle : '# $currentTitle\n\n$body',
    ));
  }

  for (final line in lines) {
    final heading = RegExp(r'^#{1,3}\s+(.+?)\s*$').firstMatch(line);
    if (heading != null) {
      if (currentLines.isNotEmpty || sections.isNotEmpty) save();
      currentTitle = heading.group(1)!.trim();
      currentLines = [line];
    } else {
      currentLines.add(line);
    }
  }
  save();
  return sections;
}
