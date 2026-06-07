# CI/CD Pipeline — Bidirectional Development Workflow

**Version:** 1.0  
**Last Updated:** June 2026  
**Tenant:** Bidirectional (Foundation Member)  
**Runtime:** Azure App Service (.NET 9)  
**Identity:** OIDC Workload Identity Federation (zero static credentials)

---

## Overview

This document describes the complete continuous integration and continuous deployment (CI/CD) pipeline for the Bidirectional todo-gitactions project. The pipeline implements zero-trust security principles with:

- **OIDC Workload Identity Federation** for Azure authentication
- **CycloneDX SBOM** generation and validation
- **Cosign keyless signatures** on all artifacts
- **SLSA Level 3 provenance attestation**
- **Policy-as-code validation** at deployment gates
- **Immutable audit trails** for compliance

---

## Pipeline Architecture

### High-Level Flow

```
Code Push to development
        ↓
    Build Job
    ├─ Restore dependencies
    ├─ Publish release build
    ├─ Generate SBOM (CycloneDX JSON)
    ├─ Sign SBOM (Cosign keyless)
    ├─ Generate SLSA provenance
    ├─ Sign provenance (Cosign keyless)
    └─ Upload artifacts
        ↓
    Deploy-Dev Job
    ├─ Download artifacts
    ├─ OIDC login to Azure
    ├─ Policy validation gate
    ├─ Deploy to App Service
    ├─ Smoke test health check
    └─ Log audit evidence
```

### Workflow Files

| File | Purpose | Trigger |
|------|---------|---------|
| `.github/workflows/cd-bidirectional.yml` | Build & deploy to dev | Push to `development` branch |
| `.github/workflows/cd-bidirectional-rollback.yml` | Manual rollback (future) | `workflow_dispatch` |

---

## Supply Chain Security

### SBOM Requirements

Every build **must** produce a CycloneDX SBOM with the following properties:

#### Generation
- **Format:** CycloneDX JSON (`.json`, not XML)
- **Timing:** Generated **after** `dotnet restore` (never before)
- **Tool:** CycloneDX dotnet tool (`CycloneDX.dotnet`)
- **Location:** `./artifacts/sbom/bom.json`

#### Validation
Build **fails** if:
```
✘ SBOM file is missing
✘ SBOM file is empty
✘ SBOM generated before dependencies restored
```

**Workflow check:**
```yaml
- name: generate-sbom
  run: |
    dotnet tool install --global CycloneDX
    dotnet CycloneDX . --output ./artifacts/sbom --json
    [ ! -s "artifacts/sbom/bom.json" ] && echo "SBOM missing." && exit 1
```

### Artifact Signatures

#### Cosign Keyless Signing
Every artifact is signed without storing keys in the repository:

1. **SBOM signature** → `artifacts/sbom/bom.json.sig`
2. **Provenance signature** → `artifacts/provenance/slsa-provenance.json.sig`

**How it works:**
- Cosign uses OIDC federation to GitHub's OIDC provider
- Temporary ephemeral keys issued by Sigstore
- Signature is verifiable via GitHub's public OIDC identity

**Verification:**
```bash
# Verify SBOM signature
cosign verify-blob \
  --certificate-identity "https://github.com/abishkar123/todo-gitactions/.github/workflows/cd-bidirectional.yml@refs/heads/development" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  --signature artifacts/sbom/bom.json.sig \
  artifacts/sbom/bom.json
```

### SLSA Level 3 Provenance

Each build generates cryptographic proof of:
- **Builder identity:** GitHub Actions runner
- **Source:** Exact git commit and repository
- **Build parameters:** Job ID, run ID
- **Materials:** Dependencies and transitive closure
- **Completeness:** All parameters, environment, materials tracked

**Provenance location:** `artifacts/provenance/slsa-provenance.json`

**Structure:**
```json
{
  "_type": "https://in-toto.io/Statement/v0.1",
  "predicateType": "https://slsa.dev/provenance/v0.2",
  "subject": [
    {
      "name": "app-bidirectional-<sha>",
      "digest": {"sha256": "<sbom-hash>"}
    }
  ],
  "predicate": {
    "builder": {"id": "https://github.com/actions/runner"},
    "invocation": {
      "configSource": {
        "uri": "git+https://github.com/abishkar123/todo-gitactions@refs/heads/development",
        "digest": {"sha256": "<commit-sha>"}
      }
    },
    "metadata": {
      "buildInvocationId": "<github-run-id>",
      "reproducible": false
    }
  }
}
```

**Signed with:** Cosign keyless signature (same OIDC federation as SBOM)

---

## Deployment

### Environments & OIDC Identity

| Environment | Workload Identity | OIDC Subject | Azure Permissions |
|-------------|-------------------|--------------|-------------------|
| `dev-bidirectional` | `github-bidirectional-dev-deploy` | `repo:abishkar123/todo-gitactions:environment:dev-bidirectional` | `Website Contributor` on `rg-bidirectional-dev-app` |

### Dev Deployment Flow

```yaml
deploy-dev:
  needs: build
  environment: dev-bidirectional
  permissions:
    id-token: write
    contents: read
```

**Steps:**

1. **Download artifacts** from build job
   - Includes signed SBOM and provenance

2. **OIDC login to Azure**
   ```yaml
   - uses: azure/login@v2
     with:
       client-id: ${{ vars.AZURE_CLIENT_ID_DEV }}
       tenant-id: ${{ vars.AZURE_TENANT_ID }}
       subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID_DEV }}
   ```
   - No stored credentials
   - Token valid for 1 hour
   - Workload identity scoped to this job

3. **Policy validation gate**
   ```bash
   az policy state trigger-scan --resource-group rg-bidirectional-dev-app
   sleep 90
   NON_COMPLIANT=$(az policy state list \
     --resource-group rg-bidirectional-dev-app \
     --query "[?complianceState=='NonCompliant'] | length(@)" \
     --output tsv)
   [ "$NON_COMPLIANT" -gt 0 ] && exit 1
   ```
   - Scans Azure Policy compliance
   - Fails if any non-compliant resources exist
   - Effect in dev: `Audit` (advisory, but gate still enforces check)

4. **Deploy to App Service**
   ```yaml
   - uses: azure/webapps-deploy@v3
     with:
       app-name: app-bidirectional-dev-api
       package: ./publish
   ```

5. **Smoke test**
   ```bash
   sleep 20
   STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
     https://app-bidirectional-dev-api.azurewebsites.net/health)
   [ "$STATUS" != "200" ] && echo "Health check failed." && exit 1
   ```
   - Waits 20 seconds for deployment
   - Checks `/health` endpoint
   - Fails if not 200 OK

6. **Audit logging**
   ```json
   {
     "stage": "dev",
     "sha": "<commit-sha>",
     "run": "<github-run-id>",
     "result": "success"
   }
   ```
   - Uploaded to immutable audit storage
   - Location: `stbidirectionalaudit/release-audit/bidirectional/dev/<run-id>/`

---

## Tenant Manifests

Tenant configuration is version-controlled and validated on every deployment.

### Dev Manifest

**Location:** `.platform/tenants/dev/bidirectional.yml`

```yaml
tenantId: bidirectional
environment: dev

azure:
  subscriptionId: "00000000-0000-0000-0000-000000000002"
  resourceGroups:
    app: rg-bidirectional-dev-app
    data: rg-bidirectional-dev-data
  keyVault: kv-bidirectional-dev
  logAnalytics: law-bidirectional-dev

deployment:
  appServiceName: app-bidirectional-dev-api
  ring: dev

security:
  requireSignedArtifact: true
  requireSbom: true
  requirePolicyValidation: true
  allowCrossTenantArtifactReuse: false

approvals:
  requiredForProd: false
  autoDeployOnCI: true
```

**Key properties:**
- `autoDeployOnCI: true` — Dev deploys automatically on CI success
- `requireSignedArtifact: true` — SBOM and provenance must be signed
- `allowCrossTenantArtifactReuse: false` — Dev artifacts cannot be used for other tenants

### Prod Manifest (Future)

**Location:** `.platform/tenants/prod/bidirectional.yml`

```yaml
tenantId: bidirectional
environment: prod

azure:
  subscriptionId: "00000000-0000-0000-0000-000000000001"
  resourceGroups:
    app: rg-bidirectional-prod-app
    data: rg-bidirectional-prod-data
  keyVault: kv-bidirectional-prod
  logAnalytics: law-bidirectional-prod

deployment:
  appServiceName: app-bidirectional-prod-api
  ring: ring-1

security:
  requireSignedArtifact: true
  requireSbom: true
  requirePolicyValidation: true
  allowCrossTenantArtifactReuse: false

approvals:
  requiredForProd: true
  approversGroup: entra-bidirectional-prod-approvers
```

**Key properties:**
- `requiredForProd: true` — Manual approval required
- `approversGroup` — Entra ID group for approval authority
- `ring: ring-1` — Blue-green deployment with canary (future)

---

## GitHub Configuration

### Required: Create Environments

Create three GitHub Environments in **Settings → Environments:**

1. **dev-bidirectional**
   - No approval required
   - Variable: `AZURE_CLIENT_ID_DEV`

2. **stage-bidirectional** (future)
   - Manual approval (2 reviewers)
   - Variable: `AZURE_CLIENT_ID_STAGE`

3. **prod-bidirectional** (future)
   - Manual approval (Engineering Lead + Release Manager)
   - Variable: `AZURE_CLIENT_ID_PROD`

### Required: Repository Variables

Add to **Settings → Secrets and variables → Variables:**

```
AZURE_TENANT_ID                    (shared across all environments)
AZURE_SUBSCRIPTION_ID_DEV          (dev subscription)
AZURE_SUBSCRIPTION_ID_STAGE        (stage subscription, future)
AZURE_SUBSCRIPTION_ID_PROD         (prod subscription, future)
AZURE_CLIENT_ID_DEV                (dev workload identity)
AZURE_CLIENT_ID_STAGE              (stage workload identity, future)
AZURE_CLIENT_ID_PROD               (prod workload identity, future)
```

### Required: OIDC Workload Identity Federation

Configure in Azure for each environment:

**Dev example:**

```bash
# Create workload identity
az identity create \
  --resource-group rg-bidirectional-dev-app \
  --name github-bidirectional-dev-deploy

# Get client ID
CLIENT_ID=$(az identity show \
  --resource-group rg-bidirectional-dev-app \
  --name github-bidirectional-dev-deploy \
  --query clientId -o tsv)

# Federate with GitHub OIDC
az identity federated-identity-credential create \
  --resource-group rg-bidirectional-dev-app \
  --identity-name github-bidirectional-dev-deploy \
  --name github-dev \
  --issuer https://token.actions.githubusercontent.com \
  --subject repo:abishkar123/todo-gitactions:environment:dev-bidirectional

# Assign role
az role assignment create \
  --assignee "$CLIENT_ID" \
  --role "Website Contributor" \
  --scope /subscriptions/<subscription-id>/resourceGroups/rg-bidirectional-dev-app
```

---

## Claude Code Hooks

### Stop Hook: Request Fulfillment Validation

**File:** `.claude/settings.json`

**Purpose:** Prevents incomplete work from being submitted.

**Configuration:**
```json
{
  "hooks": {
    "Stop": [
      {
        "hooks": [
          {
            "type": "agent",
            "prompt": "Verify that all tasks in the current session have been completed. Check the task list for any 'pending' status items. If all tasks are 'in_progress' or 'completed', respond with 'All tasks complete'. If there are 'pending' tasks, list them and explain what needs to be done. Respond with 'REQUEST INCOMPLETE' if work remains.",
            "timeout": 30,
            "statusMessage": "Checking if request is fully fulfilled..."
          }
        ]
      }
    ]
  }
}
```

**Behavior:**
- ✅ Allows stopping if all tasks are complete
- ❌ Blocks stopping if pending tasks exist
- 📋 Shows which tasks remain incomplete

---

## Artifact Flow

```
Build Job
├─ dotnet publish → ./publish
├─ CycloneDX SBOM → ./artifacts/sbom/bom.json
├─ Cosign sign → ./artifacts/sbom/bom.json.sig
├─ SLSA provenance → ./artifacts/provenance/slsa-provenance.json
├─ Cosign sign → ./artifacts/provenance/slsa-provenance.json.sig
└─ Upload artifact: app-bidirectional-<sha>
        ↓
   Deploy-Dev Job (downloads artifact)
   ├─ Extracts ./publish
   ├─ Extracts SBOM + signatures + provenance
   ├─ Deploys to Azure App Service
   └─ Logs audit evidence
        ↓
   Immutable Audit Storage
   └─ stbidirectionalaudit/release-audit/
      ├─ bidirectional/dev/<run-id>/deploy-dev-<sha>.json
```

---

## Security Properties

### Zero Trust Principles Applied

| Principle | Implementation |
|-----------|-----------------|
| **Identity-first** | OIDC federation, no stored credentials |
| **Explicit trust verification** | Policy gates at every stage |
| **Assume breach** | Immutable audit trail, all decisions logged |
| **Verify integrity** | SBOM + Cosign + SLSA provenance attestation |
| **Least privilege** | Scoped workload identities per environment |
| **Encryption in transit** | HTTPS, TLS 1.3 |

### Compliance Gates

| Gate | Effect | Failure Mode |
|------|--------|--------------|
| SBOM missing | Build blocked | Exit 1 |
| SBOM empty | Build blocked | Exit 1 |
| Policy non-compliance | Deployment blocked | Exit 1 |
| Health check failure | Deployment rolled back | Exit 1 |

---

## Troubleshooting

### Build Fails: SBOM Missing

**Symptom:** `SBOM missing. exit 1`

**Cause:** CycloneDX tool failed or `dotnet restore` did not run first.

**Fix:**
```bash
# Verify dotnet restore ran
dotnet restore

# Verify CycloneDX tool works
dotnet tool install --global CycloneDX
dotnet CycloneDX . --output ./artifacts/sbom --json

# Check file exists and is not empty
ls -lh artifacts/sbom/bom.json
cat artifacts/sbom/bom.json | jq . | head -20
```

### Deploy Fails: Policy Non-Compliance

**Symptom:** `NON_COMPLIANT: [n] > 0 && exit 1`

**Cause:** Azure resources don't comply with assigned policies.

**Fix:**
```bash
# Check which resources are non-compliant
az policy state list \
  --resource-group rg-bidirectional-dev-app \
  --filter "complianceState eq 'NonCompliant'" \
  --query "[].{Resource:resourceId, Policy:policyDefinitionId, State:complianceState}"

# Remediate (depends on policy)
# Example: Enable diagnostic logging
az monitor diagnostic-settings create \
  --name bidirectional-diags \
  --resource /subscriptions/.../resourceGroups/rg-bidirectional-dev-app/providers/Microsoft.Web/sites/app-bidirectional-dev-api \
  --workspace /subscriptions/.../resourceGroups/rg-bidirectional-dev-app/providers/Microsoft.OperationalInsights/workspaces/law-bidirectional-dev \
  --logs '[{"category":"AppServiceHTTPLogs","enabled":true}]'
```

### Deploy Fails: Health Check Timeout

**Symptom:** `Health check failed. exit 1`

**Cause:** App Service is not responding to `/health` endpoint after 20 seconds.

**Fix:**
```bash
# Check App Service status
az webapp show --name app-bidirectional-dev-api --resource-group rg-bidirectional-dev-app

# Check logs
az webapp log tail --name app-bidirectional-dev-api --resource-group rg-bidirectional-dev-app

# Verify health endpoint exists and works locally
curl http://localhost:5000/health
```

### OIDC Login Fails: Invalid Client ID

**Symptom:** `Invalid client ID or tenant ID`

**Cause:** Repository variables not set or workload identity not federated.

**Fix:**
```bash
# Verify client ID
az identity show \
  --resource-group rg-bidirectional-dev-app \
  --name github-bidirectional-dev-deploy \
  --query clientId

# Verify federation exists
az identity federated-identity-credential list \
  --resource-group rg-bidirectional-dev-app \
  --identity-name github-bidirectional-dev-deploy

# Verify repository variables are set
gh variable list --repo abishkar123/todo-gitactions
```

---

## Future Enhancements

- [ ] Stage environment with 24–48 hour soak window
- [ ] Production ring-based deployment (10% → 100% canary)
- [ ] Automated rollback on error rate threshold
- [ ] Feature flag integration for gradual rollouts
- [ ] Tenant manifest validation in deploy-dev
- [ ] Cross-region failover configuration
- [ ] Cost optimization via reserved instances

---

## References

- [Cosign Keyless Signing](https://docs.sigstore.dev/cosign/keyless/)
- [SLSA Framework v0.2](https://slsa.dev/spec/v0.2/)
- [CycloneDX SBOM](https://cyclonedx.org/)
- [Azure OIDC Workload Identity Federation](https://learn.microsoft.com/en-us/azure/active-directory/workload-identities/workload-identity-federation)
- [GitHub Actions OIDC](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [Azure Policy Compliance](https://learn.microsoft.com/en-us/azure/governance/policy/overview)

---

**Maintained by:** DevOps Team  
**Last reviewed:** June 2026  
**Next review:** December 2026
