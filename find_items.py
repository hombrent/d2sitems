#!/usr/bin/env python3
"""Search for items matching field/pattern combos across all d2sitems JSON files in a directory."""

import argparse
import json
import os
import re

def numeric_match(value, expr):
    """Match a numeric value against an expression like '3', '>=4', '<=2', '>0', '<5', '1-3'."""
    expr = expr.strip()
    m = re.match(r'^(\d+)-(\d+)$', expr)
    if m:
        return int(m.group(1)) <= value <= int(m.group(2))
    m = re.match(r'^(>=|<=|>|<|=|==)?(\d+)$', expr)
    if m:
        op, num = m.group(1) or '==', int(m.group(2))
        if op in ('=', '=='): return value == num
        if op == '>=': return value >= num
        if op == '<=': return value <= num
        if op == '>': return value > num
        if op == '<': return value < num
    return False

def matches_field(item, field, pattern):
    """Check if an item matches the pattern in the specified field.
    pattern is a compiled regex for text fields, or a string for numeric fields."""
    if field == "socketCount":
        return numeric_match(item.get("socketCount", 0), pattern)
    if field == "openSockets":
        return numeric_match(item.get("openSockets", 0), pattern)
    regex = pattern
    if field == "name":
        return regex.search(item.get("name", ""))
    elif field == "baseName":
        return regex.search(item.get("baseName", ""))
    elif field == "itemCode":
        return regex.search(item.get("itemCode", ""))
    elif field == "quality":
        return regex.search(item.get("quality", ""))
    elif field == "tier":
        return regex.search(item.get("tier") or "")
    elif field == "set":
        return regex.search(item.get("set") or "")
    elif field == "type":
        return regex.search(item.get("type") or "")
    elif field == "location":
        return regex.search(item.get("location", ""))
    elif field == "stats":
        for stat_list in ("stats", "runewordStats"):
            for stat in item.get(stat_list, []):
                if regex.search(stat.get("description", "")):
                    return True
        for bonus in item.get("socketBonuses", []):
            if regex.search(bonus):
                return True
        return False
    elif field == "flags":
        return any(regex.search(f) for f in item.get("flags", []))
    elif field == "sockets":
        return any(regex.search(s.get("name", "")) for s in item.get("sockets", []))
    else:
        # Search any string field or nested value
        val = item.get(field)
        if isinstance(val, str):
            return regex.search(val)
        elif isinstance(val, (int, float)):
            return regex.search(str(val))
        return False

def matches_all_filters(item, filters):
    """Check if an item matches ALL field/pattern filters."""
    for field, regex in filters:
        if not matches_field(item, field, regex):
            return False
    return True

def print_item(source, filename, item):
    print(f"[{source}] {item.get('name', '?')}")
    print(f"  File: {filename}")
    print(f"  Location: {item.get('location', '?')}")
    if "quality" in item:
        print(f"  Quality: {item['quality']}")
    if item.get("type"):
        print(f"  Type: {item['type']}")
    if item.get("tier"):
        print(f"  Tier: {item['tier']}")
    if item.get("set"):
        print(f"  Set: {item['set']}")
    if "flags" in item:
        print(f"  Flags: {', '.join(item['flags'])}")
    if "defense" in item:
        print(f"  Defense: {item['defense']}")
    for stat in item.get("runewordStats", []):
        print(f"  {stat['description']}")
    for stat in item.get("stats", []):
        print(f"  {stat['description']}")
    for bonus in item.get("socketBonuses", []):
        print(f"  {bonus}")
    if item.get("sockets"):
        socket_names = [s.get("name", "?") for s in item["sockets"]]
        print(f"  Sockets [{item.get('socketCount', '?')}]: {', '.join(socket_names)}")
    print()

def search_items(directory, filters):
    for filename in sorted(os.listdir(directory)):
        if not filename.endswith(".json"):
            continue
        filepath = os.path.join(directory, filename)
        with open(filepath) as f:
            data = json.load(f)

        source = data.get("character", {}).get("name", filename)
        if "type" in data and data["type"] == "SharedStash":
            source = "Shared Stash"

        for item in data.get("items", []):
            if matches_all_filters(item, filters):
                print_item(source, filename, item)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Search for items matching field/pattern combos across d2sitems JSON files.",
        epilog="Examples:\n"
               "  find_items.py Infinity                              # search by name\n"
               "  find_items.py -f stats Teleport                     # search in stats\n"
               "  find_items.py -f quality Unique -f stats Resist     # Unique items with Resist\n"
               "  find_items.py -f name Torch -f stats Warlock        # Warlock Torches\n"
               "  find_items.py -f socketCount 4                      # items with exactly 4 sockets\n"
               "  find_items.py -f socketCount >=3 -f quality Unique  # Unique items with 3+ sockets\n"
               "  find_items.py -f socketCount 1-3                    # items with 1 to 3 sockets\n"
               "  find_items.py -f tier Elite                         # all Elite tier items\n"
               "  find_items.py -f tier Elite -f quality Unique       # Elite Unique items\n"
               "  find_items.py -f flags Ethereal                     # all Ethereal items\n"
               "  find_items.py -f flags Ethereal -f openSockets 4 -f tier Elite -f type Armor\n"
               "                                                      # Ethereal Elite Armors with 4 open sockets\n"
               '  find_items.py -f set "Tal Rasha"                    # items in Tal Rasha\'s set\n',
        formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("pattern", nargs="?", default=None,
                        help="regex pattern to match item names (default: Infinity)")
    default_dir = os.path.join(os.path.expanduser("~"), "Saved Games", "Diablo II Resurrected")
    parser.add_argument("-d", "--directory", default=default_dir,
                        help=f"directory containing JSON files (default: {default_dir})")
    parser.add_argument("-f", "--filter", nargs=2, action="append", metavar=("FIELD", "PATTERN"),
                        help="field and pattern pair (can be repeated); "
                             "fields: name, baseName, itemCode, quality, type, tier, set, location, stats, flags, "
                             "sockets, socketCount, openSockets (numeric: 3, >=4, 1-3)")
    args = parser.parse_args()

    NUMERIC_FIELDS = {"socketCount", "openSockets"}

    # Build filter list: combine -f pairs with the positional pattern (which searches name)
    filters = []
    if args.filter:
        for field, pattern in args.filter:
            if field in NUMERIC_FIELDS:
                filters.append((field, pattern))
            else:
                filters.append((field, re.compile(pattern, re.IGNORECASE)))
    if args.pattern:
        filters.append(("name", re.compile(args.pattern, re.IGNORECASE)))
    if not filters:
        filters.append(("name", re.compile("Infinity", re.IGNORECASE)))

    search_items(args.directory, filters)
