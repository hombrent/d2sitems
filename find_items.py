#!/usr/bin/env python3
"""Search for items matching field/pattern combos across all d2sitems JSON files in a directory."""

import argparse
import json
import os
import re

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
        break  # use the first config file found
    return config

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

RESIST_STAT_IDS = {
    "resistfire": "FireResist",
    "resistcold": "ColdResist",
    "resistlightning": "LightningResist",
    "resistpoison": "PoisonResist",
}

def get_stat_value(item, stat_id):
    """Sum all values for a given stat ID across stats, runewordStats, and set bonuses."""
    total = 0
    for stat_list in ("stats", "runewordStats"):
        for stat in item.get(stat_list, []):
            if stat.get("id") == stat_id:
                total += stat.get("value", 0)
    # Also check set bonus stats
    for i in range(1, 6):
        for stat in item.get(f"setBonus{i}", []):
            if stat.get("id") == stat_id:
                total += stat.get("value", 0)
    return total

def matches_field(item, field, pattern):
    """Check if an item matches the pattern in the specified field.
    pattern is a compiled regex for text fields, or a string for numeric fields."""
    if field == "socketCount":
        return numeric_match(item.get("socketCount", 0), pattern)
    if field == "openSockets":
        return numeric_match(item.get("openSockets", 0), pattern)
    if field == "itemLevel":
        return numeric_match(item.get("itemLevel", 0), pattern)
    if field in RESIST_STAT_IDS:
        return numeric_match(get_stat_value(item, RESIST_STAT_IDS[field]), pattern)
    if field == "resistall":
        return all(
            numeric_match(get_stat_value(item, sid), pattern)
            for sid in RESIST_STAT_IDS.values()
        )
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
    elif field == "stat":
        for stat_list in ("stats", "runewordStats"):
            for stat in item.get(stat_list, []):
                if regex.search(stat.get("description", "")):
                    return True
        for bonus in item.get("socketBonuses", []):
            if regex.search(bonus):
                return True
        return False
    elif field == "flag":
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
    """Check if an item matches ALL field/pattern filters.
    Each filter is (field, pattern, negate) where negate inverts the match."""
    for field, pattern, negate in filters:
        result = matches_field(item, field, pattern)
        if negate:
            result = not result
        if not result:
            return False
    return True

def print_item(source, filename, item):
    print(f"[Character: {source}] (file: {filename})")
    print(f"  Name: {item.get('name', '?')}")
    print(f"  Location: {item.get('location', '?')}")
    if "itemLevel" in item:
        print(f"  Item Level: {item['itemLevel']}")
    if "quality" in item:
        print(f"  Quality: {item['quality']}")
    if item.get("type"):
        print(f"  Type: {item['type']}")
    if item.get("tier"):
        print(f"  Tier: {item['tier']}")
    if item.get("set"):
        print(f"  Set: {item['set']}")
    if "perfectionScore" in item:
        print(f"  Perfection: {item['perfectionScore']}%")
    if "flags" in item:
        print(f"  Flags: {', '.join(item['flags'])}")
    if "defense" in item:
        defRange = item.get("baseDefenseRange")
        if defRange:
            print(f"  Defense: {item['defense']} (base: {defRange})")
        else:
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
        save_file = data.get("file", filename)

        for item in data.get("items", []):
            if matches_all_filters(item, filters):
                print_item(source, save_file, item)

ALL_RESIST_ELEMENTS = ["fire", "cold", "lightning", "poison"]

def transform_stat_pattern(pattern):
    """Transform 'resist X' to match both 'resist X' and 'X resist'.
    'all resist' expands to check all four elements."""
    m = re.match(r'^(all\s+resist|resist\s+all)$', pattern, re.IGNORECASE)
    if m:
        return None  # sentinel: handled specially

    m = re.match(r'^resist\s+(.+)$', pattern, re.IGNORECASE)
    if m:
        word = re.escape(m.group(1))
        return f"({word}.*resist|resist.*{word})"
    return pattern

def is_all_resist_pattern(pattern):
    return bool(re.match(r'^(all\s+resist|resist\s+all)$', pattern, re.IGNORECASE))

NUMERIC_FIELDS = ["socketcount", "opensockets", "ilvl", "resistfire", "resistcold",
                   "resistlightning", "resistpoison", "resistall"]
TEXT_FIELDS = ["name", "base", "quality", "type", "tier", "set", "stat"]

# Map lowercase CLI arg names to internal field names used in matches_field
ARG_TO_FIELD = {
    "base": "baseName",
    "ilvl": "itemLevel",
    "socketcount": "socketCount",
    "opensockets": "openSockets",
}

FIELD_HELP = {
    "base": 'item base, ie "Mage Plate" or "Phase Blade"',
    "quality": 'item quality, ie Normal, Superior, Magic, Rare, Set, Unique',
    "tier": 'item tier, ie Normal, Exceptional, Elite',
    "type": 'item type, ie Axe, Sword, Shield, Ring',
    "set": 'the set that an item belongs to.  can be a partial match, ie "trang"',
}

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Search for items matching field/pattern combos across d2sitems JSON files.",
        epilog="Examples:\n"
               "  find_items.py --name Infinity                       # search by name\n"
               "  find_items.py --stat Teleport                       # search in stats\n"
               '  find_items.py --quality Unique --resistall ">20"    # Unique items resist all greater than 20%\n'
               "  find_items.py --name Torch --stat Warlock           # Warlock Torches\n"
               '  find_items.py --socketcount ">=3" --quality Unique     # Unique items with 3+ sockets\n'
               "  find_items.py --tier Elite --quality Unique           # Elite Unique items\n"
               "  find_items.py --ethereal --opensockets 4 --tier Elite --type Armor --quality Superior\n"
               "                                                       # Superior Ethereal Elite Armors with 4 open sockets\n"
               '  find_items.py --set "Tal Rasha"                      # items in Tal Rasha\'s set\n'
               "  find_items.py --base \"Small Charm\" --resistall 5  # small charms with 5 all resist\n"
               '  find_items.py --base Amulet --ilvl ">=90" --quality Magic   # Amulets for crafting\n',
        formatter_class=argparse.RawDescriptionHelpFormatter)
    config = load_config()
    default_dir = config.get("save_dir",
        os.path.join(os.path.expanduser("~"), "Saved Games", "Diablo II Resurrected"))

    parser.add_argument("pattern", nargs="?", default=None,
                        help="regex pattern to match item names (shorthand for --name)")
    parser.add_argument("-d", "--directory", default=default_dir,
                        help=f"directory containing JSON files (default: {default_dir})")

    # Add a named argument for each searchable field
    for field in TEXT_FIELDS:
        parser.add_argument(f"--{field}", metavar="PATTERN",
                            help=FIELD_HELP.get(field, f"regex pattern to match in the {field} field"))
    for field in NUMERIC_FIELDS:
        parser.add_argument(f"--{field}", metavar="EXPR",
                            help=f"numeric expression for {field} (e.g. 3, >=4, 1-3)")

    # Shorthand boolean flags
    parser.add_argument("--ethereal", action="store_true", help="only Ethereal items")
    parser.add_argument("--notethereal", action="store_true", help="only non-Ethereal items")

    args = parser.parse_args()

    # Build filter list from all specified field arguments
    filters = []
    for arg in TEXT_FIELDS:
        val = getattr(args, arg, None)
        if val is not None:
            field = ARG_TO_FIELD.get(arg, arg)
            if field == "stat" and is_all_resist_pattern(val):
                for element in ALL_RESIST_ELEMENTS:
                    pat = f"({element}.*resist|resist.*{element})"
                    filters.append((field, re.compile(pat, re.IGNORECASE), False))
            else:
                if field == "stat":
                    val = transform_stat_pattern(val)
                filters.append((field, re.compile(val, re.IGNORECASE), False))
    for arg in NUMERIC_FIELDS:
        val = getattr(args, arg, None)
        if val is not None:
            field = ARG_TO_FIELD.get(arg, arg)
            filters.append((field, val, False))
    if args.ethereal:
        filters.append(("flags", re.compile(r"Ethereal", re.IGNORECASE), False))
    if args.notethereal:
        filters.append(("flags", re.compile(r"Ethereal", re.IGNORECASE), True))
    if args.pattern:
        filters.append(("name", re.compile(args.pattern, re.IGNORECASE), False))
    if not filters:
        filters.append(("name", re.compile("Infinity", re.IGNORECASE), False))

    search_items(args.directory, filters)
