local waypointsByNpcId = {
    [5702] = { -- NPC ID for Kelvin Wiskerman in Qeynos
        { x = 4458.4, y = 57.7, z = 17166, pause = 10000 },
        { x = 4443.5, y = 57.7, z = 17172.1, pause = 0 },
        { x = 4367, y = 57.7, z = 17166.6, pause = 0 },
        { x = 4356.9, y = 57.7, z = 17175.7, pause = 0 },
        { x = 4358.5, y = 57.7, z = 17293.8, pause = 0 },
        { x = 4367, y = 57.7, z = 17304.3, pause = 0 },
        { x = 4414, y = 57.7, z = 17312.5, pause = 0 },
        { x = 5580.5, y = 58, z = 17330.6, pause = 0 },
        { x = 5585.1, y = 58.2, z = 17340.1, pause = 10000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
