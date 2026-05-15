using ENet_Driver;
using ENet_Driver.Data;
using ENetDriver;
using System;
using System.Collections.Generic;
using System.Text;

namespace Example_ENetDriver
{
    internal class ExampleDataProcessor : AbstractDataProcessor
    {
        protected override void ProcessIncomingData(NetRecvObject recvObject)
        {
            switch (recvObject.ActionType)
            {
                case ENetAction.Connect:
                    {
                        LogMessage($"New connection with peer at {recvObject.PeerIP}!");
                        break;
                    }
                case ENetAction.Disconnect:
                    {
                        LogMessage($"Disconnected from peer at {recvObject.PeerIP}.");
                        break;
                    }
                case ENetAction.Timeout:
                    {
                        LogMessage($"Timed out from peer at {recvObject.PeerIP}.");
                        break;
                    }
                case ENetAction.Message:
                    {
                        LogMessage($"Message received from peer at {recvObject.PeerIP}. Message raw bytes: {recvObject.Bytes}");
                        break;
                    }
            }
        }



        public void ConnectToRemoteHost(string ip, ushort port)
        {
            LogMessage($"[COMMAND] Attempting to connect to remote host at {ip}:{port}...");

            NetSendObject obj = NetSendObject.CreateForConnect(ip, port, 100u);
            EnqueueOneOutgoing(obj);
        }

        public void MessageOneRemoteHost(uint id, string message)
        {
            LogMessage($"[COMMAND] Sending message to peer with id {id}...");

            // Add null terminator to string, then use ArrayBuffer class to generate byte[] and enqueue.
            message += '\0';
            ArrayBuffer buffer = new ArrayBuffer(message.Length * 2)
                .AddString(message);

            NetSendObject obj = NetSendObject.CreateForMessage(id, buffer.Bytes, buffer.Length);
            EnqueueOneOutgoing(obj);
        }
    }
}
