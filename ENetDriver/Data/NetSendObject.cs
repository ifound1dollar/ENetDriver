using ENetDriver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetDriver.Data
{
    /// <summary>
    /// Data object for outgoing command data like data uint (for non-message actions) and byte[] payload (for
    ///  message actions). New NetSendObjects must be created using the static .CreateFor_() methods.
    /// </summary>
    public class NetSendObject
    {
        /// <summary>
        /// This ENetAction type determines the type of action to perform on the server. NetSendObjects should
        ///  not use ENetAction.Timeout (should only be utilized by ENet on automatic timeout). Outgoing
        ///  disconnects should always use ENetAction.Disconnect with a 'timeout' data uint disconnect code.
        /// </summary>
        public ENetAction ActionType { get; }

        public uint PeerID { get; }
        public string PeerIP { get; }
        public ushort PeerPort { get; }

        public uint Data { get; }

        public byte[]? Bytes { get; }
        public int Length { get; }
        public byte ChannelID { get; }
        public bool Reliable { get; }




        /// <summary>
        /// Privately constructs a new NetSendObject for non-message actions, requiring only a Data uint. Bytes,
        ///  Length, ChannelID, and Reliable remain empty (unused for non-message actions like connect/disconnect).
        /// </summary>
        private NetSendObject(ENetAction actionType, uint peerId, string peerIp, ushort peerPort, uint data)
        {
            ActionType = actionType;
            
            PeerID = peerId;
            PeerIP = peerIp;
            PeerPort = peerPort;

            Data = data;

            // Bytes, Length, and ChannelID are left empty (null or 0). Reliable defaults to false.
            Bytes = null;
        }

        /// <summary>
        /// Privately constructs a new NetSendObject for a message action, requiring a byte[] payload, length,
        ///  channel ID, and reliable flag. The Data uint remains 0, as it is not relevant for message actions.
        /// </summary>
        private NetSendObject(ENetAction actionType, uint peerId, string peerIp, ushort peerPort, byte[] bytes, int length,
            byte channelId, bool reliable)
        {
            ActionType = actionType;
            
            PeerID = peerId;
            PeerIP = peerIp;
            PeerPort = peerPort;

            Bytes = bytes;
            Length = length;
            ChannelID = channelId;
            Reliable = reliable;

            // Data remains 0.
        }



        #region Static CreateFor_ Methods

        /// <summary>
        /// Creates and returns a new NetSendObject for a connect action. Bytes, Length, ChannelID, and Reliable
        ///  remain empty (null or 0 or false), and PeerID will be generated later on connection success.
        /// </summary>
        /// <param name="peerIp"> IP address (string) of the peer to connect to. </param>
        /// <param name="peerPort"> Port number of the peer to connect to. </param>
        /// <param name="data"> Data uint containing basic data describing the connection attempt (ex. 32-bit checksum). </param>
        /// <returns> The newly constructed NetSendObject for an outgoing connection attempt. </returns>
        public static NetSendObject CreateForConnect(string peerIp, ushort peerPort, uint data)
        {
            // Return new non-message NetSendObject, leaving peerId 0 because it is irrelevant for outgoing connect attempt.
            return new NetSendObject(ENetAction.Connect, 0, peerIp, peerPort, data);
        }

        /// <summary>
        /// Creates and returns a new NetSendObject for a disconnect action on a peer by ID. Bytes, Length,
        ///  ChannelID, and Reliable remain empty (null or 0 or false), as they are not relevant for
        ///  disconnect actions.
        /// </summary>
        /// <param name="peerId"> ID of the peer to disconnect. This must be a known valid peer ID. </param>
        /// <param name="data"> Data uint containing basic data describing the disconnect action (ex. disconnect code). </param>
        /// <returns> The newly constructed NetSendObject for an outgoing disconnect action. </returns>
        public static NetSendObject CreateForDisconnect(uint peerId, uint data)
        {
            // Return new non-message NetSendObject with disconnect ENetAction. IP and Port are not defined.
            return new NetSendObject(ENetAction.Disconnect, peerId, string.Empty, 0, data);
        }

        /// <summary>
        /// Creates and returns a new NetSendObject for a disconnect action on a peer by ID. Bytes, Length,
        ///  ChannelID, and Reliable remain empty (null or 0 or false), as they are not relevant for
        ///  disconnect actions.
        /// NOTE: This overloaded method includes optional parameters for the peer's IP and Port (peer ID
        ///  is still used to find the peer).
        /// </summary>
        /// <param name="peerId"> ID of the peer to disconnect. This must be a known valid peer ID. </param>
        /// <param name="peerIp"> IP address string of the peer to disconnect (optional). </param>
        /// <param name="peerPort"> Port number of the peer to disconnect (optional). </param>
        /// <param name="data"> Data uint containing basic data describing the disconnect action (ex. disconnect code). </param>
        /// <returns> The newly constructed NetSendObject for an outgoing disconnect action. </returns>
        public static NetSendObject CreateForDisconnect(uint peerId, string peerIp, ushort peerPort, uint data)
        {
            // Return new non-message NetSendObject with disconnect ENetAction.
            return new NetSendObject(ENetAction.Disconnect, peerId, peerIp, peerPort, data);
        }

        /// <summary>
        /// Creates and returns a new NetSendObject for a message action to be sent to a peer by ID. Data
        ///  uint remains 0, but message sends require a payload byte[] with associated length, an OPTIONAL
        ///  channel ID (default 0), and an OPTIONAL reliable flag (default true).
        /// </summary>
        /// <param name="peerId"> ID of peer to send message to. This must be a known valid peer ID. </param>
        /// <param name="bytes"> Byte[] containing packet payload data. </param>
        /// <param name="length"> Length of data contained in byte[] (in case array is larger than data length). </param>
        /// <param name="channelId"> OPTIONAL Channel ID (byte) to send the message on. Default 0. </param>
        /// <param name="reliable"> OPTIONAL Flag determining whether to send the message reliably. Default true. </param>
        /// <returns> The newly constructed NetSendObject for an outgoing message. </returns>
        public static NetSendObject CreateForMessage(uint peerId, byte[] bytes, int length, byte channelId = 0, bool reliable = true)
        {
            // Return a new message NetSendObject with byte[] payload and length, plus channel ID and reliable flag.
            // IP and Port are not defined.
            return new NetSendObject(ENetAction.Message, peerId, string.Empty, 0, bytes, length, channelId, reliable);
        }

        /// <summary>
        /// Creates and returns a new NetSendObject for a message action to be sent to a peer by ID. Data
        ///  uint remains 0, but message sends require a payload byte[] with associated length, an OPTIONAL
        ///  channel ID (default 0), and an OPTIONAL reliable flag (default true).
        /// NOTE: This overloaded method includes optional parameters for the peer's IP and Port (peer ID
        ///  is still used to find the peer).
        /// </summary>
        /// <param name="peerId"> ID of peer to send message to. This must be a known valid peer ID. </param>
        /// <param name="peerIp"> IP address string of peer to send message to (optional). </param>
        /// <param name="peerPort"> Port of peer to send message to (optional). </param>
        /// <param name="bytes"> Byte[] containing packet payload data. </param>
        /// <param name="length"> Length of data contained in byte[] (in case array is larger than data length). </param>
        /// <param name="channelId"> OPTIONAL Channel ID (byte) to send the message on. Default 0. </param>
        /// <param name="reliable"> OPTIONAL Flag determining whether to send the message reliably. Default true. </param>
        /// <returns> The newly constructed NetSendObject for an outgoing message. </returns>
        public static NetSendObject CreateForMessage(uint peerId, string peerIp, ushort peerPort, byte[] bytes, int length,
            byte channelId = 0, bool reliable = true)
        {
            // Return a new message NetSendObject with byte[] payload and length, plus channel ID and reliable flag.
            return new NetSendObject(ENetAction.Message, peerId, peerIp, peerPort, bytes, length, channelId, reliable);
        }

        #endregion
    }
}
