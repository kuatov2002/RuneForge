using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks discovered combos, relics, and reactions across runs.
/// Persists via PlayerPrefs.
/// </summary>
public static class Codex
{
    const string KEY_PREFIX = "codex_";
    static HashSet<string> discoveredCombos = new();
    static HashSet<string> discoveredReactions = new();
    static HashSet<string> discoveredRelics = new();
    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        string comboData = PlayerPrefs.GetString(KEY_PREFIX + "combos", "");
        if (!string.IsNullOrEmpty(comboData))
            foreach (var s in comboData.Split('|')) if (s.Length > 0) discoveredCombos.Add(s);

        string reactionData = PlayerPrefs.GetString(KEY_PREFIX + "reactions", "");
        if (!string.IsNullOrEmpty(reactionData))
            foreach (var s in reactionData.Split('|')) if (s.Length > 0) discoveredReactions.Add(s);

        string relicData = PlayerPrefs.GetString(KEY_PREFIX + "relics", "");
        if (!string.IsNullOrEmpty(relicData))
            foreach (var s in relicData.Split('|')) if (s.Length > 0) discoveredRelics.Add(s);
    }

    static void Save()
    {
        PlayerPrefs.SetString(KEY_PREFIX + "combos", string.Join("|", discoveredCombos));
        PlayerPrefs.SetString(KEY_PREFIX + "reactions", string.Join("|", discoveredReactions));
        PlayerPrefs.SetString(KEY_PREFIX + "relics", string.Join("|", discoveredRelics));
        PlayerPrefs.Save();
    }

    public static void DiscoverCombo(string comboName)
    {
        if (string.IsNullOrEmpty(comboName)) return;
        if (discoveredCombos.Add(comboName)) Save();
    }

    public static void DiscoverReaction(string reactionName, Vector3 pos, Color color)
    {
        if (string.IsNullOrEmpty(reactionName)) return;
        if (discoveredReactions.Add(reactionName)) Save();
    }

    public static void DiscoverRelic(string relicName)
    {
        if (string.IsNullOrEmpty(relicName)) return;
        if (discoveredRelics.Add(relicName)) Save();
    }

    public static bool IsComboDiscovered(string name) => discoveredCombos.Contains(name);
    public static bool IsReactionDiscovered(string name) => discoveredReactions.Contains(name);

    public static IReadOnlyCollection<string> DiscoveredCombos => discoveredCombos;
    public static IReadOnlyCollection<string> DiscoveredReactions => discoveredReactions;
    public static IReadOnlyCollection<string> DiscoveredRelics => discoveredRelics;

    public static int TotalComboCount => ComboSpellRegistry.AllCombos != null ? ComboSpellRegistry.AllCombos.Count : 0;
    public static int DiscoveredComboCount => discoveredCombos.Count;
}
