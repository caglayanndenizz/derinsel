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
    private const string SourceFolder   = "Assets/Augments";
    private const string OutputFolder   = "Assets/Augments/Unlocks";
    private const string DatabasePath   = "Assets/Augments/UnlockAugmentDatabase.asset";

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
