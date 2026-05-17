using ENetDriver.Config;
using ENetDriver.Data;
using ENetDriver.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetDriver
{
    public enum ENetAction { None, Connect, Disconnect, Timeout, Message }

    public class Driver
    {
        // ----- DESCRIPTION ----- //
        // The Driver class is a singleton that manages a multi-threaded networking system. This system
        //  manages two separate threads, one for data processing and one for network events, which
        //  work concurrently to dramatically increase the efficiency of service tasks.
        // The main thread should access this class Instance to enqueue and dequeue objects if necessary,
        //  never directly interacting with either of the running threads. This class strictly prohibits
        //  access to the running threads, ensuring thread safety and simplicity for developers. 
        // The Worker nested classes manage their own specific object instances that perform relevant tasks.
        //  The DataProcessor classes handle all data processing. The ENetServer class starts, stops, and
        //  runs the ENet host to handle network sending and receiving.

        // The main thread needs only to call the Initialize(), StartThreadedOperations(), and
        //  StopThreadedOperations() methods to use this library. These methods fully encapsulate all
        //  networking operations.
        // Additionally, the various SetOptional_() methods can be used to set optional configuration
        //  for the driver, like configuring the number of ENet communication channels or peer timeout
        //  durations.

        // As discussed in the high-level documentation, users of this library need to create their
        //  own data processor class that inherits from AbstractDataProcessor and overrides the 
        //  ProcessIncomingData() method. All incoming objects will pass through this method, so any
        //  specific processing must be dispatched from the user-defined override. The base class 
        //  includes the necessary method to enqueue a new NetSendObject to be handled by the ENetServer.
        // Users must utilize the NetSendObject and NetRecvObject classes for working with data.

        // ----- END DESCRIPTION ----- //

        public enum State { Uninitialized, Initialized, Running, Stopped }

        #region Singleton Stuff

        private static readonly Driver instance = new();
        public static Driver Instance { get { return instance; } }

        static Driver()
        {
            // Static constructor allows for thread-safe singleton usage.
            // See: https://csharpindepth.com/articles/singleton, fourth option.
        }
        private Driver()
        {
            // Default constructor
        }

        #endregion



        // Thread-safe queues for communicating data between network and processing threads.
        private BlockingCollection<NetSendObject> NetSendQueue { get; } = [];
        private BlockingCollection<NetRecvObject> NetRecvQueue { get; } = [];

        // These are manually initialized in Initialize(). State is checked everywhere these workers are used,
        //  so temporary nullity is safe here (if not Uninitialized, the workers are guaranteed non-null).
        private ProcessThreadWorker _processThreadWorker = null!;
        private NetworkThreadWorker _networkThreadWorker = null!;

        private State _state;



        #region Public: Initialization/Deinitialization and Start/Stop

        /// <summary>
        /// Initializes the Driver and the underlying ENet native library. Requires a configured instance of a
        ///  user-defined class which derives from AbstractDataProcessor; this user-defined class will perform all
        ///  incoming and outgoing command and message logic (AbstractDataProcessor only handles backend operation -
        ///  all actual logic must be implemented by the user in the concrete subclass). This method also expects
        ///  configuration settings for the server host (data processor will already be configured on initialization).
        /// </summary>
        /// <param name="processorInstance"> Instance of user-defined class that inherits from AbstractDataProcessor. All data processing logic should be defined in this class. </param>
        /// <param name="serverConfig"> The configuration settings that will be used by the server host. </param>
        public void Initialize(AbstractDataProcessor processorInstance, ServerConfig serverConfig)
        {
            // Throw exception if Driver has already been initialized.
            if (_state != State.Uninitialized)
            {
                throw new InvalidOperationException("Cannot initialize driver again once it has already been initialized.");
            }

            // Initialize ENet and thread workers, then set Driver state.
            ENet.Library.Initialize();

            _processThreadWorker = new ProcessThreadWorker(processorInstance);
            _networkThreadWorker = new NetworkThreadWorker(serverConfig);

            _state = State.Initialized;
        }

        /// <summary>
        /// Executes deinitialization operations on the Driver, like deinitializing ENet. Should be called
        ///  before application exit.
        /// </summary>
        public void Deinitialize()
        {
            ENet.Library.Deinitialize();

            _state = State.Uninitialized;
        }

        /// <summary>
        /// Starts worker threads for data processing and network operations. IMPORTANT: This method simply starts the
        ///  threads, but does not block. It is the responsibility of the caller to keep the application running while
        ///  threaded operations are active.
        /// </summary>
        public void StartThreadedOperations()
        {
            // Verify proper state.
            if (_state == State.Uninitialized)
            {
                throw new InvalidOperationException("Cannot start threads before Driver is properly initialized. State: "
                    + _state.ToString());
            }
            else if (_state == State.Running)
            {
                throw new InvalidOperationException("Threaded operations are already running. State: " + _state.ToString());
            }

            // Starts each threaded operation (one for data processing, one for network) here.
            _processThreadWorker.StartThread();
            _networkThreadWorker.StartThread();

            _state = State.Running;
        }

        /// <summary>
        /// Stops data processor and network worker threads gracefully. IMPORTANT: Threads stop gracefully in the background,
        ///  this method does not block. It returns immediately while threads are shutting down.
        /// </summary>
        public void StopThreadedOperations()
        {
            // If state is not Running, cannot stop threads.
            if (_state != State.Running)
            {
                throw new InvalidOperationException("Cannot stop threads which are not running. State: " + _state.ToString());
            }

            // Stops each threaded operation gracefully. NetworkWorker should be stopped first to prevent any
            //  incoming/outgoing messages immediately.
            _networkThreadWorker.StopThread();
            _processThreadWorker.StopThread();

            _state = State.Stopped;
        }

        #endregion



        #region Worker Nested Classes

        /// <summary>
        /// This class is responsible for managing the Serialization/Deserialization thread.
        /// </summary>
        private class ProcessThreadWorker
        {
            private readonly Thread _thread;
            private readonly AbstractDataProcessor _processor;

            internal ProcessThreadWorker(AbstractDataProcessor processorInstance)
            {
                // Assign existing instance of AbstractDataProcessor subclass (already configured).
                _processor = processorInstance;
                _processor.SetRequiredQueueReferences(Instance.NetSendQueue, Instance.NetRecvQueue);

                // Thread will call the DataProcessor.Run() method, which loops until commanded to stop.
                _thread = new(_processor.Run);
            }



            /// <summary>
            /// Starts the worker thread, beginning data processing operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                _thread.Start();
                _processor.GetConfiguration().Logger.Invoke("[STARTUP] Starting DataProcessor thread.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for any remaining work to finish before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Commands the worker to stop, which will gracefully exit the threaded loop.
                _processor.CommandStop();

                // Wait for the DataProcessor.Run() function to return, then join the thread (BLOCKS).
                _processor.GetConfiguration().Logger.Invoke("[EXIT] Waiting for DataProcessor thread to stop...");
                _thread.Join();
                _processor.GetConfiguration().Logger.Invoke("[EXIT] DataProcessor thread stopped successfully.");
            }

        }

        /// <summary>
        /// This class is responsible for managing the Network/ENet thread.
        /// </summary>
        private class NetworkThreadWorker
        {
            private readonly Thread _thread;
            private readonly ENetServer _server;

            internal NetworkThreadWorker(ServerConfig config)
            {
                // Construct and initialize ENetServer then set configuration, but do not start yet.
                _server = new ENetServer(config);
                _server.SetQueueReferences(Instance.NetSendQueue, Instance.NetRecvQueue);

                // Thread will call the ENetServer.Run() method, which actually starts the Host.
                _thread = new(_server.Run);
            }



            /// <summary>
            /// Starts the worker thread, beginning server operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                _thread.Start();

                _server.GetConfiguration().Logger.Invoke($"[STARTUP] Starting server host thread on port {_server.GetPort()}.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Command the server to stop, which will gracefully exit the threaded loop.
                _server.CommandStop();

                // Wait for the Run() function to return, then join the thread (BLOCKS).
                _server.GetConfiguration().Logger.Invoke("[EXIT] Waiting for ENetServer thread to stop...");
                _thread.Join();
                _server.GetConfiguration().Logger.Invoke("[EXIT] ENetServer thread stopped successfully.");
            }
        }

        #endregion

    }
}
