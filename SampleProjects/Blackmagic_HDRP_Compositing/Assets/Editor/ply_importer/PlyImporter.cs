using System.Collections;
using System.Collections.Generic;
using System.IO;
using ThreeDeeBear.Models.Ply;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

[ScriptedImporter(1, "ply")]
public class PlyImporter : ScriptedImporter
{
    Mesh m_Mesh;
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var resultPly = PlyHandler.GetVerticesAndTriangles(ctx.assetPath);
        m_Mesh = new Mesh();
        m_Mesh.vertices = resultPly.Vertices.ToArray();
        m_Mesh.triangles = resultPly.Triangles.ToArray();
        var gameObject = new GameObject(new FileInfo(ctx.assetPath).Name);
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshFilter>();

        var newMat = new Material(Shader.Find("Unlit/Color"));
        newMat.color =  new Color(1f,1f,1f,1f);;

        var meshRenderer = gameObject.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = newMat;
        var meshFilter = gameObject.GetComponent<MeshFilter>();
        m_Mesh.RecalculateNormals();
        meshFilter.sharedMesh = m_Mesh;
        ctx.AddObjectToAsset(gameObject.name, gameObject);
        ctx.SetMainObject(gameObject);
        ctx.AddObjectToAsset("mesh", m_Mesh);
        ctx.AddObjectToAsset("mat", newMat);
    }
}
