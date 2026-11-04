# 1. Record architecture decisions

**Date:** 2026-11-04
**Status:** Accepted

## Context

This project makes a series of choices — messaging library, broker, data isolation strategy, hosting model — where the reasoning matters more than the outcome. Six months from now the choice will still be visible in the code; the reason for it will not.

## Decision

Record each significant decision as a short numbered file in `docs/adr/`. Three sections: context, decision, consequences. Three sentences each is sufficient — these are notes, not essays.

A decision is significant if reversing it would require changing more than one project, or if a reviewer would reasonably ask "why this and not the obvious alternative?".

## Consequences

- The reasoning survives independently of memory.
- `docs/adr/` becomes the honest record, including decisions later reversed. Superseded ADRs are marked, not deleted.
- Slight overhead per decision, which is the point: it discourages changing direction casually.
