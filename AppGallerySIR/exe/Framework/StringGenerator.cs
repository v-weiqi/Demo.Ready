using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AppGallery.SIR
{
    /// <summary>
    /// Class to create strings from regular expressions.
    /// Usage 1: 
    /// StringGenerator.StringFromRegex("[0-9]{3}-[0-9]{3}-[0-9]{4}");  // will generate a random telephone number
    /// Usage 2:
    /// RegEx reg = "[0-9]{3}-[0-9]{3}-[0-9]{4}";
    /// StringGenerator.StringFromRegex(reg);                           // will generate a random telephone number
    /// 
    /// </summary>
    [Serializable]
    internal class StringGenerator
    {
        #region data members
        internal static Random rndGen = new Random(DateTime.Now.Millisecond); // always start with a random number
        #endregion

        #region StringFromRegex overloads

        /// <summary>
        /// use this overload to generate strings directly from a regex 
        /// </summary> 
        public static string StringFromRegex(Regex regex)
        {
            return StringFromRegex(regex.ToString());
        }

        /// <summary>
        /// use this overload to generate strings from a regular expression string value.
        /// </summary> 
        public static string StringFromRegex(string regex)
        {
            StringCompiler.IsInvalidSection = false;
            StringCompiler.InvalidNode = null;
            StringCompiler.InvalidableNodes.Clear();
            StringCompiler compiler = new StringCompiler();
            BaseNode node = compiler.Compile(regex);
            if (regex.IndexOf("\\i") != -1)
            {
                //something should have been invalidated
                //select a node to invalidate
                if (StringCompiler.InvalidableNodes.Count == 0)
                {
                    throw new ArgumentException("Asked to generate invalid: Impossible to invalidate");
                }
                StringCompiler.InvalidNode = StringCompiler.InvalidableNodes[StringGenerator.rndGen.Next(StringCompiler.InvalidableNodes.Count)];
                StringCompiler.InvalidNode.ReservePath(null);
            }
                        
            string result = node.Generate();

            if (StringCompiler.InvalidNode != null)
            {
                Regex compare = new Regex("^" + regex.Replace("\\i", "") + "$");
                if (compare.IsMatch(result))
                {
                    throw new ArgumentException(regex + ": Did not generate invalid string: " + result);
                }
            }
            return result;
        }
        #endregion

        //actual class for running through the engine    
        private class StringCompiler
        {
            public static bool IsInvalidSection = false;
            public static List<BaseNode> InvalidableNodes = new List<BaseNode>();           //Picked the invalidableNodes logic from codeproject, 
            //as without it the engine would not run correctly.        
            public static BaseNode InvalidNode = null;
            StringBuilder mRegex;
            List<BaseNode> BackRefs = new List<BaseNode>();
            List<BaseNode> NamedBackRefs = new List<BaseNode>();
            int mIndex = -1;
            char mCurrent = '0';
            bool mParseDone = false;

            public void ParseError(bool b, string message)
            {
                if (!b)
                    throw new ArgumentException("Regex parse error at index " + mIndex + ": " + message);
            }

            BaseNode GetBackRef(int index)
            {
                try
                {
                    //Backreference indexes are 1 based
                    return (index <= BackRefs.Count) ? BackRefs[index - 1]
                                                        : NamedBackRefs[index - BackRefs.Count - 1];
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }


            private BaseNode GetBackRef(string name)
            {
                foreach (StringSubExprNode node in NamedBackRefs)
                {
                    if (node.Name.Equals(name))
                        return node;
                }

                return null;
            }


            private void NextChar()
            {
                if (mIndex < mRegex.Length - 1)
                    mCurrent = mRegex[++mIndex];
                else
                    mParseDone = true;
            }


            public char EscapeValue()
            {
                int value = 0;

                if (Char.ToLower(mCurrent) == 'x')
                {
                    NextChar();

                    ParseError(Uri.IsHexDigit(mCurrent), "Invalid escape character.");

                    while (Uri.IsHexDigit(mCurrent) && (!mParseDone))
                    {
                        value *= 16;
                        value += Char.IsDigit(mCurrent) ? mCurrent - '0' : Char.ToLower(mCurrent) - 'a' + 10;
                        NextChar();
                    }
                }
                else if (mCurrent == '0')
                {
                    NextChar();

                    ParseError(mCurrent >= '0' && mCurrent <= '7', "Invalid escape character.");

                    while (mCurrent >= '0' && mCurrent <= '7' && (!mParseDone))
                    {
                        value *= 8;
                        value += mCurrent - '0';
                        NextChar();
                    }
                }
                else if (Char.IsDigit(mCurrent))
                {
                    while (Char.IsDigit(mCurrent) && (!mParseDone))
                    {
                        value *= 10;
                        value += mCurrent - '0';
                        NextChar();
                    }
                }
                else
                {
                    ParseError(false, "Invalid escape character.");
                }

                return (char)value;
            }


            private char EscapeSetChar()
            {
                char c = '0';

                if (Char.ToLower(mCurrent) == 'x' || Char.IsDigit(mCurrent))
                    return EscapeValue();

                switch (mCurrent)
                {
                    case '^': c = '^'; break;
                    case '*': c = '*'; break;
                    case '\\': c = '\\'; break;
                    case 'r': c = '\r'; break;
                    case 'a': c = '\a'; break;
                    case 'b': c = '\b'; break;
                    case 'e': c = '\x1B'; break;
                    case 'n': c = '\n'; break;
                    case 't': c = '\t'; break;
                    case 'f': c = '\f'; break;
                    case 'v': c = '\v'; break;
                    case '-': c = '-'; break;
                    case '[': c = '['; break;
                    case ']': c = ']'; break;
                    default:
                        ParseError(false, "Invalid escape inside of set.");
                        break;
                }

                NextChar();

                return c;
            }


            private char CompileSetChar()
            {
                char val = mCurrent;
                NextChar();
                ParseError(val != '-', "Invalid character inside set.");
                return (val == '\\') ? EscapeSetChar() : val;
            }


            public BaseNode Compile(string pattern)
            {
                mRegex = new StringBuilder(pattern);
                mParseDone = false;
                NextChar();
                return CompileExpr();
            }

            //Compile the expression i.e. main body or expr in paranthesis
            public BaseNode CompileExpr()
            {
                BaseNode branch = CompileBranch();

                if (mCurrent != '|')
                    return branch;

                StringOrNode expr = new StringOrNode();
                expr.Children.Add(branch);
                branch.ParentNode = expr;

                while (mCurrent == '|')
                {
                    NextChar();
                    BaseNode nextBranch = CompileBranch();
                    expr.Children.Add(nextBranch);
                    nextBranch.ParentNode = expr;
                }

                return expr;
            }


            public BaseNode CompileBranch()
            {
                BaseNode piece = CompilePiece();

                if (mParseDone || mCurrent == '|' || mCurrent == ')')
                    return piece;

                StringAndNode andNode = new StringAndNode();
                andNode.Children.Add(piece);
                piece.ParentNode = andNode;

                while (!(mParseDone || mCurrent == '|' || mCurrent == ')'))
                {
                    BaseNode nextPiece = CompilePiece();
                    andNode.Children.Add(nextPiece);
                    nextPiece.ParentNode = andNode;
                }

                return andNode;
            }


            public BaseNode CompilePiece()
            {
                BaseNode node = null;


                bool oldInvalidState = StringCompiler.IsInvalidSection;
                //check if we want to invalidate the 'atom' node and subnodes
                if (mCurrent == '\\' && mRegex[mIndex + 1] == 'i')
                {
                    NextChar();
                    NextChar();

                    StringCompiler.IsInvalidSection = true;
                }

                BaseNode atom = CompileAtom();

                //revert the invalidating state
                StringCompiler.IsInvalidSection = oldInvalidState;



                if (mCurrent == '\\' && mRegex[mIndex + 1] == 'i' && "*+?{".Contains(mRegex[mIndex + 2].ToString()))
                {
                    NextChar();
                    NextChar();

                    StringCompiler.IsInvalidSection = true;
                }

                const int MAXREPEAT = -1;

                switch (mCurrent)
                {
                    case '*':
                        node = new StringRepeatNode(atom, 0, MAXREPEAT, false);
                        NextChar();
                        break;
                    case '+':
                        node = new StringRepeatNode(atom, 1, MAXREPEAT, false);
                        NextChar();
                        break;
                    case '?':
                        node = new StringRepeatNode(atom, 0, 1, false);
                        NextChar();
                        break;
                    case '{':
                        int nMin = 0;
                        int nMax = 0;
                        bool sameChar = false;
                        NextChar();

                        if (mCurrent == '=')
                        {
                            sameChar = true;
                            NextChar();
                        }

                        int closeIndex = mRegex.ToString().IndexOf('}', mIndex);
                        ParseError(closeIndex != -1, "Expected '}'");

                        string[] repeatTokens = mRegex.ToString().Substring(mIndex, closeIndex - mIndex).
                                                Split(new char[] { ',' });

                        if (repeatTokens.Length == 1)
                        {
                            nMin = nMax = int.Parse(repeatTokens[0]);
                        }
                        else if (repeatTokens.Length == 2)
                        {
                            nMin = int.Parse(repeatTokens[0]);

                            if (repeatTokens[1].Length > 0)
                            {
                                nMax = int.Parse(repeatTokens[1]);
                            }
                            else
                            {
                                nMax = MAXREPEAT;
                            }
                        }
                        else
                        {
                            ParseError(false, "Repeat values cannot be parsed");
                        }

                        ParseError(nMin <= nMax || repeatTokens[1].Length == 0, "Max repeat is less than min repeat");
                        mIndex = closeIndex;
                        NextChar();
                        node = new StringRepeatNode(atom, nMin, nMax, sameChar);
                        break;
                    default:
                        node = atom;
                        break;
                }


                StringCompiler.IsInvalidSection = oldInvalidState;

                return node;
            }


            public BaseNode CompileAtom()
            {
                BaseNode atom = null;
                StringSetNode set = null;
                int start = 0;
                int end = 0;

                ParseError(!mParseDone, "Reached end of string. No element found.");
                ParseError(!("|)?+*{}".Contains(mCurrent.ToString())), "No element found.");

                switch (mCurrent)
                {
                    case '.':
                        atom = set = new StringSetNode(true);
                        set.AddRange(Convert.ToChar(0), Convert.ToChar(127));
                        NextChar();
                        break;
                    case '[':
                        NextChar();
                        atom = CompileSet();
                        break;
                    case '(':
                        int refIndex = 0;
                        NextChar();


                        if (mCurrent == '?')
                        {
                            NextChar();
                            if (mCurrent == ':')
                            {
                                NextChar();
                                refIndex = -2;
                            }
                            else
                            {
                                ExtractBackrefName(ref start, ref end);
                                refIndex = -1;
                            }
                        }

                        atom = new StringSubExprNode(CompileExpr());
                        ParseError(mCurrent == ')', "Expected ')'");
                        NextChar();

                        if (refIndex == -1)
                        {
                            (atom as StringSubExprNode).Name = mRegex.ToString().Substring(start, end - start + 1);
                            NamedBackRefs.Add(atom);
                        }
                        else if (refIndex == 0)
                        {
                            BackRefs.Add(atom);
                        }

                        break;
                    case '^':
                    case '$':
                        atom = new StringTextNode(String.Empty);
                        NextChar();
                        break;
                    case '\\':
                        NextChar();

                        if (Char.ToLower(mCurrent) == 'x' || Char.ToLower(mCurrent) == 'u' || mCurrent == '0')
                        {
                            atom = new StringTextNode(EscapeValue().ToString());
                        }
                        else if (Char.IsDigit(mCurrent))
                        {
                            atom = GetBackRef((int)EscapeValue());
                            ParseError(atom != null, "Couldn't find back reference");
                            atom = new StringSubExprNode(atom);
                        }
                        else if (mCurrent == 'k')
                        {
                            NextChar();
                            ExtractBackrefName(ref start, ref end);
                            atom = GetBackRef(mRegex.ToString().Substring(start, end - start + 1));
                            ParseError(atom != null, "Couldn't find back reference");
                            atom = new StringSubExprNode(atom);
                        }
                        else
                        {
                            atom = CompileSimpleMacro(mCurrent);
                            NextChar();
                        }
                        break;
                    default:
                        int closeIndex = mRegex.ToString().IndexOfAny("-*+?(){}\\[]^$.|".ToCharArray(), mIndex + 1);

                        if (closeIndex == -1)
                        {
                            mParseDone = true;
                            closeIndex = mRegex.Length - 1;
                            atom = new StringTextNode(mRegex.ToString().Substring(mIndex, closeIndex - mIndex + 1));
                        }
                        else
                        {
                            atom = new StringTextNode(mRegex.ToString().Substring(mIndex, closeIndex - mIndex));
                        }

                        mIndex = closeIndex;
                        mCurrent = mRegex[mIndex];
                        break;
                }

                return atom;
            }


            public void ExtractBackrefName(ref int start, ref int end)
            {
                char tChar = mCurrent;
                ParseError(tChar == '\'' || tChar == '<', "Backref must begin with ' or <.");


                if (tChar == '<')
                    tChar = '>';

                NextChar();

                ParseError((Char.ToLower(mCurrent) >= 'a' && Char.ToLower(mCurrent) <= 'z') || mCurrent == '_',
                                    "Invalid characters in backreference name.");
                start = mIndex;
                NextChar();

                while (mCurrent == '_' || Char.IsLetterOrDigit(mCurrent))
                    NextChar();

                ParseError(mCurrent == tChar, "Name end not found.");
                end = mIndex;
                NextChar();
            }


            public BaseNode CompileSet()
            {
                BaseNode atom = null;
                char cStart, cEnd;
                StringSetNode set;

                if (mCurrent == ':')
                {
                    NextChar();
                    int closeIndex = mRegex.ToString().IndexOf(":]");
                    atom = CompileMacro(mIndex, closeIndex - mIndex);
                    mIndex = closeIndex;
                    NextChar();
                    NextChar();
                    return atom;
                }

                if (mCurrent == '^')
                {
                    atom = set = new StringSetNode(false);
                    NextChar();
                }
                else
                {
                    atom = set = new StringSetNode(true);
                }

                if (mCurrent == '-' || mCurrent == ']') //if - or ] are specified as the first char, escape is not required
                {
                    set.AddChars(mCurrent.ToString());
                    NextChar();
                }

                while ((!mParseDone) && (mCurrent != ']'))
                {
                    cStart = CompileSetChar();

                    if (mCurrent == '-')
                    {
                        NextChar();
                        ParseError(!mParseDone && mCurrent != ']', "End of range is not specified.");
                        cEnd = CompileSetChar();
                        set.AddRange(cStart, cEnd);
                    }
                    else
                    {
                        set.AddChars(cStart.ToString());
                    }
                }

                ParseError(mCurrent == ']', "Expected ']'.");
                NextChar();
                return atom;
            }

            //Compile \d \D \s \S etc. From "Mastering regular expressions" book
            public BaseNode CompileSimpleMacro(char c)
            {
                BaseNode node = null;
                StringSetNode set = null;

                if (@"[]{}()*-+.?\|".Contains(c.ToString()))
                    return new StringTextNode(c.ToString());

                switch (c)
                {
                    case 'd': // [0-9]
                        node = set = new StringSetNode(true);
                        set.AddRange('0', '9');
                        break;
                    case 'D': // [^0-9]
                        node = set = new StringSetNode(false);
                        set.AddRange('0', '9');
                        break;
                    case 's':
                        node = set = new StringSetNode(true);
                        set.AddChars(" \r\n\f\v\t");
                        break;
                    case 'S':
                        node = set = new StringSetNode(false);
                        set.AddChars(" \r\n\f\v\t");
                        break;
                    case 'w': // [a-zA-Z0-9_]
                        node = set = new StringSetNode(true);
                        set.AddRange('a', 'z');
                        set.AddRange('A', 'Z');
                        set.AddRange('0', '9');
                        set.AddChars("_");
                        break;
                    case 'W': // [^a-zA-Z0-9_]
                        node = set = new StringSetNode(false);
                        set.AddRange('a', 'z');
                        set.AddRange('A', 'Z');
                        set.AddRange('0', '9');
                        set.AddChars("_");
                        break;
                    case 'f':
                        node = new StringTextNode("\f");
                        break;
                    case 'n':
                        node = new StringTextNode("\n");
                        break;
                    case 'r':
                        node = new StringTextNode("\r");
                        break;
                    case 't':
                        node = new StringTextNode("\t");
                        break;
                    case 'v':
                        node = new StringTextNode("\v");
                        break;
                    case 'A':
                    case 'Z':
                    case 'z':
                        node = new StringTextNode(String.Empty);
                        break;
                    default:
                        ParseError(false, "Invalid escape.");
                        break;
                }

                return node;
            }

            //Compile [:alpha:] [:punct:] etc . From the "mastering RegularExpressions" book
            public BaseNode CompileMacro(int index, int len)
            {
                ParseError(len >= 0, "Cannot parse macro.");
                string substr = mRegex.ToString().Substring(index, len);
                string expanded = null;

                switch (substr)
                {
                    case "alnum": expanded = "[a-zA-Z0-9]"; break;
                    case "alpha": expanded = "[a-zA-Z]"; break;
                    case "upper": expanded = "[A-Z]"; break;
                    case "lower": expanded = "[a-z]"; break;
                    case "digit": expanded = "[0-9]"; break;
                    case "xdigit": expanded = "[A-F0-9a-f]"; break;
                    case "space": expanded = "[ \t]"; break;
                    case "print": expanded = "[\\x20-\\x7F]"; break;
                    case "punct": expanded = "[,;.!'\"]"; break;
                    case "graph": expanded = "[\\x80-\\xFF]"; break;
                    case "cntrl": expanded = "[]"; break;
                    case "blank": expanded = "[ \t\r\n\f]"; break;
                    case "guid": expanded = "[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}"; break;
                    default: ParseError(false, "Cannot parse macro."); break;
                }

                StringCompiler subcompiler = new StringCompiler();
                return subcompiler.Compile(expanded);
            }
        }

        #region Nodes of the Tree
        internal abstract class BaseNode   //Base class for regex elements
        {
            public BaseNode ParentNode;
            public abstract string Generate();

            public virtual void ReservePath(BaseNode child)
            {
                if (ParentNode != null)
                {
                    ParentNode.ReservePath(this);
                }
            }

            //Assert in parsing
            static public void ParseError(bool pass, string message)
            {
                if (!pass)
                    throw new ArgumentException("Regex parse error: " + message);
            }
        }

        //text portion of a regex 
        private class StringTextNode : BaseNode
        {
            private StringBuilder NodeText;

            public StringTextNode(string str)
            {
                if ((StringCompiler.IsInvalidSection) && (!String.IsNullOrEmpty(str)))
                {
                    StringCompiler.InvalidableNodes.Add(this);
                }
                NodeText = new StringBuilder(str);
            }

            public override string Generate()
            {
                if (this == StringCompiler.InvalidNode)
                {
                    int pos = StringGenerator.rndGen.Next(NodeText.Length);
                    StringSetNode others = new StringSetNode(false);
                    others.AddChars(NodeText[pos].ToString());

                    char backup = NodeText[pos];
                    NodeText[pos] = others.Generate()[0];
                    string result = NodeText.ToString();

                    NodeText[pos] = backup;

                    return result;
                }
                else
                {
                    return NodeText.ToString();
                }
            }
        }

        private class StringSetNode : BaseNode
        {
            private int MapSize = 128;
            private byte[] Map = new byte[128];
            private bool PositiveSet;
            private int NumChoices;

            public StringSetNode(bool positiveSet)
            {
                if (StringCompiler.IsInvalidSection)
                {
                    StringCompiler.InvalidableNodes.Add(this);
                }

                PositiveSet = positiveSet;
                NumChoices = PositiveSet ? 0 : MapSize;
            }


            private void ExpandToUnicodeRange()
            {
                byte[] mNewMap = new byte[char.MaxValue + 1];
                Array.Copy(Map, 0, mNewMap, 0, 128);

                if (!PositiveSet)
                    NumChoices += char.MaxValue + 1 - 128;

                MapSize = char.MaxValue + 1;
                Map = mNewMap;
            }

            public void AddChars(string chars)
            {

                foreach (char c in chars.ToCharArray())
                {
                    if (c > MapSize - 1)
                        ExpandToUnicodeRange();

                    if (Map[c] == 0)
                    {
                        Map[c] = 1;
                        NumChoices += PositiveSet ? 1 : -1;
                    }
                }


                if ((PositiveSet && NumChoices == MapSize) || (!PositiveSet && NumChoices == 0))
                {

                    StringCompiler.InvalidableNodes.Remove(this);
                }
            }


            public void AddRange(char start, char end)
            {
                BaseNode.ParseError((start < end) && end <= char.MaxValue, "Invalid range specified in char set");

                if (end > MapSize)
                    ExpandToUnicodeRange();

                for (long c = start; c <= end; c++)
                {
                    if (Map[c] == 0)
                    {
                        Map[c] = 1;
                        NumChoices += PositiveSet ? 1 : -1;
                    }
                }

                if ((PositiveSet && NumChoices == MapSize) || (!PositiveSet && NumChoices == 0))
                {
                    StringCompiler.InvalidableNodes.Remove(this);
                }
            }

            public override string Generate()
            {
                if (this == StringCompiler.InvalidNode)
                {
                    BaseNode.ParseError(NumChoices > 0, "No valid range specified in char set");

                    int randIndex = StringGenerator.rndGen.Next(MapSize - NumChoices);

                    int i = -1;
                    while (randIndex >= 0)
                    {
                        i++;
                        if ((PositiveSet && Map[i] == 0) || (!PositiveSet && Map[i] == 1))
                            randIndex--;

                    }

                    return Convert.ToChar(i).ToString();
                }
                else
                {
                    BaseNode.ParseError(NumChoices > 0, "No valid range specified in char set");
                    int randIndex = StringGenerator.rndGen.Next(NumChoices);

                    int i = -1;
                    while (randIndex >= 0)
                    {
                        i++;
                        if ((PositiveSet && Map[i] == 1) || (!PositiveSet && Map[i] == 0))
                            randIndex--;

                    }

                    return Convert.ToChar(i).ToString();
                }
            }
        }

        private class StringRepeatNode : BaseNode
        {
            private int MinRepeat;
            private int MaxRepeat;
            private bool SameValue;
            private BaseNode RefNode;
            public static int extraRepetitions = 10;
            private BaseNode ReservedPath;


            public StringRepeatNode(BaseNode refNode, int minRepeat, int maxRepeat, bool sameValue)
            {
                if (StringCompiler.IsInvalidSection && (minRepeat > 0 || maxRepeat != -1))
                {
                    StringCompiler.InvalidableNodes.Add(this);
                }
                MinRepeat = minRepeat;
                MaxRepeat = maxRepeat;
                SameValue = sameValue;
                ReservedPath = null;
                RefNode = refNode;
                RefNode.ParentNode = this;
            }

            public override void ReservePath(BaseNode child)
            {
                ReservedPath = child;
                base.ReservePath(child);
            }
            public override string Generate()
            {
                int numRepeat;
                StringBuilder buffer = new StringBuilder();
                if (this == StringCompiler.InvalidNode)
                {
                    int repeatMore = StringGenerator.rndGen.Next(2);
                    if ((MaxRepeat != -1 && 1 == repeatMore) || MinRepeat == 0)
                    {
                        checked
                        {
                            numRepeat = StringGenerator.rndGen.Next(MaxRepeat + 1, MaxRepeat + 11);
                        }
                    }
                    else
                    {
                        numRepeat = StringGenerator.rndGen.Next(0, MinRepeat);
                    }
                }
                else
                {
                    checked
                    {
                        int maxRepeat = (MaxRepeat == -1) ? MinRepeat + extraRepetitions : MaxRepeat;
                        int minRepeat = (MinRepeat == 0 && RefNode == ReservedPath) ? 1 : MinRepeat;
                        numRepeat = (minRepeat < maxRepeat) ? StringGenerator.rndGen.Next(minRepeat, maxRepeat + 1) : minRepeat;
                    }
                }
                string childStr;

                if (RefNode is StringTextNode)
                {
                    childStr = RefNode.Generate();
                    buffer.Append(childStr.Substring(0, childStr.Length - 1));
                    childStr = childStr[childStr.Length - 1].ToString();
                    SameValue = true;
                }
                else
                {
                    childStr = RefNode.Generate();
                }

                for (int i = 0; i < numRepeat; i++)
                    buffer.Append(SameValue ? childStr : RefNode.Generate());

                return buffer.ToString();
            }
        }

        private class StringOrNode : BaseNode
        {
            public List<BaseNode> Children = new List<BaseNode>();
            private BaseNode ReservedPath = null;

            public override void ReservePath(BaseNode child)
            {
                ReservedPath = child;
                base.ReservePath(child);
            }
            public override string Generate()
            {
                if (ReservedPath != null)
                {
                    return ReservedPath.Generate();
                }
                else
                {
                    return Children[StringGenerator.rndGen.Next(Children.Count)].Generate();
                }
            }
        }

        private class StringAndNode : BaseNode
        {
            public List<BaseNode> Children = new List<BaseNode>();

            public override string Generate()
            {
                StringBuilder buffer = new StringBuilder();

                foreach (BaseNode node in Children)
                    buffer.Append(node.Generate());

                return buffer.ToString();
            }
        }

        private class StringSubExprNode : BaseNode
        {
            BaseNode RefNode;
            public string Name; //Identifies subexpression by name, used for named backreferences

            public StringSubExprNode(BaseNode subExpr)
            {
                RefNode = subExpr;
                RefNode.ParentNode = this;
            }

            public override string Generate()
            {
                return RefNode.Generate();
            }
        }
        #endregion    
    }    
}
