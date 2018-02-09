# LandSteel
Try to start a procedural planet as wide as earth. Starting code for Unity is http://code-phi.com/infinite-terrain-generation-in-unity-3d. Code for planet terrain generation is adapted from http://libnoise.sourceforge.net/examples/complexplanet/index.html.

# What's have been done
- Calculating height of terrain with a distance to a virtual plane touching the earth sphere surface at a specific lat/lon
- Using a QuadTree to draw terrain from First person camera to far distance (50Km)

# What's need to be done
- clamping lat/lon to have a finit number of plane touching the earth surface
- Using a QuadTree visitor (instead of chunk Cache) to avoid 25% point calculation when split and 100% on fusion and terrain chunck hole.
- Connect Terrain from quadtree different level (not native in Unity...)
- Adapt terrain chunks height locally to have a better resolution (will it suppress the flickering ?)
- Work on terrain texture
- Manage the rivers (see the last noise module)

# What idea for the future
- Use a noise module (perlin or more) to generate weather and weight it with season (day of the year)
- Use a noise module (perlin or more) to generate trees (perlin value is density proba to find a tree) weight it with lat/lon
- Use a noise module to generate town position and size.
- we can also generate maze or house.

# And what can we do with this ?
A new world of adventure. Whe just have to write an adventure in a already existing world.
An RPG engine depending of Rpg type is needed.
