import 'package:flutter/material.dart';

import '../ui_palette.dart';
import 'app_dimensions.dart';

class AppSurface extends StatelessWidget {
  const AppSurface({
    super.key,
    required this.child,
    this.padding,
    this.margin,
    this.color,
    this.borderColor,
    this.elevation = 0,
    this.radius,
  });

  final Widget child;
  final EdgeInsetsGeometry? padding;
  final EdgeInsetsGeometry? margin;
  final Color? color;
  final Color? borderColor;
  final double elevation;
  final double? radius;

  @override
  Widget build(BuildContext context) {
    final resolvedColor = color ?? UiPalette.surfaceCard;
    final resolvedBorder = borderColor ?? UiPalette.borderSoft;
    return Container(
      margin: margin ?? const EdgeInsets.all(4),
      padding: padding ?? const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: resolvedColor,
        borderRadius: BorderRadius.circular(radius ?? AppDimensions.cardRadius),
        border: Border.all(color: resolvedBorder, width: AppDimensions.borderWidth),
        boxShadow: elevation > 0
            ? [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.06),
                  blurRadius: elevation,
                  offset: const Offset(0, 2),
                ),
              ]
            : null,
      ),
      child: child,
    );
  }
}
