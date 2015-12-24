using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace FastColoredTextBoxNS
{
    public class SyntaxHighlighter : IDisposable
    {
        //styles
        protected static readonly Platform platformType = PlatformType.GetOperationSystemPlatform();
        public readonly Style BlueBoldStyle = new TextStyle(Brushes.Blue, null, FontStyle.Bold);
        public readonly Style BlueStyle = new TextStyle(Brushes.Blue, null, FontStyle.Regular);
        public readonly Style BoldStyle = new TextStyle(null, null, FontStyle.Bold | FontStyle.Underline);
        public readonly Style BrownStyle = new TextStyle(Brushes.Brown, null, FontStyle.Italic);
        public readonly Style GrayStyle = new TextStyle(Brushes.Gray, null, FontStyle.Regular);
        public readonly Style GreenStyle = new TextStyle(Brushes.Green, null, FontStyle.Italic);
        public readonly Style MagentaStyle = new TextStyle(Brushes.Magenta, null, FontStyle.Regular);
        public readonly Style MaroonStyle = new TextStyle(Brushes.Maroon, null, FontStyle.Regular);
        public readonly Style RedStyle = new TextStyle(Brushes.Red, null, FontStyle.Regular);
        public readonly Style BlackStyle = new TextStyle(Brushes.Black, null, FontStyle.Regular);
        //
        protected readonly Dictionary<string, SyntaxDescriptor> descByXMLfileNames =
            new Dictionary<string, SyntaxDescriptor>();


        protected Regex CCommentRegex1, CCommentRegex2, CCommentRegex3;
        protected Regex CKeywordRegex, CDirectiveRegex;
        protected Regex CNumberRegex;
        protected Regex CStringRegex;
        protected Regex CStructRegex;


        public static RegexOptions RegexCompiledOption
        {
            get
            {
                if (platformType == Platform.X86)
                    return RegexOptions.Compiled;
                else
                    return RegexOptions.None;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            foreach (SyntaxDescriptor desc in descByXMLfileNames.Values)
                desc.Dispose();
        }

        #endregion

        /// <summary>
        /// Highlights syntax for given language
        /// </summary>
        public virtual void HighlightSyntax(Language language, Range range)
        {
            switch (language)
            {
                case Language.C:
                    CSyntaxHighLight(range);
                    break;              
                default:
                    break;
            }
        }

        /// <summary>
        /// Highlights syntax for given XML description file
        /// </summary>
        public virtual void HighlightSyntax(string XMLdescriptionFile, Range range)
        {
            SyntaxDescriptor desc = null;
            if (!descByXMLfileNames.TryGetValue(XMLdescriptionFile, out desc))
            {
                var doc = new XmlDocument();
                string file = XMLdescriptionFile;
                if (!File.Exists(file))
                    file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(file));

                doc.LoadXml(File.ReadAllText(file));
                desc = ParseXmlDescription(doc);
                descByXMLfileNames[XMLdescriptionFile] = desc;
            }

            HighlightSyntax(desc, range);
        }

        public virtual void AutoIndentNeeded(object sender, AutoIndentEventArgs args)
        {
            var tb = (sender as FastColoredTextBox);
            Language language = tb.Language;
            switch (language)
            {
                case Language.C:
                    CAutoIndentNeeded(sender, args);
                    break;
                
                default:
                    break;
            }
        }

        protected void CAutoIndentNeeded(object sender, AutoIndentEventArgs args)
        {
            //block {}
            if (Regex.IsMatch(args.LineText, @"^[^""']*\{.*\}[^""']*$"))
                return;
            //start of block {}
            if (Regex.IsMatch(args.LineText, @"^[^""']*\{"))
            {
                args.ShiftNextLines = args.TabLength;
                return;
            }
            //end of block {}
            if (Regex.IsMatch(args.LineText, @"}[^""']*$"))
            {
                args.Shift = -args.TabLength;
                args.ShiftNextLines = -args.TabLength;
                return;
            }
            //label
            if (Regex.IsMatch(args.LineText, @"^\s*\w+\s*:\s*($|//)") &&
                !Regex.IsMatch(args.LineText, @"^\s*default\s*:"))
            {
                args.Shift = -args.TabLength;
                return;
            }
            //some statements: case, default
            if (Regex.IsMatch(args.LineText, @"^\s*(case|default)\b.*:\s*($|//)"))
            {
                args.Shift = -args.TabLength / 2;
                return;
            }
            //is unclosed operator in previous line ?
            if (Regex.IsMatch(args.PrevLineText, @"^\s*(if|for|foreach|while|[\}\s]*else)\b[^{]*$"))
                if (!Regex.IsMatch(args.PrevLineText, @"(;\s*$)|(;\s*//)")) //operator is unclosed
                {
                    args.Shift = args.TabLength;
                    return;
                }
        }

        public static SyntaxDescriptor ParseXmlDescription(XmlDocument doc)
        {
            var desc = new SyntaxDescriptor();
            XmlNode brackets = doc.SelectSingleNode("doc/brackets");
            if (brackets != null)
            {
                if (brackets.Attributes["left"] == null || brackets.Attributes["right"] == null ||
                    brackets.Attributes["left"].Value == "" || brackets.Attributes["right"].Value == "")
                {
                    desc.leftBracket = '\x0';
                    desc.rightBracket = '\x0';
                }
                else
                {
                    desc.leftBracket = brackets.Attributes["left"].Value[0];
                    desc.rightBracket = brackets.Attributes["right"].Value[0];
                }

                if (brackets.Attributes["left2"] == null || brackets.Attributes["right2"] == null ||
                    brackets.Attributes["left2"].Value == "" || brackets.Attributes["right2"].Value == "")
                {
                    desc.leftBracket2 = '\x0';
                    desc.rightBracket2 = '\x0';
                }
                else
                {
                    desc.leftBracket2 = brackets.Attributes["left2"].Value[0];
                    desc.rightBracket2 = brackets.Attributes["right2"].Value[0];
                }

                if (brackets.Attributes["strategy"] == null || brackets.Attributes["strategy"].Value == "")
                    desc.bracketsHighlightStrategy = BracketsHighlightStrategy.Strategy2;
                else
                    desc.bracketsHighlightStrategy = (BracketsHighlightStrategy)Enum.Parse(typeof(BracketsHighlightStrategy), brackets.Attributes["strategy"].Value);
            }

            var styleByName = new Dictionary<string, Style>();

            foreach (XmlNode style in doc.SelectNodes("doc/style"))
            {
                Style s = ParseStyle(style);
                styleByName[style.Attributes["name"].Value] = s;
                desc.styles.Add(s);
            }
            foreach (XmlNode rule in doc.SelectNodes("doc/rule"))
                desc.rules.Add(ParseRule(rule, styleByName));
            foreach (XmlNode folding in doc.SelectNodes("doc/folding"))
                desc.foldings.Add(ParseFolding(folding));

            return desc;
        }

        protected static FoldingDesc ParseFolding(XmlNode foldingNode)
        {
            var folding = new FoldingDesc();
            //regex
            folding.startMarkerRegex = foldingNode.Attributes["start"].Value;
            folding.finishMarkerRegex = foldingNode.Attributes["finish"].Value;
            //options
            XmlAttribute optionsA = foldingNode.Attributes["options"];
            if (optionsA != null)
                folding.options = (RegexOptions)Enum.Parse(typeof(RegexOptions), optionsA.Value);

            return folding;
        }

        protected static RuleDesc ParseRule(XmlNode ruleNode, Dictionary<string, Style> styles)
        {
            var rule = new RuleDesc();
            rule.pattern = ruleNode.InnerText;
            //
            XmlAttribute styleA = ruleNode.Attributes["style"];
            XmlAttribute optionsA = ruleNode.Attributes["options"];
            //Style
            if (styleA == null)
                throw new Exception("Rule must contain style name.");
            if (!styles.ContainsKey(styleA.Value))
                throw new Exception("Style '" + styleA.Value + "' is not found.");
            rule.style = styles[styleA.Value];
            //options
            if (optionsA != null)
                rule.options = (RegexOptions)Enum.Parse(typeof(RegexOptions), optionsA.Value);

            return rule;
        }

        protected static Style ParseStyle(XmlNode styleNode)
        {
            XmlAttribute typeA = styleNode.Attributes["type"];
            XmlAttribute colorA = styleNode.Attributes["color"];
            XmlAttribute backColorA = styleNode.Attributes["backColor"];
            XmlAttribute fontStyleA = styleNode.Attributes["fontStyle"];
            XmlAttribute nameA = styleNode.Attributes["name"];
            //colors
            SolidBrush foreBrush = null;
            if (colorA != null)
                foreBrush = new SolidBrush(ParseColor(colorA.Value));
            SolidBrush backBrush = null;
            if (backColorA != null)
                backBrush = new SolidBrush(ParseColor(backColorA.Value));
            //fontStyle
            FontStyle fontStyle = FontStyle.Regular;
            if (fontStyleA != null)
                fontStyle = (FontStyle)Enum.Parse(typeof(FontStyle), fontStyleA.Value);

            return new TextStyle(foreBrush, backBrush, fontStyle);
        }

        protected static Color ParseColor(string s)
        {
            if (s.StartsWith("#"))
            {
                if (s.Length <= 7)
                    return Color.FromArgb(255,
                                          Color.FromArgb(Int32.Parse(s.Substring(1), NumberStyles.AllowHexSpecifier)));
                else
                    return Color.FromArgb(Int32.Parse(s.Substring(1), NumberStyles.AllowHexSpecifier));
            }
            else
                return Color.FromName(s);
        }

        public void HighlightSyntax(SyntaxDescriptor desc, Range range)
        {
            //set style order
            range.tb.ClearStylesBuffer();
            for (int i = 0; i < desc.styles.Count; i++)
                range.tb.Styles[i] = desc.styles[i];
            //brackets
            char[] oldBrackets = RememberBrackets(range.tb);
            range.tb.LeftBracket = desc.leftBracket;
            range.tb.RightBracket = desc.rightBracket;
            range.tb.LeftBracket2 = desc.leftBracket2;
            range.tb.RightBracket2 = desc.rightBracket2;
            //clear styles of range
            range.ClearStyle(desc.styles.ToArray());
            //highlight syntax
            foreach (RuleDesc rule in desc.rules)
                range.SetStyle(rule.style, rule.Regex);
            //clear folding
            range.ClearFoldingMarkers();
            //folding markers
            foreach (FoldingDesc folding in desc.foldings)
                range.SetFoldingMarkers(folding.startMarkerRegex, folding.finishMarkerRegex, folding.options);

            //
            RestoreBrackets(range.tb, oldBrackets);
        }

        protected void RestoreBrackets(FastColoredTextBox tb, char[] oldBrackets)
        {
            tb.LeftBracket = oldBrackets[0];
            tb.RightBracket = oldBrackets[1];
            tb.LeftBracket2 = oldBrackets[2];
            tb.RightBracket2 = oldBrackets[3];
        }

        protected char[] RememberBrackets(FastColoredTextBox tb)
        {
            return new[] { tb.LeftBracket, tb.RightBracket, tb.LeftBracket2, tb.RightBracket2 };
        }

       

        public void InitStyleSchema(Language lang)
        {
            switch (lang)
            {
                case Language.C:
                    StringStyle = BrownStyle;
                    CommentStyle = GreenStyle;
                    NumberStyle = MagentaStyle;
                    AttributeStyle = GreenStyle;
                    ClassNameStyle = BoldStyle;
                    KeywordStyle = BlueStyle;
                    CommentTagStyle = GrayStyle;
                    break;
            }
        }
        protected void InitCRegex()
        {
            CStringRegex =
               new Regex(
                   @"
                            # Character definitions:
                            '
                            (?> # disable backtracking
                              (?:
                                \\[^\r\n]|    # escaped meta char
                                [^'\r\n]      # any character except '
                              )*
                            )
                            '?
                            |
                            # Normal string & verbatim strings definitions:
                            (?<verbatimIdentifier>@)?         # this group matches if it is an verbatim string
                            ""
                            (?> # disable backtracking
                              (?:
                                # match and consume an escaped character including escaped double quote ("") char
                                (?(verbatimIdentifier)        # if it is a verbatim string ...
                                  """"|                         #   then: only match an escaped double quote ("") char
                                  \\.                         #   else: match an escaped sequence
                                )
                                | # OR
            
                                # match any char except double quote char ("")
                                [^""]
                              )*
                            )
                            ""
                        ",
                   RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace |
                   RegexCompiledOption
                   );
            CCommentRegex1 = new Regex(@"//.*$", RegexOptions.Multiline | RegexCompiledOption);
            CDirectiveRegex = new Regex(@"#.*$", RegexOptions.Multiline | RegexCompiledOption);
            CCommentRegex2 = new Regex(@"(/\*.*?\*/)|(/\*.*)", RegexOptions.Singleline | RegexCompiledOption);
            CCommentRegex3 = new Regex(@"(/\*.*?\*/)|(.*\*/)",
                                            RegexOptions.Singleline | RegexOptions.RightToLeft | RegexCompiledOption);
            CNumberRegex = new Regex(@"\b\d+[\.]?\d*([eE]\-?\d+)?[lLdDfF]?\b|\b0x[a-fA-F\d]+\b",
                                          RegexCompiledOption);
            CStructRegex = new Regex(@"\b(struct|enum)\s+(?<range>\w+?)\b", RegexCompiledOption);
            CKeywordRegex = new Regex(@"\b(auto|double|int|break|else|long|switch|case|enum|register|typedef|char|extern|return|union|const|float|short|unsigned|continue|for|signed|void|default|GOTO|sizeof|volatile|do|if|static|while|__asm|dllimport2|__int8|naked2|__based|__except|__int16|__stdcall|__cdecl|__fastcall|__int32|thread2|__declspec|__finally|__int64|__try|dllexport2|__inline|__leave)");
        }


        public virtual void CSyntaxHighLight(Range range)
        {
            range.tb.CommentPrefix = "//";
            range.tb.LeftBracket = '(';
            range.tb.RightBracket = ')';
            range.tb.LeftBracket2 = '{';
            range.tb.RightBracket2 = '}';
            range.tb.BracketsHighlightStrategy = BracketsHighlightStrategy.Strategy2;

            range.tb.AutoIndentCharsPatterns
                = @"
^\s*[\w\.]+(\s\w+)?\s*(?<range>=)\s*(?<range>[^;]+);
^\s*(case|default)\s*[^:]*(?<range>:)\s*(?<range>[^;]+);
";
            //clear style of changed range
            range.ClearStyle(StringStyle, CommentStyle, NumberStyle, AttributeStyle, ClassNameStyle, KeywordStyle);
            //
            if (CStringRegex == null)
                InitCRegex();
            //string highlighting
            range.SetStyle(StringStyle, CStringRegex);
            //comment highlighting
            range.SetStyle(CommentStyle, CCommentRegex1);
            range.SetStyle(CommentStyle, CCommentRegex2);
            range.SetStyle(CommentStyle, CCommentRegex3);
            //number highlighting
            range.SetStyle(NumberStyle, CNumberRegex);
            //directive highlighting
            range.SetStyle(StringStyle, CDirectiveRegex);
            //class name highlighting
            range.SetStyle(ClassNameStyle, CStructRegex);
            //keyword highlighting
            range.SetStyle(KeywordStyle, CKeywordRegex);

            //find document comments
            foreach (Range r in range.GetRanges(@"^\s*///.*$", RegexOptions.Multiline))
            {
                //remove C# highlighting from this fragment
                r.ClearStyle(StyleIndex.All);
                r.SetStyle(CommentStyle);
                //tags
                //prefix '///'
                foreach (Range rr in r.GetRanges(@"^\s*///", RegexOptions.Multiline))
                {
                    rr.ClearStyle(StyleIndex.All);
                    rr.SetStyle(CommentTagStyle);
                }
            }

            //clear folding markers
            range.ClearFoldingMarkers();
            //set folding markers
            range.SetFoldingMarkers("{", "}"); //allow to collapse brackets block
            range.SetFoldingMarkers(@"#region\b", @"#endregion\b"); //allow to collapse #region blocks
            range.SetFoldingMarkers(@"/\*", @"\*/"); //allow to collapse comment block
        }

      

        #region Styles

        /// <summary>
        /// String style
        /// </summary>
        public Style StringStyle { get; set; }

        /// <summary>
        /// Comment style
        /// </summary>
        public Style CommentStyle { get; set; }

        /// <summary>
        /// Number style
        /// </summary>
        public Style NumberStyle { get; set; }

        /// <summary>
        /// C# attribute style
        /// </summary>
        public Style AttributeStyle { get; set; }

        /// <summary>
        /// Class name style
        /// </summary>
        public Style ClassNameStyle { get; set; }

        /// <summary>
        /// Keyword style
        /// </summary>
        public Style KeywordStyle { get; set; }

        /// <summary>
        /// Style of tags in comments of C#
        /// </summary>
        public Style CommentTagStyle { get; set; }

        /// <summary>
        /// HTML attribute value style
        /// </summary>
        public Style AttributeValueStyle { get; set; }

        /// <summary>
        /// HTML tag brackets style
        /// </summary>
        public Style TagBracketStyle { get; set; }

        /// <summary>
        /// HTML tag name style
        /// </summary>
        public Style TagNameStyle { get; set; }

        /// <summary>
        /// HTML Entity style
        /// </summary>
        public Style HtmlEntityStyle { get; set; }

        /// <summary>
        /// XML attribute style
        /// </summary>
        public Style XmlAttributeStyle { get; set; }

        /// <summary>
        /// XML attribute value style
        /// </summary>
        public Style XmlAttributeValueStyle { get; set; }

        /// <summary>
        /// XML tag brackets style
        /// </summary>
        public Style XmlTagBracketStyle { get; set; }

        /// <summary>
        /// XML tag name style
        /// </summary>
        public Style XmlTagNameStyle { get; set; }

        /// <summary>
        /// XML Entity style
        /// </summary>
        public Style XmlEntityStyle { get; set; }

        /// <summary>
        /// XML CData style
        /// </summary>
        public Style XmlCDataStyle { get; set; }

        /// <summary>
        /// Variable style
        /// </summary>
        public Style VariableStyle { get; set; }

        /// <summary>
        /// Specific PHP keyword style
        /// </summary>
        public Style KeywordStyle2 { get; set; }

        /// <summary>
        /// Specific PHP keyword style
        /// </summary>
        public Style KeywordStyle3 { get; set; }

        /// <summary>
        /// SQL Statements style
        /// </summary>
        public Style StatementsStyle { get; set; }

        /// <summary>
        /// SQL Functions style
        /// </summary>
        public Style FunctionsStyle { get; set; }

        /// <summary>
        /// SQL Types style
        /// </summary>
        public Style TypesStyle { get; set; }

        #endregion
    }

    /// <summary>
    /// Language
    /// </summary>
    public enum Language
    {
        Custom,
        C
    }
}