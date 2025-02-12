local waypointsByNpcId = {
    [5703] = { -- NPC ID for Guard Crispin in Qeynos
        { x = 4381.02, y = 61.6797, z = 17163.1, pause = 5000 },
        { x = 4357.1826, y = 57.65735, z = 17165.762, pause = 0 },
        { x = 4355.921, y = 57.65735, z = 17145.834, pause = 0 },
        { x = 4355.3115, y = 57.65735, z = 17122.521, pause = 0 },
        { x = 4383.2285, y = 62.3468, z = 17118.305, pause = 0 },
        { x = 4408.1387, y = 57.65735, z = 17117.238, pause = 0 },
        { x = 4447.1396, y = 57.65735, z = 17117.275, pause = 0 },
        { x = 4488.328, y = 57.65735, z = 17116.299, pause = 0 },
        { x = 4461.662, y = 57.65735, z = 17117.145, pause = 0 },
        { x = 4419.337, y = 57.65735, z = 17117.467, pause = 0 },
        { x = 4384.1104, y = 62.64148, z = 17117.588, pause = 0 },
        { x = 4356.0947, y = 57.65735, z = 17117.396, pause = 0 },
        { x = 4356.335, y = 57.65735, z = 17149.463, pause = 0 },
        { x = 4354.7236, y = 57.65735, z = 17191.564, pause = 0 },
        { x = 4354.168, y = 57.65735, z = 17227.998, pause = 0 },
        { x = 4355.1826, y = 57.65735, z = 17265.162, pause = 0 },
        { x = 4358.073, y = 57.65735, z = 17303.338, pause = 0 },
        { x = 4384.4043, y = 63.147217, z = 17305.283, pause = 0 },
        { x = 4414.073, y = 57.65735, z = 17311.809, pause = 0 },
        { x = 4455.0254, y = 57.65735, z = 17310.7, pause = 0 },
        { x = 4481.631, y = 57.65735, z = 17313.295, pause = 0 },
        { x = 4502.1016, y = 57.65735, z = 17348.465, pause = 0 },
        { x = 4521.9326, y = 62.444702, z = 17351.445, pause = 5000 },
    },
}
function getWaypointsForNpc(npcId)
    return waypointsByNpcId[npcId]
end
