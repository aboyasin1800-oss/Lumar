import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../core/ui_palette.dart';

class PrintHeaderTemplatePreview extends StatelessWidget {
  const PrintHeaderTemplatePreview({super.key});

  @override
  Widget build(BuildContext context) {
    return const PrintHeaderTemplatePreviewScreen();
  }
}

class PrintHeaderTemplatePreviewScreen extends StatelessWidget {
  const PrintHeaderTemplatePreviewScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: UiPalette.screenBackground,
      appBar: AppBar(
        backgroundColor: UiPalette.surfaceCard,
        foregroundColor: UiPalette.adaptiveTextColor(UiPalette.surfaceCard),
        title: const Text('Print Header Template Preview'),
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1600),
            child: AspectRatio(
              aspectRatio: 6.5,
              child: _HeaderCard(),
            ),
          ),
        ),
      ),
    );
  }
}

class _HeaderCard extends StatelessWidget {
  const _HeaderCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(18),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFFB98A35).withValues(alpha: 0.34),
            blurRadius: 25,
            spreadRadius: 6,
          ),
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.36),
            blurRadius: 30,
            offset: const Offset(0, 12),
          ),
        ],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(18),
        child: Container(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: [
                const Color(0xFF0B1620),
                const Color(0xFF132A38),
                const Color(0xFF0A1420),
              ],
              begin: Alignment.centerLeft,
              end: Alignment.centerRight,
            ),
            border: Border.all(
              color: const Color(0xFFD9B35B),
              width: 3,
            ),
          ),
          child: Stack(
            children: [
              Positioned.fill(
                child: IgnorePointer(
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [
                          Colors.transparent,
                          const Color(0xFFDAAF4A).withValues(alpha: 0.15),
                          Colors.transparent,
                        ],
                        begin: Alignment.centerLeft,
                        end: Alignment.centerRight,
                      ),
                    ),
                  ),
                ),
              ),
              Positioned(
                top: 10,
                left: 0,
                right: 0,
                child: Container(
                  height: 1,
                  color: const Color(0xFFEAC47E).withValues(alpha: 0.9),
                ),
              ),
              Positioned(
                bottom: 10,
                left: 0,
                right: 0,
                child: Container(
                  height: 1,
                  color: const Color(0xFFEAC47E).withValues(alpha: 0.8),
                ),
              ),
              Row(
                children: [
                  Expanded(
                    flex: 2,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        vertical: 18,
                        horizontal: 18,
                      ),
                      child: Align(
                        alignment: Alignment.centerLeft,
                        child: _BrandMark(),
                      ),
                    ),
                  ),
                  Expanded(
                    flex: 6,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 20),
                      child: Directionality(
                        textDirection: TextDirection.rtl,
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            ShaderMask(
                              shaderCallback: (bounds) => const LinearGradient(
                                colors: [
                                  Color(0xFFF5D98B),
                                  Color(0xFFB8822E),
                                  Color(0xFFF1D481),
                                ],
                                stops: [0.0, 0.5, 1.0],
                              ).createShader(bounds),
                              child: const Text(
                                'مؤسسة لومار VIP للملابس الرجالية',
                                textAlign: TextAlign.center,
                                style: TextStyle(
                                  fontSize: 68,
                                  fontWeight: FontWeight.w900,
                                  height: 1.0,
                                  letterSpacing: 0.6,
                                  color: Colors.white,
                                  fontFamily: 'serif',
                                ),
                              ),
                            ),
                            const SizedBox(height: 18),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                _PhoneItem(icon: Icons.phone_rounded, number: '96775106545'),
                                const SizedBox(width: 30),
                                _PhoneItem(icon: Icons.phone_android_rounded, number: '967775106545'),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _BrandMark extends StatelessWidget {
  const _BrandMark();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 200,
      height: 200,
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(
          color: const Color(0xFFD8AF48),
          width: 4,
        ),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFFE7C96B).withValues(alpha: 0.36),
            blurRadius: 20,
            spreadRadius: 2,
          ),
        ],
      ),
      child: Container(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(
            color: const Color(0xFFE7C96B),
            width: 3,
          ),
        ),
        child: Stack(
          alignment: Alignment.center,
          children: [
            Positioned.fill(
              child: CustomPaint(
                painter: _CircularGoldPatternPainter(),
              ),
            ),
            Icon(
              Icons.precision_manufacturing_rounded,
              size: 92,
              color: const Color(0xFFDDAE55),
            ),
          ],
        ),
      ),
    );
  }
}

class _PhoneItem extends StatelessWidget {
  const _PhoneItem({required this.icon, required this.number});

  final IconData icon;
  final String number;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          icon,
          size: 28,
          color: const Color(0xFFE4BF62),
        ),
        const SizedBox(width: 8),
        Text(
          number,
          style: const TextStyle(
            color: Color(0xFFF3D485),
            fontSize: 24,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.5,
            fontFamily: 'serif',
          ),
        ),
      ],
    );
  }
}

class _CircularGoldPatternPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.2
      ..color = const Color(0xFFE9C66D).withValues(alpha: 0.8);

    final center = size.center(Offset.zero);
    final radius = size.width * 0.42;

    canvas.drawCircle(center, radius, paint);
    canvas.drawCircle(center, radius * 0.72, paint);

    for (var i = 0; i < 8; i++) {
      final angle = (i / 8) * (2 * math.pi);
      final x = center.dx + math.cos(angle) * radius;
      final y = center.dy + math.sin(angle) * radius;
      final x2 = center.dx + math.cos(angle) * radius * 0.8;
      final y2 = center.dy + math.sin(angle) * radius * 0.8;
      canvas.drawLine(Offset(x, y), Offset(x2, y2), paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
