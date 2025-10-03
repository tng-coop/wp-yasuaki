#!/bin/bash

# Check required variables
required_vars=(
  Authentication__GitHub__ClientId 
  Authentication__GitHub__ClientSecret
  Authentication__LINE__ClientId 
  Authentication__LINE__ClientSecret
  Authentication__Google__ClientId 
  Authentication__Google__ClientSecret
  PEXELS_API_KEY
  )

for var in "${required_vars[@]}"; do
  if [ -z "${!var}" ]; then
    echo "❌ Environment variable '$var' is not set in asp.sh!"
    exit 1
  fi
done

# Set GitHub secrets securely
gh secret set AUTH_GITHUB_CLIENT_ID --body "$Authentication__GitHub__ClientId"
gh secret set AUTH_GITHUB_CLIENT_SECRET --body "$Authentication__GitHub__ClientSecret"
gh secret set AUTH_LINE_CLIENT_ID --body "$Authentication__LINE__ClientId"
gh secret set AUTH_LINE_CLIENT_SECRET --body "$Authentication__LINE__ClientSecret"
gh secret set AUTH_GOOGLE_CLIENT_ID --body "$Authentication__Google__ClientId"
gh secret set AUTH_GOOGLE_CLIENT_SECRET --body "$Authentication__Google__ClientSecret"
gh secret set PEXELS_API_KEY --body "$PEXELS_API_KEY"

echo "✅ GitHub Actions secrets set successfully!"
