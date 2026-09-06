using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VisualNovelEngine
{
    public enum DiagnosticSeverity
    {
        Warning,
        Error
    }

    public class ParseDiagnostic
    {
        public int line;
        public DiagnosticSeverity severity;
        public string message;

        public override string ToString()
        {
            return $"linha {line}: {message}";
        }
    }

    public static class ScriptParser
    {
        // Grammar / conventions:
        // Lines starting with "label <name>" define a label.
        // Lines starting with "#" are comments.
        // Commands: @command param1=val param2="val with spaces"
        // Dialogue: "Nome: Texto" or "Texto" (narrador)
        // Menu: "@menu" then choices starting with "*" or "-": "* Ver o bosque -> label_bosque"
        // Params: key=value separated by spaces. Values may be quoted.
        // Inline params: dialogue accepts "{key=val}" anywhere in the text; menu choices
        // accept "{key=val}" or "(key=val)". The group is removed from the displayed text,
        // but only when it actually contains at least one key=value pair - so prose like
        // "(talvez)" or "{...}" survives untouched.

        static readonly Regex commandLineRegex = new Regex(@"^@(?<cmd>\w+)\s*(?<rest>.*)$", RegexOptions.Compiled);
        static readonly Regex labelRegex = new Regex(@"^label\s+([A-Za-z0-9_\-]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex labelLikeRegex = new Regex(@"^label(\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex dialogRegex = new Regex(@"^(?<speaker>[^:]+):\s*(?<text>.+)$", RegexOptions.Compiled);
        static readonly Regex paramRegex = new Regex(@"(?<k>\w+)\s*=\s*(""(?<vq>[^""]*)""|'(?<vs>[^']*)'|(?<vu>[^\s]+))", RegexOptions.Compiled);
        static readonly Regex lineSplitRegex = new Regex(@"\r\n|\r|\n", RegexOptions.Compiled);
        static readonly Regex choiceLineRegex = new Regex(@"^\s*[\*\-]", RegexOptions.Compiled);
        // Inline param groups. Duplicate group names are legal in .NET regex.
        static readonly Regex braceGroupRegex = new Regex(@"\{(?<inside>[^{}]*)\}", RegexOptions.Compiled);
        static readonly Regex braceOrParenGroupRegex = new Regex(@"\{(?<inside>[^{}]*)\}|\((?<inside>[^()]*)\)", RegexOptions.Compiled);
        static readonly Regex extraSpaceRegex = new Regex(@"[ \t]{2,}", RegexOptions.Compiled);

        // Must stay in sync with the switch in VisualNovelEngine.HandleCommandNode.
        public static readonly HashSet<string> KnownCommands = new HashSet<string>
        {
            "show", "hide", "play", "stop", "jump", "scene", "set", "flag", "if", "shake", "menu"
        };

        public static List<ScriptNode> Parse(string scriptText)
        {
            return Parse(scriptText, out _);
        }

        public static List<ScriptNode> Parse(string scriptText, out List<ParseDiagnostic> diagnostics)
        {
            diagnostics = new List<ParseDiagnostic>();
            var nodes = new List<ScriptNode>();

            if (string.IsNullOrWhiteSpace(scriptText))
            {
                Report(diagnostics, 0, DiagnosticSeverity.Error, "Script vazio.");
                return nodes;
            }

            var lines = lineSplitRegex.Split(scriptText);
            int ln = 0;
            while (ln < lines.Length)
            {
                string line = lines[ln].Trim();
                int lineNo = ln + 1; // 1-based, usado por todos os nós
                ln++;

                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#")) continue; // comment

                // label
                var lMatch = labelRegex.Match(line);
                if (lMatch.Success)
                {
                    nodes.Add(new LabelNode() { label = lMatch.Groups[1].Value, lineNumber = lineNo });
                    continue;
                }
                if (labelLikeRegex.IsMatch(line))
                {
                    Report(diagnostics, lineNo, DiagnosticSeverity.Error,
                        $"Label inválido: '{line}'. Use 'label <nome>' com letras, números, '_' ou '-' e sem espaços.");
                    continue;
                }

                // command
                var cMatch = commandLineRegex.Match(line);
                if (cMatch.Success)
                {
                    string cmd = cMatch.Groups["cmd"].Value.ToLower();
                    string rest = cMatch.Groups["rest"].Value.Trim();
                    var parameters = new Dictionary<string, string>();
                    ParseParams(rest, parameters);

                    // special: menu block
                    if (cmd == "menu")
                    {
                        var menuNode = new MenuNode() { lineNumber = lineNo };
                        foreach (var kv in parameters) menuNode.parameters[kv.Key] = kv.Value;

                        // read following lines until blank or non-choice
                        while (ln < lines.Length)
                        {
                            string nextRaw = lines[ln];
                            if (string.IsNullOrWhiteSpace(nextRaw)) { ln++; break; }
                            if (!choiceLineRegex.IsMatch(nextRaw)) break; // not a choice

                            int choiceLineNo = ln + 1;
                            ln++;

                            // pattern: * Texto (key=val) -> labelName
                            string choiceText = nextRaw.Trim().Substring(1).Trim();
                            string jumpTo = null;
                            var arrowIdx = choiceText.IndexOf("->");
                            if (arrowIdx >= 0)
                            {
                                jumpTo = choiceText.Substring(arrowIdx + 2).Trim();
                                choiceText = choiceText.Substring(0, arrowIdx).Trim();
                            }

                            var choice = new MenuChoice() { lineNumber = choiceLineNo };

                            // inline params may sit on either side of the arrow
                            ExtractInlineParams(choiceText, choice.parameters, out string cleanedChoice, braceOrParenGroupRegex);
                            choice.text = cleanedChoice;

                            if (jumpTo != null)
                            {
                                ExtractInlineParams(jumpTo, choice.parameters, out string cleanedJump, braceOrParenGroupRegex);
                                jumpTo = cleanedJump;
                            }
                            choice.jumpTo = jumpTo ?? "";

                            menuNode.choices.Add(choice);
                        }

                        nodes.Add(menuNode);
                        continue;
                    }

                    var cmdNode = new CommandNode() { command = cmd, raw = rest, lineNumber = lineNo };
                    foreach (var kv in parameters) cmdNode.parameters[kv.Key] = kv.Value;
                    nodes.Add(cmdNode);
                    continue;
                }
                if (line.StartsWith("@"))
                {
                    Report(diagnostics, lineNo, DiagnosticSeverity.Error,
                        $"Comando malformado: '{line}'. Use '@comando param=valor' sem espaço depois do '@'.");
                    continue;
                }

                // dialogue
                var dMatch = dialogRegex.Match(line);
                if (dMatch.Success)
                {
                    var node = new DialogueNode()
                    {
                        speaker = dMatch.Groups["speaker"].Value.Trim(),
                        text = dMatch.Groups["text"].Value.Trim(),
                        lineNumber = lineNo
                    };
                    ExtractInlineParams(node.text, node.parameters, out string cleanedText, braceGroupRegex);
                    node.text = cleanedText;
                    nodes.Add(node);
                }
                else
                {
                    // treat as narration/dialog without speaker
                    var node = new DialogueNode() { speaker = null, text = line, lineNumber = lineNo };
                    ExtractInlineParams(node.text, node.parameters, out string cleanedText, braceGroupRegex);
                    node.text = cleanedText;
                    nodes.Add(node);
                }
            }

            Validate(nodes, diagnostics);
            return nodes;
        }

        static void ParseParams(string source, Dictionary<string, string> dict)
        {
            foreach (Match pm in paramRegex.Matches(source))
            {
                dict[pm.Groups["k"].Value] = ValueOf(pm);
            }
        }

        static string ValueOf(Match pm)
        {
            return pm.Groups["vq"].Success ? pm.Groups["vq"].Value :
                   pm.Groups["vs"].Success ? pm.Groups["vs"].Value :
                   pm.Groups["vu"].Value;
        }

        /// <summary>
        /// Extrai grupos de parâmetros inline (ex.: "{speed=30}") de qualquer posição do texto
        /// e os remove do texto exibido. Um grupo só é removido se contiver ao menos um
        /// par key=value, para não apagar texto legítimo do roteiro.
        /// </summary>
        static void ExtractInlineParams(string text, Dictionary<string, string> dict, out string cleaned, Regex groupRegex)
        {
            cleaned = text;
            if (string.IsNullOrEmpty(text)) return;

            bool removedAny = false;
            cleaned = groupRegex.Replace(text, m =>
            {
                string inside = m.Groups["inside"].Value;
                var found = paramRegex.Matches(inside);
                if (found.Count == 0) return m.Value; // não é grupo de parâmetros: preserva

                foreach (Match pm in found)
                {
                    dict[pm.Groups["k"].Value] = ValueOf(pm);
                }
                removedAny = true;
                return string.Empty;
            });

            if (removedAny)
            {
                cleaned = extraSpaceRegex.Replace(cleaned, " ").Trim();
            }
        }

        #region Validation
        static void Report(List<ParseDiagnostic> diagnostics, int line, DiagnosticSeverity severity, string message)
        {
            diagnostics.Add(new ParseDiagnostic() { line = line, severity = severity, message = message });
        }

        static void Validate(List<ScriptNode> nodes, List<ParseDiagnostic> diagnostics)
        {
            // 1ª passada: labels definidos (e duplicados)
            var labelLines = new Dictionary<string, int>();
            foreach (var node in nodes)
            {
                if (node is LabelNode lbl)
                {
                    if (labelLines.TryGetValue(lbl.label, out int firstLine))
                    {
                        Report(diagnostics, lbl.lineNumber, DiagnosticSeverity.Error,
                            $"Label duplicado '{lbl.label}' (já definido na linha {firstLine}). Só o último é alcançável; o bloco anterior vira código morto.");
                    }
                    else
                    {
                        labelLines[lbl.label] = lbl.lineNumber;
                    }
                }
            }

            // 2ª passada: comandos e menus
            foreach (var node in nodes)
            {
                if (node is CommandNode cn) ValidateCommand(cn, labelLines, diagnostics);
                else if (node is MenuNode mn) ValidateMenu(mn, labelLines, diagnostics);
            }
        }

        static void CheckLabelExists(string label, int line, string context, Dictionary<string, int> labelLines, List<ParseDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(label)) return;
            if (!labelLines.ContainsKey(label))
            {
                Report(diagnostics, line, DiagnosticSeverity.Error, $"{context}: label '{label}' não existe no script.");
            }
        }

        static void ValidateCommand(CommandNode cn, Dictionary<string, int> labelLines, List<ParseDiagnostic> diagnostics)
        {
            if (!KnownCommands.Contains(cn.command))
            {
                Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error,
                    $"Comando não reconhecido: '@{cn.command}'.");
                return;
            }

            cn.parameters.TryGetValue("target", out string target);
            cn.parameters.TryGetValue("name", out string name);
            cn.parameters.TryGetValue("type", out string type);

            switch (cn.command)
            {
                case "show":
                    if (target == null && name == null)
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, "@show precisa de 'target=' ou 'name='.");
                    }
                    else if (target != null && name == null && !IsTarget(target, "textbox"))
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, $"@show target={target} precisa do parâmetro 'name='.");
                    }
                    else if (target != null && !IsTarget(target, "bg", "background", "sprite", "char", "textbox"))
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Warning,
                            $"@show target='{target}' não é reconhecido (esperado: bg, background, sprite, char ou textbox).");
                    }
                    break;

                case "hide":
                    if (target == null && name == null)
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, "@hide precisa de 'target=' ou 'name='.");
                    }
                    else if (IsTarget(target, "sprite", "char") && name == null)
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, $"@hide target={target} precisa do parâmetro 'name='.");
                    }
                    else if (target != null && !IsTarget(target, "bg", "background", "sprite", "char", "textbox"))
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Warning,
                            $"@hide target='{target}' não é reconhecido (esperado: bg, background, sprite, char ou textbox).");
                    }
                    break;

                case "play":
                    if (name == null)
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, "@play precisa do parâmetro 'name='.");
                    }
                    if (type != null && type != "bgm" && type != "sfx")
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Warning,
                            $"@play type='{type}' não é reconhecido (esperado: bgm ou sfx). Será tratado como som avulso.");
                    }
                    break;

                case "stop":
                    if (type != null && type != "bgm" && type != "sfx")
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Warning,
                            $"@stop type='{type}' não é reconhecido (esperado: bgm ou sfx). Tudo será parado.");
                    }
                    break;

                case "jump":
                    {
                        string to = ResolveJumpTarget(cn);
                        if (string.IsNullOrEmpty(to))
                        {
                            Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, "@jump precisa de 'to=<label>'.");
                        }
                        else
                        {
                            CheckLabelExists(to, cn.lineNumber, "@jump", labelLines, diagnostics);
                        }
                        break;
                    }

                case "scene":
                    if (name == null)
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, "@scene precisa do parâmetro 'name='.");
                    }
                    break;

                case "set":
                    if (cn.parameters.Count == 0)
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Warning, "@set sem parâmetros não faz nada.");
                    }
                    break;

                case "flag":
                    if (!cn.parameters.ContainsKey("add") && !cn.parameters.ContainsKey("remove"))
                    {
                        Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error, "@flag precisa de 'add=' ou 'remove='.");
                    }
                    break;

                case "if":
                    {
                        bool hasKey = cn.parameters.ContainsKey("key");
                        bool hasValue = cn.parameters.ContainsKey("value");
                        cn.parameters.TryGetValue("goto", out string gotoLabel);
                        if (!hasKey || !hasValue || string.IsNullOrEmpty(gotoLabel))
                        {
                            Report(diagnostics, cn.lineNumber, DiagnosticSeverity.Error,
                                "@if precisa de 'key=', 'value=' e 'goto=<label>'.");
                        }
                        else
                        {
                            CheckLabelExists(gotoLabel, cn.lineNumber, "@if goto", labelLines, diagnostics);
                        }
                        break;
                    }
            }
        }

        static void ValidateMenu(MenuNode mn, Dictionary<string, int> labelLines, List<ParseDiagnostic> diagnostics)
        {
            if (mn.choices.Count == 0)
            {
                Report(diagnostics, mn.lineNumber, DiagnosticSeverity.Error,
                    "@menu sem escolhas. As escolhas devem vir nas linhas seguintes, começando com '*' ou '-', sem linha em branco entre elas.");
                return;
            }

            foreach (var choice in mn.choices)
            {
                if (string.IsNullOrWhiteSpace(choice.text))
                {
                    Report(diagnostics, choice.lineNumber, DiagnosticSeverity.Warning, "Escolha de menu sem texto: o botão ficará em branco.");
                }
                CheckLabelExists(choice.jumpTo, choice.lineNumber, "Escolha de menu", labelLines, diagnostics);
            }
        }

        static string ResolveJumpTarget(CommandNode cn)
        {
            if (cn.parameters.TryGetValue("to", out string to)) return to;
            // permite '@jump labelname'
            if (!string.IsNullOrEmpty(cn.raw) && cn.raw.IndexOf('=') < 0) return cn.raw.Trim();
            return null;
        }

        static bool IsTarget(string target, params string[] expected)
        {
            if (target == null) return false;
            foreach (var e in expected)
            {
                if (target.Equals(e, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        #endregion
    }
}
