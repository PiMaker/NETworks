// File: NETworks.cs
// Created: 26.04.2017
// 
// See <summary> tags for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleLogger;
using SimpleLogger.Logging.Handlers;

namespace NETworks
{
    public static class Network
    {
        internal static readonly byte[] MagicMessage = {64, 128, 255};

        internal static bool Alive = true;
        internal static readonly ServiceCache ServiceCache = new ServiceCache();

        private static bool loggingInitialized;
        private static bool discoveryListenerIntialized;

        internal static readonly List<Service> Services = new List<Service>();
        internal static readonly object ServicesLock = new object();

        /// <summary>
        /// Setting this value enables or disables the logging to console and debug console.
        /// </summary>
        public static bool EnableLog
        {
            set
            {
                if (value)
                {
                    if (!Network.loggingInitialized)
                    {
                        Network.loggingInitialized = true;
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

        /// <summary>
        /// Issues a network request to a service provider. A fitting provider is chosen at random.
        /// </summary>
        /// <param name="service">The service to contact.</param>
        /// <param name="command">The command to send.</param>
        /// <param name="body">Message body, consisting of arbitrary bytes. The transmission is guaranteed to be intact and in order.</param>
        /// <returns>The response received by the service or constructed locally (in case an error occurs).</returns>
        public static async Task<Response> RequestAny(string service, string command, byte[] body)
        {
            if (!Network.Alive)
            {
                throw new InvalidOperationException("Cannot operate after calling Shutdown");
            }

            var cmdLength = Encoding.Unicode.GetByteCount(command);

            if (cmdLength > 255)
            {
                throw new ArgumentException("Commands cannot be longer than 255 Unicode characters");
            }

            Logger.Log(Logger.Level.Info, $"RequestAny issued (To: {service}, Cmd: {command}, Body: {body.Length} B)");

            var request =
                new[] {(byte) cmdLength}.Concat(Encoding.Unicode.GetBytes(command)).Concat(body).ToArray();

            var response = await NetworkHelpers.ReceiveFromService(service, request);

            return response;
        }

        /// <summary>
        /// Registers a service to be called by other clients.
        /// </summary>
        /// <param name="service">The service tag to register as. This enables clients to contact this service. Must be no greater than 200 characters.</param>
        /// <param name="requestCallback">The callback that is invoked every time a message is received. This callback is not guaranteed to be on the same thread that called Register, and is, by no means, thread-safe.</param>
        public static void Register(string service, RequestCallback requestCallback)
        {
            if (!Network.Alive)
            {
                throw new InvalidOperationException("Cannot operate after calling Shutdown");
            }

            if (service.Length > 200)
            {
                throw new ArgumentException("The service name has to be no greater than 200 characters");
            }

            lock (Network.ServicesLock)
            {
                if (Network.Services.Any(x => x.Tag == service))
                {
                    Logger.Log("Tried to register service twice");
                    return;
                }

                if (!Network.discoveryListenerIntialized)
                {
                    Network.discoveryListenerIntialized = true;
                    Task.Run(NetworkHelpers.DiscoveryListener);
                }

                Network.Services.Add(new Service(service, requestCallback));
            }

            Logger.Log($"Service registered (Tag: {service})");
        }

        /// <summary>
        /// Shuts down the entire library. Call this at the end of your applications lifetime. NOTE: Once this method is called, the library can no longer be used!
        /// </summary>
        public static void Shutdown()
        {
            Logger.Log("Shutting down");

            Network.Alive = false;
            Network.ServiceCache.Shutdown();
            NetworkHelpers.UdpClient.Close();

            Logger.Log("Shutdown complete");
        }
    }
}