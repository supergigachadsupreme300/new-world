using UnityEngine;

/// <summary>
/// Passive-perk effect (game-design §3.3): when its skill is learned (or restored from a save) this
/// registers <see cref="Amount"/> of <see cref="Perk"/> into the owner's <see cref="PassivePerkManager"/>.
/// Always-on once applied; percent kinds carry their percent value (5 = +5%), flat kinds raw points.
/// Replaces the old <see cref="StatBuffEffect"/> flat stat buffs so tree passives read as themed
/// perks instead of anonymous "+N stat" bumps.
/// </summary>
[System.Serializable]
public sealed class PassivePerkEffect : IEffect
{
    public PassivePerkType Perk;
    public float Amount;

    public void Execute(SkillContext ctx)
    {
        if (ctx == null || ctx.User == null) return;
        var pm = ctx.User.GetComponent<PassivePerkManager>();
        if (pm == null) pm = ctx.User.AddComponent<PassivePerkManager>();
        pm.AddPerk(Perk, Amount);
    }
}