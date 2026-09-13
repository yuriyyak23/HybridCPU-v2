namespace DoomSharp.Core.UI;

public class Menu
{
    public Menu(int numItems, Menu? previousMenu, MenuItem[] items, MenuDrawAction drawAction, int x, int y, int lastOn)
    {
        NumItems = numItems;
        PreviousMenu = previousMenu;
        Items = items;
        DrawAction = drawAction;
        X = x;
        Y = y;
        LastOn = lastOn;
    }

    public int NumItems { get; set; }
    public Menu? PreviousMenu { get; set; }
    public MenuItem[] Items { get; set; }
    public MenuDrawAction DrawAction { get; set; }
    
    public int X { get; set; }
    public int Y { get; set; }
    public int LastOn { get; set; }
}


public class MenuItem
{
    public MenuItem(MenuItemStatus status, string name, MenuChoiceAction choiceAction = MenuChoiceAction.None, char hotKey = '\0')
    {
        Status = status;
        Name = name;
        ChoiceAction = choiceAction;
        HotKey = hotKey;
    }

    public MenuItemStatus Status { get; set; }
    public string Name { get; set; }
    public MenuChoiceAction ChoiceAction { get; set; }
    public char HotKey { get; set; }
}


public enum MenuDrawAction
{
    Main,
    Episodes,
    NewGame,
    Options,
    ReadThis,
    ReadThis2,
    Load,
    Save
}

public enum MenuChoiceAction
{
    None,
    NewGame,
    Options,
    LoadGame,
    SaveGame,
    ReadThis,
    Quit,
    SelectEpisode,
    ChooseSkill,
    EndGame,
    ChangeMessages,
    ChangeDetail,
    SizeDisplay,
    ChangeSensitivity,
    ReadThis2,
    FinishReadThis,
    LoadSelect,
    SaveSelect
}

public enum MenuMessageAction
{
    None,
    VerifyNightmare,
    EndGameResponse,
    QuickSaveResponse,
    QuickLoadResponse,
    QuitResponse
}

public enum MenuItemStatus
{
    Empty = -1,
    NoCursorHere = 0,
    Ok = 1,
    ArrowsOk = 2
}
