using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Editor.Abstractions;
using Microsoft.Extensions.Options;

namespace Editor.WordPress;

public sealed class PostFeed : IPostFeed
{
    private readonly IContentStream _stream;
    private readonly StreamOptions _options;

    // restBase -> (id -> post)
    private readonly ConcurrentDictionary<string, ImmutableDictionary<long, PostSummary>> _snapshots = new();

    // restBase -> subscriberId -> channel
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<IReadOnlyList<PostSummary>>>> _subs = new();

    public PostFeed(IContentStream stream) : this(stream, new StreamOptions()) { }

    public PostFeed(IContentStream stream, StreamOptions options)
    {
        _stream = stream;
        _options = new StreamOptions(
            WarmFirstCount: options.WarmFirstCount <= 0 ? 10 : options.WarmFirstCount,
            MaxBatchSize:   options.MaxBatchSize   <= 0 ? 100 : options.MaxBatchSize
        );
    }

    public PostFeed(IContentStream stream, IOptions<StreamOptions> options)
        : this(stream, options?.Value ?? new StreamOptions()) { }

    public IReadOnlyList<PostSummary> Current(string restBase)
        => _snapshots.TryGetValue(restBase, out var snap) ? snap.Values.ToList() : new List<PostSummary>();

    public IAsyncEnumerable<IReadOnlyList<PostSummary>> Subscribe(string restBase, CancellationToken ct = default)
    {
        var group = _subs.GetOrAdd(restBase, _ => new ConcurrentDictionary<Guid, Channel<IReadOnlyList<PostSummary>>>());
        var id = Guid.NewGuid();
        var ch = Channel.CreateUnbounded<IReadOnlyList<PostSummary>>();
        group[id] = ch;

        ct.Register(() =>
        {
            ch.Writer.TryComplete();
            group.TryRemove(id, out _);
        });

        if (_snapshots.TryGetValue(restBase, out var snap) && snap.Count > 0)
        {
            ch.Writer.TryWrite(snap.Values.ToList());
        }

        return ch.Reader.ReadAllAsync(ct);
    }

    public async Task RefreshAsync(string restBase, CancellationToken ct = default)
    {
        await foreach (var batch in _stream.StreamAllCachedThenFreshAsync(restBase, options: _options, ct: ct))
        {
            var snap = _snapshots.GetOrAdd(restBase, ImmutableDictionary<long, PostSummary>.Empty);
            foreach (var p in batch) snap = snap.SetItem(p.Id, p);
            _snapshots[restBase] = snap;

            Broadcast(restBase, snap.Values.ToList());
        }
    }

    // -------- NEW: optimistic local updates --------

    public void Evict(string restBase, long id)
    {
        var snap = _snapshots.GetOrAdd(restBase, ImmutableDictionary<long, PostSummary>.Empty);
        if (snap.ContainsKey(id))
        {
            snap = snap.Remove(id);
            _snapshots[restBase] = snap;
            Broadcast(restBase, snap.Values.ToList());
        }
        // if the id isn't present, no-op (still idempotent)
    }

    public void Invalidate(string restBase)
    {
        // For now, just no-op. Left here to mirror the API and for future strategies
        // like marking a "stale" flag. Callers should invoke RefreshAsync next.
    }

    // -------- helpers --------

    private void Broadcast(string restBase, IReadOnlyList<PostSummary> full)
    {
        if (_subs.TryGetValue(restBase, out var group))
        {
            foreach (var kv in group.ToArray())
            {
                var writer = kv.Value.Writer;
                if (!writer.TryWrite(full))
                {
                    writer.TryComplete();
                    group.TryRemove(kv.Key, out _);
                }
            }
        }
    }
}
