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
            Driver driver = Driver.Instance;

            // Stop driver when cancel key is pressed. Processes SIGINT signal.
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;    // Do not immediately exit (allow graceful stop).
                driver.Stop();
            };

            try
            {
                // First, ask user for port to run on.
                ushort userPort = GetPortFromUser();

                // Create ExampleDataProcessor instance with custom config.
                DataProcessorConfig dataProcessorConfig = new DataProcessorConfig.Builder()
                    .SetPollTimeIntervals(10, 100)
                    //.SetHealthLoggingInterval(10)
                    .Build();
                ExampleDataProcessor processor = new(dataProcessorConfig);

                // Create server config.
                ServerConfig serverConfig = new ServerConfig.Builder()
                    .SetPort(userPort)
                    .SetPeerLimit(64)
                    .SetChannelLimit(2)
                    .SetPeerTimeoutSettings(5000, 5, 10000, 30000)
                    .SetPollTimeIntervals(10, 100)
                    .Build();

                // Initialize the driver with the data processor instance and server config.
                driver.Initialize(processor, serverConfig);

                // Call the Run() method, which blocks until threads successfully stop.
                //driver.Run();

                // Finally, de-initialize driver before exit.
                //driver.Deinitialize();



                // Start threads but allow input loop, PRIMARILY FOR TESTING.
                RunWithInputLoop(driver, processor);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] :: {ex}");

                driver.Stop();
                driver.WaitUntilStopped();
                driver.Deinitialize();
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

        static void RunWithInputLoop(Driver driver, ExampleDataProcessor processor)
        {
            // Start driver non-blocking.
            driver.Start();

            // INPUT LOOP
            string? userInput;
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

            // Stop and de-initialize on normal exit.
            driver.Stop();
            driver.WaitUntilStopped();
            driver.Deinitialize();
        }
    }
}
