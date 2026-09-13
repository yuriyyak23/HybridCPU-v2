namespace DoomSharp.Core.Data;

public sealed class WadFileCollection
{
    private readonly WadLump[] _lumps;

    private WadFileCollection(WadFile wadFile)
    {
        _lumps = wadFile.Lumps;
    }

    public static WadFileCollection Initialize(byte[] wadImage)
    {
        try
        {
            return new WadFileCollection(new WadFile(wadImage));
        }
        catch (WadFormatException error)
        {
            DoomGame.Error(string.Concat("W_InitFiles: ", error.Message, ""));
            throw;
        }
    }

    public WadLump this[int index] => _lumps[index];
    public int LumpCount => _lumps.Length;

    public byte[]? GetLumpName(string name, PurgeTag tag)
    {
        return GetLumpNum(GetNumForName(name), tag);
    }

    public byte[]? GetLumpNum(int lump, PurgeTag tag)
    {
        if (lump < 0)
            return null;
        if (lump >= LumpCount)
        {
            DoomGame.Error("W_CacheLumpNum: lump index is outside the lump table");
            return null;
        }

        var wadLump = _lumps[lump];
        if (wadLump.Data == null)
        {
            ReadLump(lump, wadLump);
        }
        wadLump.Tag = tag;
        return wadLump.Data;
    }

    public int GetNumForName(string name)
    {
        var num = CheckNumForName(name);
        if (num == -1)
            DoomGame.Console.WriteLine(string.Concat("W_GetNumForName: ", name, " not found!"));
        return num;
    }

    public int CheckNumForName(string name)
    {
        for (var i = LumpCount - 1; i >= 0; i--)
        {
            if (NameEquals(_lumps[i].Lump.Name, name))
                return i;
        }
        return -1;
    }

    public static bool NameEquals(string left, string right)
    {
        if (left.Length != right.Length)
            return false;
        for (var i = 0; i < left.Length; i++)
        {
            var a = left[i];
            var b = right[i];
            if (a >= 'a' && a <= 'z') a = (char)(a - ('a' - 'A'));
            if (b >= 'a' && b <= 'z') b = (char)(b - ('a' - 'A'));
            if (a != b)
                return false;
        }
        return true;
    }

    public int LumpLength(int lump)
    {
        if (lump < 0 || lump >= LumpCount)
        {
            DoomGame.Error("W_LumpLength: lump index is outside the lump table");
            return -1;
        }
        return _lumps[lump].Lump.Size;
    }

    public void ReadLump(int lump, WadLump destination)
    {
        if (lump < 0 || lump >= LumpCount)
        {
            DoomGame.Error("W_ReadLump: lump index is outside the lump table");
            return;
        }
        destination.File.ReadLumpData(destination);
    }
}
