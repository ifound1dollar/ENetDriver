using ENetDriver.Config;
using ENetDriver.Data;
using ENetDriver.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ENetDriver
{
    /// <summary>
    /// The NetDriverBase class is the central driver for the ENetDriver, managing the data processing
    ///  and network thread logic. An internal thread-safe state machine ensures that only one instance
    ///  of the driver can be running at one time, and also enables any thread to utilize public
    ///  methods to carry out network send tasks. Users must implement a concrete subclass that handles
    ///  data processing logic, then instantiate an object of that class and utilize it accordingly.
    /// Users must first Initialize() the driver with configuration settings, then Run() the driver to
    ///  execute the data processor loop synchronously (blocking). The Stop() method must be invoked
    ///  to gracefully stop the driver, causing the Run() method to return once stopped. Lastly, the
    ///  user must Deinitialize() the driver before the application exits.
    /// </summary>
    public abstract class NetDriverBase
    {
        // Store NetDriver state in STATIC VOLATILE field, which enables thread safety.
        public enum State { Uninitialized, Initialized, Running, Stopping, Stopped }
        private static volatile State _state;



        private readonly BlockingCollection<NetSendObject> _netSendQueue;
        private readonly BlockingCollection<NetRecvObject> _netRecvQueue;
        private readonly NetworkThreadWorker _networkThreadWorker;
        private NetDriverConfig _config;

        public NetDriverBase()
        {
            _netSendQueue = [];
            _netRecvQueue = [];
            _networkThreadWorker = new(_netSendQueue, _netRecvQueue);
            _config = NetDriverConfig.Default();
        }



        #region User Methods (Abstract & Built-in)

        /// <summary>
        /// This method will be invoked whenever an incoming object is received and must be processed.
        ///  Subclasses must implement this abstract method to perform the desired logic on receive
        ///  event. NOTE: This method should never be manually called.
        /// </summary>
        /// <param name="recvObject"> The incoming NetRecvObject containing whatever data was received. </param>
        protected abstract void ProcessIncomingData(NetRecvObject recvObject);

        /// <summary>
        /// Attempts to connect to a remote peer at the provided IP:port. Enqueues an outgoing connect object.
        /// </summary>
        /// <param name="ip"> The IP address of the remote peer. </param>
        /// <param name="port"> The network port of the remote peer. </param>
        public void ConnectToPeer(string ip, ushort port)
        {
            if (_state != State.Running)
            {
                _config.Logger.Invoke("[WARN] Cannot connect to peer if driver is not running.");
                return;
            }

            NetSendObject obj = NetSendObject.CreateConnect(ip, port, 100u);
            _netSendQueue.Add(obj);
        }

        /// <summary>
        /// Attempts to disconnect from a currently-connected remote peer with the provided peer ID. Enqueues
        ///  an outgoing disconnect object.
        /// </summary>
        /// <param name="peerId"> The numeric ID of the peer. </param>
        public void DisconnectFromPeer(uint peerId)
        {
            if (_state != State.Running)
            {
                _config.Logger.Invoke("[WARN] Cannot disconnect from peer if driver is not running.");
                return;

            }

            NetSendObject obj = NetSendObject.CreateDisconnect(peerId, 200u);
            _netSendQueue.Add(obj);
        }

        /// <summary>
        /// Attempts to send a message to a remote peer. Peer and payload data must all be contained within
        ///  the provided NetSendObject. Enqueues an outgoing message object.
        /// </summary>
        /// <param name="sendObject"> The constructed NetSendObject containing peer and payload data. </param>
        public void SendMessage(NetSendObject sendObject)
        {
            if (_state != State.Running)
            {
                _config.Logger.Invoke("[WARN] Cannot send message if driver is not running.");
                return;

            }

            _netSendQueue.Add(sendObject);
        }

        /// <summary>
        /// Prints a message to the log. Prints string exactly as it is given.
        /// </summary>
        /// <param name="message"> The message to print to the log. </param>
        protected void LogMessage(string message)
        {
            _config.Logger.Invoke(message);
        }

        #endregion



        #region Public: Management Methods (Initialize / Deinitialize / Run / Stop)

        /// <summary>
        /// Initializes the Driver and the underlying ENet native library, passing configuration data for data
        ///  processing and network thread operations. This method must be called before the driver can be run
        ///  using the Run() method.
        /// </summary>
        /// <param name="netDriverConfig"> The configuration settings that will be used for data processing logic. </param>
        /// <param name="serverConfig"> The configuration settings that will be used by the server host. </param>
        public void Initialize(NetDriverConfig netDriverConfig, ServerConfig serverConfig)
        {
            // Throw exception if Driver has already been initialized.
            if (_state != State.Uninitialized)
            {
                Console.WriteLine("[WARN] Cannot initialize driver again once it has already been initialized.");
                return;
            }

            // Set state immediately for thread-safety purposes.
            _state = State.Initialized;

            // Initialize ENet and thread workers, then set Driver state.
            ENet.Library.Initialize();
            _networkThreadWorker.SetConfig(serverConfig);
            _config = netDriverConfig;
        }

        /// <summary>
        /// De-initializes the Driver, like deinitializing the underlying ENet library. Should be called
        ///  before application exit.
        /// </summary>
        public void Deinitialize()
        {
            // Throw exception if currently running or actively stopping, or uninitialized.
            if (_state == State.Uninitialized)
            {
                Console.WriteLine("[WARN] Cannot deinitialize driver which has not been initialized.");
                return;
            }
            else if (_state == State.Running)
            {
                Console.WriteLine("[WARN] Cannot deinitialize driver while currently running. Please stop threads first.");
                return;
            }
            else if (_state == State.Stopping)
            {
                Console.WriteLine("[WARN] Cannot deinitialize driver while actively stopping. Please wait until fully stopped.");
                return;
            }

            // Immediately update state for thread-safety purposes.
            _state = State.Uninitialized;

            // De-initialize ENet library.
            ENet.Library.Deinitialize();
        }

        /// <summary>
        /// Runs the driver synchronously, starting the network host thread and the data processing loop. This
        ///  method blocks until the network thread successfully stops after the Stop() method is called.
        /// </summary>
        public void Run()
        {
            // Verify proper state.
            if (_state == State.Uninitialized)
            {
                Console.WriteLine("[WARN] Cannot start threads before Driver is initialized.");
                return;
            }
            else if (_state == State.Running)
            {
                Console.WriteLine("[WARN] Cannot start threads which are already running.");
                return;
            }
            else if (_state == State.Stopping)
            {
                Console.WriteLine("[WARN] Cannot start threads while actively stopping. Please wait until fully stopped.");
                return;
            }

            // Immediately set state to Running for thread-safety purposes.
            _state = State.Running;

            // Start network thread worker and data processor loop.
            _networkThreadWorker.StartThread();
            RunLoop();                          // Blocks until network thread successfully joins and state becomes Stopped.

            // Clear both queues after successful stop.
            while (_netSendQueue.TryTake(out _)) ;
            while (_netRecvQueue.TryTake(out _)) ;
        }

        private void RunLoop()
        {
            _config.Logger.Invoke("[STARTUP] Started data processing loop.");

            /* ----- TIME LIMITS ----- //
            // This variable limits how long the server will continuously loop to perform either incoming or
            //  outgoing dispatches. Without this limit, the server might get bogged down handling incoming
            //  events indefinitely if traffic is heavy enough (the service might infinitely be processing
            //  new incoming events without ever moving onto outgoing events). The server will flip-flop
            //  between incoming and outgoing event processing at this interval.
            // This interval determines the MAXIMUM time it will sit at each mode; both modes will
            //  automatically flip back to the previous mode if there is no work to enqueue at all (this
            //  interval is just an anti-block failsafe).
            // The automatic flip time is calculated at 1/10 of this maximum interval (we call it the minimum
            //  interval). This is the minimum amount of time that the server should run in each mode, and
            //  this minimum is important to ensuring that the application is not switching contexts each
            //  tick and leaving each thread at 100% CPU usage.
            // These values must be positive (obviously, is milliseconds).
            */
            Stopwatch stopwatch = new();
            try
            {
                // Continuously loop until Stopped (continue while Stopping to ensure only returns after network thread joins).
                while (_state == State.Running || _state == State.Stopping)
                {
                    // Restart stopwatch each loop, then loop until maximum timeout duration in milliseconds.
                    stopwatch.Restart();
                    while (stopwatch.ElapsedMilliseconds < _config.MaxPollTimeMS)
                    {
                        /* ----- TRYTAKE DOCUMENTATION -----
                        // Try to dequeue an item from the queue. Immediately returns the item if successful, otherwise stops
                        //  blocking after duration has elapsed. Delay for poll minimum duration.
                        // If at least one item is found before the timeout, then we will return to the top of the loop and
                        //  try again until we EITHER 1) run out of items to take, or 2) exceed the maximum poll duration.
                        // If no item is found before the timeout, then we will immediately break and flip back to processing
                        //  incoming events (no reason to sit here idle with no items to take).
                        */
                        if (!_netRecvQueue.TryTake(out NetRecvObject? item, _config.MinPollTimeMS))
                        {
                            // NO ITEM AVAILABLE TO TAKE/DEQUEUE - BREAK FROM INNER LOOP
                            break;
                        }

                        // Else if item was taken, call abstract processor method.
                        ProcessIncomingData(item);
                    }

                }
            }
            catch (Exception e)
            {
                _config.Logger.Invoke($"[EXCEPTION] :: {e}.");
                _config.Logger.Invoke("[EXCEPTION] Stopping Driver operations...");

                // Command to stop, waiting until state is Stopped before returning (allows graceful exit).
                Stop();
                while (_state != State.Stopped)
                {
                    Thread.Sleep(100);      // 100ms increments.
                }
            }

            _config.Logger.Invoke("[EXIT] Stopped data processing loop.");
        }

        /// <summary>
        /// Stops the NetDriver gracefully, executing stop logic in the background (this method does not block).
        ///  The Run() method will continue blocking until the network thread successfully stops, at which time
        ///  the method will finally return. This method simply begins the stop process.
        /// </summary>
        public void Stop()
        {
            // If state is not Running, cannot stop threads.
            if (_state != State.Running)
            {
                Console.WriteLine("[WARN] Cannot stop threads which are not running. State: " + _state.ToString());
                return;
            }

            // Immediately set state to Stopping to note that the driver is currently in the stop process.
            _state = State.Stopping;

            _config.Logger.Invoke("[EXIT] Stopping net driver...");

            // Run asynchronous task to stop network thread, updating state once successfully stopped.
            Task.Run(async () =>
            {
                // The StopThread() method blocks until Thread.Join() returns successfully.
                _networkThreadWorker.StopThread();

                // Set to Stopped after thread fully stops. This will break from Run() loop and allow safe application exit.
                _state = State.Stopped;
            });
        }

        #endregion

        /// <summary>
        /// This class is responsible for managing the Network/ENet thread.
        /// </summary>
        private class NetworkThreadWorker
        {
            private readonly Thread _thread;
            private readonly Server _server;

            internal NetworkThreadWorker(BlockingCollection<NetSendObject> netSendQueue, BlockingCollection<NetRecvObject> netRecvQueue)
            {
                // Construct and initialize ENetServer then set configuration, but do not start yet.
                _server = new Server(netSendQueue, netRecvQueue);

                // Thread will call the ENetServer.Run() method, which actually starts the Host.
                _thread = new(_server.Run);
            }

            internal void SetConfig(ServerConfig config)
            {
                _server.SetConfiguration(config);
            }



            /// <summary>
            /// Starts the worker thread, beginning server operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                _thread.Start();

                _server.GetConfiguration().Logger.Invoke($"[STARTUP] Started network thread with server host on port {_server.GetConfiguration().Port}.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning. This
            ///  method blocks until the thread has successfully stopped.
            /// </summary>
            internal void StopThread()
            {
                // Command the server to stop, which will gracefully exit the threaded loop.
                _server.CommandStop();

                // Block until the thread completes and joins (after threaded loop graceful exit).
                _thread.Join();
                _server.GetConfiguration().Logger.Invoke("[EXIT] Stopped network thread.");
            }
        }
    }
}
