using System.Collections.Generic;

namespace SephiriaDamageMeter
{
    internal sealed class SummarySourceData
    {
        public string Id;
        public EDamageElementalType Element;
        public float Value;
        public float Percent;
    }

    internal sealed class SummaryPlayerData
    {
        public string Name;
        public int Slot;
        public float Total;
        public float Dps;
        public float Share;
        public readonly List<SummarySourceData> Sources = new List<SummarySourceData>();

        public SummaryPlayerData Clone()
        {
            SummaryPlayerData copy = new SummaryPlayerData
            {
                Name = Name,
                Slot = Slot,
                Total = Total,
                Dps = Dps,
                Share = Share
            };
            for (int i = 0; i < Sources.Count; i++)
            {
                SummarySourceData source = Sources[i];
                copy.Sources.Add(new SummarySourceData
                {
                    Id = source.Id,
                    Element = source.Element,
                    Value = source.Value,
                    Percent = source.Percent
                });
            }
            return copy;
        }
    }

    internal sealed class SummarySnapshot
    {
        public readonly List<SummaryPlayerData> Players = new List<SummaryPlayerData>();
        public float TeamTotal;
        public bool FromCache;
        public float CapturedAt;

        public void Clear()
        {
            Players.Clear();
            TeamTotal = 0f;
            FromCache = false;
            CapturedAt = 0f;
        }

        public void CopyFrom(SummarySnapshot other)
        {
            Clear();
            if (other == null)
            {
                return;
            }
            TeamTotal = other.TeamTotal;
            FromCache = other.FromCache;
            CapturedAt = other.CapturedAt;
            for (int i = 0; i < other.Players.Count; i++)
            {
                Players.Add(other.Players[i].Clone());
            }
        }
    }
}
