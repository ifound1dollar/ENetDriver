using ENet;
using ENet_Driver.Data;
using ENet_Driver.Network;
using ENetDriver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ENet_Driver.Network
{
    internal class ENetServer
    {
        #region Loop Cancellation

        private volatile bool shouldExit = false;

        /// <summary>
        /// Commands the Server to stop running, exiting the main loop on its next iteration.
        /// </summary>
        internal void CommandStop()
        {
            shouldExit = true;
        }

        #endregion

        // REQUIRED QUEUE REFERENCES
        private bool isQueueRefsSetup   = false;
        private BlockingCollection<NetSendObject> netSendQueue = null!;
        private BlockingCollection<NetRecvObject> netRecvQueue = null!;

        // REQUIRED HOST DATA
        private bool isHostSetup        = false;
        private Host serverHost = null!;
        private Address address;

        // OPTIONAL HOST PARAMETERS
        private int peerLimit           = 64;
        private int channelLimit        = 2;

        // OPTIONAL PEER PING/TIMEOUT PARAMETERS
        private uint pingIntervalMS     = 5000;     // ENet default: 500
        private uint pingAttemptLimit   = 5;        // ENet default: 32
        private uint timeoutMinimumMS   = 10000;    // ENet default: 5000
        private uint timeoutMaximumMS   = 30000;    // ENet default: 30000

        // OPTIONAL POLL (RUN) TIME LIMITS
        private int maxPollTimeoutMS    = 1000;
        private int minPollTimeoutMS    = 100;

        // PEER CONTAINERS
        private readonly Dictionary<uint, PeerData> clientsById = [];
        private readonly Dictionary<string, PeerData> clientsByAddress = [];

        // The logger method used by this class. This is explicitly set by the Driver using SetLogger() below.
        private Action<string> _logger = null!;

        internal ENetServer()
        {
            
        }

        /// <summary>
        /// Gets the enet Address object associated with this server. Will be uninitialized until SetHostParameters() is called.
        /// </summary>
        /// <returns></returns>
        internal Address GetAddress()
        {
            return address;
        }



        #region Setup / Start / Stop Operations

        #region Required Setup

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
        /// Sets listening port for the ENetServer. Listening address is irrelevant because we listen on our public IP address.
        /// </summary>
        /// <param name="port"> Port that the host should listen on. </param>
        internal void SetRequiredHostListenPort(ushort port)
        {
            // Address for this server Host.
            address = default;
            address.Port = port;                // Actual port server will listen on.

            isHostSetup = true;
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

        #endregion

        #region Optional Setup

        /// <summary>
        /// OPTIONAL: Sets maximum peer limit and maximum channel limit for the ENet Host. Peer limit only applies to
        ///  connected peers; once a peer is disconnected, it is completely removed from consideration.
        /// </summary>
        /// <param name="peerLimit"> Maximum number of peers that can be connected at once (library limits to 4095). </param>
        /// <param name="channelLimit"> Maximum number of channels that the host will communicate on with peers. Default 2. </param>
        internal void SetOptionalHostSettings(int peerLimit, int channelLimit)
        {
            // IMPORTANT: Peer limit only applies to connected peers. Once a peer is fully disconnected, ENet
            //  completely disposes the object and no longer considers it at all. Host.PeersCount reflects this
            //  disposal (as soon as a disconnect or timeout event is received and dispatched, PeersCount is
            //  updated.

            // Log error and set default if parameters are invalid.
            if (peerLimit < 1 || peerLimit > 4095 || channelLimit < 1 || channelLimit > 255)
            {
                _logger.Invoke($"[ENetServer] Maximum peer limit must be in range 1-4095, and maximum channel" +
                    $" limit must be in range 1-255. Defaulting to peerLimit of {this.peerLimit}, channelLimit of {this.channelLimit}.");
                return;
            }

            // If valid, simply set fields.
            this.peerLimit = peerLimit;
            this.channelLimit = channelLimit;
        }

        /// <summary>
        /// OPTIONAL: Sets ping and timeout parameters for all Peers which connect to this Host.
        /// </summary>
        /// <param name="pingIntervalMS"> Interval in milliseconds to send pings to the client peer. </param>
        /// <param name="pingAttemptLimit"> Maximum (minimum?) number of consecutive failed ping attempts before the peer 'times out'. </param>
        /// <param name="timeoutMinimumMS"> Minimum duration in milliseconds that a ping has not been acknowledged before timing out. </param>
        /// <param name="timeoutMaximumMS"> Maximum duration in milliseconds that a ping has not been acknowledged before timing out. </param>
        internal void SetOptionalPeerTimeoutSettings(uint pingIntervalMS, uint pingAttemptLimit, uint timeoutMinimumMS, uint timeoutMaximumMS)
        {
            this.pingIntervalMS = pingIntervalMS;
            this.pingAttemptLimit = pingAttemptLimit;
            this.timeoutMinimumMS = timeoutMinimumMS;
            this.timeoutMaximumMS = timeoutMaximumMS;
        }

        /// <summary>
        /// OPTIONAL: Configures run (poll) time limits. Sets maximum and minimum poll duration fields,
        ///  ensuring smooth looping and avoiding potential hangs.
        /// </summary>
        /// <param name="maxPollTimeoutMS"> The maximum duration in ms that the server can remain in one polling mode (ex. receive or send). Default 1000ms. </param>
        /// <param name="minPollTimeoutMS"> The minimum duration in ms that the server can remain in one polling mode (ex. receive or send). Default 100ms. </param>
        internal void SetOptionalPollIntervals(int maxPollTimeoutMS, int minPollTimeoutMS)
        {
            // Log error and set default if parameters are invalid.
            if (maxPollTimeoutMS <= 0 || minPollTimeoutMS <= 0 || maxPollTimeoutMS < minPollTimeoutMS)
            {
                _logger.Invoke($"[ENetServer] Maximum and minimum poll timeout values must be greater than zero," +
                    $" and maximum must be greater than minimum. Defaulting to {this.maxPollTimeoutMS}ms max, {this.minPollTimeoutMS}ms min.");
                return;
            }

            // If valid, simply set fields.
            this.maxPollTimeoutMS = maxPollTimeoutMS;
            this.minPollTimeoutMS = minPollTimeoutMS;
        }

        #endregion

        /// <summary>
        /// Starts server Host to begin listening on the designated port.
        /// </summary>
        private void Start()
        {
            // Throw exception if not yet initialized.
            if (!isQueueRefsSetup || !isHostSetup)
            {
                throw new InvalidOperationException("Cannot start server which is not yet initialized (must" +
                    " call SetQueueReferences() and SetHostParameters() before starting server.");
            }

            // Create server host with address, port, and other Host arguments.
            serverHost = new();
            serverHost.Create(address, peerLimit, channelLimit, 0u, 0u, 1024*1024);     // No limits, and use default enet.h buffer size.
        }

        /// <summary>
        /// Stops the server Host, performing shutdown operations like disconnecting all connected peers (blocks while disconnecting).
        /// </summary>
        /// <param name="serviceDelay"> How long to run the (blocking) host service to wait for peer disconnect ACKs. </param>
        private void Stop(int serviceDelay = 3)
        {
            // Disconnect all clients and wait 3 seconds for disconnect ACKs.
            foreach (var peerData in clientsById)
            {
                // Command every peer to disconnect, regardless of whether they are currently Connected.
                peerData.Value.Peer.Disconnect(300u);       // Code 300 indicates server-initiated disconnect on server shutdown.
            }

            // Run host service for serviceDelay seconds, waiting for peers (clients) to ACK disconnect command.
            while (serverHost.Service(serviceDelay * 1000, out Event netEvent) > 0)
            {
                switch (netEvent.Type)
                {
                    case EventType.Disconnect:
                        {
                            HandleDisconnectEvent(ref netEvent, isTimeout: false);
                            break;
                        }
                    case EventType.Receive:
                        {
                            netEvent.Packet.Dispose();      // Ignore and dispose any received packets.
                            break;
                        }
                    // Do not process any other events.
                }
            }

            // Finally, flush and dispose server host.
            serverHost.Flush();
            serverHost.Dispose();
        }

        #endregion

        #region Run / Dispatch Operations

        /// <summary>
        /// Begins continuous server running operations. Continuously swaps between handling incoming and outgoing
        ///  events. Can be stopped by calling the CommandStop() method on this class instance.
        /// </summary>
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

            try
            {
                // Start server, opening the host up for incoming connections and messages.
                Start();

                // Loop until shouldExit is true. Will flip-flop between incoming and outgoing at pollDurationMs interval.
                while (!shouldExit)
                {
                    DispatchIncomingEvents(stopwatch);

                    DispatchOutgoingEvents(stopwatch);
                }

                // Stop server, blocking for a duration to ensure all clients can disconnect properly.
                Stop();
            }
            catch (Exception e)
            {
                _logger.Invoke($"[EXCEPTION] :: {e}.");
                _logger.Invoke("[EXCEPTION] Stopping ENetServer thread.");
            }
        }

        /// <summary>
        /// Dispatches incoming ENet events, calling Host Service() to process network events. Uses time
        ///  tracking variables to avoid getting stuck or running at maximum CPU load per thread. Can hit
        ///  maximum duration if there is more work to do than there is time to complete it. Will execute
        ///  for at least minimum duration, immediately returning after minimum if there is no work to do.
        /// </summary>
        /// <param name="stopwatch"> Existing stopwatch reference used to track time in this method. </param>
        private void DispatchIncomingEvents(Stopwatch stopwatch)
        {
            Event netEvent;
            stopwatch.Restart();

            // Loop only for poll maximum to prevent getting stuck on incoming.
            while (stopwatch.ElapsedMilliseconds < maxPollTimeoutMS)
            {
                /* ----- CHECKEVENTS DOCUMENTATION -----
                 * Run CheckEvents() to dispatch incoming events that were enqueued by Service() but were not immediately
                 *  processed. The host Service() may wind up enqueueing multiple events while it can only process one,
                 *  so we call CheckEvents() before calling Service() to ensure that we dispatch any enqueued events from
                 *  the last time Service() was called.
                 * CheckEvents() returns 0 if no event was dispatched, and will return 1 each time it is called in the
                 *  loop as long as an event is dispatched (only return 0 once all enqueued events are dispatched).
                 * NOTE: This method only dispatches enqueued INCOMING commands; Service() handles outgoing sends.
                */
                if (serverHost.CheckEvents(out netEvent) <= 0)
                {
                    /* ----- SERVICE DOCUMENTATION -----
                    // If no events were dispatched (none were waiting after being enqueued by Service()), then we should
                    //  run the host Service() to receive and enqueue new incoming events.
                    // We should NOT call the function with a 0 timeout because this will cause 100% CPU usage on the
                    //  thread. Instead, we should listen for the pollMinimum duration to reduce CPU usage while still
                    //  allowing CheckEvents() and Service() to run back-and-forth.

                    // If the host Service() does not receive any event, we immediately return from awaiting Service()
                    //  because there has been no incoming OR outgoing work to do. We should immediately jump back to
                    //  enqueuing commands from the data processor thread, then come back and try to do more work.
                    // NOTE: It is possible that the service enqueues an event which is not dispatched until the next
                    //  flip-flop to incoming event handling BECAUSE Service() might enqueue commands which have not
                    //  yet been handled (will be handled in next CheckEvents() call). We might have exceeded the
                    //  maximum poll duration after the return, which would leave a delay between when the incoming
                    //  event is received and when the associated event is actually dispatched.
                    */
                    if (serverHost.Service(minPollTimeoutMS, out netEvent) <= 0) return;
                }

                // Handle event dispatched by CheckEvents() or Service() (both dispatch events the same way).
                switch (netEvent.Type)
                {
                    case EventType.None:
                        break;

                    case EventType.Connect:
                        HandleConnectEvent(ref netEvent);
                        break;

                    case EventType.Disconnect:
                        HandleDisconnectEvent(ref netEvent, isTimeout: false);
                        break;

                    case EventType.Timeout:
                        HandleDisconnectEvent(ref netEvent, isTimeout: true);
                        break;

                    case EventType.Receive:
                        HandleReceiveEvent(ref netEvent);
                        break;
                }
            }
        }

        /// <summary>
        /// Dispatches outgoing events from the data processor, pulling data from the BlockingCollection and
        ///  enqueuing commands to ENet. Uses time tracking variables to avoid getting stuck or running at
        ///  maximum CPU load per thread. Can hit maximum duration if there is more work to do than there is
        ///  time to complete it. Will execute for at least minimum duration, immediately returning after
        ///  minimum if there is no work to do.
        /// </summary>
        /// <param name="stopwatch"> Existing stopwatch reference used to track time in this method. </param>
        private void DispatchOutgoingEvents(Stopwatch stopwatch)
        {
            stopwatch.Restart();

            // Loop only for poll maximum duration to prevent getting stuck on outgoing.
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
                if (!netSendQueue.TryTake(out NetSendObject? item, minPollTimeoutMS))
                {
                    return;     // NO ITEM AVAILABLE TO TAKE/DEQUEUE - RETURN TO DISPATCH INCOMING EVENTS
                }

                // Switch on ENetAction to determine what operation to perform.
                switch (item.ActionType)
                {
                    case ENetAction.None:
                    case ENetAction.Timeout:    // We do not manually send Timeout events.
                        break;

                    case ENetAction.Connect:
                        QueueConnectToPeer(item);
                        break;

                    case ENetAction.Disconnect:
                        QueueDisconnectPeer(item);
                        break;

                    case ENetAction.Message:
                        QueueMessageOnePeer(item);
                        break;
                }
            }
        }

        #endregion

        #region Incoming Event Handling

        private void HandleConnectEvent(ref Event connectEvent)
        {
            Peer peer = connectEvent.Peer;
            string key = MakeAddressString(peer.IP, peer.Port);

            // Add new PeerData to both Dictionaries (special data like state does not matter to us here).
            PeerData peerData = new(peer);
            clientsById.Add(peer.ID, peerData);
            clientsByAddress.Add(key, peerData);

            // Set ping interval and timeout parameters for the Peer.
            peer.PingInterval(pingIntervalMS);
            peer.Timeout(pingAttemptLimit, timeoutMinimumMS, timeoutMaximumMS);     // Default 32, 5000, 30000

            // Create new NetRecvObject and enqueue for data processing.
            NetRecvObject obj = NetRecvObject.CreateFromConnect(peer.ID, peer.IP, peer.Port, connectEvent.Data);
            netRecvQueue.Add(obj);
        }

        private void HandleDisconnectEvent(ref Event netEvent, bool isTimeout)
        {
            Peer peer = netEvent.Peer;
            string key = MakeAddressString(peer.IP, peer.Port);

            // Try to remove the peer from both client Dictionaries.
            clientsById.Remove(peer.ID);
            clientsByAddress.Remove(key);

            // Create new NetRecvObject and enqueue for data processing.
            NetRecvObject obj;
            if (!isTimeout)
            {
                obj = NetRecvObject.CreateFromDisconnect(peer.ID, peer.IP, peer.Port, netEvent.Data);
            }
            else
            {
                obj = NetRecvObject.CreateFromTimeout(peer.ID, peer.IP, peer.Port, 400u);
            }
            netRecvQueue.Add(obj);
        }

        private void HandleReceiveEvent(ref Event netEvent)
        {
            Peer peer = netEvent.Peer;

            // Copy packet payload into new byte[].
            byte[] bytes = new byte[netEvent.Packet.Length];
            netEvent.Packet.CopyTo(bytes);

            // Create NetRecvObject from received packet and enqueue to be handled by DataProcessor.
            NetRecvObject obj = NetRecvObject.CreateFromMessage(peer.ID, peer.IP, peer.Port, bytes, bytes.Length, netEvent.ChannelID);
            netRecvQueue.Add(obj);

            // Finally, dispose the packet to free memory. Dispose as final operation as safe practice.
            netEvent.Packet.Dispose();
        }

        #endregion

        #region Outgoing Event Handling

        /// <summary>
        /// Queues a new connection attempt to a single remote peer. A new Peer object will be generated
        ///  by ENet if the Connect attempt is valid, but the connection will not yet be created until an
        ///  incoming connect event is received.
        /// </summary>
        /// <param name="netSendObject"></param>
        private void QueueConnectToPeer(NetSendObject netSendObject)
        {
            // Create Address object with IP and Port from NetSendObject.
            Address remoteAddress = new();
            remoteAddress.SetIP(netSendObject.PeerIP);
            remoteAddress.Port = netSendObject.PeerPort;

            // Try to connect to peer, catching InvalidOperationException if queueing the connect attempt fails.
            try
            {
                // Enqueue connection request, ENet auto-generates Peer object if enqueue is successful.
                Peer? pendingPeer = serverHost.Connect(remoteAddress, 1, netSendObject.Data);
                if (pendingPeer != null)
                {
                    // We can store and utilize Peer immediately on connection attempt if we want.
                    // IMPORTANT: This preliminary (pending) peer does not yet have an ID (is 0).
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Queues a disconnect command for a single peer. Only sends the disconnect command, but does not
        ///  actually disconnect the peer yet. An incoming disconnect Event will be generated when the peer
        ///  disconnects (do not remove from clients container here).
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant Peer and command data. </param>
        private void QueueDisconnectPeer(NetSendObject netSendObject)
        {
            // Search for a Peer in clients Dictionary which matches the NetSendObject's peer ID.
            if (clientsById.TryGetValue(netSendObject.PeerID, out var client))
            {
                // Disconnect client Peer, regardless of whether technically in Connected state.
                client.Peer.Disconnect(netSendObject.Data);
                return;
            }

            // If peer does not exist in clients Dictionary, log error.
            _logger.Invoke($"[ENetServer] Tried to disconnect Peer with ID {netSendObject.PeerID}, but no peer with matching ID was found.");
        }

        /// <summary>
        /// Queues a message (packet) to be sent to a single peer.
        /// </summary>
        /// <param name="netSendObject"> NetSendObject containing relevant Peer and payload (packet) data. </param>
        private void QueueMessageOnePeer(NetSendObject netSendObject)
        {
            // Create packet from NetSendObject, determining reliability based on user-defined flag.
            Packet packet = new();
            if (netSendObject.Reliable)
            {
                // Only a single flag. The bitwise or | operator can be used to set multiple flags (ex. [flag1] | [flag2]).
                packet.Create(netSendObject.Bytes, netSendObject.Length, (PacketFlags.Reliable));
            }
            else
            {
                packet.Create(netSendObject.Bytes, netSendObject.Length);
            }

            // Send to peer if one matching peer ID exists in clients.
            if (clientsById.TryGetValue(netSendObject.PeerID, out PeerData? client))
            {
                // Verify that client is actually connected before attempting to send.
                if (client.Peer.State != PeerState.Connected) return;

                // Send message to peer on specified channel (default 0 in NetSendObject if unspecified).
                client.Peer.Send(netSendObject.ChannelID, ref packet);
                return;
            }

            // If peer does not exist in clients Dictionary, log error.
            _logger.Invoke($"[ENetServer] Tried to message Peer with ID {netSendObject.PeerID}, but no peer with matching ID was found.");
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Creates a combined string from the passed-in IP and Port number, separating with a ':'.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns> The IP address and Port combined into a string in the form "[ip]:[port]". </returns>
        private static string MakeAddressString(string ip, ushort port)
        {
            return ip + ":" + port;
        }

        #endregion
    }
}
