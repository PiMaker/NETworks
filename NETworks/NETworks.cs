// File: NETworks.cs
// Created: 26.04.2017
// 
// See <summary> tags for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SimpleLogger;
using SimpleLogger.Logging.Handlers;

namespace NETworks
{
    public static class NETworks
    {
        internal static readonly byte[] MagicMessage = {64, 128, 255};

        internal static bool Alive = true;
        private static readonly ServiceCache serviceCache = new ServiceCache();

        private static bool loggingInitialized;
        private static bool discoveryListenerIntialized;

        private static readonly List<Service> services = new List<Service>();
        private static readonly object servicesLock = new object();

        private static readonly UdpClient udpClient = new UdpClient(6205);

        public static bool EnableLog
        {
            set
            {
                if (value)
                {
                    if (!NETworks.loggingInitialized)
                    {
                        NETworks.loggingInitialized = true;
                        Logger.LoggerHandlerManager.AddHandler(new DebugConsoleLoggerHandler());
                        Logger.LoggerHandlerManager.AddHandler(new ConsoleLoggerHandler());
                        Logger.DefaultLevel = Logger.Level.Debug;
                    }

                    Logger.On();

                    Logger.Log("Logging enabled");
                }
                else
                {
                    Logger.Off();
                }
            }
        }

        public static async Task<Response> Request(string service, string command, byte[] body)
        {
            var cmdLength = Encoding.Unicode.GetByteCount(command);

            if (cmdLength > 255)
            {
                throw new ArgumentException("Commands cannot be longer than 255 Unicode characters");
            }

            Logger.Log(Logger.Level.Info, $"Request issued (To: {service}, Cmd: {command}, Body: {body.Length} B)");

            var request =
                new[] {(byte) cmdLength}.Concat(Encoding.Unicode.GetBytes(command)).Concat(body).ToArray();

            var response = await NETworks.ReceiveFromService(service, request);

            return response;
        }

        public static async Task<ChannelResponse> RequestChannel(string service, string command)
        {
            Logger.Log($"Channel-Request issued (To: {service}, Cmd: {command})");
            throw new NotImplementedException();
        }


        public static void Register(string service, RequestCallback requestCallback,
            ChannelRequestCallback channelRequestCallback, ChannelOpenedCallback channelOpenedCallback)
        {
            if (service.Length > 200)
            {
                throw new ArgumentException("The service name has to be no greater than 200 characters");
            }

            lock (NETworks.servicesLock)
            {
                if (NETworks.services.Any(x => x.Tag == service))
                {
                    Logger.Log("Tried to register service twice");
                    return;
                }

                if (!NETworks.discoveryListenerIntialized)
                {
                    NETworks.discoveryListenerIntialized = true;
                    Task.Run(NETworks.DiscoveryListener);
                }

                NETworks.services.Add(new Service(service, requestCallback, channelRequestCallback,
                    channelOpenedCallback));
            }

            Logger.Log($"Service registered (Tag: {service})");
        }

        public static void Shutdown()
        {
            Logger.Log("Shutting down");
            NETworks.Alive = false;
            NETworks.serviceCache.Shutdown();
            NETworks.udpClient.Close();
            Logger.Log("Shutdown complete");
        }

        private static async Task<Response> ReceiveFromService(string service, byte[] request)
        {
            Response response = null;
            while (response == null)
            {
                var connection = await NETworks.serviceCache.Get(service);
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
                    connection.State = ConnectionState.Closed;
                    Logger.Log(e);
                }
            }
            return response;
        }

        private static async Task DiscoveryListener()
        {
            while (NETworks.Alive)
            {
                var received = await NETworks.udpClient.ReceiveAsync();

                if (received.Buffer.Take(NETworks.MagicMessage.Length).SequenceEqual(NETworks.MagicMessage) &&
                    received.Buffer.Length > NETworks.MagicMessage.Length + 2)
                {
                    var port = BitConverter.ToInt16(received.Buffer, NETworks.MagicMessage.Length);
                    var serviceTag = Encoding.Unicode.GetString(received.Buffer, NETworks.MagicMessage.Length + 2,
                        received.Buffer.Length - NETworks.MagicMessage.Length - 2);

                    Logger.Log($"Received discovery request for '{serviceTag}' (:{port})");

                    Service service;

                    lock (NETworks.servicesLock)
                    {
                        service = NETworks.services.FirstOrDefault(x => x.Tag == serviceTag);
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