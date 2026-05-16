using ENetDriver;
using ENetDriver.Config;

namespace Example_ENetDriver
{
    internal class Program
    {
        static readonly ushort DEFAULT_PORT = 7777;

        static void Main()
        {
            Driver driver = Driver.Instance;

            // Wrap entire main function in try-catch-finally to ensure ENet is deinitialized at exit.
            try
            {
                // First, ask user for port to run on.
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

                // Create ExampleDataProcessor instance and pass to Driver, then configure optional settings and start the Driver.
                DataProcessorConfig dataProcessorConfig = new DataProcessorConfig.Builder()
                    .SetPollTimeIntervals(10, 100)
                    //.SetHealthLoggingInterval(10)
                    .Build();
                ExampleDataProcessor processor = new(dataProcessorConfig);
                ServerConfig serverConfig = new ServerConfig.Builder()
                    .SetPort(userPort)
                    .SetPeerLimit(64)
                    .SetChannelLimit(2)
                    .SetPeerTimeoutSettings(5000, 5, 10000, 30000)
                    .SetPollTimeIntervals(10, 100)
                    .Build();
                driver.Initialize(processor, serverConfig);

                driver.StartThreadedOperations();



                // INPUT LOOP
                string[]? inputSplit;
                while (true)
                {
                    userInput = Console.ReadLine();
                    if (userInput == null) continue;
                    inputSplit = userInput.ToLower().Split(' ');

                    if (userInput == "e" || userInput == "exit" || userInput == "q" || userInput == "quit" || userInput == "stop")
                    {
                        break;
                    }
                    else if (inputSplit.Length > 0 && inputSplit[0] == "connect")
                    {
                        if (inputSplit.Length < 2) continue;

                        if (ushort.TryParse(inputSplit[1], out ushort port))
                        {
                            processor.ConnectToRemoteHost("127.0.0.1", port);
                        }
                    }
                    else
                    {
                        processor.MessageOneRemoteHost(0, userInput);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] :: {ex}");
            }
            finally
            {
                driver.StopThreadedOperations();
                driver.Deinitialize();
            }
        }
    }
}
