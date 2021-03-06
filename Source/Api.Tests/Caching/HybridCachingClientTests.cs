﻿using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Caching;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class HybridCachingClientTests: CacheClientTestsBase {
        protected override ICacheClient GetCache() {
            if (String.IsNullOrEmpty(Settings.Current.RedisConnectionString))
                return null;

            var muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionString);
            return new HybridCacheClient(muxer);
        }

        [Fact]
        public override void CanSetAndGetValue() {
            var cache = GetCache() as HybridCacheClient;
            if (cache == null)
                return;

            cache.FlushAll();

            cache.Set("test", 1);
            var value = cache.Get<int>("test");
            Assert.Equal(1, value);
            Assert.Equal(1, cache.LocalCache.Count);
        }

        [Fact]
        public void CanInvalidateLocalCache() {
            var firstCache = GetCache() as HybridCacheClient;
            Assert.NotNull(firstCache);
             
            var secondCache = GetCache() as HybridCacheClient;
            Assert.NotNull(secondCache);
            
            firstCache.FlushAll();

            firstCache.Set("willCacheLocallyOnFirst", 1);
            Assert.Equal(1, firstCache.LocalCache.Count);
            Assert.Equal(0, secondCache.LocalCache.Count);

            secondCache.Set("keyWillExpire", 50, TimeSpan.FromMilliseconds(50));
            Assert.Equal(1, firstCache.LocalCache.Count);
            Assert.Equal(1, secondCache.LocalCache.Count);

            Assert.Equal(1, firstCache.Get<int>("willCacheLocallyOnFirst"));
            Assert.Equal(50, firstCache.Get<int>("keyWillExpire"));
            Assert.Equal(2, firstCache.LocalCache.Count);
            Assert.Equal(1, secondCache.LocalCache.Count);

            // Remove key from second machine and ensure first cache is cleared.
            secondCache.Remove("willCacheLocallyOnFirst");
            
            Task.Delay(TimeSpan.FromMilliseconds(600)).Wait();
            Assert.Equal(0, firstCache.LocalCache.Count);
            Assert.Equal(0, secondCache.LocalCache.Count);
        }
    }
}
