"""Rebuild repo/pluginmaster.json + repo/UmadLab/latest.zip from a packaged plugin zip.

Usage:
    python make_repo.py --base-url https://raw.githubusercontent.com/<user>/<repo>/master
    python make_repo.py --base-url https://cdn.jsdelivr.net/gh/<user>/<repo>@master

The base URL is where repo/ itself will be served from. Downloads are expected at
<base-url>/UmadLab/latest.zip.
"""

import argparse
import hashlib
import io
import json
import os
import shutil
import time

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_ZIP = r"D:\download\UmadLab-0.1.0.0-local-20261126.zip"

PLUGIN = {
    "Author": "lieng",
    "Name": "UmadLab",
    "Punchline": "绝妖星练习实验室：P1–P5 完整串联、国服野队打法。",
    "Description": (
        "AnoMech 的分支版本（AGPL-3.0，原作者 Anomek）。针对绝妖星做了 P1–P5 场景串联，"
        "补充 AnoMech 所有未收录的机制，使 AI 队友采取国服野队走位，可在串联器配置倒计时，自动存档/读档。"
        " Spawns fake party members and bosses client-side so you can practice raid mechanics without a full party."
    ),
    "InternalName": "UmadLab",
    "AssemblyVersion": "0.1.0.0",
    "RepoUrl": "https://github.com/<user>/UmadLab",
    "ApplicableVersion": "any",
    "Tags": ["umad", "ultimate", "practice", "simulator", "kefka"],
    "DalamudApiLevel": 15,
    "IsHide": False,
    "IsTestingExclusive": False,
    "DownloadCount": 0,
}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base-url", required=True, help="https URL that serves the repo/ directory")
    ap.add_argument("--zip", default=DEFAULT_ZIP, help="packaged plugin zip to publish as latest.zip")
    ap.add_argument("--min-dalamud", default="15.0.3.4")
    args = ap.parse_args()

    payload_dir = os.path.join(HERE, "UmadLab")
    os.makedirs(payload_dir, exist_ok=True)
    latest = os.path.join(payload_dir, "latest.zip")
    shutil.copy2(args.zip, latest)

    with open(latest, "rb") as fh:
        digest = hashlib.sha256(fh.read()).hexdigest().upper()

    download = args.base_url.rstrip("/") + "/UmadLab/latest.zip"

    entry = dict(PLUGIN)
    entry["MinimumDalamudVersion"] = args.min_dalamud
    entry["DownloadLinkInstall"] = download
    entry["DownloadLinkUpdate"] = download
    entry["DownloadLinkTesting"] = download
    entry["LastUpdate"] = int(time.time())

    master = os.path.join(HERE, "pluginmaster.json")
    io.open(master, "w", encoding="utf-8").write(
        json.dumps([entry], ensure_ascii=False, indent=2) + "\n"
    )

    print("latest.zip :", latest, "{:,} B".format(os.path.getsize(latest)))
    print("sha256     :", digest)
    print("download   :", download)
    print("pluginmaster.json written:", master)
    print()
    print("仓库地址（交给用户填进 /xlsettings → 第三方仓库）:")
    print("   ", args.base_url.rstrip("/") + "/pluginmaster.json")


if __name__ == "__main__":
    main()
