"""Build an offline ZIP from pinned public releases. Python 3, standard library only.

Run from any directory. Produces ignored artifacts; never reads installed configs,
saves, generated bindings or plugins. Does not build, deploy, launch or publish.
"""
import hashlib
import json
import shutil
import urllib.request
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CACHE = REPO / "downloads" / "friend-installer"
OUT = REPO / "artifacts" / "friend-installer"
STAGE = OUT / "Chained-Beasts-Mod-Installer"
GAME_SHA = "72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b"
BEP_COMMIT = "b782da4ac54e1072d2857f4a2f77c80c3ef08005"
PRODUCTS = [
    ("BepInEx.zip", "https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791%2Bb782da4.zip",
     "bb1c510909b69522733791f3a0817a487379a52da6468a492f1c9c19b3ec6f9a", None),
    ("ChainedBeasts.ModFoundation.dll", "https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.8.3/ChainedBeasts.ModFoundation.dll",
     "ef6d3c4a04508f65dab732c66fa865c584151446536d2e83044e2d7f2370ecce", "BepInEx/plugins/ChainedBeasts.ModFoundation.dll"),
    ("TermsAndContusions.dll", "https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.8.3/TermsAndContusions.dll",
     "bb958bc7660cf3128c376eba59b0c498ca6edf6815344f4b2db1d3946cac263a", "BepInEx/plugins/TermsAndContusions.dll"),
    ("ChainedBeasts.BattleNumbers.dll", "https://github.com/Krrrrrrk/Chained-Beasts_Battle-Numbers/releases/download/v0.1.1/ChainedBeasts.BattleNumbers.dll",
     "0e36714efb13fa5f92918539a873cec1cd23c4ee99ae33346e747ff6e0c07bb5", "BepInEx/plugins/BattleNumbers/ChainedBeasts.BattleNumbers.dll"),
]
UNITY_URL = "https://unity.bepinex.dev/libraries/6000.3.21.zip"
UNITY_SHA = "d836329482e33ec5f63a842aa1967cef467a75952b81b7d3d08aa0e7542ee67f"
NOTICES = {
    "BepInEx-LICENSE.txt": f"https://raw.githubusercontent.com/BepInEx/BepInEx/{BEP_COMMIT}/LICENSE",
    "BepInEx-source.zip": f"https://codeload.github.com/BepInEx/BepInEx/zip/{BEP_COMMIT}",
    "BattleNumbers-LICENSE.txt": "https://raw.githubusercontent.com/Krrrrrrk/Chained-Beasts_Battle-Numbers/91a362abefa5f5332b82aa0be8e7ca2251a229fd/LICENSE",
    "DotNet-LICENSE.txt": "https://raw.githubusercontent.com/dotnet/runtime/v6.0.7/LICENSE.TXT",
    "DotNet-THIRD-PARTY-NOTICES.txt": "https://raw.githubusercontent.com/dotnet/runtime/v6.0.7/THIRD-PARTY-NOTICES.TXT",
    "Doorstop-LICENSE.txt": "https://raw.githubusercontent.com/NeighTools/UnityDoorstop/v4.6.0/LICENSE",
    "Il2CppInterop-source.zip": "https://codeload.github.com/BepInEx/Il2CppInterop/zip/refs/tags/v1.5.3",
    "HarmonyX-LICENSE.txt": "https://raw.githubusercontent.com/BepInEx/HarmonyX/v2.10.2/LICENSE",
    "MonoMod-LICENSE.txt": "https://raw.githubusercontent.com/MonoMod/MonoMod/v22.07.31.01/LICENSE",
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fetch(name, url, expected=None):
    path = CACHE / name
    original_loader = REPO / "downloads" / "BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791+b782da4.zip"
    if name == "BepInEx.zip" and not path.exists() and original_loader.exists():
        shutil.copy2(original_loader, path)
    if not path.exists():
        with urllib.request.urlopen(url, timeout=90) as response:
            path.write_bytes(response.read())
    if expected and sha(path) != expected:
        raise ValueError(f"SHA-256 mismatch; no bundle made: {name}")
    return path


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    # A new folder only: do not retain stray payload files from a previous build.
    if STAGE.exists():
        raise SystemExit(f"Output already exists. Preserve/rename it before rebuilding: {STAGE}")
    (STAGE / "payload").mkdir(parents=True)
    (STAGE / "licenses").mkdir()
    for name in ("INSTALL.bat", "Install.ps1", "InstallerCore.ps1", "GameDiscovery.ps1", "UNINSTALL.bat", "Uninstall.ps1", "UninstallerCore.ps1", "START HERE.txt"):
        # CRLF for cmd.exe and Windows PowerShell 5.1, including the wrapper.
        text = (REPO / "installer" / name).read_text(encoding="utf-8")
        (STAGE / name).write_bytes(text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))
    payloads, files, sources = [], [], []
    for name, url, expected, target in PRODUCTS:
        source = fetch(name, url, expected)
        shutil.copy2(source, STAGE / "payload" / name)
        payloads.append(dict(path=f"payload/{name}", sha256=expected))
        sources.append(f"{name}\n{url}\nSHA-256: {expected}\n")
        if target:
            files.append(dict(path=target, kind="plugin", payload=f"payload/{name}", sha256=expected))
    with zipfile.ZipFile(STAGE / "payload" / "BepInEx.zip") as archive:
        for entry in archive.infolist():
            path = entry.filename.replace("\\", "/")
            if entry.is_dir():
                continue
            if path.startswith("/") or ":" in path or any(x in (".", "..") for x in path.split("/")):
                raise ValueError(f"Unsafe archive member: {path}")
            files.append(dict(path=path, kind="loader", sha256=hashlib.sha256(archive.read(entry)).hexdigest()))
    supplied = OUT / "BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791+b782da4"
    if supplied.exists():
        expected = {f["path"]: f["sha256"] for f in files if f["kind"] == "loader"}
        actual = {p.relative_to(supplied).as_posix(): sha(p) for p in supplied.rglob("*") if p.is_file()}
        if actual != expected:
            raise ValueError("Supplied BepInEx folder differs from the pinned official distribution; do not bundle local configs or generated game files.")
        sources.append("Supplied extracted loader folder verified: all 228 files exactly match the unmodified bundled official archive.\n")
    unity = CACHE / "6000.3.21.zip"
    # This is the official reference-library ZIP, never generated game interop.
    cached_unity = REPO / "downloads" / "6000.3.21.zip"
    if not unity.exists() and cached_unity.exists():
        shutil.copy2(cached_unity, unity)
    source = fetch("6000.3.21.zip", UNITY_URL, UNITY_SHA)
    shutil.copy2(source, STAGE / "payload" / "6000.3.21.zip")
    payloads.append(dict(path="payload/6000.3.21.zip", sha256=UNITY_SHA))
    files.append(dict(path="BepInEx/unity-libs/6000.3.21.zip", kind="offline-cache", optional=True,
                      payload="payload/6000.3.21.zip", sha256=UNITY_SHA))
    sources.append(f"Unity reference libraries 6000.3.21\n{UNITY_URL}\nSHA-256: {UNITY_SHA}\n")
    config = STAGE / "payload" / "BepInEx-offline.cfg"
    config.write_bytes((REPO / "installer" / "BepInEx-offline.cfg").read_bytes())
    payloads.append(dict(path="payload/BepInEx-offline.cfg", sha256=sha(config)))
    files.append(dict(path="BepInEx/config/BepInEx.cfg", kind="config", optional=True,
                      payload="payload/BepInEx-offline.cfg", sha256=sha(config)))
    for name, url in NOTICES.items():
        source = fetch(name, url)
        shutil.copy2(source, STAGE / "licenses" / name)
        sources.append(f"{name}\n{url}\nSHA-256: {sha(source)}\n")
    shutil.copy2(REPO / "LICENSE", STAGE / "licenses" / "ModFoundation-and-TermsAndContusions-LICENSE.txt")
    # Preserve the complete original distribution. Additional license/source archives
    # travel with it; neither the archive nor its individual binaries are modified.
    (STAGE / "SOURCES.txt").write_text(
        "Unmodified third-party loader and published plugins. No game files.\n"
        "BepInEx / Il2CppInterop are LGPL libraries, independently replaceable.\n"
        "Their source archives and licenses are included under licenses.\n"
        "Dependencies retain their upstream licenses and copyright.\n"
        "The additional Unity reference-library ZIP is from unity.bepinex.dev. No installed game DLLs or generated game bindings are copied.\n"
        "Official installation guide: https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html\n\n"
        + "\n".join(sources), encoding="utf-8")
    manifest = dict(schemaVersion=1, version="TC-0.8.3_BattleNumbers-0.1.1_BepInEx-791",
                    gameAssemblySha256=GAME_SHA, pluginNames=["ChainedBeasts.ModFoundation", "TermsAndContusions", "ChainedBeasts.BattleNumbers"],
                    payloads=payloads, files=sorted(files, key=lambda f: f["path"].lower()))
    (STAGE / "bundle.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    sums = [f"{sha(p)}  {p.relative_to(STAGE).as_posix()}" for p in sorted(STAGE.rglob("*")) if p.is_file()]
    (STAGE / "SHA256SUMS.txt").write_text("\n".join(sums) + "\n", encoding="utf-8")
    output = OUT / "Chained-Beasts-Offline-Installer-TC-0.8.3-BattleNumbers-0.1.1.zip"
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(STAGE.rglob("*")):
            if path.is_file():
                archive.write(path, f"{STAGE.name}/{path.relative_to(STAGE).as_posix()}")
    (OUT / (output.name + ".sha256")).write_text(f"{sha(output)}  {output.name}\n", encoding="ascii")
    print(f"{output}\n{output.stat().st_size:,} bytes\nSHA-256 {sha(output)}")


if __name__ == "__main__":
    main()
