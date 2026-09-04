using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Transport seam (planning Task 7.1). Cleaned from concrete socket code so the rest of the
/// networking layer (server, sessions, sync) doesn't care whether bytes travel over UDP, a
/// relay, or WebSockets. <see cref="UdpNetTransport"/> is a dependency-free managed fallback.
/// </summary>
public interface INetTransport : IDisposable
{
    bool IsRunning { get; }
    /// <summary>Bind + begin receiving. Returns false if the address/port is taken.</summary>
    bool Start(string address, int port);
    /// <summary>Send a raw datagram to an endpoint (address:port).</summary>
    void SendTo(IPEndPoint remote, byte[] data);
    /// <summary>Non-blocking: pop any received datagrams since the last poll.</summary>
    bool Poll(out IPEndPoint remote, out byte[] data);
    void Stop();
}

/// <summary>
/// Pure-managed UDP transport (no Unity netcode package required). One <see cref="Socket"/>
/// bound to a local endpoint; send/receive framed from pooled buffers. Suitable for a dedicated
/// server bootstrap and LAN/loopback sessions. Not encrypted — the anti-cheat layer (Task 7.4)
/// can wrap the byte streams before they reach this transport.
/// </summary>
public sealed class UdpNetTransport : INetTransport
{
    private Socket _socket;
    private bool _running;
    private byte[] _recvBuffer;
    private readonly Queue<KeyValuePair<IPEndPoint, byte[]>> _inbox = new Queue<KeyValuePair<IPEndPoint, byte[]>>();

    public bool IsRunning => _running;

    public bool Start(string address, int port)
    {
        try
        {
            var ip = Resolve(address);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(ip, port));
            _socket.Blocking = false;
            _recvBuffer = new byte[ushort.MaxValue];
            _running = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Net] UDP start failed: " + e.Message);
            Stop();
            return false;
        }
    }

    private static IPAddress Resolve(string address)
    {
        if (string.IsNullOrEmpty(address)) return IPAddress.Any;
        if (IPAddress.TryParse(address, out var parsed)) return parsed;
        try
        {
            var resolved = Dns.GetHostAddresses(address);
            if (resolved != null && resolved.Length > 0) return resolved[0];
        }
        catch { }
        return IPAddress.Any;
    }

    public void SendTo(IPEndPoint remote, byte[] data)
    {
        if (!_running || _socket == null) return;
        try { _socket.SendTo(data, remote); }
        catch (Exception e) { Debug.LogWarning("[Net] UDP send failed: " + e.Message); }
    }

    public bool Poll(out IPEndPoint remote, out byte[] data)
    {
        remote = null;
        data = null;
        if (!_running || _socket == null) return false;

        bool gotAny = false;
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            if (_socket.Available <= 0)
                break;
            try
            {
                int n = _socket.ReceiveFrom(_recvBuffer, ref ep);
                if (n <= 0) continue;
                var buf = new byte[n];
                Array.Copy(_recvBuffer, buf, n);
                _inbox.Enqueue(new KeyValuePair<IPEndPoint, byte[]>((IPEndPoint)ep, buf));
                if (_inbox.Count > 256) _inbox.Dequeue(); // bound memory
            }
            catch (SocketException)
            {
                break;
            }
        }

        if (_inbox.Count > 0)
        {
            var pair = _inbox.Dequeue();
            remote = pair.Key;
            data = pair.Value;
            return true;
        }
        return false;
    }

    public void Stop()
    {
        _running = false;
        if (_socket != null)
        {
            try { _socket.Close(); } catch { }
            _socket = null;
        }
        _inbox.Clear();
    }

    public void Dispose() => Stop();
}