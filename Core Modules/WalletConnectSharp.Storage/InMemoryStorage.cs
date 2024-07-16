using System.Collections.Concurrent;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Storage.Interfaces;

namespace WalletConnectSharp.Storage
{
    public class InMemoryStorage : IKeyValueStorage
    {
        protected ConcurrentDictionary<string, object> Entries = new ConcurrentDictionary<string, object>();
        protected bool Initialized = false;
        protected bool Disposed;

        public virtual Task Init()
        {
            if (Initialized)
                return Task.CompletedTask;

            Initialized = true;
            return Task.CompletedTask;
        }

        public virtual Task<string[]> GetKeys()
        {
            IsInitialized();
            return Task.FromResult(Entries.Keys.ToArray());
        }

        public virtual async Task<T[]> GetEntriesOfType<T>()
        {
            IsInitialized();
            // GetEntries is thread-safe
            return (await GetEntries()).OfType<T>().ToArray();
        }

        public virtual Task<object[]> GetEntries()
        {
            IsInitialized();
            return Task.FromResult(Entries.Values.ToArray());
        }

        public virtual Task<T> GetItem<T>(string key)
        {
            IsInitialized();
            return Task.FromResult(Entries[key] is T ? (T)Entries[key] : default);
        }

        public virtual Task SetItem<T>(string key, T value)
        {
            IsInitialized();
            Entries[key] = value;

            return Task.CompletedTask;
        }

        public virtual Task RemoveItem(string key)
        {
            IsInitialized();
            Entries.Remove(key, out _);

            return Task.CompletedTask;
        }

        public virtual Task<bool> HasItem(string key)
        {
            IsInitialized();
            return Task.FromResult(Entries.ContainsKey(key));
        }

        public virtual Task Clear()
        {
            IsInitialized();
            Entries.Clear();

            return Task.CompletedTask;
        }

        protected void IsInitialized()
        {
            if (!Initialized)
            {
                throw new InvalidOperationException($"{nameof(Storage)} module not initialized.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Disposed = true;
        }
    }
}
