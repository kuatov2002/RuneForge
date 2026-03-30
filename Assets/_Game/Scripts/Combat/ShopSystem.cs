using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates shop inventory for in-run shop rooms.
/// Items include relics, mutations, potions, and rerolls.
/// </summary>
public static class ShopSystem
{
    public enum ShopItemType { Relic, CursedRelic, Mutation, Potion, Reroll }

    public struct ShopItem
    {
        public ShopItemType type;
        public string name;
        public string description;
        public int price;
        public Color color;
        public RelicSO relic;          // for Relic/CursedRelic type
        public SpellMutationSystem.MutationDef mutation; // for Mutation type
    }

    /// <summary>Tier-based relic pricing: cursed = cheap, common = mid, uncommon = expensive.</summary>
    static int RelicPrice(RelicSO relic, int floor)
    {
        if (relic.isCursed)
            return 50 + floor * 8;

        if (relic.relicType == RelicType.SpeedBoost || relic.relicType == RelicType.GaleRing ||
            relic.relicType == RelicType.Lucky || relic.relicType == RelicType.Regeneration)
            return 40 + floor * 8;   // Common tier

        return 70 + floor * 12; // Uncommon tier
    }

    /// <summary>Apply Haggler meta-upgrade discount to a price.</summary>
    static int ApplyDiscount(int basePrice)
    {
        return Mathf.Max(1, Mathf.RoundToInt(basePrice * MetaProgression.HagglerDiscount));
    }

    public static ShopItem[] GenerateInventory(int floor, RelicSO[] allRelics, RelicManager relicMgr, int ownedMutations)
    {
        var items = new List<ShopItem>();

        // 1. Random non-cursed relic
        var availableRelics = new List<RelicSO>();
        foreach (var r in allRelics)
            if (!r.isCursed && !relicMgr.HasRelic(r.relicType)) availableRelics.Add(r);

        if (availableRelics.Count > 0)
        {
            var relic = availableRelics[Random.Range(0, availableRelics.Count)];
            items.Add(new ShopItem
            {
                type = ShopItemType.Relic,
                name = relic.relicName,
                description = relic.description,
                price = ApplyDiscount(RelicPrice(relic, floor)),
                color = relic.color,
                relic = relic
            });
            availableRelics.Remove(relic);
        }

        // 2. Mutation (if player has < 3)
        if (ownedMutations < SpellMutationSystem.MaxMutations)
        {
            var choices = SpellMutationSystem.GenerateChoices(1);
            if (choices.Length > 0)
            {
                items.Add(new ShopItem
                {
                    type = ShopItemType.Mutation,
                    name = choices[0].name,
                    description = choices[0].description,
                    price = ApplyDiscount(80 + floor * 10),
                    color = choices[0].color,
                    mutation = choices[0]
                });
            }
        }

        // 3. Potion
        items.Add(new ShopItem
        {
            type = ShopItemType.Potion,
            name = "Healing Potion",
            description = "+1 potion for this floor",
            price = ApplyDiscount(30 + floor * 3),
            color = new Color(0.3f, 0.9f, 0.4f)
        });

        // 4. Reroll
        items.Add(new ShopItem
        {
            type = ShopItemType.Reroll,
            name = "Rune Reroll",
            description = "Reroll your next upgrade choice",
            price = ApplyDiscount(50 + floor * 6),
            color = new Color(0.6f, 0.4f, 0.9f)
        });

        // 5. Floor 3+: second relic or cursed relic at discount
        if (floor >= 3)
        {
            var cursedRelics = new List<RelicSO>();
            foreach (var r in allRelics)
                if (r.isCursed && !relicMgr.HasRelic(r.relicType)) cursedRelics.Add(r);

            if (cursedRelics.Count > 0 && Random.value < 0.5f)
            {
                var cursed = cursedRelics[Random.Range(0, cursedRelics.Count)];
                items.Add(new ShopItem
                {
                    type = ShopItemType.CursedRelic,
                    name = cursed.relicName,
                    description = cursed.description,
                    price = ApplyDiscount(RelicPrice(cursed, floor)),
                    color = cursed.color,
                    relic = cursed
                });
            }
            else if (availableRelics.Count > 0)
            {
                var relic2 = availableRelics[Random.Range(0, availableRelics.Count)];
                items.Add(new ShopItem
                {
                    type = ShopItemType.Relic,
                    name = relic2.relicName,
                    description = relic2.description,
                    price = ApplyDiscount(RelicPrice(relic2, floor)),
                    color = relic2.color,
                    relic = relic2
                });
            }
        }

        return items.ToArray();
    }
}
