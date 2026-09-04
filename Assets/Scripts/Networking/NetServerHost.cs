using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-side dedicated-server harness (planning Task 7.1 "dedicated server bootstrap").
/// Add to a server build object; boots a <see cref="GameServer"/> over
/// <see cref="UdpNetTransport"/> and ticks it once per frame. Guarded by the
/// <c>NEWWORLD_SERVER</c> symbol so normal (dedicated client) builds are untouched — define
/// that symbol only in the dedicated-server build configuration.
/// </summary>
public sealed class NetServerHost : MonoBehaviour
{
    [Header("Server")]
    public string BindAddress = "127.0.0.1";
    public int Port = 7777;
    [Tooltip("Tile sync broadcast radius around session positions.")]
    public int ChunkRadius = 3;
    public bool StartOnEnable = true;

    private GameServer _server;
    private UdpNetTransport _transport;
    private long _worldSeed = 12345L;
    private float _chunkTimer;
    private readonly List<NetLobby> _lobbies = new List<NetLobby>();

    public GameServer Server => _server;
    public bool IsRunning => _server != null && _server.IsRunning;

    /// <summary>Live snapshot of all active lobbies on this host (Phase 8 / Task 8.2 browser).</summary>
    public System.Collections.Generic.List<NetLobby> Lobbies
    {
        get { lock (_lobbyLock) { return new System.Collections.Generic.List<NetLobby>(_lobbies); } }
    }

    private readonly object _lobbyLock = new object();

    private void OnEnable()
    {
        if (StartOnEnable) StartServer();
    }

    public void StartServer()
    {
        #if NEWWORLD_SERVER
        _transport = new UdpNetTransport();
        _server = new GameServer(_transport);
        _server.OnMessage += HandleServerMessage;
        if (!_server.Start(BindAddress, Port))
        {
            Debug.LogWarning("[Net] Failed to start dedicated server.");
            _server = null;
            _transport = null;
            return;
        }
        _chunkTimer = 1f;
        #else
        Debug.LogWarning("[Net] NetServerHost disabled: build with NEWWORLD_SERVER defined.");
        #endif
    }

    private void Update()
    {
        #if NEWWORLD_SERVER
        if (_server == null) return;
        _server.Tick();

        // Periodically broadcast chunk heights to sessions, per Task 7.1.
        _chunkTimer -= Time.deltaTime;
        if (_chunkTimer <= 0f)
        {
            _chunkTimer = 2f;
            foreach (var s in _server.Sessions)
                ChunkSync.SendRegion(_server,
                    Mathf.RoundToInt(s.RemotePosition.x),
                    Mathf.RoundToInt(s.RemotePosition.z),
                    _worldSeed, ChunkRadius);
        }
        #endif
    }

    private void HandleServerMessage(PlayerSession session, NetMessage msg)
    {
        // Route inbound messages to the authoritative state-sync handlers (Task 7.2).
        switch (msg.Op)
        {
            case NetOp.PlayerState:
                {
                    // Anti-cheat (Task 7.4): reject teleports before reconciliation.
                    var prev = session.RemotePosition;
                    PlayerStateSync.UnpackState(msg, out var proposed, out var rotY);
                    float dt = Time.deltaTime > 1e-6f ? Time.deltaTime : 0.1f;
                    if (!AntiCheat.ValidatePosition(prev, proposed, dt, out var corrected))
                    {
                        // push the session to the plausible frontier instead of the claimed spot
                        corrected.y = Mathf.Max(corrected.y, 0f);
                        session.RemotePosition = corrected;
                        session.RemoteRotationY = rotY;
                        break;
                    }
                    PlayerStateSync.ServerReceive(_server, session, msg);
                }
                break;
            case NetOp.PlayerAction:
                // Validated in Task 7.4 anti-cheat; ack + relay for now.
                OnValidatedAction(session, msg);
                break;
            case NetOp.Chat:
                ChatSync.ServerRelay(_server, session, msg);
                break;
            case NetOp.LobbyJoin:
                HandleLobbyJoin(session, msg);
                break;
            case NetOp.LobbyLeave:
                LeaveAllLobbies(session);
                break;
        }
    }

    private void HandleLobbyJoin(PlayerSession session, NetMessage msg)
    {
        #if NEWWORLD_SERVER
        var mode = LobbyPack.ReadJoin(msg);
        var lobby = NetLobby.Create(_server, mode, session.Id);
        if (lobby != null)
        {
            lock (_lobbyLock)
            {
                for (int i = _lobbies.Count - 1; i >= 0; i--)
                    if (_lobbies[i].HostSessionId == session.Id)
                        _lobbies.RemoveAt(i);
                _lobbies.Add(lobby);
            }
            lobby.Broadcast(_server, LobbyPack.JoinAck(mode));
        }
        #endif
    }

    private void LeaveAllLobbies(PlayerSession session)
    {
        // Anti-cheat: free the session's action budget on departure.
        AntiCheat.Release(session.Id);
        lock (_lobbyLock)
        {
            for (int i = _lobbies.Count - 1; i >= 0; i--)
            {
                _lobbies[i].RemoveMember(session.Id);
                if (_lobbies[i].MemberCount == 0)
                    _lobbies.RemoveAt(i);
            }
        }
    }

    private void OnValidatedAction(PlayerSession session, NetMessage msg)
    {
        PlayerStateSync.UnpackAction(msg, out var action, out var target);
        if (action == 0) return;
        // Anti-cheat (Task 7.4): rate-limit incoming actions per session.
        if (!AntiCheat.AllowAction(session.Id, 1f)) return;
        var relay = new NetMessage(NetOp.PlayerAction, session.Id, PlayerStateSync.ActionChannel, msg.Data);
        foreach (var s in _server.Sessions)
        {
            if (s == session) continue;
            _server.SendTo(s, relay);
        }
    }

    private void OnDisable() => StopServer();

    public void StopServer()
    {
        if (_server != null) { _server.Dispose(); _server = null; }
        _transport = null;
    }

    private void OnDestroy() => StopServer();
}