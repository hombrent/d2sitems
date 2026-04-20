using D2SSharp.Model;
using D2SSharp.Enums;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    Console.WriteLine("Usage: d2sitems <file1.d2s|dir> [file2.d2s|dir] ...");
    return;
}

// Expand arguments: directories become all .d2s files within them
var saveFiles = new List<string>();
foreach (var arg in args)
{
    if (Directory.Exists(arg))
        saveFiles.AddRange(Directory.GetFiles(arg, "*.d2s"));
    else
        saveFiles.Add(arg);
}

var excelDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "game_files", "default", "excel");

// Build lookups from game_files/default/excel (shared across all files)
var itemNames = BuildItemNameLookup(excelDir);
var skillNames = BuildSkillNameLookup(excelDir);
var runewordsByRunes = BuildRunewordLookup(excelDir);
var uniqueItemNames = BuildUniqueItemNameLookup(excelDir);
var setItemNames = BuildSetItemNameLookup(excelDir);
var gemApplyTypes = BuildGemApplyTypeLookup(excelDir);
var gemStats = BuildGemStatsLookup(excelDir);
var propertyToStats = BuildPropertyToStatsLookup(excelDir);
var statNameToId = BuildStatNameToIdLookup(excelDir);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

foreach (var saveFile in saveFiles)
{
    if (!File.Exists(saveFile))
    {
        Console.WriteLine($"File not found: {saveFile}");
        continue;
    }

    byte[] saveBytes = File.ReadAllBytes(saveFile);
    D2Save save = D2Save.Read(saveBytes);

    // Group items by location
    var equipped = new List<Item>();
    var inventory = new List<Item>();
    var stash = new List<Item>();
    var cube = new List<Item>();
    var belt = new List<Item>();

    foreach (var item in save.Items)
    {
        if (item.Position.Mode == ItemMode.Equipped)
            equipped.Add(item);
        else if (item.Position.Mode == ItemMode.InBelt)
            belt.Add(item);
        else if (item.Position.StorePage == StorePage.Inventory)
            inventory.Add(item);
        else if (item.Position.StorePage == StorePage.Stash)
            stash.Add(item);
        else if (item.Position.StorePage == StorePage.Cube)
            cube.Add(item);
        else
            inventory.Add(item);
    }

    var merc = new List<Item>();
    if (save.MercItems != null)
    {
        foreach (var item in save.MercItems.Items)
            merc.Add(item);
    }

    // ── Write text output ──

    var txtPath = Path.ChangeExtension(saveFile, ".txt");
    using (var writer = new StreamWriter(txtPath))
    {
        writer.WriteLine($"════════════════════════════════════════════");
        writer.WriteLine($"  Character: {save.Character.Preview.Name}");
        writer.WriteLine($"  Level {save.Character.Level} {save.Character.Class}");
        writer.WriteLine($"  File: {saveFile}");
        writer.WriteLine($"════════════════════════════════════════════");
        writer.WriteLine();

        writer.WriteLine("Character Stats:");
        WriteCharStat(writer, "Strength", StatId.Strength);
        WriteCharStat(writer, "Dexterity", StatId.Dexterity);
        WriteCharStat(writer, "Vitality", StatId.Vitality);
        WriteCharStat(writer, "Energy", StatId.Energy);
        WriteCharStat(writer, "Life", StatId.MaxLife);
        WriteCharStat(writer, "Mana", StatId.MaxMana);
        WriteCharStat(writer, "Stamina", StatId.MaxStamina);
        WriteCharStat(writer, "Gold", StatId.Gold);
        WriteCharStat(writer, "Stash Gold", StatId.StashGold);
        writer.WriteLine();

        WriteItemSection(writer, "Equipped Items", equipped);
        WriteItemSection(writer, "Belt", belt);
        WriteItemSection(writer, "Inventory", inventory);
        WriteItemSection(writer, "Stash", stash);
        WriteItemSection(writer, "Horadric Cube", cube);
        WriteItemSection(writer, "Mercenary Items", merc);
    }
    Console.WriteLine($"Text written to {txtPath}");

    // ── Write JSON output ──

    var jsonData = new Dictionary<string, object>
    {
        ["character"] = new Dictionary<string, object>
        {
            ["name"] = save.Character.Preview.Name,
            ["level"] = save.Character.Level,
            ["class"] = save.Character.Class.ToString()
        },
        ["stats"] = BuildCharStatsJson(),
        ["items"] = equipped.Concat(belt).Concat(inventory).Concat(stash).Concat(cube).Concat(merc)
            .Select(BuildItemJson).ToList()
    };

    var jsonPath = Path.ChangeExtension(saveFile, ".json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonData, jsonOptions));
    Console.WriteLine($"JSON written to {jsonPath}");

    // Local helpers that capture 'save'
    void WriteCharStat(TextWriter w, string label, StatId id)
    {
        var val = save.Stats.GetStat(id);
        if (val != 0)
        {
            if (id is StatId.MaxLife or StatId.MaxMana or StatId.MaxStamina)
                w.WriteLine($"  {label,-14} {val >> 8}");
            else
                w.WriteLine($"  {label,-14} {val}");
        }
    }

    Dictionary<string, long> BuildCharStatsJson()
    {
        var stats = new Dictionary<string, long>();
        void Add(string label, StatId id)
        {
            var val = save.Stats.GetStat(id);
            if (val != 0)
                stats[label] = (id is StatId.MaxLife or StatId.MaxMana or StatId.MaxStamina) ? val >> 8 : val;
        }
        Add("strength", StatId.Strength);
        Add("dexterity", StatId.Dexterity);
        Add("vitality", StatId.Vitality);
        Add("energy", StatId.Energy);
        Add("life", StatId.MaxLife);
        Add("mana", StatId.MaxMana);
        Add("stamina", StatId.MaxStamina);
        Add("gold", StatId.Gold);
        Add("stashGold", StatId.StashGold);
        return stats;
    }
}

// ── Helper methods ──

Dictionary<string, object?> BuildItemJson(Item item)
{
    var obj = new Dictionary<string, object?>
    {
        ["name"] = GetItemDisplayName(item),
        ["baseName"] = GetItemName(item.ItemCodeString),
        ["itemCode"] = item.ItemCodeString.TrimEnd('\0').Trim(),
        ["itemLevel"] = item.ItemLevel,
        ["quality"] = item.Quality.ToString(),
        ["location"] = GetLocationString(item)
    };

    // Flags
    var flags = new List<string>();
    if (item.Flags.HasFlag(ItemFlags.Ethereal)) flags.Add("Ethereal");
    if (item.Flags.HasFlag(ItemFlags.Runeword)) flags.Add("Runeword");
    if (item.Flags.HasFlag(ItemFlags.Socketed)) flags.Add($"Socketed ({item.Sockets.Count})");
    if (!item.Flags.HasFlag(ItemFlags.Identified)) flags.Add("Unidentified");
    if (item.Flags.HasFlag(ItemFlags.Personalized)) flags.Add("Personalized");
    if (flags.Count > 0)
        obj["flags"] = flags;

    if (item.Defense.HasValue)
        obj["defense"] = item.Defense.Value;
    if (item.MaxDurability.HasValue && item.MaxDurability > 0)
    {
        obj["durability"] = item.Durability;
        obj["maxDurability"] = item.MaxDurability.Value;
    }
    if (item.Quantity.HasValue)
        obj["quantity"] = item.Quantity.Value;

    if (item.RunewordStats?.Count > 0)
        obj["runewordStats"] = item.RunewordStats.Select(FormatStatJson).ToList();

    if (item.Stats?.Count > 0)
        obj["stats"] = item.Stats.Select(FormatStatJson).ToList();

    for (int i = 0; i < (item.SetBonusStats?.Count ?? 0); i++)
    {
        if (item.SetBonusStats![i] != null && item.SetBonusStats[i].Count > 0)
        {
            obj[$"setBonus{i + 1}"] = item.SetBonusStats[i].Select(FormatStatJson).ToList();
        }
    }

    var socketStatLines = GetSocketStats(item);
    if (socketStatLines.Count > 0)
        obj["socketBonuses"] = socketStatLines;

    if (item.Sockets.Count > 0)
    {
        obj["socketCount"] = item.Sockets.Count;
        obj["sockets"] = item.Sockets
            .Where(s => s != null)
            .Select(s => new Dictionary<string, object>
            {
                ["code"] = s!.ItemCodeString.TrimEnd('\0').Trim(),
                ["name"] = GetItemName(s.ItemCodeString)
            })
            .ToList();
    }

    return obj;
}

Dictionary<string, object> FormatStatJson(Stat stat)
{
    var obj = new Dictionary<string, object>
    {
        ["id"] = stat.Id.ToString(),
        ["description"] = FormatStat(stat)
    };

    var value = stat.Value;
    if (stat.Id is StatId.MaxLife or StatId.MaxMana or StatId.MaxStamina)
        value >>= 8;
    obj["value"] = value;

    if (stat.Layer != 0)
        obj["layer"] = stat.Layer;

    return obj;
}

void WriteItemSection(TextWriter w, string title, List<Item> items)
{
    if (items.Count == 0) return;
    w.WriteLine($"── {title} ({items.Count}) ──");
    w.WriteLine();
    foreach (var item in items)
        WriteItem(w, item);
}

void WriteItem(TextWriter w, Item item)
{
    var name = GetItemDisplayName(item);
    var locationStr = GetLocationString(item);

    w.WriteLine($"  {name}");
    w.WriteLine($"    Item Code: {item.ItemCodeString}, Level: {item.ItemLevel}, Quality: {item.Quality}");

    if (locationStr.Length > 0)
        w.WriteLine($"    Location: {locationStr}");

    var flags = new List<string>();
    if (item.Flags.HasFlag(ItemFlags.Ethereal)) flags.Add("Ethereal");
    if (item.Flags.HasFlag(ItemFlags.Runeword)) flags.Add("Runeword");
    if (item.Flags.HasFlag(ItemFlags.Socketed)) flags.Add($"Socketed ({item.Sockets.Count})");
    if (!item.Flags.HasFlag(ItemFlags.Identified)) flags.Add("Unidentified");
    if (item.Flags.HasFlag(ItemFlags.Personalized)) flags.Add("Personalized");
    if (flags.Count > 0)
        w.WriteLine($"    Flags: {string.Join(", ", flags)}");

    if (item.Defense.HasValue)
        w.WriteLine($"    Defense: {item.Defense}");
    if (item.MaxDurability.HasValue && item.MaxDurability > 0)
        w.WriteLine($"    Durability: {item.Durability}/{item.MaxDurability}");
    if (item.Quantity.HasValue)
        w.WriteLine($"    Quantity: {item.Quantity}");

    if (item.RunewordStats?.Count > 0)
    {
        w.WriteLine("    Runeword Stats:");
        foreach (var stat in item.RunewordStats)
            w.WriteLine($"      {FormatStat(stat)}");
    }

    if (item.Stats?.Count > 0)
    {
        w.WriteLine("    Stats:");
        foreach (var stat in item.Stats)
            w.WriteLine($"      {FormatStat(stat)}");
    }

    for (int i = 0; i < (item.SetBonusStats?.Count ?? 0); i++)
    {
        if (item.SetBonusStats![i] != null && item.SetBonusStats[i].Count > 0)
        {
            w.WriteLine($"    Set Bonus ({i + 1} pieces):");
            foreach (var stat in item.SetBonusStats[i])
                w.WriteLine($"      {FormatStat(stat)}");
        }
    }

    var socketStatLines = GetSocketStats(item);
    if (socketStatLines.Count > 0)
    {
        w.WriteLine("    Socket Bonuses:");
        foreach (var line in socketStatLines)
            w.WriteLine($"      {line}");
    }

    if (item.Sockets.Count > 0)
    {
        var socketNames = item.Sockets
            .Where(s => s != null)
            .Select(s => GetItemName(s!.ItemCodeString))
            .ToList();
        w.WriteLine($"    Sockets [{item.Sockets.Count}]: {string.Join(", ", socketNames)}");
    }

    w.WriteLine();
}

string GetItemDisplayName(Item item)
{
    var baseName = GetItemName(item.ItemCodeString);

    if (item.Flags.HasFlag(ItemFlags.Runeword))
    {
        var rwName = GetRunewordNameFromSockets(item);
        return $"{rwName} ({baseName})";
    }

    if (item.Quality == ItemQuality.Unique && item.QualityData is SetUniqueQualityData uqd)
    {
        if (uniqueItemNames.TryGetValue(uqd.SetUniqueFileIndex, out var uName))
            return $"{uName} ({baseName})";
    }

    if (item.Quality == ItemQuality.Set && item.QualityData is SetUniqueQualityData sqd)
    {
        if (setItemNames.TryGetValue(sqd.SetUniqueFileIndex, out var sName))
            return $"{sName} ({baseName})";
    }

    return item.Quality switch
    {
        ItemQuality.Inferior => $"Crude {baseName}",
        ItemQuality.Superior => $"Superior {baseName}",
        ItemQuality.Unique => $"[Unique] {baseName}",
        ItemQuality.Set => $"[Set] {baseName}",
        ItemQuality.Rare => $"[Rare] {baseName}",
        ItemQuality.Craft => $"[Crafted] {baseName}",
        ItemQuality.Tempered => $"[Tempered] {baseName}",
        _ => baseName
    };
}

string GetRunewordNameFromSockets(Item item)
{
    var runeKey = string.Join(",", item.Sockets
        .Where(s => s != null)
        .Select(s => s!.ItemCodeString.TrimEnd('\0').Trim()));

    if (runewordsByRunes.TryGetValue(runeKey, out var rwName))
        return rwName;

    return "Unknown Runeword";
}

string GetLocationString(Item item)
{
    if (item.Position.Mode == ItemMode.Equipped)
        return item.Position.BodyLocation.ToString();
    if (item.Position.Mode == ItemMode.InBelt)
        return "Belt";
    if (item.Position.Mode == ItemMode.Stored)
        return item.Position.StorePage.ToString();
    return item.Position.Mode.ToString();
}

string GetItemName(string code)
{
    var trimmed = code.TrimEnd('\0').Trim();
    if (itemNames.TryGetValue(trimmed, out var name))
        return name;
    return trimmed;
}

string FormatStat(Stat stat)
{
    var name = FormatStatName(stat.Id);
    var value = stat.Value;

    if (stat.Id is StatId.MaxLife or StatId.MaxMana or StatId.MaxStamina)
        value >>= 8;

    if (IsPerLevelStat(stat.Id))
    {
        var desc = GetPerLevelDescription(stat.Id);
        return $"+{value / 8.0:0.###} {desc} (per level)";
    }

    if (stat.Layer != 0 && IsSkillStat(stat.Id))
    {
        var skillName = GetSkillName(stat.Id, stat.Layer);
        return $"+{value} to {skillName}";
    }

    if (IsPercentStat(stat.Id))
        return $"{name}: {(value >= 0 ? "+" : "")}{value}%";

    if (IsSignedStat(stat.Id))
        return $"{name}: {(value >= 0 ? "+" : "")}{value}";

    return $"{name}: {value}";
}

string FormatStatName(StatId id)
{
    var name = id.ToString();
    return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
}

bool IsPerLevelStat(StatId id) => id is
    StatId.StrengthPerLevel or StatId.DexterityPerLevel or StatId.VitalityPerLevel or
    StatId.EnergyPerLevel or StatId.LifePerLevel or StatId.ManaPerLevel or
    StatId.ArmorPerLevel or StatId.MagicFindPerLevel or StatId.FindGoldPerLevel or
    StatId.AttackRatingPerLevel or StatId.MaxDamagePerLevel or StatId.MaxDamagePercentPerLevel;

string GetPerLevelDescription(StatId id) => id switch
{
    StatId.StrengthPerLevel => "Strength",
    StatId.DexterityPerLevel => "Dexterity",
    StatId.VitalityPerLevel => "Vitality",
    StatId.EnergyPerLevel => "Energy",
    StatId.LifePerLevel => "Life",
    StatId.ManaPerLevel => "Mana",
    StatId.ArmorPerLevel => "Defense",
    StatId.MagicFindPerLevel => "Magic Find%",
    StatId.FindGoldPerLevel => "Gold Find%",
    StatId.AttackRatingPerLevel => "Attack Rating",
    StatId.MaxDamagePerLevel => "Max Damage",
    StatId.MaxDamagePercentPerLevel => "Max Damage%",
    _ => id.ToString()
};

bool IsSkillStat(StatId id) => id is
    StatId.NonClassSkill or StatId.SingleSkill or StatId.AddSkillTab or
    StatId.AddClassSkills;

string GetSkillName(StatId statType, int id)
{
    if (statType == StatId.AddSkillTab)
    {
        // Skill tabs are class_index * 8 + tab_offset (0-2)
        // Tab names are hardcoded per class since they come from string tables
        return id switch
        {
            0 => "Amazon Bow & Crossbow Skills",
            1 => "Amazon Passive & Magic Skills",
            2 => "Amazon Javelin & Spear Skills",
            8 => "Sorceress Fire Skills",
            9 => "Sorceress Lightning Skills",
            10 => "Sorceress Cold Skills",
            16 => "Necromancer Curses",
            17 => "Necromancer Poison & Bone Skills",
            18 => "Necromancer Summoning Skills",
            24 => "Paladin Combat Skills",
            25 => "Paladin Offensive Auras",
            26 => "Paladin Defensive Auras",
            32 => "Barbarian Combat Skills",
            33 => "Barbarian Masteries",
            34 => "Barbarian Warcries",
            40 => "Druid Summoning Skills",
            41 => "Druid Shape Shifting Skills",
            42 => "Druid Elemental Skills",
            48 => "Assassin Traps",
            49 => "Assassin Shadow Disciplines",
            50 => "Assassin Martial Arts",
            56 => "Warlock Destruction Skills",
            57 => "Warlock Darkness Skills",
            58 => "Warlock Chaos Skills",
            _ => $"Skill Tab {id}"
        };
    }

    if (statType == StatId.AddClassSkills)
    {
        return id switch
        {
            0 => "Amazon Skills",
            1 => "Sorceress Skills",
            2 => "Necromancer Skills",
            3 => "Paladin Skills",
            4 => "Barbarian Skills",
            5 => "Druid Skills",
            6 => "Assassin Skills",
            7 => "Warlock Skills",
            _ => $"Class {id} Skills"
        };
    }

    // Look up individual skill names from skills.txt
    if (skillNames.TryGetValue(id, out var skillName))
        return skillName;
    return $"Skill #{id}";
}

bool IsSignedStat(StatId id) => id is
    StatId.Strength or StatId.Dexterity or StatId.Vitality or StatId.Energy or
    StatId.MaxLife or StatId.MaxMana or StatId.MaxStamina or
    StatId.ArmorClass or StatId.MinDamage or StatId.MaxDamage or
    StatId.FireMinDamage or StatId.FireMaxDamage or StatId.ColdMinDamage or StatId.ColdMaxDamage or
    StatId.LightningMinDamage or StatId.LightningMaxDamage or
    StatId.PoisonMinDamage or StatId.PoisonMaxDamage or
    StatId.MagicMinDamage or StatId.MagicMaxDamage or
    StatId.FireResist or StatId.ColdResist or StatId.LightningResist or StatId.PoisonResist or
    StatId.MagicResist or StatId.AllSkills or StatId.LightRadius or StatId.AttackRating or
    StatId.DefenseVsMissiles or StatId.HealAfterKill or
    StatId.AbsorbMagic or StatId.AbsorbFire or StatId.AbsorbLightning or StatId.AbsorbCold or
    StatId.HitPointRegeneration or StatId.ManaRecovery or
    StatId.NormalDamageReduction or StatId.MagicDamageReduction;

bool IsPercentStat(StatId id) => id is
    StatId.FasterRunWalk or StatId.FasterHitRecovery or StatId.FasterBlockRate or
    StatId.FasterCastRate or StatId.IncreasedAttackSpeed or
    StatId.MagicFind or StatId.GoldFind or
    StatId.CrushingBlow or StatId.OpenWounds or StatId.DeadlyStrike or
    StatId.MaxDamagePercent or StatId.MinDamagePercent or
    StatId.LifeSteal or StatId.ManaSteal or
    StatId.DamageReduced;

List<string> GetSocketStats(Item item)
{
    var lines = new List<string>();
    if (item.Sockets.Count == 0) return lines;

    // Determine gem apply type for the parent item (0=weapon, 1=helm/armor, 2=shield)
    var parentCode = item.ItemCodeString.TrimEnd('\0').Trim();
    int applyType = 1; // default to armor/helm
    if (gemApplyTypes.TryGetValue(parentCode, out var gat))
        applyType = gat;

    foreach (var socket in item.Sockets)
    {
        if (socket == null) continue;
        var socketCode = socket.ItemCodeString.TrimEnd('\0').Trim();

        // First, add any direct stats the socketed item has (jewels)
        if (socket.Stats?.Count > 0)
        {
            foreach (var stat in socket.Stats)
                lines.Add($"{FormatStat(stat)} ({GetItemName(socketCode)})");
        }

        // Then, look up gem/rune stats from gems.txt
        if (gemStats.TryGetValue(socketCode, out var mods))
        {
            var modSet = applyType switch
            {
                0 => mods.WeaponMods,
                2 => mods.ShieldMods,
                _ => mods.HelmMods
            };

            foreach (var mod in modSet)
            {
                var resolved = ResolvePropertyToText(mod.Code, mod.Param, mod.Min, mod.Max);
                if (resolved != null)
                    lines.Add($"{resolved} ({GetItemName(socketCode)})");
            }
        }
    }

    return lines;
}

string? ResolvePropertyToText(string propCode, string param, int min, int max)
{
    if (string.IsNullOrEmpty(propCode)) return null;

    if (!propertyToStats.TryGetValue(propCode, out var propEntries))
        return $"{propCode}: {min}-{max}";

    var parts = new List<string>();
    foreach (var entry in propEntries)
    {
        var statId = -1;
        if (!string.IsNullOrEmpty(entry.Stat) && statNameToId.TryGetValue(entry.Stat, out var sid))
            statId = sid;

        var value = (min == max) ? $"{min}" : $"{min}-{max}";

        // func determines how the property applies
        switch (entry.Func)
        {
            case 1: // direct stat assignment
            case 3: // same as 1 for our purposes
            case 8: // same as 1, speed-type stats
                if (statId >= 0)
                {
                    var id = (StatId)statId;
                    var name = FormatStatName(id);
                    if (IsPercentStat(id))
                        parts.Add($"{name}: +{value}%");
                    else if (IsSignedStat(id))
                        parts.Add($"{name}: +{value}");
                    else
                        parts.Add($"{name}: {value}");
                }
                else
                    parts.Add($"{propCode}: {value}");
                break;
            case 5: // min damage (mindamage stat)
                parts.Add($"Min Damage: +{value}");
                break;
            case 6: // max damage (maxdamage stat)
                parts.Add($"Max Damage: +{value}");
                break;
            case 7: // dmg% - enhanced damage (min/max damage percent)
                parts.Add($"Enhanced Damage: +{value}%");
                break;
            case 10: // skilltab
                parts.Add($"+{value} to Skill Tab {param}");
                break;
            case 15: // min damage for elemental
                if (statId >= 0)
                    parts.Add($"{FormatStatName((StatId)statId)}: +{value}");
                break;
            case 16: // max damage for elemental
                if (statId >= 0)
                    parts.Add($"{FormatStatName((StatId)statId)}: +{value}");
                break;
            case 22: // skill (oskill/item_singleskill)
                var sName = skillNames.TryGetValue(int.TryParse(param, out var pid) ? pid : -1, out var sn) ? sn : $"Skill {param}";
                parts.Add($"+{value} to {sName}");
                break;
            default:
                if (statId >= 0)
                    parts.Add($"{FormatStatName((StatId)statId)}: {value}");
                else
                    parts.Add($"{propCode}: {value}");
                break;
        }
    }

    return parts.Count > 0 ? string.Join(", ", parts) : null;
}

// ── Data loading from game_files/default/excel ──

Dictionary<string, string> BuildItemNameLookup(string dir)
{
    var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var file in new[] { "armor.txt", "weapons.txt", "misc.txt" })
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) continue;

        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) continue;

        var header = lines[0].Split('\t');
        int nameIdx = Array.IndexOf(header, "name");
        int codeIdx = Array.IndexOf(header, "code");
        if (nameIdx < 0 || codeIdx < 0) continue;

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split('\t');
            if (cols.Length > Math.Max(nameIdx, codeIdx))
            {
                var code = cols[codeIdx].Trim();
                var name = cols[nameIdx].Trim();
                if (code.Length > 0 && name.Length > 0 && !lookup.ContainsKey(code))
                    lookup[code] = name;
            }
        }
    }

    return lookup;
}

Dictionary<int, string> BuildSkillNameLookup(string dir)
{
    var lookup = new Dictionary<int, string>();
    var path = Path.Combine(dir, "skills.txt");
    if (!File.Exists(path)) return lookup;

    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return lookup;

    var header = lines[0].Split('\t');
    int nameIdx = Array.IndexOf(header, "skill");
    int idIdx = Array.IndexOf(header, "*Id");
    if (nameIdx < 0 || idIdx < 0) return lookup;

    for (int i = 1; i < lines.Length; i++)
    {
        var cols = lines[i].Split('\t');
        if (cols.Length > Math.Max(nameIdx, idIdx)
            && int.TryParse(cols[idIdx].Trim(), out var id))
        {
            var name = cols[nameIdx].Trim();
            if (name.Length > 0)
                lookup[id] = name;
        }
    }

    return lookup;
}

Dictionary<string, string> BuildRunewordLookup(string dir)
{
    // Maps "r31,r06,r30" -> "Enigma" (rune code combo -> runeword name)
    var lookup = new Dictionary<string, string>();
    var path = Path.Combine(dir, "runes.txt");
    if (!File.Exists(path)) return lookup;

    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return lookup;

    var header = lines[0].Split('\t');
    int nameIdx = Array.IndexOf(header, "*Rune Name");
    int completeIdx = Array.IndexOf(header, "complete");
    int rune1Idx = Array.IndexOf(header, "Rune1");
    if (nameIdx < 0 || rune1Idx < 0) return lookup;

    for (int i = 1; i < lines.Length; i++)
    {
        var cols = lines[i].Split('\t');
        if (cols.Length <= rune1Idx + 5) continue;

        // Only include complete runewords
        if (completeIdx >= 0 && cols[completeIdx].Trim() != "1") continue;

        var name = cols[nameIdx].Trim();
        if (name.Length == 0) continue;

        var runes = new List<string>();
        for (int r = 0; r < 6; r++)
        {
            var rune = cols[rune1Idx + r].Trim();
            if (rune.Length > 0)
                runes.Add(rune);
        }

        if (runes.Count > 0)
        {
            var key = string.Join(",", runes);
            if (!lookup.ContainsKey(key))
                lookup[key] = name;
        }
    }

    return lookup;
}

Dictionary<int, string> BuildUniqueItemNameLookup(string dir)
{
    return BuildIndexedNameLookup(Path.Combine(dir, "uniqueitems.txt"));
}

Dictionary<int, string> BuildSetItemNameLookup(string dir)
{
    return BuildIndexedNameLookup(Path.Combine(dir, "setitems.txt"));
}

Dictionary<int, string> BuildIndexedNameLookup(string path)
{
    var lookup = new Dictionary<int, string>();
    if (!File.Exists(path)) return lookup;

    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return lookup;

    var header = lines[0].Split('\t');
    int nameIdx = Array.IndexOf(header, "index");
    int idIdx = Array.IndexOf(header, "*ID");
    if (nameIdx < 0 || idIdx < 0) return lookup;

    for (int i = 1; i < lines.Length; i++)
    {
        var cols = lines[i].Split('\t');
        if (cols.Length > Math.Max(nameIdx, idIdx)
            && int.TryParse(cols[idIdx].Trim(), out var id))
        {
            var name = cols[nameIdx].Trim();
            if (name.Length > 0)
                lookup[id] = name;
        }
    }

    return lookup;
}

// gemapplytype: 0=weapon, 1=armor/helm, 2=shield
Dictionary<string, int> BuildGemApplyTypeLookup(string dir)
{
    var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (var file in new[] { "armor.txt", "weapons.txt" })
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) continue;

        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) continue;

        var header = lines[0].Split('\t');
        int codeIdx = Array.IndexOf(header, "code");
        int gatIdx = Array.IndexOf(header, "gemapplytype");
        if (codeIdx < 0 || gatIdx < 0) continue;

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split('\t');
            if (cols.Length > Math.Max(codeIdx, gatIdx))
            {
                var code = cols[codeIdx].Trim();
                if (code.Length > 0 && int.TryParse(cols[gatIdx].Trim(), out var gat))
                    lookup[code] = gat;
            }
        }
    }

    return lookup;
}

Dictionary<string, GemModSet> BuildGemStatsLookup(string dir)
{
    var lookup = new Dictionary<string, GemModSet>(StringComparer.OrdinalIgnoreCase);
    var path = Path.Combine(dir, "gems.txt");
    if (!File.Exists(path)) return lookup;

    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return lookup;

    var header = lines[0].Split('\t');
    int codeIdx = Array.IndexOf(header, "code");
    int wStart = Array.IndexOf(header, "weaponMod1Code");
    int hStart = Array.IndexOf(header, "helmMod1Code");
    int sStart = Array.IndexOf(header, "shieldMod1Code");
    if (codeIdx < 0 || wStart < 0) return lookup;

    for (int i = 1; i < lines.Length; i++)
    {
        var cols = lines[i].Split('\t');
        if (cols.Length <= codeIdx) continue;
        var code = cols[codeIdx].Trim();
        if (code.Length == 0) continue;

        lookup[code] = new GemModSet(
            ParseGemMods(cols, wStart),
            ParseGemMods(cols, hStart),
            ParseGemMods(cols, sStart));
    }

    return lookup;
}

List<GemMod> ParseGemMods(string[] cols, int startIdx)
{
    var mods = new List<GemMod>();
    for (int m = 0; m < 3; m++)
    {
        int baseIdx = startIdx + m * 4;
        if (baseIdx + 3 >= cols.Length) break;
        var modCode = cols[baseIdx].Trim();
        if (modCode.Length == 0) continue;
        var param = cols[baseIdx + 1].Trim();
        int.TryParse(cols[baseIdx + 2].Trim(), out var min);
        int.TryParse(cols[baseIdx + 3].Trim(), out var max);
        mods.Add(new GemMod(modCode, param, min, max));
    }
    return mods;
}

Dictionary<string, List<PropertyEntry>> BuildPropertyToStatsLookup(string dir)
{
    var lookup = new Dictionary<string, List<PropertyEntry>>(StringComparer.OrdinalIgnoreCase);
    var path = Path.Combine(dir, "properties.txt");
    if (!File.Exists(path)) return lookup;

    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return lookup;

    var header = lines[0].Split('\t');
    int codeIdx = Array.IndexOf(header, "code");
    var funcIndices = new int[5];
    var statIndices = new int[5];
    for (int f = 0; f < 5; f++)
    {
        funcIndices[f] = Array.IndexOf(header, $"func{f + 1}");
        statIndices[f] = Array.IndexOf(header, $"stat{f + 1}");
    }

    for (int i = 1; i < lines.Length; i++)
    {
        var cols = lines[i].Split('\t');
        if (cols.Length <= codeIdx) continue;
        var code = cols[codeIdx].Trim();
        if (code.Length == 0) continue;

        var entries = new List<PropertyEntry>();
        for (int f = 0; f < 5; f++)
        {
            if (funcIndices[f] < 0 || funcIndices[f] >= cols.Length) continue;
            var funcStr = cols[funcIndices[f]].Trim();
            if (!int.TryParse(funcStr, out var func)) continue;
            var stat = (statIndices[f] >= 0 && statIndices[f] < cols.Length)
                ? cols[statIndices[f]].Trim() : "";
            entries.Add(new PropertyEntry(func, stat));
        }

        if (entries.Count > 0)
            lookup[code] = entries;
    }

    return lookup;
}

Dictionary<string, int> BuildStatNameToIdLookup(string dir)
{
    var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var path = Path.Combine(dir, "itemstatcost.txt");
    if (!File.Exists(path)) return lookup;

    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return lookup;

    var header = lines[0].Split('\t');
    int nameIdx = Array.IndexOf(header, "Stat");
    int idIdx = Array.IndexOf(header, "*ID");
    if (nameIdx < 0 || idIdx < 0) return lookup;

    for (int i = 1; i < lines.Length; i++)
    {
        var cols = lines[i].Split('\t');
        if (cols.Length > Math.Max(nameIdx, idIdx)
            && int.TryParse(cols[idIdx].Trim(), out var id))
        {
            var name = cols[nameIdx].Trim();
            if (name.Length > 0)
                lookup[name] = id;
        }
    }

    return lookup;
}

// Record types must come after all top-level statements
record GemMod(string Code, string Param, int Min, int Max);
record GemModSet(List<GemMod> WeaponMods, List<GemMod> HelmMods, List<GemMod> ShieldMods);
record PropertyEntry(int Func, string Stat);
