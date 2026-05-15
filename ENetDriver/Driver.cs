using ENet_Driver;
using ENet_Driver.Data;
using ENet_Driver.Network;
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
        private Action<string> _logger = Console.WriteLine;



        #region Initialization / Deinitialization

        /// <summary>
        /// Initializes Driver as server, initializing and configuring thread workers. Also initializes ENet.
        ///  Returns a Driver instance so optional configuration methods can be fluently called immediately.
        /// IMPORTANT: This method must be passed an instance of a user-defined class that inherits from
        ///  AbstractDataProcessor, which will actually process incoming data. AbstractDataProcessor only
        ///  implements basic run and  enqueue/dequeue operations; the user-defined subclass must implement
        ///  logic accordingly.
        /// </summary>
        /// <param name="processorInstance"> Configured instance of user-defined class that inherits from AbstractDataProcessor. </param>
        /// <param name="port"> Port to run the ENet host on (IP address is irrelevant for listening socket). </param>
        public Driver Initialize(AbstractDataProcessor processorInstance, ushort port)
        {
            // Throw exception if Driver has already been initialized.
            if (_state != State.Uninitialized)
            {
                _logger.Invoke("Failed to initialize Driver - Driver already initialized.");
                return instance;
            }

            // Initialize ENet and thread workers, then set Driver state.
            ENet.Library.Initialize();

            _processThreadWorker = new ProcessThreadWorker(processorInstance, _logger);
            _networkThreadWorker = new NetworkThreadWorker(port, _logger);

            _state = State.Initialized;

            return instance;
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

        #endregion

        #region Optional Setup

        /// <summary>
        /// Configures optional host settings for the ENetServer, like peer and channel limits. Cannot be called before Initialize().
        /// </summary>
        /// <param name="peerLimit"> Maximum number of peers that can be connected at once (library limits to 4095). Default 64. </param>
        /// <param name="channelLimit"> Maximum number of channels that the host will communicate on with peers. Default 2. </param>
        public Driver SetOptionalHostSettings(int peerLimit = 64, int channelLimit = 2)
        {
            // Prevent attempting to set Worker settings if not yet initialized (null until initialized).
            if (_state == State.Uninitialized)
            {
                _logger.Invoke("[Driver] Cannot set optional host settings - Driver not yet initialized.");
                return instance;
            }

            _networkThreadWorker.SetOptionalHostSettings(peerLimit, channelLimit);
            return instance;
        }

        /// <summary>
        /// Configures optional peer ping and timeout settings for the ENetServer. Cannot be called before Initialize().
        /// </summary>
        /// <param name="pingIntervalMS"> Interval in milliseconds to send pings to the client peer. Default 5000ms. </param>
        /// <param name="pingAttemptLimit"> Maximum (minimum?) number of consecutive failed ping attempts before the peer 'times out'. Default 5. </param>
        /// <param name="timeoutMinimumMS"> Minimum duration in milliseconds that a ping has not been acknowledged before timing out. Default 10000ms. </param>
        /// <param name="timeoutMaximumMS"> Maximum duration in milliseconds that a ping has not been acknowledged before timing out. Default 30000ms. </param>
        public Driver SetOptionalPeerTimeoutSettings(uint pingIntervalMS = 5000, uint pingAttemptLimit = 5,
            uint timeoutMinimumMS = 10000, uint timeoutMaximumMS = 30000)
        {
            // Prevent attempting to set Worker settings if not yet initialized (null until initialized).
            if (_state == State.Uninitialized)
            {
                _logger.Invoke("[Driver] Cannot set optional peer timeout settings - Driver not yet initialized.");
                return instance;
            }

            _networkThreadWorker.SetOptionalPeerTimeoutSettings(pingIntervalMS, pingAttemptLimit, timeoutMinimumMS, timeoutMaximumMS);
            return instance;
        }

        /// <summary>
        /// Configures optional poll (run) time limits for both the DataProcessor and the ENetServer. This limits
        ///  the amount of time that they can remain in a single mode (ex. incoming or outgoing command handling),
        ///  preventing unexpected hangs in certain situations. Cannot be called before Initialize().
        /// </summary>
        /// <param name="maxPollTimeoutMS"> The maximum duration in milliseconds that it can remain in one polling mode (ex. receive or send). Default 1000ms. </param>
        /// <param name="minPollTimeoutMS"> The minimum duration in milliseconds that it can remain in one polling mode (ex. receive or send). Default 100ms. </param>
        public Driver SetOptionalPollIntervals(int maxPollTimeoutMS = 1000, int minPollTimeoutMS = 100)
        {
            // Prevent attempting to set Worker settings if not yet initialized (null until initialized).
            if (_state == State.Uninitialized)
            {
                _logger.Invoke("[Driver] Cannot set optional poll intervals - Driver not yet initialized.");
                return instance;
            }

            // Set poll intervals for both workers.
            _processThreadWorker.SetOptionalPollIntervals(maxPollTimeoutMS, minPollTimeoutMS);
            _networkThreadWorker.SetOptionalPollIntervals(maxPollTimeoutMS, minPollTimeoutMS);
            return instance;
        }

        #endregion

        #region Optional Logging Configuration

        /// <summary>
        /// Optionally sets the interval to log health tracking information for the data processor. Will output data like
        ///  average items remaining in queue at each interval and average milliseconds taken to process all incoming
        ///  messages each interval. Set interval to 0 to disable health information logging.
        /// </summary>
        /// <param name="intervalSeconds"> The interval (in seconds) to display health information to the log. Set to 0 to disable health logging. Default 60s. </param>
        public Driver SetOptionalHealthLoggingInterval(uint intervalSeconds = 60)
        {
            _processThreadWorker.SetHealthLoggingInterval(intervalSeconds);
            return instance;
        }

        /// <summary>
        /// Optionally sets the output (print) method used for logging by this application. If not explicitly set
        ///  here, defaults to Console.WriteLine().
        /// </summary>
        /// <param name="logMethod"> The method to use for logging. Must accept a string argument and return void. </param>
        public Driver SetOptionalLogger(Action<string> logMethod)
        {
            // Prevent attempting to set Worker settings if not yet initialized (null until initialized).
            if (_state == State.Uninitialized)
            {
                _logger.Invoke("[Driver] Cannot set logger - Driver not yet initialized.");
                return instance;
            }

            _logger = logMethod;
            _processThreadWorker.SetLogger(logMethod);
            _networkThreadWorker.SetLogger(logMethod);

            return instance;
        }

        #endregion

        #region Start / Stop

        /// <summary>
        /// Starts worker threads for data processing and network operations.
        /// </summary>
        public void StartDriver()
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
        /// Stops data processor and network worker threads gracefully.
        /// </summary>
        public void StopDriver()
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
            private readonly Thread thread;
            private readonly AbstractDataProcessor processor;

            private Action<string> _logger;

            internal ProcessThreadWorker(AbstractDataProcessor processorInstance, Action<string> logMethod)
            {
                // Assign existing instance of AbstractDataProcessor subclass, then configure with REQUIRED parameters.
                processor = processorInstance;
                processor.SetRequiredQueueReferences(Instance.NetSendQueue, Instance.NetRecvQueue);

                // Thread will call the DataProcessor.Run() method, which loops until commanded to stop.
                thread = new(processor.Run);

                // Sets the logger method used here and within the data processor instance. Must be explicitly called here.
                processor.SetLogger(logMethod);
                _logger = logMethod;
            }



            /// <summary>
            /// Starts the worker thread, beginning data processing operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();
                _logger.Invoke("[STARTUP] Starting DataProcessor thread.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for any remaining work to finish before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Commands the worker to stop, which will gracefully exit the threaded loop.
                processor.CommandStop();

                // Wait for the DataProcessor.Run() function to return, then join the thread (BLOCKS).
                _logger.Invoke("[EXIT] Waiting for DataProcessor thread to stop...");
                thread.Join();
                _logger.Invoke("[EXIT] DataProcessor thread stopped successfully.");
            }

            #region Optional Setup

            internal void SetOptionalPollIntervals(int maxPollTimeoutMS, int minPollTimeoutMS)
            {
                processor.SetOptionalPollIntervals(maxPollTimeoutMS, minPollTimeoutMS);
            }

            internal void SetLogger(Action<string> logMethod)
            {
                _logger = logMethod;
            }

            internal void SetHealthLoggingInterval(uint intervalSeconds)
            {
                processor.SetHealthLoggingInterval(intervalSeconds);
            }

            #endregion
        }

        /// <summary>
        /// This class is responsible for managing the Network/ENet thread.
        /// </summary>
        private class NetworkThreadWorker
        {
            private readonly Thread thread;
            private readonly ENetServer server;

            private Action<string> _logger;

            internal NetworkThreadWorker(ushort port, Action<string> logMethod)
            {
                // Construct and initialize ENetServer then set REQUIRED parameters, but do not start yet.
                server = new ENetServer();
                server.SetRequiredQueueReferences(Instance.NetSendQueue, Instance.NetRecvQueue);
                server.SetRequiredHostListenPort(port);

                // Thread will call the ENetServer.Run() method, which actually starts the Host.
                thread = new(server.Run);

                // Sets the logger method used here and within the server instance. Must be explicitly called here.
                server.SetLogger(logMethod);
                _logger = logMethod;
            }



            /// <summary>
            /// Starts the worker thread, beginning server operations on a separate thread.
            /// </summary>
            internal void StartThread()
            {
                thread.Start();

                _logger.Invoke($"[STARTUP] Starting server host thread on port {server.GetAddress().Port}.");
            }

            /// <summary>
            /// Stops the worker thread, waiting for server to shut down before joining and returning.
            /// </summary>
            internal void StopThread()
            {
                // Command the server to stop, which will gracefully exit the threaded loop.
                server.CommandStop();

                // Wait for the Run() function to return, then join the thread (BLOCKS).
                _logger.Invoke("[EXIT] Waiting for ENetServer thread to stop...");
                thread.Join();
                _logger.Invoke("[EXIT] ENetServer thread stopped successfully.");
            }

            #region Optional Setup

            internal void SetOptionalHostSettings(int peerLimit, int channelLimit)
            {
                server.SetOptionalHostSettings(peerLimit, channelLimit);
            }

            internal void SetOptionalPeerTimeoutSettings(uint pingIntervalMS, uint pingAttemptLimit, uint timeoutMinimumMS, uint timeoutMaximumMS)
            {
                server.SetOptionalPeerTimeoutSettings(pingIntervalMS, pingAttemptLimit, timeoutMinimumMS, timeoutMaximumMS);
            }

            internal void SetOptionalPollIntervals(int maxPollTimeoutMS, int minPollTimeoutMS)
            {
                server.SetOptionalPollIntervals(maxPollTimeoutMS, minPollTimeoutMS);
            }

            internal void SetLogger(Action<string> logMethod)
            {
                _logger = logMethod;
                server.SetLogger(logMethod);
            }

            #endregion
        }

        #endregion





        ///             NETWORK DATA (STATUS) CODES
        /// 0    | Default value from ENet, should only be used for ACK responses (ex. ACK from remote peer after successful user-initiated connect)
        /// 
        /// 100  | Successful peer-initiated connection
        /// 101  | Successful self-initiated connection
        /// 200  | Peer-initiated disconnect
        /// 201  | Self-initiated disconnect
        /// 300  | Peer-initiated disconnect on shutdown
        /// 301  | Self-initiated disconnect on shutdown
        /// 400  | Timeout
        /// 1000 | Client checksum validation error
        /// 1001 | Server checksum validation error
        /// 1100 | Client login token validation error
        /// 1101 | Server login token validation error
        /// 1200 | Client validation ACK error
        /// 1201 | Server validation ACK error
        /// 1300 | Client missing login token to send
        /// 1301 | Server missing login token to send
        /// 1500 | Master server connection error
        /// 2000 | Disallowed new connection
        /// 2500 | Disconnect on message from unknown Peer
        /// 3000 | Rejected blacklisted address
    }
}
