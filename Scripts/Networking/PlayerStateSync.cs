using System;
using UnityEngine;

/// <summary>
/// Player position/action synchronization with client prediction + server reconciliation
/// (planning Task 7.2). Clients send their *predicted* position/rotation on the
/// <c>"state"</c> channel; the server stores the authoritative position on the
/// <see cref="PlayerSession"/> and re-broadcasts the reconciling state to all other clients,
/// which render/move a remote avatar toward it. Actions ride <c>NetOp.PlayerAction</c>.
/// </summary>
public static class PlayerStateSync
{
    public const string Channel = "state";
    public const string ActionChannel = "action";

    // -------------------------------------------------------------
    //  Pack / unpack
    // -------------------------------------------------------------

    /// <summary>Pack a predicted player state from the local client.</summary>
    public static NetMessage PackState(int sessionId, Vector3 predicted, float rotationY)
    {
        var w = new NetWriter();
        w.W(predicted);
        w.W(rotationY);
        return new NetMessage(NetOp.PlayerState, sessionId, Channel, w.ToArray());
    }

    /// <summary>Parse a (predicted) player state payload.</summary>
    public static void UnpackState(NetMessage msg, out Vector3 position, out float rotationY)
    {
        position = Vector3.zero;
        rotationY = 0f;
        if (msg == null || msg.Data == null) return;
        try
        {
            var r = new NetReader(msg.Data);
            position = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            rotationY = r.ReadFloat();
        }
        catch (Exception) { }
    }

    // -------------------------------------------------------------
    //  Server side (authoritative)
    // -------------------------------------------------------------

    /// <summary>
    /// Server handler: adopt the client's predicted position as the new authoritative one,
    /// then re-broadcast the reconciled state to all OTHER sessions so they render the mover.
    /// </summary>
    public static void ServerReceive(GameServer server, PlayerSession sender, NetMessage msg)
    {
        if (server == null || sender == null) return;
        UnpackState(msg, out var pos, out var rotY);
        sender.RemotePosition = pos;
        sender.RemoteRotationY = rotY;

        // Re-broadcast authoritative state to everyone except the sender.
        var relay = new NetMessage(NetOp.PlayerState, sender.Id, Channel, msg.Data);
        foreach (var s in server.Sessions)
        {
            if (s == sender) continue;
            server.SendTo(s, relay);
        }
    }

    // -------------------------------------------------------------
    //  Client side (predict + reconcile)
    // -------------------------------------------------------------

    /// <summary>Fill channel-count smoothing into the remote avatar transform.</summary>
    public static float Smooth(float targetRotY, float currentRotY, float deltaTime)
    {
        float delta = targetRotY - currentRotY;
        while (delta > 180f) delta -= 360f;
        while (delta < -180f) delta += 360f;
        return currentRotY + delta * Mathf.Clamp01(deltaTime * 8f);
    }

    /// <summary>Move a remote avatar transform toward the authoritative position.</summary>
    public static void Reconcile(Transform remoteTransform, Vector3 target, float rotationY, float deltaTime)
    {
        if (remoteTransform == null) return;
        remoteTransform.position = Vector3.Lerp(remoteTransform.position, target, Mathf.Clamp01(deltaTime * 8f));
        float smoothed = Smooth(rotationY, remoteTransform.eulerAngles.y, deltaTime);
        remoteTransform.rotation = Quaternion.Euler(0f, smoothed, 0f);
    }

    // -------------------------------------------------------------
    //  Action sync (client intent → server validated)
    // -------------------------------------------------------------

    /// <summary>Pack a player action (e.g. attack/use). <paramref name="action"/> is a short code.</summary>
    public static NetMessage PackAction(int sessionId, short action, Vector3 target)
    {
        var w = new NetWriter();
        w.W(action);
        w.W(target);
        return new NetMessage(NetOp.PlayerAction, sessionId, ActionChannel, w.ToArray());
    }

    /// <summary>Unpack a player action payload.</summary>
    public static void UnpackAction(NetMessage msg, out short action, out Vector3 target)
    {
        action = 0;
        target = Vector3.zero;
        if (msg == null || msg.Data == null) return;
        try
        {
            var r = new NetReader(msg.Data);
            action = r.ReadShort();
            target = new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
        }
        catch (Exception) { }
    }
}