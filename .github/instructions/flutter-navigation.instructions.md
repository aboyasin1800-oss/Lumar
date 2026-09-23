---
description: "LUMAR ERP permanent mouse and Enter navigation policies for every current or future Flutter screen."
applyTo: "frontend/tailoring_system/lib/**/*.dart"
---

# Flutter Navigation Policies

Every new navigable Flutter screen must follow both project policies below.

## Mouse Navigation Policy

- Open pages through `AppNavigation.push` or `AppNavigation.pushNamed`.
- Do not add direct `Navigator.push`, `Navigator.pushNamed`, or `MaterialPageRoute` calls outside the central navigation implementation and route factory.
- A child page must use `Scaffold` with `AppBar` so Flutter displays its back button.
- Back must follow the actual Navigator stack. At the root it must do nothing and must not close the Windows application.
- Mouse Back and Mouse Forward are provided globally by `AppNavigationRegion`; do not implement per-screen pointer handlers.
- Escape closes dialogs or returns to the previous screen; it does not perform save or delete.

## Enter Navigation Policy

- Interactive controls must be focusable and expose Flutter's standard `ActivateIntent`; prefer `Button`, `IconButton`, `ListTile`, `InkWell`, and other standard controls.
- Enter and Numpad Enter activate the currently focused non-editable control through `KeyboardPolicy`.
- Enter moves to the next element in the natural screen order; vertically or horizontally depending on layout.
- In forms, Enter moves between fields in input order and executes the primary action at the final field.
- The primary action is the screen's main action such as Save, OK, Search, Approve, Login, or Execute.
- Enter does not perform delete, cancel, close, or back operations.
- Text fields keep their own Enter behavior. Use `textInputAction` and `onSubmitted` for field-to-field movement or form submission.
- Do not add screen-specific raw Enter listeners when standard focus and activation actions can implement the behavior.

## Adaptive Foreground Policy

- Every screen must use the shared palette constants defined in `UiPalette` for base colors.
- Text, icons, and control labels on any colored surface must use `UiPalette.adaptiveTextColor(backgroundColor)` or `UiPalette.adaptiveTextStyle(...)`.
- If the background is light, text must be dark (black). If the background is dark, text must be light (white).
- Do not hardcode `Colors.black` or `Colors.white` for text placed on colored containers unless the background is intentionally fixed and the contrast is verified manually.
- For cards, buttons, panels, and other containers, the text color must be computed from the actual surface color rather than from the screen theme alone.

## Standard Palette Policy

- Use the project palette values in every new screen: `screenBackground`, `surfaceCard`, `softBlue`, `primaryBlue`, `primaryDark`, `purpleAccent`, `textMain`, `textSoft`, and `borderSoft`.
- Keep the visual identity consistent across screens: dark backgrounds with luminous accents and readable contrast.
- Reuse the shared palette instead of introducing local ad hoc colors in new screens.
- The core LUMAR brand color is `const Color.fromARGB(255, 78, 201, 176)`.
- Primary buttons use a light green variant derived from the primary color; borders and form elements use a darker green derivative.
- Dark Mode and Light Mode must both be supported and verified before any screen is accepted.

## Central Design System Reference

- Material Design 3 remains the official foundation for Flutter components.
- The project-specific central design system is the execution layer for all visual implementation.
- The execution layer uses `AppTheme`, `AppTypography`, `AppSpacing`, `AppDimensions`, `AppBreakpoints`, `AppSurface`, and `AppIcons` as the reference implementation for all current and future screens.
- `flex_color_scheme` is used only through `AppTheme`.
- `google_fonts` is used only through `AppTypography`.
- `gap` is used only through `AppSpacing`.
- `responsive_framework` is used only through `AppBreakpoints` and the central layout layer.
- `flutter_svg` is used only through `AppIcons`.

Both policies are application-wide requirements for all current and future screens.