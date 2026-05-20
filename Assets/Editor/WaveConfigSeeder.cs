#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tüm WaveDefinition ve FloorWaveConfig asset'lerini otomatik oluşturur.
/// Çalıştır: Tools ▶ Waves ▶ Create Wave Config Assets
/// </summary>
public static class WaveConfigSeeder
{
    private const string OutputFolder = "Assets/Waves";

    [MenuItem("Tools/Waves/Create Wave Config Assets")]
    public static void CreateWaveConfigs()
    {
        EnsureFolder(OutputFolder);

        // ── Wave Definition'ları oluştur ──────────────────────────────────────

        // FLOORS 1-3 ─ Tutorial feel: önce ranged baskı, sonra melee
        WaveDefinition f1_w1 = MakeWave("Floor1-3_Wave1",
            (Enemy.EnemyType.Mage,    4),
            spawnDelay: 0.5f, exp: 1.0f, gold: 1.0f);

        WaveDefinition f1_w2 = MakeWave("Floor1-3_Wave2",
            (Enemy.EnemyType.Warrior, 2),
            (Enemy.EnemyType.Mage,    2),
            spawnDelay: 0.4f, exp: 1.2f, gold: 1.2f);

        WaveDefinition f1_w3 = MakeWave("Floor1-3_Wave3",
            (Enemy.EnemyType.Warrior, 2),
            (Enemy.EnemyType.Tanky,   1),
            (Enemy.EnemyType.Mage,    2),
            spawnDelay: 0.4f, exp: 1.5f, gold: 1.5f);

        // FLOORS 4-6 ─ Baskı artar: Tanky'ler çoğalıyor, Warrior tehlikeli
        WaveDefinition f4_w1 = MakeWave("Floor4-6_Wave1",
            (Enemy.EnemyType.Warrior, 2),
            (Enemy.EnemyType.Mage,    3),
            spawnDelay: 0.35f, exp: 1.3f, gold: 1.3f);

        WaveDefinition f4_w2 = MakeWave("Floor4-6_Wave2",
            (Enemy.EnemyType.Warrior, 3),
            (Enemy.EnemyType.Tanky,   1),
            (Enemy.EnemyType.Mage,    2),
            spawnDelay: 0.35f, exp: 1.5f, gold: 1.5f);

        WaveDefinition f4_w3 = MakeWave("Floor4-6_Wave3",
            (Enemy.EnemyType.Tanky,   2),
            (Enemy.EnemyType.Warrior, 2),
            (Enemy.EnemyType.Mage,    3),
            spawnDelay: 0.3f, exp: 1.8f, gold: 1.8f);

        // FLOORS 7-9 ─ Yüksek baskı: her wave kaotik, dopamin yüksek
        WaveDefinition f7_w1 = MakeWave("Floor7-9_Wave1",
            (Enemy.EnemyType.Warrior, 3),
            (Enemy.EnemyType.Mage,    4),
            spawnDelay: 0.25f, exp: 1.6f, gold: 1.6f);

        WaveDefinition f7_w2 = MakeWave("Floor7-9_Wave2",
            (Enemy.EnemyType.Tanky,   2),
            (Enemy.EnemyType.Warrior, 3),
            (Enemy.EnemyType.Mage,    2),
            spawnDelay: 0.25f, exp: 1.8f, gold: 1.8f);

        WaveDefinition f7_w3 = MakeWave("Floor7-9_Wave3",
            (Enemy.EnemyType.Tanky,   2),
            (Enemy.EnemyType.Warrior, 3),
            (Enemy.EnemyType.Mage,    4),
            spawnDelay: 0.2f, exp: 2.2f, gold: 2.2f);

        // FLOORS 10+ ─ Boss antesi: maksimum kaos, yüksek ödül
        WaveDefinition f10_w1 = MakeWave("Floor10Plus_Wave1",
            (Enemy.EnemyType.Warrior, 3),
            (Enemy.EnemyType.Mage,    5),
            spawnDelay: 0.2f, exp: 2.0f, gold: 2.0f);

        WaveDefinition f10_w2 = MakeWave("Floor10Plus_Wave2",
            (Enemy.EnemyType.Tanky,   2),
            (Enemy.EnemyType.Warrior, 4),
            (Enemy.EnemyType.Mage,    3),
            spawnDelay: 0.2f, exp: 2.2f, gold: 2.2f);

        WaveDefinition f10_w3 = MakeWave("Floor10Plus_Wave3",
            (Enemy.EnemyType.Tanky,   3),
            (Enemy.EnemyType.Warrior, 4),
            (Enemy.EnemyType.Mage,    4),
            spawnDelay: 0.15f, exp: 2.5f, gold: 2.5f);

        // ── FloorWaveConfig'leri oluştur ──────────────────────────────────────

        MakeConfig("Config_Floors1-3",  fromFloor: 1,  toFloor: 3,  f1_w1,  f1_w2,  f1_w3);
        MakeConfig("Config_Floors4-6",  fromFloor: 4,  toFloor: 6,  f4_w1,  f4_w2,  f4_w3);
        MakeConfig("Config_Floors7-9",  fromFloor: 7,  toFloor: 9,  f7_w1,  f7_w2,  f7_w3);
        MakeConfig("Config_Floors10Plus", fromFloor: 10, toFloor: -1, f10_w1, f10_w2, f10_w3);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WaveConfigSeeder] 12 WaveDefinition + 4 FloorWaveConfig oluşturuldu → Assets/Waves/");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WaveDefinition MakeWave(
        string assetName,
        float spawnDelay,
        float exp,
        float gold,
        params (Enemy.EnemyType type, int count)[] entries)
    {
        string path = $"{OutputFolder}/{assetName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
        if (existing != null)
        {
            existing.entries.Clear();
            foreach (var (type, count) in entries)
                existing.entries.Add(new WaveEntry { enemyType = type, count = count });
            existing.spawnDelayBetweenEnemies = spawnDelay;
            existing.expMultiplier  = exp;
            existing.goldMultiplier = gold;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var wave = ScriptableObject.CreateInstance<WaveDefinition>();
        foreach (var (type, count) in entries)
            wave.entries.Add(new WaveEntry { enemyType = type, count = count });
        wave.spawnDelayBetweenEnemies = spawnDelay;
        wave.expMultiplier  = exp;
        wave.goldMultiplier = gold;

        AssetDatabase.CreateAsset(wave, path);
        return wave;
    }

    // params overload without named spawnDelay (for cleaner call sites above)
    private static WaveDefinition MakeWave(
        string assetName,
        (Enemy.EnemyType type, int count) e1,
        float spawnDelay, float exp, float gold)
        => MakeWave(assetName, spawnDelay, exp, gold, e1);

    private static WaveDefinition MakeWave(
        string assetName,
        (Enemy.EnemyType type, int count) e1,
        (Enemy.EnemyType type, int count) e2,
        float spawnDelay, float exp, float gold)
        => MakeWave(assetName, spawnDelay, exp, gold, e1, e2);

    private static WaveDefinition MakeWave(
        string assetName,
        (Enemy.EnemyType type, int count) e1,
        (Enemy.EnemyType type, int count) e2,
        (Enemy.EnemyType type, int count) e3,
        float spawnDelay, float exp, float gold)
        => MakeWave(assetName, spawnDelay, exp, gold, e1, e2, e3);

    private static void MakeConfig(string assetName, int fromFloor, int toFloor, params WaveDefinition[] waves)
    {
        string path = $"{OutputFolder}/{assetName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<FloorWaveConfig>(path);
        if (existing != null)
        {
            existing.fromFloor = fromFloor;
            existing.toFloor   = toFloor;
            existing.waves     = new List<WaveDefinition>(waves);
            EditorUtility.SetDirty(existing);
            return;
        }

        var cfg = ScriptableObject.CreateInstance<FloorWaveConfig>();
        cfg.fromFloor = fromFloor;
        cfg.toFloor   = toFloor;
        cfg.waves     = new List<WaveDefinition>(waves);
        AssetDatabase.CreateAsset(cfg, path);
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
