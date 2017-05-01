using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SimpleLogger;

namespace NETworks
{
    internal class Service
    {
        private readonly List<TcpClient> clients = new List<TcpClient>();
        private readonly object clientsLock = new object();

        public Service(string tag, RequestCallback requestCallback)
        {
            this.Tag = tag;
            this.RequestCallback = requestCallback;

            this.Guid = Guid.NewGuid();
            Logger.Log($"Service initialized ({tag} -> {this.Guid})");
        }

        public string Tag { get; }
        public RequestCallback RequestCallback { get; }

        public Guid Guid { get; }

        public void AddClient(IPEndPoint remoteEndPoint)
        {
            Task.Run(() =>
            {
                var client = new TcpClient();
                client.Connect(remoteEndPoint);

                Logger.Log($"Client connection established (Tag: {this.Tag}): {remoteEndPoint.Address}");

                var stream = client.GetStream();

                if (!client.Connected)
                {
                    return;
                }

                lock (this.clientsLock)
                {
                    this.clients.RemoveAll(x => !x.Connected);
                    this.clients.Add(client);
                }

                var guid = this.Guid.ToString();
                var guidBytes = Encoding.ASCII.GetBytes(guid);
                var preamble =
                    new[] {(byte) guidBytes.Length}.Concat(guidBytes)
                        .Concat(Encoding.Unicode.GetBytes(this.Tag))
                        .ToArray();

                try
                {
                    stream.Write(preamble, 0, preamble.Length);

                    while (Network.Alive && client.Connected)
                    {
                        var requestBytes = new List<byte>();
                        var buffer = new byte[1024];
                        int read;

                        do
                        {
                            read = stream.Read(buffer, 0, buffer.Length);

                            if (read == 0)
                            {
                                // Client closed
                                return;
                            }

                            requestBytes.AddRange(buffer.Take(read));
                        } while (read == buffer.Length);

                        var commandLength = requestBytes[0];
                        var command = commandLength > 0
                            ? Encoding.Unicode.GetString(requestBytes.Skip(1).Take(commandLength).ToArray())
                            : "";
                        var body = requestBytes.Skip(commandLength + 1).ToArray();

                        var response = this.RequestCallback?.Invoke(command, body);

                        byte[] responseBytes;
                        if (response != null)
                        {
                            responseBytes = response.AsBytes;
                        }
                        else
                        {
                            responseBytes = new Response(ResponseStatus.ServerError, new byte[0], "").AsBytes;
                        }

                        stream.Write(responseBytes, 0, responseBytes.Length);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
                finally
                {
                    client.Close();
                    stream.Dispose();
                }
            });
        }

        public void Stop()
        {
            lock (this.clientsLock)
            {
                foreach (var tcpClient in this.clients)
                {
                    tcpClient.Close();
                }
            }
        }
    }
}