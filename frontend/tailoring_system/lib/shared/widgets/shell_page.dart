import 'package:flutter/material.dart';

class ShellPage extends StatelessWidget {
  const ShellPage({
    required this.title,
    required this.icon,
    required this.subtitle,
    this.body,
    this.actions,
    this.drawer,
    this.bottomNavigationBar,
    super.key,
  });

  final String title;
  final IconData icon;
  final String subtitle;
  final Widget? body;
  final List<Widget>? actions;
  final Widget? drawer;
  final Widget? bottomNavigationBar;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Row(
          children: [
            Icon(icon),
            const SizedBox(width: 8),
            Text(title),
          ],
        ),
        actions: actions ?? const [],
      ),
      drawer: drawer,
      body: body ??
          Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 56),
                const SizedBox(height: 12),
                Text(title),
                const SizedBox(height: 8),
                Text(subtitle),
              ],
            ),
          ),
      bottomNavigationBar: bottomNavigationBar,
    );
  }
}

class ShellInfoCard extends StatelessWidget {
  const ShellInfoCard({
    required this.title,
    required this.caption,
    this.trailing,
    super.key,
  });

  final String title;
  final String caption;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: ListTile(
        title: Text(title),
        subtitle: Text(caption),
        trailing: trailing ?? const Icon(Icons.chevron_right),
      ),
    );
  }
}
