# R3 SAFE ZERO-OPERATIONAL RESET

This tool is a fail-closed reset planner for the approved development database:

- Database: `LUMAR_ERP_ES_VALIDATION_R3`
- Manifest source: `R3_RESET_REFERENCE_MANIFEST.csv`
- Dependency source: `R3_RESET_DEPENDENCY_GRAPH.csv`

## Modes

### Preview
Runs only a read-only preview of the operational tables and protected baseline tables.

```powershell
powershell -ExecutionPolicy Bypass -File .\R3_SAFE_ZERO_OPERATIONAL_RESET.ps1 -Mode Preview
```

### Verify
Performs read-only verification of the current state without deleting anything.

```powershell
powershell -ExecutionPolicy Bypass -File .\R3_SAFE_ZERO_OPERATIONAL_RESET.ps1 -Mode Verify
```

### Execute
Requires explicit approval to run the actual delete planning. This is intentionally protected by a fail-closed guard.

```powershell
powershell -ExecutionPolicy Bypass -File .\R3_SAFE_ZERO_OPERATIONAL_RESET.ps1 -Mode Execute -AllowExecute
```

## Safety rules

- Only the exact approved database is allowed.
- No delete is executed unless `-AllowExecute` is set.
- All `PRESERVE_BASELINE` tables remain untouched.
- `MIXED_RULE` tables are not blindly deleted.
- `EXCEPTION_REVIEW` tables are excluded from destructive logic.
- Every run is validated against the approved manifest and dependency graph.

## Notes

This implementation is intentionally conservative. It captures the reset plan and validation logic without performing a destructive live reset in the current session.
