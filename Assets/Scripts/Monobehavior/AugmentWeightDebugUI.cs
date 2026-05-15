using System.Text;
using TMPro;
using UnityEngine;

public class AugmentWeightDebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI outputText;
    [SerializeField] private float refreshInterval = 0.5f;

    private AugmentWeightSystem _weightSystem;
    private DungeonGenerator _dungeonGenerator;
    private float _nextRefresh;

    private void Awake()
    {
        TryResolveReferences();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        TryResolveReferences();
        if (outputText == null || _weightSystem == null) return;

        int floor = _dungeonGenerator != null ? _dungeonGenerator.CurrentFloor : 0;
        bool milestone = _weightSystem.IsMilestoneFloor(floor);

        var sb = new StringBuilder(512);
        sb.Append("<b>Floor: ").Append(floor);
        if (milestone) sb.Append(" [MILESTONE]");
        sb.AppendLine("</b>");
        sb.Append("Pity: ").Append(_weightSystem.OffersWithoutT3).Append(" offers w/o T3")
          .Append("  |  x").AppendFormat("{0:F2}", _weightSystem.CurrentPityMultiplier).AppendLine();
        sb.Append("Total Offers: ").AppendLine(_weightSystem.TotalOffers.ToString());
        sb.AppendLine("─────────────────────────────");

        var all = _weightSystem.AllAugments;
        for (int i = 0; i < all.Count; i++)
        {
            AugmentDefinition aug = all[i];
            if (aug == null) continue;
            float w = _weightSystem.GetEffectiveWeight(aug, floor);
            sb.Append("[T").Append(aug.rarity).Append("] ")
              .Append(aug.displayName).Append(": ")
              .AppendFormat("{0:F1}", w).AppendLine();
        }

        outputText.text = sb.ToString();
    }

    private void TryResolveReferences()
    {
        if (_weightSystem == null)
        {
            _weightSystem = AugmentWeightSystem.Instance;
            if (_weightSystem == null)
                _weightSystem = Object.FindAnyObjectByType<AugmentWeightSystem>();
        }
        if (_dungeonGenerator == null)
            _dungeonGenerator = Object.FindAnyObjectByType<DungeonGenerator>();
    }
}
