#!/usr/bin/env bash
set -euo pipefail

# Ensure API key is set
if [[ -z "${Pexels__ApiKey:-}" ]]; then
  echo "ERROR: Pexels__ApiKey environment variable is not set."
  exit 1
fi

# List of video IDs to fetch
video_ids=(6394054 30646036)

for id in "${video_ids[@]}"; do
  out_file="pexels-video-${id}.json"
  echo "Fetching video ${id} → ${out_file}"
  curl -s -H "Authorization: ${Pexels__ApiKey}" \
       "https://api.pexels.com/videos/videos/${id}" \
    > "${out_file}"
  echo "  ✔ Saved ${out_file}"
done

