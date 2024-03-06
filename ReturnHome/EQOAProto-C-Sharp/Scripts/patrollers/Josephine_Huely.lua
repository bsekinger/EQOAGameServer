local waypointsByNpcId = {
    [5782] = { -- NPC ID for Josephine Huely in Qeynos
        { x = 4349.65, y = 57.6562, z = 17242.6, pause = 20000 },
        { x = 4350.9, y = 57.7, z = 17064.2, pause = 0 },
        { x = 4322.8, y = 62.6, z = 17043, pause = 0 },
        { x = 4301.4, y = 57.7, z = 17046.2, pause = 0 },
        { x = 4264.5, y = 57.7, z = 17075.7, pause = 0 },
        { x = 4268.1, y = 62.3, z = 17080.4, pause = 0 },
        { x = 4220.5, y = 62, z = 17072.4, pause = 20000 },
        { x = 4268.1, y = 62.3, z = 17080.4, pause = 0 },
        { x = 4254.1, y = 57.7, z = 17087.8, pause = 0 },
        { x = 4258, y = 57.9, z = 17095.5, pause = 0 },
        { x = 4256.1, y = 57.9, z = 17101.4, pause = 10000 },
        { x = 4258, y = 57.9, z = 17095.5, pause = 0 },
        { x = 4299.6, y = 57.7, z = 17041.2, pause = 0 },
        { x = 4348.8, y = 57.7, z = 17049.3, pause = 0 },
        { x = 4360.3, y = 57.7, z = 17346.9, pause = 10000 },
        { x = 4352.4, y = 57.7, z = 17242.3, pause = 1000 },
        { x = 4349.1, y = 57.7, z = 17241.3, pause = 20000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
