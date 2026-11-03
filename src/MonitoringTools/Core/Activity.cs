using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Core
{
    public enum ActivityKind { Convert, Push, Shortcut, Files, Network, Docker, Service }

    public sealed class ActivityEntry
    {
        public DateTime At = DateTime.Now;
        public ActivityKind Kind;
        public string Title;
        public string Detail;
        public bool Ok = true;
    }

    public sealed class Outage
    {
        public DateTime Start;
        public DateTime? End;
        public string Cause;
        public TimeSpan Length => (End ?? DateTime.Now) - Start;
    }

    /// <summary>In-memory activity feed and outage log for the control centre. Nothing here is written to disk.</summary>
    public static class Activity
    {
        private static readonly object Gate = new object();
        private static readonly List<ActivityEntry> Entries = new List<ActivityEntry>();
        private static readonly List<Outage> Outages = new List<Outage>();
        public static event Action Changed;

        public static void Add(ActivityKind kind, string title, string detail = null, bool ok = true)
        {
            lock (Gate)
            {
                Entries.Insert(0, new ActivityEntry { Kind = kind, Title = title, Detail = detail, Ok = ok });
                if (Entries.Count > 300) Entries.RemoveAt(Entries.Count - 1);
            }
            Log.Info($"[{kind}] {title}{(detail == null ? "" : " :: " + detail)}");
            Raise();
        }

        public static List<ActivityEntry> Recent(int n, ActivityKind? kind = null)
        {
            lock (Gate) return Entries.Where(e => kind == null || e.Kind == kind).Take(n).ToList();
        }

        public static void OutageStarted(string cause)
        {
            lock (Gate)
            {
                if (Outages.Count > 0 && Outages[0].End == null) { Outages[0].Cause = cause; return; }
                Outages.Insert(0, new Outage { Start = DateTime.Now, Cause = cause });
                if (Outages.Count > 50) Outages.RemoveAt(Outages.Count - 1);
            }
            Add(ActivityKind.Network, "Connection lost", cause, ok: false);
        }

        public static void OutageEnded()
        {
            Outage o = null;
            lock (Gate)
            {
                if (Outages.Count > 0 && Outages[0].End == null) { o = Outages[0]; o.End = DateTime.Now; }
            }
            if (o != null) Add(ActivityKind.Network, "Back online", $"down {(int)o.Length.TotalMinutes} m {o.Length.Seconds:00} s · {o.Cause}");
        }

        public static List<Outage> RecentOutages(int n)
        {
            lock (Gate) return Outages.Take(n).ToList();
        }

        /// <summary>Used by the screenshot mode to show a lived-in control centre.</summary>
        public static void Seed(IEnumerable<ActivityEntry> entries, IEnumerable<Outage> outages)
        {
            lock (Gate)
            {
                Entries.InsertRange(0, entries);
                Outages.InsertRange(0, outages);
            }
            Raise();
        }

        private static void Raise()
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() => Changed?.Invoke()));
        }
    }
}
