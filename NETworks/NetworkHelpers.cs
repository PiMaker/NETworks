using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SimpleLogger;

namespace NETworks
{
    public static class NetworkHelpers
    {
        internal static readonly UdpClient UdpClient;

        static NetworkHelpers()
        {
            NetworkHelpers.UdpClient = new UdpClient();
            NetworkHelpers.UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            NetworkHelpers.UdpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 6205));
        }

        internal static async Task<Response> ReceiveFromService(string service, byte[] request)
        {
            Response response = null;
            while (response == null)
            {
                var connection = await Network.ServiceCache.Get(service);
                if (connection == null)
                {
                    return new Response(ResponseStatus.Error, null, "No service found");
                }

                try
                {
                    await connection.Stream.WriteAsync(request, 0, request.Length);

                    var responseBytes = new List<byte>();
                    var buffer = new byte[1024];
                    int read;

                    do
                    {
                        read = await connection.Stream.ReadAsync(buffer, 0, buffer.Length);

                        if (read == 0)
                        {
                            throw new Exception("Stream returned 0 => TcpClient closed");
                        }

                        responseBytes.AddRange(buffer.Take(read));
                    } while (read == buffer.Length);

                    var respStatus = responseBytes[0];
                    var respMessageLength = responseBytes[1];
                    var message = respMessageLength > 0
                        ? Encoding.Unicode.GetString(responseBytes.Skip(2).Take(respMessageLength).ToArray())
                        : "";
                    var respBody = responseBytes.Skip(respMessageLength + 2).ToArray();

                    response = new Response(respStatus == 0 ? ResponseStatus.Ok : ResponseStatus.ServerError, respBody,
                        message);
                }
                catch (Exception e)
                {
                    Logger.Log(
                        $"Connection looks unavailable, closing and trying again with different server (Message: {e.Message})");
                    try
                    {
                        connection.Close();
                    }
                    catch
                    {
                    }
                }
            }
            return response;
        }

        internal static async Task DiscoveryListener()
        {
            while (Network.Alive)
            {
                var received = await NetworkHelpers.UdpClient.ReceiveAsync();

                if (received.Buffer.Take(Network.MagicMessage.Length).SequenceEqual(Network.MagicMessage) &&
                    received.Buffer.Length > Network.MagicMessage.Length + 2)
                {
                    var port = BitConverter.ToInt16(received.Buffer, Network.MagicMessage.Length);
                    var serviceTag = Encoding.Unicode.GetString(received.Buffer, Network.MagicMessage.Length + 2,
                        received.Buffer.Length - Network.MagicMessage.Length - 2);

                    Logger.Log($"Received discovery request for '{serviceTag}' (:{port})");

                    Service service;

                    lock (Network.ServicesLock)
                    {
                        service = Network.Services.FirstOrDefault(x => x.Tag == serviceTag);
                    }

                    if (service != default(Service))
                    {
                        Logger.Log($"Satisfying request with local service ({service.Guid})");
                        service.AddClient(new IPEndPoint(received.RemoteEndPoint.Address, port));
                    }
                }
            }
        }
    }

    public delegate Response RequestCallback(string command, byte[] body);

    public delegate bool ChannelRequestCallback(string command);

    public delegate bool ChannelOpenedCallback(Stream channel);
}