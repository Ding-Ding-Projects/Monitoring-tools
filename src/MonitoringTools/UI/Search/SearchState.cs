using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Search
{
    /// <summary>
    /// The state of one search field: query, plain or regex mode, case, regex options, the last
    /// replacement and test sample, and recent patterns. Each field has its own instance, keyed
    /// by a stable id, so a page that rebuilds keeps what the user typed and no two fields share it.
    /// Held in memory only; the library is the one thing saved to disk, and only on request.
    /// </summary>
    public sealed class SearchState
    {
        private static readonly Dictionary<string, SearchState> All = new Dictionary<string, SearchState>();

        public static SearchState For(string id)
        {
            if (!All.TryGetValue(id, out var s)) All[id] = s = new SearchState(id);
            return s;
        }

        public string Id { get; }
        public string Query { get; private set; } = "";
        public bool IsRegex { get; private set; }
        public bool MatchCase { get; private set; }
        public RegexOptions Options { get; private set; }
        public string Replacement { get; set; } = "";
        public string Sample { get; set; }
        public readonly List<string> Recent = new List<string>();

        public string Error { get; private set; }
        public bool TimedOut { get; set; }

        /// <summary>Raised after any change; the argument names the field that caused it, so a sender can skip its own echo.</summary>
        public event Action<object> Changed;

        private Regex _rx;

        private SearchState(string id) { Id = id; }

        public void SetQuery(string q, object source = null) { q ??= ""; if (q == Query) return; Query = q; Rebuild(); Changed?.Invoke(source); }
        public void SetRegex(bool on, object source = null) { if (on == IsRegex) return; IsRegex = on; Rebuild(); Changed?.Invoke(source); }
        public void SetMatchCase(bool on, object source = null) { if (on == MatchCase) return; MatchCase = on; Rebuild(); Changed?.Invoke(source); }

        public void SetOption(RegexOptions flag, bool on, object source = null)
        {
            var next = on ? Options | flag : Options & ~flag;
            if (next == Options) return;
            Options = next;
            Rebuild();
            Changed?.Invoke(source);
        }

        public void Load(SavedPattern p, object source = null)
        {
            Query = p.Pattern ?? "";
            Options = (RegexOptions)p.Options & ~RegexOptions.IgnoreCase;
            MatchCase = ((RegexOptions)p.Options & RegexOptions.IgnoreCase) == 0;
            Replacement = p.Replacement ?? "";
            IsRegex = true;
            Rebuild();
            Changed?.Invoke(source);
        }

        /// <summary>The full option set handed to the engine, including case.</summary>
        public RegexOptions EngineOptions => Options | (MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);

        public Regex Regex => IsRegex ? _rx : null;

        public bool IsActive => Query.Length > 0;

        public void Remember()
        {
            if (!IsRegex || Query.Length == 0 || Error != null) return;
            Recent.Remove(Query);
            Recent.Insert(0, Query);
            if (Recent.Count > 12) Recent.RemoveRange(12, Recent.Count - 12);
        }

        private void Rebuild()
        {
            Error = null; _rx = null; TimedOut = false;
            if (!IsRegex || Query.Length == 0) return;
            _rx = RegexKit.TryCreate(Query, EngineOptions, out var err);
            Error = err;
        }

        /// <summary>Whether a piece of text matches. Plain mode wants every word; an invalid pattern matches everything so nothing vanishes by mistake.</summary>
        public bool Matches(string text)
        {
            text ??= "";
            if (Query.Length == 0) return true;
            if (IsRegex)
            {
                if (_rx == null) return true;
                bool t = false;
                var hit = RegexKit.SafeIsMatch(_rx, text, ref t);
                if (t) TimedOut = true;
                return hit;
            }
            var cmp = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return Query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => text.Contains(w, cmp));
        }
    }
}
