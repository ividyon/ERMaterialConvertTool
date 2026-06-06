using PPlus;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    public static bool RemoveMotionBlur(ref FLVER2 flver, ref string filePath)
    {
        PromptPlus.WriteLine(
            "This tool will remove Motion Blur facesets from the model, to fix NR port issues.");

        PromptPlus.WriteLine("Processing...");
        foreach (FLVER2.Mesh mesh in flver.Meshes)
        {
            List<FLVER.Vertex> verts = new();
            List<FLVER2.FaceSet> faceSetsToRemove = new();
            foreach (FLVER2.FaceSet faceSet in mesh.FaceSets.Where(f => f.Flags.HasFlag(FLVER2.FaceSet.FSFlags.MotionBlur)))
            {
                foreach (int i in faceSet.Indices)
                {
                    verts.AddRange(mesh.Vertices[i]);
                }
                faceSetsToRemove.Add(faceSet);
            }

            foreach (FLVER2.FaceSet faceSet in faceSetsToRemove)
            {
                mesh.FaceSets.Remove(faceSet);
            }
            foreach (FLVER.Vertex vert in verts)
            {
                mesh.Vertices.Remove(vert);
            }
        }

        Program.SaveFlver(ref flver, ref filePath, true);

        return true;
    }
}