# ENetDriver

This library is essentially a plug-and-play multithreaded wrapper around the ENet reliable UDP library. By design, the ENet host/listener must run on a single thread, and this can cause a performance bottleneck with high traffic. High-performance UDP communication with ENet requires the application to implement multithreading strategies to allow ENet network tasks to run on a dedicated thread isolated from application logic. This ENetDriver library intends to do just that, but in a way that abstracts the low-level/behind-the-scenes ENet operations from the application developer. A description of how the library works is below, followed by a general user guide.

## How It Works

Ultimately, this library works by breaking data processing and network tasks into separate threads. The NetDriver class handles all tasks from the user's perspective, including managing a network worker thread which runs the ENet host in the background and communications with the NetDriver (data processor) thread via a pair of thread-safe queues. Note that the NetDriver's data processing logic executes synchronously directly on the thread that calls the Run() method, meaning that the operation blocks by default; that said, the NetDriver's public methods are inherently thread-safe and thus it can be interacted with by any thread. The inter-thread messaging system used by the driver allows both threads to safely and efficiently communicate with each other, avoiding common issues that often appear when working with multiple threads. These complex inter-thread messaging operations are largely abstracted from the user, as the user only needs to create a concrete NetDriver class which inherits from NetDriverBase. The user simply needs to implement the ProcessIncomingData() method which is invoked the instant an incoming message is received from a remote peer; the necessary outgoing connect/disconnect/message methods are built into the NetDriverBase class by default and can simply be called by the user to perform outgoing tasks.

The library consists of two main classes which handle most application logic. These three classes handle threading operations, data processing, and ENet host/listener tasks.
1. **NetDriverBase (Abstract):** This is the main driver class which the user will be working with to initialize and run data processing operations. It is an abstract class that implements complex multithreading logic behind the scenes, but is simple to use from the user's perspective. The user must implement a concrete class inheriting from NetDriverBase and implement the abstract ProcessIncomingData() method, which is the initial point where incoming data is actually parsed and processed. Behind the scenes, this class manages a network thread worker which performs all ENet host/listener tasks. Upon calling the blocking Run() method, the driver starts the background network thread and begins listening for incoming data objects with the user-defined ProcessIncomingData() method. There are a handful of publicly-facing methods within the class which are necessary to actually doing network tasks, like outgoing connect/disconnect/message methods and the required initialize/run/stop methods. Note that by design, this driver class is thread-safe and the publicly-facing outgoing methods can be used by any thread.
2. **Server:** This is the internal ENet host/listener class which is entirely abstracted from the user, performing networking operations and automatically passing them to the NetDriver (data processor) as they are received; similarly, it handles sending outgoing messages coming from the driver. The driver manages this class instance entirely, from creation to starting/stopping execution. Internally, the server creates an ENet host and configures it according to the server configuration settings object that must be passed into the NetDriver class on initialization. Configuration settings for this class include a listening port, a maximum number of peers that can be connected, a maximum number of communication channels, peer timeout settings, and time limits for incoming/outgoing contexts (to prevent high CPU usage when switching contexts each frame OR from getting stuck in one context indefinitely during heavy traffic). All ENet-specific actions are handled internally within this class.

Additionally, a handful of data objects are used by the library to communicate data between the data processor and server threads, and to allow high-efficiency byte[] creation for outgoing messages. These classes are NetSendObject (for outgoing commands/messages), NetRecvObject (for incoming commands/messages), and ArrayBuffer (for efficiently assembling byte[]s to send over the network).
1. **NetSendObject:** Used for outgoing commands/messages. These objects *cannot* be instantiated directly, and instead require the use of a handful of static methods within the class. These static methods correspond to the type of outgoing command/message to send, and are prefixed with "CreateFor_". For example, CreateForConnect() creates and returns a NetSendObject with only the necessary data to make an outgoing connection request (remote IP, remote port, and an optional uint for request data). Likewise, the CreateForMessage() method accepts a payload byte[] with associated length and other message-relevant data.
2. **NetRecvObject:** Has many of the same fields as the NetSendObject class, but is generated internally by the ENet server class and thus *does not* have public "CreateFor_" methods. Instead, the user only needs to read data from these objects in order to determine what logic to execute. The most important piece of this class is the ENetAction enum field, which determines whether the incoming command is for connect, disconnect, timeout, or message. The user should switch on this enum and route logic accordingly.
3. **ArrayBuilder:** This class exists exclusively to allow high-efficiency byte[] creation that minimizes memory allocations by using an arbitrary-length byte[] internally with a separate integer for length (the byte[] might be 1024 allocated bytes, but the dedicated Length integer will correctly describe that the data length is only 76 bytes). The ArrayBuilder, as the name indicates, implements the builder pattern to write various data (primitive types and strings) to the byte[] buffer; all logic to write data is entirely abstracted from the user. Additionally, the internal byte[] buffer will automatically expand its size if data is attempted to be added that the buffer does not have room for. Once all desired data is written, the Build() method must be called to return the buffer as a raw byte[] alongside its data length. Because the array that was contained in the buffer is returned by reference, the ArrayBuilder instance should be discarded immediately after the Build() method is called in order to prevent accidental modification of the referenced byte[].
4. **ArrayReader:** This is the counterpart to the ArrayBuilder, and is used to efficiently read primitive data (and strings) from a provided payload byte[]. The ArrayReader requires a byte[] and a data length integer upon instantiation, and then each piece of data should be read from the object in the *exact reverse order* from which it was written (ex. add double -> add string =>> read string -> read double). The developer must know which order the data was written to use this effectively, but that is often the case when working with raw-serialized data received over a network. If a piece of data is attempted to be read that does not exist in the reader (i.e. the buffer does not contain enough bytes for the requested data), an IndexOutOfBoundsException will be thrown.

Lastly, the library includes an interface that can be used by any class which needs to be serialized and deserialized to/from a byte[]. The INetSerializable interface includes two methods: NetSerialize() to serialize the object into a byte[] and return the array and its length as an integer, and NetDeserialize() to deserialize a byte[] and associated length into a usable object (note that the deserialize method should be called on an empty object after construction). This interface makes serializing and deserializing objects particularly easy. The user is expected to override both methods to write/read all data to/from an ArrayBuilder or ArrayReader (examples below).

### ArrayBuilder and ArrayReader examples
Use the ArrayBuilder to serialize data into a byte[]:
```csharp
// Instantiate using default constructor, which allocates a byte[] of size 1024.
// Can use overloaded constructor to initialize byte[] to a specific size.
(byte[] payload, int length) = new ArrayBuilder()
  .AddString("A string")
  .AddBool(true)
  .AddDouble(10.0d)
  .Build();

// Enqueue the payload and send over the network...
```
Use the ArrayReader to read data from a byte[]:
```csharp
// Instantiate with received byte[] and its data length as an integer.
ArrayReader reader = new ArrayReader(payloadBytes, payloadLength);

// Read items into variables in the EXACT reverse order they were added.
// Will throw IndexOutOfRangeException if invalid read (wrong order, wrong type, or wrong number of elements).
double d0 = reader.ReadDouble();    // 10.0d
bool b0 = reader.ReadBool();        // true
string s0 = reader.ReadString();    // "A string"

// Do something with the deserialized data...
```

___

# User Guide

Using the library is meant to be as intuitive and easy-to-understand as possible. The most basic, high-level description of how to use the library is as follows (examples below):
1. Implement a concrete driver class which inherits from NetDriverBase and overrides the required ProcessIncomingData(). This is the main driver class where data processing logic will be handled.
2. Create an instance of the NetDriverConfig class to set up various configuration for the driver / data processor. This configuration is used by the internal NetDriverBase logic and is separate from any custom configuration that may be implemented in the user-defined driver class.
3. Create an instance of the ServerConfig class to set various configuration settings for the ENet host/listener. This is done the same way as the net driver configuration, but includes associated network-relevant settings.
4. Create an instance of the custom driver class (from above), then initialize with the previously-created NetDriverConfig and ServerConfig objects. The driver must be initialized before it can be run.
5. Run the custom driver class using the Run() method. This starts up the background ENet host/listener thread then *blocks* the calling thread until the driver is explicitly stopped using the Stop() method. Note that it is the user's responsibility to ensure that the Stop() method can actually be called from somewhere while the Run() method blocks.
6. **Once execution should stop,** explicitly command the driver to stop using the Stop() method. This method begins the graceful stop process in the background, returning immediately without waiting for the driver to stop. The Run() method which is currently blocking will return once the stopping process completes; the user must wait for the graceful stop process after calling this method. IMPORTANT: After the driver is stopped but immediately before the application exits, the user *must* call the Deinitialize() method to ensure that the underlying ENet native library is successfully de-initialized.

### EXAMPLE: Concrete NetDriver class (inherits from NetDriverBase)
```csharp
public class ExampleNetDriver : NetDriverBase
{
    // Custom implementation of the abstract method, processing an incoming message based on ENetAction type.
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


    // This user-defined send method accepts a peer ID and a simple string message.
    public void MessageOneRemoteHost(uint id, string message)
    {
        LogMessage($"[COMMAND] Sending message to peer with id {id}...");

        // Add null terminator to string, then use ArrayBuilder class to generate byte[] and enqueue.
        message += '\0';
        (byte[] bytes, int length) = new ArrayBuilder(message.Length * 2)
            .AddString(message)
            .Build();

        // Create a NetSendObject with the associated creator method, then send with the built-in NetDriverBase's send method.
        NetSendObject obj = NetSendObject.CreateForMessage(id, bytes, length);
        SendMessage(obj);
    }
}
```

### EXAMPLE: NetDriverConfig usage
```csharp
// NOTE: All configuration settings have default values, so the user only needs to set their desired settings in the Builder.
NetDriverConfig netDriverConfig = new NetDriverConfig.Builder()
  .SetPollTimeIntervals(10, 100)        // Sets minimum and maximum time (ms) to spend before switching between incoming/outgoing contexts.
  .SetHealthLoggingInterval(10)         // Sets the interval (seconds) between health logging output (prints performance health data).
  .Build();
```

### EXAMPLE: ServerConfig usage
```csharp
// NOTE: All configuration settings have default values, so the user only needs to set their desired settings in the Builder.
ServerConfig serverConfig = new ServerConfig.Builder()
  .SetPort(7777)
  .SetPeerLimit(64)                                // Must be in range 1-4095.
  .SetChannelLimit(2)                              // Must be in range 1-255.
  .SetPeerTimeoutSettings(5000, 5, 10000, 30000)   // Ping interval, maximum ping attempts, timeout minimum ms, timeout maximum ms.
  .SetPollTimeIntervals(10, 100)                   // Sets the interval (seconds) between health logging output (prints performance health data).
  .Build();
```

### EXAMPLE: Custom NetDriver class initialization and run (blocking)
```csharp
// Create custom NetDriver class instance and initialize with config (defined above).
ExampleNetDriver netDriver = new();
netDriver.Initialize(netDriverConfig, serverConfig);

// Run the driver synchronously, blocking until the data processing / network threads stop.
netDriver.Run();

// NOTE: The user must implement some behavior to enable the Stop() method to be called by another non-blocking thread.
// For example, an asynchronous input loop using Task.Run() that listens for input continuously using Console.Readline().
//  This hypothetical input loop can safely call public outgoing connect/disconnect/message methods via the NetDriver, as
//  it is inherently thread safe.
```

### EXAMPLE: Driver stop and de-initialization
```csharp
// Async Stop() method call example: listen for Ctrl+C and stop driver once detected.
// NOTE: This CancelKeyPress subscription must be performed sometime before the blocking Run() method is called, but
//  after the NetDriver object is created (obviously).
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;        // Do not immediately exit (allow graceful stop).
    netDriver.Stop();       // Initiates stop but does not block.
};

// -- COMING BACK TO THE RUN METHOD CALL --
netDriver.Run()
// The Run() method returns once the stop operation finishes successfully.

// After stopping completes, de-initialize the NetDriver before the application exits.
netDriver.Deinitialize();
```

### EXAMPLE: Custom serializable data object
```csharp
public class ExampleSerializableObject : INetSerializable
{
    public string ExampleString { get; private set; } = "A string";
    public bool ExampleBool { get; private set; } = true;
    public double ExampleDouble { get; private set; } = 10.0d;

    public (byte[], int) NetSerialize()
    {
        // Create ArrayBuilder and add all relevant data, returning the resulting byte[] and length.
        return new ArrayBuilder()
            .AddString(ExampleString)
            .AddBool(ExampleBool)
            .AddDouble(ExampleDouble)
            .Build();
    }
    public void NetDeserialize(byte[] bytes, int length)
    {
        // Create ArrayReader from the passed-in byte[] and length, then read values in reverse order.
        var reader = new ArrayReader(bytes, length);
        ExampleDouble = reader.ReadDouble();
        ExampleBool = reader.ReadBool();
        ExampleString = reader.ReadString();
    }
}
```
