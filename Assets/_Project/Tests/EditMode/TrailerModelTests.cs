using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dad.Tests
{
    /// <summary>The GLB trailer models must import whole: every triangle and the
    /// largest texture of the source file, with no shader failures.</summary>
    public class TrailerModelTests
    {
        const string Root = "Assets/Trailer/Models/";

        // Triangles and largest texture measured from each source GLB. The trailer people went with every other person on 2026-11-12.
        [TestCase("warehouse/abandoned_warehouse_-_interior_scene.glb", 993366, 4096)]
        [TestCase("suv/tayota_prado_2010_3d_model.glb", 595366, 0)]
        [TestCase("crate/game-ready_wooden_crate.glb", 2718, 1024)]
        public void ImportsAtFullQuality(string file, int triangles, int largest)
        {
            string path = Root + file;
            Assert.That(AssetImporter.GetAtPath(path)?.GetType().Name, Is.EqualTo("GltfImporter"));

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            long found = all.OfType<Mesh>().Sum(m =>
                Enumerable.Range(0, m.subMeshCount).Sum(s => (long)m.GetIndexCount(s)) / 3);
            Assert.That(found, Is.EqualTo(triangles));

            Assert.That(all.OfType<Texture2D>().Select(t => Mathf.Max(t.width, t.height)).DefaultIfEmpty(0).Max(), Is.EqualTo(largest));
            Assert.That(all.OfType<Material>().Where(m => m.shader == null || !m.shader.isSupported)
                .Select(m => m.name), Is.Empty);
        }
    }
}
