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
        /// Return the piston direction that is the inverse of the passed direction
        static public PistonDirection PistonDirectionInvert(PistonDirection direction)
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
  
        static public string PistonDirectionToString(PistonDirection direction)
        {
            /// Return a string representation of the piston direction
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

        /// Encapsulate the scripts configuration
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


        /// Container for a printer's pistons 
        class PrinterPistons
        {
            public Dictionary<PistonDirection, List<IMyPistonBase>> pistons;
            public List<PistonDirection> pistonDirectionList;
            public List<Base6Directions.Direction> baseDirectionList;
            public Dictionary<Base6Directions.Direction, PistonDirection?> directonMap;
            public Dictionary<PistonDirection, string> Tags;
            public bool init;

            public PrinterPistons()
            {
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

                directonMap = new Dictionary<Base6Directions.Direction, PistonDirection?>();
                foreach (var baseDirection in baseDirectionList)
                {
                    directonMap[baseDirection] = null;
                }

                pistons = new Dictionary<PistonDirection, List<IMyPistonBase>>();
                foreach (var pistonDirection in pistonDirectionList)
                {
                    pistons.Add(pistonDirection, new List<IMyPistonBase>());
                }
            }

            public void AddPiston(PistonDirection direction, IMyPistonBase piston)
            {
                pistons[direction].Add(piston);
            }

            public void AddPiston(Base6Directions.Direction direction, IMyPistonBase piston)
            {
                if (directonMap[direction].HasValue)
                {
                    pistons[directonMap[direction].Value].Add(piston);
                }
                else
                {
                    throw new Exception("Cannot add a psiton using GRF when GRF is not yet mapped.");
                }
            }

            public bool DirectionIsTagged(Base6Directions.Direction direction)
            {
                return directonMap[direction].HasValue;
            }
        }

        /// Conifguration parseer
        class ConfigParser
        {
            ///  All possible states of the parser
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

            /// 
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

            /// Based on the initial keys on the configuration variable
            /// we choose which  to 
            enum TreePath
            {
                XPath,
                YPath,
                ZPath,
                SPath
            }

            /// Each configuration key is associated with a data type. This
            /// represents the data that is expected and it is an error if 
            /// this expecation is not met.
            enum Expect
            {
                String,
                Float,
                Integer // TODO: are there integers?
            }

            ///  Return states of a subparser
            enum ParseLoopReturn
            {
                Error,
                Ok,
                NewLine
            }

            /// raise this exception if we get an unknown enum member
            Exception BadEnum()
            {
                return new Exception("Uknown enum member");
            }

            /// the strings 
            const string printerTagDefault = "pp";
            const string xForwardTagDefault = "x+";
            const string xReverseTagDefault = "x-";
            const string yForwardTagDefault = "y+";
            const string yReverseTagDefault = "y-";
            const string zForwardTagDefault = "z+";
            const string zReverseTagDefault = "z-";


            ///  The string being parsed.
            string src;
            /// The sink to which status information is to be sent
            InfoDisplay infoDisplay;
            ///  The configuration object to which the parsed configuration
            ///  values are to be set
            Config cfObj;

            ///  The current line of `src` that is being parsed
            int line_no;
            /// Contains all characters in the current line
            string line_buffer;
            int key_idx;


            string buffer;
            TreePath? treePath;

            ///  The current state of the parser
            State state;
            /// The currently expected data type
            Expect? expect;
            ///  The current configuration key that is being parsed
            ConfigKeySymbols? key;
            ///  if parsing a string this is True if the string is quoted
            ///  with a double quote else False
            bool? isDoubleQuote;
            ///  track the first line where a setting is declared for
            ///  possible debugging 
            Dictionary<ConfigKeySymbols, int> symbolTrack;

            public ConfigParser(InfoDisplay info, ref Config config, string str)
            {
                infoDisplay = info;
                cfObj = config;
                src = str;
            }

            /// Set the appropiate variable on the configuration object
            /// to the passed value
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

            /// Set the appropiate variable on the configuration object
            /// to the value in the buffer
            void ConfigSetter()
            {
                ConfigSetter(buffer);
            }

            /// Return a string representation of the current key configuration
            /// key symbol
            string KeyToString()
            {
                return KeyToString(key.Value);
            }

            /// Return a string representation of the passed configuration
            /// key symbol
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

            /// Get the expected return type for the setting
            /// represented by the passed configuration symbol
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

            /// Return " if isDoubleQuote is True else '
            char QuoteChar(bool isDoubleQuote)
            {
                return isDoubleQuote ? '"' : '\'';
            }

            /// Send an error message to the info display and return an error
            /// state
            ParseLoopReturn Error(string msg)
            {
                infoDisplay.addError(string.Format("Error parsing conifg (line {0}) : {1}", line_no, msg));
                return ParseLoopReturn.Error;
            }


            /// Send a status message to the info display and return an error
            /// state
            void Status(string msg)
            {
                infoDisplay.addToBody(msg + "\n");
            }

            /// Helper that progress eskey parsing state
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

            /// Helper to set state for an unknown key and reset key parsing state.
            void UnknownKey()
            {
                state = State.UnknowKey;
                key_idx = 0;
                treePath = null;
                key = null;
            }

            /// Setup the symbolic represention of the current parsed setting
            /// if this symbol has already been seen then we enter a fail state
            ParseLoopReturn SymHelper(ConfigKeySymbols sym, bool? con)
            {
                if (symbolTrack[sym] == 0)
                {
                    // Symbol not seen, everything is happy

                    // save the symbol as current key
                    key = sym;

                    // Remeber where this symbol was used in 
                    // case of future debugging needs
                    symbolTrack[sym] = line_no;

                    // Now we know which setting we've got we can
                    // forecase what type the settings value should be
                    expect = GetSymExpectedType(sym);

                    // The setting has been finalised, no need
                    // to keep markers to possible alternatives
                    treePath = null;

                    // And progress are parser state if required
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

            /// helper called when parsing a setting for a certain direction
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

            /// Helper to validate string representing key
            /// key_offset is where this helper starts examining the string from
            /// test is the character currently being parsed
            /// cs are the candidate characters to test against. These are provided as
            /// upper and lower case pairs and test is compared to both - a cludge
            /// but it gets where wed need to go
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


            /// attempt to parse c as part of a numeric value. This parse function
            /// allows the number to contain a decimal point.
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
            /// attempt to parse c as part of a numeric value. This parse function
            /// does not allows the number to contain a decimal point.
            /// 
            /// TODO: are there integers? is this always a float?
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
                        // Decimal points are not allowed
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

            /// Parse a character
            private ParseLoopReturn ParseLoop(char c)
            {
                if (c == '\n')
                {
                    Status("found new line");
                    switch (state)
                    {
                        case State.String:
                        case State.Escape:
                            // The string has a well defineded end (the closing quote)
                            // A suprised end means the string was not complete - fail
                            return Error(string.Format("String does not have closing quote (opened with a {0})", QuoteChar(isDoubleQuote.Value)));
                        case State.Key:
                        case State.KeyValueDelim:
                        case State.KeyEnd:
                        case State.UnknowKey:
                        case State.MaybeKeyRepeat:
                            // The setting name was still being parsed - fail
                            return Error("Unexpected line end; line must must have a key and a value but only found key or partial key");
                        case State.Float:
                        case State.PostDecimal:
                            // Posibble that a float value for the setting has
                            // een parsed but not yet saved to the setting -
                            // save it now
                            ConfigSetter();
                            return ParseLoopReturn.NewLine;
                        case State.EndOfLine:
                        case State.StartOfLine:
                        case State.Ignore:
                            // Blank lines, expected end of lines and ingored
                            // remainding of lines make us happy
                            return ParseLoopReturn.NewLine;
                        default:
                            throw BadEnum();
                    }
                }
                else if (state == State.Ignore)
                {
                    // The rest of this line has been marked to be ignored,
                    // continue
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.Key)
                {
                    // we have started parsing a key so lets continue...

                    if (key != null)
                    {
                        // If we are then there's only one possible setting
                        // name that can match the characters already passed
                        // so all that's left to do is validate the remaining
                        // characters in the setting name
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
                            // If we end up here we have narrowed the possible
                            // setting names that will match but there's still
                            // multiple options - narrow the choices down further
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
                                        return SymHelper(ConfigKeySymbols.Step, true);
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
                    // This character has been escaped by the last character
                    // make sure this a character than is allowed to be escaped
                    // and add it to the buffer if it is. Else fail
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
                else if (state == State.String)
                {
                    if ((c == '\'' && (!isDoubleQuote.Value)) || (c == '"' && isDoubleQuote.Value))
                    {
                        // We have found the quote matching the starting quote of the string 
                        // finish parsing the string value
                        Status("found end of quoted string");
                        isDoubleQuote = null;
                        ConfigSetter();
                        return ParseLoopReturn.Ok;
                    }
                    else if (c == '\\')
                    {
                        // Setup for next character to be escaped
                        Status("found start of escaped character");
                        state = State.Escape;
                        return ParseLoopReturn.Ok;
                    }
                    else
                    {
                        // Just part of the string, on to the buffer it goes
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
                    // We don't always want to immediatly error out if we find an unknown key
                    // continue even through we know we're in a fail state
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.KeyEnd)
                {
                    // the setting name was expected to end but we found a character
                    // other than the expected whitespace - enter a fail state
                    return Error("Unknown key (perhaps missing whitespace?)");
                }
                else if ((state == State.EndOfLine || state == State.StartOfLine) && c == '#')
                {
                    // comments can be at the end or start of a line (not in
                    // the middle of setting line) - mark everything else on
                    // this line to be ignored
                    Status("found a comment start #");
                    state = State.Ignore;
                    return ParseLoopReturn.Ok;
                }
                else if (state == State.EndOfLine)
                {
                    // the line was expected to end but we found a character
                    // other than the expected line break or comment - enter a fail state
                    return Error("Unexpected characters after value; each line must have only a key and a value");
                }
                else if (state == State.StartOfLine)
                {
                    // We have a character that is comment starter or whitespace
                    // and we are at the start of the line - start key parsing
                    Status(string.Format("found key start"));
                    state = State.Key;
                    key_idx = 1;

                    // Use the first character in the setting name to
                    // pair down the possibe settings this setting
                    // name might refer to

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
                    // We have seen a key/value deliminator and
                    // have found a non whitespace character - parse
                    // it as the settings value
                    switch (expect)
                    {
                        case Expect.String:
                            // the setting takes a string value and a string
                            // value must be quoted - make sure we have a quote
                            // and remember if it was a single or double so the
                            // end quote can be matched
                            expect = null;
                            switch (c)
                            {
                                case '\'':
                                    state = State.String;
                                    isDoubleQuote = false;
                                    return ParseLoopReturn.Ok;
                                case '"':
                                    state = State.String;
                                    isDoubleQuote = true;
                                    return ParseLoopReturn.Ok;
                                default:
                                    return Error("Expected a string value but value is not quoted");
                            }
                        case Expect.Float:
                            // The setting takes a float value
                            state = State.Float;
                            expect = null;
                            return ParseNumeric(c);
                        default:
                            throw BadEnum();
                    }
                }
                else if (state == State.Float)
                {
                    // Continue parsing float value
                    return ParseNumeric(c);
                }
                else if (state == State.PostDecimal)
                {
                    // Continue parsing float value - if we're here
                    // a decimal point has been seen and further
                    // decimal points are an error
                    return ParseNumericNoDecimal(c, true);
                }
                else
                {
                    return Error("Uknown state");
                }
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
                isDoubleQuote = null;

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

                /// After parsing we may have a hanging
                /// value - set it if we do
                switch (state)
                {
                    case State.Float:
                    case State.PostDecimal:
                        ConfigSetter();
                        break;

                }

                // Finally apply defaults

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
        }


        class ProgramSetup
        {
            Config CF;
            PrinterPistons State;
            InfoDisplay InfoOut;
            List<IMyPistonBase> secondRun;

            public ProgramSetup(Config cf, PrinterPistons ps, InfoDisplay display)
            {
                CF = cf;
                State = ps;
                InfoOut = display;
            }

            public void GetPistonTags(IMyPistonBase piston)
            {
                // PRF = Printer Reference Frame
                // GRF = Global Reference Fram

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
                    
                    if (direction != State.directonMap[forwardDirectionGRF])
                    {
                        // If this `forward` direction has been seen before but 
                        // the tag is different then we can't continuie as the 
                        // situation is ambigious

                        InfoOut.addError("Multiple pistons with `" + PistonDirectionToString(direction) + "` tag are in different orientations");
                        return;
                    }
                    else
                    {
                        if (State.directonMap[forwardDirectionGRF] == null)
                        {
                            // If this `forward` direction has not been seen before then
                            // and this piston is tagged then we learn what tag points in
                            // this direction

                            State.directonMap[forwardDirectionGRF] = direction;
                            State.directonMap[Base6Directions.GetOppositeDirection(forwardDirectionGRF)] = PistonDirectionInvert(direction);
                        }
                        State.AddPiston(direction, piston);
                    }
                }
                else if (State.DirectionIsTagged(forwardDirectionGRF))
                {
                    // There's no tag for this piston but if a tagged piston already
                    // told us what this pistons direction is we're good
                    State.AddPiston(forwardDirectionGRF, piston);
                }
                else
                {
                    // Not enough info at this time, we'll need to take another look later
                    secondRun.Add(piston);
                }
            }

            public IEnumerator<int> SecondRun()
            {
                int ops = 0;
                bool notFound = false;
                foreach (var piston in secondRun)
                {
                    var forwardDirectionGRF = piston.Orientation.Forward;

                    // If we have learnt what direction this pistion is meant to be facing in
                    // then add it as appropiate otherwise we don't have enough information
                    // and enter a fail state

                    if (State.DirectionIsTagged(forwardDirectionGRF))
                    {
                        State.AddPiston(forwardDirectionGRF, piston);
                    }
                    else
                    {
                        notFound = true;
                        break;
                    }

                    if (ops == 5)
                    {
                        ops = 0;
                        yield return 1;
                    }
                    else
                    {
                        ops += 1;
                    }
                }

                if (notFound)
                {
                    yield return 0;
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
