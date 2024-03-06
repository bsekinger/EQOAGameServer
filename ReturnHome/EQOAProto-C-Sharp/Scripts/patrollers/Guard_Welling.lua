local waypointsByNpcId = {
    [5842] = { -- NPC ID 106 for Guard Welling in Qeynos
        { x = 4356.58, y = 57.6562, z = 17268.1, pause = 5000 },
        { x = 4355.7793, y = 57.65735, z = 17243.69, pause = 0 },
        { x = 4355.786, y = 57.65735, z = 17223.047, pause = 0 },
        { x = 4354.6123, y = 57.65735, z = 17199.838, pause = 0 },
        { x = 4354.747, y = 57.65735, z = 17171.248, pause = 5000 },
    },

    [456] = { -- NPC ID 456 is a dummy just to show a second set of waypoints would be implemented
        {x = 50.0, y = 0.0, z = 45.0, pause = 5000},
        {x = 60.0, y = 0.0, z = 55.0, pause = 6000},
        {x = 70.0, y = 0.0, z = 65.0, pause = 7000},
        {x = 80.0, y = 0.0, z = 75.0, pause = 8000},
    },
    -- Add more NPCs by ID as needed
}

function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
