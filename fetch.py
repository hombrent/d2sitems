#!/usr/bin/env python3
"""Update the last modified time on .d2s files for specified characters."""

import argparse
import glob
import os
import shutil
import time

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
        description="Update the last modified time on .d2s files for specified characters.")
    parser.add_argument("characters", nargs="+", help="character names to touch")
    args = parser.parse_args()

    for name in args.characters:
        path = os.path.join(save_dir, f"{name}.d2s")
        if os.path.exists(path):
            os.utime(path, None)
            mtime = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(os.path.getmtime(path)))
            print(f"Brought forth {name} by touching {path}")
            print("You may have to quit the game and relaunch for the game to notice")
        elif mule_dir and os.path.isdir(mule_dir):
            mule_files = glob.glob(os.path.join(mule_dir, f"{name}.*"))
            if mule_files:
                print(f"Found {name} in mule directory, moving to save directory...")
                for src in mule_files:
                    dst = os.path.join(save_dir, os.path.basename(src))
                    shutil.move(src, dst)
                    print(f"  Moved {os.path.basename(src)}")
                d2s_path = os.path.join(save_dir, f"{name}.d2s")
                if os.path.exists(d2s_path):
                    os.utime(d2s_path, None)
                print(f"Brought forth {name} from mule directory")
                print("You may have to quit the game and relaunch for the game to notice")
            else:
                print(f"File not found: {path}")
                print(f"  Also not found in mule directory: {mule_dir}")
        else:
            print(f"File not found: {path}")
