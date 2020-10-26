using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sentry.Internal;
using Sentry.Internal.Extensions;

namespace Sentry.Protocol
{
    /// <summary>
    /// Envelope.
    /// </summary>
    public class Envelope : IDisposable, ISerializable
    {
        private const string EventIdKey = "event_id";

        /// <summary>
        /// Header associated with this envelope.
        /// </summary>
        public IReadOnlyDictionary<string, object> Header { get; }

        /// <summary>
        /// Items inside this envelope.
        /// </summary>
        public IReadOnlyList<EnvelopeItem> Items { get; }

        /// <summary>
        /// Initializes an instance of <see cref="Envelope"/>.
        /// </summary>
        public Envelope(IReadOnlyDictionary<string, object> header, IReadOnlyList<EnvelopeItem> items)
        {
            Header = header;
            Items = items;
        }

        /// <summary>
        /// Attempts to extract the value of "sentry_id" header if it's present.
        /// </summary>
        public SentryId? TryGetEventId() =>
            Header.TryGetValue(EventIdKey, out var value) &&
            value is string valueString &&
            Guid.TryParse(valueString, out var guid)
                ? new SentryId(guid)
                : (SentryId?)null;

        /// <inheritdoc />
        public async Task SerializeAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            // Header
            await Json.SerializeToStreamAsync(Header, stream, cancellationToken).ConfigureAwait(false);
            stream.WriteByte((byte)'\n');

            // Items
            foreach (var item in Items)
            {
                await item.SerializeAsync(stream, cancellationToken).ConfigureAwait(false);
                stream.WriteByte((byte)'\n');
            }
        }

        /// <inheritdoc />
        public void Dispose() => Items.DisposeAll();

        /// <summary>
        /// Creates an envelope that contains a single event.
        /// </summary>
        public static Envelope FromEvent(SentryEvent @event)
        {
            var header = new Dictionary<string, object>
            {
                [EventIdKey] = @event.EventId.ToString()
            };

            var items = new[]
            {
                EnvelopeItem.FromEvent(@event)
            };

            return new Envelope(header, items);
        }

        public static async Task<Envelope> DeserializeAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            // Header
            var header = await Json.DeserializeFromStreamAsync<Dictionary<string, object>>(stream, cancellationToken)
                .ConfigureAwait(false);

            // Items
            var items = new List<EnvelopeItem>();
            while (stream.ReadByte() == (byte)'\n')
            {
                var item = await EnvelopeItem.DeserializeAsync(stream, cancellationToken).ConfigureAwait(false);
                items.Add(item);
            }

            return new Envelope(header, items);
        }
    }
}
