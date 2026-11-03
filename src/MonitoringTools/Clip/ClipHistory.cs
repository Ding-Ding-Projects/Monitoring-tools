using System;
using System.Collections.ObjectModel;
using MonitoringTools.Core;

namespace MonitoringTools.Clip
{
    /// <summary>In-memory history only; nothing copied is ever written to disk.</summary>
    public sealed class ClipHistory
    {
        public ObservableCollection<ClipItem> Items { get; } = new ObservableCollection<ClipItem>();
        public int Today { get; private set; }
        public int TextCount { get; private set; }
        public int LinkCount { get; private set; }
        public int FileCount { get; private set; }
        public int MediaCount { get; private set; }
        private DateTime _day = DateTime.Today;

        public event Action Updated;

        public void Add(ClipItem item)
        {
            if (DateTime.Today != _day) { _day = DateTime.Today; Today = TextCount = LinkCount = FileCount = MediaCount = 0; }
            Today++;
            switch (item.Kind)
            {
                case ClipKind.Url: case ClipKind.Email: LinkCount++; break;
                case ClipKind.Files: case ClipKind.Command:
                    var k = item.FirstFileKind;
                    if (k == FileKind.Video || k == FileKind.Audio || k == FileKind.Image) MediaCount++; else FileCount++;
                    break;
                case ClipKind.Image: case ClipKind.Audio: MediaCount++; break;
                default: TextCount++; break;
            }
            Items.Insert(0, item);
            // Keep pixels only for the newest few images so history stays light on memory.
            for (int i = 5; i < Items.Count; i++) { Items[i].Image = null; Items[i].Audio = null; }
            while (Items.Count > Math.Max(5, Settings.Current.HistorySize)) Items.RemoveAt(Items.Count - 1);
            Updated?.Invoke();
        }
    }
}
