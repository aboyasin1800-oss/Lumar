import 'package:flutter/material.dart';

import '../core/app_navigation.dart';

class StructurePlaceholder extends StatelessWidget {
  const StructurePlaceholder({required this.title, required this.description, super.key});

  final String title;
  final String description;

  @override
  Widget build(BuildContext context) => Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 520),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.construction_outlined, size: 48, color: Theme.of(context).colorScheme.primary),
              const SizedBox(height: 16),
              Text(title, style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              Text(description, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              const Text('هذه الشاشة جاهزة للمرحلة التشغيلية اللاحقة.', textAlign: TextAlign.center),
            ],
          ),
        ),
      );
}

class ModuleSection extends StatelessWidget {
  const ModuleSection({required this.title, required this.items, super.key});

  final String title;
  final List<ModuleSectionItem> items;

  @override
  Widget build(BuildContext context) => ListView(
        children: [
          Text(title, style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 20),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: items
                .map((item) => SizedBox(
                      width: 240,
                      child: Card(
                        child: ListTile(
                          leading: Icon(item.icon),
                          title: Text(item.title),
                          subtitle: Text(item.description),
                          onTap: () => AppNavigation.pushNamed(context, '/structure/${Uri.encodeComponent(item.title)}'),
                        ),
                      ),
                    ))
                .toList(),
          ),
        ],
      );
}

class ModuleSectionItem {
  const ModuleSectionItem(this.title, this.description, this.icon);

  final String title;
  final String description;
  final IconData icon;
}
