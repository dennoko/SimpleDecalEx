#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    // SimpleDecalEx: lilToon のデカール仕様に準拠したデカールを 6 枚追加するカスタムシェーダー。
    // 名前の整合性（4 箇所）:
    //   1. この class 名 SimpleDecalExInspector
    //   2. 下の shaderName 定数 "dennokoworks/SimpleDecalEx"
    //   3. Shaders/lilCustomShaderDatas.lilblock の ShaderName / EditorName タグ
    //   4. Editor/SimpleDecalEx.asmdef の name フィールドとファイル名
    public class SimpleDecalExInspector : lilToonInspector
    {
        private const int DecalCount = 6;

        private readonly MaterialProperty[] decalEnable      = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalColor       = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalTex         = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalUVMode      = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalBlendMode   = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalCull        = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalAngle       = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalScrollRotate= new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalIsDecal     = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalLeftOnly    = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalRightOnly   = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalCopy        = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalFlipMirror  = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalFlipCopy    = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalMSDF        = new MaterialProperty[DecalCount];

        private static bool isShowCustomProperties;
        private static readonly bool[] isShowDecal = new bool[DecalCount];
        private const string shaderName = "dennokoworks/SimpleDecalEx";

        protected override void LoadCustomProperties(MaterialProperty[] props, Material material)
        {
            isCustomShader = true;

            // If you want to change rendering modes in the editor, specify the shader here
            ReplaceToCustomShaders();
            isShowRenderMode = !material.shader.name.Contains("Optional");

            for(int i = 0; i < DecalCount; i++)
            {
                int n = i + 1;
                decalEnable[i]       = FindProperty("_Decal" + n + "Enable", props);
                decalColor[i]        = FindProperty("_Decal" + n + "Color", props);
                decalTex[i]          = FindProperty("_Decal" + n + "Tex", props);
                decalUVMode[i]       = FindProperty("_Decal" + n + "Tex_UVMode", props);
                decalBlendMode[i]    = FindProperty("_Decal" + n + "TexBlendMode", props);
                decalCull[i]         = FindProperty("_Decal" + n + "Tex_Cull", props);
                decalAngle[i]        = FindProperty("_Decal" + n + "TexAngle", props);
                decalScrollRotate[i] = FindProperty("_Decal" + n + "Tex_ScrollRotate", props);
                decalIsDecal[i]      = FindProperty("_Decal" + n + "TexIsDecal", props);
                decalLeftOnly[i]     = FindProperty("_Decal" + n + "TexIsLeftOnly", props);
                decalRightOnly[i]    = FindProperty("_Decal" + n + "TexIsRightOnly", props);
                decalCopy[i]         = FindProperty("_Decal" + n + "TexShouldCopy", props);
                decalFlipMirror[i]   = FindProperty("_Decal" + n + "TexShouldFlipMirror", props);
                decalFlipCopy[i]     = FindProperty("_Decal" + n + "TexShouldFlipCopy", props);
                decalMSDF[i]         = FindProperty("_Decal" + n + "TexIsMSDF", props);
            }
        }

        protected override void DrawCustomProperties(Material material)
        {
            isShowCustomProperties = Foldout("SimpleDecalEx", "SimpleDecalEx", isShowCustomProperties);
            if(!isShowCustomProperties) return;

            EditorGUILayout.BeginVertical(boxOuter);

            for(int i = 0; i < DecalCount; i++)
            {
                int n = i + 1;
                isShowDecal[i] = Foldout("Decal " + n, isShowDecal[i]);
                if(isShowDecal[i])
                {
                    EditorGUILayout.BeginVertical(boxInner);
                    m_MaterialEditor.ShaderProperty(decalEnable[i], "Enable");
                    m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Texture / Color"), decalTex[i], decalColor[i]);
                    m_MaterialEditor.TextureScaleOffsetProperty(decalTex[i]);
                    m_MaterialEditor.ShaderProperty(decalAngle[i], "Angle");
                    m_MaterialEditor.ShaderProperty(decalScrollRotate[i], "Scroll & Rotate");
                    m_MaterialEditor.ShaderProperty(decalUVMode[i], "UV Mode");
                    m_MaterialEditor.ShaderProperty(decalBlendMode[i], "Blend Mode");
                    m_MaterialEditor.ShaderProperty(decalCull[i], "Cull Mode");
                    DrawLine();
                    m_MaterialEditor.ShaderProperty(decalIsDecal[i], "As Decal");
                    m_MaterialEditor.ShaderProperty(decalLeftOnly[i], "Left Only");
                    m_MaterialEditor.ShaderProperty(decalRightOnly[i], "Right Only");
                    m_MaterialEditor.ShaderProperty(decalCopy[i], "Mirror Copy");
                    m_MaterialEditor.ShaderProperty(decalFlipMirror[i], "Flip Mirror");
                    m_MaterialEditor.ShaderProperty(decalFlipCopy[i], "Flip Copy");
                    m_MaterialEditor.ShaderProperty(decalMSDF[i], "As MSDF");
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }

        protected override void ReplaceToCustomShaders()
        {
            lts         = Shader.Find(shaderName + "/lilToon");
            ltsc        = Shader.Find("Hidden/" + shaderName + "/Cutout");
            ltst        = Shader.Find("Hidden/" + shaderName + "/Transparent");
            ltsot       = Shader.Find("Hidden/" + shaderName + "/OnePassTransparent");
            ltstt       = Shader.Find("Hidden/" + shaderName + "/TwoPassTransparent");

            ltso        = Shader.Find("Hidden/" + shaderName + "/OpaqueOutline");
            ltsco       = Shader.Find("Hidden/" + shaderName + "/CutoutOutline");
            ltsto       = Shader.Find("Hidden/" + shaderName + "/TransparentOutline");
            ltsoto      = Shader.Find("Hidden/" + shaderName + "/OnePassTransparentOutline");
            ltstto      = Shader.Find("Hidden/" + shaderName + "/TwoPassTransparentOutline");

            ltsoo       = Shader.Find(shaderName + "/[Optional] OutlineOnly/Opaque");
            ltscoo      = Shader.Find(shaderName + "/[Optional] OutlineOnly/Cutout");
            ltstoo      = Shader.Find(shaderName + "/[Optional] OutlineOnly/Transparent");

            ltstess     = Shader.Find("Hidden/" + shaderName + "/Tessellation/Opaque");
            ltstessc    = Shader.Find("Hidden/" + shaderName + "/Tessellation/Cutout");
            ltstesst    = Shader.Find("Hidden/" + shaderName + "/Tessellation/Transparent");
            ltstessot   = Shader.Find("Hidden/" + shaderName + "/Tessellation/OnePassTransparent");
            ltstesstt   = Shader.Find("Hidden/" + shaderName + "/Tessellation/TwoPassTransparent");

            ltstesso    = Shader.Find("Hidden/" + shaderName + "/Tessellation/OpaqueOutline");
            ltstessco   = Shader.Find("Hidden/" + shaderName + "/Tessellation/CutoutOutline");
            ltstessto   = Shader.Find("Hidden/" + shaderName + "/Tessellation/TransparentOutline");
            ltstessoto  = Shader.Find("Hidden/" + shaderName + "/Tessellation/OnePassTransparentOutline");
            ltstesstto  = Shader.Find("Hidden/" + shaderName + "/Tessellation/TwoPassTransparentOutline");

            ltsl        = Shader.Find(shaderName + "/lilToonLite");
            ltslc       = Shader.Find("Hidden/" + shaderName + "/Lite/Cutout");
            ltslt       = Shader.Find("Hidden/" + shaderName + "/Lite/Transparent");
            ltslot      = Shader.Find("Hidden/" + shaderName + "/Lite/OnePassTransparent");
            ltsltt      = Shader.Find("Hidden/" + shaderName + "/Lite/TwoPassTransparent");

            ltslo       = Shader.Find("Hidden/" + shaderName + "/Lite/OpaqueOutline");
            ltslco      = Shader.Find("Hidden/" + shaderName + "/Lite/CutoutOutline");
            ltslto      = Shader.Find("Hidden/" + shaderName + "/Lite/TransparentOutline");
            ltsloto     = Shader.Find("Hidden/" + shaderName + "/Lite/OnePassTransparentOutline");
            ltsltto     = Shader.Find("Hidden/" + shaderName + "/Lite/TwoPassTransparentOutline");

            ltsref      = Shader.Find("Hidden/" + shaderName + "/Refraction");
            ltsrefb     = Shader.Find("Hidden/" + shaderName + "/RefractionBlur");
            ltsfur      = Shader.Find("Hidden/" + shaderName + "/Fur");
            ltsfurc     = Shader.Find("Hidden/" + shaderName + "/FurCutout");
            ltsfurtwo   = Shader.Find("Hidden/" + shaderName + "/FurTwoPass");
            ltsfuro     = Shader.Find(shaderName + "/[Optional] FurOnly/Transparent");
            ltsfuroc    = Shader.Find(shaderName + "/[Optional] FurOnly/Cutout");
            ltsfurotwo  = Shader.Find(shaderName + "/[Optional] FurOnly/TwoPass");
            ltsgem      = Shader.Find("Hidden/" + shaderName + "/Gem");
            ltsfs       = Shader.Find(shaderName + "/[Optional] FakeShadow");

            ltsover     = Shader.Find(shaderName + "/[Optional] Overlay");
            ltsoover    = Shader.Find(shaderName + "/[Optional] OverlayOnePass");
            ltslover    = Shader.Find(shaderName + "/[Optional] LiteOverlay");
            ltsloover   = Shader.Find(shaderName + "/[Optional] LiteOverlayOnePass");

            ltsm        = Shader.Find(shaderName + "/lilToonMulti");
            ltsmo       = Shader.Find("Hidden/" + shaderName + "/MultiOutline");
            ltsmref     = Shader.Find("Hidden/" + shaderName + "/MultiRefraction");
            ltsmfur     = Shader.Find("Hidden/" + shaderName + "/MultiFur");
            ltsmgem     = Shader.Find("Hidden/" + shaderName + "/MultiGem");
        }
    }
}
#endif
