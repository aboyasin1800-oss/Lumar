# LUMAR ERP Flutter

## Windows SDK and launch

The approved Flutter SDK is `C:\src\flutter`. Open this Flutter folder as a
VS Code workspace so its `.vscode/settings.json` selects that SDK. The local
analysis tasks also use its absolute executable path, without relying on PATH.
Do not reuse another SDK's compiled build hooks or generated Windows configuration.

From `D:\YASIN\frontend\tailoring_system`, run these commands in PowerShell:

```powershell
& 'C:\src\flutter\bin\flutter.bat' pub get
& 'C:\src\flutter\bin\flutter.bat' analyze
& 'C:\src\flutter\bin\flutter.bat' build windows -v
& 'C:\src\flutter\bin\flutter.bat' run -d windows
```

A native-assets error such as `Can't load Kernel binary: Invalid SDK hash`
indicates a compiled Dart hook incompatible with the executing Dart SDK.
Check the failing hook's command and SDK path before changing dependencies or
cleaning build outputs. `MSB8066` alone does not identify the underlying failure.
Build success must be followed by a visible application window and a check for
runtime exceptions.

## Navigation Policies

All current and future screens use two application-wide navigation policies:

- Mouse navigation: pages open through `AppNavigation`; Mouse Back follows the Navigator stack and Mouse Forward restores available forward history.
- Enter navigation: Enter activates the focused control, while editable fields retain their `onSubmitted` behavior.

The enforceable implementation rules are defined in `.github/instructions/flutter-navigation.instructions.md` at the repository root.

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.
