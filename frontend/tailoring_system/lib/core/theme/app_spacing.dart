import 'package:flutter/material.dart';
import 'package:gap/gap.dart';

class AppSpacing {
  static const double xxs = 4;
  static const double xs = 8;
  static const double sm = 12;
  static const double md = 16;
  static const double lg = 24;
  static const double xl = 32;

  static const EdgeInsets all = EdgeInsets.all(md);
  static const EdgeInsets horizontalPadding = EdgeInsets.symmetric(horizontal: md);
  static const EdgeInsets verticalPadding = EdgeInsets.symmetric(vertical: md);

  static SizedBox horizontal(double value) => SizedBox(width: value);
  static SizedBox vertical(double value) => SizedBox(height: value);
  static SizedBox horizontalGap(double value) => SizedBox(width: value);
  static SizedBox verticalGap(double value) => SizedBox(height: value);
  static Widget gap(double value) => Gap(value);
}
