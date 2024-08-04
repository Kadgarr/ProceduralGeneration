using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TController: MonoBehaviour
{
    public float perlinOffsetX = 0;
    public float perlinOffsetZ = 0;
    public float perlinXScale = 0.001f;
    public float perlinZScale = 0.001f;
    public float perlinHeightScale = 0.5f;
    public float perlinPersistance = 8;
    public int perlinOctaves = 3;

    public void OnValidate()
    {
        float perlinZeroOffset = (Terrain.activeTerrains.Length + 1) * Terrain.activeTerrains[0].terrainData.size.x;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            TerrainData terrainData = terrain.terrainData;
            int HMR = terrainData.heightmapResolution;
            float[,] heightMap = new float[HMR, HMR];
            for (int z = 0; z < HMR; ++z)
            {


                for (int x = 0; x < HMR; ++x)
                {
                    float worldPositionX = x / (float)HMR * terrainData.size.x + terrain.transform.position.x;
                    float worldPositionZ = z / (float)HMR * terrainData.size.z + terrain.transform.position.z;
                    heightMap[z, x] += Utils.fBM((worldPositionX + perlinOffsetX+ perlinZeroOffset) * perlinXScale,
                                               (worldPositionZ + perlinOffsetZ+ perlinZeroOffset) * perlinZScale,
                                               perlinOctaves,
                                               perlinPersistance) * perlinHeightScale;
                }

                   
            }
            terrain.terrainData.SetHeights(0, 0, heightMap);
        }

        foreach (Terrain terrain in Terrain.activeTerrains)
        {

            TerrainData terrainData = terrain.terrainData;
            int HMR = terrain.terrainData.heightmapResolution;
            float[,] thisHeightMap = terrainData.GetHeights(0, 0, HMR, HMR);

            if (terrain.topNeighbor != null)
            {

                float[,] topNeighbourHeightMap = terrain.topNeighbor.terrainData.GetHeights(0, 0, HMR, HMR);

                for (int z = 0; z < HMR; ++z)
                {

                    topNeighbourHeightMap[0, z] = thisHeightMap[HMR - 1, z];
                }
                terrain.topNeighbor.terrainData.SetHeights(0, 0, topNeighbourHeightMap);
            }

            if (terrain.rightNeighbor != null)
            {

                float[,] rightNeighbourHeightMap = terrain.rightNeighbor.terrainData.GetHeights(0, 0, HMR, HMR);

                for (int x = 0; x < HMR; ++x)
                {

                    rightNeighbourHeightMap[x, 0] = thisHeightMap[x, HMR - 1];
                }
                terrain.rightNeighbor.terrainData.SetHeights(0, 0, rightNeighbourHeightMap);
            }
        }
    }
}
