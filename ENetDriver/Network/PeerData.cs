using ENet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENetDriver.Network
{
    internal record class PeerData
    {
        // IMPORTANT: Peer structure hard-references a native by-reference peer object (Peer struct
        //  behaves exactly like a reference variable).
        private Peer _peer;

        internal ref Peer Peer => ref _peer;      // Cannot use auto property to return Peer by reference.
        internal DateTime ConnectTime { get; }



        /// <summary>
        /// Creates a new PeerData object with the specified Peer struct. IMPORTANT: The passed-in Peer
        ///  struct uses a native IntPtr that directly points to a by-reference native Peer object. Even
        ///  though Peer is a struct in C#, this peer is effectively by-reference because of the native
        ///  implementation.
        /// </summary>
        /// <param name="peer"></param>
        internal PeerData(Peer peer)
        {
            _peer = peer;

            ConnectTime = DateTime.UtcNow;
        }
    }
}
