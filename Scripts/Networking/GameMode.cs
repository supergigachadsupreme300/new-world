using System;

/// <summary>
/// Game mode definitions for Task 7.3. A <see cref="GameModeDefinition"/> describes the
/// parametrics of each lobby type (Solo / Co-op / Invasion / Arena / World Boss): player
/// capacity, whether it is PvP, hostile PvP, and boss involvement. Pure data, no behaviour —
/// the <see cref="NetLobby"/> consumes these to gate join/mode selection.
/// </summary>
public static class GameMode
{
    public enum Type : byte
    {
        Solo = 0,
        Coop = 1,
        Invasion = 2,
        Arena = 3,
        WorldBoss = 4
    }

    /// <summary>Parametrics for one lobby/game-mode type.</summary>
    public sealed class Definition
    {
        public Type Mode;
        public string DisplayName;
        public int MinPlayers;
        public int MaxPlayers;
        public bool IsPvP;
        public bool IsHostile;     // invasion: other players are enemies
        public bool UsesBoss;      // world boss fight
        public bool Matchmade;     // matchmaking finds candidates (arena)

        public Definition(Type mode, string name, int min, int max,
            bool pvp = false, bool hostile = false, bool boss = false, bool matchmade = false)
        {
            Mode = mode;
            DisplayName = name;
            MinPlayers = min;
            MaxPlayers = max;
            IsPvP = pvp;
            IsHostile = hostile;
            UsesBoss = boss;
            Matchmade = matchmade;
        }
    }

    /// <summary>Well-known definitions for each mode.</summary>
    public static Definition For(Type mode)
    {
        switch (mode)
        {
            case Type.Solo: return new Definition(Type.Solo, "Solo", 1, 1);
            case Type.Coop: return new Definition(Type.Coop, "Co-op", 2, 4, pvp: false, matchmade: false);
            case Type.Invasion: return new Definition(Type.Invasion, "Invasion", 1, 6, pvp: true, hostile: true);
            case Type.Arena: return new Definition(Type.Arena, "Arena", 2, 8, pvp: true, matchmade: true);
            case Type.WorldBoss: return new Definition(Type.WorldBoss, "World Boss", 4, 16, pvp: false, boss: true);
            default: return new Definition(Type.Solo, "Solo", 1, 1);
        }
    }

    /// <summary>True if <paramref name="count"/> fits the capacity of <paramref name="def"/>.</summary>
    public static bool Fits(Definition def, int count) =>
        def != null && count >= def.MinPlayers && count <= def.MaxPlayers;
}