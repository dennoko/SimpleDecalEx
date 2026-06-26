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

        // Global mip bias
        private MaterialProperty decalMipBias;

        private static bool isShowCustomProperties;
        private static readonly bool[] isShowDecal = new bool[DecalCount];
        private static int  s_PickingSlot = -1;
        private static Rect s_PickerRect;
        private static Texture2D s_DummyTex;
        private static bool isShowMatCap;
        private static bool isShowSticker;
        private static bool isShowMaskExport;
        private static bool isShowMipBias;
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

        private static string[] MirrorTooltips => IsJapanese
            ? new[] {
                "なし: ミラー処理を行いません。",
                "左のみ: モデルの左側（X < 0.5）にのみ表示します。左右共有UVで左側だけに表示したい場合に使用します。",
                "右のみ: モデルの右側（X >= 0.5）にのみ表示します。左右共有UVで右側だけに表示したい場合に使用します。",
                "左右対称コピー: 左右両側に同じ向き（反転なし）でコピーして表示します。文字など反転させたくない場合に使用します。",
                "左右対称コピー(反転): 左右両側に表示し、片方を水平反転したコピーにします。翼や模様など左右対称にしたい場合に使用します。",
                "ミラーで反転: 右半分（X >= 0.5）でのみデカールを水平反転します。左右共有UVでの反転を補正する場合に使用します。"
            }
            : new[] {
                "None: Do not apply mirror processing.",
                "Left Only: Display only on the left side (X < 0.5) of the model. Use for symmetrical UVs to display on the left side only.",
                "Right Only: Display only on the right side (X >= 0.5) of the model. Use for symmetrical UVs to display on the right side only.",
                "Symmetry Copy: Copy and display the decal on both sides in the same direction. Use when you do not want to flip text, etc.",
                "Symmetry Copy (Flip): Display on both sides, with one side as a horizontally flipped copy. Use for symmetrical patterns like wings.",
                "Flip on Mirror: Flip the decal horizontally only on the right side (X >= 0.5) of the model. Use to correct flipping on symmetrical UVs."
            };

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
            decalMipBias          = FindProperty("_DecalMipBias", props);
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

                DrawToggle(decalEnable[i], L("Enable", "有効"), L("Enable or disable this decal slot.", "このデカールスロットの有効・無効を切り替えます。"));
                m_MaterialEditor.TexturePropertySingleLine(
                    new GUIContent(L("Texture / Color", "テクスチャ / 色"), L("Select decal texture and color tint.", "デカールのテクスチャとカラーティントを設定します。")), 
                    decalTex[i], decalColor[i]
                );

                DrawSubHeader(L("Placement", "配置"));
                DrawSlider(decalPosX[i],   L("Position X", "位置 X"), -1f, 1f, L("Horizontal position of the decal on UV space.", "UV空間上でのデカールの横方向の位置。"));
                DrawSlider(decalPosY[i],   L("Position Y", "位置 Y"), -1f, 1f, L("Vertical position of the decal on UV space.", "UV空間上でのデカールの縦方向の位置。"));
                DrawSlider(decalScaleX[i], L("Scale X", "スケール X"), 0f, 1f, L("Horizontal scale of the decal.", "デカールの横方向の大きさ。"));
                DrawSlider(decalScaleY[i], L("Scale Y", "スケール Y"), 0f, 1f, L("Vertical scale of the decal.", "デカールの縦方向の大きさ。"));
                m_MaterialEditor.ShaderProperty(decalAngle[i], new GUIContent(L("Angle", "角度"), L("Rotation angle of the decal (in degrees).", "デカールの回転角度。")));
                DrawPopup(decalUVMode[i], L("UV Mode", "UV モード"), UVModeOptions, L("Select UV channel for mapping.", "デカールをマッピングするUVチャンネルを選択します。"));
                DrawPopup(decalMirror[i], new GUIContent(L("Mirror Mode", "ミラー"), L("Select mirror behavior for symmetrical/asymmetrical UV mapping.", "デカールの複製や左右の反転などの挙動を設定します。")), MirrorOptions, MirrorTooltips);

                DrawSubHeader(L("Blending", "合成"));
                DrawPopup(decalBlendMode[i], L("Blend Mode", "合成モード"), BlendModeOptions, L("Blend mode for combining the decal color.", "デカールを下地と合成する際のブレンドモード。"));
                DrawPopup(decalCull[i],      L("Cull Mode", "表示面"), CullOptions, L("Decide whether to show on Front, Back, or Both sides.", "デカールをポリゴンの表面、裏面、または両面に表示するかを選択します。"));

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

            isShowMipBias = Foldout(L("Mip Bias", "ミップバイアス"), isShowMipBias);
            if(isShowMipBias)
            {
                EditorGUILayout.BeginVertical(boxInner);
                EditorGUILayout.HelpBox(
                    L("Negative values sharpen all decals at distance by biasing mip level selection. 0 = hardware automatic, -1.5 to -4 recommended.",
                      "負値にするほど遠距離でのデカールのぼけを抑制します。0=自動、-1.5〜-4 が目安です。"),
                    MessageType.None);
                DrawSlider(decalMipBias, new GUIContent(
                    L("Mip Bias", "ミップバイアス"),
                    L("Negative values sharpen all decals at distance.", "負値にするほど遠距離でのぼけを抑制します。")),
                    -8f, 0f);
                EditorGUILayout.EndVertical();
            }

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

            bool isPixelRight = (u >= 0.5f);

            // 表示・非表示の判定
            if(mirror == 1 && isPixelRight) return 0f; // Left Only
            if(mirror == 2 && !isPixelRight) return 0f; // Right Only

            bool decalOnRight = (posX >= 0.5f);
            bool isCopy = (mirror == 3 || mirror == 4) && (decalOnRight != isPixelRight);
            float mappedU = isCopy ? (1.0f - u) : u;

            float activeAngle = angle; // マテリアルに格納されている値はすでにラジアン値
            bool flipX = false;

            if(mirror == 4 && isCopy)
            {
                flipX = true;
            }
            else if(mirror == 5 && isPixelRight)
            {
                activeAngle = -activeAngle;
                flipX = true;
            }

            float cosA = Mathf.Cos(activeAngle);
            float sinA = Mathf.Sin(activeAngle);

            float deltaX = mappedU - posX;
            float deltaY = v - posY;

            float rotDeltaX = deltaX * cosA - deltaY * sinA;
            float rotDeltaY = deltaX * sinA + deltaY * cosA;

            float uDecal = rotDeltaX / scaleX;
            float vDecal = rotDeltaY / scaleY;

            if(flipX) uDecal = -uDecal;

            uDecal += 0.5f;
            vDecal += 0.5f;

            if(uDecal < 0f || uDecal > 1f || vDecal < 0f || vDecal > 1f) return 0f;

            float texAlpha = tex != null ? tex.GetPixelBilinear(uDecal, vDecal).a : 1f;
            return texAlpha * colorAlpha;
        }

        //----------------------------------------------------------------------------------------------------------------------
        // UI helpers

        private static Texture2D GetDummyTexture()
        {
            if(s_DummyTex != null) return s_DummyTex;
            const int size = 256;
            const int cell = 16;
            s_DummyTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for(int y = 0; y < size; y++)
                for(int x = 0; x < size; x++)
                {
                    bool checker = ((x / cell + y / cell) % 2 == 0);
                    pixels[y * size + x] = checker ? new Color32(204, 204, 204, 255) : new Color32(153, 153, 153, 255);
                }
            s_DummyTex.SetPixels32(pixels);
            s_DummyTex.Apply();
            s_DummyTex.filterMode = FilterMode.Point;
            return s_DummyTex;
        }

        private static void DrawSubHeader(string label)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        private void DrawToggle(MaterialProperty prop, string label, string tooltip = null)
        {
            DrawToggle(prop, new GUIContent(label, tooltip));
        }

        private void DrawToggle(MaterialProperty prop, GUIContent label)
        {
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.Toggle(label, prop.floatValue > 0.5f);
            if(EditorGUI.EndChangeCheck()) prop.floatValue = value ? 1f : 0f;
            EditorGUI.showMixedValue = false;
        }

        private void DrawPopup(MaterialProperty prop, string label, string[] options, string tooltip = null)
        {
            DrawPopup(prop, new GUIContent(label, tooltip), options);
        }

        private void DrawPopup(MaterialProperty prop, GUIContent label, string[] options, string[] tooltips = null)
        {
            GUIContent[] guiOptions = new GUIContent[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                string t = (tooltips != null && i < tooltips.Length) ? tooltips[i] : null;
                guiOptions[i] = new GUIContent(options[i], t);
            }

            int index = Mathf.Clamp((int)(prop.floatValue + 0.5f), 0, options.Length - 1);
            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup(label, index, guiOptions);
            if(EditorGUI.EndChangeCheck()) prop.floatValue = index;
            EditorGUI.showMixedValue = false;
        }

        private void DrawSlider(MaterialProperty prop, string label, float min, float max, string tooltip = null)
        {
            DrawSlider(prop, new GUIContent(label, tooltip), min, max);
        }

        private void DrawSlider(MaterialProperty prop, GUIContent label, float min, float max)
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

            int uvMode = Mathf.RoundToInt(decalUVMode[slotIndex].floatValue);
            Texture mainTex = material.mainTexture;
            bool hasMainTex = mainTex != null && mainTex.width > 0;
            bool uvMismatch = uvMode != 0;

            Texture displayTex;
            if(uvMismatch)
            {
                displayTex = GetDummyTexture();
                EditorGUILayout.HelpBox(
                    L("Decal uses UV" + uvMode + " but the main texture (_MainTex) is UV0. Showing a blank canvas — click to set position.",
                      "デカールは UV" + uvMode + " を参照していますが、メインテクスチャ (_MainTex) は UV0 です。ブランクキャンバスを表示します — クリックして位置を設定してください。"),
                    MessageType.Info);
            }
            else if(!hasMainTex)
            {
                displayTex = GetDummyTexture();
                EditorGUILayout.HelpBox(
                    L("No main texture (_MainTex) assigned. Showing a blank canvas — click to set position.",
                      "メインテクスチャ (_MainTex) が設定されていません。ブランクキャンバスを表示します — クリックして位置を設定してください。"),
                    MessageType.Info);
            }
            else
            {
                displayTex = mainTex;
                EditorGUILayout.HelpBox(
                    L("Click on the texture to set Position X/Y.",
                      "テクスチャをクリックして位置 X/Y を設定します。"),
                    MessageType.None);
            }

            float availW   = EditorGUIUtility.currentViewWidth - 44f;
            float aspect   = (float)displayTex.height / displayTex.width;
            float previewH = Mathf.Min(availW * aspect, 300f);
            float previewW = previewH / aspect;
            Rect allocRect   = GUILayoutUtility.GetRect(availW, previewH);
            Rect previewRect = new Rect(
                allocRect.x + (allocRect.width - previewW) * 0.5f,
                allocRect.y, previewW, previewH);
            s_PickerRect = previewRect;

            if(Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawPreviewTexture(previewRect, displayTex);

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
