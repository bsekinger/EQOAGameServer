﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using ReturnHome.Database.SQL;
using ReturnHome.Server.EntityObject.Actors;
using ReturnHome.Server.Network.Managers;
using ReturnHome.Server.EntityObject.Items;
using ReturnHome.Server.EntityObject;

namespace ReturnHome.Server.Managers
{
    public static class WorldServer
    {
        public static NPCMovementManager npcMovementManager = new NPCMovementManager();

        // Initialize a single shared instance of of each movement controller
        public static NpcRoamController npcRoamController = new NpcRoamController(); //Added for NPC Movement
        public static NpcChaseController npcChaseController = new NpcChaseController(); //Added for NPC Movement
        public static NpcPatrolController npcPatrolController = new NpcPatrolController(); //Added for NPC Movement
        public static NpcRootController npcRootController = new NpcRootController(); //Added for NPC movement

        private static Stopwatch gameTimer;
        private static int serverTick = 1000 / 10;

        static WorldServer()
        {

        }

        public static async void Initialize()
        {
            //Creates NPC List
            Console.WriteLine("Collecting Item Patterns...");
            CharacterSQL npcList = new();

            List<ItemPattern> myItemPatterns = npcList.ItemPatterns();
            npcList.CloseConnection();
            Console.WriteLine("Total Item Pattern's Acquired: " + myItemPatterns.Count);
            Console.WriteLine("Adding Item Patterns...");
            for (int i = 0; i < myItemPatterns.Count; ++i)
                ItemManager.AddItem(myItemPatterns[i]);

            npcList = new();
            Console.WriteLine("Collecting Actors...");
            //Calls sql query function that fills list full of NPCs
            List<Actor> myNpcList = npcList.WorldActors();

            //Closing DB connection
            npcList.CloseConnection();
            MapManager.Initialize();

            Console.WriteLine("Done.");
            //Loops through each npc in list and sets their position, adds them to the entity manager, and mapmanager
            Console.WriteLine("Adding NPCs...");

            foreach (Actor myActor in myNpcList)
            {
                EntityManager.AddEntity(myActor);
                MapManager.Add(myActor);

            }

            Console.WriteLine("Done.");
            Console.WriteLine("Getting itemID seed.");
            CharacterSQL itemIDs = new();
            itemIDs.GetMaxItemID();
            itemIDs.CloseConnection();

            Console.WriteLine("Loading Default character options");
            CharacterSQL LoadDefaultCharacters = new();
            LoadDefaultCharacters.CollectDefaultCharacters();
            LoadDefaultCharacters.CloseConnection();


            Console.WriteLine("Done...");
            var thread = new Thread(() =>
            {
                UpdateWorld();
            });
            thread.Name = "World Manager";
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
            
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                try
                {                    
                    await ZoneManager.CheckZonesForPlayers(cancellationTokenSource.Token);
                    
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Checking zones for players was canceled.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }

        public async static void UpdateWorld()
        {
            gameTimer = new Stopwatch();

            while (true)
            {
                gameTimer.Restart();

                SessionManager.BeginSessionWork();

                gameTimer.Stop();

                if (gameTimer.ElapsedMilliseconds > serverTick)
                {
                    Console.WriteLine($"Server can't keep up - elapsed time: {gameTimer.ElapsedMilliseconds}");
                }

                await Task.Delay(Math.Max(0, serverTick - (int)gameTimer.ElapsedMilliseconds));
            }
        }
    }
}
