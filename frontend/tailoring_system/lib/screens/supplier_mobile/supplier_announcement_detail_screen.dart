import 'package:flutter/material.dart';

import '../../core/ui_palette.dart';
import '../../models/supplier_mobile_models.dart';
import '../../repositories/supplier_mobile_repository.dart';

class SupplierAnnouncementDetailScreen extends StatelessWidget {
  const SupplierAnnouncementDetailScreen({
    super.key,
    required this.announcement,
    required this.repository,
  });

  final SupplierAnnouncement announcement;
  final SupplierMobileRepository repository;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.primary,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
        title: const Text('تفاصيل الإعلان'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: UiPalette.primary,
                borderRadius: BorderRadius.circular(16),
              ),
              child: Text(
                announcement.title.isEmpty ? 'إعلان' : announcement.title,
                style: TextStyle(
                  color: UiPalette.adaptiveTextColor(UiPalette.primary),
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            const SizedBox(height: 16),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: UiPalette.surfaceCard,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: UiPalette.borderSoft),
              ),
              child: Text(
                announcement.body,
                style: const TextStyle(color: UiPalette.textMain, height: 1.7),
              ),
            ),
            const SizedBox(height: 16),
            _MetaRow(label: 'نشط', value: announcement.isActive ? 'نعم' : 'لا'),
            _MetaRow(label: 'يبدأ', value: _formatDate(announcement.startsAtUtc)),
            _MetaRow(label: 'ينتهي', value: _formatDate(announcement.endsAtUtc)),
            _MetaRow(label: 'تم الإنشاء', value: _formatDate(announcement.createdAtUtc)),
          ],
        ),
      ),
    );
  }

  String _formatDate(DateTime value) => '${value.day}/${value.month}/${value.year}';
}

class _MetaRow extends StatelessWidget {
  const _MetaRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: UiPalette.surfaceCard,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: UiPalette.borderSoft),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: UiPalette.textSoft)),
          Text(value, style: const TextStyle(color: UiPalette.textMain, fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }
}
