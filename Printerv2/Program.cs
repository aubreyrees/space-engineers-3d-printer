using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    public enum Direction { Extend, Retraction }

    public enum Limit { Max, Min }

    enum PistonDirection { XPos, XNeg, YPos, YNeg, ZPos, ZNeg }


    

    partial class Program : MyGridProgram
    {
        public PistonDirection PistonDirectionInvert(PistonDirection direction)
        {
            switch (direction)
            {
                case PistonDirection.XPos:
                    return PistonDirection.XNeg;
                case PistonDirection.XNeg:
                    return PistonDirection.XPos;
                case PistonDirection.YPos:
                    return PistonDirection.YNeg;
                case PistonDirection.YNeg:
                    return PistonDirection.YPos;
                case PistonDirection.ZPos:
                    return PistonDirection.ZNeg;
                case PistonDirection.ZNeg:
                    return PistonDirection.ZPos;
                default:
                    throw new Exception("Unexpected enum value");

            }
        }
  
        public string PistonDirectionToString(PistonDirection direction)
        {
            switch (direction)
            {
                case PistonDirection.XPos:
                    return "x+";
                case PistonDirection.XNeg:
                    return "x-";
                case PistonDirection.YPos:
                    return "y+";
                case PistonDirection.YNeg:
                    return "y-";
                case PistonDirection.ZPos:
                    return "z+";
                case PistonDirection.ZNeg:
                    return "z-";
                default:
                    throw new Exception("Unexpected enum value");

            }
        }

        class Config
        {
            public string printerTag;
            public float? speed;
            public float? step;
            public float? xExt;
            public float? yExt;
            public float? zExt;
            public string xForwardTag;
            public string xReverseTag;
            public string yForwardTag;
            public string yReverseTag;
            public string zForwardTag;
            public string zReverseTag;
        }

        class PrinterState
        {
            public Dictionary<PistonDirection, List<IMyPistonBase>> pistons;
            public List<PistonDirection> pistonDirectionList;
            public List<Base6Directions.Direction> baseDirectionList;
            public Dictionary<Base6Directions.Direction, PistonDirection?> directonMap;
            public Dictionary<PistonDirection, string> Tags;
            public bool init;

            public PrinterState()
            {
                init = false;
            }

            public void InitVars() {
                pistonDirectionList = new List<PistonDirection>{
                    PistonDirection.XNeg,
                    PistonDirection.XPos,
                    PistonDirection.YNeg,
                    PistonDirection.YPos,
                    PistonDirection.ZNeg,
                    PistonDirection.ZPos
                };

                baseDirectionList = new List<Base6Directions.Direction> {
                     Base6Directions.Direction.Backward,
                     Base6Directions.Direction.Forward,
                     Base6Directions.Direction.Left,
                     Base6Directions.Direction.Right,
                     Base6Directions.Direction.Up,
                     Base6Directions.Direction.Down
                };

            }

            public void InitPistons() {
                pistons = new Dictionary<PistonDirection, List<IMyPistonBase>>();
                foreach (var pistonDirection in pistonDirectionList)
                {
                    pistons.Add(pistonDirection, new List<IMyPistonBase>());
                }
            }

            public void InitDirectionMap()
            {
                directonMap = new Dictionary<Base6Directions.Direction, PistonDirection?>();
                foreach (var baseDirection in baseDirectionList)
                {
                    directonMap[baseDirection] = null;
                }
            }

            public void addPiston(PistonDirection direction, IMyPistonBase piston)
            {
                pistons[direction].Add(piston);
            }
        }

        class ConfigParser
        {
            enum State
            {
                StartOfLine,
                Key,
                KeyValueDelim,
                Float,
                String,
                EndOfLine,
                Ignore,
                KeyEnd,
                PostDecimal,
                UnknowKey,
                Escape,
                MaybeKeyRepeat
            }

            enum ConfigKeySymbols
            {
                XPos,
                XNeg,
                YPos,
                YNeg,
                ZPos,
                ZNeg,
                PrinterTag,
                Speed,
                Step,
                XExt,
                YExt,
                ZExt
            }

            enum TreePath
            {
                XPath,
                YPath,
                ZPath,
                SPath
            }

            enum Expect
            {
                String,
                Float,
                Integer
            }

            enum StringQuote
            {
                Double,
                Single
            }

            const string printerTagDefault = "pp";
            const string xForwardTagDefault = "x+";
            const string xReverseTagDefault = "x-";
            const string yForwardTagDefault = "y+";
            const string yReverseTagDefault = "y-";
            const string zForwardTagDefault = "z+";
            const string zReverseTagDefault = "z-";

            InfoDisplay infoDisplay;
            int line_no;
            int key_idx;
            Dictionary<ConfigKeySymbols, int> symbolTrack;
            State state;
            Expect? expect;
            ConfigKeySymbols? key;
            Config cfObj;
            string buffer;
            string line_buffer;
            TreePath? treePath;
            string src;
            StringQuote? stringQuote;

            public ConfigParser(InfoDisplay info, ref Config config, string str)
            {
                infoDisplay = info;
                cfObj = config;
                src = str;
            }


            void ConfigSetter(string v)
            {
                switch (key.Value)
                {
                    case ConfigKeySymbols.XPos:
                        Status(string.Format("set x+ to {0}", v));
                        cfObj.xForwardTag = v;
                        break;
                    case ConfigKeySymbols.XNeg:
                        Status(string.Format("set x- to {0}", v));
                        cfObj.xReverseTag = v;
                        break;
                    case ConfigKeySymbols.XExt:
                        Status(string.Format("set xext to {0}", v));
                        cfObj.xExt = float.Parse(v);
                        break;
                    case ConfigKeySymbols.YPos:
                        cfObj.yForwardTag = v;
                        break;
                    case ConfigKeySymbols.YNeg:
                        cfObj.yReverseTag = v;
                        break;
                    case ConfigKeySymbols.YExt:
                        cfObj.yExt = float.Parse(v);
                        break;
                    case ConfigKeySymbols.ZPos:
                        cfObj.zForwardTag = v;
                        break;
                    case ConfigKeySymbols.ZNeg:
                        cfObj.zReverseTag = v;
                        break;
                    case ConfigKeySymbols.ZExt:
                        cfObj.zExt = float.Parse(v);
                        break;
                    case ConfigKeySymbols.Speed:
                        cfObj.speed = float.Parse(v);
                        break;
                    case ConfigKeySymbols.Step:
                        cfObj.step = float.Parse(v);
                        break;
                    case ConfigKeySymbols.PrinterTag:
                        cfObj.printerTag = v;
                        break;
                }
                buffer = null;
                key = null;
                state = State.EndOfLine;
            }

            string KeyToString()
            {
                return KeyToString(key.Value);
            }

            string KeyToString(ConfigKeySymbols sym)
            {
                switch (sym)
                {
                    case ConfigKeySymbols.XPos:
                        return "x+";
                    case ConfigKeySymbols.XNeg:
                        return "x-";
                    case ConfigKeySymbols.XExt:
                        return "xExt";
                    case ConfigKeySymbols.YPos:
                        return "y+";
                    case ConfigKeySymbols.YNeg:
                        return "y-";
                    case ConfigKeySymbols.YExt:
                        return "yExt";
                    case ConfigKeySymbols.ZPos:
                        return "z+";
                    case ConfigKeySymbols.ZNeg:
                        return "z-";
                    case ConfigKeySymbols.ZExt:
                        return "zExt";
                    case ConfigKeySymbols.Speed:
                        return "speed";
                    case ConfigKeySymbols.Step:
                        return "step";
                    case ConfigKeySymbols.PrinterTag:
                        return "tag";
                    default:
                        throw new Exception("unknown symbol");
                }
            }

            Expect GetSymExpectedType(ConfigKeySymbols sym)
            {
                switch (sym)
                {
                    case ConfigKeySymbols.PrinterTag:
                    case ConfigKeySymbols.XPos:
                    case ConfigKeySymbols.XNeg:
                    case ConfigKeySymbols.YPos:
                    case ConfigKeySymbols.YNeg:
                    case ConfigKeySymbols.ZPos:
                    case ConfigKeySymbols.ZNeg:
                        return Expect.String;
                    case ConfigKeySymbols.XExt:
                    case ConfigKeySymbols.YExt:
                    case ConfigKeySymbols.ZExt:
                    case ConfigKeySymbols.Speed:
                    case ConfigKeySymbols.Step:
                        return Expect.Float;
                    default:
                        throw new Exception("unknown symbol");
                }
            }

            enum ParseLoopReturn
            {
                Error,
                Ok,
                NewLine
            }

            ParseLoopReturn Error(string msg)
            {
                infoDisplay.addError(string.Format("Error parsing conifg (line {0}) : {1}", line_no, msg));
                return ParseLoopReturn.Error;
            }

            void Status(string msg)
            {
                infoDisplay.addToBody(msg + "\n");
            }
            public IEnumerator<int> ParseConfig()
            {
                state = State.StartOfLine;
                key = null;
                expect = null;
                key_idx = 0;
                buffer = null;
                line_no = 1;
                treePath = null;
                stringQuote = null;

                symbolTrack = new Dictionary<ConfigKeySymbols, int> {
                    { ConfigKeySymbols.XPos, 0 },
                    { ConfigKeySymbols.XNeg, 0 },
                    { ConfigKeySymbols.YPos, 0 },
                    { ConfigKeySymbols.YNeg, 0 },
                    { ConfigKeySymbols.ZPos, 0 },
                    { ConfigKeySymbols.ZNeg, 0 },
                    { ConfigKeySymbols.PrinterTag, 0 },
                    { ConfigKeySymbols.Speed, 0 },
                    { ConfigKeySymbols.Step, 0 },
                    { ConfigKeySymbols.XExt, 0 },
                    { ConfigKeySymbols.YExt, 0 },
                    { ConfigKeySymbols.ZExt, 0 }
                };

                int opCounter = 0;

                Status("setup done, parsing string");
                foreach (var c in src)
                {
                    if (opCounter == 10)
                    {
                        Status("10 characters parsed, ceding to main program");
                        opCounter = 0;
                        yield return 1;
                    }
                    opCounter += 1;
                    switch (ParseLoop(c))
                    {
                        case ParseLoopReturn.Error:
                            goto ErrorOut;
                        case ParseLoopReturn.Ok:
                            line_buffer += c;
                            break;
                        case ParseLoopReturn.NewLine:
                            state = State.StartOfLine;
                            line_buffer = "";
                            line_no += 1;
                            break;
                    }

                }

                switch (state)
                {
                    case State.Float:
                    case State.PostDecimal:
                        ConfigSetter();
                        break;

                }


                if (cfObj.xForwardTag == null)
                {
                    cfObj.xForwardTag = "x+";
                }
                if (cfObj.xReverseTag == null)
                {
                    cfObj.xReverseTag = "x-";
                }
                if (cfObj.xExt == null)
                {
                    cfObj.xExt = 1.0f;
                }
                yield return 1;
                if (cfObj.yForwardTag == null)
                {
                    cfObj.yForwardTag = "y+";
                }
                if (cfObj.yReverseTag == null)
                {
                    cfObj.yReverseTag = "y-";
                }
                if (cfObj.yExt == null)
                {
                    cfObj.yExt = 1.0f;
                }
                yield return 1;
                if (cfObj.zForwardTag == null)
                {
                    cfObj.zForwardTag = "z+";
                }
                if (cfObj.zReverseTag == null)
                {
                    cfObj.zReverseTag = "z-";
                }
                if (cfObj.zExt == null)
                {
                    cfObj.zExt = 1.0f;
                }
                yield return 1;
                if (cfObj.printerTag == null)
                {
                    cfObj.printerTag = "tag";
                }
                if (cfObj.step == null)
                {
                    cfObj.step = 1.0f;
                }
                if (cfObj.speed == null)
                {
                    cfObj.speed = 1.0f;
                }

                ErrorOut:;
            }

            void ProgressKeyState(bool finish)
            {
                if (finish)
                {
                    state = State.KeyEnd;
                    key_idx = 0;
                }
                else
                {
                    key_idx += 1;
                }
            }

            void UnknownKey()
            {
                state = State.UnknowKey;
                key_idx = 0;
                treePath = null;
                key = null;
            }

            ParseLoopReturn SymHelper(ConfigKeySymbols sym, bool? con)
            {
                if (symbolTrack[sym] == 0)
                {
                    symbolTrack[sym] = line_no;
                    expect = GetSymExpectedType(sym);
                    key = sym;
                    treePath = null;

                    if (con != null)
                    {
                        ProgressKeyState(!con.Value);
                    }


                    return ParseLoopReturn.Ok;
                }
                else if (con.Value)
                {
                    // If we end up here then either the key is bad or the key is repeated
                    // but we don't know which yet
                    state = State.MaybeKeyRepeat;
                    key = sym;
                    return ParseLoopReturn.Ok;
                }
                else {
                    return Error(string.Format("{0} key is appears on lines {1} and {2}; keys may only be used once.", KeyToString(sym), symbolTrack[sym], line_no));
                }
            }

            ParseLoopReturn DimHelper(char c, ConfigKeySymbols pos, ConfigKeySymbols neg, ConfigKeySymbols ext)
            {
                switch (c)
                {
                    case '+':
                        return SymHelper(pos, false);
                    case '-':
                        return SymHelper(neg, false);
                    case 'e':
                    case 'E':
                        return SymHelper(ext, true);
                    default:
                        UnknownKey();
                        return ParseLoopReturn.Ok;
                }
            }

            ParseLoopReturn KeyParseHelper(int key_offset, char test, params char[] cs)
            {
                int idx = 2 * (key_idx - key_offset);

                Status(String.Format("test possible key letter `{0}` (key_idx: {3}, expect `{1}` or `{2}`)", test, cs[idx], cs[idx + 1], key_idx));

                if (cs[idx] == test || cs[idx + 1] == test)
                {
                    ProgressKeyState(idx + 2 == cs.Length);
                }
                else
                {
                    UnknownKey();
                }
                return ParseLoopReturn.Ok;
            }

            void ConfigSetter()
            {
                ConfigSetter(buffer);
            }

            ParseLoopReturn ParseNumeric(char c)
            {
                switch (c) {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        buffer += c;
                        return ParseLoopReturn.Ok;
                    case '.':
                    case ',':
                        buffer += c;
                        state = State.PostDecimal;
                        return ParseLoopReturn.Ok;
                    default:
                        return Error(string.Format("Character not allowed: expected float value, found `{0}`", c));
                }
            }

            ParseLoopReturn ParseNumericNoDecimal(char c, bool isFloat)
            {
                switch (c)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        buffer += c;
                        return ParseLoopReturn.Ok;
                    case '.':
                    case ',':
                        if (isFloat)
                        {
                            return Error("Invalid floating point number (multiple decimal points?)");
                        }
                        else
                        {
                            return Error("Invalid integer, no decimal points allowed");
                        }
                    default:
                        return Error(string.Format("Character not allowed: expected float value, found `{0}`", c));
                }
            }

            char StringDescribe(StringQuote sq)
            {
                switch(sq) {
                    case StringQuote.Double:
                        return '"';
                    case StringQuote.Single:
                        return '\'';
                    default:
                        throw new Exception("unknown enum member");
                }
            }

            Exception BadEnum()
            {
                return new Exception("Uknown enum member");
            }

            private ParseLoopReturn ParseLoop(char c)
            {
                if (c == '\n')
                {
                    Status("found new line");
                    switch (state)
                    {
                        case State.String:
                        case State.Escape:
                            return Error(string.Format("String does not have closing quote (opened with a {0})", StringDescribe(stringQuote.Value)));
                        case State.Key:
                        case State.KeyValueDelim:
                        case State.KeyEnd:
                        case State.UnknowKey:
                        case State.MaybeKeyRepeat:
                            return Error("Unexpected line end; line must must have a key and a value but only found key or partial key");
                        case State.Float:
                        case State.PostDecimal:
                            ConfigSetter();
                            return ParseLoopReturn.NewLine;
                        case State.EndOfLine:
                        case State.StartOfLine:
                        case State.Ignore:
                            return ParseLoopReturn.NewLine;
                        default:
                            throw BadEnum();
                    }
                }
                else if (state == State.Ignore)
                {
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.Key)
                {
                    if (key != null)
                    {
                        switch (key)
                        {
                            case ConfigKeySymbols.PrinterTag:
                                return KeyParseHelper(1, c, 'a', 'A', 'g', 'G');
                            case ConfigKeySymbols.Step:
                                return KeyParseHelper(2, c, 'e', 'E', 'p', 'P');
                            case ConfigKeySymbols.Speed:
                                return KeyParseHelper(2, c, 'e', 'E', 'e', 'E', 'd', 'D');
                            case ConfigKeySymbols.XExt:
                            case ConfigKeySymbols.YExt:
                            case ConfigKeySymbols.ZExt:
                                return KeyParseHelper(2, c, 'x', 'X', 't', 'T');
                            default:
                                throw BadEnum();
                        }
                    }
                    else
                    {
                        switch (treePath)
                        {
                            case TreePath.XPath:
                                return DimHelper(c, ConfigKeySymbols.XPos, ConfigKeySymbols.XNeg, ConfigKeySymbols.XExt);
                            case TreePath.YPath:
                                return DimHelper(c, ConfigKeySymbols.YPos, ConfigKeySymbols.YNeg, ConfigKeySymbols.YExt);
                            case TreePath.ZPath:
                                return DimHelper(c, ConfigKeySymbols.ZPos, ConfigKeySymbols.ZNeg, ConfigKeySymbols.ZExt);
                            case TreePath.SPath:
                                switch (c)
                                {
                                    case 't':
                                    case 'T':
                                        return SymHelper(ConfigKeySymbols.Speed, true);
                                    case 'p':
                                    case 'P':
                                        return SymHelper(ConfigKeySymbols.Speed, true);
                                    default:
                                        UnknownKey();
                                        return ParseLoopReturn.Ok;
                                }
                            default:
                                throw BadEnum();
                        }
                    }
                }
                else if (state == State.Escape)
                {
                    if (c == '\'' || c == '\\' || c == '"')
                    {
                        Status("found escaped character");
                        state = State.String;
                        buffer += c;
                        return ParseLoopReturn.Ok;
                    }
                    else
                    {
                        return Error("Only `\"`, `'` and `\\` characters can be escaped");
                    }
                }
                else if ((c == '\'' && stringQuote == StringQuote.Single) || (c == '"' && stringQuote == StringQuote.Double))
                {
                    Status("found end of quoted string");
                    stringQuote = null;
                    ConfigSetter();
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.String)
                {
                    if (c == '\\')
                    {
                        Status("found start of escaped character");
                        state = State.Escape;
                        return ParseLoopReturn.Ok;
                    }
                    else if (c == '\'' || c == '"')
                    {
                        return Error("Unexpected quote in string (escape the quote if you meant to use it)");
                    }
                    else
                    {
                        buffer += c;
                        return ParseLoopReturn.Ok;
                    }
                }
                else if (c == ' ')
                {
                    Status("found whitespace");
                    switch (state)
                    {
                        case State.StartOfLine:
                        case State.KeyValueDelim:
                        case State.EndOfLine:
                            return ParseLoopReturn.Ok;
                        case State.KeyEnd:
                            state = State.KeyValueDelim;
                            return ParseLoopReturn.Ok;
                        case State.Float:
                        case State.PostDecimal:
                            ConfigSetter();
                            return ParseLoopReturn.Ok;
                        case State.UnknowKey:
                            return Error(string.Format("Unknown key: `{0}`", line_buffer));
                        case State.MaybeKeyRepeat:
                            return Error(string.Format("{0} key is appears on lines {1} and {2}; keys may only be used once.", KeyToString(), symbolTrack[key.Value], line_no));
                        default:
                            throw BadEnum();
                    }
                }
                else if (state == State.UnknowKey)
                {
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.KeyEnd)
                {
                    return Error("Unknown key (perhaps missing whitespace?)");
                }
                else if ((state == State.EndOfLine || state == State.StartOfLine) && c == '#')
                {
                    Status("found a comment start #");
                    state = State.Ignore;
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.EndOfLine)
                {
                    return Error("Unexpected characters after value; each line must have only a key and a value");
                }
                else if (state == State.StartOfLine)
                {
                    Status(string.Format("found key start"));
                    state = State.Key;
                    key_idx = 1;

                    switch (c)
                    {
                        case 'x':
                        case 'X':
                            treePath = TreePath.XPath;
                            Status("explore keys starting with x");
                            break;
                        case 'y':
                        case 'Y':
                            treePath = TreePath.YPath;
                            Status("explore keys starting with y");
                            break;
                        case 'z':
                        case 'Z':
                            treePath = TreePath.ZPath;
                            Status("explore keys starting with z");
                            break;
                        case 'S':
                        case 's':
                            treePath = TreePath.SPath;
                            Status("explore keys starting with s");
                            break;
                        case 'T':
                        case 't':
                            Status("key may be `tag`");
                            return SymHelper(ConfigKeySymbols.PrinterTag, null);
                        default:
                            UnknownKey();
                            break;
                    }
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.KeyValueDelim)
                {
                    switch (expect)
                    {
                        case Expect.String:
                            expect = null;
                            switch (c)
                            {
                                case '\'':
                                    state = State.String;
                                    stringQuote = StringQuote.Single;
                                    return ParseLoopReturn.Ok;
                                case '"':
                                    state = State.String;
                                    stringQuote = StringQuote.Double;
                                    return ParseLoopReturn.Ok;
                                default:
                                    return Error("Expected a string value but value is not quoted");
                            }
                        case Expect.Float:
                            state = State.Float;
                            expect = null;
                            return ParseNumeric(c);
                        default:
                            throw BadEnum();
                    }
                }
                else if (state == State.Float)
                {
                    return ParseNumeric(c);
                }
                else if (state == State.PostDecimal)
                {
                    return ParseNumericNoDecimal(c, true);
                }
                else
                {
                    return Error("Uknown state");
                }
            }
        }


        class ProgramSetup
        {
            Config CF;
            PrinterState State;
            InfoDisplay InfoOut;

            public ProgramSetup(Config cf, PrinterState ps, InfoDisplay display)
            {
                CF = cf;
                State = ps;
                InfoOut = display;
            }

            IEnumerable<int> Setup()
            {
                if (!State.init)
                {
                    State.InitVars();
                    State.InitPistons();
                    yield return 1;
                    State.InitDirectionMap();
                    yield return 1;
                }
            }

            public void getPistonTags(IMyPistonBase piston)
            {
                // Is the piston tagged, which direction is it tagged for?
                PistonDirection? maybeDirectionPRF = null;
                foreach (var pistonDirectionPRF in State.pistonDirectionList)
                {
                    if (piston.CustomName.Contains(State.Tags[pistonDirectionPRF]))
                    {
                        if (maybeDirectionPRF.HasValue)
                        {
                            InfoOut.addError("Piston with name `" + piston.CustomName + "` has more than 1 direction tag");
                            return;
                        }
                        else
                        {
                            maybeDirectionPRF = pistonDirectionPRF;
                        }
                    }
                }

                // Grab the `forward` direction of the piston
                var forwardDirectionGRF = piston.Orientation.Forward;

                if (maybeDirectionPRF.HasValue)
                {
                    var direction = maybeDirectionPRF.Value;
                    if (State.directonMap[forwardDirectionGRF] == null)
                    {
                        // If this `forward` direction has not been seen before then
                        // and this piston is tagged then we learn what tag points in
                        // this direction

                        State.directonMap[forwardDirectionGRF] = direction;
                        State.directonMap[Base6Directions.GetOppositeDirection(forwardDirectionGRF)] = PistonDirectionInvert(direction);
                    }
                    else if (direction != State.directonMap[forwardDirectionGRF])
                    {
                        // If this `forward` direction has been seen before but 
                        // the tag is different then we can't continuie as the 
                        // situation is ambigious

                        InfoOut.addError("Multiple pistons with `" + PistonDirectionToString(direction) + "` tag are in different orientations");
                        return;
                    }
                    State.pistonAdd(direction, piston);
                }
                else if (State.directionTagged(forward))
                {
                    // There's no tag for this piston but if a tagged piston already
                    // told us what this pistons direction is we're good
                    State.pistonAdd(forward, piston);
                }
                else
                {
                    // Not enough info at this time, we'll need to take another look later
                    secondRun.Add(piston);
                }
            }
        }


        class StackRunner
        {
            Stack<IEnumerator<int>> runners;
            Queue<IEnumerator<int>> pushBuffer;
            bool isRunning;
            bool isRolledBack;
            bool isStopped;

            public StackRunner()
            {
                runners = new Stack<IEnumerator<int>>();
                pushBuffer = new Queue<IEnumerator<int>>();
                isRunning = false;
                isRolledBack = false;
                isStopped = false;
            }

            public void Stop()
            {
                isStopped = true;
            }

            public void RollbackMarker()
            {
                Push(null);
            }

            public void DoRollback()
            {
                isRolledBack = true;
                while (runners.Count > 0)
                {
                    var item = runners.Pop();
                    if (item == null)
                    {
                        return;
                    }
                    else
                    {
                        item.Dispose();
                    }
                }
            }

            public bool Run()
            {
                isRunning = true;
                var r = false;
                if (!isStopped && runners.Count > 0)
                {
                    IEnumerator<int> item;
                    while ((item = runners.Pop()) == null) ;

                    if (item.MoveNext() && !isRolledBack)
                    {
                        runners.Push(item);
                    }
                    else
                    {
                        item.Dispose();
                    }

                    while (pushBuffer.Count > 0)
                    {
                        runners.Push(pushBuffer.Dequeue());
                    }

                    r = true;
                }

                isRunning = false;
                isRolledBack = false;
                return r;
            }

            public void Push(IEnumerator<int> item)
            {
                if (isRunning)
                {
                    pushBuffer.Enqueue(item);
                }
                else
                {
                    runners.Push(item);
                }
            }
        }


        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }


        class CFDump
        {
            InfoDisplay id;
            Config cf;

            public CFDump(ref InfoDisplay myid, ref Config mycf)
            {
                id = myid;
                cf = mycf;
            }

            void helper(InfoDisplay id, string key, string value)
            {
                if (value != null)
                {
                    id.addToBody(string.Format("{0} : \"{1}\"\n", key, value));
                }
                else
                {
                    id.addToBody(string.Format("{0} : null\n", key));
                }
            }
            void helper(InfoDisplay id, string key, float? value)
            {
                if (value != null)
                {
                    id.addToBody(string.Format("{0} : {1}\n", key, value));
                }
                else
                {
                    id.addToBody(string.Format("{0} : null\n", key));
                }
            }

            public IEnumerator<int> configDump()
            {
                helper(id, "Printer Tag", cf.printerTag);
                helper(id, "X+Tag", cf.xForwardTag);
                helper(id, "X-Tag", cf.xReverseTag);
                helper(id, "XExt", cf.xExt);
                helper(id, "Y+Tag", cf.yForwardTag);
                helper(id, "Y-Tag", cf.yReverseTag);
                helper(id, "YExt", cf.yExt);
                helper(id, "Z+Tag", cf.zForwardTag);
                helper(id, "Z-Tag", cf.zReverseTag);
                helper(id, "ZExt", cf.zExt);
                helper(id, "Step", cf.step);
                helper(id, "Speed", cf.speed);
                yield return 1;
            }
        }


        class InfoDisplay
        {
            Program prog;
            List<string> errors;
            string messageBody;
            string messageStatusLine;

            public InfoDisplay(Program myProgram)
            {
                prog = myProgram;
                messageBody = "Stalwart Piston Array\n";
                errors = new List<string> { };
            }


            public void reset()
            {
                messageBody = "Stalwart Piston Array\n";
                errors.Clear();
            }

            public void writeInfo()
            {
                if (errors.Count() > 0)
                {
                    prog.Echo(messageBody);
                    prog.Echo("Cannot run due to following error(s):\n");
                    foreach (var error in errors)
                    {
                        prog.Echo(error + "\n");
                    }
                }
                else
                {
                    prog.Echo(messageBody);
                    if (messageStatusLine != null)
                    {
                        prog.Echo("\n --- \n" + messageStatusLine);
                    }
                }
            }

            public void addToBody(string s)
            {
                messageBody = messageBody + s;
            }

            public void setStatusLine(string s)
            {
                messageStatusLine = s;
            }

            public void addError(string e)
            {
                errors.Add(e);
            }
        }

        ConfigParser cfParser;
        Config cf;
        CFDump cfd;
        StackRunner runner;
        InfoDisplay infoDisplay;


        UpdateType mask = UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100;

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Terminal) == UpdateType.Terminal)
            {
                switch (argument.Trim().ToLower())
                {
                    case "setup":
                        Echo("run");
                        infoDisplay = new InfoDisplay(this);
                        cf = new Config();
                        cfd = new CFDump(ref infoDisplay, ref cf);
                        cfParser = new ConfigParser(infoDisplay, ref cf, Me.CustomData);
                        runner = new StackRunner();
                        runner.Push(cfd.configDump());
                        runner.Push(cfParser.ParseConfig());

                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        break;
                }
            }
            else if ((updateSource & mask) != 0)
            {
                infoDisplay.writeInfo();
                runner.Run();
            }
        }
    }
}
