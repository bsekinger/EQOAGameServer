local waypointsByNpcId = {
    [5849] = { -- NPC ID for Beltray Bellfister in Qeynos
        { x = 4554.5, y = 57.8, z = 17358, pause = 0 },
        { x = 4553.4, y = 61.6, z = 17318.6, pause = 0 },
        { x = 4550, y = 61.6, z = 17309.2, pause = 0 },
        { x = 4561.6, y = 61.6, z = 17304.1, pause = 0 },
        { x = 4561.6, y = 73.8, z = 17291.3, pause = 0 },
        { x = 4559, y = 73.8, z = 17292.3, pause = 0 },
        { x = 4561.1, y = 73.8, z = 17322.1, pause = 10000 },
        { x = 4559, y = 73.8, z = 17292.3, pause = 0 },
        { x = 4561.6, y = 73.8, z = 17291.3, pause = 0 },
        { x = 4561.6, y = 61.6, z = 17304.1, pause = 0 },
        { x = 4550, y = 61.6, z = 17309.2, pause = 0 },
        { x = 4553.4, y = 61.6, z = 17318.6, pause = 0 },
        { x = 4554.5, y = 57.8, z = 17358, pause = 0 },
        { x = 4521.5, y = 62.3, z = 17351.6, pause = 0 },
        { x = 4470, y = 57.7, z = 17315.5, pause = 0 },
        { x = 4370.6, y = 58.1, z = 17306.9, pause = 0 },
        { x = 4361.4, y = 57.7, z = 17313.8, pause = 0 },
        { x = 4354.7, y = 57.7, z = 17335.7, pause = 0 },
        { x = 4321.9, y = 62, z = 17343.8, pause = 0 },
        { x = 4256.1, y = 57.7, z = 17295.7, pause = 10000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
