local waypointsByNpcId = {
    [5774] = { -- NPC ID for Scruffy in Qeynos
        { x = 4254.6, y = 57.7, z = 17363.4, pause = 10000 },
        { x = 4314, y = 59.8, z = 17308.1, pause = 0 },
        { x = 4317, y = 62.9, z = 17265.9, pause = 0 },
        { x = 4192.6, y = 60.8, z = 17267.8, pause = 0 },
        { x = 4192.1, y = 62.6, z = 17378.6, pause = 3000 },
        { x = 4222.1, y = 58.1, z = 17332.3, pause = 5000 },
        { x = 4278, y = 59.8, z = 17375.4, pause = 10000 },
        { x = 4242.9, y = 57.7, z = 17334.4, pause = 0 },
        { x = 4242, y = 57.7, z = 17309.4, pause = 0 },
        { x = 4278.1, y = 57.7, z = 17295.9, pause = 0 },
        { x = 4299.1, y = 57.7, z = 17295.5, pause = 0 },
        { x = 4298.4, y = 57.9, z = 17276.2, pause = 0 },
        { x = 4286.6, y = 58, z = 17276, pause = 10000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
