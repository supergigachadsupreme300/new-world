using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

/// <summary>
/// Dedicated server bootstrap (planning Task 7.1 "GameServer.cs"). Owns an
/// <see cref="INetTransport"/>, a registry of <see cref="PlayerSession"/>s, and a fixed-tick
/// loop that routes messages by opcode. Transport-agnostic: pass any <see cref="INetTransport"/>.
/// The server is authoritative — it assigns session ids, times out dead sessions, and replies
/// to handshakes/heartbeats. Chunk generation broadcast lives in <see cref="ChunkSync"/>.
/// </summary>
public sealed class GameServer : IDisposable
{
    public INetTransport Transport { get; }
    public int TickRate { get; set; } = 20;
    public bool IsRunning { get; private set; }

    private readonly List<PlayerSession> _sessions = new List<PlayerSession>();
    private readonly Dictionary<IPEndPoint, PlayerSession> _byEndpoint = new Dictionary<IPEndPoint, PlayerSession>();
    private readonly object _syncLock = new object();
    private int _nextSessionId = 1;

    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Optional hook for systems interested in inbound messages (e.g. state sync).</summary>
    public Action<PlayerSession, NetMessage> OnMessage;

    public GameServer(INetTransport transport)
    {
        Transport = transport;
    }

    /// <summary>Bind + begin serving. Returns false if the transport could not start.</summary>
    public bool Start(string address, int port)
    {
        if (IsRunning) return true;
        if (Transport == null || !Transport.Start(address, port))
            return false;
        IsRunning = true;
        Debug.Log("[Net] GameServer started on " + address + ":" + port);
        return true;
    }

    /// <summary>Advance the server tick (poll transport, handle messages, drop timeouts).</summary>
    public void Tick()
    {
        if (!IsRunning || Transport == null) return;

        IPEndPoint remote;
        byte[] data;
        while (Transport.Poll(out remote, out data) && data != null)
        {
            NetMessage msg;
            try { msg = NetMessage.Deserialize(data); }
            catch (Exception) { continue; } // drop malformed

            HandleDatagram(remote, msg);
        }

        DropTimedOutSessions();
    }

    private void HandleDatagram(IPEndPoint remote, NetMessage msg)
    {
        lock (_syncLock)
        {
            _byEndpoint.TryGetValue(remote, out var session);
            if (msg.Op == NetOp.Handshake)
            {
                if (session == null)
                {
                    session = new PlayerSession(_nextSessionId++, remote);
                    _sessions.Add(session);
                    _byEndpoint[remote] = session;
                    var name = ReadName(msg.Data);
                    if (!string.IsNullOrEmpty(name)) session.PlayerName = name;
                    Debug.Log("[Net] Session " + session.Id + " joined (" + remote + ")");
                    SendTo(session, new NetMessage(NetOp.Handshake, session.Id, "net", new byte[] { 1 }));
                }
                session.NotifyActivity();
                return;
            }
            if (msg.Op == NetOp.Heartbeat)
            {
                session?.NotifyActivity();
                SendTo(session, new NetMessage(NetOp.Heartbeat, session != null ? session.Id : 0, "net", new byte[0]));
                return;
            }
            if (session == null) return; // must handshake first
            session.NotifyActivity();
            OnMessage?.Invoke(session, msg);
        }
    }

    /// <summary>Send a message to one session over the transport.</summary>
    public void SendTo(PlayerSession session, NetMessage msg)
    {
        if (session == null || !IsRunning || Transport == null) return;
        Transport.SendTo(session.Remote, msg.Serialize());
    }

    /// <summary>Broadcast a message to all live sessions.</summary>
    public void Broadcast(NetMessage msg)
    {
        if (!IsRunning) return;
        var bytes = msg.Serialize();
        lock (_syncLock)
        {
            foreach (var s in _sessions)
                if (s.IsAlive) Transport.SendTo(s.Remote, bytes);
        }
    }

    private void DropTimedOutSessions()
    {
        if (_sessions.Count == 0) return;
        lock (_syncLock)
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                var s = _sessions[i];
                if (!s.TimedOut(SessionTimeout))
                    continue;
                s.IsAlive = false;
                _byEndpoint.Remove(s.Remote);
                _sessions.RemoveAt(i);
                Debug.Log("[Net] Session " + s.Id + " timed out.");
            }
        }
    }

    public IReadOnlyList<PlayerSession> Sessions
    {
        get { lock (_syncLock) { return new List<PlayerSession>(_sessions); } }
    }

    private static string ReadName(byte[] data)
    {
        if (data == null || data.Length == 0) return "";
        try { return new NetReader(data).ReadString(); }
        catch (Exception) { return ""; }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        lock (_syncLock)
        {
            _sessions.Clear();
            _byEndpoint.Clear();
        }
        Transport?.Stop();
        Debug.Log("[Net] GameServer stopped.");
    }

    public void Dispose() => Stop();
}