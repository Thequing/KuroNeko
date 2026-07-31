using System.Collections.Generic;

namespace VisualNovelEngine
{
    #region Data Models
    // Base node
    using System.Collections.Generic;

    public abstract class ScriptNode
    {
        public int lineNumber;
    }

    public class DialogueNode : ScriptNode
    {
        public string speaker; // nome do personagem (pode ser null = narrador)
        public string text; // texto raw com tags
        public Dictionary<string, string> parameters = new Dictionary<string, string>(); // e.g. speed=30, expression=happy
    }

    public class CommandNode : ScriptNode
    {
        public string command; // e.g. show, hide, play, stop
        public Dictionary<string, string> parameters = new Dictionary<string, string>();
        public string raw; // raw remainder for debugging
    }

    public class LabelNode : ScriptNode
    {
        public string label;
    }

    public class MenuNode : ScriptNode
    {
        public List<MenuChoice> choices = new List<MenuChoice>();
    }

    public class MenuChoice
    {
        public string text;
        public string jumpTo; // label or null
        public Dictionary<string, string> parameters = new Dictionary<string, string>();
    }
    #endregion
}