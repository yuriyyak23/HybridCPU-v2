namespace DoomSharp.Core.GameLogic;

public enum ThinkerAction
{
    None,
    MapObjectThinker,
    VerticalDoor,
    MoveFloor,
    MoveCeiling,
    PlatformRaise,
    StrobeFlash,
    LightFlash,
    GlowingLight,
    FireFlicker
}

public abstract class Thinker
{
    public ThinkerAction Action { get; set; }
    internal Thinker? Next { get; set; }
}

internal enum NestedStateCompletion
{
    None,
    SpawnFly,
    DamageDeath,
    MissileExplosion
}

internal enum MapObjectActionContinuation
{
    None,
    BfgSpray,
    VileAfterDamage,
    PainDie,
    SkelMissileAfterSpawn,
    BrainSpitAfterSpawn,
    FatAttack1,
    FatAttack2,
    FatAttack3,
    RadiusAttack,
    TeleportMove,
    MissilePlacement,
    SPosAttack
}

internal sealed class RadiusAttackWork
{
    internal MapObject Spot { get; set; } = null!;
    internal MapObject Source { get; set; } = null!;
    internal int Damage { get; set; }
    internal int XLow { get; set; }
    internal int XHigh { get; set; }
    internal int YLow { get; set; }
    internal int YHigh { get; set; }
    internal int X { get; set; }
    internal int Y { get; set; }
    internal bool BlockLoaded { get; set; }
    internal MapObject? CurrentThing { get; set; }
    internal MapObject? NextThing { get; set; }
}

internal sealed class TeleportMoveWork
{
    internal MapObject Thing { get; set; } = null!;
    internal MapObject? Cube { get; set; }
    internal Fixed XPosition { get; set; }
    internal Fixed YPosition { get; set; }
    internal Fixed FloorZ { get; set; }
    internal Fixed CeilingZ { get; set; }
    internal Fixed DropOffZ { get; set; }
    internal int XLow { get; set; }
    internal int XHigh { get; set; }
    internal int YLow { get; set; }
    internal int YHigh { get; set; }
    internal int X { get; set; }
    internal int Y { get; set; }
    internal bool BlockLoaded { get; set; }
    internal MapObject? CurrentThing { get; set; }
    internal MapObject? NextThing { get; set; }
}

internal sealed class MissilePlacementWork
{
    internal MapObject Missile { get; set; } = null!;
    internal Fixed XPosition { get; set; }
    internal Fixed YPosition { get; set; }
    internal Fixed FloorZ { get; set; }
    internal Fixed CeilingZ { get; set; }
    internal Fixed DropOffZ { get; set; }
    internal int XLow { get; set; }
    internal int XHigh { get; set; }
    internal int YLow { get; set; }
    internal int YHigh { get; set; }
    internal int X { get; set; }
    internal int Y { get; set; }
    internal bool BlockLoaded { get; set; }
    internal MapObject? CurrentThing { get; set; }
    internal MapObject? NextThing { get; set; }
    internal MapObjectActionContinuation Completion { get; set; }
    internal int CompletionIndex { get; set; }
    internal MapObject? CompletionMapObject2 { get; set; }
}

internal sealed class HitscanAttackWork
{
    internal Angle BaseAngle { get; set; }
    internal Fixed Slope { get; set; }
    internal int NextIndex { get; set; }
}

public sealed class ActionParams
{
    public ActionParams(MapObject? MapObject = null, Player? Player = null, PlayerSprite? PlayerSprite = null, Thinker? Thinker = null)
    {
        this.MapObject = MapObject;
        this.Player = Player;
        this.PlayerSprite = PlayerSprite;
        this.Thinker = Thinker;
    }

    public MapObject? MapObject { get; }
    public Player? Player { get; }
    public PlayerSprite? PlayerSprite { get; }
    public Thinker? Thinker { get; }

    internal MapObject? NestedStateMapObject { get; private set; }
    internal StateNum NestedState { get; private set; } = StateNum.NUMSTATES;
    internal NestedStateCompletion NestedCompletion { get; private set; }
    internal MapObject? NestedCompletionMapObject { get; private set; }
    internal MapObjectActionContinuation Continuation { get; private set; }
    internal int ContinuationIndex { get; private set; }
    internal MapObject? ContinuationMapObject { get; private set; }
    internal MapObject? ContinuationMapObject2 { get; private set; }
    internal RadiusAttackWork? ContinuationRadiusWork { get; private set; }
    internal TeleportMoveWork? ContinuationTeleportMoveWork { get; private set; }
    internal MissilePlacementWork? ContinuationMissilePlacementWork { get; private set; }
    internal HitscanAttackWork? ContinuationHitscanAttackWork { get; private set; }
    internal bool HasDeferredContinuation { get; private set; }

    internal void ScheduleSpawnFlyTransition(MapObject spawned, StateNum state, MapObject cube)
    {
        NestedStateMapObject = spawned;
        NestedState = state;
        NestedCompletion = NestedStateCompletion.SpawnFly;
        NestedCompletionMapObject = cube;
    }

    internal void ScheduleStateTransition(
        MapObject mapObject,
        StateNum state,
        NestedStateCompletion completion,
        MapObjectActionContinuation continuation = MapObjectActionContinuation.None,
        int continuationIndex = 0,
        MapObject? continuationMapObject = null,
        MapObject? continuationMapObject2 = null)
    {
        NestedStateMapObject = mapObject;
        NestedState = state;
        NestedCompletion = completion;
        Continuation = continuation;
        ContinuationIndex = continuationIndex;
        ContinuationMapObject = continuationMapObject;
        ContinuationMapObject2 = continuationMapObject2;
        ContinuationRadiusWork = null;
        ContinuationTeleportMoveWork = null;
        ContinuationMissilePlacementWork = null;
        ContinuationHitscanAttackWork = null;
    }

    internal void ScheduleStateTransition(MapObject mapObject, StateNum state, NestedStateCompletion completion,
        MapObjectActionContinuation continuation, RadiusAttackWork continuationRadiusWork)
    {
        ScheduleStateTransition(mapObject, state, completion, continuation);
        ContinuationRadiusWork = continuationRadiusWork;
    }

    internal void ScheduleStateTransition(MapObject mapObject, StateNum state, NestedStateCompletion completion,
        MapObjectActionContinuation continuation, TeleportMoveWork continuationTeleportMoveWork)
    {
        ScheduleStateTransition(mapObject, state, completion, continuation);
        ContinuationTeleportMoveWork = continuationTeleportMoveWork;
    }

    internal void ScheduleStateTransition(MapObject mapObject, StateNum state, NestedStateCompletion completion,
        MapObjectActionContinuation continuation, MissilePlacementWork continuationMissilePlacementWork)
    {
        ScheduleStateTransition(mapObject, state, completion, continuation);
        ContinuationMissilePlacementWork = continuationMissilePlacementWork;
    }

    internal void ScheduleStateTransition(MapObject mapObject, StateNum state, NestedStateCompletion completion,
        MapObjectActionContinuation continuation, HitscanAttackWork continuationHitscanAttackWork)
    {
        ScheduleStateTransition(mapObject, state, completion, continuation);
        ContinuationHitscanAttackWork = continuationHitscanAttackWork;
    }

    internal void SetContinuationWork(RadiusAttackWork continuationRadiusWork,
        TeleportMoveWork continuationTeleportMoveWork)
    {
        ContinuationRadiusWork = continuationRadiusWork;
        ContinuationTeleportMoveWork = continuationTeleportMoveWork;
    }

    internal void ScheduleContinuation(
        MapObjectActionContinuation continuation,
        int continuationIndex,
        MapObject? continuationMapObject,
        MapObject? continuationMapObject2)
    {
        Continuation = continuation;
        ContinuationIndex = continuationIndex;
        ContinuationMapObject = continuationMapObject;
        ContinuationMapObject2 = continuationMapObject2;
        HasDeferredContinuation = continuation != MapObjectActionContinuation.None;
    }

    internal void ClearNestedStateTransition()
    {
        NestedStateMapObject = null;
        NestedState = StateNum.NUMSTATES;
        NestedCompletion = NestedStateCompletion.None;
        NestedCompletionMapObject = null;
        Continuation = MapObjectActionContinuation.None;
        ContinuationIndex = 0;
        ContinuationMapObject = null;
        ContinuationMapObject2 = null;
        ContinuationRadiusWork = null;
        ContinuationTeleportMoveWork = null;
        ContinuationMissilePlacementWork = null;
        ContinuationHitscanAttackWork = null;
        HasDeferredContinuation = false;
    }
}
