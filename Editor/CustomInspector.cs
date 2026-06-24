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
    //
    // enum（UV Mode / Blend Mode / Cull / Mirror）は lilEnum ドロワーに頼らず、
    // EditorGUILayout.Popup で手動描画する（ドロワー経由だと選択できない不具合があるため）。
    public class SimpleDecalExInspector : lilToonInspector
    {
        private const int DecalCount = 6;
        private const string shaderName = "dennokoworks/SimpleDecalEx";

        private readonly MaterialProperty[] decalEnable    = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalTex       = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalColor     = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalPosX      = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalPosY      = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalScaleX    = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalScaleY    = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalAngle     = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalUVMode    = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalBlendMode = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalCull      = new MaterialProperty[DecalCount];
        private readonly MaterialProperty[] decalMirror    = new MaterialProperty[DecalCount];

        // MatCap（全デカール領域に限定して合成する単一マットキャップ）
        private MaterialProperty matcapEnable;
        private MaterialProperty matcapTex;
        private MaterialProperty matcapColor;
        private MaterialProperty matcapBlendMode;
        private MaterialProperty matcapOpacity;
        private MaterialProperty matcapShadowMask;
        private MaterialProperty matcapEnableLighting;

        private static bool isShowCustomProperties;
        private static readonly bool[] isShowDecal = new bool[DecalCount];
        private static bool isShowMatCap;

        //----------------------------------------------------------------------------------------------------------------------
        // Localization
        private static bool IsJapanese => lilLanguageManager.langSet.languageName == "ja-JP";
        private static string L(string en, string ja) => IsJapanese ? ja : en;

        private static string[] UVModeOptions => new[] { "UV0", "UV1", "UV2", "UV3" };
        private static string[] BlendModeOptions => IsJapanese
            ? new[] { "通常", "加算", "スクリーン", "乗算" }
            : new[] { "Normal", "Add", "Screen", "Multiply" };
        private static string[] CullOptions => IsJapanese
            ? new[] { "両面", "表面のみ", "裏面のみ" }
            : new[] { "Off", "Front", "Back" };
        private static string[] MirrorOptions => IsJapanese
            ? new[] { "なし", "左のみ", "右のみ", "左右対称コピー", "左右対称コピー(反転)", "ミラーで反転" }
            : new[] { "None", "Left Only", "Right Only", "Symmetry Copy", "Symmetry Copy (Flip)", "Flip on Mirror" };

        protected override void LoadCustomProperties(MaterialProperty[] props, Material material)
        {
            isCustomShader = true;

            ReplaceToCustomShaders();
            isShowRenderMode = !material.shader.name.Contains("Optional");

            for(int i = 0; i < DecalCount; i++)
            {
                int n = i + 1;
                decalEnable[i]    = FindProperty("_Decal" + n + "Enable", props);
                decalTex[i]       = FindProperty("_Decal" + n + "Tex", props);
                decalColor[i]     = FindProperty("_Decal" + n + "Color", props);
                decalPosX[i]      = FindProperty("_Decal" + n + "PosX", props);
                decalPosY[i]      = FindProperty("_Decal" + n + "PosY", props);
                decalScaleX[i]    = FindProperty("_Decal" + n + "ScaleX", props);
                decalScaleY[i]    = FindProperty("_Decal" + n + "ScaleY", props);
                decalAngle[i]     = FindProperty("_Decal" + n + "Angle", props);
                decalUVMode[i]    = FindProperty("_Decal" + n + "UVMode", props);
                decalBlendMode[i] = FindProperty("_Decal" + n + "BlendMode", props);
                decalCull[i]      = FindProperty("_Decal" + n + "Cull", props);
                decalMirror[i]    = FindProperty("_Decal" + n + "Mirror", props);
            }

            matcapEnable         = FindProperty("_SDEXMatCapEnable", props);
            matcapTex            = FindProperty("_SDEXMatCapTex", props);
            matcapColor          = FindProperty("_SDEXMatCapColor", props);
            matcapBlendMode      = FindProperty("_SDEXMatCapBlendMode", props);
            matcapOpacity        = FindProperty("_SDEXMatCapOpacity", props);
            matcapShadowMask     = FindProperty("_SDEXMatCapShadowMask", props);
            matcapEnableLighting = FindProperty("_SDEXMatCapEnableLighting", props);
        }

        protected override void DrawCustomProperties(Material material)
        {
            isShowCustomProperties = Foldout("SimpleDecalEx", "SimpleDecalEx", isShowCustomProperties);
            if(!isShowCustomProperties) return;

            EditorGUILayout.BeginVertical(boxOuter);

            for(int i = 0; i < DecalCount; i++)
            {
                int n = i + 1;
                isShowDecal[i] = Foldout(L("Decal ", "デカール ") + n, isShowDecal[i]);
                if(!isShowDecal[i]) continue;

                EditorGUILayout.BeginVertical(boxInner);

                DrawToggle(decalEnable[i], L("Enable", "有効"));
                m_MaterialEditor.TexturePropertySingleLine(new GUIContent(L("Texture / Color", "テクスチャ / 色")), decalTex[i], decalColor[i]);

                DrawSubHeader(L("Placement", "配置"));
                m_MaterialEditor.ShaderProperty(decalPosX[i],   L("Position X", "位置 X"));
                m_MaterialEditor.ShaderProperty(decalPosY[i],   L("Position Y", "位置 Y"));
                m_MaterialEditor.ShaderProperty(decalScaleX[i], L("Scale X", "スケール X"));
                m_MaterialEditor.ShaderProperty(decalScaleY[i], L("Scale Y", "スケール Y"));
                m_MaterialEditor.ShaderProperty(decalAngle[i],  L("Angle", "角度"));
                DrawPopup(decalUVMode[i], L("UV Mode", "UV モード"), UVModeOptions);
                DrawPopup(decalMirror[i], L("Mirror Mode", "ミラー"), MirrorOptions);

                DrawSubHeader(L("Blending", "合成"));
                DrawPopup(decalBlendMode[i], L("Blend Mode", "合成モード"), BlendModeOptions);
                DrawPopup(decalCull[i],      L("Cull Mode", "表示面"), CullOptions);

                EditorGUILayout.EndVertical();
            }

            isShowMatCap = Foldout(L("MatCap (All Decals)", "MatCap（全デカール領域）"), isShowMatCap);
            if(isShowMatCap)
            {
                EditorGUILayout.BeginVertical(boxInner);
                EditorGUILayout.HelpBox(
                    L("Composites a single MatCap limited to the combined visible area of all enabled decals.",
                      "有効な全デカールの表示領域の和集合に限定して、1枚のMatCapを合成します。"),
                    MessageType.Info);
                DrawToggle(matcapEnable, L("Enable", "有効"));
                if(matcapEnable.floatValue > 0.5f)
                {
                    m_MaterialEditor.TexturePropertySingleLine(new GUIContent(L("Texture / Color", "テクスチャ / 色")), matcapTex, matcapColor);
                    DrawPopup(matcapBlendMode, L("Blend Mode", "合成モード"), BlendModeOptions);
                    m_MaterialEditor.ShaderProperty(matcapOpacity, new GUIContent(L("Opacity", "不透明度"), L("Overall MatCap opacity", "MatCap全体の不透明度")));
                    m_MaterialEditor.ShaderProperty(matcapEnableLighting, new GUIContent(L("Enable Lighting", "ライティング反映"), L("0 = unlit overlay, 1 = follow surface shading", "0=陰影なしのオーバーレイ / 1=陰影に追従")));
                    m_MaterialEditor.ShaderProperty(matcapShadowMask, new GUIContent(L("Shadow Mask", "影マスク"), L("Attenuate MatCap in shadowed areas (1 = hidden in shadow)", "影部分でMatCapを減衰（1で影では非表示）")));
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        //----------------------------------------------------------------------------------------------------------------------
        // UI helpers

        private static void DrawSubHeader(string label)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private void DrawToggle(MaterialProperty prop, string label)
        {
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.Toggle(label, prop.floatValue > 0.5f);
            if(EditorGUI.EndChangeCheck()) prop.floatValue = value ? 1f : 0f;
            EditorGUI.showMixedValue = false;
        }

        // enum を Popup で手動描画する。lilEnum ドロワー経由だと選択できない不具合を回避するため。
        private void DrawPopup(MaterialProperty prop, string label, string[] options)
        {
            int index = Mathf.Clamp((int)(prop.floatValue + 0.5f), 0, options.Length - 1);
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup(label, index, options);
            if(EditorGUI.EndChangeCheck()) prop.floatValue = index;
            EditorGUI.showMixedValue = false;
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

            ltsover     = Shader.Find(shaderName + "/[Optional] Overlay");
            ltsoover    = Shader.Find(shaderName + "/[Optional] OverlayOnePass");
        }
    }
}
#endif
