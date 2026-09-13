using DoomSharp.Core.Data;
using DoomSharp.Core.Graphics;
using DoomSharp.Core.Input;

namespace DoomSharp.Core.GameLogic;

public enum StateAction
{
    None,
    Light0,
    Light1,
    Light2,
    WeaponReady,
    Lower,
    Raise,
    Punch,
    ReFire,
    FirePistol,
    FireShotgun,
    FireShotgun2,
    CheckReload,
    OpenShotgun2,
    LoadShotgun2,
    CloseShotgun2,
    FireCGun,
    GunFlash,
    FireMissile,
    Saw,
    FirePlasma,
    FireBFG,
    BFGSpray,
    Explode,
    Fall,
    XScream,
    Look,
    Chase,
    FaceTarget,
    PosAttack,
    SPosAttack,
    CPosAttack,
    CPosRefire,
    SpidRefire,
    BspiAttack,
    TroopAttack,
    SargAttack,
    HeadAttack,
    CyberAttack,
    BruisAttack,
    SkelMissile,
    Tracer,
    VileChase,
    VileStart,
    VileTarget,
    VileAttack,
    StartFire,
    FireCrackle,
    Fire,
    SkelWhoosh,
    SkelFist,
    FatRaise,
    FatAttack1,
    FatAttack2,
    FatAttack3,
    BossDeath,
    SkullAttack,
    PainAttack,
    PainDie,
    Metal,
    BabyMetal,
    Hoof,
    KeenDie,
    BrainAwake,
    BrainPain,
    BrainScream,
    BrainExplode,
    BrainDie,
    BrainSpit,
    SpawnFly,
 }

public sealed class State
{
    public State(SpriteNum sprite, int frame, int tics, StateAction action, StateNum nextState, int misc1, int misc2)
    {
        Sprite = sprite;
        Frame = frame;
        Tics = tics;
        Action = action;
        NextState = nextState;
        Misc1 = misc1;
        Misc2 = misc2;
    }

    public SpriteNum Sprite { get; }
    public int Frame { get; }
    public int Tics { get; set; }
    public StateAction Action { get; }
    public StateNum NextState { get; }
    public int Misc1 { get; }
    public int Misc2 { get; }
    private static readonly State[] _states = new State[(int)StateNum.NUMSTATES];
    private static int _stateCount;

    static State()
    {
        AddPredefinedStates();
    }

    private static void AddPredefinedState(State state)
    {
        _states[_stateCount++] = state;
    }

    public static State GetSpawnState(StateNum stateNum)
    {
        return _states[(int)stateNum];
    }

    public static StateNum GetStateNum(State state)
    {
        for (var i = 0; i < _stateCount; i++)
        {
            if (ReferenceEquals(_states[i], state))
            {
                return (StateNum)i;
            }
        }

        return StateNum.S_NULL;
    }

    public static StateNum ExecutePlayerSpriteAction(StateAction action, ActionParams parameters)
    {
        switch (action)
        {
            case StateAction.Light0: ActionLight0(parameters); break;
            case StateAction.Light1: ActionLight1(parameters); break;
            case StateAction.Light2: ActionLight2(parameters); break;
            case StateAction.WeaponReady: return ActionWeaponReady(parameters);
            case StateAction.Lower: return ActionLower(parameters);
            case StateAction.Raise: return ActionRaise(parameters);
            case StateAction.Punch: ActionPunch(parameters); break;
            case StateAction.ReFire: return ActionReFire(parameters);
            case StateAction.FirePistol: ActionFirePistol(parameters); break;
            case StateAction.FireShotgun: ActionFireShotgun(parameters); break;
            case StateAction.FireShotgun2: ActionFireShotgun2(parameters); break;
            case StateAction.CheckReload: return ActionCheckReload(parameters);
            case StateAction.OpenShotgun2: ActionOpenShotgun2(parameters); break;
            case StateAction.LoadShotgun2: ActionLoadShotgun2(parameters); break;
            case StateAction.CloseShotgun2: return ActionCloseShotgun2(parameters);
            case StateAction.FireCGun: ActionFireCGun(parameters); break;
            case StateAction.GunFlash: ActionGunFlash(parameters); break;
            case StateAction.FireMissile: ActionFireMissile(parameters); break;
            case StateAction.Saw: ActionSaw(parameters); break;
            case StateAction.FirePlasma: ActionFirePlasma(parameters); break;
            case StateAction.FireBFG: ActionFireBFG(parameters); break;
            case StateAction.None:
                break;
            default:
                DoomGame.Error("Invalid player-sprite state action");
                break;
        }

        return StateNum.NUMSTATES;
    }

    public static void ExecutePlayerFlashAction(StateAction action, ActionParams parameters)
    {
        switch (action)
        {
            case StateAction.Light0: ActionLight0(parameters); break;
            case StateAction.Light1: ActionLight1(parameters); break;
            case StateAction.Light2: ActionLight2(parameters); break;
            case StateAction.None:
                break;
            default:
                DoomGame.Error("Invalid player-flash state action");
                break;
        }
    }

    internal static void ExecutePainStateAction(StateAction action, ActionParams parameters)
    {
        switch (action)
        {
            case StateAction.BrainPain:
                ActionBrainPain(parameters);
                break;
            case StateAction.None:
                break;
            default:
                DoomGame.Error("Invalid pain-state action");
                break;
        }
    }

    public static StateNum ExecuteMapObjectAction(StateAction action, ActionParams parameters)
    {
        switch (action)
        {
            case StateAction.BFGSpray: ActionBFGSpray(parameters); break;
            case StateAction.Explode: ActionExplode(parameters); break;
            case StateAction.Fall: ActionFall(parameters); break;
            case StateAction.XScream: ActionXScream(parameters); break;
            case StateAction.Look: return ActionLook(parameters);
            case StateAction.Chase: return ActionChase(parameters);
            case StateAction.FaceTarget: ActionFaceTarget(parameters); break;
            case StateAction.PosAttack: ActionPosAttack(parameters); break;
            case StateAction.SPosAttack: ActionSPosAttack(parameters); break;
            case StateAction.CPosAttack: ActionCPosAttack(parameters); break;
            case StateAction.CPosRefire: return ActionCPosRefire(parameters);
            case StateAction.SpidRefire: return ActionSpidRefire(parameters);
            case StateAction.BspiAttack: ActionBspiAttack(parameters); break;
            case StateAction.TroopAttack: ActionTroopAttack(parameters); break;
            case StateAction.SargAttack: ActionSargAttack(parameters); break;
            case StateAction.HeadAttack: ActionHeadAttack(parameters); break;
            case StateAction.CyberAttack: ActionCyberAttack(parameters); break;
            case StateAction.BruisAttack: ActionBruisAttack(parameters); break;
            case StateAction.SkelMissile: ActionSkelMissile(parameters); break;
            case StateAction.Tracer: ActionTracer(parameters); break;
            case StateAction.VileChase: return ActionVileChase(parameters);
            case StateAction.VileStart: ActionVileStart(parameters); break;
            case StateAction.VileTarget: ActionVileTarget(parameters); break;
            case StateAction.VileAttack: ActionVileAttack(parameters); break;
            case StateAction.StartFire: ActionStartFire(parameters); break;
            case StateAction.FireCrackle: ActionFireCrackle(parameters); break;
            case StateAction.Fire: ActionFire(parameters); break;
            case StateAction.SkelWhoosh: ActionSkelWhoosh(parameters); break;
            case StateAction.SkelFist: ActionSkelFist(parameters); break;
            case StateAction.FatRaise: ActionFatRaise(parameters); break;
            case StateAction.FatAttack1: ActionFatAttack1(parameters); break;
            case StateAction.FatAttack2: ActionFatAttack2(parameters); break;
            case StateAction.FatAttack3: ActionFatAttack3(parameters); break;
            case StateAction.BossDeath: ActionBossDeath(parameters); break;
            case StateAction.SkullAttack: ActionSkullAttack(parameters); break;
            case StateAction.PainAttack: ActionPainAttack(parameters); break;
            case StateAction.PainDie: ActionPainDie(parameters); break;
            case StateAction.Metal: return ActionMetal(parameters);
            case StateAction.BabyMetal: return ActionBabyMetal(parameters);
            case StateAction.Hoof: return ActionHoof(parameters);
            case StateAction.KeenDie: ActionKeenDie(parameters); break;
            case StateAction.BrainAwake: ActionBrainAwake(parameters); break;
            case StateAction.BrainPain: ActionBrainPain(parameters); break;
            case StateAction.BrainScream: ActionBrainScream(parameters); break;
            case StateAction.BrainExplode: ActionBrainExplode(parameters); break;
            case StateAction.BrainDie: ActionBrainDie(parameters); break;
            case StateAction.BrainSpit: ActionBrainSpit(parameters); break;
            case StateAction.SpawnFly: ActionSpawnFly(parameters); break;
            case StateAction.None:
                break;
            default:
                DoomGame.Error("Invalid map-object state action");
                break;
        }

        return StateNum.NUMSTATES;
    }

    internal static void ResumeMapObjectAction(
        MapObjectActionContinuation continuation,
        int continuationIndex,
        MapObject? continuationMapObject,
        MapObject? continuationMapObject2,
        ActionParams parameters)
    {
        switch (continuation)
        {
            case MapObjectActionContinuation.BfgSpray:
                ContinueBfgSpray(parameters, continuationIndex);
                break;
            case MapObjectActionContinuation.VileAfterDamage:
                ContinueVileAttack(parameters);
                break;
            case MapObjectActionContinuation.PainDie:
                ContinuePainDie(parameters, continuationIndex);
                break;
            case MapObjectActionContinuation.SkelMissileAfterSpawn:
                ContinueSkelMissile(parameters, continuationMapObject!);
                break;
            case MapObjectActionContinuation.BrainSpitAfterSpawn:
                ContinueBrainSpit(parameters, continuationMapObject!, continuationMapObject2!);
                break;
            case MapObjectActionContinuation.FatAttack1:
                ContinueFatAttack1(parameters, continuationIndex, continuationMapObject);
                break;
            case MapObjectActionContinuation.FatAttack2:
                ContinueFatAttack2(parameters, continuationIndex, continuationMapObject);
                break;
            case MapObjectActionContinuation.FatAttack3:
                ContinueFatAttack3(parameters, continuationIndex, continuationMapObject);
                break;
            default:
                DoomGame.Error("Invalid map-object action continuation");
                break;
        }
    }

    internal static void ResumeMapObjectAction(ActionParams parameters, RadiusAttackWork work) =>
        DoomGame.Instance.Game.ContinueRadiusAttackForAction(parameters, work, true);

    internal static void ResumeMapObjectAction(ActionParams parameters, TeleportMoveWork work) =>
        DoomGame.Instance.Game.ContinueTeleportMoveForAction(parameters, work, true);

    internal static void ResumeMapObjectAction(ActionParams parameters, MissilePlacementWork work) =>
        DoomGame.Instance.Game.ContinueMissilePlacementForAction(parameters, work, true);

    internal static void ResumeMapObjectAction(ActionParams parameters, HitscanAttackWork work) =>
        ContinueSPosAttack(parameters, work);

    private static void ActionLight0(ActionParams actionParams)
    {
        actionParams.Player!.ExtraLight = 0;
    }

    private static void ActionLight1(ActionParams actionParams) 
    {
        actionParams.Player!.ExtraLight = 1;
    }

    private static void ActionLight2(ActionParams actionParams) 
    {
        actionParams.Player!.ExtraLight = 2;
    }

    // The player can fire the weapon
    // or change to another weapon at this time.
    // Follows after getting weapon up,
    // or after previous attack/fire sequence.
    private static StateNum ActionWeaponReady(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var psp = actionParams.PlayerSprite!;
        var game = DoomGame.Instance.Game;

        if (player.MapObject!.State == _states[(int)StateNum.S_PLAY_ATK1] || player.MapObject.State == _states[(int)StateNum.S_PLAY_ATK2])
        {
            player.MapObject.SetStateWithoutAction(StateNum.S_PLAY);
        }

        if (player.ReadyWeapon == WeaponType.Chainsaw && psp.State == _states[(int)StateNum.S_SAW])
        {
        }

        // check for change
        //  if player is dead, put the weapon away
        if (player.PendingWeapon != WeaponType.NoChange || player.Health == 0)
        {
            // change weapon
            //  (pending weapon should already be validated)
            var newState = WeaponInfo.GetByType(player.ReadyWeapon).DownState;
            return newState;
        }

        // check for fire
        //  the missile launcher and bfg do not auto fire
        if ((player.Command.Buttons & ButtonCode.Attack) != 0)
        {
            if (player.AttackDown == false
                 || (player.ReadyWeapon != WeaponType.Missile
                 && player.ReadyWeapon != WeaponType.Bfg))
            {
                player.AttackDown = true;
                return game.P_FireWeaponTransition(player);
            }
        }
        else
        {
            player.AttackDown = false;
        }

        // bob the weapon based on movement speed
        var angle = (128 * game.LevelTime) & DoomMath.FineMask;
        psp.SX = Fixed.Unit + (player.Bob * DoomMath.Cos(angle));
        angle &= DoomMath.FineAngleCount / 2 - 1;
        psp.SY = WeaponInfo.WeaponTop + (player.Bob * DoomMath.Sin(angle));
        return StateNum.NUMSTATES;
    }

    // Lowers current weapon,
    //  and changes weapon at bottom.
    private static StateNum ActionLower(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var psp = actionParams.PlayerSprite!;

        psp.SY += WeaponInfo.LowerSpeed;

        // Is already down.
        if (psp.SY < WeaponInfo.WeaponBottom)
        {
            return StateNum.NUMSTATES;
        }

        // Player is dead.
        if (player.PlayerState == PlayerState.Dead)
        {
            psp.SY = WeaponInfo.WeaponBottom;

            // don't bring weapon back up
            return StateNum.NUMSTATES;
        }

        var game = DoomGame.Instance.Game;
        // The old weapon has been lowered off the screen,
        // so change the weapon and start raising it
        if (player.Health == 0)
        {
            // Player is dead, so keep the weapon off screen.
            return StateNum.S_NULL;
        }

        player.ReadyWeapon = player.PendingWeapon;

        if (player.PendingWeapon == WeaponType.NoChange)
        {
            player.PendingWeapon = player.ReadyWeapon;
        }

        var newState = WeaponInfo.GetByType(player.PendingWeapon).UpState;
        player.PendingWeapon = WeaponType.NoChange;
        psp.SY = WeaponInfo.WeaponBottom;
        return newState;
    }
    
    private static StateNum ActionRaise(ActionParams actionParams) 
    {
        var player = actionParams.Player!;
        var psp = actionParams.PlayerSprite!;
        psp.SY -= WeaponInfo.RaiseSpeed;

        if (psp.SY > WeaponInfo.WeaponTop)
        {
            return StateNum.NUMSTATES;
        }

        psp.SY = WeaponInfo.WeaponTop;

        // The weapon has been raised all the way,
        //  so change to the ready state.
        var newState = WeaponInfo.GetByType(player.ReadyWeapon).ReadyState;
        return newState;
    }

    private static void ActionPunch(ActionParams actionParams)
    {
        var player = actionParams.Player!;

        var damage = (DoomRandom.P_Random() % 10 + 1) << 1;
        if (player.Powers[(int)PowerUpType.Strength] != 0)
        {
            damage *= 10;
        }

        var angle = player.MapObject!.Angle;
        angle += new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 18);
        var slope = DoomGame.Instance.Game.P_AimLineAttack(player.MapObject, angle, Constants.MeleeRange);
        DoomGame.Instance.Game.P_LineAttack(player.MapObject, angle, Constants.MeleeRange, slope, damage);

        // turn to face target
        var lineTarget = DoomGame.Instance.Game.LineTarget;
        if (lineTarget != null)
        {
            player.MapObject.Angle = DoomGame.Instance.Renderer.PointToAngle2(
                    player.MapObject.X,
                    player.MapObject.Y,
                    lineTarget.X,
                    lineTarget.Y);
        }
    }

    /// <summary>
    /// The player can re-fire the weapon
    /// without lowering it entirely.
    /// </summary>
    private static StateNum ActionReFire(ActionParams actionParams)
    {
        // check for fire
        //  (if a weaponchange is pending, let it go through instead)
        var player = actionParams.Player!;
        if ((player.Command.Buttons & ButtonCode.Attack) != 0 &&
            player.PendingWeapon == WeaponType.NoChange &&
            player.Health != 0)
        {
            player.Refire++;
            return DoomGame.Instance.Game.P_FireWeaponTransition(player);
        }
        else
        {
            player.Refire = 0;
            return DoomGame.Instance.Game.P_CheckAmmoTransition(player);
        }

    }

    private static void ActionFirePistol(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);
        
        player.MapObject!.SetStateWithoutAction(StateNum.S_PLAY_ATK2);
        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - 1;

        var game = DoomGame.Instance.Game;
        game.P_SetPlayerFlashSprite(player, weapon.FlashState);

        game.P_BulletSlope(player.MapObject);
        game.P_GunShot(player.MapObject, player.Refire == 0);
    }

    private static void ActionFireShotgun(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);
        
        player.MapObject!.SetStateWithoutAction(StateNum.S_PLAY_ATK2);
        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - 1;

        var game = DoomGame.Instance.Game;
        game.P_SetPlayerFlashSprite(player, weapon.FlashState);

        game.P_BulletSlope(player.MapObject);
        for (var i = 0; i < 7; i++)
        {
            game.P_GunShot(player.MapObject, false);
        }
    }

    private static void ActionFireShotgun2(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);
        
        player.MapObject!.SetStateWithoutAction(StateNum.S_PLAY_ATK2);
        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - 2;

        var game = DoomGame.Instance.Game;
        game.P_SetPlayerFlashSprite(player, weapon.FlashState);

        game.P_BulletSlope(player.MapObject);
        for (var i = 0; i < 20; i++)
        {
            var damage = 5 * (DoomRandom.P_Random() % 3 + 1);
            var angle = player.MapObject.Angle;
            angle += new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 19);

            game.P_LineAttack(
                player.MapObject, 
                angle, 
                Constants.MissileRange, 
                game.BulletSlope + new Fixed((DoomRandom.P_Random() - DoomRandom.P_Random()) << 5), 
                damage);
        }
    }

    private static StateNum ActionCheckReload(ActionParams actionParams)
    {
        return DoomGame.Instance.Game.P_CheckAmmoTransition(actionParams.Player!);
    }

    private static void ActionOpenShotgun2(ActionParams actionParams) { }
    private static void ActionLoadShotgun2(ActionParams actionParams) { }
    private static StateNum ActionCloseShotgun2(ActionParams actionParams) { return ActionReFire(actionParams); }

    private static void ActionFireCGun(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var psp = actionParams.PlayerSprite!;

        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);
        
        if (player.Ammo[(int)weapon.Ammo] == 0)
        {
            return;
        }

        player.MapObject!.SetStateWithoutAction(StateNum.S_PLAY_ATK2);
        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - 1;

        var game = DoomGame.Instance.Game;
        var stateNum = State.GetStateNum(psp.State!);
        game.P_SetPlayerFlashSprite(player, (StateNum)(weapon.FlashState + (int)stateNum - StateNum.S_CHAIN1));

        game.P_BulletSlope(player.MapObject);
        game.P_GunShot(player.MapObject, player.Refire == 0);
    }

    private static void ActionGunFlash(ActionParams actionParams)
    {
        var player = actionParams.Player!;

        player.MapObject!.SetStateWithoutAction(StateNum.S_PLAY_ATK2);
        DoomGame.Instance.Game.P_SetPlayerFlashSprite(player,
            WeaponInfo.GetByType(player.ReadyWeapon).FlashState);
    }

    private static void ActionFireMissile(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);

        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - 1;

        var game = DoomGame.Instance.Game;
        game.P_SpawnPlayerMissile(player.MapObject!, MapObjectType.MT_ROCKET);
    }

    private static void ActionSaw(ActionParams actionParams)
    {
        var player = actionParams.Player!;

        var damage = 2 * (DoomRandom.P_Random() % 10 + 1);
        var angle = player.MapObject!.Angle;
        angle += new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 18);
        
        // use meleerange + 1 so the puff doesn't skip the flash
        var sawRange = Fixed.FromRaw(Fixed.RawValue(Constants.MeleeRange) + 1);
        var slope = DoomGame.Instance.Game.P_AimLineAttack(player.MapObject, angle, sawRange);
        DoomGame.Instance.Game.P_LineAttack(player.MapObject, angle, sawRange, slope, damage);

        // turn to face target
        var lineTarget = DoomGame.Instance.Game.LineTarget;
        if (lineTarget == null)
        {
            return;
        }
        
        // turn to face target
        angle = DoomGame.Instance.Renderer.PointToAngle2(
                player.MapObject.X,
                player.MapObject.Y,
                lineTarget.X,
                lineTarget.Y);

        if (angle - player.MapObject.Angle > Angle.Angle180)
        {
            if (angle - player.MapObject.Angle < -Angle.Angle90 / 20)
                player.MapObject.Angle = angle + Angle.Angle90 / 21;
            else
                player.MapObject.Angle -= Angle.Angle90 / 20;
        }
        else
        {
            if (angle - player.MapObject.Angle > Angle.Angle90 / 20)
                player.MapObject.Angle = angle - Angle.Angle90 / 21;
            else
                player.MapObject.Angle += Angle.Angle90 / 20;
        }
        player.MapObject.Flags |= MapObjectFlag.MF_JUSTATTACKED;
    }

    private static void ActionFirePlasma(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);

        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - 1;

        var game = DoomGame.Instance.Game;
        
        game.P_SetPlayerFlashSprite(player, weapon.FlashState + (DoomRandom.P_Random() & 1));
        game.P_SpawnPlayerMissile(player.MapObject!, MapObjectType.MT_PLASMA);
    }

    private static void ActionFireBFG(ActionParams actionParams)
    {
        var player = actionParams.Player!;
        var weapon = WeaponInfo.GetByType(player.ReadyWeapon);

        var ammoIndex = (int)weapon.Ammo;
        player.Ammo[ammoIndex] = player.Ammo[ammoIndex] - WeaponInfo.BFGCells;

        var game = DoomGame.Instance.Game;
        game.P_SpawnPlayerMissile(player.MapObject!, MapObjectType.MT_BFG);
    }

    private static void ActionBFGSpray(ActionParams actionParams)
    {
        ContinueBfgSpray(actionParams, 0);
    }

    private static void ContinueBfgSpray(ActionParams actionParams, int startIndex)
    {
        var mo = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        for (var i = startIndex; i < 40; i++)
        {
            var angle90 = Angle.RawValue(Angle.Angle90);
            var an = Angle.FromRaw(Angle.RawValue(mo.Angle) - angle90 / 2 + angle90 / 40 * (uint)i);

            // mo->target is the originator (player) of the missile
            game.P_AimLineAttack(mo.Target!, an, Fixed.FromInt(16 * 64));

            if (game.LineTarget == null)
            {
                continue;
            }

            game.P_SpawnMapObject(
                game.LineTarget.X,
                game.LineTarget.Y,
                game.LineTarget.Z + (game.LineTarget.Height >> 2),
                MapObjectType.MT_EXTRABFG);

            var damage = 0;
            for (var j = 0; j < 15; j++)
            {
                damage += (DoomRandom.P_Random() & 7) + 1;
            }

            if (ScheduleDamageTransition(
                    actionParams,
                    game.LineTarget,
                    mo.Target,
                    mo.Target,
                    damage,
                    MapObjectActionContinuation.BfgSpray,
                    i + 1))
            {
                return;
            }
        }
    }

    private static void ActionExplode(ActionParams actionParams)
    {
        var thingy = actionParams.MapObject!;
        DoomGame.Instance.Game.P_RadiusAttackForAction(actionParams, thingy, thingy.Target!, 128);
    }

    private static void ActionFall(ActionParams actionParams)
    {
        // actor is on the ground, it can be walked over
        actionParams.MapObject!.Flags &= ~MapObjectFlag.MF_SOLID;

        // So change this if corpse objects
        // are meant to be obstacles.
    }

    private static void ActionXScream(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
    }

    /// <summary>
    /// Stay in state until a player is sighted.
    /// </summary>
    private static StateNum ActionLook(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;

        actor.Threshold = 0; // any shot will wake up
        if (!DoomGame.Instance.Game.P_LookForPlayers(actor, false))
        {
            return StateNum.NUMSTATES;
        }

        // go into chase state
        return actor.Info.SeeState;
    }

    /// <summary>
    /// Actor has a melee attack, so it tries to close as fast as possible
    /// </summary>
    private static StateNum ActionChase(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.ReactionTime != 0)
        {
            actor.ReactionTime--;
        }

        // modify target threshold
        if (actor.Threshold != 0)
        {
            if (actor.Target is not { Health: > 0 })
            {
                actor.Threshold = 0;
            }
            else
            {
                actor.Threshold--;
            }
        }

        // turn towards movement direction if not there yet
        if ((int)actor.MoveDir < 8)
        {
            actor.Angle = new Angle(Angle.RawValue(actor.Angle) & (7u << 29));
            var delta = (int)(Angle.RawValue(actor.Angle) - ((uint)actor.MoveDir << 29));

            if (delta > 0)
            {
                actor.Angle = actor.Angle - Angle.Angle90 / 2;
            }
            else if (delta < 0)
            {
                actor.Angle = actor.Angle + Angle.Angle90 / 2;
            }
        }

        if (actor.Target == null || (actor.Target.Flags & MapObjectFlag.MF_SHOOTABLE) == 0)
        {
            // look for a new target
            if (game.P_LookForPlayers(actor, true))
            {
                return StateNum.NUMSTATES; // got a new target
            }

            return actor.Info.SpawnState;
        }

        // do not attack twice in a row
        if ((actor.Flags & MapObjectFlag.MF_JUSTATTACKED) != 0)
        {
            actor.Flags &= ~MapObjectFlag.MF_JUSTATTACKED;
            if (game.GameSkill != SkillLevel.Nightmare && !game.FastParm)
            {
                game.P_NewChaseDir(actor);
            }

            return StateNum.NUMSTATES;
        }

        // check for melee attack
        if (actor.Info.MeleeState != StateNum.S_NULL && game.P_CheckMeleeRange(actor))
        {
            return actor.Info.MeleeState;
        }

        // check for missile attack
                if (actor.Info.MissileState != StateNum.S_NULL)
            {
                if (game.GameSkill < SkillLevel.Nightmare && actor.MoveCount != 0 && !game.FastParm)
                {
                    ActionChaseNoMissile(actor, game);
                    return StateNum.NUMSTATES;
                }

                if (!game.P_CheckMissileRange(actor))
                {
                    ActionChaseNoMissile(actor, game);
                    return StateNum.NUMSTATES;
                }

                // Attack-state actions do not observe MF_JUSTATTACKED; setting the
                // flag before returning the tail transition preserves the first
                // subsequent Chase observation without recursive state dispatch.
                actor.Flags |= MapObjectFlag.MF_JUSTATTACKED;
                return actor.Info.MissileState;
            }

            ActionChaseNoMissile(actor, game);
            return StateNum.NUMSTATES;
        }

        private static void ActionChaseNoMissile(MapObject actor, GameController game)
        {
            // possibly choose another target
            if (actor.Threshold == 0 && !game.P_CheckSight(actor, actor.Target!))
            {
                if (game.P_LookForPlayers(actor, true))
                {
                    return; // got a new target
                }
            }

            // chase towards player
            if (--actor.MoveCount < 0 || !game.P_Move(actor))
            {
                game.P_NewChaseDir(actor);
            }
        }

    private static void ActionFaceTarget(ActionParams actionParams)
        {
            var actor = actionParams.MapObject!;
        if (actor.Target is null)
        {
            return;
        }

        actor.Flags &= ~MapObjectFlag.MF_AMBUSH;
        actor.Angle = DoomGame.Instance.Renderer.PointToAngle2(actor.X, actor.Y, actor.Target.X, actor.Target.Y);

        if ((actor.Target.Flags & MapObjectFlag.MF_SHADOW) != 0)
        {
            actor.Angle += new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 21);
        }
    }

    private static void ActionPosAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        if (actor.Target is null)
        {
            return;
        }

        var game = DoomGame.Instance.Game;
        
        ActionFaceTarget(actionParams);
        
        var angle = actor.Angle;
        var slope = game.P_AimLineAttack(actor, angle, Constants.MissileRange);
        angle += new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 20);
        var damage = ((DoomRandom.P_Random() % 5) + 1) * 3;
        game.P_LineAttackForAction(actionParams, actor, angle, Constants.MissileRange, slope, damage);
    }

    private static void ActionSPosAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        if (actor.Target is null)
        {
            return;
        }

        var game = DoomGame.Instance.Game;
        ActionFaceTarget(actionParams);
        var bangle = actor.Angle;
        var slope = game.P_AimLineAttack(actor, bangle, Constants.MissileRange);

        ContinueSPosAttack(actionParams, new HitscanAttackWork
        {
            BaseAngle = bangle,
            Slope = slope,
            NextIndex = 0
        });
    }

    private static void ContinueSPosAttack(ActionParams actionParams, HitscanAttackWork work)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;
        for (var i = work.NextIndex; i < 3; i++)
        {
            var angle = work.BaseAngle + new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 20);
            var damage = ((DoomRandom.P_Random() % 5) + 1) * 3;
            work.NextIndex = i + 1;
            game.P_LineAttackForAction(
                actionParams,
                actor,
                angle,
                Constants.MissileRange,
                work.Slope,
                damage,
                work);
            if (actionParams.NestedStateMapObject is not null)
                return;
        }
    }

    private static void ActionCPosAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        if (actor.Target is null)
        {
            return;
        }

        var game = DoomGame.Instance.Game;
        ActionFaceTarget(actionParams);
        var bangle = actor.Angle;
        var slope = game.P_AimLineAttack(actor, bangle, Constants.MissileRange);

        var angle = bangle + new Angle((DoomRandom.P_Random() - DoomRandom.P_Random()) << 20);
        var damage = ((DoomRandom.P_Random() % 5) + 1) * 3;
        game.P_LineAttackForAction(actionParams, actor, angle, Constants.MissileRange, slope, damage);
    }

    private static StateNum ActionCPosRefire(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        // keep firing unless target got out of sight
        ActionFaceTarget(actionParams);

        if (DoomRandom.P_Random() < 40)
        {
            return StateNum.NUMSTATES;
        }

        if (actor.Target is null
            || actor.Target.Health <= 0
            || !game.P_CheckSight(actor, actor.Target))
        {
            return actor.Info.SeeState;
        }

        return StateNum.NUMSTATES;
    }

    private static bool ScheduleDamageTransition(
        ActionParams actionParams,
        MapObject target,
        MapObject? inflictor,
        MapObject? source,
        int damage,
        MapObjectActionContinuation continuation = MapObjectActionContinuation.None,
        int continuationIndex = 0)
    {
        var transition = MapObject.DamageMapObjectCore(target, inflictor, source, damage);
        var transitionState = MapObject.DamageTransitionState(transition);
        if (transitionState != StateNum.NUMSTATES)
        {
            actionParams.ScheduleStateTransition(
                target,
                transitionState,
                MapObject.DamageTransitionCompletion(transition),
                continuation,
                continuationIndex);
            return true;
        }

        return false;
    }

    private static StateNum ActionSpidRefire(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        // keep firing unless target got out of sight
        ActionFaceTarget(actionParams);

        if (DoomRandom.P_Random() < 10)
        {
            return StateNum.NUMSTATES;
        }

        if (actor.Target is null
            || actor.Target.Health <= 0
            || !game.P_CheckSight(actor, actor.Target))
        {
            return actor.Info.SeeState;
        }

        return StateNum.NUMSTATES;
    }

    private static void ActionBspiAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        ActionFaceTarget(actionParams);

        // launch a missile
        game.P_SpawnMissileForAction(actor, actor.Target, MapObjectType.MT_ARACHPLAZ, actionParams);
    }

    private static void ActionTroopAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        ActionFaceTarget(actionParams);
        if (game.P_CheckMeleeRange(actor))
        {
            var damage = (DoomRandom.P_Random() % 8 + 1) * 3;
            ScheduleDamageTransition(actionParams, actor.Target, actor, actor, damage);
            return;
        }

        // launch a missile
        game.P_SpawnMissileForAction(actor, actor.Target, MapObjectType.MT_TROOPSHOT, actionParams);
    }

    private static void ActionSargAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        ActionFaceTarget(actionParams);
        if (game.P_CheckMeleeRange(actor))
        {
            var damage = ((DoomRandom.P_Random() % 10) + 1) * 4;
            ScheduleDamageTransition(actionParams, actor.Target, actor, actor, damage);
        }
    }

    private static void ActionHeadAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        ActionFaceTarget(actionParams);
        if (game.P_CheckMeleeRange(actor))
        {
            var damage = (DoomRandom.P_Random() % 6 + 1) * 10;
            ScheduleDamageTransition(actionParams, actor.Target, actor, actor, damage);
            return;
        }

        // launch a missile
        game.P_SpawnMissileForAction(actor, actor.Target, MapObjectType.MT_HEADSHOT, actionParams);
    }

    private static void ActionCyberAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        ActionFaceTarget(actionParams);
        game.P_SpawnMissileForAction(actor, actor.Target, MapObjectType.MT_ROCKET, actionParams);
    }

    private static void ActionBruisAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        if (game.P_CheckMeleeRange(actor))
        {
            var damage = (DoomRandom.P_Random() % 8 + 1) * 10;
            ScheduleDamageTransition(actionParams, actor.Target, actor, actor, damage);
            return;
        }

        // launch a missile
        game.P_SpawnMissileForAction(actor, actor.Target, MapObjectType.MT_BRUISERSHOT, actionParams);
    }

    private static void ActionSkelMissile(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
        {
            return;
        }

        ActionFaceTarget(actionParams);
        actor.Z += Fixed.FromInt(16); // so missile spawns higher
        var mo = game.P_SpawnMissileForAction(
            actor,
            actor.Target,
            MapObjectType.MT_TRACER,
            actionParams,
            MapObjectActionContinuation.SkelMissileAfterSpawn);
        if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
            return;

        ContinueSkelMissile(actionParams, mo);
    }

    private static void ContinueSkelMissile(ActionParams actionParams, MapObject mo)
    {
        var actor = actionParams.MapObject!;
        actor.Z -= Fixed.FromInt(16); // back to normal

        mo.X += mo.MomX;
        mo.Y += mo.MomY;
        mo.Tracer = actor.Target;
    }

    private static void ActionTracer(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if ((game.GameTic & 3) != 0)
        {
            return;
        }

        // spawn a puff of smoke behind the rocket		
        game.P_SpawnPuff(actor.X, actor.Y, actor.Z);

        var th = game.P_SpawnMapObject(actor.X - actor.MomX,
            actor.Y - actor.MomY,
            actor.Z, MapObjectType.MT_SMOKE);

        th.MomZ = Fixed.Unit;
        th.Tics -= DoomRandom.P_Random() & 3;
        if (th.Tics < 1)
        {
            th.Tics = 1;
        }

        // adjust direction
        var dest = actor.Tracer;

        if (dest is not { Health: > 0 })
        {
            return;
        }

        // change angle
        var exact = DoomGame.Instance.Renderer.PointToAngle2(actor.X, actor.Y, dest.X, dest.Y);

        if (exact != actor.Angle)
        {
            if (exact - actor.Angle > Angle.Angle180)
            {
                actor.Angle = actor.Angle - Angle.TraceAngle;
                if (exact - actor.Angle < Angle.Angle180)
                {
                    actor.Angle = exact;
                }
            }
            else
            {
                actor.Angle = actor.Angle + Angle.TraceAngle;
                if (exact - actor.Angle > Angle.Angle180)
                {
                    actor.Angle = exact;
                }
            }
        }

        actor.MomX = new Fixed(actor.Info.Speed) * DoomMath.Cos(actor.Angle);
        actor.MomY = new Fixed(actor.Info.Speed) * DoomMath.Sin(actor.Angle);

        // change slope
        var dist = Fixed.RawValue(game.P_AproxDistance(dest.X - actor.X, dest.Y - actor.Y));

        dist /= actor.Info.Speed;

        if (dist < 1)
        {
            dist = 1;
        }

        var slope = (dest.Z + Fixed.FromInt(40) - actor.Z) / dist;

        var fracOver8 = Fixed.Unit / 8;
        if (slope < actor.MomZ)
        {
            actor.MomZ = actor.MomZ - fracOver8;
        }
        else
        {
            actor.MomZ = actor.MomZ + fracOver8;
        }
    }

    //
    // PIT_VileCheck
    // Detect a corpse that could be raised.
    //
    private static MapObject? _corpsehit;
    private static MapObject? _vileobj;
    private static Fixed _viletryx;
    private static Fixed _viletryy;

    internal static bool PIT_VileCheck(MapObject thing)
    {
        if ((thing.Flags & MapObjectFlag.MF_CORPSE) == 0)
            return true; // not a monster

        if (thing.Tics != -1)
            return true; // not lying still yet

        if (thing.Info.RaiseState == StateNum.S_NULL)
            return true; // monster doesn't have a raise state

        var maxdist = thing.Info.Radius + MapObjectInfo.GetByType(MapObjectType.MT_VILE).Radius;

        if (Fixed.RawValue(Fixed.Abs(thing.X - _viletryx)) > maxdist
            || Fixed.RawValue(Fixed.Abs(thing.Y - _viletryy)) > maxdist)
            return true; // not actually touching

        _corpsehit = thing;
        _corpsehit.MomX = Fixed.Zero;
        _corpsehit.MomY = Fixed.Zero;
        _corpsehit.Height = Fixed.FromRaw(Fixed.RawValue(_corpsehit.Height) << 2);

        var game = DoomGame.Instance.Game;
        var check = game.P_CheckPositionPassive(_corpsehit, _corpsehit.X, _corpsehit.Y);
        _corpsehit.Height = Fixed.FromRaw(Fixed.RawValue(_corpsehit.Height) >> 2);

        if (!check)
            return true; // doesn't fit here

        return false; // got one, so stop checking
    }

    //
    // A_VileChase
    // Check for resurrecting a body
    //
    private static StateNum ActionVileChase(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.MoveDir != DirectionType.NoDirection)
        {
            // check for corpses to raise
            _viletryx =
                actor.X + actor.Info.Speed * Fixed.FromRaw(GameController.XSpeed[(int)actor.MoveDir]);
            _viletryy =
                actor.Y + actor.Info.Speed * Fixed.FromRaw(GameController.YSpeed[(int)actor.MoveDir]);

            var maxRadiusTwice = Fixed.FromRaw(Fixed.RawValue(Constants.MaxRadius) * 2);
            var xl = Fixed.RawValue(_viletryx - game.BlockMapOriginX - maxRadiusTwice) >> Constants.MapBlockShift;
            var xh = Fixed.RawValue(_viletryx - game.BlockMapOriginX + maxRadiusTwice) >> Constants.MapBlockShift;
            var yl = Fixed.RawValue(_viletryy - game.BlockMapOriginY - maxRadiusTwice) >> Constants.MapBlockShift;
            var yh = Fixed.RawValue(_viletryy - game.BlockMapOriginY + maxRadiusTwice) >> Constants.MapBlockShift;

            _vileobj = actor;
            for (var bx = xl; bx <= xh; bx++)
            {
                for (var by = yl; by <= yh; by++)
                {
                    // Call PIT_VileCheck to check
                    // whether object is a corpse
                    // that can be raised.
                    if (!game.P_BlockVileThingsIterator(bx, by))
                    {
                        // got one!
                        var temp = actor.Target;
                        actor.Target = _corpsehit;
                        ActionFaceTarget(actionParams);
                        actor.Target = temp;

                        actor.SetStateWithoutAction(StateNum.S_VILE_HEAL1);
                        var info = _corpsehit!.Info;

                        _corpsehit.SetStateWithoutAction(info.RaiseState);
                        _corpsehit.Height = Fixed.FromRaw(Fixed.RawValue(_corpsehit.Height) << 2);
                        _corpsehit.Flags = info.Flags;
                        _corpsehit.Health = info.SpawnHealth;
                        _corpsehit.Target = null;

                        return StateNum.NUMSTATES;
                    }
                }
            }
        }

        // Return to normal attack.
        return ActionChase(actionParams);
    }

    //
    // A_VileStart
    //
    private static void ActionVileStart(ActionParams actionParams)
    {
    }

    //
    // A_VileTarget
    // Spawn the hellfire
    //
    private static void ActionVileTarget(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
            return;

        ActionFaceTarget(actionParams);

        var fog = game.P_SpawnMapObject(actor.Target.X,
                                        actor.Target.X, // NOTE: yes, this is actor->target->x in the original (bug in original DOOM)
                                        actor.Target.Z, MapObjectType.MT_FIRE);

        actor.Tracer = fog;
        fog.Target = actor;
        fog.Tracer = actor.Target;
        ActionFire(new ActionParams(fog));
    }

    //
    // A_VileAttack
    //
    private static void ActionVileAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (actor.Target is null)
            return;

        ActionFaceTarget(actionParams);

        if (!game.P_CheckSight(actor, actor.Target))
            return;
        if (ScheduleDamageTransition(
                actionParams,
                actor.Target,
                actor,
                actor,
                20,
                MapObjectActionContinuation.VileAfterDamage))
        {
            return;
        }

        ContinueVileAttack(actionParams);
    }

    private static void ContinueVileAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;
        if (actor.Target is null)
            return;

        actor.Target.MomZ = new Fixed(1000 * Constants.FracUnit / actor.Target.Info.Mass);

        var an = (int)(Angle.RawValue(actor.Angle) >> DoomMath.AngleToFineShift);

        var fire = actor.Tracer;

        if (fire is null)
            return;

        // move the fire between the vile and the player
        fire.X = actor.Target.X - Fixed.FromInt(24) * DoomMath.Cos(an);
        fire.Y = actor.Target.Y - Fixed.FromInt(24) * DoomMath.Sin(an);
        game.P_RadiusAttackForAction(actionParams, fire, actor, 70);
    }

    //
    // A_StartFire
    //
    private static void ActionStartFire(ActionParams actionParams)
    {
        ActionFire(actionParams);
    }

    //
    // A_FireCrackle
    //
    private static void ActionFireCrackle(ActionParams actionParams)
    {
        ActionFire(actionParams);
    }

    //
    // A_Fire
    // Keep fire in front of player unless out of sight
    //
    private static void ActionFire(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        var dest = actor.Tracer;
        if (dest is null)
            return;

        // don't move it if the vile lost sight
        if (!game.P_CheckSight(actor.Target!, dest))
            return;

        var an = (int)(Angle.RawValue(dest.Angle) >> DoomMath.AngleToFineShift);

        game.P_UnsetThingPosition(actor);
        actor.X = dest.X + Fixed.FromInt(24) * DoomMath.Cos(an);
        actor.Y = dest.Y + Fixed.FromInt(24) * DoomMath.Sin(an);
        actor.Z = dest.Z;
        game.P_SetThingPosition(actor);
    }

    //
    // A_SkelWhoosh
    //
    private static void ActionSkelWhoosh(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        if (actor.Target is null)
            return;
        ActionFaceTarget(actionParams);
    }

    //
    // A_SkelFist
    //
    private static void ActionSkelFist(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        if (actor.Target is null)
            return;

        ActionFaceTarget(actionParams);

        if (DoomGame.Instance.Game.P_CheckMeleeRange(actor))
        {
            var damage = ((DoomRandom.P_Random() % 10) + 1) * 6;
            ScheduleDamageTransition(actionParams, actor.Target, actor, actor, damage);
        }
    }

    //
    // A_FatRaise
    //
    private static void ActionFatRaise(ActionParams actionParams)
    {
        ActionFaceTarget(actionParams);
    }

    private static readonly Angle FatSpread = Angle.Angle90 / 8;

    //
    // A_FatAttack1
    //
    private static void ActionFatAttack1(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        ActionFaceTarget(actionParams);
        // Change direction to ...
        actor.Angle = actor.Angle + FatSpread;
        ContinueFatAttack1(actionParams, 0, null);
    }

    private static void ContinueFatAttack1(ActionParams actionParams, int step, MapObject? missile)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;
        if (step == 0)
        {
            game.P_SpawnMissileForAction(
                actor, actor.Target!, MapObjectType.MT_FATSHOT, actionParams,
                MapObjectActionContinuation.FatAttack1, 1);
            if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
                return;
        }

        if (step <= 1)
        {
            missile = game.P_SpawnMissileForAction(
                actor, actor.Target!, MapObjectType.MT_FATSHOT, actionParams,
                MapObjectActionContinuation.FatAttack1, 2);
            if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
                return;
        }

        AdjustFatShot(missile!, FatSpread);
    }

    //
    // A_FatAttack2
    //
    private static void ActionFatAttack2(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        ActionFaceTarget(actionParams);
        // Now here choose opposite deviation.
        actor.Angle = actor.Angle - FatSpread;
        ContinueFatAttack2(actionParams, 0, null);
    }

    private static void ContinueFatAttack2(ActionParams actionParams, int step, MapObject? missile)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;
        if (step == 0)
        {
            game.P_SpawnMissileForAction(
                actor, actor.Target!, MapObjectType.MT_FATSHOT, actionParams,
                MapObjectActionContinuation.FatAttack2, 1);
            if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
                return;
        }

        if (step <= 1)
        {
            missile = game.P_SpawnMissileForAction(
                actor, actor.Target!, MapObjectType.MT_FATSHOT, actionParams,
                MapObjectActionContinuation.FatAttack2, 2);
            if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
                return;
        }

        AdjustFatShot(missile!, -FatSpread * 2);
    }

    //
    // A_FatAttack3
    //
    private static void ActionFatAttack3(ActionParams actionParams)
    {
        ActionFaceTarget(actionParams);
        ContinueFatAttack3(actionParams, 0, null);
    }

    private static void ContinueFatAttack3(ActionParams actionParams, int step, MapObject? missile)
    {
        var actor = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;
        if (step == 0)
        {
            missile = game.P_SpawnMissileForAction(
                actor, actor.Target!, MapObjectType.MT_FATSHOT, actionParams,
                MapObjectActionContinuation.FatAttack3, 1);
            if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
                return;
        }
        if (step <= 1)
        {
            AdjustFatShot(missile!, -FatSpread / 2);
            missile = game.P_SpawnMissileForAction(
                actor, actor.Target!, MapObjectType.MT_FATSHOT, actionParams,
                MapObjectActionContinuation.FatAttack3, 2);
            if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
                return;
        }

        AdjustFatShot(missile!, FatSpread / 2);
    }

    private static void AdjustFatShot(MapObject mo, Angle spread)
    {
        mo.Angle = mo.Angle + spread;
        var an = (int)(Angle.RawValue(mo.Angle) >> DoomMath.AngleToFineShift);
        mo.MomX = new Fixed(mo.Info.Speed) * DoomMath.Cos(an);
        mo.MomY = new Fixed(mo.Info.Speed) * DoomMath.Sin(an);
    }

    //
    // A_BossDeath
    // Possibly trigger special effects
    // if on first boss level
    //
    private static void ActionBossDeath(ActionParams actionParams)
    {
        var mo = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (DoomGame.Instance.GameMode == GameMode.Commercial)
        {
            if (game.GameMap != 7)
                return;

            if (mo.Type != MapObjectType.MT_FATSO
                && mo.Type != MapObjectType.MT_BABY)
                return;
        }
        else
        {
            switch (game.GameEpisode)
            {
                case 1:
                    if (game.GameMap != 8)
                        return;
                    if (mo.Type != MapObjectType.MT_BRUISER)
                        return;
                    break;

                case 2:
                    if (game.GameMap != 8)
                        return;
                    if (mo.Type != MapObjectType.MT_CYBORG)
                        return;
                    break;

                case 3:
                    if (game.GameMap != 8)
                        return;
                    if (mo.Type != MapObjectType.MT_SPIDER)
                        return;
                    break;

                case 4:
                    switch (game.GameMap)
                    {
                        case 6:
                            if (mo.Type != MapObjectType.MT_CYBORG)
                                return;
                            break;

                        case 8:
                            if (mo.Type != MapObjectType.MT_SPIDER)
                                return;
                            break;

                        default:
                            return;
                    }
                    break;

                default:
                    if (game.GameMap != 8)
                        return;
                    break;
            }
        }

        // make sure there is a player alive for victory
        int i;
        for (i = 0; i < Constants.MaxPlayers; i++)
        {
            if (game.PlayerInGame[i] && game.Players[i].Health > 0)
                break;
        }

        if (i == Constants.MaxPlayers)
            return; // no one left alive, so do not end game

        // scan the remaining thinkers to see
        // if all bosses are dead
        var thinker = game.Thinkers;
        while (thinker != null)
        {
            if (thinker is not MapObject mo2)
            {
                thinker = thinker.Next;
                continue;
            }

            if (mo2 != mo
                && mo2.Type == mo.Type
                && mo2.Health > 0)
            {
                // other boss not dead
                return;
            }

            thinker = thinker.Next;
        }

        // victory!
        var dummyVertex = new Vertex(Fixed.Zero, Fixed.Zero);

        if (DoomGame.Instance.GameMode == GameMode.Commercial)
        {
            if (game.GameMap == 7)
            {
                if (mo.Type == MapObjectType.MT_FATSO)
                {
                    var junk = new Line(dummyVertex, dummyVertex, 0, 0, 666);
                    Trigger.FloorEvent(junk, FloorType.LowerFloorToLowest);
                    return;
                }

                if (mo.Type == MapObjectType.MT_BABY)
                {
                    var junk = new Line(dummyVertex, dummyVertex, 0, 0, 667);
                    Trigger.FloorEvent(junk, FloorType.RaiseToTexture);
                    return;
                }
            }
        }
        else
        {
            switch (game.GameEpisode)
            {
                case 1:
                {
                    var junk = new Line(dummyVertex, dummyVertex, 0, 0, 666);
                    Trigger.FloorEvent(junk, FloorType.LowerFloorToLowest);
                    return;
                }

                case 4:
                    switch (game.GameMap)
                    {
                        case 6:
                        {
                            var junk = new Line(dummyVertex, dummyVertex, 0, 0, 666);
                            Trigger.DoorEvent(junk, DoorType.BlazeOpen);
                            return;
                        }

                        case 8:
                        {
                            var junk = new Line(dummyVertex, dummyVertex, 0, 0, 666);
                            Trigger.FloorEvent(junk, FloorType.LowerFloorToLowest);
                            return;
                        }
                    }
                    break;
            }
        }

        game.ExitLevel();
    }

    //
    // SkullAttack
    // Fly at the player like a missile.
    //
    private const int SkullSpeed = 20 * Constants.FracUnit;

    private static void ActionSkullAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;

        if (actor.Target is null)
            return;

        var dest = actor.Target;
        actor.Flags |= MapObjectFlag.MF_SKULLFLY;
        ActionFaceTarget(actionParams);
        var an = (int)(Angle.RawValue(actor.Angle) >> DoomMath.AngleToFineShift);
        actor.MomX = new Fixed(SkullSpeed) * DoomMath.Cos(an);
        actor.MomY = new Fixed(SkullSpeed) * DoomMath.Sin(an);
        var dist = Fixed.RawValue(DoomGame.Instance.Game.P_AproxDistance(dest.X - actor.X, dest.Y - actor.Y));
        dist /= SkullSpeed;

        if (dist < 1)
            dist = 1;
        actor.MomZ = Fixed.FromRaw((Fixed.RawValue(dest.Z) + (Fixed.RawValue(dest.Height) >> 1) - Fixed.RawValue(actor.Z)) / dist);
    }

    //
    // A_PainShootSkull
    // Spawn a lost soul and launch it at the target
    //
    private static bool PainShootSkull(
        ActionParams actionParams,
        MapObject actor,
        Angle angle,
        MapObjectActionContinuation continuation = MapObjectActionContinuation.None,
        int continuationIndex = 0)
    {
        var game = DoomGame.Instance.Game;

        // count total number of skulls currently on the level
        var count = 0;

        var thinker = game.Thinkers;
        while (thinker != null)
        {
            if (thinker is MapObject mo && mo.Type == MapObjectType.MT_SKULL)
                count++;

            thinker = thinker.Next;
        }

        // if there are already 20 skulls on the level,
        // don't spit another one
        if (count > 20)
            return false;

        // okay, there's place for another one
        var an = (int)(Angle.RawValue(angle) >> DoomMath.AngleToFineShift);

        var prestep =
            4 * Constants.FracUnit
            + 3 * (actor.Info.Radius + MapObjectInfo.GetByType(MapObjectType.MT_SKULL).Radius) / 2;

        var x = actor.X + new Fixed(prestep) * DoomMath.Cos(an);
        var y = actor.Y + new Fixed(prestep) * DoomMath.Sin(an);
        var z = actor.Z + Fixed.FromInt(8);

        var newmobj = game.P_SpawnMapObject(x, y, z, MapObjectType.MT_SKULL);

        // Check for movements.
        if (!game.P_TryMoveAtCurrentPositionPassive(newmobj))
        {
            // kill it immediately
            return ScheduleDamageTransition(
                actionParams,
                newmobj,
                actor,
                actor,
                10000,
                continuation,
                continuationIndex);
        }

        newmobj.Target = actor.Target;
        ActionSkullAttack(new ActionParams(newmobj));
        return false;
    }

    //
    // A_PainAttack
    // Spawn a lost soul and launch it at the target
    //
    private static void ActionPainAttack(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        if (actor.Target is null)
            return;

        ActionFaceTarget(actionParams);
        PainShootSkull(actionParams, actor, actor.Angle);
    }

    //
    // A_PainDie
    //
    private static void ActionPainDie(ActionParams actionParams)
    {
        var actor = actionParams.MapObject!;
        ActionFall(actionParams);
        ContinuePainDie(actionParams, 0);
    }

    private static void ContinuePainDie(ActionParams actionParams, int startIndex)
    {
        var actor = actionParams.MapObject!;
        for (var i = startIndex; i < 3; i++)
        {
            var angle = i switch
            {
                0 => actor.Angle + Angle.Angle90,
                1 => actor.Angle + Angle.Angle180,
                _ => actor.Angle + Angle.Angle270
            };
            if (PainShootSkull(
                    actionParams,
                    actor,
                    angle,
                    MapObjectActionContinuation.PainDie,
                    i + 1))
            {
                return;
            }
        }
    }

    private static StateNum ActionMetal(ActionParams actionParams)
    {
        return ActionChase(actionParams);
    }

    private static StateNum ActionBabyMetal(ActionParams actionParams)
    {
        return ActionChase(actionParams);
    }

    private static StateNum ActionHoof(ActionParams actionParams)
    {
        return ActionChase(actionParams);
    }

    //
    // A_KeenDie
    // DOOM II special, map 32.
    // Uses special tag 666.
    //
    private static void ActionKeenDie(ActionParams actionParams)
    {
        var mo = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        ActionFall(actionParams);

        // scan the remaining thinkers
        // to see if all Keens are dead
        var thinker = game.Thinkers;
        while (thinker != null)
        {
            if (thinker is not MapObject mo2)
                continue;

            if (mo2 != mo
                && mo2.Type == mo.Type
                && mo2.Health > 0)
            {
                // other Keen not dead
                return;
            }

            thinker = thinker.Next;
        }

        var dummyVertex = new Vertex(Fixed.Zero, Fixed.Zero);
        var junk = new Line(dummyVertex, dummyVertex, 0, 0, 666);
        Trigger.DoorEvent(junk, DoorType.Open);
    }

    //
    // Icon of Sin / Boss Brain
    //
    private static MapObject?[] _brainTargets = new MapObject?[32];
    private static int _numBrainTargets;
    private static int _brainTargetOn;

    private static void ActionBrainAwake(ActionParams actionParams)
    {
        var game = DoomGame.Instance.Game;

        // find all the target spots
        _numBrainTargets = 0;
        _brainTargetOn = 0;

        var thinker = game.Thinkers;
        while (thinker != null)
        {
            if (thinker is not MapObject m)
            {
                thinker = thinker.Next;
                continue;
            }

            if (m.Type == MapObjectType.MT_BOSSTARGET)
            {
                _brainTargets[_numBrainTargets] = m;
                _numBrainTargets++;
            }

            thinker = thinker.Next;
        }
    }

    private static void ActionBrainPain(ActionParams actionParams)
    {
    }

    private static void ActionBrainScream(ActionParams actionParams)
    {
        var mo = actionParams.MapObject!;

        var moX = Fixed.RawValue(mo.X);
        for (var x = moX - 196 * Constants.FracUnit; x < moX + 320 * Constants.FracUnit; x += Constants.FracUnit * 8)
        {
            var y = Fixed.RawValue(mo.Y) - 320 * Constants.FracUnit;
            var z = 128 + DoomRandom.P_Random() * 2 * Constants.FracUnit;
            var th = DoomGame.Instance.Game.P_SpawnMapObject(new Fixed(x), new Fixed(y), new Fixed(z), MapObjectType.MT_ROCKET);
            th.MomZ = new Fixed(DoomRandom.P_Random() * 512);

            th.SetStateWithoutAction(StateNum.S_BRAINEXPLODE1);

            th.Tics -= DoomRandom.P_Random() & 7;
            if (th.Tics < 1)
                th.Tics = 1;
        }
    }

    private static void ActionBrainExplode(ActionParams actionParams)
    {
        var mo = actionParams.MapObject!;

        var x = Fixed.RawValue(mo.X) + (DoomRandom.P_Random() - DoomRandom.P_Random()) * 2048;
        var y = Fixed.RawValue(mo.Y);
        var z = 128 + DoomRandom.P_Random() * 2 * Constants.FracUnit;
        var th = DoomGame.Instance.Game.P_SpawnMapObject(new Fixed(x), new Fixed(y), new Fixed(z), MapObjectType.MT_ROCKET);
        th.MomZ = new Fixed(DoomRandom.P_Random() * 512);

        th.SetStateWithoutAction(StateNum.S_BRAINEXPLODE1);

        th.Tics -= DoomRandom.P_Random() & 7;
        if (th.Tics < 1)
            th.Tics = 1;
    }

    private static void ActionBrainDie(ActionParams actionParams)
    {
        DoomGame.Instance.Game.ExitLevel();
    }

    private static int _easy;

    private static void ActionBrainSpit(ActionParams actionParams)
    {
        var game = DoomGame.Instance.Game;

        _easy ^= 1;
        if (game.GameSkill <= SkillLevel.Easy && (_easy == 0))
            return;

        // shoot a cube at current target
        var targ = _brainTargets[_brainTargetOn]!;
        _brainTargetOn = (_brainTargetOn + 1) % _numBrainTargets;

        // spawn brain missile
        var newmobj = game.P_SpawnMissileForAction(
            actionParams.MapObject!,
            targ,
            MapObjectType.MT_SPAWNSHOT,
            actionParams,
            MapObjectActionContinuation.BrainSpitAfterSpawn,
            continuationMapObject2: targ);
        if (actionParams.NestedStateMapObject is not null || actionParams.HasDeferredContinuation)
            return;

        ContinueBrainSpit(actionParams, newmobj, targ);
    }

    private static void ContinueBrainSpit(ActionParams actionParams, MapObject newmobj, MapObject targ)
    {
        newmobj.Target = targ;
        newmobj.ReactionTime =
            ((Fixed.RawValue(targ.Y) - Fixed.RawValue(actionParams.MapObject!.Y)) / Fixed.RawValue(newmobj.MomY)) / newmobj.State!.Tics;
    }

    private static void ActionSpawnFly(ActionParams actionParams)
    {
        var mo = actionParams.MapObject!;
        var game = DoomGame.Instance.Game;

        if (--mo.ReactionTime != 0)
            return; // still flying

        var targ = mo.Target!;

        // First spawn teleport fog.
        var fog = game.P_SpawnMapObject(targ.X, targ.Y, targ.Z, MapObjectType.MT_SPAWNFIRE);

        // Randomly select monster to spawn.
        var r = DoomRandom.P_Random();

        // Probability distribution (kind of :),
        // decreasing likelihood.
        MapObjectType type;
        if (r < 50)
            type = MapObjectType.MT_TROOP;
        else if (r < 90)
            type = MapObjectType.MT_SERGEANT;
        else if (r < 120)
            type = MapObjectType.MT_SHADOWS;
        else if (r < 130)
            type = MapObjectType.MT_PAIN;
        else if (r < 160)
            type = MapObjectType.MT_HEAD;
        else if (r < 162)
            type = MapObjectType.MT_VILE;
        else if (r < 172)
            type = MapObjectType.MT_UNDEAD;
        else if (r < 192)
            type = MapObjectType.MT_BABY;
        else if (r < 222)
            type = MapObjectType.MT_FATSO;
        else if (r < 246)
            type = MapObjectType.MT_KNIGHT;
        else
            type = MapObjectType.MT_BRUISER;

        var newmobj = game.P_SpawnMapObject(targ.X, targ.Y, targ.Z, type);
        if (game.P_LookForPlayers(newmobj, true))
        {
            actionParams.ScheduleSpawnFlyTransition(newmobj, newmobj.Info.SeeState, mo);
            return;
        }

        // Telefrag traversal may enter a victim state action. Keep it on the
        // explicit action work stack instead of recursing through SetState.
        game.P_TeleportMoveForAction(actionParams, newmobj, newmobj.X, newmobj.Y, mo);
    }

    private static void AddPredefinedStates()
    {
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_NULL
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 4, 0, StateAction.Light0, StateNum.S_NULL, 0, 0));  // S_LIGHTDONE
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 0, 1, StateAction.WeaponReady, StateNum.S_PUNCH, 0, 0));    // S_PUNCH
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 0, 1, StateAction.Lower, StateNum.S_PUNCHDOWN, 0, 0));  // S_PUNCHDOWN
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 0, 1, StateAction.Raise, StateNum.S_PUNCHUP, 0, 0)); // S_PUNCHUP
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 1, 4, StateAction.None, StateNum.S_PUNCH2, 0, 0));        // S_PUNCH1
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 2, 4, StateAction.Punch, StateNum.S_PUNCH3, 0, 0)); // S_PUNCH2
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 3, 5, StateAction.None, StateNum.S_PUNCH4, 0, 0));     // S_PUNCH3
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 2, 4, StateAction.None, StateNum.S_PUNCH5, 0, 0));     // S_PUNCH4
        AddPredefinedState(new State(SpriteNum.SPR_PUNG, 1, 5, StateAction.ReFire, StateNum.S_PUNCH, 0, 0)); // S_PUNCH5
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 0, 1, StateAction.WeaponReady, StateNum.S_PISTOL, 0, 0));// S_PISTOL
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 0, 1, StateAction.Lower, StateNum.S_PISTOLDOWN, 0, 0)); // S_PISTOLDOWN
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 0, 1, StateAction.Raise, StateNum.S_PISTOLUP, 0, 0));   // S_PISTOLUP
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 0, 4, StateAction.None, StateNum.S_PISTOL2, 0, 0));   // S_PISTOL1
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 1, 6, StateAction.FirePistol, StateNum.S_PISTOL3, 0, 0));// S_PISTOL2
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 2, 4, StateAction.None, StateNum.S_PISTOL4, 0, 0));   // S_PISTOL3
        AddPredefinedState(new State(SpriteNum.SPR_PISG, 1, 5, StateAction.ReFire, StateNum.S_PISTOL, 0, 0));    // S_PISTOL4
        AddPredefinedState(new State(SpriteNum.SPR_PISF, 32768, 7, StateAction.Light1, StateNum.S_LIGHTDONE, 0, 0)); // S_PISTOLFLASH
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 1, StateAction.WeaponReady, StateNum.S_SGUN, 0, 0)); // S_SGUN
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 1, StateAction.Lower, StateNum.S_SGUNDOWN, 0, 0));   // S_SGUNDOWN
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 1, StateAction.Raise, StateNum.S_SGUNUP, 0, 0)); // S_SGUNUP
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 3, StateAction.None, StateNum.S_SGUN2, 0, 0)); // S_SGUN1
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 7, StateAction.FireShotgun, StateNum.S_SGUN3, 0, 0));    // S_SGUN2
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 1, 5, StateAction.None, StateNum.S_SGUN4, 0, 0)); // S_SGUN3
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 2, 5, StateAction.None, StateNum.S_SGUN5, 0, 0)); // S_SGUN4
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 3, 4, StateAction.None, StateNum.S_SGUN6, 0, 0)); // S_SGUN5
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 2, 5, StateAction.None, StateNum.S_SGUN7, 0, 0)); // S_SGUN6
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 1, 5, StateAction.None, StateNum.S_SGUN8, 0, 0)); // S_SGUN7
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 3, StateAction.None, StateNum.S_SGUN9, 0, 0)); // S_SGUN8
        AddPredefinedState(new State(SpriteNum.SPR_SHTG, 0, 7, StateAction.ReFire, StateNum.S_SGUN, 0, 0));  // S_SGUN9
        AddPredefinedState(new State(SpriteNum.SPR_SHTF, 32768, 4, StateAction.Light1, StateNum.S_SGUNFLASH2, 0, 0));    // S_SGUNFLASH1
        AddPredefinedState(new State(SpriteNum.SPR_SHTF, 32769, 3, StateAction.Light2, StateNum.S_LIGHTDONE, 0, 0)); // S_SGUNFLASH2
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 1, StateAction.WeaponReady, StateNum.S_DSGUN, 0, 0));    // S_DSGUN
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 1, StateAction.Lower, StateNum.S_DSGUNDOWN, 0, 0));  // S_DSGUNDOWN
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 1, StateAction.Raise, StateNum.S_DSGUNUP, 0, 0));    // S_DSGUNUP
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 3, StateAction.None, StateNum.S_DSGUN2, 0, 0));    // S_DSGUN1
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 7, StateAction.FireShotgun2, StateNum.S_DSGUN3, 0, 0));  // S_DSGUN2
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 1, 7, StateAction.None, StateNum.S_DSGUN4, 0, 0));    // S_DSGUN3
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 2, 7, StateAction.CheckReload, StateNum.S_DSGUN5, 0, 0));   // S_DSGUN4
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 3, 7, StateAction.OpenShotgun2, StateNum.S_DSGUN6, 0, 0));  // S_DSGUN5
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 4, 7, StateAction.None, StateNum.S_DSGUN7, 0, 0));    // S_DSGUN6
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 5, 7, StateAction.LoadShotgun2, StateNum.S_DSGUN8, 0, 0));  // S_DSGUN7
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 6, 6, StateAction.None, StateNum.S_DSGUN9, 0, 0));    // S_DSGUN8
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 7, 6, StateAction.CloseShotgun2, StateNum.S_DSGUN10, 0, 0));    // S_DSGUN9
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 5, StateAction.ReFire, StateNum.S_DSGUN, 0, 0)); // S_DSGUN10
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 1, 7, StateAction.None, StateNum.S_DSNR2, 0, 0)); // S_DSNR1
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 0, 3, StateAction.None, StateNum.S_DSGUNDOWN, 0, 0)); // S_DSNR2
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 32776, 5, StateAction.Light1, StateNum.S_DSGUNFLASH2, 0, 0));   // S_DSGUNFLASH1
        AddPredefinedState(new State(SpriteNum.SPR_SHT2, 32777, 4, StateAction.Light2, StateNum.S_LIGHTDONE, 0, 0)); // S_DSGUNFLASH2
        AddPredefinedState(new State(SpriteNum.SPR_CHGG, 0, 1, StateAction.WeaponReady, StateNum.S_CHAIN, 0, 0));    // S_CHAIN
        AddPredefinedState(new State(SpriteNum.SPR_CHGG, 0, 1, StateAction.Lower, StateNum.S_CHAINDOWN, 0, 0));  // S_CHAINDOWN
        AddPredefinedState(new State(SpriteNum.SPR_CHGG, 0, 1, StateAction.Raise, StateNum.S_CHAINUP, 0, 0));    // S_CHAINUP
        AddPredefinedState(new State(SpriteNum.SPR_CHGG, 0, 4, StateAction.FireCGun, StateNum.S_CHAIN2, 0, 0));  // S_CHAIN1
        AddPredefinedState(new State(SpriteNum.SPR_CHGG, 1, 4, StateAction.FireCGun, StateNum.S_CHAIN3, 0, 0));  // S_CHAIN2
        AddPredefinedState(new State(SpriteNum.SPR_CHGG, 1, 0, StateAction.ReFire, StateNum.S_CHAIN, 0, 0)); // S_CHAIN3
        AddPredefinedState(new State(SpriteNum.SPR_CHGF, 32768, 5, StateAction.Light1, StateNum.S_LIGHTDONE, 0, 0)); // S_CHAINFLASH1
        AddPredefinedState(new State(SpriteNum.SPR_CHGF, 32769, 5, StateAction.Light2, StateNum.S_LIGHTDONE, 0, 0)); // S_CHAINFLASH2
        AddPredefinedState(new State(SpriteNum.SPR_MISG, 0, 1, StateAction.WeaponReady, StateNum.S_MISSILE, 0, 0));  // S_MISSILE
        AddPredefinedState(new State(SpriteNum.SPR_MISG, 0, 1, StateAction.Lower, StateNum.S_MISSILEDOWN, 0, 0));    // S_MISSILEDOWN
        AddPredefinedState(new State(SpriteNum.SPR_MISG, 0, 1, StateAction.Raise, StateNum.S_MISSILEUP, 0, 0));  // S_MISSILEUP
        AddPredefinedState(new State(SpriteNum.SPR_MISG, 1, 8, StateAction.GunFlash, StateNum.S_MISSILE2, 0, 0));    // S_MISSILE1
        AddPredefinedState(new State(SpriteNum.SPR_MISG, 1, 12, StateAction.FireMissile, StateNum.S_MISSILE3, 0, 0));    // S_MISSILE2
        AddPredefinedState(new State(SpriteNum.SPR_MISG, 1, 0, StateAction.ReFire, StateNum.S_MISSILE, 0, 0));   // S_MISSILE3
        AddPredefinedState(new State(SpriteNum.SPR_MISF, 32768, 3, StateAction.Light1, StateNum.S_MISSILEFLASH2, 0, 0)); // S_MISSILEFLASH1
        AddPredefinedState(new State(SpriteNum.SPR_MISF, 32769, 4, StateAction.None, StateNum.S_MISSILEFLASH3, 0, 0)); // S_MISSILEFLASH2
        AddPredefinedState(new State(SpriteNum.SPR_MISF, 32770, 4, StateAction.Light2, StateNum.S_MISSILEFLASH4, 0, 0)); // S_MISSILEFLASH3
        AddPredefinedState(new State(SpriteNum.SPR_MISF, 32771, 4, StateAction.Light2, StateNum.S_LIGHTDONE, 0, 0)); // S_MISSILEFLASH4
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 2, 4, StateAction.WeaponReady, StateNum.S_SAWB, 0, 0)); // S_SAW
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 3, 4, StateAction.WeaponReady, StateNum.S_SAW, 0, 0));  // S_SAWB
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 2, 1, StateAction.Lower, StateNum.S_SAWDOWN, 0, 0));    // S_SAWDOWN
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 2, 1, StateAction.Raise, StateNum.S_SAWUP, 0, 0));  // S_SAWUP
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 0, 4, StateAction.Saw, StateNum.S_SAW2, 0, 0)); // S_SAW1
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 1, 4, StateAction.Saw, StateNum.S_SAW3, 0, 0)); // S_SAW2
        AddPredefinedState(new State(SpriteNum.SPR_SAWG, 1, 0, StateAction.ReFire, StateNum.S_SAW, 0, 0));   // S_SAW3
        AddPredefinedState(new State(SpriteNum.SPR_PLSG, 0, 1, StateAction.WeaponReady, StateNum.S_PLASMA, 0, 0));   // S_PLASMA
        AddPredefinedState(new State(SpriteNum.SPR_PLSG, 0, 1, StateAction.Lower, StateNum.S_PLASMADOWN, 0, 0)); // S_PLASMADOWN
        AddPredefinedState(new State(SpriteNum.SPR_PLSG, 0, 1, StateAction.Raise, StateNum.S_PLASMAUP, 0, 0));   // S_PLASMAUP
        AddPredefinedState(new State(SpriteNum.SPR_PLSG, 0, 3, StateAction.FirePlasma, StateNum.S_PLASMA2, 0, 0));   // S_PLASMA1
        AddPredefinedState(new State(SpriteNum.SPR_PLSG, 1, 20, StateAction.ReFire, StateNum.S_PLASMA, 0, 0));   // S_PLASMA2
        AddPredefinedState(new State(SpriteNum.SPR_PLSF, 32768, 4, StateAction.Light1, StateNum.S_LIGHTDONE, 0, 0)); // S_PLASMAFLASH1
        AddPredefinedState(new State(SpriteNum.SPR_PLSF, 32769, 4, StateAction.Light1, StateNum.S_LIGHTDONE, 0, 0)); // S_PLASMAFLASH2
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 0, 1, StateAction.WeaponReady, StateNum.S_BFG, 0, 0));  // S_BFG
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 0, 1, StateAction.Lower, StateNum.S_BFGDOWN, 0, 0));    // S_BFGDOWN
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 0, 1, StateAction.Raise, StateNum.S_BFGUP, 0, 0));  // S_BFGUP
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 0, 20, StateAction.None, StateNum.S_BFG2, 0, 0));   // S_BFG1
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 1, 10, StateAction.GunFlash, StateNum.S_BFG3, 0, 0));   // S_BFG2
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 1, 10, StateAction.FireBFG, StateNum.S_BFG4, 0, 0));    // S_BFG3
        AddPredefinedState(new State(SpriteNum.SPR_BFGG, 1, 20, StateAction.ReFire, StateNum.S_BFG, 0, 0));  // S_BFG4
        AddPredefinedState(new State(SpriteNum.SPR_BFGF, 32768, 11, StateAction.Light1, StateNum.S_BFGFLASH2, 0, 0));    // S_BFGFLASH1
        AddPredefinedState(new State(SpriteNum.SPR_BFGF, 32769, 6, StateAction.Light2, StateNum.S_LIGHTDONE, 0, 0)); // S_BFGFLASH2
        AddPredefinedState(new State(SpriteNum.SPR_BLUD, 2, 8, StateAction.None, StateNum.S_BLOOD2, 0, 0));    // S_BLOOD1
        AddPredefinedState(new State(SpriteNum.SPR_BLUD, 1, 8, StateAction.None, StateNum.S_BLOOD3, 0, 0));    // S_BLOOD2
        AddPredefinedState(new State(SpriteNum.SPR_BLUD, 0, 8, StateAction.None, StateNum.S_NULL, 0, 0));  // S_BLOOD3
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 32768, 4, StateAction.None, StateNum.S_PUFF2, 0, 0)); // S_PUFF1
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 1, 4, StateAction.None, StateNum.S_PUFF3, 0, 0)); // S_PUFF2
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 2, 4, StateAction.None, StateNum.S_PUFF4, 0, 0)); // S_PUFF3
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 3, 4, StateAction.None, StateNum.S_NULL, 0, 0));  // S_PUFF4
        AddPredefinedState(new State(SpriteNum.SPR_BAL1, 32768, 4, StateAction.None, StateNum.S_TBALL2, 0, 0));    // S_TBALL1
        AddPredefinedState(new State(SpriteNum.SPR_BAL1, 32769, 4, StateAction.None, StateNum.S_TBALL1, 0, 0));    // S_TBALL2
        AddPredefinedState(new State(SpriteNum.SPR_BAL1, 32770, 6, StateAction.None, StateNum.S_TBALLX2, 0, 0));   // S_TBALLX1
        AddPredefinedState(new State(SpriteNum.SPR_BAL1, 32771, 6, StateAction.None, StateNum.S_TBALLX3, 0, 0));   // S_TBALLX2
        AddPredefinedState(new State(SpriteNum.SPR_BAL1, 32772, 6, StateAction.None, StateNum.S_NULL, 0, 0));  // S_TBALLX3
        AddPredefinedState(new State(SpriteNum.SPR_BAL2, 32768, 4, StateAction.None, StateNum.S_RBALL2, 0, 0));    // S_RBALL1
        AddPredefinedState(new State(SpriteNum.SPR_BAL2, 32769, 4, StateAction.None, StateNum.S_RBALL1, 0, 0));    // S_RBALL2
        AddPredefinedState(new State(SpriteNum.SPR_BAL2, 32770, 6, StateAction.None, StateNum.S_RBALLX2, 0, 0));   // S_RBALLX1
        AddPredefinedState(new State(SpriteNum.SPR_BAL2, 32771, 6, StateAction.None, StateNum.S_RBALLX3, 0, 0));   // S_RBALLX2
        AddPredefinedState(new State(SpriteNum.SPR_BAL2, 32772, 6, StateAction.None, StateNum.S_NULL, 0, 0));  // S_RBALLX3
        AddPredefinedState(new State(SpriteNum.SPR_PLSS, 32768, 6, StateAction.None, StateNum.S_PLASBALL2, 0, 0)); // S_PLASBALL
        AddPredefinedState(new State(SpriteNum.SPR_PLSS, 32769, 6, StateAction.None, StateNum.S_PLASBALL, 0, 0));  // S_PLASBALL2
        AddPredefinedState(new State(SpriteNum.SPR_PLSE, 32768, 4, StateAction.None, StateNum.S_PLASEXP2, 0, 0));  // S_PLASEXP
        AddPredefinedState(new State(SpriteNum.SPR_PLSE, 32769, 4, StateAction.None, StateNum.S_PLASEXP3, 0, 0));  // S_PLASEXP2
        AddPredefinedState(new State(SpriteNum.SPR_PLSE, 32770, 4, StateAction.None, StateNum.S_PLASEXP4, 0, 0));  // S_PLASEXP3
        AddPredefinedState(new State(SpriteNum.SPR_PLSE, 32771, 4, StateAction.None, StateNum.S_PLASEXP5, 0, 0));  // S_PLASEXP4
        AddPredefinedState(new State(SpriteNum.SPR_PLSE, 32772, 4, StateAction.None, StateNum.S_NULL, 0, 0));  // S_PLASEXP5
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32768, 1, StateAction.None, StateNum.S_ROCKET, 0, 0));    // S_ROCKET
        AddPredefinedState(new State(SpriteNum.SPR_BFS1, 32768, 4, StateAction.None, StateNum.S_BFGSHOT2, 0, 0));  // S_BFGSHOT
        AddPredefinedState(new State(SpriteNum.SPR_BFS1, 32769, 4, StateAction.None, StateNum.S_BFGSHOT, 0, 0));   // S_BFGSHOT2
        AddPredefinedState(new State(SpriteNum.SPR_BFE1, 32768, 8, StateAction.None, StateNum.S_BFGLAND2, 0, 0));  // S_BFGLAND
        AddPredefinedState(new State(SpriteNum.SPR_BFE1, 32769, 8, StateAction.None, StateNum.S_BFGLAND3, 0, 0));  // S_BFGLAND2
        AddPredefinedState(new State(SpriteNum.SPR_BFE1, 32770, 8, StateAction.BFGSpray, StateNum.S_BFGLAND4, 0, 0));    // S_BFGLAND3
        AddPredefinedState(new State(SpriteNum.SPR_BFE1, 32771, 8, StateAction.None, StateNum.S_BFGLAND5, 0, 0));  // S_BFGLAND4
        AddPredefinedState(new State(SpriteNum.SPR_BFE1, 32772, 8, StateAction.None, StateNum.S_BFGLAND6, 0, 0));  // S_BFGLAND5
        AddPredefinedState(new State(SpriteNum.SPR_BFE1, 32773, 8, StateAction.None, StateNum.S_NULL, 0, 0));  // S_BFGLAND6
        AddPredefinedState(new State(SpriteNum.SPR_BFE2, 32768, 8, StateAction.None, StateNum.S_BFGEXP2, 0, 0));   // S_BFGEXP
        AddPredefinedState(new State(SpriteNum.SPR_BFE2, 32769, 8, StateAction.None, StateNum.S_BFGEXP3, 0, 0));   // S_BFGEXP2
        AddPredefinedState(new State(SpriteNum.SPR_BFE2, 32770, 8, StateAction.None, StateNum.S_BFGEXP4, 0, 0));   // S_BFGEXP3
        AddPredefinedState(new State(SpriteNum.SPR_BFE2, 32771, 8, StateAction.None, StateNum.S_NULL, 0, 0));  // S_BFGEXP4
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32769, 8, StateAction.Explode, StateNum.S_EXPLODE2, 0, 0)); // S_EXPLODE1
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32770, 6, StateAction.None, StateNum.S_EXPLODE3, 0, 0));  // S_EXPLODE2
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32771, 4, StateAction.None, StateNum.S_NULL, 0, 0));  // S_EXPLODE3
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32768, 6, StateAction.None, StateNum.S_TFOG01, 0, 0));    // S_TFOG
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32769, 6, StateAction.None, StateNum.S_TFOG02, 0, 0));    // S_TFOG01
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32768, 6, StateAction.None, StateNum.S_TFOG2, 0, 0)); // S_TFOG02
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32769, 6, StateAction.None, StateNum.S_TFOG3, 0, 0)); // S_TFOG2
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32770, 6, StateAction.None, StateNum.S_TFOG4, 0, 0)); // S_TFOG3
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32771, 6, StateAction.None, StateNum.S_TFOG5, 0, 0)); // S_TFOG4
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32772, 6, StateAction.None, StateNum.S_TFOG6, 0, 0)); // S_TFOG5
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32773, 6, StateAction.None, StateNum.S_TFOG7, 0, 0)); // S_TFOG6
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32774, 6, StateAction.None, StateNum.S_TFOG8, 0, 0)); // S_TFOG7
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32775, 6, StateAction.None, StateNum.S_TFOG9, 0, 0)); // S_TFOG8
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32776, 6, StateAction.None, StateNum.S_TFOG10, 0, 0));    // S_TFOG9
        AddPredefinedState(new State(SpriteNum.SPR_TFOG, 32777, 6, StateAction.None, StateNum.S_NULL, 0, 0));  // S_TFOG10
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32768, 6, StateAction.None, StateNum.S_IFOG01, 0, 0));    // S_IFOG
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32769, 6, StateAction.None, StateNum.S_IFOG02, 0, 0));    // S_IFOG01
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32768, 6, StateAction.None, StateNum.S_IFOG2, 0, 0)); // S_IFOG02
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32769, 6, StateAction.None, StateNum.S_IFOG3, 0, 0)); // S_IFOG2
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32770, 6, StateAction.None, StateNum.S_IFOG4, 0, 0)); // S_IFOG3
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32771, 6, StateAction.None, StateNum.S_IFOG5, 0, 0)); // S_IFOG4
        AddPredefinedState(new State(SpriteNum.SPR_IFOG, 32772, 6, StateAction.None, StateNum.S_NULL, 0, 0));  // S_IFOG5
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_PLAY
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 0, 4, StateAction.None, StateNum.S_PLAY_RUN2, 0, 0)); // S_PLAY_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 1, 4, StateAction.None, StateNum.S_PLAY_RUN3, 0, 0)); // S_PLAY_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 2, 4, StateAction.None, StateNum.S_PLAY_RUN4, 0, 0)); // S_PLAY_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 3, 4, StateAction.None, StateNum.S_PLAY_RUN1, 0, 0)); // S_PLAY_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 4, 12, StateAction.None, StateNum.S_PLAY, 0, 0)); // S_PLAY_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 32773, 6, StateAction.None, StateNum.S_PLAY_ATK1, 0, 0)); // S_PLAY_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 6, 4, StateAction.None, StateNum.S_PLAY_PAIN2, 0, 0));    // S_PLAY_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 6, 4, StateAction.None, StateNum.S_PLAY, 0, 0));    // S_PLAY_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 7, 10, StateAction.None, StateNum.S_PLAY_DIE2, 0, 0));    // S_PLAY_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 8, 10, StateAction.None, StateNum.S_PLAY_DIE3, 0, 0));  // S_PLAY_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 9, 10, StateAction.Fall, StateNum.S_PLAY_DIE4, 0, 0));  // S_PLAY_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 10, 10, StateAction.None, StateNum.S_PLAY_DIE5, 0, 0));   // S_PLAY_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 11, 10, StateAction.None, StateNum.S_PLAY_DIE6, 0, 0));   // S_PLAY_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 12, 10, StateAction.None, StateNum.S_PLAY_DIE7, 0, 0));   // S_PLAY_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 13, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_PLAY_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 14, 5, StateAction.None, StateNum.S_PLAY_XDIE2, 0, 0));   // S_PLAY_XDIE1
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 15, 5, StateAction.XScream, StateNum.S_PLAY_XDIE3, 0, 0));  // S_PLAY_XDIE2
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 16, 5, StateAction.Fall, StateNum.S_PLAY_XDIE4, 0, 0)); // S_PLAY_XDIE3
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 17, 5, StateAction.None, StateNum.S_PLAY_XDIE5, 0, 0));   // S_PLAY_XDIE4
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 18, 5, StateAction.None, StateNum.S_PLAY_XDIE6, 0, 0));   // S_PLAY_XDIE5
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 19, 5, StateAction.None, StateNum.S_PLAY_XDIE7, 0, 0));   // S_PLAY_XDIE6
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 20, 5, StateAction.None, StateNum.S_PLAY_XDIE8, 0, 0));   // S_PLAY_XDIE7
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 21, 5, StateAction.None, StateNum.S_PLAY_XDIE9, 0, 0));   // S_PLAY_XDIE8
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 22, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_PLAY_XDIE9
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 0, 10, StateAction.Look, StateNum.S_POSS_STND2, 0, 0)); // S_POSS_STND
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 1, 10, StateAction.Look, StateNum.S_POSS_STND, 0, 0));  // S_POSS_STND2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 0, 4, StateAction.Chase, StateNum.S_POSS_RUN2, 0, 0));  // S_POSS_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 0, 4, StateAction.Chase, StateNum.S_POSS_RUN3, 0, 0));  // S_POSS_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 1, 4, StateAction.Chase, StateNum.S_POSS_RUN4, 0, 0));  // S_POSS_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 1, 4, StateAction.Chase, StateNum.S_POSS_RUN5, 0, 0));  // S_POSS_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 2, 4, StateAction.Chase, StateNum.S_POSS_RUN6, 0, 0));  // S_POSS_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 2, 4, StateAction.Chase, StateNum.S_POSS_RUN7, 0, 0));  // S_POSS_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 3, 4, StateAction.Chase, StateNum.S_POSS_RUN8, 0, 0));  // S_POSS_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 3, 4, StateAction.Chase, StateNum.S_POSS_RUN1, 0, 0));  // S_POSS_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 4, 10, StateAction.FaceTarget, StateNum.S_POSS_ATK2, 0, 0));    // S_POSS_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 5, 8, StateAction.PosAttack, StateNum.S_POSS_ATK3, 0, 0));  // S_POSS_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 4, 8, StateAction.None, StateNum.S_POSS_RUN1, 0, 0)); // S_POSS_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 6, 3, StateAction.None, StateNum.S_POSS_PAIN2, 0, 0));    // S_POSS_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 6, 3, StateAction.None, StateNum.S_POSS_RUN1, 0, 0));   // S_POSS_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 7, 5, StateAction.None, StateNum.S_POSS_DIE2, 0, 0)); // S_POSS_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 8, 5, StateAction.None, StateNum.S_POSS_DIE3, 0, 0)); // S_POSS_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 9, 5, StateAction.Fall, StateNum.S_POSS_DIE4, 0, 0));   // S_POSS_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 10, 5, StateAction.None, StateNum.S_POSS_DIE5, 0, 0));    // S_POSS_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 11, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_POSS_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 12, 5, StateAction.None, StateNum.S_POSS_XDIE2, 0, 0));   // S_POSS_XDIE1
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 13, 5, StateAction.XScream, StateNum.S_POSS_XDIE3, 0, 0));  // S_POSS_XDIE2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 14, 5, StateAction.Fall, StateNum.S_POSS_XDIE4, 0, 0)); // S_POSS_XDIE3
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 15, 5, StateAction.None, StateNum.S_POSS_XDIE5, 0, 0));   // S_POSS_XDIE4
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 16, 5, StateAction.None, StateNum.S_POSS_XDIE6, 0, 0));   // S_POSS_XDIE5
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 17, 5, StateAction.None, StateNum.S_POSS_XDIE7, 0, 0));   // S_POSS_XDIE6
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 18, 5, StateAction.None, StateNum.S_POSS_XDIE8, 0, 0));   // S_POSS_XDIE7
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 19, 5, StateAction.None, StateNum.S_POSS_XDIE9, 0, 0));   // S_POSS_XDIE8
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 20, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_POSS_XDIE9
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 10, 5, StateAction.None, StateNum.S_POSS_RAISE2, 0, 0));  // S_POSS_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 9, 5, StateAction.None, StateNum.S_POSS_RAISE3, 0, 0));   // S_POSS_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 8, 5, StateAction.None, StateNum.S_POSS_RAISE4, 0, 0));   // S_POSS_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_POSS, 7, 5, StateAction.None, StateNum.S_POSS_RUN1, 0, 0)); // S_POSS_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 0, 10, StateAction.Look, StateNum.S_SPOS_STND2, 0, 0)); // S_SPOS_STND
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 1, 10, StateAction.Look, StateNum.S_SPOS_STND, 0, 0));  // S_SPOS_STND2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 0, 3, StateAction.Chase, StateNum.S_SPOS_RUN2, 0, 0));  // S_SPOS_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 0, 3, StateAction.Chase, StateNum.S_SPOS_RUN3, 0, 0));  // S_SPOS_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 1, 3, StateAction.Chase, StateNum.S_SPOS_RUN4, 0, 0));  // S_SPOS_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 1, 3, StateAction.Chase, StateNum.S_SPOS_RUN5, 0, 0));  // S_SPOS_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 2, 3, StateAction.Chase, StateNum.S_SPOS_RUN6, 0, 0));  // S_SPOS_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 2, 3, StateAction.Chase, StateNum.S_SPOS_RUN7, 0, 0));  // S_SPOS_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 3, 3, StateAction.Chase, StateNum.S_SPOS_RUN8, 0, 0));  // S_SPOS_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 3, 3, StateAction.Chase, StateNum.S_SPOS_RUN1, 0, 0));  // S_SPOS_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 4, 10, StateAction.FaceTarget, StateNum.S_SPOS_ATK2, 0, 0));    // S_SPOS_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 32773, 10, StateAction.SPosAttack, StateNum.S_SPOS_ATK3, 0, 0));    // S_SPOS_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 4, 10, StateAction.None, StateNum.S_SPOS_RUN1, 0, 0));    // S_SPOS_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 6, 3, StateAction.None, StateNum.S_SPOS_PAIN2, 0, 0));    // S_SPOS_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 6, 3, StateAction.None, StateNum.S_SPOS_RUN1, 0, 0));   // S_SPOS_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 7, 5, StateAction.None, StateNum.S_SPOS_DIE2, 0, 0)); // S_SPOS_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 8, 5, StateAction.None, StateNum.S_SPOS_DIE3, 0, 0)); // S_SPOS_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 9, 5, StateAction.Fall, StateNum.S_SPOS_DIE4, 0, 0));   // S_SPOS_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 10, 5, StateAction.None, StateNum.S_SPOS_DIE5, 0, 0));    // S_SPOS_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 11, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_SPOS_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 12, 5, StateAction.None, StateNum.S_SPOS_XDIE2, 0, 0));   // S_SPOS_XDIE1
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 13, 5, StateAction.XScream, StateNum.S_SPOS_XDIE3, 0, 0));  // S_SPOS_XDIE2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 14, 5, StateAction.Fall, StateNum.S_SPOS_XDIE4, 0, 0)); // S_SPOS_XDIE3
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 15, 5, StateAction.None, StateNum.S_SPOS_XDIE5, 0, 0));   // S_SPOS_XDIE4
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 16, 5, StateAction.None, StateNum.S_SPOS_XDIE6, 0, 0));   // S_SPOS_XDIE5
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 17, 5, StateAction.None, StateNum.S_SPOS_XDIE7, 0, 0));   // S_SPOS_XDIE6
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 18, 5, StateAction.None, StateNum.S_SPOS_XDIE8, 0, 0));   // S_SPOS_XDIE7
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 19, 5, StateAction.None, StateNum.S_SPOS_XDIE9, 0, 0));   // S_SPOS_XDIE8
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 20, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_SPOS_XDIE9
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 11, 5, StateAction.None, StateNum.S_SPOS_RAISE2, 0, 0));  // S_SPOS_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 10, 5, StateAction.None, StateNum.S_SPOS_RAISE3, 0, 0));  // S_SPOS_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 9, 5, StateAction.None, StateNum.S_SPOS_RAISE4, 0, 0));   // S_SPOS_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 8, 5, StateAction.None, StateNum.S_SPOS_RAISE5, 0, 0));   // S_SPOS_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_SPOS, 7, 5, StateAction.None, StateNum.S_SPOS_RUN1, 0, 0)); // S_SPOS_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 0, 10, StateAction.Look, StateNum.S_VILE_STND2, 0, 0)); // S_VILE_STND
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 1, 10, StateAction.Look, StateNum.S_VILE_STND, 0, 0));  // S_VILE_STND2
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 0, 2, StateAction.VileChase, StateNum.S_VILE_RUN2, 0, 0));  // S_VILE_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 0, 2, StateAction.VileChase, StateNum.S_VILE_RUN3, 0, 0));  // S_VILE_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 1, 2, StateAction.VileChase, StateNum.S_VILE_RUN4, 0, 0));  // S_VILE_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 1, 2, StateAction.VileChase, StateNum.S_VILE_RUN5, 0, 0));  // S_VILE_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 2, 2, StateAction.VileChase, StateNum.S_VILE_RUN6, 0, 0));  // S_VILE_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 2, 2, StateAction.VileChase, StateNum.S_VILE_RUN7, 0, 0));  // S_VILE_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 3, 2, StateAction.VileChase, StateNum.S_VILE_RUN8, 0, 0));  // S_VILE_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 3, 2, StateAction.VileChase, StateNum.S_VILE_RUN9, 0, 0));  // S_VILE_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 4, 2, StateAction.VileChase, StateNum.S_VILE_RUN10, 0, 0)); // S_VILE_RUN9
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 4, 2, StateAction.VileChase, StateNum.S_VILE_RUN11, 0, 0)); // S_VILE_RUN10
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 5, 2, StateAction.VileChase, StateNum.S_VILE_RUN12, 0, 0)); // S_VILE_RUN11
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 5, 2, StateAction.VileChase, StateNum.S_VILE_RUN1, 0, 0));  // S_VILE_RUN12
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32774, 0, StateAction.VileStart, StateNum.S_VILE_ATK2, 0, 0));  // S_VILE_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32774, 10, StateAction.FaceTarget, StateNum.S_VILE_ATK3, 0, 0));    // S_VILE_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32775, 8, StateAction.VileTarget, StateNum.S_VILE_ATK4, 0, 0)); // S_VILE_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32776, 8, StateAction.FaceTarget, StateNum.S_VILE_ATK5, 0, 0)); // S_VILE_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32777, 8, StateAction.FaceTarget, StateNum.S_VILE_ATK6, 0, 0)); // S_VILE_ATK5
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32778, 8, StateAction.FaceTarget, StateNum.S_VILE_ATK7, 0, 0)); // S_VILE_ATK6
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32779, 8, StateAction.FaceTarget, StateNum.S_VILE_ATK8, 0, 0)); // S_VILE_ATK7
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32780, 8, StateAction.FaceTarget, StateNum.S_VILE_ATK9, 0, 0)); // S_VILE_ATK8
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32781, 8, StateAction.FaceTarget, StateNum.S_VILE_ATK10, 0, 0));    // S_VILE_ATK9
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32782, 8, StateAction.VileAttack, StateNum.S_VILE_ATK11, 0, 0));    // S_VILE_ATK10
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32783, 20, StateAction.None, StateNum.S_VILE_RUN1, 0, 0));    // S_VILE_ATK11
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32794, 10, StateAction.None, StateNum.S_VILE_HEAL2, 0, 0));   // S_VILE_HEAL1
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32795, 10, StateAction.None, StateNum.S_VILE_HEAL3, 0, 0));   // S_VILE_HEAL2
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 32796, 10, StateAction.None, StateNum.S_VILE_RUN1, 0, 0));    // S_VILE_HEAL3
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 16, 5, StateAction.None, StateNum.S_VILE_PAIN2, 0, 0));   // S_VILE_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 16, 5, StateAction.None, StateNum.S_VILE_RUN1, 0, 0));  // S_VILE_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 16, 7, StateAction.None, StateNum.S_VILE_DIE2, 0, 0));    // S_VILE_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 17, 7, StateAction.None, StateNum.S_VILE_DIE3, 0, 0));    // S_VILE_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 18, 7, StateAction.Fall, StateNum.S_VILE_DIE4, 0, 0));  // S_VILE_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 19, 7, StateAction.None, StateNum.S_VILE_DIE5, 0, 0));    // S_VILE_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 20, 7, StateAction.None, StateNum.S_VILE_DIE6, 0, 0));    // S_VILE_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 21, 7, StateAction.None, StateNum.S_VILE_DIE7, 0, 0));    // S_VILE_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 22, 7, StateAction.None, StateNum.S_VILE_DIE8, 0, 0));    // S_VILE_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 23, 5, StateAction.None, StateNum.S_VILE_DIE9, 0, 0));    // S_VILE_DIE8
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 24, 5, StateAction.None, StateNum.S_VILE_DIE10, 0, 0));   // S_VILE_DIE9
        AddPredefinedState(new State(SpriteNum.SPR_VILE, 25, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_VILE_DIE10
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32768, 2, StateAction.StartFire, StateNum.S_FIRE2, 0, 0));  // S_FIRE1
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32769, 2, StateAction.Fire, StateNum.S_FIRE3, 0, 0));   // S_FIRE2
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32768, 2, StateAction.Fire, StateNum.S_FIRE4, 0, 0));   // S_FIRE3
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32769, 2, StateAction.Fire, StateNum.S_FIRE5, 0, 0));   // S_FIRE4
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32770, 2, StateAction.FireCrackle, StateNum.S_FIRE6, 0, 0));    // S_FIRE5
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32769, 2, StateAction.Fire, StateNum.S_FIRE7, 0, 0));   // S_FIRE6
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32770, 2, StateAction.Fire, StateNum.S_FIRE8, 0, 0));   // S_FIRE7
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32769, 2, StateAction.Fire, StateNum.S_FIRE9, 0, 0));   // S_FIRE8
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32770, 2, StateAction.Fire, StateNum.S_FIRE10, 0, 0));  // S_FIRE9
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32771, 2, StateAction.Fire, StateNum.S_FIRE11, 0, 0));  // S_FIRE10
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32770, 2, StateAction.Fire, StateNum.S_FIRE12, 0, 0));  // S_FIRE11
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32771, 2, StateAction.Fire, StateNum.S_FIRE13, 0, 0));  // S_FIRE12
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32770, 2, StateAction.Fire, StateNum.S_FIRE14, 0, 0));  // S_FIRE13
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32771, 2, StateAction.Fire, StateNum.S_FIRE15, 0, 0));  // S_FIRE14
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32772, 2, StateAction.Fire, StateNum.S_FIRE16, 0, 0));  // S_FIRE15
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32771, 2, StateAction.Fire, StateNum.S_FIRE17, 0, 0));  // S_FIRE16
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32772, 2, StateAction.Fire, StateNum.S_FIRE18, 0, 0));  // S_FIRE17
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32771, 2, StateAction.Fire, StateNum.S_FIRE19, 0, 0));  // S_FIRE18
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32772, 2, StateAction.FireCrackle, StateNum.S_FIRE20, 0, 0));   // S_FIRE19
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32773, 2, StateAction.Fire, StateNum.S_FIRE21, 0, 0));  // S_FIRE20
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32772, 2, StateAction.Fire, StateNum.S_FIRE22, 0, 0));  // S_FIRE21
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32773, 2, StateAction.Fire, StateNum.S_FIRE23, 0, 0));  // S_FIRE22
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32772, 2, StateAction.Fire, StateNum.S_FIRE24, 0, 0));  // S_FIRE23
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32773, 2, StateAction.Fire, StateNum.S_FIRE25, 0, 0));  // S_FIRE24
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32774, 2, StateAction.Fire, StateNum.S_FIRE26, 0, 0));  // S_FIRE25
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32775, 2, StateAction.Fire, StateNum.S_FIRE27, 0, 0));  // S_FIRE26
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32774, 2, StateAction.Fire, StateNum.S_FIRE28, 0, 0));  // S_FIRE27
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32775, 2, StateAction.Fire, StateNum.S_FIRE29, 0, 0));  // S_FIRE28
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32774, 2, StateAction.Fire, StateNum.S_FIRE30, 0, 0));  // S_FIRE29
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32775, 2, StateAction.Fire, StateNum.S_NULL, 0, 0));    // S_FIRE30
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 1, 4, StateAction.None, StateNum.S_SMOKE2, 0, 0));    // S_SMOKE1
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 2, 4, StateAction.None, StateNum.S_SMOKE3, 0, 0));    // S_SMOKE2
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 1, 4, StateAction.None, StateNum.S_SMOKE4, 0, 0));    // S_SMOKE3
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 2, 4, StateAction.None, StateNum.S_SMOKE5, 0, 0));    // S_SMOKE4
        AddPredefinedState(new State(SpriteNum.SPR_PUFF, 3, 4, StateAction.None, StateNum.S_NULL, 0, 0));  // S_SMOKE5
        AddPredefinedState(new State(SpriteNum.SPR_FATB, 32768, 2, StateAction.Tracer, StateNum.S_TRACER2, 0, 0));   // S_TRACER
        AddPredefinedState(new State(SpriteNum.SPR_FATB, 32769, 2, StateAction.Tracer, StateNum.S_TRACER, 0, 0));    // S_TRACER2
        AddPredefinedState(new State(SpriteNum.SPR_FBXP, 32768, 8, StateAction.None, StateNum.S_TRACEEXP2, 0, 0)); // S_TRACEEXP1
        AddPredefinedState(new State(SpriteNum.SPR_FBXP, 32769, 6, StateAction.None, StateNum.S_TRACEEXP3, 0, 0)); // S_TRACEEXP2
        AddPredefinedState(new State(SpriteNum.SPR_FBXP, 32770, 4, StateAction.None, StateNum.S_NULL, 0, 0));  // S_TRACEEXP3
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 0, 10, StateAction.Look, StateNum.S_SKEL_STND2, 0, 0)); // S_SKEL_STND
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 1, 10, StateAction.Look, StateNum.S_SKEL_STND, 0, 0));  // S_SKEL_STND2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 0, 2, StateAction.Chase, StateNum.S_SKEL_RUN2, 0, 0));  // S_SKEL_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 0, 2, StateAction.Chase, StateNum.S_SKEL_RUN3, 0, 0));  // S_SKEL_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 1, 2, StateAction.Chase, StateNum.S_SKEL_RUN4, 0, 0));  // S_SKEL_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 1, 2, StateAction.Chase, StateNum.S_SKEL_RUN5, 0, 0));  // S_SKEL_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 2, 2, StateAction.Chase, StateNum.S_SKEL_RUN6, 0, 0));  // S_SKEL_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 2, 2, StateAction.Chase, StateNum.S_SKEL_RUN7, 0, 0));  // S_SKEL_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 3, 2, StateAction.Chase, StateNum.S_SKEL_RUN8, 0, 0));  // S_SKEL_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 3, 2, StateAction.Chase, StateNum.S_SKEL_RUN9, 0, 0));  // S_SKEL_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 4, 2, StateAction.Chase, StateNum.S_SKEL_RUN10, 0, 0)); // S_SKEL_RUN9
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 4, 2, StateAction.Chase, StateNum.S_SKEL_RUN11, 0, 0)); // S_SKEL_RUN10
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 5, 2, StateAction.Chase, StateNum.S_SKEL_RUN12, 0, 0)); // S_SKEL_RUN11
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 5, 2, StateAction.Chase, StateNum.S_SKEL_RUN1, 0, 0));  // S_SKEL_RUN12
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 6, 0, StateAction.FaceTarget, StateNum.S_SKEL_FIST2, 0, 0));    // S_SKEL_FIST1
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 6, 6, StateAction.SkelWhoosh, StateNum.S_SKEL_FIST3, 0, 0));    // S_SKEL_FIST2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 7, 6, StateAction.FaceTarget, StateNum.S_SKEL_FIST4, 0, 0));    // S_SKEL_FIST3
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 8, 6, StateAction.SkelFist, StateNum.S_SKEL_RUN1, 0, 0));   // S_SKEL_FIST4
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 32777, 0, StateAction.FaceTarget, StateNum.S_SKEL_MISS2, 0, 0));    // S_SKEL_MISS1
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 32777, 10, StateAction.FaceTarget, StateNum.S_SKEL_MISS3, 0, 0));   // S_SKEL_MISS2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 10, 10, StateAction.SkelMissile, StateNum.S_SKEL_MISS4, 0, 0)); // S_SKEL_MISS3
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 10, 10, StateAction.FaceTarget, StateNum.S_SKEL_RUN1, 0, 0));   // S_SKEL_MISS4
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 11, 5, StateAction.None, StateNum.S_SKEL_PAIN2, 0, 0));   // S_SKEL_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 11, 5, StateAction.None, StateNum.S_SKEL_RUN1, 0, 0));  // S_SKEL_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 11, 7, StateAction.None, StateNum.S_SKEL_DIE2, 0, 0));    // S_SKEL_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 12, 7, StateAction.None, StateNum.S_SKEL_DIE3, 0, 0));    // S_SKEL_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 13, 7, StateAction.None, StateNum.S_SKEL_DIE4, 0, 0));    // S_SKEL_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 14, 7, StateAction.Fall, StateNum.S_SKEL_DIE5, 0, 0));  // S_SKEL_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 15, 7, StateAction.None, StateNum.S_SKEL_DIE6, 0, 0));    // S_SKEL_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 16, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_SKEL_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 16, 5, StateAction.None, StateNum.S_SKEL_RAISE2, 0, 0));  // S_SKEL_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 15, 5, StateAction.None, StateNum.S_SKEL_RAISE3, 0, 0));  // S_SKEL_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 14, 5, StateAction.None, StateNum.S_SKEL_RAISE4, 0, 0));  // S_SKEL_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 13, 5, StateAction.None, StateNum.S_SKEL_RAISE5, 0, 0));  // S_SKEL_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 12, 5, StateAction.None, StateNum.S_SKEL_RAISE6, 0, 0));  // S_SKEL_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_SKEL, 11, 5, StateAction.None, StateNum.S_SKEL_RUN1, 0, 0));    // S_SKEL_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_MANF, 32768, 4, StateAction.None, StateNum.S_FATSHOT2, 0, 0));  // S_FATSHOT1
        AddPredefinedState(new State(SpriteNum.SPR_MANF, 32769, 4, StateAction.None, StateNum.S_FATSHOT1, 0, 0));  // S_FATSHOT2
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32769, 8, StateAction.None, StateNum.S_FATSHOTX2, 0, 0)); // S_FATSHOTX1
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32770, 6, StateAction.None, StateNum.S_FATSHOTX3, 0, 0)); // S_FATSHOTX2
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32771, 4, StateAction.None, StateNum.S_NULL, 0, 0));  // S_FATSHOTX3
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 0, 15, StateAction.Look, StateNum.S_FATT_STND2, 0, 0)); // S_FATT_STND
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 1, 15, StateAction.Look, StateNum.S_FATT_STND, 0, 0));  // S_FATT_STND2
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 0, 4, StateAction.Chase, StateNum.S_FATT_RUN2, 0, 0));  // S_FATT_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 0, 4, StateAction.Chase, StateNum.S_FATT_RUN3, 0, 0));  // S_FATT_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 1, 4, StateAction.Chase, StateNum.S_FATT_RUN4, 0, 0));  // S_FATT_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 1, 4, StateAction.Chase, StateNum.S_FATT_RUN5, 0, 0));  // S_FATT_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 2, 4, StateAction.Chase, StateNum.S_FATT_RUN6, 0, 0));  // S_FATT_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 2, 4, StateAction.Chase, StateNum.S_FATT_RUN7, 0, 0));  // S_FATT_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 3, 4, StateAction.Chase, StateNum.S_FATT_RUN8, 0, 0));  // S_FATT_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 3, 4, StateAction.Chase, StateNum.S_FATT_RUN9, 0, 0));  // S_FATT_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 4, 4, StateAction.Chase, StateNum.S_FATT_RUN10, 0, 0)); // S_FATT_RUN9
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 4, 4, StateAction.Chase, StateNum.S_FATT_RUN11, 0, 0)); // S_FATT_RUN10
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 5, 4, StateAction.Chase, StateNum.S_FATT_RUN12, 0, 0)); // S_FATT_RUN11
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 5, 4, StateAction.Chase, StateNum.S_FATT_RUN1, 0, 0));  // S_FATT_RUN12
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 6, 20, StateAction.FatRaise, StateNum.S_FATT_ATK2, 0, 0));  // S_FATT_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 32775, 10, StateAction.FatAttack1, StateNum.S_FATT_ATK3, 0, 0));    // S_FATT_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 8, 5, StateAction.FaceTarget, StateNum.S_FATT_ATK4, 0, 0)); // S_FATT_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 6, 5, StateAction.FaceTarget, StateNum.S_FATT_ATK5, 0, 0)); // S_FATT_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 32775, 10, StateAction.FatAttack2, StateNum.S_FATT_ATK6, 0, 0));    // S_FATT_ATK5
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 8, 5, StateAction.FaceTarget, StateNum.S_FATT_ATK7, 0, 0)); // S_FATT_ATK6
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 6, 5, StateAction.FaceTarget, StateNum.S_FATT_ATK8, 0, 0)); // S_FATT_ATK7
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 32775, 10, StateAction.FatAttack3, StateNum.S_FATT_ATK9, 0, 0));    // S_FATT_ATK8
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 8, 5, StateAction.FaceTarget, StateNum.S_FATT_ATK10, 0, 0));    // S_FATT_ATK9
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 6, 5, StateAction.FaceTarget, StateNum.S_FATT_RUN1, 0, 0)); // S_FATT_ATK10
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 9, 3, StateAction.None, StateNum.S_FATT_PAIN2, 0, 0));    // S_FATT_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 9, 3, StateAction.None, StateNum.S_FATT_RUN1, 0, 0));   // S_FATT_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 10, 6, StateAction.None, StateNum.S_FATT_DIE2, 0, 0));    // S_FATT_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 11, 6, StateAction.None, StateNum.S_FATT_DIE3, 0, 0));    // S_FATT_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 12, 6, StateAction.Fall, StateNum.S_FATT_DIE4, 0, 0));  // S_FATT_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 13, 6, StateAction.None, StateNum.S_FATT_DIE5, 0, 0));    // S_FATT_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 14, 6, StateAction.None, StateNum.S_FATT_DIE6, 0, 0));    // S_FATT_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 15, 6, StateAction.None, StateNum.S_FATT_DIE7, 0, 0));    // S_FATT_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 16, 6, StateAction.None, StateNum.S_FATT_DIE8, 0, 0));    // S_FATT_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 17, 6, StateAction.None, StateNum.S_FATT_DIE9, 0, 0));    // S_FATT_DIE8
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 18, 6, StateAction.None, StateNum.S_FATT_DIE10, 0, 0));   // S_FATT_DIE9
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 19, -1, StateAction.BossDeath, StateNum.S_NULL, 0, 0)); // S_FATT_DIE10
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 17, 5, StateAction.None, StateNum.S_FATT_RAISE2, 0, 0));  // S_FATT_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 16, 5, StateAction.None, StateNum.S_FATT_RAISE3, 0, 0));  // S_FATT_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 15, 5, StateAction.None, StateNum.S_FATT_RAISE4, 0, 0));  // S_FATT_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 14, 5, StateAction.None, StateNum.S_FATT_RAISE5, 0, 0));  // S_FATT_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 13, 5, StateAction.None, StateNum.S_FATT_RAISE6, 0, 0));  // S_FATT_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 12, 5, StateAction.None, StateNum.S_FATT_RAISE7, 0, 0));  // S_FATT_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 11, 5, StateAction.None, StateNum.S_FATT_RAISE8, 0, 0));  // S_FATT_RAISE7
        AddPredefinedState(new State(SpriteNum.SPR_FATT, 10, 5, StateAction.None, StateNum.S_FATT_RUN1, 0, 0));    // S_FATT_RAISE8
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 0, 10, StateAction.Look, StateNum.S_CPOS_STND2, 0, 0)); // S_CPOS_STND
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 1, 10, StateAction.Look, StateNum.S_CPOS_STND, 0, 0));  // S_CPOS_STND2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 0, 3, StateAction.Chase, StateNum.S_CPOS_RUN2, 0, 0));  // S_CPOS_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 0, 3, StateAction.Chase, StateNum.S_CPOS_RUN3, 0, 0));  // S_CPOS_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 1, 3, StateAction.Chase, StateNum.S_CPOS_RUN4, 0, 0));  // S_CPOS_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 1, 3, StateAction.Chase, StateNum.S_CPOS_RUN5, 0, 0));  // S_CPOS_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 2, 3, StateAction.Chase, StateNum.S_CPOS_RUN6, 0, 0));  // S_CPOS_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 2, 3, StateAction.Chase, StateNum.S_CPOS_RUN7, 0, 0));  // S_CPOS_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 3, 3, StateAction.Chase, StateNum.S_CPOS_RUN8, 0, 0));  // S_CPOS_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 3, 3, StateAction.Chase, StateNum.S_CPOS_RUN1, 0, 0));  // S_CPOS_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 4, 10, StateAction.FaceTarget, StateNum.S_CPOS_ATK2, 0, 0));    // S_CPOS_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 32773, 4, StateAction.CPosAttack, StateNum.S_CPOS_ATK3, 0, 0)); // S_CPOS_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 32772, 4, StateAction.CPosAttack, StateNum.S_CPOS_ATK4, 0, 0)); // S_CPOS_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 5, 1, StateAction.CPosRefire, StateNum.S_CPOS_ATK2, 0, 0)); // S_CPOS_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 6, 3, StateAction.None, StateNum.S_CPOS_PAIN2, 0, 0));    // S_CPOS_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 6, 3, StateAction.None, StateNum.S_CPOS_RUN1, 0, 0));   // S_CPOS_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 7, 5, StateAction.None, StateNum.S_CPOS_DIE2, 0, 0)); // S_CPOS_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 8, 5, StateAction.None, StateNum.S_CPOS_DIE3, 0, 0)); // S_CPOS_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 9, 5, StateAction.Fall, StateNum.S_CPOS_DIE4, 0, 0));   // S_CPOS_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 10, 5, StateAction.None, StateNum.S_CPOS_DIE5, 0, 0));    // S_CPOS_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 11, 5, StateAction.None, StateNum.S_CPOS_DIE6, 0, 0));    // S_CPOS_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 12, 5, StateAction.None, StateNum.S_CPOS_DIE7, 0, 0));    // S_CPOS_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 13, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_CPOS_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 14, 5, StateAction.None, StateNum.S_CPOS_XDIE2, 0, 0));   // S_CPOS_XDIE1
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 15, 5, StateAction.XScream, StateNum.S_CPOS_XDIE3, 0, 0));  // S_CPOS_XDIE2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 16, 5, StateAction.Fall, StateNum.S_CPOS_XDIE4, 0, 0)); // S_CPOS_XDIE3
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 17, 5, StateAction.None, StateNum.S_CPOS_XDIE5, 0, 0));   // S_CPOS_XDIE4
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 18, 5, StateAction.None, StateNum.S_CPOS_XDIE6, 0, 0));   // S_CPOS_XDIE5
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 19, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_CPOS_XDIE6
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 13, 5, StateAction.None, StateNum.S_CPOS_RAISE2, 0, 0));  // S_CPOS_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 12, 5, StateAction.None, StateNum.S_CPOS_RAISE3, 0, 0));  // S_CPOS_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 11, 5, StateAction.None, StateNum.S_CPOS_RAISE4, 0, 0));  // S_CPOS_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 10, 5, StateAction.None, StateNum.S_CPOS_RAISE5, 0, 0));  // S_CPOS_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 9, 5, StateAction.None, StateNum.S_CPOS_RAISE6, 0, 0));   // S_CPOS_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 8, 5, StateAction.None, StateNum.S_CPOS_RAISE7, 0, 0));   // S_CPOS_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_CPOS, 7, 5, StateAction.None, StateNum.S_CPOS_RUN1, 0, 0)); // S_CPOS_RAISE7
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 0, 10, StateAction.Look, StateNum.S_TROO_STND2, 0, 0)); // S_TROO_STND
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 1, 10, StateAction.Look, StateNum.S_TROO_STND, 0, 0));  // S_TROO_STND2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 0, 3, StateAction.Chase, StateNum.S_TROO_RUN2, 0, 0));  // S_TROO_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 0, 3, StateAction.Chase, StateNum.S_TROO_RUN3, 0, 0));  // S_TROO_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 1, 3, StateAction.Chase, StateNum.S_TROO_RUN4, 0, 0));  // S_TROO_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 1, 3, StateAction.Chase, StateNum.S_TROO_RUN5, 0, 0));  // S_TROO_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 2, 3, StateAction.Chase, StateNum.S_TROO_RUN6, 0, 0));  // S_TROO_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 2, 3, StateAction.Chase, StateNum.S_TROO_RUN7, 0, 0));  // S_TROO_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 3, 3, StateAction.Chase, StateNum.S_TROO_RUN8, 0, 0));  // S_TROO_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 3, 3, StateAction.Chase, StateNum.S_TROO_RUN1, 0, 0));  // S_TROO_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 4, 8, StateAction.FaceTarget, StateNum.S_TROO_ATK2, 0, 0)); // S_TROO_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 5, 8, StateAction.FaceTarget, StateNum.S_TROO_ATK3, 0, 0)); // S_TROO_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 6, 6, StateAction.TroopAttack, StateNum.S_TROO_RUN1, 0, 0));    // S_TROO_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 7, 2, StateAction.None, StateNum.S_TROO_PAIN2, 0, 0));    // S_TROO_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 7, 2, StateAction.None, StateNum.S_TROO_RUN1, 0, 0));   // S_TROO_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 8, 8, StateAction.None, StateNum.S_TROO_DIE2, 0, 0)); // S_TROO_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 9, 8, StateAction.None, StateNum.S_TROO_DIE3, 0, 0)); // S_TROO_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 10, 6, StateAction.None, StateNum.S_TROO_DIE4, 0, 0));    // S_TROO_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 11, 6, StateAction.Fall, StateNum.S_TROO_DIE5, 0, 0));  // S_TROO_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 12, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_TROO_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 13, 5, StateAction.None, StateNum.S_TROO_XDIE2, 0, 0));   // S_TROO_XDIE1
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 14, 5, StateAction.XScream, StateNum.S_TROO_XDIE3, 0, 0));  // S_TROO_XDIE2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 15, 5, StateAction.None, StateNum.S_TROO_XDIE4, 0, 0));   // S_TROO_XDIE3
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 16, 5, StateAction.Fall, StateNum.S_TROO_XDIE5, 0, 0)); // S_TROO_XDIE4
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 17, 5, StateAction.None, StateNum.S_TROO_XDIE6, 0, 0));   // S_TROO_XDIE5
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 18, 5, StateAction.None, StateNum.S_TROO_XDIE7, 0, 0));   // S_TROO_XDIE6
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 19, 5, StateAction.None, StateNum.S_TROO_XDIE8, 0, 0));   // S_TROO_XDIE7
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 20, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_TROO_XDIE8
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 12, 8, StateAction.None, StateNum.S_TROO_RAISE2, 0, 0));  // S_TROO_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 11, 8, StateAction.None, StateNum.S_TROO_RAISE3, 0, 0));  // S_TROO_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 10, 6, StateAction.None, StateNum.S_TROO_RAISE4, 0, 0));  // S_TROO_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 9, 6, StateAction.None, StateNum.S_TROO_RAISE5, 0, 0));   // S_TROO_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_TROO, 8, 6, StateAction.None, StateNum.S_TROO_RUN1, 0, 0)); // S_TROO_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 0, 10, StateAction.Look, StateNum.S_SARG_STND2, 0, 0)); // S_SARG_STND
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 1, 10, StateAction.Look, StateNum.S_SARG_STND, 0, 0));  // S_SARG_STND2
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 0, 2, StateAction.Chase, StateNum.S_SARG_RUN2, 0, 0));  // S_SARG_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 0, 2, StateAction.Chase, StateNum.S_SARG_RUN3, 0, 0));  // S_SARG_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 1, 2, StateAction.Chase, StateNum.S_SARG_RUN4, 0, 0));  // S_SARG_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 1, 2, StateAction.Chase, StateNum.S_SARG_RUN5, 0, 0));  // S_SARG_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 2, 2, StateAction.Chase, StateNum.S_SARG_RUN6, 0, 0));  // S_SARG_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 2, 2, StateAction.Chase, StateNum.S_SARG_RUN7, 0, 0));  // S_SARG_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 3, 2, StateAction.Chase, StateNum.S_SARG_RUN8, 0, 0));  // S_SARG_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 3, 2, StateAction.Chase, StateNum.S_SARG_RUN1, 0, 0));  // S_SARG_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 4, 8, StateAction.FaceTarget, StateNum.S_SARG_ATK2, 0, 0)); // S_SARG_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 5, 8, StateAction.FaceTarget, StateNum.S_SARG_ATK3, 0, 0)); // S_SARG_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 6, 8, StateAction.SargAttack, StateNum.S_SARG_RUN1, 0, 0)); // S_SARG_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 7, 2, StateAction.None, StateNum.S_SARG_PAIN2, 0, 0));    // S_SARG_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 7, 2, StateAction.None, StateNum.S_SARG_RUN1, 0, 0));   // S_SARG_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 8, 8, StateAction.None, StateNum.S_SARG_DIE2, 0, 0)); // S_SARG_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 9, 8, StateAction.None, StateNum.S_SARG_DIE3, 0, 0)); // S_SARG_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 10, 4, StateAction.None, StateNum.S_SARG_DIE4, 0, 0));    // S_SARG_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 11, 4, StateAction.Fall, StateNum.S_SARG_DIE5, 0, 0));  // S_SARG_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 12, 4, StateAction.None, StateNum.S_SARG_DIE6, 0, 0));    // S_SARG_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 13, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_SARG_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 13, 5, StateAction.None, StateNum.S_SARG_RAISE2, 0, 0));  // S_SARG_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 12, 5, StateAction.None, StateNum.S_SARG_RAISE3, 0, 0));  // S_SARG_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 11, 5, StateAction.None, StateNum.S_SARG_RAISE4, 0, 0));  // S_SARG_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 10, 5, StateAction.None, StateNum.S_SARG_RAISE5, 0, 0));  // S_SARG_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 9, 5, StateAction.None, StateNum.S_SARG_RAISE6, 0, 0));   // S_SARG_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_SARG, 8, 5, StateAction.None, StateNum.S_SARG_RUN1, 0, 0)); // S_SARG_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 0, 10, StateAction.Look, StateNum.S_HEAD_STND, 0, 0));  // S_HEAD_STND
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 0, 3, StateAction.Chase, StateNum.S_HEAD_RUN1, 0, 0));  // S_HEAD_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 1, 5, StateAction.FaceTarget, StateNum.S_HEAD_ATK2, 0, 0)); // S_HEAD_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 2, 5, StateAction.FaceTarget, StateNum.S_HEAD_ATK3, 0, 0)); // S_HEAD_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 32771, 5, StateAction.HeadAttack, StateNum.S_HEAD_RUN1, 0, 0)); // S_HEAD_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 4, 3, StateAction.None, StateNum.S_HEAD_PAIN2, 0, 0));    // S_HEAD_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 4, 3, StateAction.None, StateNum.S_HEAD_PAIN3, 0, 0));  // S_HEAD_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 5, 6, StateAction.None, StateNum.S_HEAD_RUN1, 0, 0)); // S_HEAD_PAIN3
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 6, 8, StateAction.None, StateNum.S_HEAD_DIE2, 0, 0)); // S_HEAD_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 7, 8, StateAction.None, StateNum.S_HEAD_DIE3, 0, 0)); // S_HEAD_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 8, 8, StateAction.None, StateNum.S_HEAD_DIE4, 0, 0)); // S_HEAD_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 9, 8, StateAction.None, StateNum.S_HEAD_DIE5, 0, 0)); // S_HEAD_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 10, 8, StateAction.Fall, StateNum.S_HEAD_DIE6, 0, 0));  // S_HEAD_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 11, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_HEAD_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 11, 8, StateAction.None, StateNum.S_HEAD_RAISE2, 0, 0));  // S_HEAD_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 10, 8, StateAction.None, StateNum.S_HEAD_RAISE3, 0, 0));  // S_HEAD_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 9, 8, StateAction.None, StateNum.S_HEAD_RAISE4, 0, 0));   // S_HEAD_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 8, 8, StateAction.None, StateNum.S_HEAD_RAISE5, 0, 0));   // S_HEAD_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 7, 8, StateAction.None, StateNum.S_HEAD_RAISE6, 0, 0));   // S_HEAD_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_HEAD, 6, 8, StateAction.None, StateNum.S_HEAD_RUN1, 0, 0)); // S_HEAD_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_BAL7, 32768, 4, StateAction.None, StateNum.S_BRBALL2, 0, 0));   // S_BRBALL1
        AddPredefinedState(new State(SpriteNum.SPR_BAL7, 32769, 4, StateAction.None, StateNum.S_BRBALL1, 0, 0));   // S_BRBALL2
        AddPredefinedState(new State(SpriteNum.SPR_BAL7, 32770, 6, StateAction.None, StateNum.S_BRBALLX2, 0, 0));  // S_BRBALLX1
        AddPredefinedState(new State(SpriteNum.SPR_BAL7, 32771, 6, StateAction.None, StateNum.S_BRBALLX3, 0, 0));  // S_BRBALLX2
        AddPredefinedState(new State(SpriteNum.SPR_BAL7, 32772, 6, StateAction.None, StateNum.S_NULL, 0, 0));  // S_BRBALLX3
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 0, 10, StateAction.Look, StateNum.S_BOSS_STND2, 0, 0)); // S_BOSS_STND
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 1, 10, StateAction.Look, StateNum.S_BOSS_STND, 0, 0));  // S_BOSS_STND2
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 0, 3, StateAction.Chase, StateNum.S_BOSS_RUN2, 0, 0));  // S_BOSS_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 0, 3, StateAction.Chase, StateNum.S_BOSS_RUN3, 0, 0));  // S_BOSS_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 1, 3, StateAction.Chase, StateNum.S_BOSS_RUN4, 0, 0));  // S_BOSS_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 1, 3, StateAction.Chase, StateNum.S_BOSS_RUN5, 0, 0));  // S_BOSS_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 2, 3, StateAction.Chase, StateNum.S_BOSS_RUN6, 0, 0));  // S_BOSS_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 2, 3, StateAction.Chase, StateNum.S_BOSS_RUN7, 0, 0));  // S_BOSS_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 3, 3, StateAction.Chase, StateNum.S_BOSS_RUN8, 0, 0));  // S_BOSS_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 3, 3, StateAction.Chase, StateNum.S_BOSS_RUN1, 0, 0));  // S_BOSS_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 4, 8, StateAction.FaceTarget, StateNum.S_BOSS_ATK2, 0, 0)); // S_BOSS_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 5, 8, StateAction.FaceTarget, StateNum.S_BOSS_ATK3, 0, 0)); // S_BOSS_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 6, 8, StateAction.BruisAttack, StateNum.S_BOSS_RUN1, 0, 0));    // S_BOSS_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 7, 2, StateAction.None, StateNum.S_BOSS_PAIN2, 0, 0));    // S_BOSS_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 7, 2, StateAction.None, StateNum.S_BOSS_RUN1, 0, 0));   // S_BOSS_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 8, 8, StateAction.None, StateNum.S_BOSS_DIE2, 0, 0)); // S_BOSS_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 9, 8, StateAction.None, StateNum.S_BOSS_DIE3, 0, 0)); // S_BOSS_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 10, 8, StateAction.None, StateNum.S_BOSS_DIE4, 0, 0));    // S_BOSS_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 11, 8, StateAction.Fall, StateNum.S_BOSS_DIE5, 0, 0));  // S_BOSS_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 12, 8, StateAction.None, StateNum.S_BOSS_DIE6, 0, 0));    // S_BOSS_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 13, 8, StateAction.None, StateNum.S_BOSS_DIE7, 0, 0));    // S_BOSS_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 14, -1, StateAction.BossDeath, StateNum.S_NULL, 0, 0)); // S_BOSS_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 14, 8, StateAction.None, StateNum.S_BOSS_RAISE2, 0, 0));  // S_BOSS_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 13, 8, StateAction.None, StateNum.S_BOSS_RAISE3, 0, 0));  // S_BOSS_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 12, 8, StateAction.None, StateNum.S_BOSS_RAISE4, 0, 0));  // S_BOSS_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 11, 8, StateAction.None, StateNum.S_BOSS_RAISE5, 0, 0));  // S_BOSS_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 10, 8, StateAction.None, StateNum.S_BOSS_RAISE6, 0, 0));  // S_BOSS_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 9, 8, StateAction.None, StateNum.S_BOSS_RAISE7, 0, 0));   // S_BOSS_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_BOSS, 8, 8, StateAction.None, StateNum.S_BOSS_RUN1, 0, 0)); // S_BOSS_RAISE7
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 0, 10, StateAction.Look, StateNum.S_BOS2_STND2, 0, 0)); // S_BOS2_STND
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 1, 10, StateAction.Look, StateNum.S_BOS2_STND, 0, 0));  // S_BOS2_STND2
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 0, 3, StateAction.Chase, StateNum.S_BOS2_RUN2, 0, 0));  // S_BOS2_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 0, 3, StateAction.Chase, StateNum.S_BOS2_RUN3, 0, 0));  // S_BOS2_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 1, 3, StateAction.Chase, StateNum.S_BOS2_RUN4, 0, 0));  // S_BOS2_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 1, 3, StateAction.Chase, StateNum.S_BOS2_RUN5, 0, 0));  // S_BOS2_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 2, 3, StateAction.Chase, StateNum.S_BOS2_RUN6, 0, 0));  // S_BOS2_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 2, 3, StateAction.Chase, StateNum.S_BOS2_RUN7, 0, 0));  // S_BOS2_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 3, 3, StateAction.Chase, StateNum.S_BOS2_RUN8, 0, 0));  // S_BOS2_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 3, 3, StateAction.Chase, StateNum.S_BOS2_RUN1, 0, 0));  // S_BOS2_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 4, 8, StateAction.FaceTarget, StateNum.S_BOS2_ATK2, 0, 0)); // S_BOS2_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 5, 8, StateAction.FaceTarget, StateNum.S_BOS2_ATK3, 0, 0)); // S_BOS2_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 6, 8, StateAction.BruisAttack, StateNum.S_BOS2_RUN1, 0, 0));    // S_BOS2_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 7, 2, StateAction.None, StateNum.S_BOS2_PAIN2, 0, 0));    // S_BOS2_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 7, 2, StateAction.None, StateNum.S_BOS2_RUN1, 0, 0));   // S_BOS2_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 8, 8, StateAction.None, StateNum.S_BOS2_DIE2, 0, 0)); // S_BOS2_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 9, 8, StateAction.None, StateNum.S_BOS2_DIE3, 0, 0)); // S_BOS2_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 10, 8, StateAction.None, StateNum.S_BOS2_DIE4, 0, 0));    // S_BOS2_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 11, 8, StateAction.Fall, StateNum.S_BOS2_DIE5, 0, 0));  // S_BOS2_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 12, 8, StateAction.None, StateNum.S_BOS2_DIE6, 0, 0));    // S_BOS2_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 13, 8, StateAction.None, StateNum.S_BOS2_DIE7, 0, 0));    // S_BOS2_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 14, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_BOS2_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 14, 8, StateAction.None, StateNum.S_BOS2_RAISE2, 0, 0));  // S_BOS2_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 13, 8, StateAction.None, StateNum.S_BOS2_RAISE3, 0, 0));  // S_BOS2_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 12, 8, StateAction.None, StateNum.S_BOS2_RAISE4, 0, 0));  // S_BOS2_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 11, 8, StateAction.None, StateNum.S_BOS2_RAISE5, 0, 0));  // S_BOS2_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 10, 8, StateAction.None, StateNum.S_BOS2_RAISE6, 0, 0));  // S_BOS2_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 9, 8, StateAction.None, StateNum.S_BOS2_RAISE7, 0, 0));   // S_BOS2_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_BOS2, 8, 8, StateAction.None, StateNum.S_BOS2_RUN1, 0, 0)); // S_BOS2_RAISE7
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32768, 10, StateAction.Look, StateNum.S_SKULL_STND2, 0, 0));    // S_SKULL_STND
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32769, 10, StateAction.Look, StateNum.S_SKULL_STND, 0, 0)); // S_SKULL_STND2
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32768, 6, StateAction.Chase, StateNum.S_SKULL_RUN2, 0, 0)); // S_SKULL_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32769, 6, StateAction.Chase, StateNum.S_SKULL_RUN1, 0, 0)); // S_SKULL_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32770, 10, StateAction.FaceTarget, StateNum.S_SKULL_ATK2, 0, 0));   // S_SKULL_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32771, 4, StateAction.SkullAttack, StateNum.S_SKULL_ATK3, 0, 0));   // S_SKULL_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32770, 4, StateAction.None, StateNum.S_SKULL_ATK4, 0, 0));    // S_SKULL_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32771, 4, StateAction.None, StateNum.S_SKULL_ATK3, 0, 0));    // S_SKULL_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32772, 3, StateAction.None, StateNum.S_SKULL_PAIN2, 0, 0));   // S_SKULL_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32772, 3, StateAction.None, StateNum.S_SKULL_RUN1, 0, 0));  // S_SKULL_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32773, 6, StateAction.None, StateNum.S_SKULL_DIE2, 0, 0));    // S_SKULL_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32774, 6, StateAction.None, StateNum.S_SKULL_DIE3, 0, 0));    // S_SKULL_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32775, 6, StateAction.None, StateNum.S_SKULL_DIE4, 0, 0));    // S_SKULL_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 32776, 6, StateAction.Fall, StateNum.S_SKULL_DIE5, 0, 0));  // S_SKULL_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 9, 6, StateAction.None, StateNum.S_SKULL_DIE6, 0, 0));    // S_SKULL_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_SKUL, 10, 6, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SKULL_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 0, 10, StateAction.Look, StateNum.S_SPID_STND2, 0, 0)); // S_SPID_STND
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 1, 10, StateAction.Look, StateNum.S_SPID_STND, 0, 0));  // S_SPID_STND2
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 0, 3, StateAction.Metal, StateNum.S_SPID_RUN2, 0, 0));  // S_SPID_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 0, 3, StateAction.Chase, StateNum.S_SPID_RUN3, 0, 0));  // S_SPID_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 1, 3, StateAction.Chase, StateNum.S_SPID_RUN4, 0, 0));  // S_SPID_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 1, 3, StateAction.Chase, StateNum.S_SPID_RUN5, 0, 0));  // S_SPID_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 2, 3, StateAction.Metal, StateNum.S_SPID_RUN6, 0, 0));  // S_SPID_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 2, 3, StateAction.Chase, StateNum.S_SPID_RUN7, 0, 0));  // S_SPID_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 3, 3, StateAction.Chase, StateNum.S_SPID_RUN8, 0, 0));  // S_SPID_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 3, 3, StateAction.Chase, StateNum.S_SPID_RUN9, 0, 0));  // S_SPID_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 4, 3, StateAction.Metal, StateNum.S_SPID_RUN10, 0, 0)); // S_SPID_RUN9
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 4, 3, StateAction.Chase, StateNum.S_SPID_RUN11, 0, 0)); // S_SPID_RUN10
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 5, 3, StateAction.Chase, StateNum.S_SPID_RUN12, 0, 0)); // S_SPID_RUN11
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 5, 3, StateAction.Chase, StateNum.S_SPID_RUN1, 0, 0));  // S_SPID_RUN12
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 32768, 20, StateAction.FaceTarget, StateNum.S_SPID_ATK2, 0, 0));    // S_SPID_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 32774, 4, StateAction.SPosAttack, StateNum.S_SPID_ATK3, 0, 0)); // S_SPID_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 32775, 4, StateAction.SPosAttack, StateNum.S_SPID_ATK4, 0, 0)); // S_SPID_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 32775, 1, StateAction.SpidRefire, StateNum.S_SPID_ATK2, 0, 0)); // S_SPID_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 8, 3, StateAction.None, StateNum.S_SPID_PAIN2, 0, 0));    // S_SPID_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 8, 3, StateAction.None, StateNum.S_SPID_RUN1, 0, 0));   // S_SPID_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 9, 20, StateAction.None, StateNum.S_SPID_DIE2, 0, 0));    // S_SPID_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 10, 10, StateAction.Fall, StateNum.S_SPID_DIE3, 0, 0)); // S_SPID_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 11, 10, StateAction.None, StateNum.S_SPID_DIE4, 0, 0));   // S_SPID_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 12, 10, StateAction.None, StateNum.S_SPID_DIE5, 0, 0));   // S_SPID_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 13, 10, StateAction.None, StateNum.S_SPID_DIE6, 0, 0));   // S_SPID_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 14, 10, StateAction.None, StateNum.S_SPID_DIE7, 0, 0));   // S_SPID_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 15, 10, StateAction.None, StateNum.S_SPID_DIE8, 0, 0));   // S_SPID_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 16, 10, StateAction.None, StateNum.S_SPID_DIE9, 0, 0));   // S_SPID_DIE8
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 17, 10, StateAction.None, StateNum.S_SPID_DIE10, 0, 0));  // S_SPID_DIE9
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 18, 30, StateAction.None, StateNum.S_SPID_DIE11, 0, 0));  // S_SPID_DIE10
        AddPredefinedState(new State(SpriteNum.SPR_SPID, 18, -1, StateAction.BossDeath, StateNum.S_NULL, 0, 0)); // S_SPID_DIE11
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 0, 10, StateAction.Look, StateNum.S_BSPI_STND2, 0, 0)); // S_BSPI_STND
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 1, 10, StateAction.Look, StateNum.S_BSPI_STND, 0, 0));  // S_BSPI_STND2
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 0, 20, StateAction.None, StateNum.S_BSPI_RUN1, 0, 0));    // S_BSPI_SIGHT
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 0, 3, StateAction.BabyMetal, StateNum.S_BSPI_RUN2, 0, 0));  // S_BSPI_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 0, 3, StateAction.Chase, StateNum.S_BSPI_RUN3, 0, 0));  // S_BSPI_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 1, 3, StateAction.Chase, StateNum.S_BSPI_RUN4, 0, 0));  // S_BSPI_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 1, 3, StateAction.Chase, StateNum.S_BSPI_RUN5, 0, 0));  // S_BSPI_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 2, 3, StateAction.Chase, StateNum.S_BSPI_RUN6, 0, 0));  // S_BSPI_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 2, 3, StateAction.Chase, StateNum.S_BSPI_RUN7, 0, 0));  // S_BSPI_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 3, 3, StateAction.BabyMetal, StateNum.S_BSPI_RUN8, 0, 0));  // S_BSPI_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 3, 3, StateAction.Chase, StateNum.S_BSPI_RUN9, 0, 0));  // S_BSPI_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 4, 3, StateAction.Chase, StateNum.S_BSPI_RUN10, 0, 0)); // S_BSPI_RUN9
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 4, 3, StateAction.Chase, StateNum.S_BSPI_RUN11, 0, 0)); // S_BSPI_RUN10
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 5, 3, StateAction.Chase, StateNum.S_BSPI_RUN12, 0, 0)); // S_BSPI_RUN11
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 5, 3, StateAction.Chase, StateNum.S_BSPI_RUN1, 0, 0));  // S_BSPI_RUN12
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 32768, 20, StateAction.FaceTarget, StateNum.S_BSPI_ATK2, 0, 0));    // S_BSPI_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 32774, 4, StateAction.BspiAttack, StateNum.S_BSPI_ATK3, 0, 0)); // S_BSPI_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 32775, 4, StateAction.None, StateNum.S_BSPI_ATK4, 0, 0)); // S_BSPI_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 32775, 1, StateAction.SpidRefire, StateNum.S_BSPI_ATK2, 0, 0)); // S_BSPI_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 8, 3, StateAction.None, StateNum.S_BSPI_PAIN2, 0, 0));    // S_BSPI_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 8, 3, StateAction.None, StateNum.S_BSPI_RUN1, 0, 0));   // S_BSPI_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 9, 20, StateAction.None, StateNum.S_BSPI_DIE2, 0, 0));    // S_BSPI_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 10, 7, StateAction.Fall, StateNum.S_BSPI_DIE3, 0, 0));  // S_BSPI_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 11, 7, StateAction.None, StateNum.S_BSPI_DIE4, 0, 0));    // S_BSPI_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 12, 7, StateAction.None, StateNum.S_BSPI_DIE5, 0, 0));    // S_BSPI_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 13, 7, StateAction.None, StateNum.S_BSPI_DIE6, 0, 0));    // S_BSPI_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 14, 7, StateAction.None, StateNum.S_BSPI_DIE7, 0, 0));    // S_BSPI_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 15, -1, StateAction.BossDeath, StateNum.S_NULL, 0, 0)); // S_BSPI_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 15, 5, StateAction.None, StateNum.S_BSPI_RAISE2, 0, 0));  // S_BSPI_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 14, 5, StateAction.None, StateNum.S_BSPI_RAISE3, 0, 0));  // S_BSPI_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 13, 5, StateAction.None, StateNum.S_BSPI_RAISE4, 0, 0));  // S_BSPI_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 12, 5, StateAction.None, StateNum.S_BSPI_RAISE5, 0, 0));  // S_BSPI_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 11, 5, StateAction.None, StateNum.S_BSPI_RAISE6, 0, 0));  // S_BSPI_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 10, 5, StateAction.None, StateNum.S_BSPI_RAISE7, 0, 0));  // S_BSPI_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_BSPI, 9, 5, StateAction.None, StateNum.S_BSPI_RUN1, 0, 0)); // S_BSPI_RAISE7
        AddPredefinedState(new State(SpriteNum.SPR_APLS, 32768, 5, StateAction.None, StateNum.S_ARACH_PLAZ2, 0, 0));   // S_ARACH_PLAZ
        AddPredefinedState(new State(SpriteNum.SPR_APLS, 32769, 5, StateAction.None, StateNum.S_ARACH_PLAZ, 0, 0));    // S_ARACH_PLAZ2
        AddPredefinedState(new State(SpriteNum.SPR_APBX, 32768, 5, StateAction.None, StateNum.S_ARACH_PLEX2, 0, 0));   // S_ARACH_PLEX
        AddPredefinedState(new State(SpriteNum.SPR_APBX, 32769, 5, StateAction.None, StateNum.S_ARACH_PLEX3, 0, 0));   // S_ARACH_PLEX2
        AddPredefinedState(new State(SpriteNum.SPR_APBX, 32770, 5, StateAction.None, StateNum.S_ARACH_PLEX4, 0, 0));   // S_ARACH_PLEX3
        AddPredefinedState(new State(SpriteNum.SPR_APBX, 32771, 5, StateAction.None, StateNum.S_ARACH_PLEX5, 0, 0));   // S_ARACH_PLEX4
        AddPredefinedState(new State(SpriteNum.SPR_APBX, 32772, 5, StateAction.None, StateNum.S_NULL, 0, 0));  // S_ARACH_PLEX5
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 0, 10, StateAction.Look, StateNum.S_CYBER_STND2, 0, 0));    // S_CYBER_STND
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 1, 10, StateAction.Look, StateNum.S_CYBER_STND, 0, 0)); // S_CYBER_STND2
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 0, 3, StateAction.Hoof, StateNum.S_CYBER_RUN2, 0, 0));  // S_CYBER_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 0, 3, StateAction.Chase, StateNum.S_CYBER_RUN3, 0, 0)); // S_CYBER_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 1, 3, StateAction.Chase, StateNum.S_CYBER_RUN4, 0, 0)); // S_CYBER_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 1, 3, StateAction.Chase, StateNum.S_CYBER_RUN5, 0, 0)); // S_CYBER_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 2, 3, StateAction.Chase, StateNum.S_CYBER_RUN6, 0, 0)); // S_CYBER_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 2, 3, StateAction.Chase, StateNum.S_CYBER_RUN7, 0, 0)); // S_CYBER_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 3, 3, StateAction.Metal, StateNum.S_CYBER_RUN8, 0, 0)); // S_CYBER_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 3, 3, StateAction.Chase, StateNum.S_CYBER_RUN1, 0, 0)); // S_CYBER_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 4, 6, StateAction.FaceTarget, StateNum.S_CYBER_ATK2, 0, 0));    // S_CYBER_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 5, 12, StateAction.CyberAttack, StateNum.S_CYBER_ATK3, 0, 0));  // S_CYBER_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 4, 12, StateAction.FaceTarget, StateNum.S_CYBER_ATK4, 0, 0));   // S_CYBER_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 5, 12, StateAction.CyberAttack, StateNum.S_CYBER_ATK5, 0, 0));  // S_CYBER_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 4, 12, StateAction.FaceTarget, StateNum.S_CYBER_ATK6, 0, 0));   // S_CYBER_ATK5
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 5, 12, StateAction.CyberAttack, StateNum.S_CYBER_RUN1, 0, 0));  // S_CYBER_ATK6
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 6, 10, StateAction.None, StateNum.S_CYBER_RUN1, 0, 0)); // S_CYBER_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 7, 10, StateAction.None, StateNum.S_CYBER_DIE2, 0, 0));   // S_CYBER_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 8, 10, StateAction.None, StateNum.S_CYBER_DIE3, 0, 0));   // S_CYBER_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 9, 10, StateAction.None, StateNum.S_CYBER_DIE4, 0, 0));   // S_CYBER_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 10, 10, StateAction.None, StateNum.S_CYBER_DIE5, 0, 0));  // S_CYBER_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 11, 10, StateAction.None, StateNum.S_CYBER_DIE6, 0, 0));  // S_CYBER_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 12, 10, StateAction.Fall, StateNum.S_CYBER_DIE7, 0, 0));    // S_CYBER_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 13, 10, StateAction.None, StateNum.S_CYBER_DIE8, 0, 0));  // S_CYBER_DIE7
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 14, 10, StateAction.None, StateNum.S_CYBER_DIE9, 0, 0));  // S_CYBER_DIE8
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 15, 30, StateAction.None, StateNum.S_CYBER_DIE10, 0, 0)); // S_CYBER_DIE9
        AddPredefinedState(new State(SpriteNum.SPR_CYBR, 15, -1, StateAction.BossDeath, StateNum.S_NULL, 0, 0)); // S_CYBER_DIE10
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 0, 10, StateAction.Look, StateNum.S_PAIN_STND, 0, 0));  // S_PAIN_STND
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 0, 3, StateAction.Chase, StateNum.S_PAIN_RUN2, 0, 0));  // S_PAIN_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 0, 3, StateAction.Chase, StateNum.S_PAIN_RUN3, 0, 0));  // S_PAIN_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 1, 3, StateAction.Chase, StateNum.S_PAIN_RUN4, 0, 0));  // S_PAIN_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 1, 3, StateAction.Chase, StateNum.S_PAIN_RUN5, 0, 0));  // S_PAIN_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 2, 3, StateAction.Chase, StateNum.S_PAIN_RUN6, 0, 0));  // S_PAIN_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 2, 3, StateAction.Chase, StateNum.S_PAIN_RUN1, 0, 0));  // S_PAIN_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 3, 5, StateAction.FaceTarget, StateNum.S_PAIN_ATK2, 0, 0)); // S_PAIN_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 4, 5, StateAction.FaceTarget, StateNum.S_PAIN_ATK3, 0, 0)); // S_PAIN_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32773, 5, StateAction.FaceTarget, StateNum.S_PAIN_ATK4, 0, 0)); // S_PAIN_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32773, 0, StateAction.PainAttack, StateNum.S_PAIN_RUN1, 0, 0)); // S_PAIN_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 6, 6, StateAction.None, StateNum.S_PAIN_PAIN2, 0, 0));    // S_PAIN_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 6, 6, StateAction.None, StateNum.S_PAIN_RUN1, 0, 0));   // S_PAIN_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32775, 8, StateAction.None, StateNum.S_PAIN_DIE2, 0, 0)); // S_PAIN_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32776, 8, StateAction.None, StateNum.S_PAIN_DIE3, 0, 0)); // S_PAIN_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32777, 8, StateAction.None, StateNum.S_PAIN_DIE4, 0, 0)); // S_PAIN_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32778, 8, StateAction.None, StateNum.S_PAIN_DIE5, 0, 0)); // S_PAIN_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32779, 8, StateAction.PainDie, StateNum.S_PAIN_DIE6, 0, 0));    // S_PAIN_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 32780, 8, StateAction.None, StateNum.S_NULL, 0, 0));  // S_PAIN_DIE6
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 12, 8, StateAction.None, StateNum.S_PAIN_RAISE2, 0, 0));  // S_PAIN_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 11, 8, StateAction.None, StateNum.S_PAIN_RAISE3, 0, 0));  // S_PAIN_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 10, 8, StateAction.None, StateNum.S_PAIN_RAISE4, 0, 0));  // S_PAIN_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 9, 8, StateAction.None, StateNum.S_PAIN_RAISE5, 0, 0));   // S_PAIN_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 8, 8, StateAction.None, StateNum.S_PAIN_RAISE6, 0, 0));   // S_PAIN_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_PAIN, 7, 8, StateAction.None, StateNum.S_PAIN_RUN1, 0, 0)); // S_PAIN_RAISE6
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 0, 10, StateAction.Look, StateNum.S_SSWV_STND2, 0, 0)); // S_SSWV_STND
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 1, 10, StateAction.Look, StateNum.S_SSWV_STND, 0, 0));  // S_SSWV_STND2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 0, 3, StateAction.Chase, StateNum.S_SSWV_RUN2, 0, 0));  // S_SSWV_RUN1
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 0, 3, StateAction.Chase, StateNum.S_SSWV_RUN3, 0, 0));  // S_SSWV_RUN2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 1, 3, StateAction.Chase, StateNum.S_SSWV_RUN4, 0, 0));  // S_SSWV_RUN3
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 1, 3, StateAction.Chase, StateNum.S_SSWV_RUN5, 0, 0));  // S_SSWV_RUN4
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 2, 3, StateAction.Chase, StateNum.S_SSWV_RUN6, 0, 0));  // S_SSWV_RUN5
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 2, 3, StateAction.Chase, StateNum.S_SSWV_RUN7, 0, 0));  // S_SSWV_RUN6
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 3, 3, StateAction.Chase, StateNum.S_SSWV_RUN8, 0, 0));  // S_SSWV_RUN7
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 3, 3, StateAction.Chase, StateNum.S_SSWV_RUN1, 0, 0));  // S_SSWV_RUN8
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 4, 10, StateAction.FaceTarget, StateNum.S_SSWV_ATK2, 0, 0));    // S_SSWV_ATK1
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 5, 10, StateAction.FaceTarget, StateNum.S_SSWV_ATK3, 0, 0));    // S_SSWV_ATK2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 32774, 4, StateAction.CPosAttack, StateNum.S_SSWV_ATK4, 0, 0)); // S_SSWV_ATK3
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 5, 6, StateAction.FaceTarget, StateNum.S_SSWV_ATK5, 0, 0)); // S_SSWV_ATK4
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 32774, 4, StateAction.CPosAttack, StateNum.S_SSWV_ATK6, 0, 0)); // S_SSWV_ATK5
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 5, 1, StateAction.CPosRefire, StateNum.S_SSWV_ATK2, 0, 0)); // S_SSWV_ATK6
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 7, 3, StateAction.None, StateNum.S_SSWV_PAIN2, 0, 0));    // S_SSWV_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 7, 3, StateAction.None, StateNum.S_SSWV_RUN1, 0, 0));   // S_SSWV_PAIN2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 8, 5, StateAction.None, StateNum.S_SSWV_DIE2, 0, 0)); // S_SSWV_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 9, 5, StateAction.None, StateNum.S_SSWV_DIE3, 0, 0)); // S_SSWV_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 10, 5, StateAction.Fall, StateNum.S_SSWV_DIE4, 0, 0));  // S_SSWV_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 11, 5, StateAction.None, StateNum.S_SSWV_DIE5, 0, 0));    // S_SSWV_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 12, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_SSWV_DIE5
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 13, 5, StateAction.None, StateNum.S_SSWV_XDIE2, 0, 0));   // S_SSWV_XDIE1
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 14, 5, StateAction.XScream, StateNum.S_SSWV_XDIE3, 0, 0));  // S_SSWV_XDIE2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 15, 5, StateAction.Fall, StateNum.S_SSWV_XDIE4, 0, 0)); // S_SSWV_XDIE3
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 16, 5, StateAction.None, StateNum.S_SSWV_XDIE5, 0, 0));   // S_SSWV_XDIE4
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 17, 5, StateAction.None, StateNum.S_SSWV_XDIE6, 0, 0));   // S_SSWV_XDIE5
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 18, 5, StateAction.None, StateNum.S_SSWV_XDIE7, 0, 0));   // S_SSWV_XDIE6
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 19, 5, StateAction.None, StateNum.S_SSWV_XDIE8, 0, 0));   // S_SSWV_XDIE7
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 20, 5, StateAction.None, StateNum.S_SSWV_XDIE9, 0, 0));   // S_SSWV_XDIE8
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 21, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_SSWV_XDIE9
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 12, 5, StateAction.None, StateNum.S_SSWV_RAISE2, 0, 0));  // S_SSWV_RAISE1
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 11, 5, StateAction.None, StateNum.S_SSWV_RAISE3, 0, 0));  // S_SSWV_RAISE2
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 10, 5, StateAction.None, StateNum.S_SSWV_RAISE4, 0, 0));  // S_SSWV_RAISE3
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 9, 5, StateAction.None, StateNum.S_SSWV_RAISE5, 0, 0));   // S_SSWV_RAISE4
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 8, 5, StateAction.None, StateNum.S_SSWV_RUN1, 0, 0)); // S_SSWV_RAISE5
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 0, -1, StateAction.None, StateNum.S_KEENSTND, 0, 0)); // S_KEENSTND
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 0, 6, StateAction.None, StateNum.S_COMMKEEN2, 0, 0)); // S_COMMKEEN
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 1, 6, StateAction.None, StateNum.S_COMMKEEN3, 0, 0)); // S_COMMKEEN2
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 2, 6, StateAction.None, StateNum.S_COMMKEEN4, 0, 0)); // S_COMMKEEN3
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 3, 6, StateAction.None, StateNum.S_COMMKEEN5, 0, 0)); // S_COMMKEEN4
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 4, 6, StateAction.None, StateNum.S_COMMKEEN6, 0, 0)); // S_COMMKEEN5
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 5, 6, StateAction.None, StateNum.S_COMMKEEN7, 0, 0)); // S_COMMKEEN6
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 6, 6, StateAction.None, StateNum.S_COMMKEEN8, 0, 0)); // S_COMMKEEN7
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 7, 6, StateAction.None, StateNum.S_COMMKEEN9, 0, 0)); // S_COMMKEEN8
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 8, 6, StateAction.None, StateNum.S_COMMKEEN10, 0, 0));    // S_COMMKEEN9
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 9, 6, StateAction.None, StateNum.S_COMMKEEN11, 0, 0));    // S_COMMKEEN10
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 10, 6, StateAction.KeenDie, StateNum.S_COMMKEEN12, 0, 0));// S_COMMKEEN11
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 11, -1, StateAction.None, StateNum.S_NULL, 0, 0));     // S_COMMKEEN12
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 12, 4, StateAction.None, StateNum.S_KEENPAIN2, 0, 0));    // S_KEENPAIN
        AddPredefinedState(new State(SpriteNum.SPR_KEEN, 12, 8, StateAction.None, StateNum.S_KEENSTND, 0, 0));   // S_KEENPAIN2
        AddPredefinedState(new State(SpriteNum.SPR_BBRN, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0));      // S_BRAIN
        AddPredefinedState(new State(SpriteNum.SPR_BBRN, 1, 36, StateAction.BrainPain, StateNum.S_BRAIN, 0, 0)); // S_BRAIN_PAIN
        AddPredefinedState(new State(SpriteNum.SPR_BBRN, 0, 100, StateAction.BrainScream, StateNum.S_BRAIN_DIE2, 0, 0)); // S_BRAIN_DIE1
        AddPredefinedState(new State(SpriteNum.SPR_BBRN, 0, 10, StateAction.None, StateNum.S_BRAIN_DIE3, 0, 0));   // S_BRAIN_DIE2
        AddPredefinedState(new State(SpriteNum.SPR_BBRN, 0, 10, StateAction.None, StateNum.S_BRAIN_DIE4, 0, 0));   // S_BRAIN_DIE3
        AddPredefinedState(new State(SpriteNum.SPR_BBRN, 0, -1, StateAction.BrainDie, StateNum.S_NULL, 0, 0));   // S_BRAIN_DIE4
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 0, 10, StateAction.Look, StateNum.S_BRAINEYE, 0, 0));   // S_BRAINEYE
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 0, 181, StateAction.BrainAwake, StateNum.S_BRAINEYE1, 0, 0));   // S_BRAINEYESEE
        AddPredefinedState(new State(SpriteNum.SPR_SSWV, 0, 150, StateAction.BrainSpit, StateNum.S_BRAINEYE1, 0, 0));    // S_BRAINEYE1
        AddPredefinedState(new State(SpriteNum.SPR_BOSF, 32768, 3, StateAction.SpawnFly, StateNum.S_SPAWN2, 0, 0));    // S_SPAWN1
        AddPredefinedState(new State(SpriteNum.SPR_BOSF, 32769, 3, StateAction.SpawnFly, StateNum.S_SPAWN3, 0, 0));  // S_SPAWN2
        AddPredefinedState(new State(SpriteNum.SPR_BOSF, 32770, 3, StateAction.SpawnFly, StateNum.S_SPAWN4, 0, 0));  // S_SPAWN3
        AddPredefinedState(new State(SpriteNum.SPR_BOSF, 32771, 3, StateAction.SpawnFly, StateNum.S_SPAWN1, 0, 0));  // S_SPAWN4
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32768, 4, StateAction.Fire, StateNum.S_SPAWNFIRE2, 0, 0));  // S_SPAWNFIRE1
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32769, 4, StateAction.Fire, StateNum.S_SPAWNFIRE3, 0, 0));  // S_SPAWNFIRE2
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32770, 4, StateAction.Fire, StateNum.S_SPAWNFIRE4, 0, 0));  // S_SPAWNFIRE3
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32771, 4, StateAction.Fire, StateNum.S_SPAWNFIRE5, 0, 0));  // S_SPAWNFIRE4
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32772, 4, StateAction.Fire, StateNum.S_SPAWNFIRE6, 0, 0));  // S_SPAWNFIRE5
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32773, 4, StateAction.Fire, StateNum.S_SPAWNFIRE7, 0, 0));  // S_SPAWNFIRE6
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32774, 4, StateAction.Fire, StateNum.S_SPAWNFIRE8, 0, 0));  // S_SPAWNFIRE7
        AddPredefinedState(new State(SpriteNum.SPR_FIRE, 32775, 4, StateAction.Fire, StateNum.S_NULL, 0, 0));        // S_SPAWNFIRE8
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32769, 10, StateAction.None, StateNum.S_BRAINEXPLODE2, 0, 0));    // S_BRAINEXPLODE1
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32770, 10, StateAction.None, StateNum.S_BRAINEXPLODE3, 0, 0));    // S_BRAINEXPLODE2
        AddPredefinedState(new State(SpriteNum.SPR_MISL, 32771, 10, StateAction.BrainExplode, StateNum.S_NULL, 0, 0));   // S_BRAINEXPLODE3
        AddPredefinedState(new State(SpriteNum.SPR_ARM1, 0, 6, StateAction.None, StateNum.S_ARM1A, 0, 0)); // S_ARM1
        AddPredefinedState(new State(SpriteNum.SPR_ARM1, 32769, 7, StateAction.None, StateNum.S_ARM1, 0, 0));  // S_ARM1A
        AddPredefinedState(new State(SpriteNum.SPR_ARM2, 0, 6, StateAction.None, StateNum.S_ARM2A, 0, 0)); // S_ARM2
        AddPredefinedState(new State(SpriteNum.SPR_ARM2, 32769, 6, StateAction.None, StateNum.S_ARM2, 0, 0));  // S_ARM2A
        AddPredefinedState(new State(SpriteNum.SPR_BAR1, 0, 6, StateAction.None, StateNum.S_BAR2, 0, 0));  // S_BAR1
        AddPredefinedState(new State(SpriteNum.SPR_BAR1, 1, 6, StateAction.None, StateNum.S_BAR1, 0, 0));  // S_BAR2
        AddPredefinedState(new State(SpriteNum.SPR_BEXP, 32768, 5, StateAction.None, StateNum.S_BEXP2, 0, 0)); // S_BEXP
        AddPredefinedState(new State(SpriteNum.SPR_BEXP, 32769, 5, StateAction.None, StateNum.S_BEXP3, 0, 0)); // S_BEXP2
        AddPredefinedState(new State(SpriteNum.SPR_BEXP, 32770, 5, StateAction.None, StateNum.S_BEXP4, 0, 0)); // S_BEXP3
        AddPredefinedState(new State(SpriteNum.SPR_BEXP, 32771, 10, StateAction.Explode, StateNum.S_BEXP5, 0, 0));   // S_BEXP4
        AddPredefinedState(new State(SpriteNum.SPR_BEXP, 32772, 10, StateAction.None, StateNum.S_NULL, 0, 0)); // S_BEXP5
        AddPredefinedState(new State(SpriteNum.SPR_FCAN, 32768, 4, StateAction.None, StateNum.S_BBAR2, 0, 0)); // S_BBAR1
        AddPredefinedState(new State(SpriteNum.SPR_FCAN, 32769, 4, StateAction.None, StateNum.S_BBAR3, 0, 0)); // S_BBAR2
        AddPredefinedState(new State(SpriteNum.SPR_FCAN, 32770, 4, StateAction.None, StateNum.S_BBAR1, 0, 0)); // S_BBAR3
        AddPredefinedState(new State(SpriteNum.SPR_BON1, 0, 6, StateAction.None, StateNum.S_BON1A, 0, 0)); // S_BON1
        AddPredefinedState(new State(SpriteNum.SPR_BON1, 1, 6, StateAction.None, StateNum.S_BON1B, 0, 0)); // S_BON1A
        AddPredefinedState(new State(SpriteNum.SPR_BON1, 2, 6, StateAction.None, StateNum.S_BON1C, 0, 0)); // S_BON1B
        AddPredefinedState(new State(SpriteNum.SPR_BON1, 3, 6, StateAction.None, StateNum.S_BON1D, 0, 0)); // S_BON1C
        AddPredefinedState(new State(SpriteNum.SPR_BON1, 2, 6, StateAction.None, StateNum.S_BON1E, 0, 0)); // S_BON1D
        AddPredefinedState(new State(SpriteNum.SPR_BON1, 1, 6, StateAction.None, StateNum.S_BON1, 0, 0));  // S_BON1E
        AddPredefinedState(new State(SpriteNum.SPR_BON2, 0, 6, StateAction.None, StateNum.S_BON2A, 0, 0)); // S_BON2
        AddPredefinedState(new State(SpriteNum.SPR_BON2, 1, 6, StateAction.None, StateNum.S_BON2B, 0, 0)); // S_BON2A
        AddPredefinedState(new State(SpriteNum.SPR_BON2, 2, 6, StateAction.None, StateNum.S_BON2C, 0, 0)); // S_BON2B
        AddPredefinedState(new State(SpriteNum.SPR_BON2, 3, 6, StateAction.None, StateNum.S_BON2D, 0, 0)); // S_BON2C
        AddPredefinedState(new State(SpriteNum.SPR_BON2, 2, 6, StateAction.None, StateNum.S_BON2E, 0, 0)); // S_BON2D
        AddPredefinedState(new State(SpriteNum.SPR_BON2, 1, 6, StateAction.None, StateNum.S_BON2, 0, 0));  // S_BON2E
        AddPredefinedState(new State(SpriteNum.SPR_BKEY, 0, 10, StateAction.None, StateNum.S_BKEY2, 0, 0));    // S_BKEY
        AddPredefinedState(new State(SpriteNum.SPR_BKEY, 32769, 10, StateAction.None, StateNum.S_BKEY, 0, 0)); // S_BKEY2
        AddPredefinedState(new State(SpriteNum.SPR_RKEY, 0, 10, StateAction.None, StateNum.S_RKEY2, 0, 0));    // S_RKEY
        AddPredefinedState(new State(SpriteNum.SPR_RKEY, 32769, 10, StateAction.None, StateNum.S_RKEY, 0, 0)); // S_RKEY2
        AddPredefinedState(new State(SpriteNum.SPR_YKEY, 0, 10, StateAction.None, StateNum.S_YKEY2, 0, 0));    // S_YKEY
        AddPredefinedState(new State(SpriteNum.SPR_YKEY, 32769, 10, StateAction.None, StateNum.S_YKEY, 0, 0)); // S_YKEY2
        AddPredefinedState(new State(SpriteNum.SPR_BSKU, 0, 10, StateAction.None, StateNum.S_BSKULL2, 0, 0));  // S_BSKULL
        AddPredefinedState(new State(SpriteNum.SPR_BSKU, 32769, 10, StateAction.None, StateNum.S_BSKULL, 0, 0));   // S_BSKULL2
        AddPredefinedState(new State(SpriteNum.SPR_RSKU, 0, 10, StateAction.None, StateNum.S_RSKULL2, 0, 0));  // S_RSKULL
        AddPredefinedState(new State(SpriteNum.SPR_RSKU, 32769, 10, StateAction.None, StateNum.S_RSKULL, 0, 0));   // S_RSKULL2
        AddPredefinedState(new State(SpriteNum.SPR_YSKU, 0, 10, StateAction.None, StateNum.S_YSKULL2, 0, 0));  // S_YSKULL
        AddPredefinedState(new State(SpriteNum.SPR_YSKU, 32769, 10, StateAction.None, StateNum.S_YSKULL, 0, 0));   // S_YSKULL2
        AddPredefinedState(new State(SpriteNum.SPR_STIM, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_STIM
        AddPredefinedState(new State(SpriteNum.SPR_MEDI, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_MEDI
        AddPredefinedState(new State(SpriteNum.SPR_SOUL, 32768, 6, StateAction.None, StateNum.S_SOUL2, 0, 0)); // S_SOUL
        AddPredefinedState(new State(SpriteNum.SPR_SOUL, 32769, 6, StateAction.None, StateNum.S_SOUL3, 0, 0)); // S_SOUL2
        AddPredefinedState(new State(SpriteNum.SPR_SOUL, 32770, 6, StateAction.None, StateNum.S_SOUL4, 0, 0)); // S_SOUL3
        AddPredefinedState(new State(SpriteNum.SPR_SOUL, 32771, 6, StateAction.None, StateNum.S_SOUL5, 0, 0)); // S_SOUL4
        AddPredefinedState(new State(SpriteNum.SPR_SOUL, 32770, 6, StateAction.None, StateNum.S_SOUL6, 0, 0)); // S_SOUL5
        AddPredefinedState(new State(SpriteNum.SPR_SOUL, 32769, 6, StateAction.None, StateNum.S_SOUL, 0, 0));  // S_SOUL6
        AddPredefinedState(new State(SpriteNum.SPR_PINV, 32768, 6, StateAction.None, StateNum.S_PINV2, 0, 0)); // S_PINV
        AddPredefinedState(new State(SpriteNum.SPR_PINV, 32769, 6, StateAction.None, StateNum.S_PINV3, 0, 0)); // S_PINV2
        AddPredefinedState(new State(SpriteNum.SPR_PINV, 32770, 6, StateAction.None, StateNum.S_PINV4, 0, 0)); // S_PINV3
        AddPredefinedState(new State(SpriteNum.SPR_PINV, 32771, 6, StateAction.None, StateNum.S_PINV, 0, 0));  // S_PINV4
        AddPredefinedState(new State(SpriteNum.SPR_PSTR, 32768, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_PSTR
        AddPredefinedState(new State(SpriteNum.SPR_PINS, 32768, 6, StateAction.None, StateNum.S_PINS2, 0, 0)); // S_PINS
        AddPredefinedState(new State(SpriteNum.SPR_PINS, 32769, 6, StateAction.None, StateNum.S_PINS3, 0, 0)); // S_PINS2
        AddPredefinedState(new State(SpriteNum.SPR_PINS, 32770, 6, StateAction.None, StateNum.S_PINS4, 0, 0)); // S_PINS3
        AddPredefinedState(new State(SpriteNum.SPR_PINS, 32771, 6, StateAction.None, StateNum.S_PINS, 0, 0));  // S_PINS4
        AddPredefinedState(new State(SpriteNum.SPR_MEGA, 32768, 6, StateAction.None, StateNum.S_MEGA2, 0, 0)); // S_MEGA
        AddPredefinedState(new State(SpriteNum.SPR_MEGA, 32769, 6, StateAction.None, StateNum.S_MEGA3, 0, 0)); // S_MEGA2
        AddPredefinedState(new State(SpriteNum.SPR_MEGA, 32770, 6, StateAction.None, StateNum.S_MEGA4, 0, 0)); // S_MEGA3
        AddPredefinedState(new State(SpriteNum.SPR_MEGA, 32771, 6, StateAction.None, StateNum.S_MEGA, 0, 0));  // S_MEGA4
        AddPredefinedState(new State(SpriteNum.SPR_SUIT, 32768, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SUIT
        AddPredefinedState(new State(SpriteNum.SPR_PMAP, 32768, 6, StateAction.None, StateNum.S_PMAP2, 0, 0)); // S_PMAP
        AddPredefinedState(new State(SpriteNum.SPR_PMAP, 32769, 6, StateAction.None, StateNum.S_PMAP3, 0, 0)); // S_PMAP2
        AddPredefinedState(new State(SpriteNum.SPR_PMAP, 32770, 6, StateAction.None, StateNum.S_PMAP4, 0, 0)); // S_PMAP3
        AddPredefinedState(new State(SpriteNum.SPR_PMAP, 32771, 6, StateAction.None, StateNum.S_PMAP5, 0, 0)); // S_PMAP4
        AddPredefinedState(new State(SpriteNum.SPR_PMAP, 32770, 6, StateAction.None, StateNum.S_PMAP6, 0, 0)); // S_PMAP5
        AddPredefinedState(new State(SpriteNum.SPR_PMAP, 32769, 6, StateAction.None, StateNum.S_PMAP, 0, 0));  // S_PMAP6
        AddPredefinedState(new State(SpriteNum.SPR_PVIS, 32768, 6, StateAction.None, StateNum.S_PVIS2, 0, 0)); // S_PVIS
        AddPredefinedState(new State(SpriteNum.SPR_PVIS, 1, 6, StateAction.None, StateNum.S_PVIS, 0, 0));  // S_PVIS2
        AddPredefinedState(new State(SpriteNum.SPR_CLIP, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_CLIP
        AddPredefinedState(new State(SpriteNum.SPR_AMMO, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_AMMO
        AddPredefinedState(new State(SpriteNum.SPR_ROCK, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_ROCK
        AddPredefinedState(new State(SpriteNum.SPR_BROK, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_BROK
        AddPredefinedState(new State(SpriteNum.SPR_CELL, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_CELL
        AddPredefinedState(new State(SpriteNum.SPR_CELP, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_CELP
        AddPredefinedState(new State(SpriteNum.SPR_SHEL, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SHEL
        AddPredefinedState(new State(SpriteNum.SPR_SBOX, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SBOX
        AddPredefinedState(new State(SpriteNum.SPR_BPAK, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_BPAK
        AddPredefinedState(new State(SpriteNum.SPR_BFUG, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_BFUG
        AddPredefinedState(new State(SpriteNum.SPR_MGUN, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_MGUN
        AddPredefinedState(new State(SpriteNum.SPR_CSAW, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_CSAW
        AddPredefinedState(new State(SpriteNum.SPR_LAUN, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_LAUN
        AddPredefinedState(new State(SpriteNum.SPR_PLAS, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_PLAS
        AddPredefinedState(new State(SpriteNum.SPR_SHOT, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SHOT
        AddPredefinedState(new State(SpriteNum.SPR_SGN2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SHOT2
        AddPredefinedState(new State(SpriteNum.SPR_COLU, 32768, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_COLU
        AddPredefinedState(new State(SpriteNum.SPR_SMT2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_STALAG
        AddPredefinedState(new State(SpriteNum.SPR_GOR1, 0, 10, StateAction.None, StateNum.S_BLOODYTWITCH2, 0, 0));    // S_BLOODYTWITCH
        AddPredefinedState(new State(SpriteNum.SPR_GOR1, 1, 15, StateAction.None, StateNum.S_BLOODYTWITCH3, 0, 0));    // S_BLOODYTWITCH2
        AddPredefinedState(new State(SpriteNum.SPR_GOR1, 2, 8, StateAction.None, StateNum.S_BLOODYTWITCH4, 0, 0)); // S_BLOODYTWITCH3
        AddPredefinedState(new State(SpriteNum.SPR_GOR1, 1, 6, StateAction.None, StateNum.S_BLOODYTWITCH, 0, 0));  // S_BLOODYTWITCH4
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 13, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_DEADTORSO
        AddPredefinedState(new State(SpriteNum.SPR_PLAY, 18, -1, StateAction.None, StateNum.S_NULL, 0, 0));    // S_DEADBOTTOM
        AddPredefinedState(new State(SpriteNum.SPR_POL2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HEADSONSTICK
        AddPredefinedState(new State(SpriteNum.SPR_POL5, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_GIBS
        AddPredefinedState(new State(SpriteNum.SPR_POL4, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HEADONASTICK
        AddPredefinedState(new State(SpriteNum.SPR_POL3, 32768, 6, StateAction.None, StateNum.S_HEADCANDLES2, 0, 0));  // S_HEADCANDLES
        AddPredefinedState(new State(SpriteNum.SPR_POL3, 32769, 6, StateAction.None, StateNum.S_HEADCANDLES, 0, 0));   // S_HEADCANDLES2
        AddPredefinedState(new State(SpriteNum.SPR_POL1, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_DEADSTICK
        AddPredefinedState(new State(SpriteNum.SPR_POL6, 0, 6, StateAction.None, StateNum.S_LIVESTICK2, 0, 0));    // S_LIVESTICK
        AddPredefinedState(new State(SpriteNum.SPR_POL6, 1, 8, StateAction.None, StateNum.S_LIVESTICK, 0, 0)); // S_LIVESTICK2
        AddPredefinedState(new State(SpriteNum.SPR_GOR2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_MEAT2
        AddPredefinedState(new State(SpriteNum.SPR_GOR3, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_MEAT3
        AddPredefinedState(new State(SpriteNum.SPR_GOR4, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_MEAT4
        AddPredefinedState(new State(SpriteNum.SPR_GOR5, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_MEAT5
        AddPredefinedState(new State(SpriteNum.SPR_SMIT, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_STALAGTITE
        AddPredefinedState(new State(SpriteNum.SPR_COL1, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_TALLGRNCOL
        AddPredefinedState(new State(SpriteNum.SPR_COL2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SHRTGRNCOL
        AddPredefinedState(new State(SpriteNum.SPR_COL3, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_TALLREDCOL
        AddPredefinedState(new State(SpriteNum.SPR_COL4, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SHRTREDCOL
        AddPredefinedState(new State(SpriteNum.SPR_CAND, 32768, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_CANDLESTIK
        AddPredefinedState(new State(SpriteNum.SPR_CBRA, 32768, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_CANDELABRA
        AddPredefinedState(new State(SpriteNum.SPR_COL6, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_SKULLCOL
        AddPredefinedState(new State(SpriteNum.SPR_TRE1, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_TORCHTREE
        AddPredefinedState(new State(SpriteNum.SPR_TRE2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_BIGTREE
        AddPredefinedState(new State(SpriteNum.SPR_ELEC, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_TECHPILLAR
        AddPredefinedState(new State(SpriteNum.SPR_CEYE, 32768, 6, StateAction.None, StateNum.S_EVILEYE2, 0, 0));  // S_EVILEYE
        AddPredefinedState(new State(SpriteNum.SPR_CEYE, 32769, 6, StateAction.None, StateNum.S_EVILEYE3, 0, 0));  // S_EVILEYE2
        AddPredefinedState(new State(SpriteNum.SPR_CEYE, 32770, 6, StateAction.None, StateNum.S_EVILEYE4, 0, 0));  // S_EVILEYE3
        AddPredefinedState(new State(SpriteNum.SPR_CEYE, 32769, 6, StateAction.None, StateNum.S_EVILEYE, 0, 0));   // S_EVILEYE4
        AddPredefinedState(new State(SpriteNum.SPR_FSKU, 32768, 6, StateAction.None, StateNum.S_FLOATSKULL2, 0, 0));   // S_FLOATSKULL
        AddPredefinedState(new State(SpriteNum.SPR_FSKU, 32769, 6, StateAction.None, StateNum.S_FLOATSKULL3, 0, 0));   // S_FLOATSKULL2
        AddPredefinedState(new State(SpriteNum.SPR_FSKU, 32770, 6, StateAction.None, StateNum.S_FLOATSKULL, 0, 0));    // S_FLOATSKULL3
        AddPredefinedState(new State(SpriteNum.SPR_COL5, 0, 14, StateAction.None, StateNum.S_HEARTCOL2, 0, 0));    // S_HEARTCOL
        AddPredefinedState(new State(SpriteNum.SPR_COL5, 1, 14, StateAction.None, StateNum.S_HEARTCOL, 0, 0)); // S_HEARTCOL2
        AddPredefinedState(new State(SpriteNum.SPR_TBLU, 32768, 4, StateAction.None, StateNum.S_BLUETORCH2, 0, 0));    // S_BLUETORCH
        AddPredefinedState(new State(SpriteNum.SPR_TBLU, 32769, 4, StateAction.None, StateNum.S_BLUETORCH3, 0, 0));    // S_BLUETORCH2
        AddPredefinedState(new State(SpriteNum.SPR_TBLU, 32770, 4, StateAction.None, StateNum.S_BLUETORCH4, 0, 0));    // S_BLUETORCH3
        AddPredefinedState(new State(SpriteNum.SPR_TBLU, 32771, 4, StateAction.None, StateNum.S_BLUETORCH, 0, 0)); // S_BLUETORCH4
        AddPredefinedState(new State(SpriteNum.SPR_TGRN, 32768, 4, StateAction.None, StateNum.S_GREENTORCH2, 0, 0));   // S_GREENTORCH
        AddPredefinedState(new State(SpriteNum.SPR_TGRN, 32769, 4, StateAction.None, StateNum.S_GREENTORCH3, 0, 0));   // S_GREENTORCH2
        AddPredefinedState(new State(SpriteNum.SPR_TGRN, 32770, 4, StateAction.None, StateNum.S_GREENTORCH4, 0, 0));   // S_GREENTORCH3
        AddPredefinedState(new State(SpriteNum.SPR_TGRN, 32771, 4, StateAction.None, StateNum.S_GREENTORCH, 0, 0));    // S_GREENTORCH4
        AddPredefinedState(new State(SpriteNum.SPR_TRED, 32768, 4, StateAction.None, StateNum.S_REDTORCH2, 0, 0)); // S_REDTORCH
        AddPredefinedState(new State(SpriteNum.SPR_TRED, 32769, 4, StateAction.None, StateNum.S_REDTORCH3, 0, 0)); // S_REDTORCH2
        AddPredefinedState(new State(SpriteNum.SPR_TRED, 32770, 4, StateAction.None, StateNum.S_REDTORCH4, 0, 0)); // S_REDTORCH3
        AddPredefinedState(new State(SpriteNum.SPR_TRED, 32771, 4, StateAction.None, StateNum.S_REDTORCH, 0, 0));  // S_REDTORCH4
        AddPredefinedState(new State(SpriteNum.SPR_SMBT, 32768, 4, StateAction.None, StateNum.S_BTORCHSHRT2, 0, 0));   // S_BTORCHSHRT
        AddPredefinedState(new State(SpriteNum.SPR_SMBT, 32769, 4, StateAction.None, StateNum.S_BTORCHSHRT3, 0, 0));   // S_BTORCHSHRT2
        AddPredefinedState(new State(SpriteNum.SPR_SMBT, 32770, 4, StateAction.None, StateNum.S_BTORCHSHRT4, 0, 0));   // S_BTORCHSHRT3
        AddPredefinedState(new State(SpriteNum.SPR_SMBT, 32771, 4, StateAction.None, StateNum.S_BTORCHSHRT, 0, 0));    // S_BTORCHSHRT4
        AddPredefinedState(new State(SpriteNum.SPR_SMGT, 32768, 4, StateAction.None, StateNum.S_GTORCHSHRT2, 0, 0));   // S_GTORCHSHRT
        AddPredefinedState(new State(SpriteNum.SPR_SMGT, 32769, 4, StateAction.None, StateNum.S_GTORCHSHRT3, 0, 0));   // S_GTORCHSHRT2
        AddPredefinedState(new State(SpriteNum.SPR_SMGT, 32770, 4, StateAction.None, StateNum.S_GTORCHSHRT4, 0, 0));   // S_GTORCHSHRT3
        AddPredefinedState(new State(SpriteNum.SPR_SMGT, 32771, 4, StateAction.None, StateNum.S_GTORCHSHRT, 0, 0));    // S_GTORCHSHRT4
        AddPredefinedState(new State(SpriteNum.SPR_SMRT, 32768, 4, StateAction.None, StateNum.S_RTORCHSHRT2, 0, 0));   // S_RTORCHSHRT
        AddPredefinedState(new State(SpriteNum.SPR_SMRT, 32769, 4, StateAction.None, StateNum.S_RTORCHSHRT3, 0, 0));   // S_RTORCHSHRT2
        AddPredefinedState(new State(SpriteNum.SPR_SMRT, 32770, 4, StateAction.None, StateNum.S_RTORCHSHRT4, 0, 0));   // S_RTORCHSHRT3
        AddPredefinedState(new State(SpriteNum.SPR_SMRT, 32771, 4, StateAction.None, StateNum.S_RTORCHSHRT, 0, 0));    // S_RTORCHSHRT4
        AddPredefinedState(new State(SpriteNum.SPR_HDB1, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HANGNOGUTS
        AddPredefinedState(new State(SpriteNum.SPR_HDB2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HANGBNOBRAIN
        AddPredefinedState(new State(SpriteNum.SPR_HDB3, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HANGTLOOKDN
        AddPredefinedState(new State(SpriteNum.SPR_HDB4, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HANGTSKULL
        AddPredefinedState(new State(SpriteNum.SPR_HDB5, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HANGTLOOKUP
        AddPredefinedState(new State(SpriteNum.SPR_HDB6, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_HANGTNOBRAIN
        AddPredefinedState(new State(SpriteNum.SPR_POB1, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0)); // S_COLONGIBS
        AddPredefinedState(new State(SpriteNum.SPR_POB2, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0));  // S_SMALLPOOL
        AddPredefinedState(new State(SpriteNum.SPR_BRS1, 0, -1, StateAction.None, StateNum.S_NULL, 0, 0));     // S_BRAINSTEM
        AddPredefinedState(new State(SpriteNum.SPR_TLMP, 32768, 4, StateAction.None, StateNum.S_TECHLAMP2, 0, 0)); // S_TECHLAMP
        AddPredefinedState(new State(SpriteNum.SPR_TLMP, 32769, 4, StateAction.None, StateNum.S_TECHLAMP3, 0, 0)); // S_TECHLAMP2
        AddPredefinedState(new State(SpriteNum.SPR_TLMP, 32770, 4, StateAction.None, StateNum.S_TECHLAMP4, 0, 0)); // S_TECHLAMP3
        AddPredefinedState(new State(SpriteNum.SPR_TLMP, 32771, 4, StateAction.None, StateNum.S_TECHLAMP, 0, 0));  // S_TECHLAMP4
        AddPredefinedState(new State(SpriteNum.SPR_TLP2, 32768, 4, StateAction.None, StateNum.S_TECH2LAMP2, 0, 0));    // S_TECH2LAMP
        AddPredefinedState(new State(SpriteNum.SPR_TLP2, 32769, 4, StateAction.None, StateNum.S_TECH2LAMP3, 0, 0));    // S_TECH2LAMP2
        AddPredefinedState(new State(SpriteNum.SPR_TLP2, 32770, 4, StateAction.None, StateNum.S_TECH2LAMP4, 0, 0)); // S_TECH2LAMP3
        AddPredefinedState(new State(SpriteNum.SPR_TLP2, 32771, 4, StateAction.None, StateNum.S_TECH2LAMP, 0, 0));	// S_TECH2LAMP4
    }
}
