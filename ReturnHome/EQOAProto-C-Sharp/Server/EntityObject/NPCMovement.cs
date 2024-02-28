using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ReturnHome.Server.Managers;
using NLua;
using System.IO;
using System.Xml;
using System.Security.Policy;


namespace ReturnHome.Server.EntityObject
{
    public class NPCMovement
    {
        private readonly object _syncLock = new object();
        private readonly Dictionary<Entity, Vector3> _npcPositionsRoam = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Task> _npcTasksRoam = new Dictionary<Entity, Task>();
        private readonly SemaphoreSlim _resourceLockRoam = new SemaphoreSlim(1, 1); 

        private readonly Dictionary<Entity, Vector3> _npcPositionsChase = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Vector3> _playerPositionsChase = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Task> _npcTasksChase = new Dictionary<Entity, Task>();
        private readonly SemaphoreSlim _resourceLockChase = new SemaphoreSlim(1, 1);

        private readonly Dictionary<Entity, Vector3> _npcPositionsPatrol = new Dictionary<Entity, Vector3>();
        private readonly Dictionary<Entity, Task> _npcTasksPatrol = new Dictionary<Entity, Task>();
        private readonly SemaphoreSlim _resourceLockPatrol = new SemaphoreSlim(1, 1);

        private static float _roamSpeed = 2.0f;
        private static float _chaseSpeed = 10.0f;
        private static float _returnSpeed = 20.0f;

        public async Task npcPatrolAsync(int world, int zone, Entity npc)
        {
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

            Vector3 npcPosition = npc.Position;

            await _resourceLockPatrol.WaitAsync(); // Acquire the async lock to manage resources

            try
            {
                _npcPositionsPatrol[npc] = npcPosition; // Maintain a dictionary of NPC positions
                if (!_npcTasksPatrol.ContainsKey(npc) || _npcTasksPatrol[npc].IsCompleted)
                {
                    _npcTasksPatrol[npc] = Task.Run(() => NpcPatrolStateMachineAsync(world, zone, npc, waypoints, pauses)); // Maintain a dictionary of NPC tasks
                }
            }
            finally
            {
                _resourceLockPatrol.Release(); // Release the async lock when done
            }

            // Wait for the specific NPC's task to finish
            await _npcTasksPatrol[npc];

            await _resourceLockPatrol.WaitAsync(); // Acquire the async lock to manage resources again

            try
            {
                _npcTasksPatrol.Remove(npc); // Remove the completed task
                _npcPositionsPatrol.Remove(npc); // Remove the NPC's position
            }
            finally
            {
                _resourceLockPatrol.Release(); // Release the async lock when done
            }

        }

        private async Task NpcPatrolStateMachineAsync(int world, int zone, Entity npc, List<Vector3> waypoints, List<int> pauses)
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

            // Ensure waypoints and pauses lists are of the same length
            if (waypoints.Count != pauses.Count)
            {
                throw new InvalidOperationException("Waypoints and pauses lists must be of the same length.");
            }

            while (npc.isPatrolling)
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
                        path = NavMeshManager.path(world, zone, npc.Position, waypoints[waypointIndex]);
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

        public async Task npcRoamAsync(int world, int zone, Entity npc)
        {
            Vector3 npcPosition = npc.Position;            

            await _resourceLockRoam.WaitAsync(); // Acquire the async lock to manage resources

            try
            {
                _npcPositionsRoam[npc] = npcPosition; // Maintain a dictionary of NPC positions
                if (!_npcTasksRoam.ContainsKey(npc) || _npcTasksRoam[npc].IsCompleted)
                {
                    _npcTasksRoam[npc] = Task.Run(() => NpcRoamStateMachineAsync(world, zone, npc)); // Maintain a dictionary of NPC tasks
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
         private async Task NpcRoamStateMachineAsync(int world, int zone, Entity npc)
        {
            List<Vector3> path = new List<Vector3>();
            List<Vector3> updatedPath = new List<Vector3>();
            float MovementSpeed = 0;
            int roamState = 0;
            int targetIndex = 1;
            long lastUpdateTime = 0;
            Vector3 direction = new Vector3();
            Vector3 originalPosition = new Vector3();
            Vector3 newPosition = new Vector3();
            Vector3 rndPoint = new Vector3();
            object timerLock = new object();
            long roamlastUpdateTime = 0;
            int roamDirection = 1; // Direction of movement, 1 for forward, -1 for backward

            System.Timers.Timer pathUpdateTimer = new System.Timers.Timer(500);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            while (npc.isRoaming)
            {
                switch (roamState)
                {
                    case 0:
                        // Read npc position
                        originalPosition = npc.Position;
                        // Get random point
                        rndPoint = Vector3.Zero;
                        rndPoint = NavMeshManager.point(world, zone, npc.Position, 40.0f);
                        // Console.WriteLine($"Our random point is: {rndPoint.ToString// Flag to indicate if the first path has been received

                        if (rndPoint == Vector3.Zero)
                        {
                            // Console.WriteLine("Could not find random point.");
                            roamState = 10;
                            break;
                        }
                        else
                        {
                            roamState = 1;
                            break;
                        }                        

                    case 1:
                        // Record start time
                        roamlastUpdateTime = Environment.TickCount;
                        
                        bool firstPathReceived = false;

                        // Timer to request a new path
                        lock (timerLock)
                        {
                            pathUpdateTimer.Elapsed += (sender, args) =>
                            {
                                if (roamDirection == 1)
                                {
                                    updatedPath = OnPathUpdate(world, zone, npc.Position, rndPoint);
                                }
                                else
                                {
                                    updatedPath = OnPathUpdate(world, zone, npc.Position, originalPosition);
                                }
                                
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
                                    roamState = 10; // Set chaseState to 10 to stop chasing
                                    return;
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

                        roamState = 2; // Proceed to the next state
                        break;

                    case 2:
                        // Set animation and speed
                        npc.Animation = 1;
                        npc.Movement = 1;
                        npc.VelocityY = 14839;
                        MovementSpeed = _roamSpeed;

                        // Set index to the first point in the path
                        // targetIndex = 1;

                        // Set roaming direction to forward (1)
                        // roamDirection = 1;

                        roamState = 3;
                        break;                        

                    case 3:
                        // Simluate a 60 fps frame rate
                        await Task.Delay(16);

                        roamState = 4;
                        break;                        

                    case 4:
                        // Calculate direction vector
                        direction = Vector3.Normalize(path[targetIndex] - npc.Position);
                        //Console.WriteLine($"direction: {direction.ToString()}");

                        npc.Facing = UpdateFacing(npc.Position, path[path.Count - 1]);
                        //Console.WriteLine($"facing: {npc.Facing}");
                        roamState = 5;                        

                        break;                        

                    case 5:
                        // Calculate the elapsed time since the last frame
                        long currentTime = Environment.TickCount;
                        long deltaTime = currentTime - roamlastUpdateTime;
                        // Console.WriteLine($"deltaTime: {deltaTime}");

                        // Calculate the new position based on the elapsed time
                        float elapsedSeconds = deltaTime / 1000.0f;
                        newPosition = npc.Position + direction * MovementSpeed * elapsedSeconds;
                        // Console.WriteLine($"newPosition: {newPosition.ToString()}");

                        // Update the lastUpdateTime
                        roamlastUpdateTime = currentTime;

                        roamState = 6;
                        break;

                    case 6:
                        // Update npc position
                        npc.Position = newPosition;

                        roamState = 7;
                        break;

                    case 7:
                        //Check distance to target if moving forward
                        float distance = Vector3.Distance(path[targetIndex], npc.Position);
                        //Console.WriteLine($"distance remaining: {distance}");

                        if (distance < 1.0)
                        {
                            targetIndex += 1;
                            // Console.WriteLine($"Target Index: {targetIndex}, Path count: {path.Count}");

                            if (targetIndex >= path.Count)
                            {
                                roamDirection *= -1; // flip state
                                pathUpdateTimer.Stop();

                                npc.Animation = 0;
                                npc.Movement = 0;
                                MovementSpeed = 0;

                                npc.VelocityX = 0;
                                npc.VelocityY = 0;
                                npc.VelocityZ = 0;
                                roamState = 1;
                                break;
                            }
                        }
                              
                        roamState = 3;
                        break;

                    case 10:

                        pathUpdateTimer.Stop();
                        pathUpdateTimer.Dispose();
                        pathUpdateTimer = null;

                        npc.Animation = 0;
                        npc.Movement = 0;
                        MovementSpeed = 0;

                        npc.VelocityX = 0;
                        npc.VelocityY = 0;
                        npc.VelocityZ = 0;                        
                  
                        npc.isRoaming = false;
                        roamState = 0;
                        break;                        
                }
            }
        }

        public async Task npcChaseAsync(int world, int zone, Entity player, Entity npc)
        {
            Vector3 npcPosition = npc.Position;
            Vector3 playerPosition = player.Position;

            await _resourceLockChase.WaitAsync();

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

        private async Task npcChaseStateMachineAsync(int world, int zone, Entity player, Entity npc)
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

            System.Timers.Timer pathUpdateTimer = new System.Timers.Timer(200);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            Vector3 originalPosition = npc.Position; // Store the original position of the NPC
            byte originalFacing = npc.Facing;

            while (npc.isChasing)
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
                                    chaseState = 10; // Set chaseState to 10 to stop chasing
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
                        npc.Movement = 1;
                        npc.VelocityY = 14839;
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
                        // Check for no path or targetIndex exceeds path count
                        if (path is null || path.Count == 0 || targetIndex >= path.Count)
                        {
                            chaseState = 10; // Stop if the path is empty or finished
                            break;
                        }

                        // Check if the NPC has moved too far from its original position
                        if (Vector3.Distance(npc.Position, originalPosition) > chaseDistance)
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

                    case 10:

                        pathUpdateTimer.Stop();
                        pathUpdateTimer.Dispose();
                        pathUpdateTimer = null;

                        npc.Animation = 0;
                        npc.Movement = 0;
                        movementSpeed = 0;

                        npc.VelocityX = 0;
                        npc.VelocityY = 0;
                        npc.VelocityZ = 0;

                        // Set npc is chasing to false here
                        npc.isChasing = false;
                        // npc.Position = originalPosition;
                        // npc.Facing = originalFacing;
                        chaseState = 0;
                        // Console.WriteLine("Chasing stopped!");
                        break;

                    case 11:
                        pathUpdateTimer.Stop();
                        /// Update npc facing
                        npc.Facing = UpdateFacing(npc.Position, originalPosition);
                        chaseState = 12;
                        break;

                    case 12:
                        // Flag to indicate if the first path has been received
                        bool leashPathReceived = false;

                        // Timer to request a new path
                        lock (timerLock)
                        {
                            pathUpdateTimer.Elapsed += (sender, args) =>
                            {
                                var leashPath = OnPathUpdate(world, zone, npc.Position, originalPosition);
                                if (leashPath.Count > 0)
                                {
                                    lock (timerLock) // Additional lock for thread safety
                                    {
                                        path = leashPath;
                                        leashPathReceived = true;
                                        targetIndex = 0; // Reset to start of the new path
                                    }
                                }
                                else
                                {
                                    // Handle no path case
                                    chaseState = 10; // Set chaseState to 10 to stop chasing
                                }
                            };
                            pathUpdateTimer.AutoReset = true;
                            pathUpdateTimer.Start();
                        }

                        // Wait for the first path update
                        while (!leashPathReceived)
                        {
                            await Task.Delay(10); // Adjust the delay as needed
                        }

                        lastUpdateTime = Environment.TickCount; // Record the start time

                        // Set animation and speed
                        npc.Animation = 3;
                        npc.Movement = 1;
                        npc.VelocityY = 14839;
                        movementSpeed = _returnSpeed;

                        chaseState = 13; // Proceed to the next state
                        break;

                    case 13:
                        // Initialize the index to the first point in the path
                        targetIndex = 0;
                        chaseState = 14;
                        break;

                    case 14:
                        // Simulate frame time asynchronously using Task.Delay
                        await Task.Delay(16);
                        chaseState = 15;
                        break;

                    case 15:
                        // Check for no path or targetIndex exceeds path count
                        if (path is null || path.Count == 0 || targetIndex >= path.Count)
                        {
                            chaseState = 10; // Stop if the path is empty or finished
                            break;
                        }

                        // Calculate the direction vector from NPC to the target point
                        direction = Vector3.Normalize(path[targetIndex] - npc.Position);

                        // Update npc facing
                        // npc.Facing = UpdateFacing(npc.Position, player.Position);

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
                }
            }
        }

        private List<Vector3> OnPathUpdate(int world, int zone, Vector3 npcPosition, Vector3 playerPosition)
        {
            List<Vector3> path = NavMeshManager.path(world, zone, npcPosition, playerPosition);
            return path;
        }

//------------------------------------------------------------------------------------------------------------------------------------------//
//                                                                                                                                          //
//                                                   Common functions                                                                       //
//                                                                                                                                          //
//------------------------------------------------------------------------------------------------------------------------------------------//

        public static byte UpdateFacing(Vector3 start, Vector3 end)
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
