using ENet;
using System;
using System.Collections.Generic;
using System.Text;

namespace ENetDriver.Config
{
    public class ServerConfig
    {
        // REQUIRED HOST PARAMETERS
        public required ushort Port { get; init; }

        // OPTIONAL HOST PARAMETERS
        public required int PeerLimit { get; init; }
        public required int ChannelLimit { get; init; }

        // OPTIONAL PEER PING/TIMEOUT PARAMETERS
        public required uint PeerTimeoutPingIntervalMS { get; init; }
        public required uint PeerTimeoutPingAttemptLimit { get; init; }
        public required uint PeerTimeoutMinimumMS { get; init; }
        public required uint PeerTimeoutMaximumMS { get; init; }

        // OPTIONAL POLL (RUN) TIME LIMITS
        public required int MaxPollTimeMS { get; init; }
        public required int MinPollTimeMS { get; init; }

        // LOGGER
        public required Action<string> Logger { get; init; }

        private ServerConfig()
        {
            // Use init functionality in Builder.
        }



        public class Builder
        {
            // REQUIRED HOST PARAMETERS
            private ushort port = 7777;

            // OPTIONAL HOST PARAMETERS
            private int peerLimit = 64;
            private int channelLimit = 2;

            // OPTIONAL PEER PING/TIMEOUT PARAMETERS
            private uint peerTimeoutPingIntervalMS = 5000;      // ENet default: 500
            private uint peerTimeoutPingAttemptLimit = 5;       // ENet default: 32
            private uint peerTimeoutMinimumMS = 10000;          // ENet default: 5000
            private uint peerTimeoutMaximumMS = 30000;          // ENet default: 30000

            // OPTIONAL POLL (RUN) TIME LIMITS
            private int maxPollTimeMS = 1000;
            private int minPollTimeMS = 100;

            // LOGGER
            private Action<string> logger = Console.WriteLine;



            /// <summary>
            /// Sets the network port that the server will listen on. If not explicitly set, defaults to port 7777.
            /// </summary>
            /// <param name="port"> The network port that the host will listen on. </param>
            public Builder SetPort(ushort port)
            {
                this.port = port;
                return this;
            }

            /// <summary>
            /// Sets the maximum number of peers that can be connected to the server host at one time. Value must be
            ///  in range 1-4095. Default 64.
            /// </summary>
            /// <param name="peerLimit"> The maximum number of peers that can be connected at once. </param>
            /// <exception cref="InvalidOperationException"></exception>
            public Builder SetPeerLimit(int peerLimit)
            {
                if (peerLimit < 1 || peerLimit > 4095)
                {
                    throw new InvalidOperationException("Peer limit must be in range 1-4095.");
                }

                this.peerLimit = peerLimit;
                return this;
            }

            /// <summary>
            /// Sets the maximum number of channels that the server host will support. Value must be in range
            ///  1-255. Default 2.
            /// </summary>
            /// <param name="channelLimit"> The maximum number of channels that will be supported. </param>
            /// <exception cref="InvalidOperationException"></exception>
            public Builder SetChannelLimit(int channelLimit)
            {
                if (channelLimit < 1 || channelLimit > 255)
                {
                    throw new InvalidOperationException("Channel limit must be in range 1-255.");
                }

                this.channelLimit = channelLimit;
                return this;
            }

            /// <summary>
            /// Sets ping and timeout settings for the server host, which determines how the server will process automatic
            ///  timeouts when a peer is no longer responding. Default 5000ms ping interval, 5 attempt limit, 10000ms timeout
            ///  minimum, 30000ms timeout maximum.
            /// </summary>
            /// <param name="pingIntervalMS"> The time interval (ms) that the server will ping the connected client. </param>
            /// <param name="pingAttemptLimit"> The maximum number of ping attempts the server will make before explicitly considering the peer timed out. Once exceeding this, it is considered timed out regardless of maximum timeout duration. </param>
            /// <param name="timeoutMinimumMS"> The minimum duration (ms) for a peer to not respond before potentially considering timed out. </param>
            /// <param name="timeoutMaximumMS"> The maximum duration (ms) for a peer to not respond before it is explicitly considered timed out. Once exceeding this value, it is considered timed out regardless of ping attempt limit. </param>
            /// <returns></returns>
            public Builder SetPeerTimeoutSettings(uint pingIntervalMS, uint pingAttemptLimit, uint timeoutMinimumMS, uint timeoutMaximumMS)
            {
                this.peerTimeoutPingIntervalMS = pingIntervalMS;
                this.peerTimeoutPingAttemptLimit = pingAttemptLimit;
                this.peerTimeoutMinimumMS = timeoutMinimumMS;
                this.peerTimeoutMaximumMS = timeoutMaximumMS;
                return this;
            }

            /// <summary>
            /// Sets minimum and maximum time intervals used during thread loop polling (ex. processing incoming vs. outgoing
            ///  messages). A minimum time ensures the internal loop does not run each clock which would cause high CPU usage
            ///  per thread, and a maximum time ensures that the polling mode (incoming/outgoing) cannot get indefinitely stuck
            ///  in the case of heavy traffic. Values must be positive, and minimum must be less than or equal to maximum.
            ///  Default 100ms minimum, 1000ms maximum.
            /// </summary>
            /// <param name="minPollTimeMS"> The minimum time (in milliseconds) that the server will spend in one polling mode. </param>
            /// <param name="maxPollTimeMS"> The maximum time (in milliseconds) that the server will spend in one polling mode. </param>
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
            /// Sets the output/print method used for logging by the server. The logger method must accept a single string
            ///  argument and return void. Default method is Console.WriteLine.
            /// </summary>
            /// <param name="logger"> The logger method which accepts a single string argument and returns void. </param>
            public Builder SetLogger(Action<string> logger)
            {
                this.logger = logger;
                return this;
            }

            /// <summary>
            /// Constructs and returns a new ServerConfig object using the Builder.
            /// </summary>
            /// <returns> The newly-constructed ServerConfig object. </returns>
            public ServerConfig Build()
            {
                return new ServerConfig()
                {
                    Port = port,

                    PeerLimit = peerLimit,
                    ChannelLimit = channelLimit,

                    PeerTimeoutPingIntervalMS = peerTimeoutPingIntervalMS,
                    PeerTimeoutPingAttemptLimit = peerTimeoutPingAttemptLimit,
                    PeerTimeoutMinimumMS = peerTimeoutMinimumMS,
                    PeerTimeoutMaximumMS = peerTimeoutMaximumMS,

                    MinPollTimeMS = minPollTimeMS,
                    MaxPollTimeMS = maxPollTimeMS,

                    Logger = logger
                };
            }
        }
    }
}
