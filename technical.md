# Landing Manager


### Speed set-point explicit formula
The formula for the speed set-point is derived assuming a time-reversed take-off with constant acceleration

Newton formula : 

$mass * acceleration = forces$\
$forces = lift - weight$\
$weight = mass * gravity$

Divide by mass:

$acceleration = \frac{lift}{mass} - gravity$\
$acceleration = (\frac{lift}{weight} - 1)*gravity$

If initial altitude and speed are zero, and we assume acceleration is constant:

$speed = acceleration * time$\
$altitude = 1/2 * acceleration * time^2$

Then solve for time as a function of altitude

$time = \sqrt{2 * \frac{altitude}{acceleration}}$
		
Substitude for time in the speed formula:

$speed = acceleration * \sqrt{2*\frac{altitude}{acceleration}}$\
$speed = \sqrt{2 * altitude * acceleration}$

Finally :

$speed = \sqrt{2 * altitude * (\frac{lift}{weight} - 1)*gravity}$

Because gravity, lift and weight are not actually constant, this formula provides an approximation
that is more and more incorrect at high altitude and thus margins must be applied here and there so
that the ship is capable of following the changes in the set-point

# GroundRadar

## Justification for the terrain scan distance :

The camera raycast charge rate is 2km/s
If called with tick10 (6 times per second), each tick lets us scan 2000/6 = 333m

1) If we have a single radar, it will be used for both altitude and terrain
	
The altitude scan has priority and will use all available scan range above an altitude X when X+RANGE_INCREMENT = 333m
Below that, we will accumulate 333m - (alt+RANGE_INCREMENT) of range per tick to scan for terrain.
A terrain scan that begins Y meters above the ground and with a range of Y meters uses 4 scans, therefore uses 4 x Y meters of range.
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

- Initial conditions : 0 vertical speed, altitude=ship at the surface
- Compute atmospheric density using the same simplified model as the game
- Compute the ship maximum lift from ion and atmospheric thrusters considering that density
- Compute the ship maximum lift from hydrogen thrusters
- If necessary, limit the total thrust according to configured acceleration and TWR limits (cut back on hydrogen first)
- Compute gravity using the same simplified model as the game
- Compute ship vertical acceleration : thrust/weight - gravity
- Compute ship vertical speed for some time step dt
- Compute the new ship altitude for some time step dt
- Repeat for 256 points

Then we use the altitude/speed table to interpolate (using binary search and linear interpolation) the speed set-point for any altitude value.




# EarlySurfaceGravityEstimator

If gravity follows the inverse square law:

This solves the equation :

$grav * (radius+alt)^2 = prev_grav * (radius+prev_alt)^2$

for the unknown radius

At each call, use the new updated values for the computations
and then push them to the old values for the next update

In the real world, gravity works as follows:

$grav = \frac{C}{d^2}$

with C a constant
d the distance to the center of the planet

In Space Engineers (with the Real Orbits mod!) gravity works as follows:
- below radius * planet.hillparam

$g(alt_{sealevel}) = g_{sealevel}$

- above radius * planet.hillparam

$g(alt_{sealevel}) = g_{sealevel}  * {(\frac{MaxRadius}{alt_{sealevel}+radius})}^2$

with :

$MaxRadius = radius  * (1  +  planet.hillparam)$

