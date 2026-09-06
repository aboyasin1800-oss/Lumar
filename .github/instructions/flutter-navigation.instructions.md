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

## Enter Navigation Policy

- Interactive controls must be focusable and expose Flutter's standard `ActivateIntent`; prefer `Button`, `IconButton`, `ListTile`, `InkWell`, and other standard controls.
- Enter and Numpad Enter activate the currently focused non-editable control through `KeyboardPolicy`.
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

Both policies are application-wide requirements for all current and future screens.