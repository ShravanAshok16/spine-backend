using System.Net;
using System.Net.Sockets;

namespace Spine.Api.Services;

public static class Ipv4HttpHandler
{
    public static SocketsHttpHandler Create() => new()
    {
        ConnectTimeout = TimeSpan.FromSeconds(4),
        ConnectCallback = async (context, ct) =>
        {
            // Resolve IPv4 addresses only
            var entry = await Dns.GetHostEntryAsync(
                context.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };
}