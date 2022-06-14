using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// This editor quickly covers up a blockout with defined walls and corners.
/// This editor expects all elements that should be dressed up to be blocks and to have the "Level" tag.
/// </summary>
public class SceneDresser : EditorWindow
{
    // A list of all objects in the scene making up the blockout that has to be dressed
    private List<GameObject> _toDress = null;

    // Different prefabs representing a wall
    private List<GameObject> _wallVisuals = null;
    private ReorderableList _wallList = null;

    // Different prefabs of scribbles for the corners
    private List<GameObject> _scribbles = null;
    private ReorderableList _scribbleList = null;

    // size of Wall prefabs
    private Vector2 _wallDimensions = new Vector2(2, 2);

    // Size limits for scribbles covering corners
    private Vector2 _scribbleLengthMinMax = new Vector2(5, 5);
    private Vector2 _scribbleThicknessMinMax = new Vector2(1.4f, 2.5f);
    private Vector2 _scribbleOffsetMinMax = new Vector2(0, 2f);

    // Length of edge Scribble prefabs
    private float _scribbleLength = 2f;
    private float _wallOffset = 0.2f;


    [MenuItem("Window/ScribblesOM/SceneDresser")]
    public static void ShowWindow()
    {
        GetWindow<SceneDresser>();
    }

    private void OnEnable()
    {
        // prepare wall prefab list to make drawing them later easier
        _wallVisuals = new List<GameObject>();
        _wallList = new ReorderableList(_wallVisuals, typeof(GameObject), true, false, true, true);
        _wallList.drawElementCallback =
            (Rect rect, int index, bool isActive, bool isFocused) => {

                rect.y += 2;
                _wallVisuals[index] = EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), _wallVisuals[index], typeof(GameObject), false) as GameObject;
            };
        _wallList.onAddCallback = (ReorderableList list) => { _wallVisuals.Add(null); };


        // prepare edge scribble list to make drawing them later easier
        _scribbles = new List<GameObject>();
        _scribbleList = new ReorderableList(_scribbles, typeof(GameObject), true, false, true, true);
        _scribbleList.drawElementCallback =
            (Rect rect, int index, bool isActive, bool isFocused) => {

                rect.y += 2;
                _scribbles[index] = EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), _scribbles[index], typeof(GameObject), false) as GameObject;
            };
        _scribbleList.onAddCallback = (ReorderableList list) => { _scribbles.Add(null); };
    }

    private void OnGUI()
    {
        ShowSettings();
        HandleDressup();
        
        if (GUILayout.Button("Clear Scene"))
            ClearAll();
    }

    /// <summary>
    /// This method draws all settings that can be adjusted in the editor window
    /// </summary>
    private void ShowSettings()
    {
        EditorGUILayout.LabelField("Wall prefabs: ");
        _wallList.DoLayoutList();
        EditorGUILayout.LabelField("Edge Scribble prefabs: ");
        _scribbleList.DoLayoutList();
        _wallDimensions = EditorGUILayout.Vector2Field("Wall size: ", _wallDimensions);
        _scribbleLengthMinMax = EditorGUILayout.Vector2Field("Scribble length Range(Min Max): ", _scribbleLengthMinMax);
        _scribbleThicknessMinMax = EditorGUILayout.Vector2Field("Scribble thickness Range(Min Max): ", _scribbleThicknessMinMax);
        _scribbleOffsetMinMax = EditorGUILayout.Vector2Field("Offset between Scribbles(Min Max): ", _scribbleOffsetMinMax);
        _scribbleLength = EditorGUILayout.FloatField("Length of edge Scribble prefabs:", _scribbleLength);
        _wallOffset = EditorGUILayout.FloatField("Offset to wall:", _wallOffset);
    }

    /// <summary>
    /// This method handles dressing up the scene.
    /// This needs to be called in a OnGUI since this method also shows the button starting the dress-up
    /// </summary>
    private void HandleDressup()
    {
        if (!GUILayout.Button("Dress"))
            return;

        // We get all elements we need to dress up
        _toDress = new List<GameObject>(GameObject.FindGameObjectsWithTag("Level"));

        // We mark all active scenes dirty to allow for saving after the adjustments
        EditorSceneManager.MarkAllScenesDirty();
        ClearAll();

        // We go through all objects we want to dress up and dress them one by one
        foreach (GameObject toDress in _toDress)
        {
            MeshFilter[] meshFilter = toDress.GetComponentsInChildren<MeshFilter>();

            // We get all vertices in the object we are dressing up
            List<Vector3> vertices = new List<Vector3>();

            foreach (MeshFilter filter in meshFilter)
            {
                Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    vertices.Add(localToWorld.MultiplyPoint3x4(vertex));
                }
            }

            // We disable all renderers on the object we are dressing up, so nothing can peek out of a deformed wall
            MeshRenderer[] meshRenderer = toDress.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in meshRenderer)
            {
                renderer.enabled = false;
            }

            // We find highest and lowest x, y and z coordinates of all vertices to find our wall positions
            Vector3 bottomLeftFront = vertices[0];
            Vector3 topRightBack = vertices[0];

            foreach (Vector3 vertex in vertices)
            {
                bottomLeftFront.x = Mathf.Min(bottomLeftFront.x, vertex.x);
                bottomLeftFront.y = Mathf.Min(bottomLeftFront.y, vertex.y);
                bottomLeftFront.z = Mathf.Min(bottomLeftFront.z, vertex.z);

                topRightBack.x = Mathf.Max(topRightBack.x, vertex.x);
                topRightBack.y = Mathf.Max(topRightBack.y, vertex.y);
                topRightBack.z = Mathf.Max(topRightBack.z, vertex.z);
            }

            // we adjust our positions based on the offset
            bottomLeftFront.x -= _wallOffset;
            bottomLeftFront.y -= _wallOffset;
            bottomLeftFront.z -= _wallOffset;

            topRightBack.x += _wallOffset;
            topRightBack.y += _wallOffset;
            topRightBack.z += _wallOffset;

            // We create a gameobject to keep track of all created objects
            // This way we keep order in the scene and can easily remove all visuals if needed
            GameObject folder = new GameObject("Visuals");
            folder.transform.parent = toDress.transform;
            folder.transform.localPosition = Vector3.zero;

            SpawnWalls(bottomLeftFront, topRightBack, folder.transform);

            SpawnEdges(bottomLeftFront, topRightBack, folder.transform);


        }
        
    }

    /// <summary>
    /// This method creates walls around a cube, based on 2 points defining the borders of said cube.
    /// The method only creates 3 walls. The top wall, the front wall and the left wall, since these are the only walls facing the camera
    /// </summary>
    /// <param name="bottomLeftFront">A vector defining the point the furthes at the bottom, left and front of the cube</param>
    /// <param name="topRightBack">A vector defining the point the furthes at the top, right and back of the cube</param>
    /// <param name="parent">A transform acting as a parent for all created walls</param>
    private void SpawnWalls(Vector3 bottomLeftFront, Vector3 topRightBack, Transform parent)
    {
        Vector3 boxDimensions = new Vector3(topRightBack.x - bottomLeftFront.x, topRightBack.y - bottomLeftFront.y, topRightBack.z - bottomLeftFront.z);

        // We instatiate the front wall at the bottom, left, front corner
        GameObject wall = Instantiate(_wallVisuals[Random.Range(0, _wallVisuals.Count)], new Vector3(bottomLeftFront.x, bottomLeftFront.y, bottomLeftFront.z), Quaternion.Euler(0, -180, 0), parent);
        // We rescale the wall 
        wall.transform.localScale = new Vector3(boxDimensions.x / _wallDimensions.x, boxDimensions.y / _wallDimensions.y, 1);

        // We instatiate the left wall at the bottom, left, back corner
        wall = Instantiate(_wallVisuals[Random.Range(0, _wallVisuals.Count)], new Vector3(bottomLeftFront.x, bottomLeftFront.y, topRightBack.z), Quaternion.Euler(0, -90, 0), parent);
        // We rescale the wall 
        wall.transform.localScale = new Vector3(boxDimensions.z / _wallDimensions.x, boxDimensions.y / _wallDimensions.y, 1);

        // We instatiate the top wall at the top, left, front corner
        wall = Instantiate(_wallVisuals[Random.Range(0, _wallVisuals.Count)], new Vector3(bottomLeftFront.x, topRightBack.y, bottomLeftFront.z), Quaternion.Euler(-90, -180, 0), parent);
        // We rescale the wall 
        wall.transform.localScale = new Vector3(boxDimensions.x / _wallDimensions.x, boxDimensions.z / _wallDimensions.y, 1);
    }

    /// <summary>
    /// This method creates scribbles, covering the edges of a cube, based on 2 points defining the borders of said cube.
    /// The method only covers up 7 edges, the 7 facing the camera.
    /// </summary>
    /// <param name="bottomLeftFront">A vector defining the point the furthes at the bottom, left and front of the cube</param>
    /// <param name="topRightBack">A vector defining the point the furthes at the top, right and back of the cube</param>
    /// <param name="parent">A transform acting as a parent for all created edges</param>
    private void SpawnEdges(Vector3 bottomLeftFront, Vector3 topRightBack, Transform parent)
    {
        float sideLength = 0;

        // We calculate the side length of all edges aligning with the y-axis
        sideLength = topRightBack.y - bottomLeftFront.y;
        // We spawn all edges aligning y axis
        SpawnEdge(bottomLeftFront, Vector3.up, parent, sideLength);

        SpawnEdge(new Vector3(bottomLeftFront.x, bottomLeftFront.y, topRightBack.z), Vector3.up, parent, sideLength);

        SpawnEdge(new Vector3(topRightBack.x, bottomLeftFront.y, bottomLeftFront.z), Vector3.up, parent, sideLength);


        // We calculate the side length of all edges aligning with the x-axis
        sideLength = topRightBack.x - bottomLeftFront.x;
        // We spawn all edges aligning x axis
        SpawnEdge(new Vector3(bottomLeftFront.x, bottomLeftFront.y, bottomLeftFront.z), Vector3.right, parent, sideLength);

        SpawnEdge(new Vector3(bottomLeftFront.x, topRightBack.y, bottomLeftFront.z), Vector3.right, parent, sideLength);


        // We calculate the side length of all edges aligning with the z-axis
        sideLength = topRightBack.z - bottomLeftFront.z;
        // We spawn all edges aligning z axis
        SpawnEdge(new Vector3(bottomLeftFront.x, topRightBack.y, topRightBack.z), Vector3.back, parent, sideLength);

        SpawnEdge(new Vector3(bottomLeftFront.x, bottomLeftFront.y, topRightBack.z), Vector3.back, parent, sideLength);
    }

    /// <summary>
    /// This method covers up one given edge.
    /// </summary>
    /// <param name="edgePosition">The start position of the edge to cover</param>
    /// <param name="edgeDirection">The direction of the edge to cover</param>
    /// <param name="parent">A transform acting as a parent for all created edges</param>
    /// <param name="sideLength">The length of the edge to cover</param>
    private void SpawnEdge(Vector3 edgePosition, Vector3 edgeDirection, Transform parent, float sideLength)
    {
        // We keep track of how much length we still have to cover
        float leftSideLength = sideLength;

        do
        {
            // We randomize how much of the edge we want to cover with a single scribble
            // The randomization is based on the given limits for a scribble length as well as the left over length
            float wantedEdgeLength = Random.Range(Mathf.Min(_scribbleLengthMinMax.x, leftSideLength), Mathf.Min(_scribbleLengthMinMax.y, leftSideLength));

            // if the length we have left after this scribble is less than the minimal length of a scribble, we ensure that this is the last edge by shortening the left length
            if (leftSideLength - wantedEdgeLength < _scribbleLengthMinMax.x)
                leftSideLength = wantedEdgeLength - 0.1f;

            // We create a randomly chosen scribble, along the edge we want to cover
            // We place it based on how much we already have covered
            GameObject edge = Instantiate(_scribbles[Random.Range(0, _scribbles.Count)], edgePosition + edgeDirection * (sideLength - leftSideLength), Quaternion.identity, parent);

            // We align and scale the scribble
            edge.transform.right = edgeDirection;
            edge.transform.localScale = new Vector3(wantedEdgeLength / _scribbleLength, Random.Range(_scribbleThicknessMinMax.x, _scribbleThicknessMinMax.y), 1);

            // We keep track of the left over length and give a random offset between scribbles
            leftSideLength -= wantedEdgeLength + Random.Range(_scribbleOffsetMinMax.x, _scribbleOffsetMinMax.y);

        } while (leftSideLength > 0); // We repeat this until the entire edge is covered
    }

    /// <summary>
    /// This method removes everything this scene dresser creates.
    /// It can be used to adjust the scene easier or when just re-dressing the scene.
    /// </summary>
    private void ClearAll()
    {
        // We mark all active scenes dirty to allow for saving after the adjustments
        EditorSceneManager.MarkAllScenesDirty();

        // We find all elements that are meant to be dressed
        _toDress = new List<GameObject>(GameObject.FindGameObjectsWithTag("Level"));

        foreach (GameObject toClear in _toDress)
        {
            // we search for and destroy an object called Visuals, since this contains everything this script creates
            Transform toDestroy = toClear.transform.Find("Visuals");

            // We destroy that folder and with that everything we created before
            if(toDestroy)
                DestroyImmediate(toDestroy.gameObject, false);

            // We make the blockout, we covered up previously, visible again
            MeshRenderer[] meshRenderer = toClear.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in meshRenderer)
            {
                renderer.enabled = true;
            }

        }
    }
}
