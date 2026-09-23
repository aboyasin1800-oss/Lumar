import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/ui_palette.dart';
import '../../models/customer_creation_models.dart';
import '../../repositories/customer_creation_repository.dart';

class CustomerCreateScreen extends StatefulWidget {
  const CustomerCreateScreen({super.key, this.initialPhone, this.repository});

  final String? initialPhone;
  final CustomerCreationRepository? repository;

  @override
  State<CustomerCreateScreen> createState() => _CustomerCreateScreenState();
}

class _CustomerCreateScreenState extends State<CustomerCreateScreen> {
  final _formKey = GlobalKey<FormState>();
  final _phoneController = TextEditingController();
  final _nameController = TextEditingController();
  final _addressController = TextEditingController();
  final _notesController = TextEditingController();
  final _referrerSearchController = TextEditingController();
  final _otherRelationshipController = TextEditingController();
  late final CustomerCreationRepository _repository =
      widget.repository ?? CustomerCreationRepository();
  Timer? _searchTimer;
  List<CustomerReferralCandidate> _referrerSuggestions = const [];
  bool _referrerSearchLoading = false;
  String? _referrerSearchError;
  int _referrerSearchRequestId = 0;
  bool _selectingReferrer = false;
  CustomerReferralCandidate? _selectedReferrer;
  String _relationshipChoice = '';
  bool _hasReferrer = false;
  bool _saving = false;

  static const _relationships = [
    'أب',
    'أم',
    'أخ',
    'أخت',
    'عم',
    'خال',
    'قريب',
    'صديق',
    'زميل',
    'عميل سابق',
    'أخرى',
  ];

  @override
  void initState() {
    super.initState();
    _phoneController.text = widget.initialPhone?.trim() ?? '';
  }

  @override
  void dispose() {
    _searchTimer?.cancel();
    _phoneController.dispose();
    _nameController.dispose();
    _addressController.dispose();
    _notesController.dispose();
    _referrerSearchController.dispose();
    _otherRelationshipController.dispose();
    super.dispose();
  }

  void _onReferrerSearchChanged(String value) {
    if (_selectingReferrer) return;
    _searchTimer?.cancel();
    final requestId = ++_referrerSearchRequestId;
    if (_selectedReferrer != null) {
      setState(() => _selectedReferrer = null);
    }
    final term = value.trim();
    if (term.length < 2) {
      setState(() {
        _referrerSuggestions = const [];
        _referrerSearchLoading = false;
        _referrerSearchError = null;
      });
      return;
    }
    setState(() {
      _referrerSearchLoading = true;
      _referrerSearchError = null;
    });
    _searchTimer = Timer(const Duration(milliseconds: 250), () async {
      if (!mounted) return;
      try {
        final suggestions = await _repository.searchReferrers(term);
        if (!mounted || requestId != _referrerSearchRequestId) return;
        setState(() {
          _referrerSuggestions = suggestions;
          _referrerSearchLoading = false;
        });
      } catch (_) {
        if (!mounted || requestId != _referrerSearchRequestId) return;
        setState(() {
          _referrerSuggestions = const [];
          _referrerSearchLoading = false;
          _referrerSearchError = 'تعذر البحث عن العملاء الحقيقيين.';
        });
      }
    });
  }

  void _selectReferrer(CustomerReferralCandidate candidate) {
    _searchTimer?.cancel();
    _selectingReferrer = true;
    _referrerSearchController.text = _candidateTitle(candidate);
    _referrerSearchController.selection = TextSelection.collapsed(
      offset: _referrerSearchController.text.length,
    );
    setState(() {
      _selectedReferrer = candidate;
      _referrerSuggestions = const [];
      _referrerSearchLoading = false;
      _referrerSearchError = null;
    });
    _selectingReferrer = false;
  }

  String _candidateTitle(CustomerReferralCandidate candidate) => [
        candidate.customerName,
        candidate.customerCode
      ].where((value) => value?.trim().isNotEmpty == true).join(' - ');

  String? _relationshipValue() {
    if (_relationshipChoice == 'أخرى') {
      final value = _otherRelationshipController.text.trim();
      return value.isEmpty ? null : value;
    }
    return _relationshipChoice.trim().isEmpty ? null : _relationshipChoice;
  }

  String? _validateCustomerName(String? value) {
    final normalized = value?.trim().replaceAll(RegExp(r'\s+'), ' ') ?? '';
    if (normalized.isEmpty) return 'اسم العميل مطلوب';
    final wordCount = normalized.split(' ').length;
    if (wordCount < 3 || wordCount > 4) {
      return 'اكتب اسم العميل ثلاثياً أو رباعياً';
    }
    return null;
  }

  Future<void> _save() async {
    if (!mounted || !_formKey.currentState!.validate()) return;
    if (_hasReferrer && _selectedReferrer == null) {
      _showMessage('اختر العميل المحيل من قائمة العملاء الحقيقيين.');
      return;
    }
    final relationship = _hasReferrer ? _relationshipValue() : null;
    if (_hasReferrer && relationship == null) {
      _showMessage('حدد صلة المحيل بالعميل.');
      return;
    }
    setState(() => _saving = true);
    try {
      final result = await _repository.create(
        customerName: _nameController.text,
        phoneNumber: _phoneController.text,
        address: _addressController.text,
        notes: _notesController.text,
        referrerCustomerId: _hasReferrer ? _selectedReferrer!.customerId : null,
        relationshipType: relationship,
      );
      if (mounted) Navigator.of(context).pop<CustomerCreationResult>(result);
    } on CustomerCreationException catch (error) {
      if (mounted) _showMessage(error.message);
    } catch (_) {
      if (mounted) _showMessage('تعذر الاتصال بالخادم لإنشاء العميل.');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: UiPalette.screenBackground,
        appBar: AppBar(title: const Text('إنشاء عميل')),
        body: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 720),
            child: Form(
              key: _formKey,
              child: ListView(
                padding: const EdgeInsets.all(20),
                children: [
                  _buildIdentitySection(),
                  const SizedBox(height: 16),
                  _buildReferralSection(),
                  const SizedBox(height: 20),
                  FilledButton.icon(
                    onPressed: _saving ? null : _save,
                    icon: _saving
                        ? const SizedBox.square(
                            dimension: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.save_outlined),
                    label: const Text('حفظ'),
                  ),
                ],
              ),
            ),
          ),
        ),
      );

  Widget _buildIdentitySection() => _section(
        title: 'بيانات العميل',
        child: Column(
          children: [
            TextFormField(
              controller: _phoneController,
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(
                labelText: 'رقم الهاتف *',
                prefixIcon: Icon(Icons.phone_outlined),
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'رقم الهاتف مطلوب';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _nameController,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(
                labelText: 'اسم العميل *',
                prefixIcon: Icon(Icons.person_outline),
              ),
              validator: _validateCustomerName,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _addressController,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(
                labelText: 'العنوان',
                prefixIcon: Icon(Icons.location_on_outlined),
              ),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _notesController,
              maxLines: 3,
              textInputAction: TextInputAction.newline,
              decoration: const InputDecoration(
                labelText: 'ملاحظات',
                prefixIcon: Icon(Icons.notes_outlined),
              ),
            ),
          ],
        ),
      );

  Widget _buildReferralSection() => _section(
        title: 'بيانات الإحالة',
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SwitchListTile.adaptive(
              contentPadding: EdgeInsets.zero,
              value: _hasReferrer,
              onChanged: (value) => setState(() {
                _hasReferrer = value;
                if (!value) {
                  _selectedReferrer = null;
                  _referrerSuggestions = const [];
                  _referrerSearchLoading = false;
                  _referrerSearchError = null;
                  _referrerSearchController.clear();
                  _relationshipChoice = '';
                  _otherRelationshipController.clear();
                }
              }),
              title: const Text('هل العميل جاء عن طريق محيل؟'),
              subtitle: Text(_hasReferrer ? 'نعم' : 'لا'),
            ),
            if (_hasReferrer) ...[
              const SizedBox(height: 10),
              TextField(
                controller: _referrerSearchController,
                textInputAction: TextInputAction.search,
                onChanged: _onReferrerSearchChanged,
                decoration: const InputDecoration(
                  labelText: 'ابحث باسم المحيل أو هاتفه أو كود العميل',
                  prefixIcon: Icon(Icons.person_search_outlined),
                ),
              ),
              if (_referrerSearchLoading)
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: LinearProgressIndicator(minHeight: 2),
                )
              else if (_referrerSearchError != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(_referrerSearchError!),
                )
              else if (_referrerSuggestions.isNotEmpty)
                _buildSuggestions()
              else if (_referrerSearchController.text.trim().length >= 2 &&
                  _selectedReferrer == null)
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: Text('لا توجد نتائج مطابقة.'),
                ),
              if (_selectedReferrer != null) _buildSelectedReferrer(),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue:
                    _relationshipChoice.isEmpty ? null : _relationshipChoice,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'صلة المحيل بالعميل',
                  prefixIcon: Icon(Icons.people_alt_outlined),
                ),
                items: _relationships
                    .map((value) => DropdownMenuItem(
                          value: value,
                          child: Text(value),
                        ))
                    .toList(),
                onChanged: (value) =>
                    setState(() => _relationshipChoice = value ?? ''),
              ),
              if (_relationshipChoice == 'أخرى') ...[
                const SizedBox(height: 12),
                TextFormField(
                  controller: _otherRelationshipController,
                  decoration: const InputDecoration(
                    labelText: 'اكتب صلة القرابة أو العلاقة',
                  ),
                ),
              ],
            ],
          ],
        ),
      );

  Widget _buildSuggestions() => Card(
        margin: const EdgeInsets.only(top: 8),
        child: Column(
          children: _referrerSuggestions
              .take(8)
              .map(
                (candidate) => ListTile(
                  dense: true,
                  leading: const Icon(Icons.person_outline),
                  title: Text(candidate.customerName ?? 'عميل بدون اسم'),
                  subtitle: Text([
                    candidate.customerCode,
                    candidate.phoneNumber,
                    if (candidate.referralCode?.trim().isNotEmpty == true)
                      'كود الإحالة: ${candidate.referralCode}',
                  ].whereType<String>().join(' | ')),
                  onTap: () => _selectReferrer(candidate),
                ),
              )
              .toList(),
        ),
      );

  Widget _buildSelectedReferrer() {
    final candidate = _selectedReferrer!;
    return Card(
      color: UiPalette.softBlue,
      margin: const EdgeInsets.only(top: 12),
      child: ListTile(
        leading: const Icon(Icons.verified_user_outlined),
        title: const Text('المحيل المحدد'),
        subtitle: Text([
          candidate.customerName,
          candidate.customerCode,
          candidate.phoneNumber,
          if (candidate.referralCode?.trim().isNotEmpty == true)
            'كود الإحالة: ${candidate.referralCode}',
        ].whereType<String>().join('\n')),
        trailing: IconButton(
          tooltip: 'إلغاء اختيار المحيل',
          onPressed: () => setState(() {
            _selectedReferrer = null;
            _referrerSuggestions = const [];
            _referrerSearchError = null;
            _referrerSearchController.clear();
          }),
          icon: const Icon(Icons.close),
        ),
      ),
    );
  }

  Widget _section({required String title, required Widget child}) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(title, style: const TextStyle(fontWeight: FontWeight.bold)),
              const SizedBox(height: 14),
              child,
            ],
          ),
        ),
      );
}
