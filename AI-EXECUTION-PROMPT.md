# AI Execution Prompt (Authoritative)

This prompt must be provided verbatim to the AI agent responsible for implementing
the Runner refactor.

---

## Prompt

You have full read/write access to the repository.

**Authoritative documents (read carefully before coding):**
- `Unified-Plugin-Execution-Refactoring-Plan-v2.md`
- `PR-INDEX.md`
- `PR01–PR06` Markdown documents
- `AI-BRANCHING-RULES.md`
- `REFERENCE-USAGE-RULES.md`

These documents are provided together in:
**`Runner-Refactor-AI-Handoff-Package-ServiceClient.zip`**

**Reference projects:**
A separate ZIP containing reference projects is provided **for conceptual inspiration only**.
Follow `REFERENCE-USAGE-RULES.md` strictly.
Do not copy code, identifiers, or structures from the reference projects.

---

## Branching rules (mandatory)
- All work must be done on feature branches created from `develop`
- All PRs must target `develop`
- Never push directly to `develop` or `main`
- Never open PRs against `main`

---

## Execution order (mandatory)
- Execute PRs **strictly in the order defined in `PR-INDEX.md`**
- One PR at a time
- Do not start the next PR until the current PR is merged
- Do not skip, merge, or reorder PRs

---

## Live connectivity rule (CRITICAL)
For **all plugin debugging execution paths** (WebAPI entry executing the unified pipeline and Profiler replay):

- Any mode that touches the live Dataverse environment **must use a full-fidelity `IOrganizationService`
  backed by `Microsoft.PowerPlatform.Dataverse.Client.ServiceClient`**
- **Online:** ServiceClient (live reads + writes, gated by `AllowLiveWrites`)
- **Hybrid:** Hybrid wrapper over ServiceClient (live reads only, cached writes)
- **Offline:** No live connectivity

**Host-side Web API proxy / pass-through routing is out of scope and must remain unchanged.**
Do not attempt to refactor or replace proxy routing logic.

---

## Behavioral rules
- **Internal projects rule:** All new execution logic must be implemented in `DataverseDebugger.Runner`. Changes to `DataverseDebugger.Protocol` and `DataverseDebugger.Runner.Conversion` are allowed **only when required** for backward-compatible DTO/contract additions and necessary mapping/translation updates.
- Preserve existing behavior unless a PR explicitly allows change
- WebAPI entry wiring must not be refactored beyond thin delegation
- `ExecutionMode` is authoritative when provided; legacy `WriteMode` fallback applies only when absent
- Offline must be truly offline
- Hybrid must never write to live Dataverse
- Use `RunnerNotSupportedException` for deterministic NotSupported cases

---

## Testing rules
- Each PR must satisfy its acceptance criteria
- Required unit and integration tests must be added or updated
- If acceptance criteria are not met, stop and fix before proceeding

---

## Output expectations
- Each PR must be minimal and scoped strictly to its PR document
- Use squash merges
- PR titles must follow the naming convention in `AI-BRANCHING-RULES.md`

---

## Priority rule
If there is any conflict between:
- reference projects
- your own assumptions
- any document not listed above

👉 **The Runner documents always win.**
If a conflict, ambiguity, or decision cannot be resolved *unambiguously* using the Runner documents:
- **Stop**
- **Do not guess**
- **Ask the repository owner for clarification before proceeding**


---

Begin with **PR01 only**.

---

_End of document_
