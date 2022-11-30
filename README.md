# TerraInvicta-AngularAccelerationTweaks

All ships in Terra Invicta have a fixed vector thrust magnitude of 3.5MN, which results in angular acceleration scaling with 1/(mass*length). Larger ships should have larger vector thrusters, so this mod scales vector thrust with hull mass (leaving gunships, escorts, and corvettes untouched while larger ship classes see enhanced maneuverability). Additionally, spiker utitily modules will provide a matching bonus to vector thrust as well. It also places an additional limitation on maximum angular velocity based on the g-tolerance of the ships crew and its length (using the combat values already present in the code), though I don't think you can reach this limit with stock ships/parts (as angular velocity is already limited to angular acceleration * 5 seconds).

Large ships will become much more maneuverable, which is a buff to them and nose weapons. Alien and AI ships will also benefit from this, though I'm not sure they'll realize the increased value of a spiker module. As is the effect is probably too strong to get for free, so I might add a faction project or 2 to limit it. Ideally vector thrust would be configurable or tied to a new primary system module, but I probably won't have time to figure that out.

Requires Unity Mod Manager: https://www.nexusmods.com/site/mods/21
