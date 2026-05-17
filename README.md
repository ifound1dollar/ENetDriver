# ENetDriver

This project is meant to be a plug-and-play multithreaded wrapper around the ENet reliable UDP library. By design, the ENet host/listener must run on a single thread, and this can cause a performance bottleneck with high traffic. For high-performance UDP communication with ENet, the application must devise a multithreaded solution to allow ENet network tasks to run on a dedicated thread isolated from application logic. This ENetDriver library intends to do just that, but in a way that abstracts the low-level/behind-the-scenes ENet operations from the application developer. A description of how the library works is below, followed by a basic user guide.

## How It Works

Ultimately, this library works by running two separate threads for data processing and for ENet host/listener tasks. These threads communicate via an inter-thread messaging system which avoids the risks and potential pitfalls that can occur with multithreading. This inter-thread messaging system is largely abstracted from the user, requiring only a concrete implementation of the data processor class and a few calls to public methods in the Driver class (see below). IMPORTANT: The two threads used by the data processor and the ENet server are separate from the main thread, meaning that the developer must ensure that the main thread remains running while the server is active.

The library consists of three main classes which handle most application logic. These three classes handle threading operations, data processing, and ENet host/listener tasks.
1. **Driver:** This is the main driver class which the user will be working with to initialize and run the ENet server. This class has only a handful of publicly-facing methods which the application developer will interact with, as the rest of the logic is internal or private and is used for abstracted logic. A high-level summary is that the Driver class manages two separate worker threads: one for data processing (user-defined logic) and one for ENet host/listener tasks (completely abstracted from the user). Upon calling the Initialize() method, the Driver initializes the ENet native library and accepts an existing concrete data processor class (see below) alongside a server configuration settings object which determines how the server operates. Calling the Start() method will start up both the data processor and the server threads, which begins listening and processing data according to the user-defined logic described below.
2. **ENet Server:** This is the internal ENet host/listener class which is entirely abstracted from the user, performing networking operations and automatically passing them to the data processor as they are received; similarly, it handles sending outgoing messages coming from the data processor. The driver manages this class instance entirely, from creation to starting/stopping execution. Internally, the server creates an ENet host and configures it according to the server configuration settings object that must be passed into the Driver class on initialization. Configuration settings for this class include a listening port, a maximum number of peers that can be connected, a maximum number of communication channels, peer timeout settings, and time limits for incoming/outgoing contexts (to prevent high CPU usage when switching contexts each frame OR from getting stuck in one context indefinitely during heavy traffic). All ENet-specific actions are handled internally within this class.
3. **(Abstract) Data Processor:** This abstract class implements the internal, behind-the-scenes functionality that is required for application execution. It handles enqueueing and dequeueing items from the thread-safe queues (BlockingCollections), as well as actually dispatching incoming commands/messages in a way that the user can easily work with. The purpose behind this abstract class is to ensure that the user does not need to work directly with multithreading, instead allowing them to focus on application logic. Importantly, this abstract class only handles internal logic and thus does *not* implement any application logic; the developer must create a concrete data processor class deriving from this abstract class which handles application logic. The user needs only to override the handler methods to process incoming connect, disconnect, timeout, and message events. Additionally, the data processor runs on a separate thread from the main thread, and the user's concrete implementation must be careful to ensure thread safety (ex. if processing incoming messages asynchronously). That said, the default Enqueue() method which adds an outgoing command/message data object to the thread-safe queue is inherently thread-safe and can be used by any thread.

Additionally, a handful of data objects are used by the library to communicate data between the data processor and server threads, and to allow high-efficiency byte[] creation for outgoing messages. These classes are NetSendObject (for outgoing commands/messages), NetRecvObject (for incoming commands/messages), and ArrayBuffer (for efficiently assembling byte[]s to send over the network).
1. **NetSendObject:** Used for outgoing commands/messages. These objects *cannot* be instantiated directly, and instead require the use of a handful of static methods within the class. These static methods correspond to the type of outgoing command/message to send, and are prefixed with "CreateFor_". For example, CreateForConnect() creates and returns a NetSendObject with only the necessary data to make an outgoing connection request (remote IP, remote port, and an optional uint for request data). Likewise, the CreateForMessage() method accepts a payload byte[] with associated length and other message-relevant data.
2. **NetRecvObject:** Has many of the same fields as the NetSendObject class, but is generated internally by the ENet server class and thus *does not* have public "CreateFor_" methods. Instead, the user only needs to read data from these objects in order to determine what logic to execute. The most important piece of this class is the ENetAction enum field, which determines whether the incoming command is for connect, disconnect, timeout, or message. The user should switch on this enum and route logic accordingly.
3. **ArrayBuffer:** This class exists exclusively to allow high-efficiency byte[] creation that minimizes memory allocations by using an arbitrary-length byte[] internally with a separate integer for length (the byte[] might be length 1024, but the dedicated Length integer will correctly describe that its length may only be 76). The ArrayBuffer is entirely mutable and is designed to be used with fluent syntax, as it allows adding and reading different types of primitive data in a fully-abstracted way. The user should create new EMPTY instance of this class (which allows setting a specific byte[] size OR defaulting to 1024) each time they want to send a message with a payload, adding items in a specific order. The recipient should create a POPULATED instance of this class from the existing byte[], then reading items in the exact reverse order as they were created. It is the user's responsibility to ensure that the sender and receiver know exactly which order to add/read elements in. NOTE: The internal byte[] will resize itself during adding if it is not large enough to fit the data, and an exception will be thrown if the user tries to read an element which there is not enough room for (IndexOutOfRangeException).

### ArrayBuffer examples
Create empty ArrayBuffer and add a few items:
```csharp
// Instantiate using default constructor, which allocates a byte[] of size 1024. Can use overloaded constructor to choose a specific size.
ArrayBuffer buffer = new ArrayBuffer()
  .AddString("A string")
  .AddBool(true)
  .AddDouble(10.0d);
```
Create populated ArrayBuffer from payload byte[] and read items into variables:
```csharp
// Instantiate using overloaded construtor which accepts a byte[] and length integer.
ArrayBuffer buffer = new ArrayBuffer(payloadBytes, payloadLength);

// Read items into variables in the EXACT reverse order they were added. Will throw IndexOutOfRangeException if invalid read (wrong order, wrong type, or wrong number of elements).
double d0 = buffer.ReadDouble();
bool b0 = buffer.ReadBool();
string s0 = buffer.ReadString();
```

## User Guide

Using the library is meant to be as intuitive and easy-to-understand as possible. The most basic, high-level description of how to use the library is as follows (detailed description of each step will follow):
1. Implement a concrete data processor class which inherits from AbstractDataProcessor and overrides the required incoming command/message handler methods. This class is where the application logic should be.
2. Create an instance of the DataProcessorConfig class to set up various configuration for the data processor. This configuration is used by the internal logic and is separate from any custom configuration that may be implemented in the user-defined concrete data processor class.
3. Create an instance of the user-defined concrete data processor, which requires passing the previously-created DataProcessorConfig object to the AbstractDataProcessor's constructor to configure internal behavior.
4. Create an instance of the ServerConfig class to set various configuration settings for the ENet host/listener. This is done the same way as the data processor configuration, but includes associated network-relevant settings.
5. Initialize the Driver singleton class, passing it the previously-instantiated concrete data processor and server config instances. Note that this does not start the server host or the data processor, just initializes them with configuration settings.
6. Start threaded operations within the Driver. This will start up both the data processor and host/listener threads, immediately allowing connections and listening for incoming messages. IMPORTANT: Starting the threads does *not* block the main thread; the user must ensure that the main thread remains running for as long as the driver is running.
7. **Once execution should stop,** explicitly command the Driver to stop threaded operations, then de-initialize the Driver before the application exits. IMPORTANT: The Driver's de-initialization method *must* be called before the application exits to ensure that the underlying ENet native library is successfully de-initialized.

### EXAMPLE: Concrete Data Processor 
```csharp
public class ConcreteDataProcessor : AbstractDataProcessor
{
  public ExampleDataProcessor(DataProcessorConfig config) : base(config)
  {
      // We must call base constructor with our config.
  }

  // This method is invoked by the internal library whenever an incoming command/message is received. Implementation 
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

  // This user-defined 'send' method accepts the ID of the peer to message and a simple string. 
  public void MessageOneRemoteHost(uint id, string message)
  {
    LogMessage($"[COMMAND] Sending message to peer with id {id}...");
  
    // Add null terminator to string, then use ArrayBuffer class to generate byte[] and enqueue.
    message += '\0';
    ArrayBuffer buffer = new ArrayBuffer(message.Length * 2)
        .AddString(message);

    // Create an outgoing NetSendObject for a message action, then use the AbstractDataProcessor's built-in EnqueueOneOutgoing to pass it to the ENet server.
    NetSendObject obj = NetSendObject.CreateForMessage(id, buffer.Bytes, buffer.Length);
    EnqueueOneOutgoing(obj);
  }
}
```
