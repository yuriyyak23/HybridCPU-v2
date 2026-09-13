namespace DoomSharp.Core.Data;

public sealed class WadFile
{
    public sealed class WadInfo
    {
        public string Identification = "";
        public int NumLumps;
        public int InfoTableOfs;
    }

    public sealed class FileLump
    {
        public int FilePos;
        public int Size;
        public string Name = "";
    }

    private readonly byte[] _image;

    public WadFile(byte[] image)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (image.Length < 12)
            throw new WadFormatException("WAD header is truncated.");

        _image = image;
        var numLumps = ReadInt32(image, 4);
        var infoTableOfs = ReadInt32(image, 8);
        ValidateDirectory(image, numLumps, infoTableOfs);

        var reader = new ByteReader(image);
        Header = new WadInfo
        {
            Identification = reader.ReadName(4),
            NumLumps = reader.ReadInt32(),
            InfoTableOfs = reader.ReadInt32()
        };
        Lumps = new WadLump[Header.NumLumps];
        reader.Seek(Header.InfoTableOfs);
        for (var i = 0; i < Header.NumLumps; i++)
        {
            var fileLump = new FileLump
            {
                FilePos = reader.ReadInt32(),
                Size = reader.ReadInt32(),
                Name = reader.ReadName8()
            };
            Lumps[i] = new WadLump(this, fileLump);
        }
    }

    public WadInfo Header { get; }
    public WadLump[] Lumps { get; }
    public int LumpCount => Lumps.Length;

    public void ReadLumpData(WadLump destination)
    {
        var data = new byte[destination.Lump.Size];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = _image[destination.Lump.FilePos + i];
        }

        destination.Data = data;
    }

    private static void ValidateDirectory(byte[] image, int numLumps, int infoTableOfs)
    {
        if (!IsWadIdentification(image))
            throw new WadFormatException("WAD identification must be IWAD or PWAD.");
        if (numLumps < 0)
            throw new WadFormatException("WAD lump count is negative.");
        if (infoTableOfs < 12 || infoTableOfs > image.Length)
            throw new WadFormatException("WAD directory offset is outside the image.");
        if (numLumps > (image.Length - infoTableOfs) / 16)
            throw new WadFormatException("WAD directory is truncated or its size overflows the image.");

        for (var i = 0; i < numLumps; i++)
        {
            var entryOffset = infoTableOfs + i * 16;
            var filePos = ReadInt32(image, entryOffset);
            var size = ReadInt32(image, entryOffset + 4);
            if (filePos < 0 || size < 0 || filePos > image.Length || size > image.Length - filePos)
                throw new WadFormatException("WAD lump range is outside the image.");
        }
    }

    private static bool IsWadIdentification(byte[] image)
    {
        return (image[0] == (byte)'I' || image[0] == (byte)'P') &&
               image[1] == (byte)'W' && image[2] == (byte)'A' && image[3] == (byte)'D';
    }

    private static int ReadInt32(byte[] image, int offset)
    {
        return image[offset]
            | (image[offset + 1] << 8)
            | (image[offset + 2] << 16)
            | (image[offset + 3] << 24);
    }
}
