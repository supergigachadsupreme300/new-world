using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arena matchmaking (planning Task 7.3). A lightweight server-side matcher: takes a candidate
/// pool of ready sessions and forms the largest possible match within a <see cref="GameMode"/>
/// capacity, notifying participants. Pure helper — the server UI decides when to call it.
/// </summary>
public static class Matchmaker
{
    /// <summary>Form a match from <paramref name="candidates"/> under the given mode. Returns a copy of the chosen members.</summary>
    public static List<PlayerSession> TryMatch(List<PlayerSession> candidates, GameMode.Type mode)
    {
        var def = GameMode.For(mode);
        var chosen = new List<PlayerSession>();
        if (def == null || !def.Matchmade) return chosen;
        for (int i = 0; i < def.MaxPlayers && i < candidates.Count; i++)
            chosen.Add(candidates[i]);
        // trim to a valid size (arena min 2)
        while (!GameMode.Fits(def, chosen.Count) && chosen.Count > 0)
            chosen.RemoveAt(chosen.Count - 1);
        return chosen;
    }

    /// <summary>Broadcast match formation to the chosen sessions and return count.</summary>
    public static int Announce(GameServer server, List<PlayerSession> match)
    {
        if (server == null || match == null || match.Count == 0) return 0;
        var w = new NetWriter();
        w.W(match.Count);
        foreach (var m in match)
        {
            w.W(m.Id);
            w.W(m.PlayerName ?? "");
        }
        var msg = new NetMessage(NetOp.LobbyJoin, 0, "match", w.ToArray());
        foreach (var m in match)
            server.SendTo(m, msg);
        return match.Count;
    }
}