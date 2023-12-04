using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoftbodyGenerator : MonoBehaviour
{
    private List<Vector3> writableVertices { get; set; }
    private List<Vector3> writableNormals { get; set; }
    private int[] writableTris { get; set; }
    private Mesh writableMesh;

    private List<GameObject> phyisicedVertexes;
    private void Awake()
    {
        writableVertices = new List<Vector3>();
        writableNormals = new List<Vector3>();
        phyisicedVertexes = new List<GameObject>();

        var _originalMeshFilter = GetComponent<MeshFilter>();        
        _originalMeshFilter.mesh.GetVertices(writableVertices);
        _originalMeshFilter.mesh.GetNormals(writableNormals);
        writableTris =  _originalMeshFilter.mesh.triangles ;

        writableMesh = new Mesh();
        writableMesh.SetVertices(writableVertices);
        writableMesh.SetNormals(writableNormals);
        writableMesh.triangles = writableTris;

        _originalMeshFilter.mesh = writableMesh;


        foreach (var vertecs in writableVertices)
        {
            var _tempObj = Instantiate(new GameObject());
            _tempObj.transform.parent = this.transform;
            _tempObj.AddComponent<SphereCollider>();
            _tempObj.transform.position = vertecs;

        }

    }
    void Start()
    {
        
    }

    
    void Update()
    {
        
    }
}
