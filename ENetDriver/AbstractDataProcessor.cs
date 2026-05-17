using ENet;
using ENetDriver.Config;
using ENetDriver.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetDriver
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
        private BlockingCollection<NetSendObject> netSendQueue = null!;
        private BlockingCollection<NetRecvObject> netRecvQueue = null!;

        // CONFIGURATION
        private DataProcessorConfig config;

        // Health tracking information. Store the last 10 interval switches.
        private int _lastTenIndex = 0;
        private int[] _lastTenRemainingCounts = new int[10];
        private long[] _lastTenMillisecondsTaken = new long[10];

        protected AbstractDataProcessor(DataProcessorConfig config)
        {
            this.config = config;
        }

        /// <summary>
        /// Gets the configuration object (DataProcessorConfig) used by this data processor. Returns null if called
        ///  before SetConfiguration() is called.
        /// </summary>
        /// <returns></returns>
        public DataProcessorConfig GetConfiguration()
        {
            return config;
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
        }

        private void Start()
        {
            // Throw exception if not yet initialized.
            if (netSendQueue == null || netRecvQueue == null)
            {
                throw new InvalidOperationException("Cannot start data processor which is not yet initialized (must" +
                    " call SetQueueReferences() and SetConfiguration() before starting data processor.");
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
                    while (stopwatch.ElapsedMilliseconds < config.MaxPollTimeMS)
                    {
                        /* ----- TRYTAKE DOCUMENTATION -----
                        // Try to dequeue an item from the queue. Immediately returns the item if successful, otherwise stops
                        //  blocking after duration has elapsed. Delay for poll minimum duration.
                        // If at least one item is found before the timeout, then we will return to the top of the loop and
                        //  try again until we EITHER 1) run out of items to take, or 2) exceed the maximum poll duration.
                        // If no item is found before the timeout, then we will immediately break and flip back to processing
                        //  incoming events (no reason to sit here idle with no items to take).
                        */
                        if (!netRecvQueue.TryTake(out NetRecvObject? item, config.MinPollTimeMS))
                        {
                            // NO ITEM AVAILABLE TO TAKE/DEQUEUE - BREAK FROM INNER LOOP
                            break;
                        }

                        // Else if item was taken, call abstract processor method.
                        ProcessIncomingData(item);
                    }

                    // Log our health data if it is enabled (interval > 0).
                    if (config.HealthLoggingInterval > 0)
                    {
                        // Update ring buffer index and then store current remaining count and elapsed milliseconds.
                        _lastTenIndex = (_lastTenIndex + 1) % 10;
                        _lastTenRemainingCounts[_lastTenIndex] = netRecvQueue.Count;
                        _lastTenMillisecondsTaken[_lastTenIndex] = stopwatch.ElapsedMilliseconds;

                        // If we have exceeded the health log interval, print stats to the log and reset timer.
                        if (healthLogTimer.ElapsedMilliseconds >= config.HealthLoggingInterval)
                        {
                            double remainingAvg = _lastTenRemainingCounts.Sum() / 10.0d;
                            double millisecondsAvg = _lastTenMillisecondsTaken.Sum() / 10.0d;
                            config.Logger.Invoke($"HEALTH DATA FOR LAST TEN INTERVALS:\n" +
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
                config.Logger.Invoke($"[EXCEPTION] :: {e}.");
                config.Logger.Invoke("[EXCEPTION] Stopping DataProcessor thread.");
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
            config.Logger.Invoke(message);
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
