using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TerrainGenerator
{
    public class ChunkCache
    {
		private readonly int MaxChunkThreads = SystemInfo.processorCount - 1;

        private Dictionary<Vector2i, TerrainChunk> RequestedChunks { get; set; }

        private Dictionary<Vector2i, TerrainChunk> ChunksBeingGenerated { get; set; }

		private Dictionary<Vector2i, TerrainChunk> GeneratedChunks { get; set; }

		private Dictionary<Vector2i, TerrainChunk> LoadedChunks { get; set; }

        private HashSet<Vector2i> ChunksToRemove { get; set; }

        public OnChunkGeneratedDelegate OnChunkGenerated { get; set; }

        public ChunkCache()
        {
            RequestedChunks = new Dictionary<Vector2i, TerrainChunk>();
            ChunksBeingGenerated = new Dictionary<Vector2i, TerrainChunk>();
			GeneratedChunks = new Dictionary<Vector2i, TerrainChunk>();
            LoadedChunks = new Dictionary<Vector2i, TerrainChunk>();
            ChunksToRemove = new HashSet<Vector2i>();
        }

		public void Update(Vector2i chunkPosition, List<TerrainChunk> ready)
        {
			TryToDeleteQueuedChunks();
			GenerateHeightmapForAvailableChunks(chunkPosition);

			// Adding all ready chuncks in ChunksGenerated if not loaded
			GeneratedChunks.Clear();
			foreach (var terrain in ready) {
				GeneratedChunks.Add (terrain.Position, terrain);
			}
			CreateTerrainForReadyChunks();
        }

        public void AddNewChunk(TerrainChunk chunk)
        {
            RequestedChunks.Add(chunk.Position, chunk);
        }

		public void RemoveChunk(Vector2i key)
        {
			ChunksToRemove.Add(key);
        }

		public bool ChunkCanBeAdded(Vector2i key)
        {
			return !ChunkCanBeRemoved (key);
        }

		public bool ChunkCanBeRemoved(Vector2i key)
        {
			return
                RequestedChunks.ContainsKey (key)
			|| ChunksBeingGenerated.ContainsKey (key);
        }

		public bool IsChunkLoaded(Vector2i chunkPosition)
        {
			return GetLoadedChunk(chunkPosition) != null;
        }

        public TerrainChunk GetLoadedChunk(Vector2i chunkPosition)
        {
            if (LoadedChunks.ContainsKey(chunkPosition))
                return LoadedChunks[chunkPosition];

            return null;
        }

		public List<TerrainChunk> GetLoadedChunks()
        {
			return LoadedChunks.Values.ToList ();
        }

		public List<TerrainChunk> GetAllChunks()
		{
			List<TerrainChunk> lst = new List<TerrainChunk>();
			lst.AddRange (LoadedChunks.Values.ToList ());
			lst.AddRange (RequestedChunks.Values.ToList ());
			lst.AddRange (GeneratedChunks.Values.ToList ());
			lst.AddRange (ChunksBeingGenerated.Values.ToList ());
			return lst;
		}

		private void GenerateHeightmapForAvailableChunks(Vector2i chunkPosition)
        {
            var requestedChunks = RequestedChunks.ToList();

			// move directly the ready heightmap to ChunksGenerated
			foreach (var chunkEntry in requestedChunks) {
				if (chunkEntry.Value.IsHeightmapReady ()) {
					RequestedChunks.Remove(chunkEntry.Key);
					//ChunksGenerated.Add(chunkEntry.Key, chunkEntry.Value);
				}
			}
			// start creating heightmap for the requested not ready
			requestedChunks = RequestedChunks.ToList();
			requestedChunks.Sort ((a, b) => (chunkPosition.DistanceTo(a.Key).CompareTo(chunkPosition.DistanceTo(b.Key))));
            if (requestedChunks.Count > 0 && ChunksBeingGenerated.Count < MaxChunkThreads)
            {
                var chunksToAdd = requestedChunks.Take(MaxChunkThreads - ChunksBeingGenerated.Count);
                foreach (var chunkEntry in chunksToAdd)
                {
                    ChunksBeingGenerated.Add(chunkEntry.Key, chunkEntry.Value);
                    RequestedChunks.Remove(chunkEntry.Key);

					//StartGenerateHeightmap is launched in an other thread
					chunkEntry.Value.StartGenerateHeightmap ();
                }
            }

			{
				var chunks = ChunksBeingGenerated.ToList ();
				foreach (var chunk in chunks) {
					if (chunk.Value.IsHeightmapReady ()) {
						ChunksBeingGenerated.Remove (chunk.Key);
						// is added by QT
						// ChunksGenerated.Add (chunk.Key, chunk.Value);
					}
				}
			}

        }

        private void CreateTerrainForReadyChunks()
        {
            var anyTerrainCreated = false;

			// Remove chunk that we don't need anymore in display
			{
				var chunks = LoadedChunks.ToList ();
				foreach (var chunk in chunks) {
					if (!GeneratedChunks.ContainsKey (chunk.Key)) {
						chunk.Value.Remove ();
						LoadedChunks.Remove (chunk.Key);
					}
				}
			}

			// Create terrain if not already created
			{
				var chunks = GeneratedChunks.ToList ();
				foreach (var chunk in chunks) {
					if (!LoadedChunks.ContainsKey (chunk.Key)) {
						chunk.Value.CreateTerrain ();
						LoadedChunks.Add (chunk.Key, chunk.Value);
						anyTerrainCreated = true;
						if (OnChunkGenerated != null)
							OnChunkGenerated.Invoke (GeneratedChunks.Count);
						SetChunkNeighborhood (chunk.Value);
					}
				}
			}
            if (anyTerrainCreated)
                UpdateAllChunkNeighbors();
        }

        private void TryToDeleteQueuedChunks()
        {
			var chunksToRemove = ChunksToRemove.ToList();
            foreach (var chunkPosition in chunksToRemove)
            {
                if (RequestedChunks.ContainsKey(chunkPosition))
                {
                    RequestedChunks.Remove(chunkPosition);
                    ChunksToRemove.Remove(chunkPosition);
				}
                else if (!ChunksBeingGenerated.ContainsKey(chunkPosition))
                    ChunksToRemove.Remove(chunkPosition);
            }
        }

        private void SetChunkNeighborhood(TerrainChunk chunk)
        {
            TerrainChunk xUp=null;
			TerrainChunk xDown=null;
			TerrainChunk zUp=null;
			TerrainChunk zDown=null;

			//only connect same Resolution chunck
			//try no neighbors
			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X + chunk.Position.Res, chunk.Position.Z, chunk.Position.Res), out xUp);
			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X - chunk.Position.Res, chunk.Position.Z, chunk.Position.Res), out xDown);
			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X, chunk.Position.Z + chunk.Position.Res, chunk.Position.Res), out zUp);
			LoadedChunks.TryGetValue(new Vector2i(chunk.Position.X, chunk.Position.Z - chunk.Position.Res, chunk.Position.Res), out zDown);

			//Debug.Log ("SetChunkNeighborhood" + chunk.Position + ": xd=" + xDown + " xu=" + xUp + " zd=" + zDown + " zu=" + zUp);


			if (xUp != null) {
				chunk.SetNeighbors (xUp, TerrainNeighbor.XUp);
				xUp.SetNeighbors (chunk, TerrainNeighbor.XDown);
			}
            if (xDown != null)
            {
                chunk.SetNeighbors(xDown, TerrainNeighbor.XDown);
                xDown.SetNeighbors(chunk, TerrainNeighbor.XUp);
            }
            if (zUp != null)
            {
                chunk.SetNeighbors(zUp, TerrainNeighbor.ZUp);
                zUp.SetNeighbors(chunk, TerrainNeighbor.ZDown);
            }
            if (zDown != null)
            {
                chunk.SetNeighbors(zDown, TerrainNeighbor.ZDown);
                zDown.SetNeighbors(chunk, TerrainNeighbor.ZUp);
            }
        }

        private void UpdateAllChunkNeighbors()
        {
            foreach (var chunkEntry in LoadedChunks)
                chunkEntry.Value.UpdateNeighbors();
        }
    }
}