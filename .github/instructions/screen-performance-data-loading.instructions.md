---
description: "Use when creating a new LUMAR ERP Flutter screen or its supporting list/search/filter API. Enforces server-side paging, search, filtering, lazy loading, summary APIs, indexes, and performance measurement."
applyTo: ["frontend/tailoring_system/lib/**/*.dart", "Backend/LUMAR_ERP_API_V2/**/*.cs"]
---

# LUMAR ERP Screen Performance And Data Loading Policy

## Scope

- This policy is mandatory for every new screen and every new API that supports a screen.
- Do not refactor an existing screen to this policy unless Yasin explicitly requests it.
- Do not optimize a screen based on a guess. Record the current load time, query time, and record count before changing its loading path, then measure again afterward.

## Screen Opening

- Render the screen immediately; load operational data after the first frame.
- Use an Arabic loading skeleton, placeholder, or progress state while data is pending.
- Load only the section, tab, and data that the user has requested. Do not preload all tabs, panels, or detail records.
- Keep list DTOs minimal. Fetch a details DTO only when the user opens that record.

## Backend List APIs

- Every new list API must use server-side paging, search, filtering, and sorting. It returns one page only plus `TotalCount`.
- Accept and validate `PageNumber`, `PageSize`, search text, filters, and sort direction as relevant to the screen.
- Execute search, filtering, sorting, and paging in SQL; never return all rows for Flutter to process locally.
- Use explicit column lists. Never add `SELECT *`.
- Create or prove the required database index before relying on paging, search, filtering, or sorting. Paging does not replace indexes.
- For queries expected to operate on more than 10,000 records, validate the execution plan, index usage, estimated cost, and actual runtime before acceptance. Do not accept table scans, large index scans, or unused data retrieval without documented justification.

## Flutter Lists And Interaction

- Use server-side paging with incremental loading for large lists.
- Use `ListView.builder`, `SliverList`, or an equivalent virtualized widget for long collections.
- Send search terms and filters to the API; never load all operational rows to search or filter locally.
- Support refresh while preserving a responsive open state.
- Do not compute business or financial totals from raw list rows inside Flutter.

## Summaries And Financial Data

- Obtain KPIs, totals, balances, revenue, receivables, cash, movements, and journal counts from dedicated summary APIs.
- Each financial indicator must have one documented official source of truth.
- Live operational and financial data must be read from the official source, not from a client cache.

## Cache

- Cache only low-change reference data such as types, categories, cities, regions, and settings.
- Every reference cache must define its TTL, refresh strategy, and invalidation behavior.
- Do not cache live operational or financial data as a substitute for the official source.

## Performance Targets

- Excellent: under 1 second.
- Warning: 1 to 3 seconds.
- Failure: more than 3 seconds; investigate and address the root cause.
- Document before-and-after measurements for every requested performance optimization.
