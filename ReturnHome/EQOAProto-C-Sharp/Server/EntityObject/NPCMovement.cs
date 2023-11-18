// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using ReturnHome.Server.EntityObject;
using ReturnHome.Server.EntityObject.EntityState;
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
        Chasing
    }

    class NPCMovement
    {
        private static Vector3 _playerPosition = new Vector3(0, 0, 0);
        private static Vector3 _npcPosition = new Vector3(0, 0, 0);
        private static float _roamSpeed = 4.0f;
        private static float _chaseSpeed = 10.0f;
        private static NpcState _state = NpcState.Idle;
        private static int _chaseState = 0;
        private static bool _isChasing = false;
        private static int _targetIndex = 0;
        private static float _movementSpeed = 0;
        private static float _elapsedMilliseconds = 0;
        private static float _elapsedSeconds = 0;
        private static Vector3 _newPosition = new Vector3(0, 0, 0);
        private static Vector3 _direction = new Vector3(0, 0, 0);
        private static float _distance = 0.0f;
        private System.Timers.Timer pathUpdateTimer;
        private List<Vector3> path = new List<Vector3>();
        private int _world = 0;
        private int _zone = 0;

        public async Task npcPatrolAsync(int world, int zone, Entity npc)
        {
            _npcPosition = npc.Position;
            List<Vector3> path = NavMeshManager.roam(world, zone, _npcPosition);
            foreach (var pathItem in path)
            {
                Console.WriteLine(pathItem.ToString());
            }

            _state = NpcState.Init;
            await NpcPatrolStateMachineAsync(path, npc);

            Console.WriteLine("NPC has completed the patrol.");
        }

        public void npcChase(int world, int zone, Entity player, Entity npc)
        {
            _isChasing = true;                      

            while (_isChasing)
            {
                switch (_chaseState)
                {
                    case 0:

                        _world = world;
                        _zone = zone;

                        // Read player and npc positions
                        _npcPosition = npc.Position;
                        _playerPosition = player.Position;

                        // Set chase state to 1
                        _chaseState = 1;
                        break;
                        
                    case 1:

                        // Timer to request a new path                        
                        if (pathUpdateTimer == null)
                        {
                            pathUpdateTimer = new System.Timers.Timer(100); // 5 seconds, adjust as needed
                            pathUpdateTimer.Elapsed += OnPathUpdate;
                            pathUpdateTimer.AutoReset = true; // If you want it to keep firing
                            pathUpdateTimer.Start();
                        }

                        //Call onPathUpdate once manually to get a path
                        OnPathUpdate(null, null);

                        //  Set animation and speed
                        npc.Animation = 3;
                        _movementSpeed = _chaseSpeed;

                        //  Set chase state to 2
                        _chaseState = 2;
                        break;

                    case 2:

                        // Initialize the index to the first point in the path
                        _targetIndex = 0;

                        // Set chaseState to 3
                        _chaseState = 3;
                        break;

                    case 3:

                        // Increment the index
                        _targetIndex++;

                        // Set chaseState to 4
                        _chaseState = 4;
                        break;

                    case 4:

                        // Calculate the direction vector from NPC to the target point
                        _direction = Vector3.Normalize(_playerPosition - _npcPosition);

                        // Update npc facing
                        npc.Facing = UpdateFacing(_npcPosition, _playerPosition);

                        // Set chaseState to 5
                        _chaseState = 5;
                        break;

                    case 5:

                        // Calculate the new position based on constant speed and elapsed time
                        _elapsedMilliseconds = pathUpdateTimer.ElapsedMilliseconds;
                        _elapsedSeconds = _elapsedMilliseconds / 1000.0f;
                        _newPosition = _npcPosition + _direction * _movementSpeed * _elapsedSeconds;

                        // Check if newPosition contains NaN values
                        if (float.IsNaN(_newPosition.X) || float.IsNaN(_newPosition.Y) || float.IsNaN(_newPosition.Z))
                        {
                            // Handle the NaN values (e.g., skip this iteration)
                            Console.WriteLine("Invalid newPosition vector, skipping iteration.");
                            continue;
                        }

                        // Set chaseState to 6
                        _chaseState = 6;
                        break;

                    case 6:
                        //Update npc position
                        npc.Position = _newPosition;

                        //Set chaseState to 7
                        _chaseState = 7;
                        break;

                    case 7:
                        // Read player and npc positions
                        _npcPosition = npc.Position;
                        _playerPosition = player.Position;

                        //Check distance between npc and player
                        _distance = Vector3.Distance(_playerPosition, _npcPosition);
                        if (_distance < 1.0)
                        {
                            //Set chaseState to 10
                            _chaseState = 10;
                            break;
                        }

                        //Set chaseState to 3
                        _chaseState = 3;
                        break;

                    case 10:
                        if (pathUpdateTimer != null)
                        {
                            pathUpdateTimer.Stop();
                            pathUpdateTimer.Dispose();
                            pathUpdateTimer = null;
                        }
                        npc.Animation = 0;
                        _movementSpeed = 0;
                        //set npc is chasing to false here
                        break;
                }
                
            }
        }

        private void OnPathUpdate(object source, System.Timers.ElapsedEventArgs e)
        {
            path = NavMeshManager.path(_world, _zone, _npcPosition, _playerPosition);
            _chaseState = 2;
        }



        private async Task NpcPatrolStateMachineAsync(List<Vector3> path, Entity npc)
        {
            int currentIndex = 0;
            float t = 0f; // Parameter for interpolation
            int direction = 1; // Direction of movement, 1 for forward, -1 for backward

            while (true)
            {
                switch (_state)
                {
                    case NpcState.Idle:
                        npc.Animation = 0;
                        break;

                    case NpcState.Init:
                        t = 0f; // Reset t when initializing the chase behavior
                        _state = NpcState.UpdateFacing;
                        break;

                    case NpcState.UpdateFacing:
                        // Call updateFacing function before starting to move                        
                        if (direction == 1)
                        {
                            npc.Facing = UpdateFacing(path[currentIndex], path[currentIndex + 1]);
                            _state = NpcState.MovingForward;
                        }
                        if (direction == -1)
                        {
                            npc.Facing = UpdateFacing(path[currentIndex], path[currentIndex - 1]);
                            _state = NpcState.MovingBackward;
                        }
                        Console.WriteLine($"NPC updating facing to: " + npc.Facing);
                        break;

                    case NpcState.MovingForward:
                        // Set movement speed
                        float movementSpeed = _roamSpeed;
                        npc.Animation = 1;

                        // Call the Smoothstep function to interpolate the NPC's position
                        Vector3 smoothstepValue = Smoothstep(path[currentIndex], path[currentIndex + 1], ref t, movementSpeed * 0.001f);
                        npc.Position = smoothstepValue;

                        // Simulate movement by printing the interpolated position
                        Console.WriteLine($"NPC Position: {npc.Position.ToString()}");

                        // Check if the interpolation is complete for the current segment
                        if (t >= 1.0f)
                        {
                            currentIndex += direction; // Move to the next segment of the path
                            t = 0f; // Reset the interpolation parameter

                            // Check if the NPC has reached the end of the path
                            if (currentIndex >= path.Count - 1)
                            {
                                direction = -1; // Change direction to move backward
                                _state = NpcState.UpdateFacing;
                            }
                        }
                        break;

                    case NpcState.MovingBackward:
                        // Set movement speed
                        npc.Animation = 1;
                        movementSpeed = _roamSpeed;

                        // Call the Smoothstep function to interpolate the NPC's position backward
                        Vector3 smoothstepValueBackward = Smoothstep(path[currentIndex], path[currentIndex - 1], ref t, movementSpeed * 0.01f);
                        npc.Position = smoothstepValueBackward;

                        // Simulate movement by printing the interpolated position
                        Console.WriteLine($"NPC Position (Backward): {npc.Position.ToString()}");

                        // Check if the interpolation is complete for the current segment
                        if (t >= 1.0f)
                        {
                            currentIndex -= direction; // Move to the previous segment of the path
                            t = 0f; // Reset the interpolation parameter

                            // Check if the NPC has reached the starting point of the path
                            if (currentIndex <= 0)
                            {
                                direction = 1; // Change direction to move forward
                                _state = NpcState.UpdateFacing;
                            }
                        }
                        break;
                }
                // Simulate frame time asynchronously using Task.Delay
                await Task.Delay(16);
            }
        }


        // Modified Smoothstep function that controls speed
        private static Vector3 Smoothstep(Vector3 start, Vector3 end, ref float t, float speed)
        {
            t = Math.Clamp(t + 0.01f * speed, 0.0f, 1.0f);
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

        // Placeholder for the updateFacing function
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

//            while (true)
//{
//    // Calculate the path at each iteration to follow the player's movement
//    List<Vector3> path = NavMeshManager.path(world, zone, _npcPosition, _playerPosition);

//    if (path.Count == 0)
//    {
//        // No valid path found, handle this situation as needed
//        Console.WriteLine("No valid path found.");
//        return;
//    }
//    else
//    {
//        foreach (var pathItem in path)
//        {
//            Console.WriteLine($"PathItem before loop: {pathItem.ToString()}");
//        }
//    }

//    // Check if it's time to update the path (every 100ms) and if the NPC is not within 2.0f of the player
//    if (pathUpdateTimer.ElapsedMilliseconds >= 100 && Vector3.Distance(_npcPosition, player.Position) >= 2.0f)
//    {
//        // Request a new path
//        path = NavMeshManager.path(world, zone, _npcPosition, _playerPosition);

//        if (path.Count == 0)
//        {
//            // No valid path found, handle this situation as needed
//            Console.WriteLine("No valid path found.");
//            return;
//        }
//        else
//        {
//            foreach (var pathItem in path)
//            {
//                Console.WriteLine($"PathItem inside Loop{pathItem.ToString()}");
//            }
//        }

//        // Reset the timer
//        pathUpdateTimer.Restart();

//        // Reset the target index to the beginning of the path
//        targetIndex = 0;
//    }

//    // Ensure the target index is within bounds
//    if (targetIndex < 0)
//        targetIndex = 0;
//    else if (targetIndex >= path.Count)
//        targetIndex = path.Count - 1;

//    // Get the current target point from the path
//    Vector3 targetPoint = path[targetIndex];

//    // Calculate the direction vector from NPC to the target point
//    Vector3 direction = Vector3.Normalize(targetPoint - _npcPosition);

//    // Check if direction contains NaN values
//    if (float.IsNaN(direction.X) || float.IsNaN(direction.Y) || float.IsNaN(direction.Z))
//    {
//        // Handle the NaN values (e.g., skip this iteration)
//        Console.WriteLine("Invalid direction vector, skipping iteration.");
//        continue;
//    }

//    // Set movement speed
//    npc.Animation = 3;
//    float movementSpeed = _chaseSpeed;

//    // Calculate the new position based on constant speed and elapsed time
//    float elapsedMilliseconds = pathUpdateTimer.ElapsedMilliseconds;
//    float elapsedSeconds = elapsedMilliseconds / 1000.0f;
//    Vector3 newPosition = _npcPosition + direction * movementSpeed * elapsedSeconds;

//    // Check if newPosition contains NaN values
//    if (float.IsNaN(newPosition.X) || float.IsNaN(newPosition.Y) || float.IsNaN(newPosition.Z))
//    {
//        // Handle the NaN values (e.g., skip this iteration)
//        Console.WriteLine("Invalid newPosition vector, skipping iteration.");
//        continue;
//    }

//    // Simulate movement by printing the new position
//    Console.WriteLine($"NPC Position (Chasing): {newPosition.ToString()}");

//    // Check if the NPC is close enough to the target point to move to the next point
//    float distanceToTarget = Vector3.Distance(newPosition, targetPoint);
//    npc.Facing = UpdateFacing(newPosition, targetPoint);
//    if (distanceToTarget < 0.1f) // Adjust this threshold as needed
//    {
//        // Transition to the next path point
//        targetIndex++;
//    }

//    // Update the NPC's position
//    npc.Position = newPosition;

//    // Reset the timer for the next frame
//    pathUpdateTimer.Restart();
//}

//Console.WriteLine("NPC has completed the chase.");
//        }
