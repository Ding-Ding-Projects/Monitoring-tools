#!/usr/bin/env bash
# Fetches the same pinned, digest-verified Windows dependencies on Linux or macOS,
# for cross-building the Windows app (dotnet build works with EnableWindowsTargeting).
# Usage: ./download-dependencies.sh [--silent]   (SILENT=1 also works)
set -euo pipefail
cd "$(dirname "$0")"
python3 - "$@" <<'PY'
import hashlib, json, os, re, shutil, subprocess, sys, time, urllib.request, zipfile

root = os.getcwd()
deps_dir = os.path.join(root, ".deps")
cache = os.path.join(deps_dir, "cache")
os.makedirs(cache, exist_ok=True)
manifest = json.load(open(os.path.join(root, "deps", "manifest.json")))
failed = False

def say(m): print(f"[dependencies] {m}", flush=True)

def get(url):
    headers = {"User-Agent": "monitoring-tools-dependencies"}
    if os.environ.get("GITHUB_TOKEN"):
        headers["Authorization"] = "Bearer " + os.environ["GITHUB_TOKEN"]
    return urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=120)

for dep in manifest["dependencies"]:
    started = time.time()
    target = os.path.join(deps_dir, dep["target"])
    stamp = os.path.join(target, ".pinned")
    try:
        want = f'{dep["tag"]} {dep["sha256"]}'
        if dep["sha256"] and os.path.exists(stamp) and open(stamp).read().strip() == want:
            say(f'{dep["name"]}: present at {target} (pinned {dep["tag"]}); skipped')
            continue
        rel = json.load(get(f'https://api.github.com/repos/{dep["repository"]}/releases/tags/{dep["tag"]}'))
        assets = [a for a in rel["assets"] if (a["name"] == dep["asset"] if dep["asset"] else re.match(dep["assetPattern"], a["name"]))]
        if not assets:
            raise RuntimeError(f'no asset matching {dep["assetPattern"]} in {dep["repository"]} {dep["tag"]}')
        asset = assets[0]
        archive = os.path.join(cache, asset["name"])
        if not os.path.exists(archive):
            say(f'{dep["name"]}: downloading {asset["name"]} ({asset["size"] / 1048576:.1f} MB)')
            with get(asset["browser_download_url"]) as r, open(archive + ".part", "wb") as f:
                shutil.copyfileobj(r, f)
            os.replace(archive + ".part", archive)
        digest = hashlib.sha256(open(archive, "rb").read()).hexdigest()
        if not dep["sha256"]:
            print(f'PIN {dep["name"]} asset={asset["name"]} sha256={digest}')
            raise RuntimeError("no sha256 recorded in deps/manifest.json; record the value above and run again")
        if digest != dep["sha256"].lower():
            os.remove(archive)
            raise RuntimeError(f'SHA-256 mismatch for {asset["name"]}: expected {dep["sha256"]}, got {digest}')
        shutil.rmtree(target, ignore_errors=True)
        os.makedirs(target)
        if dep.get("copyAs"):
            shutil.copyfile(archive, os.path.join(target, dep["copyAs"]))
            open(stamp, "w").write(want)
            say(f'{dep["name"]}: installed {dep["tag"]} to {target}')
            continue
        if dep.get("extractWith"):
            # A .7z archive or a 7-Zip installer exe (itself a 7z archive): needs a 7-Zip extractor on this machine (7zz, 7z or 7zr). Without one the 7-Zip features (and RAR reading) stay off in the cross-built app.
            opener = next((c for c in ("7zz", "7z", "7zr") if shutil.which(c)), None)
            if not opener:
                say(f'{dep["name"]}: skipped, no 7z, 7zz or 7zr command is installed to open {asset["name"]}; archives with 7-Zip stay off in this build')
                shutil.rmtree(target, ignore_errors=True)
                continue
            tmp = os.path.join(cache, dep["name"] + "-extract")
            shutil.rmtree(tmp, ignore_errors=True)
            subprocess.run([opener, "x", "-o" + tmp, archive, "-y"], check=True, stdout=subprocess.DEVNULL)
            for src, dst in dep["extract"].items():
                shutil.copyfile(os.path.join(tmp, src), os.path.join(target, dst))
            shutil.rmtree(tmp, ignore_errors=True)
            open(stamp, "w").write(want)
            say(f'{dep["name"]}: installed {dep["tag"]} to {target}')
            continue
        with zipfile.ZipFile(archive) as z:
            if "*" in dep["extract"]:
                z.extractall(target)
            else:
                top = z.namelist()[0].split("/")[0]
                for src, dst in dep["extract"].items():
                    if src.endswith("/*"):
                        prefix = f"{top}/{src[:-1]}"
                        for n in z.namelist():
                            if n.startswith(prefix) and not n.endswith("/") and "/" not in n[len(prefix):]:
                                with z.open(n) as s, open(os.path.join(target, n[len(prefix):]), "wb") as d:
                                    shutil.copyfileobj(s, d)
                        continue
                    with z.open(f"{top}/{src}") as s, open(os.path.join(target, dst), "wb") as d:
                        shutil.copyfileobj(s, d)
        open(stamp, "w").write(want)
        say(f'{dep["name"]}: installed {dep["tag"]} to {target} in {time.time() - started:.1f} s')
    except Exception as e:
        print(f'[dependencies] FAILED {dep["name"]} (pin {dep["tag"]}, source {dep["repository"]}): {e}', flush=True)
        failed = True

sys.exit(1 if failed else 0)
PY
