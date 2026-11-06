using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>Automatic lookups: after a toast opens, asks one public service for the missing detail (today's rate,
    /// a book title, a package's newest version, who owns an address) and adds it to the toast. Sends only the
    /// smallest piece needed, never for sensitive copies, gives up after four seconds and remembers answers for a day.</summary>
    public static class SmartLookups
    {
        private static readonly HttpClient Http = MakeClient();
        private static readonly ConcurrentDictionary<string, (DateTime At, string Body)> Cache = new ConcurrentDictionary<string, (DateTime, string)>();

        private static HttpClient MakeClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/" + (typeof(SmartLookups).Assembly.GetName().Version?.ToString(3) ?? "1"));
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return c;
        }

        /// <summary>Which lookup a fact needs, and whether settings allow it. Null when nothing is sent.</summary>
        public static string UrlFor(SmartFact f)
        {
            var s = Settings.Current;
            if (f == null || f.Sensitive || !s.AutoLookups || f.FromExtension != null) return null;
            var key = f.Get("lookup");
            if (key == null) return null;
            switch (f.Kind)
            {
                case "money": return s.LookupMoney ? "https://api.frankfurter.app/latest?from=" + Uri.EscapeDataString(key) : null;
                case "isbn": return s.LookupBooksAndPackages ? "https://openlibrary.org/isbn/" + Uri.EscapeDataString(key) + ".json" : null;
                case "ip": return s.LookupIps ? "https://rdap.org/ip/" + Uri.EscapeDataString(key) : null;
                case "package":
                    if (!s.LookupBooksAndPackages) return null;
                    var eco = f.Get("ecosystem"); var name = f.Get("name");
                    return eco == "npm" ? "https://registry.npmjs.org/" + name.Replace("/", "%2F") + "/latest"
                         : eco == "nuget" ? "https://api.nuget.org/v3-flatcontainer/" + name.ToLowerInvariant() + "/index.json"
                         : "https://pypi.org/pypi/" + Uri.EscapeDataString(name) + "/json";
            }
            return null;
        }

        public static void Enrich(ClipItem item, SmartFact fact, SmartToast toast)
        {
            var url = UrlFor(fact);
            if (url == null) return;
            _ = Task.Run(async () =>
            {
                var body = await Get(url);
                if (body == null) return;
                var adds = Read(fact, body, out var summary);
                if (adds.Count == 0) return;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    bool first = true;
                    foreach (var v in adds) { fact.Values.Add(v); toast.Add(v, first ? summary : null); first = false; }
                });
            });
        }

        private static async Task<string> Get(string url)
        {
            if (Cache.TryGetValue(url, out var hit) && DateTime.UtcNow - hit.At < TimeSpan.FromDays(1)) return hit.Body;
            try
            {
                var r = await Http.GetAsync(url);
                if (!r.IsSuccessStatusCode) return null;
                var body = await r.Content.ReadAsStringAsync();
                if (body.Length > 2_000_000) return null;
                Cache[url] = (DateTime.UtcNow, body);
                return body;
            }
            catch (Exception ex) { Log.Info("Lookup skipped: " + ex.GetType().Name); return null; }
        }

        /// <summary>The values a lookup adds. Public for the self-test, which feeds it saved answers.</summary>
        public static List<SmartValue> Read(SmartFact f, string body, out string summary)
        {
            summary = null;
            var list = new List<SmartValue>();
            try
            {
                using var doc = JsonDocument.Parse(body);
                var r = doc.RootElement;
                switch (f.Kind)
                {
                    case "money":
                        if (!r.TryGetProperty("rates", out var rates)) break;
                        var dict = rates.EnumerateObject().Where(p => Everyday2.Currencies.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value.GetDouble());
                        var before = f.Values.Count;
                        var copy = new SmartFact(f.Kind, f.Title, f.Summary); foreach (var kv in f.Data) copy.Data[kv.Key] = kv.Value;
                        Everyday2.AddRates(copy, dict);
                        list.AddRange(copy.Values);
                        summary = copy.Summary + (r.TryGetProperty("date", out var d) ? " · rates of " + d.GetString() : "");
                        break;
                    case "isbn":
                        if (r.TryGetProperty("title", out var t)) { list.Add(new SmartValue("Title", t.GetString())); summary = t.GetString(); }
                        if (r.TryGetProperty("publish_date", out var pd)) list.Add(new SmartValue("Published", pd.GetString()));
                        if (r.TryGetProperty("publishers", out var pubs) && pubs.ValueKind == JsonValueKind.Array && pubs.GetArrayLength() > 0) list.Add(new SmartValue("Publisher", pubs[0].GetString()));
                        break;
                    case "ip":
                        var owner = r.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var country = r.TryGetProperty("country", out var c) ? c.GetString() : null;
                        if (owner != null) list.Add(new SmartValue("Network", owner));
                        if (country != null) list.Add(new SmartValue("Country", country));
                        if (owner != null) summary = f.Summary + " · " + owner + (country != null ? ", " + country : "");
                        break;
                    case "package":
                        string ver = null;
                        if (r.TryGetProperty("version", out var v)) ver = v.GetString();                                   // npm
                        else if (r.TryGetProperty("info", out var info) && info.TryGetProperty("version", out var pv)) ver = pv.GetString();   // PyPI
                        else if (r.TryGetProperty("versions", out var vs) && vs.ValueKind == JsonValueKind.Array)           // NuGet
                            ver = vs.EnumerateArray().Select(x => x.GetString()).LastOrDefault(x => x != null && !x.Contains('-'));
                        if (ver != null) { list.Add(new SmartValue("Newest version", ver)); summary = f.Get("name") + " · newest is " + ver; }
                        break;
                }
            }
            catch { }
            return list;
        }
    }
}
