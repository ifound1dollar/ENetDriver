using ENetDriver;
using ENetDriver.Config;
using System.Diagnostics;

namespace Example_ENetDriver
{
    internal class Program
    {
        static readonly ushort DEFAULT_PORT = 7777;

        static void Main()
        {
            ushort port = GetPortFromUser();

            // Create custom NetDriver instance.
            ExampleNetDriver netDriver = new();

            // Register CancelKeyPress event to stop driver asynchronously when Ctrl+C is triggered. Processes SIGINT.
            //Console.CancelKeyPress += (sender, e) =>
            //{
            //    e.Cancel = true;        // Do not immediately exit (allow graceful stop).
            //    netDriver.Stop();       // Initiates stop but does not block.
            //};

            // Create configuration objects for the NetDriver.
            NetDriverConfig netDriverConfig = new NetDriverConfig.Builder()
                .SetPollTimeIntervals(10, 100)
                //.SetHealthLoggingInterval(10)
                .Build();
            ServerConfig serverConfig = new ServerConfig.Builder()
                .SetPort(port)
                .SetPeerLimit(64)
                .SetChannelLimit(2)
                .SetPeerTimeoutSettings(5000, 5, 10000, 30000)
                .SetPollTimeIntervals(10, 100)
                .Build();

            // Initialize the NetDriver.
            netDriver.Initialize(netDriverConfig, serverConfig);

            // Run input loop on separate thread (NetDriver public actions are thread-safe).
            Task.Run(() => RunInputLoop(netDriver));

            // Run the NetDriver. Runs on the main thread, blocking until the driver stops.
            netDriver.Run();

            // After the NetDriver returns (has stopped gracefully), de-initialize before return.
            netDriver.Deinitialize();
        }

        static void RunInputLoop(ExampleNetDriver netDriver)
        {
            string? userInput;
            string[]? inputSplit;
            while (true)
            {
                // Get input and split into array.
                userInput = Console.ReadLine();
                if (userInput == null) continue;
                inputSplit = userInput.ToLower().Split(' ');

                // If exit command, stop driver (non-blocking) and exit input loop.
                if (userInput == "e" || userInput == "exit" || userInput == "q" || userInput == "quit" || userInput == "stop")
                {
                    netDriver.Stop();
                    break;
                }
                // If input starts with connect, attempt to connect to peer at port.
                else if (inputSplit.Length > 0 && inputSplit[0] == "connect")
                {
                    if (inputSplit.Length < 2) continue;

                    if (ushort.TryParse(inputSplit[1], out ushort port))
                    {
                        netDriver.ConnectToRemoteHost("127.0.0.1", port);
                    }
                }
                // Else if no recognized command, send raw text message to first connected peer.
                else
                {
                    netDriver.MessageOneRemoteHost(0, userInput);
                }
            }
        }

        static ushort GetPortFromUser()
        {
            Console.Write("Enter port number to run client host on (minimum {0}): ", DEFAULT_PORT);
            string? userInput = Console.ReadLine();
            ushort userPort = DEFAULT_PORT;
            if (userInput != null)
            {
                if (!ushort.TryParse(userInput, out userPort) || userPort < DEFAULT_PORT)
                {
                    Console.WriteLine("Port number out of range, defaulting to {0}.",
                        DEFAULT_PORT);
                    userPort = DEFAULT_PORT;
                }
            }

            return userPort;
        }

    }
}
