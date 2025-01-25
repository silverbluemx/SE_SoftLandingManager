# SE_SoftLandingManager
Soft Landing Manager script for the game Space Engineers

A script to automatically manage your thrusters to land safely on planets while optimizing your fuel and energy use.

The new version uses an acceleration model of the ship, with a planet gravity and atmosphere model (as used by the game, not real-life !), to simulate a liftoff from the surface at max thrust, and plays that in reverse during landing. Some final tweaking may be needed but it works quite well at the moment and is better able to capture how ion and atmo thrusters capability evolve as the atmosphere density changes.
This heavily relies on the planet gravity extending away to a long distance from the surface and following an inverse square law, which is not what the game natively does, hence why the mod Real Orbit is required (and a speed mod to make things even more interesting).

## Designed for use with:
- inverse square law gravity mods such as Real Orbits
- high speed limit mods such as 1000m/s speed mod
If you're not using both of these, then the script won't work well but also is not really needed.
- ships with not a lof of margin for vertical thrust (ex : lift to weight ratio of 1.5)

## Functions:
- computes and follows an optimal vertical speed profile for the descent
- prioritizes electric thrusters (atmospheric and ion) before using hydrogen ones
- uses a radar (raycasts from a downward facing camera) to measure your altitude way above what the game normally provides (useful for planets with gravity extending more than 100km from them!)
- computes the maximum lift that your ship is capable of (both in vacuum and ideal atmosphere)
- provides an estimate of the surface gravity for the planet
- warns if the ship is not capable of landing on the planet
- automatically deploy parachutes if about to crash

## Installation:
- (optional but recommanded) install a downwards facing camera on your ship
- install the script in a programmable block
- (optional but recommanded) configure the names of the downward camera and ship controller (cockpit, helm etc.)
- (optional) configure LDCs, timers, sound blocks etc. as needed, see below for the functions they provide
- recompile the script to let it autoconfigure itself
- (recommanded) Install and configure on your ship an auto-levelling script such as flight assist or other

## Usage:
- Set your ship in the gravity field of a planet, properly leveled
- Activate your auto-levelling script, or keep the ship level manually
- Activate mode1 or mode2 to have the script manage your descent
- (optionnally) Set vacuum or atmosphere mode, or select a planet to optimize the descent profile
- Once landed, check if the script automatically switched to mode0 (off), if not turn it off yourself

## Command line arguments:
- mode0 : turns the script off (the LCDs still give you information about your ship capabilities)
- mode1 : activate a descent mode that prioritizes electric thrusters (atmospheric and ions)
	It fires the ion thrusters early to bleed the gravitational potential energy and reduce the work
	left for the hydrogen thrusters. However this uses more total electrical energy than mode2
- mode2 : activate a faster descent mode that fires thrusters only when needed
	It is possible to switch between mode1 and mode2 during the descent, for example use mode2 to
	let the ship pick up speed, and then switch to mode1 to try and maintain that speed using ion thrusters
- vacuum : tells the script to expect vacuum at the planet surface (atmo thrusters won't work, max ion efficiency)
- atmo : tells the script to expect atmosphere at the planet surface (atmo thrusters will work, low ion efficiency)
- unknown : tells the script that the surface atmosphere conditions are unknown (default setting)
- earth, mars, moon etc. : optimize the script for the selected planet
Can be combined, ex : mode1mars, mode2atmo, etc.

## Ship configuration

Use the following names for your ship blocks. They may be changed in the script configuration.

*SLMref* OPTINAL BUT RECOMMANDED : Reference controller (seat, cockpit, etc.) to use for the ship orientation. This is optional, if the script doesnt find a controller with this name then it tries to find another suitable controller on the grid.

*SLMradar* OPTIONAL BUT RECOMMANDED : Name of downward-facing camera used as a ground radar (to measure altitude from very long distance and also account for landing pads above or below a planet surface)

*SLMignore* OPTIONAL : Include this tag in downward facing thruster (ie thrusters that lift the ship) that you want this script to ignore. For example, on an auxiliary drone.

*SLMdisplay* OPTIONAL : Name of the main display for the script (there can be any number of them, or none at all)

*SLMdebug* OPTIONAL : Additional debug display for the script (there can be any number of them, or none at all)

*SLMgraph* OPTIONAL : Additional graphical display for the script (there can be any number of them, or none at all)

*SLMlanding* OPTIONAL : Name of timer blocks that will be triggered a little before landing (ex : extend landing gear)

*SLMliftoff* OPTIONAL : Name of timer blocks that will be triggered a little after liftoff landing (ex : retract landing gear)

*SLMon* OPTIONAL : Name of timer blocks that will be triggered when the SLM activates (ex : by the command "mode1")

*SLMoff* OPTIONAL : Name of timer blocks that will be triggered when the SLM disactivates (ex : by the command "off", or at landing)

*SLMsound* OPTIONAL : Name of a sound block used to warn if expected surface gravity is higher than what the ship can handle or the ship is in panic mode (incapable of slowing down enough)

## Script configuration

Speed limits, thrust-to-weight ratio margins, PID controller coefficients, timer trigger altitude etc. can be changed in the *SLMConfiguration* class (see code).

## Planet catalog

All vanilla planets are included. Modded planets can be added to the *PlanetCatalog* class (see code).

## Technical info
### Speed set-point
The speed set-point is computed with 4 possible methods:

|Altitude							|Landing Profile			|Method|
|------------------------------------|---------------------------|---|
|available & above transition		|computed & valid			|detailed profile computed by the Liftoff Profile Builder|
|available & above transition		|not computed or not valid	|explicit formula assuming a time-reversed take-off with constant acceleration and gravity|
|available & below transition		|-							|constant final speed|
|not available						|-							|back-up formula simply using the local gravity |

### Liftoff Profile Builder

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

	
### Altitude
The script computes ship altitude (distance from surface) by combining altitude from controller (as shown on HUD) and radar (raytracing from ground-facing camera) as follows:

|Altitude from controller	|		Altitude from radar		|	Method|
|---------------|--------------|-----------------------|
|available & above transition		|-					|		Use altitude from surface
|available & below transition		|available				|	Use radar altitude
|available & below transition		|not available				|Use altitude from surface
|not available				|	available			|		Use radar altitude
|not available					|not available			|	default value (1e6)
