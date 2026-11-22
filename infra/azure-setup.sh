#!/usr/bin/env bash
# One-time setup of Mingle in Azure. Safe to run again: existing resources are kept.
#
# Creates, in Germany West Central:
#   - resource group mingle-rg
#   - Container Apps environment mingle-env (consumption plan, no log workspace)
#   - container app mingle-app: one copy at most (rooms live in memory), scales to zero when idle
#   - a sign-in for GitHub Actions (OIDC, no stored password) with Contributor on mingle-rg only,
#     and the AZURE_* variables plus the "production" environment in the GitHub repository
#
# Needs: `az login` and `gh auth login` done, and the image ghcr.io/bmbul/mingle:latest public.
# The database connection string is NOT handled here; set it afterwards (see README, "Deploying").
set -euo pipefail

RG="${RG:-mingle-rg}"
LOCATION="${LOCATION:-germanywestcentral}"
ENVIRONMENT="${ENVIRONMENT:-mingle-env}"
APP="${APP:-mingle-app}"
IMAGE="${IMAGE:-ghcr.io/bmbul/mingle:latest}"
REPO="${REPO:-Bmbul/Mingle}"
DEPLOYER="${DEPLOYER:-mingle-github-deploy}"

echo "==> Azure CLI extension and resource providers"
az extension add --name containerapp --upgrade --only-show-errors
az provider register --namespace Microsoft.App --wait
az provider register --namespace Microsoft.OperationalInsights --wait

echo "==> Resource group $RG in $LOCATION"
az group create --name "$RG" --location "$LOCATION" --output none

echo "==> Container Apps environment $ENVIRONMENT"
if ! az containerapp env show --name "$ENVIRONMENT" --resource-group "$RG" --output none 2>/dev/null; then
  az containerapp env create --name "$ENVIRONMENT" --resource-group "$RG" --location "$LOCATION" \
    --logs-destination none --output none
fi

echo "==> Container app $APP"
if ! az containerapp show --name "$APP" --resource-group "$RG" --output none 2>/dev/null; then
  # The connection string starts as a placeholder; the app cannot start until it is replaced.
  az containerapp create --name "$APP" --resource-group "$RG" --environment "$ENVIRONMENT" \
    --image "$IMAGE" --target-port 8080 --ingress external \
    --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi \
    --secrets db-connection=not-set-yet \
    --env-vars "ConnectionStrings__Mingle=secretref:db-connection" \
    --output none
fi

echo "==> Sign-in for GitHub Actions ($DEPLOYER)"
APP_ID="$(az ad app list --display-name "$DEPLOYER" --query "[0].appId" -o tsv)"
if [ -z "$APP_ID" ]; then
  APP_ID="$(az ad app create --display-name "$DEPLOYER" --query appId -o tsv)"
fi
az ad sp show --id "$APP_ID" --output none 2>/dev/null || az ad sp create --id "$APP_ID" --output none

# GitHub identifies a workflow either by names ("repo:owner/repo:...") or, for newer setups, by ids
# ("repo:owner@<owner id>/repo@<repo id>:..."). Trust both, so the deploy works whichever GitHub sends.
OWNER_ID="$(gh api "users/${REPO%%/*}" --jq .id)"
REPO_ID="$(gh api "repos/$REPO" --jq .id)"
trust_subject() {
  local name="$1" subject="$2"
  if [ -z "$(az ad app federated-credential list --id "$APP_ID" --query "[?subject=='$subject'].name" -o tsv)" ]; then
    az ad app federated-credential create --id "$APP_ID" --parameters "{
      \"name\": \"$name\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"$subject\",
      \"audiences\": [\"api://AzureADTokenExchange\"]
    }" --output none
  fi
}
trust_subject github-production "repo:${REPO}:environment:production"
trust_subject github-production-ids "repo:${REPO%%/*}@${OWNER_ID}/${REPO##*/}@${REPO_ID}:environment:production"

RG_ID="$(az group show --name "$RG" --query id -o tsv)"
az role assignment create --assignee "$APP_ID" --role Contributor --scope "$RG_ID" --output none 2>/dev/null || true

echo "==> GitHub: production environment and AZURE_* variables in $REPO"
gh api --method PUT "repos/$REPO/environments/production" --silent
gh variable set AZURE_CLIENT_ID --repo "$REPO" --body "$APP_ID"
gh variable set AZURE_TENANT_ID --repo "$REPO" --body "$(az account show --query tenantId -o tsv)"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$(az account show --query id -o tsv)"

FQDN="$(az containerapp show --name "$APP" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv)"
echo
echo "Done. The app will live at: https://$FQDN"
echo "Next: set the database connection string (README, \"Deploying\")."
