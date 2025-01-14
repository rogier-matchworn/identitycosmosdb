﻿// MIT License Copyright 2019 (c) David Melendez. All rights reserved. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElCamino.AspNetCore.Identity.CosmosDB.Model;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Azure.Cosmos.Linq;

namespace ElCamino.AspNetCore.Identity.CosmosDB
{
    public class IdentityCloudContext : IDisposable
    {
        internal class InternalContext : IDisposable
        {
            private CosmosClient _client = null;
            private Database _db = null;
            private string _databaseId = null;
            private Container _identityContainer;
            private string _identityContainerId;
            private string _sessionToken = string.Empty;
            private bool _disposed = false;

            public InternalContext(IdentityConfiguration config)
            {
                
                _client = new CosmosClient(config.Uri, config.AuthKey, config.Options);
                _databaseId = config.Database;
                _identityContainerId = config.IdentityCollection;
                InitDatabase();
                InitCollection();
            }

            public async Task CreateIfNotExistsAsync()
            {
                await CreateDatabaseAsync();
                await CreateContainerAsync();
            }

            private async Task CreateDatabaseAsync()
            {
                _db = await _client.CreateDatabaseIfNotExistsAsync(_databaseId);
            }

            private void InitDatabase()
            {
                if (_db == null)
                {
                    _db = _client.GetDatabase(_databaseId);
                }
            }

            private async Task CreateContainerAsync()
            {
                var containerResponse = await _db.CreateContainerIfNotExistsAsync(new ContainerProperties(_identityContainerId, "/partitionKey"));
                IdentityContainer = containerResponse.Container;
            }

            private void InitCollection()
            {
                if (IdentityContainer == null)
                {
                    IdentityContainer = _db.GetContainer(_identityContainerId);
                }
            }

            ~InternalContext()
            {
                this.Dispose(false);
            }

            public CosmosClient Client
            {
                get { ThrowIfDisposed(); return _client; }
            }

            public Database Database
            {
                get { ThrowIfDisposed(); return _db; }
            }

            public ConsistencyLevel ConsistencyLevel { get; private set; } = ConsistencyLevel.Session;

            public ItemRequestOptions RequestOptions
            {
                get
                {
                    return new ItemRequestOptions()
                    {
                        ConsistencyLevel = this.ConsistencyLevel,
                        SessionToken = SessionToken,
                    };
                }
            }

            public QueryRequestOptions FeedOptions
            {
                get
                {
                    return new QueryRequestOptions()
                    {
                        EnableScanInQuery = true,
                        ConsistencyLevel = this.ConsistencyLevel,                           
                    };
                }
            }

            public string SessionToken
            {
                get { return _sessionToken; }
            }

            public void SetSessionTokenIfEmpty(string tokenNew)
            {
                if (string.IsNullOrWhiteSpace(_sessionToken))
                {
                    _sessionToken = tokenNew;
                }
            }

            public Container IdentityContainer
            {
                get { ThrowIfDisposed(); return _identityContainer; }
                set { _identityContainer = value; }
            }

            protected void ThrowIfDisposed()
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException(base.GetType().Name);
                }
            }

            public void Dispose()
            {
                this.Dispose(true);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed && disposing)
                {
                    _disposed = true;
                    _client = null;
                    _db = null;
                    _identityContainer = null;
                    _sessionToken = null;
                }
            }
        }
        private bool _disposed = false;

        //Thread safe dictionary 
        private static readonly ConcurrentDictionary<string, InternalContext> ContextCache = new ConcurrentDictionary<string, InternalContext>();
        private string _configHash = null;
        private InternalContext _currentContext = null;     

        public IdentityCloudContext(IdentityConfiguration config)
        {
            _configHash = config.ToString();
            if (!ContextCache.TryGetValue(_configHash, out InternalContext tempContext))
            {
                tempContext = new InternalContext(config);
                ContextCache.TryAdd(_configHash, tempContext);
                Debug.WriteLine(string.Format("ContextCacheAdd {0}", _configHash));
            }
#if DEBUG
            else
            {
                Debug.WriteLine(string.Format("ContextCacheGet {0}", _configHash));
            }
#endif
            _currentContext = tempContext;
        }
        

        ~IdentityCloudContext()
        {
            this.Dispose(false);
        }

        public CosmosClient Client
        {
            get { ThrowIfDisposed(); return _currentContext.Client; }
        }

        public Database Database
        {
            get { ThrowIfDisposed(); return _currentContext.Database; }
        }


        public ItemRequestOptions RequestOptions
        {
            get
            {
                return _currentContext.RequestOptions;
            }
        }

        public QueryRequestOptions QueryOptions
        {
            get
            {
                return _currentContext.FeedOptions;
            }
        }


        public string SessionToken
        {
            get { return _currentContext.SessionToken; }
        }

        public void SetSessionTokenIfEmpty(string tokenNew)
        {
            _currentContext.SetSessionTokenIfEmpty(tokenNew);
        }

        public Container IdentityContainer
        {
            get { ThrowIfDisposed(); return _currentContext.IdentityContainer; }
            set { _currentContext.IdentityContainer = value; }
        }

        public Task CreateIfNotExistsAsync()
        {
            return _currentContext.CreateIfNotExistsAsync();
        }

        protected void ThrowIfDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(base.GetType().Name);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                Debug.WriteLine(string.Format("ContextCacheDispose({0})", _configHash));
                _currentContext = null;
            }
        }
    }

}
