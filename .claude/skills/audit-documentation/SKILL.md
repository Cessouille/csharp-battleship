---
name: audit-documentation
description: Nightly audit of README/CLAUDE.md quality and whether the code respects the BattleShip assignment's documented rules, written to docs/audits/documentation.md.
---

# Documentation & assignment-compliance audit

Check the project's documentation and its accuracy against the actual repo state, and write a single Markdown report to `docs/audits/documentation.md`, **replacing its previous content entirely**.

## What to check

1. **README.md completeness** — per `CLAUDE.md` §8 ("README.md"), it must be enough on its own for another team to launch the project: member names, launch commands for API + App with their *real* ports (check `BattleShip.API/Properties/launchSettings.json` and `BattleShip.App/Properties/launchSettings.json`, not the catalog example ports), delivered features, backlog trade-offs (done / dropped / why), and known limitations. Flag anything missing or stale.

2. **CLAUDE.md accuracy** — `CLAUDE.md` describes the repo's state (which files exist/are absent, e.g. `.gitignore`, `global.json`, `Protos/`, `docs/adr/`, `PROMPTS.md`, `REVUE-IA.md`). Re-check each claim against the actual repo and flag drift in either direction (something it says is absent that now exists, or vice versa).

3. **Rendu checklist** — `CLAUDE.md`'s "Checklist de rendu" section lists the definition of done for grading. Go through it item by item and report, for each, whether it appears satisfied, partially satisfied, or not satisfied, with evidence (file paths, or absence thereof).

4. **Engine-rule and technical-constraint compliance** — cross-check the "Règles du moteur de jeu (non négociables)" and "Contraintes techniques du cours" sections of `CLAUDE.md` against the actual code: server-side-only rule validation, no leaking of the hidden opponent grid in any DTO, invalid moves rejected without mutating state, FluentValidation on server inputs (HTTP and gRPC), a working gRPC-Web exchange, CORS configured for the real App origin, explicit DTOs at the API/App boundary. Where the relevant code doesn't exist yet, say so rather than guessing.

5. **PROMPTS.md / docs/adr/ / REVUE-IA.md** — if present, sanity-check they're being kept current (recent-looking entries, not just template boilerplate) rather than auditing their prose quality in depth.

## Report format

```markdown
# Audit de documentation et conformité — BattleShip

Date de l'audit : <date>
Commit analysé : <sha>

## Résumé
<1-3 sentences>

## README.md
## CLAUDE.md — exactitude
## Checklist de rendu
## Règles du moteur et contraintes techniques
## Livrables IA (PROMPTS.md / ADR / REVUE-IA.md)

Each section: findings as `- description — evidence`, or `Aucun problème notable.` if none found.
```

## Tool use

Read-only over the whole repo (`Read`, `Grep`, `Glob`). Only write to `docs/audits/documentation.md` — never modify README.md, CLAUDE.md, or any other source file.
