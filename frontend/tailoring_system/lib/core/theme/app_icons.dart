import 'package:flutter/material.dart';
import 'package:flutter_svg/svg.dart';

import '../ui_palette.dart';
import 'app_dimensions.dart';

class AppIcons {
  static Widget icon(
    IconData icon, {
    double? size,
    Color? color,
    bool rtl = false,
    String? semanticLabel,
  }) {
    return Icon(
      icon,
      size: size ?? AppDimensions.iconMedium,
      color: color ?? UiPalette.primaryBlue,
      textDirection: rtl ? TextDirection.rtl : null,
      semanticLabel: semanticLabel,
    );
  }

  static Widget svg(
    String assetPath, {
    double? size,
    Color? color,
    bool rtl = false,
    String? semanticLabel,
    Widget? fallback,
  }) {
    final resolvedPath = assetPath.startsWith('assets/') ? assetPath : 'assets/icons/$assetPath.svg';
    final iconSize = size ?? AppDimensions.iconMedium;

    try {
      return SvgPicture.asset(
        resolvedPath,
        width: iconSize,
        height: iconSize,
        colorFilter: color != null ? ColorFilter.mode(color, BlendMode.srcIn) : null,
        semanticsLabel: semanticLabel ?? assetPath,
        matchTextDirection: rtl,
      );
    } catch (_) {
      return fallback ??
          Icon(
            Icons.image_not_supported_outlined,
            size: iconSize,
            color: color ?? UiPalette.textSoft,
            textDirection: rtl ? TextDirection.rtl : null,
          );
    }
  }
}
