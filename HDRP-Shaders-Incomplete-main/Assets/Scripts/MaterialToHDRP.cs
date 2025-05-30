#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class MaterialToHDRP : MonoBehaviour
{

    [MenuItem("Tools/Convert selected Materials to HDRP...", priority = 0)]
    private static void upgradeSelected()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Material mInstance = Instantiate(AssetDatabase.LoadAssetAtPath<Material>(assetPath));
            mInstance.name = m.name;
            if (convert(mInstance))
                EditorUtility.CopySerialized(mInstance, m); //Makes sure we keep the original GUID      
        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Convert All Materials to HDRP...", priority = CoreUtils.Priorities.editMenuPriority + 1)]
    internal static void UpgradeMaterialsProject()
    {
        MaterialUpgrader.UpgradeProjectFolder(GetHDUpgraders(), "Upgrade to HDRP Material");
    }
    public static List<MaterialUpgrader> GetHDUpgraders()
    {
        var upgraders = new List<MaterialUpgrader>();
        upgraders.Add(new StandardsToHDLitMaterialUpgrader("Autodesk Interactive", "HDRP/Lit"));
        upgraders.Add(new StandardsToHDLitMaterialUpgrader("Standard", "HDRP/Lit"));
        upgraders.Add(new StandardsToHDLitMaterialUpgrader("Standard (Specular setup)", "HDRP/Lit"));
        upgraders.Add(new StandardsToHDLitMaterialUpgrader("Standard (Roughness setup)", "HDRP/Lit"));

        return upgraders;
    }

    private static bool convert(Material m)
    {
        string shaderName = m.shader.name;
        if (shaderName.Equals("Autodesk Interactive", StringComparison.OrdinalIgnoreCase))
        {
            //Read
            Texture albedo = m.GetTexture("_MainTex");
            Texture metallic = m.GetTexture("_MetallicGlossMap");
            Texture roughness = m.GetTexture("_SpecGlossMap");
            Texture normal = m.GetTexture("_BumpMap");
            float bumpScale = m.GetFloat("_BumpScale");
            Vector2 offset = m.mainTextureOffset;
            Vector2 tiling = m.mainTextureScale;

            //Convert
            m.shader = Shader.Find("HDRP/Lit");
            m.SetTexture("_BaseColorMap", albedo);
            m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalScale", bumpScale);
            m.mainTextureOffset = offset;
            m.mainTextureScale = tiling;

            return true;
        }
        return false;
    }
}
namespace UnityEditor.Rendering
{
    public class StandardsToHDLitMaterialUpgrader : MaterialUpgrader
    {
        static readonly string Standard = "Standard";
        static readonly string Standard_Spec = "Standard (Specular setup)";
        static readonly string Standard_Rough = "Autodesk Interactive";

        public StandardsToHDLitMaterialUpgrader(string sourceShaderName, string destShaderName, MaterialFinalizer finalizer = null)
        {
            RenameShader(sourceShaderName, destShaderName, finalizer);

            RenameTexture("_MainTex", "_BaseColorMap");
            RenameColor("_Color", "_BaseColor");
            RenameFloat("_Glossiness", "_Smoothness");
            RenameTexture("_BumpMap", "_NormalMap");
            RenameFloat("_BumpScale", "_NormalScale");
            RenameTexture("_ParallaxMap", "_HeightMap");
            RenameTexture("_EmissionMap", "_EmissiveColorMap");
            RenameTexture("_DetailAlbedoMap", "_DetailMap");
            RenameFloat("_UVSec", "_UVDetail");
            SetFloat("_LinkDetailsWithBase", 0);
            RenameFloat("_DetailNormalMapScale", "_DetailNormalScale");
            RenameFloat("_Cutoff", "_AlphaCutoff");
            RenameKeywordToFloat("_ALPHATEST_ON", "_AlphaCutoffEnable", 1f, 0f);


            if (sourceShaderName == Standard)
            {
                SetFloat("_MaterialID", 1f);
            }

            if (sourceShaderName == Standard_Spec)
            {
                SetFloat("_MaterialID", 4f);

                RenameColor("_SpecColor", "_SpecularColor");
                RenameTexture("_SpecGlossMap", "_SpecularColorMap");
            }
        }

        public override void Convert(Material srcMaterial, Material dstMaterial)
        {
            dstMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

            base.Convert(srcMaterial, dstMaterial);

            // ---------- Mask Map ----------

            // Metallic
            bool hasMetallic = false;
            Texture metallicMap = TextureCombiner.TextureFromColor(Color.black);
            if ((srcMaterial.shader.name == Standard) || (srcMaterial.shader.name == Standard_Rough))
            {
                hasMetallic = srcMaterial.GetTexture("_MetallicGlossMap") != null;
                if (hasMetallic)
                {
                    metallicMap = TextureCombiner.GetTextureSafe(srcMaterial, "_MetallicGlossMap", Color.white);
                }
                else
                {
                    metallicMap = TextureCombiner.TextureFromColor(Color.white);
                }

                // Convert _Metallic value from Gamma to Linear, or set to 1 if a map is used
                float metallicValue = Mathf.Pow(srcMaterial.GetFloat("_Metallic"), 2.2f);
                dstMaterial.SetFloat("_Metallic", hasMetallic ? 1f : metallicValue);
            }

            // Occlusion
            bool hasOcclusion = srcMaterial.GetTexture("_OcclusionMap") != null;
            Texture occlusionMap = Texture2D.whiteTexture;
            if (hasOcclusion) occlusionMap = TextureCombiner.GetTextureSafe(srcMaterial, "_OcclusionMap", Color.white);

            dstMaterial.SetFloat("_AORemapMin", 1f - srcMaterial.GetFloat("_OcclusionStrength"));

            // Detail Mask
            bool hasDetailMask = srcMaterial.GetTexture("_DetailMask") != null;
            Texture detailMaskMap = Texture2D.whiteTexture;
            if (hasDetailMask) detailMaskMap = TextureCombiner.GetTextureSafe(srcMaterial, "_DetailMask", Color.white);

            // Smoothness
            bool hasSmoothness = false;
            Texture2D smoothnessMap = TextureCombiner.TextureFromColor(Color.white);

            dstMaterial.SetFloat("_SmoothnessRemapMax", srcMaterial.GetFloat("_Glossiness"));

            if (srcMaterial.shader.name == Standard_Rough)
            {
                hasSmoothness = srcMaterial.GetTexture("_SpecGlossMap") != null;

                if (hasSmoothness)
                    smoothnessMap = (Texture2D)TextureCombiner.GetTextureSafe(srcMaterial, "_SpecGlossMap", Color.grey);
            }
            else
            {
                string smoothnessTextureChannel = "_MainTex";

                if (srcMaterial.GetFloat("_SmoothnessTextureChannel") == 0)
                {
                    if (srcMaterial.shader.name == Standard) smoothnessTextureChannel = "_MetallicGlossMap";
                    if (srcMaterial.shader.name == Standard_Spec) smoothnessTextureChannel = "_SpecGlossMap";
                }

                smoothnessMap = (Texture2D)srcMaterial.GetTexture(smoothnessTextureChannel);
                if (smoothnessMap != null)
                {
                    hasSmoothness = true;

                    dstMaterial.SetFloat("_SmoothnessRemapMax", srcMaterial.GetFloat("_GlossMapScale"));

                    if (!TextureCombiner.TextureHasAlpha(smoothnessMap))
                    {
                        smoothnessMap = TextureCombiner.TextureFromColor(Color.white);
                    }
                }
                else
                {
                    smoothnessMap = TextureCombiner.TextureFromColor(Color.white);
                }
            }


            // Build the mask map
            if (hasMetallic || hasOcclusion || hasDetailMask || hasSmoothness)
            {
                Texture2D maskMap;

                TextureCombiner maskMapCombiner = new TextureCombiner(
                    metallicMap, 0,                                                         // R: Metallic from red
                    occlusionMap, 1,                                                        // G: Occlusion from green
                    detailMaskMap, 3,                                                       // B: Detail Mask from alpha
                    smoothnessMap, (srcMaterial.shader.name == Standard_Rough) ? -4 : 3     // A: Smoothness Texture from inverse greyscale for roughness setup, or alpha
                );

                string maskMapPath = AssetDatabase.GetAssetPath(srcMaterial);
                maskMapPath = maskMapPath.Remove(maskMapPath.Length - 4) + "_MaskMap.png";
                maskMap = maskMapCombiner.Combine(maskMapPath);
                dstMaterial.SetTexture("_MaskMap", maskMap);
            }

            // Specular Setup Specific
            if (srcMaterial.shader.name == Standard_Spec)
            {
                // if there is a specular map, change the specular color to white
                if (srcMaterial.GetTexture("_SpecGlossMap") != null) dstMaterial.SetColor("_SpecularColor", Color.white);
            }

            // ---------- Height Map ----------
            bool hasHeightMap = srcMaterial.GetTexture("_ParallaxMap") != null;
            if (hasHeightMap) // Enable Parallax Occlusion Mapping
            {
                dstMaterial.SetFloat("_DisplacementMode", 2);
                dstMaterial.SetFloat("_HeightPoMAmplitude", srcMaterial.GetFloat("_Parallax") * 2f);
            }

            // ---------- Detail Map ----------
            bool hasDetailAlbedo = srcMaterial.GetTexture("_DetailAlbedoMap") != null;
            bool hasDetailNormal = srcMaterial.GetTexture("_DetailNormalMap") != null;
            if (hasDetailAlbedo || hasDetailNormal)
            {
                Texture2D detailMap;
                TextureCombiner detailCombiner = new TextureCombiner(
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailAlbedoMap", Color.grey), 4,     // Albedo (overlay)
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailNormalMap", Color.grey), 1,     // Normal Y
                    TextureCombiner.midGrey, 1,                                                         // Smoothness
                    TextureCombiner.GetTextureSafe(srcMaterial, "_DetailNormalMap", Color.grey), 0      // Normal X
                );
                string detailMapPath = AssetDatabase.GetAssetPath(srcMaterial);
                detailMapPath = detailMapPath.Remove(detailMapPath.Length - 4) + "_DetailMap.png";
                detailMap = detailCombiner.Combine(detailMapPath);
                dstMaterial.SetTexture("_DetailMap", detailMap);
            }


            // Blend Mode
            int previousBlendMode = srcMaterial.GetInt("_Mode");
            switch (previousBlendMode)
            {
                case 0: // Opaque
                    dstMaterial.SetFloat("_SurfaceType", 0);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 0);
                    dstMaterial.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
                    //dstMaterial.renderQueue = HDRenderQueue.ChangeType(HDRenderQueue.RenderQueueType.Opaque, 0, false, true);
                    dstMaterial.renderQueue = (int)RenderQueue.Geometry;
                    break;
                case 1: // Cutout
                    dstMaterial.SetFloat("_SurfaceType", 0);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 1);
                    dstMaterial.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
                    //dstMaterial.renderQueue = HDRenderQueue.ChangeType(HDRenderQueue.RenderQueueType.Opaque, 0, true, true);
                    dstMaterial.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case 2: // Fade -> Alpha with depth prepass + Disable preserve specular
                    dstMaterial.SetFloat("_SurfaceType", 1);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 0);
                    dstMaterial.SetFloat("_EnableBlendModePreserveSpecularLighting", 0);
                    dstMaterial.SetFloat("_TransparentDepthPrepassEnable", 1);
                    //dstMaterial.renderQueue = HDRenderQueue.ChangeType(HDRenderQueue.RenderQueueType.Transparent, 0, false, true);
                    dstMaterial.renderQueue = (int)RenderQueue.Transparent;
                    break;
                case 3: // Transparent -> Alpha
                    dstMaterial.SetFloat("_SurfaceType", 1);
                    dstMaterial.SetFloat("_BlendMode", 0);
                    dstMaterial.SetFloat("_AlphaCutoffEnable", 0);
                    dstMaterial.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
                    //dstMaterial.renderQueue = HDRenderQueue.ChangeType(HDRenderQueue.RenderQueueType.Transparent, 0, false, true);
                    dstMaterial.renderQueue = (int)RenderQueue.Transparent;
                    break;
            }

            Color hdrEmission = srcMaterial.GetColor("_EmissionColor");

            // Get the _EMISSION keyword of the Standard shader
            if (!srcMaterial.IsKeywordEnabled("_EMISSION"))
                hdrEmission = Color.black;

            // Emission toggle of Particle Standard Surface
            if (srcMaterial.HasProperty("_EmissionEnabled"))
                if (srcMaterial.GetFloat("_EmissionEnabled") == 0)
                    hdrEmission = Color.black;

            dstMaterial.SetColor("_EmissiveColor", hdrEmission);

            HDShaderUtils.ResetMaterialKeywords(dstMaterial);
        }
    }

    internal class TextureCombiner
    {
        static Texture2D _midGrey;

        /// <summary>
        /// Returns a 1 by 1 mid grey (0.5, 0.5, 0.5, 1) Texture.
        /// </summary>
        public static Texture2D midGrey
        {
            get
            {
                if (_midGrey == null)
                    _midGrey = TextureFromColor(Color.grey);

                return _midGrey;
            }
        }

        private static Dictionary<Color, Texture2D> singleColorTextures = new Dictionary<Color, Texture2D>();

        /// <summary>
        /// Returns a 1 by 1 Texture that is the color that you pass in.
        /// </summary>
        /// <param name="color">The color that Unity uses to create the Texture.</param>
        /// <returns></returns>
        public static Texture2D TextureFromColor(Color color)
        {
            if (color == Color.white) return Texture2D.whiteTexture;
            if (color == Color.black) return Texture2D.blackTexture;

            bool makeTexture = !singleColorTextures.ContainsKey(color);
            if (!makeTexture)
                makeTexture = (singleColorTextures[color] == null);

            if (makeTexture)
            {
                Texture2D tex = new Texture2D(1, 1, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
                tex.SetPixel(0, 0, color);
                tex.Apply();

                singleColorTextures[color] = tex;
            }

            return singleColorTextures[color];
        }

        /// <summary>
        /// Returns the Texture assigned to the property "propertyName" of "srcMaterial".
        /// If no matching property is found, or no Texture is assigned, returns a 1 by 1 Texture of "fallback" color.
        /// </summary>
        /// <param name="srcMaterial">The Material to get the Texture from.</param>
        /// <param name="propertyName">The name of the Texture property.</param>
        /// <param name="fallback">The fallback color that Unity uses to create a Texture if it could not find the Texture property on the Material.</param>
        /// <returns></returns>
        public static Texture GetTextureSafe(Material srcMaterial, string propertyName, Color fallback)
        {
            return GetTextureSafe(srcMaterial, propertyName, TextureFromColor(fallback));
        }

        /// <summary>
        /// Returns the Texture assigned to the property "propertyName" of "srcMaterial".
        /// If no matching property is found, or no Texture is assigned, returns the "fallback" Texture.
        /// </summary>
        /// <param name="srcMaterial">The Material to get the Texture from.</param>
        /// <param name="propertyName">The name of the Texture property.</param>
        /// <param name="fallback">The fallback color that Unity uses to create a Texture if it could not find the Texture property on the Material.</param>
        /// <returns></returns>
        public static Texture GetTextureSafe(Material srcMaterial, string propertyName, Texture fallback)
        {
            if (!srcMaterial.HasProperty(propertyName))
                return fallback;

            Texture tex = srcMaterial.GetTexture(propertyName);
            if (tex == null)
                return fallback;
            else
                return tex;
        }

        /// <summary>
        /// Specifies whether the Texture has an alpha channel or not. Returns true if it does and false otherwise.
        /// </summary>
        /// <param name="tex">The Texture for this function to check.</param>
        /// <returns></returns>
        public static bool TextureHasAlpha(Texture2D tex)
        {
            if (tex == null) return false;

            return GraphicsFormatUtility.HasAlphaChannel(tex.graphicsFormat);
        }

        private Texture m_rSource;
        private Texture m_gSource;
        private Texture m_bSource;
        private Texture m_aSource;

        // Chanels are : r=0, g=1, b=2, a=3, greyscale from rgb = 4
        // If negative, the chanel is inverted
        private int m_rChanel;
        private int m_gChanel;
        private int m_bChanel;
        private int m_aChanel;

        // Chanels remaping
        private Vector4[] m_remapings =
        {
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(0f, 1f, 0f, 0f)
        };

        private bool m_bilinearFilter;

        private Dictionary<Texture, Texture> m_RawTextures;

        /// <summary>
        /// Creates a TextureCombiner object.
        /// </summary>
        /// <param name="rSource">Source Texture for the RED output.</param>
        /// <param name="rChanel">Channel index to use for the RED output.</param>
        /// <param name="gSource">Source Texture for the GREEN output.</param>
        /// <param name="gChanel">Channel index to use for the GREEN output.</param>
        /// <param name="bSource">Source Texture for the BLUE output.</param>
        /// <param name="bChanel">Channel index to use for the BLUE output.</param>
        /// <param name="aSource">Source Texture for the ALPHA output.</param>
        /// <param name="aChanel">Channel index to use for the ALPHA output.</param>
        /// <param name="bilinearFilter">Use bilinear filtering when combining (default = true).</param>
        public TextureCombiner(Texture rSource, int rChanel, Texture gSource, int gChanel, Texture bSource, int bChanel, Texture aSource, int aChanel, bool bilinearFilter = true)
        {
            m_rSource = rSource;
            m_gSource = gSource;
            m_bSource = bSource;
            m_aSource = aSource;
            m_rChanel = rChanel;
            m_gChanel = gChanel;
            m_bChanel = bChanel;
            m_aChanel = aChanel;
            m_bilinearFilter = bilinearFilter;
        }

        /// <summary>
        /// Set the remapping of a specific color channel.
        /// </summary>
        /// <param name="channel">Target color channel (Red:0, Green:1, Blue:2, Alpha:3).</param>
        /// <param name="min">Minimum input value mapped to 0 in output.</param>
        /// <param name="max">Maximum input value mapped to 1 in output.</param>
        public void SetRemapping(int channel, float min, float max)
        {
            if (channel > 3 || channel < 0) return;

            m_remapings[channel].x = min;
            m_remapings[channel].y = max;
        }

        /// <summary>
        /// Process the TextureCombiner.
        /// Unity creates the Texture Asset at the "savePath", and returns the Texture object.
        /// </summary>
        /// <param name="savePath">The path to save the Texture Asset to, relative to the Project folder.</param>
        /// <returns></returns>
        public Texture2D Combine(string savePath)
        {
            int xMin = int.MaxValue;
            int yMin = int.MaxValue;

            if (m_rSource.width > 4 && m_rSource.width < xMin) xMin = m_rSource.width;
            if (m_gSource.width > 4 && m_gSource.width < xMin) xMin = m_gSource.width;
            if (m_bSource.width > 4 && m_bSource.width < xMin) xMin = m_bSource.width;
            if (m_aSource.width > 4 && m_aSource.width < xMin) xMin = m_aSource.width;
            if (xMin == int.MaxValue) xMin = 4;

            if (m_rSource.height > 4 && m_rSource.height < yMin) yMin = m_rSource.height;
            if (m_gSource.height > 4 && m_gSource.height < yMin) yMin = m_gSource.height;
            if (m_bSource.height > 4 && m_bSource.height < yMin) yMin = m_bSource.height;
            if (m_aSource.height > 4 && m_aSource.height < yMin) yMin = m_aSource.height;
            if (yMin == int.MaxValue) yMin = 4;

            Texture2D combined = new Texture2D(xMin, yMin, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.MipChain);
            combined.hideFlags = HideFlags.DontUnloadUnusedAsset;

            Material combinerMaterial = new Material(Shader.Find("Hidden/SRP_Core/TextureCombiner"));
            combinerMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

            combinerMaterial.SetTexture("_RSource", GetRawTexture(m_rSource));
            combinerMaterial.SetTexture("_GSource", GetRawTexture(m_gSource));
            combinerMaterial.SetTexture("_BSource", GetRawTexture(m_bSource));
            combinerMaterial.SetTexture("_ASource", GetRawTexture(m_aSource));

            combinerMaterial.SetFloat("_RChannel", m_rChanel);
            combinerMaterial.SetFloat("_GChannel", m_gChanel);
            combinerMaterial.SetFloat("_BChannel", m_bChanel);
            combinerMaterial.SetFloat("_AChannel", m_aChanel);

            combinerMaterial.SetVector("_RRemap", m_remapings[0]);
            combinerMaterial.SetVector("_GRemap", m_remapings[1]);
            combinerMaterial.SetVector("_BRemap", m_remapings[2]);
            combinerMaterial.SetVector("_ARemap", m_remapings[3]);

            RenderTexture combinedRT = new RenderTexture(xMin, yMin, 0, GraphicsFormat.R32G32B32A32_SFloat);

            Graphics.Blit(Texture2D.whiteTexture, combinedRT, combinerMaterial);

            // Readback the render texture
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = combinedRT;
            combined.ReadPixels(new Rect(0, 0, xMin, yMin), 0, 0, false);
            combined.Apply();
            RenderTexture.active = previousActive;

            byte[] bytes = new byte[0];

            if (savePath.EndsWith("png"))
                bytes = ImageConversion.EncodeToPNG(combined);
            if (savePath.EndsWith("exr"))
                bytes = ImageConversion.EncodeToEXR(combined);
            if (savePath.EndsWith("jpg"))
                bytes = ImageConversion.EncodeToJPG(combined);

            string systemPath = Path.Combine(Application.dataPath.Remove(Application.dataPath.Length - 6), savePath);
            File.WriteAllBytes(systemPath, bytes);

            UnityEngine.Object.DestroyImmediate(combined);

            AssetDatabase.ImportAsset(savePath);

            TextureImporter combinedImporter = (TextureImporter)AssetImporter.GetAtPath(savePath);
            combinedImporter.sRGBTexture = false;
            combinedImporter.SaveAndReimport();

            if (savePath.EndsWith("exr"))
            {
                // The options for the platform string are: "Standalone", "iPhone", "Android", "WebGL", "Windows Store Apps", "PSP2", "PS4", "XboxOne", "Nintendo 3DS", "WiiU", "tvOS".
                combinedImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings() { name = "Standalone", format = TextureImporterFormat.DXT5, overridden = true });
            }

            combined = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

            //cleanup "raw" textures
            foreach (KeyValuePair<Texture, Texture> prop in m_RawTextures)
            {
                if (prop.Key != prop.Value && AssetDatabase.Contains(prop.Value))
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(prop.Value));
            }

            UnityEngine.Object.DestroyImmediate(combinerMaterial);

            m_RawTextures.Clear();

            return combined;
        }

        private Texture GetRawTexture(Texture original, bool sRGBFallback = false)
        {
            if (m_RawTextures == null) m_RawTextures = new Dictionary<Texture, Texture>();
            if (!m_RawTextures.ContainsKey(original))
            {
                string path = AssetDatabase.GetAssetPath(original);
                string rawPath = "Assets/raw_" + Path.GetFileName(path);
                bool isBuiltinResource = path.Contains("unity_builtin");

                if (!isBuiltinResource && AssetDatabase.Contains(original) && AssetDatabase.CopyAsset(path, rawPath))
                {
                    AssetDatabase.ImportAsset(rawPath);

                    TextureImporter rawImporter = (TextureImporter)AssetImporter.GetAtPath(rawPath);
                    rawImporter.textureType = TextureImporterType.Default;
                    rawImporter.mipmapEnabled = false;
                    rawImporter.isReadable = true;
                    rawImporter.filterMode = m_bilinearFilter ? FilterMode.Bilinear : FilterMode.Point;
                    rawImporter.npotScale = TextureImporterNPOTScale.None;
                    rawImporter.wrapMode = TextureWrapMode.Clamp;

                    Texture2D originalTex2D = original as Texture2D;
                    rawImporter.sRGBTexture = (originalTex2D == null) ? sRGBFallback : (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original)) as TextureImporter).sRGBTexture;

                    rawImporter.maxTextureSize = 8192;

                    rawImporter.textureCompression = TextureImporterCompression.Uncompressed;

                    rawImporter.SaveAndReimport();

                    m_RawTextures.Add(original, AssetDatabase.LoadAssetAtPath<Texture>(rawPath));
                }
                else
                    m_RawTextures.Add(original, original);
            }

            return m_RawTextures[original];
        }
    }
}
#endif