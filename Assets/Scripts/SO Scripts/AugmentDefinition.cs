using UnityEngine;

[CreateAssetMenu(fileName = "AugmentDefinition", menuName = "Scriptable Objects/Augment Definition")]
public class AugmentDefinition : ScriptableObject
{
    [Header("Identity")]
    public AugmentId id;
    public string displayName;
    [TextArea(2, 5)] public string description;

    [Header("Presentation")]
    public Sprite icon;

    [Header("Progression")]
    public int rarity = 1;
    public int tier = 1;

    [Header("Balance (Optional)")]
    public float value = 1f;
    public AnimationCurve scalingCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Mutation / picker")]
    [Tooltip("Oyuncuda otomatik radial ok mutasyonu açılmışsa augment havuzundan çıkar.")]
    public bool excludeFromAugmentPickerWhenRadialBowMutationComplete;
}
