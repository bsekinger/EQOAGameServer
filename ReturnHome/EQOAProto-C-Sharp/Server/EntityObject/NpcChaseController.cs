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
    public class NpcChaseController : IDisposable
    {
        private Dictionary<Entity, (Vector3 OriginalPosition, byte OriginalFacing)> _npcChaseData = new Dictionary<Entity, (Vector3, byte)>();
        private Dictionary<Entity, Task> _npcTasks = new Dictionary<Entity, Task>();
        private SemaphoreSlim _resourceSemaphore = new SemaphoreSlim(1, 1);

        private Dictionary<Entity, CancellationTokenSource> _npcCancellationTokens = new Dictionary<Entity, CancellationTokenSource>();
        private float _chaseSpeed = 10.0f;
        private float _returnSpeed = 20.0f;

        public NpcChaseController()
        {
            
        }

        public async Task AddNpcAsync(int world, int zone, Entity player, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            byte npcFacing = npc.Facing;            

            await _resourceSemaphore.WaitAsync();
            try
            {
                _npcChaseData[npc] = (npcPosition, npcFacing);
                if (!_npcTasks.ContainsKey(npc) || _npcTasks[npc].IsCompleted)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    _npcCancellationTokens[npc] = cancellationTokenSource;
                    var cancellationToken = cancellationTokenSource.Token;

                    _npcTasks[npc] = Task.Run(() => NpcChaseStateMachineAsync(world, zone, player, npc, cancellationToken));
                }
            }
            finally
            {
                _resourceSemaphore.Release();
            }
        }


        private async Task NpcChaseStateMachineAsync(int world, int zone, Entity player, Entity npc, CancellationToken cancellationToken)
        {
            List<Vector3> path = new List<Vector3>();
            float movementSpeed = 0;
            float chaseDistance = 100.0f;
            int chaseState = 0;
            int targetIndex = 1;
            long lastUpdateTime = 0;
            Vector3 direction = new Vector3();
            Vector3 newPosition = new Vector3();
            object timerLock = new object();
            Vector3 velocity = new Vector3();
            bool isChasing = true;

            System.Timers.Timer pathUpdateTimer = new System.Timers.Timer(500);

            while (isChasing && !cancellationToken.IsCancellationRequested)
            {
                switch (chaseState)
                {
                    case 0:
                        Vector3 npcPos = npc.Position;
                        Vector3 playerPos = player.Position;

                        chaseState = 1;
                        break;

                    case 1:
                        // Flag to indicate if the first path has been received
                        bool firstPathReceived = false;

                        // Timer to request a new path
                        lock (timerLock)
                        {
                            pathUpdateTimer.Elapsed += (sender, args) =>
                            {
                                var updatedPath = OnPathUpdate(world, zone, npc.Position, player.Position);
                                if (updatedPath.Count > 0)
                                {
                                    lock (timerLock) // Additional lock for thread safety
                                    {
                                        path = updatedPath;
                                        firstPathReceived = true;
                                        targetIndex = 0; // Reset to start of the new path
                                    }
                                }
                                else
                                {
                                    // Handle no path case
                                    chaseState = 20; // Set chaseState to 10 to stop chasing
                                }
                            };
                            pathUpdateTimer.AutoReset = true;
                            pathUpdateTimer.Start();
                        }

                        // Wait for the first path update
                        while (!firstPathReceived)
                        {
                            await Task.Delay(10); // Adjust the delay as needed
                        }

                        lastUpdateTime = Environment.TickCount; // Record the start time

                        // Set animation and speed
                        npc.Animation = 3;
                        movementSpeed = _chaseSpeed;

                        chaseState = 2; // Proceed to the next state
                        break;

                    case 2:
                        // Initialize the index to the first point in the path
                        targetIndex = 0;
                        chaseState = 3;
                        break;

                    case 3:
                        // Simulate frame time asynchronously using Task.Delay
                        await Task.Delay(16);
                        chaseState = 4;
                        break;

                    case 4:
                        // Check fo no path condition while chasing (Mob zoned)
                        if (path is null || path.Count == 0)
                        {
                            npc.Position = _npcChaseData[npc].OriginalPosition;
                            npc.Facing = _npcChaseData[npc].OriginalFacing;
                            chaseState = 20;
                            break;
                        }

                        // Check for no path or targetIndex exceeds path count
                        if (targetIndex >= path.Count)
                        {
                            chaseState = 20; // Stop if the path is empty or finished
                            break;
                        }

                        // Check if the NPC has moved too far from its original position
                        if (Vector3.Distance(npc.Position, _npcChaseData[npc].OriginalPosition) > chaseDistance)
                        {
                            chaseState = 11; // Transition to leashing state
                            break;
                        }

                        // Calculate the direction vector from NPC to the target point
                        direction = Vector3.Normalize(path[targetIndex] - npc.Position);

                        // Update npc facing
                        npc.Facing = UpdateFacing(npc.Position, player.Position);

                        chaseState = 5;
                        break;

                    case 5:
                        // Calculate the elapsed time since the last frame
                        long currentTime = Environment.TickCount;
                        long deltaTime = currentTime - lastUpdateTime;
                        //Console.WriteLine($"Delta Time (ms): {deltaTime}");

                        // Calculate the new position based on the elapsed time
                        float elapsedSeconds = deltaTime / 1000.0f; // Convert to seconds
                        newPosition = npc.Position + direction * movementSpeed * elapsedSeconds;

                        // Calculate velocity
                        velocity = direction * movementSpeed;

                        // Update the last update time
                        lastUpdateTime = currentTime;

                        chaseState = 6;
                        break;

                    case 6:
                        // Update npc position
                        npc.Position = newPosition;

                        chaseState = 7;
                        break;

                    case 7:
                        // Read player and npc positions
                        Vector3 npcPosition = npc.Position;
                        Vector3 playerPosition = player.Position;

                        // Check if the NPC has reached the current target point
                        if (Vector3.Distance(npc.Position, path[targetIndex]) < 1.0)
                        {
                            targetIndex++; // Move to the next waypoint
                        }

                        chaseState = 3;
                        break;

                    case 11:
                        pathUpdateTimer.Stop();
                        
                        chaseState = 12;
                        break;

                    case 12:
                        // Request return path
                        var leashPath = CalculatePath(world, zone, npc);
                        if (leashPath.Count > 0)
                        {
                            path = leashPath;
                        }
                        else
                        {
                            // Handle no path case                                    
                            npc.Position = _npcChaseData[npc].OriginalPosition;
                            npc.Facing = _npcChaseData[npc].OriginalFacing;
                            chaseState = 20;
                            break;
                        }                        

                        // Set animation and speed
                        npc.Animation = 3;
                        movementSpeed = _returnSpeed;

                        chaseState = 13; // Proceed to the next state
                        break;

                    case 13:
                        // Initialize the index to the first point in the path
                        targetIndex = 0;
                        chaseState = 14;
                        break;

                    case 14:
                        lastUpdateTime = Environment.TickCount; // Record the start time

                        // Simulate frame time asynchronously using Task.Delay
                        await Task.Delay(16);
                        chaseState = 15;
                        break;

                    case 15:
                        // Check for no path while returning
                        if (path is null || path.Count == 0)
                        {
                            npc.Position = _npcChaseData[npc].OriginalPosition;
                            npc.Facing = _npcChaseData[npc].OriginalFacing;
                            chaseState = 20; // Stop if the path is empty or finished
                            break;
                        }

                        if (targetIndex >= path.Count)
                        {
                            chaseState = 20; // Stop if the path is empty or finished
                            break;
                        }

                        // Calculate the direction vector from NPC to the target point
                        direction = Vector3.Normalize(path[targetIndex] - npc.Position);

                        /// Update npc facing
                        npc.Facing = UpdateFacing(npc.Position, path[targetIndex]);

                        chaseState = 16;
                        break;

                    case 16:
                        // Calculate the elapsed time since the last frame
                        currentTime = Environment.TickCount;
                        deltaTime = currentTime - lastUpdateTime;
                        //Console.WriteLine($"Delta Time (ms): {deltaTime}");

                        // Calculate the new position based on the elapsed time
                        elapsedSeconds = deltaTime / 1000.0f; // Convert to seconds
                        newPosition = npc.Position + direction * movementSpeed * elapsedSeconds;

                        // Calculate velocity
                        velocity = direction * movementSpeed;

                        // Update the last update time
                        lastUpdateTime = currentTime;

                        chaseState = 17;
                        break;

                    case 17:
                        // Update npc position
                        npc.Position = newPosition;

                        chaseState = 18;
                        break;

                    case 18:
                        // Read player and npc positions
                        npcPosition = npc.Position;

                        // Check if the NPC has reached the current target point
                        if (Vector3.Distance(npc.Position, path[targetIndex]) < 1.0)
                        {
                            targetIndex++; // Move to the next waypoint
                        }

                        chaseState = 14;
                        break;

                    case 20:

                        pathUpdateTimer.Stop();
                        pathUpdateTimer.Dispose();
                        pathUpdateTimer = null;

                        npc.Animation = 0;
                        movementSpeed = 0;

                        chaseState = 0;
                        Task.Run(() => RemoveNpcAsync(npc)).ConfigureAwait(false);
                        return;
                }
            }
        }

        private List<Vector3> OnPathUpdate(int world, int zone, Vector3 npcPosition, Vector3 playerPosition)
        {
            List<Vector3> path = NavMeshManager.path(world, zone, npcPosition, playerPosition);
            return path;
        }

        private List<Vector3> CalculatePath(int world, int zone, Entity npc)
        {
            if (!_npcChaseData.TryGetValue(npc, out var npcData))
            {
                Console.WriteLine($"Data for NPC: {npc.CharName} not found.");
                return new List<Vector3>(); // Return an empty path if NPC data is not found
            }

            List<Vector3> path = NavMeshManager.smoothPath(world, zone, npc.Position, npcData.OriginalPosition);
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
                _npcChaseData.Remove(npc);

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
