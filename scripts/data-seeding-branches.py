#!/usr/bin/env python3
"""
Seed Branch subcategories (children of the 'Branch' category) from offices.csv.

Env (required):
  WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
Optional:
  OFFICES_CSV   (default: seeders/offices.csv)
  ENSURE_ADDRESS=1 to create/update Address if missing (default: 0)
"""
import os, sys, csv, re, requests
from typing import Dict, Tuple, Optional

CSV_REQUIRED = ["ID","ID2","Office","Address","TEL","FAX","Email","URL","Work"]

BASE = (os.environ.get("WP_BASE_URL") or "").rstrip("/")
USER = os.environ.get("WP_USERNAME") or ""
PASS = (os.environ.get("WP_APP_PASSWORD") or "").replace(" ","")
if not (BASE and USER and PASS):
    print("Set WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD", file=sys.stderr); sys.exit(1)

ENSURE_ADDRESS = str(os.environ.get("ENSURE_ADDRESS","0")).lower() in ("1","true","yes","on")

API_ADDR = f"{BASE}/wp-json/wp/v2/address"
API_CAT  = f"{BASE}/wp-json/wp/v2/categories"

S = requests.Session()
S.auth = (USER, PASS)
S.headers.update({"Accept":"application/json","Content-Type":"application/json; charset=utf-8"})

def slugify(s:str)->str:
    s = re.sub(r"[^a-zA-Z0-9_-]+","-", (s or "").strip())
    s = re.sub(r"-+","-", s).strip("-").lower()
    return s

def get_csv_path()->str:
    if len(sys.argv)>1 and sys.argv[1]: return sys.argv[1]
    if os.environ.get("OFFICES_CSV"):   return os.environ["OFFICES_CSV"]
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(here,"offices.csv")

def fetch_all(url, fields)->list:
    out, page = [], 1
    while True:
        r = S.get(url, params={"per_page":100,"page":page,"context":"edit","_fields":fields}, timeout=15)
        if r.status_code == 404:
            print(f"404 at {url} (is endpoint/meta registered?)", file=sys.stderr); sys.exit(1)
        r.raise_for_status()
        items = r.json()
        if not items: break
        out.extend(items)
        if len(items) < 100: break
        page += 1
    return out

def ensure_parent_branch()->int:
    r = S.get(API_CAT, params={"slug":"branch","_fields":"id,slug"}, timeout=10); r.raise_for_status()
    items = r.json()
    if items: return int(items[0]["id"])
    r = S.post(API_CAT, json={"name":"Branch","slug":"branch","parent":0}, timeout=15); r.raise_for_status()
    return int(r.json()["id"])

def load_address_maps()->Tuple[Dict[str,int], Dict[str,int]]:
    by_csv, by_slug = {}, {}
    for a in fetch_all(API_ADDR,"id,slug,meta,title"):
        aid  = int(a["id"])
        slug = (a.get("slug") or "").strip()
        if slug: by_slug[slug] = aid
        meta = a.get("meta") or {}
        cid  = (meta.get("csv_id") or "").strip()
        if cid: by_csv[cid] = aid
    return by_csv, by_slug

def load_branch_cat_maps(parent_id:int)->Tuple[Dict[str,int], Dict[str,int]]:
    by_code, by_slug = {}, {}
    # Get only descendants of the parent to keep scope tight
    cats = fetch_all(API_CAT, "id,slug,parent,meta,name")
    for c in cats:
        if int(c.get("parent") or 0) != parent_id: continue
        tid  = int(c["id"])
        slug = (c.get("slug") or "").strip()
        if slug: by_slug[slug] = tid
        meta = c.get("meta") or {}
        code = (meta.get("csv_id") or "").strip()
        if code: by_code[code] = tid
    return by_code, by_slug

def addr_slug(csv_id:str)->str:   return f"address-{slugify(csv_id)}"
def branch_slug(csv_id:str)->str: return f"branch-{slugify(csv_id)}"

def upsert_address(row:dict, existing_id:Optional[int])->int:
    payload = {
        "status":"publish",
        "title": row["Office"],
        "slug":  addr_slug(row["ID"]),
        "meta": {
            "csv_id":  row["ID"], "csv_id2": row["ID2"], "address": row["Address"],
            "tel":     row["TEL"], "fax":    row["FAX"], "email":   row["Email"],
            "url":     row["URL"], "work":   row["Work"],
        }
    }
    r = S.post(f"{API_ADDR}/{existing_id}" if existing_id else API_ADDR, json=payload, timeout=15)
    r.raise_for_status()
    return int(r.json()["id"])

def upsert_branch_category(code:str, name:str, parent_id:int, address_id:int, existing_id:Optional[int])->int:
    payload = {
        "name":  name or f"Branch {code}",
        "slug":  branch_slug(code),
        "parent": parent_id,
        "meta": {"csv_id": code, "address_post_id": address_id}
    }
    r = S.post(f"{API_CAT}/{existing_id}" if existing_id else API_CAT, json=payload, timeout=15)
    r.raise_for_status()
    return int(r.json()["id"])

def main():
    csv_path = get_csv_path()
    if not os.path.exists(csv_path):
        print(f"CSV not found: {csv_path}", file=sys.stderr); sys.exit(1)

    # quick probes
    S.get(API_ADDR, params={"per_page":1,"_fields":"id"}, timeout=10).raise_for_status()
    S.get(API_CAT,  params={"per_page":1,"_fields":"id"}, timeout=10).raise_for_status()

    parent_id = ensure_parent_branch()
    addr_by_csv, addr_by_slug = load_address_maps()
    br_by_code, br_by_slug   = load_branch_cat_maps(parent_id)

    created_b = updated_b = created_a = skipped = 0

    with open(csv_path, newline="", encoding="utf-8") as f:
        rdr = csv.DictReader(f)
        missing = [h for h in CSV_REQUIRED if h not in rdr.fieldnames]
        if missing:
            print(f"CSV missing headers: {missing}", file=sys.stderr); sys.exit(1)

        for i, raw in enumerate(rdr, 1):
            row = {k:(raw.get(k,"") or "").strip() for k in CSV_REQUIRED}
            if not row["ID"] or not row["Office"]:
                print(f"[WARN] Row {i}: missing ID or Office — skipping"); skipped += 1; continue

            code = row["ID"]
            aid = addr_by_csv.get(code) or addr_by_slug.get(addr_slug(code))
            if not aid and ENSURE_ADDRESS:
                try:
                    aid = upsert_address(row, None)
                    created_a += 1
                    addr_by_csv[code] = aid
                    addr_by_slug[addr_slug(code)] = aid
                    print(f"[OK] Created Address {code} (post_id={aid})")
                except requests.HTTPError as e:
                    print(f"[FAIL] Address {code}: HTTP {e.response.status_code} -> {e.response.text}", file=sys.stderr)
                    continue
            if not aid:
                print(f"[SKIP] {code}: Address not found and ENSURE_ADDRESS=0", file=sys.stderr)
                skipped += 1
                continue

            tid = br_by_code.get(code) or br_by_slug.get(branch_slug(code))
            try:
                nid = upsert_branch_category(code, row["Office"], parent_id, aid, tid)
                if tid:
                    updated_b += 1
                    print(f"[OK] Updated Branch category {code} (term_id={nid}, addr_id={aid})")
                else:
                    created_b += 1
                    print(f"[OK] Created Branch category {code} (term_id={nid}, addr_id={aid})")
                br_by_code[code] = nid
                br_by_slug[branch_slug(code)] = nid
            except requests.HTTPError as e:
                print(f"[FAIL] Branch {code}: HTTP {e.response.status_code} -> {e.response.text}", file=sys.stderr)

    print(f"\nDone. Branch cats created:{created_b}, updated:{updated_b}, "
          f"Addresses created:{created_a}, Skipped:{skipped}")

if __name__ == "__main__":
    main()
