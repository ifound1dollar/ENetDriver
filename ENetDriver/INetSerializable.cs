using ENetDriver.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace ENetDriver
{
    /// <summary>
    /// This interface exposes a pair of methods that should be used to serialize and deserialize
    ///  objects of the implementing class to/from byte arrays. The implementing class should
    ///  override the NetSerialize() and NetDeserialize() methods to serialize all desired data
    ///  into a byte[], or to populate an empty instance with data from a byte[].
    /// </summary>
    public interface INetSerializable
    {
        /// <summary>
        /// Serializes the object into a byte[] containing all relevant data, returning the array
        ///  and its associated length as an integer. NOTE: The returned byte[] will often be larger
        ///  than the data contained in the array; the returned integer represents the actual data
        ///  length (do not use array.Length).
        /// </summary>
        /// <returns> A byte[] containing all serialized object data, and a length integer representing the data length. </returns>
        public (byte[], int) NetSerialize();

        /// <summary>
        /// Deserializes the object from a byte[] and its data length, setting all relevant data
        ///  in the object. This method should be called immediately after an empty object is
        ///  constructed, and requires that all relevant fields can be set by this method.
        /// </summary>
        /// <param name="bytes"> The raw byte[] containing data to deserialize the object from. </param>
        /// <param name="length"> The length of the data in the byte[]. </param>
        public void NetDeserialize(byte[] bytes, int length);
    }
}
