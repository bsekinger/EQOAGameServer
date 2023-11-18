// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReturnHome.Server.EntityObject
{
    using System;
    using System.Collections.Generic;    
    using System.Numerics;
    using System.Security.Policy;
    using System.Threading;
    using System.Timers;
    using DotRecast.Core;
    using ReturnHome.Server.Managers;
    using Timer = System.Timers.Timer;

    public class NPCMovement2
    {
        private Entity entity;
        public Vector3 startingPoint;   // The starting point of the character        
        public Vector3 rndPt = new Vector3();
        public List<Vector3> path;

        private float maxMagnitude = 2f; // Maximum magnitude for the movement
        private Timer timer;
        private int updateIntervalMilliseconds = 10;

        private Vector3 currentPosition; // Current position of the character
        private int currentPathIndex = 0; // Index of the current target waypoint
        private float movementSpeed = 2.0f;
        private int world;
        private int zone;

        public void StartRndRoam(Entity e)
        {
            entity = e;

            currentPosition.X = e.x;
            currentPosition.Y = e.y;
            currentPosition.Z = e.z;
            world = (int)e.World;
            zone = e.zone;

            path = NavMeshManager.roam(world, zone, currentPosition);
            Console.WriteLine("Path points: ");
            foreach (Vector3 point in path)
            {
                Console.WriteLine(point.ToString());
            }
            currentPathIndex = 0;
            RoamWithTimer();
        }
        private void RoamWithTimer()
        {
            timer = new Timer(updateIntervalMilliseconds);
            timer.Elapsed += OnTimerElapsed;
            timer.Start();
        } 
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Calculate deltaTime as 0.010 seconds (10ms)
            float deltaTime = 0.010f;

            // Call the Update method with the calculated deltaTime
            Update(deltaTime);
        }
        public float Magnitude(Vector3 vec)
        {
            return (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
        }

        public void Update(float deltaTime)
        {
            // If there are no waypoints, do nothing
            if (path.Count == 0)
            {
                Console.WriteLine("No waypoints!");                 
                timer.Stop();
                return;
            }

            // Get the current target waypoint
            Vector3 targetWaypoint = path[currentPathIndex];
            Vector3 temp = (targetWaypoint - currentPosition);

            // Calculate the direction towards the target waypoint            
            Vector3 direction = Vector3.Normalize(targetWaypoint - currentPosition);
            Console.WriteLine("Direction: " + temp.ToString());
            Console.WriteLine("NormalizedDirection: " + direction.ToString());

            // Calculate the magnitude of the movement
            float magnitude = movementSpeed * deltaTime;

            // Check if the character has reached the target waypoint
            if (Vector3.Distance(currentPosition, targetWaypoint) < magnitude)
            {
                Console.WriteLine("PathIndex: " + currentPathIndex);
                // Move the character to the target waypoint exactly
                currentPosition = targetWaypoint;

                // Move to the next waypoint in the list or stop if it's the last waypoint
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    // Character has reached the final destination
                    currentPathIndex = path.Count - 1;
                    entity.Animation = 0;
                    entity.NorthToSouth = 0;
                    entity.EastToWest = 0;
                    entity.Position = currentPosition;
                    timer.Stop();
                    return;
                }
            }
            
            // Convert the direction and magnitude to bytes
            byte ns = GetNSByte(direction.Z, magnitude);
            byte ew = GetEWByte(direction.X, magnitude);

            // For demonstration, print the movement information
            Console.WriteLine($"Character moved to: {currentPosition}, Direction: {direction}, Magnitude: {magnitude}");

            // Uncomment the line below to send the bytes to the client (implementation-specific)
            UpdateClient(ns, ew);
        }
        private byte GetNSByte(float zDirection, float magnitude)
        {
            float normalizedZ = Math.Clamp(zDirection, -1f, 1f);
            byte nsByte = (byte)(normalizedZ * magnitude * 127f); //+ 128f);
            return nsByte;
        }

        private byte GetEWByte(float xDirection, float magnitude)
        {
            float normalizedX = Math.Clamp(xDirection, -1f, 1f);
            byte ewByte = (byte)(normalizedX * magnitude * 127f); //+ 128f);
            return ewByte;
        }
        private void UpdateClient(byte ns, byte ew)
        {
            entity.Animation = 1;            
            entity.NorthToSouth = ns;
            entity.EastToWest = ew;                                  
            Console.WriteLine("ns= " + ns + " ew= " + ew);
        }

    }
    
}
