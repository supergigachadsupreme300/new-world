using System;
using System.Net;
using UnityEngine;

/// <summary>
/// A player connection session hosted by <see cref="GameServer"/> (planning Task 7.1
/// "player session management"). Tracks the remote endpoint, an assigned session id, a
/// monotonic ack/heartbeat, and a coarse synchronized player position handled by the
/// state-sync layer (Task 7.2). Sessions are server-authoritative: the server owns timers
/// and decides liveness.
/// </summary>
public sealed class PlayerSession
{
    public int Id;
    public IPEndPoint Remote;
    public string PlayerName = "";
    public int AckCount;
    public bool IsAlive = true;

    // Replicated player state (filled by Task 7.2 state sync).
    public Vector3 RemotePosition;
    public float RemoteRotationY;

    public DateTime LastActivityUtc = DateTime.UtcNow;

    public PlayerSession(int id, IPEndPoint remote)
    {
        Id = id;
        Remote = remote;
        LastActivityUtc = DateTime.UtcNow;
    }

    public void NotifyActivity()
    {
        LastActivityUtc = DateTime.UtcNow;
        AckCount++;
    }

    /// <summary>True if no message arrived within the given timeout.</summary>
    public bool TimedOut(TimeSpan timeout) => (DateTime.UtcNow - LastActivityUtc) > timeout;
}