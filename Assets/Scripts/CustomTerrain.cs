using System;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.Sqlite;

[ExecuteInEditMode]
public class CustomTerrain : MonoBehaviour
{

    int hmr { get { return terrainData.heightmapResolution; } } //heightmap resolution
    bool isRidged = false;

    public Vector2 randomHeightRange = new Vector2(0.0f, 0.1f);
    public Texture2D heightMapImage;
    public Vector3 heightMapScale = new Vector3(1.0f, 1.0f, 1.0f);
    public bool resetTerrain = true;

    // Splatmaps **********************************************
    [System.Serializable]
    public class SplatHeights
    {

        public Texture2D texture = null;
        public Texture2D textureNormalMap = null;
        public float minHeight = 0.1f;
        public float maxHeight = 0.2f;
        public float minSlope = 0.0f;
        public float maxSlope = 90.0f;
        public float splatOffset = 000000.10f;
        public float splatNoiseXScale = 0.01f;
        public float splatNoiseYScale = 0.01f;
        public float splatNoiseZScale = 0.10f;
        public Vector2 tileOffset = Vector2.zero;
        public Vector2 tileSize = new Vector2(50.0f, 50.0f);
        public bool remove = false;
    }
    public List<SplatHeights> splatHeights = new List<SplatHeights>() {

        new SplatHeights()
    };

    // Perlin Noise ***********************************************
    public float perlinXScale = 0.01f;
    public float perlinYScale = 0.01f;
    public int perlinOffsetX = 0;
    public int perlinOffsetY = 0;
    public int perlinOctaves = 3;
    public float perlinPersistance = 8.0f;
    public float perlinHeightScale = 0.009f;

    // Multiple Perlin Noise ***********************************************
    // Multiple Perlin Noise *************************************
    [System.Serializable]
    public class PerlinParameters
    {

        public float mPerlinXScale = 0.01f;
        public float mPerlinYScale = 0.01f;
        public int mPerlinOctaves = 3;
        public float mPerlinPersistance = 8;
        public float mPerlinHeightScale = 0.09f;
        public int mPerlinOffsetX = 0;
        public int mPerlinOffsetY = 0;
        public bool remove = false;
    }

    public List<PerlinParameters> perlinParameters = new List<PerlinParameters>() {

        new PerlinParameters()
    };

    // Voronoi ************************************************
    public float voronoiFallOff = 0.2f;
    public float voronoiDropOff = 0.6f;
    public float voronoiMinHeight = 0.1f;
    public float voronoiMaxHeight = 0.5f;
    public int voronoiPeaks = 5;
    public VoronoiType voronoiType = VoronoiType.Linear;
    public enum VoronoiType
    {

        Linear,
        Power,
        SinPow,
        Combined,
        Perlin
    }

    // Midpoint Displacement *********************************
    public float MPDHeightMin = -2.0f;
    public float MPDHeightMax = 2.0f;
    public float MPDHeightDampner = 2.0f;
    public float MPDRoughness = 2.0f;

    public int smoothAmount = 1;

    public Terrain terrain;
    public TerrainData terrainData;

    public void SplatMaps()
    {

        int tah = terrainData.alphamapHeight;
        int taw = terrainData.alphamapWidth;
        int aml = terrainData.alphamapLayers;
        TerrainLayer[] newSplatPrototypes;
        newSplatPrototypes = new TerrainLayer[splatHeights.Count];
        int spIndex = 0;

        foreach (SplatHeights sh in splatHeights)
        {

            newSplatPrototypes[spIndex] = new TerrainLayer
            {
                diffuseTexture = sh.texture,
                normalMapTexture = sh.textureNormalMap,
                tileOffset = sh.tileOffset,
                tileSize = sh.tileSize
            };

            newSplatPrototypes[spIndex].diffuseTexture.Apply(true);
            string path = "Assets/New Terrain Layer " + spIndex + ".terrainlayer";
            AssetDatabase.CreateAsset(newSplatPrototypes[spIndex], path);
            spIndex++;
            Selection.activeObject = this.gameObject;
        }
        terrainData.terrainLayers = newSplatPrototypes;

        //USE HEIGHTMAP RESOLUTION HERE
        float[,] heightMap = terrainData.GetHeights(0, 0, hmr, hmr);

        //USE SPLATMAP RESOLUTUON HERE
        float[,,] splatmapData = new float[taw, tah, aml];

        for (int y = 0; y < tah; ++y)
        {

            for (int x = 0; x < taw; ++x)
            {

                float[] splat = new float[aml];

                for (int i = 0; i < splatHeights.Count; ++i)
                {
                    float noise = Mathf.PerlinNoise(x * splatHeights[i].splatNoiseXScale,
                                y * splatHeights[i].splatNoiseYScale) *
                                splatHeights[i].splatNoiseZScale;

                    float offset = splatHeights[i].splatOffset + noise;
                    float thisHeightStart = splatHeights[i].minHeight - offset;
                    float thisHeightStop = splatHeights[i].maxHeight + offset;
                    //SCALE FOR RESOLUTION DIFFERENCES
                    //NOTE: The switching of the x and y is no longer needed here
                    //Scale between the heightmap resolution and the splatmap resolution
                    int hmX = x * ((hmr - 1) / taw);
                    int hmY = y * ((hmr - 1) / tah);


                    float normX = x * 1.0f / (terrainData.alphamapWidth - 1);
                    float normY = y * 1.0f / (terrainData.alphamapHeight - 1);

                    // Get the steepness value at the normalised coordinate
                    float steepness = terrainData.GetSteepness(normX, normY);

                    if ((heightMap[hmX, hmY] >= thisHeightStart && heightMap[hmX, hmY] <= thisHeightStop) &&
                          (steepness >= splatHeights[i].minSlope && steepness <= splatHeights[i].maxSlope))
                    {

                        if (heightMap[hmX, hmY] <= splatHeights[i].minHeight)
                            splat[i] = 1.0f - Mathf.Abs(heightMap[hmX, hmY] - splatHeights[i].minHeight) / offset;
                        else if (heightMap[hmX, hmY] >= splatHeights[i].maxHeight)
                            splat[i] = 1.0f - Mathf.Abs(heightMap[hmX, hmY] - splatHeights[i].maxHeight) / offset;
                        else
                            splat[i] = 1;
                    }
                }

                NormalizeVector(ref splat);
                for (int j = 0; j < splatHeights.Count; j++)
                {
                    splatmapData[x, y, j] = splat[j];
                }
            }
        }
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    public static void NormalizeVector(ref float[] v)
    {

        float total = 0.0f;
        for (int i = 0; i < v.Length; ++i)
        {

            total += v[i];
        }

        for (int i = 0; i < v.Length; ++i)
        {

            v[i] /= total;
        }
    }

    public void AddNewSplatHeight()
    {

        splatHeights.Add(new SplatHeights());
    }

    public void RemoveSplatHeights()
    {

        List<SplatHeights> keptSplatHeights = new List<SplatHeights>();
        for (int i = 0; i < splatHeights.Count; i++)
        {
            if (!splatHeights[i].remove)
            {
                keptSplatHeights.Add(splatHeights[i]);
            }
        }
        if (keptSplatHeights.Count == 0) //don't want to keep any
        {
            keptSplatHeights.Add(splatHeights[0]); //add at least 1
        }
        splatHeights = keptSplatHeights;
    }

    public void Perlin()
    {

        float[,] heightMap = GetHeightMap();
        for (int y = 0; y < hmr; ++y)
        {


            for (int x = 0; x < hmr; ++x)

                heightMap[x, y] += Utils.fBM((x + perlinOffsetX) * perlinXScale,
                                            (y + perlinOffsetY) * perlinYScale,
                                            perlinOctaves,
                                            perlinPersistance) * perlinHeightScale;
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void MultiplePerlinTerrain()
    {

        float[,] heightMap = GetHeightMap();
        for (int y = 0; y < hmr; ++y)
        {

            for (int x = 0; x < hmr; ++x)
            {

                foreach (PerlinParameters p in perlinParameters)
                {

                    heightMap[x, y] += Utils.fBM((x + p.mPerlinOffsetX) * p.mPerlinXScale,
                                                (y + p.mPerlinOffsetY) * p.mPerlinYScale,
                                                p.mPerlinOctaves,
                                                p.mPerlinPersistance) * p.mPerlinHeightScale;
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }
    public void AddNewPerlin()
    {

        perlinParameters.Add(new PerlinParameters());
    }

    public void RemovePerlin()
    {

        for (int i = perlinParameters.Count - 1; i >= 1; --i)
            if (perlinParameters[i].remove) perlinParameters.RemoveAt(i);
    }

    public void RandomTerrain()
    {

        //int hmr = terrainData.heightmapResolution;
        float[,] heightMap = GetHeightMap();

        for (int x = 0; x < hmr; ++x)
        {
            for (int z = 0; z < hmr; ++z)
            {

                heightMap[x, z] += UnityEngine.Random.Range(randomHeightRange.x, randomHeightRange.y);
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void LoadTexture()
    {

        //float[,] heightMap = new float[hmr, hmr]; //terrainData.GetHeights(0, 0, hmr, hmr);
        float[,] heightMap = GetHeightMap();

        for (int x = 0; x < hmr; ++x)
        {
            for (int z = 0; z < hmr; ++z)
            {

                heightMap[x, z] += heightMapImage.GetPixel((int)(x * heightMapScale.x),
                    (int)(z * heightMapScale.z)).grayscale * heightMapScale.y;
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void RidgeNoise()
    {

        //ResetTerrain();
        MultiplePerlinTerrain();
        float[,] heightMap = GetHeightMap();

        for (int y = 0; y < hmr; ++y)
        {

            for (int x = 0; x < hmr; ++x)
            {

                heightMap[x, y] = 1 - Mathf.Abs(heightMap[x, y] - 0.5f);
            }
        }
        isRidged = !isRidged;
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void Voronoi()
    {

        float[,] heightMap = GetHeightMap();

        for (int p = 0; p < voronoiPeaks; ++p)
        {

            Vector3 peak = new Vector3(UnityEngine.Random.Range(0, hmr),
                                       UnityEngine.Random.Range(voronoiMinHeight, voronoiMaxHeight),
                                       UnityEngine.Random.Range(0, hmr));

            if (heightMap[(int)peak.x, (int)peak.z] < peak.y)
            {

                heightMap[(int)peak.x, (int)peak.z] = peak.y;
            }
            else
            {

                continue;
            }

            Vector2 peakLocation = new Vector2(peak.x, peak.z);
            float maxDistance = Vector2.Distance(new Vector2(0.0f, 0.0f), new Vector2(hmr, hmr));

            for (int y = 0; y < hmr; ++y)
            {

                for (int x = 0; x < hmr; ++x)
                {

                    if (!(x == peak.x && y == peak.z))
                    {

                        float distanceToPeak = Vector2.Distance(peakLocation, new Vector2(x, y)) / maxDistance;
                        float h;

                        if (voronoiType == VoronoiType.Combined)
                        {

                            h = peak.y - distanceToPeak * voronoiFallOff -
                                MathF.Pow(distanceToPeak, voronoiDropOff); // Combined
                        }
                        else if (voronoiType == VoronoiType.Power)
                        {

                            h = peak.y - Mathf.Pow(distanceToPeak, voronoiDropOff) * voronoiFallOff;    // Power
                        }
                        else if (voronoiType == VoronoiType.SinPow)
                        {

                            h = peak.y - Mathf.Pow(distanceToPeak * 3.0f, voronoiFallOff) -
                                Mathf.Sin(distanceToPeak * 2.0f * Mathf.PI) / voronoiDropOff;   // SinPow
                        }
                        else if (voronoiType == VoronoiType.Perlin)
                        {

                            h = (peak.y - distanceToPeak * voronoiFallOff) + Utils.fBM((x + perlinOffsetX) * perlinXScale,
                                            (y + perlinOffsetY) * perlinYScale,
                                            perlinOctaves,
                                            perlinPersistance) * perlinHeightScale;    // Perlin
                        }
                        else
                        {

                            h = (peak.y - distanceToPeak * voronoiFallOff);   // Linear
                        }

                        if (heightMap[x, y] < h)
                        {

                            heightMap[x, y] = h;
                        }
                    }
                }
            }
        }

        terrainData.SetHeights(0, 0, heightMap);
    }

    public void MidPointDisplacement()
    {

        float[,] heightMap = GetHeightMap();
        int width = hmr - 1;
        int squareSize = width;
        float heightMin = MPDHeightMin;
        float heightMax = MPDHeightMax;
        float heightDampener = (float)Mathf.Pow(MPDHeightDampner, -1 * MPDRoughness);

        int cornerX, cornerY;
        int midX, midY;
        int pmidXL, pmidXR, pmidYU, pmidYD;

        //heightMap[0, 0] = UnityEngine.Random.Range(0.0f, 0.2f);
        //heightMap[0, hmr - 2] = UnityEngine.Random.Range(0.0f, 0.2f);
        //heightMap[hmr - 2, 0] = UnityEngine.Random.Range(0.0f, 0.2f);
        //heightMap[hmr - 2, hmr - 2] = UnityEngine.Random.Range(0.0f, 0.2f);

        while (squareSize > 0)
        {

            for (int x = 0; x < width; x += squareSize)
            {

                for (int y = 0; y < width; y += squareSize)
                {

                    cornerX = (x + squareSize);
                    cornerY = (y + squareSize);

                    midX = (int)(x + squareSize / 2.0f);
                    midY = (int)(y + squareSize / 2.0f);

                    heightMap[midX, midY] = (float)((heightMap[x, y] +
                                                     heightMap[cornerX, y] +
                                                     heightMap[x, cornerY] +
                                                     heightMap[cornerX, cornerY]) / 4.0f +
                                                     UnityEngine.Random.Range(heightMin, heightMax));
                }
            }

            for (int x = 0; x < width; x += squareSize)
            {

                for (int y = 0; y < width; y += squareSize)
                {

                    cornerX = (x + squareSize);
                    cornerY = (y + squareSize);

                    midX = (int)(x + squareSize / 2.0f);
                    midY = (int)(y + squareSize / 2.0f);

                    pmidXR = (int)(midX + squareSize);
                    pmidYU = (int)(midY + squareSize);
                    pmidXL = (int)(midX - squareSize);
                    pmidYD = (int)(midY - squareSize);

                    if (pmidXL <= 0 || pmidYD <= 0
                        || pmidXR >= width - 1 || pmidYU >= width - 1) continue;

                    // Calculate the square value for the bottom right
                    heightMap[midX, y] = (float)((heightMap[midX, midY] +
                                                  heightMap[x, y] +
                                                  heightMap[midX, pmidYD] +
                                                  heightMap[cornerX, y]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));

                    // Calculate the square value for the top side
                    heightMap[midX, cornerY] = (float)((heightMap[x, cornerY] +
                                                  heightMap[midX, midY] +
                                                  heightMap[cornerX, cornerY] +
                                                  heightMap[midX, pmidYU]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));

                    // Calculate the square value for the left side
                    heightMap[x, midY] = (float)((heightMap[x, y] +
                                                  heightMap[pmidXL, midY] +
                                                  heightMap[x, cornerY] +
                                                  heightMap[midX, midY]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));

                    // Calculate the square value for the right side
                    heightMap[cornerX, midY] = (float)((heightMap[midX, y] +
                                                  heightMap[midX, midY] +
                                                  heightMap[cornerX, cornerY] +
                                                  heightMap[pmidXR, midY]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));
                }
            }

            squareSize = (int)(squareSize / 2.0f);
            heightMin *= heightDampener;
            heightMax *= heightDampener;
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    List<Vector2> GenerateNeighbours(Vector2 pos, int width, int height)
    {

        List<Vector2> neighbours = new List<Vector2>();

        for (int y = -1; y < 2; ++y)
        {

            for (int x = -1; x < 2; ++x)
            {

                if (!(x == 0 && y == 0))
                {

                    Vector2 nPos = new Vector2(
                        Mathf.Clamp(pos.x + x, 0.0f, width - 1),
                        Mathf.Clamp(pos.y + y, 0.0f, height - 1));

                    if (!neighbours.Contains(nPos))
                        neighbours.Add(nPos);
                }
            }
        }
        return neighbours;
    }

    public void Smooth()
    {

        float[,] heightMap = terrainData.GetHeights(0, 0, hmr, hmr);
        float smoothProgress = 0.0f;
        EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress);

        for (int s = 0; s < smoothAmount; ++s)
        {

            for (int y = 0; y < hmr; ++y)
            {

                for (int x = 0; x < hmr; ++x)
                {

                    float avgHeight = heightMap[x, y];
                    List<Vector2> neighbours = GenerateNeighbours(new Vector2(x, y), hmr, hmr);

                    foreach (Vector2 n in neighbours)
                    {

                        avgHeight += heightMap[(int)n.x, (int)n.y];
                    }
                    heightMap[x, y] = avgHeight / ((float)neighbours.Count + 1);
                }
            }
            smoothProgress++;
            EditorUtility.DisplayProgressBar("Smoothin Terrain", "Progress", smoothProgress / smoothAmount);
        }
        terrainData.SetHeights(0, 0, heightMap);
        EditorUtility.ClearProgressBar();
    }


    public float[,] GetHeightMap()
    {

        if (!resetTerrain)
        {

            return terrainData.GetHeights(0, 0, hmr, hmr);
        }
        else
        {

            return new float[hmr, hmr];
        }
    }

    public void ResetTerrain()
    {

        float[,] heightMap = new float[hmr, hmr];
        terrainData.SetHeights(0, 0, heightMap);
    }

    private void OnEnable()
    {

        Debug.Log("Initialising Tertain Data");
        //Debug.Log(terrainData.size);
        //terrainData.size;
        terrain = this.GetComponent<Terrain>();
        terrainData = Terrain.activeTerrain.terrainData;
    }

    public enum TagType { Tag, Layer };
    [SerializeField]
    int terrainLayer = 0;

    private void Start()
    {

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        AddTag(tagsProp, "Terrain", TagType.Tag);
        AddTag(tagsProp, "Cloud", TagType.Tag);
        AddTag(tagsProp, "Shore", TagType.Tag);

        // Apply tag changes to tag database
        tagManager.ApplyModifiedProperties();

        SerializedProperty layerProp = tagManager.FindProperty("layers");
        terrainLayer = AddTag(layerProp, "Terrain", TagType.Layer);
        tagManager.ApplyModifiedProperties();

        // Tag this object
        this.gameObject.tag = "Terrain";
        this.gameObject.layer = terrainLayer;
    }

    int AddTag(SerializedProperty tagsProp, string newTag, TagType tType)
    {

        bool found = false;

        // Ensure the tag doesn't already exist
        for (int i = 0; i < tagsProp.arraySize; ++i)
        {

            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals(newTag))
            {

                found = true;
                return i;
            }
        }

        // Add your new tag
        if (!found && tType == TagType.Tag)
        {

            tagsProp.InsertArrayElementAtIndex(0);
            SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(0);
            newTagProp.stringValue = newTag;
        }

        // Add new layer
        else if (!found && tType == TagType.Layer)
        {

            for (int j = 8; j < tagsProp.arraySize; ++j)
            {

                SerializedProperty newLayer = tagsProp.GetArrayElementAtIndex(j);
                // Add layer in next slot
                if (newLayer.stringValue == "")
                {

                    Debug.Log("Adding New Layer: " + newTag);
                    newLayer.stringValue = newTag;
                    return j;
                }
            }
        }
        return -1;
    }
}
