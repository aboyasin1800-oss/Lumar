# LUMAR Rebuild Roadmap

## Current State

The clean workspace, an empty buildable ASP.NET Core backend, a Flutter project location, Git metadata, and read-only recovery documentation are the only deliverables at this stage.

## Current Platform Scope

The active rebuild scope is Backend, the existing `LUMAR_ERP` database, and Flutter for Windows only. Flutter verification and future application runs use `flutter run -d windows`.

Android development is deferred as a future capability. Android SDK repair, command-line tools, emulators, APK/AAB packaging, USB debugging, and mobile deployment are outside the current phase.

## Deferred Work

1. Review and approve the documented module-to-table map.
2. Define bounded domain contracts for one approved module at a time.
3. Add connection configuration outside Git using environment-specific secret storage.
4. Implement tests and API contracts before user interface work for each module.
5. Build and verify Windows Flutter flows only after the corresponding backend contracts are reviewed.

## Explicitly Excluded

- No source code was recovered from damaged files.
- No executable files were decompiled.
- No database migration, restore, scaffold, or data seeding was performed.
- No Android setup, testing, packaging, or deployment is part of the current phase.
- No claim is made that this structure is functionally equivalent to the prior system.