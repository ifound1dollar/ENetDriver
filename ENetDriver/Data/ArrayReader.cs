using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace ENetDriver.Data
{
    /// <summary>
    /// A reader class which accepts a byte[] and its explicit length, allowing primitive data (and strings)
    ///  to be easily and efficiently read from the provided byte[]. Automatically handles internal logic
    ///  to read data without memory allocations. Will throw an IndexOutOfRangeException if a piece of data
    ///  is attempted to be read but does not exist (no bytes remaining for the requested data).
    /// </summary>
    public class ArrayReader
    {
        private byte[] Bytes { get; set; }
        private int Length { get; set; }

        /// <summary>
        /// Instantiates a new ArrayReader with the provided byte[] payload and its data length. An
        ///  explicit data length must be provided because the supplied byte[] may be of arbitrary
        ///  length, meaning the array.Length value will not accurately represent the length of the
        ///  data in bytes.
        /// </summary>
        /// <param name="bytes"> The byte[] containing payload data. May be of arbitrary size. </param>
        /// <param name="length"> The length of the data within the byte[]. </param>
        public ArrayReader(byte[] bytes, int length)
        {
            Bytes = bytes;
            Length = length;
        }



        // READER METHODS BELOW

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

        public short ReadShort()
        {
            // Move length index left 2 bytes, then directly return short from those bytes.
            Length -= 2;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read short - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 2);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadInt16LittleEndian(target);      // Read as little-endian, aligning with how it was written.
        }

        public ushort ReadUShort()
        {
            // Move length index left 2 bytes, then directly return ushort from those bytes.
            Length -= 2;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read ushort - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 2);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadUInt16LittleEndian(target);     // Read as little-endian, aligning with how it was written.
        }

        public int ReadInt()
        {
            // Move length index left 4 bytes, then directly return int from those bytes.
            Length -= 4;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read int - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 4);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadInt32LittleEndian(target);      // Read as little-endian, aligning with how it was written.
        }

        public uint ReadUInt()
        {
            // Move length index left 4 bytes, then directly return uint from those bytes.
            Length -= 4;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read uint - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 4);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadUInt32LittleEndian(target);     // Read as little-endian, aligning with how it was written.
        }

        public long ReadLong()
        {
            // Move length index left 8 bytes, then directly return long from those bytes.
            Length -= 8;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read long - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 8);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadInt64LittleEndian(target);      // Read as little-endian, aligning with how it was written.
        }

        public ulong ReadULong()
        {
            // Move length index left 8 bytes, then directly return ulong from those bytes.
            Length -= 8;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read ulong - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 8);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadUInt64LittleEndian(target);     // Read as little-endian, aligning with how it was written.
        }

        public float ReadFloat()
        {
            // Move length index left 4 bytes, then directly return float from those bytes.
            Length -= 4;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read float - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 4);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadSingleLittleEndian(target);     // Read as little-endian, aligning with how it was written.
        }

        public double ReadDouble()
        {
            // Move length index left 8 bytes, then directly return double from those bytes.
            Length -= 8;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read double - index out of range.");
            }

            // Use ReadOnlySpan insted of BitConverter to avoid endian-ness issues and potential array slice GC.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 8);        // We have already decremented length, so index is correct.
            return BinaryPrimitives.ReadDoubleLittleEndian(target);     // Read as little-endian, aligning with how it was written.
        }

        public bool ReadBool()
        {
            // Move length index left one byte, then directly return that byte.
            Length--;

            if (Length < 0)
            {
                throw new IndexOutOfRangeException("Cannot read byte - index out of range.");
            }

            // Use ReadOnlySpan instead of direct access to avoid allocation on large byte[] buffers.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, 1);    // Length has already been decremented so index is accurate.
            return (target[0] == 0x01);
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

            // Use ReadOnlySpan to allow native string slicing without allocation.
            ReadOnlySpan<byte> target = Bytes.AsSpan(Length, strLen);   // We have already decremented length, so index is correct.
            return Encoding.UTF8.GetString(target);                     // Read entire span because the length and data are correct.
        }
    }
}
