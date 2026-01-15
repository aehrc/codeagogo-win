// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for the thread-safe LruCache with ConceptResult values.
/// Translated from Mac version ConceptCacheTests.swift.
/// </summary>
public class ConceptCacheTests
{
    #region Basic Cache Operations

    [Fact]
    public void CacheSetAndGet_StoresAndRetrievesValue()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);
        var found = cache.TryGet("123456789", out var cached);

        found.Should().BeTrue();
        cached.Should().NotBeNull();
        cached!.ConceptId.Should().Be("123456789");
    }

    [Fact]
    public void CacheMiss_ReturnsNullForNonexistentKey()
    {
        var cache = new LruCache<string, ConceptResult>();

        var found = cache.TryGet("nonexistent", out var cached);

        found.Should().BeFalse();
        cached.Should().BeNull();
    }

    [Fact]
    public void CacheOverwrite_UpdatesExistingEntry()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result1 = MakeConceptResult("123456789", fsn: "Original term");
        var result2 = MakeConceptResult("123456789", fsn: "Updated term");

        cache.Set("123456789", result1);
        cache.Set("123456789", result2);

        cache.TryGet("123456789", out var cached);

        cached.Should().NotBeNull();
        cached!.Fsn.Should().Be("Updated term");
    }

    [Fact]
    public void CacheContains_ReturnsTrueForCachedItem()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);

        cache.TryGet("123456789", out _).Should().BeTrue();
    }

    [Fact]
    public void CacheContains_ReturnsFalseForMissingItem()
    {
        var cache = new LruCache<string, ConceptResult>();

        cache.TryGet("nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void CacheCount_ReturnsCorrectCount()
    {
        var cache = new LruCache<string, ConceptResult>();

        cache.Count.Should().Be(0);

        cache.Set("A", MakeConceptResult("A"));
        cache.Count.Should().Be(1);

        cache.Set("B", MakeConceptResult("B"));
        cache.Count.Should().Be(2);

        cache.Set("C", MakeConceptResult("C"));
        cache.Count.Should().Be(3);
    }

    [Fact]
    public void CacheRemove_RemovesEntry()
    {
        var cache = new LruCache<string, ConceptResult>();
        cache.Set("123456789", MakeConceptResult("123456789"));

        cache.Remove("123456789").Should().BeTrue();
        cache.TryGet("123456789", out _).Should().BeFalse();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void CacheRemove_ReturnsFalseForMissingKey()
    {
        var cache = new LruCache<string, ConceptResult>();

        cache.Remove("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void CacheClear_RemovesAllEntries()
    {
        var cache = new LruCache<string, ConceptResult>();
        cache.Set("A", MakeConceptResult("A"));
        cache.Set("B", MakeConceptResult("B"));
        cache.Set("C", MakeConceptResult("C"));

        cache.Clear();

        cache.Count.Should().Be(0);
        cache.TryGet("A", out _).Should().BeFalse();
        cache.TryGet("B", out _).Should().BeFalse();
        cache.TryGet("C", out _).Should().BeFalse();
    }

    #endregion

    #region TTL/Expiration Tests

    [Fact]
    public void CacheExpiration_ExpiredEntryReturnsNull()
    {
        var cache = new LruCache<string, ConceptResult>(defaultTtl: TimeSpan.FromMilliseconds(1));
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);

        // Wait for TTL to expire
        Thread.Sleep(10);

        var found = cache.TryGet("123456789", out var cached);

        found.Should().BeFalse("Cache entry should be expired");
        cached.Should().BeNull();
    }

    [Fact]
    public void CacheExpiration_ZeroTTLIsAlwaysExpired()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);

        // With a TTL of zero, should be expired immediately
        var found = cache.TryGet("123456789", TimeSpan.Zero, out var cached);

        found.Should().BeFalse("Cache entry should be expired with TTL of 0");
        cached.Should().BeNull();
    }

    [Fact]
    public void ValidTTL_ReturnsEntry()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);

        // With large TTL, should still be valid
        var found = cache.TryGet("123456789", TimeSpan.FromHours(24), out var cached);

        found.Should().BeTrue();
        cached.Should().NotBeNull();
    }

    [Fact]
    public void DifferentTTLsForSameEntry_WorksCorrectly()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);

        // Same entry, different TTL checks
        var withLargeTTL = cache.TryGet("123456789", TimeSpan.FromHours(24), out var cachedLarge);
        var withSmallTTL = cache.TryGet("123456789", TimeSpan.Zero, out var cachedSmall);

        withLargeTTL.Should().BeTrue();
        cachedLarge.Should().NotBeNull();
        withSmallTTL.Should().BeFalse();
        cachedSmall.Should().BeNull();
    }

    [Fact]
    public void ExpiredEntryRemoval_RemovesEntryFromCache()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 5);
        var result = MakeConceptResult("123456789");

        cache.Set("123456789", result);
        cache.Count.Should().Be(1);

        // Access with 0 TTL should remove the expired entry
        cache.TryGet("123456789", TimeSpan.Zero, out _);

        cache.Count.Should().Be(0, "Expired entry should be removed");
    }

    [Fact]
    public void DefaultTTL_IsUsedWhenNotSpecified()
    {
        var cache = new LruCache<string, ConceptResult>(defaultTtl: TimeSpan.FromMilliseconds(50));
        cache.Set("123456789", MakeConceptResult("123456789"));

        // Should be valid immediately
        cache.TryGet("123456789", out var cachedImmediate).Should().BeTrue();
        cachedImmediate.Should().NotBeNull();

        // Wait for default TTL to expire
        Thread.Sleep(100);

        cache.TryGet("123456789", out var cachedExpired).Should().BeFalse();
        cachedExpired.Should().BeNull();
    }

    #endregion

    #region LRU Eviction Tests

    [Fact]
    public void CacheSizeLimit_EvictsWhenAtCapacity()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 5);

        // Add 5 entries (at capacity)
        for (int i = 0; i < 5; i++)
        {
            cache.Set($"concept{i}", MakeConceptResult($"concept{i}"));
        }

        cache.Count.Should().Be(5);

        // Add a 6th entry - should trigger eviction
        cache.Set("concept5", MakeConceptResult("concept5"));

        cache.Count.Should().Be(5, "Cache size should remain at max");
    }

    [Fact]
    public void LRUEviction_EvictsLeastRecentlyUsed()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 3);

        // Add 3 entries in order: A, B, C
        cache.Set("A", MakeConceptResult("A"));
        cache.Set("B", MakeConceptResult("B"));
        cache.Set("C", MakeConceptResult("C"));

        // Access A to make it more recently used (moves to front)
        cache.TryGet("A", out _);

        // Add D - should evict B (least recently used)
        // Order before: A (front), C, B (back/LRU)
        // After accessing A: A (front), C, B (back/LRU) -> still B is LRU since we moved A
        // Actually: Set adds to front. So after Set A,B,C order is: C(front), B, A(back)
        // Then TryGet A moves A to front: A(front), C, B(back)
        // Then Set D evicts B (back): D(front), A, C
        cache.Set("D", MakeConceptResult("D"));

        cache.TryGet("A", out var cachedA);
        cache.TryGet("B", out var cachedB);
        cache.TryGet("C", out var cachedC);
        cache.TryGet("D", out var cachedD);

        cachedA.Should().NotBeNull("A should still be cached (was accessed)");
        cachedB.Should().BeNull("B should be evicted (least recently used)");
        cachedC.Should().NotBeNull("C should still be cached");
        cachedD.Should().NotBeNull("D should be cached");
    }

    [Fact]
    public void LRUEviction_UpdateDoesNotEvict()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 3);

        // Add 3 entries
        cache.Set("A", MakeConceptResult("A"));
        cache.Set("B", MakeConceptResult("B"));
        cache.Set("C", MakeConceptResult("C"));

        // Update A with new data - should not trigger eviction
        cache.Set("A", MakeConceptResult("A", fsn: "Updated"));

        cache.Count.Should().Be(3);
        cache.TryGet("B", out var cachedB);
        cache.TryGet("C", out var cachedC);
        cache.TryGet("A", out var updated);

        cachedB.Should().NotBeNull("B should still be cached");
        cachedC.Should().NotBeNull("C should still be cached");
        updated!.Fsn.Should().Be("Updated");
    }

    [Fact]
    public void LRUEviction_OrderIsCorrect()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 3);

        // Add in order: A, B, C (C is most recent)
        cache.Set("A", MakeConceptResult("A"));
        cache.Set("B", MakeConceptResult("B"));
        cache.Set("C", MakeConceptResult("C"));

        // Now add D - A should be evicted (oldest)
        cache.Set("D", MakeConceptResult("D"));

        cache.TryGet("A", out _).Should().BeFalse("A should be evicted (oldest)");
        cache.TryGet("B", out _).Should().BeTrue("B should still be cached");
        cache.TryGet("C", out _).Should().BeTrue("C should still be cached");
        cache.TryGet("D", out _).Should().BeTrue("D should be cached");
    }

    [Fact]
    public void AccessPromotesToFront_PreventingEviction()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 3);

        cache.Set("A", MakeConceptResult("A"));
        cache.Set("B", MakeConceptResult("B"));
        cache.Set("C", MakeConceptResult("C"));

        // Access A - moves it to front
        cache.TryGet("A", out _);

        // Add D, E - should evict B, then C (A was recently accessed)
        cache.Set("D", MakeConceptResult("D"));
        cache.Set("E", MakeConceptResult("E"));

        cache.TryGet("A", out _).Should().BeTrue("A was recently accessed");
        cache.TryGet("B", out _).Should().BeFalse("B should be evicted");
        cache.TryGet("C", out _).Should().BeFalse("C should be evicted");
        cache.TryGet("D", out _).Should().BeTrue("D should be cached");
        cache.TryGet("E", out _).Should().BeTrue("E should be cached");
    }

    [Fact]
    public void SingleCapacityCache_WorksCorrectly()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 1);

        cache.Set("A", MakeConceptResult("A"));
        cache.Count.Should().Be(1);
        cache.TryGet("A", out _).Should().BeTrue();

        cache.Set("B", MakeConceptResult("B"));
        cache.Count.Should().Be(1);
        cache.TryGet("A", out _).Should().BeFalse("A should be evicted");
        cache.TryGet("B", out _).Should().BeTrue("B should be cached");
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentReads_AllSucceed()
    {
        var cache = new LruCache<string, ConceptResult>();
        var result = MakeConceptResult("123456789");
        cache.Set("123456789", result);

        var results = new List<ConceptResult?>();
        var tasks = new Task[100];

        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                cache.TryGet("123456789", out var cached);
                lock (results)
                {
                    results.Add(cached);
                }
            });
        }

        await Task.WhenAll(tasks);

        // All reads should succeed
        results.Should().HaveCount(100);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().AllSatisfy(r => r!.ConceptId.Should().Be("123456789"));
    }

    [Fact]
    public async Task ConcurrentWrites_AllSucceed()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 200);
        var tasks = new Task[100];

        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                cache.Set($"concept{index}", MakeConceptResult($"concept{index}"));
            });
        }

        await Task.WhenAll(tasks);

        // Verify all writes succeeded
        for (int i = 0; i < 100; i++)
        {
            cache.TryGet($"concept{i}", out var cached).Should().BeTrue($"concept{i} should be cached");
            cached.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ConcurrentReadWrite_NoExceptions()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 100);
        var tasks = new List<Task>();

        // Writers
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                cache.Set($"{index}", MakeConceptResult($"{index}"));
            }));
        }

        // Readers (may or may not find entries - that's OK)
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                cache.TryGet($"{index}", out _);
            }));
        }

        // Test passes if no exceptions occur (thread safety)
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentEviction_NoExceptions()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 10);
        var tasks = new List<Task>();

        // Many concurrent writes that will trigger evictions
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                cache.Set($"concept{index}", MakeConceptResult($"concept{index}"));
            }));
        }

        await Task.WhenAll(tasks);

        // Cache should have at most 10 entries
        cache.Count.Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public async Task HighContention_MaintainsIntegrity()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 50);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var tasks = new List<Task>();

        // Multiple readers and writers operating concurrently
        for (int i = 0; i < 10; i++)
        {
            var writerIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                int counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    cache.Set($"writer{writerIndex}_item{counter % 20}",
                        MakeConceptResult($"writer{writerIndex}_item{counter}"));
                    counter++;
                    await Task.Yield();
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                int counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    cache.TryGet($"writer{writerIndex}_item{counter % 20}", out _);
                    counter++;
                    await Task.Yield();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // If we got here without exceptions, thread safety is working
        cache.Count.Should().BeLessOrEqualTo(50);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyCache_CountIsZero()
    {
        var cache = new LruCache<string, ConceptResult>();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void NullValue_CanBeCached()
    {
        var cache = new LruCache<string, ConceptResult?>();

        cache.Set("null-entry", null);

        cache.TryGet("null-entry", out var cached).Should().BeTrue();
        cached.Should().BeNull();
    }

    [Fact]
    public void LargeNumberOfEntries_WorksCorrectly()
    {
        var cache = new LruCache<string, ConceptResult>(capacity: 10000);

        for (int i = 0; i < 10000; i++)
        {
            cache.Set($"concept{i}", MakeConceptResult($"concept{i}"));
        }

        cache.Count.Should().Be(10000);

        // Verify random access works
        cache.TryGet("concept5000", out var cached).Should().BeTrue();
        cached!.ConceptId.Should().Be("concept5000");
    }

    [Fact]
    public void RapidSetAndGet_WorksCorrectly()
    {
        var cache = new LruCache<string, ConceptResult>();

        for (int i = 0; i < 1000; i++)
        {
            var key = $"concept{i % 10}";
            cache.Set(key, MakeConceptResult(key, fsn: $"Iteration {i}"));
            cache.TryGet(key, out var cached);
            cached.Should().NotBeNull();
            cached!.Fsn.Should().Be($"Iteration {i}");
        }
    }

    [Fact]
    public void DefaultConstructor_UsesReasonableDefaults()
    {
        var cache = new LruCache<string, ConceptResult>();

        // Should be able to add 100 entries (default capacity)
        for (int i = 0; i < 100; i++)
        {
            cache.Set($"concept{i}", MakeConceptResult($"concept{i}"));
        }

        cache.Count.Should().Be(100);

        // Adding one more should evict
        cache.Set("concept100", MakeConceptResult("concept100"));
        cache.Count.Should().Be(100);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void IntegerKeys_WorkCorrectly()
    {
        var cache = new LruCache<int, ConceptResult>(capacity: 5);

        cache.Set(1, MakeConceptResult("1"));
        cache.Set(2, MakeConceptResult("2"));
        cache.Set(3, MakeConceptResult("3"));

        cache.TryGet(1, out var cached).Should().BeTrue();
        cached!.ConceptId.Should().Be("1");

        cache.TryGet(999, out _).Should().BeFalse();
    }

    [Fact]
    public void StringValues_WorkCorrectly()
    {
        var cache = new LruCache<string, string>(capacity: 5);

        cache.Set("key1", "value1");
        cache.Set("key2", "value2");

        cache.TryGet("key1", out var value).Should().BeTrue();
        value.Should().Be("value1");
    }

    #endregion

    #region Helpers

    private static ConceptResult MakeConceptResult(
        string id,
        string? fsn = "Test term (test)",
        string? pt = "Test term",
        bool active = true)
    {
        return new ConceptResult(
            ConceptId: id,
            Branch: "MAIN",
            Fsn: fsn,
            Pt: pt,
            Active: active,
            EffectiveTime: "20240101",
            ModuleId: "900000000000207008"
        );
    }

    #endregion
}
