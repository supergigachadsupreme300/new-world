using System;
using UnityEngine;

/// <summary>
/// Loot synchronization (planning Task 7.2). The server is authoritative over shared world
/// loot (spawn / collect / despawn); clients that pick up or that observe a drop inform the
/// server, which broadcasts the resulting state so every client agrees and no double-claim
/// occurs. Events are lightweight ops on the <c>"loot"</c> channel.
/// </summary>
public static class LootSync
{
    public const string Channel = "loot";

    public enum Op : byte { Spawn = 0, Collect = 1, Despawn = 2 }

    /// <summary>Server-authoritative loot event.</summary>
    public struct LootEvent
    {
        public Op Operation;
        public int NetId;         // shared drop id on the server
        public Vector3 Position;
        public string ItemId;     // collected/spawned item (ItemDatabase id)
        public int Count;
        public int ClaimerSessionId;
    }

    public static NetMessage Pack(LootEvent e)
    {
        var w = new NetWriter();
        w.W((byte)e.Operation);
        w.W(e.NetId);
        w.W(e.Position);
        w.W(e.ItemId ?? "");
        w.W(e.Count);
        w.W(e.ClaimerSessionId);
        return new NetMessage(NetOp.LootSync, 0, Channel, w.ToArray());
    }

    public static LootEvent Unpack(NetMessage msg)
    {
        var e = new LootEvent();
        if (msg == null || msg.Data == null || msg.Data.Length == 0) return e;
        try
        {
            var r = new NetReader(msg.Data);
            e.Operation = (Op)r.ReadByte();
            e.NetId = r.ReadInt();
            e.Position = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            e.ItemId = r.ReadString();
            e.Count = r.ReadInt();
            e.ClaimerSessionId = r.ReadInt();
        }
        catch (Exception) { }
        return e;
    }

    /// <summary>Server broadcasts a loot event to all clients.</summary>
    public static void Broadcast(GameServer server, LootEvent e)
    {
        if (server == null || !server.IsRunning) return;
        server.Broadcast(Pack(e));
    }
}