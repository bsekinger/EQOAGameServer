local waypointsByNpcId = {
    [5690] = { -- NPC ID for Julian Tallfellow in Qeynos
        { x = 4491.13, y = 57.5547, z = 17216.7, pause = 5000 },
        { x = 4480.8867, y = 57.65735, z = 17220.688, pause = 0 },
        { x = 4433.2246, y = 57.65735, z = 17222.842, pause = 0 },
        { x = 4433.201, y = 57.65735, z = 17176.635, pause = 0 },
        { x = 4492.6562, y = 57.65735, z = 17175.162, pause = 0 },
        { x = 4493.5244, y = 57.65735, z = 17200.373, pause = 0 },
        { x = 4636.5166, y = 57.65735, z = 17199.291, pause = 0 },
        { x = 4722.373, y = 57.65735, z = 17237.049, pause = 0 },
        { x = 4736.51, y = 57.65735, z = 17225.709, pause = 0 },
        { x = 4752.6797, y = 57.65735, z = 17162.795, pause = 0 },
        { x = 4856.419, y = 57.65735, z = 17110.293, pause = 10000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
