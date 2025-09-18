#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Project Reporter (Python)
- .NET solutions & projects (rich metadata)
- Playwright project detection LIMITED to an e2e dir (default: ./e2e)
- Zero heavy actions: no restore/test runs
"""
from __future__ import annotations

import argparse, json, os, re, sys, time, fnmatch
from pathlib import Path
from typing import Dict, List, Any, Optional
from xml.etree import ElementTree as ET

# ---------- config ----------
EXCLUDE_DIRS = {".git", "bin", "obj", "node_modules", "TestResults", ".cache"}
EXCLUDE_DIR_PREFIXES = ("playwright-report",)
PLAYWRIGHT_CONFIG_GLOBS = ("playwright.config.*",)
SPEC_GLOBS = ("*.spec.ts", "*.spec.tsx", "*.spec.js", "*.spec.jsx")

# ---------- cli ----------
def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(description="Report solutions, .NET projects, and Playwright projects.")
    ap.add_argument("--root", default=".", help="Repo root (default: .)")
    ap.add_argument("--e2e-dir", default="e2e", help="Playwright project directory (default: e2e)")
    ap.add_argument("--plain", action="store_true", help="Disable ANSI colors")
    ap.add_argument("--json", action="store_true", help="Emit JSON summary after text report")
    return ap.parse_args()

# ---------- colors ----------
class P:
    def __init__(self, plain: bool):
        if plain or not sys.stdout.isatty():
            self.B = ""
            self.D = ""
            self.R = ""
            self.BU = ""
            self.G = ""
            self.Y = ""
            self.M = ""
            self.C = ""
        else:
            self.B  = "\033[1m"
            self.D  = "\033[2m"
            self.R  = "\033[0m"
            self.BU = "\033[38;5;33m"
            self.G  = "\033[32m"
            self.Y  = "\033[33m"
            self.M  = "\033[35m"
            self.C  = "\033[36m"

def hr(p:P):  print(f"{p.D}"+"─"*78+f"{p.R}")
def sec(t,p): print(f"\n{p.B}{t}{p.R}"); hr(p)
def sub(t,p): print(f"{p.C}{t}{p.R}")
def note(t,p):print(f"{p.D}{t}{p.R}")

# ---------- fs scan ----------
def walk_files(root: Path) -> List[Path]:
    out: List[Path] = []
    root = root.resolve()
    for dirpath, dirnames, filenames in os.walk(root):
        for d in list(dirnames):
            if d in EXCLUDE_DIRS or any(d.startswith(pref) for pref in EXCLUDE_DIR_PREFIXES):
                dirnames.remove(d)
        for f in filenames:
            out.append(Path(dirpath)/f)
    return out

# ---------- .NET parsing ----------
def _all(root: ET.Element, tag:str): return [n for n in root.iter() if n.tag.split('}')[-1]==tag]
def _first(root: ET.Element, tag:str)->Optional[str]:
    for n in _all(root,tag):
        if n.text and n.text.strip(): return n.text.strip()
    return None

def parse_csproj(path: Path) -> Dict[str,Any]:
    data={"path":str(path.as_posix()),"sdk":None,"tfms":[],"outputType":None,"nullable":None,
          "treatWarningsAsErrors":None,"packageReferences":[],"projectReferences":[],
          "isTestProject":False,"testCounts":{"testFileCount":0,"factCount":0,"nunitTestAttrCount":0,"theoryCount":0}}
    try:
        root=ET.parse(path).getroot()
        data["sdk"]=root.attrib.get("Sdk")
        tfm=_first(root,"TargetFramework"); tfms=_first(root,"TargetFrameworks")
        data["tfms"]=[tfm] if tfm else ([t.strip() for t in tfms.split(";") if t.strip()] if tfms else [])
        data["outputType"]=_first(root,"OutputType")
        data["nullable"]=_first(root,"Nullable")
        data["treatWarningsAsErrors"]=_first(root,"TreatWarningsAsErrors")
        pkgs=[]; projs=[]
        for n in _all(root,"PackageReference"):
            inc=n.attrib.get("Include") or n.attrib.get("Update"); ver=n.attrib.get("Version")
            if inc: pkgs.append({"id":inc,"version":ver})
        for n in _all(root,"ProjectReference"):
            inc=n.attrib.get("Include")
            if inc: projs.append(inc.replace("\\","/"))
        data["packageReferences"]=pkgs; data["projectReferences"]=projs
        test_pkgs={"xunit","nunit","mstest.testframework","mstest","microsoft.net.test.sdk"}
        name=path.name.lower()
        data["isTestProject"]= name.endswith(".tests.csproj") or any((p["id"] or "").lower() in test_pkgs for p in pkgs)
        if data["isTestProject"]:
            data["testCounts"]=count_cs_tests(path.parent)
    except Exception as ex:
        data["parseError"]=str(ex)
    return data

def count_cs_tests(dir: Path)->Dict[str,int]:
    test_files=fact=theory=nunit=0
    rxF=re.compile(r"\[\s*Fact\s*\]",re.IGNORECASE)
    rxT=re.compile(r"\[\s*Theory\s*\]",re.IGNORECASE)
    rxN=re.compile(r"\[\s*Test\s*\]",re.IGNORECASE)
    for dp, dn, fn in os.walk(dir):
        if any(part in EXCLUDE_DIRS for part in Path(dp).parts): continue
        if any(Path(dp).name.startswith(pref) for pref in EXCLUDE_DIR_PREFIXES): continue
        for f in fn:
            if not f.endswith(".cs"): continue
            if f.endswith("Tests.cs") or f.endswith(".Tests.cs"): test_files+=1
            try: s=(Path(dp)/f).read_text(encoding="utf-8",errors="ignore")
            except: continue
            fact += len(rxF.findall(s)); theory += len(rxT.findall(s)); nunit += len(rxN.findall(s))
    return {"testFileCount":test_files,"factCount":fact,"nunitTestAttrCount":nunit,"theoryCount":theory}

# ---------- Playwright detection (restricted to e2e dir) ----------
def detect_playwright(root: Path, e2e_dir_name: str)->List[Dict[str,Any]]:
    e2e_root = root.joinpath(e2e_dir_name).resolve()
    if not e2e_root.exists(): return []
    # require playwright.config.* in e2e root
    has_config = any(e2e_root.glob(pat) for pat in PLAYWRIGHT_CONFIG_GLOBS)
    if not has_config: return []
    pkg = e2e_root.joinpath("package.json")
    data={"path":str(e2e_root.as_posix()),"package":None,"version":None,"scripts":{},
          "specCount":0,"hasPlaywrightReport":False,"hasTestResults":False}
    if pkg.exists():
        try:
            obj=json.loads(pkg.read_text(encoding="utf-8"))
            data["package"]=obj.get("name"); data["version"]=obj.get("version")
            scripts = obj.get("scripts") or {}
            keys=[k for k in scripts.keys() if any(x in k.lower() for x in ("test","playwright","e2e"))]
            data["scripts"]={k:scripts[k] for k in keys}
        except Exception as ex:
            data["parseError"]=f"package.json: {ex}"
    # count specs under e2e/
    spec=0; has_pw=False; has_tr=False
    for dp, dn, fn in os.walk(e2e_root):
        if "node_modules" in dn: dn.remove("node_modules")
        if any(part in EXCLUDE_DIRS for part in Path(dp).parts): continue
        name=Path(dp).name
        if name.startswith("playwright-report"): has_pw=True
        if name=="test-results": has_tr=True
        for f in fn:
            if any(fnmatch.fnmatch(f, pat) for pat in SPEC_GLOBS): spec+=1
    data["specCount"]=spec; data["hasPlaywrightReport"]=has_pw; data["hasTestResults"]=has_tr
    return [data]

# ---------- misc ----------
def classify_bucket(path: Path)->str:
    lp=str(path.as_posix()).lower(); base=path.name.lower()
    if "/e2e/" in lp or base.endswith("e2e.csproj"): return "e2e"
    if "/tests/" in lp or "/test/" in lp or base.endswith(".tests.csproj"): return "tests"
    return "src"
def has_cpm(root: Path)->bool: return root.joinpath("Directory.Packages.props").exists()

# ---------- render ----------
def print_report(root: Path, slns: List[Path], details: List[Dict[str,Any]], pw: List[Dict[str,Any]], p:P, emit_json:bool):
    now=time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime())
    print(f"Project Report  {now}"); hr(p)

    sec("Solutions", p)
    if slns: 
        for s in sorted(slns, key=lambda x: str(x)): print(f"{p.BU}•{p.R} {s.as_posix()}")
    else: note("No .sln files found.", p)

    buckets={"src":[], "tests":[], "e2e":[]}
    for d in details:
        buckets[ classify_bucket(Path(d["path"])) ].append(d)

    sec("Summary", p)
    print(f"{p.M}{'src':<10}{p.R} {len(buckets['src'])} project(s)")
    print(f"{p.G}{'tests':<10}{p.R} {len(buckets['tests'])} project(s)")
    print(f"{p.Y}{'e2e':<10}{p.R} {len(buckets['e2e'])} project(s)")
    print(f"{p.C}{'playwright':<10}{p.R} {len(pw)} project(s)")
    print(f"{p.D}CPM (Directory.Packages.props): {'yes' if has_cpm(root) else 'no'}{p.R}")

    sec("Projects by Group", p)
    for name, color in (("src",p.M),("tests",p.G),("e2e",p.Y)):
        print(f"{color}[ {name} ]{p.R}")
        if buckets[name]:
            for d in buckets[name]: print(f"  {d['path']}")
        else: note("  (none)", p)

    sec(".NET Project Details", p)
    if not details: note("No .csproj files found.", p)
    else:
        for d in sorted(details, key=lambda x: x["path"]):
            sub(d["path"], p)
            print(f"  SDK:           {d.get('sdk') or '-'}")
            print(f"  TFMs:          {', '.join(d.get('tfms') or []) or '-'}")
            print(f"  OutputType:    {d.get('outputType') or '-'}")
            print(f"  Nullable:      {d.get('nullable') or '-'}")
            print(f"  WarningsAsErr: {d.get('treatWarningsAsErrors') or '-'}")
            pkgs=d.get("packageReferences") or []; projs=d.get("projectReferences") or []
            print(f"  Packages:      {len(pkgs)}", end="")
            if pkgs:
                sample=", ".join([pkg['id'] for pkg in pkgs[:5]])
                print(f"  [{sample}{'…' if len(pkgs)>5 else ''}]")
            else: print()
            print(f"  References:    {len(projs)}", end="")
            if projs:
                sample=", ".join(projs[:3])
                print(f"  [{sample}{'…' if len(projs)>3 else ''}]")
            else: print()
            if d.get("isTestProject"):
                tc=d.get("testCounts") or {}
                print(f"  Tests:         files={tc.get('testFileCount',0)}  [Fact]={tc.get('factCount',0)}  [Theory]={tc.get('theoryCount',0)}  [Test]={tc.get('nunitTestAttrCount',0)}")
            if d.get("parseError"): print(f"  ParseError:    {d['parseError']}")

    sec("Playwright Projects", p)
    if not pw: note("No Playwright projects detected under the e2e/ directory.", p)
    else:
        for x in pw:
            sub(x["path"], p)
            print(f"  package:       {x.get('package') or '-'}")
            print(f"  version:       {x.get('version') or '-'}")
            scripts=x.get("scripts") or {}
            if scripts:
                print("  scripts:")
                for k,v in scripts.items(): print(f"    - {k}: {v}")
            else: print("  scripts:       -")
            print(f"  spec files:    {x.get('specCount',0)}")
            print(f"  artifacts:     playwright-report={'yes' if x.get('hasPlaywrightReport') else 'no'}, test-results={'yes' if x.get('hasTestResults') else 'no'}")
            if x.get("parseError"): print(f"  ParseError:    {x['parseError']}")

    if emit_json:
        sec("JSON", p)
        print(json.dumps({
            "root": str(root.as_posix()),
            "solutions": [s.as_posix() for s in slns],
            "dotnet": details,
            "playwright": pw
        }, indent=2))

# ---------- main ----------
def main():
    ns=parse_args()
    root=Path(ns.root).resolve()
    pal=P(plain=ns.plain)
    all_files=walk_files(root)
    slns=[p for p in all_files if p.suffix==".sln"]
    csprojs=[p for p in all_files if p.suffix==".csproj"]
    details=[parse_csproj(p) for p in csprojs]
    pw=detect_playwright(root, ns.e2e_dir)
    print_report(root, slns, details, pw, pal, emit_json=ns.json)

if __name__=="__main__":
    main()
