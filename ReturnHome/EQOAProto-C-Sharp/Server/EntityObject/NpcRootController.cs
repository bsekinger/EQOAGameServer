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
    public class NpcRootController : IDisposable
    {
        private Dictionary<Entity, (Vector3 OriginalPosition, byte OriginalFacing)> _npcRootData = new Dictionary<Entity, (Vector3, byte)>();
        private Dictionary<Entity, Task> _npcTasks = new Dictionary<Entity, Task>();
        private SemaphoreSlim _resourceSemaphore = new SemaphoreSlim(1, 1);

        private Dictionary<Entity, CancellationTokenSource> _npcCancellationTokens = new Dictionary<Entity, CancellationTokenSource>();
        private float _chaseSpeed = 10.0f;
        private float _returnSpeed = 20.0f;

        public NpcRootController()
        {
            
        }

        public async Task AddNpcAsync(int world, int zone, Entity player, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            byte npcFacing = npc.Facing;            

            await _resourceSemaphore.WaitAsync();
            try
            {
                _npcRootData[npc] = (npcPosition, npcFacing);
                if (!_npcTasks.ContainsKey(npc) || _npcTasks[npc].IsCompleted)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    _npcCancellationTokens[npc] = cancellationTokenSource;
                    var cancellationToken = cancellationTokenSource.Token;

                    _npcTasks[npc] = Task.Run(() => NpcRootStateMachineAsync(world, zone, player, npc, cancellationToken));
                }
            }
            finally
            {
                _resourceSemaphore.Release();
            }
        }


        private async Task NpcRootStateMachineAsync(int world, int zone, Entity player, Entity npc, CancellationToken cancellationToken)
        {
            List<Vector3> path = new List<Vector3>();
            float movementSpeed = 0;
            int rootState = 0;
            long lastUpdateTime = 0;
            bool isRooted = true;            

            while (isRooted && !cancellationToken.IsCancellationRequested)
            {
                switch (rootState)
                {
                    case 0:
                        Vector3 npcPos = npc.Position;
                        Vector3 playerPos = player.Position;

                        rootState = 1;
                        break;

                    case 1: 
                        lastUpdateTime = Environment.TickCount; // Record the start time

                        // Set animation and speed
                        npc.Animation = 0;
                        movementSpeed = 0;

                        rootState = 2;
                        break;

                    case 2:
                        // Simulate frame time asynchronously using Task.Delay
                        await Task.Delay(16);
                        rootState = 3;
                        break;

                    case 3:
                        // Update npc facing
                        npc.Facing = UpdateFacing(npc.Position, player.Position);

                        rootState = 4;
                        break;

                    case 4:
                        // Read player position
                        Vector3 playerPosition = player.Position;

                        rootState = 2;
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
                _npcRootData.Remove(npc);
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
