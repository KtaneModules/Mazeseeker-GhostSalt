using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class MazeseekerScript : MonoBehaviour {

    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable ModuleSelectable;
    public KMColorblindMode Colourblind;
    public KMSelectable[] Buttons;
    public KMSelectable LEDSelectable;
    public Material ScreenMat;
    public Material ScreenMatPhaseTwo;
    public MeshRenderer[] ButtonMeshes;
    public MeshRenderer LED;
    public MeshRenderer Screen;
    public TextMesh ColourblindText;
    public TextMesh CooldownDisplay;

    private KeyCode[] TypableKeys =
    {
        KeyCode.Return,
        KeyCode.W, KeyCode.D, KeyCode.S, KeyCode.A,
        KeyCode.UpArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.LeftArrow,
        KeyCode.E
    };

    private int[,] Grid = { { 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0 } };
    private int[,] Matrix = new int[36, 36];
    private int Row;
    private int Column;
    private int LogRow;
    private int LogColumn;
    private int StartingRow;
    private int StartingColumn;
    private int GoalRow;
    private int GoalColumn;
    private int SolvePresses;
    private float Cooldown;
    private float Delay = 0.5f;
    private float RandomSuspense;
    private float TimerFloat;
    private string LogDirections;
    private bool[][] Walls = { new bool[6], new bool[7], new bool[6], new bool[7], new bool[6], new bool[7], new bool[6], new bool[7], new bool[6], new bool[7], new bool[6], new bool[7], new bool[6] };
    private bool[,] VisitedSquares = new bool[6, 6];
    private bool[,] Radars = new bool[6, 6];
    private bool Moving;
    private bool ColourblindEnabled;
    private bool Inputting;
    private bool Radarable = true;
    private bool Solved;
    private bool Focused;

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        ColourblindEnabled = Colourblind.ColorblindModeActive;
        ColourblindText.text = "";
        ScreenMat.color = new Color(0, 0, 0);
        Screen.material = ScreenMat;
        Module.OnActivate += delegate { ScreenMat.color = new Color(1, 1, 1); };
        for (int i = 0; i < 5; i++)
        {
            int x = i;
            Buttons[i].OnInteract += delegate { if (!Moving) StartCoroutine(ButtonPress(x)); return false; };
        }
        LEDSelectable.OnInteract += delegate { LEDPress(); return false; };
        GenerateMaze();
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                if (Walls[i * 2][j])
                    Grid[i, j]++;
                if (Walls[(i * 2) + 1][j])
                    Grid[i, j]++;
                if (Walls[(i * 2) + 2][j])
                    Grid[i, j]++;
                if (Walls[(i * 2) + 1][j + 1])
                    Grid[i, j]++;
            }
        }
        List<int>[] LogNums = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                LogNums[i].Add(Grid[i, j]);
        Debug.LogFormat("[Mazeseeker #{0}] For reference, the numbers in the grid are:\n{1}", _moduleID, LogNums.Select(x => x.Join(" ")).Join("\n"));
        Row = Rnd.Range(0, 6);
        Column = Rnd.Range(0, 6);
        if (Row == StartingRow && Row == 0 && Column == StartingColumn && Column == 0)
        {
            LED.material.color = new Color(1, 0, 1);
            if (ColourblindEnabled)
                ColourblindText.text = "M";
        }
        else if (Row == GoalRow && Row == 0 && Column == GoalColumn && Column == 0)
        {
            LED.material.color = new Color(0, 1, 1);
            if (ColourblindEnabled)
                ColourblindText.text = "C";
        }
        else if (Row == StartingRow && Column == StartingColumn)
        {
            LED.material.color = new Color(1, 0, 0);
            if (ColourblindEnabled)
                ColourblindText.text = "R";
        }
        else if (Row == GoalRow && Column == GoalColumn)
        {
            LED.material.color = new Color(0, 1, 0);
            if (ColourblindEnabled)
                ColourblindText.text = "G";
        }
        else if (Row == 0 && Column == 0)
        {
            LED.material.color = new Color(0, 0, 1);
            if (ColourblindEnabled)
                ColourblindText.text = "B";
        }
        else
        {
            LED.material.color = new Color(0, 0, 0);
            ColourblindText.text = "";
        }
        switch (Grid[Row, Column])
        {
            case 0:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                break;
            case 1:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                break;
            case 2:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                break;
            default:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                break;
        }
        ModuleSelectable.OnFocus += delegate { Focused = true; };
        ModuleSelectable.OnDefocus += delegate { Focused = false; };
        if (Application.isEditor)
            Focused = true;
    }

    void Update()
    {
        for (int i = 0; i < TypableKeys.Count(); i++)
        {
            if (Input.GetKeyDown(TypableKeys[i]) && Focused)
            {
                if (i < 5)
                    Buttons[i].OnInteract();
                else if (i == 9)
                    LEDPress();
                else
                    Buttons[i - 4].OnInteract();
                goto Skip;
            }
        }
        Skip:
        bool DidISleepWithYourMumLastNight = true;
    }

    void GenerateMaze()
    {
        for (int i = 0; i < Walls.Length; i++)
            for (int j = 0; j < Walls[i].Length; j++)
                Walls[i][j] = true;
        Pathfinder(Rnd.Range(0, 6), Rnd.Range(0, 6));
        Debug.LogFormat("[Mazeseeker #{0}] The maze is as follows:\n{1}", _moduleID, Walls.Select(x => (x.Length == 6 ? "+" + x.Select(y => y ? "-" : " ").Join("+") + "+" : x.Select(y => y ? "|" : " ").Join(" "))).Join("\n"));
        StartingRow = Rnd.Range(0, 6);
        StartingColumn = Rnd.Range(0, 6);
        GoalRow = Rnd.Range(0, 6);
        GoalColumn = Rnd.Range(0, 6);
        while ((GoalRow == StartingRow && GoalColumn == StartingColumn) || Math.Abs(StartingRow - GoalRow) + Math.Abs(StartingColumn - GoalColumn) < 3)
        {
            GoalRow = Rnd.Range(0, 6);
            GoalColumn = Rnd.Range(0, 6);
        }
        Debug.LogFormat("[Mazeseeker #{0}] The start is at the coordinate {1} and the goal is at the coordinate {2}.", _moduleID, "ABCDEF"[StartingColumn] + (StartingRow + 1).ToString(), "ABCDEF"[GoalColumn] + (GoalRow + 1).ToString());
        for (int i = 0; i < 36; i++)
        {
            if (!Walls[(i / 6) * 2][i % 6])
                Matrix[i, i - 6] = 1;
            if (!Walls[((i / 6) * 2) + 1][(i % 6) + 1])
                Matrix[i, i + 1] = 2;
            if (!Walls[((i / 6) * 2) + 2][i % 6])
                Matrix[i, i + 6] = 3;
            if (!Walls[((i / 6) * 2) + 1][i % 6])
                Matrix[i, i - 1] = 4;
        }
        for (int i = 0; i < 36; i++)
        {
            int[,] Matrix2 = new int[36, 36];
            for (int j = 0; j < 36; j++)
                for (int k = 0; k < 36; k++)
                    for (int l = 0; l < 36; l++)
                        if (Matrix2[j, l] == 0 && Matrix[k, l] != 0)
                            Matrix2[j, l] = Matrix[j, k];
            for (int j = 0; j < 36; j++)
                for (int k = 0; k < 36; k++)
                    if (Matrix[j, k] == 0)
                        Matrix[j, k] = Matrix2[j, k];
        }
        LogRow = StartingRow;
        LogColumn = StartingColumn;
        while (LogRow != GoalRow || LogColumn != GoalColumn)
        {
            LogDirections += "URDL"[Matrix[(LogRow * 6) + LogColumn, (GoalRow * 6) + GoalColumn] - 1];
            switch (Matrix[(LogRow * 6) + LogColumn, (GoalRow * 6) + GoalColumn])
            {
                case 1:
                    LogRow = (LogRow + 5) % 6;
                    break;
                case 2:
                    LogColumn = (LogColumn + 1) % 6;
                    break;
                case 3:
                    LogRow = (LogRow + 1) % 6;
                    break;
                default:
                    LogColumn = (LogColumn + 5) % 6;
                    break;
            }
        }
        Debug.LogFormat("[Mazeseeker #{0}] To get from the start to the goal you can move: {1}.", _moduleID, LogDirections.Split().Join(", "));
    }

    void Pathfinder(int x, int y) //pog
    {
        VisitedSquares[x, y] = true;
        List<int> Directions = new List<int> { 0, 1, 2, 3 };
        Directions.Shuffle();
        for (int i = 0; i < 4; i++)
        {
            switch (Directions[i])
            {
                case 0:
                    if (y != 0 && VisitedSquares[x, y - 1] != true)
                    {
                        Walls[y * 2][x] = false;
                        Pathfinder(x, y - 1);
                    }
                    break;
                case 1:
                    if (x != 5 && VisitedSquares[x + 1, y] != true)
                    {
                        Walls[(y * 2) + 1][x + 1] = false;
                        Pathfinder(x + 1, y);
                    }
                    break;
                case 2:
                    if (y != 5 && VisitedSquares[x, y + 1] != true)
                    {
                        Walls[(y * 2) + 2][x] = false;
                        Pathfinder(x, y + 1);
                    }
                    break;
                default:
                    if (x != 0 && VisitedSquares[x - 1, y] != true)
                    {
                        Walls[(y * 2) + 1][x] = false;
                        Pathfinder(x - 1, y);
                    }
                    break;
            }
        }
    }

    void LEDPress()
    {
        LEDSelectable.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, LEDSelectable.transform);
        if (Radarable && !Solved && !Radars[Row, Column] && !Inputting)
        {
            Radarable = false;
            Radars[Row, Column] = true;
            for (int i = 0; i < 4; i++)
                ButtonMeshes[i].material.color = new Color32(64, 64, 64, 255);
            if (Walls[Row * 2][Column])
                ButtonMeshes[0].material.color = new Color(0.75f, 0.75f, 0.75f);
            if (Walls[(Row * 2) + 1][Column + 1])
                ButtonMeshes[1].material.color = new Color(0.75f, 0.75f, 0.75f);
            if (Walls[(Row * 2) + 2][Column])
                ButtonMeshes[2].material.color = new Color(0.75f, 0.75f, 0.75f);
            if (Walls[(Row * 2) + 1][Column])
                ButtonMeshes[3].material.color = new Color(0.75f, 0.75f, 0.75f);
            StartCoroutine(Timer());
            Audio.PlaySoundAtTransform("radar", LEDSelectable.transform);
        }
        else if (!Solved)
            Audio.PlaySoundAtTransform("buzzer", LEDSelectable.transform);
    }

    private IEnumerator ButtonPress (int pos)
    {
        Buttons[pos].AddInteractionPunch(0.5f);
        if (pos == 0)
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, Buttons[0].transform);
        if (pos != 0)
        {
            Audio.PlaySoundAtTransform("press", Buttons[pos].transform);
            Moving = true;
            for (int i = 0; i < 3; i++)
            {
                Buttons[pos].transform.localPosition -= new Vector3(0, 0.002f, 0);
                yield return null;
            }
            if (!Solved)
                StartCoroutine(Move(pos));
            for (int i = 0; i < 3; i++)
            {
                Buttons[pos].transform.localPosition += new Vector3(0, 0.002f, 0);
                yield return null;
            }
            if (Solved)
                Moving = false;
        }
        else if (!Inputting)
        {
            Screen.material = ScreenMatPhaseTwo;
            for (int i = 0; i < 4; i++)
                ButtonMeshes[i].material.color = new Color32(64, 64, 64, 255);
            Screen.material.color = new Color(0, 0, 0);
            Row = StartingRow;
            Column = StartingColumn;
            Inputting = true;
            LED.material.color = new Color(0, 0, 0);
            CooldownDisplay.text = "";
            ColourblindText.text = "";
        }
        else if (!Solved)
        {
            if (Row == GoalRow && Column == GoalColumn)
            {
                Cooldown = -1f;
                Moving = true;
                Audio.PlaySoundAtTransform("suspense", Buttons[0].transform);
                Screen.material.color = new Color(1f, 1f, 1f);
                for (int i = 0; i < 100; i++)
                {
                    Screen.material.color -= new Color(0.01f, 0.01f, 0.01f);
                    yield return null;
                }
                Module.HandlePass();
                Debug.LogFormat("[Mazeseeker #{0}] You got to the goal and pressed the display without hitting any walls. Module solved!", _moduleID);
                Screen.material.color = new Color(0, 1, 0);
                LED.material.color = new Color(0, 1, 0);
                Audio.PlaySoundAtTransform("solve", Buttons[0].transform);
                Solved = true;
                CooldownDisplay.text = "GG";
                CooldownDisplay.characterSize = 0.0004f;
                CooldownDisplay.color = new Color(0, 1, 0);
                Moving = false;
                yield return "solve";
            }
            else
            {
                Moving = true;
                Audio.PlaySoundAtTransform("suspense", Buttons[0].transform);
                Screen.material.color = new Color(1f, 1f, 1f);
                for (int i = 0; i < 100; i++)
                {
                    Screen.material.color -= new Color(0.01f, 0.01f, 0.01f);
                    yield return null;
                }
                Inputting = false;
                Module.HandleStrike();
                CooldownDisplay.text = Mathf.FloorToInt(TimerFloat).ToString();
                Debug.LogFormat("[Mazeseeker #{0}] You pressed the display on a tile that was not the goal (tile {1}). Strike!", _moduleID, "ABCDEF"[Column] + (Row + 1).ToString());
                Screen.material = ScreenMat;
                Moving = false;
                for (int i = 0; i < 4; i++)
                    ButtonMeshes[i].material.color = new Color32(64, 64, 64, 255);
                if (Radars[Row, Column] && !Inputting)
                {
                    if (Walls[Row * 2][Column])
                        ButtonMeshes[0].material.color = new Color(0.75f, 0.75f, 0.75f);
                    if (Walls[(Row * 2) + 1][Column + 1])
                        ButtonMeshes[1].material.color = new Color(0.75f, 0.75f, 0.75f);
                    if (Walls[(Row * 2) + 2][Column])
                        ButtonMeshes[2].material.color = new Color(0.75f, 0.75f, 0.75f);
                    if (Walls[(Row * 2) + 1][Column])
                        ButtonMeshes[3].material.color = new Color(0.75f, 0.75f, 0.75f);
                }
                if (Row == StartingRow && Row == 0 && Column == StartingColumn && Column == 0)
                {
                    LED.material.color = new Color(1, 0, 1);
                    Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                    if (ColourblindEnabled)
                        ColourblindText.text = "M";
                }
                else if (Row == GoalRow && Row == 0 && Column == GoalColumn && Column == 0)
                {
                    LED.material.color = new Color(0, 1, 1);
                    Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                    if (ColourblindEnabled)
                        ColourblindText.text = "C";
                }
                else if (Row == StartingRow && Column == StartingColumn)
                {
                    LED.material.color = new Color(1, 0, 0);
                    Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                    if (ColourblindEnabled)
                        ColourblindText.text = "R";
                }
                else if (Row == GoalRow && Column == GoalColumn)
                {
                    LED.material.color = new Color(0, 1, 0);
                    Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                    if (ColourblindEnabled)
                        ColourblindText.text = "G";
                }
                else if (Row == 0 && Column == 0)
                {
                    LED.material.color = new Color(0, 0, 1);
                    Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                    if (ColourblindEnabled)
                        ColourblindText.text = "B";
                }
                else
                {
                    LED.material.color = new Color(0, 0, 0);
                    ColourblindText.text = "";
                }
                switch (Grid[Row, Column])
                {
                    case 0:
                        Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                        break;
                    case 1:
                        Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                        break;
                    case 2:
                        Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                        break;
                    default:
                        Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                        break;
                }
                yield return "strike";
            }
        }
        else
        {
            SolvePresses++;
            if (SolvePresses >= 50)
            {
                SolvePresses = 0;
                Audio.PlaySoundAtTransform("secret", Buttons[0].transform);
            }
        }
    }

    private IEnumerator Move(int pos)
    {
        bool Strike = false;
        Screen.material.color = new Color(0, 0, 0);
        if (Inputting)
        {
            RandomSuspense = Rnd.Range(0.5f, 1.5f);
            yield return new WaitForSeconds(RandomSuspense);
            if ((!Walls[Row * 2][Column] || pos != 1) && (!Walls[(Row * 2) + 1][Column + 1] || pos != 2) && (!Walls[(Row * 2) + 2][Column] || pos != 3) && (!Walls[(Row * 2) + 1][Column] || pos != 4))
            {
                Screen.material.color = new Color(0.85f, 0.85f, 0);
                Audio.PlaySoundAtTransform("correct", Buttons[0].transform);
            }
            else
            {
                Module.HandleStrike();
                CooldownDisplay.text = Mathf.FloorToInt(TimerFloat).ToString();
                Debug.LogFormat("[Mazeseeker #{0}] You walked into a wall ({1} from tile {2}). Strike!", _moduleID, new string[]{ "up", "right", "down", "left" }[pos - 1], "ABCDEF"[Column] + (Row + 1).ToString());
                Strike = true;
                Inputting = false;
                Screen.material = ScreenMat;
            }
        }
        switch (Grid[Row, Column])
        {
            case 0:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                break;
            case 1:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                break;
            case 2:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                break;
            default:
                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                break;
        }
        if (!Strike)
        {
            switch (pos)
            {
                case 1:
                    switch (Grid[Row, Column])
                    {
                        case 0:
                            switch (Grid[(Row + 5) % 6, Column])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 2 / 3f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 2 / 3f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 0f));
                                    break;
                            }
                            break;
                        case 1:
                            switch (Grid[(Row + 5) % 6, Column])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 1 / 2f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 2 / 3f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 2 / 3f));
                                    break;
                            }
                            break;
                        case 2:
                            switch (Grid[(Row + 5) % 6, Column])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 1 / 2f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 1 / 2f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 1 / 6f));
                                    break;
                            }
                            break;
                        default:
                            switch (Grid[(Row + 5) % 6, Column])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 1 / 6f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 1 / 2f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 0f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                                    break;
                            }
                            break;
                    }
                    Row = (Row + 5) % 6;
                    for (int i = 0; i < 10; i++)
                    {
                        Screen.material.SetTextureOffset("_MainTex", new Vector2(Screen.material.GetTextureOffset("_MainTex").x, Screen.material.GetTextureOffset("_MainTex").y + 1f / 60f));
                        yield return null;
                    }
                    break;
                case 2:
                    switch (Grid[Row, Column])
                    {
                        case 0:
                            switch (Grid[Row, (Column + 1) % 6])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 2f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 5 / 6f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 1 / 3f));
                                    break;
                            }
                            break;
                        case 1:
                            switch (Grid[Row, (Column + 1) % 6])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 5 / 6f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 2f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 5 / 6f));
                                    break;
                            }
                            break;
                        case 2:
                            switch (Grid[Row, (Column + 1) % 6])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 1 / 2f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 5 / 6f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 0f));
                                    break;
                            }
                            break;
                        default:
                            switch (Grid[Row, (Column + 1) % 6])
                            {
                                case 0:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 0f));
                                    break;
                                case 1:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 1 / 2f));
                                    break;
                                case 2:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 1 / 3f));
                                    break;
                                default:
                                    Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                                    break;
                            }
                            break;
                }
                Column = (Column + 1) % 6;
                for (int i = 0; i < 10; i++)
                {
                    Screen.material.SetTextureOffset("_MainTex", new Vector2(Screen.material.GetTextureOffset("_MainTex").x + 1f / 60f, Screen.material.GetTextureOffset("_MainTex").y));
                    yield return null;
                }
                break;
            case 3:
                switch (Grid[Row, Column])
                {
                    case 0:
                        switch (Grid[(Row + 1) % 6, Column])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 2 / 3f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 2 / 3f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 1 / 3f));
                                break;
                        }
                        break;
                    case 1:
                        switch (Grid[(Row + 1) % 6, Column])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 5 / 6f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 2 / 3f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 2 / 3f));
                                break;
                        }
                        break;
                    case 2:
                        switch (Grid[(Row + 1) % 6, Column])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 5 / 6f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 2f, 5 / 6f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 1 / 6f));
                                break;
                        }
                        break;
                    default:
                        switch (Grid[(Row + 1) % 6, Column])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 1 / 6f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 5 / 6f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(0f, 1 / 3f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                                break;
                        }
                        break;
                }
                Row = (Row + 1) % 6;
                for (int i = 0; i < 10; i++)
                {
                    Screen.material.SetTextureOffset("_MainTex", new Vector2(Screen.material.GetTextureOffset("_MainTex").x, Screen.material.GetTextureOffset("_MainTex").y - 1f / 60f));
                    yield return null;
                }
                break;
            default:
                switch (Grid[Row, Column])
                {
                    case 0:
                        switch (Grid[Row, (Column + 5) % 6])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 2 / 3f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 5 / 6f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 2f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 0f));
                                break;
                        }
                        break;
                    case 1:
                        switch (Grid[Row, (Column + 5) % 6])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 1 / 2f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 2 / 3f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 5 / 6f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 2f));
                                break;
                        }
                        break;
                    case 2:
                        switch (Grid[Row, (Column + 5) % 6])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 5 / 6f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 1 / 2f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 6f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 6f, 1 / 3f));
                                break;
                        }
                        break;
                    default:
                        switch (Grid[Row, (Column + 5) % 6])
                        {
                            case 0:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 3f));
                                break;
                            case 1:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(5 / 6f, 5 / 6f));
                                break;
                            case 2:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(1 / 3f, 0f));
                                break;
                            default:
                                Screen.material.SetTextureOffset("_MainTex", new Vector2(2 / 3f, 1 / 6f));
                                break;
                        }
                        break;
                }
                Column = (Column + 5) % 6;
                for (int i = 0; i < 10; i++)
                {
                    Screen.material.SetTextureOffset("_MainTex", new Vector2(Screen.material.GetTextureOffset("_MainTex").x - 1f / 60f, Screen.material.GetTextureOffset("_MainTex").y));
                    yield return null;
                }
                break;
            }
            if (Inputting)
                Screen.material.color = new Color(0, 0, 0);
        }
        for (int i = 0; i < 4; i++)
            ButtonMeshes[i].material.color = new Color32(64, 64, 64, 255);
        if (Radars[Row, Column] && !Inputting)
        {
            if (Walls[Row * 2][Column])
                ButtonMeshes[0].material.color = new Color(0.75f, 0.75f, 0.75f);
            if (Walls[(Row * 2) + 1][Column + 1])
                ButtonMeshes[1].material.color = new Color(0.75f, 0.75f, 0.75f);
            if (Walls[(Row * 2) + 2][Column])
                ButtonMeshes[2].material.color = new Color(0.75f, 0.75f, 0.75f);
            if (Walls[(Row * 2) + 1][Column])
                ButtonMeshes[3].material.color = new Color(0.75f, 0.75f, 0.75f);
        }
        if (!Inputting)
        {
            if (Row == StartingRow && Row == 0 && Column == StartingColumn && Column == 0)
            {
                LED.material.color = new Color(1, 0, 1);
                Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                if (ColourblindEnabled)
                    ColourblindText.text = "M";
            }
            else if (Row == GoalRow && Row == 0 && Column == GoalColumn && Column == 0)
            {
                LED.material.color = new Color(0, 1, 1);
                Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                if (ColourblindEnabled)
                    ColourblindText.text = "C";
            }
            else if (Row == StartingRow && Column == StartingColumn)
            {
                LED.material.color = new Color(1, 0, 0);
                Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                if (ColourblindEnabled)
                    ColourblindText.text = "R";
            }
            else if (Row == GoalRow && Column == GoalColumn)
            {
                LED.material.color = new Color(0, 1, 0);
                Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                if (ColourblindEnabled)
                    ColourblindText.text = "G";
            }
            else if (Row == 0 && Column == 0)
            {
                LED.material.color = new Color(0, 0, 1);
                Audio.PlaySoundAtTransform("alert", Buttons[0].transform);
                if (ColourblindEnabled)
                    ColourblindText.text = "B";
            }
            else
            {
                LED.material.color = new Color(0, 0, 0);
                ColourblindText.text = "";
            }
        }
        Moving = false;
        if (Strike)
            yield return "strike";
    }

    private IEnumerator Timer()
    {
        if (Cooldown < 1.5f)
            Cooldown += 0.5f;
        TimerFloat = Cooldown * 60;
        while (Mathf.FloorToInt(TimerFloat) > 0 && !Solved)
        {
            yield return null;
            TimerFloat -= Time.deltaTime;
            if (!Inputting && !Solved)
            {
                CooldownDisplay.text = Mathf.FloorToInt(TimerFloat).ToString();
                if (CooldownDisplay.text.Length == 4)
                    CooldownDisplay.characterSize = 0.0002f;
                else if (CooldownDisplay.text.Length == 3)
                    CooldownDisplay.characterSize = 0.0003f;
                else
                    CooldownDisplay.characterSize = 0.0004f;
            }
        }
        if (!Solved)
            Radarable = true;
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} urdl' to move up, then right, then down, then left. Use '!{0} radar' to radar, '!{0} submit' to press the display and '!{0} delay 0.5' to change the delay between presses to 0.5 seconds. This can be between 0.5 and 5 seconds and is not available during submission.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        string[] CommandArray = command.Split(' ');
        string validcmds = "urdl";
        char[] validcmdssplit = { 'u', 'r', 'd', 'l' };
        if (command != "radar" && command != "submit" && CommandArray[0] != "delay")
        {
            for (int i = 0; i < command.Length; i++)
            {
                if (!validcmds.Contains(command[i]))
                {
                    yield return "sendtochaterror Invalid command.";
                    yield break;
                }
            }
            yield return null;
            for (int i = 0; i < command.Length; i++)
            {
                if (!Inputting)
                {
                    Buttons[Array.IndexOf(validcmdssplit, command[i]) + 1].OnInteract();
                    yield return new WaitForSeconds(Delay);
                }
                else
                {
                    while (Moving)
                        yield return null;
                    Buttons[Array.IndexOf(validcmdssplit, command[i]) + 1].OnInteract();
                    if (i != command.Length - 1)
                        yield return new WaitForSeconds(RandomSuspense + 0.1f);
                }
            }
        }
        else if (command == "radar")
        {
            yield return null;
            LEDSelectable.OnInteract();
        }
        else if (command == "submit")
        {
            yield return null;
            Buttons[0].OnInteract();
        }
        else
        {
            float bruh = 0;
            if (CommandArray.Length == 2)
            {
                if (float.TryParse(CommandArray[1], out bruh))
                {
                    if (float.Parse(CommandArray[1]) > 0.5f && float.Parse(CommandArray[1]) <= 5f)
                    {
                        yield return null;
                        Delay = float.Parse(CommandArray[1]);
                    }
                    else
                    {
                        yield return "sendtochaterror Invalid delay: Please enter a delay between 0.5 and 5 seconds.";
                        yield break;
                    }
                }
                else
                {
                    yield return "sendtochaterror Invalid use of delay command.";
                    yield break;
                }
            }
            else
            {
                yield return "sendtochaterror Invalid use of delay command.";
                yield break;
            }
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        yield return true;
        if (!Inputting)
        {
            Buttons[0].OnInteract();
            yield return true;
        }
        while (Row != GoalRow || Column != GoalColumn)
        {
            Buttons[Matrix[(Row * 6) + Column, (GoalRow * 6) + GoalColumn]].OnInteract();
            yield return true;
        }
        yield return new WaitForSeconds(0.2f);
        Buttons[0].OnInteract();
    }
    //I haven't said anything for a while.
}
