//----------------------------------------------------------------------------------------------------------------------
// lilToon のビルド最適化が LIL_FEATURE_DECAL を除外しても、このカスタムシェーダーでは常にデカールUVクリッピングが必要なため強制定義する。
// 未定義のまま lilGetSubTex が呼ばれると #else ブランチ（lilIsIn0to1 なし）へ落ちてタイリングが発生する。
#ifndef LIL_FEATURE_DECAL
    #define LIL_FEATURE_DECAL
#endif

//----------------------------------------------------------------------------------------------------------------------
// Macro

// Custom variables
// SimpleDecalEx: lilToon の lilGetSubTex デカール仕様のセットを 7 枚ぶん複製する。
// 配置は Position(中心UV) と Scale(UVサイズ) で持ち、合成時に _ST を組み立てる。
// ミラー/コピー/左右限定は _DecalNMirror の単一 enum にまとめ、合成時にフラグへ展開する。
#define LIL_CUSTOM_PROPERTIES \
    float4 _Decal1Color;    \
    float  _Decal1PosX;     \
    float  _Decal1PosY;     \
    float  _Decal1ScaleX;   \
    float  _Decal1ScaleY;   \
    float  _Decal1Angle;    \
    uint   _Decal1Enable;   \
    uint   _Decal1BlendMode;\
    uint   _Decal1UVMode;   \
    uint   _Decal1Cull;     \
    uint   _Decal1Mirror;   \
    float4 _Decal2Color;    \
    float  _Decal2PosX;     \
    float  _Decal2PosY;     \
    float  _Decal2ScaleX;   \
    float  _Decal2ScaleY;   \
    float  _Decal2Angle;    \
    uint   _Decal2Enable;   \
    uint   _Decal2BlendMode;\
    uint   _Decal2UVMode;   \
    uint   _Decal2Cull;     \
    uint   _Decal2Mirror;   \
    float4 _Decal3Color;    \
    float  _Decal3PosX;     \
    float  _Decal3PosY;     \
    float  _Decal3ScaleX;   \
    float  _Decal3ScaleY;   \
    float  _Decal3Angle;    \
    uint   _Decal3Enable;   \
    uint   _Decal3BlendMode;\
    uint   _Decal3UVMode;   \
    uint   _Decal3Cull;     \
    uint   _Decal3Mirror;   \
    float4 _Decal4Color;    \
    float  _Decal4PosX;     \
    float  _Decal4PosY;     \
    float  _Decal4ScaleX;   \
    float  _Decal4ScaleY;   \
    float  _Decal4Angle;    \
    uint   _Decal4Enable;   \
    uint   _Decal4BlendMode;\
    uint   _Decal4UVMode;   \
    uint   _Decal4Cull;     \
    uint   _Decal4Mirror;   \
    float4 _Decal5Color;    \
    float  _Decal5PosX;     \
    float  _Decal5PosY;     \
    float  _Decal5ScaleX;   \
    float  _Decal5ScaleY;   \
    float  _Decal5Angle;    \
    uint   _Decal5Enable;   \
    uint   _Decal5BlendMode;\
    uint   _Decal5UVMode;   \
    uint   _Decal5Cull;     \
    uint   _Decal5Mirror;   \
    float4 _Decal6Color;    \
    float  _Decal6PosX;     \
    float  _Decal6PosY;     \
    float  _Decal6ScaleX;   \
    float  _Decal6ScaleY;   \
    float  _Decal6Angle;    \
    uint   _Decal6Enable;   \
    uint   _Decal6BlendMode;\
    uint   _Decal6UVMode;   \
    uint   _Decal6Cull;     \
    uint   _Decal6Mirror;   \
    float4 _Decal7Color;    \
    float  _Decal7PosX;     \
    float  _Decal7PosY;     \
    float  _Decal7ScaleX;   \
    float  _Decal7ScaleY;   \
    float  _Decal7Angle;    \
    uint   _Decal7Enable;   \
    uint   _Decal7BlendMode;\
    uint   _Decal7UVMode;   \
    uint   _Decal7Cull;     \
    uint   _Decal7Mirror;   \
    float  _DecalMipBias;             \
    float4 _SDEXMatCapColor;          \
    uint   _SDEXMatCapEnable;         \
    uint   _SDEXMatCapBlendMode;      \
    float  _SDEXMatCapOpacity;        \
    float  _SDEXMatCapMainStrength;   \
    float  _SDEXMatCapShadowMask;     \
    float  _SDEXMatCapEnableLighting; \
    uint   _SDEXAlphaOverrideEnable;  \
    float  _SDEXAlphaOverrideStrength;

// Custom textures
// SimpleDecalEx: デカール 7 枚ぶんのテクスチャ。SAMPLER は新規に持たず、
// lilToon 共有の lil_sampler_linear_repeat を流用してサンプラー数の上限を回避する。
#define LIL_CUSTOM_TEXTURES \
    TEXTURE2D(_Decal1Tex); \
    TEXTURE2D(_Decal2Tex); \
    TEXTURE2D(_Decal3Tex); \
    TEXTURE2D(_Decal4Tex); \
    TEXTURE2D(_Decal5Tex); \
    TEXTURE2D(_Decal6Tex); \
    TEXTURE2D(_Decal7Tex); \
    TEXTURE2D(_SDEXMatCapTex);

//----------------------------------------------------------------------------------------------------------------------
// SimpleDecalEx: デカール合成マクロ
//
// _ST の組み立て:
//   テクスチャの [0,1] をメッシュUVの「中心 pos / サイズ scale」の領域へ写す。
//   tex_uv = mesh_uv / scale + (0.5 - pos / scale)  ->  _ST = float4(1/scale, 0.5 - pos/scale)
//   既定 (pos=0.5, scale=1) では _ST = (1,1,0,0) となり、変換なしでUV全面に一致する。
//
// 回転中心の修正:
//   lilCalcDecalUV が行う回転はメッシュUV (0.5,0.5) 中心であり、デカール位置中心ではない。
//   そのためデカールが中心以外にある場合、回転させると位置が大きくずれる。
//   対策: lilGetSubTex に angle=0 を渡し、事前に sdexUV を sdexPos 中心で回転させる。
//   これにより tex_uv = rotate(angle) * (uv - pos) / scale + 0.5 の正しい変換が得られる。
//
// _DecalNMirror（排他選択）-> lilCalcDecalUV のフラグへの展開:
//   0:None / 1:Left Only / 2:Right Only / 3:Symmetry Copy / 4:Symmetry Copy(Flip) / 5:Flip on Mirror
//
// 合成先は fd.col ではなく fd.albedo。lilToon は lil_pass_forward_*.hlsl で
//   `fd.albedo = fd.col.rgb;` の後に BEFORE_SHADOW を実行し、続く OVERRIDE_SHADOW で
//   `fd.col.rgb` を albedo から再構築するため、fd.col に書くと陰影計算で上書きされる。
//   albedo に合成することでデカールは表面と同じ陰影を受けつつ確実に反映される。
#define LIL_SDEX_APPLY(idx) \
    if(_Decal##idx##Enable) \
    { \
        float2 sdexUV = fd.uv0; \
        uint sdexUVMode = _Decal##idx##UVMode; \
        if(sdexUVMode == 1) sdexUV = fd.uv1; \
        else if(sdexUVMode == 2) sdexUV = fd.uv2; \
        else if(sdexUVMode == 3) sdexUV = fd.uv3; \
        float2 sdexScale = max(abs(float2(_Decal##idx##ScaleX, _Decal##idx##ScaleY)), 1e-4); \
        float2 sdexPos   = float2(_Decal##idx##PosX, _Decal##idx##PosY); \
        uint sdexMir = _Decal##idx##Mirror; \
        bool sdexIsPixelRight = (sdexUV.x >= 0.5); \
        bool sdexShow = true; \
        if(sdexMir == 1 && sdexIsPixelRight) sdexShow = false; \
        if(sdexMir == 2 && !sdexIsPixelRight) sdexShow = false; \
        if(sdexShow) \
        { \
            bool sdexDecalOnRight = (sdexPos.x >= 0.5); \
            bool sdexIsCopy = (sdexMir == 3 || sdexMir == 4) && (sdexDecalOnRight != sdexIsPixelRight); \
            float2 sdexMappedUV = sdexIsCopy ? float2(1.0 - sdexUV.x, sdexUV.y) : sdexUV; \
            float sdexActiveAngle = _Decal##idx##Angle; \
            bool sdexFlipX = false; \
            if(sdexMir == 4 && sdexIsCopy) \
            { \
                sdexFlipX = true; \
            } \
            else if(sdexMir == 5 && fd.isRightHand) \
            { \
                sdexActiveAngle = -sdexActiveAngle; \
                sdexFlipX = true; \
            } \
            float sdexCosA, sdexSinA; \
            sincos(sdexActiveAngle, sdexCosA, sdexSinA); \
            float2 sdexDelta = sdexMappedUV - sdexPos; \
            float2 sdexRotDelta = float2(sdexDelta.x * sdexCosA - sdexDelta.y * sdexSinA, \
                                         sdexDelta.x * sdexSinA + sdexDelta.y * sdexCosA); \
            float2 sdexLocalUV = sdexRotDelta / sdexScale; \
            if(sdexFlipX) sdexLocalUV.x = -sdexLocalUV.x; \
            float2 sdexFinalUV = sdexLocalUV + 0.5; \
            bool sdexInRange = all(sdexFinalUV >= 0.0) && all(sdexFinalUV <= 1.0); \
            float4 sdexCol = sdexInRange \
                ? _Decal##idx##Color * _Decal##idx##Tex.SampleBias(lil_sampler_linear_repeat, sdexFinalUV, _DecalMipBias) \
                : float4(0, 0, 0, 0); \
            if((_Decal##idx##Cull == 1 && fd.facing > 0) || (_Decal##idx##Cull == 2 && fd.facing < 0)) sdexCol.a = 0.0; \
            fd.albedo = lilBlendColor(fd.albedo, sdexCol.rgb, sdexCol.a, _Decal##idx##BlendMode); \
            sdexCoverage = max(sdexCoverage, sdexCol.a); \
        } \
    }

// Add vertex shader input
//#define LIL_REQUIRE_APP_POSITION
//#define LIL_REQUIRE_APP_TEXCOORD0
//#define LIL_REQUIRE_APP_TEXCOORD1
//#define LIL_REQUIRE_APP_TEXCOORD2
//#define LIL_REQUIRE_APP_TEXCOORD3
//#define LIL_REQUIRE_APP_TEXCOORD4
//#define LIL_REQUIRE_APP_TEXCOORD5
//#define LIL_REQUIRE_APP_TEXCOORD6
//#define LIL_REQUIRE_APP_TEXCOORD7
//#define LIL_REQUIRE_APP_COLOR
//#define LIL_REQUIRE_APP_NORMAL
//#define LIL_REQUIRE_APP_TANGENT
//#define LIL_REQUIRE_APP_VERTEXID

// Add vertex shader output
//#define LIL_V2F_FORCE_TEXCOORD0
//#define LIL_V2F_FORCE_TEXCOORD1
//#define LIL_V2F_FORCE_POSITION_OS
//#define LIL_V2F_FORCE_POSITION_WS
//#define LIL_V2F_FORCE_POSITION_SS
//#define LIL_V2F_FORCE_NORMAL
//#define LIL_V2F_FORCE_TANGENT
//#define LIL_V2F_FORCE_BITANGENT
//#define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7)

// Add vertex copy
// ジオメトリシェーダーを使う場合に定義する
// appdataCopy 型と appdataOriginalToCopy() 関数が生成され、
// vertCustom() / geomCustom() を custom_insert_post.hlsl で定義できるようになる
// 不要な場合はコメントアウト: //#define LIL_CUSTOM_VERT_COPY
#define LIL_CUSTOM_VERT_COPY

// Inserting a process into the vertex shader
// LIL_CUSTOM_VERTEX_OS: オブジェクト空間で処理する（positionOS の変形など）
//   使用可能な変数: inout appdata input, inout float2 uvMain, inout float4 positionOS
// LIL_CUSTOM_VERTEX_WS: ワールド空間で処理する
//   使用可能な変数: inout appdata input, inout float2 uvMain,
//                  inout lilVertexPositionInputs vertexInput,
//                  inout lilVertexNormalInputs vertexNormalInput
//#define LIL_CUSTOM_VERTEX_OS
//#define LIL_CUSTOM_VERTEX_WS

// Inserting a process into pixel shader
// BEFORE_xx : 指定処理の直前に割り込む  例: #define BEFORE_EMISSION_1ST fd.emissionColor.rgb *= 2.0;
// OVERRIDE_xx: 指定処理を完全に上書きする  例: #define OVERRIDE_OUTPUT return float4(fd.col.rgb, 1.0);
// xx に入るキーワード（処理順）:
//   UNPACK_V2F / ANIMATE_MAIN_UV / ANIMATE_OUTLINE_UV / PARALLAX / MAIN / OUTLINE_COLOR /
//   FUR / ALPHAMASK / DISSOLVE / NORMAL_1ST / NORMAL_2ND / ANISOTROPY / AUDIOLINK /
//   MAIN2ND / MAIN3RD / SHADOW / BACKLIGHT / REFRACTION / REFLECTION /
//   MATCAP / MATCAP_2ND / RIMLIGHT / GLITTER / EMISSION_1ST / EMISSION_2ND /
//   DISSOLVE_ADD / BLEND_EMISSION / DISTANCE_FADE / FOG / OUTPUT
// ピクセルシェーダー内では lilFragData fd のメンバーを読み書きする（下記リファレンス参照）
//#define BEFORE_xx
//#define OVERRIDE_xx

// SimpleDecalEx: albedo 確定後・陰影計算の直前（BEFORE_SHADOW）で 7 枚のデカールを
// fd.albedo に合成する。これによりメインカラー処理の後段に入り、かつ陰影で上書きされない。
// 併せて、全デカールの可視アルファの和集合 sdexCoverage を蓄積し、後段の MatCap マスクに使う。
// sdexCoverage は同一フラグメント関数内に展開される後続フック（BEFORE_EMISSION_1ST）から参照する。
#define BEFORE_SHADOW \
    float sdexCoverage = 0.0; \
    LIL_SDEX_APPLY(1) \
    LIL_SDEX_APPLY(2) \
    LIL_SDEX_APPLY(3) \
    LIL_SDEX_APPLY(4) \
    LIL_SDEX_APPLY(5) \
    LIL_SDEX_APPLY(6) \
    LIL_SDEX_APPLY(7) \
    if(_SDEXAlphaOverrideEnable) fd.col.a = lerp(fd.col.a, max(fd.col.a, sdexCoverage), sdexCoverage * _SDEXAlphaOverrideStrength);

// SimpleDecalEx: MatCap 合成。
// フック選定理由（BEFORE_EMISSION_1ST）:
//   - 通常パス / lite パスの双方で陰影計算（OVERRIDE_SHADOW）より後に実行される唯一の共通点。
//     （lite では BEFORE_MATCAP が陰影より前に来るため使えない）
//   - 両パスとも #ifndef LIL_PASS_FORWARDADD の内側にあり、加算ライトパスでは実行されない＝二重合成しない。
//   - BEFORE_SHADOW で宣言した sdexCoverage が関数スコープで参照可能。
// 仕様:
//   - 既定はオーバーレイ（陰影非依存）。fd.col へ直接 lilBlendColor で重ねる。
//   - Enable Lighting=1 で fd.lightColor を乗じて陰影に追従。
//   - Shadow Mask=1 で fd.shadowmix（影=0,非影=1）により影部分を減衰。
//   - 適用範囲は sdexCoverage（全デカール領域の和集合）に限定。
#define BEFORE_EMISSION_1ST \
    if(_SDEXMatCapEnable && sdexCoverage > 0.0) \
    { \
        float3 sdexMat = _SDEXMatCapColor.rgb * LIL_SAMPLE_2D(_SDEXMatCapTex, lil_sampler_linear_repeat, fd.uvMat).rgb; \
        sdexMat = lerp(sdexMat, sdexMat * fd.albedo, _SDEXMatCapMainStrength); \
        sdexMat = lerp(sdexMat, sdexMat * fd.lightColor, _SDEXMatCapEnableLighting); \
        float sdexMatMask = sdexCoverage * _SDEXMatCapColor.a * _SDEXMatCapOpacity * lerp(1.0, fd.shadowmix, _SDEXMatCapShadowMask); \
        fd.col.rgb = lilBlendColor(fd.col.rgb, sdexMat, sdexMatMask, _SDEXMatCapBlendMode); \
    }

//----------------------------------------------------------------------------------------------------------------------
// Information about variables
//----------------------------------------------------------------------------------------------------------------------

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader inputs (appdata structure)
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   input.positionOS        POSITION
// float2   input.uv0               TEXCOORD0
// float2   input.uv1               TEXCOORD1
// float2   input.uv2               TEXCOORD2
// float2   input.uv3               TEXCOORD3
// float2   input.uv4               TEXCOORD4
// float2   input.uv5               TEXCOORD5
// float2   input.uv6               TEXCOORD6
// float2   input.uv7               TEXCOORD7
// float4   input.color             COLOR
// float3   input.normalOS          NORMAL
// float4   input.tangentOS         TANGENT
// uint     vertexID                SV_VertexID

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader outputs or pixel shader inputs (v2f structure)
//
// The structure depends on the pass.
// Please check lil_pass_xx.hlsl for details.
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   output.positionCS       SV_POSITION
// float2   output.uv01             TEXCOORD0 TEXCOORD1
// float2   output.uv23             TEXCOORD2 TEXCOORD3
// float3   output.positionOS       object space position
// float3   output.positionWS       world space position
// float3   output.normalWS         world space normal
// float4   output.tangentWS        world space tangent

//----------------------------------------------------------------------------------------------------------------------
// Variables commonly used in the forward pass
//
// These are members of `lilFragData fd`
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   col                     lit color
// float3   albedo                  unlit color
// float3   emissionColor           color of emission
// -------- ----------------------- --------------------------------------------------------------------
// float3   lightColor              color of light
// float3   indLightColor           color of indirectional light
// float3   addLightColor           color of additional light
// float    attenuation             attenuation of light
// float3   invLighting             saturate((1.0 - lightColor) * sqrt(lightColor));
// -------- ----------------------- --------------------------------------------------------------------
// float2   uv0                     TEXCOORD0
// float2   uv1                     TEXCOORD1
// float2   uv2                     TEXCOORD2
// float2   uv3                     TEXCOORD3
// float2   uvMain                  Main UV
// float2   uvMat                   MatCap UV
// float2   uvRim                   Rim Light UV
// float2   uvPanorama              Panorama UV
// float2   uvScn                   Screen UV
// bool     isRightHand             input.tangentWS.w > 0.0;
// -------- ----------------------- --------------------------------------------------------------------
// float3   positionOS              object space position
// float3   positionWS              world space position
// float4   positionCS              clip space position
// float4   positionSS              screen space position
// float    depth                   distance from camera
// -------- ----------------------- --------------------------------------------------------------------
// float3x3 TBN                     tangent / bitangent / normal matrix
// float3   T                       tangent direction
// float3   B                       bitangent direction
// float3   N                       normal direction
// float3   V                       view direction
// float3   L                       light direction
// float3   origN                   normal direction without normal map
// float3   origL                   light direction without sh light
// float3   headV                   middle view direction of 2 cameras
// float3   reflectionN             normal direction for reflection
// float3   matcapN                 normal direction for reflection for MatCap
// float3   matcap2ndN              normal direction for reflection for MatCap 2nd
// float    facing                  VFACE
// -------- ----------------------- --------------------------------------------------------------------
// float    vl                      dot(viewDirection, lightDirection);
// float    hl                      dot(headDirection, lightDirection);
// float    ln                      dot(lightDirection, normalDirection);
// float    nv                      saturate(dot(normalDirection, viewDirection));
// float    nvabs                   abs(dot(normalDirection, viewDirection));
// -------- ----------------------- --------------------------------------------------------------------
// float4   triMask                 TriMask (for lite version)
// float3   parallaxViewDirection   mul(tbnWS, viewDirection);
// float2   parallaxOffset          parallaxViewDirection.xy / (parallaxViewDirection.z+0.5);
// float    anisotropy              strength of anisotropy
// float    smoothness              smoothness
// float    roughness               roughness
// float    perceptualRoughness     perceptual roughness
// float    shadowmix               this variable is 0 in the shadow area
// float    audioLinkValue          volume acquired by AudioLink
// -------- ----------------------- --------------------------------------------------------------------
// uint     renderingLayers         light layer of object (for URP / HDRP)
// uint     featureFlags            feature flags (for HDRP)
// uint2    tileIndex               tile index (for HDRP)