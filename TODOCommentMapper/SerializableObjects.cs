using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO.Compression;

namespace NewMind.Tourism.DataServices.Base.Misc
{
	/// <summary>
	/// Class to implement object comparisons where we want objects of the same type containing
	/// all the same data to be reported as being Equal
	/// </summary>
	public static class SerializableObjects
	{
		// ========================================================================================
		// CLONING
		// ========================================================================================
		/// <summary>
		/// Clone a serialize class instance (will throw an exception is object is not Serializable)
		/// </summary>
		public static object Clone(object obj)
		{
			if (obj == null)
				return null;
			IFormatter formatter = new BinaryFormatter();
			Stream stream = new MemoryStream();
			object objOut;
			using (stream)
			{
				formatter.Serialize(stream, obj);
				stream.Seek(0, SeekOrigin.Begin);
				objOut = formatter.Deserialize(stream);
				stream.Close();
			}
			return objOut;
		}

		// ========================================================================================
		// INSTANCE COMPARISONS
		// ========================================================================================
		/// <summary>
		/// Object must be of the same type and describe all the same data. The objects must be
		/// serialisable, as this is how the comparison is made. If byte arrays are passed in,
		/// their contents will be compared directly (without serialisation).
		/// </summary>
		public static bool Compare(object obj1, object obj2)
		{
			// If objects are references to the same object (or both null), they must match
			if (obj1 == obj2)
				return true;
			// If one object is null, but not the other (following above), they don't match
			if ((obj1 == null) || (obj2 == null))
				return false;
			// If object types don't match, they aren't the same (see method comment)
			if (obj1.GetType() != obj2.GetType())
				return false;
			// If objects are byte arrays, compare their contents
			if (obj1.GetType() == typeof(byte[]))
				return doByteArraysMatch((byte[])obj1, (byte[])obj2);
			// Otherwise, serialise to bytes and compare those arrays
			return doByteArraysMatch(getByteRepresentation(obj1), getByteRepresentation(obj2));
		}

		/// <summary>
		/// Provide a GetHashCode method that is consistent with the Compare method
		/// </summary>
		public static int GetHashCodeFor(object obj)
		{
			// Strings that match will get the same hash code value - so for now let's just
			// mash all the byte value up together and compare the resulting strings
			if (obj == null)
				throw new NullReferenceException();

			// Get byte data - if already a byte array, just use that direct, otherwise
			// serialise and use result byte array
			byte[] data;
			if (obj.GetType() == typeof(byte[]))
				data = (byte[])obj;
			else
				data = getByteRepresentation(obj);

			// Mash array contents together into string along with type name, and use the
			// hashcode of the resulting string (this is to be consistent with the Compare
			// method, and that requires that objects be the same type)
			string content = obj.GetType().ToString();
			foreach (byte value in data)
				content += (content == "" ? "" : ",") + value.ToString();
			content = obj.ToString() + ":" + content;
			return content.GetHashCode();
		}

		/// <summary>
		/// Get a byte array representing a serialisable object's serialised data - can be
		/// used for comparisons.
		/// </summary>
		private static byte[] getByteRepresentation(object obj)
		{
			if (obj == null)
				return null;
			byte[] buffer;
			using (Stream stream = new MemoryStream())
			{
				IFormatter formatter = new BinaryFormatter();
				formatter.Serialize(stream, obj);
				stream.Seek(0, SeekOrigin.Begin);
				buffer = readBytesFromStream(stream);
				stream.Close();
			}
			return buffer;
		}

		/// <summary>
		/// Do two byte arrays match - they must both be null, or both have the same number of
		/// elements, with each element pairs matching. If both are null, true is returned. If
		/// only one is null, false is returned.
		/// </summary>
		private static bool doByteArraysMatch(byte[] arr1, byte[] arr2)
		{
			if ((arr1 == null) && (arr2 == null))
				return true;
			if ((arr1 == null) || (arr2 == null))
				return false;
			if (arr1.Length != arr2.Length)
				return false;
			for (int index = 0; index < arr1.Length; index++)
				if (arr1[index] != arr2[index])
					return false;
			return true;
		}

		private static byte[] readBytesFromStream(Stream stream)
		{
			byte[] buffer = new byte[4096];
			int read = 0;
			int chunk;
			while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
			{
				read += chunk;
				if (read == buffer.Length)
				{
					int nextByte = stream.ReadByte();
					if (nextByte == -1)
						return buffer;
					byte[] newBuffer = new byte[buffer.Length * 2];
					Array.Copy(buffer, newBuffer, buffer.Length);
					newBuffer[read] = (byte)nextByte;
					buffer = newBuffer;
					read++;
				}
			}
			byte[] ret = new byte[read];
			Array.Copy(buffer, ret, read);
			return ret;
		}

		// ========================================================================================
		// OBJECT DATA COMPRESSION
		// ========================================================================================
		public class TransportWrapping
		{
			public static byte[] ToByteArray(object obj) { return ToByteArray(obj, false); }
			public static byte[] ToByteArray(object obj, bool compressData)
			{
				if (obj == null)
					return null;
				byte[] data = getByteRepresentation(obj);
				if (compressData)
					data = Compression.Compress(data);
				return data;
			}

			public static object FromByteArray(byte[] data) { return FromByteArray(data, false); }
			public static object FromByteArray(byte[] data, bool compressedData)
			{
				if (data == null)
					return null;
				if (compressedData)
					data = Compression.Decompress(data);
				using (MemoryStream inputDataStream = new MemoryStream(data))
				{
					BinaryFormatter formatter = new BinaryFormatter();
					return formatter.Deserialize(inputDataStream);
				}
			}
		}

		public class Compression
		{
			public static byte[] Compress(byte[] data)
			{
				if (data == null)
					return null;
				using (MemoryStream compressedStream = new MemoryStream())
				{
					using (GZipStream compressingStream = new GZipStream(compressedStream, CompressionMode.Compress))
					{
						compressingStream.Write(data, 0, data.Length);
					}
					return compressedStream.ToArray();
				}
			}

			public static byte[] Decompress(byte[] data)
			{
				if (data == null)
					return null;
				using (MemoryStream inputDataStream = new MemoryStream(data))
				{
					using (GZipStream decompressingStream = new GZipStream(inputDataStream, CompressionMode.Decompress))
					{
						return readBytesFromStream(decompressingStream);
					}
				}
			}
		}

		// ========================================================================================
		// DISK READING / WRITING
		// ========================================================================================
		/// <summary>
		/// Attempt to write object data to disk - if there are any errors (eg. file permissions, 
		/// invalid filename), an exception will be thrown
		/// </summary>
		public static void WriteToDisk(string filename, object obj)
		{
			using (Stream stream = File.Open(filename, FileMode.Create))
			{
				BinaryFormatter formatter = new BinaryFormatter();
				formatter.Serialize(stream, obj);
				stream.Close();
			}
		}

		/// <summary>
		/// Attempt to write a byte array data to disk - if there are any errors (eg. file permissions, 
		/// invalid filename), an exception will be thrown
		/// </summary>
		public static void WriteToDiskRaw(string filename, byte[] data)
		{
			if (data == null)
				throw new ArgumentException("SerializableObjects.WriteToDiskRaw: Null data provided");
			using (Stream stream = File.Open(filename, FileMode.Create))
			{
				stream.Write(data, 0, data.Length);
			}
		}

		/// <summary>
		/// Attempt to write object data to disk as xml - if there are any errors (eg. file permissions,
		/// invalid filename), an exception will be thrown
		/// </summary>
		public static void WriteToDiskXml(string filename, object obj)
		{
			if (obj == null)
				return;
			using (Stream stream = File.Open(filename, FileMode.Create))
			{
				writeAsXml(obj, stream);
			}
		}

		private static void writeAsXml(object obj, Stream stream)
		{
			if (obj == null)
				throw new ArgumentNullException("obj");
			if (stream == null)
				throw new ArgumentNullException("stream");
			XmlSerializer serializer = new XmlSerializer(obj.GetType());
			serializer.Serialize(stream, obj);
			return;
		}

		/// <summary>
		/// Attempt to read object data from disk - if there are any errors (eg. file permissions, 
		/// invalid filename), an exception will be thrown - null should never be returned
		/// </summary>
		public static object ReadFromDisk(string filename)
		{
			if (!File.Exists(filename))
				throw new FileNotFoundException("SerializableObjects.ReadFromDisk: File not found: " + filename);
			object obj;
			using (Stream stream = File.Open(filename, FileMode.Open))
			{
				BinaryFormatter formatter = new BinaryFormatter();
				obj = formatter.Deserialize(stream);
				stream.Close();
			}
			return obj;
		}

		/// <summary>
		/// Try to read raw binary data from a file - if there are any errors (eg. file permissions, 
		/// invalid filename, file not found), an exception will be thrown - null should never be
		/// returned
		/// </summary>
		public static byte[] ReadFromDiskRaw(string filename)
		{
			if (!File.Exists(filename))
				throw new FileNotFoundException("SerializableObjects.ReadFromDiskRaw: File not found: " + filename);
			using (Stream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return readBytesFromStream(stream);
			}
		}

		/// <summary>
		/// Attempt to read xml object data from disk - if there are any errors (eg. file permissions, 
		/// invalid filename), an exception will be thrown - null should never be returned
		/// </summary>
		public static object ReadFromDiskXml(string filename, Type type)
		{
			if (!File.Exists(filename))
				throw new FileNotFoundException("SerializableObjects.ReadFromDiskRaw: File not found: " + filename);
			object obj;
			using (Stream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (XmlReader reader = XmlReader.Create(stream))
				{
					XmlSerializer serializer = new XmlSerializer(type);
					obj = serializer.Deserialize(reader);
				}
				stream.Close();
			}
			return obj;
		}
	}
}
