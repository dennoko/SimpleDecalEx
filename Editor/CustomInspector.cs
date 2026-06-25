#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    // SimpleDecalEx: lilToon のデカール仕様に準拠したデカールを 7 枚追加するカスタムシェーダー。
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
        private const int DecalCount = 7;
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
        private MaterialProperty matcapMainStrength;
        private MaterialProperty matcapShadowMask;
        private MaterialProperty matcapEnableLighting;

        // Decal Sticker（半透明マテリアルでデカール部分を不透明化）
        private MaterialProperty alphaOverrideEnable;
        private MaterialProperty alphaOverrideStrength;

        private static bool isShowCustomProperties;
        private static readonly bool[] isShowDecal = new bool[DecalCount];
        private static int  s_PickingSlot = -1;
        private static Rect s_PickerRect;
        private static bool isShowMatCap;
        private static bool isShowSticker;
        private static bool isShowMaskExport;
        private static int  maskResolutionIdx = 2; // default = 512
        private static int  maskUVChannel;
        private static string maskOutputDir = "Assets/DecalMask/";
        private static bool maskOverwrite;
        private static int  maskAlphaMode; // 0=Binary, 1=Linear

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

        private static readonly int[]    MaskSizeValues  = { 128, 256, 512, 1024, 2048, 4096 };
        private static readonly string[] MaskSizeOptions = { "128", "256", "512", "1024", "2048", "4096" };
        private static string[] MaskAlphaModeOptions => IsJapanese
            ? new[] { "二値（完全透明かどうか）", "リニア（アルファ値に比例）" }
            : new[] { "Binary (transparent or not)", "Linear (proportional to alpha)" };

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
            matcapMainStrength   = FindProperty("_SDEXMatCapMainStrength", props);
            matcapShadowMask     = FindProperty("_SDEXMatCapShadowMask", props);
            matcapEnableLighting = FindProperty("_SDEXMatCapEnableLighting", props);
            alphaOverrideEnable   = FindProperty("_SDEXAlphaOverrideEnable", props);
            alphaOverrideStrength = FindProperty("_SDEXAlphaOverrideStrength", props);
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
                DrawSlider(decalPosX[i],   L("Position X", "位置 X"), -1f, 1f);
                DrawSlider(decalPosY[i],   L("Position Y", "位置 Y"), -1f, 1f);
                DrawSlider(decalScaleX[i], L("Scale X", "スケール X"), 0f, 1f);
                DrawSlider(decalScaleY[i], L("Scale Y", "スケール Y"), 0f, 1f);
                m_MaterialEditor.ShaderProperty(decalAngle[i],  L("Angle", "角度"));
                DrawPopup(decalUVMode[i], L("UV Mode", "UV モード"), UVModeOptions);
                DrawPopup(decalMirror[i], L("Mirror Mode", "ミラー"), MirrorOptions);

                DrawSubHeader(L("Blending", "合成"));
                DrawPopup(decalBlendMode[i], L("Blend Mode", "合成モード"), BlendModeOptions);
                DrawPopup(decalCull[i],      L("Cull Mode", "表示面"), CullOptions);

                EditorGUILayout.EndVertical();
                DrawCoordPickerSection(material, i);
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
                    m_MaterialEditor.ShaderProperty(matcapMainStrength, new GUIContent(L("Main Color Power", "メインカラー強度"), L("Tint the MatCap by the surface main color (0 = none, 1 = full multiply)", "MatCapの色を表面のメインカラーで染める（0=なし / 1=完全乗算）")));
                    m_MaterialEditor.ShaderProperty(matcapEnableLighting, new GUIContent(L("Enable Lighting", "ライティング反映"), L("0 = unlit overlay, 1 = follow surface shading", "0=陰影なしのオーバーレイ / 1=陰影に追従")));
                    m_MaterialEditor.ShaderProperty(matcapShadowMask, new GUIContent(L("Shadow Mask", "影マスク"), L("Attenuate MatCap in shadowed areas (1 = hidden in shadow)", "影部分でMatCapを減衰（1で影では非表示）")));
                }
                EditorGUILayout.EndVertical();
            }

            isShowSticker = Foldout(L("Decal Sticker (Transparent)", "デカールのステッカー化（半透明用）"), isShowSticker);
            if(isShowSticker)
            {
                EditorGUILayout.BeginVertical(boxInner);
                EditorGUILayout.HelpBox(
                    L("Forces decal areas opaque on Transparent materials (sticker on glass). No effect on Opaque/Cutout.",
                      "半透明マテリアルでデカール部分を不透明化します（ガラス面のシール表現）。不透明/カットアウトでは無効。"),
                    MessageType.Info);
                DrawToggle(alphaOverrideEnable, L("Enable", "有効"));
                if(alphaOverrideEnable.floatValue > 0.5f)
                {
                    m_MaterialEditor.ShaderProperty(alphaOverrideStrength,
                        new GUIContent(L("Strength", "強度"),
                            L("1 = fully opaque in decal areas", "1でデカール部分を完全に不透明化")));
                }
                EditorGUILayout.EndVertical();
            }

            DrawMaskExportSection(material);

            EditorGUILayout.EndVertical();
        }

        //----------------------------------------------------------------------------------------------------------------------
        // Mask Export

        private void DrawMaskExportSection(Material material)
        {
            isShowMaskExport = Foldout(L("Mask Export", "マスク書き出し"), isShowMaskExport);
            if(!isShowMaskExport) return;

            EditorGUILayout.BeginVertical(boxInner);
            EditorGUILayout.HelpBox(
                L("Exports the union of all enabled decal footprints as a grayscale mask. Both normal and inverted (_invert) versions are saved.",
                  "有効な全デカールフットプリントの和集合をグレースケールマスクとして書き出します。通常版と反転版（_invert）が同時に保存されます。"),
                MessageType.Info);

            maskResolutionIdx = EditorGUILayout.Popup(L("Resolution", "解像度"),          maskResolutionIdx, MaskSizeOptions);
            maskUVChannel     = EditorGUILayout.Popup(L("UV Channel", "UV チャンネル"),   maskUVChannel,     UVModeOptions);
            maskAlphaMode     = EditorGUILayout.Popup(L("Alpha Mode", "アルファモード"), maskAlphaMode,     MaskAlphaModeOptions);
            maskOutputDir     = EditorGUILayout.TextField(L("Output Directory", "出力先ディレクトリ"), maskOutputDir);
            maskOverwrite     = EditorGUILayout.Toggle(L("Overwrite Existing", "上書き"), maskOverwrite);

            EditorGUILayout.Space(4f);
            if(GUILayout.Button(L("Export Decal Mask", "デカールマスクを書き出し")))
                ExportDecalMask(material);

            EditorGUILayout.EndVertical();
        }

        private void ExportDecalMask(Material material)
        {
            int size = MaskSizeValues[maskResolutionIdx];

            // 対象デカールを収集し、サンプリング可能なテクスチャを用意
            int[]       activeIdx = new int[DecalCount];
            Texture2D[] readTex   = new Texture2D[DecalCount];
            int activeCount = 0;
            for(int i = 0; i < DecalCount; i++)
            {
                if(decalEnable[i].floatValue < 0.5f) continue;
                if(Mathf.RoundToInt(decalUVMode[i].floatValue) != maskUVChannel) continue;
                activeIdx[activeCount] = i;
                readTex[activeCount]   = GetReadableCopy(decalTex[i].textureValue as Texture2D);
                activeCount++;
            }

            var pixels = new Color32[size * size];
            try
            {
                for(int py = 0; py < size; py++)
                {
                    if(size > 512 && py % 64 == 0)
                        EditorUtility.DisplayProgressBar(
                            L("Exporting Decal Mask", "デカールマスク書き出し中"),
                            py + " / " + size, (float)py / size);

                    float v = (py + 0.5f) / size;
                    for(int px = 0; px < size; px++)
                    {
                        float u        = (px + 0.5f) / size;
                        float maxAlpha = 0f;
                        for(int k = 0; k < activeCount; k++)
                        {
                            int di = activeIdx[k];
                            float a = SampleDecalAlpha(u, v,
                                decalPosX[di].floatValue,   decalPosY[di].floatValue,
                                decalScaleX[di].floatValue, decalScaleY[di].floatValue,
                                decalAngle[di].floatValue,
                                Mathf.RoundToInt(decalMirror[di].floatValue),
                                readTex[k], decalColor[di].colorValue.a);
                            if(a > maxAlpha) maxAlpha = a;
                        }
                        byte c = maskAlphaMode == 0
                            ? (maxAlpha > 0f ? (byte)255 : (byte)0)
                            : (byte)Mathf.RoundToInt(Mathf.Clamp01(maxAlpha) * 255f);
                        pixels[py * size + px] = new Color32(c, c, c, 255);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                for(int k = 0; k < activeCount; k++)
                {
                    var orig = decalTex[activeIdx[k]].textureValue as Texture2D;
                    if(readTex[k] != null && readTex[k] != orig)
                        Object.DestroyImmediate(readTex[k]);
                }
            }

            string baseName  = SanitizeFileName(material.name) + "_DecalMask";
            string savedPath = SaveMaskTexture(pixels, size, baseName, "");
            for(int k = 0; k < pixels.Length; k++)
            {
                byte ic = (byte)(255 - pixels[k].r);
                pixels[k] = new Color32(ic, ic, ic, 255);
            }
            SaveMaskTexture(pixels, size, baseName, "_invert");

            AssetDatabase.Refresh();

            // 保存先フォルダをプロジェクトタブで開く
            string dir       = NormalizeDir(maskOutputDir);
            var folderAsset  = AssetDatabase.LoadAssetAtPath<Object>(dir);
            if(folderAsset != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = folderAsset;
                EditorGUIUtility.PingObject(folderAsset);
            }
            Debug.Log("[SimpleDecalEx] Decal mask exported to: " + dir);
        }

        private static string SaveMaskTexture(Color32[] pixels, int size, string baseName, string suffix)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string dir    = NormalizeDir(maskOutputDir);
            string absDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", dir));
            Directory.CreateDirectory(absDir);

            string fileName = baseName + suffix + ".png";
            string absPath  = Path.Combine(absDir, fileName);

            if(!maskOverwrite && File.Exists(absPath))
            {
                int n = 1;
                do
                {
                    fileName = baseName + suffix + " " + n + ".png";
                    absPath  = Path.Combine(absDir, fileName);
                    n++;
                } while(File.Exists(absPath));
            }

            File.WriteAllBytes(absPath, png);
            string assetPath = dir + "/" + fileName;
            AssetDatabase.ImportAsset(assetPath);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if(importer != null)
            {
                importer.sRGBTexture         = false;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return assetPath;
        }

        private static string NormalizeDir(string dir)
        {
            dir = dir.Replace('\\', '/').TrimEnd('/');
            if(!dir.StartsWith("Assets/") && dir != "Assets")
                dir = "Assets/" + dir.TrimStart('/');
            return dir;
        }

        private static string SanitizeFileName(string name)
        {
            return string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        }

        // 非Readableテクスチャを RenderTexture 経由でCPU読み取り可能なコピーに変換する。
        // 元から isReadable な場合はそのまま返す（コピーなし）。
        private static Texture2D GetReadableCopy(Texture2D src)
        {
            if(src == null) return null;
            if(src.isReadable) return src;
            var rt   = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        // デカール1枚のUV(u,v)におけるアルファ値を返す（範囲外・フィルタ後に0）。
        // ミラー変換(UV空間x方向) → ST変換 → 回転 の順で適用し、テクスチャアルファ×カラーアルファを返す。
        private static float SampleDecalAlpha(float u, float v, float posX, float posY, float scaleX, float scaleY, float angle, int mirror, Texture2D tex, float colorAlpha)
        {
            if(scaleX <= 0f || scaleY <= 0f) return 0f;

            float testU = u;
            switch(mirror)
            {
                case 1: if(u >= 0.5f) return 0f; break;                  // Left Only
                case 2: if(u <  0.5f) return 0f; break;                  // Right Only
                case 3: case 4: testU = u >= 0.5f ? 1f - u : u; break;  // Symmetry Copy
                // 0=None, 5=Flip on Mirror → use u as-is
            }

            // ST変換: メッシュUV → デカールテクスチャUV空間（中心0.5,0.5）
            float du = (testU - posX) / scaleX;
            float dv = (v     - posY) / scaleY;

            // デカールテクスチャ空間内で(0.5,0.5)中心に回転
            float rad    = angle * Mathf.Deg2Rad;
            float cosA   = Mathf.Cos(rad);
            float sinA   = Mathf.Sin(rad);
            float uDecal = 0.5f + du * cosA - dv * sinA;
            float vDecal = 0.5f + du * sinA + dv * cosA;

            if(uDecal < 0f || uDecal > 1f || vDecal < 0f || vDecal > 1f) return 0f;

            float texAlpha = tex != null ? tex.GetPixelBilinear(uDecal, vDecal).a : 1f;
            return texAlpha * colorAlpha;
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

        private void DrawSlider(MaterialProperty prop, string label, float min, float max)
        {
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(label, prop.floatValue, min, max);
            if(EditorGUI.EndChangeCheck()) prop.floatValue = value;
            EditorGUI.showMixedValue = false;
        }

        private void DrawCoordPickerSection(Material material, int slotIndex)
        {
            bool isActive = (s_PickingSlot == slotIndex);

            Color prevBg = GUI.backgroundColor;
            if(isActive) GUI.backgroundColor = new Color(0.4f, 0.8f, 1.0f);
            string btnLabel = isActive
                ? L("[ Picking... ] Click to cancel", "[ 取得中... ] クリックでキャンセル")
                : L("Pick Position on Texture", "テクスチャから座標を取得");
            if(GUILayout.Button(btnLabel))
            {
                s_PickingSlot = isActive ? -1 : slotIndex;
                m_MaterialEditor.Repaint();
            }
            GUI.backgroundColor = prevBg;

            if(s_PickingSlot != slotIndex) return;

            Texture mainTex = material.mainTexture;
            if(mainTex == null || mainTex.width == 0)
            {
                EditorGUILayout.HelpBox(
                    L("No main texture (_MainTex) assigned.",
                      "メインテクスチャ (_MainTex) が設定されていません。"),
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                L("Click on the texture to set Position X/Y.",
                  "テクスチャをクリックして位置 X/Y を設定します。"),
                MessageType.None);

            float availW   = EditorGUIUtility.currentViewWidth - 44f;
            float aspect   = (float)mainTex.height / mainTex.width;
            float previewH = Mathf.Min(availW * aspect, 300f);
            float previewW = previewH / aspect;
            Rect allocRect   = GUILayoutUtility.GetRect(availW, previewH);
            Rect previewRect = new Rect(
                allocRect.x + (allocRect.width - previewW) * 0.5f,
                allocRect.y, previewW, previewH);
            s_PickerRect = previewRect;

            if(Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawPreviewTexture(previewRect, mainTex);

                float px = decalPosX[slotIndex].floatValue;
                float py = decalPosY[slotIndex].floatValue;
                if(px >= 0f && px <= 1f && py >= 0f && py <= 1f)
                {
                    float cx = previewRect.x + px * previewRect.width;
                    float cy = previewRect.y + (1f - py) * previewRect.height;
                    const float HL = 8f;
                    EditorGUI.DrawRect(new Rect(cx - HL, cy - 1f, HL * 2f, 2f), Color.red);
                    EditorGUI.DrawRect(new Rect(cx - 1f, cy - HL, 2f, HL * 2f), Color.red);
                    EditorGUI.DrawRect(new Rect(cx - 2f, cy - 2f, 4f, 4f), new Color(1f, 1f, 0f, 0.9f));
                }

                EditorGUI.DrawRect(new Rect(previewRect.x - 1, previewRect.y - 1, previewRect.width + 2, 1), Color.gray);
                EditorGUI.DrawRect(new Rect(previewRect.x - 1, previewRect.yMax,  previewRect.width + 2, 1), Color.gray);
                EditorGUI.DrawRect(new Rect(previewRect.x - 1, previewRect.y, 1, previewRect.height), Color.gray);
                EditorGUI.DrawRect(new Rect(previewRect.xMax,  previewRect.y, 1, previewRect.height), Color.gray);


            }

            if(Event.current.type == EventType.MouseDown &&
               Event.current.button == 0 &&
               s_PickerRect.Contains(Event.current.mousePosition))
            {
                Vector2 local = Event.current.mousePosition - new Vector2(s_PickerRect.x, s_PickerRect.y);
                float clickU = Mathf.Clamp01(local.x / s_PickerRect.width);
                float clickV = Mathf.Clamp01(1f - local.y / s_PickerRect.height);

                Undo.RecordObjects(m_MaterialEditor.targets,
                    L("Set Decal Position", "デカール位置を設定"));
                decalPosX[slotIndex].floatValue = clickU;
                decalPosY[slotIndex].floatValue = clickV;

                Event.current.Use();
                m_MaterialEditor.Repaint();
            }
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
