import 'package:flutter/material.dart';

import '../../core/ui_palette.dart';
import '../../models/supplier_mobile_models.dart';
import '../../repositories/supplier_mobile_repository.dart';

class SupplierMessageDetailScreen extends StatelessWidget {
  const SupplierMessageDetailScreen({
    super.key,
    required this.message,
    required this.repository,
  });

  final SupplierMessage message;
  final SupplierMobileRepository repository;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.primary,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.primary),
        title: const Text('تفاصيل الرسالة'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _InfoCard(
              title: message.subject.isEmpty ? 'رسالة بدون عنوان' : message.subject,
              subtitle: 'النوع: ${message.messageType}',
              color: UiPalette.primary,
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
                message.body,
                style: const TextStyle(color: UiPalette.textMain, height: 1.7),
              ),
            ),
            const SizedBox(height: 16),
            _MetaRow(label: 'الحالة', value: message.deliveryStatus),
            _MetaRow(label: 'القناة', value: message.channel),
            _MetaRow(label: 'تم الإرسال', value: _formatDate(message.sentAtUtc ?? message.createdAtUtc)),
            _MetaRow(label: 'تم الإنشاء', value: _formatDate(message.createdAtUtc)),
            _MetaRow(label: 'مقروء', value: message.isRead ? 'نعم' : 'لا'),
          ],
        ),
      ),
    );
  }

  String _formatDate(DateTime value) => '${value.day}/${value.month}/${value.year}';
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.title, required this.subtitle, required this.color});

  final String title;
  final String subtitle;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: TextStyle(
              color: UiPalette.adaptiveTextColor(color),
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            subtitle,
            style: TextStyle(
              color: UiPalette.adaptiveTextColor(color),
            ),
          ),
        ],
      ),
    );
  }
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
