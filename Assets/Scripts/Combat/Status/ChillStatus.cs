using UnityEngine;

/// <summary>Chill-to-Frost cold build gauge (§3.7). Attached to the hit target's root.</summary>
public class ChillStatus : MonoBehaviour
{
    public const int FrostStackThreshold = 5;

    public const int WetStackGain = 2;

    private int _cold;
    private bool _frozen;

    public int Cold => _cold;

    public bool IsFrozen => _frozen;

    public static ChillStatus Apply(GameObject target)
    {
        if (target == null) return null;
        var root = target.transform.root.gameObject;

        bool wet = WetStatus.IsWet(root);
        var chill = root.GetComponent<ChillStatus>();
        if (chill == null)
            chill = root.AddComponent<ChillStatus>();

        chill._cold += wet ? WetStackGain : 13;

        // Reaching the threshold converts the chilly buildup into a full Frost freeze (§3.7):
        // target locks into the heavy 50% slow for the same duration the SpellCaster frost hit
        // applies. The gauge stays put while frozen so the freeze lingers until Melt or expiry.
        if (!chill._frozen && chill._cold >= FrostStackThreshold)
        {
            chill._frozen = true;
            var rootEnemy = root.GetComponent<EnemyController>();
            if (rootEnemy != null)
                rootEnemy.ApplySlow(0.5f, 3.5f);
        }
        return chill;
    }

    private void Update()
    {
        // Freeze decays back out on its own; a lone chill stack without follow-up also
        // drops off over time so the gauge doesn't sit at 4/5 forever. (Fire Melt resets
        // instantly via ChillStatus.Melt.)
        _cold = Mathf.Max(0, _cold - 1);
        if (_frozen)
        {
            _cold = 0;
            Destroy(this);
        }
    }

    public static void Melt(GameObject target)
    {
        if (target == null) return;
        var root = target.transform.root.gameObject;
        var chill = root.GetComponent<ChillStatus>();
        if (chill == null) return;

        chill._cold = 0;
        chill._frozen = false;
        Destroy(chill);
    }
}
