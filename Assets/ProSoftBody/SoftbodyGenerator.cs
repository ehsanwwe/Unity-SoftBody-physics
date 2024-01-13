using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GK;
public class SoftbodyGenerator : MonoBehaviour
{
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
    public float softness = 1f;
    public float damp = .2f;
    public float mass = 1f;
    public bool debugMode = false;
    public float physicsRoughness = 0;
    private GameObject centerOfMasObj = null;
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


        var localToWorld = transform.localToWorldMatrix;
        for (int i = 0; i < writableVertices.Count; ++i)
        {
            writableVertices[i] = localToWorld.MultiplyPoint3x4(writableVertices[i]);
        }
        
        if (runOptimizedVersion)
        {
            new ConvexHullCalculator().GenerateHull(
                 writableVertices
                 , false
                 , ref writableVerticesConvaxed, ref writableTrisConvaxed, ref writableNormalsConvaxed
                 );
            writableVertices = writableVerticesConvaxed;
            writableNormals = writableNormalsConvaxed;
            writableTris = writableTrisConvaxed.ToArray();
        }

        writableMesh = new Mesh();
        writableMesh.SetVertices(writableVertices);
        writableMesh.SetNormals(writableNormals);
        writableMesh.triangles = writableTris;
        originalMeshFilter.mesh = writableMesh;
        // remove duplicated vertex
        var _optimizedVertex = new List<Vector3>();

        // first column = original vertex index , last column = optimized vertex index 
        vertexDictunery = new Dictionary<int, int>();
        for (int i = 0; i < writableVertices.Count; i++)
        {   
            bool isVertexDuplicated = false;
            for (int j = 0; j < _optimizedVertex.Count; j++)
                if (_optimizedVertex[j] == writableVertices[i])
                {
                    isVertexDuplicated = true;
                    vertexDictunery.Add(i, j);
                    break;
                }
            if (!isVertexDuplicated)
            {
                _optimizedVertex.Add(writableVertices[i]);
                vertexDictunery.Add(i, _optimizedVertex.Count - 1);
            }
        }

        
        // create balls at each of vertex also center of mass
        foreach (var vertecs in _optimizedVertex)
        {
            var _tempObj = new GameObject("Point "+ _optimizedVertex.IndexOf(vertecs));


            if (!debugMode)
                _tempObj.hideFlags = HideFlags.HideAndDontSave;

            _tempObj.transform.parent = this.transform;
            _tempObj.transform.position = vertecs; 

            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;
            

            var _tempRigidBody = _tempObj.AddComponent<Rigidbody>();
            _tempRigidBody.mass = mass / _optimizedVertex.Count;
            _tempRigidBody.drag = physicsRoughness;
            //_tempRigidBody.useGravity = false;
            
            if(debugMode)
                _tempObj.AddComponent<DebugColorGameObject>().Color = Random.ColorHSV(); 
            
            
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
        // add center of mass vertex to OptimizedVertex list
        {
            var _tempObj = new GameObject("centerOfMass");
            if (!debugMode)
                _tempObj.hideFlags = HideFlags.HideAndDontSave;
            _tempObj.transform.parent = this.transform;
            _tempObj.transform.position = centerOfMass;

            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;

            var _tempRigidBody = _tempObj.AddComponent<Rigidbody>();
            //_tempRigidBody.useGravity = false;
            centerOfMasObj = _tempObj;            
        }



        
        List<Vector2Int> tempListOfSprings = new List<Vector2Int>();
        
        for (int i=0;i<writableTris.Length;i+=3)
        {
            int index0 = vertexDictunery[writableTris[i]];
            int index1 = vertexDictunery[writableTris[i+1]];
            int index2 = vertexDictunery[writableTris[i+2]];

            tempListOfSprings.Add(new Vector2Int(index0, index1));
            //tempListOfSprings.Add(new Vector2Int(index1, index2));
            tempListOfSprings.Add(new Vector2Int(index2, index0));
        }


        // distinct normal Duplicates with check revers
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
            var thisBodyJoint = thisGameObject.AddComponent<CharacterJoint>();
            var destinationBody = phyisicedVertexes[jointIndex.y].GetComponent<Rigidbody>();
            float distanceBetween = Vector3.Distance(thisGameObject.transform.position, destinationBody.transform.position);
            

            // configure current spring joint
            thisBodyJoint.connectedBody = destinationBody;
            SoftJointLimit jointlimitHihj = new SoftJointLimit();
            jointlimitHihj.bounciness = 1.1f;
            jointlimitHihj.contactDistance = distanceBetween;
            jointlimitHihj.limit = 10;

            SoftJointLimit jointlimitLow = new SoftJointLimit();
            jointlimitLow.bounciness = 1.1f;
            jointlimitLow.contactDistance = distanceBetween;
            jointlimitLow.limit = -10;
            

            thisBodyJoint.highTwistLimit = jointlimitHihj;
            thisBodyJoint.lowTwistLimit = jointlimitLow;
            thisBodyJoint.swing1Limit = jointlimitLow;
            thisBodyJoint.swing2Limit = jointlimitHihj;
            

            //thisBodyJoint.

            SoftJointLimitSpring springlimit = new SoftJointLimitSpring();
            springlimit.damper = 0.3f;
            springlimit.spring = 1f;

            thisBodyJoint.swingLimitSpring = springlimit;
            thisBodyJoint.twistLimitSpring = springlimit;

            if (!runOptimizedVersion)
                thisBodyJoint.enableCollision = true;
           
            
        }
        // Decelare Center of mass variable
        foreach (var jointIndex in phyisicedVertexes)
        {
            var destinationBodyJoint = jointIndex.AddComponent<FixedJoint>();
            
            float distanceToCenterOfmass = Vector3.Distance(
                  centerOfMasObj.transform.localPosition
                , destinationBodyJoint.transform.localPosition
            );
            
            destinationBodyJoint.connectedBody = centerOfMasObj.GetComponent<Rigidbody>();
          
            destinationBodyJoint.massScale = 0.001f;
            destinationBodyJoint.connectedMassScale = 0.001f;
            if (!runOptimizedVersion)
                destinationBodyJoint.enableCollision = true;
                
        }

    }
    List<Vector2Int> noDupesListOfSprings = new List<Vector2Int>();
    public void Update()
    {
       if (debugMode)
        {
            foreach (var jointIndex in noDupesListOfSprings)
            {
                Debug.DrawLine(
                    phyisicedVertexes[jointIndex.x].transform.position
                    , phyisicedVertexes[jointIndex.y].transform.position
                    , phyisicedVertexes[jointIndex.x].GetComponent<DebugColorGameObject>().Color
                );

            }
            foreach (var jointIndex in noDupesListOfSprings)
            {
                Debug.DrawLine(
                      phyisicedVertexes[jointIndex.x].transform.position
                    , centerOfMasObj.transform.position
                    , Color.red
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
    }

}

public class DebugColorGameObject : MonoBehaviour
{
    public Color Color { get; set; }
}