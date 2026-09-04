using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World Boss group fight (planning Task 7.3). A server-side authoritative boss state that
/// member residents apply damage to and that is broadcast to all members via
/// <see cref="EnemyStateSync"/> so everyone sees the same health/pose. Authoritative server
/// decrements health on validated damages (anti-cheat hooks in Task 7.4).
/// </summary>
public sealed class BossEvent
{
    public string BossId;
    public int NetId;
    public float MaxHealth;
    public float Health;
    public bool IsActive;
    public Vector3 Position;

    private readonly List<int> _memberIds = new List<int>();

    public BossEvent(string bossId, int netId, float maxHealth, Vector3 position)
    {
        BossId = bossId;
        NetId = netId;
        MaxHealth = maxHealth;
        Health = maxHealth;
        Position = position;
        IsActive = true;
    }

    public void AddMember(int sessionId)
    {
        if (!_memberIds.Contains(sessionId)) _memberIds.Add(sessionId);
    }

    public int MemberCount => _memberIds.Count;

    public void RemoveMember(int sessionId) => _memberIds.Remove(sessionId);

    /// <summary>Server-authoritative damage application. Returns false if the boss is down.</summary>
    public bool ApplyDamage(float amount)
    {
        if (!IsActive) return false;
        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f) IsActive = false;
        return IsActive;
    }

    /// <summary>Broadcast the boss snapshot to all members.</summary>
    public void Broadcast(GameServer server)
    {
        if (server == null) return;
        var snap = new EnemyStateSync.EnemySnapshot
        {
            NetId = NetId,
            Position = Position,
            RotationY = 0f,
            Health = Health,
            MaxHealth = MaxHealth,
            AttackTick = 0,
            Type = 255
        };
        var batch = new List<EnemyStateSync.EnemySnapshot>();
        batch.Add(snap);
        var msg = EnemyStateSync.Pack(batch);
        for (int i = 0; i < _memberIds.Count; i++)
        {
            PlayerSession session = Found(server, _memberIds[i]);
            if (session != null) server.SendTo(session, msg);
        }
    }

    private static PlayerSession Found(GameServer server, int id)
    {
        foreach (var s in server.Sessions)
            if (s.Id == id) return s;
        return null;
    }
}