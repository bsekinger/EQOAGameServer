// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReturnHome.Server.EntityObject;

namespace ReturnHome.Server.Managers
{
    public unsafe partial class NavMeshManager
    {
        private static readonly object findPathLock = new object();
        private static readonly object findSmoothPointLock = new object();
        private static readonly object RandomRoamLock = new object();
        private static readonly object RandomPointLock = new object();


        const string DllPath = @"C:\Users\bseki\source\repos\DetourWrapper\x64\Release\DetourWrapper.dll";
        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial void* allocDetour();

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial void freeDetour(void* detourPtr);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint load(void* detourPtr, [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint find_path(void* ptr, void* start, void* end, void* strPath);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint find_smoothPath(void* ptr, void* start, void* end, void* strPath);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint check_los(void* ptr, void* start, void* end, void* range);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint random_point(void* ptr, void* centerPoint, float radius, float* rndPoint);

        private static Dictionary<string, IntPtr> detourInstances = new Dictionary<string, IntPtr>();
        private static Dictionary<string, Stopwatch> zoneTimers = new Dictionary<string, Stopwatch>();

        public static void LoadMeshIfNeeded(int world, int zone)
        {
            string zoneIdentifier = $"{world}_{zone}";

            if (!detourInstances.ContainsKey(zoneIdentifier))
            {
                // Load the mesh because it's not already loaded
                Console.WriteLine($"Player(s) detected in Zone {zone} of World {world}. Loading navmesh...");
                LoadMesh(world, zone);
            }

            // Reset or start the timer for this zone
            if (!zoneTimers.TryGetValue(zoneIdentifier, out Stopwatch timer))
            {
                timer = new Stopwatch();
                zoneTimers[zoneIdentifier] = timer;
            }
            timer.Restart(); // Reset the timer whenever activity is detected
        }

        public static void LoadMesh(int world, int zone)
        {
            // Generate a unique identifier for the zone (world + zone number)
            string zoneIdentifier = $"{world}_{zone}";

            // Only proceed if the detour instance for this zone is not already loaded
            if (!detourInstances.ContainsKey(zoneIdentifier))
            {
                // Load the detour instance
                void* detourPtr = allocDetour();
                if (detourPtr == null)
                {
                    Console.WriteLine("Detour allocation failed!");
                }
                else
                {
                    string filePath = $@"C:\Users\bseki\source\repos\EQOAGameServer\ReturnHome\EQOAProto-C-Sharp\Meshes\{world}\{zone}.bin";

                    uint result = load(detourPtr, filePath);
                    if (result == 0)
                    {
                        Console.WriteLine($"Load failed for World: {world}, Zone: {zone}");
                        freeDetour(detourPtr); // Ensure to free allocated memory on failure
                    }
                    else
                    {
                        Console.WriteLine($"World: {world}, Zone: {zone} Load successful!");
                        // Successfully loaded, store the detour instance
                        detourInstances[zoneIdentifier] = new IntPtr(detourPtr);
                        // No need to check or create a timer here as it's managed in LoadMeshIfNeeded
                    }
                }
            }
            else
            {
                Console.WriteLine($"Detour instance for World: {world}, Zone: {zone} is already loaded.");
            }
        }

        public static async Task MonitorAndUnloadMeshes()
        {
            // This method should be called periodically, e.g., every second
            var zonesToUnload = new List<string>();
            foreach (var kvp in zoneTimers)
            {
                if (kvp.Value.Elapsed.TotalSeconds > 20)
                {
                    // Identify zones that are ready to be unloaded
                    zonesToUnload.Add(kvp.Key);
                }
            }

            foreach (var zoneKey in zonesToUnload)
            {
                // Parse world and zone identifiers
                string[] identifiers = zoneKey.Split('_');
                int world = int.Parse(identifiers[0]);
                int zone = int.Parse(identifiers[1]);

                // Before unloading the mesh, stop all roamers and patrollers in the zone
                _ = WorldServer.npcRoamController.RemoveNpcsAsync(world, zone);
                _ = WorldServer.npcPatrolController.RemoveNpcsAsync(world, zone);

                // Unload the mesh
                UnloadMesh(world, zone);

                // Remove the zone from zonesWithNpcsStarted to reflect that NPCs are no longer active in this zone
                ZoneManager.zonesWithNpcsStarted.Remove(zoneKey);

                // Additionally, stop and remove the timer for this zone to prevent future checks
                zoneTimers[zoneKey].Stop();
                zoneTimers.Remove(zoneKey);
            }
        }

        public static void UnloadMesh(int world, int zone)
        {
            // Generate a unique identifier for the zone (world + zone number)
            string currentZoneIdentifier = $"{world}_{zone}";

            // Iterate through all loaded detour instances
            foreach (string zoneIdentifier in detourInstances.Keys.ToList())
            {
                if (zoneIdentifier == currentZoneIdentifier)
                {                    
                    // Unload the detour instance and remove it from dictionaries
                    UnloadDetourInstance(zoneIdentifier);
                }
            }
        }

        public static bool IsDetourInstance(int world, int zone)
        {
                // Generate a unique identifier for the zone (world + zone number)
                string zoneIdentifier = $"{world}_{zone}";

                // Check if the detour instance for this zone is already loaded
                if (detourInstances.TryGetValue(zoneIdentifier, out IntPtr existingDetourPtr))
                {
                    return true;
                }

                return false;
        }

        private static void UnloadDetourInstance(string zoneIdentifier)
        {
            if (detourInstances.TryGetValue(zoneIdentifier, out IntPtr detourPtr))
            {
                freeDetour((void*)detourPtr);
                detourInstances.Remove(zoneIdentifier);
                zoneTimers.Remove(zoneIdentifier);
                Console.WriteLine("Detour instance for " + zoneIdentifier + " unloaded.");
            }
        }

        public static List<Vector3> path(int world, int zone, Vector3 startPosition, Vector3 endPosition)
        {
            Vector3 startPt = startPosition;
            Vector3 endPt = endPosition;
            uint pathCount = 0;
            List<Vector3> pathPoints = new List<Vector3>();

            try
            {
                // Generate a unique identifier for the zone (world + zone number)
                string ZoneIdentifier = $"{world}_{zone}";

                if (detourInstances.TryGetValue(ZoneIdentifier, out IntPtr detourPtr))
                {
                    Span<float> strPathArray = stackalloc float[768];

                    // Ensure memory safety with fixed block
                    fixed (float* strPathPtr = strPathArray)
                        lock (findPathLock)
                        {
                            pathCount = find_path((void*)detourPtr, &startPt, &endPt, strPathPtr);
                        }

                    if (pathCount > 0)
                    {
                        for (int i = 0; i < pathCount * 3; i += 3)
                        {
                            Vector3 point = new Vector3(strPathArray[i], strPathArray[i + 1], strPathArray[i + 2]);
                            pathPoints.Add(point);
                        }
                        return pathPoints;
                    }
                }

                Console.WriteLine("No path found.");
                pathCount = 0;
                return pathPoints;
            }
            catch (Exception ex)
            {
                // Log the exception for diagnostics
                Console.WriteLine($"Error in path finding: {ex.Message}");
                return pathPoints;
            }
        }

        public static List<Vector3> smoothPath(int world, int zone, Vector3 startPosition, Vector3 endPosition)
        {
            Vector3 startPt = startPosition;
            Vector3 endPt = endPosition;
            uint pathCount = 0;
            List<Vector3> pathPoints = new List<Vector3>();

            try
            {
                // Generate a unique identifier for the zone (world + zone number)
                string ZoneIdentifier = $"{world}_{zone}";

                if (detourInstances.TryGetValue(ZoneIdentifier, out IntPtr detourPtr))
                {
                    Span<float> smoothPathArray = stackalloc float[6144];

                    // Ensure memory safety with fixed block
                    fixed (float* smoothPathPtr = smoothPathArray)
                        lock (findSmoothPointLock)
                        {
                            pathCount = find_smoothPath((void*)detourPtr, &startPt, &endPt, smoothPathPtr);
                        }

                    if (pathCount > 0)
                    {
                        for (int i = 0; i < pathCount * 3; i += 3)
                        {
                            Vector3 point = new Vector3(smoothPathArray[i], smoothPathArray[i + 1], smoothPathArray[i + 2]);
                            pathPoints.Add(point);
                        }
                        return pathPoints;
                    }
                }

                Console.WriteLine("No smooth path found.");
                pathCount = 0;
                return pathPoints;
            }
            catch (Exception ex)
            {
                // Log the exception for diagnostics
                Console.WriteLine($"Error in path finding: {ex.Message}");
                return pathPoints;
            }
        }

        public static Vector3 point(int world, int zone, Vector3 center, float radius)
        {
            uint pointFound = 0;
            Vector3 centerPt = center;

            // Generate a unique identifier for the zone (world + zone number)
            string zoneIdentifier = $"{world}_{zone}";

            if (detourInstances.TryGetValue(zoneIdentifier, out IntPtr detourPtr))
            {
                Span<float> rndPointArray = stackalloc float[3];
                fixed (float* rndPointPtr = rndPointArray)
                {
                    lock (RandomPointLock)
                    {
                        pointFound = random_point((void*)detourPtr, (float*)&centerPt, radius, rndPointPtr);
                    }                   

                    if (pointFound > 0)
                    {                        
                        Vector3 randomPoint = new Vector3(rndPointArray[0], rndPointArray[1], rndPointArray[2]);
                        // Console.WriteLine($"Random Point found! {randomPoint.ToString()}");
                        return randomPoint;
                    }
                    else
                    {
                        Console.WriteLine($"No random point found.");
                    }
                }

            }

            return Vector3.Zero;
        }
    }
}



