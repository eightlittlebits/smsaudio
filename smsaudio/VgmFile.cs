using System;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace smsaudio
{
    class VgmFile
    {
        const uint VgmIdentifier = 0x206d6756; // "Vgm ";
        const uint Gd3TagIdentifier = 0x20336447; // "Gd3 "

        public VgmHeader Header { get; private set; }
        public Gd3Tag Gd3 { get; private set; }

        public byte[] VgmData { get; private set; }
        public uint LoopOffset { get; private set; }

        public static VgmFile Load(Stream stream)
        {
            Stream readStream;

            if (IsVgmGzipped(stream))
            {
                readStream = DecompressVgm(stream);
            }
            else
                readStream = stream;

            VgmFile file = new VgmFile();

            using (var reader = new BinaryReader(readStream, Encoding.Unicode, true))
            {
                file.Header = ReadVgmHeader(reader);

                uint gd3DataOffset = 0;

                if (file.Header.GD3Offset != 0)
                {
                    // GD3 offset is relative to the header entry so add 0x14 to get absolute offset
                    gd3DataOffset = file.Header.GD3Offset + 0x14;

                    reader.BaseStream.Seek(gd3DataOffset, SeekOrigin.Begin);
                    file.Gd3 = ReadGd3Tag(reader);
                }

                // for versions < 1.5 data offset is 0 and data starts at 0x40
                uint vgmDataOffset = file.Header.VGMDataOffset > 0 ? file.Header.VGMDataOffset + 0x34 : 0x40;
                uint vgmDataLength;

                if (gd3DataOffset > vgmDataOffset)
                    vgmDataLength = gd3DataOffset - vgmDataOffset;
                else
                    vgmDataLength = (file.Header.EofOffset + 4) - vgmDataOffset;

                reader.BaseStream.Seek(vgmDataOffset, SeekOrigin.Begin);
                file.VgmData = reader.ReadBytes((int)vgmDataLength);

                // correct loop offset, calculate absolute offset and remove vgm offset
                if (file.Header.LoopOffset > 0)
                    file.LoopOffset = (file.Header.LoopOffset + 0x1C) - vgmDataOffset;
                else
                    file.LoopOffset = 0;
            }

            return file;
        }
        
        private static bool IsVgmGzipped(Stream stream)
        {
            // read the first two bytes to get identifier
            byte[] id = new byte[2];
            stream.Read(id, 0, 2);

            // reset the stream
            stream.Seek(0, SeekOrigin.Begin);

            bool isGzipped = false;

            if (id[0] == 0x1F && id[1] == 0x8B)
            {
                isGzipped = true;
            }

            return isGzipped;
        }

        private static Stream DecompressVgm(Stream stream)
        {
            MemoryStream memStream = new MemoryStream();

            // decompress the gzipped stream into a memory stream and return the memory stream
            using (GZipStream decompress = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                decompress.CopyTo(memStream);
            }

            memStream.Seek(0, SeekOrigin.Begin);

            return memStream;
        }
        
        static VgmHeader ReadVgmHeader(BinaryReader reader)
        {
            uint identifier = reader.ReadUInt32();

            // check the file identifier to make sure it's a valid VGM file
            if (identifier != VgmIdentifier)
            {
                throw new FileFormatException("VGM file identifier does not match \"Vgm \"");
            }

            var header = new VgmHeader
            {
                // version 1.00 header values
                EofOffset = reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                SN76489Clock = reader.ReadUInt32(),
                YM2413Clock = reader.ReadUInt32(),
                GD3Offset = reader.ReadUInt32(),
                TotalSamples = reader.ReadUInt32(),
                LoopOffset = reader.ReadUInt32(),
                LoopSamples = reader.ReadUInt32()
            };

            // check that we support the version of the vgm
            if (header.Version > 0x150)
            {
                throw new NotSupportedException("VGM files greater than version 1.50 cannot currently be loaded");
            }

            if (header.Version >= 0x101)
            {
                header.Rate = reader.ReadUInt32();
            }

            if (header.Version >= 0x110)
            {
                header.SN76489Feedback = reader.ReadUInt16();
                header.SN76489ShiftRegisterWidth = reader.ReadByte();

                reader.ReadByte();

                header.YM2612Clock = reader.ReadUInt32();
                header.YM2151Clock = reader.ReadUInt32();
            }

            if (header.Version >= 0x150)
            {
                header.VGMDataOffset = reader.ReadUInt32();
            }

            return header;
        }

        static Gd3Tag ReadGd3Tag(BinaryReader reader)
        {
            uint identifier = reader.ReadUInt32();
            uint gd3Version = reader.ReadUInt32();
            uint dataLength = reader.ReadUInt32();

            // check the tag identifier to make sure it's a valid GD3 tag
            if (identifier != Gd3TagIdentifier)
            {
                throw new FileFormatException("GD3 tag identifier does not match \"Gd3 \"");
            }

            var tag = new Gd3Tag();

            tag.English.TrackName = ReadNullTerminatedString(reader);
            tag.Japanese.TrackName = ReadNullTerminatedString(reader);

            tag.English.GameName = ReadNullTerminatedString(reader);
            tag.Japanese.GameName = ReadNullTerminatedString(reader);

            tag.English.SystemName = ReadNullTerminatedString(reader);
            tag.Japanese.SystemName = ReadNullTerminatedString(reader);

            tag.English.TrackAuthor = ReadNullTerminatedString(reader);
            tag.Japanese.TrackAuthor = ReadNullTerminatedString(reader);

            tag.ReleaseDate = ReadNullTerminatedString(reader);
            tag.VgmAuthor = ReadNullTerminatedString(reader);
            tag.Notes = ReadNullTerminatedString(reader);

            return tag;
        }

        static string ReadNullTerminatedString(BinaryReader reader)
        {
            var builder = new StringBuilder();

            char c;

            while ((c = reader.ReadChar()) != '\0')
            {
                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
