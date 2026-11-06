using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>What a smart action does when pressed. The toast layer turns each one into a button.</summary>
    public enum SmartDo { Copy, Open, Code, Run, Lookup }

    /// <summary>A button a detector offers. Copy puts Value on the clipboard; Open opens Value as a link;
    /// Code opens Value ("file:line") in the editor; Run hands Value to a named app-side handler.</summary>
    public sealed class SmartAction
    {
        public string Label;
        public SmartDo Do;
        public string Value;
        public bool Primary;
        public string Tip;
        public SmartAction(string label, SmartDo how, string value, bool primary = false, string tip = null)
        { Label = label; Do = how; Value = value; Primary = primary; Tip = tip; }
    }

    /// <summary>A labelled value shown on the toast and copied when tapped (a conversion, a decoded field).</summary>
    public sealed class SmartValue
    {
        public string Label;
        public string Text;
        public SmartValue(string label, string text) { Label = label; Text = text; }
        public override string ToString() => Label + ": " + Text;
    }

    /// <summary>Everything one detector worked out about a copied text. Pure data, so the detectors run
    /// and are tested without Windows.</summary>
    public sealed class SmartFact
    {
        /// <summary>A stable id such as "unit", "phone" or "ext:ticket-keys/ticket".</summary>
        public string Kind;
        /// <summary>The toast heading, for example "You copied an amount".</summary>
        public string Title;
        /// <summary>One line under the heading, for example "5 kg · weight".</summary>
        public string Summary;
        public List<SmartValue> Values = new List<SmartValue>();
        public List<SmartAction> Actions = new List<SmartAction>();
        /// <summary>Keys, tokens, bank numbers: kept out of history, never looked up, never sent to extensions.</summary>
        public bool Sensitive;
        /// <summary>Extra facts other layers use: a lookup key, a family name, a parsed number.</summary>
        public Dictionary<string, string> Data = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The extension that produced this fact, or null for a built-in detector.</summary>
        public string FromExtension;

        public SmartFact(string kind, string title, string summary = null) { Kind = kind; Title = title; Summary = summary; }

        public SmartFact Value(string label, string text) { if (!string.IsNullOrEmpty(text)) Values.Add(new SmartValue(label, text)); return this; }
        public SmartFact Act(string label, SmartDo how, string value, bool primary = false, string tip = null)
        { Actions.Add(new SmartAction(label, how, value, primary, tip)); return this; }
        public SmartFact Set(string key, string value) { Data[key] = value; return this; }
        public string Get(string key) => Data.TryGetValue(key, out var v) ? v : null;

        /// <summary>The values as a plain list, one per line, for "Copy all".</summary>
        public string AllValuesText() => string.Join(Environment.NewLine, Values.Select(v => v.Text));
    }
}
