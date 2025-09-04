#!/usr/bin/env python3
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

def require_env(var: str) -> str:
    val = os.getenv(var)
    if val is None or val == "":
        print(f"Environment variable {var} must be set", file=sys.stderr)
        sys.exit(1)
    return val

def run(cmd, *, check=True, capture=False, cwd=None):
    if capture:
        result = subprocess.run(
            cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True
        )
        if check and result.returncode != 0:
            # Stream the captured output, then exit with the same code
            print(result.stdout, end="")
            sys.exit(result.returncode)
        return result
    else:
        result = subprocess.run(cmd, cwd=cwd)
        if check and result.returncode != 0:
            sys.exit(result.returncode)
        return result

def main():
    # === Ensure required environment variables are set ===
    REMOTE_USER = require_env("Server__User")
    REMOTE_HOST = require_env("Server__Host")
    REMOTE_WEBPATH = require_env("Server__RemoteBlazorDir")

    # === Configuration ===
    PROJECT_DIR = Path(__file__).resolve().parent
    PROJECT_FILE = PROJECT_DIR / "BlazorWP.csproj"

    PUBLISH_DIR = (PROJECT_DIR / ".." / "blazor-publish").resolve()
    WWWROOT_DIR = PUBLISH_DIR / "wwwroot"
    DESIRED_BASE = "/blazorapp/"

    # === 1) Clean previous publish ===
    print("→ Cleaning old publish…")
    shutil.rmtree(PUBLISH_DIR, ignore_errors=True)

    # === 2) Restore client-side libraries into wwwroot/libman ===
    print("→ Restoring client-side libraries…")
    run(["libman", "restore"])

    # === 3) Publish the project (implicit NuGet restore/build/pack) ===
    print(f"→ Publishing {PROJECT_FILE} to {PUBLISH_DIR}…")
    # Capture combined stdout+stderr, filter specific WASM warnings/optimizations
    result = run(
        ["dotnet", "publish", str(PROJECT_FILE), "-c", "Release", "-o", str(PUBLISH_DIR)],
        check=False,
        capture=True,
    )

    # Filter out specific lines from the output (like the sed in Bash)
    filtered = []
    pattern = re.compile(r"(WASM0001|WASM0060|WASM0062|Optimizing assemblies for size)")
    for line in result.stdout.splitlines():
        if not pattern.search(line):
            filtered.append(line)
    print("\n".join(filtered))

    if result.returncode != 0:
        sys.exit(result.returncode)

    # === 5) Patch <base> href in the generated index.html ===
    index_html = WWWROOT_DIR / "index.html"
    print(f"→ Patching <base> href in {index_html}…")
    if not index_html.is_file():
        print(f"❌ ERROR: {index_html} not found", file=sys.stderr)
        sys.exit(1)

    html = index_html.read_text(encoding="utf-8")
    # Replace any <base href="..."> or <base href="..." /> with desired base
    new_html, nsubs = re.subn(
        r'<base\s+href="[^"]*"\s*/?>',
        f'<base href="{DESIRED_BASE}" />',
        html,
        flags=re.IGNORECASE,
    )
    if nsubs == 0:
        # If no existing base tag is found, you could choose to insert one.
        # To preserve behavior closest to the Bash version (which expects it),
        # treat this as an error.
        print("❌ ERROR: No <base href=\"…\"> tag found to replace", file=sys.stderr)
        sys.exit(1)
    index_html.write_text(new_html, encoding="utf-8")

    # === 6) Deploy via rsync (quiet mode to reduce verbosity) ===
    print(f"→ Rsyncing to {REMOTE_HOST}:{REMOTE_WEBPATH}…")
    # Ensure trailing slash semantics match the Bash script ("WWWROOT_DIR/" -> contents)
    run([
        "rsync", "-azq", "--delete",
        str(WWWROOT_DIR) + "/",
        f"{REMOTE_USER}@{REMOTE_HOST}:{REMOTE_WEBPATH}/",
    ])

    print("✅ Deployment complete!")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nAborted by user.", file=sys.stderr)
        sys.exit(130)
