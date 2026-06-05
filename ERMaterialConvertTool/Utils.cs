using System.Numerics;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool;

public static class FlverUtils
{
    public static void PadVertex(FLVER.Vertex vertex, IEnumerable<FLVER2.BufferLayout> bufferLayouts)
    {
        Dictionary<FLVER.LayoutSemantic, int> usageCounts = new();
        int normalWCount = 0;
        FLVER.LayoutSemantic[] paddedProperties =
            { FLVER.LayoutSemantic.Position, FLVER.LayoutSemantic.Normal, FLVER.LayoutSemantic.Tangent, FLVER.LayoutSemantic.UV, FLVER.LayoutSemantic.VertexColor };

        IEnumerable<FLVER.LayoutMember> layoutMembers = bufferLayouts.SelectMany(bufferLayout => bufferLayout)
            .Where(x => paddedProperties.Contains(x.Semantic));
        foreach (FLVER.LayoutMember layoutMember in layoutMembers)
        {
            bool isDouble = layoutMember is
            {
                Semantic: FLVER.LayoutSemantic.UV,
                Type: FLVER.LayoutType.Float4 or
                FLVER.LayoutType.Short4 or
                FLVER.LayoutType.Half2 or
                FLVER.LayoutType.UByte4Norm
            };
            int count = isDouble ? 2 : 1;

            if (!usageCounts.TryAdd(layoutMember.Semantic, count))
            {
                usageCounts[layoutMember.Semantic] += count;
            }

            if (layoutMember.Semantic == FLVER.LayoutSemantic.Normal && layoutMember.Type != FLVER.LayoutType.Float3)
            {
                normalWCount += 1;
            }
        }

        if (usageCounts.TryGetValue(FLVER.LayoutSemantic.Position, out int posCount))
        {
            int missingPosCount = posCount - vertex.Positions.Count;
            var replacePosition = Vector3.Zero;
            if (vertex.Positions.Count > 0)
            {
                replacePosition = vertex.Positions.First();
            }
            for (int i = 0; i < missingPosCount; i++)
            {
                vertex.Positions.Add(replacePosition);
            }
        }

        if (usageCounts.TryGetValue(FLVER.LayoutSemantic.Normal, out int normCount))
        {
            int missingNormCount = normCount - vertex.Normals.Count;
            var replaceNormal = Vector3.Zero;
            if (vertex.Normals.Count > 0)
            {
                replaceNormal = vertex.Normals.First();
            }
            for (int i = 0; i < missingNormCount; i++)
            {
                vertex.Normals.Add(replaceNormal);
            }
        }

        int missingNormWCount = normalWCount - vertex.NormalWs.Count;
        var replaceNormW = 0;
        if (vertex.NormalWs.Count > 0)
        {
            replaceNormW = vertex.NormalWs.First();
        }
        for (int i = 0; i < missingNormWCount; i++)
        {
            vertex.NormalWs.Add(replaceNormW);
        }

        if (usageCounts.TryGetValue(FLVER.LayoutSemantic.Tangent, out int tangentCount))
        {
            int missingTangentCount = tangentCount - vertex.Tangents.Count;
            var replaceTangent = Vector4.Zero;
            if (vertex.Tangents.Count > 0)
            {
                replaceTangent = vertex.Tangents.First();
            }
            for (int i = 0; i < missingTangentCount; i++)
            {
                vertex.Tangents.Add(replaceTangent);
            }
        }

        if (usageCounts.TryGetValue(FLVER.LayoutSemantic.UV, out int uvCount))
        {
            int missingUvCount = uvCount - vertex.UVs.Count;
            var replaceUv = Vector3.Zero;
            if (vertex.UVs.Count > 0)
                replaceUv = vertex.UVs.First();
            for (int i = 0; i < missingUvCount; i++)
            {
                vertex.UVs.Add(replaceUv);
            }
        }

        if (usageCounts.TryGetValue(FLVER.LayoutSemantic.VertexColor, out int colorCount))
        {
            int missingColorCount = colorCount - vertex.Colors.Count;
            for (int i = 0; i < missingColorCount; i++)
            {
                vertex.Colors.Add(new FLVER.VertexColor(255, 255, 0, 0));
            }
        }
    }

    public static List<int> GetLayoutIndices(FLVER2 flver, List<FLVER2.BufferLayout> bufferLayouts)
    {
        List<int> indices = new();

        foreach (FLVER2.BufferLayout referenceBufferLayout in bufferLayouts)
        {
            if (flver.BufferLayouts.Count == 0)
            {
                indices.Add(0);
                flver.BufferLayouts.Add(referenceBufferLayout);
            }
            else
            {
                for (int i = 0; i < flver.BufferLayouts.Count; i++)
                {
                    FLVER2.BufferLayout bufferLayout = flver.BufferLayouts[i];
                    if (bufferLayout.SequenceEqual(referenceBufferLayout, new LayoutMemberComparer()))
                    {
                        indices.Add(i);
                        break;
                    }

                    if (i != flver.BufferLayouts.Count - 1) continue;

                    indices.Add(i + 1);
                    flver.BufferLayouts.Add(referenceBufferLayout);
                    break;
                }
            }
        }

        return indices;
    }

    public static void AdjustBoneIndexBufferSize(FLVER2 flver, List<FLVER2.BufferLayout> bufferLayouts)
    {
        if (flver.Nodes.FindLastIndex(x => !x.Flags.HasFlag(FLVER.Node.NodeFlags.Disabled)) <= byte.MaxValue) return;

        foreach (FLVER2.BufferLayout bufferLayout in bufferLayouts)
        {
            foreach (FLVER.LayoutMember layoutMember in bufferLayout.Where(x =>
                         x.Semantic == FLVER.LayoutSemantic.BoneIndices))
            {
                layoutMember.Type = FLVER.LayoutType.UShort4;
            }
        }
    }

    private class LayoutMemberComparer : IEqualityComparer<FLVER.LayoutMember>
    {
        public bool Equals(FLVER.LayoutMember? x, FLVER.LayoutMember? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Stream == y.Stream && x.Type == y.Type && x.Semantic == y.Semantic && x.Index == y.Index &&
                   x.Size == y.Size;
        }

        public int GetHashCode(FLVER.LayoutMember obj)
        {
            return HashCode.Combine(obj.Stream, (int)obj.Type, (int)obj.Semantic, obj.Index, obj.Size);
        }
    }
}

public static class FlverExtensions
{
    public static string ToString(this FLVER2.Material m, int index, FLVER2MaterialInfoBank matInfoBank)
    {
        var name = m.Name;
        var mtd = m.MTD;
        var matchDef = matInfoBank.MaterialDefs.Values.FirstOrDefault(d =>
            Path.GetFileNameWithoutExtension(d.MTD).Equals(Path.GetFileNameWithoutExtension(mtd),
                StringComparison.InvariantCultureIgnoreCase));
        var shader = matchDef?.Shader;
        List<string> components = new List<string?>{ name, mtd, shader }.OfType<string>().ToList();
        return $"{index}: {string.Join(" | ", components)}";
    }
}

public static class PromptPlusExtensions
{
    public static string PromptPlusEscape(this string s)
    {
        // return s.Replace("\\", "\\\\").Replace("[", "[[").Replace("]","]]");
        return s;
    }
}