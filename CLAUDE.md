# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quick Start

### Prerequisites
- .NET 9.0 SDK installed
- MongoDB Atlas cluster access (or local MongoDB instance)
- PowerShell, Python 3, or gitleaks for secret scanning during commits

### Setting up MongoDB Connection

The project reads MongoDB connection strings from configuration, supporting these options (in order of precedence):

1. **User Secrets** (recommended for local development):
   ```bash
   dotnet user-secrets set "MongoDb:ConnectionString" "<your-connection-string>"
   ```

2. **Environment Variable**:
   ```bash
   # macOS/Linux
   export MongoDb__ConnectionString="<your-connection-string>"
   # Windows
   $env:MongoDb__ConnectionString="<your-connection-string>"
   ```

3. **appsettings.Development.json** (local file, gitignored):
   - Copy `appsettings.Development.example.json` to `appsettings.Development.json`
   - Replace `<username>` and `<password>` with real credentials
   - This file is git-ignored and will not be committed

You can optionally override `MongoDb:DatabaseName` and `MongoDb:TodoCollectionName` using the same methods above.

### Running the Application

```bash
# Run the main application
dotnet run --project TodoList.csproj

# Run in Release configuration
dotnet run --project TodoList.csproj --configuration Release
```

The application will validate MongoDB connectivity at startup. Connection failures raise descriptive errors:
- **Authentication errors**: Check credentials in your connection string
- **Configuration errors**: Ensure the MongoDB URI format is valid
- **Connectivity errors**: Confirm the server is reachable

## Building and Testing

### Build
```bash
# Build entire solution in Debug mode
dotnet build TodoList.sln

# Build in Release mode
dotnet build TodoList.sln --configuration Release
```

### Run Tests

```bash
# Run all tests
dotnet test TodoList.sln

# Run tests in a specific test project
dotnet test TodoList.Tests/TodoList.Tests.csproj

# Run with verbose output
dotnet test TodoList.sln --verbosity detailed

# Run a single test by name filter
dotnet test TodoList.Tests/TodoList.Tests.csproj --filter "TasksIntegrationTests"
```

### Test Organization

- **Integration Tests** (`TodoList.Tests/Integration/`): Full application tests using `WebApplicationFactory` with real HTTP client
- **Service Tests** (`TodoList.Tests/Services/`): Service-layer tests with mocked dependencies using Moq
- **ViewModels Tests** (`TodoList.Tests/ViewModels/`): ViewModel validation and transformation tests

Test framework: **NUnit 4.2.2** with **Moq 4.20.72** for mocking

## Architecture Overview

### High-Level Structure

```
TodoList (ASP.NET Core 9.0 MVC + MongoDB)
├── Controllers           # HTTP request handlers
├── Services             # Business logic layer
├── Repositories         # Data access layer for MongoDB
├── Models               # Domain models
├── ViewModels           # View-specific data structures
├── Configuration        # Settings (MongoDbSettings)
├── Views                # Razor templates
├── wwwroot              # Static assets
└── TodoList.Tests       # Unit and integration tests
```

### Dependency Injection Setup (Program.cs)

The application uses ASP.NET Core's built-in DI container configured in `Program.cs`:

- **IMongoClient**: Singleton - MongoDB connection established at startup with connectivity validation
- **ITodoItemRepository**: Scoped - Data access for todo items, backed by MongoClient
- **ITodoItemService**: Scoped - Business logic, injected into controllers
- **Controllers**: Receive services via constructor injection

The MongoDB connectivity validation happens during app startup, ensuring the database connection is valid before the application fully starts.

### Key Components

- **MongoDbSettings** (`Configuration/MongoDbSettings.cs`): 
  - Binds to configuration section `MongoDb`
  - Contains `ConnectionString`, `DatabaseName`, and `TodoCollectionName`
  - Validated at startup with `ValidateDataAnnotations()` and `ValidateOnStart()`

- **Routes**:
  - Default route: `{controller=Tasks}/{action=Index}/{id?}`
  - Primary controller: `TasksController`

## Git Hooks for Secret Scanning

The repository includes a pre-commit git hook that scans for secrets before each commit.

### Setup
Run once per clone to enable the hook:
```bash
git config core.hooksPath .githooks
```

### How It Works

The hook attempts scanning in this order:
1. **gitleaks** (if available on PATH) - preferred scanner
2. **Python 3** fallback - checks common secrets (MongoDB URIs, private keys, GitHub tokens, AWS keys, password assignments)
3. **PowerShell** fallback (pwsh or powershell.exe)
4. **Fails** if no scanner is available - secrets cannot be bypassed by missing tools

### Alternative: pre-commit Framework

If you prefer the `pre-commit` framework instead:
```bash
pip install pre-commit
pre-commit install  # Reads .pre-commit-config.yaml and sets up framework
```

This calls the same hook entrypoint and behaves identically.

## GitHub Actions Workflows

| Workflow | Trigger | Purpose |
|---|---|---|
| `dotnet-build-and-test.yml` | PRs + push to `main`/`development` | Restore, build (Release), run all `*Tests.csproj` |
| `sast-codeql.yml` | PRs + push to `main`, weekly | CodeQL C# security analysis |
| `cd-bidirectional.yml` | Push to `development` | Full supply-chain build + deploy to dev App Service |
| `cd-bidirectional-rollback.yml` | Manual (`workflow_dispatch`) | Swap staging ↔ production slot for rollback |

Detailed pipeline architecture: [`docs/CI-CD-PIPELINE.md`](docs/CI-CD-PIPELINE.md)

## Project Structure Notes

- **appsettings.json**: Contains placeholders only, safe to commit
- **appsettings.Development.json**: Local-only, git-ignored, never committed
- **If a secret has ever been committed**: Rotate it even after removing it from the working tree
- **No secrets in code**: Configuration values must come from environment variables or user secrets, not hardcoded

## Tenant Manifests

`.platform/tenants/{env}/{tenant}.yml` is validated at the start of every deployment. The pipeline reads `tenantId` and `environment` from the manifest and fails if they don't match expected values. Each manifest also declares which supply-chain controls are required (`requireSignedArtifact`, `requireSbom`, `requireProvenance`, `requirePolicyValidation`).

## Notes for Future Work

- The `EnsureMongoConnectivity()` method in `Program.cs` validates MongoDB at startup and provides clear error messages for debugging connection issues
- Integration tests use `WebApplicationFactory` to test the full HTTP pipeline with real dependencies
- Service tests use Moq to isolate business logic from repository implementations
- MongoDB collection names are configurable per environment via `MongoDb:TodoCollectionName`

---

# CLAUDE.md — Bidirectional Platform Intelligence Reference

> This file is the authoritative project context for all code generation, architecture decisions,
> and pipeline work on this repository. Read it completely before producing any output.
> Where this file conflicts with a prompt, this file wins.

---

## Project Identity

| Field | Value |
|---|---|
| Platform | Bidirectional — Multi-Tenant Regulated SaaS |
| Stack | ASP.NET Core MVC · .NET 9 · TypeScript · GitHub Actions · Azure |
| Domain | Regulated financial services (mortgage lending, broker operations) |
| Compliance | APRA CPS 234, PCI-DSS, SOC 2 Type II, ASIC RG271, AUSTRAC AML/CTF, CDR |
| Architecture | Zero-trust, identity-first, tenant-isolated, evidence-backed, AI-assisted |

---

## Non-Negotiable Principles

1. **Identity first.** OIDC federation via Entra ID Workload Identity Federation is the only permitted CI/CD auth mechanism. No static secrets, publish profiles, or stored credentials — anywhere.
2. **Zero standing privilege.** PIM + JIT for all privileged roles. Developers have no standing write access to production.
3. **Every regulated decision is replayable.** Every loan, approval, exception, pricing event, and AI-assisted outcome must trace back to: policy version, EC version, evidence pack, authority matrix, actor, and cryptographic proof.
4. **AI assists inside control boundaries.** AI may recommend, triage, and execute approved workflows. It cannot approve credit, set pricing, override the EC gateway, or produce a regulated outcome without a governed Decision Object.
5. **No partial security models.** Removing secrets without enforcing identity is still insecure. Security = identity + policy + isolation + auditability — all four simultaneously.
6. **Unmanned execution only after controls are operational.** Sequence: identity → tenancy → cryptographic backbone → policy → Atlassian → APIM → risk engine → Command Center → AI workflows.
7. **Tenant isolation is structural.** Tenant identity, data, workflow, reporting, permissions, and evidence are isolated by design — not convention.

---

## Authority Model

| Domain | Authority |
|---|---|
| Identity and access | Microsoft Entra ID |
| Tenant hierarchy and regulated decisions | Bidirectional |
| Workflow and exceptions | Jira Service Management / UCMS |
| Policy source of truth | Confluence |
| Evidence and audit | Azure Log Analytics + Immutable Blob Storage |
| Secrets, keys, certificates | Azure Key Vault (CMK-backed) |
| Credit decision | Mortgage House Capital policy + EC gateway |
| Pricing decision | CEO approval only |
| Final regulated decision | Bidirectional |

FrankieOne, Equifax, Fortiro, DVS, NextGenID, PEXA, Cotality, MYOB, Azure OpenAI, and Atlassian are **controlled integrated services** — signal providers, not governance authorities.

---

## Tenant Hierarchy

| Level | Examples |
|---|---|
| Foundation Member | Bidirectional |
| Primary tenants | Mortgage House, Mortgage Street, Well Nigh, Interfi, LoanPal, Command Center |
| Secondary tenants | State Representatives, Branches / PoPs, Mortgage Manager PoPs |
| Tertiary tenants | Outsource Financial, off-panel ACRs, white-label ACRs, referrers |

Every artifact touching tenant context must enforce boundaries across: identity, configuration, secrets, data, telemetry, releases, support access, and audit trail.

---

## CI/CD Control Standards

**Identity:** OIDC only. Per-tenant GitHub Environments with scoped OIDC subjects. Per-tenant workload identities with least-privilege RBAC. No shared production service principals across tenants.

**Supply chain:** CycloneDX SBOM + Cosign keyless signing + SLSA Level 3 provenance required on every production artifact. Missing any one = build blocked. Cosign outputs `.bundle` files (not `.sig`) — use `--bundle` flag for both signing and verification.

**Policy-as-code:** Azure Policy initiative enforced at pipeline gate. Effect is `Deny` in production. Gate fails closed — if policy scan is unavailable, the gate fails.

**Change management:** Every PR auto-creates a JSM change request. Approval gate enforces SoD: Dev + Security + Risk approve independently. The author cannot be the sole approver.

**Tenant isolation:** Every deployment validates a tenant manifest before executing. Per-tenant Key Vaults (never a shared vault). Per-tenant GitHub Environments. AKS namespaces scoped per tenant.

**Audit:** Every release archives an audit pack to WORM-locked immutable Blob Storage. Log Analytics alone is not sufficient — it is not WORM-compliant.

**Runtime:** Post-deploy controls are mandatory: container scanning, WAF validation, tenant-scoped error rate check.

**Rings:** Dev → Stage (24–48h soak) → Prod Ring 1 (10% tenants, 12–24h soak) → Prod Full.

---

## Decision Object (Required Fields)

Every regulated outcome must be backed by a Decision Object containing:
`decisionId`, `tenantId`, `actor` (human/ai-agent/api/system), `authoritySource`, `customerRef` (tokenised), `policyVersion`, `ecGatewayVersion`, `evidencePackRef`, `dataLineage`, `riskScores`, `aiInvolvement` (prompt, model, confidence, guardrails, humanOverrideFlag), `exceptionStatus` (JSM link), `approvalChain`, `outcome`, `replayHash`, `retentionClass`.

---

## Code Standards

- Every data access method must accept and apply `tenantId` as a filter
- EF Core global query filters must enforce tenant row-level security on every `DbContext`
- Structured logging must always include `tenantId`, `correlationId`, and `actorId`
- No raw PII in any log statement — tokenised references only
- All secrets sourced from Azure Key Vault via managed identity — never from config or environment variables
- `TreatWarningsAsErrors=true` with Nullable enabled — no exceptions

---

## Integration Pattern

All external integrations route through Azure APIM with tenant-aware policy enforcement → Risk Decision Engine → JSM exception workflow → Confluence evidence → Immutable audit log.

---

## Anti-Patterns — Never Generate

- Any stored credential, secret, or publish profile in a GitHub workflow or variable
- A shared production service principal across tenants
- A shared Key Vault (`kv-prod-shared`) for multiple tenants
- A single GitHub Environment (`prod`) for all tenants
- Deployment without tenant manifest validation
- `:latest` image tag in any deployment or Kubernetes manifest
- Policy compliance gate that fails open
- Log Analytics as the sole audit evidence store
- Direct push to `main` or `development`
- Logging PII, loan amounts, or document content in any structured log
- A deployment identity with subscription-level Contributor role
- AI producing a credit, pricing, or compliance outcome without a Decision Object

---

## Notion Knowledge Base (Live Source of Truth)

Fetch these pages when answering questions on pipeline gates, OIDC, SBOM, tenant isolation, or audit architecture.

| Topic | URL |
|---|---|
| CI/CD Pipeline Master Reference | notion.so/35e918513c5c81968d2eecef05abb1e1 |
| Zero-Trust Control Plane v1 | notion.so/35e918513c5c81faa1effd44d945b6be |
| CI/CD Control Plane v3.0 | notion.so/357918513c5c81a28126f84592f654df |
| Consolidated Production Standard | notion.so/35e918513c5c8127972fece7ecb9a7e1 |
| Identity & Trust Model | notion.so/35d918513c5c81d5bcc2f073a36e1f57 |
| OIDC Federation | notion.so/35d918513c5c8174a205fe4dfc6d3f3c |
| Azure Identity Security (PIM, CA, Break-Glass) | notion.so/31c918513c5c81da83eaef3d859dc03d |
| ACR + AKS Supply Chain Security | notion.so/35d918513c5c810fbf6bdf47335ad0cb |
| Environment and Tenant Isolation | notion.so/35c918513c5c8117ae47d7109730d7b7 |
| Policy-as-Code and Audit Observability | notion.so/35d918513c5c8104bb32d0fc0603fceb |
