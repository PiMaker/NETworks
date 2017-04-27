// File: NETworks.cs
// Created: 26.04.2017
// 
// See <summary> tags for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleLogger;
using SimpleLogger.Logging.Handlers;

namespace NETworks
{
    public static class NETworks
    {
        internal static bool Alive = true;
        private static ServiceCache serviceCache = new ServiceCache();

        public static async Task<Response> Request(string service, string command, byte[] body)
        {
            var cmdLength = Encoding.Unicode.GetByteCount(command);

            if (cmdLength > 255)
            {
                throw new ArgumentException("Commands cannot be longer than 255 Unicode characters");
            }

            Logger.Log(Logger.Level.Info, $"Request issued (To: {service}, Cmd: {command}, Body: {body.Length} B)");

            var request =
                new [] {(byte) cmdLength}.Concat(Encoding.Unicode.GetBytes(command)).Concat(body).ToArray();

            Response response = null;
            while (response == null)
            {
                var connection = await NETworks.serviceCache.Get(service);
                if (connection == null)
                {
                    return new Response(ResponseStatus.Error, null, "No service available");
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

                    // TODO Decode response
                }
                catch (Exception e)
                {
                    connection.State = ConnectionState.Closed;
                    Logger.Log(e);
                }
            }

            return null;
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

            Logger.Log($"Service registering (Tag: {service})");
        }

        public static void Shutdown()
        {
            NETworks.Alive = false;
            NETworks.serviceCache.Shutdown();
        }

        private static bool loggingInitialized = false;
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
                        Logger.DefaultLevel = Logger.Level.Debug;
                    }

                    Logger.On();
                }
                else
                {
                    Logger.Off();
                }
            }
        }
    }

    public delegate Response RequestCallback(string command, byte[] body);

    public delegate bool ChannelRequestCallback(string command);

    public delegate bool ChannelOpenedCallback(Stream channel);
}