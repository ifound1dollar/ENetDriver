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

        public enum State { Uninitialized, Initialized, Running, Stopping, Stopped }

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

        private volatile State _state;



        #region Public: Initialization / Deinitialization Methods

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
                Console.WriteLine("[WARN] Cannot initialize driver again once it has already been initialized.");
                return;
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
            // Throw exception if currently running or actively stopping, or uninitialized.
            if (_state  == State.Uninitialized)
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

            // De-initialize ENet library, then set Driver state.
            ENet.Library.Deinitialize();
            _state = State.Uninitialized;
        }

        #endregion

        #region Public: Start / Stop / Run / Wait Methods

        /// <summary>
        /// Starts background worker threads for data processing and network operations. This method is non-blocking,
        ///  meaning the user must ensure the application remains running after calling this method. To start threads
        ///  and block/await until stopped, use Run() or RunAsync().
        /// </summary>
        public void Start()
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

            // Starts each threaded operation (one for data processing, one for network) here.
            _processThreadWorker.StartThread();
            _networkThreadWorker.StartThread();

            _state = State.Running;
        }

        /// <summary>
        /// Stops data processor and network worker threads gracefully. This method is non-blocking, with threads
        ///  gracefully shutting down in the background. Use the WaitUntilStopped() or WaitUntilStoppedAsync()
        ///  methods to block/await until threads are fully stopped. If threads were started using Run(), the
        ///  Run() method will return automatically once threads are fully stopped.
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

            // Run asynchronous task to stop threads, updating state once threads have successfully stopped.
            Task.Run(async () =>
            {
                // The StopThread() methods block until Thread.Join() returns successfully.
                _networkThreadWorker.StopThread();      // Stop network thread first to prevent incoming/outgoing messages.
                _processThreadWorker.StopThread();

                // After threads have successfully stopped, finally update state. This causes Wait() methods to return.
                _state = State.Stopped;
            });
        }

        /// <summary>
        /// Blocks the calling thread until the data processor and network threads are fully stopped. This method
        ///  should be used whenever the Driver is started using Start() instead of Run() or RunAsync(), as
        ///  calling Stop() without waiting may end application execution before the threads can safely stop.
        /// </summary>
        public void WaitUntilStopped()
        {
            // Continuously block the current thread while state is Running or Stopping.
            while (_state == State.Running || _state == State.Stopping)
            {
                Thread.Sleep(1000);     // Sleep for 1s, meaning there could be up to a 1s delay before return.
            }
        }

        /// <summary>
        /// Awaits indefinitely until the data processor and network threads are fully stopped. This method
        ///  should be used whenever the Driver is started using Start() instead of Run() or RunAsync(), as
        ///  calling Stop() without waiting may end application execution before the threads can safely stop.
        /// </summary>
        public async Task WaitUntilStoppedAsync()
        {
            // IN THE FUTURE, USE CANCELLATION TOKEN INSTEAD FOR INSTANT RETURN.

            // Continuously await with Task.Delay() while state is Running or Stopping.
            while (_state == State.Running || _state == State.Stopping)
            {
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Runs the driver synchronously, starting worker threads for data processing and network operations.
        ///  This method blocks until the threads are successfully stopped using Stop(). To start the threads
        ///  asynchronously without blocking or awaiting, use the Start() method.
        /// </summary>
        public void Run()
        {
            // Re-use Start() method to actually start threads. State will be Running after the method returns.
            Start();

            // After started, continuously block until threads are Stopped.
            WaitUntilStopped();
        }

        /// <summary>
        /// Runs the driver asynchronously, starting worker threads for data processing and network operations.
        ///  This method awaits until the threads are successfully stopped using Stop(). To start the threads
        ///  asynchronously without blocking or awaiting, use the Start() method.
        /// </summary>
        public async Task RunAsync()
        {
            // Re-use Start() method to actually start threads. State will be Running after the method returns.
            Start();

            // After started, continuously await until threads are Stopped.
            await WaitUntilStoppedAsync();
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
            ///  This method blocks until Thread.Join() returns.
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
            /// Stops the worker thread, waiting for server to shut down before joining and returning. This
            ///  method blocks until Thread.Join() returns.
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
