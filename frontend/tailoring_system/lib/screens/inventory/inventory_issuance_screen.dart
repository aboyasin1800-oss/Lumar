import 'package:flutter/material.dart';

import '../../core/ui_palette.dart';

class InventoryIssuanceScreen extends StatelessWidget {
  const InventoryIssuanceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('إدارة الصرف المخزني والعهد'),
        centerTitle: true,
      ),
      body: DefaultTabController(
        length: 4,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 8),
            Container(
              height: 52,
              decoration: BoxDecoration(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                    color: UiPalette.borderSoft.withValues(alpha: 0.55)),
              ),
              child: TabBar(
                isScrollable: false,
                tabAlignment: TabAlignment.fill,
                labelColor: UiPalette.textMain,
                unselectedLabelColor: UiPalette.textSoft,
                indicatorColor: const Color.fromARGB(255, 18, 247, 216),
                dividerColor: Colors.transparent,
                indicatorSize: TabBarIndicatorSize.tab,
                indicator: const UnderlineTabIndicator(
                  borderSide: BorderSide(
                      width: 3, color: Color.fromARGB(255, 18, 247, 216)),
                  insets: EdgeInsets.symmetric(horizontal: 6),
                ),
                labelStyle: Theme.of(context).textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      fontSize: 16,
                    ),
                unselectedLabelStyle:
                    Theme.of(context).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w600,
                          fontSize: 12,
                        ),
                tabs: const [
                  Tab(text: 'الصرف التشغيلي'),
                  Tab(text: 'صرف العهدة'),
                  Tab(text: 'العهد المفتوحة'),
                  Tab(text: 'سجل العمليات'),
                ],
              ),
            ),
            const SizedBox(height: 12),
            Expanded(
              child: TabBarView(
                children: [
                  const _OperationalIssueForm(),
                  _buildPlaceholder(
                      context, 'سيتم تنفيذ صرف العهدة في المهمة التالية'),
                  _buildPlaceholder(
                      context, 'سيتم عرض العهد المفتوحة في المهمة التالية'),
                  _buildPlaceholder(
                      context, 'سيتم عرض سجل العمليات في المهمة التالية'),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPlaceholder(BuildContext context, String message) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.hourglass_bottom_rounded,
              size: 48,
              color: Theme.of(context).colorScheme.outline,
            ),
            const SizedBox(height: 16),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: Theme.of(context).colorScheme.outline,
                    fontWeight: FontWeight.w600,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _OperationalIssueForm extends StatefulWidget {
  const _OperationalIssueForm();

  @override
  State<_OperationalIssueForm> createState() => _OperationalIssueFormState();
}

class _OperationalIssueFormState extends State<_OperationalIssueForm> {
  final _formKey = GlobalKey<FormState>();
  String _selectedTool = 'اختر الأداة';
  final TextEditingController _quantityController = TextEditingController();
  final TextEditingController _reasonController = TextEditingController();
  final TextEditingController _notesController = TextEditingController();

  @override
  void dispose() {
    _quantityController.dispose();
    _reasonController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: ListView(
          children: [
            DropdownButtonFormField<String>(
              decoration: InputDecoration(
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                filled: true,
                fillColor: UiPalette.surfaceCard,
              ),
              hint: const Text('اختر الأداة'),
              initialValue: _selectedTool == 'اختر الأداة' ? null : _selectedTool,
              items: const [
                DropdownMenuItem(value: 'مقص', child: Text('مقص')),
                DropdownMenuItem(value: 'مبرة', child: Text('مبرة')),
                DropdownMenuItem(value: 'خيط', child: Text('خيط')),
                DropdownMenuItem(value: 'شريط قياس', child: Text('شريط قياس')),
                DropdownMenuItem(value: 'دبوس', child: Text('دبوس')),
              ],
              onChanged: (value) {
                if (value != null) {
                  setState(() => _selectedTool = value);
                }
              },
              validator: (value) {
                if (value == null) {
                  return 'يرجى اختيار أداة';
                }
                return null;
              },
            ),
            const SizedBox(height: 16),
            buildQuantityField(),
            const SizedBox(height: 16),
            buildReasonField(),
            const SizedBox(height: 16),
            buildNotesField(),
            const SizedBox(height: 24),
            buildExecuteButton(),
          ],
        ),
      ),
    );
  }

  Widget buildQuantityField() {
    return TextFormField(
      controller: _quantityController,
      decoration: InputDecoration(
        labelText: 'الكمية',
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
        ),
        filled: true,
        fillColor: UiPalette.surfaceCard,
        prefixIcon: const Icon(Icons.numbers),
      ),
      keyboardType: TextInputType.number,
      validator: (value) {
        if (value == null || value.isEmpty) {
          return 'يرجى إدخال الكمية';
        }
        if (int.tryParse(value) == null) {
          return 'يرجى إدخال رقم صالح';
        }
        return null;
      },
    );
  }

  Widget buildReasonField() {
    return TextFormField(
      controller: _reasonController,
      decoration: InputDecoration(
        labelText: 'سبب الصرف',
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
        ),
        filled: true,
        fillColor: UiPalette.surfaceCard,
      ),
      maxLines: 2,
      validator: (value) {
        if (value == null || value.isEmpty) {
          return 'يرجى إدخال سبب الصرف';
        }
        return null;
      },
    );
  }

  Widget buildNotesField() {
    return TextFormField(
      controller: _notesController,
      decoration: InputDecoration(
        labelText: 'ملاحظات',
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
        ),
        filled: true,
        fillColor: UiPalette.surfaceCard,
      ),
      maxLines: 3,
    );
  }

  Widget buildExecuteButton() {
    return SizedBox(
      width: double.infinity,
      child: ElevatedButton.icon(
        onPressed: () {
          if (_formKey.currentState?.validate() ?? false) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('تم تنفيذ الصرف بنجاح (نموذج تجريبي)'),
                backgroundColor: Colors.green,
              ),
            );
            _formKey.currentState?.reset();
            setState(() {
              _selectedTool = 'اختر الأداة';
              _quantityController.clear();
              _reasonController.clear();
              _notesController.clear();
            });
          }
        },
        icon: const Icon(Icons.check_circle_outline),
        label: const Text('تنفيذ الصرف'),
        style: ElevatedButton.styleFrom(
          padding: const EdgeInsets.symmetric(vertical: 16),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
      ),
    );
  }
}
