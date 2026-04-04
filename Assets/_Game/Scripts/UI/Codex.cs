using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks discovered combos, relics, and reactions across runs.
/// </summary>
public static class Codex
{
    static HashSet<string> discoveredCombos = new();
    static HashSet<string> discoveredReactions = new();
    static HashSet<string> discoveredRelics = new();
    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        var d = SaveSystem.Data;
        foreach (var s in d.codexCombos) if (s.Length > 0) discoveredCombos.Add(s);
        foreach (var s in d.codexReactions) if (s.Length > 0) discoveredReactions.Add(s);
        foreach (var s in d.codexRelics) if (s.Length > 0) discoveredRelics.Add(s);
    }

    static void Save()
    {
        var d = SaveSystem.Data;
        d.codexCombos = new List<string>(discoveredCombos);
        d.codexReactions = new List<string>(discoveredReactions);
        d.codexRelics = new List<string>(discoveredRelics);
        SaveSystem.Save();
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
