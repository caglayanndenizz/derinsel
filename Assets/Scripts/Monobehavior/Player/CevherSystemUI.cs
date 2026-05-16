using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CevherSystemUI : MonoBehaviour
{
    [Header("Tier Icons (sırayla: Kömür, Altın, Elmas, Obsidyen)")]
    [SerializeField] private Image komurIcon;
    [SerializeField] private Image altinIcon;
    [SerializeField] private Image elmasIcon;
    [SerializeField] private Image obsidyenIcon;

    [Header("Tier Renkleri")]
    [SerializeField] private Color inactiveColor     = new Color(0.18f, 0.18f, 0.18f, 0.55f);
    [SerializeField] private Color completedColor    = new Color(0.65f, 0.65f, 0.65f, 0.80f);
    [SerializeField] private Color komurActiveColor  = new Color(0.45f, 0.45f, 0.45f, 1.00f);
    [SerializeField] private Color altinActiveColor  = new Color(1.00f, 0.84f, 0.00f, 1.00f);
    [SerializeField] private Color elmasActiveColor  = new Color(0.00f, 0.75f, 1.00f, 1.00f);
    [SerializeField] private Color obsidyenActiveColor = new Color(0.55f, 0.00f, 1.00f, 1.00f);

    [Header("İlerleme Metni (opsiyonel)")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Referans")]
    [SerializeField] private PlayerAugmentController augmentController;

    private CevherTier _lastTier  = (CevherTier)(-1);
    private int        _lastCount = -1;

    private void Awake()
    {
        if (augmentController != null) return;
        Player player = Object.FindAnyObjectByType<Player>();
        if (player != null)
            augmentController = player.PlayerAugmentController;
    }

    private void OnEnable()
    {
        if (augmentController != null)
            augmentController.AugmentApplied += OnAugmentApplied;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (augmentController != null)
            augmentController.AugmentApplied -= OnAugmentApplied;
    }

    private void OnAugmentApplied(AugmentDefinition _) => RefreshUI();

    private void RefreshUI()
    {
        if (augmentController == null) return;

        CevherTier tier  = augmentController.BowCevherTier;
        int        count = augmentController.BowCevherAugmentCount;

        if (tier == _lastTier && count == _lastCount) return;
        _lastTier  = tier;
        _lastCount = count;

        SetTierIcon(komurIcon,    CevherTier.Komur,    tier, komurActiveColor);
        SetTierIcon(altinIcon,    CevherTier.Altin,    tier, altinActiveColor);
        SetTierIcon(elmasIcon,    CevherTier.Elmas,    tier, elmasActiveColor);
        SetTierIcon(obsidyenIcon, CevherTier.Obsidyen, tier, obsidyenActiveColor);

        RefreshProgressText(tier, count);
    }

    private void SetTierIcon(Image icon, CevherTier thisTier, CevherTier currentTier, Color activeColor)
    {
        if (icon == null) return;

        if ((int)currentTier > (int)thisTier)
            icon.color = completedColor;
        else if (currentTier == thisTier)
            icon.color = activeColor;
        else
            icon.color = inactiveColor;
    }

    private void RefreshProgressText(CevherTier tier, int count)
    {
        if (progressText == null) return;

        switch (tier)
        {
            case CevherTier.Komur:
                progressText.text = $"{count} / {PlayerAugmentController.CevherAltinThreshold}";
                break;
            case CevherTier.Altin:
                progressText.text = $"{count} / {PlayerAugmentController.CevherElmasThreshold}";
                break;
            case CevherTier.Elmas:
                progressText.text = $"{count} / {PlayerAugmentController.CevherObsidyenThreshold}";
                break;
            case CevherTier.Obsidyen:
                progressText.text = "OBSIDYEN";
                break;
        }
    }
}
