using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataverseDebugger.Protocol
{
    /// <summary>
    /// Envelope for messages sent over the named pipe.
    /// </summary>
    public sealed class PipeMessage
    {
        /// <summary>Command identifier (e.g., "health", "execute", "initWorkspace").</summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>JSON-serialized payload for the command.</summary>
        public string? Payload { get; set; }
    }

    /// <summary>
    /// Provides methods for reading and writing length-prefixed JSON messages over a stream.
    /// </summary>
    public static class PipeProtocol
    {
        private const int MaxMessageBytes = 128 * 1024 * 1024; // 128 MB ceiling for pipe payloads

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Writes a command and payload to the stream as a length-prefixed JSON message.
        /// </summary>
        /// <typeparam name="T">Type of the payload object.</typeparam>
        /// <param name="stream">Stream to write to.</param>
        /// <param name="command">Command identifier.</param>
        /// <param name="payload">Payload object to serialize.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task WriteAsync<T>(Stream stream, string command, T payload, CancellationToken cancellationToken)
        {
            var message = new PipeMessage
            {
                Command = command,
                Payload = JsonSerializer.Serialize(payload, Options)
            };

            var json = JsonSerializer.Serialize(message, Options);
            var bytes = Encoding.UTF8.GetBytes(json);
            var length = BitConverter.GetBytes(bytes.Length);

            await stream.WriteAsync(length, 0, length.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a length-prefixed JSON message from the stream.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The deserialized message, or null if the stream ended.</returns>
        /// <exception cref="InvalidDataException">Thrown when message length is invalid.</exception>
        public static async Task<PipeMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[4];
            var read = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
            if (!read)
            {
                return null;
            }

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > MaxMessageBytes)
            {
                throw new InvalidDataException($"Invalid message length: {length}");
            }

            var payloadBuffer = new byte[length];
            var payloadRead = await ReadExactAsync(stream, payloadBuffer, cancellationToken).ConfigureAwait(false);
            if (!payloadRead)
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(payloadBuffer);
            return JsonSerializer.Deserialize<PipeMessage>(json, Options);
        }

        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return false;
                }
                offset += read;
            }
            return true;
        }
    }
}
