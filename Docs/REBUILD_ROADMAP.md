# LUMAR Rebuild Roadmap

## Current State

The clean workspace, an empty buildable ASP.NET Core backend, a Flutter project location, Git metadata, and read-only recovery documentation are the only deliverables at this stage.

## Deferred Work

1. Review and approve the documented module-to-table map.
2. Define bounded domain contracts for one approved module at a time.
3. Add connection configuration outside Git using environment-specific secret storage.
4. Implement tests and API contracts before user interface work for each module.
5. Build Flutter flows only after the corresponding backend contracts are reviewed.

## Explicitly Excluded

- No source code was recovered from damaged files.
- No executable files were decompiled.
- No database migration, restore, scaffold, or data seeding was performed.
- No claim is made that this structure is functionally equivalent to the prior system.