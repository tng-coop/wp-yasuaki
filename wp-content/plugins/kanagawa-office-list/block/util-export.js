// import_offices.mjs
import Papa from "https://esm.sh/papaparse@5.5.3";
import { readFile } from "node:fs/promises";

const BASE_URL = process.env.BASE_URL || "https://yasuaki.com";
const JWT = process.env.JWT || "";
if (!JWT) {
  console.error("Missing JWT. Run: JWT=... node --experimental-network-imports import_offices.mjs");
  process.exit(1);
}

async function apiGet(path) {
  const r = await fetch(`${BASE_URL}${path}`, { headers: { Authorization: `Bearer ${JWT}` } });
  if (!r.ok) throw new Error(`GET ${path} -> ${r.status} ${await r.text()}`);
  return r.json();
}
async function apiPost(path, body) {
  const r = await fetch(`${BASE_URL}${path}`, {
    method: "POST",
    headers: { Authorization: `Bearer ${JWT}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!r.ok) throw new Error(`POST ${path} -> ${r.status} ${await r.text()}`);
  return r.json();
}

/** Find-or-create by exact title on your CPT (rest_base: /wp-json/wp/v2/offices) */
async function upsertOfficeByTitle(title) {
  const q = new URLSearchParams({ search: title, per_page: "50", context: "edit" }).toString();
  const arr = await apiGet(`/wp-json/wp/v2/offices?${q}`);
  const exact = Array.isArray(arr)
    ? arr.find(p => (p?.title?.raw ?? p?.title?.rendered) === title || p?.title?.rendered === title)
    : null;
  if (exact?.id) return exact.id;
  const created = await apiPost(`/wp-json/wp/v2/offices`, { title, status: "publish" });
  return created.id;
}

/** Parse CSV as raw rows and repair any row with too many/few fields */
function parseCsvRepairingTail(csvText) {
  const headParse = Papa.parse(csvText, { header: false, preview: 1, skipEmptyLines: "greedy" });
  if (headParse.errors?.length) {
    throw new Error("Failed to read header row: " + JSON.stringify(headParse.errors, null, 2));
  }
  const headers = headParse.data?.[0] ?? [];
  if (!headers.length) throw new Error("CSV has no header row");

  const all = Papa.parse(csvText, { header: false, skipEmptyLines: "greedy" });
  const targetLen = headers.length;
  const rows = [];
  const repairs = [];

  for (let i = 1; i < all.data.length; i++) {
    let row = all.data[i];
    if (!Array.isArray(row) || row.length === 0) continue;

    if (row.length > targetLen) {
      const mergedLast = row.slice(targetLen - 1).join(","); // merge overflow into last column
      row = [...row.slice(0, targetLen - 1), mergedLast];
      repairs.push({ line: i + 1, type: "TooManyFields" });
    } else if (row.length < targetLen) {
      row = [...row, ...Array(targetLen - row.length).fill("")];
      repairs.push({ line: i + 1, type: "TooFewFields" });
    }

    const obj = Object.fromEntries(headers.map((h, idx) => [h, row[idx] ?? ""]));
    rows.push(obj);
  }

  return { headers, rows, repairs };
}

async function main() {
  const csvPath = new URL("./offices.csv", import.meta.url);
  const csv = await readFile(csvPath, "utf8");

  const { rows, repairs } = parseCsvRepairingTail(csv);
  if (repairs.length) {
    const sample = repairs.slice(0, 5).map(r => `line ${r.line}: ${r.type}`).join("\n");
    console.warn(
      `Note: repaired ${repairs.length} row(s) with header mismatches.\n` +
      (sample ? `Examples:\n${sample}\n` : "") +
      `Tip: wrap comma-containing cells in quotes in the CSV source.`
    );
  }

  let ok = 0;
  for (const row of rows) {
    const title = (row["Office"] || "").trim();
    if (!title) continue; // must have a title for the CPT

    const postId = await upsertOfficeByTitle(title);

    // Straight export: keep CSV columns as-is inside "data"
    const data = {
      ID: row["ID"] ?? "",
      ID2: row["ID2"] ?? "",
      Office: row["Office"] ?? "",
      Address: row["Address"] ?? "",
      TEL: row["TEL"] ?? "",
      FAX: row["FAX"] ?? "",
      Email: row["Email"] ?? "",
      URL: row["URL"] ?? "",
      Work: row["Work"] ?? "",
    };

    const res = await apiPost(`/wp-json/office/v1/post/${postId}`, { data });
    console.log(`Upserted #${postId}: ${title}`);
    console.log(JSON.stringify(res, null, 2));
    ok++;
  }

  console.log(`Done. Imported/updated ${ok} office(s).`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});

