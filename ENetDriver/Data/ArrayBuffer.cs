using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENet_Driver.Data
{
    /// <summary>
    /// The ArrayBuffer class exists to allow efficient and robust writing to and reading from a
    ///  byte array. All primitive data types (plus string) can be dynamically written and read from
    ///  an ArrayBuffer quickly and efficiently. The ArrayBuffer can be instantiated empty with default
    ///  capacity (1024 bytes) or with a specified capacity (used for writing data), or can be
    ///  instantiated from an existing populated byte[] and its associated length (for reading data).
    /// As data is written to and read from the ArrayBuffer, the Length field will be updated
    ///  according to the size of the type of data (ex. 4 bytes for an int). Writing and reading data
    ///  is destructive to Length. Note that if the initial capacity of the byte[] is not large enough
    ///  to hold new data, it will automatically expand. Additionally, if data is attempted to be read
    ///  but no data exists (Length goes negative), an IndexOutOfRangeException will be thrown.
    /// IMPORTANT: The Length field must be used to get the length of the data within the ArrayBuffer;
    ///  the Bytes array size will often not match the actual length of its data. 
    /// </summary>
    public class ArrayBuffer
    {
        public byte[] Bytes { get; private set; }
        public int Length { get; private set; }

        /// <summary>
        /// Instantiates a new EMPTY ArrayBuffer object with a default capacity of 1024. This will likely
        ///  be far larger than necessary, and is relatively wasteful and thus the overloaded constructor
        ///  with a capacity argument should be used when possible.
        /// </summary>
        public ArrayBuffer()
        {
            Bytes = new byte[1024];
            Length = 0;
        }

        /// <summary>
        /// Instantiates a new EMPTY ArrayBuffer object with the specified capacity. Capacity will
        ///  automatically expand if data is added that exceeds this initial capacity (but this is
        ///  relatively expensive and should be avoided when possible).
        /// </summary>
        /// <param name="capacity"> The capacity (length) of the ArrayBuffer's byte[]. </param>
        public ArrayBuffer(int capacity)
        {
            Bytes = new byte[capacity];
            Length = 0;
        }

        /// <summary>
        /// Instantiates a new POPULATED ArrayBuffer object from an existing byte[] and length. This
        ///  object should be read from in the exact reverse order from when it was originally created.
        /// For example, if created with [byte + int + string], it should be read in the order [string
        ///  + int + byte].
        /// </summary>
        /// <param name="bytes"> The existing byte[] populated with data (array size may not match length argument). </param>
        /// <param name="length"> The actual length of data within the byte[]. </param>
        public ArrayBuffer(byte[] bytes, int length)
        {
            Bytes = bytes;
            Length = length;
        }



        private ArrayBuffer FromCapacity(int capacity)
        {
            Bytes = new byte[capacity];
            Length = 0;
            return this;
        }

        private ArrayBuffer FromBytes(byte[] bytes, int length)
        {
            Bytes = bytes;
            Length = length;
            return this;
        }



        private void ExpandCapacity()
        {
            // Initialize array with 2x current capacity, then copy current contents over and replace reference.
            byte[] expanded = new byte[Bytes.Length * 2];
            Buffer.BlockCopy(Bytes, 0, expanded, 0, Length + 1);
            Bytes = expanded;
        }



        #region Byte

        public ArrayBuffer AddByte(byte value)
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

        public byte ReadByte()
        {
            // Move length index left one byte, then directly return that byte.
            Length--;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read byte - index out of range.");
            }

            return Bytes[Length];
        }

        #endregion

        #region Short

        public ArrayBuffer AddShort(short value)
        {
            if (Length + 2 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert short to array of size 2, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 2);
            Length += 2;

            return this;
        }

        public short ReadShort()
        {
            // Move length index left 2 bytes, then directly return short from those bytes.
            Length -= 2;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read short - index out of range.");
            }

            return BitConverter.ToInt16(Bytes, Length);
        }

        #endregion

        #region UShort

        public ArrayBuffer AddUShort(ushort value)
        {
            if (Length + 2 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert ushort to array of size 2, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 2);
            Length += 2;

            return this;
        }

        public ushort ReadUShort()
        {
            // Move length index left 2 bytes, then directly return ushort from those bytes.
            Length -= 2;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read ushort - index out of range.");
            }

            return BitConverter.ToUInt16(Bytes, Length);
        }

        #endregion

        #region Int

        public ArrayBuffer AddInt(int value)
        {
            if (Length + 4 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert int to array of size 4, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 4);
            Length += 4;

            return this;
        }

        public int ReadInt()
        {
            // Move length index left 4 bytes, then directly return int from those bytes.
            Length -= 4;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read int - index out of range.");
            }

            return BitConverter.ToInt32(Bytes, Length);
        }

        #endregion

        #region UInt

        public ArrayBuffer AddUInt(uint value)
        {
            // Below code block is a test implementation of unsafe memory copying.
            //unsafe
            //{
            //    // Passed-in 'value' object is allocated on the stack and thus the & operator can
            //    //  be used outside a fixed() block.
            //    void* source = &value;

            //    // Get pointer to next empty element in Bytes using fixed() block, which temporarily
            //    //  pins the address of the array while execution remains inside the block.
            //    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/pointer-related-operators
            //    fixed (void* dest = &Bytes[Length])
            //    {
            //        // Copy bytes at passed-in uint memory address into Bytes at the Length address.
            //        Buffer.MemoryCopy(source, dest, 4, 4);
            //    }
            //}

            if (Length + 4 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert uint to array of size 4, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 4);
            Length += 4;

            return this;
        }

        public uint ReadUInt()
        {
            // Move length index left 4 bytes, then directly return uint from those bytes.
            Length -= 4;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read uint - index out of range.");
            }

            return BitConverter.ToUInt32(Bytes, Length);
        }

        #endregion

        #region Long

        public ArrayBuffer AddLong(long value)
        {
            if (Length + 8 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert long to array of size 8, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 8);
            Length += 8;

            return this;
        }

        public long ReadLong()
        {
            // Move length index left 8 bytes, then directly return long from those bytes.
            Length -= 8;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read long - index out of range.");
            }

            return BitConverter.ToInt64(Bytes, Length);
        }

        #endregion

        #region ULong

        public ArrayBuffer AddULong(ulong value)
        {
            if (Length + 8 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert ulong to array of size 8, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 8);
            Length += 8;

            return this;
        }

        public ulong ReadULong()
        {
            // Move length index left 8 bytes, then directly return ulong from those bytes.
            Length -= 8;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read ulong - index out of range.");
            }

            return BitConverter.ToUInt64(Bytes, Length);
        }

        #endregion

        #region Float

        public ArrayBuffer AddFloat(float value)
        {
            if (Length + 4 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert float to array of size 4, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 4);
            Length += 4;

            return this;
        }

        public float ReadFloat()
        {
            // Move length index left 4 bytes, then directly return float from those bytes.
            Length -= 4;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read float - index out of range.");
            }

            return BitConverter.ToSingle(Bytes, Length);
        }

        #endregion

        #region Double

        public ArrayBuffer AddDouble(double value)
        {
            if (Length + 8 > Bytes.Length)
            {
                ExpandCapacity();
            }

            // Convert double to array of size 8, then copy directly into main byte[].
            byte[] temp = BitConverter.GetBytes(value);
            Buffer.BlockCopy(temp, 0, Bytes, Length, 8);
            Length += 8;

            return this;
        }

        public double ReadDouble()
        {
            // Move length index left 8 bytes, then directly return double from those bytes.
            Length -= 8;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read double - index out of range.");
            }

            return BitConverter.ToDouble(Bytes, Length);
        }

        #endregion

        #region Bool

        public ArrayBuffer AddBool(bool value)
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

        public bool ReadBool()
        {
            // Move length index left one byte, then directly return that byte.
            Length--;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read byte - index out of range.");
            }

            return (Bytes[Length] == 1);
        }

        #endregion

        #region String

        public ArrayBuffer AddString(string value)
        {
            // IMPORTANT: String argument should be verified length <= 127 before calling this method.
            //if (string.IsNullOrEmpty(value) || value.Length > 127)
            //{
            //    throw new ArgumentException("Argument string cannot be longer than 127 characters in length.");
            //}

            // Convert string into dynamic-sized byte[], then copy directly into main byte[].
            byte[] temp = Encoding.UTF8.GetBytes(value);

            if (Length + temp.Length > Bytes.Length)
            {
                ExpandCapacity();
            }

            Buffer.BlockCopy(temp, 0, Bytes, Length, temp.Length);
            Length += temp.Length;

            // Add byte for string length immediately after string.
            AddByte((byte)temp.Length);

            return this;
        }

        public string ReadString()
        {
            // Read length of bytes used by the string from the byte immediately after string.
            byte strLen = ReadByte();

            // Move length index left by strLen bytes, then directly return string from those bytes.
            Length -= strLen;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read string - index out of range.");
            }

            return Encoding.UTF8.GetString(Bytes, Length, strLen);
        }

        #endregion



        /// <summary>
        /// THREAD SAFE pool of one ArrayBuffer object. This will return a static
        ///  instance of an ArrayPool.
        /// </summary>
        public static class Pool
        {
            [ThreadStatic]
            private static ArrayBuffer? arrayBuffer;

            public static ArrayBuffer GetEmpty(int capacity)
            {
                arrayBuffer ??= new(1);

                // If already exists, reset data and length and set capacity.
                return arrayBuffer.FromCapacity(capacity);
            }

            public static ArrayBuffer GetFromBytes(byte[] bytes, int length)
            {
                arrayBuffer ??= new(1);

                // If already exists, set data and length to passed-in values.
                return arrayBuffer.FromBytes(bytes, length);
            }
        }
    }
}
