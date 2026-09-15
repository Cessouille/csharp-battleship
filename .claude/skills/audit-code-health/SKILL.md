---
name: audit-code-health
description: Nightly audit of DRY/KISS/YAGNI/SOLID and design-pattern health across the BattleShip codebase, written to docs/audits/code-health.md.
---

# Code health audit

Review the source code for principle and design violations, and write a single Markdown report to `docs/audits/code-health.md`, **replacing its previous content entirely**.

## Scope

Read all non-generated C# and Razor source under:
- `BattleShip.API/`
- `BattleShip.App/`
- `BattleShip.Models/`
- `BattleShip.Tests/`

Skip `bin/`, `obj/`, and any default `dotnet new` scaffold files not yet replaced by real game code (e.g. `Weather.razor`, `Counter.razor`, `Class1.cs`, `UnitTest1.cs` — see `CLAUDE.md` for the full list). Note their presence in the report instead of reviewing their contents as if they were real code.

## What to check

For each of the following, look for concrete violations (not stylistic nitpicks) and cite `file:line`:

- **DRY** — duplicated logic that should be a shared method/type (not superficial repetition like similar-looking but semantically distinct code).
- **KISS** — needlessly complex control flow, indirection, or abstraction for what the logic actually does.
- **YAGNI** — speculative generality, unused extensibility hooks, or configuration for requirements nobody asked for (cross-check against `CLAUDE.md`'s "imposed vs. team decision" table — the course only asks for what's listed there).
- **SOLID** — classes with more than one reason to change, tight coupling that should go through an interface, violations of substitutability, fat interfaces, or dependencies pointing the wrong way.
- **Design patterns** — missing pattern where one would meaningfully simplify the code (e.g. strategy for opponent AI difficulty, factory for fleet/ship creation), or a pattern applied where it adds ceremony without benefit.

## Report format

```markdown
# Audit de santé du code — BattleShip

Date de l'audit : <date>
Commit analysé : <sha>

## Résumé
<1-3 sentences: overall state, most important finding>

## DRY
## KISS
## YAGNI
## SOLID
## Design patterns

Each section: a finding as `- **file:line** — description, why it matters, suggested fix`, or `Aucun problème notable.` if none found.
```

If the codebase is still an empty scaffold with no game logic, say so plainly in the summary instead of inventing findings to fill sections.

## Tool use

Read-only over the source tree (`Read`, `Grep`, `Glob`). Only write to `docs/audits/code-health.md` — never modify source files.
