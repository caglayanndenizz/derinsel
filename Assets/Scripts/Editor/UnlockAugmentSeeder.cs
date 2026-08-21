#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that creates UnlockAugmentDefinition assets from the existing
/// AugmentDefinition source assets and populates the UnlockAugmentDatabase.
///
/// Run via: Tools ▶ Augments ▶ Create Unlock Augment Assets
/// </summary>
public static class UnlockAugmentSeeder
{
    private const string SourceFolder       = "Assets/Augments";
    private const string OutputFolder       = "Assets/Augments/Unlocks";
    private const string HammerOutputFolder = "Assets/Augments/Unlocks/Hammer";
    private const string DatabasePath       = "Assets/Augments/UnlockAugmentDatabase.asset";

    [MenuItem("Tools/Augments/Create Unlock Augment Assets")]
    public static void CreateUnlockAssets()
    {
        EnsureFolder(OutputFolder);

        var db = AssetDatabase.LoadAssetAtPath<UnlockAugmentDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogError($"[UnlockAugmentSeeder] UnlockAugmentDatabase not found at '{DatabasePath}'. " +
                           "Create the asset first via Create → Scriptable Objects → Unlock Augment Database.");
            return;
        }

        db.longbowUnlocks.Clear();
        db.crossbowUnlocks.Clear();
        db.hammerUnlocks.Clear();

        // ── Longbow ───────────────────────────────────────────────────────────
        db.longbowUnlocks.Add(Make("ChargedBowAoeUnlock",              "Unlock_ChargedLongbowAoe",        AugmentId.ChargedLongbowAoeUnlock,           WeaponType.Longbow));
        db.longbowUnlocks.Add(Make("TripleTheAmountOfArrows",          "Unlock_TripleArrow",              AugmentId.TripleArrowUnlock,                 WeaponType.Longbow));
        db.longbowUnlocks.Add(Make("ArrowFreezeUnlock",                "Unlock_LongbowFreeze",            AugmentId.LongbowFreezeUnlock,               WeaponType.Longbow));
        db.longbowUnlocks.Add(Make("FireArrowUnlock",                  "Unlock_FireArrow",                AugmentId.FireArrowUnlock,                   WeaponType.Longbow));
        db.longbowUnlocks.Add(Make("PoisonArrowUnlock",                "Unlock_PoisonArrow",              AugmentId.PoisonArrowUnlock,                 WeaponType.Longbow));

        // ── Crossbow ──────────────────────────────────────────────────────────
        db.crossbowUnlocks.Add(Make("CrossbowBoltPierce",              "Unlock_CrossbowBoltPierce",       AugmentId.CrossbowBoltPierce,                WeaponType.Crossbow));
        db.crossbowUnlocks.Add(Make("CrossbowBoltBleed",               "Unlock_CrossbowBoltBleed",        AugmentId.CrossbowBoltBleed,                 WeaponType.Crossbow));

        // ── Hammer ────────────────────────────────────────────────────────────
        db.hammerUnlocks.Add(Make("HammerChargeDamageReductionUnlock", "Unlock_HammerChargeBarrier",      AugmentId.HammerChargeDamageReductionUnlock, WeaponType.Hammer));
        db.hammerUnlocks.Add(Make("HammerFreeze_Extraordinary",        "Unlock_HammerFreeze",             AugmentId.HammerFreezeUnlock,                WeaponType.Hammer));
        db.hammerUnlocks.Add(Make("HammerAoeRadius_Extraordinary",     "Unlock_HammerAoeRadius",          AugmentId.HammerAoeRadiusUnlock,             WeaponType.Hammer));

        EditorUtility.SetDirty(db);

        // Remove unlock augments from AugmentDatabase.tier1Augments/tier2Augments/tier3Augments
        // so they don't appear in regular tier offers.
        CleanRegularAugmentDatabase();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[UnlockAugmentSeeder] Done — unlock augment assets created/updated and database populated.");
    }

    /// <summary>
    /// Adds 5 new simple Hammer unlock augments (bleed, lifesteal, knockback, magnet range,
    /// guaranteed crit on full charge) and rebuilds db.hammerUnlocks from the 3 existing assets
    /// (loaded from their current location) plus these 5 — this also fixes a pre-existing data bug
    /// where Unlock_HammerAoeRadius was listed twice in hammerUnlocks instead of a distinct 3rd entry.
    ///
    /// Run via: Tools ▶ Augments ▶ Add Hammer Unlock Augments
    /// </summary>
    [MenuItem("Tools/Augments/Add Hammer Unlock Augments")]
    public static void AddHammerUnlockAssets()
    {
        EnsureFolder(HammerOutputFolder);

        var db = AssetDatabase.LoadAssetAtPath<UnlockAugmentDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogError($"[UnlockAugmentSeeder] UnlockAugmentDatabase not found at '{DatabasePath}'.");
            return;
        }

        var barrier   = AssetDatabase.LoadAssetAtPath<UnlockAugmentDefinition>($"{HammerOutputFolder}/Unlock_HammerChargeBarrier.asset");
        var freeze    = AssetDatabase.LoadAssetAtPath<UnlockAugmentDefinition>($"{HammerOutputFolder}/Unlock_HammerFreeze.asset");
        var aoeRadius = AssetDatabase.LoadAssetAtPath<UnlockAugmentDefinition>($"{HammerOutputFolder}/Unlock_HammerAoeRadius.asset");
        if (barrier == null || freeze == null || aoeRadius == null)
        {
            Debug.LogError("[UnlockAugmentSeeder] One or more existing Hammer unlock assets not found under "
                          + $"'{HammerOutputFolder}'. Aborting so hammerUnlocks isn't rebuilt with missing entries.");
            return;
        }

        var bleed = MakeNew(
            "Unlock_HammerBleed", AugmentId.HammerBleedUnlock, WeaponType.Hammer,
            "Serrated Maul",
            "Hammer heavy hits also cause bleeding, dealing 5% of the hit's damage per stack over time.",
            0.05f);

        var lifesteal = MakeNew(
            "Unlock_HammerLifesteal", AugmentId.HammerLifestealUnlock, WeaponType.Hammer,
            "Vampiric Might",
            "Heal for 15% of hammer heavy damage dealt.",
            0.15f);

        var knockback = MakeNew(
            "Unlock_HammerKnockback", AugmentId.HammerKnockbackUnlock, WeaponType.Hammer,
            "Earthbreaker",
            "Hammer heavy hits knock enemies back 75% harder.",
            0.75f);

        var magnetRange = MakeNew(
            "Unlock_HammerMagnetRange", AugmentId.HammerMagnetRangeUnlock, WeaponType.Hammer,
            "Gravity Well",
            "Increases the hammer's charge magnet pull radius by 50%.",
            0.5f);

        var guaranteedCrit = MakeNew(
            "Unlock_HammerGuaranteedCrit", AugmentId.HammerGuaranteedCritOnFullChargeUnlock, WeaponType.Hammer,
            "Perfect Swing",
            "Fully-charged hammer slams always land as a critical hit.",
            1.5f);

        db.hammerUnlocks.Clear();
        db.hammerUnlocks.Add(barrier);
        db.hammerUnlocks.Add(freeze);
        db.hammerUnlocks.Add(aoeRadius);
        db.hammerUnlocks.Add(bleed);
        db.hammerUnlocks.Add(lifesteal);
        db.hammerUnlocks.Add(knockback);
        db.hammerUnlocks.Add(magnetRange);
        db.hammerUnlocks.Add(guaranteedCrit);

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[UnlockAugmentSeeder] Done — 5 new Hammer unlock augments created and hammerUnlocks rebuilt (duplicate entry fixed).");
    }

    private static UnlockAugmentDefinition MakeNew(
        string outputAssetName,
        AugmentId id,
        WeaponType weaponType,
        string displayName,
        string description,
        float value)
    {
        string outputPath = $"{HammerOutputFolder}/{outputAssetName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<UnlockAugmentDefinition>(outputPath);
        if (existing != null)
        {
            existing.id          = id;
            existing.weaponType  = weaponType;
            existing.displayName = displayName;
            existing.description = description;
            existing.value       = value;
            EditorUtility.SetDirty(existing);
            Debug.Log($"[UnlockAugmentSeeder] Updated existing: {outputAssetName}");
            return existing;
        }

        var def = ScriptableObject.CreateInstance<UnlockAugmentDefinition>();
        def.id          = id;
        def.weaponType  = weaponType;
        def.displayName = displayName;
        def.description = description;
        def.value        = value;
        def.rarity       = 1;
        def.tier         = 1;

        AssetDatabase.CreateAsset(def, outputPath);
        Debug.Log($"[UnlockAugmentSeeder] Created: {outputPath}");
        return def;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UnlockAugmentDefinition Make(
        string sourceAssetName,
        string outputAssetName,
        AugmentId id,
        WeaponType weaponType)
    {
        string outputPath = $"{OutputFolder}/{outputAssetName}.asset";

        // Reuse existing UnlockAugmentDefinition if already created
        var existing = AssetDatabase.LoadAssetAtPath<UnlockAugmentDefinition>(outputPath);
        if (existing != null)
        {
            existing.id         = id;
            existing.weaponType = weaponType;
            EditorUtility.SetDirty(existing);
            Debug.Log($"[UnlockAugmentSeeder] Updated existing: {outputAssetName}");
            return existing;
        }

        var def = ScriptableObject.CreateInstance<UnlockAugmentDefinition>();
        def.id         = id;
        def.weaponType = weaponType;

        // Copy display data from the original AugmentDefinition asset
        string sourcePath = $"{SourceFolder}/{sourceAssetName}.asset";
        var source = AssetDatabase.LoadAssetAtPath<AugmentDefinition>(sourcePath);
        if (source != null)
        {
            def.displayName  = source.displayName;
            def.description  = source.description;
            def.icon         = source.icon;
            def.value        = source.value;
            def.baseWeight   = source.baseWeight;
        }
        else
        {
            Debug.LogWarning($"[UnlockAugmentSeeder] Source asset not found: '{sourcePath}'. " +
                             "displayName/description/icon will be empty — fill in the Inspector.");
        }

        AssetDatabase.CreateAsset(def, outputPath);
        Debug.Log($"[UnlockAugmentSeeder] Created: {outputPath}");
        return def;
    }

    private static void CleanRegularAugmentDatabase()
    {
        const string augDbPath = "Assets/Augments/AugmentDatabase.asset";
        var augDb = AssetDatabase.LoadAssetAtPath<AugmentDatabase>(augDbPath);
        if (augDb == null)
        {
            Debug.LogWarning("[UnlockAugmentSeeder] AugmentDatabase not found — skipping tier1Augments/tier2Augments/tier3Augments cleanup.");
            return;
        }

        // IDs that belong to the unlock pool and must NOT be in tier1Augments/tier2Augments/tier3Augments
        var unlockIds = new System.Collections.Generic.HashSet<AugmentId>
        {
            AugmentId.ChargedLongbowAoeUnlock,
            AugmentId.TripleArrowUnlock,
            AugmentId.LongbowFreezeUnlock,
            AugmentId.FireArrowUnlock,
            AugmentId.PoisonArrowUnlock,
            AugmentId.CrossbowBoltPierce,
            AugmentId.CrossbowBoltBleed,
            AugmentId.HammerChargeDamageReductionUnlock,
            AugmentId.HammerFreezeUnlock,
            AugmentId.HammerAoeRadiusUnlock,
        };

        System.Func<AugmentDefinition, bool> shouldRemove = a =>
        {
            if (a == null) return true;
            if (unlockIds.Contains(a.id)) return true;
            return false;
        };
        int removed = augDb.tier1Augments.RemoveAll(a => shouldRemove(a));
        removed += augDb.tier2Augments.RemoveAll(a => shouldRemove(a));
        removed += augDb.tier3Augments.RemoveAll(a => shouldRemove(a));

        if (removed > 0)
        {
            EditorUtility.SetDirty(augDb);
            Debug.Log($"[UnlockAugmentSeeder] Removed {removed} unlock entries from AugmentDatabase.tier1Augments/tier2Augments/tier3Augments.");
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
