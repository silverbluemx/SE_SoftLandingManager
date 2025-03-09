# GroundRadar

## Justification for the terrain scan distance :

The camera raycast charge rate is 2km/s
If called with tick10 (6 times per second), each tick lets us scan 2000/6 = 333m

1) If we have a single radar, it will be used for both altitude and terrain
	
The altitude scan has priority and will use all available scan range above an altitude X when X+RANGE_INCREMENT = 333m
Below that, we will accumulate 333m - (alt+RANGE_INCREMENT) of range per tick to scan for terrain.
A terrain scan that begins Y meters above the ground and with a range of Y meters uses 4 scans, therefore uses 4*Y meters of range.
The maximum number of ticks between each terrain scan is therefore  4*Y / (333m - (Y+RANGE_INCREMENT))
For range increment of 50m, and Y=180m, that is:
4*180 / (333 - (180+50)) = 4*180 / 103 = 6.9 ticks
This is almost one per second, and sufficient


2) If we have two radars, the first one will be used for altitude and the second one for terrain
The maximum number of ticks between each terrain scan is therefore  4*Y / 333m
For Y = 400m that is 4*400 / 333 = 4.8 ticks

# LiftoffProfileBuilder

A landing profile has two attribues :
- computed : if the profile has been computed or not
- valid    : if the computed profile concludes on a successfull liftoff, meaning that the vertical
speed is always positive. If the profile is computed but invalid, that means the ship is not capable
of exiting the planet gravity well. It is however possible that the ship is capable of landing safely
(ex : with a lot of atmopheric thrusters) but this landing profile cannot be used to control landing
and a backup method will be needed.