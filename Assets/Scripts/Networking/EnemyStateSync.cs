using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy state synchronization (planning Task 7.2). The server owns the authoritative enemy
/// list (AI, health, attacks) and broadcasts lightweight enemy snapshots on the <c>"enemy"</c>
/// channel; clients render remote enemy copies from those snapshots and feed superficial
/// transforms into their own sim. <see cref="EnemySyncState"/> is a plain struct so pools of
/// enemies can be serialized without tying into a live <see cref="EnemyController"/>.
/// </summary>
public static class EnemyStateSync
{
    public const string Channel = "enemy";
    public const int MaxEnemiesPerMessage = 48;

    /// <summary>Authoritative enemy snapshot (server side).</summary>
    public struct EnemySnapshot
    {
        public int NetId;
        public Vector3 Position;
        public float RotationY;
        public float Health;
        public float MaxHealth;
        public short AttackTick;   // incremented by the server after each validated attack
        public byte Type;          // designer enemy type id
    }

    // -------------------------------------------------------------
    //  Pack / unpack
    // -------------------------------------------------------------

    public static NetMessage Pack(List<EnemySnapshot> batch)
    {
        var w = new NetWriter();
        int n = Mathf.Min(batch.Count, MaxEnemiesPerMessage);
        w.W(n);
        for (int i = 0; i < n; i++)
        {
            var e = batch[i];
            w.W(e.NetId);
            w.W(e.Position);
            w.W(e.RotationY);
            w.W(e.Health);
            w.W(e.MaxHealth);
            w.W((short)e.AttackTick);
            w.W((byte)e.Type);
        }
        return new NetMessage(NetOp.EnemyState, 0, Channel, w.ToArray());
    }

    public static List<EnemySnapshot> Unpack(NetMessage msg)
    {
        var list = new List<EnemySnapshot>();
        if (msg == null || msg.Data == null || msg.Data.Length == 0) return list;
        try
        {
            var r = new NetReader(msg.Data);
            int count = Mathf.Clamp(r.ReadInt(), 0, MaxEnemiesPerMessage);
            for (int i = 0; i < count; i++)
            {
                var e = new EnemySnapshot();
                e.NetId = r.ReadInt();
                e.Position = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                e.RotationY = r.ReadFloat();
                e.Health = r.ReadFloat();
                e.MaxHealth = r.ReadFloat();
                e.AttackTick = r.ReadShort();
                e.Type = r.ReadByte();
                list.Add(e);
            }
        }
        catch (Exception)
        {
            // malformed batch: return what parsed so far
        }
        return list;
    }

    // -------------------------------------------------------------
    //  Server-side broadcast helper
    // -------------------------------------------------------------

    /// <summary>Broadcast an enemy batch to all clients. Returns count sent.</summary>
    public static int Broadcast(GameServer server, List<EnemySnapshot> batch)
    {
        if (server == null || !server.IsRunning || batch == null || batch.Count == 0) return 0;
        server.Broadcast(Pack(batch));
        return batch.Count;
    }
}