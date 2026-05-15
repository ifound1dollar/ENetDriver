using ENetDriver;

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
                ExampleDataProcessor processor = new();
                driver.Initialize(processor, userPort)
                    .SetOptionalHostSettings(64, 2)
                    .SetOptionalPeerTimeoutSettings(5000, 5, 10000, 30000)
                    .SetOptionalPollIntervals(100, 10)
                    //.SetHealthLoggingInterval(10)
                    .SetOptionalLogger(Console.WriteLine);
                driver.StartDriver();



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
                driver.StopDriver();
                driver.Deinitialize();
            }
        }
    }
}
