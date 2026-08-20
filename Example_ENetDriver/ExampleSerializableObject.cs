using ENetDriver;
using ENetDriver.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Example_ENetDriver
{
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
}
