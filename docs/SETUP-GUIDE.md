# CI/CD Pipeline Setup Guide

**Quick-start for developers and DevOps teams**

---

## For Developers

### Prerequisites

- .NET 9.0 SDK installed
- Git configured with your name and email
- GitHub CLI (`gh`) installed

### Understanding the Pipeline

When you push to `development`:

```
Your commit
    ↓
GitHub Actions triggered
    ↓
Build job (creates SBOM, signs artifacts)
    ↓
Deploy job (deploys to dev-bidirectional in Azure)
    ↓
Smoke test validates deployment
    ↓
Audit event logged
```

**You don't need to do anything.** The pipeline runs automatically.

### Local Testing

Test your build locally before pushing:

```bash
# Restore and build
dotnet restore
dotnet build --configuration Release

# Run tests
dotnet test

# Check what will be built
dotnet publish --configuration Release --output ./publish

# Verify SBOM generation (optional)
dotnet tool install --global CycloneDX
dotnet CycloneDX . --output ./artifacts/sbom --json
cat artifacts/sbom/bom.json | jq .
```

### Monitoring Deployments

Watch your deployment in GitHub:

```bash
# View workflow runs
gh run list --repo abishkar123/todo-gitactions --workflow cd-bidirectional.yml

# Follow a specific run
gh run view <run-id> --log

# Check deployment status
gh run view <run-id> --json status,conclusion
```

### Troubleshooting

If the pipeline fails:

1. **Check the workflow log:**
   ```bash
   gh run view <run-id> --log
   ```

2. **Common failures:**
   - `SBOM missing` → Dependencies weren't restored before publishing
   - `Health check failed` → App Service didn't start or `/health` endpoint is down
   - `Policy non-compliant` → Azure resources violate assigned policies

3. **Ask for help:**
   - Slack: `#devops-pipelines`
   - Docs: `docs/CI-CD-PIPELINE.md`

---

## For DevOps/Cloud Engineers

### Initial Setup (One-time)

#### 1. Create GitHub Environments

In **Settings → Environments**, create:

```
dev-bidirectional
stage-bidirectional  (for future)
prod-bidirectional   (for future)
```

#### 2. Add Repository Variables

In **Settings → Secrets and variables → Variables**, add:

```
AZURE_TENANT_ID              = Your Azure tenant ID
AZURE_SUBSCRIPTION_ID_DEV    = Dev subscription ID
AZURE_CLIENT_ID_DEV          = Will get from step 3
```

Example:
```
AZURE_TENANT_ID              = a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d
AZURE_SUBSCRIPTION_ID_DEV    = 12345678-1234-1234-1234-123456789012
AZURE_CLIENT_ID_DEV          = 87654321-4321-4321-4321-210987654321
```

#### 3. Set Up OIDC Workload Identity Federation

Create workload identity in Azure:

```bash
#!/bin/bash
set -e

RESOURCE_GROUP="rg-bidirectional-dev-app"
IDENTITY_NAME="github-bidirectional-dev-deploy"
SUBSCRIPTION_ID="<your-subscription-id>"
TENANT_ID="<your-tenant-id>"

# Create resource group (if not exists)
az group create \
  --name "$RESOURCE_GROUP" \
  --location "australiaeast"

# Create managed identity
az identity create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME"

# Get client ID
CLIENT_ID=$(az identity show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$IDENTITY_NAME" \
  --query clientId -o tsv)

echo "✓ Client ID: $CLIENT_ID"
echo "  Add this to GitHub variable: AZURE_CLIENT_ID_DEV"

# Federate with GitHub OIDC
az identity federated-identity-credential create \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME" \
  --name github-dev \
  --issuer https://token.actions.githubusercontent.com \
  --subject repo:abishkar123/todo-gitactions:environment:dev-bidirectional

echo "✓ OIDC federation created"

# Assign Website Contributor role
az role assignment create \
  --assignee "$CLIENT_ID" \
  --role "Website Contributor" \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP

echo "✓ Website Contributor role assigned"

# Assign Key Vault Secrets User role (optional, if using secrets)
# az role assignment create \
#   --assignee "$CLIENT_ID" \
#   --role "Key Vault Secrets User" \
#   --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP

echo ""
echo "Setup complete! Add to GitHub variables:"
echo "  AZURE_TENANT_ID = $TENANT_ID"
echo "  AZURE_SUBSCRIPTION_ID_DEV = $SUBSCRIPTION_ID"
echo "  AZURE_CLIENT_ID_DEV = $CLIENT_ID"
```

Save as `setup-oidc.sh` and run:
```bash
chmod +x setup-oidc.sh
./setup-oidc.sh
```

#### 4. Create Azure App Service

```bash
#!/bin/bash
set -e

RESOURCE_GROUP="rg-bidirectional-dev-app"
APP_SERVICE_PLAN="asp-bidirectional-dev"
APP_SERVICE_NAME="app-bidirectional-dev-api"
LOCATION="australiaeast"

# Create App Service plan
az appservice plan create \
  --name "$APP_SERVICE_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --sku B1 \
  --is-linux

# Create App Service
az webapp create \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_SERVICE_PLAN" \
  --runtime "DOTNETCORE|9.0"

# Enable health check endpoint (optional but recommended)
az webapp config set \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --health-check-path /health

echo "✓ App Service created: $APP_SERVICE_NAME"
echo "  URL: https://$APP_SERVICE_NAME.azurewebsites.net"
```

#### 5. Verify Setup

```bash
# Test OIDC login
az login \
  --service-principal \
  --username <CLIENT_ID> \
  --tenant <TENANT_ID> \
  --federated-token <GITHUB_TOKEN>

# Verify workload identity
az identity federated-identity-credential list \
  --resource-group "$RESOURCE_GROUP" \
  --identity-name "$IDENTITY_NAME"

# Check role assignments
az role assignment list \
  --assignee <CLIENT_ID> \
  --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP>
```

### Monitoring & Troubleshooting

#### View Workflow Runs

```bash
# List recent runs
gh run list \
  --repo abishkar123/todo-gitactions \
  --workflow cd-bidirectional.yml \
  --limit 10

# View detailed logs
gh run view <run-id> --log

# Check job status
gh run view <run-id> --json jobs
```

#### Check Azure Policies

```bash
# List policy assignments
az policy assignment list \
  --resource-group rg-bidirectional-dev-app

# Check compliance status
az policy state list \
  --resource-group rg-bidirectional-dev-app \
  --query "[].{Resource:resourceId, State:complianceState}"

# List non-compliant resources
az policy state list \
  --resource-group rg-bidirectional-dev-app \
  --filter "complianceState eq 'NonCompliant'"
```

#### Check App Service Logs

```bash
# Stream logs
az webapp log tail \
  --name app-bidirectional-dev-api \
  --resource-group rg-bidirectional-dev-app

# Download logs
az webapp log download \
  --name app-bidirectional-dev-api \
  --resource-group rg-bidirectional-dev-app \
  --log-file ./app-logs.zip
```

#### Verify SBOM & Provenance

```bash
# Download artifacts from GitHub
gh run download <run-id> --name app-bidirectional-<sha> --dir ./artifacts

# Check SBOM
jq . artifacts/artifacts/sbom/bom.json | head -50

# Verify signatures (requires cosign)
cosign verify-blob \
  --certificate-identity "https://github.com/abishkar123/todo-gitactions/.github/workflows/cd-bidirectional.yml@refs/heads/development" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  --signature artifacts/sbom/bom.json.sig \
  artifacts/sbom/bom.json

# Check provenance
jq . artifacts/provenance/slsa-provenance.json
```

### Scaling to Prod

When ready for production:

1. **Create prod environment and variables** (same pattern as dev)
2. **Set up prod Azure resources:**
   ```bash
   # Same scripts as dev, replace "dev" with "prod"
   RESOURCE_GROUP="rg-bidirectional-prod-app"
   ```

3. **Update workflows:** Edit `cd-bidirectional.yml` to add `deploy-stage` and `deploy-prod` jobs

4. **Configure approval gates:** Set GitHub Environment protection rules

5. **Enable blue-green deployment:** Uncomment slot-swap logic in prod job

---

## For Security/Compliance

### Audit Trail Access

All deployments are logged to immutable storage:

```bash
# List deployment events
az storage blob list \
  --account-name stbidirectionalaudit \
  --container-name release-audit \
  --prefix "bidirectional/dev/"

# Download audit event
az storage blob download \
  --account-name stbidirectionalaudit \
  --container-name release-audit \
  --name "bidirectional/dev/<run-id>/deploy-dev-<sha>.json" \
  --file ./audit-event.json
```

### Verify Supply Chain Artifacts

```bash
# Check SBOM signature (trustless verification)
cosign verify-blob \
  --certificate-identity "https://github.com/abishkar123/todo-gitactions/.github/workflows/cd-bidirectional.yml@refs/heads/development" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  --signature bom.json.sig \
  bom.json

# Extract and audit provenance
jq '.predicate.invocation.configSource' slsa-provenance.json

# Verify no cross-tenant artifacts
jq '.subject[].name' slsa-provenance.json | grep -v bidirectional && echo "CROSS-TENANT RISK" || echo "OK"
```

### Policy Compliance Check

```bash
# List all policy assignments
az policy assignment list --query "[].{Name:displayName, Scope:scope}"

# Evaluate current compliance
az policy state list \
  --resource-group rg-bidirectional-dev-app \
  --query "[].{Policy:policyDefinitionId, State:complianceState, Count:count}"
```

---

## Checklist: Pre-Production

- [ ] OIDC workload identity created and federated
- [ ] GitHub variables set (`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID_DEV`, `AZURE_CLIENT_ID_DEV`)
- [ ] GitHub `dev-bidirectional` environment created
- [ ] Azure App Service deployed and responding to `/health`
- [ ] Azure Policy assignments active
- [ ] First CI run successful (SBOM generated, signed, deployed)
- [ ] Audit events logged to immutable storage
- [ ] Team trained on monitoring workflows
- [ ] On-call runbook for deployment failures
- [ ] Backup/restore procedures documented

---

## Support & Documentation

| Need | Resource |
|------|----------|
| Full pipeline details | [`docs/CI-CD-PIPELINE.md`](./CI-CD-PIPELINE.md) |
| Workflow source code | `.github/workflows/cd-bidirectional.yml` |
| Tenant config | `.platform/tenants/dev/bidirectional.yml` |
| GitHub Docs | https://docs.github.com/en/actions |
| Azure CLI Docs | https://learn.microsoft.com/cli/azure/ |
| OIDC Workload Identity | https://learn.microsoft.com/en-us/azure/active-directory/workload-identities/ |
| Cosign Signing | https://docs.sigstore.dev/cosign/ |

---

**Last updated:** June 2026  
**Maintained by:** DevOps Team
