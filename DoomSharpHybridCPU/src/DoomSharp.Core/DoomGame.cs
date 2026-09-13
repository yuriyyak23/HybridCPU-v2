using DoomSharp.Core.Data;
using DoomSharp.Core.Graphics;
using DoomSharp.Core.Input;

using DoomSharp.Core.UI;
using DoomSharp.Core.GameLogic;


namespace DoomSharp.Core;

public enum Command
{
    Send,
    Get
}

public sealed class DoomGame
{
    public const byte DoomVersion = 109;
    public const string NewLine = "\r\n";
    private static IConsole _console = new NullConsole();
    private static IGraphics _graphics = new NullGraphics();
    private static IDoomClock _clock = new NullDoomClock();
    public static DoomGame Instance { get; } = new();

    private readonly RenderEngine _renderer = new();
    private readonly Video _video;
    private readonly Zone _zone = new();
    private readonly GameController _game = new();
    private readonly IntermissionController _intermission = new();
    private readonly StatusBar _statusBar = new();
    private MenuController? _menu;
    private HudController? _hud;
    private WadFileCollection? _wadFiles;
    private byte[]? _wadImage;
    private bool _singleTics = false; // debug flag to cancel adaptiveness

    private int[] _frametics = new int[4];
    private int _frameOn;
    private bool[] _frameskip = new bool[4];

    private int _skiptics = 0;
    private int _gametime;

    private GameController.TicCommand[] _localCommands = new GameController.TicCommand[Constants.BackupTics];


    private int _oldEnterTics = 0;

    private readonly InputEventRingBuffer _events = new(Constants.MaxEvents);

    private int _demoSequence;
    private bool _advancedemo;
    private int _demoPageTic = 0;
    private string _demoPageName = "";

    // Display method "statics"
    private bool _displayViewActiveState = false;
    private bool _displayMenuActiveState = false;
    private bool _displayInHelpScreensState = false;
    private bool _displayFullscreen = false;
    private GameState _oldDisplayGameState = GameState.Wipe;
    private int _displayBorderDrawCount;

    private DoomGame()
    {
        _video = new Video(_graphics);

    }

    /// <summary>
    /// To be used for platforms where the filesystem is not widely available, like mobile platforms
    /// </summary>
    /// <param name="gameMode"></param>
    /// <param name="wadImage">The complete WAD image already resident in RAM.</param>
    public void Run(GameMode gameMode, byte[] wadImage)
    {
        GameMode = gameMode;
        _wadImage = wadImage;

        ModifiedGame = false;

            var titleFormat = GameMode switch
            {
                GameMode.Retail => "The Ultimate DOOM Startup v{0}.{1}",
                GameMode.Shareware => "DOOM Shareware Startup v{0}.{1}",
                GameMode.Registered => "DOOM Registered Startup v{0}.{1}",
                GameMode.Commercial => "DOOM 2: Hell on Earth v{0}.{1}",
                _ => "Public DOOM - v{0}.{1}"
            };

            // DoomVersion is the compile-time vanilla 1.9 protocol/version byte (109).
            // Keep startup text exact without manufacturing address-taken Int32 temporaries.
            _console.SetTitle(string.Concat(titleFormat, "1.9", ""));

            // init subsystems
            _console.WriteLine("V_Init: allocate screens.");
            _video.Initialize();

            _console.WriteLine("M_LoadDefaults: Load system defaults.");
            // M_LoadDefaults();              // load before initing other systems

            _console.WriteLine("Z_Init: Init zone memory allocation daemon.");
            _zone.Initialize();

            _console.WriteLine("W_Init: Init WADfiles.");
            _wadFiles = WadFileCollection.Initialize(_wadImage ?? Array.Empty<byte>());

            switch (GameMode)
            {
                case GameMode.Shareware:
                case GameMode.Indetermined:
                    _console.Write(
                        "===========================================================================" +
                        NewLine +
                        "                                Shareware!" + NewLine +
                        "===========================================================================" +
                        NewLine
                    );
                    break;
                case GameMode.Registered:
                case GameMode.Retail:
                case GameMode.Commercial:
                    _console.Write(
                        "===========================================================================" +
                        NewLine +
                        "                 Commercial product - do not distribute!" + NewLine +
                        "         Please report software piracy to the SPA: 1-800-388-PIR8" + NewLine +
                        "===========================================================================" +
                        NewLine
                    );
                    break;

                default:
                    // Ouch.
                    break;
            }

            _console.WriteLine("M_Init: Init miscellaneous info.");
            _menu = new MenuController();

            _console.Write("R_Init: Init DOOM refresh daemon - ");
            _renderer.Initialize();

            _console.WriteLine(NewLine + "P_Init: Init Playloop state.");
            _game.P_Init();

            _console.WriteLine("I_Init: Setting up machine state.");
            // I_Init ();

            _game.ConsolePlayer = _game.DisplayPlayer = 0;

            _console.WriteLine("HU_Init: Setting up heads up display.");
            _hud = new HudController();

            _console.WriteLine("ST_Init: Init status bar.");
            _statusBar.Init();

            if (_game.GameAction != GameAction.LoadGame)
            {
                if (AutoStart)
                {
                    _game.InitNew(StartSkill, StartEpisode, StartMap);
                }
                else
                {
                    StartTitle(); // start up intro loop
                }
            }

        DoomLoop();  // never returns
    }

    public bool ModifiedGame { get; private set; } = false;
    public GameMode GameMode { get; private set; } = GameMode.Indetermined;
    public GameState WipeGameState { get; private set; } = GameState.Wipe;
    public GameLanguage Language { get; private set; } = GameLanguage.English;

    public SkillLevel StartSkill { get; set; } = SkillLevel.Medium;
    public int StartEpisode { get; set; } = 1;
    public int StartMap { get; set; } = 1;
    public bool AutoStart { get; set; } = false;


    public int TicDup { get; private set; } = 1; // tic duplication // 1 = no duplication, 2-5 = dup for slow nets

    /// <summary>
    /// maketic is the tick that hasn't had control made for it yet
    /// </summary>
    public int MakeTic { get; private set; } = 0;

    public bool StatusBarActive { get; set; } = false;

    public bool AutoMapActive { get; set; } = false;   // In AutoMap mode?
    public bool MenuActive { get; set; } = false;  // Menu overlayed?
    public bool InHelpScreensActive { get; set; } = false;

    public RenderEngine Renderer => _renderer;
    public GameController Game => _game;
    public GameController.TicCommand LocalCommand(int buffer) => _localCommands[buffer] ?? new GameController.TicCommand();
    public Video Video => _video;
    public HudController Hud => _hud!;
    public WadFileCollection WadData => _wadFiles!;
    public MenuController Menu => _menu!;
    public StatusBar StatusBar => _statusBar;
    public IntermissionController Intermission => _intermission;

    public static void SetConsole(IConsole console)
    {
        _console = console;
    }

    public static void SetOutputRenderer(IGraphics renderer)
    {
        _graphics = renderer;
        Instance.Video.SetOutputRenderer(renderer);
    }

    public static void SetClock(IDoomClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public static IConsole Console => _console;

    private void Display()
    {
        bool wipe;

        if (Game.NoDrawers)
        {
            return;                    // for comparative timing / profiling
        }

        var redrawsbar = false;

        // change the view size if needed
        if (Renderer.SetSizeNeeded)
        {
            Renderer.ExecuteSetViewSize();
            _oldDisplayGameState = GameState.Wipe; // force background redraw
            _displayBorderDrawCount = 3;
        }

        // save the current screen if about to wipe
        if (_game.GameState != _game.WipeGameState)
        {
            wipe = true;
            _video.WipeStartScreen(0, 0, Constants.ScreenWidth, Constants.ScreenHeight);
        }
        else
        {
            wipe = false;
        }

        if (_game.GameState == GameState.Level && _game.GameTic != 0)
        {
            DoomGame.Instance.Hud.Erase();
        }

        // do buffered drawing
        switch (_game.GameState)
        {
            case GameState.Level:
                if (_game.GameTic == 0)
                {
                    break;
                }

                if (AutoMapActive)
                {
                    // AM_Drawer();
                }

                if (wipe || (Renderer.ViewHeight != 200 && _displayFullscreen))
                {
                    redrawsbar = true;
                }

                if (_displayInHelpScreensState && !Menu.InHelpScreens)
                {
                    redrawsbar = true;              // just put away the help screen
                }

                _statusBar.Drawer(Renderer.ViewHeight == 200, redrawsbar);
                _displayFullscreen = Renderer.ViewHeight == 200;
                break;

            case GameState.Intermission:
                Intermission.Drawer();
                break;
            case GameState.Finale:
                // F_Drawer();
                break;
            case GameState.DemoScreen:
                PageDrawer();
                break;
        }

        // draw buffered stuff to screen
        // I_UpdateNoBlit(); // Doesn't do anything

        // draw the view directly
        if (_game.GameState == GameState.Level && !AutoMapActive && _game.GameTic != 0)
        {
            Renderer.RenderPlayerView(Game.Players[Game.DisplayPlayer]);
        }

        if (_game.GameState == GameState.Level && _game.GameTic != 0)
        {
            Hud.Drawer();
        }

        // clean up border stuff
        if (_game.GameState != _oldDisplayGameState && _game.GameState != GameState.Level)
        {
            _video.SetPalette("PLAYPAL");
        }

        // see if the border needs to be initially drawn
        if (_game.GameState == GameState.Level && _oldDisplayGameState != GameState.Level)
        {
            _displayViewActiveState = false; // view was not active
            Renderer.FillBackScreen(); // draw the pattern into the back screen
        }

        // see if the border needs to be updated to the screen
        if (_game.GameState == GameState.Level && !AutoMapActive && Renderer.ScaledViewWidth != 320)
        {
            if (_menu!.IsActive || MenuActive || !_displayViewActiveState)
            {
                _displayBorderDrawCount = 3;
            }
            if (_displayBorderDrawCount != 0)
            {
                Renderer.DrawViewBorder(); // erase old menu stuff
                _displayBorderDrawCount--;
            }
        }

        _displayMenuActiveState = _menu!.IsActive;
        _displayViewActiveState = _game.ViewActive;
        _displayInHelpScreensState = _menu!.InHelpScreens;
        _oldDisplayGameState = _game.WipeGameState = _game.GameState;

        // draw pause pic
        if (_game.Paused)
        {
            int y;
            if (AutoMapActive)
            {
                y = 4;
            }
            else
            {
                y = Renderer.ViewWindowY + 4;
            }

            _video.DrawPatchDirect(Renderer.ViewWindowX + (Renderer.ScaledViewWidth - 68) / 2, y, 0, WadData.GetLumpName("M_PAUSE", PurgeTag.Cache));
        }

        // menus go directly to the screen
        _menu!.Drawer(); // menu is drawn even on top of everything

        // normal update
        if (!wipe)
        {
            _video.SignalOutputReady(); // page flip or blit buffer
            return;
        }

        // wipe update
        _video.WipeEndScreen(0, 0, Constants.ScreenWidth, Constants.ScreenHeight);

        var wipeStart = GetTime() - 1;
        bool done;

        do
        {
            int nowTime;
            int tics;
            do
            {
                nowTime = GetTime();
                tics = nowTime - wipeStart;
            } while (tics == 0);

            wipeStart = nowTime;
            done = _video.WipeScreenEffect(WipeMethod.Melt, 0, 0, Constants.ScreenWidth, Constants.ScreenHeight, tics);
            // I_UpdateNoBlit(); // Doesn't do anything
            _menu!.Drawer();                       // menu is drawn even on top of wipes
            _video.SignalOutputReady();            // page flip or blit buffer
        } while (!done);
    }

    private void DoomLoop()
    {
        // demo recording
        // debug
        _graphics.Initialize();

        while (true)
        {
            // frame syncronous IO operations
            //I_StartFrame(); // not needed

            // process one or more tics
            if (_singleTics)
            {
                _graphics.StartTic();
                ProcessEvents();
                _localCommands[MakeTic % Constants.BackupTics] = Game.BuildTicCommand();
                if (_advancedemo)
                {
                    DoAdvanceDemo();
                }

                _menu!.Ticker();
                _game.Ticker();

                _game.GameTic++;
                MakeTic++;

                _clock.WaitTic();
            }
            else
            {
                TryRunTics(); // will run at least one tic
            }

            // Update display, next frame, with current state.
            Display();
        }
    }

    private void TryRunTics()
    {
        var now = GetTime();
        while (now <= _gametime)
        {
            _clock.WaitTic();
            now = GetTime();
        }

        var counts = now - _gametime;
        _gametime = now;
        for (var count = 0; count < counts; count++)
        {
            if (_advancedemo) DoAdvanceDemo();
            _graphics.StartTic();
            ProcessEvents();
            _localCommands[MakeTic % Constants.BackupTics] = Game.BuildTicCommand();
            _menu!.Ticker();
            _game.Ticker();
            _game.GameTic++;
            MakeTic++;
        }
    }
    public void PostEvent(InputEvent ev)
    {
        _events.Enqueue(ev);
    }

    private void ProcessEvents()
    {
        // process events and dispatch them to the menu and the game logic (the latter only if the menu didn't eat the event)

        // IF STORE DEMO, DO NOT ACCEPT INPUT
        if (GameMode == GameMode.Commercial && WadData.GetNumForName("map01") < 0)
        {
            return;
        }

        while (true)
        {
            var currentEvent = _events.DequeueOrNull();
            if (currentEvent is null)
                break;

            if (_menu!.HandleEvent(currentEvent))
            {
                continue;
            }

            Game.HandleEvent(currentEvent);
        }
    }

    public int GetTime()
    {
        return _clock.GetTime();
    }

    public void StartTitle()
    {
        _game.GameAction = GameAction.Nothing;
        _demoSequence = -1;
        AdvanceDemo();
    }

    public void AdvanceDemo()
    {
        _advancedemo = true;
    }

    private void DoAdvanceDemo()
    {
        _game.Players[_game.ConsolePlayer].PlayerState = PlayerState.Alive; // not reborn
        _advancedemo = false;
        _game.UserGame = false;               // no save / end game here
        _game.Paused = false;
        _game.GameAction = GameAction.Nothing;

        if (GameMode == GameMode.Retail)
        {
            _demoSequence = (_demoSequence + 1) % 7;
        }
        else
        {
            _demoSequence = (_demoSequence + 1) % 6;
        }

        switch (_demoSequence)
        {
            case 0:
                if (GameMode == GameMode.Commercial)
                {
                    _demoPageTic = 35 * 11;
                }
                else
                {
                    _demoPageTic = 170;
                }
                _game.GameState = GameState.DemoScreen;
                _demoPageName = "TITLEPIC";
                break;
            case 1:
                _game.DeferedPlayDemo("demo1");
                break;
            case 2:
                _demoPageTic = 200;
                _game.GameState = GameState.DemoScreen;
                _demoPageName = "CREDIT";
                break;
            case 3:
                _game.DeferedPlayDemo("demo2");
                break;
            case 4:
                _game.GameState = GameState.DemoScreen;
                if (GameMode == GameMode.Commercial)
                {
                    _demoPageTic = 35 * 11;
                    _demoPageName = "TITLEPIC";
                }
                else
                {
                    _demoPageTic = 200;

                    if (GameMode == GameMode.Retail)
                    {
                        _demoPageName = "CREDIT";
                    }
                    else
                    {
                        _demoPageName = "HELP2";
                    }
                }
                break;
            case 5:
                _game.DeferedPlayDemo("demo3");
                break;
            // THE DEFINITIVE DOOM Special Edition demo
            case 6:
                _game.DeferedPlayDemo("demo4");
                break;
        }
    }

    private void PageDrawer()
    {
        _video.DrawPatch(0, 0, 0, WadData.GetLumpName(_demoPageName, PurgeTag.Cache));
    }

    public void PageTicker()
    {
        if (--_demoPageTic < 0)
        {
            AdvanceDemo();
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public void Quit()
    {
        //M_SaveDefaults();
        //I_ShutdownGraphics();

        Console.Shutdown();
        throw new DoomTerminationException("Doom requested process termination.", 0);
    }

    public void WaitVBL(int count)
    {
        _clock.WaitVBL(count);
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Error(string message)
    {
        _console.WriteLine("Error: " + message);
        _console.Shutdown();
        throw new DoomTerminationException(message, 1);
    }

}
