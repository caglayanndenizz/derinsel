using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TFT tarzı Cevher Sistemi göstergesi.
/// Sol panel örneği:
///
///   [BowIcon]  3
///   [2] > [4] > [6]
///
/// Her milestone badge'i, eşik aşılınca kendi tier rengine geçer.
/// Bow ikonu da aktif tier rengine boyandırılır.
/// </summary>
public class CevherSystemUI : MonoBehaviour
{
    [Header("Ana Eleman")]
    [Tooltip("Bow ikonu — aktif tier rengine boyanır.")]
    [SerializeField] private Image bowIcon;

    [Tooltip("Alınan bow augment sayısını gösterir. (örnek: '3')")]
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Kilometre Taşı Rozet Arka Planları")]
    [SerializeField] private Image milestone2Badge;
    [SerializeField] private Image milestone4Badge;
    [SerializeField] private Image milestone6Badge;

    [Header("Kilometre Taşı Sayı Metinleri")]
    [Tooltip("Rozet üzerindeki '2' metni.")]
    [SerializeField] private TextMeshProUGUI milestone2Label;
    [Tooltip("Rozet üzerindeki '4' metni.")]
    [SerializeField] private TextMeshProUGUI milestone4Label;
    [Tooltip("Rozet üzerindeki '6' metni.")]
    [SerializeField] private TextMeshProUGUI milestone6Label;

    [Header("Renkler")]
    [SerializeField] private Color inactiveColor     = new Color(0.22f, 0.22f, 0.22f, 1.00f);
    [SerializeField] private Color altinColor        = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    [SerializeField] private Color elmasColor        = new Color(0.00f, 0.75f, 1.00f, 1.00f);
    [SerializeField] private Color obsidyenColor     = new Color(0.55f, 0.00f, 1.00f, 1.00f);

    [Header("Referans")]
    [SerializeField] private PlayerAugmentController augmentController;

    private CevherTier _cachedTier  = (CevherTier)(-1);
    private int        _cachedCount = -1;

    private void Awake()
    {
        if (augmentController != null) return;
        Player p = Object.FindAnyObjectByType<Player>();
        if (p != null) augmentController = p.PlayerAugmentController;
    }

    private void OnEnable()
    {
        if (augmentController != null)
            augmentController.AugmentApplied += HandleAugmentApplied;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (augmentController != null)
            augmentController.AugmentApplied -= HandleAugmentApplied;
    }

    private void HandleAugmentApplied(AugmentDefinition _) => RefreshUI();

    public void RefreshUI()
    {
        if (augmentController == null) return;

        CevherTier tier  = augmentController.BowCevherTier;
        int        count = augmentController.BowCevherAugmentCount;

        if (tier == _cachedTier && count == _cachedCount) return;
        _cachedTier  = tier;
        _cachedCount = count;

        // Sayaç
        if (countText != null)
            countText.text = count.ToString();

        // Milestone rozetleri
        bool altinReached    = count >= PlayerAugmentController.CevherAltinThreshold;
        bool elmasReached    = count >= PlayerAugmentController.CevherElmasThreshold;
        bool obsidyenReached = count >= PlayerAugmentController.CevherObsidyenThreshold;

        ApplyMilestoneColor(milestone2Badge, milestone2Label, altinReached,    altinColor);
        ApplyMilestoneColor(milestone4Badge, milestone4Label, elmasReached,    elmasColor);
        ApplyMilestoneColor(milestone6Badge, milestone6Label, obsidyenReached, obsidyenColor);

        // Bow ikonu aktif tier rengini alır
        if (bowIcon != null)
            bowIcon.color = TierColor(tier);
    }

    private void ApplyMilestoneColor(Image badge, TextMeshProUGUI label, bool reached, Color activeColor)
    {
        Color c = reached ? activeColor : inactiveColor;
        if (badge != null) badge.color = c;
        if (label != null) label.color = c;
    }

    private Color TierColor(CevherTier tier)
    {
        switch (tier)
        {
            case CevherTier.Altin:    return altinColor;
            case CevherTier.Elmas:    return elmasColor;
            case CevherTier.Obsidyen: return obsidyenColor;
            default:                  return inactiveColor;
        }
    }
}
