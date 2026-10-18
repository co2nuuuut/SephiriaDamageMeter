using System.Collections.Generic;
using UnityEngine;

namespace SephiriaDamageMeter
{
    internal enum DamageStatsScope
    {
        Location,
        Run
    }

    internal struct DamageRowData
    {
        public string Id;
        public EDamageElementalType Element;
        public float Value;
        public float Percent;
    }

    internal sealed class PlayerBlockData
    {
        public string Name;
        public int Slot;
        public Sprite Icon;
        public float Total;
        public float Share;
        public bool IsLocal;
        public int HiddenSourceCount;
        public float HiddenTotal;
        public readonly List<DamageRowData> Sources = new List<DamageRowData>();

        public void Reset()
        {
            Name = "";
            Slot = 0;
            Icon = null;
            Total = 0f;
            Share = 0f;
            IsLocal = false;
            HiddenSourceCount = 0;
            HiddenTotal = 0f;
            Sources.Clear();
        }
    }

    internal sealed class DamageSnapshot
    {
        public readonly List<PlayerBlockData> Blocks = new List<PlayerBlockData>();
        public float Total;
        public int HiddenPlayerCount;
        public float HiddenPlayerTotal;
        public bool Party;
        public bool HasLocal;
        public bool LocalInBattle;

        public string LocalPlayerName;
        public int LocalSlot;
        public Sprite LocalPlayerIcon;

        public void Clear()
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                Blocks[i].Reset();
            }
            Blocks.Clear();
            Total = 0f;
            HiddenPlayerCount = 0;
            HiddenPlayerTotal = 0f;
            Party = false;
            HasLocal = false;
            LocalInBattle = false;
            LocalPlayerName = "";
            LocalSlot = 0;
            LocalPlayerIcon = null;
        }
    }
}
