#!/usr/bin/env python3
"""Move character files to the mule directory for inactive storage."""

import argparse
import glob
import os
import shutil

def load_config(filename="d2sitems.conf"):
    """Load config from file next to this script or in the current directory."""
    config = {}
    candidates = [
        os.path.join(os.path.dirname(os.path.abspath(__file__)), filename),
        os.path.join(os.getcwd(), filename),
    ]
    for path in candidates:
        if not os.path.exists(path):
            continue
        with open(path) as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue
                if '=' not in line:
                    continue
                key, _, value = line.partition('=')
                key, value = key.strip(), value.strip()
                if key and value:
                    if value.startswith("~"):
                        value = os.path.expanduser("~") + value[1:]
                    config[key] = value
        break
    return config

if __name__ == "__main__":
    config = load_config()
    save_dir = config.get("save_dir",
        os.path.join(os.path.expanduser("~"), "Saved Games", "Diablo II Resurrected"))
    mule_dir = config.get("mule_dir")

    parser = argparse.ArgumentParser(
        description="Move character files to the mule directory for inactive storage.")
    parser.add_argument("characters", nargs="+", help="character names to mule")
    args = parser.parse_args()

    if not mule_dir:
        print("Error: mule_dir is not configured in d2sitems.conf")
        exit(1)

    if not os.path.isdir(mule_dir):
        os.makedirs(mule_dir)
        print(f"Created mule directory: {mule_dir}")

    for name in args.characters:
        files = glob.glob(os.path.join(save_dir, f"{name}.*"))
        if not files:
            print(f"No files found for {name} in {save_dir}")
            continue
        print(f"Muling {name}...")
        for src in files:
            dst = os.path.join(mule_dir, os.path.basename(src))
            shutil.move(src, dst)
            print(f"  Moved {os.path.basename(src)}")
        print(f"{name} is now muled")
