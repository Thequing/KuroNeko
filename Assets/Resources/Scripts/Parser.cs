using System.Collections.Generic;

namespace VisualNovelEngine 
{ 

    #region Parser
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    public static class ScriptParser
    {
        // Grammar / conventions:
        // Lines starting with "label <name>" define a label.
        // Lines starting with "#" are comments.
        // Commands start with "@" or a bare word with ":"? We'll use @command param1=val param2="val with spaces"
        // Dialogue: "Nome: Texto" or "Texto" (narrador)
        // Menu: "@menu" then indented choices starting with "*" or "-": "* Ver o bosque -> label_bosque"
        // Params: key=value separated by spaces. Values may be quoted.

        static Regex commandLineRegex = new Regex(@"^@(?<cmd>\w+)\s*(?<rest>.*)$", RegexOptions.Compiled);
        static Regex labelRegex = new Regex(@"^label\s+([A-Za-z0-9_\-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex dialogRegex = new Regex(@"^(?<speaker>[^:]+):\s*(?<text>.+)$", RegexOptions.Compiled);
        static Regex paramRegex = new Regex(@"(?<k>\w+)\s*=\s*(""(?<vq>[^""]*)""|'(?<vs>[^']*)'|(?<vu>[^\s]+))", RegexOptions.Compiled);

        public static List<ScriptNode> Parse(string scriptText)
        {
            var nodes = new List<ScriptNode>();
            var lines = Regex.Split(scriptText, "\r\n|\r|\n");
            int ln = 0;
            while (ln < lines.Length)
            {
                string raw = lines[ln].TrimEnd();
                string line = raw.Trim();
                ln++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#")) continue; // comment

                // label
                var lMatch = labelRegex.Match(line);
                if (lMatch.Success)
                {
                    var lbl = new LabelNode() { label = lMatch.Groups[1].Value, lineNumber = ln };
                    nodes.Add(lbl);
                    continue;
                }

                // command
                var cMatch = commandLineRegex.Match(line);
                if (cMatch.Success)
                {
                    string cmd = cMatch.Groups["cmd"].Value.ToLower();
                    string rest = cMatch.Groups["rest"].Value.Trim();
                    var cmdNode = new CommandNode() { command = cmd, raw = rest, lineNumber = ln };

                    // parse params key=val
                    foreach (Match pm in paramRegex.Matches(rest))
                    {
                        string key = pm.Groups["k"].Value;
                        string val = pm.Groups["vq"].Success ? pm.Groups["vq"].Value :
                                     pm.Groups["vs"].Success ? pm.Groups["vs"].Value :
                                     pm.Groups["vu"].Value;
                        cmdNode.parameters[key] = val;
                    }

                    // special: menu block
                    if (cmd == "menu")
                    {
                        var menuNode = new MenuNode() { lineNumber = ln - 1 };
                        // read following lines until blank or non-choice
                        while (ln < lines.Length)
                        {
                            string nextRaw = lines[ln];
                            if (string.IsNullOrWhiteSpace(nextRaw)) { ln++; break; }
                            if (!Regex.IsMatch(nextRaw, @"^\s*[\*\-]")) break; // not a choice
                            var choiceLine = nextRaw.Trim();
                            // pattern: * Texto -> labelName (optional)
                            string choiceText = choiceLine.Substring(1).Trim();
                            string jumpTo = null;
                            var arrowIdx = choiceText.IndexOf("->");
                            if (arrowIdx >= 0)
                            {
                                jumpTo = choiceText.Substring(arrowIdx + 2).Trim();
                                choiceText = choiceText.Substring(0, arrowIdx).Trim();
                            }
                            // parse inline params in parentheses: e.g. "Vá embora (flag=left)"
                            var choice = new MenuChoice() { text = choiceText, jumpTo = jumpTo ?? "" };
                            // simple param extraction from choice text (key=val)
                            foreach (Match pm in paramRegex.Matches(choiceText))
                            {
                                string key = pm.Groups["k"].Value;
                                string val = pm.Groups["vq"].Success ? pm.Groups["vq"].Value :
                                             pm.Groups["vs"].Success ? pm.Groups["vs"].Value :
                                             pm.Groups["vu"].Value;
                                choice.parameters[key] = val;
                            }

                            menuNode.choices.Add(choice);
                            ln++;
                        }
                        nodes.Add(menuNode);
                        continue;
                    }

                    nodes.Add(cmdNode);
                    continue;
                }

                // dialogue
                var dMatch = dialogRegex.Match(line);
                if (dMatch.Success)
                {
                    var node = new DialogueNode() { speaker = dMatch.Groups["speaker"].Value.Trim(), text = dMatch.Groups["text"].Value.Trim(), lineNumber = ln };
                    // inline params at end of text in {key=val, key2=val2}
                    ExtractInlineParams(node.text, node.parameters, out string cleanedText);
                    node.text = cleanedText;
                    nodes.Add(node);
                    continue;
                }
                else
                {
                    // treat as narration/dialog without speaker
                    var node = new DialogueNode() { speaker = null, text = line, lineNumber = ln };
                    ExtractInlineParams(node.text, node.parameters, out string cleanedText);
                    node.text = cleanedText;
                    node.text = cleanedText;
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        static void ExtractInlineParams(string text, Dictionary<string, string> dict, out string cleaned)
        {
            cleaned = text;
            // look for trailing { ... }
            var m = Regex.Match(text, @"\{(?<inside>.*)\}\s*$");
            if (m.Success)
            {
                string inside = m.Groups["inside"].Value;
                foreach (Match pm in paramRegex.Matches(inside))
                {
                    string key = pm.Groups["k"].Value;
                    string val = pm.Groups["vq"].Success ? pm.Groups["vq"].Value :
                                 pm.Groups["vs"].Success ? pm.Groups["vs"].Value :
                                 pm.Groups["vu"].Value;
                    dict[key] = val;
                }
                cleaned = text.Substring(0, m.Index).TrimEnd();
            }
        }
    }
    #endregion
}