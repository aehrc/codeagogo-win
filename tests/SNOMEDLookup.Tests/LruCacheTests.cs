namespace SNOMEDLookup.Tests;

public class LruCacheTests
{
    [Fact]
    public void TryGet_ReturnsValue_WhenKeyExists()
    {
        var cache = new LruCache<string, string>();
        cache.Set("key1", "value1");

        var found = cache.TryGet("key1", out var value);

        Assert.True(found);
        Assert.Equal("value1", value);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var cache = new LruCache<string, string>();

        var found = cache.TryGet("nonexistent", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var cache = new LruCache<string, string>();
        cache.Set("key1", "value1");
        cache.Set("key1", "value2");

        var found = cache.TryGet("key1", out var value);

        Assert.True(found);
        Assert.Equal("value2", value);
    }

    [Fact]
    public void Set_EvictsLruEntry_WhenAtCapacity()
    {
        var cache = new LruCache<string, string>(capacity: 3);
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Set("key3", "value3");

        // key1 is now the LRU, adding key4 should evict it
        cache.Set("key4", "value4");

        Assert.False(cache.TryGet("key1", out _)); // evicted
        Assert.True(cache.TryGet("key2", out _));
        Assert.True(cache.TryGet("key3", out _));
        Assert.True(cache.TryGet("key4", out _));
    }

    [Fact]
    public void TryGet_MovesEntryToFront()
    {
        var cache = new LruCache<string, string>(capacity: 3);
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Set("key3", "value3");

        // Access key1 to move it to front
        cache.TryGet("key1", out _);

        // Now key2 is the LRU, adding key4 should evict it
        cache.Set("key4", "value4");

        Assert.True(cache.TryGet("key1", out _)); // not evicted because we accessed it
        Assert.False(cache.TryGet("key2", out _)); // evicted
        Assert.True(cache.TryGet("key3", out _));
        Assert.True(cache.TryGet("key4", out _));
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenEntryExpired()
    {
        var cache = new LruCache<string, string>(defaultTtl: TimeSpan.FromMilliseconds(50));
        cache.Set("key1", "value1");

        // Wait for entry to expire
        Thread.Sleep(100);

        var found = cache.TryGet("key1", out var value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_ReturnsValue_WhenNotExpired()
    {
        var cache = new LruCache<string, string>(defaultTtl: TimeSpan.FromSeconds(10));
        cache.Set("key1", "value1");

        var found = cache.TryGet("key1", out var value);

        Assert.True(found);
        Assert.Equal("value1", value);
    }

    [Fact]
    public void TryGet_WithCustomTtl_OverridesDefaultTtl()
    {
        var cache = new LruCache<string, string>(defaultTtl: TimeSpan.FromSeconds(10));
        cache.Set("key1", "value1");

        // Use short TTL override
        Thread.Sleep(100);
        var found = cache.TryGet("key1", TimeSpan.FromMilliseconds(50), out var value);

        Assert.False(found); // expired with custom TTL
    }

    [Fact]
    public void Remove_RemovesEntry()
    {
        var cache = new LruCache<string, string>();
        cache.Set("key1", "value1");

        var removed = cache.Remove("key1");

        Assert.True(removed);
        Assert.False(cache.TryGet("key1", out _));
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var cache = new LruCache<string, string>();

        var removed = cache.Remove("nonexistent");

        Assert.False(removed);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new LruCache<string, string>();
        cache.Set("key1", "value1");
        cache.Set("key2", "value2");
        cache.Set("key3", "value3");

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("key1", out _));
        Assert.False(cache.TryGet("key2", out _));
        Assert.False(cache.TryGet("key3", out _));
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var cache = new LruCache<string, string>();

        Assert.Equal(0, cache.Count);

        cache.Set("key1", "value1");
        Assert.Equal(1, cache.Count);

        cache.Set("key2", "value2");
        Assert.Equal(2, cache.Count);

        cache.Remove("key1");
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void ThreadSafety_ConcurrentOperations()
    {
        var cache = new LruCache<int, int>(capacity: 100);
        var exceptions = new List<Exception>();

        // Run multiple threads performing concurrent operations
        var threads = new Thread[10];
        for (int t = 0; t < threads.Length; t++)
        {
            int threadNum = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var key = threadNum * 100 + i;
                        cache.Set(key, key * 2);
                        cache.TryGet(key, out _);
                        if (i % 10 == 0) cache.Remove(key);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) exceptions.Add(ex);
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        Assert.Empty(exceptions);
    }

    [Fact]
    public void DefaultCapacity_Is100()
    {
        var cache = new LruCache<int, int>();

        for (int i = 0; i < 150; i++)
        {
            cache.Set(i, i);
        }

        Assert.Equal(100, cache.Count);
    }
}
