#!/usr/bin/env python3
"""Update the last modified time on .d2s files for specified characters."""

import argparse
import os
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
    default_dir = config.get("save_dir",
        os.path.join(os.path.expanduser("~"), "Saved Games", "Diablo II Resurrected"))

    parser = argparse.ArgumentParser(
        description="Update the last modified time on .d2s files for specified characters.")
    parser.add_argument("characters", nargs="+", help="character names to touch")
    parser.add_argument("-d", "--directory", default=default_dir,
                        help=f"directory containing save files (default: {default_dir})")
    args = parser.parse_args()

    for name in args.characters:
        path = os.path.join(args.directory, f"{name}.d2s")
        if os.path.exists(path):
            os.utime(path, None)
            mtime = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(os.path.getmtime(path)))
            print(f"Brought forth {name} by touching {path}")
            print("You may have to quit the game and relaunch for the game to notice")
        else:
            print(f"File not found: {path}")
