using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-side lobby aggregation (planning Task 7.3). Groups connected
/// <see cref="PlayerSession"/>s under a <see cref="GameMode.Definition"/>, enforces capacity,
/// and fronts the existing <see cref="NetOp.LobbyJoin"/> / <see cref="NetOp.LobbyLeave"/>
/// opcodes. Invitation-based join (co-op) is handled by slot reservation. Solo mode boots a
/// local server with no network peers (see <see cref="EnterSoloLocal"/>).
/// </summary>
public sealed class NetLobby
{
    public GameMode.Definition Mode;
    public int HostSessionId;
    public bool IsOpenToJoin;

    private readonly List<PlayerSession> _members = new List<PlayerSession>();
    private readonly List<int> _pendingInvites = new List<int>();

    public NetLobby(GameMode.Definition mode, int hostSessionId)
    {
		Mode = mode;
        HostSessionId = hostSessionId;
        IsOpenToJoin = mode != null && !mode.Matchmade;
    }

    /// <summary>Create + configure a lobby on the server. Returns the new lobby or null if unfit.</summary>
    public static NetLobby Create(GameServer server, GameMode.Type mode, int hostSessionId)
    {
        if (server == null) return null;
        var def = GameMode.For(mode);
        var lobby = new NetLobby(def, hostSessionId);
        var host = lobby.FindSession(server, hostSessionId);
        if (host != null) lobby.AddMember(host);
        return lobby;
    }

    public IReadOnlyList<PlayerSession> Members => _members;
    public int MemberCount => _members.Count;

    private PlayerSession FindSession(GameServer server, int id)
    {
        foreach (var s in server.Sessions)
            if (s.Id == id) return s;
        return null;
    }

    public bool AddMember(PlayerSession s)
    {
        if (s == null || _members.Contains(s)) return false;
        if (!GameMode.Fits(Mode, _members.Count + 1)) return false;
        if (!IsOpenToJoin && !_pendingInvites.Contains(s.Id)) return false;
        _members.Add(s);
        _pendingInvites.Remove((int)s.Id);
        return true;
    }

    public bool RemoveMember(int sessionId)
    {
        for (int i = 0; i < _members.Count; i++)
            if (_members[i].Id == sessionId) { _members.RemoveAt(i); return true; }
        return false;
    }

    /// <summary>Reserve a slot for an invitee (co-op invite). True if capacity allows.</summary>
    public bool Invite(GameServer server, int inviteeSessionId)
    {
        if (server == null) return false;
        if (_members.Count >= Mode.MaxPlayers) return false;
        if (_pendingInvites.Contains(inviteeSessionId)) return true;
        _pendingInvites.Add(inviteeSessionId);
        var invitee = FindSession(server, inviteeSessionId);
        if (invitee != null)
            server.SendTo(invitee, LobbyPack.JoinAck(Mode.Mode));
        return true;
    }

    public bool IsFull => _members.Count >= Mode.MaxPlayers;
    public bool IsReady => GameMode.Fits(Mode, _members.Count);

    /// <summary>
    /// Solo mode needs no network: boots a local, non-listening server with a single session.
    /// Caller may pass a server set up with a loopback transport; this just reserves the slot.
    /// </summary>
    public static bool EnterSoloLocal(GameServer server)
    {
        if (server == null) return false;
        var lobby = Create(server, GameMode.Type.Solo, 0);
        if (lobby == null) return false;
        lobby.IsOpenToJoin = false;
        return lobby.MemberCount == 1 && lobby.IsReady;
    }

    /// <summary>Notify all members of a lobby message payload.</summary>
    public void Broadcast(GameServer server, NetMessage msg)
    {
        if (server == null) return;
        foreach (var m in _members)
            server.SendTo(m, msg);
    }
}

/// <summary>Helpers for packing/unpacking lobby messages.</summary>
public sealed class LobbyPack
{
    public const string Channel = "lobby";

    public static NetMessage JoinAck(GameMode.Type mode)
    {
        var w = new NetWriter();
        w.W((byte)mode);
        return new NetMessage(NetOp.LobbyJoin, 0, Channel, w.ToArray());
    }

    public static GameMode.Type ReadJoin(NetMessage msg)
    {
        if (msg == null || msg.Data == null || msg.Data.Length == 0) return GameMode.Type.Solo;
        try { var r = new NetReader(msg.Data); return (GameMode.Type)r.ReadByte(); }
        catch (Exception) { return GameMode.Type.Solo; }
    }
}