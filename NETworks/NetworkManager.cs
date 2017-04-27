// File: NetworkManager.cs
// Created: 27.04.2017
// 
// See <summary> tags for more information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SimpleLogger;

namespace NETworks
{
    internal class NetworkManager
    {
        private const int TIMEOUT = 5000;
        private static readonly byte[] MagicMessage = {64, 128, 255};

        private TcpListener tcpListener;

        private readonly UdpClient udpClient = new UdpClient(6205);

        public async Task Discover(string service)
        {
            Logger.Log("Sending discovery request");

            try
            {
                var sent = await this.udpClient.SendAsync(NetworkManager.MagicMessage,
                    NetworkManager.MagicMessage.Length, new IPEndPoint(IPAddress.Broadcast, 6205));

                if (sent != NetworkManager.MagicMessage.Length)
                {
                    Logger.Log("Unknown UDP error, no bytes were sent");
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Log(e);
                return;
            }

            Logger.Log("Commencing delay");
            await Task.Delay(NetworkManager.TIMEOUT);
        }

        public void RegisterClientListener(ServerConnectionCallback callback)
        {
            if (this.tcpListener != null)
            {
                throw new InvalidOperationException("tcpListener already initialized, something went wrong");
            }

            this.tcpListener = new TcpListener(IPAddress.Any, 6205);
            this.tcpListener.Start();

            Task.Run(async () =>
            {
                while (NETworks.Alive)
                {
                    try
                    {
                        var client = await this.tcpListener.AcceptTcpClientAsync();
                        if (client.Connected)
                        {
                            Logger.Log("Incoming TCP connection, beginning preamble decoding");

                            var buffer = new byte[256];
                            using (var stream = client.GetStream())
                            {
                                var read = await stream.ReadAsync(buffer, 0, buffer.Length);

                                if (read > 0 && read > buffer[0] + 1)
                                {
                                    var guidLength = buffer[0];
                                    var guid = Encoding.ASCII.GetString(buffer, 1, guidLength);
                                    var service = Encoding.Unicode.GetString(buffer, 1 + guidLength,
                                        read - 1 - guidLength);

                                    callback?.Invoke(service, new Connection(guid, service, client, stream));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e);
                    }
                }
            });
        }

        public void Shutdown()
        {
            
        }
    }

    internal delegate void ServerConnectionCallback(string service, Connection connection);
}