using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GK;
public class SoftbodyGenerator : MonoBehaviour
{
    public LayerMask layer;
    private MeshFilter originalMeshFilter;
    private List<Vector3> writableVertices { get; set; }
    private List<Vector3> writableVerticesConvaxed;//{ get; set; }
    private List<Vector3> writableNormals { get; set; }
    private List<Vector3> writableNormalsConvaxed;//{ get; set; }
    private int[] writableTris { get; set; }
    private List<int> writableTrisConvaxed;// { get; set; }
    private Mesh writableMesh;

    private List<GameObject> phyisicedVertexes;
    private new Dictionary<int, int> vertexDictunery;
    /** public variable to controll softbody **/
    public bool runOptimizedVersion = true;
    public float collissionSurfaceOffset = 0.1f;
    public float softness = 1000f;
    public float damp = 100f;
    public float mass = 1f;
    public float gravity = 7f;
    public bool debugMode = false;
    public float physicsRoughness = 0;
    private void Awake()
    {
       
        writableVertices = new List<Vector3>();
        writableVerticesConvaxed = new List<Vector3>();
        writableNormals = new List<Vector3>();
        writableNormalsConvaxed = new List<Vector3>();
        phyisicedVertexes = new List<GameObject>();

        writableTrisConvaxed = new List<int>();

        originalMeshFilter = GetComponent<MeshFilter>();
        originalMeshFilter.mesh.GetVertices(writableVertices);
        originalMeshFilter.mesh.GetNormals(writableNormals);
        writableTris = originalMeshFilter.mesh.triangles;

        new ConvexHullCalculator().GenerateHull(
            writableVertices
            , false
            , ref writableVerticesConvaxed, ref writableTrisConvaxed, ref writableNormalsConvaxed
            );
        
        writableMesh = new Mesh();
        writableMesh.SetVertices(writableVerticesConvaxed);
        writableMesh.SetNormals(writableNormalsConvaxed);
        writableMesh.triangles = writableTrisConvaxed.ToArray();
        
        originalMeshFilter.mesh = writableMesh;
        
        // remove duplicated vertex
        var _optimizedVertex = new List<Vector3>();

        // first column = original vertex index , last column = optimized vertex index 
        vertexDictunery = new Dictionary<int, int>();
        for (int i = 0; i < writableVerticesConvaxed.Count; i++)
        {   
            bool isVertexDuplicated = false;
            for (int j = 0; j < _optimizedVertex.Count; j++)
                if (_optimizedVertex[j] == writableVerticesConvaxed[i])
                {
                    isVertexDuplicated = true;
                    vertexDictunery.Add(i, j);
                    break;
                }
            if (!isVertexDuplicated)
            {
                _optimizedVertex.Add(writableVerticesConvaxed[i]);
                vertexDictunery.Add(i, _optimizedVertex.Count - 1);
            }
        }
        

        // create balls at each of vertex also center of mass
        foreach (var vertecs in _optimizedVertex)
        {
            var _tempObj = new GameObject("Point "+ _optimizedVertex.IndexOf(vertecs));

            // add laye to rigided body physics can't affect together
            _tempObj.layer = 6;

            if (!debugMode)
                _tempObj.hideFlags = HideFlags.HideAndDontSave;

            _tempObj.transform.parent = this.transform;
            _tempObj.transform.position = new Vector3(
                  transform.lossyScale.x * (transform.position.x + vertecs.x)
                , transform.lossyScale.y * (transform.position.y + vertecs.y)
                , transform.lossyScale.z * (transform.position.z + vertecs.z)
            );
            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;
            

            var _tempRigidBody = _tempObj.AddComponent<Rigidbody>();
            _tempRigidBody.mass = mass / _optimizedVertex.Count;
            _tempRigidBody.drag = physicsRoughness;


            phyisicedVertexes.Add(_tempObj);
        }



        // calculate center of mass
        Vector3 tempCenter = Vector3.zero;

        foreach (var vertecs in _optimizedVertex)
            tempCenter = new Vector3(tempCenter.x + vertecs.x, tempCenter.y + vertecs.y,tempCenter.z + vertecs.z );

        Vector3 centerOfMass = new Vector3(
              tempCenter.x / _optimizedVertex.Count
            , tempCenter.y / _optimizedVertex.Count
            , tempCenter.z / _optimizedVertex.Count
        );
        GameObject centerOfMasObj = null;
        // add center of mass vertex to OptimizedVertex list
        {
            var _tempObj = new GameObject("centerOfMass");
            if (!debugMode)
                _tempObj.hideFlags = HideFlags.HideAndDontSave;
            _tempObj.transform.parent = this.transform;
            _tempObj.transform.localPosition = centerOfMass;

            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;


            var _tempRigidBody = _tempObj.AddComponent<ArticulationBody>();
            centerOfMasObj = _tempObj;            
        }



        
        List<Vector2Int> tempListOfSprings = new List<Vector2Int>();
        
        for (int i=0;i<writableTrisConvaxed.Count;i+=3)
        {
            int index0 = vertexDictunery[writableTrisConvaxed[i]];
            int index1 = vertexDictunery[writableTrisConvaxed[i+1]];
            int index2 = vertexDictunery[writableTrisConvaxed[i+2]];

            tempListOfSprings.Add(new Vector2Int(index0, index1));
            tempListOfSprings.Add(new Vector2Int(index1, index2));
            tempListOfSprings.Add(new Vector2Int(index2, index0));


        }

        // distinct normal Duplicates with check revers
        Debug.Log(tempListOfSprings.Count);
        for (int i = 0; i < tempListOfSprings.Count; i++)
        {
            bool isDuplicated = false;
            Vector2Int normal = tempListOfSprings[i];
            Vector2Int reversed = new Vector2Int(tempListOfSprings[i].y, tempListOfSprings[i].x);
            for (int j = 0; j < noDupesListOfSprings.Count; j++)
            {
                if (normal == tempListOfSprings[j])
                {
                    isDuplicated = true;
                    break;
                }
                else if (reversed == tempListOfSprings[j])
                {
                    isDuplicated = true;
                    break;
                }                
                
            }
            if (isDuplicated == false)
                noDupesListOfSprings.Add(tempListOfSprings[i]);
        }
        // making Springs bodies
        foreach (var jointIndex in noDupesListOfSprings)
        {            
            var thisGameObject = phyisicedVertexes[jointIndex.x];
            var thisBodyJoint = thisGameObject.AddComponent<SpringJoint>();
            var destinationBody = centerOfMasObj.GetComponent<Rigidbody>();
            float distanceBetween = Vector3.Distance(thisGameObject.transform.position, destinationBody.transform.position);
            

            // configure current spring joint
            thisBodyJoint.connectedBody = destinationBody;
            thisBodyJoint.spring = softness;
            thisBodyJoint.damper = damp;
            thisBodyJoint.autoConfigureConnectedAnchor = false;
            thisBodyJoint.connectedAnchor = Vector3.zero;
            thisBodyJoint.anchor = Vector3.zero;
            thisBodyJoint.minDistance = distanceBetween;
            thisBodyJoint.maxDistance = distanceBetween;
            if (!runOptimizedVersion)
                thisBodyJoint.enableCollision = true;
           
            
        }
        // Decelare Center of mass variable
        var centerOfMassPoint = phyisicedVertexes[phyisicedVertexes.Count - 1];
        Debug.Log(centerOfMassPoint.name);
        foreach (var jointIndex in noDupesListOfSprings)
        {
            
            var thisGameObject = centerOfMassPoint;
            var destinationBodyJoint = thisGameObject.AddComponent<SpringJoint>();
            var destinationBody = phyisicedVertexes[jointIndex.x].GetComponent<ArticulationBody>();
            float distanceToCenterOfmass = Vector3.Distance(thisGameObject.transform.localPosition, destinationBody.transform.localPosition);

            destinationBodyJoint.connectedArticulationBody = destinationBody;
            destinationBodyJoint.spring = softness ;
            destinationBodyJoint.damper = damp;
            destinationBodyJoint.autoConfigureConnectedAnchor = false;
            destinationBodyJoint.connectedAnchor = Vector3.zero;
            destinationBodyJoint.anchor = Vector3.zero;
            destinationBodyJoint.minDistance = distanceToCenterOfmass;
            destinationBodyJoint.maxDistance = distanceToCenterOfmass;
            if (!runOptimizedVersion)
                destinationBodyJoint.enableCollision = true;
        }
        */
    }
    List<Vector2Int> noDupesListOfSprings = new List<Vector2Int>();
    public void Update()
    {
       /* if (debugMode)
        {
            foreach (var jointIndex in noDupesListOfSprings)
            {
                if (jointIndex.x == jointIndex.y)
                    continue;
                Debug.DrawLine(
                    phyisicedVertexes[jointIndex.x].transform.position
                    , phyisicedVertexes[jointIndex.y].transform.position
                    ,Random.ColorHSV()

                );

            }
        }

        var tempVertexes = new Vector3[originalMeshFilter.mesh.vertices.Length];
        for (int i = 0; i < tempVertexes.Length; i++)
        {
            tempVertexes[i] = phyisicedVertexes[vertexDictunery[i]].transform.localPosition;

        }
        originalMeshFilter.mesh.vertices = tempVertexes;
        originalMeshFilter.mesh.RecalculateBounds();
        originalMeshFilter.mesh.RecalculateTangents();
        originalMeshFilter.mesh.RecalculateNormals();
        */
    }
}
