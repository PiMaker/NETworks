// File: Response.cs
// Created: 26.04.2017
// 
// See <summary> tags for more information.

using System.Linq;
using System.Text;

namespace NETworks
{
    public class Response
    {
        public Response(ResponseStatus status, byte[] body, string message)
        {
            this.Status = status;
            this.Body = body;
            this.Message = message;
        }

        public ResponseStatus Status { get; }

        public byte[] Body { get; }

        public string Message { get; }

        internal byte[] AsBytes
        {
            get
            {
                var messageBytes = Encoding.Unicode.GetBytes(this.Message);
                return
                    new[] {this.Status == ResponseStatus.Ok ? (byte) 0 : (byte) 1, (byte) messageBytes.Length}.Concat(
                        messageBytes).Concat(this.Body).ToArray();
            }
        }
    }

    public enum ResponseStatus
    {
        /// <summary>
        /// Everything is good.
        /// </summary>
        Ok,

        /// <summary>
        /// Something bad happened at the service.
        /// </summary>
        ServerError,

        /// <summary>
        /// The service could not be reached. NOTE: Not intended for use as a response code in a service callback.
        /// </summary>
        Error
    }
}