using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DoomSharp.Core;
using DoomSharp.Core.Data;
using DoomSharp.Core.GameLogic;
using DoomSharp.Core.Graphics;
using DoomSharp.Core.Input;
using DoomSharp.HybridCpu.Guest;

namespace DoomSharp.Tests;

internal static class Program
{
    private static int _passed;

    public static int Main()
    {
        Run("minimal WAD", TestMinimalWad);
        Run("WAD bounds and overflow", TestWadValidation);
        Run("column is an exact zero-copy view", TestColumnView);
        Run("renderer working columns use exact int storage", TestRendererWorkingColumns);
        Run("patch is an immutable reference payload", TestPatchReferencePayload);
        Run("vertex is an immutable reference payload", TestVertexReferencePayload);
        Run("texture patches are immutable reference payloads", TestTexturePatchReferencePayloads);
        Run("raw bounding box preserves fixed-point ordering", TestRawBoundingBox);
        Run("BSP node bounding boxes retain exact fixed raw values", TestNodeRawBoundingBox);
        Run("line bounding boxes retain exact fixed raw values", TestLineRawBoundingBox);
        Run("non-commercial things continue after skip", TestThingCompatibility);
        Run("fatal path is no-return", TestFatalPath);
        Run("deterministic monotonic clock", TestClock);
        Run("deterministic guest newline", TestNewLine);
        Run("input ring preserves FIFO reference events", TestInputEventRing);
        Run("nested state continuation payload is exact and clearable", TestNestedStateContinuationPayload);
        Run("fixed raw-value accessor", TestFixedRawValue);
        Run("sprite rotation state", TestSpriteRotationState);
        Run("map-object info preserves value lookup semantics", TestMapObjectInfoLookupSemantics);
        Run("map things are immutable reference payloads", TestMapThingReferencePayload);
        Run("WAD names use exact ASCII case folding", TestWadNameComparison);
        Run("menu hotkey uses a scalar sentinel", TestMenuHotKeySentinel);
        Run("animation definitions are immutable reference entries", TestAnimationDefinitions);
        Run("switch definitions are immutable reference entries", TestSwitchDefinitions);
        Run("save labels use their exact ASCII-space domain", TestSaveLabelSpaces);
        Run("cheat first/repeated use and mismatch reset", TestCheatInitialization);
        Console.WriteLine("PASS " + _passed + " smoke tests");
        return 0;
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void TestCheatInitialization()
    {
        var first = new CheatSequence("iddqd");
        var second = new CheatSequence("idkfa");
        foreach (char c in "iddq") Equal(false, first.CheckCheat(c));
        Equal(true, first.CheckCheat('d'));
        foreach (char c in "idkf") Equal(false, second.CheckCheat(c));
        Equal(true, second.CheckCheat('a'));
        Equal(false, first.CheckCheat('i'));
        Equal(false, first.CheckCheat('x'));
        foreach (char c in "iddq") Equal(false, first.CheckCheat(c));
        Equal(true, first.CheckCheat('d'));
    }

    private static void TestMinimalWad()
    {
        var image = MinimalWad();
        var wad = new WadFile(image);
        Equal("IWAD", wad.Header.Identification);
        Equal(0, wad.LumpCount);
    }

    private static void TestWadValidation()
    {
        Throws<WadFormatException>(() => new WadFile(new byte[11]));

        var hugeCount = MinimalWad();
        WriteInt32(hugeCount, 4, int.MaxValue);
        Throws<WadFormatException>(() => new WadFile(hugeCount));

        var badOffset = MinimalWad();
        WriteInt32(badOffset, 8, int.MaxValue);
        Throws<WadFormatException>(() => new WadFile(badOffset));

        var badRange = new byte[28];
        WriteAscii(badRange, 0, "PWAD");
        WriteInt32(badRange, 4, 1);
        WriteInt32(badRange, 8, 12);
        WriteInt32(badRange, 12, 24);
        WriteInt32(badRange, 16, int.MaxValue);
        Throws<WadFormatException>(() => new WadFile(badRange));
    }

    private static void TestColumnView()
    {
        var composite = new byte[] { 10, 11, 12, 13, 14, 15 };
        var column = RenderEngine.CreateCompositeColumn(composite, 2, 3);
        True(ReferenceEquals(composite, column.Pixels));
        Equal(2, column.PixelOffset);
        Equal(3, column.Length);
        Equal((byte)12, column.Pixels[column.PixelOffset]);
        Equal((byte)14, column.Pixels[column.PixelOffset + column.Length - 1]);
        Throws<ArgumentOutOfRangeException>(() => RenderEngine.CreateCompositeColumn(composite, 5, 2));
    }

    private static void TestRendererWorkingColumns()
    {
        var values = new[] { -1, short.MaxValue, ushort.MaxValue };
        var segment = new DrawSegment
        {
            SpriteTopClip = values,
            SpriteBottomClip = values,
            MaskedTextureCol = values
        };

        True(ReferenceEquals(values, segment.SpriteTopClip));
        True(ReferenceEquals(values, segment.SpriteBottomClip));
        True(ReferenceEquals(values, segment.MaskedTextureCol));
        Equal(-1, segment.SpriteTopClip![0]);
        Equal(ushort.MaxValue, segment.SpriteTopClip[2]);
    }

    private static void TestNestedStateContinuationPayload()
    {
        var owner = new MapObject();
        var nested = new MapObject();
        var second = new MapObject();
        var work = new RadiusAttackWork { Spot = owner, Source = second, Damage = 70 };
        var teleportWork = new TeleportMoveWork
        {
            Thing = owner,
            Cube = second,
            XLow = 2,
            XHigh = 3,
            YLow = 4,
            YHigh = 5,
            BlockLoaded = true,
            CurrentThing = nested,
            NextThing = second
        };
        var parameters = new ActionParams(owner);
        parameters.ScheduleStateTransition(
            nested,
            StateNum.S_PUFF1,
            NestedStateCompletion.DamageDeath,
            MapObjectActionContinuation.TeleportMove,
            17,
            nested,
            second);
        parameters.SetContinuationWork(work, teleportWork);

        True(ReferenceEquals(nested, parameters.NestedStateMapObject));
        Equal(StateNum.S_PUFF1, parameters.NestedState);
        Equal(NestedStateCompletion.DamageDeath, parameters.NestedCompletion);
        Equal(MapObjectActionContinuation.TeleportMove, parameters.Continuation);
        Equal(17, parameters.ContinuationIndex);
        True(ReferenceEquals(work, parameters.ContinuationRadiusWork));
        True(ReferenceEquals(teleportWork, parameters.ContinuationTeleportMoveWork));
        True(teleportWork.BlockLoaded);
        True(ReferenceEquals(nested, teleportWork.CurrentThing));
        True(ReferenceEquals(second, teleportWork.NextThing));

        parameters.ClearNestedStateTransition();
        True(parameters.NestedStateMapObject is null);
        Equal(StateNum.NUMSTATES, parameters.NestedState);
        Equal(MapObjectActionContinuation.None, parameters.Continuation);
        True(parameters.ContinuationRadiusWork is null);
        True(parameters.ContinuationTeleportMoveWork is null);

        parameters.ScheduleContinuation(MapObjectActionContinuation.FatAttack1, 2, nested, second);
        True(parameters.HasDeferredContinuation);
        Equal(MapObjectActionContinuation.FatAttack1, parameters.Continuation);
        parameters.ClearNestedStateTransition();
        True(!parameters.HasDeferredContinuation);

        var hitscanWork = new HitscanAttackWork
        {
            BaseAngle = Angle.Angle90,
            Slope = Fixed.FromRaw(1234),
            NextIndex = 2
        };
        parameters.ScheduleStateTransition(
            nested,
            StateNum.S_PUFF3,
            NestedStateCompletion.None,
            MapObjectActionContinuation.SPosAttack,
            continuationHitscanAttackWork: hitscanWork);
        True(ReferenceEquals(hitscanWork, parameters.ContinuationHitscanAttackWork));
        Equal(2, parameters.ContinuationHitscanAttackWork!.NextIndex);
        parameters.ClearNestedStateTransition();
        True(parameters.ContinuationHitscanAttackWork is null);

        var packed = (long)(int)StateNum.S_PUFF2 |
                     ((long)(int)NestedStateCompletion.MissileExplosion << 32);
        Equal(StateNum.S_PUFF2, MapObject.DamageTransitionState(packed));
        Equal(NestedStateCompletion.MissileExplosion, MapObject.DamageTransitionCompletion(packed));
    }

    private static void TestPatchReferencePayload()
    {
        var offsets = new uint[] { 7 };
        var columns = new Column?[] { null };
        var patch = new Patch(1, 2, 3, 4, offsets, columns);
        Patch?[] slots = new Patch?[1];

        True(slots[0] is null);
        slots[0] = patch;
        True(ReferenceEquals(patch, slots[0]));
        True(ReferenceEquals(offsets, patch.ColumnOffsets));
        True(ReferenceEquals(columns, patch.Columns));
        Equal((ushort)1, patch.Width);
        Equal((ushort)2, patch.Height);
        Equal((short)3, patch.LeftOffset);
        Equal((short)4, patch.TopOffset);
    }

    private static void TestVertexReferencePayload()
    {
        var vertex = new Vertex(new Fixed(11), new Fixed(-7));
        var line = new Line(vertex, vertex, 0, 0, 0);
        var vertices = new[] { vertex };
        True(ReferenceEquals(vertex, vertices[0]));
        True(ReferenceEquals(vertex, line.V1));
        True(ReferenceEquals(vertex, line.V2));
        Equal(11, vertex.X.Value);
        Equal(-7, vertex.Y.Value);
    }

    private static void TestTexturePatchReferencePayloads()
    {
        var mapPatch = new MapPatch(-3, 7, 11, 13, 17);
        var mapPatches = new[] { mapPatch };
        True(ReferenceEquals(mapPatch, mapPatches[0]));
        Equal((short)-3, mapPatches[0].OriginX);
        Equal((short)17, mapPatches[0].ColorMap);

        var texturePatch = new TexturePatch(-5, 19, 23);
        var texturePatches = new[] { texturePatch };
        True(ReferenceEquals(texturePatch, texturePatches[0]));
        Equal(-5, texturePatches[0].OriginX);
        Equal(23, texturePatches[0].Patch);
    }

    private static void TestRawBoundingBox()
    {
        var fixedBox = new Fixed[4];
        var rawBox = new int[4];
        BoundingBox.ClearBox(fixedBox);
        BoundingBox.ClearRawBox(rawBox);
        foreach (var (x, y) in new[] { (-9, 7), (3, -11), (19, 5), (0, 0) })
        {
            BoundingBox.AddToBox(fixedBox, new Fixed(x), new Fixed(y));
            BoundingBox.AddRawToBox(rawBox, x, y);
        }
        for (var index = 0; index < 4; index++)
            Equal(Fixed.RawValue(fixedBox[index]), rawBox[index]);
    }

    private static void TestNodeRawBoundingBox()
    {
        var node = new Node(Fixed.Zero, Fixed.Zero, Fixed.Zero, Fixed.Zero);
        node.BoundingBox[0][BoundingBox.BoxLeft] = Fixed.RawValue(Fixed.FromInt(-17));
        node.BoundingBox[0][BoundingBox.BoxRight] = Fixed.RawValue(Fixed.FromInt(23));
        Equal(Fixed.RawValue(Fixed.FromInt(-17)), node.BoundingBox[0][BoundingBox.BoxLeft]);
        Equal(Fixed.RawValue(Fixed.FromInt(23)), node.BoundingBox[0][BoundingBox.BoxRight]);
    }

    private static void TestThingCompatibility()
    {
        var types = new short[] { 1, 68, 3004 };
        var accepted = new List<short>();
        foreach (var type in types)
        {
            if (!GameController.ShouldSpawnThing(GameMode.Retail, type))
                continue;
            accepted.Add(type);
        }

        Equal(2, accepted.Count);
        Equal((short)3004, accepted[1]);
        True(GameController.ShouldSpawnThing(GameMode.Commercial, 68));
    }

    private static void TestFatalPath()
    {
        var console = new TestConsole();
        DoomGame.SetConsole(console);
        try
        {
            DoomGame.Error("controlled");
            throw new Exception("DoomGame.Error returned");
        }
        catch (DoomTerminationException error)
        {
            Equal(1, error.ExitCode);
            Equal("controlled", error.Message);
            True(console.ShutdownCalled);
        }
        finally
        {
            DoomGame.SetConsole(new NullConsole());
        }
    }

    private static void TestClock()
    {
        var services = new TestServices();
        var clock = new DeterministicDoomClock(services);
        Equal(0, clock.GetTime());
        clock.WaitTic();
        Equal(1, clock.GetTime());
        clock.WaitVBL(5);
        Equal(4, clock.GetTime());
        services.CurrentTic = 3;
        Throws<DoomTerminationException>(() => clock.GetTime());
    }

    private static void TestNewLine()
    {
        Equal("\r\n", DoomGame.NewLine);
    }

    private static void TestFixedRawValue()
    {
        var value = new Fixed(unchecked((int)0x81234567));
        Equal(value.Value, Fixed.RawValue(value));
    }

    private static void TestSpriteRotationState()
    {
        var frame = new SpriteFrame();
        Equal(SpriteRotationMode.Unknown, frame.RotationMode);
        frame.RotationMode = SpriteRotationMode.Single;
        Equal(SpriteRotationMode.Single, frame.RotationMode);
        frame.RotationMode = SpriteRotationMode.Rotating;
        Equal(SpriteRotationMode.Rotating, frame.RotationMode);
    }

    private static void TestMapObjectInfoLookupSemantics()
    {
        const MapObjectType type = MapObjectType.MT_BRUISERSHOT;
        var first = MapObjectInfo.GetByType(type);
        int originalSpeed = first.Speed;
        first.Speed = originalSpeed + 1;
        Equal(originalSpeed, MapObjectInfo.GetByType(type).Speed);

        try
        {
            MapObjectInfo.SetSpeed(type, originalSpeed + 2);
            Equal(originalSpeed + 1, first.Speed);
            var second = MapObjectInfo.GetByType(type);
            Equal(originalSpeed + 2, second.Speed);
            True(!ReferenceEquals(first, second));
        }
        finally
        {
            MapObjectInfo.SetSpeed(type, originalSpeed);
        }
    }

    private static void TestWadNameComparison()
    {
        True(WadFileCollection.NameEquals("playpal", "PLAYPAL"));
        True(WadFileCollection.NameEquals("A1_z", "a1_Z"));
        True(!WadFileCollection.NameEquals("PLAYPAL", "PLAYPA"));
        True(!WadFileCollection.NameEquals("PLAYPAL", "PLAYPAM"));
        True(!WadFileCollection.NameEquals("Ä", "ä"));
    }

    private static void TestMenuHotKeySentinel()
    {
        var empty = new DoomSharp.Core.UI.MenuItem(DoomSharp.Core.UI.MenuItemStatus.Empty, "");
        var keyed = new DoomSharp.Core.UI.MenuItem(DoomSharp.Core.UI.MenuItemStatus.Ok, "M_TEST",
            DoomSharp.Core.UI.MenuChoiceAction.None, 't');
        Equal('\0', empty.HotKey);
        Equal('t', keyed.HotKey);
    }

    private static void TestMapThingReferencePayload()
    {
        var original = new MapThing(1, 2, 90, 3004, 8);
        var playerStart = original.WithType(2);
        True(ReferenceEquals(original, original));
        True(!ReferenceEquals(original, playerStart));
        Equal((short)3004, original.Type);
        Equal((short)2, playerStart.Type);
        Equal(original.X, playerStart.X);
        Equal(original.Y, playerStart.Y);
        Equal(original.Angle, playerStart.Angle);
        Equal(original.Options, playerStart.Options);
    }

    private static void TestLineRawBoundingBox()
    {
        var line = new Line(new Vertex(Fixed.FromRaw(-123), Fixed.FromRaw(456)),
            new Vertex(Fixed.FromRaw(789), Fixed.FromRaw(-1011)), 0, 0, 0);
        line.BoundingBox[BoundingBox.BoxLeft] = Fixed.RawValue(line.V1.X);
        line.BoundingBox[BoundingBox.BoxRight] = Fixed.RawValue(line.V2.X);
        line.BoundingBox[BoundingBox.BoxBottom] = Fixed.RawValue(line.V2.Y);
        line.BoundingBox[BoundingBox.BoxTop] = Fixed.RawValue(line.V1.Y);
        Equal(-123, line.BoundingBox[BoundingBox.BoxLeft]);
        Equal(789, line.BoundingBox[BoundingBox.BoxRight]);
        Equal(-1011, line.BoundingBox[BoundingBox.BoxBottom]);
        Equal(456, line.BoundingBox[BoundingBox.BoxTop]);
    }

    private static void TestAnimationDefinitions()
    {
        var first = AnimationDefinition.Definitions[0];
        True(ReferenceEquals(first, AnimationDefinition.Definitions[0]));
        True(!first.IsTexture);
        Equal("NUKAGE3", first.EndName);
        Equal("NUKAGE1", first.StartName);
        Equal(8, first.Speed);
    }

    private static void TestSwitchDefinitions()
    {
        var first = SwitchControl.PredefinedSwitchList[0];
        True(ReferenceEquals(first, SwitchControl.PredefinedSwitchList[0]));
        Equal("SW1BRCOM", first.Name1);
        Equal("SW2BRCOM", first.Name2);
        Equal(1, first.Episode);
        Equal(0, SwitchControl.PredefinedSwitchList[^1].Episode);
    }

    private static void TestSaveLabelSpaces()
    {
        True(DoomString.IsNullOrSpaces(null));
        True(DoomString.IsNullOrSpaces(""));
        True(DoomString.IsNullOrSpaces("   "));
        True(!DoomString.IsNullOrSpaces(" E1M1 "));
        True(!DoomString.IsNullOrSpaces("\t"));
        Equal('A', DoomString.AsciiUpper('a'));
        Equal('Z', DoomString.AsciiUpper('Z'));
        Equal('!', DoomString.AsciiUpper('!'));
        Equal("0", DoomString.DecimalDigit(0));
        Equal("9", DoomString.DecimalDigit(9));
        Throws<ArgumentOutOfRangeException>(() => DoomString.DecimalDigit(10));
        Equal("00", DoomString.DecimalTwoDigits(0));
        Equal("09", DoomString.DecimalTwoDigits(9));
        Equal("35", DoomString.DecimalTwoDigits(35));
        Equal("0", DoomString.DecimalInt(0));
        Equal("-2147483648", DoomString.DecimalInt(int.MinValue));
        Equal("2147483647", DoomString.DecimalInt(int.MaxValue));
        Equal("0", DoomString.HexUInt(0));
        Equal("abcdef", DoomString.HexUInt(0xabcdef));
        Equal("ffffffff", DoomString.HexInt(-1));
        Equal("map01", DoomString.MapLumpName(true, 1, 1));
        Equal("map32", DoomString.MapLumpName(true, 0, 32));
        Equal("E4M9", DoomString.MapLumpName(false, 4, 9));
        Throws<ArgumentOutOfRangeException>(() => DoomString.DecimalTwoDigits(100));
    }

    private static void TestInputEventRing()
    {
        var ring = new InputEventRingBuffer(2);
        True(ring.DequeueOrNull() is null);

        var first = new InputEvent(EventType.KeyDown, 1, 2, 3);
        var second = new InputEvent(EventType.Mouse, 4, 5, 6);
        ring.Enqueue(first);
        ring.Enqueue(second);
        ring.Enqueue(new InputEvent(EventType.KeyUp, 7, 8, 9));

        True(ReferenceEquals(first, ring.DequeueOrNull()));
        True(ring.TryDequeue(out var dequeued));
        True(ReferenceEquals(second, dequeued));
        True(!ring.TryDequeue(out _));
    }

    private static byte[] MinimalWad()
    {
        var image = new byte[12];
        WriteAscii(image, 0, "IWAD");
        WriteInt32(image, 8, 12);
        return image;
    }

    private static void WriteAscii(byte[] data, int offset, string value)
    {
        for (var i = 0; i < value.Length; i++) data[offset + i] = (byte)value[i];
    }

    private static void WriteInt32(byte[] data, int offset, int value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).FullName);
    }

    private static void True(bool value)
    {
        if (!value) throw new Exception("Assertion failed");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", actual " + actual);
    }

    private sealed class TestConsole : IConsole
    {
        public bool ShutdownCalled { get; private set; }
        public void Write(string message) { }
        public void WriteLine(string message) { }
        public void SetTitle(string title) { }
        public void Shutdown() => ShutdownCalled = true;
    }

    private sealed class TestServices : IHybridCpuGuestServices
    {
        public int CurrentTic { get; set; }
        public byte[] GetBootBlob(int blobId) => Array.Empty<byte>();
        public void ConsoleWrite(string message) { }
        public void ConsoleSetTitle(string title) { }
        public void InitializeFramebuffer(int width, int height) { }
        public void UpdatePalette(byte[] palette) { }
        public void PresentFramebuffer(byte[] framebuffer) { }
        public InputEvent? PullInput() => null;
        public int GetMonotonicDoomTics() => CurrentTic;
        public void WaitUntilDoomTic(int targetTic) => CurrentTic = targetTic;
        [DoesNotReturn] public void ProcessExit(int exitCode) => throw new Exception("exit " + exitCode);
    }
}
