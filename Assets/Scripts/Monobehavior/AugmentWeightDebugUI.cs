using System.Text;
using TMPro;
using UnityEngine;

public class AugmentWeightDebugUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI outputText;
    [SerializeField] private float refreshInterval = 0.5f;

    private AugmentWeightSystem _weightSystem;
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

        var sb = new StringBuilder(512);
        sb.Append("<b>Total Offers: ").Append(_weightSystem.TotalOfferCount)
          .Append("  |  Regular: ").Append(_weightSystem.RegularOfferCount).AppendLine("</b>");
        sb.Append("Next offer: ").AppendLine(_weightSystem.IsNextOfferUnlock ? "[UNLOCK]" : "[REGULAR]");
        sb.Append("Since last T3: ").Append(_weightSystem.OffersSinceLastT3)
          .Append(" / ").Append(_weightSystem.PityThreshold);
        if (_weightSystem.IsPityActive) sb.Append("  <color=red>[PITY ACTIVE]</color>");
        sb.AppendLine();
        sb.AppendLine("─────────────────────────────");

        var all = _weightSystem.AllAugments;
        for (int i = 0; i < all.Count; i++)
        {
            AugmentDefinition aug = all[i];
            if (aug == null) continue;
            float w = _weightSystem.GetEffectiveWeight(aug);
            sb.Append("[T").Append(aug.rarity).Append("] ")
              .Append(aug.displayName).Append(": ")
              .AppendFormat("{0:F1}", w).AppendLine();
        }

        outputText.text = sb.ToString();
    }

    private void TryResolveReferences()
    {
        if (_weightSystem != null) return;
        _weightSystem = AugmentWeightSystem.Instance;
        if (_weightSystem == null)
            _weightSystem = Object.FindAnyObjectByType<AugmentWeightSystem>();
    }
}
