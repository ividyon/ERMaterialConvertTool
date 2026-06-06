using System.ComponentModel.DataAnnotations;
using PPlus;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;

namespace ERMaterialConvertTool.Modes;

public static partial class EditFlver
{
    public enum MatNameDecision
    {
        [Display(Name = "Update to selected MATBIN")]
        UseNewMTD,

        [Display(Name = "Keep previous MATBIN")]
        KeepOldMTD,

        [Display(Name = "Select custom MATBIN")]
        CustomName,
    }

    public static bool ChangeMaterial(FLVER2 flver, string filePath, FLVER2.Material srcMaterial,
        FLVER2MaterialInfoBank srcMatInfoBank, FLVER2MaterialInfoBank tarMatInfoBank, bool single,
        ref MatNameDecision mtdDecisionKeep, ref string? nameKeep, bool auto = false)
    {
        Dictionary<string, FLVER2> dict = new() { { filePath, flver } };
        return ChangeMaterial(dict, srcMaterial, srcMatInfoBank, tarMatInfoBank, single, ref mtdDecisionKeep,
            ref nameKeep, auto);
    }

    public static bool ChangeMaterial(Dictionary<string, FLVER2> flvers, FLVER2.Material srcMaterial,
        FLVER2MaterialInfoBank srcMatInfoBank, FLVER2MaterialInfoBank tarMatInfoBank, bool single,
        ref MatNameDecision mtdDecisionKeep, ref string? nameKeep, bool auto = false)
    {
        if (flvers.Count > 1)
            single = false;

        var srcFlver = flvers.Values.First(f => f.Materials.Contains(srcMaterial));
        var mtd = srcMaterial.MTD;
        Dictionary<FLVER2, List<FLVER2.Material>> groupedMaterials = single
            ? new()
                { { srcFlver, new() { srcMaterial } } }
            : flvers.Values.ToDictionary(f => f,
                f => f.Materials.Where(m => m.MTD.Equals(srcMaterial.MTD, StringComparison.CurrentCultureIgnoreCase))
                    .ToList());
        var groupedIndices =
            groupedMaterials.ToDictionary(kvp => kvp.Key,
                kvp => kvp.Value.Select(m => kvp.Key.Materials.IndexOf(m)).ToList());
        var groupedMeshes = groupedIndices.ToDictionary(kvp => kvp.Key,
            kvp => kvp.Key.Meshes.Where(m => kvp.Value.Contains(m.MaterialIndex)).ToList());

        var allMeshes = groupedMeshes.SelectMany(kvp => kvp.Value).ToList();
        var allMaterials = groupedMaterials.SelectMany(kvp => kvp.Value).ToList();

        var materialStrings = groupedMaterials.SelectMany(kvp =>
        {
            return kvp.Value.Select(m => m.ToString(kvp.Key, srcMatInfoBank));
        }).ToList();
        if (single)
        {
            PromptPlus.WriteLine(
                $"Selected material: {srcMaterial.ToString(srcFlver, srcMatInfoBank)}");
        }
        else
        {
            PromptPlus.WriteLine(
                $"Selected the following materials:\n{string.Join("\n", materialStrings)}");
        }

        var skinned = groupedMeshes.Any(kvp =>
        {
            return kvp.Value.Any(m => m.UseBoneWeights);
            // var flver = kvp.Key;
            // return kvp.Value.Any(m =>
            //     {
            //         var layoutIndices = m.VertexBuffers.Select(a => a.LayoutIndex).ToList();
            //         var layouts = flver.BufferLayouts.Where(l => layoutIndices.Contains(flver.BufferLayouts.IndexOf(l)))
            //             .ToList();
            //         return layouts.Any(l => l.Any(m => m.Semantic == FLVER.LayoutSemantic.BoneWeights));
            //     }
            // );
        });

        bool filterSkinned = false;
        if (skinned)
        {
            if (auto)
            {
                filterSkinned = true;
            }
            else
            {
                // var skinConfirm = PromptPlus
                //     .Confirm(
                //         "This material is used on skinned meshes, which requires specific materials. Filter for those?")
                //     .Run();
                // if (skinConfirm.IsAborted) return false;
                // filterSkinned = skinConfirm.Value.IsYesResponseKey();
                filterSkinned = true;
            }
        }

        if (filterSkinned)
        {
            PromptPlus.WriteLine("Filtering for materials used on skinned meshes...");
            tarMatInfoBank.MaterialDefs = tarMatInfoBank.MaterialDefs.Where(d =>
                d.Value.AcceptableVertexBufferDeclarations.Any(dc =>
                    dc.Buffers.Any(b => b.Any(m => m.Semantic == FLVER.LayoutSemantic.BoneWeights)))).ToDictionary();
        }

        FLVER2MaterialInfoBank.MaterialDef? tempMatDef = null;

        var originalMatDef =
            srcMatInfoBank.MaterialDefs.Values.FirstOrDefault(d =>
                d.MTD.Equals(mtd, StringComparison.CurrentCultureIgnoreCase));
        if (originalMatDef != null)
        {
            var matchDef = tarMatInfoBank.MaterialDefs.Values
                .Where(d => d.Shader != null &&
                            d.Shader.Equals(originalMatDef.Shader, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(d =>
                    d.MTD.Equals(mtd, StringComparison.CurrentCultureIgnoreCase))
                .ToList().FirstOrDefault();
            if (matchDef != null)
            {
                var name = string.Join(" | ", (new string?[] { matchDef.MTD, matchDef.Shader }).OfType<string>());
                if (auto)
                {
                    PromptPlus.WriteLine($"Using matching target material: {name.PromptPlusEscape()}");
                    tempMatDef = matchDef;
                }
                else
                {
                    var confirm = PromptPlus
                        .Confirm($"Found matching target material: {name.PromptPlusEscape()}. Use this?")
                        .Run();
                    if (!confirm.IsAborted && confirm.Value.IsYesResponseKey())
                    {
                        tempMatDef = matchDef;
                    }
                }
            }
            else if (auto)
            {
                PromptPlus.KeyPress(
                        $"Failed operation on {srcMaterial.ToString(srcFlver, srcMatInfoBank)}: no matching material found.")
                    .Run();
                return false;
            }
        }

        if (tempMatDef == null)
        {
            var select = PromptPlus
                .Select<FLVER2MaterialInfoBank.MaterialDef>(
                    $"Select replacement material")
                .TextSelector(a => $"{a.MTD} | {a.Shader}".PromptPlusEscape())
                .AddItems(tarMatInfoBank.MaterialDefs.Values.OrderBy(v => v.MTD.ToLower().StartsWith(mtd)))
                .Run();
            if (select.IsAborted) return false;
            tempMatDef = select.Value;
        }

        FLVER2MaterialInfoBank.MaterialDef replaceMatDef = tempMatDef;

        MatNameDecision nameDecision = MatNameDecision.KeepOldMTD;
        if (!auto)
        {
            var mtdDecisionPrompt = PromptPlus
                .Select<MatNameDecision>("Which MATBIN file to point the material at?")
                .Default(mtdDecisionKeep)
                .Run();
            if (mtdDecisionPrompt.IsAborted) return false;
            nameDecision = mtdDecisionPrompt.Value;
        }

        switch (nameDecision)
        {
            case MatNameDecision.UseNewMTD:
                foreach (FLVER2.Material material in allMaterials)
                {
                    material.MTD = replaceMatDef.MTD;
                }

                break;
            case MatNameDecision.KeepOldMTD:
                break;
            case MatNameDecision.CustomName:
                var mtdInput = PromptPlus
                    .Input("Input new material name for the mesh (.matxml will be appended)")
                    .Default(nameKeep)
                    .AcceptInput(_ => true)
                    .Run();
                if (mtdInput.IsAborted)
                {
                    PromptPlus.WriteLine("Aborted; defaulting to new MATBIN.");
                    foreach (FLVER2.Material material in allMaterials)
                    {
                        material.MTD = replaceMatDef.MTD;
                    }

                    break;
                }

                nameKeep = mtdInput.Value;
                foreach (FLVER2.Material material in allMaterials)
                {
                    material.MTD = $"{mtdInput.Value.Replace(".matxml", "")}.matxml";
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        mtdDecisionKeep = nameDecision;

        PromptPlus.WriteLine("Making changes from this point on, do not abort.");

// Handle GXList
        FLVER2.GXList gxList = new();
        gxList.AddRange(tarMatInfoBank.GetDefaultGXItemsForMTD(replaceMatDef.MTD).ToList());


        foreach (FLVER2 flver in flvers.Values)
        {
            FLVER2.GXList? existingGxList = null;
            existingGxList = flver.GXLists.FirstOrDefault(l =>
            {
                if (l.TerminatorID != gxList.TerminatorID) return false;
                if (l.TerminatorLength != gxList.TerminatorLength) return false;
                return l.Count == gxList.Count && l.All(i =>
                {
                    var j = gxList[l.IndexOf(i)];
                    return i.ID == j.ID && i.Data.SequenceEqual(j.Data) &&
                           i.Unk04 == j.Unk04;
                });
            });

            if (existingGxList != null)
            {
                // PromptPlus.WriteLine("Existing GXList matches, not changing.");
                gxList = existingGxList;
            }
            else
            {
                // PromptPlus.WriteLine("Adding new GXList.");
                flver.GXLists.Add(gxList);
            }

            foreach (FLVER2.Material material in groupedMaterials[flver])
            {
                material.GXIndex = flver.GXLists.IndexOf(gxList);

                material.Textures =
                    replaceMatDef.TextureChannels.Values.Select(x => new FLVER2.Texture { ParamName = x }).ToList();
            }
        }

        var acceptableBufferDeclarations = replaceMatDef.AcceptableVertexBufferDeclarations;

        var uniqueBufferMeshGroups = allMeshes
            .GroupBy(m =>
            {
                var l = m.VertexBuffers;
                return string.Join("-", l.Select(b => b.LayoutIndex));
            }).ToList();
        if (uniqueBufferMeshGroups.Count > 1)
        {
            PromptPlus.WriteLine($"Found {uniqueBufferMeshGroups.Count} unique vertex buffers");
        }

        foreach (IGrouping<string, FLVER2.Mesh> group in uniqueBufferMeshGroups)
        {
            var exampleFlver = flvers.Values.First(f => f.Meshes.Contains(group.First(m => m.Vertices.Count > 0)));
            var exampleMesh = groupedMeshes[exampleFlver].First(m => m.Vertices.Count > 0);
            var buffers = exampleMesh.VertexBuffers;
            var layouts = buffers.Select(b => exampleFlver.BufferLayouts[b.LayoutIndex]).ToList();
            var members = layouts.SelectMany(a => a).ToList();
            var groupIsSkinned = group.Any(m => m.UseBoneWeights);

            FLVER2MaterialInfoBank.VertexBufferDeclaration declaration = acceptableBufferDeclarations.Where(d =>
            {
                var skinCompatible = d.Buffers.Any(b => b.Any(m => m.Semantic == FLVER.LayoutSemantic.BoneWeights));
                return groupIsSkinned ? skinCompatible : !skinCompatible;
            }).OrderBy(d =>
            {
                var accMembers = d.Buffers.SelectMany(b => b).ToList();
                var origGroups = members.GroupBy(a => $"{a.Semantic}-{a.Type}");
                var accGroups = accMembers.GroupBy(a => $"{a.Semantic}-{a.Type}");
                var matches = origGroups.Where(g =>
                {
                    return accGroups.FirstOrDefault(h => g.Key == h.Key && h.Count() >= g.Count()) != null;
                });
                return matches.Count();
            }).First();

            // Handle data mismatches
            if (!auto)
            {
                var exampleVertex = exampleMesh.Vertices.First();
                foreach (FLVER.LayoutSemantic semantic in new[]
                         {
                             FLVER.LayoutSemantic.Position, FLVER.LayoutSemantic.Normal, FLVER.LayoutSemantic.UV,
                             FLVER.LayoutSemantic.Tangent, FLVER.LayoutSemantic.VertexColor
                         })
                {
                    int targetCount = declaration.Buffers.SelectMany(b => b).Where(c => c.Semantic == semantic)
                        .Select(d =>
                        {
                            bool isDouble = d is
                            {
                                Semantic: FLVER.LayoutSemantic.UV,
                                Type: FLVER.LayoutType.Float4 or
                                FLVER.LayoutType.Short4 or
                                FLVER.LayoutType.Half2 or
                                FLVER.LayoutType.UByte4Norm
                            };
                            return isDouble ? 2 : 1;
                        }).Sum();
                    var currentCount = semantic switch
                    {
                        FLVER.LayoutSemantic.Position => exampleVertex.Positions.Count,
                        FLVER.LayoutSemantic.Normal => exampleVertex.Normals.Count,
                        FLVER.LayoutSemantic.UV => exampleVertex.UVs.Count,
                        FLVER.LayoutSemantic.Tangent => exampleVertex.Tangents.Count,
                        FLVER.LayoutSemantic.VertexColor => exampleVertex.Colors.Count,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    if (targetCount > currentCount)
                    {
                        PromptPlus.WriteLine(
                            $"WARNING: Target needs {targetCount} {semantic.ToString()}, current material has only {currentCount}. Expect issues.");
                    }
                    else if (targetCount < currentCount)
                    {
                        PromptPlus.WriteLine(
                            $"DATA LOSS WARNING: {semantic.ToString()} count mismatch: Current material has {currentCount}, target material only {targetCount}.");
                        PromptPlus.WriteLine(
                            $"Please choose how to reallocate current {semantic.ToString()}s.");
                        var currentChoices = new List<int>();
                        for (int currentI = 0; currentI < currentCount; currentI++)
                        {
                            currentChoices.Add(currentI);
                        }

                        var targets = new List<int>();
                        for (int targetI = 0; targetI < targetCount; targetI++)
                        {
                            var currentSelect = PromptPlus
                                .Select<int>(
                                    $"Select current {semantic.ToString()} to place at ID #{targetI} ({targetI + 1}/{targetCount})")
                                .AddItems(currentChoices)
                                .TextSelector(c => $"#{c}")
                                .Config(o => o.EnabledAbortKey(false));

                            if (currentChoices.Contains(targetI))
                            {
                                currentSelect.Default(targetI);
                            }

                            var selectRun = currentSelect.Run();
                            targets.Add(selectRun.Value);
                        }

                        PromptPlus.WriteLine("Applying new UV setup to all affected vertices...");
                        foreach (FLVER2.Mesh mesh in group)
                        {
                            foreach (FLVER.Vertex vertex in mesh.Vertices)
                            {
                                switch (semantic)
                                {
                                    case FLVER.LayoutSemantic.Position:
                                        vertex.Positions = targets.Select(u => vertex.Positions[u]).ToList();
                                        break;
                                    case FLVER.LayoutSemantic.Normal:
                                        vertex.Normals = targets.Select(u => vertex.Normals[u]).ToList();
                                        break;
                                    case FLVER.LayoutSemantic.UV:
                                        vertex.UVs = targets.Select(u => vertex.UVs[u]).ToList();
                                        break;
                                    case FLVER.LayoutSemantic.Tangent:
                                        vertex.Tangents = targets.Select(u => vertex.Tangents[u]).ToList();
                                        break;
                                    case FLVER.LayoutSemantic.VertexColor:
                                        vertex.Colors = targets.Select(u => vertex.Colors[u]).ToList();
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }
                }
            }

            foreach (FLVER2 flver in flvers.Values)
            {
                List<int> layoutIndices = FlverUtils.GetLayoutIndices(flver, declaration.Buffers);
                foreach (FLVER2.Mesh mesh in group)
                {
                    mesh.VertexBuffers = layoutIndices.Select(x => new FLVER2.VertexBuffer(x)).ToList();
                    foreach (var v in mesh.Vertices)
                    {
                        FlverUtils.PadVertex(v, declaration.Buffers);
                    }
                }

                FlverUtils.AdjustBoneIndexBufferSize(flver, declaration.Buffers);
            }
        }

        if (!auto)
        {
            PromptPlus.WriteLine("Operation complete.");
            Program.SaveFlvers(flvers, true);
        }

        return true;
    }
}