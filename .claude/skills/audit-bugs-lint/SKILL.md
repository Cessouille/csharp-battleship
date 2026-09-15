---
name: audit-bugs-lint
description: Nightly audit for bugs, code smells, build/test warnings, and formatting issues across the BattleShip codebase, written to docs/audits/bugs-lint.md.
---

# Bugs, code smells & formatting audit

Find bugs, error-prone patterns, and formatting/lint issues, and write a single Markdown report to `docs/audits/bugs-lint.md`, **replacing its previous content entirely**.

## Steps

1. Run, via Bash, and capture full output:
   - `dotnet build BattleShip.slnx`
   - `dotnet test BattleShip.Tests`
   - `dotnet format --verify-no-changes` (this is the project's formatter — the C#/Blazor equivalent of Prettier; there is no separate JS/CSS toolchain in this repo yet, note that explicitly rather than silently skipping a "prettier" section)
2. Manually review source under `BattleShip.API/`, `BattleShip.App/`, `BattleShip.Models/`, `BattleShip.Tests/` (skip `bin/`, `obj/`) for bugs and code smells not caught by the build: null-reference risks, unhandled exceptions on invalid input, resource/disposal leaks, off-by-one errors, dead code, and mutable shared state that could race across requests.
3. For each finding, cite `file:line`, describe the concrete failure scenario (not just "this looks risky"), and suggest a fix.

## Report format

```markdown
# Audit bugs, code smells et formatage — BattleShip

Date de l'audit : <date>
Commit analysé : <sha>

## Résumé
<1-3 sentences>

## Build
<output summary: errors/warnings, or "Build réussi sans avertissement.">

## Tests
<output summary: pass/fail counts, or notable failures>

## Formatage (dotnet format)
<diff/violations found, or "Aucune violation de formatage.". Note explicitly that no JS/CSS lint/prettier target exists yet if that's still the case.>

## Bugs et code smells
<findings as `- **file:line** — scénario d'échec concret, correctif suggéré`, or "Aucun problème notable.">
```

If the codebase is still an empty scaffold with no game logic, say so plainly instead of inventing findings.

## Tool use

`Read`, `Grep`, `Glob` for review; `Bash` restricted to `dotnet build`, `dotnet test`, and `dotnet format` commands. Only write to `docs/audits/bugs-lint.md` — never modify source files or run `dotnet format` without `--verify-no-changes` (never auto-apply formatting changes).
