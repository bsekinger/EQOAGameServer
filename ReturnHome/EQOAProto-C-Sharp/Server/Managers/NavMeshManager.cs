// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ReturnHome.Server.EntityObject;

namespace ReturnHome.Server.Managers
{
    public unsafe partial class NavMeshManager
    {
        private static readonly object findPathLock = new object();
        private static readonly object RandomRoamLock = new object();

        const string DllPath = @"C:\Users\bseki\source\repos\DetourWrapper\x64\Release\DetourWrapper.dll";
        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial void* allocDetour();

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial void freeDetour(void* detourPtr);

        [LibraryImport(DllPath), UnmanagedCallConv]        
        public static partial uint load(void* detourPtr, [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint random_roam(void* ptr, void* start, void* strPath);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint find_path(void* ptr, void* start, void* end, void* strPath);

        [LibraryImport(DllPath), UnmanagedCallConv]
        public static partial uint check_los(void* ptr, void* start, void* end, void* range);

        static readonly Rectangle[][] Worlds =
        {
            // Define rectangles for each world
            new Rectangle[] // Tunaria = 0
            {
                new Rectangle(20000, 0, 8000, 4000), // Area 1
                new Rectangle(2000, 4000, 26000, 18000), // Area 2
                new Rectangle(2000, 22000, 4000, 2000), // Area 3
                new Rectangle(12000, 22000, 16000, 12000) // Area 4
            },
            new Rectangle[] // RatheMountains = 1
            {
                new Rectangle(2000, 2000, 8000, 8000)
            },
            new Rectangle[] // RatheMountains = 2
            {
                new Rectangle(2000, 0, 12000, 14000)
            },
            new Rectangle[] // LavaStorm Mountains = 3
            {
                new Rectangle(4000, 4000, 4000, 2000)
            },
            new Rectangle[] // Plane of Sky = 4
            {
                new Rectangle(4000, 4000, 2000, 4000)
            },
            new Rectangle[] // Secrets = 5
            {
                new Rectangle(4000, 2000, 2000, 2000), // Last Home
                new Rectangle(4000, 6000, 2000, 2000)  // Zoaran Plateau
            },
        };

        private static int _world;
        private static int _zoneNumber = 256;
        private static int _zoneNumberLast = -1;

        private static Dictionary<string, IntPtr> detourInstances = new Dictionary<string, IntPtr>();
        private static Dictionary<string, Stopwatch> zoneTimers = new Dictionary<string, Stopwatch>();

        private static string zoneIdentifier = "";
        private static string currentZoneIdentifier = "";

        public static int GetPlayerZone(World world, float X, float Z)
        {
            _world = (int)world;
            Point position = new Point((int)X, (int)Z);
            Rectangle[] areas = Worlds[_world];
            int zoneOffset = 0;

            switch (_world)
            {
                case 0: // Tunaria
                    foreach (Rectangle area in areas)
                    {
                        if (area.Contains(position))
                        {
                            int areaIndex = Array.IndexOf(areas, area);
                            zoneOffset = GetZoneOffsetForTunaria(areaIndex);
                            break;
                        }
                    }
                    break;
                case 1: // RatheMountains
                    zoneOffset = 2;
                    break;
                case 2: // Odus
                    zoneOffset = 0;
                    break;
                case 3: // LavaStorm
                    zoneOffset = 0;
                    break;
                case 4: // Plane of Sky
                    zoneOffset = 0;
                    break;
                case 5: // Secrets
                    foreach (Rectangle area in areas)
                    {
                        if (area.Contains(position))
                        {
                            int areaIndex = Array.IndexOf(areas, area);
                            zoneOffset = areaIndex;
                            break;
                        }
                    }
                    break;
            }

            foreach (Rectangle area in areas)
            {
                if (area.Contains(position))
                {
                    int xIndex = (position.X - area.Left) / 2000;
                    int zIndex = (position.Y - area.Top) / 2000;

                    int columns = area.Width / 2000;

                    _zoneNumber = xIndex + zIndex * columns + zoneOffset;
                    return _zoneNumber;
                }
            }
            return -1;
        }

        public static void LoadMesh()
        {            
            // Generate a unique identifier for the zone (world + zone number)
            zoneIdentifier = $"{_world}_{_zoneNumber}";

            // Check if the detour instance for this zone is already loaded
            if (detourInstances.TryGetValue(zoneIdentifier, out IntPtr existingDetourPtr))
            {
                // Detour instance already loaded, check if timer exists
                if (zoneTimers.TryGetValue(zoneIdentifier, out Stopwatch tmr))
                {
                    // Timer exists. Restart timer.
                    tmr.Restart();
                }
            }
            else
            {
                // If the detour instance doesn't exist, load it
                void* detourPtr = allocDetour();
                if (detourPtr == null)
                {
                    //Console.WriteLine("Detour allocation failed!");
                }
                else
                {
                    //Console.WriteLine("Detour allocation success!");
                    string filePath = @"C:\Users\bseki\source\repos\EQOAGameServer\ReturnHome\EQOAProto-C-Sharp\Meshes\" + _world + @"\" + _zoneNumber + @".bin";
                    uint result = load(detourPtr, filePath);
                    if (result == 0)
                    {
                        //Console.WriteLine("World: " + _world + " Zone: " + _zoneNumber + " Load failed!");
                        freeDetour(detourPtr);
                        //Console.WriteLine("Detour allocation removed!");
                    }
                    else
                    {
                        Console.WriteLine("World: " + _world + " Zone: " + _zoneNumber + " Load successful!");
                        // Store the detour instance in the dictionary
                        detourInstances[zoneIdentifier] = new IntPtr(detourPtr);
                    }
                }

                // Check if the zone has a timer
                if (!zoneTimers.TryGetValue(zoneIdentifier, out Stopwatch timer))
                {
                    // If not, create a new timer and start it
                    timer = new Stopwatch();
                    timer.Start();
                    zoneTimers[zoneIdentifier] = timer;
                    Console.WriteLine("Timer started/loaded for zone identifier: " + zoneIdentifier);
                }
            }
        }

        static int GetZoneOffsetForTunaria(int areaIndex)
        {
            int zoneOffset = 0;

            switch (areaIndex)
            {
                case 1: // Area 2
                    zoneOffset = 8;
                    break;
                case 2: // Area 3
                    zoneOffset = 125;
                    break;
                case 3: // Area 4
                    zoneOffset = 127;
                    break;
            }

            return zoneOffset;
        }        

        public static void UnloadMesh()
        {
            // Generate a unique identifier for the zone (world + zone number)
            currentZoneIdentifier = $"{_world}_{_zoneNumber}";

            // Iterate through all loaded detour instances
            foreach (string zoneIdentifier in detourInstances.Keys.ToList())
            {
                if (zoneIdentifier != currentZoneIdentifier)
                {
                    // Check if the zone has a timer
                    if (zoneTimers.TryGetValue(zoneIdentifier, out Stopwatch timer))
                    {
                        // Check if the timer has exceeded 10 seconds
                        if (timer.Elapsed.TotalSeconds > 10)
                        {
                            // Unload the detour instance and remove it from dictionaries
                            UnloadDetourInstance(zoneIdentifier);
                        }
                    }
                }
            }
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
            try
            {
                Vector3 startPt = startPosition;
                Vector3 endPt = endPosition;
                uint pathCount = 0;

                // Generate a unique identifier for the zone (world + zone number)
                string ZoneIdentifier = $"{world}_{zone}";

                if (detourInstances.TryGetValue(ZoneIdentifier, out IntPtr detourPtr))
                {
                    Span<float> strPathArray = stackalloc float[48];

                    // Ensure memory safety with fixed block
                    fixed (float* strPathPtr = strPathArray)
                    lock (findPathLock)
                    {
                        pathCount = find_path((void*)detourPtr, &startPt, &endPt, strPathPtr);
                    }
                    
                    if (pathCount > 0)
                    {
                        List<Vector3> pathPoints = new List<Vector3>();
                        for (int i = 0; i < pathCount * 3; i += 3)
                        {
                            Vector3 point = new Vector3(strPathArray[i], strPathArray[i + 1], strPathArray[i + 2]);
                            pathPoints.Add(point);
                        }

                        return pathPoints;
                    }
                }

                Console.WriteLine("No path found.");
                return null;
            }
            catch (Exception ex)
            {
                // Log the exception for diagnostics
                Console.WriteLine($"Error in path finding: {ex.Message}");
                // Optionally, rethrow the exception if you want the caller to handle it
                // throw;
                return null;
            }
        }

         public static List<Vector3> roam(int world, int zone, Vector3 startPosition)
        {
            Vector3 startPt = startPosition;
            List<Vector3> pathPoints = new List<Vector3>();
            uint pathCount = 0;

            // Generate a unique identifier for the zone (world + zone number)
            string ZoneIdentifier = $"{_world}_{_zoneNumber}";

            if (detourInstances.TryGetValue(zoneIdentifier, out IntPtr detourPtr))
            {
                Span<float> strPathArray2 = stackalloc float[48];
                fixed (float* strPathPtr2 = strPathArray2)
                {
                    lock (RandomRoamLock)
                    {
                        pathCount = random_roam((void*)detourPtr, &startPt, strPathPtr2);
                    }

                    if (pathCount > 0)
                    {                        
                        for (int i = 0; i < pathCount * 3; i += 3)
                        {
                            Vector3 point = new Vector3(strPathArray2[i], strPathArray2[i + 1], strPathArray2[i + 2]);
                            pathPoints.Add(point);
                        }                        
                    }                    
                }
            }
            return pathPoints;
        }
    }
}



