﻿using System;
using LibNoise.Generator;
using LibNoise.Operator;
using UnityEngine;

namespace TerrainGenerator
{
    public class NoiseProvider : INoiseProvider
    {
		private int pseed;
		private double r;
		private double zero;
		private double upper;
		private double maxh;
		private double minh;
		private double dvl_min;
		private double dvl_max;
		public double LongitudeAngle = 0;
		public double LatitudeAngle = -70;

		//private Perlin PerlinNoiseGenerator;

		//private object NoiseLibThreadLockObject;
		public NoiseProvider(NoiseProvider np)
		{
			init (np.pseed, np.r, np.maxh, np.minh, np.dvl_min, np.dvl_max);
			LongitudeAngle = np.LongitudeAngle;
			LatitudeAngle = np.LatitudeAngle;
		}

		public NoiseProvider(int seed, double radius, double max, double min, double denivel_min, double denivel_max)
		{
			init (seed, radius, max, min, denivel_min, denivel_max);
		}

		private void init(int seed, double radius, double max, double min, double denivel_min, double denivel_max)
		{
			//PerlinNoiseGenerator = new Perlin(100, 4, 0.25, 10, seed, LibNoise.QualityMode.High);

			//NoiseLibThreadLockObject = new object ();
			pseed = seed;
			r = radius;
			zero = radius + denivel_min;
			upper = denivel_max - denivel_min;
			dvl_min = denivel_min;
			dvl_max = denivel_max;
			maxh = max; //highest mountain
			minh = min; //lowest under sea
			//Debug.Log ("NoiseProvider: r="+radius+" maxh="+maxh+" minh="+minh+" zero="+zero+" upper="+upper+" denivel_min="+denivel_min+" denivel_max="+denivel_max);
			//lock (NoiseLibThreadLockObject) {
			CreatePlanet (pseed);
			//}
			minx=5000;
			maxx=-5000;
		}

		public string getFolder() {
			return LongitudeAngle.ToString () + "_" + LatitudeAngle.ToString ();
		}

		public double minx;
		public double maxx;

		private double HeightAtXYZ(double dx, double dy, double dz) {
			double decal = 0;
			//lock (NoiseLibThreadLockObject) {
			decal = unscaledFinalPlanet.GetValue (dx, dy, dz);
			//decal = PerlinNoiseGenerator.GetValue(dx, dy, dz);
			//}
			if (decal < -1.0) {
				decal = -1.0;
			}
			if (decal > 1.0) {
				decal = 1.0;
			}
			double h = 0;
			if (decal >= 0) {
				h = decal * maxh;
			} else {
				//here decal is < 0 and minh also so we need to minus return
				h = - decal * minh;
			}
			if (h < dvl_min) {
				h = dvl_min;
			}
			if (h > dvl_max) {
				h = dvl_max;
			}
			return h;
		}

		public double HeightAtLatLon(double lat, double lon) {
			//lat = phi (-90 -> 90)
			//lon = teta (0 -> 360)
			double phi = Math.PI * (90 - lat) / 180; //coordonnée sphérique et radian
			double teta = Math.PI * lon / 180;
			double sinphi = Math.Sin (phi);
			double dx = sinphi * Math.Cos (teta);
			double dy = sinphi * Math.Sin (teta);
			double dz = Math.Cos (phi);
			return HeightAtXYZ (dx, dy, dz);
		}

		public double LongitudeRotateX(double dx, double dy, double dz, double angle) {
			double teta = Math.PI * angle / 180;
			return dx * Math.Cos (teta) + dz * Math.Sin (teta);
		}

		public double LongitudeRotateY(double dx, double dy, double dz, double angle) {
			return dy;
		}

		public double LongitudeRotateZ(double dx, double dy, double dz, double angle) {
			double teta = Math.PI * angle / 180;
			return dz * Math.Cos (teta) - dx * Math.Sin (teta);
		}

		public double LatitudeRotateX(double dx, double dy, double dz, double angle) {
			return dx;
		}

		public double LatitudeRotateY(double dx, double dy, double dz, double angle) {
			double teta = Math.PI * angle / 180;
			return dy * Math.Cos (teta) - dz * Math.Sin (teta);
		}

		public double LatitudeRotateZ(double dx, double dy, double dz, double angle) {
			double teta = Math.PI * angle / 180;
			return dz * Math.Cos (teta) + dy * Math.Sin (teta);
		}

		//si on projette la sphere sur un plan
		//on calcul la hauteur a la position x, z (du plan)
		//le but est de faire un height map qui projette la sphere
		public double HeightAt(double dx, double dz, bool log = false) {
			//normal/clamped
			if (dx < -r || dx > r || dz < -r || dz > r) {
				return zero;
			}
			double hyp = Math.Sqrt (dx * dx + dz * dz);
			if (hyp > r) {
				return zero;
			}
			//on calcul le delta y rapport au rayon de la planete
			double dy = Math.Sqrt (r * r - hyp * hyp);
			//on a maintenant un point sur le rayon nominal de la planete

			//normalize
			double ndx = dx / r;
			double ndy = dy / r;
			double ndz = dz / r;

			double rdx = LongitudeRotateX (ndx, ndy, ndz, LongitudeAngle);
			double rdy = LongitudeRotateY (ndx, ndy, ndz, LongitudeAngle);
			double rdz = LongitudeRotateZ (ndx, ndy, ndz, LongitudeAngle);

			ndx = LatitudeRotateX (rdx, rdy, rdz, LatitudeAngle);
			ndy = LatitudeRotateY (rdx, rdy, rdz, LatitudeAngle);
			ndz = LatitudeRotateZ (rdx, rdy, rdz, LatitudeAngle);
			double h = HeightAtXYZ (ndx, ndy, ndz);
			return dy + h;
		}

		public float GetValue(float x, float z)
		{
			if (x < minx) { minx = x; }
			if (x > maxx) { maxx = x; }
			double h = HeightAt ((double)x, (double)z) - zero;
			//clamp
			if (h < 0) {
				h = 0;
			}
			if (h > upper) {
				h = upper;
			}
			return (float)(h/upper);
		}

		//part of planet creation. (cf: http://libnoise.sourceforge.net/examples/complexplanet/index.html)
	 	double CONTINENT_FREQUENCY = 1.0;
		double CONTINENT_LACUNARITY = 2.208984375;
		double MOUNTAIN_LACUNARITY = 2.142578125;
		double HILLS_LACUNARITY = 2.162109375;
		double PLAINS_LACUNARITY = 2.314453125;
		double BADLANDS_LACUNARITY = 2.212890625;
		double MOUNTAINS_TWIST = 1.0;
		double HILLS_TWIST = 1.0;
		double BADLANDS_TWIST = 1.0;
		double SEA_LEVEL = 0.0;
		double SHELF_LEVEL = -0.375;
		double MOUNTAINS_AMOUNT = 0.5;
		double HILLS_AMOUNT = (1.0 + 0.5) / 2.0;
		double BADLANDS_AMOUNT = 0.03125;
		double TERRAIN_OFFSET = 1.0;
		double MOUNTAIN_GLACIATION = 1.375;
		double CONTINENT_HEIGHT_SCALE = (1.0 - 0) / 4.0;
		double RIVER_DEPTH = 0.0234375;

		Perlin baseContinentDef_pe0;
		Curve baseContinentDef_cu;
		Perlin baseContinentDef_pe1;
		ScaleBias baseContinentDef_sb;
		Min baseContinentDef_mi;
		Clamp baseContinentDef_cl;
		LibNoise.Operator.Cache baseContinentDef;
		Turbulence continentDef_tu0;
		Turbulence continentDef_tu1;
		Turbulence continentDef_tu2;
		Select continentDef_se;
		LibNoise.Operator.Cache continentDef;
		Turbulence terrainTypeDef_tu;
		Terrace terrainTypeDef_te;
		LibNoise.Operator.Cache terrainTypeDef;
		RidgedMultifractal mountainBaseDef_rm0;
		ScaleBias mountainBaseDef_sb0;
		RidgedMultifractal mountainBaseDef_rm1;
		ScaleBias mountainBaseDef_sb1;
		Const mountainBaseDef_co;
		Blend mountainBaseDef_bl;
		Turbulence mountainBaseDef_tu0;
		Turbulence mountainBaseDef_tu1;
		LibNoise.Operator.Cache mountainBaseDef;
		RidgedMultifractal mountainousHigh_rm0;
		RidgedMultifractal mountainousHigh_rm1;
		Max mountainousHigh_ma;
		Turbulence mountainousHigh_tu;
		LibNoise.Operator.Cache mountainousHigh;
		RidgedMultifractal mountainousLow_rm0;
		RidgedMultifractal mountainousLow_rm1;
		Multiply mountainousLow_mu;
		LibNoise.Operator.Cache mountainousLow;
		ScaleBias mountainousTerrain_sb0;
		ScaleBias mountainousTerrain_sb1;
		Add mountainousTerrain_ad;
		Select mountainousTerrain_se;
		ScaleBias mountainousTerrain_sb2;
		Exponent mountainousTerrain_ex;
		LibNoise.Operator.Cache mountainousTerrain;
		Billow hillyTerrain_bi;
		ScaleBias hillyTerrain_sb0;
		RidgedMultifractal hillyTerrain_rm;
		ScaleBias hillyTerrain_sb1;
		Const hillyTerrain_co;
		Blend hillyTerrain_bl;
		ScaleBias hillyTerrain_sb2;
		Exponent hillyTerrain_ex;
		Turbulence hillyTerrain_tu0;
		Turbulence hillyTerrain_tu1;
		LibNoise.Operator.Cache hillyTerrain;
		Billow plainsTerrain_bi0;
		ScaleBias plainsTerrain_sb0;
		Billow plainsTerrain_bi1;
		ScaleBias plainsTerrain_sb1;
		Multiply plainsTerrain_mu;
		ScaleBias plainsTerrain_sb2;
		LibNoise.Operator.Cache plainsTerrain;
		RidgedMultifractal badlandsSand_rm;
		ScaleBias badlandsSand_sb0;
		Voronoi badlandsSand_vo;
		ScaleBias badlandsSand_sb1;
		Add badlandsSand_ad;
		LibNoise.Operator.Cache badlandsSand;
		Perlin badlandsCliffs_pe;
		Curve badlandsCliffs_cu;
		Clamp badlandsCliffs_cl;
		Terrace badlandsCliffs_te;
		Turbulence badlandsCliffs_tu0;
		Turbulence badlandsCliffs_tu1;
		LibNoise.Operator.Cache badlandsCliffs;
		ScaleBias badlandsTerrain_sb;
		Max badlandsTerrain_ma;
		LibNoise.Operator.Cache badlandsTerrain;
		RidgedMultifractal riverPositions_rm0;
		Curve riverPositions_cu0;
		RidgedMultifractal riverPositions_rm1;
		Curve riverPositions_cu1;
		Min riverPositions_mi;
		Turbulence riverPositions_tu;
		LibNoise.Operator.Cache riverPositions;
		ScaleBias scaledMountainousTerrain_sb0;
		Perlin scaledMountainousTerrain_pe;
		Exponent scaledMountainousTerrain_ex;
		ScaleBias scaledMountainousTerrain_sb1;
		Multiply scaledMountainousTerrain_mu;
		LibNoise.Operator.Cache scaledMountainousTerrain;
		ScaleBias scaledHillyTerrain_sb0;
		Perlin scaledHillyTerrain_pe;
		Exponent scaledHillyTerrain_ex;
		ScaleBias scaledHillyTerrain_sb1;
		Multiply scaledHillyTerrain_mu;
		LibNoise.Operator.Cache scaledHillyTerrain;
		ScaleBias scaledPlainsTerrain_sb;
		LibNoise.Operator.Cache scaledPlainsTerrain;
		ScaleBias scaledBadlandsTerrain_sb;
		LibNoise.Operator.Cache scaledBadlandsTerrain;
		Terrace continentalShelf_te;
		RidgedMultifractal continentalShelf_rm;
		ScaleBias continentalShelf_sb;
		Clamp continentalShelf_cl;
		Add continentalShelf_ad;
		LibNoise.Operator.Cache continentalShelf;
		ScaleBias baseContinentElev_sb;
		Select baseContinentElev_se;
		LibNoise.Operator.Cache baseContinentElev;
		Add continentsWithPlains_ad;
		LibNoise.Operator.Cache continentsWithPlains;
		Add continentsWithHills_ad;
		Select continentsWithHills_se;
		LibNoise.Operator.Cache continentsWithHills;
		Add continentsWithMountains_ad0;
		Curve continentsWithMountains_cu;
		Add continentsWithMountains_ad1;
		Select continentsWithMountains_se;
		LibNoise.Operator.Cache continentsWithMountains;
		Perlin continentsWithBadlands_pe;
		Add continentsWithBadlands_ad;
		Select continentsWithBadlands_se;
		Max continentsWithBadlands_ma;
		LibNoise.Operator.Cache continentsWithBadlands;
		ScaleBias continentsWithRivers_sb;
		Add continentsWithRivers_ad;
		Select continentsWithRivers_se;
		LibNoise.Operator.Cache continentsWithRivers;
		LibNoise.Operator.Cache unscaledFinalPlanet;

		private void CreatePlanet(int CUR_SEED)
        {

			// 1: [Continent module]: This Perlin-noise module generates the continents.
			//    This noise module has a high number of octaves so that detail is
			//    visible at high zoom levels.
			baseContinentDef_pe0 = new Perlin();
			baseContinentDef_pe0.Seed = (CUR_SEED + 0);
			baseContinentDef_pe0.Frequency = (CONTINENT_FREQUENCY);
			baseContinentDef_pe0.Persistence = (0.5);
			baseContinentDef_pe0.Lacunarity = (CONTINENT_LACUNARITY);
			baseContinentDef_pe0.OctaveCount = (14);
			baseContinentDef_pe0.Quality = (LibNoise.QualityMode.Medium);

			// 2: [Continent-with-ranges module]: Next, a curve module modifies the
			//    output value from the continent module so that very high values appear
			//    near sea level.  This defines the positions of the mountain ranges.
			baseContinentDef_cu = new Curve(baseContinentDef_pe0);
			baseContinentDef_cu.Add (-2.0000 + SEA_LEVEL,-1.625 + SEA_LEVEL);
			baseContinentDef_cu.Add (-1.0000 + SEA_LEVEL,-1.375 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 0.0000 + SEA_LEVEL,-0.375 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 0.0625 + SEA_LEVEL, 0.125 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 0.1250 + SEA_LEVEL, 0.250 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 0.2500 + SEA_LEVEL, 1.000 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 0.5000 + SEA_LEVEL, 0.250 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 0.7500 + SEA_LEVEL, 0.250 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 1.0000 + SEA_LEVEL, 0.500 + SEA_LEVEL);
			baseContinentDef_cu.Add ( 2.0000 + SEA_LEVEL, 0.500 + SEA_LEVEL);

			// 3: [Carver module]: This higher-frequency Perlin-noise module will be
			//    used by subsequent noise modules to carve out chunks from the mountain
			//    ranges within the continent-with-ranges module so that the mountain
			//    ranges will not be complely impassible.
			baseContinentDef_pe1 = new Perlin();
			baseContinentDef_pe1.Seed = (CUR_SEED + 1);
			baseContinentDef_pe1.Frequency = (CONTINENT_FREQUENCY * 4.34375);
			baseContinentDef_pe1.Persistence = (0.5);
			baseContinentDef_pe1.Lacunarity = (CONTINENT_LACUNARITY);
			baseContinentDef_pe1.OctaveCount = (11);
			baseContinentDef_pe1.Quality = (LibNoise.QualityMode.Medium);

			// 4: [Scaled-carver module]: This scale/bias module scales the output
			//    value from the carver module such that it is usually near 1.0.  This
			//    is required for step 5.
			ScaleBias baseContinentDef_sb = new ScaleBias(0.375, 0.625, baseContinentDef_pe1);

			// 5: [Carved-continent module]: This minimum-value module carves out chunks
			//    from the continent-with-ranges module.  It does this by ensuring that
			//    only the minimum of the output values from the scaled-carver module
			//    and the continent-with-ranges module contributes to the output value
			//    of this subgroup.  Most of the time, the minimum-value module will
			//    select the output value from the continents-with-ranges module since
			//    the output value from the scaled-carver module is usually near 1.0.
			//    Occasionally, the output value from the scaled-carver module will be
			//    less than the output value from the continent-with-ranges module, so
			//    in this case, the output value from the scaled-carver module is
			//    selected.
			baseContinentDef_mi = new Min(baseContinentDef_sb, baseContinentDef_cu);

			// 6: [Clamped-continent module]: Finally, a clamp module modifies the
			//    carved-continent module to ensure that the output value of this
			//    subgroup is between -1.0 and 1.0.
			baseContinentDef_cl = new Clamp(-1.0, 1.0, baseContinentDef_mi);

			// 7: [Base-continent-definition subgroup]: LibNoise.Operator.Caches the output value from the
			//    clamped-continent module.
			baseContinentDef = new LibNoise.Operator.Cache(baseContinentDef_cl);

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continent definition (5 noise modules)
			//
			// This subgroup warps the output value from the the base-continent-
			// definition subgroup, producing more realistic terrain.
			//
			// Warping the base continent definition produces lumpier terrain with
			// cliffs and rifts.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Coarse-turbulence module]: This turbulence module warps the output
			//    value from the base-continent-definition subgroup, adding some coarse
			//    detail to it.
			continentDef_tu0 = new Turbulence(baseContinentDef);
			continentDef_tu0.Seed = (CUR_SEED + 10);
			continentDef_tu0.Frequency = (CONTINENT_FREQUENCY * 15.25);
			continentDef_tu0.Power = (CONTINENT_FREQUENCY / 113.75);
			continentDef_tu0.Roughness = (13);

			// 2: [Intermediate-turbulence module]: This turbulence module warps the
			//    output value from the coarse-turbulence module.  This turbulence has
			//    a higher frequency, but lower power, than the coarse-turbulence
			//    module, adding some intermediate detail to it.
			continentDef_tu1 = new Turbulence(continentDef_tu0);
			continentDef_tu1.Seed = (CUR_SEED + 11);
			continentDef_tu1.Frequency = (CONTINENT_FREQUENCY * 47.25);
			continentDef_tu1.Power = (CONTINENT_FREQUENCY / 433.75);
			continentDef_tu1.Roughness = (12);

			// 3: [Warped-base-continent-definition module]: This turbulence module
			//    warps the output value from the intermediate-turbulence module.  This
			//    turbulence has a higher frequency, but lower power, than the
			//    intermediate-turbulence module, adding some fine detail to it.
			continentDef_tu2 = new Turbulence(continentDef_tu1);
			continentDef_tu2.Seed = (CUR_SEED + 12);
			continentDef_tu2.Frequency = (CONTINENT_FREQUENCY * 95.25);
			continentDef_tu2.Power = (CONTINENT_FREQUENCY / 1019.75);
			continentDef_tu2.Roughness = (11);

			// 4: [Select-turbulence module]: At this stage, the turbulence is applied
			//    to the entire base-continent-definition subgroup, producing some very
			//    rugged, unrealistic coastlines.  This selector module selects the
			//    output values from the (unwarped) base-continent-definition subgroup
			//    and the warped-base-continent-definition module, based on the output
			//    value from the (unwarped) base-continent-definition subgroup.  The
			//    selection boundary is near sea level and has a relatively smooth
			//    transition.  In effect, only the higher areas of the base-continent-
			//    definition subgroup become warped; the underwater and coastal areas
			//    remain unaffected.
			continentDef_se = new Select(baseContinentDef, continentDef_tu2, baseContinentDef);
			continentDef_se.SetBounds (SEA_LEVEL - 0.0375, SEA_LEVEL + 1000.0375);
			continentDef_se.Falloff = (0.0625);

			// 7: [Continent-definition group]: LibNoise.Operator.Caches the output value from the
			//    clamped-continent module.  This is the output value for the entire
			//    continent-definition group.
			continentDef = new LibNoise.Operator.Cache(continentDef_se);


			////////////////////////////////////////////////////////////////////////////
			// Module group: terrain type definition
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: terrain type definition (3 noise modules)
			//
			// This subgroup defines the positions of the terrain types on the planet.
			//
			// Terrain types include, in order of increasing roughness, plains, hills,
			// and mountains.
			//
			// This subgroup's output value is based on the output value from the
			// continent-definition group.  Rougher terrain mainly appears at higher
			// elevations.
			//
			// -1.0 represents the smoothest terrain types (plains and underwater) and
			// +1.0 represents the roughest terrain types (mountains).
			//

			// 1: [Warped-continent module]: This turbulence module slightly warps the
			//    output value from the continent-definition group.  This prevents the
			//    rougher terrain from appearing exclusively at higher elevations.
			//    Rough areas may now appear in the the ocean, creating rocky islands
			//    and fjords.
			terrainTypeDef_tu = new Turbulence(continentDef);
			terrainTypeDef_tu.Seed = (CUR_SEED + 20);
			terrainTypeDef_tu.Frequency = (CONTINENT_FREQUENCY * 18.125);
			terrainTypeDef_tu.Power = (CONTINENT_FREQUENCY / 20.59375 * TERRAIN_OFFSET);
			terrainTypeDef_tu.Roughness = (3);

			// 2: [Roughness-probability-shift module]: This terracing module sharpens
			//    the edges of the warped-continent module near sea level and lowers
			//    the slope towards the higher-elevation areas.  This shrinks the areas
			//    in which the rough terrain appears, increasing the "rarity" of rough
			//    terrain.
			terrainTypeDef_te = new Terrace(terrainTypeDef_tu);
			terrainTypeDef_te.Add (-1.00);
			terrainTypeDef_te.Add (SHELF_LEVEL + SEA_LEVEL / 2.0);
			terrainTypeDef_te.Add (1.00);

			// 3: [Terrain-type-definition group]: LibNoise.Operator.Caches the output value from the
			//    roughness-probability-shift module.  This is the output value for
			//    the entire terrain-type-definition group.
			terrainTypeDef = new LibNoise.Operator.Cache(terrainTypeDef_te);


			////////////////////////////////////////////////////////////////////////////
			// Module group: mountainous terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: mountain base definition (9 noise modules)
			//
			// This subgroup generates the base-mountain elevations.  Other subgroups
			// will add the ridges and low areas to the base elevations.
			//
			// -1.0 represents low mountainous terrain and +1.0 represents high
			// mountainous terrain.
			//

			// 1: [Mountain-ridge module]: This ridged-multifractal-noise module
			//    generates the mountain ridges.
			mountainBaseDef_rm0 = new RidgedMultifractal();
			mountainBaseDef_rm0.Seed = (CUR_SEED + 30);
			mountainBaseDef_rm0.Frequency = (1723.0);
			mountainBaseDef_rm0.Lacunarity = (MOUNTAIN_LACUNARITY);
			mountainBaseDef_rm0.OctaveCount = (4);
			mountainBaseDef_rm0.Quality = (LibNoise.QualityMode.Medium);

			// 2: [Scaled-mountain-ridge module]: Next, a scale/bias module scales the
			//    output value from the mountain-ridge module so that its ridges are not
			//    too high.  The reason for this is that another subgroup adds actual
			//    mountainous terrain to these ridges.
			mountainBaseDef_sb0 = new ScaleBias(mountainBaseDef_rm0);
			mountainBaseDef_sb0.Scale = (0.5);
			mountainBaseDef_sb0.Bias = (0.375);

			// 3: [River-valley module]: This ridged-multifractal-noise module generates
			//    the river valleys.  It has a much lower frequency than the mountain-
			//    ridge module so that more mountain ridges will appear outside of the
			//    valleys.  Note that this noise module generates ridged-multifractal
			//    noise using only one octave; this information will be important in the
			//    next step.
			mountainBaseDef_rm1 = new RidgedMultifractal();
			mountainBaseDef_rm1.Seed = (CUR_SEED + 31);
			mountainBaseDef_rm1.Frequency = (367.0);
			mountainBaseDef_rm1.Lacunarity = (MOUNTAIN_LACUNARITY);
			mountainBaseDef_rm1.OctaveCount = (1);
			mountainBaseDef_rm1.Quality = (LibNoise.QualityMode.High);

			// 4: [Scaled-river-valley module]: Next, a scale/bias module applies a
			//    scaling factor of -2.0 to the output value from the river-valley
			//    module.  This stretches the possible elevation values because one-
			//    octave ridged-multifractal noise has a lower range of output values
			//    than multiple-octave ridged-multifractal noise.  The negative scaling
			//    factor inverts the range of the output value, turning the ridges from
			//    the river-valley module into valleys.
			mountainBaseDef_sb1 = new ScaleBias(mountainBaseDef_rm1);
			mountainBaseDef_sb1.Scale = (-2.0);
			mountainBaseDef_sb1.Bias = (-0.5);

			// 5: [Low-flat module]: This low constant value is used by step 6.
			mountainBaseDef_co = new Const();
			mountainBaseDef_co.Value = (-1.0);

			// 6: [Mountains-and-valleys module]: This blender module merges the
			//    scaled-mountain-ridge module and the scaled-river-valley module
			//    together.  It causes the low-lying areas of the terrain to become
			//    smooth, and causes the high-lying areas of the terrain to contain
			//    ridges.  To do this, it uses the scaled-river-valley module as the
			//    control module, causing the low-flat module to appear in the lower
			//    areas and causing the scaled-mountain-ridge module to appear in the
			//    higher areas.
			mountainBaseDef_bl = new Blend(mountainBaseDef_co, mountainBaseDef_sb0, mountainBaseDef_sb1);

			// 7: [Coarse-turbulence module]: This turbulence module warps the output
			//    value from the mountain-and-valleys module, adding some coarse detail
			//    to it.
			mountainBaseDef_tu0 = new Turbulence(mountainBaseDef_bl);
			mountainBaseDef_tu0.Seed = (CUR_SEED + 32);
			mountainBaseDef_tu0.Frequency = (1337.0);
			mountainBaseDef_tu0.Power = (1.0 / 6730.0 * MOUNTAINS_TWIST);
			mountainBaseDef_tu0.Roughness = (4);

			// 8: [Warped-mountains-and-valleys module]: This turbulence module warps
			//    the output value from the coarse-turbulence module.  This turbulence
			//    has a higher frequency, but lower power, than the coarse-turbulence
			//    module, adding some fine detail to it.
			mountainBaseDef_tu1 = new Turbulence(mountainBaseDef_tu0);
			mountainBaseDef_tu1.Seed = (CUR_SEED + 33);
			mountainBaseDef_tu1.Frequency = (21221.0);
			mountainBaseDef_tu1.Power = (1.0 / 120157.0 * MOUNTAINS_TWIST);
			mountainBaseDef_tu1.Roughness = (6);

			// 9: [Mountain-base-definition subgroup]: LibNoise.Operator.Caches the output value from the
			//    warped-mountains-and-valleys module.
			mountainBaseDef = new LibNoise.Operator.Cache(mountainBaseDef_tu1);


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: high mountainous terrain (5 noise modules)
			//
			// This subgroup generates the mountainous terrain that appears at high
			// elevations within the mountain ridges.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Mountain-basis-0 module]: This ridged-multifractal-noise module,
			//    along with the mountain-basis-1 module, generates the individual
			//    mountains.
			mountainousHigh_rm0 = new RidgedMultifractal();
			mountainousHigh_rm0.Seed = (CUR_SEED + 40);
			mountainousHigh_rm0.Frequency = (2371.0);
			mountainousHigh_rm0.Lacunarity = (MOUNTAIN_LACUNARITY);
			mountainousHigh_rm0.OctaveCount = (3);
			mountainousHigh_rm0.Quality = (LibNoise.QualityMode.High);

			// 2: [Mountain-basis-1 module]: This ridged-multifractal-noise module,
			//    along with the mountain-basis-0 module, generates the individual
			//    mountains.
			mountainousHigh_rm1 = new RidgedMultifractal();
			mountainousHigh_rm1.Seed = (CUR_SEED + 41);
			mountainousHigh_rm1.Frequency = (2341.0);
			mountainousHigh_rm1.Lacunarity = (MOUNTAIN_LACUNARITY);
			mountainousHigh_rm1.OctaveCount = (3);
			mountainousHigh_rm1.Quality = (LibNoise.QualityMode.High);

			// 3: [High-mountains module]: Next, a maximum-value module causes more
			//    mountains to appear at the expense of valleys.  It does this by
			//    ensuring that only the maximum of the output values from the two
			//    ridged-multifractal-noise modules contribute to the output value of
			//    this subgroup.
			mountainousHigh_ma = new Max();
			mountainousHigh_ma[0] = mountainousHigh_rm0;
			mountainousHigh_ma[1] = mountainousHigh_rm1;

			// 4: [Warped-high-mountains module]: This turbulence module warps the
			//    output value from the high-mountains module, adding some detail to it.
			mountainousHigh_tu = new Turbulence();
			mountainousHigh_tu[0] = mountainousHigh_ma;
			mountainousHigh_tu.Seed = (CUR_SEED + 42);
			mountainousHigh_tu.Frequency = (31511.0);
			mountainousHigh_tu.Power = (1.0 / 180371.0 * MOUNTAINS_TWIST);
			mountainousHigh_tu.Roughness = (4);

			// 5: [High-mountainous-terrain subgroup]: LibNoise.Operator.Caches the output value from the
			//    warped-high-mountains module.
			mountainousHigh = new LibNoise.Operator.Cache();
			mountainousHigh[0] = mountainousHigh_tu;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: low mountainous terrain (4 noise modules)
			//
			// This subgroup generates the mountainous terrain that appears at low
			// elevations within the river valleys.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Lowland-basis-0 module]: This ridged-multifractal-noise module,
			//    along with the lowland-basis-1 module, produces the low mountainous
			//    terrain.
			mountainousLow_rm0 = new RidgedMultifractal();
			mountainousLow_rm0.Seed = (CUR_SEED + 50);
			mountainousLow_rm0.Frequency = (1381.0);
			mountainousLow_rm0.Lacunarity = (MOUNTAIN_LACUNARITY);
			mountainousLow_rm0.OctaveCount = (8);
			mountainousLow_rm0.Quality = (LibNoise.QualityMode.High);

			// 1: [Lowland-basis-1 module]: This ridged-multifractal-noise module,
			//    along with the lowland-basis-0 module, produces the low mountainous
			//    terrain.
			mountainousLow_rm1 = new RidgedMultifractal();
			mountainousLow_rm1.Seed = (CUR_SEED + 51);
			mountainousLow_rm1.Frequency = (1427.0);
			mountainousLow_rm1.Lacunarity = (MOUNTAIN_LACUNARITY);
			mountainousLow_rm1.OctaveCount = (8);
			mountainousLow_rm1.Quality = (LibNoise.QualityMode.High);

			// 3: [Low-mountainous-terrain module]: This multiplication module combines
			//    the output values from the two ridged-multifractal-noise modules.
			//    This causes the following to appear in the resulting terrain:
			//    - Cracks appear when two negative output values are multiplied
			//      together.
			//    - Flat areas appear when a positive and a negative output value are
			//      multiplied together.
			//    - Ridges appear when two positive output values are multiplied
			//      together.
			mountainousLow_mu = new Multiply();
			mountainousLow_mu[0] = mountainousLow_rm0;
			mountainousLow_mu[1] = mountainousLow_rm1;

			// 4: [Low-mountainous-terrain subgroup]: LibNoise.Operator.Caches the output value from the
			//    low-moutainous-terrain module.
			mountainousLow = new LibNoise.Operator.Cache();
			mountainousLow[0] = mountainousLow_mu;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: mountainous terrain (7 noise modules)
			//
			// This subgroup generates the final mountainous terrain by combining the
			// high-mountainous-terrain subgroup with the low-mountainous-terrain
			// subgroup.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Scaled-low-mountainous-terrain module]: First, this scale/bias module
			//    scales the output value from the low-mountainous-terrain subgroup to a
			//    very low value and biases it towards -1.0.  This results in the low
			//    mountainous areas becoming more-or-less flat with little variation.
			//    This will also result in the low mountainous areas appearing at the
			//    lowest elevations in this subgroup.
			mountainousTerrain_sb0 = new ScaleBias();
			mountainousTerrain_sb0[0] = mountainousLow;
			mountainousTerrain_sb0.Scale = (0.03125);
			mountainousTerrain_sb0.Bias = (-0.96875);

			// 2: [Scaled-high-mountainous-terrain module]: Next, this scale/bias module
			//    scales the output value from the high-mountainous-terrain subgroup to
			//    1/4 of its initial value and biases it so that its output value is
			//    usually positive.
			mountainousTerrain_sb1 = new ScaleBias();
			mountainousTerrain_sb1[0] = mountainousHigh;
			mountainousTerrain_sb1.Scale = (0.25);
			mountainousTerrain_sb1.Bias = (0.25);

			// 3: [Added-high-mountainous-terrain module]: This addition module adds the
			//    output value from the scaled-high-mountainous-terrain module to the
			//    output value from the mountain-base-definition subgroup.  Mountains
			//    now appear all over the terrain.
			mountainousTerrain_ad = new Add();
			mountainousTerrain_ad[0] = mountainousTerrain_sb1;
			mountainousTerrain_ad[1] = mountainBaseDef;

			// 4: [Combined-mountainous-terrain module]: Note that at this point, the
			//    entire terrain is covered in high mountainous terrain, even at the low
			//    elevations.  To make sure the mountains only appear at the higher
			//    elevations, this selector module causes low mountainous terrain to
			//    appear at the low elevations (within the valleys) and the high
			//    mountainous terrain to appear at the high elevations (within the
			//    ridges.)  To do this, this noise module selects the output value from
			//    the added-high-mountainous-terrain module if the output value from the
			//    mountain-base-definition subgroup is higher than a set amount.
			//    Otherwise, this noise module selects the output value from the scaled-
			//    low-mountainous-terrain module.
			mountainousTerrain_se = new Select();
			mountainousTerrain_se[0] = mountainousTerrain_sb0;
			mountainousTerrain_se[1] = mountainousTerrain_ad;
			mountainousTerrain_se.Controller = (mountainBaseDef);
			mountainousTerrain_se.SetBounds(-0.5, 999.5);
			mountainousTerrain_se.Falloff = (0.5);

			// 5: [Scaled-mountainous-terrain-module]: This scale/bias module slightly
			//    reduces the range of the output value from the combined-mountainous-
			//    terrain module, decreasing the heights of the mountain peaks.
			mountainousTerrain_sb2 = new ScaleBias();
			mountainousTerrain_sb2[0] = mountainousTerrain_se;
			mountainousTerrain_sb2.Scale = (0.8);
			mountainousTerrain_sb2.Bias = (0.0);

			// 6: [Glaciated-mountainous-terrain-module]: This exponential-curve module
			//    applies an exponential curve to the output value from the scaled-
			//    mountainous-terrain module.  This causes the slope of the mountains to
			//    smoothly increase towards higher elevations, as if a glacier grinded
			//    out those mountains.  This exponential-curve module expects the output
			//    value to range from -1.0 to +1.0.
			mountainousTerrain_ex = new Exponent();
			mountainousTerrain_ex[0] = mountainousTerrain_sb2;
			mountainousTerrain_ex.Value = (MOUNTAIN_GLACIATION);

			// 7: [Mountainous-terrain group]: LibNoise.Operator.Caches the output value from the
			//    glaciated-mountainous-terrain module.  This is the output value for
			//    the entire mountainous-terrain group.
			mountainousTerrain = new LibNoise.Operator.Cache();
			mountainousTerrain[0] = mountainousTerrain_ex;


			////////////////////////////////////////////////////////////////////////////
			// Module group: hilly terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: hilly terrain (11 noise modules)
			//
			// This subgroup generates the hilly terrain.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Hills module]: This billow-noise module generates the hills.
			hillyTerrain_bi = new Billow();
			hillyTerrain_bi.Seed = (CUR_SEED + 60);
			hillyTerrain_bi.Frequency = (1663.0);
			hillyTerrain_bi.Persistence = (0.5);
			hillyTerrain_bi.Lacunarity = (HILLS_LACUNARITY);
			hillyTerrain_bi.OctaveCount = (6);
			hillyTerrain_bi.Quality = (LibNoise.QualityMode.High);

			// 2: [Scaled-hills module]: Next, a scale/bias module scales the output
			//    value from the hills module so that its hilltops are not too high.
			//    The reason for this is that these hills are eventually added to the
			//    river valleys (see below.)
			hillyTerrain_sb0 = new ScaleBias();
			hillyTerrain_sb0[0] = hillyTerrain_bi;
			hillyTerrain_sb0.Scale = (0.5);
			hillyTerrain_sb0.Bias = (0.5);

			// 3: [River-valley module]: This ridged-multifractal-noise module generates
			//    the river valleys.  It has a much lower frequency so that more hills
			//    will appear in between the valleys.  Note that this noise module
			//    generates ridged-multifractal noise using only one octave; this
			//    information will be important in the next step.
			hillyTerrain_rm = new RidgedMultifractal();
			hillyTerrain_rm.Seed = (CUR_SEED + 61);
			hillyTerrain_rm.Frequency = (367.5);
			hillyTerrain_rm.Lacunarity = (HILLS_LACUNARITY);
			hillyTerrain_rm.Quality = (LibNoise.QualityMode.High);
			hillyTerrain_rm.OctaveCount = (1);

			// 4: [Scaled-river-valley module]: Next, a scale/bias module applies a
			//    scaling factor of -2.0 to the output value from the river-valley
			//    module.  This stretches the possible elevation values because one-
			//    octave ridged-multifractal noise has a lower range of output values
			//    than multiple-octave ridged-multifractal noise.  The negative scaling
			//    factor inverts the range of the output value, turning the ridges from
			//    the river-valley module into valleys.
			hillyTerrain_sb1 = new ScaleBias();
			hillyTerrain_sb1[0] = hillyTerrain_rm;
			hillyTerrain_sb1.Scale = (-2.0);
			hillyTerrain_sb1.Bias = (-0.5);

			// 5: [Low-flat module]: This low constant value is used by step 6.
			hillyTerrain_co = new Const();
			hillyTerrain_co.Value = (-1.0);

			// 6: [Mountains-and-valleys module]: This blender module merges the
			//    scaled-hills module and the scaled-river-valley module together.  It
			//    causes the low-lying areas of the terrain to become smooth, and causes
			//    the high-lying areas of the terrain to contain hills.  To do this, it
			//    uses the scaled-hills module as the control module, causing the low-
			//    flat module to appear in the lower areas and causing the scaled-river-
			//    valley module to appear in the higher areas.
			hillyTerrain_bl = new Blend();
			hillyTerrain_bl[0] = hillyTerrain_co;
			hillyTerrain_bl[1] = hillyTerrain_sb1;
			hillyTerrain_bl.Controller = (hillyTerrain_sb0);

			// 7: [Scaled-hills-and-valleys module]: This scale/bias module slightly
			//    reduces the range of the output value from the hills-and-valleys
			//    module, decreasing the heights of the hilltops.
			hillyTerrain_sb2 = new ScaleBias();
			hillyTerrain_sb2[0] = hillyTerrain_bl;
			hillyTerrain_sb2.Scale = (0.75);
			hillyTerrain_sb2.Bias = (-0.25);

			// 8: [Increased-slope-hilly-terrain module]: To increase the hill slopes at
			//    higher elevations, this exponential-curve module applies an
			//    exponential curve to the output value the scaled-hills-and-valleys
			//    module.  This exponential-curve module expects the input value to
			//    range from -1.0 to 1.0.
			hillyTerrain_ex = new Exponent();
			hillyTerrain_ex[0] = hillyTerrain_sb2;
			hillyTerrain_ex.Value = (1.375);

			// 9: [Coarse-turbulence module]: This turbulence module warps the output
			//    value from the increased-slope-hilly-terrain module, adding some
			//    coarse detail to it.
			hillyTerrain_tu0 = new Turbulence();
			hillyTerrain_tu0[0] = hillyTerrain_ex;
			hillyTerrain_tu0.Seed = (CUR_SEED + 62);
			hillyTerrain_tu0.Frequency = (1531.0);
			hillyTerrain_tu0.Power = (1.0 / 16921.0 * HILLS_TWIST);
			hillyTerrain_tu0.Roughness = (4);

			// 10: [Warped-hilly-terrain module]: This turbulence module warps the
			//     output value from the coarse-turbulence module.  This turbulence has
			//     a higher frequency, but lower power, than the coarse-turbulence
			//     module, adding some fine detail to it.
			hillyTerrain_tu1 = new Turbulence();
			hillyTerrain_tu1[0] = hillyTerrain_tu0;
			hillyTerrain_tu1.Seed = (CUR_SEED + 63);
			hillyTerrain_tu1.Frequency = (21617.0);
			hillyTerrain_tu1.Power = (1.0 / 117529.0 * HILLS_TWIST);
			hillyTerrain_tu1.Roughness = (6);

			// 11: [Hilly-terrain group]: LibNoise.Operator.Caches the output value from the warped-hilly-
			//     terrain module.  This is the output value for the entire hilly-
			//     terrain group.
			hillyTerrain = new LibNoise.Operator.Cache();
			hillyTerrain[0] = hillyTerrain_tu1;


			////////////////////////////////////////////////////////////////////////////
			// Module group: plains terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: plains terrain (7 noise modules)
			//
			// This subgroup generates the plains terrain.
			//
			// Because this subgroup will eventually be flattened considerably, the
			// types and combinations of noise modules that generate the plains are not
			// really that important; they only need to "look" interesting.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Plains-basis-0 module]: This billow-noise module, along with the
			//    plains-basis-1 module, produces the plains.
			plainsTerrain_bi0 = new Billow();
			plainsTerrain_bi0.Seed = (CUR_SEED + 70);
			plainsTerrain_bi0.Frequency = (1097.5);
			plainsTerrain_bi0.Persistence = (0.5);
			plainsTerrain_bi0.Lacunarity = (PLAINS_LACUNARITY);
			plainsTerrain_bi0.OctaveCount = (8);
			plainsTerrain_bi0.Quality = (LibNoise.QualityMode.High);

			// 2: [Positive-plains-basis-0 module]: This scale/bias module makes the
			//    output value from the plains-basis-0 module positive since this output
			//    value will be multiplied together with the positive-plains-basis-1
			//    module.
			plainsTerrain_sb0 = new ScaleBias();
			plainsTerrain_sb0[0] = plainsTerrain_bi0;
			plainsTerrain_sb0.Scale = (0.5);
			plainsTerrain_sb0.Bias = (0.5);

			// 3: [Plains-basis-1 module]: This billow-noise module, along with the
			//    plains-basis-2 module, produces the plains.
			plainsTerrain_bi1 = new Billow();
			plainsTerrain_bi1.Seed = (CUR_SEED + 71);
			plainsTerrain_bi1.Frequency = (1319.5);
			plainsTerrain_bi1.Persistence = (0.5);
			plainsTerrain_bi1.Lacunarity = (PLAINS_LACUNARITY);
			plainsTerrain_bi1.OctaveCount = (8);
			plainsTerrain_bi1.Quality = (LibNoise.QualityMode.High);

			// 4: [Positive-plains-basis-1 module]: This scale/bias module makes the
			//    output value from the plains-basis-1 module positive since this output
			//    value will be multiplied together with the positive-plains-basis-0
			//    module.
			plainsTerrain_sb1 = new ScaleBias();
			plainsTerrain_sb1[0] = plainsTerrain_bi1;
			plainsTerrain_sb1.Scale = (0.5);
			plainsTerrain_sb1.Bias = (0.5);

			// 5: [Combined-plains-basis module]: This multiplication module combines
			//    the two plains basis modules together.
			plainsTerrain_mu = new Multiply();
			plainsTerrain_mu[0] = plainsTerrain_sb0;
			plainsTerrain_mu[1] = plainsTerrain_sb1;

			// 6: [Rescaled-plains-basis module]: This scale/bias module maps the output
			//    value that ranges from 0.0 to 1.0 back to a value that ranges from
			//    -1.0 to +1.0.
			plainsTerrain_sb2 = new ScaleBias();
			plainsTerrain_sb2[0] = plainsTerrain_mu;
			plainsTerrain_sb2.Scale = (2.0);
			plainsTerrain_sb2.Bias = (-1.0);

			// 7: [Plains-terrain group]: LibNoise.Operator.Caches the output value from the rescaled-
			//    plains-basis module.  This is the output value for the entire plains-
			//    terrain group.
			plainsTerrain = new LibNoise.Operator.Cache();
			plainsTerrain[0] = plainsTerrain_sb2;


			////////////////////////////////////////////////////////////////////////////
			// Module group: badlands terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: badlands sand (6 noise modules)
			//
			// This subgroup generates the sandy terrain for the badlands.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Sand-dunes module]: This ridged-multifractal-noise module generates
			//    sand dunes.  This ridged-multifractal noise is generated with a single
			//    octave, which makes very smooth dunes.
			badlandsSand_rm = new RidgedMultifractal();
			badlandsSand_rm.Seed = (CUR_SEED + 80);
			badlandsSand_rm.Frequency = (6163.5);
			badlandsSand_rm.Lacunarity = (BADLANDS_LACUNARITY);
			badlandsSand_rm.Quality = (LibNoise.QualityMode.High);
			badlandsSand_rm.OctaveCount = (1);

			// 2: [Scaled-sand-dunes module]: This scale/bias module shrinks the dune
			//    heights by a small amount.  This is necessary so that the subsequent
			//    noise modules in this subgroup can add some detail to the dunes.
			badlandsSand_sb0 = new ScaleBias();
			badlandsSand_sb0[0] = badlandsSand_rm;
			badlandsSand_sb0.Scale = (0.875);
			badlandsSand_sb0.Bias = (0.0);

			// 3: [Dune-detail module]: This noise module uses Voronoi polygons to
			//    generate the detail to add to the dunes.  By enabling the distance
			//    algorithm, small polygonal pits are generated; the edges of the pits
			//    are joined to the edges of nearby pits.
			badlandsSand_vo = new Voronoi();
			badlandsSand_vo.Seed = (CUR_SEED + 81);
			badlandsSand_vo.Frequency = (16183.25);
			badlandsSand_vo.Displacement = (0.0);
			badlandsSand_vo.UseDistance = true;

			// 4: [Scaled-dune-detail module]: This scale/bias module shrinks the dune
			//    details by a large amount.  This is necessary so that the subsequent
			//    noise modules in this subgroup can add this detail to the sand-dunes
			//    module.
			badlandsSand_sb1 = new ScaleBias();
			badlandsSand_sb1[0] = badlandsSand_vo;
			badlandsSand_sb1.Scale = (0.25);
			badlandsSand_sb1.Bias = (0.25);

			// 5: [Dunes-with-detail module]: This addition module combines the scaled-
			//    sand-dunes module with the scaled-dune-detail module.
			badlandsSand_ad = new Add();
			badlandsSand_ad[0] = badlandsSand_sb0;
			badlandsSand_ad[1] = badlandsSand_sb1;

			// 6: [Badlands-sand subgroup]: LibNoise.Operator.Caches the output value from the dunes-with-
			//    detail module.
			badlandsSand = new LibNoise.Operator.Cache();
			badlandsSand[0] = badlandsSand_ad;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: badlands cliffs (7 noise modules)
			//
			// This subgroup generates the cliffs for the badlands.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Cliff-basis module]: This Perlin-noise module generates some coherent
			//    noise that will be used to generate the cliffs.
			badlandsCliffs_pe = new Perlin();
			badlandsCliffs_pe.Seed = (CUR_SEED + 90);
			badlandsCliffs_pe.Frequency = (CONTINENT_FREQUENCY * 839.0);
			badlandsCliffs_pe.Persistence = (0.5);
			badlandsCliffs_pe.Lacunarity = (BADLANDS_LACUNARITY);
			badlandsCliffs_pe.OctaveCount = (6);
			badlandsCliffs_pe.Quality = (LibNoise.QualityMode.Medium);

			// 2: [Cliff-shaping module]: Next, this curve module applies a curve to the
			//    output value from the cliff-basis module.  This curve is initially
			//    very shallow, but then its slope increases sharply.  At the highest
			//    elevations, the curve becomes very flat again.  This produces the
			//    stereotypical Utah-style desert cliffs.
			badlandsCliffs_cu = new Curve();
			badlandsCliffs_cu[0] = badlandsCliffs_pe;
			badlandsCliffs_cu.Add (-2.0000, -2.0000);
			badlandsCliffs_cu.Add (-1.0000, -1.2500);
			badlandsCliffs_cu.Add (-0.0000, -0.7500);
			badlandsCliffs_cu.Add ( 0.5000, -0.2500);
			badlandsCliffs_cu.Add ( 0.6250,  0.8750);
			badlandsCliffs_cu.Add ( 0.7500,  1.0000);
			badlandsCliffs_cu.Add ( 2.0000,  1.2500);

			// 3: [Clamped-cliffs module]: This clamping module makes the tops of the
			//    cliffs very flat by clamping the output value from the cliff-shaping
			//    module so that the tops of the cliffs are very flat.
			badlandsCliffs_cl = new Clamp();
			badlandsCliffs_cl[0] = badlandsCliffs_cu;
			badlandsCliffs_cl.SetBounds(-999.125, 0.875);

			// 4: [Terraced-cliffs module]: Next, this terracing module applies some
			//    terraces to the clamped-cliffs module in the lower elevations before
			//    the sharp cliff transition.
			badlandsCliffs_te = new Terrace();
			badlandsCliffs_te[0] = badlandsCliffs_cl;
			badlandsCliffs_te.Add (-1.0000);
			badlandsCliffs_te.Add (-0.8750);
			badlandsCliffs_te.Add (-0.7500);
			badlandsCliffs_te.Add (-0.5000);
			badlandsCliffs_te.Add ( 0.0000);
			badlandsCliffs_te.Add ( 1.0000);

			// 5: [Coarse-turbulence module]: This turbulence module warps the output
			//    value from the terraced-cliffs module, adding some coarse detail to
			//    it.
			badlandsCliffs_tu0 = new Turbulence();
			badlandsCliffs_tu0.Seed = (CUR_SEED + 91);
			badlandsCliffs_tu0[0] = badlandsCliffs_te;
			badlandsCliffs_tu0.Frequency = (16111.0);
			badlandsCliffs_tu0.Power = (1.0 / 141539.0 * BADLANDS_TWIST);
			badlandsCliffs_tu0.Roughness = (3);

			// 6: [Warped-cliffs module]: This turbulence module warps the output value
			//    from the coarse-turbulence module.  This turbulence has a higher
			//    frequency, but lower power, than the coarse-turbulence module, adding
			//    some fine detail to it.
			badlandsCliffs_tu1 = new Turbulence();
			badlandsCliffs_tu1.Seed = (CUR_SEED + 92);
			badlandsCliffs_tu1[0] = badlandsCliffs_tu0;
			badlandsCliffs_tu1.Frequency = (36107.0);
			badlandsCliffs_tu1.Power = (1.0 / 211543.0 * BADLANDS_TWIST);
			badlandsCliffs_tu1.Roughness = (3);

			// 7: [Badlands-cliffs subgroup]: LibNoise.Operator.Caches the output value from the warped-
			//    cliffs module.
			badlandsCliffs = new LibNoise.Operator.Cache();
			badlandsCliffs[0] = badlandsCliffs_tu1;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: badlands terrain (3 noise modules)
			//
			// Generates the final badlands terrain.
			//
			// Using a scale/bias module, the badlands sand is flattened considerably,
			// then the sand elevations are lowered to around -1.0.  The maximum value
			// from the flattened sand module and the cliff module contributes to the
			// final elevation.  This causes sand to appear at the low elevations since
			// the sand is slightly higher than the cliff base.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Scaled-sand-dunes module]: This scale/bias module considerably
			//    flattens the output value from the badlands-sands subgroup and lowers
			//    this value to near -1.0.
			badlandsTerrain_sb = new ScaleBias();
			badlandsTerrain_sb[0] = badlandsSand;
			badlandsTerrain_sb.Scale = (0.25);
			badlandsTerrain_sb.Bias = (-0.75);

			// 2: [Dunes-and-cliffs module]: This maximum-value module causes the dunes
			//    to appear in the low areas and the cliffs to appear in the high areas.
			//    It does this by selecting the maximum of the output values from the
			//    scaled-sand-dunes module and the badlands-cliffs subgroup.
			badlandsTerrain_ma = new Max();
			badlandsTerrain_ma[0] = badlandsCliffs;
			badlandsTerrain_ma[1] = badlandsTerrain_sb;

			// 3: [Badlands-terrain group]: LibNoise.Operator.Caches the output value from the dunes-and-
			//    cliffs module.  This is the output value for the entire badlands-
			//    terrain group.
			badlandsTerrain = new LibNoise.Operator.Cache();
			badlandsTerrain[0] = badlandsTerrain_ma;


			////////////////////////////////////////////////////////////////////////////
			// Module group: river positions
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: river positions (7 noise modules)
			//
			// This subgroup generates the river positions.
			//
			// -1.0 represents the lowest elevations and +1.0 represents the highest
			// elevations.
			//

			// 1: [Large-river-basis module]: This ridged-multifractal-noise module
			//    creates the large, deep rivers.
			riverPositions_rm0 = new RidgedMultifractal();
			riverPositions_rm0.Seed = (CUR_SEED + 100);
			riverPositions_rm0.Frequency = (18.75);
			riverPositions_rm0.Lacunarity = (CONTINENT_LACUNARITY);
			riverPositions_rm0.OctaveCount = (1);
			riverPositions_rm0.Quality = (LibNoise.QualityMode.High);

			// 2: [Large-river-curve module]: This curve module applies a curve to the
			//    output value from the large-river-basis module so that the ridges
			//    become inverted.  This creates the rivers.  This curve also compresses
			//    the edge of the rivers, producing a sharp transition from the land to
			//    the river bottom.
			riverPositions_cu0 = new Curve();
			riverPositions_cu0[0] = riverPositions_rm0;
			riverPositions_cu0.Add (-2.000,  2.000);
			riverPositions_cu0.Add (-1.000,  1.000);
			riverPositions_cu0.Add (-0.125,  0.875);
			riverPositions_cu0.Add ( 0.000, -1.000);
			riverPositions_cu0.Add ( 1.000, -1.500);
			riverPositions_cu0.Add ( 2.000, -2.000);

			/// 3: [Small-river-basis module]: This ridged-multifractal-noise module
			//     creates the small, shallow rivers.
			riverPositions_rm1 = new RidgedMultifractal();
			riverPositions_rm1.Seed = (CUR_SEED + 101);
			riverPositions_rm1.Frequency = (43.25);
			riverPositions_rm1.Lacunarity = (CONTINENT_LACUNARITY);
			riverPositions_rm1.OctaveCount = (1);
			riverPositions_rm1.Quality = (LibNoise.QualityMode.High);

			// 4: [Small-river-curve module]: This curve module applies a curve to the
			//    output value from the small-river-basis module so that the ridges
			//    become inverted.  This creates the rivers.  This curve also compresses
			//    the edge of the rivers, producing a sharp transition from the land to
			//    the river bottom.
			riverPositions_cu1 = new Curve();
			riverPositions_cu1[0] = riverPositions_rm1;
			riverPositions_cu1.Add (-2.000,  2.0000);
			riverPositions_cu1.Add (-1.000,  1.5000);
			riverPositions_cu1.Add (-0.125,  1.4375);
			riverPositions_cu1.Add ( 0.000,  0.5000);
			riverPositions_cu1.Add ( 1.000,  0.2500);
			riverPositions_cu1.Add ( 2.000,  0.0000);

			// 5: [Combined-rivers module]: This minimum-value module causes the small
			//    rivers to cut into the large rivers.  It does this by selecting the
			//    minimum output values from the large-river-curve module and the small-
			//    river-curve module.
			riverPositions_mi = new Min();
			riverPositions_mi[0] = riverPositions_cu0;
			riverPositions_mi[1] = riverPositions_cu1;

			// 6: [Warped-rivers module]: This turbulence module warps the output value
			//    from the combined-rivers module, which twists the rivers.  The high
			//    roughness produces less-smooth rivers.
			riverPositions_tu = new Turbulence();
			riverPositions_tu[0] = riverPositions_mi;
			riverPositions_tu.Seed = (CUR_SEED + 102);
			riverPositions_tu.Frequency = (9.25);
			riverPositions_tu.Power = (1.0 / 57.75);
			riverPositions_tu.Roughness = (6);

			// 7: [River-positions group]: LibNoise.Operator.Caches the output value from the warped-
			//    rivers module.  This is the output value for the entire river-
			//    positions group.
			riverPositions = new LibNoise.Operator.Cache();
			riverPositions[0] = riverPositions_tu;


			////////////////////////////////////////////////////////////////////////////
			// Module group: scaled mountainous terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: scaled mountainous terrain (6 noise modules)
			//
			// This subgroup scales the output value from the mountainous-terrain group
			// so that it can be added to the elevation defined by the continent-
			// definition group.
			//
			// This subgroup scales the output value such that it is almost always
			// positive.  This is done so that a negative elevation does not get applied
			// to the continent-definition group, preventing parts of that group from
			// having negative terrain features "stamped" into it.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Base-scaled-mountainous-terrain module]: This scale/bias module
			//    scales the output value from the mountainous-terrain group so that the
			//    output value is measured in planetary elevation units.
			scaledMountainousTerrain_sb0 = new ScaleBias();
			scaledMountainousTerrain_sb0[0] = mountainousTerrain;
			scaledMountainousTerrain_sb0.Scale = (0.125);
			scaledMountainousTerrain_sb0.Bias = (0.125);

			// 2: [Base-peak-modulation module]: At this stage, most mountain peaks have
			//    roughly the same elevation.  This Perlin-noise module generates some
			//    random values that will be used by subsequent noise modules to
			//    randomly change the elevations of the mountain peaks.
			scaledMountainousTerrain_pe = new Perlin();
			scaledMountainousTerrain_pe.Seed = (CUR_SEED + 110);
			scaledMountainousTerrain_pe.Frequency = (14.5);
			scaledMountainousTerrain_pe.Persistence = (0.5);
			scaledMountainousTerrain_pe.Lacunarity = (MOUNTAIN_LACUNARITY);
			scaledMountainousTerrain_pe.OctaveCount = (6);
			scaledMountainousTerrain_pe.Quality = (LibNoise.QualityMode.Medium);

			// 3: [Peak-modulation module]: This exponential-curve module applies an
			//    exponential curve to the output value from the base-peak-modulation
			//    module.  This produces a small number of high values and a much larger
			//    number of low values.  This means there will be a few peaks with much
			//    higher elevations than the majority of the peaks, making the terrain
			//    features more varied.
			scaledMountainousTerrain_ex = new Exponent();
			scaledMountainousTerrain_ex[0] = scaledMountainousTerrain_pe;
			scaledMountainousTerrain_ex.Value = (1.25);

			// 4: [Scaled-peak-modulation module]: This scale/bias module modifies the
			//    range of the output value from the peak-modulation module so that it
			//    can be used as the modulator for the peak-height-multiplier module.
			//    It is important that this output value is not much lower than 1.0.
			scaledMountainousTerrain_sb1 = new ScaleBias();
			scaledMountainousTerrain_sb1[0] = scaledMountainousTerrain_ex;
			scaledMountainousTerrain_sb1.Scale = (0.25);
			scaledMountainousTerrain_sb1.Bias = (1.0);

			// 5: [Peak-height-multiplier module]: This multiplier module modulates the
			//    heights of the mountain peaks from the base-scaled-mountainous-terrain
			//    module using the output value from the scaled-peak-modulation module.
			scaledMountainousTerrain_mu = new Multiply();
			scaledMountainousTerrain_mu[0] = scaledMountainousTerrain_sb0;
			scaledMountainousTerrain_mu[1] = scaledMountainousTerrain_sb1;

			// 6: [Scaled-mountainous-terrain group]: LibNoise.Operator.Caches the output value from the
			//    peak-height-multiplier module.  This is the output value for the
			//    entire scaled-mountainous-terrain group.
			scaledMountainousTerrain = new LibNoise.Operator.Cache();
			scaledMountainousTerrain[0] = scaledMountainousTerrain_mu;


			////////////////////////////////////////////////////////////////////////////
			// Module group: scaled hilly terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: scaled hilly terrain (6 noise modules)
			//
			// This subgroup scales the output value from the hilly-terrain group so
			// that it can be added to the elevation defined by the continent-
			// definition group.  The scaling amount applied to the hills is one half of
			// the scaling amount applied to the scaled-mountainous-terrain group.
			//
			// This subgroup scales the output value such that it is almost always
			// positive.  This is done so that negative elevations are not applied to
			// the continent-definition group, preventing parts of the continent-
			// definition group from having negative terrain features "stamped" into it.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Base-scaled-hilly-terrain module]: This scale/bias module scales the
			//    output value from the hilly-terrain group so that this output value is
			//    measured in planetary elevation units 
			scaledHillyTerrain_sb0 = new ScaleBias();
			scaledHillyTerrain_sb0[0] = hillyTerrain;
			scaledHillyTerrain_sb0.Scale = (0.0625);
			scaledHillyTerrain_sb0.Bias = (0.0625);

			// 2: [Base-hilltop-modulation module]: At this stage, most hilltops have
			//    roughly the same elevation.  This Perlin-noise module generates some
			//    random values that will be used by subsequent noise modules to
			//    randomly change the elevations of the hilltops.
			scaledHillyTerrain_pe = new Perlin();
			scaledHillyTerrain_pe.Seed = (CUR_SEED + 120);
			scaledHillyTerrain_pe.Frequency = (13.5);
			scaledHillyTerrain_pe.Persistence = (0.5);
			scaledHillyTerrain_pe.Lacunarity = (HILLS_LACUNARITY);
			scaledHillyTerrain_pe.OctaveCount = (6);
			scaledHillyTerrain_pe.Quality = (LibNoise.QualityMode.Medium);

			// 3: [Hilltop-modulation module]: This exponential-curve module applies an
			//    exponential curve to the output value from the base-hilltop-modulation
			//    module.  This produces a small number of high values and a much larger
			//    number of low values.  This means there will be a few hilltops with
			//    much higher elevations than the majority of the hilltops, making the
			//    terrain features more varied.
			scaledHillyTerrain_ex = new Exponent();
			scaledHillyTerrain_ex[0] = scaledHillyTerrain_pe;
			scaledHillyTerrain_ex.Value = (1.25);

			// 4: [Scaled-hilltop-modulation module]: This scale/bias module modifies
			//    the range of the output value from the hilltop-modulation module so
			//    that it can be used as the modulator for the hilltop-height-multiplier
			//    module.  It is important that this output value is not much lower than
			//    1.0.
			scaledHillyTerrain_sb1 = new ScaleBias();
			scaledHillyTerrain_sb1[0] = scaledHillyTerrain_ex;
			scaledHillyTerrain_sb1.Scale = (0.5);
			scaledHillyTerrain_sb1.Bias = (1.5);

			// 5: [Hilltop-height-multiplier module]: This multiplier module modulates
			//    the heights of the hilltops from the base-scaled-hilly-terrain module
			//    using the output value from the scaled-hilltop-modulation module.
			scaledHillyTerrain_mu = new Multiply();
			scaledHillyTerrain_mu[0] = scaledHillyTerrain_sb0;
			scaledHillyTerrain_mu[1] = scaledHillyTerrain_sb1;

			// 6: [Scaled-hilly-terrain group]: LibNoise.Operator.Caches the output value from the
			//    hilltop-height-multiplier module.  This is the output value for the
			//    entire scaled-hilly-terrain group.
			scaledHillyTerrain = new LibNoise.Operator.Cache();
			scaledHillyTerrain[0] = scaledHillyTerrain_mu;


			////////////////////////////////////////////////////////////////////////////
			// Module group: scaled plains terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: scaled plains terrain (2 noise modules)
			//
			// This subgroup scales the output value from the plains-terrain group so
			// that it can be added to the elevations defined by the continent-
			// definition group.
			//
			// This subgroup scales the output value such that it is almost always
			// positive.  This is done so that negative elevations are not applied to
			// the continent-definition group, preventing parts of the continent-
			// definition group from having negative terrain features "stamped" into it.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Scaled-plains-terrain module]: This scale/bias module greatly
			//    flattens the output value from the plains terrain.  This output value
			//    is measured in planetary elevation units 
			scaledPlainsTerrain_sb = new ScaleBias();
			scaledPlainsTerrain_sb[0] = plainsTerrain;
			scaledPlainsTerrain_sb.Scale = (0.00390625);
			scaledPlainsTerrain_sb.Bias = (0.0078125);

			// 2: [Scaled-plains-terrain group]: LibNoise.Operator.Caches the output value from the
			//    scaled-plains-terrain module.  This is the output value for the entire
			//    scaled-plains-terrain group.
			scaledPlainsTerrain = new LibNoise.Operator.Cache();
			scaledPlainsTerrain[0] = scaledPlainsTerrain_sb;


			////////////////////////////////////////////////////////////////////////////
			// Module group: scaled badlands terrain
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: scaled badlands terrain (2 noise modules)
			//
			// This subgroup scales the output value from the badlands-terrain group so
			// that it can be added to the elevations defined by the continent-
			// definition group.
			//
			// This subgroup scales the output value such that it is almost always
			// positive.  This is done so that negative elevations are not applied to
			// the continent-definition group, preventing parts of the continent-
			// definition group from having negative terrain features "stamped" into it.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Scaled-badlands-terrain module]: This scale/bias module scales the
			//    output value from the badlands-terrain group so that it is measured
			//    in planetary elevation units 
			scaledBadlandsTerrain_sb = new ScaleBias();
			scaledBadlandsTerrain_sb[0] = badlandsTerrain;
			scaledBadlandsTerrain_sb.Scale = (0.0625);
			scaledBadlandsTerrain_sb.Bias = (0.0625);

			// 2: [Scaled-badlands-terrain group]: LibNoise.Operator.Caches the output value from the
			//    scaled-badlands-terrain module.  This is the output value for the
			//    entire scaled-badlands-terrain group.
			scaledBadlandsTerrain = new LibNoise.Operator.Cache();
			scaledBadlandsTerrain[0] = scaledBadlandsTerrain_sb;


			////////////////////////////////////////////////////////////////////////////
			// Module group: final planet
			////////////////////////////////////////////////////////////////////////////

			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continental shelf (6 noise modules)
			//
			// This module subgroup creates the continental shelves.
			//
			// The output value from this module subgroup are measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Shelf-creator module]: This terracing module applies a terracing
			//    curve to the continent-definition group at the specified shelf level.
			//    This terrace becomes the continental shelf.  Note that this terracing
			//    module also places another terrace below the continental shelf near
			//    -1.0.  The bottom of this terrace is defined as the bottom of the
			//    ocean; subsequent noise modules will later add oceanic trenches to the
			//    bottom of the ocean.
			continentalShelf_te = new Terrace();
			continentalShelf_te[0] = continentDef;
			continentalShelf_te.Add (-1.0);
			continentalShelf_te.Add (-0.75);
			continentalShelf_te.Add (SHELF_LEVEL);
			continentalShelf_te.Add (1.0);

			// 2: [Oceanic-trench-basis module]: This ridged-multifractal-noise module
			//    generates some coherent noise that will be used to generate the
			//    oceanic trenches.  The ridges represent the bottom of the trenches.
			continentalShelf_rm = new RidgedMultifractal();
			continentalShelf_rm.Seed = (CUR_SEED + 130);
			continentalShelf_rm.Frequency = (CONTINENT_FREQUENCY * 4.375);
			continentalShelf_rm.Lacunarity = (CONTINENT_LACUNARITY);
			continentalShelf_rm.OctaveCount = (16);
			continentalShelf_rm.Quality = (LibNoise.QualityMode.High);

			// 3: [Oceanic-trench module]: This scale/bias module inverts the ridges
			//    from the oceanic-trench-basis-module so that the ridges become
			//    trenches.  This noise module also reduces the depth of the trenches so
			//    that their depths are measured in planetary elevation units.
			continentalShelf_sb = new ScaleBias();
			continentalShelf_sb[0] = continentalShelf_rm;
			continentalShelf_sb.Scale = (-0.125);
			continentalShelf_sb.Bias = (-0.125);

			// 4: [Clamped-sea-bottom module]: This clamping module clamps the output
			//    value from the shelf-creator module so that its possible range is
			//    from the bottom of the ocean to sea level.  This is done because this
			//    subgroup is only concerned about the oceans.
			continentalShelf_cl = new Clamp();
			continentalShelf_cl[0] = continentalShelf_te;
			continentalShelf_cl.SetBounds(-0.75, SEA_LEVEL);

			// 5: [Shelf-and-trenches module]: This addition module adds the oceanic
			//    trenches to the clamped-sea-bottom module.
			continentalShelf_ad = new Add();
			continentalShelf_ad[0] = continentalShelf_sb;
			continentalShelf_ad[1] = continentalShelf_cl;

			// 6: [Continental-shelf subgroup]: LibNoise.Operator.Caches the output value from the shelf-
			//    and-trenches module.
			continentalShelf = new LibNoise.Operator.Cache();
			continentalShelf[0] = continentalShelf_ad;


			////////////////////////////////////////////////////////////////////////////
			// Module group: base continent elevations (3 noise modules)
			//
			// This subgroup generates the base elevations for the continents, before
			// terrain features are added.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Base-scaled-continent-elevations module]: This scale/bias module
			//    scales the output value from the continent-definition group so that it
			//    is measured in planetary elevation units 
			baseContinentElev_sb = new ScaleBias();
			baseContinentElev_sb[0] = continentDef;
			baseContinentElev_sb.Scale = (CONTINENT_HEIGHT_SCALE);
			baseContinentElev_sb.Bias = (0.0);

			// 2: [Base-continent-with-oceans module]: This selector module applies the
			//    elevations of the continental shelves to the base elevations of the
			//    continent.  It does this by selecting the output value from the
			//    continental-shelf subgroup if the corresponding output value from the
			//    continent-definition group is below the shelf level.  Otherwise, it
			//    selects the output value from the base-scaled-continent-elevations
			//    module.
			baseContinentElev_se = new Select();
			baseContinentElev_se[0] = baseContinentElev_sb;
			baseContinentElev_se[1] = continentalShelf;
			baseContinentElev_se.Controller = (continentDef);
			baseContinentElev_se.SetBounds(SHELF_LEVEL - 1000.0, SHELF_LEVEL);
			baseContinentElev_se.Falloff = (0.03125);

			// 3: [Base-continent-elevation subgroup]: LibNoise.Operator.Caches the output value from the
			//    base-continent-with-oceans module.
			baseContinentElev = new LibNoise.Operator.Cache();
			baseContinentElev[0] = baseContinentElev_se;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continents with plains (2 noise modules)
			//
			// This subgroup applies the scaled-plains-terrain group to the base-
			// continent-elevation subgroup.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Continents-with-plains module]:  This addition module adds the
			//    scaled-plains-terrain group to the base-continent-elevation subgroup.
			continentsWithPlains_ad = new Add();
			continentsWithPlains_ad[0] = baseContinentElev;
			continentsWithPlains_ad[1] = scaledPlainsTerrain;

			// 2: [Continents-with-plains subgroup]: LibNoise.Operator.Caches the output value from the
			//    continents-with-plains module.
			continentsWithPlains = new LibNoise.Operator.Cache();
			continentsWithPlains[0] = continentsWithPlains_ad;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continents with hills (3 noise modules)
			//
			// This subgroup applies the scaled-hilly-terrain group to the continents-
			// with-plains subgroup.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Continents-with-hills module]:  This addition module adds the scaled-
			//    hilly-terrain group to the base-continent-elevation subgroup.
			continentsWithHills_ad = new Add();
			continentsWithHills_ad[0] = baseContinentElev;
			continentsWithHills_ad[1] = scaledHillyTerrain;

			// 2: [Select-high-elevations module]: This selector module ensures that
			//    the hills only appear at higher elevations.  It does this by selecting
			//    the output value from the continent-with-hills module if the
			//    corresponding output value from the terrain-type-defintion group is
			//    above a certain value. Otherwise, it selects the output value from the
			//    continents-with-plains subgroup.
			continentsWithHills_se = new Select();
			continentsWithHills_se[0] = continentsWithPlains;
			continentsWithHills_se[1] = continentsWithHills_ad;
			continentsWithHills_se.Controller = (terrainTypeDef);
			continentsWithHills_se.SetBounds(1.0 - HILLS_AMOUNT, 1001.0 - HILLS_AMOUNT);
			continentsWithHills_se.Falloff = (0.25);

			// 3: [Continents-with-hills subgroup]: LibNoise.Operator.Caches the output value from the
			//    select-high-elevations module.
			continentsWithHills = new LibNoise.Operator.Cache();
			continentsWithHills[0] = continentsWithHills_se;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continents with mountains (5 noise modules)
			//
			// This subgroup applies the scaled-mountainous-terrain group to the
			// continents-with-hills subgroup.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Continents-and-mountains module]:  This addition module adds the
			//    scaled-mountainous-terrain group to the base-continent-elevation
			//    subgroup.
			continentsWithMountains_ad0 = new Add();
			continentsWithMountains_ad0[0] = baseContinentElev;
			continentsWithMountains_ad0[1] = scaledMountainousTerrain;

			// 2: [Increase-mountain-heights module]:  This curve module applies a curve
			//    to the output value from the continent-definition group.  This
			//    modified output value is used by a subsequent noise module to add
			//    additional height to the mountains based on the current continent
			//    elevation.  The higher the continent elevation, the higher the
			//    mountains.
			continentsWithMountains_cu = new Curve();
			continentsWithMountains_cu[0] = continentDef;
			continentsWithMountains_cu.Add (                  -1.0, -0.0625);
			continentsWithMountains_cu.Add (                   0.0,  0.0000);
			continentsWithMountains_cu.Add (1.0 - MOUNTAINS_AMOUNT,  0.0625);
			continentsWithMountains_cu.Add (                   1.0,  0.2500);

			// 3: [Add-increased-mountain-heights module]: This addition module adds
			//    the increased-mountain-heights module to the continents-and-
			//    mountains module.  The highest continent elevations now have the
			//    highest mountains.
			continentsWithMountains_ad1 = new Add();
			continentsWithMountains_ad1[0] = continentsWithMountains_ad0;
			continentsWithMountains_ad1[1] = continentsWithMountains_cu;

			// 4: [Select-high-elevations module]: This selector module ensures that
			//    mountains only appear at higher elevations.  It does this by selecting
			//    the output value from the continent-with-mountains module if the
			//    corresponding output value from the terrain-type-defintion group is
			//    above a certain value.  Otherwise, it selects the output value from
			//    the continents-with-hills subgroup.  Note that the continents-with-
			//    hills subgroup also contains the plains terrain.
			continentsWithMountains_se = new Select();
			continentsWithMountains_se[0] = continentsWithHills;
			continentsWithMountains_se[1] = continentsWithMountains_ad1;
			continentsWithMountains_se.Controller = (terrainTypeDef);
			continentsWithMountains_se.SetBounds(1.0 - MOUNTAINS_AMOUNT,
				1001.0 - MOUNTAINS_AMOUNT);
			continentsWithMountains_se.Falloff = (0.25);

			// 5: [Continents-with-mountains subgroup]: LibNoise.Operator.Caches the output value from
			//    the select-high-elevations module.
			continentsWithMountains = new LibNoise.Operator.Cache();
			continentsWithMountains[0] = continentsWithMountains_se;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continents with badlands (5 noise modules)
			//
			// This subgroup applies the scaled-badlands-terrain group to the
			// continents-with-mountains subgroup.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Badlands-positions module]: This Perlin-noise module generates some
			//    random noise, which is used by subsequent noise modules to specify the
			//    locations of the badlands.
			continentsWithBadlands_pe = new Perlin();
			continentsWithBadlands_pe.Seed = (CUR_SEED + 140);
			continentsWithBadlands_pe.Frequency = (16.5);
			continentsWithBadlands_pe.Persistence = (0.5);
			continentsWithBadlands_pe.Lacunarity = (CONTINENT_LACUNARITY);
			continentsWithBadlands_pe.OctaveCount = (2);
			continentsWithBadlands_pe.Quality = (LibNoise.QualityMode.Medium);

			// 2: [Continents-and-badlands module]:  This addition module adds the
			//    scaled-badlands-terrain group to the base-continent-elevation
			//    subgroup.
			continentsWithBadlands_ad = new Add();
			continentsWithBadlands_ad[0] = baseContinentElev;
			continentsWithBadlands_ad[1] = scaledBadlandsTerrain;

			// 3: [Select-badlands-positions module]: This selector module places
			//    badlands at random spots on the continents based on the Perlin noise
			//    generated by the badlands-positions module.  To do this, it selects
			//    the output value from the continents-and-badlands module if the
			//    corresponding output value from the badlands-position module is
			//    greater than a specified value.  Otherwise, this selector module
			//    selects the output value from the continents-with-mountains subgroup.
			//    There is also a wide transition between these two noise modules so
			//    that the badlands can blend into the rest of the terrain on the
			//    continents.
			continentsWithBadlands_se = new Select();
			continentsWithBadlands_se[0] = continentsWithMountains;
			continentsWithBadlands_se[1] = continentsWithBadlands_ad;
			continentsWithBadlands_se.Controller = (continentsWithBadlands_pe);
			continentsWithBadlands_se.SetBounds(1.0 - BADLANDS_AMOUNT,
				1001.0 - BADLANDS_AMOUNT);
			continentsWithBadlands_se.Falloff = (0.25);

			// 4: [Apply-badlands module]: This maximum-value module causes the badlands
			//    to "poke out" from the rest of the terrain.  It does this by ensuring
			//    that only the maximum of the output values from the continents-with-
			//    mountains subgroup and the select-badlands-positions modules
			//    contribute to the output value of this subgroup.  One side effect of
			//    this process is that the badlands will not appear in mountainous
			//    terrain.
			continentsWithBadlands_ma = new Max();
			continentsWithBadlands_ma[0] = continentsWithMountains;
			continentsWithBadlands_ma[1] = continentsWithBadlands_se;

			// 5: [Continents-with-badlands subgroup]: LibNoise.Operator.Caches the output value from the
			//    apply-badlands module.
			continentsWithBadlands = new LibNoise.Operator.Cache();
			continentsWithBadlands[0] = continentsWithBadlands_ma;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: continents with rivers (4 noise modules)
			//
			// This subgroup applies the river-positions group to the continents-with-
			// badlands subgroup.
			//
			// The output value from this module subgroup is measured in planetary
			// elevation units (-1.0 for the lowest underwater trenches and +1.0 for the
			// highest mountain peaks.)
			//

			// 1: [Scaled-rivers module]: This scale/bias module scales the output value
			//    from the river-positions group so that it is measured in planetary
			//    elevation units and is negative; this is required for step 2.
			continentsWithRivers_sb = new ScaleBias();
			continentsWithRivers_sb[0] = riverPositions;
			continentsWithRivers_sb.Scale = (RIVER_DEPTH / 2.0);
			continentsWithRivers_sb.Bias = (-RIVER_DEPTH / 2.0);

			// 2: [Add-rivers-to-continents module]: This addition module adds the
			//    rivers to the continents-with-badlands subgroup.  Because the scaled-
			//    rivers module only outputs a negative value, the scaled-rivers module
			//    carves the rivers out of the terrain.
			continentsWithRivers_ad = new Add();
			continentsWithRivers_ad[0] = continentsWithBadlands;
			continentsWithRivers_ad[1] = continentsWithRivers_sb;

			// 3: [Blended-rivers-to-continents module]: This selector module outputs
			//    deep rivers near sea level and shallower rivers in higher terrain.  It
			//    does this by selecting the output value from the continents-with-
			//    badlands subgroup if the corresponding output value from the
			//    continents-with-badlands subgroup is far from sea level.  Otherwise,
			//    this selector module selects the output value from the add-rivers-to-
			//    continents module.
			continentsWithRivers_se = new Select();
			continentsWithRivers_se[0] = continentsWithBadlands;
			continentsWithRivers_se[1] = continentsWithRivers_ad;
			continentsWithRivers_se.Controller = (continentsWithBadlands);
			continentsWithRivers_se.SetBounds(SEA_LEVEL,
				CONTINENT_HEIGHT_SCALE + SEA_LEVEL);
			continentsWithRivers_se.Falloff = (CONTINENT_HEIGHT_SCALE - SEA_LEVEL);

			// 4: [Continents-with-rivers subgroup]: LibNoise.Operator.Caches the output value from the
			//    blended-rivers-to-continents module.
			continentsWithRivers = new LibNoise.Operator.Cache();
			continentsWithRivers[0] = continentsWithRivers_se;


			////////////////////////////////////////////////////////////////////////////
			// Module subgroup: unscaled final planet (1 noise module)
			//
			// This subgroup simply caches the output value from the continent-with-
			// rivers subgroup to contribute to the final output value.
			//

			// 1: [Unscaled-final-planet subgroup]: LibNoise.Operator.Caches the output value from the
			//    continent-with-rivers subgroup.
			unscaledFinalPlanet = new LibNoise.Operator.Cache();
			unscaledFinalPlanet[0] = continentsWithRivers;
		}
    }
}