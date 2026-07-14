# @marketdna/analytics-client

Typed boundary between the desktop/local services and future Python analytics workers.

## Phase 1A scope

- Client interface + stub implementation
- Health/version handshake shapes
- **No Python runtime**
- **No gRPC server**
- **No regime / optimisation / recommendation calls**

Later phases wire gRPC per `docs/architecture/06-api-architecture.md`
and engines in `docs/architecture/16-quantitative-intelligence-architecture.md`.
