// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReturnHome.Server.Managers;
using ReturnHome.Server.EntityObject.Player;
using ReturnHome.Server.EntityObject;

namespace ReturnHome.Server.Zone
{
    public class Zone
    {
        public int ZoneId { get; set; }
        public int World { get; set; }
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }
        public string Name { get; set; }

        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public static HashSet<string> zonesWithNpcsStarted = new HashSet<string>();

        public Zone(int world, int zoneId, Vector3 min, Vector3 max, string name)
        {            
            World = world;
            ZoneId = zoneId;
            Min = min;
            Max = max;
            Name = name;
        }

        // Checks if a given position is within the zone bounds
        private static bool IsPlayerInZone(Character player, Zone zone)
        {
            if( (int)player.World  == zone.World &&
                   player.Position.X >= zone.Min.X && player.Position.X <= zone.Max.X &&                   
                   player.Position.Z >= zone.Min.Z && player.Position.Z <= zone.Max.Z)
            {
                player.Zone = zone.ZoneId;
                return true;
            }
            return false;
        } 
    }
}
