# Phase 1B Preview (not started)

Phase 1A delivers the development foundation only. **Do not implement Phase 1B until approved.**

## Intended Phase 1B contents

1. **Contracts package** — versioned DTOs / Zod schemas for IPC payloads  
2. **Local API skeleton** — `apps/local-api` module map without business engines  
3. **Typed IPC surface** — desktop ↔ local-api hello path beyond identity bridge  
4. **Workspace path bootstrap** — create `userData/workspaces/default` layout (still empty DB)  
5. **Database package wiring prep** — Prisma project stub **or** explicit approval to add schema  
6. **Logging integration** — main-process structured logs via `@marketdna/shared`  
7. **CI hardening** — Electron smoke (optional xvfb)  

## Still out of scope for 1B (unless separately approved)

- Full Prisma domain schema + migrations  
- Market data ingest  
- Quantitative engines (regime, DNA, robustness, recommendation, learning)  
- Python workers  
- Feature UI beyond shell/navigation placeholders  

Await approval before starting Phase 1B.
