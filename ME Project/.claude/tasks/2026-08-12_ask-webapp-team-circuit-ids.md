# T-33: Ask the Web App team which CircuitIDs it sends to this board
> Created: 2026-08-12 | Status: Won't Do (2026-08-12)
> File: `tasks/2026-08-12_ask-webapp-team-circuit-ids.md`

---

## Description
**Developer closed it:** "do not bother about which circuit IDs web
application will address for now. Web application team will work as
compliance with the current ME code." The Web App conforms to the board, not
the reverse — so ADR-17's 1-of-64 limit is a known and accepted constraint,
not a run risk.

## Why / Context
It stays factually true (the `0xDD` frame carries one CircuitID, sent once
per connection) and is still worth knowing when reading `circuit_registry.c`,
but **it no longer blocks T-24 and must not be re-raised as a blocker.**

## Progress Log
- **2026-08-12**: Closed by developer decision.

## Related
- Relates to: ADR-17, T-35, T-24
