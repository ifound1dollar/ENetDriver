using ENet_Driver.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENet_Driver
{
    public abstract class AbstractDataProcessor
    {
        #region Loop Cancellation

        private volatile bool shouldExit = false;

        /// <summary>
        /// Commands the DataProcessor to stop running, exiting the main loop on its next iteration.
        /// </summary>
        internal void CommandStop()
        {
            shouldExit = true;
        }

        #endregion

        // QUEUE REFERENCES
        private bool isQueueRefsSetup = false;
        private BlockingCollection<NetSendObject> netSendQueue = null!;
        private BlockingCollection<NetRecvObject> netRecvQueue = null!;

        // POLL (LOOP) TIME LIMITS
        private int maxPollTimeoutMS = 1000;
        private int minPollTimeoutMS = 100;

        // The logger method used by this class. This is explicitly set by the Driver using SetLogger() below.
        private Action<string> _logger = null!;

        // Health tracking information. Store the last 10 interval switches.
        private int[] _lastTenRemainingCounts = new int[10];
        private int _lastTenIndex = 0;
        private long[] _lastTenMillisecondsTaken = new long[10];
        private uint _healthLoggingIntervalMS = 0;



        protected AbstractDataProcessor()
        {
            
        }



        #region Setup / Start / Stop / Run Operations

        /// <summary>
        /// Sets references to thread-safe queues.
        /// </summary>
        /// <param name="netSendQueue"> Reference to network send BlockingCollection. </param>
        /// <param name="netRecvQueue"> Reference to network receive BlockingCollection. </param>
        internal void SetRequiredQueueReferences(BlockingCollection<NetSendObject> netSendQueue, BlockingCollection<NetRecvObject> netRecvQueue)
        {
            this.netSendQueue = netSendQueue;
            this.netRecvQueue = netRecvQueue;

            isQueueRefsSetup = true;
        }

        /// <summary>
        /// Configures run (loop) behavior. Sets maximum and minimum poll duration fields, ensuring smooth looping
        ///  and avoiding potential hangs.
        /// </summary>
        /// <param name="maxPollTimeoutMS"> The maximum duration in ms that the server can remain in one polling mode (ex. receive or send). </param>
        /// <param name="minPollTimeoutMS"> The minimum duration in ms that the server can remain in one polling mode (ex. receive or send). </param>
        internal void SetOptionalPollIntervals(int maxPollTimeoutMS, int minPollTimeoutMS)
        {
            // Log error and set default if parameters are invalid.
            if (maxPollTimeoutMS <= 0 || minPollTimeoutMS <= 0 || maxPollTimeoutMS < minPollTimeoutMS)
            {
                _logger.Invoke($"[AbstractDataProcessor] Maximum and minimum poll timeout values must be greater than zero," +
                    $" and maximum must be greater than minimum. Defaulting to {this.maxPollTimeoutMS}ms max, {this.minPollTimeoutMS}ms min.");
            }

            // If valid, simply set fields.
            this.maxPollTimeoutMS = maxPollTimeoutMS;
            this.minPollTimeoutMS = minPollTimeoutMS;
        }

        /// <summary>
        /// Sets the output (print) method for logging use by the data processor. This method must be called by the Driver
        ///  on object initialization.
        /// </summary>
        /// <param name="logMethod"> The method to use for logging. Must accept a string argument and return void. </param>
        internal void SetLogger(Action<string> logMethod)
        {
            _logger = logMethod;
        }

        /// <summary>
        /// Sets the interval to print queue health data to the log. Set to 0 to disable health logging.
        /// </summary>
        /// <param name="intervalSeconds"> The interval (in seconds) to print health data to the log. Set to 0 to disable health logging. </param>
        internal void SetHealthLoggingInterval(uint intervalSeconds)
        {
            _healthLoggingIntervalMS = intervalSeconds * 1000;
        }

        private void Start()
        {
            // Throw exception if not yet initialized.
            if (!isQueueRefsSetup)
            {
                throw new InvalidOperationException("Cannot start data processor which is not yet initialized (must" +
                    " call SetQueueReferences() before starting data processor.");
            }
        }

        private void Stop()
        {

        }

        internal void Run()
        {
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
            Stopwatch healthLogTimer = new();

            try
            {
                Start();

                // Start our health log timer (stopwatch) immediately.
                healthLogTimer.Start();

                while (!shouldExit)
                {
                    // Restart stopwatch each loop, then loop until maximum timeout duration in milliseconds.
                    stopwatch.Restart();
                    while (stopwatch.ElapsedMilliseconds < maxPollTimeoutMS)
                    {
                        /* ----- TRYTAKE DOCUMENTATION -----
                        // Try to dequeue an item from the queue. Immediately returns the item if successful, otherwise stops
                        //  blocking after duration has elapsed. Delay for poll minimum duration.
                        // If at least one item is found before the timeout, then we will return to the top of the loop and
                        //  try again until we EITHER 1) run out of items to take, or 2) exceed the maximum poll duration.
                        // If no item is found before the timeout, then we will immediately break and flip back to processing
                        //  incoming events (no reason to sit here idle with no items to take).
                        */
                        if (!netRecvQueue.TryTake(out NetRecvObject? item, minPollTimeoutMS))
                        {
                            // NO ITEM AVAILABLE TO TAKE/DEQUEUE - BREAK FROM INNER LOOP
                            break;
                        }

                        // Else if item was taken, call abstract processor method.
                        ProcessIncomingData(item);
                    }

                    // Log our health data if it is enabled (interval > 0).
                    if (_healthLoggingIntervalMS > 0)
                    {
                        // Update ring buffer index and then store current remaining count and elapsed milliseconds.
                        _lastTenIndex = (_lastTenIndex + 1) % 10;
                        _lastTenRemainingCounts[_lastTenIndex] = netRecvQueue.Count;
                        _lastTenMillisecondsTaken[_lastTenIndex] = stopwatch.ElapsedMilliseconds;

                        // If we have exceeded the health log interval, print stats to the log and reset timer.
                        if (healthLogTimer.ElapsedMilliseconds >= _healthLoggingIntervalMS)
                        {
                            double remainingAvg = _lastTenRemainingCounts.Sum() / 10.0d;
                            double millisecondsAvg = _lastTenMillisecondsTaken.Sum() / 10.0d;
                            _logger.Invoke($"HEALTH DATA FOR LAST TEN INTERVALS:\n" +
                                $"> Elements remaining in queue: {remainingAvg:f2}\n" +
                                $"> Milliseconds taken: {millisecondsAvg:f2}");

                            healthLogTimer.Restart();
                        }
                    }
                }

                Stop();
            }
            catch (Exception e)
            {
                _logger.Invoke($"[EXCEPTION] :: {e}.");
                _logger.Invoke("[EXCEPTION] Stopping DataProcessor thread.");
            }
        }

        #endregion

        #region Default Methods

        protected void EnqueueOneOutgoing(NetSendObject sendObject)
        {
            netSendQueue.Add(sendObject);
        }

        protected void LogMessage(string message)
        {
            _logger.Invoke(message);
        }

        #endregion

        #region Abstract Methods

        protected abstract void ProcessIncomingData(NetRecvObject recvObject);

        #endregion



        // DATA PROCESSOR WILL HAVE TO BREAK DOWN INCOMING BYTE[] USING INCOMMANDTYPE ENUM VALUE, USING
        //  ARRAYBUFFER CLASS TO PULL DATA FROM THE PACKET PAYLOAD ACCORDINGLY.
        // OUTCOMMANDTYPE WILL HAVE TO BE CONVERTED TO A BYTE AND ADDED TO PACKET PAYLOAD ON THE WAY
        //  OUT.
    }
}
