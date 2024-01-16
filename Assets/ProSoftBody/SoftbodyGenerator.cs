using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GK;

public class SoftbodyGenerator : MonoBehaviour
{
    private MeshFilter originalMeshFilter;
    private List<Vector3> writableVertices { get; set; }
    private List<Vector3> writableVerticesConvaxed;//{ get; set; }
    private List<Vector3> writableNormals { get; set; }
    private List<Vector3> writableNormalsConvaxed;//{ get; set; }

    private List<SphereCollider> sphereColliders = new List<SphereCollider>();
    private int[] writableTris { get; set; }
    private List<int> writableTrisConvaxed;// { get; set; }
    private Mesh writableMesh;

    private List<GameObject> phyisicedVertexes;
    private new Dictionary<int, int> vertexDictunery;
    /** public variable to controll softbody **/
    public bool runOptimizedVersion = false;
    public float _collissionSurfaceOffset = 0.1f;
    public float collissionSurfaceOffset
    {
        get
        {
            return _collissionSurfaceOffset;
        }
        set
        {
            _collissionSurfaceOffset = value;
            if (phyisicedVertexes != null)
                foreach (var sCollider in sphereColliders)
                    sCollider.radius = _collissionSurfaceOffset;
        }
    }

    public SoftJointLimitSpring springlimit;
    public float _softness = 1f;
    public float softness
    {
        get
        {
            return _softness;
        }
        set
        {
            _softness = value;
            if(phyisicedVertexes!=null)
            foreach (var gObject in phyisicedVertexes)
                gObject.GetComponent<SpringJoint>().spring = _softness;

            springlimit.spring = _softness;
        }
    }
    public float _damp = .2f;
    public float damp
    {
        get
        {
            return _damp;
        }
        set
        {
            _damp = value;
            if (phyisicedVertexes != null)
            foreach (var gObject in phyisicedVertexes)
                gObject.GetComponent<SpringJoint>().damper = _damp;

            springlimit.damper = _damp;
        }
    }
    public float _mass = 1f;
    public float mass
    {
        get
        {
            return _mass;
        }
        set
        {
            _mass = value;
            if (phyisicedVertexes != null)
                foreach (var gObject in phyisicedVertexes)
                    gObject.GetComponent<Rigidbody>().mass = _mass;
        }
    }

    private bool _debugMode = false;
    public bool debugMode
    {
        get
        {
            return _debugMode;
        }
        set
        {
            _debugMode = value;
            if (_debugMode == false)
            {
                if (phyisicedVertexes != null)
                foreach (var gObject in phyisicedVertexes)
                    gObject.hideFlags = HideFlags.HideAndDontSave;
                if (centerOfMasObj != null)
                    centerOfMasObj.hideFlags = HideFlags.HideAndDontSave;
            } else {
                if (phyisicedVertexes != null)
                foreach (var gObject in phyisicedVertexes)
                    gObject.hideFlags = HideFlags.None;
                if(centerOfMasObj!=null)
                    centerOfMasObj.hideFlags = HideFlags.None;
            }

        }
    }


    private float _physicsRoughness = 4;
    public float physicsRoughness {
        get {
            return _physicsRoughness;
        }
        set {
            _physicsRoughness = value;
            if (phyisicedVertexes != null)
            foreach (var gObject in phyisicedVertexes)
                gObject.GetComponent<Rigidbody>().drag = physicsRoughness;
        }
    }
    private bool _gravity = true;
    public bool gravity
    {
        get
        {
            return _gravity;
        }
        set
        {
            _gravity = value;
            if (phyisicedVertexes != null)
                foreach (var gObject in phyisicedVertexes)
                    gObject.GetComponent<Rigidbody>().useGravity = _gravity;
            if (centerOfMasObj != null)
                centerOfMasObj.GetComponent<Rigidbody>().useGravity = _gravity;
        }
    }
    public GameObject centerOfMasObj = null;
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
        writableMesh.MarkDynamic();        
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


            // add collider to each of vertex ( sphere collider )
            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;
            // add current collider to Collider list ;
            sphereColliders.Add(sphereColider);


            // add rigidBody to each of vertex
            var _tempRigidBody = _tempObj.AddComponent<Rigidbody>();
            _tempRigidBody.mass = mass / _optimizedVertex.Count;
            _tempRigidBody.drag = physicsRoughness;
            

            
            
            
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

            // add collider to center of mass as a sphere collider
            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;
            // add current collider to Collider list ;
            sphereColliders.Add(sphereColider);

            // add rigidBody to center of mass as a sphere collider
            var _tempRigidBody = _tempObj.AddComponent<Rigidbody>();
            
            centerOfMasObj = _tempObj;            
        }

        // IGNORE COLLISTION BETWEEN ALL OF THE VERTEXES AND CENTER OFF MASS
        foreach (var collider1 in sphereColliders)
        {
            foreach (var collider2 in sphereColliders)
            {
                Physics.IgnoreCollision(collider1, collider2, true);
            }
        }

        // Extract Lines from quad of mesh
        List<Vector2Int> tempListOfSprings = new List<Vector2Int>();
        bool isFirstTrisOfQuad = true;
        for (int i=0;i<writableTris.Length;i+=3)
        {
            int index0 = vertexDictunery[writableTris[i]];
            int index1 = vertexDictunery[writableTris[i+1]];
            int index2 = vertexDictunery[writableTris[i+2]];
            
            tempListOfSprings.Add(new Vector2Int(index1, index2));
            // this System convert Tris To Quad
            if (isFirstTrisOfQuad)
            {
                tempListOfSprings.Add(new Vector2Int(index0, index1));
                isFirstTrisOfQuad = false;
            }
            else
            {
                tempListOfSprings.Add(new Vector2Int(index2, index0));
                isFirstTrisOfQuad = true;
            }
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

            springlimit.damper = damp;
            springlimit.spring = softness;

            thisBodyJoint.swingLimitSpring = springlimit;
            thisBodyJoint.twistLimitSpring = springlimit;

            if (!runOptimizedVersion)
                thisBodyJoint.enableCollision = true;
           
            
        }
        
        // Decelare Center of mass variable
        foreach (var jointIndex in phyisicedVertexes)
        {
            var destinationBodyJoint = jointIndex.AddComponent<SpringJoint>();
            
            float distanceToCenterOfmass = Vector3.Distance(
                  centerOfMasObj.transform.localPosition
                , destinationBodyJoint.transform.localPosition
            );
            
            destinationBodyJoint.connectedBody = centerOfMasObj.GetComponent<Rigidbody>();
            destinationBodyJoint.spring = softness;
            destinationBodyJoint.damper = damp;

            //destinationBodyJoint.massScale = 0.001f;
            //destinationBodyJoint.connectedMassScale = 0.001f;

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
        //originalMeshFilter.mesh.RecalculateTangents();
        originalMeshFilter.mesh.RecalculateNormals();
    }

}

public class DebugColorGameObject : MonoBehaviour
{
    public Color Color { get; set; }
}

[CustomEditor(typeof(SoftbodyGenerator))]
public class LookAtPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SoftbodyGenerator softbody = target as SoftbodyGenerator;       
        
        softbody.debugMode = EditorGUILayout.Toggle("#Debug mod", softbody.debugMode);
        EditorGUILayout.Space();

        string[] options = new string[] { "  version 1", "  version 2" };
        

        softbody.gravity = EditorGUILayout.Toggle("Gravity", softbody.gravity);
        softbody.mass = EditorGUILayout.FloatField("Mass(KG)", softbody.mass);
        softbody.physicsRoughness = EditorGUILayout.FloatField("Drag (roughness)", softbody.physicsRoughness);
        softbody.softness = EditorGUILayout.FloatField("Softbody hardness", softbody.softness);
        softbody.damp = EditorGUILayout.FloatField("Softbody damper", softbody.damp);
        softbody.collissionSurfaceOffset = EditorGUILayout.FloatField("Softbody Offset", softbody.collissionSurfaceOffset);
        
    }
}