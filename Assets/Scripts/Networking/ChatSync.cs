using System;
using UnityEngine;

/// <summary>
/// Chat/text communication (planning Task 7.2). The server receives a <see cref="NetOp.Chat"/>
/// message on the <c>"chat"</c> channel, optionally rate-limits it, and relays it to all other
/// sessions so everyone sees the same text stream. A thin async buffer lets UI poll inbound
/// lines instead of blocking.
/// </summary>
public static class ChatSync
{
    public const string Channel = "chat";
    public const int MaxLineLength = 256;

    // Client-inbound buffer (polled by chat UI).
    public static string[] Inbound = new string[0];

    public static NetMessage Pack(int sessionId, string text)
    {
        if (text != null && text.Length > MaxLineLength)
            text = text.Substring(0, MaxLineLength);
        var w = new NetWriter();
        w.W(text ?? "");
        return new NetMessage(NetOp.Chat, sessionId, Channel, w.ToArray());
    }

    public static string Unpack(NetMessage msg)
    {
        if (msg == null || msg.Data == null || msg.Data.Length == 0) return "";
        try
        {
            var r = new NetReader(msg.Data);
            return r.ReadString();
        }
        catch (Exception) { return ""; }
    }

    /// <summary>Server relay: echo the sender's line to all other sessions.</summary>
    public static void ServerRelay(GameServer server, PlayerSession sender, NetMessage msg)
    {
        if (server == null || sender == null) return;
        var text = Unpack(msg);
        if (string.IsNullOrEmpty(text)) return;
        var relay = new NetMessage(NetOp.Chat, sender.Id, Channel, msg.Data);
        foreach (var s in server.Sessions)
        {
            if (s == sender) continue;
            server.SendTo(s, relay);
        }
        Debug.Log("[Chat] " + (string.IsNullOrEmpty(sender.PlayerName) ? "anon" : sender.PlayerName) + ": " + text);
    }

    /// <summary>Client: deliver a received chat line into the inbound buffer.</summary>
    public static void ReceiveLine(NetMessage msg, string authorName)
    {
        var text = Unpack(msg);
        if (string.IsNullOrEmpty(text)) return;
        var line = authorName + ": " + text;
        var next = new string[Mathf.Min(Inbound.Length + 1, 40)];
        next[0] = line;
        for (int i = 0; i + 1 < next.Length; i++)
            next[i + 1] = Inbound[i];
        Inbound = next;
    }

    /// <summary>Client: clear the inbound chat buffer.</summary>
    public static void ClearInbound()
    {
        Inbound = new string[0];
    }
}