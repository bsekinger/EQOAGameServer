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
using ReturnHome.Server.Zone;
using ReturnHome.Server.EntityObject;

namespace ReturnHome.Server.Managers
{
    public class ZoneManager
    {
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public static HashSet<string> zonesWithNpcsStarted = new HashSet<string>();

        // Checks if a given position is within the zone bounds
        private static bool IsPlayerInZone(Character player, Zone.Zone zone)
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

        public static async Task CheckZonesForPlayers(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {                    
                    foreach (var worldZonesPair in ZoneDefinitions.ZonesByWorld)
                    {
                        int worldId = worldZonesPair.Key;
                        List<Zone.Zone> zones = worldZonesPair.Value;

                        foreach (var zone in zones)
                        {                            
                            bool isPlayerPresent = PlayerManager.playerList.Any(player => IsPlayerInZone(player, zone));

                            if (isPlayerPresent)
                            {
                                NavMeshManager.LoadMeshIfNeeded(worldId, zone.ZoneId);
                                await StartNpcsForZoneIfNeeded(worldId, zone.ZoneId);
                            }
                        }
                    }

                    // Periodically check and unload meshes if no activity
                    await NavMeshManager.MonitorAndUnloadMeshes();
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Zone player checking was canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking zones for players: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public static void CancelZoneCheck()
        {
            cancellationTokenSource.Cancel();
        }

        public static async Task StartNpcsForZoneIfNeeded(int world, int zone)
        {
            string zoneKey = $"{world}_{zone}";
            if (!zonesWithNpcsStarted.Contains(zoneKey) && NavMeshManager.IsDetourInstance(world, zone))
            {
                await StartNpcsForZoneAsync(world, zone);

                // After starting NPCs for the zone, add this zone to list
                zonesWithNpcsStarted.Add(zoneKey);
            }
        }

        public static async Task StartNpcsForZoneAsync(int world, int zone)
        {
            if (NavMeshManager.IsDetourInstance(world, zone))
            {
                var npcTasks = new List<Task>();

                // Roamers
                var roamers = EntityManager.QueryForAllRoamersByWorldAndZone(world, zone);
                foreach (var roamer in roamers)
                {
                    npcTasks.Add(WorldServer.npcRoamController.AddNpcAsync(world, zone, roamer));
                    await Task.Delay(5);
                }

                // Patrollers
                var patrollers = EntityManager.QueryForAllPatrollersByWorldAndZone(world, zone);
                foreach (var patroller in patrollers)
                {
                    npcTasks.Add(WorldServer.npcPatrolController.AddNpcAsync(world, zone, patroller));
                    await Task.Delay(5);
                }

                await Task.WhenAll(npcTasks);
            }
        }
    }
}
