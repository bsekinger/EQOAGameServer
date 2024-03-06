using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ReturnHome.Server.EntityObject;


namespace ReturnHome.Server.Managers
{
    public class NPCMovementManager
    {
         public async Task npcMovementManager(int world, int zone)
        {
            List<Entity> roamers = EntityManager.QueryForAllRoamersByWorldAndZone(world, zone);
            List<Entity> patrollers = EntityManager.QueryForAllPatrollersByWorldAndZone(world, zone);

            foreach (Entity roamer in roamers)
            {
                roamer.isRoaming = true;

                // Use the shared instance of the roamController
                _ = WorldServer.npcRoamController.AddNpcAsync(world, zone, roamer);
            }

            foreach (Entity patroller in patrollers)
            {
                patroller.isPatrolling = true;

                // Use the shared instance of the roamController
                _ = WorldServer.npcPatrolController.AddNpcAsync(world, zone, patroller);
            }
        }
    }
}
