import 'dart:ui';

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:tailoring_system/models/referral_models.dart';
import 'package:tailoring_system/repositories/referral_repository.dart';
import 'package:tailoring_system/screens/referral/referral_tree_screen.dart';

class _TestReferralRepository extends ReferralRepository {
  @override
  Future<List<ReferralRoot>> getRoots() async {
    return const [
      ReferralRoot(
        customerId: 50,
        customerCode: 'C10046',
        customerName: 'مسعد مسعد',
        directReferralsCount: 1,
        totalDescendantsCount: 4,
        maxDepth: 4,
      ),
    ];
  }

  @override
  Future<ReferralTree> getTree(int customerId) async {
    return const ReferralTree(
      rootCustomerId: 50,
      rootCustomerCode: 'C10046',
      rootCustomerName: 'مسعد مسعد',
      directReferralsCount: 1,
      totalDescendantsCount: 4,
      maxDepth: 4,
      children: [
        ReferralTreeNode(
          customerId: 51,
          customerCode: 'C10047',
          customerName: 'سعدون مسعد',
          parentCustomerId: 50,
          level: 1,
          children: [
            ReferralTreeNode(
              customerId: 52,
              customerCode: 'C10048',
              customerName: 'محمد سعدون مسعد',
              parentCustomerId: 51,
              level: 2,
              children: [],
              directChildrenCount: 0,
              totalDescendantsCount: 0,
              maxDepth: 0,
              referralCode: 'R-10052',
              isActive: true,
            ),
          ],
          directChildrenCount: 1,
          totalDescendantsCount: 1,
          maxDepth: 1,
          referralCode: 'R-10051',
          isActive: true,
        ),
      ],
    );
  }
}

class _BranchingReferralRepository extends ReferralRepository {
  @override
  Future<List<ReferralRoot>> getRoots() async {
    return const [
      ReferralRoot(
        customerId: 1,
        customerCode: 'ROOT-01',
        customerName: 'جذر',
        directReferralsCount: 3,
        totalDescendantsCount: 40,
        maxDepth: 6,
      ),
    ];
  }

  @override
  Future<ReferralTree> getTree(int customerId) async {
    return const ReferralTree(
      rootCustomerId: 1,
      rootCustomerCode: 'ROOT-01',
      rootCustomerName: 'جذر',
      directReferralsCount: 3,
      totalDescendantsCount: 40,
      maxDepth: 6,
      children: [
        ReferralTreeNode(
          customerId: 11,
          customerCode: 'C-11',
          customerName: 'فرع 1',
          parentCustomerId: 1,
          level: 1,
          children: [
            ReferralTreeNode(
              customerId: 111,
              customerCode: 'C-111',
              customerName: 'فرع 1-1',
              parentCustomerId: 11,
              level: 2,
              children: [
                ReferralTreeNode(
                  customerId: 1111,
                  customerCode: 'C-1111',
                  customerName: 'فرع 1-1-1',
                  parentCustomerId: 111,
                  level: 3,
                  children: [
                    ReferralTreeNode(
                      customerId: 11111,
                      customerCode: 'C-11111',
                      customerName: 'فرع 1-1-1-1',
                      parentCustomerId: 1111,
                      level: 4,
                      children: [
                        ReferralTreeNode(
                          customerId: 111111,
                          customerCode: 'C-111111',
                          customerName: 'فرع 1-1-1-1-1',
                          parentCustomerId: 11111,
                          level: 5,
                          children: [],
                          directChildrenCount: 0,
                          totalDescendantsCount: 0,
                          maxDepth: 0,
                          referralCode: 'R-111111',
                          isActive: true,
                        ),
                      ],
                      directChildrenCount: 1,
                      totalDescendantsCount: 1,
                      maxDepth: 1,
                      referralCode: 'R-11111',
                      isActive: true,
                    ),
                  ],
                  directChildrenCount: 1,
                  totalDescendantsCount: 1,
                  maxDepth: 1,
                  referralCode: 'R-1111',
                  isActive: true,
                ),
              ],
              directChildrenCount: 1,
              totalDescendantsCount: 1,
              maxDepth: 1,
              referralCode: 'R-111',
              isActive: true,
            ),
          ],
          directChildrenCount: 1,
          totalDescendantsCount: 2,
          maxDepth: 2,
          referralCode: 'R-11',
          isActive: true,
        ),
        ReferralTreeNode(
          customerId: 12,
          customerCode: 'C-12',
          customerName: 'فرع 2',
          parentCustomerId: 1,
          level: 1,
          children: [
            ReferralTreeNode(
              customerId: 121,
              customerCode: 'C-121',
              customerName: 'فرع 2-1',
              parentCustomerId: 12,
              level: 2,
              children: [
                ReferralTreeNode(
                  customerId: 1211,
                  customerCode: 'C-1211',
                  customerName: 'فرع 2-1-1',
                  parentCustomerId: 121,
                  level: 3,
                  children: const [],
                  directChildrenCount: 0,
                  totalDescendantsCount: 0,
                  maxDepth: 0,
                  referralCode: 'R-1211',
                  isActive: true,
                ),
              ],
              directChildrenCount: 1,
              totalDescendantsCount: 1,
              maxDepth: 1,
              referralCode: 'R-121',
              isActive: true,
            ),
          ],
          directChildrenCount: 1,
          totalDescendantsCount: 1,
          maxDepth: 1,
          referralCode: 'R-12',
          isActive: true,
        ),
        ReferralTreeNode(
          customerId: 13,
          customerCode: 'C-13',
          customerName: 'فرع 3',
          parentCustomerId: 1,
          level: 1,
          children: const [],
          directChildrenCount: 0,
          totalDescendantsCount: 0,
          maxDepth: 0,
          referralCode: 'R-13',
          isActive: true,
        ),
      ],
    );
  }
}

void main() {
  testWidgets('tree renders customer names only and removes zoom controls', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralTreeScreen(
          repository: _TestReferralRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('مسعد مسعد'), findsWidgets);
    expect(find.text('سعدون مسعد'), findsOneWidget);
    expect(find.textContaining('كود العميل'), findsNothing);
    expect(find.textContaining('الكود:'), findsNothing);
    expect(find.textContaining('العمق:'), findsNothing);
    expect(find.byIcon(Icons.zoom_in), findsNothing);
    expect(find.byIcon(Icons.zoom_out), findsNothing);
  });

  testWidgets('tree uses interactive branch layout for multiple child branches and six levels', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralTreeScreen(
          repository: _BranchingReferralRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byType(InteractiveViewer), findsOneWidget);
    expect(find.text('جذر'), findsWidgets);
    expect(find.text('فرع 1'), findsOneWidget);
    expect(find.text('فرع 2'), findsOneWidget);
    expect(find.text('فرع 3'), findsOneWidget);
    expect(find.text('فرع 1-1-1-1-1'), findsOneWidget);
  });

  testWidgets('tree keeps branch expand and collapse controls and supports deep search', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralTreeScreen(
          repository: _BranchingReferralRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.expand_more), findsWidgets);
    expect(find.byIcon(Icons.expand_less), findsNothing);

    await tester.enterText(find.byType(TextField), 'فرع 1-1-1-1-1');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();

    expect(find.text('فرع 1-1-1-1-1'), findsWidgets);
    expect(find.textContaining('لا توجد نتائج مطابقة للبحث.'), findsNothing);
  });

  testWidgets('tree zooms with ALT + mouse wheel', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralTreeScreen(
          repository: _BranchingReferralRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    final viewer = tester.widget<InteractiveViewer>(find.byType(InteractiveViewer));
    final before = viewer.transformationController!.value.getMaxScaleOnAxis();

    await tester.sendKeyDownEvent(LogicalKeyboardKey.altLeft);
    await tester.sendEventToBinding(
      PointerScrollEvent(
        position: tester.getCenter(find.byType(InteractiveViewer)),
        scrollDelta: const Offset(0, -120),
        kind: PointerDeviceKind.mouse,
      ),
    );
    await tester.pump();
    await tester.sendKeyUpEvent(LogicalKeyboardKey.altLeft);

    final after = tester.widget<InteractiveViewer>(find.byType(InteractiveViewer)).transformationController!.value.getMaxScaleOnAxis();
    expect(after, greaterThan(before));
  });

  testWidgets('tree preserves the current linear chain as a real referral sequence', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ReferralTreeScreen(
          repository: _TestReferralRepository(),
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('مسعد مسعد'), findsWidgets);
    expect(find.text('سعدون مسعد'), findsOneWidget);
    expect(find.text('محمد سعدون مسعد'), findsOneWidget);
    expect(find.byType(InteractiveViewer), findsOneWidget);
  });
}
