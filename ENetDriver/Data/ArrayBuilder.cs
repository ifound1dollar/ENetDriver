using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace ENetDriver.Data
{
    /// <summary>
    /// A builder class used to efficiently populate a byte[] with primitive data (and strings) without
    ///  memory allocations. Directly converts data to their byte[] forms and adds to an internal array
    ///  buffer. Calling the Build() method will return the populated buffer as a raw byte[] alongside
    ///  its data length (the byte[] will be of arbitrary size, so the returned data length integer
    ///  must be used to actually denote the length of the data in bytes).
    /// After the Build() method is called, the ArrayBuilder instance should be discarded.
    /// </summary>
    public class ArrayBuilder
    {
        private byte[] Bytes { get; set; }
        private int Length { get; set; }

        /// <summary>
        /// Instantiates a new ArrayBuilder object with a default byte[] capacity of 1024. This will often
        ///  be far larger than necessary and is thus relatively wasteful; the overloaded constructor
        ///  with a capacity argument should be used when possible.
        /// </summary>
        public ArrayBuilder()
        {
            Bytes = new byte[1024];
            Length = 0;
        }

        /// <summary>
        /// Instantiates a new ArrayBuilder object with the specified capacity. Capacity will
        ///  automatically expand if data is added that exceeds this initial capacity (but this is
        ///  relatively expensive and should be avoided when possible).
        /// </summary>
        /// <param name="capacity"> The capacity (length) of the ArrayBuilder's byte[]. Must be positive. </param>
        public ArrayBuilder(int capacity)
        {
            Bytes = new byte[capacity];
            Length = 0;
        }

        private void ExpandCapacity()
        {
            // Calculate target length, which is double the currently length with an enforced minimum of 13.
            // Minimum size 13 ensures the largest primitives (size 8) can always be added (ex. allocated length 2 but trying
            //  to add a double, 2x capacity would only create a size 4 array and throw an OutOfBoundsException).
            int targetLength = Math.Max(Bytes.Length * 2, 13);

            // Initialize array with target length, then copy current contents over and replace reference.
            byte[] expanded = new byte[targetLength];
            Buffer.BlockCopy(Bytes, 0, expanded, 0, Length + 1);
            Bytes = expanded;
        }



        // BUILDER METHODS BELOW

        public ArrayBuilder AddByte(byte value)
        {
            if (Length + 1 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Length will equal next empty index, so set element at Length then increment.
            Bytes[Length] = value;
            Length++;

            return this;
        }

        public ArrayBuilder AddShort(short value)
        {
            if (Length + 2 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 2);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteInt16LittleEndian(target, value);     // We will use little-endian format, as is standard.
            Length += 2;

            return this;
        }

        public ArrayBuilder AddUShort(ushort value)
        {
            if (Length + 2 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 2);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteUInt16LittleEndian(target, value);    // We will use little-endian format, as is standard.
            Length += 2;

            return this;
        }

        public ArrayBuilder AddInt(int value)
        {
            if (Length + 4 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 4);            // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteInt32LittleEndian(target, value); // We will use little-endian format, as is standard.
            Length += 4;

            return this;
        }

        public ArrayBuilder AddUInt(uint value)
        {
            if (Length + 4 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 4);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteUInt32LittleEndian(target, value);    // We will use little-endian format, as is standard.
            Length += 4;

            return this;
        }

        public ArrayBuilder AddLong(long value)
        {
            if (Length + 8 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 8);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteInt64LittleEndian(target, value);     // We will use little-endian format, as is standard.
            Length += 8;

            return this;
        }

        public ArrayBuilder AddULong(ulong value)
        {
            if (Length + 8 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 8);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteUInt64LittleEndian(target, value);    // We will use little-endian format, as is standard.
            Length += 8;

            return this;
        }

        public ArrayBuilder AddFloat(float value)
        {
            if (Length + 4 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 4);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteSingleLittleEndian(target, value);    // We will use little-endian format, as is standard.
            Length += 4;

            return this;
        }

        public ArrayBuilder AddDouble(double value)
        {
            if (Length + 8 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // To avoid allocating new memory, create a span at our current byte[] index and directly copy bytes.
            Span<byte> target = Bytes.AsSpan(Length, 8);                // Span starts at Length, which is next empty index.
            BinaryPrimitives.WriteDoubleLittleEndian(target, value);    // We will use little-endian format, as is standard.
            Length += 8;

            return this;
        }

        public ArrayBuilder AddBool(bool value)
        {
            if (Length + 1 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Length will equal next empty index, so set element at Length then increment.
            Bytes[Length] = (value) ? (byte)1 : (byte)0;
            Length++;

            return this;
        }

        public ArrayBuilder AddString(string value)
        {
            // IMPORTANT: String argument should be verified length <= 127 before calling this method.
            //if (string.IsNullOrEmpty(value) || value.Length > 127)
            //{
            //    throw new ArgumentException("Argument string cannot be longer than 127 characters in length.");
            //}

            // Get byte count of string and create span of that length, used to write to the buffer without allocation.
            int stringByteCount = Encoding.UTF8.GetByteCount(value);
            Span<byte> target = Bytes.AsSpan(Length, stringByteCount + 1);  // Extra byte for length appended to data.

            // Use while loop here to allow multiple expansions if necessary.
            while (Length + stringByteCount + 1 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Use encoding class to directly copy message payload into span, then append string byte[] length.
            Encoding.UTF8.GetBytes(value, target);          // Will write entire span EXCEPT last byte.
            target[^1] = (byte)stringByteCount;             // Write directly to last byte in span.
            Length += target.Length;

            return this;
        }

        /// <summary>
        /// Finalizes the ArrayBuilder, returning the populated byte[] and its length as a tuple.
        ///  IMPORTANT: The 'length' out parameter must be used to read the data length; the size of the
        ///  byte[] does NOT represent the actual data in the array (it is a buffer).
        /// </summary>
        /// <returns> A tuple containing the generated byte[] of arbitrary length and the data length as an integer. </returns>
        public (byte[], int) Build()
        {
            return (Bytes, Length);
        }
    }
}
