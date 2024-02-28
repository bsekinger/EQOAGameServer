// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using NLua;
using ReturnHome.Server.Managers;

namespace ReturnHome.Server.EntityObject
{
    public class NpcPatrolController : IDisposable
    {
        private Dictionary<Entity, (Vector3 OriginalPosition, byte OriginalFacing)> _npcPatrolData = new Dictionary<Entity, (Vector3, byte)>();
        private Dictionary<Entity, Task> _npcTasks = new Dictionary<Entity, Task>();
        private SemaphoreSlim _resourceSemaphore = new SemaphoreSlim(1, 1);

        private Dictionary<Entity, CancellationTokenSource> _npcCancellationTokens = new Dictionary<Entity, CancellationTokenSource>();
        private float _roamSpeed = 3.0f;

        public NpcPatrolController()
        {
            
        }

        public async Task AddNpcAsync(int world, int zone, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            byte npcFacing = npc.Facing;
            List<Vector3> waypoints = new List<Vector3>();
            List<int> pauses = new List<int>();

            string npcName = npc.CharName;
            string scriptName = npcName.Replace(" ", "_") + ".lua";
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string relativePath = @"Scripts\patrollers\";
            string filePath = Path.Combine(basePath, relativePath, scriptName);

            try
            {
                using (var lua = new Lua())
                {
                    lua.DoFile(filePath);
                    LuaFunction getWaypointsFunc = lua.GetFunction("getWaypointsForNpc");

                    int npcId = npc.ServerID;
                    LuaTable waypointsTable = getWaypointsFunc.Call(npcId)[0] as LuaTable;

                    foreach (LuaTable waypointData in waypointsTable.Values)
                    {
                        float x = Convert.ToSingle(waypointData["x"]);
                        float y = Convert.ToSingle(waypointData["y"]);
                        float z = Convert.ToSingle(waypointData["z"]);
                        int pause = Convert.ToInt32(waypointData["pause"]);

                        waypoints.Add(new Vector3(x, y, z));
                        pauses.Add(pause);
                    }
                }
            }
            catch (NLua.Exceptions.LuaScriptException ex)
            {
                Console.WriteLine("LuaScriptException caught: " + ex.Message);
            }

            await _resourceSemaphore.WaitAsync();
            try
            {
                _npcPatrolData[npc] = (npcPosition, npcFacing);
                if (!_npcTasks.ContainsKey(npc) || _npcTasks[npc].IsCompleted)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    _npcCancellationTokens[npc] = cancellationTokenSource;
                    var cancellationToken = cancellationTokenSource.Token;

                    _npcTasks[npc] = Task.Run(() => NpcPatrolStateMachineAsync(world, zone, npc, waypoints, pauses, cancellationToken));
                }
            }
            finally
            {
                _resourceSemaphore.Release();
            }
        }

        private async Task NpcPatrolStateMachineAsync(int world, int zone, Entity npc, List<Vector3> waypoints, List<int> pauses, CancellationToken cancellationToken)
        {
            List<Vector3> path = new List<Vector3>();
            float MovementSpeed = 0;
            int patrolState = 0;
            int waypointIndex = 0;
            int pathIndex = 0;
            Vector3 direction = new Vector3();
            Vector3 newPosition = new Vector3();
            long patrollastUpdateTime = 0;
            int patrolDirection = 1; // Direction of movement, 1 for forward, -1 for backward
            bool isPatrolling = true;

            // Ensure waypoints and pauses lists are of the same length
            if (waypoints.Count != pauses.Count)
            {
                throw new InvalidOperationException("Waypoints and pauses lists must be of the same length.");
            }

            while (isPatrolling && !cancellationToken.IsCancellationRequested)
            {
                switch (patrolState)
                {
                    case 0: // use this case to init the patrol only
                        waypointIndex = 1;
                        patrolDirection = 1;

                        // update facing
                        npc.Facing = UpdateFacing(npc.Position, waypoints[waypointIndex]);

                        // Delay
                        if (pauses[waypointIndex] > 0)
                        {
                            await Task.Delay(pauses[waypointIndex]); // Asynchronously wait for the pause duration
                        }

                        patrolState = 1;
                        break;

                    case 1:
                        // Record start time
                        patrollastUpdateTime = Environment.TickCount;
                        pathIndex = 1;
                        // find path to first waypoint
                        path = NavMeshManager.smoothPath(world, zone, npc.Position, waypoints[waypointIndex]);
                        if (path.Count == 0)
                        {
                            Console.WriteLine("Path empty!");
                            patrolState = 10;
                            break;
                        }

                        patrolState = 2;
                        break;

                    case 2:
                        // set animation and speed
                        npc.Animation = 1;
                        MovementSpeed = _roamSpeed;

                        patrolState = 3;
                        break;

                    case 3:
                        // Simluate a 60 fps frame rate
                        await Task.Delay(16);

                        patrolState = 4;
                        break;

                    case 4:
                        // Calculate direction vector
                        direction = Vector3.Normalize(path[pathIndex] - npc.Position);

                        // update facing
                        npc.Facing = UpdateFacing(npc.Position, waypoints[waypointIndex]);

                        patrolState = 5;
                        break;

                    case 5:
                        // Calculate the elapsed time since the last frame
                        long currentTime = Environment.TickCount;
                        long deltaTime = currentTime - patrollastUpdateTime;

                        // Calculate the new position based on the elapsed time
                        float elapsedSeconds = deltaTime / 1000.0f;
                        newPosition = npc.Position + direction * MovementSpeed * elapsedSeconds;

                        // Update the lastUpdateTime
                        patrollastUpdateTime = currentTime;

                        patrolState = 6;
                        break;

                    case 6:
                        // Update npc position
                        npc.Position = newPosition;

                        patrolState = 7;
                        break;

                    case 7:
                        //Check distance to target if moving forward
                        float distance = Vector3.Distance(path[pathIndex], npc.Position);

                        if (distance < 0.1)
                        {
                            // increment the path index
                            pathIndex++;
                            if (pathIndex >= path.Count)
                            {
                                pathIndex = path.Count;
                                patrolState = 8;
                            }
                            else patrolState = 3;
                        }
                        else
                        {
                            patrolState = 3;
                        }

                        break;

                    case 8:
                        // set animation and speed
                        npc.Animation = 0;
                        MovementSpeed = 0;

                        // Delay
                        if (pauses[waypointIndex] > 0)
                        {
                            await Task.Delay(pauses[waypointIndex]); // Asynchronously wait for the pause duration
                        }

                        // check if target is the last patrol point or starting point
                        if (waypointIndex == waypoints.Count - 1)
                        {
                            //set patrol direction to backwards
                            patrolDirection = -1;

                            // increment/decrement targetIndex based on movement direction
                            waypointIndex += patrolDirection;
                            patrolState = 1;
                        }
                        else if (waypointIndex == 0)
                        {
                            patrolState = 0;
                        }
                        else
                        {
                            // increment/decrement targetIndex based on movement direction
                            waypointIndex += patrolDirection;
                            patrolState = 1;
                        }

                        break;

                    case 10:

                        npc.Animation = 0;
                        MovementSpeed = 0;

                        npc.isPatrolling = false;
                        patrolState = 0;
                        break;
                }
            }
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
                _npcPatrolData.Remove(npc);

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
