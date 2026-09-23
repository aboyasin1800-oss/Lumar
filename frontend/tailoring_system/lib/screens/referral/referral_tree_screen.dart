import 'dart:async';

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/rl_ui_text.dart';
import '../../core/ui_palette.dart';
import '../../models/referral_models.dart';
import '../../repositories/referral_repository.dart';

class ReferralTreeScreen extends StatefulWidget {
  const ReferralTreeScreen({
    super.key,
    this.customerId,
    this.repository,
  });

  final int? customerId;
  final ReferralRepository? repository;

  @override
  State<ReferralTreeScreen> createState() => _ReferralTreeScreenState();
}

class _ReferralTreeScreenState extends State<ReferralTreeScreen> {
  late final ReferralRepository _repository;
  final TextEditingController _searchController = TextEditingController();
  final Map<int, bool> _expandedNodes = <int, bool>{};
  final TransformationController _treeTransform = TransformationController();
  final FocusNode _treeFocusNode = FocusNode();
  Timer? _searchDebounce;
  List<ReferralCustomerIdentity> _customerSuggestions = const [];
  bool _suggestionsLoading = false;

  double _treeScale = 1.0;
  Offset _treePan = Offset.zero;
  bool _altPressed = false;

  Future<List<ReferralRoot>>? _rootsFuture;
  Future<ReferralTree?>? _treeFuture;
  int? _selectedCustomerId;
  String? _selectedCustomerName;
  String? _selectedCustomerCode;

  @override
  void initState() {
    super.initState();
    _repository = widget.repository ?? ReferralRepository();
    _rootsFuture = _repository.getRoots();
    _treeFuture = widget.customerId != null && widget.customerId! > 0
        ? _loadCustomerTree(widget.customerId!)
        : _loadInitialTree();
    _searchController.addListener(() {
      if (mounted) {
        setState(() {});
      }
    });
  }

  Future<ReferralTree?> _loadInitialTree() async {
    final roots = await _repository.getRoots();
    if (roots.isEmpty) {
      return null;
    }

    return _loadCustomerTree(roots.first.customerId);
  }

  Future<ReferralTree> _loadCustomerTree(int customerId) async {
    final tree = await _repository.getTree(customerId);
    if (mounted) {
      setState(() {
        _selectedCustomerId = tree.rootCustomerId;
        _selectedCustomerName = tree.rootCustomerName;
        _selectedCustomerCode = tree.rootCustomerCode;
      });
    } else {
      _selectedCustomerId = tree.rootCustomerId;
      _selectedCustomerName = tree.rootCustomerName;
      _selectedCustomerCode = tree.rootCustomerCode;
    }
    return tree;
  }

  Future<void> _selectRoot(int customerId) async {
    final tree = await _loadCustomerTree(customerId);
    _expandedNodes.clear();
    setState(() {
      _selectedCustomerId = customerId;
      _treeFuture = Future<ReferralTree?>.value(tree);
    });
  }

  Future<void> _selectNode(ReferralTreeNode node) async {
    final tree = await _loadCustomerTree(node.customerId);
    _expandedNodes[node.customerId] = true;
    setState(() {
      _selectedCustomerId = node.customerId;
      _treeFuture = Future<ReferralTree?>.value(tree);
    });
    _showNodeDetails(node);
  }

  Future<void> _selectCustomer(ReferralCustomerIdentity customer) async {
    _searchDebounce?.cancel();
    _searchController.text = customer.customerName ??
        customer.customerCode ??
        customer.phoneNumber ??
        '';
    _customerSuggestions = const [];
    _suggestionsLoading = false;
    _expandedNodes.clear();
    setState(() {
      _selectedCustomerId = customer.customerId;
      _selectedCustomerName = customer.customerName;
      _selectedCustomerCode = customer.customerCode;
      _treeFuture = _loadCustomerTree(customer.customerId);
    });
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    final query = value.trim();
    if (query.isEmpty) {
      setState(() {
        _customerSuggestions = const [];
        _suggestionsLoading = false;
      });
      return;
    }

    setState(() => _suggestionsLoading = true);
    _searchDebounce = Timer(const Duration(milliseconds: 300), () async {
      try {
        final suggestions = await _repository.searchCustomers(query);
        if (!mounted || _searchController.text.trim() != query) return;
        setState(() {
          _customerSuggestions = suggestions;
          _suggestionsLoading = false;
        });
      } catch (_) {
        if (!mounted || _searchController.text.trim() != query) return;
        setState(() {
          _customerSuggestions = const [];
          _suggestionsLoading = false;
        });
      }
    });
  }

  void _toggleNodeExpansion(int nodeId) {
    setState(() {
      final current = _expandedNodes[nodeId] ?? false;
      _expandedNodes[nodeId] = !current;
    });
  }

  void _applyTreeTransform() {
    _treeTransform.value = Matrix4.identity()
      ..translate(_treePan.dx, _treePan.dy)
      ..scale(_treeScale);
  }

  bool get _isAltPressed {
    return _altPressed ||
        HardwareKeyboard.instance
            .isLogicalKeyPressed(LogicalKeyboardKey.altLeft) ||
        HardwareKeyboard.instance
            .isLogicalKeyPressed(LogicalKeyboardKey.altRight);
  }

  void _handleKeyEvent(RawKeyEvent event) {
    final isAltPressed = event.isAltPressed;
    if (isAltPressed != _altPressed) {
      setState(() {
        _altPressed = isAltPressed;
      });
    }
  }

  void _zoomTree(double delta) {
    if (!_isAltPressed) {
      return;
    }

    setState(() {
      _treeScale = (_treeScale + delta).clamp(0.6, 2.4);
      _applyTreeTransform();
    });
  }

  void _handleWheelZoom(PointerSignalEvent event) {
    if (event is! PointerScrollEvent || !_isAltPressed) {
      return;
    }

    final delta = event.scrollDelta.dy;
    if (delta == 0) {
      return;
    }

    final step = delta < 0 ? 0.12 : -0.12;
    setState(() {
      _treeScale = (_treeScale + step).clamp(0.6, 2.4);
      _applyTreeTransform();
    });
  }

  void _panTree(double dx, double dy) {
    setState(() {
      _treePan = Offset(
        (_treePan.dx + dx).clamp(-500.0, 500.0),
        (_treePan.dy + dy).clamp(-500.0, 500.0),
      );
      _applyTreeTransform();
    });
  }

  bool _matchesQuery(String? value, String query) {
    if (query.isEmpty) {
      return true;
    }
    return (value ?? '').toLowerCase().contains(query.toLowerCase());
  }

  ReferralTreeNode? _visibleTree(ReferralTreeNode node,
      {required String query}) {
    final normalizedQuery = query.trim();

    if (normalizedQuery.isNotEmpty) {
      final selfMatches = _matchesQuery(node.customerName, normalizedQuery) ||
          _matchesQuery(node.customerCode, normalizedQuery) ||
          _matchesQuery(node.referralCode, normalizedQuery);

      final visibleChildren = <ReferralTreeNode>[];
      for (final child in node.children) {
        final childVisible = _visibleTree(child, query: normalizedQuery);
        if (childVisible != null) {
          visibleChildren.add(childVisible);
        }
      }

      if (!selfMatches && visibleChildren.isEmpty) {
        return null;
      }

      return ReferralTreeNode(
        customerId: node.customerId,
        customerCode: node.customerCode,
        customerName: node.customerName,
        parentCustomerId: node.parentCustomerId,
        level: node.level,
        children: visibleChildren,
        directChildrenCount: node.directChildrenCount,
        totalDescendantsCount: node.totalDescendantsCount,
        maxDepth: node.maxDepth,
        referralCode: node.referralCode,
        isActive: node.isActive,
      );
    }

    if (node.parentCustomerId != null && !_isNodeExpanded(node)) {
      return ReferralTreeNode(
        customerId: node.customerId,
        customerCode: node.customerCode,
        customerName: node.customerName,
        parentCustomerId: node.parentCustomerId,
        level: node.level,
        children: const <ReferralTreeNode>[],
        directChildrenCount: node.directChildrenCount,
        totalDescendantsCount: node.totalDescendantsCount,
        maxDepth: node.maxDepth,
        referralCode: node.referralCode,
        isActive: node.isActive,
      );
    }

    final visibleChildren = <ReferralTreeNode>[];
    for (final child in node.children) {
      final childVisible = _visibleTree(child, query: '');
      if (childVisible != null) {
        visibleChildren.add(childVisible);
      }
    }

    return ReferralTreeNode(
      customerId: node.customerId,
      customerCode: node.customerCode,
      customerName: node.customerName,
      parentCustomerId: node.parentCustomerId,
      level: node.level,
      children: visibleChildren,
      directChildrenCount: node.directChildrenCount,
      totalDescendantsCount: node.totalDescendantsCount,
      maxDepth: node.maxDepth,
      referralCode: node.referralCode,
      isActive: node.isActive,
    );
  }

  bool _isNodeExpanded(ReferralTreeNode node) {
    final query = _searchController.text.trim();
    if (query.isNotEmpty) {
      return true;
    }

    if (_expandedNodes.containsKey(node.customerId)) {
      return _expandedNodes[node.customerId]!;
    }

    return true;
  }

  void _showNodeDetails(ReferralTreeNode node) {
    showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: UiPalette.surfaceCard,
          title: Text(
            node.customerName ?? 'العميل',
            style: const TextStyle(
              color: UiPalette.textMain,
              fontWeight: FontWeight.bold,
            ),
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _detailRow('اسم العميل', node.customerName ?? '-'),
              _detailRow('كود العميل', node.customerCode ?? '-'),
              _detailRow('كود الإحالة', node.referralCode ?? '-'),
              _detailRow(
                'المحيل المباشر',
                node.parentCustomerId == null
                    ? 'لا يوجد'
                    : 'العميل #${node.parentCustomerId}',
              ),
              _detailRow('المستوى', '${node.level}'),
              _detailRow(
                  'عدد الإحالات المباشرة', '${node.directChildrenCount}'),
              _detailRow('إجمالي التابعين', '${node.totalDescendantsCount}'),
              _detailRow('رصيد النقاط', '-'),
              _detailRow('إجمالي نقاط الإحالة', '-'),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text(
                'إغلاق',
                style: TextStyle(color: UiPalette.primary),
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _detailRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: UiPalette.textSoft)),
          const SizedBox(height: 2),
          Text(value, style: const TextStyle(color: UiPalette.textMain)),
        ],
      ),
    );
  }

  Future<void> _submitSearch() async {
    final query = _searchController.text.trim();
    if (query.isEmpty) {
      return;
    }

    final suggestions = _customerSuggestions.isNotEmpty
        ? _customerSuggestions
        : await _repository.searchCustomers(query);
    if (suggestions.isNotEmpty) {
      await _selectCustomer(suggestions.first);
    }
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    _searchController.dispose();
    _treeFocusNode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final screenBackground =
        isDark ? UiPalette.darkBackground : UiPalette.lightBackground;
    final cardColor = isDark ? UiPalette.surfaceCard : UiPalette.lightCard;
    final altCardColor = isDark ? UiPalette.softBlue : UiPalette.lightCardAlt;
    final textColor = isDark ? UiPalette.textMain : UiPalette.lightText;

    return Scaffold(
      backgroundColor: screenBackground,
      appBar: AppBar(
        title: const Text('شجرة الإحالة'),
        backgroundColor: cardColor,
        foregroundColor: textColor,
      ),
      body: Column(
        children: [
          FutureBuilder<List<ReferralRoot>>(
            future: _rootsFuture,
            builder: (context, rootsSnapshot) {
              if (rootsSnapshot.connectionState == ConnectionState.waiting) {
                return const LinearProgressIndicator(minHeight: 2);
              }

              final roots = rootsSnapshot.data ?? const <ReferralRoot>[];
              final selectedCustomerId = _selectedCustomerId ??
                  (roots.isEmpty ? null : roots.first.customerId);
              if (selectedCustomerId == null) {
                return const SizedBox.shrink();
              }

              return _buildHeader(
                roots,
                selectedCustomerId,
                cardColor,
                textColor,
              );
            },
          ),
          _buildSearchField(cardColor, textColor),
          Expanded(
            child: FutureBuilder<ReferralTree?>(
              future: _treeFuture,
              builder: (context, treeSnapshot) {
                if (treeSnapshot.connectionState == ConnectionState.waiting) {
                  return const Center(child: CircularProgressIndicator());
                }

                if (treeSnapshot.hasError) {
                  return Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(
                        RlUiText.friendlyError(treeSnapshot.error),
                        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                              color: textColor,
                            ),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  );
                }

                final tree = treeSnapshot.data;
                if (tree == null) {
                  return Center(
                    child: Text(
                      'لا توجد شجرة إحالة محددة. ابحث عن عميل لاختيار شجرته.',
                      style: TextStyle(color: textColor),
                      textAlign: TextAlign.center,
                    ),
                  );
                }

                final rootNode = ReferralTreeNode(
                  customerId: tree.rootCustomerId,
                  customerCode: tree.rootCustomerCode,
                  customerName: tree.rootCustomerName,
                  parentCustomerId: null,
                  level: 0,
                  children: tree.children,
                  directChildrenCount: tree.directReferralsCount,
                  totalDescendantsCount: tree.totalDescendantsCount,
                  maxDepth: tree.maxDepth,
                  referralCode: null,
                  isActive: true,
                );

                final visibleRoot = _visibleTree(
                  rootNode,
                  query: _searchController.text.trim(),
                );
                if (visibleRoot == null) {
                  return Center(
                    child: Text(
                      'لم يتم العثور على عميل مطابق داخل الشجرة الحالية. اختر عميلاً من الاقتراحات.',
                      style: TextStyle(color: textColor),
                      textAlign: TextAlign.center,
                    ),
                  );
                }

                return Column(
                  children: [
                    if (rootNode.children.isEmpty)
                      Padding(
                        padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                        child: Text(
                          'لا يوجد عملاء محالون لهذا العميل.',
                          style: TextStyle(color: textColor),
                        ),
                      ),
                    Expanded(
                      child: _buildTreeCanvas(
                        visibleRoot,
                        cardColor,
                        altCardColor,
                        textColor,
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHeader(
    List<ReferralRoot> roots,
    int selectedCustomerId,
    Color cardColor,
    Color textColor,
  ) {
    final items = roots
        .map(
          (root) => DropdownMenuItem<int>(
            value: root.customerId,
            child: Text(
              '${root.customerName ?? 'عميل'} (${root.customerCode ?? root.customerId})',
              style: TextStyle(color: textColor),
            ),
          ),
        )
        .toList();
    if (!roots.any((root) => root.customerId == selectedCustomerId)) {
      items.insert(
        0,
        DropdownMenuItem<int>(
          value: selectedCustomerId,
          child: Text(
            '${_selectedCustomerName ?? 'العميل المحدد'} (${_selectedCustomerCode ?? selectedCustomerId})',
            style: TextStyle(color: textColor),
          ),
        ),
      );
    }

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: cardColor,
          borderRadius: BorderRadius.circular(14),
          border:
              Border.all(color: UiPalette.primaryBorder.withValues(alpha: 0.5)),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          child: DropdownButtonHideUnderline(
            child: DropdownButton<int>(
              value: selectedCustomerId,
              isExpanded: true,
              dropdownColor: cardColor,
              iconEnabledColor: UiPalette.primary,
              items: items,
              onChanged: (value) {
                if (value == null) {
                  return;
                }
                _selectRoot(value);
              },
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildSearchField(Color cardColor, Color textColor) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      child: Column(
        children: [
          TextField(
            controller: _searchController,
            textInputAction: TextInputAction.search,
            onChanged: _onSearchChanged,
            onSubmitted: (_) => _submitSearch(),
            decoration: InputDecoration(
              hintText: 'ابحث عن العميل بالاسم أو الكود أو الهاتف',
              hintStyle: const TextStyle(color: UiPalette.textSoft),
              filled: true,
              fillColor: cardColor,
              prefixIcon: const Icon(Icons.search, color: UiPalette.primary),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: BorderSide.none,
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: BorderSide(
                    color: UiPalette.primaryBorder.withValues(alpha: 0.35)),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: const BorderSide(color: UiPalette.primary),
              ),
            ),
            style: TextStyle(color: textColor),
          ),
          if (_suggestionsLoading)
            const LinearProgressIndicator(minHeight: 2)
          else if (_customerSuggestions.isNotEmpty)
            _buildCustomerSuggestions(cardColor, textColor),
        ],
      ),
    );
  }

  Widget _buildCustomerSuggestions(Color cardColor, Color textColor) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxHeight: 220),
      child: Card(
        color: cardColor,
        margin: EdgeInsets.zero,
        child: ListView.separated(
          shrinkWrap: true,
          itemCount: _customerSuggestions.length,
          separatorBuilder: (_, __) => const Divider(height: 1),
          itemBuilder: (context, index) {
            final customer = _customerSuggestions[index];
            final name = customer.customerName ?? 'عميل';
            final code = customer.customerCode ?? '-';
            final phone = customer.phoneNumber ?? '';
            return ListTile(
              dense: true,
              leading: const Icon(Icons.person_search_outlined),
              title: Text(
                '$code  $name',
                style: TextStyle(color: textColor),
              ),
              subtitle: phone.isEmpty
                  ? null
                  : Text(
                      phone,
                      style:
                          TextStyle(color: textColor.withValues(alpha: 0.72)),
                    ),
              onTap: () => _selectCustomer(customer),
            );
          },
        ),
      ),
    );
  }

  Widget _buildTreeNodeWidget(
    ReferralTreeNode node, {
    required bool isRoot,
    required Color cardColor,
    required Color textColor,
  }) {
    if (isRoot) {
      return Container(
        width: 130,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFF1E3A8A),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: Colors.amber, width: 3),
          boxShadow: [
            BoxShadow(
              color: Colors.amber.withOpacity(0.4),
              blurRadius: 10,
            ),
          ],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              node.customerCode ?? 'ROOT',
              style: const TextStyle(
                color: Colors.amber,
                fontWeight: FontWeight.bold,
                fontSize: 16,
              ),
            ),
            const Text(
              '(الرئيسي)',
              style: TextStyle(
                color: Colors.white70,
                fontSize: 10,
              ),
            ),
            const Divider(color: Colors.amber),
            Text(
              node.customerName ?? 'العميل',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.bold,
                fontSize: 13,
              ),
            ),
          ],
        ),
      );
    }

    switch (node.level) {
      case 1:
        return SizedBox(
          width: 96,
          height: 96,
          child: DecoratedBox(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: const Color(0xFF15803D),
              border: Border.all(color: Colors.greenAccent, width: 2),
            ),
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Text(
                  node.customerName ?? 'عميل',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
          ),
        );
      case 2:
        return SizedBox(
          width: 82,
          height: 82,
          child: DecoratedBox(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: const Color(0xFFC2410C),
              border: Border.all(color: Colors.orangeAccent, width: 2),
            ),
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(6),
                child: Text(
                  node.customerName ?? 'عميل',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 10,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
          ),
        );
      case 3:
        return Transform.rotate(
          angle: 0.7853981633974483,
          child: Container(
            width: 62,
            height: 62,
            decoration: BoxDecoration(
              color: const Color(0xFF7E22CE),
              border: Border.all(color: Colors.purpleAccent, width: 2),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Transform.rotate(
              angle: -0.7853981633974483,
              child: Center(
                child: Padding(
                  padding: const EdgeInsets.all(4),
                  child: Text(
                    node.customerName ?? 'عميل',
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 9,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ),
            ),
          ),
        );
      default:
        return SizedBox(
          width: 60,
          height: 60,
          child: DecoratedBox(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: const Color(0xFF0E7490),
              border: Border.all(color: Colors.cyanAccent, width: 2),
            ),
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(2),
                child: Text(
                  node.customerName ?? 'عميل',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 8,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
          ),
        );
    }
  }

  Widget _buildTreeCanvas(
    ReferralTreeNode rootNode,
    Color cardColor,
    Color altCardColor,
    Color textColor,
  ) {
    final layout = _computeTreeLayout(rootNode);
    final nodeSize = const Size(128, 58);

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && !_treeFocusNode.hasFocus) {
        FocusScope.of(context).requestFocus(_treeFocusNode);
      }
    });

    return Stack(
      children: [
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: altCardColor.withValues(alpha: 0.35),
          ),
          child: RawKeyboardListener(
            focusNode: _treeFocusNode,
            onKey: _handleKeyEvent,
            child: Listener(
              onPointerSignal: _handleWheelZoom,
              child: InteractiveViewer(
                clipBehavior: Clip.none,
                constrained: false,
                boundaryMargin: const EdgeInsets.all(120),
                minScale: 0.45,
                maxScale: 2.4,
                transformationController: _treeTransform,
                panEnabled: true,
                scaleEnabled: false,
                child: SizedBox(
                  width: layout.width,
                  height: layout.height,
                  child: Stack(
                    children: [
                      Positioned.fill(
                        child: CustomPaint(
                          painter: _TreeConnectionPainter(
                            positions: layout.positions,
                            childrenByParent: layout.childrenByParent,
                            nodeSize: nodeSize,
                          ),
                        ),
                      ),
                      ...layout.positions.entries.map((entry) {
                        final node = layout.nodes[entry.key];
                        if (node == null) {
                          return const SizedBox.shrink();
                        }

                        final isSelected =
                            _selectedCustomerId == node.customerId;
                        final isRoot = node.customerId == rootNode.customerId;
                        final offset = entry.value;
                        final expanded =
                            _expandedNodes[node.customerId] ?? false;
                        final hasChildren = node.children.isNotEmpty;

                        return Positioned(
                          left: offset.dx - 64,
                          top: offset.dy - 54,
                          child: GestureDetector(
                            onTap: () => _showNodeDetails(node),
                            child: Stack(
                              children: [
                                _buildTreeNodeWidget(
                                  node,
                                  isRoot: isRoot,
                                  cardColor: cardColor,
                                  textColor: textColor,
                                ),
                                if (hasChildren)
                                  Positioned(
                                    right: 8,
                                    top: 8,
                                    child: GestureDetector(
                                      onTap: () =>
                                          _toggleNodeExpansion(node.customerId),
                                      behavior: HitTestBehavior.opaque,
                                      child: Container(
                                        width: 20,
                                        height: 20,
                                        decoration: BoxDecoration(
                                          color: UiPalette.primary
                                              .withValues(alpha: 0.16),
                                          borderRadius:
                                              BorderRadius.circular(10),
                                        ),
                                        child: Icon(
                                          expanded
                                              ? Icons.expand_less
                                              : Icons.expand_more,
                                          size: 16,
                                          color: UiPalette.primary,
                                        ),
                                      ),
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        );
                      }),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
        Positioned(
          right: 18,
          bottom: 22,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: Colors.black.withValues(alpha: 0.32),
              borderRadius: BorderRadius.circular(18),
            ),
            child: Padding(
              padding: const EdgeInsets.all(10),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _treeControlButton(
                          Icons.add, () => _zoomTree(0.15), 'ALT + تكبير'),
                      const SizedBox(width: 8),
                      _treeControlButton(
                          Icons.remove, () => _zoomTree(-0.15), 'ALT + تصغير'),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const SizedBox(width: 38),
                      _treeControlButton(Icons.keyboard_arrow_up,
                          () => _panTree(0, -60), 'أعلى'),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _treeControlButton(Icons.keyboard_arrow_left,
                          () => _panTree(-60, 0), 'يسار'),
                      _treeControlButton(Icons.keyboard_arrow_down,
                          () => _panTree(0, 60), 'أسفل'),
                      _treeControlButton(Icons.keyboard_arrow_right,
                          () => _panTree(60, 0), 'يمين'),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _treeControlButton(
      IconData icon, VoidCallback onPressed, String tooltip) {
    return Tooltip(
      message: tooltip,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onPressed,
          borderRadius: BorderRadius.circular(12),
          child: Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(
              color: UiPalette.primary.withValues(alpha: 0.18),
              borderRadius: BorderRadius.circular(12),
              border:
                  Border.all(color: UiPalette.primary.withValues(alpha: 0.45)),
            ),
            child: Icon(icon, color: UiPalette.primary, size: 22),
          ),
        ),
      ),
    );
  }

  _TreeLayout _computeTreeLayout(ReferralTreeNode rootNode) {
    final positions = <int, Offset>{};
    final nodes = <int, ReferralTreeNode>{};
    final childrenByParent = <int, List<int>>{};

    const nodeWidth = 170.0;
    const childGap = 18.0;
    const verticalGap = 150.0;
    const leftOffset = 120.0;
    const topOffset = 90.0;

    double measureSubtree(ReferralTreeNode node) {
      if (node.children.isEmpty) {
        return 1.0;
      }

      final widths = node.children.map(measureSubtree).toList();
      final total = widths.fold<double>(0, (sum, width) => sum + width);
      return total + (widths.length - 1) * 0.70;
    }

    void traverse(ReferralTreeNode node) {
      nodes[node.customerId] = node;
      childrenByParent[node.customerId] =
          node.children.map((child) => child.customerId).toList();
      for (final child in node.children) {
        traverse(child);
      }
    }

    void placeSubtree(ReferralTreeNode node, double leftX, int level) {
      final subtreeWidth = measureSubtree(node);
      final centerX = leftX + (subtreeWidth * nodeWidth / 2.0);
      positions[node.customerId] =
          Offset(centerX + leftOffset, topOffset + (level * verticalGap));

      double cursor = leftX;
      for (final child in node.children) {
        final childWidth = measureSubtree(child);
        placeSubtree(child, cursor, level + 1);
        cursor += childWidth * nodeWidth + childGap;
      }
    }

    traverse(rootNode);
    final rootWidth = measureSubtree(rootNode);
    placeSubtree(rootNode, 0.0, 0);

    final width = (rootWidth * nodeWidth) + leftOffset * 2.0 + childGap * 2.0;
    final height = ((rootNode.maxDepth + 1) * verticalGap) + 170.0;

    return _TreeLayout(
      positions: positions,
      nodes: nodes,
      childrenByParent: childrenByParent,
      width: width,
      height: height,
    );
  }
}

class _TreeLayout {
  const _TreeLayout({
    required this.positions,
    required this.nodes,
    required this.childrenByParent,
    required this.width,
    required this.height,
  });

  final Map<int, Offset> positions;
  final Map<int, ReferralTreeNode> nodes;
  final Map<int, List<int>> childrenByParent;
  final double width;
  final double height;
}

class _TreeConnectionPainter extends CustomPainter {
  const _TreeConnectionPainter({
    required this.positions,
    required this.childrenByParent,
    required this.nodeSize,
  });

  final Map<int, Offset> positions;
  final Map<int, List<int>> childrenByParent;
  final Size nodeSize;

  @override
  void paint(Canvas canvas, Size size) {
    if (positions.isEmpty) {
      return;
    }

    final paint = Paint()
      ..color = UiPalette.primaryBorder.withValues(alpha: 0.9)
      ..strokeWidth = 2.2
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..isAntiAlias = true;

    for (final entry in childrenByParent.entries) {
      final parentOffset = positions[entry.key];
      if (parentOffset == null) {
        continue;
      }

      final children = entry.value
          .map((childId) => positions[childId])
          .whereType<Offset>()
          .toList();

      if (children.isEmpty) {
        continue;
      }

      final parentBottom = parentOffset.dy + (nodeSize.height / 2);
      final branchY = parentBottom + 26;
      final minX =
          children.map((offset) => offset.dx).reduce((a, b) => a < b ? a : b);
      final maxX =
          children.map((offset) => offset.dx).reduce((a, b) => a > b ? a : b);

      canvas.drawLine(
        Offset(parentOffset.dx, parentBottom),
        Offset(parentOffset.dx, branchY),
        paint,
      );

      if (children.length > 1) {
        canvas.drawLine(
          Offset(minX, branchY),
          Offset(maxX, branchY),
          paint,
        );
      }

      for (final child in children) {
        final childTop = child.dy - (nodeSize.height / 2);
        canvas.drawLine(
          Offset(child.dx, branchY),
          Offset(child.dx, childTop),
          paint,
        );
      }
    }
  }

  @override
  bool shouldRepaint(covariant _TreeConnectionPainter oldDelegate) {
    return oldDelegate.positions != positions ||
        oldDelegate.childrenByParent != childrenByParent ||
        oldDelegate.nodeSize != nodeSize;
  }
}
