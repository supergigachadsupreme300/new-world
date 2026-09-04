using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Lightweight, dependency-free network message format (planning Task 7.1). A message is an
/// opcode plus typed payload, serialized to/from a byte buffer with a small <see cref="Writer"/> /
/// <see cref="Reader"/> pair. No external netcode package is required — broadcast/loopback RPC
/// and the UDP transport both use this shape, and the format is stable enough to swap transport
/// underneath without touching callers.
/// </summary>
public sealed class NetMessage
{
    public NetOp Op;
    public int SessionId;
    public string Channel = "";          // optional sub-stream ("state"/"chat"/"chunk")
    public byte[] Data = new byte[0];

    public NetMessage() { }
    public NetMessage(NetOp op, int sessionId, string channel, byte[] data)
    {
        Op = op; SessionId = sessionId; Channel = channel ?? ""; Data = data ?? new byte[0];
    }

    public byte[] Serialize()
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)Op);
            w.Write(SessionId);
            w.Write(Channel ?? "");
            w.Write(Data.Length);
            w.Write(Data);
            return ms.ToArray();
        }
    }

    public static NetMessage Deserialize(byte[] buffer)
    {
        using (var ms = new MemoryStream(buffer))
        using (var r = new BinaryReader(ms))
        {
            var m = new NetMessage();
            m.Op = (NetOp)r.ReadByte();
            m.SessionId = r.ReadInt32();
            m.Channel = r.ReadString();
            int len = r.ReadInt32();
            m.Data = len > 0 ? r.ReadBytes(len) : new byte[0];
            return m;
        }
    }
}

/// <summary>Message opcodes routed by the server/client dispatchers.</summary>
public enum NetOp : byte
{
    Handshake = 0,
    Heartbeat = 1,
    PlayerState = 2,
    ChunkData = 3,
    Chat = 4,
    Disconnect = 5,
    LobbyJoin = 6,
    LobbyLeave = 7
}

/// <summary>
/// Compact binary writer helper (little-endian, fixed layout). Used by systems that want to
/// pack small payloads (player state, chunk snapshots) without JSON overhead.
/// </summary>
public sealed class NetWriter
{
    private readonly MemoryStream _ms = new MemoryStream(64);

    public void W(float v) { var b = BitConverter.GetBytes(v); _ms.Write(b, 0, b.Length); }
    public void W(int v) { var b = BitConverter.GetBytes(v); _ms.Write(b, 0, b.Length); }
    public void W(uint v) { var b = BitConverter.GetBytes(v); _ms.Write(b, 0, b.Length); }
    public void W(byte v) { _ms.WriteByte(v); }
    public void W(short v) { var b = BitConverter.GetBytes(v); _ms.Write(b, 0, b.Length); }
    public void W(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
        W(bytes.Length);
        _ms.Write(bytes, 0, bytes.Length);
    }
    public void W(byte[] bytes)
    {
        W(bytes != null ? bytes.Length : 0);
        if (bytes != null) _ms.Write(bytes, 0, bytes.Length);
    }
    public void W(Vector3 v) { W(v.x); W(v.y); W(v.z); }

    public byte[] ToArray() => _ms.ToArray();
}

/// <summary>
/// Reader twin for <see cref="NetWriter"/>. Read in the same order written.
/// </summary>
public sealed class NetReader
{
    private readonly MemoryStream _ms;

    public NetReader(byte[] data)
    {
        _ms = new MemoryStream(data ?? new byte[0]);
    }

    private bool Can(int n) => _ms.Position + n <= _ms.Length;

    public float ReadFloat() { var b = ReadBytes(4); return BitConverter.ToSingle(b, 0); }
    public int ReadInt() { var b = ReadBytes(4); return BitConverter.ToInt32(b, 0); }
    public uint ReadUInt() { var b = ReadBytes(4); return BitConverter.ToUInt32(b, 0); }
    public byte ReadByte() { return (byte)_ms.ReadByte(); }
    public short ReadShort() { var b = ReadBytes(2); return BitConverter.ToInt16(b, 0); }
    public string ReadString()
    {
        int n = ReadInt();
        if (n < 0 || !Can(n)) return "";
        return System.Text.Encoding.UTF8.GetString(ReadBytes(n));
    }
    public byte[] ReadBytes(int count)
    {
        var b = new byte[count];
        int read = _ms.Read(b, 0, count);
        if (read < count) Array.Resize(ref b, read);
        return b;
    }
    public bool CanRead => _ms.Position < _ms.Length;
}