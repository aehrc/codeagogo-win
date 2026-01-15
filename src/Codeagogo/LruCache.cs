// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache with TTL (Time To Live) support.
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly TimeSpan _defaultTtl;
    private readonly object _lock = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _map;
    private readonly LinkedList<CacheEntry> _list;

    private sealed class CacheEntry
    {
        public TKey Key { get; init; } = default!;
        public TValue Value { get; init; } = default!;
        public DateTimeOffset Timestamp { get; init; }
    }

    /// <summary>
    /// Creates a new LRU cache with specified capacity and default TTL.
    /// </summary>
    /// <param name="capacity">Maximum number of entries (default: 100)</param>
    /// <param name="defaultTtl">Default time-to-live for entries (default: 6 hours)</param>
    public LruCache(int capacity = 100, TimeSpan? defaultTtl = null)
    {
        _capacity = capacity;
        _defaultTtl = defaultTtl ?? TimeSpan.FromHours(6);
        _map = new Dictionary<TKey, LinkedListNode<CacheEntry>>(capacity);
        _list = new LinkedList<CacheEntry>();
    }

    /// <summary>
    /// Tries to get a cached value if it exists and hasn't expired.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="ttl">Optional TTL override for this lookup</param>
    /// <param name="value">The cached value if found and valid</param>
    /// <returns>True if a valid cached value was found</returns>
    public bool TryGet(TKey key, TimeSpan? ttl, out TValue? value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                var effectiveTtl = ttl ?? _defaultTtl;
                var age = DateTimeOffset.UtcNow - node.Value.Timestamp;

                if (age < effectiveTtl)
                {
                    // Move to front (most recently used)
                    _list.Remove(node);
                    _list.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }

                // Expired - remove it
                _map.Remove(key);
                _list.Remove(node);
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Tries to get a cached value using the default TTL.
    /// </summary>
    public bool TryGet(TKey key, out TValue? value) => TryGet(key, null, out value);

    /// <summary>
    /// Stores a value in the cache. Evicts LRU entry if at capacity.
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            // If key already exists, remove old entry
            if (_map.TryGetValue(key, out var existingNode))
            {
                _list.Remove(existingNode);
                _map.Remove(key);
            }

            // Evict LRU entries if at capacity
            while (_map.Count >= _capacity && _list.Last != null)
            {
                var lru = _list.Last;
                _map.Remove(lru.Value.Key);
                _list.RemoveLast();
            }

            // Add new entry at front
            var entry = new CacheEntry
            {
                Key = key,
                Value = value,
                Timestamp = DateTimeOffset.UtcNow
            };
            var node = _list.AddFirst(entry);
            _map[key] = node;
        }
    }

    /// <summary>
    /// Removes an entry from the cache.
    /// </summary>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _list.Remove(node);
                _map.Remove(key);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _map.Clear();
            _list.Clear();
        }
    }

    /// <summary>
    /// Gets the current number of entries in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _map.Count;
            }
        }
    }
}
