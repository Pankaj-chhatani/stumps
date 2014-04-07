﻿namespace Stumps.Server
{

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using Stumps.Server.Data;
    using Stumps.Server.Utility;

    /// <summary>
    /// A class that represents an the environment and configuration of a proxy server.
    /// </summary>
    public class StumpsServerInstance : IDisposable
    {

        /// <summary>
        ///     The format for a URI for an insecure HTTP connection.
        /// </summary>
        private const string InsecureUriFormat = "http://{0}/";

        /// <summary>
        ///     The format for a URI for a secure HTTP connection.
        /// </summary>
        private const string SecureUriFormat = "https://{0}/";

        private readonly IServerFactory _serverFactory;

        private readonly List<StumpContract> _stumpList;
        private readonly Dictionary<string, StumpContract> _stumpReference;

        private readonly IDataAccess _dataAccess;
        private IStumpsServer _server;
        private bool _disposed;
        private ReaderWriterLockSlim _lock;

        private bool _recordTraffic;
        private bool _lastKnownStumpsEnabledState;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:Stumps.Server.StumpsServerInstance"/> class.
        /// </summary>
        /// <param name="serverFactory">The factory used to initialize new server instances.</param>
        /// <param name="proxyId">The unique identifier of the proxy.</param>
        /// <param name="dataAccess">The data access provider used by the instance.</param>
        public StumpsServerInstance(IServerFactory serverFactory, string proxyId, IDataAccess dataAccess)
        {

            if (serverFactory == null)
            {
                throw new ArgumentNullException("serverFactory");
            }

            _serverFactory = serverFactory;

            this.ServerId = proxyId;

            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _dataAccess = dataAccess;

            // Setup the objects needed to keep track of Stumps.
            _stumpList = new List<StumpContract>();
            _stumpReference = new Dictionary<string, StumpContract>(StringComparer.OrdinalIgnoreCase);

            // Setup the recordings maintained by the server instance.
            this.Recordings = new Recordings();

            // Initialize the server
            InitializeServer();

            // Initialize the Stumps
            InitializeStumps();

        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="T:Stumps.Server.StumpsServerInstance"/> class.
        /// </summary>
        ~StumpsServerInstance()
        {
            this.Dispose(false);
        }

        /// <summary>
        ///     Gets or sets a value indicating whether to automatically start the instance.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the instance should automatically; otherwise, <c>false</c>.
        /// </value>
        public bool AutoStart { get; set; }

        /// <summary>
        ///     Gets or sets the name of the external host.
        /// </summary>
        /// <value>
        ///     The name of the external host.
        /// </value>
        public string ExternalHostName { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the instance is running.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the instance is running; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunning
        {
            get
            {
                var isServerRunning = _server != null && _server.IsRunning;
                return isServerRunning;
            }
        }

        /// <summary>
        ///     Gets or sets the port the Stumps server is listening on for incomming HTTP requests.
        /// </summary>
        /// <value>
        ///     The port the Stumps server is listening on for incomming HTTP requests.
        /// </value>
        public int ListeningPort { get; set; }

        /// <summary>
        ///     Gets the recorded HTTP requests and responses.
        /// </summary>
        /// <value>
        ///     The recorded HTTP requests and responses.
        /// </value>
        public Recordings Recordings { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether to record all traffic.
        /// </summary>
        /// <value>
        ///   <c>true</c> if traffic should be recorded; otherwise, <c>false</c>.
        /// </value>
        public bool RecordTraffic
        {
            get
            {
                return _recordTraffic;
            }

            set 
            { 
                if (value)
                {
                    _lastKnownStumpsEnabledState = this.StumpsEnabled;

                    if (this.RecordingBehavior == RecordingBehavior.DisableStumps)
                    {
                        this.StumpsEnabled = false;
                    }
                }
                else
                {
                    this.StumpsEnabled = _lastKnownStumpsEnabledState;
                }

                _recordTraffic = value;
            }
        }

        /// <summary>
        ///     Gets or sets the behavior of the instance when recording traffic.
        /// </summary>
        /// <value>
        ///     The behavior of the instance when recording traffic.
        /// </value>
        public RecordingBehavior RecordingBehavior
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use stumps when serving requests.
        /// </summary>
        /// <value>
        ///   <c>true</c> to use stumps when serving requests; otherwise, <c>false</c>.
        /// </value>
        public bool StumpsEnabled
        {
            get { return _server.StumpsEnabled; }
            set { _server.StumpsEnabled = value; }
        }

        /// <summary>
        ///     Gets the number of requests served with the proxy.
        /// </summary>
        /// <value>
        ///     The number of requests served with the proxy.
        /// </value>
        public int RequestsServedWithProxy
        {
            get { return _server.RequestsServedWithProxy; }
        }

        /// <summary>
        ///     Gets the number requests served with a Stump.
        /// </summary>
        /// <value>
        ///     The number of requests served with a Stumps.
        /// </value>
        public int RequestsServedWithStump
        {
            get { return _server.RequestsServedWithStump; }
        }

        /// <summary>
        ///     Gets or sets the unique identifier for the server.
        /// </summary>
        /// <value>
        ///     The unique identifier for the server.
        /// </value>
        public string ServerId { get; set; }

        /// <summary>
        ///     Gets the count of Stumps in the collection.
        /// </summary>
        /// <value>
        ///     The count of Stumps in the collection.
        /// </value>
        public int StumpCount
        {
            get { return _stumpList.Count; }
        }

        /// <summary>
        ///     Gets the total number of requests served.
        /// </summary>
        /// <value>
        ///     The total number of requests served.
        /// </value>
        public int TotalRequestsServed
        {
            get { return _server.TotalRequestsServed; }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether the exernal host requires SSL.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the external host requires SSL; otherwise, <c>false</c>.
        /// </value>
        public bool UseSsl { get; set; }

        /// <summary>
        ///     Creates a new Stump.
        /// </summary>
        /// <param name="contract">The contract used to create the Stump.</param>
        /// <returns>
        ///     An updated <see cref="T:Stumps.Server.StumpContract"/>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="contract"/> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentException">A stump with the same name already exists.</exception>
        public StumpContract CreateStump(StumpContract contract)
        {

            if (contract == null)
            {
                throw new ArgumentNullException("contract");
            }

            if (string.IsNullOrEmpty(contract.StumpId))
            {
                contract.StumpId = RandomGenerator.GenerateIdentifier();
            }

            if (this.StumpNameExists(contract.StumpName))
            {
                throw new ArgumentException(Resources.StumpNameUsedError);
            }

            var entity = ContractEntityBinding.CreateEntityFromContract(contract);

            _dataAccess.StumpCreate(this.ServerId, entity, contract.Request.GetBody(), contract.Response.GetBody());

            UnwrapAndAddStump(contract);

            return contract;

        }

        /// <summary>
        ///     Deletes an existing stump.
        /// </summary>
        /// <param name="stumpId">The unique identifier for the Stump.</param>
        public void DeleteStump(string stumpId)
        {

            _lock.EnterWriteLock();

            var stump = _stumpReference[stumpId];
            _stumpReference.Remove(stumpId);
            _stumpList.Remove(stump);
            _server.DeleteStump(stumpId);

            _dataAccess.StumpDelete(this.ServerId, stumpId);

            _lock.ExitWriteLock();

        }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {

            this.Dispose(true);
            GC.SuppressFinalize(this);

        }

        /// <summary>
        ///     Finds a list of all Stump contracts.
        /// </summary>
        /// <returns>
        ///     A generic list of all <see cref="T:Stumps.Server.StumpContract"/> objects.
        /// </returns>
        public IList<StumpContract> FindAllContracts()
        {

            _lock.EnterReadLock();

            var stumpContractList = this._stumpList.ToList();

            _lock.ExitReadLock();

            return stumpContractList;

        }

        /// <summary>
        ///     Finds an existing stump.
        /// </summary>
        /// <param name="stumpId">The unique identifier for the Stump.</param>
        /// <returns>
        ///     A <see cref="T:Stumps.Server.StumpContract"/> with the specified <paramref name="stumpId"/>.
        /// </returns>
        /// <remarks>
        ///     A <c>null</c> value is returned if a Stump is not found.
        /// </remarks>
        public StumpContract FindStump(string stumpId)
        {

            _lock.EnterReadLock();

            var stump = _stumpReference[stumpId];
            _lock.ExitReadLock();
            return stump;

        }
        
        /// <summary>
        /// Determines if a stump with the specified name exists.
        /// </summary>
        /// <param name="stumpName">The name of the stump.</param>
        /// <returns>
        ///     <c>true</c> if a Stump with the specified name already exists; otherwise, <c>false</c>.
        /// </returns>
        public bool StumpNameExists(string stumpName)
        {

            var stumpList = new List<StumpContract>(FindAllContracts());
            var stump = stumpList.Find(s => s.StumpName.Equals(stumpName, StringComparison.OrdinalIgnoreCase));
            var stumpNameExists = stump != null;

            return stumpNameExists;

        }

        /// <summary>
        ///     Stops this instance of the Stumps server.
        /// </summary>
        public void Shutdown()
        {

            if (_server != null)
            {
                _server.Shutdown();
            }

        }

        /// <summary>
        ///     Starts this instance of the Stumps server.
        /// </summary>
        public void Start()
        {

            if (_server != null)
            {
                _server.Start();
            }

        }

        /// <summary>
        ///     Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {

            if (!_disposed)
            {

                _disposed = true;

                if (this.IsRunning)
                {
                    this.Shutdown();
                }

                if (_server != null)
                {
                    _server.Dispose();
                }

                if (_lock != null)
                {
                    _lock.Dispose();
                    _lock = null;

                }

            }

        }
        
        /// <summary>
        ///     Initializes the Stumps server controlled by this instance.
        /// </summary>
        private void InitializeServer()
        {

            // Find the persisted server entity 
            var entity = _dataAccess.ProxyServerFind(this.ServerId);
            this.AutoStart = entity.AutoStart;
            this.ExternalHostName = entity.ExternalHostName;
            this.ListeningPort = entity.Port;
            this.UseSsl = entity.UseSsl;
            this.RecordingBehavior = entity.DisableStumpsWhenRecording
                                         ? RecordingBehavior.DisableStumps
                                         : RecordingBehavior.LeaveStumpsUnchanged;

            if (!string.IsNullOrWhiteSpace(this.ExternalHostName))
            {
                var pattern = this.UseSsl
                                  ? StumpsServerInstance.SecureUriFormat
                                  : StumpsServerInstance.InsecureUriFormat;

                var uriString = string.Format(CultureInfo.InvariantCulture, pattern, this.ExternalHostName);

                var uri = new Uri(uriString);

                _server = _serverFactory.CreateServer(this.ListeningPort, uri);
            }
            else
            {
                // TODO: Choose which method to use for the fallback when no proxy is available.
                _server = _serverFactory.CreateServer(this.ListeningPort, FallbackResponse.Http503ServiceUnavailable);
            }

            _server.RequestFinished += (o, e) =>
            {
                if (this.RecordTraffic)
                {
                    this.Recordings.Add(e.Context);
                }
            };

        }

        /// <summary>
        ///     Initializes the Stumps for the server.
        /// </summary>
        private void InitializeStumps()
        {
            var entities = _dataAccess.StumpFindAll(this.ServerId);

            foreach (var entity in entities)
            {
                var contract = ContractEntityBinding.CreateContractFromEntity(entity);
                UnwrapAndAddStump(contract);
            }

        }
        
        /// <summary>
        ///     Loads a stump from a specified <see cref="T:Stumps.Server.StumpContract"/>.
        /// </summary>
        /// <param name="contract">The <see cref="T:Stumps.Server.StumpContract"/> used to create the Stump.</param>
        private void UnwrapAndAddStump(StumpContract contract)
        {

            _lock.EnterWriteLock();

            var stump = ContractBindings.CreateStumpFromContract(contract);

            _stumpList.Add(contract);
            _stumpReference.Add(stump.StumpId, contract);
            _server.AddStump(stump);

            _lock.ExitWriteLock();

        }

    }

}