using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;

[ExecuteInEditMode]
public class SplinePlacer : MonoBehaviour
{
    public bool run; //Run the script
    public bool deleteObjects; //Delete all placed pieces immediately
    public bool keepObjects; //Clears the list making the objects stay in the scene

    public SplineContainer spline; //Spline we are placing items on
    
    public List<GameObject> prefabVariants = new(); //List of prefabs to place (will use the same prefab if only 1 is entered)

    public int prefabMaxSize; //Max and min size of the prefab if resizable, if not max size is used as the spacing between objects

    public int prefabMinSize; //Min size of the prefab if resizeable

    public bool fixedSize = true; //Bool to make the pieces fixed size

    public float angleDeviance = 3f; //Maximum angle a connected piece can deviate before a new piece must be placed (variable size only)

    public bool useGlobalUp = false; //Bools to determine what the object uses as "up"

    public bool useTransformYAsHeight = false; //if it should use the Y value of the spline evaluation or the spline gameobject Y

    [SerializeField] private float totalLength; //Total length of the spline
    [SerializeField] private float stepSize; //Step size normalized between 0 and 1
    [SerializeField] private int totalObjects; //Total objects placed
    private List<GameObject> placedObjects = new();

    // Update is called once per frame
    void Update()
    {
        if (run)
        {
            if (fixedSize) PlaceObjectsFixedSize();
            else PlaceObjectsVariableSize();
            run = false;
        }
        if (deleteObjects)
        {
            foreach (GameObject obj in placedObjects) DestroyImmediate(obj);
            deleteObjects = false;
        }
        if (keepObjects) 
        {
            placedObjects.Clear();
        }
    }

    void PlaceObjectsFixedSize()
    {
        totalLength = spline.CalculateLength(); //Get the total length of the spline

        float step = totalLength / prefabMaxSize; //Get the step size in units

        stepSize = 1 / step; //normalize the step size between 0 and 1

        foreach (GameObject obj in placedObjects) DestroyImmediate(obj); // Destroy all previously placed objects
        placedObjects.Clear(); //Clear the list

        float t = 0f; //Evaluation parameter
        while (t < 1f)
        {
            float3 startPos; //world space position of the spline start point
            float3 startTang; //tangent of the spline start point
            float3 startUp; //up vector of the spline start point

            float3 midPos;
            float3 midTang;
            float3 midUp;

            float3 endPos;
            float3 endTang;
            float3 endUp;

            spline.Evaluate(t, out startPos, out startTang, out startUp); //Find the position, tangent, and up of the spine at the specific evaluation point
            if (t + stepSize <= 1f)
            {
                spline.Evaluate(t + stepSize, out endPos, out endTang, out endUp);
                spline.Evaluate(t + (stepSize / 2f), out midPos, out midTang, out midUp);
            }
            else break;

            Vector3 worldSpaceMiddle = Vector3.Lerp(startPos, endPos, 0.5f);
            

            GameObject newObject = null;

            if (useTransformYAsHeight) //Set height to y of spline object rather than spline evaluation
            {
                worldSpaceMiddle.y = spline.gameObject.transform.position.y;
            }

            if (prefabVariants.Count > 1)
            {
                int randomIndex = UnityEngine.Random.Range(0, prefabVariants.Count); //Choose a random variant
                newObject = Instantiate(prefabVariants[randomIndex], worldSpaceMiddle, Quaternion.identity); //Instantiate one of the variants
            }
            else
            {
                newObject = Instantiate(prefabVariants[0], worldSpaceMiddle, Quaternion.identity); //Instantiate prefab
            }
            
            if (useGlobalUp) //Use global Y as the up vector instead of the spline evaluation up
            {
                Vector3 flattenedDirection = new Vector3(endPos.x - startPos.x, 0f, endPos.z - startPos.z).normalized;
                newObject.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(90f, Vector3.up) * flattenedDirection, Vector3.up);
            }
            else
            {
                newObject.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(90f, midUp) * midTang, midUp);
            }
            placedObjects.Add(newObject);
            t += stepSize;
        }
        totalObjects = placedObjects.Count;
    }

    void PlaceObjectsVariableSize()
    {
        //Clear existing objects
        foreach (GameObject wall in placedObjects) DestroyImmediate(wall);
        placedObjects.Clear();

        //Calculate how to step through the spline
        totalLength = spline.CalculateLength();
        float step = totalLength / prefabMinSize;
        stepSize = 1 / step;
        int stepsBetweenMinMax = Mathf.RoundToInt(prefabMaxSize / prefabMinSize);

        float objScale = prefabMinSize; //Set minimum scale
        
        //Prep while loop escape conditions
        float t = 0f;
        int breakCounter = 0;
        bool killLoop = false;

        while (t < 1f && !killLoop)
        {
            //emergency break to prevent overflow
            breakCounter++;
            if (breakCounter > 10000) break;

            float3 startPos;
            float3 startTang;
            float3 startUp;

            float3 endPos = new();
            float3 endTang;
            float3 endUp;

            spline.Evaluate(t, out startPos, out startTang, out startUp);

            int scaleFactor = 0;
            float startT = t;
            float endT = t + stepSize;
            for (int i = 1; i < stepsBetweenMinMax; i++)
            {
                float tempT = t + stepSize * i;
                if (tempT > 1f && spline.Spline.Closed) tempT = 0f;
                scaleFactor += 1;
                float3 currentPos;
                float3 currentTang;
                float3 currentUp;
                spline.Evaluate(tempT, out currentPos, out currentTang, out currentUp);
                if (tempT == 0f)
                {
                    endPos = currentPos;
                    endTang = currentTang;
                    endUp = currentUp;
                    killLoop = true;
                    break;
                }
                if (i == 1)
                {
                    endPos = currentPos;
                    endTang = currentTang;
                    endUp = currentUp;
                }
                if (Vector3.Angle(startTang, currentTang) > angleDeviance && i != 1)
                {
                    t = tempT - stepSize;
                    endT = tempT - stepSize;
                    break;
                }
                else
                {
                    endPos = currentPos;
                    endTang = currentTang;
                    endUp = currentUp;
                    if (i == stepsBetweenMinMax - 1)
                    {
                        t = tempT;
                        endT = tempT;
                        break;
                    }
                }
            }

            //Create object
            GameObject obj = null;
            if (prefabVariants.Count > 1)
            {
                int randomIndex = UnityEngine.Random.Range(0, prefabVariants.Count); //Choose a random variant
                obj = Instantiate(prefabVariants[randomIndex]);
            }
            else
            {
                obj = Instantiate(prefabVariants[0]);
            }
            placedObjects.Add(obj);

            //Determine position on spline and evaluate midpoint
            Vector3 start = startPos;
            Vector3 end = endPos;
            Vector3 midpoint = Vector3.Lerp(start, end, 0.5f);
            float midT = (startT + endT) / 2;
            float3 midPos;
            float3 midTang;
            float3 midUp;
            spline.Evaluate(midT, out midPos, out midTang, out midUp);

            //Set height of the object
            if (useTransformYAsHeight)
            {
                obj.transform.position = new Vector3(midpoint.x, spline.transform.position.y, midpoint.z);
            }
            else
            {
                obj.transform.position = midPos;
            }

            //Determine what the "up" direction of the object is
            if (useGlobalUp)
            {
                Vector3 flattenedDirection = new Vector3(endPos.x - startPos.x, 0f, endPos.z - startPos.z).normalized;
                obj.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(90f, Vector3.up) * flattenedDirection, Vector3.up);
            }
            else
            {
                obj.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(90f, midUp) * midTang, midUp);
            }

            //Set the scale of the object
            objScale = Vector3.Distance(start, end);
            obj.transform.localScale = new Vector3((objScale), 1, 1);
        }
        totalObjects = placedObjects.Count;
    }
}
