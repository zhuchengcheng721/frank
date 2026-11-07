using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ReferenceRange.Editor
{
    internal static partial class ReferenceModelBuilder
    {
        private const string ScanFolder = Root + "/Models/ScannedOperator/";
        private const string ScanMeshPath = ScanFolder + "EricFirstPersonArms.asset";

        private readonly struct JointFit
        {
            public readonly int TargetIndex;
            public readonly Vector3 From;
            public readonly Vector3 To;
            public readonly Quaternion SourceFrame;
            public readonly Quaternion TargetFrame;
            public readonly Vector3 Scale;

            public JointFit(int targetIndex, Vector3 from, Vector3 to, Quaternion sourceFrame,
                Quaternion targetFrame, float radius, float length)
            {
                TargetIndex = targetIndex;
                From = from;
                To = to;
                SourceFrame = sourceFrame;
                TargetFrame = targetFrame;
                Scale = new Vector3(radius, radius, length);
            }

            public Vector3 Point(Vector3 position) =>
                To + TargetFrame * Vector3.Scale(Quaternion.Inverse(SourceFrame) * (position - From), Scale);

            public Vector3 Normal(Vector3 normal) =>
                TargetFrame * (Quaternion.Inverse(SourceFrame) * normal);
        }

        private static void ReplaceOperatorArms(SkinnedMeshRenderer target)
        {
            string path = ScanFolder + "Eric.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                return;

            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            var scan = Object.Instantiate(asset);
            var baked = new Mesh();
            try
            {
                var source = scan.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.bones.Any(bone => bone != null && bone.name == "hand_l"))
                    .OrderByDescending(renderer => renderer.sharedMesh.vertexCount)
                    .First();
                source.BakeMesh(baked);

                Transform[] driverBones = target.bones;
                var sourceBones = source.bones.Select((bone, index) => (bone, index))
                    .Where(item => item.bone != null)
                    .ToDictionary(item => item.bone.name, item => item.index);
                var fits = new Dictionary<int, JointFit>();
                FitArm("l", source, sourceBones, driverBones, fits);
                FitArm("r", source, sourceBones, driverBones, fits);

                var mesh = TransferArms(source, baked, target, driverBones, fits);
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(ScanMeshPath);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(mesh, ScanMeshPath);
                    saved = mesh;
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, saved);
                    Object.DestroyImmediate(mesh);
                }

                target.sharedMesh = saved;
                target.sharedMaterials = new[] { ScannedSkinMaterial() };
                target.localBounds = saved.bounds;
                Debug.Log("Scanned first-person arms bound to " + fits.Count + " arm and finger influences.");
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(scan);
            }
        }

        private static void FitArm(string side, SkinnedMeshRenderer source, Dictionary<string, int> sourceBones,
            Transform[] driver, Dictionary<int, JointFit> fits)
        {
            string suffix = "_" + side;
            string prefix = side.ToUpperInvariant() + "_";
            int Source(string name) => sourceBones[name + suffix];
            int Target(string name)
            {
                int index = Array.FindIndex(driver, bone => bone != null &&
                    bone.name.StartsWith(prefix + name + "_", StringComparison.Ordinal));
                if (index < 0)
                    throw new InvalidOperationException("Missing first-person bone " + prefix + name);
                return index;
            }
            Vector3 SourceAt(string name) => source.bones[Source(name)].position;
            Vector3 TargetAt(string name) => driver[Target(name)].position;

            Vector3 sourceWidth = SourceAt("index_01") - SourceAt("pinky_01");
            Vector3 targetWidth = TargetAt("point1") - TargetAt("pink1");
            float widthRatio = targetWidth.magnitude / sourceWidth.magnitude;
            float thickness = widthRatio * .88f;

            void Fit(string from, string to, Vector3 sourceAlong, Vector3 targetAlong, float radius)
            {
                int index = Target(to);
                var fit = new JointFit(index, SourceAt(from), driver[index].position,
                    JointFrame(sourceAlong, sourceWidth), JointFrame(targetAlong, targetWidth),
                    radius, targetAlong.magnitude / sourceAlong.magnitude);
                fits[Source(from)] = fit;
            }

            Vector3 sourceUpper = SourceAt("lowerarm") - SourceAt("upperarm");
            Vector3 targetUpper = TargetAt("elbow") - TargetAt("arm");
            Vector3 sourceLower = SourceAt("hand") - SourceAt("lowerarm");
            Vector3 targetLower = TargetAt("wrist") - TargetAt("elbow");
            Fit("upperarm", "arm", sourceUpper, targetUpper, thickness);
            Fit("lowerarm", "elbow", sourceLower, targetLower, thickness);
            Fit("hand", "wrist", SourceAt("middle_01") - SourceAt("hand"),
                TargetAt("middle1") - TargetAt("wrist"), thickness);

            foreach (string joint in new[] { "upperarm", "lowerarm" })
            {
                if (sourceBones.TryGetValue(joint + "_twist" + suffix, out int twist))
                    fits[twist] = fits[Source(joint)];
            }

            foreach (var digit in new[] { ("thumb", "thumb"), ("index", "point"), ("middle", "middle"),
                         ("ring", "ring"), ("pinky", "pink") })
            {
                for (int segment = 1; segment <= 3; segment++)
                {
                    string from = digit.Item1 + "_0" + segment;
                    string to = digit.Item2 + segment;
                    int neighbour = segment < 3 ? segment + 1 : segment - 1;
                    Vector3 a = SourceAt(digit.Item1 + "_0" + neighbour) - SourceAt(from);
                    Vector3 b = TargetAt(digit.Item2 + neighbour) - TargetAt(to);
                    Fit(from, to, segment < 3 ? a : -a, segment < 3 ? b : -b, thickness);
                }
                if (sourceBones.TryGetValue(digit.Item1 + "_end" + suffix, out int tip))
                    fits[tip] = fits[Source(digit.Item1 + "_03")];
            }
        }

        private static Quaternion JointFrame(Vector3 along, Vector3 across)
        {
            Vector3 up = Vector3.ProjectOnPlane(across, along);
            if (up.sqrMagnitude < 0.0000001f)
                up = Vector3.ProjectOnPlane(Vector3.up, along);
            return Quaternion.LookRotation(along, up);
        }

        private static Mesh TransferArms(SkinnedMeshRenderer source, Mesh baked, SkinnedMeshRenderer target,
            Transform[] driver, Dictionary<int, JointFit> fits)
        {
            Vector3[] points = baked.vertices;
            Vector3[] normals = baked.normals;
            Vector2[] uv = baked.uv;
            var weights = source.sharedMesh.boneWeights;
            var vertices = new List<Vector3>();
            var fittedNormals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var fittedWeights = new List<BoneWeight>();
            var indexMap = new int[points.Length];
            Array.Fill(indexMap, -1);

            for (int i = 0; i < points.Length; i++)
            {
                BoneWeight skin = weights[i];
                var p = source.transform.TransformPoint(points[i]);
                var n = source.transform.TransformDirection(normals[i]);
                Vector3 fitted = Vector3.zero;
                Vector3 normal = Vector3.zero;
                float sum = 0f;
                var skinWeight = new BoneWeight();

                void Add(int bone, float value, int slot)
                {
                    if (value <= 0f || !fits.TryGetValue(bone, out JointFit fit))
                        return;
                    fitted += fit.Point(p) * value;
                    normal += fit.Normal(n) * value;
                    sum += value;
                    switch (slot)
                    {
                        case 0: skinWeight.boneIndex0 = fit.TargetIndex; skinWeight.weight0 = value; break;
                        case 1: skinWeight.boneIndex1 = fit.TargetIndex; skinWeight.weight1 = value; break;
                        case 2: skinWeight.boneIndex2 = fit.TargetIndex; skinWeight.weight2 = value; break;
                        default: skinWeight.boneIndex3 = fit.TargetIndex; skinWeight.weight3 = value; break;
                    }
                }

                Add(skin.boneIndex0, skin.weight0, 0);
                Add(skin.boneIndex1, skin.weight1, 1);
                Add(skin.boneIndex2, skin.weight2, 2);
                Add(skin.boneIndex3, skin.weight3, 3);
                if (sum < .78f)
                    continue;
                // Unity requires bone weights to be sorted by magnitude.
                var ordered = new[]
                {
                    (skinWeight.boneIndex0, skinWeight.weight0), (skinWeight.boneIndex1, skinWeight.weight1),
                    (skinWeight.boneIndex2, skinWeight.weight2), (skinWeight.boneIndex3, skinWeight.weight3)
                }.OrderByDescending(item => item.Item2).ToArray();
                indexMap[i] = vertices.Count;
                vertices.Add(target.transform.InverseTransformPoint(fitted / sum));
                fittedNormals.Add(target.transform.InverseTransformDirection(normal.normalized));
                uvs.Add(uv[i]);
                fittedWeights.Add(new BoneWeight
                {
                    boneIndex0 = ordered[0].Item1, weight0 = ordered[0].Item2 / sum,
                    boneIndex1 = ordered[1].Item1, weight1 = ordered[1].Item2 / sum,
                    boneIndex2 = ordered[2].Item1, weight2 = ordered[2].Item2 / sum,
                    boneIndex3 = ordered[3].Item1, weight3 = ordered[3].Item2 / sum
                });
            }

            int[] triangles = baked.triangles;
            var indices = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = indexMap[triangles[i]];
                int b = indexMap[triangles[i + 1]];
                int c = indexMap[triangles[i + 2]];
                if (a < 0 || b < 0 || c < 0)
                    continue;
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
            }
            if (indices.Count < 300)
                throw new InvalidOperationException("Too little of the scan is weighted to the first-person arms.");

            var mesh = new Mesh { name = "Eric photogrammetry hands and sleeves", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(fittedNormals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.boneWeights = fittedWeights.ToArray();
            mesh.bindposes = driver.Select(bone => bone.worldToLocalMatrix * target.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material ScannedSkinMaterial()
        {
            string path = ScanFolder + "Scanned skin and sleeve.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            string normal = ScanFolder + "eric_normal_4k.jpg";
            string surface = ScanFolder + "eric_metallic_smoothness_4k.png";
            material.SetTexture("_BaseMap", LoadScanTexture(ScanFolder + "eric_olive_albedo_4k.jpg", sRGB: true));
            material.SetTexture("_BumpMap", LoadScanTexture(normal, normal: true));
            material.SetTexture("_MetallicGlossMap", LoadScanTexture(surface, packedGloss: true));
            material.SetFloat("_Smoothness", .52f);
            material.SetFloat("_BumpScale", .62f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadScanTexture(string path, bool sRGB = false, bool normal = false, bool packedGloss = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.maxTextureSize != 4096 ||
                importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 4 ||
                (!normal && importer.sRGBTexture != sRGB) ||
                (packedGloss && (importer.alphaIsTransparency || importer.alphaSource != TextureImporterAlphaSource.FromInput)))
            {
                importer.textureType = type;
                if (!normal)
                    importer.sRGBTexture = sRGB;
                importer.maxTextureSize = 4096;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                if (packedGloss)
                {
                    importer.alphaIsTransparency = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                }
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
