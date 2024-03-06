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
            int pathIndex = 0;
            Vector3 direction = new Vector3();
            Vector3 newPosition = new Vector3();
            long patrollastUpdateTime = 0;
            int patrolDirection = 1;
            bool isPatrolling = true;
            sbyte velocityX = 0;
            sbyte velocityY = 0;
            sbyte velocityZ = 0;

            // Ensure waypoints and pauses lists are of the same length
            if (waypoints.Count != pauses.Count)
            {
                throw new InvalidOperationException("Waypoints and pauses lists must be of the same length.");
            }

            var (completePath, completePauses) = CalculateCompletePathAndPauses(world, zone, waypoints, pauses);

            while (isPatrolling && !cancellationToken.IsCancellationRequested)
            {
                switch (patrolState)
                {
                    case 0: // use this case to init the patrol only
                        pathIndex = 0;
                        patrolDirection = 1;

                        // update facing
                        npc.Facing = UpdateFacing(npc.Position, completePath[pathIndex + 1]);

                        // Delay
                        if (completePauses[pathIndex] > 0)
                        {
                            await Task.Delay(completePauses[pathIndex]);
                        }

                        patrolState = 1;
                        break;

                    case 1:                        
                        pathIndex++;

                        patrolState = 2;
                        break;

                    case 2:
                        // set animation and speed
                        npc.Animation = 1;
                        MovementSpeed = _roamSpeed;

                        patrolState = 3;
                        break;

                    case 3:
                        // Record start time
                        patrollastUpdateTime = Environment.TickCount;

                        // Simluate a 60 fps frame rate
                        await Task.Delay(16);

                        patrolState = 4;
                        break;

                    case 4:
                        // Calculate direction vector
                        direction = Vector3.Normalize(completePath[pathIndex] - npc.Position);

                        // update facing
                        npc.Facing = UpdateFacing(npc.Position, completePath[pathIndex]);

                        patrolState = 5;
                        break;

                    case 5:
                        // Calculate the elapsed time since the last frame
                        long currentTime = Environment.TickCount;
                        long deltaTime = currentTime - patrollastUpdateTime;
                        
                        float elapsedSeconds = deltaTime / 1000.0f;                        

                        // Specify the maximum movement speed
                        float maxSpeed = 20.0f;

                        // Calculate the normalized movement speed
                        float normalizedSpeed = MovementSpeed / maxSpeed;

                        // Calculate the displacement
                        Vector3 displacement = direction * normalizedSpeed * elapsedSeconds;

                        // Calculate the maximum possible displacement within the given time frame
                        float maxDisplacement = normalizedSpeed * elapsedSeconds;

                        // Calculate velocity components
                        velocityX = (sbyte)(Math.Min(1.0f, Math.Abs(displacement.X / maxDisplacement)) * Math.Sign(displacement.X) * normalizedSpeed * sbyte.MaxValue);
                        velocityY = (sbyte)(Math.Min(1.0f, Math.Abs(displacement.Y / maxDisplacement)) * Math.Sign(displacement.Y) * normalizedSpeed * sbyte.MaxValue);
                        velocityZ = (sbyte)(Math.Min(1.0f, Math.Abs(displacement.Z / maxDisplacement)) * Math.Sign(displacement.Z) * normalizedSpeed * sbyte.MaxValue);

                        // Calculate the new position based on the elapsed time
                        newPosition = npc.Position + direction * MovementSpeed * elapsedSeconds;

                        // Update the lastUpdateTime
                        patrollastUpdateTime = currentTime;

                        patrolState = 6;
                        break;

                    case 6:
                        // Update npc position
                        npc.Position = newPosition;
                        npc.VelocityX = (ushort)velocityX;
                        npc.VelocityY = (ushort)velocityY;
                        npc.VelocityZ = (ushort)velocityZ;

                        patrolState = 7;
                        break;

                    case 7:
                        //Check distance to target if moving forward
                        float distance = Vector3.Distance(completePath[pathIndex], npc.Position);

                        if (distance < 0.1)
                        {
                            patrolState = 8;                            
                        }
                        else
                        {
                            patrolState = 3;
                        }

                        break;

                    case 8:
                        // Delay
                        if (completePauses[pathIndex] > 0)
                        {
                            npc.Animation = 0;
                            MovementSpeed = 0;
                            await Task.Delay(completePauses[pathIndex]);
                        }

                        // Check if the target is the last patrol point or starting point
                        if (pathIndex == completePath.Count - 1)
                        {
                            // Set patrol direction to backwards
                            patrolDirection = -1;
                        }
                        else if (pathIndex == 0)
                        {
                            // Set patrol direction to forward
                            patrolDirection = 1;
                        }

                        // Adjust pathIndex based on patrolDirection
                        pathIndex += patrolDirection;

                        // Reset patrolState to reinitialize the patrol
                        patrolState = 2;
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

        private (List<Vector3> completePath, List<int> completePauses) CalculateCompletePathAndPauses(int world, int zone, List<Vector3> waypoints, List<int> pauses)
        {
            List<Vector3> completePath = new List<Vector3>();
            List<int> completePauses = new List<int>();

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                // Calculate path between waypoints[i] and waypoints[i + 1]
                List<Vector3> pathSegment = NavMeshManager.smoothPath(world, zone, waypoints[i], waypoints[i + 1]);

                // For the path segment, add points and pauses
                for (int j = 0; j < pathSegment.Count; j++)
                {
                    completePath.Add(pathSegment[j]);
                    // Add pause only if it's not the last point of the last segment
                    if (!(i == waypoints.Count - 2 && j == pathSegment.Count - 1))
                    {
                        completePauses.Add(j == 0 ? pauses[i] : 0); // Add the pause for the starting point of the segment
                    }
                }
            }

            // Add the pause for the last waypoint
            completePauses.Add(pauses[pauses.Count - 1]);

            return (completePath, completePauses);
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

                // Reset NPC's state to its original position and animation
                npc.Position = _npcPatrolData[npc].OriginalPosition;
                npc.Animation = 0;
                npc.VelocityX = 0;
                npc.VelocityY = 0;
                npc.VelocityZ = 0;
            }
            finally
            {
                _resourceSemaphore.Release();
            }
        }

        public async Task RemoveNpcsAsync(int world, int zone)
        {
            await _resourceSemaphore.WaitAsync();
            try
            {                
                List<Entity> npcsToRemove = new List<Entity>();

                foreach (var npcEntry in _npcPatrolData)
                {
                    var npc = npcEntry.Key;
                    if ((int)npc.World == world && npc.Zone == zone)
                    {
                        npcsToRemove.Add(npc);
                    }
                }

                foreach (var npc in npcsToRemove)
                {
                    if (_npcCancellationTokens.TryGetValue(npc, out var cancellationTokenSource))
                    {
                        cancellationTokenSource.Cancel();
                        cancellationTokenSource.Dispose();
                        _npcCancellationTokens.Remove(npc);
                    }

                    if (_npcTasks.ContainsKey(npc))
                    {
                        _npcTasks.Remove(npc);
                    }

                    // Reset NPC's state to its original position and animation
                    npc.Position = _npcPatrolData[npc].OriginalPosition;
                    npc.Animation = 0;
                    npc.VelocityX = 0;
                    npc.VelocityY = 0;
                    npc.VelocityZ = 0;

                    // Finally, remove the NPC from _npcRoamData
                    _npcPatrolData.Remove(npc);
                }
            }
            finally
            {
                _resourceSemaphore.Release();
            }

            Console.WriteLine($"NPCs removed from patrolling in World {world}, Zone {zone}.");
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
