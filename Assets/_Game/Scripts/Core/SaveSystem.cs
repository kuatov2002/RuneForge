using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // ── Meta progression ──
    public int currency;
    public int runsCompleted;
    public int bestFloor;
    public int totalKills;
    public int totalEssenceSpent;

    // Path A upgrades
    public int maxHPBonus;
    public int baseDamage;
    public int dashCharges;
    public int speedBonus;
    public int startingGold;
    public int potionSlot;
    public int critChance;
    public int rerolls;
    public int startingRelic;

    // Path B upgrades
    public int secondWind;
    public int spellMastery;
    public int phaseStep;
    public int blinkStrike;
    public int haggler;
    public int elemMastery;
    public int bloodMage;
    public int luckyFind;
    public int cursedHeirloom;

    // Path choices
    public List<string> pathChoiceIds = new();
    public List<string> pathChoiceValues = new();

    // Element unlocks
    public List<string> unlockedElements = new();

    // Loadouts
    public List<string> unlockedLoadouts = new();
    public string selectedLoadout = "default";

    // Combo discovery (MetaProgression)
    public List<string> discoveredCombos = new();

    // ── Ascension ──
    public int ascensionHeat;
    public int ascensionMaxHeat;

    // ── Codex ──
    public List<string> codexCombos = new();
    public List<string> codexReactions = new();
    public List<string> codexRelics = new();

    // ── Misc ──
    public bool tutorialShown;
    public List<string> shownTutorialHints = new();

    // ── Path choice helpers ──

    public string GetPath(string id)
    {
        int idx = pathChoiceIds.IndexOf(id);
        return idx >= 0 ? pathChoiceValues[idx] : "A";
    }

    public void SetPath(string id, string path)
    {
        int idx = pathChoiceIds.IndexOf(id);
        if (idx >= 0)
            pathChoiceValues[idx] = path;
        else
        {
            pathChoiceIds.Add(id);
            pathChoiceValues.Add(path);
        }
    }
}

public static class SaveSystem
{
    static readonly byte[] Key = System.Text.Encoding.UTF8.GetBytes("RuneF0rg3_2024!!");
    const string FILE_NAME = "runeforge.sav";

    static SaveData _data;
    static bool _loaded;

    public static SaveData Data
    {
        get
        {
            if (!_loaded) Load();
            return _data;
        }
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    public static void Load()
    {
        _loaded = true;
        string path = FilePath;

        if (File.Exists(path))
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] decrypted = Xor(encrypted);
                string json = System.Text.Encoding.UTF8.GetString(decrypted);
                _data = JsonUtility.FromJson<SaveData>(json);
                if (_data != null) return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Failed to load save, starting fresh: {e.Message}");
            }
        }

        _data = new SaveData();
        Save();
    }

    public static void Save()
    {
        if (_data == null) _data = new SaveData();
        try
        {
            string json = JsonUtility.ToJson(_data);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] encrypted = Xor(bytes);
            File.WriteAllBytes(FilePath, encrypted);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to save: {e.Message}");
        }
    }

    static byte[] Xor(byte[] data)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ Key[i % Key.Length]);
        return result;
    }

    public static void ResetAll()
    {
        _data = new SaveData();
        Save();
    }
}
