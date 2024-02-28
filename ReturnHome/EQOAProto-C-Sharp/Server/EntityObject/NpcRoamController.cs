// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ReturnHome.Server.Managers;

namespace ReturnHome.Server.EntityObject
{
    public class NpcRoamController : IDisposable
    {
        private Dictionary<Entity, (Vector3 OriginalPosition, Vector3 RandomPoint, int RoamDirection)> _npcRoamData = new Dictionary<Entity, (Vector3, Vector3, int)>();
        private Dictionary<Entity, Task> _npcTasks = new Dictionary<Entity, Task>();
        private SemaphoreSlim _resourceSemaphore = new SemaphoreSlim(1, 1);

        private Dictionary<Entity, CancellationTokenSource> _npcCancellationTokens = new Dictionary<Entity, CancellationTokenSource>();
        private float _roamSpeed = 3.0f; // Default roam speed

        public NpcRoamController()
        {
            
        }

        public async Task AddNpcAsync(int world, int zone, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            Vector3 rndPoint = NavMeshManager.point(world, zone, npc.Position, 60.0f);

            if (rndPoint == Vector3.Zero)
            {
                Console.WriteLine($"Invalid random point for NPC: {npc.CharName}. Exiting AddNpcAsync.");
                return;
            }

            await _resourceSemaphore.WaitAsync();
            try
            {
                // Check if the npc is in the _npcRoamData dictionary
                if (!_npcRoamData.ContainsKey(npc))
                {
                    // Add the npc
                    _npcRoamData[npc] = (npcPosition, rndPoint, 1);

                    // Check if there's a need to create a new task for the NPC
                    if (!_npcTasks.ContainsKey(npc) || _npcTasks[npc].IsCompleted)
                    {
                        var cancellationTokenSource = new CancellationTokenSource();
                        _npcCancellationTokens[npc] = cancellationTokenSource;
                        var cancellationToken = cancellationTokenSource.Token;

                        _npcTasks[npc] = Task.Run(() => NpcRoamStateMachineAsync(world, zone, npc, cancellationToken));
                    }
                }
                else
                {
                    Console.WriteLine($"NPC: {npc.CharName} is already added. Skipping addition.");
                    return;
                }
            }
            finally
            {
                _resourceSemaphore.Release();
            }
        }


        private async Task NpcRoamStateMachineAsync(int world, int zone, Entity npc, CancellationToken cancellationToken)
        {
            if (!_npcRoamData.TryGetValue(npc, out var roamData))
            {
                Console.WriteLine($"NPC roam data not found. Exiting NpcRoamStateMachineAsync for NPC: {npc.CharName}.");
                return;
            }

            Vector3 direction = new Vector3();
            Vector3 newPosition = new Vector3();
            List<Vector3> path = new List<Vector3>();
            int roamState = 0;
            int targetIndex = 0;
            long lastUpdateTime = 0;
            float movementSpeed = 0;
            bool isRoaming = true;

            while (isRoaming && !cancellationToken.IsCancellationRequested)
            {
                switch (roamState)
                {
                    case 0:

                        if (path.Count == 0)
                        {
                            // No path exists, calculate a path to the random point.
                            path = CalculatePath(world, zone, npc);
                            targetIndex = 0;
                        }

                        roamState = 1;
                        break;

                    case 1:
                        // Set animation and speed
                        npc.Animation = 1;
                        movementSpeed = _roamSpeed;

                        roamState = 3;
                        break;

                    case 3:
                        // Record start time
                        lastUpdateTime = Environment.TickCount;

                        // Simluate a 60 fps frame rate
                        await Task.Delay(16);

                        roamState = 4;
                        break;

                    case 4:
                        // Calculate direction vector
                        direction = Vector3.Normalize(path[targetIndex] - npc.Position);

                        // Update facing
                        npc.Facing = UpdateFacing(npc.Position, path[targetIndex]);                        

                        roamState = 5;
                        break;

                    case 5:
                        // Calculate the elapsed time since the last frame
                        long currentTime = Environment.TickCount;
                        long deltaTime = currentTime - lastUpdateTime;

                        // Calculate the new position based on the elapsed time
                        float elapsedSeconds = deltaTime / 1000.0f;
                        newPosition = npc.Position + direction * movementSpeed * elapsedSeconds;

                        // Update the lastUpdateTime
                        lastUpdateTime = currentTime;

                        roamState = 6;
                        break;

                    case 6:
                        // Update npc position
                        npc.Position = newPosition;

                        roamState = 7;
                        break;

                    case 7:
                        // Check distance to the current target waypoint.
                        float distance = Vector3.Distance(path[targetIndex], npc.Position);

                        if (distance < 0.1) // NPC has reached the current waypoint.
                        {
                            // Check if the NPC has reached the end of the path.
                            if (targetIndex >= path.Count - 1)
                            {
                                // At the end of the path, so switch the roaming direction.
                                SwitchRoamDirection(npc);

                                // Move back to the second last waypoint to start the reverse journey.
                                targetIndex = path.Count - 2;
                                
                                targetIndex += _npcRoamData[npc].RoamDirection;

                                // Continue moving towards the new target waypoint.
                                roamState = 3;
                            }
                            else
                            {
                                // Move to the next waypoint or start reversing if at the start and path has more than one point.
                                if (targetIndex == 0 && path.Count > 1)
                                {
                                    // If already moving in reverse (back to the start), switch direction at the start.
                                    if (_npcRoamData[npc].RoamDirection < 0)
                                    {
                                        SwitchRoamDirection(npc);
                                    }

                                    targetIndex += _npcRoamData[npc].RoamDirection;
                                    roamState = 3;
                                }
                                else if (targetIndex > 0)
                                {
                                    // Normal waypoint progression or moving backwards through the waypoints.
                                    targetIndex += _npcRoamData[npc].RoamDirection;
                                    roamState = 3;
                                }
                            }
                        }
                        else
                        {
                            // If the NPC hasn't reached the current waypoint yet, continue moving towards it.
                            roamState = 3;
                        }
                        break;
                }
            }
        }

        private void SwitchRoamDirection(Entity npc)
        {
            if (_npcRoamData.TryGetValue(npc, out var roamData))
            {
                // Determine the new roam direction.
                int newRoamDirection = roamData.RoamDirection * -1;

                // Update the dictionary entry for the NPC with the new roam direction.
                _npcRoamData[npc] = (roamData.OriginalPosition, roamData.RandomPoint, newRoamDirection);
            }
        }

        private List<Vector3> CalculatePath(int world, int zone, Entity npc)
        {
            if (!_npcRoamData.TryGetValue(npc, out var npcData))
            {
                Console.WriteLine($"Data for NPC: {npc.CharName} not found.");
                return new List<Vector3>(); // Return an empty path if NPC data is not found
            }

            List<Vector3> path = NavMeshManager.smoothPath(world, zone, npc.Position, npcData.RandomPoint);
            return path;
        }

        public async Task RemoveNpcAsync(Entity npc)
        {
            await _resourceSemaphore.WaitAsync();
            try
            {
                if (_npcCancellationTokens.TryGetValue(npc, out var cancellationTokenSource))
                {
                    cancellationTokenSource.Cancel();
                    _npcCancellationTokens.Remove(npc);
                }
                _npcTasks.Remove(npc);
                _npcRoamData.Remove(npc);

                // Reset animation
                npc.Animation = 0;
            }
            finally
            {
                _resourceSemaphore.Release();
            }
        }

        public void Dispose()
        {
            foreach (var cts in _npcCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _npcCancellationTokens.Clear();
            _resourceSemaphore.Dispose();
        }

        private static byte UpdateFacing(Vector3 start, Vector3 end)
        {
            double angle = CalculateAngle(start, end);
            byte facing = TransformToByte(angle);

            return facing;
        }

        private static double CalculateAngle(Vector3 startPoint, Vector3 waypoint)
        {
            // Calculate the differences in x and z coordinates
            float deltaX = waypoint.X - startPoint.X;
            float deltaZ = waypoint.Z - startPoint.Z;

            // Calculate the angle using the arctan2 function with inverted Y value
            double angleRadians = Math.Atan2(deltaZ, deltaX);

            // Convert the angle from radians to degrees using floating-point division
            double angleDegrees = angleRadians * (180.0 / Math.PI);

            // Ensure the angle is in the range [0, 360)
            angleDegrees = (angleDegrees + 360) % 360;

            // Convert the angle to clockwise from the positive x-axis
            angleDegrees = 360 - angleDegrees;

            return angleDegrees;
        }

        private static byte TransformToByte(double angleDegrees)
        {
            // Scale the angle from the range 0 to 360 to the range 0 to 255
            double scaledAngle = angleDegrees / 360.0 * 256.0;

            // Add an offset of 64 to the scaled angle
            scaledAngle += 64;

            // Ensure the scaled angle is within the range 0 to 255
            scaledAngle = scaledAngle % 256;

            // Convert the scaled angle to a byte (unsigned 8-bit integer)
            byte angleByte = (byte)scaledAngle;

            return angleByte;
        }

    }
}
