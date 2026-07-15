using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Enemy))]
public class EnemyEditor : Editor
{
    private static readonly string[] MageOnlyProperties =
    {
        "mageProjectilePrefab",
        "mageRangedFireInterval",
        "mageProjectileSpeed",
        "mageUseAttackPowerForProjectile",
        "mageProjectileDamageOverride",
        "mageProjectileMaxLifetime",
        "mageProjectilePivot",
        "mageProjectileSpawnPoint",
        "mageProjectileSpawnOffset",
        "mageProjectilePooler",
        "mageProjectileFireRateMultiplier"
    };

    private static readonly string[] ExcludedFromDefaultDraw =
    {
        "m_Script",
        "enemyType",
        "mageProjectilePrefab",
        "mageRangedFireInterval",
        "mageProjectileSpeed",
        "mageUseAttackPowerForProjectile",
        "mageProjectileDamageOverride",
        "mageProjectileMaxLifetime",
        "mageProjectilePivot",
        "mageProjectileSpawnPoint",
        "mageProjectileSpawnOffset",
        "mageProjectilePooler",
        "mageProjectileFireRateMultiplier"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
        if (scriptProperty != null)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(scriptProperty);
            EditorGUI.EndDisabledGroup();
        }

        SerializedProperty enemyTypeProperty = serializedObject.FindProperty("enemyType");
        EditorGUILayout.PropertyField(enemyTypeProperty);

        DrawPropertiesExcluding(serializedObject, ExcludedFromDefaultDraw);

        if (enemyTypeProperty != null && enemyTypeProperty.enumValueIndex == (int)Enemy.EnemyType.Mage)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mage Ranged Attack", EditorStyles.boldLabel);

            for (int i = 0; i < MageOnlyProperties.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(MageOnlyProperties[i]);
                if (property != null)
                    EditorGUILayout.PropertyField(property, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
