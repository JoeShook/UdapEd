#!/usr/bin/env bash
# One-time setup so .github/workflows/deploy-gcp.yml can deploy to Cloud Run without a stored key.
#
# Creates a Workload Identity Federation pool and provider that trust GitHub Actions tokens only
# from JoeShook/UdapEd running in its "production" environment, and a deploy service account that
# those tokens can act as. Run it once, signed in to gcloud as a project owner. It is safe to
# re-run: steps whose resource already exists report that and carry on.
#
# Afterwards set the two repository variables it prints (Settings > Secrets and variables >
# Actions > Variables), or let the last step set them with the gh CLI.

set -uo pipefail

PROJECT="${PROJECT:-udap-idp}"
REPO="${REPO:-JoeShook/UdapEd}"
POOL="${POOL:-github}"
PROVIDER="${PROVIDER:-udaped}"
SA_NAME="${SA_NAME:-github-udaped-deploy}"
SA="$SA_NAME@$PROJECT.iam.gserviceaccount.com"

PROJECT_NUMBER=$(gcloud projects describe "$PROJECT" --format='value(projectNumber)') || exit 1

gcloud services enable iamcredentials.googleapis.com sts.googleapis.com --project "$PROJECT"

gcloud iam workload-identity-pools create "$POOL" \
  --project "$PROJECT" --location global --display-name "GitHub Actions"

gcloud iam workload-identity-pools providers create-oidc "$PROVIDER" \
  --project "$PROJECT" --location global --workload-identity-pool "$POOL" \
  --display-name "UdapEd deploys" \
  --issuer-uri "https://token.actions.githubusercontent.com" \
  --attribute-mapping "google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.environment=assertion.environment" \
  --attribute-condition "assertion.repository == '$REPO' && assertion.environment == 'production'"

gcloud iam service-accounts create "$SA_NAME" \
  --project "$PROJECT" --display-name "GitHub Actions: deploy UdapEd"

# Tokens from the repo (already narrowed to the production environment above) may act as the deploy account.
gcloud iam service-accounts add-iam-policy-binding "$SA" --project "$PROJECT" \
  --role roles/iam.workloadIdentityUser \
  --member "principalSet://iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/$POOL/attribute.repository/$REPO"

# Start builds, read the service and stream build logs.
for role in roles/cloudbuild.builds.editor roles/viewer roles/serviceusage.serviceUsageConsumer; do
  gcloud projects add-iam-policy-binding "$PROJECT" --member "serviceAccount:$SA" --role "$role" --condition None >/dev/null \
    && echo "Granted $role"
done

# gcloud builds submit uploads the source to the project's Cloud Build bucket.
gcloud storage buckets add-iam-policy-binding "gs://${PROJECT}_cloudbuild" \
  --member "serviceAccount:$SA" --role roles/storage.objectAdmin >/dev/null \
  && echo "Granted objectAdmin on gs://${PROJECT}_cloudbuild"

# The build itself runs as Cloud Build's service account (which already deploys today); the deploy
# account must be allowed to start builds that run as it.
BUILD_SA=$(gcloud builds get-default-service-account --project "$PROJECT" --format='value(serviceAccountEmail)')
BUILD_SA="${BUILD_SA##*/}"
gcloud iam service-accounts add-iam-policy-binding "$BUILD_SA" --project "$PROJECT" \
  --member "serviceAccount:$SA" --role roles/iam.serviceAccountUser >/dev/null \
  && echo "Deploy account may run builds as $BUILD_SA"

WIF_PROVIDER="projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/$POOL/providers/$PROVIDER"
echo
echo "Repository variables for $REPO:"
echo "  GCP_WIF_PROVIDER=$WIF_PROVIDER"
echo "  GCP_DEPLOY_SA=$SA"
echo
echo "Or with the gh CLI:"
echo "  gh variable set GCP_WIF_PROVIDER --repo $REPO --body \"$WIF_PROVIDER\""
echo "  gh variable set GCP_DEPLOY_SA --repo $REPO --body \"$SA\""
