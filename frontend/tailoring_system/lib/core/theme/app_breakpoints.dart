import 'package:flutter/material.dart';
import 'package:responsive_framework/responsive_framework.dart';

class AppBreakpoints {
  static const double mobile = 480;
  static const double tablet = 768;
  static const double desktop = 1200;
  static const double largeDesktop = 1600;

  static const List<Breakpoint> breakpoints = [
    Breakpoint(start: 0, end: mobile, name: MOBILE),
    Breakpoint(start: mobile + 0.1, end: tablet, name: TABLET),
    Breakpoint(start: tablet + 0.1, end: desktop, name: DESKTOP),
    Breakpoint(start: desktop + 0.1, end: double.infinity, name: 'XL'),
  ];

  static bool isMobile(double width) => width < tablet;
  static bool isTablet(double width) => width >= tablet && width < desktop;
  static bool isDesktop(double width) => width >= desktop;

  static Widget builder({
    required BuildContext context,
    required Widget mobile,
    Widget? tablet,
    Widget? desktop,
    Widget? largeDesktop,
  }) {
    final width = MediaQuery.sizeOf(context).width;
    if (width >= AppBreakpoints.largeDesktop) {
      return largeDesktop ?? desktop ?? tablet ?? mobile;
    }
    if (width >= AppBreakpoints.desktop) {
      return desktop ?? tablet ?? mobile;
    }
    if (width >= AppBreakpoints.tablet) {
      return tablet ?? mobile;
    }
    return mobile;
  }
}
