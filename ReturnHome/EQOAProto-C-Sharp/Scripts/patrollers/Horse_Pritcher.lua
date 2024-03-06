local waypointsByNpcId = {
    [5699] = { -- NPC ID 106 for Guard Welling in Qeynos
        { x = 4470.89, y = 57.6562, z = 17172.9, pause = 5000 },
        { x = 4421.21, y = 57.65735, z = 17172.355, pause = 0 },
        { x = 4411.949, y = 57.65735, z = 17163.66, pause = 0 },
        { x = 4354.5283, y = 57.65735, z = 17164.834, pause = 0 },
        { x = 4355.6035, y = 57.65735, z = 17117.523, pause = 0 },
        { x = 4487.117, y = 57.65735, z = 17114.268, pause = 5000 }, 
    },

    [456] = { -- NPC ID 456 is a dummy just to show how a second NPC of the same name would be implemented
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
