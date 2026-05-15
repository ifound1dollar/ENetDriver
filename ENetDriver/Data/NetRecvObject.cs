using ENetDriver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENet_Driver.Data
{
    /// <summary>
    /// Data object for incoming command data like data uint (for non-message actions) and byte[] payload (for
    ///  message actions). New NetRecvObjects must be created using the static .CreateFrom_() methods.
    /// </summary>
    public class NetRecvObject
    {
        /// <summary>
        /// This ENetAction type determines the type of communication received. ENetAction.Timeout will only
        ///  be used within NetRecvObject by ENet upon an automatic timeout event.
        /// </summary>
        public ENetAction ActionType { get; }

        public uint PeerID { get; }
        public string PeerIP { get; }
        public ushort PeerPort { get; }

        public uint Data { get; }

        public byte[]? Bytes { get; }
        public int Length { get; }
        public byte ChannelID { get; }



        /// <summary>
        /// Privately constructs a new NetRecvObject for non-message actions received by a client. Non-message
        ///  actions do not include a byte[] payload and length OR a channel ID, but require only a basic data
        ///  uint descriptor.
        /// </summary>
        private NetRecvObject(ENetAction actionType, uint peerID, string peerIP, ushort peerPort, uint data)
        {
            ActionType = actionType;

            PeerID = peerID;
            PeerIP = peerIP;
            PeerPort = peerPort;

            Data = data;

            // Bytes, Length, and ChannelID are left empty (null or 0).
            Bytes = null;
        }

        /// <summary>
        /// Privately constructs a new NetRecvObject for incoming messages received from a client. Messages
        ///  contain a byte[] with associated length and a channel ID, but do not use the Data uint
        ///  (irrelevant for sends).
        /// </summary>
        private NetRecvObject(ENetAction actionType, uint peerId, string peerIp, ushort peerPort, byte[] bytes, int length, byte channelId)
        {
            ActionType = actionType;
            
            PeerID = peerId;
            PeerIP = peerIp;
            PeerPort = peerPort;

            Bytes = bytes;
            Length = length;
            ChannelID = channelId;

            // Data remains 0.
        }



        #region Static CreateFrom_ Methods

        /// <summary>
        /// Creates and returns a new NetRecvObject from an incoming connect event. Bytes, Length, and ChannelID
        ///  remain empty (null or 0), as they are not relevant for new connections.
        /// </summary>
        /// <param name="peerId"> ID of the newly-connected peer. </param>
        /// <param name="peerIp"> IP address (string) of the peer that just connected. </param>
        /// <param name="peerPort"> Port number of the peer that just connected. </param>
        /// <param name="data"> Data uint containing basic data describing the new connection (ex. 32-bit checksum). </param>
        /// <returns> The newly constructed NetRecvObject from the incoming connect event. </returns>
        public static NetRecvObject CreateFromConnect(uint peerId, string peerIp, ushort peerPort, uint data)
        {
            // Return a new NetRecvObject from the incoming connect event, not including byte[] payload.
            return new NetRecvObject(ENetAction.Connect, peerId, peerIp, peerPort, data);
        }

        /// <summary>
        /// Creates and returns a new NetRecvObject from an incoming disconnect event. Bytes, Length, and ChannelID
        ///  remain empty (null or 0), as they are not relevant for disconnect actions.
        /// </summary>
        /// <param name="peerId"> ID of the peer that just disconnected. </param>
        /// <param name="peerIp"> IP address (string) of the peer that just disconnected. </param>
        /// <param name="peerPort"> Port of the peer that just disconnected. </param>
        /// <param name="data"> Data uint describing the disconnect action (ex. disconnect code). </param>
        /// <returns> The newly constructed NetRecvObject from the incoming disconnect event. </returns>
        public static NetRecvObject CreateFromDisconnect(uint peerId, string peerIp, ushort peerPort, uint data)
        {
            // Return a new NetRecvObject from the incoming disconnect event, not including byte[] payload.
            return new NetRecvObject(ENetAction.Disconnect, peerId, peerIp, peerPort, data);
        }

        /// <summary>
        /// Creates and returns a new NetRecvObject from an incoming timeout (disconnect) event. Bytes, Length,
        ///  and ChannelID remain empty (null or 0), as they are not relevant for timeout actions. NOTE: ENet
        ///  does not provide a custom data uint by default upon timeout actions; this must be provided
        ///  manually when this method is called.
        /// </summary>
        /// <param name="peerId"> ID of the peer that just timed out. </param>
        /// <param name="peerIp"> IP address (string) of the peer that just timed out. </param>
        /// <param name="peerPort"> Port of the peer that just timed out. </param>
        /// <param name="data"> Data uint describing the disconnect action (ex. timeout disconnect code). </param>
        /// <returns> The newly constructed NetRecvObject from the incoming disconnect event. </returns>
        public static NetRecvObject CreateFromTimeout(uint peerId, string peerIp, ushort peerPort, uint data)
        {
            // Return a new NetRecvObject from the incoming disconnet event, including user-provided data uint.
            return new NetRecvObject(ENetAction.Timeout, peerId, peerIp, peerPort, data);
        }

        /// <summary>
        /// Creates and returns a new NetRecvObject from a message action. Data uint remains 0, as messages only
        ///  include a payload byte[] with associated length plus a channel ID.
        /// </summary>
        /// <param name="peerId"> ID of peer message was received from. </param>
        /// <param name="peerIp"> IP address (string) of peer message was received from. </param>
        /// <param name="peerPort"> Port of peer message was received from. </param>
        /// <param name="bytes"> Byte[] containing packet payload data. </param>
        /// <param name="length"> Length of data contained in byte[] (in case array is larger than data length). </param>
        /// <param name="channelId"> Channel ID byte that the message was received on. </param>
        public static NetRecvObject CreateFromMessage(uint peerId, string peerIp, ushort peerPort, byte[] bytes, int length, byte channelId)
        {
            // Return a new NetRecvObject with message byte[] payload, length, and channel ID, but no Data uint.
            return new NetRecvObject(ENetAction.Message, peerId, peerIp, peerPort, bytes, length, channelId);
        }

        #endregion
    }
}
