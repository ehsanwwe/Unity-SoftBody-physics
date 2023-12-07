using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SoftbodyGenerator : MonoBehaviour
{
    private MeshFilter originalMeshFilter;
    private List<Vector3> writableVertices { get; set; }
    private List<Vector3> writableNormals { get; set; }
    private int[] writableTris { get; set; }
    private Mesh writableMesh;

    private List<GameObject> phyisicedVertexes;
    private new Dictionary<int, int> vertexDictunery;
    public bool runOptimizedVersion = true;
    public float collissionSurfaceOffset = 0.1f;
    private void Awake()
    {
        writableVertices = new List<Vector3>();
        writableNormals = new List<Vector3>();
        phyisicedVertexes = new List<GameObject>();

        originalMeshFilter = GetComponent<MeshFilter>();
        originalMeshFilter.mesh.GetVertices(writableVertices);
        originalMeshFilter.mesh.GetNormals(writableNormals);
        writableTris = originalMeshFilter.mesh.triangles;

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
        foreach (var vertecs in _optimizedVertex)
        {
            var _tempObj = new GameObject("Point "+ _optimizedVertex.IndexOf(vertecs));
            _tempObj.hideFlags = HideFlags.HideInHierarchy;
            _tempObj.transform.parent = this.transform;
            _tempObj.transform.position = new Vector3(
                  transform.lossyScale.x * (transform.position.x + vertecs.x)
                , transform.lossyScale.y * (transform.position.y + vertecs.y)
                , transform.lossyScale.z * (transform.position.z + vertecs.z)
            );
            var sphereColider = _tempObj.AddComponent<SphereCollider>() as SphereCollider;
            sphereColider.radius = collissionSurfaceOffset;

            _tempObj.AddComponent<Rigidbody>();
            phyisicedVertexes.Add(_tempObj);
        }



        List<Vector2Int> listOfSprings = new List<Vector2Int>();
        for (int i=0;i<writableTris.Length-3;i++)
        {
            int index0 = vertexDictunery[writableTris[i]];
            int index1 = vertexDictunery[writableTris[i+1]];
            int index2 = vertexDictunery[writableTris[i+2]];

            listOfSprings.Add(new Vector2Int(index0, index1));
            listOfSprings.Add(new Vector2Int(index1, index2));
            listOfSprings.Add(new Vector2Int(index2, index0));

        }
        var noDupesListOfSprings = listOfSprings.Distinct().ToList();
        foreach(var jointIndex in noDupesListOfSprings)
        {
            if (jointIndex.x == jointIndex.y)
                continue;
            var joint3 =  phyisicedVertexes[jointIndex.x].AddComponent<SpringJoint>();
            if (!runOptimizedVersion)
                joint3.enableCollision = true;
            joint3.spring = 1000;
            joint3.damper = 7;
            joint3.connectedBody = phyisicedVertexes[jointIndex.y].GetComponent<Rigidbody>();
        }


    }
    public void Update()
    {
        var tempVertexes = new Vector3[originalMeshFilter.mesh.vertices.Length] ;
        for(int i = 0; i < tempVertexes.Length; i++)
        {
            tempVertexes[i]  = phyisicedVertexes[vertexDictunery[i]].transform.localPosition;
            
        }
        originalMeshFilter.mesh.vertices = tempVertexes;
        originalMeshFilter.mesh.RecalculateBounds();
        
    }
}
