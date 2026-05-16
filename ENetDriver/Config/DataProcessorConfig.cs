using System;
using System.Collections.Generic;
using System.Text;

namespace ENetDriver.Config
{
    public class DataProcessorConfig
    {
        // OPTIONAL POLL (RUN) TIME LIMITS
        public required int MaxPollTimeMS { get; init; }
        public required int MinPollTimeMS { get; init; }

        // LOGGER
        public required Action<string> Logger { get; init; }

        // HEALTH LOGGING INTERVAL
        public required uint HealthLoggingInterval { get; init; }

        private DataProcessorConfig()
        {
            // Use init functionality in Builder.
        }



        public class Builder
        {
            // OPTIONAL POLL (RUN) TIME LIMITS
            private int maxPollTimeMS = 1000;
            private int minPollTimeMS = 100;

            // LOGGER
            private Action<string> logger = Console.WriteLine;

            // HEALTH LOGGING
            private uint healthLoggingIntervalMS = 0;



            /// <summary>
            /// Sets minimum and maximum time intervals used during thread loop polling (ex. processing incoming vs. outgoing
            ///  messages). A minimum time ensures the internal loop does not run each clock which would cause high CPU usage
            ///  per thread, and a maximum time ensures that the polling mode (incoming/outgoing) cannot get indefinitely stuck
            ///  in the case of heavy traffic. Values must be positive, and minimum must be less than or equal to maximum.
            ///  Default 100ms minimum, 1000ms maximum.
            /// </summary>
            /// <param name="minPollTimeMS"> The minimum time (in milliseconds) that the data processor will spend in one polling mode. </param>
            /// <param name="maxPollTimeMS"> The maximum time (in milliseconds) that the data processor will spend in one polling mode. </param>
            /// <exception cref="InvalidOperationException"></exception>
            public Builder SetPollTimeIntervals(int minPollTimeMS, int maxPollTimeMS)
            {
                if (maxPollTimeMS <= 0 || minPollTimeMS <= 0 || maxPollTimeMS < minPollTimeMS)
                {
                    throw new InvalidOperationException("Poll time intervals must be positive, and minimum must be less than or equal to maximum.");
                }

                this.minPollTimeMS = minPollTimeMS;
                this.maxPollTimeMS = maxPollTimeMS;
                return this;
            }

            /// <summary>
            /// Sets the output/print method used for logging by the data processor. The logger method must accept a
            ///  single string argument and return void. Default method is Console.WriteLine.
            /// </summary>
            /// <param name="logger"> The logger method which accepts a single string argument and returns void. </param>
            public Builder SetLogger(Action<string> logger)
            {
                this.logger = logger;
                return this;
            }

            /// <summary>
            /// Sets the interval in which to print data processor health data to the console. Prints data like
            ///  average number of elements remaining in the queue upon context switching, as well as average
            ///  milliseconds taken to process all elements over the last ten context switches.
            /// </summary>
            /// <param name="intervalSeconds"> The interval in seconds between health logging output. </param>
            public Builder SetHealthLoggingInterval(uint intervalSeconds)
            {
                this.healthLoggingIntervalMS = intervalSeconds;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new DataProcessorConfig object using the Builder.
            /// </summary>
            /// <returns> The newly-constructed DataProcessorConfig object. </returns>
            public DataProcessorConfig Build()
            {
                return new DataProcessorConfig()
                {
                    MinPollTimeMS = minPollTimeMS,
                    MaxPollTimeMS = maxPollTimeMS,

                    Logger = logger,

                    HealthLoggingInterval = healthLoggingIntervalMS
                };
            }
        }

    }
}
