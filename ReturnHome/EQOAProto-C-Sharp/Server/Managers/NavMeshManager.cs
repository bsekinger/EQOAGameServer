// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using ReturnHome.Server.EntityObject;

namespace ReturnHome.Server.Managers
{
    public static class NavMeshManager
    {
        private static int MinX = 2000;
        private static int MinZ = 2000;
        private static int MaxX = 26000;
        private static int MaxZ = 32000;
        private const int ZoneWidth = 2000;
        private const int ZoneHeight = 2000;
        private static int ZonesPerRow;
        private static readonly DtMeshSetReader reader = new DtMeshSetReader();
        private static byte previousZoneIndex = 255; // Set an initial value to ensure it's different from any valid zone index

        public static DtNavMesh mesh = null;

        public static byte GetPlayerZone(World world, float x, float z)
        {
            byte ZoneIndex;
            int playerX = (int)x;
            int playerZ = (int)z;
            switch (world)
            {
                case (World)0: //Tunaria    2000.0f, 2000.0f, 26000.0f, 32000.0f
                    MinX = 2000;
                    MaxX = 26000;
                    MinZ = 2000;
                    MaxZ = 32000;
                break;

                case (World)1: //Rathe Mountains    4000.0f, 2000.0f, 6000.0f, 8000.0f
                    MinX = 4000;
                    MaxX = 10000;
                    MinZ = 2000;
                    MaxZ = 10000;
                break;

                case (World)2: //Odus   4000.0f, 2000.0f, 8000.0f, 10000.0f
                    MinX = 40000;
                    MaxX = 20000;
                    MinZ = 12000;
                    MaxZ = 12000;
                break;

                case(World)3: //Lava Storm  4000.0f, 4000.0f, 4000.0f, 2000.0f
                    MinX = 4000;
                    MaxX = 8000;
                    MinZ = 4000;
                    MaxZ = 6000;
                break;

                case (World)4: //Plane of Sky   4000.0f, 4000.0f, 2000.0f, 4000.0f
                    MinX = 4000;
                    MaxX = 6000;
                    MinZ = 4000;
                    MaxZ = 8000;
                break;

                case (World)5: //Secrets    4000.0f, 2000.0f, 2000.0f, 6000.0f
                    MinX = 4000;
                    MaxX = 6000;
                    MinZ = 2000;
                    MaxZ = 8000;
                break;
            }

            ZonesPerRow = (MaxX - MinX) / ZoneWidth + 1;
            if (playerX >= MinX && playerX <= MaxX && playerZ >= MinZ && playerZ <= MaxZ)
            {
                int zoneX = (playerX - MinX) / ZoneWidth;
                int zoneY = (playerZ - MinZ) / ZoneHeight;

                // Calculate the zone based on X and Y coordinates
                ZoneIndex = (byte)(zoneX + zoneY * ZonesPerRow - 5);

                // Check if the ZoneIndex changed from the previous value
                if (ZoneIndex != previousZoneIndex)
                {
                    // Unload the previous NavMesh if it exists
                    UnloadNavMesh();

                    // Store the Task representing the loading process
                    Task loadingTask = LoadNavMeshAsync(world, ZoneIndex);

                    // Wait for the loading task to complete
                    loadingTask.Wait();

                    // Update the previousZoneIndex to the new value
                    previousZoneIndex = ZoneIndex;

                }

                return ZoneIndex;
            }

            // Return an invalid zone value if the player is outside the game world
            return 255;
        }
        public static async Task LoadNavMeshAsync(World world, byte ZoneIndex)
        {
            await Task.Run(() => LoadNavMesh(world, ZoneIndex));
        }

        public static async Task LoadNavMesh(World world, byte ZoneIndex)
        {
            if (ZoneIndex == 87 || ZoneIndex == 100) // Check if the ZoneIndex requires loading a new mesh
            {
                string path = string.Format(@"C:\Users\bsekinger\Source\Repos\EQOAGameServer\NavMesh-{0}\{1}.navmesh", world, ZoneIndex);
                byte[] @is = Loader.ToBytes(path);
                using var ms = new MemoryStream(@is);
                using var bris = new BinaryReader(ms);
                mesh = reader.Read(bris, 6);

                if (mesh != null)
                {
                    Console.WriteLine("Loaded Navmesh for world {0} Zone {1}.", world, ZoneIndex);
                }
                
            }
            else
            {
                mesh = null; // Set the mesh to null if ZoneIndex doesn't require loading a new mesh
                Console.WriteLine("Cannot load navmesh. Zone mesh hasn't been built yet!.");
            }
        }

        public static void UnloadNavMesh()
        {
            // Add code here to unload the previous navmesh, if needed
            // For example, you can set 'mesh' to null to release the previous navmesh from memory
            mesh = null;
            Console.WriteLine("Unloaded previous Navmesh.");
        }
    }
}
