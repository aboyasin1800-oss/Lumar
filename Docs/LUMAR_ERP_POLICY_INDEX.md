# LUMAR ERP Policy Index

## Purpose

This file is the project reference map for policy ownership. It does not duplicate the detailed policy text. Its purpose is to make the authority and scope clear and to prevent the same rule from being repeated in multiple places.

## Mandatory status

The following domains are MANDATORY 100% for all LUMAR UI covered by the policy and must remain compliant in every current and future screen to which they apply:

- LIGHT / DARK MODE
- LUMAR COLOR SYSTEM
- KEYBOARD NAVIGATION
- UI ZOOM
- RESPONSIVE LAYOUT
- CENTRAL NAVIGATION
- TABS STATE / CENTRAL TAB HANDLING
- MOUSE SIDE BUTTONS
- PERFORMANCE / DATA EXPERIENCE

The performance domain is also PERMANENT, MANDATORY, and 100% under the current source-of-truth file: `.github/instructions/screen-performance-data-loading.instructions.md`.

These are applied as policy requirements, not as recommendations.

## Policy hierarchy

`POLICY INDEX` → `SPECIALIZED POLICY` → `IMPLEMENTATION INSTRUCTION`

- The specialized policy is the source of truth for its subject.
- The implementation instruction may reference the source and state only the execution detail needed for tools or screens.
- A rule must exist in one authoritative place only.
- If a policy appears in more than one file, the specialized document is the controlling version.

## Source of truth by subject

- Arabic / RTL / end-user language visibility: `Docs/ARABIC_UI_POLICY.md`
- Material 3 / design foundation / typography / spacing / responsiveness: `Docs/LUMAR_MATERIAL_3_DESIGN_POLICY.md`
- Design system centralization: `frontend/tailoring_system/docs/DESIGN_SYSTEM_POLICY.md`
- Colors / palette / contrast / semantic colors: `Docs/LUMAR_UI_COLOR_POLICY.md`
- Keyboard behavior / Enter / Escape / Tab / Focus: `Docs/LUMAR_UI_KEYBOARD_POLICY.md`
- Interaction surfaces / hover / pressed / focused visual behavior: `Docs/LUMAR_UI_INTERACTION_SURFACE_POLICY.md`
- Screen structure / module shell / layout rules: `Docs/LUMAR_UI_SCREEN_STRUCTURE_POLICY.md`
- User guide creation and linkage: `.github/instructions/user-guide-screen.instructions.md` as the implementation gate only; user guide content remains a requested, explicit documentation artifact.
- Performance / data loading / caching / paging / rendering / profiling: `.github/instructions/screen-performance-data-loading.instructions.md` is the current permanent and mandatory implementation reference for the LUMAR performance architecture; the system retains one authoritative policy path without duplicating the full text in the index.
- Database ownership / write protection / validation baseline: `Docs/LUMAR_ERP_OFFICIAL_DATABASE_POLICY.md`
- Navigation and keyboard implementation mechanics for Flutter: `.github/instructions/flutter-navigation.instructions.md` as implementation guidance only; it does not replace the specialized policy documents.

## No duplicated rule rule

- No duplicate policy statement may be copied into a second file when a specialist file already owns that subject.
- If a detail is restated in an implementation file, it must only describe execution mechanics, not repeat the business or design rule.
- If a policy conflict exists and one version appears to be a legacy duplicate, the specialized source remains authoritative unless Yasin explicitly decides otherwise.

## Conflict handling

- Prefer the most specialized and current policy for the domain in question.
- If a conflict remains unresolved without changing meaning, stop and report it to the owner rather than inventing a decision.
- No logic change is allowed while consolidating this policy set.

## Implementation requirement

All future policy updates should follow this order:

1. Update the owning specialized document.
2. Keep the implementation instruction short and limited to execution mechanics.
3. Replace duplicate text with a reference or a short pointer to the authoritative policy.
4. Preserve all approved decisions and business meaning.

## Final rule

Any policy document that is not the authoritative source for a subject must either:

- link to the source, or
- document only the local implementation task that depends on the source.

It must not duplicate the full subject rule.
