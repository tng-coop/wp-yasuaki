#!/usr/bin/env python3
# import_addresses_rest.py
# Usage:
#   export WP_BASE_URL=https://wp.lan
#   export WP_USERNAME=admin
#   export WP_APP_PASSWORD=MivGtw7W7S6z7RTWqJmkbGCu
#   python import_addresses_rest.py              # uses office.csv next to this script
#   python import_addresses_rest.py /path/to/offices.csv

import csv, os, sys, re, requests
from typing import Dict, Optional

CSV_REQUIRED = ["ID","ID2","Office","Address","TEL","FAX","Email","URL","Work"]

WP_BASE_URL = os.environ.get("WP_BASE_URL","").rstrip("/")
WP_USERNAME = os.environ.get("WP_USERNAME","")
WP_APP_PASSWORD = (os.environ.get("WP_APP_PASSWORD","") or "").replace(" ","")
if not (WP_BASE_URL and WP_USERNAME and WP_APP_PASSWORD):
    print("Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD", file=sys.stderr); sys.exit(1)

API = f"{WP_BASE_URL}/wp-json/wp/v2/address"
S = requests.Session()
S.auth = (WP_USERNAME, WP_APP_PASSWORD)
S.headers.update({"Accept":"application/json","Content-Type":"application/json; charset=utf-8"})

def slugify_id(csv_id: str) -> str:
    # stable, deterministic slug tied to CSV ID
    s = re.sub(r"[^a-zA-Z0-9_-]+","-", csv_id.strip())
    s = re.sub(r"-+","-", s).strip("-").lower()
    return f"address-{s}" if s else "address-unnamed"

def fetch_existing_maps() -> tuple[Dict[str,int], Dict[str,int]]:
    """Return (by_csv_id, by_slug) maps for existing posts."""
    by_csv_id: Dict[str,int] = {}
    by_slug: Dict[str,int] = {}
    page = 1
    total = 0
    while True:
        r = S.get(API, params={
            "per_page": 100, "page": page,
            "context": "edit",                # ensure meta is included with permissions
            "_fields": "id,slug,meta"         # keep payload small, include meta
        })
        if r.status_code == 401:
            print("401 Unauthorized: check creds/capabilities", file=sys.stderr); sys.exit(1)
        if r.status_code == 404:
            print("CPT /address not found (is plugin active?)", file=sys.stderr); sys.exit(1)
        r.raise_for_status()
        items = r.json()
        if not items: break
        for p in items:
            pid = int(p["id"])
            total += 1
            slug = (p.get("slug") or "").strip()
            if slug: by_slug[slug] = pid
            meta = p.get("meta") or {}
            cid = (meta.get("csv_id") or "").strip()
            if cid: by_csv_id[cid] = pid
        if len(items) < 100: break
        page += 1
    print(f"[INFO] Preloaded {total} address posts (csv_id:{len(by_csv_id)}, slug:{len(by_slug)})")
    return by_csv_id, by_slug

def upsert(row: Dict[str,str], pid: Optional[int]) -> int:
    payload = {
        "status": "publish",
        "title": row["Office"],
        "meta": {
            "csv_id":  row["ID"],
            "csv_id2": row["ID2"],
            "address": row["Address"],
            "tel":     row["TEL"],
            "fax":     row["FAX"],
            "email":   row["Email"],
            "url":     row["URL"],
            "work":    row["Work"],
        },
        # make slug stable so future runs can find it even if meta lookup fails
        "slug": slugify_id(row["ID"]),
    }
    if pid:
        r = S.post(f"{API}/{pid}", json=payload)
    else:
        r = S.post(API, json=payload)
    r.raise_for_status()
    return int(r.json()["id"])

def main():
    # default to office.csv in the same directory as this script,
    # but allow overriding via a CLI argument
    script_dir = os.path.dirname(os.path.abspath(__file__))
    default_csv = os.path.join(script_dir, "offices.csv")

    if len(sys.argv) >= 2:
        csv_path = sys.argv[1]
    else:
        csv_path = default_csv

    if not os.path.exists(csv_path):
        print(
            f"CSV not found: {csv_path}\n"
            "Tip: place 'offices.csv' next to this script or pass an explicit path.",
            file=sys.stderr
        )
        sys.exit(1)

    # quick probe (also checks auth)
    probe = S.get(API, params={"per_page":1, "context":"edit", "_fields":"id"})
    probe.raise_for_status()

    by_csv_id, by_slug = fetch_existing_maps()

    created = updated = skipped = 0
    with open(csv_path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        missing = [h for h in CSV_REQUIRED if h not in reader.fieldnames]
        if missing:
            print(f"CSV missing headers: {missing}", file=sys.stderr); sys.exit(1)

        for i, raw in enumerate(reader, 1):
            row = {k:(raw.get(k,"") or "").strip() for k in CSV_REQUIRED}
            if not row["ID"] or not row["Office"]:
                print(f"[WARN] Row {i}: missing ID or Office — skipping"); skipped += 1; continue

            # primary: by csv_id; fallback: by expected slug
            pid = by_csv_id.get(row["ID"])
            if not pid:
                pid = by_slug.get(slugify_id(row["ID"]))

            try:
                nid = upsert(row, pid)
                if pid:
                    updated += 1
                    print(f"[OK] Updated {row['ID']} (post_id={nid})")
                else:
                    created += 1
                    print(f"[OK] Created {row['ID']} (post_id={nid})")
                # keep maps up-to-date for subsequent duplicates in same run
                by_csv_id[row["ID"]] = nid
                by_slug[slugify_id(row["ID"])] = nid
            except requests.HTTPError as e:
                print(f"[FAIL] Row {i} {row['ID']}: HTTP {e.response.status_code} -> {e.response.text}", file=sys.stderr)

    print(f"\nDone. Created: {created}, Updated: {updated}, Skipped: {skipped}")

if __name__ == "__main__":
    main()
