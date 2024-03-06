local waypointsByNpcId = {
    [5823] = { -- NPC ID for Guard Walker in Qeynos
        { x = 4526, y = 62, z = 17337.6, pause = 0 },
        { x = 4531.4, y = 62.2, z = 17265.5, pause = 0 },
        { x = 4574.9, y = 62.1, z = 17265.8, pause = 0 },
        { x = 4573.6, y = 59.3, z = 17331.1, pause = 0 },
        { x = 4478.2, y = 60.5, z = 17377.4, pause = 0 },
        { x = 4391.6, y = 61.2, z = 17374.2, pause = 0 },
        { x = 4405.4, y = 57.7, z = 17320.5, pause = 0 },
        { x = 4410.3, y = 61.2, z = 17266.7, pause = 0 },
        { x = 4510, y = 60.4, z = 17268.9, pause = 0 },
        { x = 4506.3, y = 57.7, z = 17324.5, pause = 5000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
