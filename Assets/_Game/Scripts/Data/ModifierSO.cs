using UnityEngine;

public enum ModifierType { None, Split, Pierce, Bounce, Leech, Oversize, Volatile, Homing }

[CreateAssetMenu(menuName = "RuneForge/Modifier")]
public class ModifierSO : ScriptableObject
{
    public string modifierName;
    public ModifierType modifierType;
    public float damageMultiplier = 1f;

    // Split
    public int splitCount = 3;
    public float splitSpreadAngle = 15f;

    // Pierce
    public int pierceCount = 5;

    // Bounce
    public int bounceCount = 3;

    // Leech
    public float leechPercent = 0.15f;

    // Oversize
    public float sizeMultiplier = 2f;
    public float speedPenalty = 0.8f; // multiplied to projectile speed

    // Volatile
    public float volatileMissChance = 0.1f;

    // Homing
    public float homingTurnSpeed = 180f; // degrees/sec
    public float homingDetectRange = 10f;
    public float homingSpeedMult = 0.7f; // -30% speed

    // Compatibility matrix
    static readonly bool[,] compat = new bool[,]
    {
        //                   Bolt   Cone   Beam   Aura   Orbit  Trap
        /* None     */ {     true,  true,  true,  true,  true,  true  },
        /* Split    */ {     true,  true,  true,  false, true,  true  },
        /* Pierce   */ {     true,  false, true,  false, false, false },
        /* Bounce   */ {     true,  false, false, false, true,  false },
        /* Leech    */ {     true,  true,  true,  true,  true,  true  },
        /* Oversize */ {     true,  true,  true,  true,  true,  true  },
        /* Volatile */ {     true,  true,  true,  true,  true,  true  },
        /* Homing   */ {     true,  false, false, false, true,  false },
    };

    public static bool IsCompatible(ModifierType mod, FormType form)
    {
        return compat[(int)mod, (int)form];
    }
}
