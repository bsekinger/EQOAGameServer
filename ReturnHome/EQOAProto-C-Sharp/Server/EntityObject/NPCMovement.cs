using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ReturnHome.Server.Managers;


namespace ReturnHome.Server.EntityObject
{
    enum NpcState
    {
        Idle,
        Init,
        MovingForward,
        MovingBackward,
        UpdateFacing,
        Chasing,
        Done
    }

    public class NPCMovement
    {
        private readonly object _syncLock = new object();
        private readonly Dictionary<Entity, Vector3> _npcPositionsRoam = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Task> _npcTasksRoam = new Dictionary<Entity, Task>();
        private readonly SemaphoreSlim _resourceLockRoam = new SemaphoreSlim(1, 1); // Async-compatible lock

        private readonly Dictionary<Entity, Vector3> _npcPositionsChase = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Vector3> _playerPositionsChase = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Task> _npcTasksChase = new Dictionary<Entity, Task>();
        private readonly SemaphoreSlim _resourceLockChase = new SemaphoreSlim(1, 1); // Async-compatible lock

        private static float _roamSpeed = 4.0f;
        private static float _chaseSpeed = 10.0f;

        public async Task npcRoamAsync(int world, int zone, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            List<Vector3> path = NavMeshManager.roam(world, zone, npcPosition);

            await _resourceLockRoam.WaitAsync(); // Acquire the async lock to manage resources

            try
            {
                _npcPositionsRoam[npc] = npcPosition; // Maintain a dictionary of NPC positions
                if (!_npcTasksRoam.ContainsKey(npc) || _npcTasksRoam[npc].IsCompleted)
                {
                    _npcTasksRoam[npc] = Task.Run(() => NpcRoamStateMachineAsync(path, npc)); // Maintain a dictionary of NPC tasks
                }
            }
            finally
            {
                _resourceLockRoam.Release(); // Release the async lock when done
            }

            // Wait for the specific NPC's task to finish
            await _npcTasksRoam[npc];

            await _resourceLockRoam.WaitAsync(); // Acquire the async lock to manage resources again

            try
            {
                _npcTasksRoam.Remove(npc); // Remove the completed task
                _npcPositionsRoam.Remove(npc); // Remove the NPC's position
            }
            finally
            {
                _resourceLockRoam.Release(); // Release the async lock when done
            }
        }
         private async Task NpcRoamStateMachineAsync(List<Vector3> path, Entity npc)
        {
            int currentIndex = 0;
            float t = 0f; // Parameter for interpolation
            int direction = 1; // Direction of movement, 1 for forward, -1 for backward

            NpcState state = NpcState.Init;

            while (state != NpcState.Done)
            {
                switch (state)
                {
                    case NpcState.Idle:
                        npc.Animation = 0;
                        break;

                    case NpcState.Init:
                        t = 0f;
                        state = NpcState.UpdateFacing;
                        break;

                    case NpcState.UpdateFacing:
                        // Call updateFacing function before starting to move                        
                        if (direction == 1)
                        {
                            npc.Facing = UpdateFacing(path[currentIndex], path[currentIndex + 1]);
                            state = NpcState.MovingForward;
                        }
                        if (direction == -1)
                        {
                            npc.Facing = UpdateFacing(path[currentIndex], path[currentIndex - 1]);
                            state = NpcState.MovingBackward;
                        }
                        //Console.WriteLine($"NPC updating facing to: " + npc.Facing);
                        break;

                    case NpcState.MovingForward:
                        // Set movement speed
                        float movementSpeed = _roamSpeed;
                        npc.Animation = 1;

                        // Call the Smoothstep function to interpolate the NPC's position
                        Vector3 smoothstepValue = Smoothstep(path[currentIndex], path[currentIndex + 1], ref t, movementSpeed * 0.040f);
                        npc.Position = smoothstepValue;
                        
                        //Console.WriteLine($"NPC Position: {npc.Position.ToString()}");

                        // Check if the interpolation is complete for the current segment
                        if (t >= 1.0f)
                        {
                            //Console.WriteLine("Reached end of path");
                            currentIndex += direction; // Move to the next segment of the path
                            t = 0f; // Reset the interpolation parameter

                            // Check if the NPC has reached the end of the path
                            if (currentIndex >= path.Count - 1)
                            {
                                direction = -1; // Change direction to move backward
                                state = NpcState.UpdateFacing;
                            }
                        }
                        break;

                    case NpcState.MovingBackward:
                        // Set movement speed
                        npc.Animation = 1;
                        movementSpeed = _roamSpeed;

                        // Call the Smoothstep function to interpolate the NPC's position backward
                        Vector3 smoothstepValueBackward = Smoothstep(path[currentIndex], path[currentIndex - 1], ref t, movementSpeed * 0.040f);
                        npc.Position = smoothstepValueBackward;
                        
                        //Console.WriteLine($"NPC Poition (Backward): {npc.Position.ToString()}");
                        //Console.WriteLine($"Index: {currentIndex}");

                        // Check if the interpolation is complete for the current segment
                        if (t >= 1.0f)
                        {
                            //Console.WriteLine("Reached beginning of path");
                            currentIndex += direction; // Move to the previous segment of the path
                            t = 0f; // Reset the interpolation parameter

                            // Check if the NPC has reached the starting point of the path
                            if (currentIndex <= 0)
                            {
                                direction = 1; // Change direction to move forward
                                state = NpcState.UpdateFacing;
                            }
                        }
                        break;

                    case NpcState.Done:
                        // When an NPC is done, exit the loop.
                        break;
                }

                // Simulate frame time asynchronously using Task.Delay
                await Task.Delay(16);
            }
        }

        public async Task npcChaseAsync(int world, int zone, Entity player, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            Vector3 playerPosition = player.Position;
            //Console.WriteLine($"Made it to npcChaseAsync with: {npc.ServerID}");

            await _resourceLockChase.WaitAsync(); // Acquire the async lock to manage resources

            try
            {
                _npcPositionsChase[npc] = npcPosition;
                _playerPositionsChase[player] = playerPosition;

                if (!_npcTasksChase.ContainsKey(npc) || _npcTasksChase[npc].IsCompleted)
                {
                    _npcTasksChase[npc] = Task.Run(() => npcChaseStateMachineAsync(world, zone, player, npc));
                }
            }
            finally
            {
                _resourceLockChase.Release(); // Release the async lock when done
            }

            // Wait for the specific NPC's task to finish
            await _npcTasksChase[npc];

            await _resourceLockChase.WaitAsync(); // Acquire the async lock to manage resources

            try
            {
                _npcPositionsChase.Remove(npc);
                _playerPositionsChase.Remove(player);
            }
            finally
            {
                _resourceLockChase.Release(); // Release the async lock when done
            }
        }

        public async Task npcChaseStateMachineAsync(int world, int zone, Entity player, Entity npc)
        {
            List<Vector3> path = new List<Vector3>();
            float movementSpeed = 0;
            int chaseState = 0;
            int targetIndex = 1;
            long lastUpdateTime = 0;
            Vector3 direction = new Vector3();
            Vector3 newPosition = new Vector3();
            object timerLock = new object();

            System.Timers.Timer pathUpdateTimer = new System.Timers.Timer(200);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            //Console.WriteLine($"Made it to npcChaseStateMachineAsync with: {npc.ServerID}");

            while (npc.isChasing)
            {
                //Console.WriteLine($"Chasing State: {chaseState} with {npc.ServerID}");
                switch (chaseState)
                {
                    case 0:
                        // Read player and npc positions
                        Vector3 npcPos = npc.Position;
                        Vector3 playerPos = player.Position;

                        chaseState = 1;
                        break;

                    case 1:
                        // Timer to request a new path
                        lock (timerLock)
                        {                            
                            pathUpdateTimer.Elapsed += (sender, args) =>
                            {
                                var updatedPath = OnPathUpdate(world, zone, npc.Position, player.Position, ref chaseState);
                                if (updatedPath != null)
                                {
                                    path = updatedPath;
                                }
                            };
                            pathUpdateTimer.AutoReset = true;
                            pathUpdateTimer.Start();
                        }

                        lastUpdateTime = Environment.TickCount; // Record the start time

                        //Call onPathUpdate once manually to get a path
                        var initialPath = OnPathUpdate(world, zone, npc.Position, player.Position, ref chaseState);
                        if (initialPath != null)
                        {
                            path = initialPath;
                        }

                        // Set animation and speed
                        npc.Animation = 3;
                        movementSpeed = _chaseSpeed;

                        chaseState = 2;
                        break;

                    case 2:
                        // Initialize the index to the first point in the path
                        targetIndex = 1;

                        chaseState = 3;
                        break;

                    case 3:                        
                        // Simulate frame time asynchronously using Task.Delay
                        await Task.Delay(16);

                        chaseState = 4;
                        break;

                    case 4:
                        if (path == null)
                        {
                            chaseState = 10;
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
                        Console.WriteLine($"Delta Time (ms): {deltaTime}");

                        // Calculate the new position based on the elapsed time
                        float elapsedSeconds = deltaTime / 1000.0f; // Convert to seconds
                        newPosition = npc.Position + direction * movementSpeed * elapsedSeconds;

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

                        // Check distance between npc and player
                        float distance = Vector3.Distance(playerPosition, npcPosition);
                        if (distance < 1.0)
                        {
                            chaseState = 10;
                            break;
                        }

                        chaseState = 3;
                        break;

                    case 10:
                        
                        pathUpdateTimer.Stop();
                        pathUpdateTimer.Dispose();
                        pathUpdateTimer = null;
                                               
                        npc.Animation = 0;
                        movementSpeed = 0;

                        // Set npc is chasing to false here
                        npc.isChasing = false;
                        chaseState = 0;
                        Console.WriteLine("Chasing stopped!");
                        return;
                }
            }
        }

        private List<Vector3> OnPathUpdate(int world, int zone, Vector3 npcPosition, Vector3 playerPosition, ref int chaseState)
        {
            List<Vector3> path = NavMeshManager.path(world, zone, npcPosition, playerPosition);

            // Handle no path error here
            if (path == null)
            {
                chaseState = 10;
            }

            chaseState = 2;

            return path;
        }

        //------------------------------------------------------------------------------------------------------------------------------------------//
        //                                                                                                                                          //
        //                                                   Common functions                                                                       //
        //                                                                                                                                          //
        //------------------------------------------------------------------------------------------------------------------------------------------//

        // Smoothstep function that controls roaming speed
        private static Vector3 Smoothstep(Vector3 start, Vector3 end, ref float t, float speed)
        {
            t = Math.Clamp(t + 0.010f * speed, 0.0f, 1.0f);
            float smoothstepValueX = Lerp(start.X, end.X, t);
            float smoothstepValueY = Lerp(start.Y, end.Y, t);
            float smoothstepValueZ = Lerp(start.Z, end.Z, t);
            return new Vector3(smoothstepValueX, smoothstepValueY, smoothstepValueZ);
        }

         // Linear interpolation function for single values
        private static float Lerp(float start, float end, float t)
        {
            t = Math.Clamp(t, 0.0f, 1.0f);
            return start + t * (end - start);
        }

        private static byte UpdateFacing(Vector3 start, Vector3 end)
        {
            double angle = CalculateAngle(start, end);
            byte facing = TransformToByte(angle);
            
            //Console.WriteLine("NPC is updating its facing direction.");
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
