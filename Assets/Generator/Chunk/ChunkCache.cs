using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TerrainGenerator
{
    public class ChunkCache
    {
		private readonly int MaxChunkThreads = SystemInfo.processorCount - 1;

        private Dictionary<Vector2i, TerrainChunk> RequestedChunks { get; set; }

        private Dictionary<Vector2i, TerrainChunk> ChunksBeingGenerated { get; set; }

        private Dictionary<Vector2i, TerrainChunk> LoadedChunks { get; set; }

        private HashSet<Vector2i> ChunksToRemove { get; set; }

        public OnChunkGeneratedDelegate OnChunkGenerated { get; set; }

        public ChunkCache()
        {
            RequestedChunks = new Dictionary<Vector2i, TerrainChunk>();
            ChunksBeingGenerated = new Dictionary<Vector2i, TerrainChunk>();
            LoadedChunks = new Dictionary<Vector2i, TerrainChunk>();
            ChunksToRemove = new HashSet<Vector2i>();
        }

        public void Update()
        {
            TryToDeleteQueuedChunks();

            GenerateHeightmapForAvailableChunks();
            CreateTerrainForReadyChunks();
        }

        public void AddNewChunk(TerrainChunk chunk)
        {
            RequestedChunks.Add(chunk.Position, chunk);
            GenerateHeightmapForAvailableChunks();
        }

		public void RemoveChunk(Vector2i key)
        {
			ChunksToRemove.Add(key);
            TryToDeleteQueuedChunks();
        }

		public bool ChunkCanBeAdded(Vector2i key)
        {
            return
                !(RequestedChunks.ContainsKey(key)
                || ChunksBeingGenerated.ContainsKey(key)
                || LoadedChunks.ContainsKey(key));
        }

		public bool ChunkCanBeRemoved(Vector2i key)
        {
            return
                RequestedChunks.ContainsKey(key)
                || ChunksBeingGenerated.ContainsKey(key)
                || LoadedChunks.ContainsKey(key);
        }

        public bool IsChunkGenerated(Vector2i chunkPosition)
        {
            return GetGeneratedChunk(chunkPosition) != null;
        }

        public TerrainChunk GetGeneratedChunk(Vector2i chunkPosition)
        {
            if (LoadedChunks.ContainsKey(chunkPosition))
                return LoadedChunks[chunkPosition];

            return null;
        }

		public List<TerrainChunk> GetGeneratedChunks()
        {
			return LoadedChunks.Values.ToList();
        }

        private void GenerateHeightmapForAvailableChunks()
        {
            var requestedChunks = RequestedChunks.ToList();
            if (requestedChunks.Count > 0 && ChunksBeingGenerated.Count < MaxChunkThreads)
            {
                var chunksToAdd = requestedChunks.Take(MaxChunkThreads - ChunksBeingGenerated.Count);
                foreach (var chunkEntry in chunksToAdd)
                {
                    ChunksBeingGenerated.Add(chunkEntry.Key, chunkEntry.Value);
                    RequestedChunks.Remove(chunkEntry.Key);

					var thread = new Thread(chunkEntry.Value.GenerateHeightmap);
					thread.Start();
                }
            }
        }

        private void CreateTerrainForReadyChunks()
        {
            var anyTerrainCreated = false;

            var chunks = ChunksBeingGenerated.ToList();
            foreach (var chunk in chunks)
            {
                if (chunk.Value.IsHeightmapReady())
                {
                    ChunksBeingGenerated.Remove(chunk.Key);
                    LoadedChunks.Add(chunk.Key, chunk.Value);

                    chunk.Value.CreateTerrain();

                    anyTerrainCreated = true;
                    if (OnChunkGenerated != null)
                        OnChunkGenerated.Invoke(ChunksBeingGenerated.Count);

                    SetChunkNeighborhood(chunk.Value);
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
                else if (LoadedChunks.ContainsKey(chunkPosition))
                {
                    var chunk = LoadedChunks[chunkPosition];
                    chunk.Remove();

                    LoadedChunks.Remove(chunkPosition);
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