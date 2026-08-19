using ENetDriver;
using ENetDriver.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Example_ENetDriver
{
    public class ExampleNetDriver : NetDriverBase
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
                        string str = (recvObject.Bytes == null) ? "[NULL PAYLOAD]" : Encoding.UTF8.GetString(recvObject.Bytes);
                        LogMessage($"Message received from peer at {recvObject.PeerIP}. Message as string: {str}");
                        break;
                    }
            }
        }



        public void ConnectToRemoteHost(string ip, ushort port)
        {
            LogMessage($"[COMMAND] Attempting to connect to remote host at {ip}:{port}...");

            ConnectToPeer(ip, port);
        }

        public void MessageOneRemoteHost(uint id, string message)
        {
            LogMessage($"[COMMAND] Sending message to peer with id {id}...");

            // Add null terminator to string, then use ArrayBuilder class to generate byte[] and enqueue.
            message += '\0';
            (byte[] bytes, int length) = new ArrayBuilder(message.Length * 2)
                .AddString(message)
                .Build();

            NetSendObject obj = NetSendObject.CreateForMessage(id, bytes, length);
            SendMessage(obj);
        }
    }
}
