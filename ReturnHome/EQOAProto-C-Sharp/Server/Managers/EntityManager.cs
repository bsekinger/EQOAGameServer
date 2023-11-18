using System;
using System.Collections.Generic;
using System.Numerics;
using DotRecast.Core;
using ReturnHome.Server.EntityObject;
using ReturnHome.Server.EntityObject.Items;

namespace ReturnHome.Server.Managers
{
    public static class EntityManager
    {
        private static List<Entity> entityList = new();
        private static List<Item> itemList = new();
        private static ObjectIDCreator _idCreator;

        static EntityManager()
        {
            //Create an ObjectID Creator Instance for NPC's
            _idCreator = new ObjectIDCreator(true);
        }
        public static bool AddEntity(Entity entity)
        {
            if (!entity.isPlayer)
            {
                _idCreator.GenerateID(entity, out uint ObjectID);
                entity.ObjectID = ObjectID;
            }

            else
            {
                if (!_idCreator.AddEntity(entity))
                    return false;

            }

            //Add entity to our tracking List
            if (entityList.Contains(entity))
                //Return false here? Boot in world entity and load new one?
                return false;

            entityList.Add(entity);
            return true;
        }

        public static bool RemoveEntity(Entity entity)
        {
            if (!entityList.Contains(entity))
                return false;
            entityList.Remove(entity);
            return true;   
        }

        public static bool QueryForEntity(uint ObjectID, out Entity e)
        {
            if(_idCreator.QueryEntity(ObjectID, out Entity ent))
            {
                e = ent;
                return true;
            }

            e = default;
            return false;
        }

        public static bool QueryForEntityByServerID(uint ServerID, out Entity e)
        {
            if (_idCreator.QueryEntity(ServerID, out Entity ent))
            {
                e = ent;
                return true;

            }
            else
            {

                e = default;
                return false;
            }
        }

        public static bool QueryForEntity(string name, out Entity c)
        {
            foreach (Entity c2 in entityList)
            {
                if (c2.CharName == name)
                {
                    c = c2;
                    return true;
                }
            }
            c = default;
            return false;
        }

        public static List<Entity> QueryForAllEntitys()
        {
            return entityList;
        }

        private static bool IsPointInCircle(Vector3 position, Vector3 center, float radius)
        {
            float dx = position.X - center.X;
            float dy = position.Y - center.Y;
            float dz = position.Z - center.Z;

            // Calculate the squared distance to avoid a square root operation
            float squaredDistance = dx * dx + dy * dy + dz * dz;

            return squaredDistance <= radius * radius;
        }

        public static List<Entity> QueryForAllRoamersWithinRange(Vector3 center)
        {
            List<Entity> roamers = new List<Entity>();
            float radius = 100.0f;

            foreach (Entity c3 in entityList)
            {
                Vector3 position = new Vector3();
                position.X = c3.x;
                position.Y = c3.y;
                position.Z = c3.z;

                bool result = IsPointInCircle(position, center, radius);
                if (result && c3.RoamType == 1)
                {
                    roamers.Add(c3);
                    Console.WriteLine(c3.CharName + ":\t" + c3.x + "\t" + c3.y + "\t" + c3.z);
                }
            }

            return roamers;
        }
    }
}
