local waypointsByNpcId = {
    [106] = { -- NPC ID 106 for Guard Filgen in Qeynos
        { x = 4628.09, y = 57.6562, z = 17221.9, pause = 5000 },
        { x = 4623.7363, y = 57.65735, z = 17200.729, pause = 5000 },
        { x = 4645.578, y = 57.65735, z = 17204.01, pause = 0 },
        { x = 4722.3213, y = 57.65735, z = 17238.523, pause = 5000 },
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
