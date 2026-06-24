lilToonの拡張シェーダーで、可能な限り多くのデカールを貼り付けられるようにしたいです。
デカール自体の仕様は既存のlilToonの仕様に則ります。

デカール一枚ごとに透過画像1枚を使用し、マスクなどは使いません。

lilToon拡張機能開発のskillも使い、dennokoworks/simple-decal-exを実装してください。

まずはデカール6枚を追加できるようにしてください。


lilToonのデカール処理の仕様

## デカール関連プロパティ一覧

`_Main2ndTex` / `_Main3rdTex` に対して以下のプロパティセットが定義されています。 [1](#5-0) 

| プロパティ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `_Main2ndTex` | `2D` | `"white"` | テクスチャ本体 |
| `_Main2ndTexAngle` | `Float` | `0` | 静的回転角度（ラジアン） |
| `_Main2ndTex_ScrollRotate` | `Vector` | `(0,0,0,0)` | `(scrollX, scrollY, 未使用, rotateSpeed)` |
| `_Main2ndTex_UVMode` | `Int` | `0` | UV0/UV1/UV2/UV3/MatCap |
| `_Main2ndTex_Cull` | `Int` | `0` | カリングモード |
| `_Main2ndTexDecalAnimation` | `Vector` | `(1,1,1,30)` | `(columns, rows, totalFrames, fps)` |
| `_Main2ndTexDecalSubParam` | `Vector` | `(1,1,0,1)` | `(scaleX, scaleY, centerBlend, ?)` |
| `_Main2ndTexIsDecal` | `Int` | `0` | デカールモード ON/OFF |
| `_Main2ndTexIsLeftOnly` | `Int` | `0` | 左側のみ表示 |
| `_Main2ndTexIsRightOnly` | `Int` | `0` | 右側のみ表示 |
| `_Main2ndTexShouldCopy` | `Int` | `0` | UV中心で折り返してコピー |
| `_Main2ndTexShouldFlipMirror` | `Int` | `0` | ミラー側（右手）でX反転 |
| `_Main2ndTexShouldFlipCopy` | `Int` | `0` | コピー側（左半分）でX反転 |
| `_Main2ndTexIsMSDF` | `Int` | `0` | MSDFフォントとして処理 |
| `_Main2ndBlendMask` | `2D` | `"white"` | ブレンドマスク |
| `_Main2ndTexBlendMode` | `Int` | `0` | ブレンドモード |
| `_Main2ndTexAlphaMode` | `Int` | `0` | アルファモード |
| `_Main2ndEnableLighting` | `Range(0,1)` | `1` | ライティング影響度 |

---

## UV計算フロー（`lilCalcDecalUV`） [2](#5-1) 

処理は以下の順序で行われます：

```
入力UV (uv_ST.xy = Tiling, uv_ST.zw = Offset)

1. [Copy]
   shouldCopy == true のとき:
     outUV.x = abs(outUV.x - 0.5) + 0.5
   → UV空間のx=0.5を軸に左側を右側へ折り返す（左右対称コピー）

2. [Scale & Offset]
   outUV = outUV * uv_ST.xy + uv_ST.zw

3. [Flip Copy]
   shouldFlipCopy == true && uv.x < 0.5 のとき:
     outUV.x = 1.0 - outUV.x
   → コピーされた左側のみX反転

4. [Flip Mirror]
   shouldFlipMirror == true && isRightHand のとき:
     outUV.x = 1.0 - outUV.x
   → 右手側（UV x >= 0.5）でX反転

5. [Hide]
   isLeftOnly == true && isRightHand のとき: outUV.x = -1.0
   isRightOnly == true && !isRightHand のとき: outUV.x = -1.0
   → 範囲外に飛ばして非表示化

6. [Rotate]
   outUV = (outUV - uv_ST.zw) / uv_ST.xy   ← Offset/Scaleを一時除去
   outUV = lilRotateUV(outUV, angle)         ← 中心(0.5, 0.5)を軸に回転
   outUV = outUV * uv_ST.xy + uv_ST.zw      ← 再適用
```

アニメーション版（`uv_SR` を受け取るオーバーロード）では、時間を加味して以下を計算してから上記に渡します：

```hlsl
uv_ST2 = uv_ST + float4(0,0, uv_SR.xy) * LIL_TIME  // Offsetにスクロール加算
angle2 = uv_SR.z + uv_SR.w * LIL_TIME               // 角度に回転速度加算
```

`_Main2ndTexAngle` は `lilGetSubTex` 内で `uv_SR.z` を上書きする形で渡されます：

```hlsl
float4 uv_SR2 = float4(uv_SR.xy, angle, uv_SR.w);
// → (scrollX, scrollY, _Main2ndTexAngle, rotateSpeed)
``` [3](#5-2) 

---

## デカール境界クリッピング

`isDecal == true` のとき、UV が 0〜1 の範囲外のピクセルを透明にします：

```hlsl
if(isDecal) outCol.a *= lilIsIn0to1(uv2, saturate(nv - 0.05));
```

`nv`（法線と視線の内積）を使って裏面では徐々に透明になります（`nv < 0.05` で完全透明）。 [4](#5-3) 

---

## アトラスアニメーション（`lilCalcAtlasAnimation`）

`_Main2ndTexDecalAnimation = (columns, rows, totalFrames, fps)` のとき：

```hlsl
// fps == 0 なら手動制御（totalFrames を直接フレーム番号として使用）
animTime = fps == 0 ? (uint)totalFrames : (uint)(LIL_TIME * fps) % (uint)totalFrames;

// decalSubParam = (scaleX, scaleY, centerBlend, ?)
outuv = lerp(float2(uv.x, 1.0-uv.y), 0.5, decalSubParam.z);  // 中心ブレンド
outuv = (outuv + float2(offsetX, offsetY)) * decalSubParam.xy / decalAnimation.xy;
``` [5](#5-4) 

---

## `LIL_GET_SUBTEX` マクロ（呼び出し規約）

フラグメントシェーダーからは以下のマクロで呼び出されます：

```hlsl
#define LIL_GET_SUBTEX(tex, uv) \
    lilGetSubTex(
        tex,
        tex##_ST,
        tex##_ScrollRotate,
        tex##Angle,
        uv,
        fd.nv,
        tex##IsDecal,
        tex##IsLeftOnly,
        tex##IsRightOnly,
        tex##ShouldCopy,
        tex##ShouldFlipMirror,
        tex##ShouldFlipCopy,
        tex##IsMSDF,
        fd.isRightHand,
        tex##DecalAnimation,
        tex##DecalSubParam
        LIL_SAMP_IN(sampler##tex))
``` [6](#5-5) 

---

## 拡張シェーダー作成時のポイント

- プロパティ名のプレフィックスを `_Main4thTex` 等に変えて上記セットをそのまま複製する
- `LIL_GET_SUBTEX(tex, uv)` マクロはプレフィックスを `##` で展開するため、**プロパティ名の命名規則（`texAngle`, `texIsDecal`, `tex_ST`, `tex_ScrollRotate` 等）を厳守**する必要があります
- `LIL_FEATURE_DECAL` と `LIL_FEATURE_ANIMATE_DECAL` のキーワードが有効でないと `lilCalcDecalUV` / `lilCalcAtlasAnimation` が呼ばれないため、カスタムシェーダーの `#pragma shader_feature` にこれらを追加する必要があります
- `isRightHand` は `fd.isRightHand`（フラグメントデータ構造体のメンバ）から取得します。これは UV の x 座標が 0.5 未満かどうかで判定されます

### Citations

**File:** Assets/lilToon/Shader/lts_overlay_one.shader (L56-81)
```text
                        _Main2ndTex                 ("Texture", 2D) = "white" {}
        [lilAngle]      _Main2ndTexAngle            ("sAngle", Float) = 0
        [lilUVAnim]     _Main2ndTex_ScrollRotate    ("sScrollRotates", Vector) = (0,0,0,0)
        [lilEnum]       _Main2ndTex_UVMode          ("UV Mode|UV0|UV1|UV2|UV3|MatCap", Int) = 0
        [lilEnum]       _Main2ndTex_Cull            ("sCullModes", Int) = 0
        [lilDecalAnim]  _Main2ndTexDecalAnimation   ("sDecalAnimations", Vector) = (1,1,1,30)
        [lilDecalSub]   _Main2ndTexDecalSubParam    ("sDecalSubParams", Vector) = (1,1,0,1)
        [lilToggle]     _Main2ndTexIsDecal          ("sAsDecal", Int) = 0
        [lilToggle]     _Main2ndTexIsLeftOnly       ("Left Only", Int) = 0
        [lilToggle]     _Main2ndTexIsRightOnly      ("Right Only", Int) = 0
        [lilToggle]     _Main2ndTexShouldCopy       ("Copy", Int) = 0
        [lilToggle]     _Main2ndTexShouldFlipMirror ("Flip Mirror", Int) = 0
        [lilToggle]     _Main2ndTexShouldFlipCopy   ("Flip Copy", Int) = 0
        [lilToggle]     _Main2ndTexIsMSDF           ("sAsMSDF", Int) = 0
        [NoScaleOffset] _Main2ndBlendMask           ("Mask", 2D) = "white" {}
        [lilEnum]       _Main2ndTexBlendMode        ("sBlendModes", Int) = 0
        [lilEnum]       _Main2ndTexAlphaMode        ("sAlphaModes", Int) = 0
                        _Main2ndEnableLighting      ("sEnableLighting", Range(0, 1)) = 1
                        _Main2ndDissolveMask        ("Dissolve Mask", 2D) = "white" {}
                        _Main2ndDissolveNoiseMask   ("Dissolve Noise Mask", 2D) = "gray" {}
        [lilUVAnim]     _Main2ndDissolveNoiseMask_ScrollRotate ("Scroll", Vector) = (0,0,0,0)
                        _Main2ndDissolveNoiseStrength ("Dissolve Noise Strength", float) = 0.1
        [lilHDR]        _Main2ndDissolveColor       ("sColor", Color) = (1,1,1,1)
        [lilDissolve]   _Main2ndDissolveParams      ("sDissolveParams", Vector) = (0,0,0.5,0.1)
        [lilDissolveP]  _Main2ndDissolvePos         ("Dissolve Position", Vector) = (0,0,0,0)
        [lilFFFB]       _Main2ndDistanceFade        ("sDistanceFadeSettings", Vector) = (0.1,0.01,0,0)
```

**File:** Assets/lilToon/Shader/Includes/lil_common_functions.hlsl (L472-531)
```text
// Decal
float2 lilCalcDecalUV(
    float2 uv,
    float4 uv_ST,
    float angle,
    bool isLeftOnly,
    bool isRightOnly,
    bool shouldCopy,
    bool shouldFlipMirror,
    bool shouldFlipCopy,
    bool isRightHand)
{
    float2 outUV = uv;

    // Copy
    if(shouldCopy) outUV.x = abs(outUV.x - 0.5) + 0.5;

    // Scale & Offset
    outUV = outUV * uv_ST.xy + uv_ST.zw;

    // Flip
    if(shouldFlipCopy && uv.x<0.5) outUV.x = 1.0 - outUV.x;
    if(shouldFlipMirror && isRightHand) outUV.x = 1.0 - outUV.x;

    // Hide
    if(isLeftOnly && isRightHand) outUV.x = -1.0;
    if(isRightOnly && !isRightHand) outUV.x = -1.0;

    // Rotate
    outUV = (outUV - uv_ST.zw) / uv_ST.xy;
    outUV = lilRotateUV(outUV, angle);
    outUV = outUV * uv_ST.xy + uv_ST.zw;

    return outUV;
}

float2 lilCalcDecalUV(
    float2 uv,
    float4 uv_ST,
    float4 uv_SR,
    bool isLeftOnly,
    bool isRightOnly,
    bool shouldCopy,
    bool shouldFlipMirror,
    bool shouldFlipCopy,
    bool isRightHand)
{
    float4 uv_ST2 = uv_ST + float4(0,0,uv_SR.xy) * LIL_TIME;
    float angle2 = uv_SR.z+ uv_SR.w * LIL_TIME;
    return lilCalcDecalUV(
        uv,
        uv_ST2,
        angle2,
        isLeftOnly,
        isRightOnly,
        shouldCopy,
        shouldFlipMirror,
        shouldFlipCopy,
        isRightHand);
}
```

**File:** Assets/lilToon/Shader/Includes/lil_common_functions.hlsl (L533-547)
```text
float2 lilCalcAtlasAnimationAtAnimTime(float2 uv, float4 decalAnimation, float4 decalSubParam, uint animTime)
{
    float2 outuv = lerp(float2(uv.x, 1.0-uv.y), 0.5, decalSubParam.z);
    uint offsetX = animTime % (uint)decalAnimation.x;
    uint offsetY = animTime / (uint)decalAnimation.x;
    outuv = (outuv + float2(offsetX,offsetY)) * decalSubParam.xy / decalAnimation.xy;
    outuv.y = 1.0-outuv.y;
    return outuv;
}

float2 lilCalcAtlasAnimation(float2 uv, float4 decalAnimation, float4 decalSubParam)
{
    uint animTime = decalAnimation.w == 0.0 ? (uint)decalAnimation.z : (uint)(LIL_TIME * decalAnimation.w) % (uint)decalAnimation.z;
    return lilCalcAtlasAnimationAtAnimTime(uv, decalAnimation, decalSubParam, animTime);
}
```

**File:** Assets/lilToon/Shader/Includes/lil_common_functions.hlsl (L746-749)
```text
        float4 outCol = LIL_SAMPLE_2D(tex,samp,uv2samp);
        if(isMSDF) outCol = float4(1.0, 1.0, 1.0, lilMSDF(outCol.rgb));
        if(isDecal) outCol.a *= lilIsIn0to1(uv2, saturate(nv-0.05));
        return outCol;
```

**File:** Assets/lilToon/Shader/Includes/lil_common_macro.hlsl (L2341-2341)
```text
    #define LIL_GET_SUBTEX(tex,uv)  lilGetSubTex(tex, tex##_ST, tex##_ScrollRotate, tex##Angle, uv, fd.nv, tex##IsDecal, tex##IsLeftOnly, tex##IsRightOnly, tex##ShouldCopy, tex##ShouldFlipMirror, tex##ShouldFlipCopy, tex##IsMSDF, fd.isRightHand, tex##DecalAnimation, tex##DecalSubParam LIL_SAMP_IN(sampler##tex))
```
