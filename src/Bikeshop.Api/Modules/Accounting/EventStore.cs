using System.Text.Json;
using Bikeshop.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Bikeshop.Api.Modules.Accounting;

/// <summary>Événement persisté dans le journal d'événements (append-only).</summary>
public class StoredEvent
{
    public long Id { get; set; }
    public required string StreamId { get; set; }
    public int Version { get; set; }
    public required string EventType { get; set; }
    public required string Data { get; set; }
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// Event store minimaliste : append-only, versionné par flux.
/// L'état du module comptabilité est intégralement reconstruit en rejouant les événements.
/// </summary>
public class EventStore(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(string streamId, object @event)
    {
        var lastVersion = await db.Events
            .Where(e => e.StreamId == streamId)
            .MaxAsync(e => (int?)e.Version) ?? 0;

        db.Events.Add(new StoredEvent
        {
            StreamId = streamId,
            Version = lastVersion + 1,
            EventType = @event.GetType().Name,
            Data = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions),
            OccurredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<object>> ReadStreamAsync(string streamId, IReadOnlyDictionary<string, Type> eventTypes)
    {
        var stored = await db.Events
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return stored
            .Select(e => JsonSerializer.Deserialize(e.Data, eventTypes[e.EventType], JsonOptions)!)
            .ToList();
    }

    public Task<int> CountAsync(string streamId) =>
        db.Events.CountAsync(e => e.StreamId == streamId);
}
