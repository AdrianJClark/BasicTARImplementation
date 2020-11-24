// Basic TAR Archive Implementation
// -------------------------------
// Adrian Clark
// adrian.clark@canterbury.ac.nz
// -------------------------------
// First Release Nov 2020

using System.Collections.Generic;

using System;
using System.Text;
using System.IO;
using System.IO.Compression;

// This class encapsulated basic TAR Archive functionality in
// an individual file. It is not a complete implementation.
//
// This file is based on the TAR archive file format as defined at
// https://en.wikipedia.org/wiki/Tar_(computing)
public class TAR
{
    // This class represents the Meta Data present in a TAR archive for
    // an individual file. It is not a complete implementation.
    public class TARMetaData
    {
        //Pre-Posix TAR specification fields
        public string fileName;
        public string fileMode;
        public string ownerID;
        public string groupID;
        public long fileSize;
        public long lastModifiedTime;
        public long checkSum;
        public string typeFlag;
        public string linkName;

        //UStar TAR specification fields
        public string ustarIndicator;
        public string ustarVersion;
        public string ownerUserName;
        public string ownerGroupName;
        public string deviceMajorNumber;
        public string deviceMinorNumber;
        public string filenamePrefix;

        //The position where the Meta Data and file sit in the buffer
        public long MetaDataBufferPositionStart;
        public long FileBufferPositionStart;

        //The various type flags - this is not a complete list
        //Some type flags are alphanumeric, so an enum may not be the best
        public enum TypeFlags
        {
            Unknown = -1,
            NormalFile = 0,
            HardLink = 1,
            SymbolicLink = 2,
            CharacterSpecial = 3,
            BlockSpecial = 4,
            Directory = 5,
            FIFO = 6,
            ContiguousFile = 7,
            GlobalExtendedHeaderWithMetaData = 103,
            ExtendedHeaderWithMetaDataForNextFileInArchive = 120,
            VendorSpecificExtensions = 65
        };

        // Returns true if the ustar indicator is set
        public bool isUStar
        {
            get
            {
                return ustarIndicator.Equals("ustar", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        // Returns the matching TypeFlag enum value
        public TypeFlags GetTypeFlags()
        {
            if (string.IsNullOrEmpty(typeFlag) || typeFlag.Trim().Length == 0)
                return TypeFlags.Unknown;

            // 0-7 map directly to entries in the TypeFlags enum
            int parsedTypeFlag = -1;
            if (int.TryParse(typeFlag, out parsedTypeFlag))
                return (TypeFlags)parsedTypeFlag;

            // Otherwise try map the binary ascii files
            int iTypeFlag = typeFlag[0];
            if (Enum.IsDefined(typeof(TypeFlags), iTypeFlag))
                return (TypeFlags)iTypeFlag;

            // Otherwise if the tag is between A and Z, return vendor specific
            if (iTypeFlag >= 'A' && iTypeFlag <= 'Z')
                return TypeFlags.VendorSpecificExtensions;

            // If all else fails, return unknown
            return TypeFlags.Unknown;
        }

        // Helper function to get a trimmed string from a byte array, and update
        // the bufferposition accordingly
        string ExtractStringFromByteArray(byte[] buffer, int length, ref int bufferPos)
        {
            string s = Encoding.ASCII.GetString(buffer, bufferPos, length).Trim().Trim('\0');
            bufferPos += length;
            return s;
        }

        // Returns the checksum of the Metadata
        public long CalculateCheckSum(byte[] buffer)
        {
            long checksum = 0;
            for (int i = 0; i < buffer.Length; i++)
                checksum += buffer[i];

            return checksum;
        }

        // Writes a string of a maximum length into the byte array
        // at a certain position
        int WriteStringToByteArray(string s, ref byte[] buffer, int maxLength, int bufferPos)
        {
            Encoding.ASCII.GetBytes(s, 0, s.Length < maxLength ? s.Length : maxLength, buffer, bufferPos);
            return maxLength;
        }

        // Create a byte array for the Meta Data, optionally recalculating the
        // Checksum
        public byte[] ToByteArray(bool recalculateChecksum = false)
        {
            byte[] buffer = new byte[512];

            int bufferPos = 0;

            // If the filename is null it is an invalid entry
            if (!String.IsNullOrWhiteSpace(fileName))
            {
                // Pre-Posix Fields
                bufferPos += WriteStringToByteArray(fileName, ref buffer, 100, bufferPos);
                bufferPos += WriteStringToByteArray(fileMode, ref buffer, 8, bufferPos);
                bufferPos += WriteStringToByteArray(ownerID, ref buffer, 8, bufferPos);
                bufferPos += WriteStringToByteArray(groupID, ref buffer, 8, bufferPos);

                // Pad the file size with 0 to a string length of 11
                string sFileSize = Convert.ToString(fileSize, 8).PadLeft(11, '0');
                bufferPos += WriteStringToByteArray(sFileSize + " ", ref buffer, 12, bufferPos);

                string sLastModifiedTime = Convert.ToString(lastModifiedTime, 8);
                bufferPos += WriteStringToByteArray(sLastModifiedTime + " ", ref buffer, 12, bufferPos);

                // If we're recalculating the checksum, set the existing checksum
                // To spaces as per the TAR file format
                string sCheckSum;
                if (recalculateChecksum)
                    sCheckSum = "        ";
                else
                    //Otherwise pad with 0 to a string length of 6
                    sCheckSum = Convert.ToString(checkSum, 8).PadLeft(6, '0');
                bufferPos += WriteStringToByteArray(sCheckSum + "\0 ", ref buffer, 8, bufferPos);

                bufferPos += WriteStringToByteArray(typeFlag, ref buffer, 1, bufferPos);
                bufferPos += WriteStringToByteArray(linkName, ref buffer, 100, bufferPos);

                // UStar Fields
                bufferPos += WriteStringToByteArray(ustarIndicator, ref buffer, 6, bufferPos);
                bufferPos += WriteStringToByteArray(ustarVersion, ref buffer, 2, bufferPos);
                bufferPos += WriteStringToByteArray(ownerUserName, ref buffer, 32, bufferPos);
                bufferPos += WriteStringToByteArray(ownerGroupName, ref buffer, 32, bufferPos);
                bufferPos += WriteStringToByteArray(deviceMajorNumber, ref buffer, 8, bufferPos);
                bufferPos += WriteStringToByteArray(deviceMinorNumber, ref buffer, 8, bufferPos);
                bufferPos += WriteStringToByteArray(filenamePrefix, ref buffer, 155, bufferPos);

                // If we're recalculating the checksum, do that now and then
                // insert the value at position 148
                if (recalculateChecksum)
                {
                    sCheckSum = Convert.ToString(CalculateCheckSum(buffer), 8).PadLeft(6, '0');
                    WriteStringToByteArray(sCheckSum + "\0 ", ref buffer, 8, 148);
                }
            }

            return buffer;
        }

        // Constructor which populates fields based a byte buffer
        public TARMetaData(byte[] buffer)
        {
            int bufferPos = 0;

            fileName = ExtractStringFromByteArray(buffer, 100, ref bufferPos);
            // If the filename is null it is an invalid entry
            if (!String.IsNullOrWhiteSpace(fileName))
            {
                // Pre-Posix Fields
                fileMode = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
                ownerID = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
                groupID = ExtractStringFromByteArray(buffer, 8, ref bufferPos);

                // Filesize is stored in Octets
                string sFileSize = ExtractStringFromByteArray(buffer, 12, ref bufferPos);
                fileSize = Convert.ToInt64(sFileSize.Trim(), 8);

                // Last Modified Time is stored in Octets
                string sLastModifiedTime = ExtractStringFromByteArray(buffer, 12, ref bufferPos);
                lastModifiedTime = Convert.ToInt64(sLastModifiedTime.Trim(), 8);

                // Checksum is stored in Octets
                string sCheckSum = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
                checkSum = Convert.ToInt64(sCheckSum.Trim(), 8);

                typeFlag = ExtractStringFromByteArray(buffer, 1, ref bufferPos);

                linkName = ExtractStringFromByteArray(buffer, 100, ref bufferPos);

                // UStar Fields
                ustarIndicator = ExtractStringFromByteArray(buffer, 6, ref bufferPos);
                ustarVersion = ExtractStringFromByteArray(buffer, 2, ref bufferPos);
                ownerUserName = ExtractStringFromByteArray(buffer, 32, ref bufferPos);
                ownerGroupName = ExtractStringFromByteArray(buffer, 32, ref bufferPos);
                deviceMajorNumber = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
                deviceMinorNumber = ExtractStringFromByteArray(buffer, 8, ref bufferPos);
                filenamePrefix = ExtractStringFromByteArray(buffer, 155, ref bufferPos);
            }
        }

        // Constructor which manually populates fields from values
        public TARMetaData(
            string fileName, string fileMode = "000000", string ownerID = "000000", string groupID = "000000",
            long fileSize = 0, long lastModifiedTime = 0, string typeFlag = "0", string linkName = "", bool isUStar = true,
            string ownerUserName = "", string ownerGroupName = "", string deviceMajorNumber = "000000", string deviceMinorNumber = "000000", string filenamePrefix = "")
        {
            this.fileName = fileName;
            this.fileMode = fileMode;
            this.ownerID = ownerID;
            this.groupID = groupID;

            this.fileSize = fileSize;
            this.lastModifiedTime = lastModifiedTime;
            this.typeFlag = typeFlag;
            this.linkName = linkName;

            // If it is a UStar TAR Archive, populate the Indicator and Version
            if (isUStar)
            {
                ustarIndicator = "ustar";
                ustarVersion = "00";
            }

            this.ownerUserName = ownerUserName;
            this.ownerGroupName = ownerGroupName;
            this.deviceMajorNumber = deviceMajorNumber;
            this.deviceMinorNumber = deviceMinorNumber;
            this.filenamePrefix = filenamePrefix;
        }

        // Helper function to create default Meta Data for a File
        public static TARMetaData CreateDefaultFileMetaData(string fileName, long fileSize = 0, long lastModifiedTime = 0, string ownerUserName = "", string ownerGroupName = "")
        {
            return new TARMetaData(fileName, "000644", "000765", "000024", fileSize, lastModifiedTime, "0", "", true, ownerUserName, ownerGroupName);
        }

        // Helper function to create default Meta Data for a Directory
        public static TARMetaData CreateDefaultDirectoryMetaData(string directoryName, long lastModifiedTime = 0, string ownerUserName = "", string ownerGroupName = "")
        {
            return new TARMetaData(directoryName, "000755", "000765", "000024", 0, lastModifiedTime, "5", "", true, ownerUserName, ownerGroupName);
        }

        // Print out the various fields for the Meta Data
        public override string ToString()
        {
            if (!String.IsNullOrWhiteSpace(fileName))
                return
                    "File Name: " + fileName + "\n" +
                    "File Mode: " + fileMode + "\n" +
                    "Owner ID: " + ownerID + "\n" +
                    "Groud ID: " + groupID + "\n" +
                    "File Size (bytes): " + fileSize + "\n" +
                    "Last Modified Time: " + DateTimeOffset.FromUnixTimeSeconds(lastModifiedTime).ToString() + "\n" +
                    "Checksum: " + checkSum + "\n" +
                    "Type Flag: " + typeFlag + " (" + GetTypeFlags() + ")\n" +
                    "UStar Indicator: " + ustarIndicator + "\n" +
                    "UStar Version: " + ustarVersion + "\n" +
                    "Owner User Name: " + ownerUserName + "\n" +
                    "Owner Group Name: " + ownerGroupName + "\n" +
                    "Device Major Number: " + deviceMajorNumber + "\n" +
                    "Device Minor Number: " + deviceMinorNumber + "\n" +
                    "Filename Prefix: " + filenamePrefix;
            else
                return null;
        }
    }

    // Stores a list of all entries in the TAR file
    public List<TARMetaData> Entries;

    // Stores the filename for the TAR file
    public string Filename;

    // Are we dealing with a compressed file
    private bool isCompressed;

    // Private Constructor
    public TAR(string filename, bool isCompressed)
    {
        Entries = new List<TARMetaData>();
        this.Filename = filename;
        this.isCompressed = isCompressed;

        //If Compressed, use a GZip stream to decompress
        using (var stream = File.OpenRead(filename))
            if (isCompressed)
                using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
                    LoadFromStream(gzip);
            else
                LoadFromStream(stream);
    }

    // Public Static Method to load a compressed TGZ file
    public static TAR LoadTGZFile(string filename)
    {
        TAR archive = new TAR(filename, true);
        return archive;
    }

    // Public Static Method to load an uncompressed TAR file
    public static TAR LoadTARFile(string filename)
    {
        TAR archive = new TAR(filename, false);
        return archive;
    }

    // Load a TAR archive from a Stream
    private void LoadFromStream(Stream stream)
    {
        // TAR Entry MetaData is stored in 512 byte buffers
        var metaDataBuffer = new byte[512];
        int readCount = 0;
        long bufferPosition = 0;

        do
        {
            // Read 512 bytes
            readCount = stream.Read(metaDataBuffer, 0, 512);

            // If we were successful
            if (readCount >= 512)
            {
                // Unpack the MetaData and update the buffer positions
                TARMetaData fileMetaData = new TARMetaData(metaDataBuffer);
                fileMetaData.MetaDataBufferPositionStart = bufferPosition;
                bufferPosition += readCount;
                fileMetaData.FileBufferPositionStart = bufferPosition;

                // If the entry has file data following it
                if (fileMetaData.GetTypeFlags() == TARMetaData.TypeFlags.NormalFile ||
                    fileMetaData.GetTypeFlags() == TARMetaData.TypeFlags.ExtendedHeaderWithMetaDataForNextFileInArchive)
                {
                    // Seek past the data
                    StreamSeek(stream, fileMetaData.fileSize);
                    bufferPosition += fileMetaData.fileSize;

                    // Make sure we seek past by a multiple of 512
                    if (fileMetaData.fileSize % 512 > 0)
                    {
                        long offset = 512 - (fileMetaData.fileSize % 512);
                        StreamSeek(stream, offset);
                        bufferPosition += offset;

                    }
                }

                // Store the meta data 
                Entries.Add(fileMetaData);
            }

            //Loop until we've run out of entries
        } while (readCount >= 512);
    }

    // Extract a file from our archive using the entry name
    public void ExtractFile(string entryName, string output)
    {
        // Look up the entry based on filename
        TARMetaData entry = Entries.Find(e => e.fileName == entryName);

        //If we found one, extract it
        if (entry != null)
            ExtractFile(entry, output);
    }

    // Extract a file from our archive
    public void ExtractFile(TARMetaData entry, string output)
    {
        // Check we're extracting a normal file
        if (entry.GetTypeFlags() == TARMetaData.TypeFlags.NormalFile)
        {
            //Create any directories as required
            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            {
                // Open relevant streams and extract
                using (var stream = File.OpenRead(Filename))
                    if (isCompressed)
                        using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
                            ExtractFile(entry, gzip, output);
                    else
                        ExtractFile(entry, stream, output);
            }
        }
    }

    // Extract a file from a stream
    private void ExtractFile(TARMetaData entry, Stream stream, string output)
    {
        // Open the output
        using (var str = File.Open(output, FileMode.OpenOrCreate, FileAccess.Write))
        {
            // Create a buffer to hold the file
            var buf = new byte[entry.fileSize];
            // Seek to the relevant place in the stream
            StreamSeek(stream, entry.FileBufferPositionStart);
            // Read from the stream, write to the file
            stream.Read(buf, 0, buf.Length);
            str.Write(buf, 0, buf.Length);
        }
    }

    // Add a file to an the archive (and save as output)
    // We cannot do in place overwrites, as we read in the original file
    // And then write it out
    public void AddFile(string filePath, string output, bool outputCompressed)
    {
        //Create any directories as required
        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        {
            // Open relevant streams and add file as necessary
            // Code is a bit messy, but essentially we have four conditions
            // where the input stream can be compressed or not, and the output
            // stream can be compressed or not
            using (var streamIn = File.OpenRead(Filename))
            {
                using (var streamOut = File.Open(output, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    if (isCompressed)
                    {
                        using (var gzipIn = new GZipStream(streamIn, CompressionMode.Decompress))
                        {
                            if (outputCompressed)
                                using (var gzipOut = new GZipStream(streamOut, CompressionMode.Compress))
                                    AddFile(filePath, gzipIn, gzipOut);
                            else
                                AddFile(filePath, gzipIn, streamOut);
                        }
                    }
                    else
                    {
                        if (outputCompressed)
                            using (var gzipOut = new GZipStream(streamOut, CompressionMode.Compress))
                                AddFile(filePath, streamIn, gzipOut);
                        else
                            AddFile(filePath, streamIn, streamOut);
                    }
                }
            }
        }
    }

    // Add a file to our archive using streams
    // For simplicity, we always add to the start of the archive
    private void AddFile(string filePath, Stream streamIn, Stream streamOut)
    {
        // For the user name and user group, we use Environment.UserName
        // I haven't found a simple, platform independent way of getting the
        // User Group correctly
        string userName = Environment.UserName;
        string userGroup = Environment.UserName;

        // Read all the file data to add
        byte[] fileData = File.ReadAllBytes(filePath);

        // Use the File's Last Write time as the file's time in the archive
        long currentTime = new DateTimeOffset(File.GetLastWriteTime(filePath)).ToUnixTimeSeconds();

        // Create the metadata structure
        TARMetaData fileMD = TARMetaData.CreateDefaultFileMetaData(Path.GetFileName(filePath), fileData.Length, currentTime, userName, userGroup);

        // Copy the metadata to a byte array and write it
        byte[] bAssetMD = fileMD.ToByteArray(true);
        streamOut.Write(bAssetMD, 0, bAssetMD.Length);

        // Then write out the file following this
        streamOut.Write(fileData, 0, fileData.Length);

        // Pad after the file data until we reach a 512 byte boundary if needed
        if (fileData.Length % 512 > 0)
        {
            int paddingBytes = 512 - (fileData.Length % 512);
            byte[] bPaddingBytes = new byte[paddingBytes];
            streamOut.Write(bPaddingBytes, 0, paddingBytes);
        }

        // Then loop through each of the original entries
        foreach (TARMetaData entry in Entries)
        {
            //Read the original meta data from the input and write to output
            var metadata = new byte[512];
            streamIn.Read(metadata, 0, metadata.Length);
            streamOut.Write(metadata, 0, metadata.Length);

            //Read the original file data from the input and write to output
            var file = new byte[entry.fileSize];
            streamIn.Read(file, 0, file.Length);
            streamOut.Write(file, 0, file.Length);

            // Pad after the file data until we reach a 512 byte boundary if needed
            if (file.Length % 512 > 0)
            {
                int paddingBytes = 512 - (file.Length % 512);
                byte[] bPaddingBytes = new byte[paddingBytes];
                streamIn.Read(bPaddingBytes, 0, paddingBytes);
                streamOut.Write(bPaddingBytes, 0, paddingBytes);
            }
        }
    }

    // Remove a file from our archive using the entry name
    // We cannot do in place overwrites, as we read in the original file
    // And then write it out
    public void RemoveFile(string entryName, string output, bool outputCompressed)
    {
        // Look up the entry based on filename
        TARMetaData entry = Entries.Find(e => e.fileName == entryName);

        //If we found one, extract it
        if (entry != null)
            RemoveFile(entry, output, outputCompressed);
    }


    // Remove a file from our archive
    // We cannot do in place overwrites, as we read in the original file
    // And then write it out
    public void RemoveFile(TARMetaData entryToRemove, string output, bool outputCompressed)
    {
        //Create any directories as required
        string directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        {
            // Open relevant streams and add file as necessary
            // Code is a bit messy, but essentially we have four conditions
            // where the input stream can be compressed or not, and the output
            // stream can be compressed or not
            using (var streamIn = File.OpenRead(Filename))
            {
                using (var streamOut = File.Open(output, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    if (isCompressed)
                    {
                        using (var gzipIn = new GZipStream(streamIn, CompressionMode.Decompress))
                        {
                            if (outputCompressed)
                                using (var gzipOut = new GZipStream(streamOut, CompressionMode.Compress))
                                    RemoveFile(entryToRemove, gzipIn, gzipOut);
                            else
                                RemoveFile(entryToRemove, gzipIn, streamOut);
                        }
                    }
                    else
                    {
                        if (outputCompressed)
                            using (var gzipOut = new GZipStream(streamOut, CompressionMode.Compress))
                                RemoveFile(entryToRemove, streamIn, gzipOut);
                        else
                            RemoveFile(entryToRemove, streamIn, streamOut);
                    }
                }
            }
        }
    }

    // Remove a file from our archive
    private void RemoveFile(TARMetaData entryToRemove, Stream inputStream, Stream outputStream)
    {
        // Loop through every entry
        foreach (TARMetaData entry in Entries)
        {
            // Check to see if this is the entry we are removing
            // If not, set write to true, else set to fale
            bool write = (entry.fileName != entryToRemove.fileName);

            // Read the metadata from the input file
            var metadata = new byte[512];
            inputStream.Read(metadata, 0, metadata.Length);

            //If this file is not being deleted, write out the metadata
            if (write) outputStream.Write(metadata, 0, metadata.Length);

            // Read the file data from the input file
            var file = new byte[entry.fileSize];
            inputStream.Read(file, 0, file.Length);

            //If this file is not being deleted, write out the file
            if (write) outputStream.Write(file, 0, file.Length);

            // Pad after the file data until we reach a 512 byte boundary if needed
            if (file.Length % 512 > 0)
            {
                int paddingBytes = 512 - (file.Length % 512);
                byte[] bPaddingBytes = new byte[paddingBytes];
                inputStream.Read(bPaddingBytes, 0, paddingBytes);
                // We only need to pad the output if the file is not being deleted
                if (write) outputStream.Write(bPaddingBytes, 0, paddingBytes);
            }

        }
    }

    // A safe stream seek function
    static void StreamSeek(Stream stream, long offset)
    {
        //If we can use the stream.seek method, use that
        if (stream.CanSeek)
            stream.Seek(offset, SeekOrigin.Current);
        else
        {
            // Otherwise create a buffer the size we want to see, and read
            // Into it
            var buf = new byte[offset];
            stream.Read(buf, 0, buf.Length);
        }
    }
}
