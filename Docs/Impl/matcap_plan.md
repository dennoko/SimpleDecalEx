# SimpleDecalEx — デカール領域限定 MatCap 合成 実装計画

> **ステータス: 実装済み（2026-06-24）。** 合成フックは当初案の `BEFORE_MATCAP` ではなく
> **`BEFORE_EMISSION_1ST`** を採用した（理由は §2 / §4.2 を参照。lite パスでは `BEFORE_MATCAP` が
> 陰影計算より前に来るため使用不可だったため）。デカール UV モードの「MatCap」選択肢は廃止済み。

## 1. 目的

全デカール（有効な 1〜6 枚）の **表示領域の和集合** に限定して、共通の MatCap を 1 枚合成できるようにする。
MatCap には次の調整オプションを持たせる。

- **影部分での無効化（Shadow Mask）**: 影になっている部分で MatCap を減衰／無効化する強度。
- **ライティングの反映（Enable Lighting）**: MatCap をライティング結果に追従させる（陰影を受ける）か、
  ライティング非依存のオーバーレイとして乗せるかの度合い。

lilToon 本体の MatCap 仕様（`fd.uvMat` / `fd.shadowmix` / `lilBlendColor`）に準拠し、自作の挙動を最小化する。

---

## 2. 現状の前提（重要なデータフロー）

`lil_pass_forward_normal.hlsl` / `lil_pass_forward_lite.hlsl` のフラグメント処理順は以下。

```
... MAIN -> MAIN2ND -> MAIN3RD
fd.albedo = fd.col.rgb;     // ← albedo 確定
BEFORE_SHADOW               // ← SimpleDecalEx はここで fd.albedo にデカールを合成中
OVERRIDE_SHADOW             // ← fd.col.rgb を albedo から再構築（陰影計算）。fd.shadowmix もここで確定
... BACKLIGHT -> REFLECTION
BEFORE_MATCAP / OVERRIDE_MATCAP  // ← lilToon 本体の MatCap 合成点（fd.col に対して行う）
... RIMLIGHT -> EMISSION -> OUTPUT
```

ポイント:

- デカール本体は `BEFORE_SHADOW` で **`fd.albedo`** に合成しているため、確実に陰影に乗る（上書きされない）。
- 一方、**「影で無効化」「ライティング反映度」は `fd.shadowmix` と確定後の `fd.col` が必要** なので、
  MatCap の合成は **陰影計算（`OVERRIDE_SHADOW`）より後** に `fd.col` に対して行う必要がある。
- **重要な落とし穴（lite パス）**: `BEFORE_MATCAP` は **通常パスでは陰影より後** だが、
  **lite パスでは陰影より前**（`fd.albedo = fd.col.rgb;` の直前）に実行される。よって `BEFORE_MATCAP` は使えない。
  両パス共通で陰影より後・かつ加算ライトパスで実行されない唯一のフックは **`BEFORE_EMISSION_1ST`**
  （通常・lite とも `#ifndef LIL_PASS_FORWARDADD` の内側、SHADOW 段より後）。これを採用する。
- したがって本機能は **2 段構え** になる:
  1. `BEFORE_SHADOW`: デカール合成のついでに **被覆マスク `sdexCoverage`** を蓄積する（先頭で変数宣言）。
  2. `BEFORE_EMISSION_1ST`: 蓄積した `sdexCoverage` を使い、`fd.col` に MatCap を合成する。

`BEFORE_SHADOW` で宣言したローカル変数は、同一フラグメント関数内へ各フックがインライン展開される
lilToon の構造上、後続の `BEFORE_EMISSION_1ST` からも参照できる（関数スコープのローカル変数として共有）。

---

## 3. 追加プロパティ

`lilCustomShaderProperties.lilblock` に追加（既定インスペクタ用の保険属性のみ。実 UI は手動描画）。

```
[lilToggle]     _SDEXMatCapEnable        ("MatCap Enable", Int) = 0
[NoScaleOffset] _SDEXMatCapTex           ("MatCap Texture", 2D) = "black" {}
                _SDEXMatCapColor         ("MatCap Color", Color) = (1,1,1,1)
[lilEnum]       _SDEXMatCapBlendMode     ("Blend Mode|Normal|Add|Screen|Multiply", Int) = 1   // 既定=Add
                _SDEXMatCapOpacity        ("Opacity", Range(0,1)) = 1                // 全体の不透明度
                _SDEXMatCapMainStrength   ("Main Color Power", Range(0,1)) = 0       // メインカラー(albedo)で染める強度
                _SDEXMatCapShadowMask     ("Shadow Mask Strength", Range(0,1)) = 0   // 1で影では完全に無効
                _SDEXMatCapEnableLighting ("Enable Lighting", Range(0,1)) = 0        // 0=非依存 / 1=陰影追従
```

`custom.hlsl` の `LIL_CUSTOM_PROPERTIES` に対応する cbuffer 変数を追加、
`LIL_CUSTOM_TEXTURES` に `TEXTURE2D(_SDEXMatCapTex);` を追加（サンプラーは既存の
`lil_sampler_linear_repeat` を流用してサンプラー上限を回避）。

---

## 4. シェーダー実装方針（custom.hlsl）

### 4.1 被覆マスクの蓄積（BEFORE_SHADOW 側）

`LIL_SDEX_APPLY(idx)` 内で、デカールの可視アルファ `sdexCol.a`（デカールクリップ・カリング適用後）を
被覆マスクへ積算する。`BEFORE_SHADOW` の先頭で累積変数を宣言しておく。

```hlsl
#define BEFORE_SHADOW \
    float sdexCoverage = 0.0; \
    LIL_SDEX_APPLY(1) \
    ... \
    LIL_SDEX_APPLY(6)
```

`LIL_SDEX_APPLY` 末尾（albedo へのブレンド後）に 1 行追加:

```hlsl
sdexCoverage = max(sdexCoverage, sdexCol.a);   // 和集合 = 各デカール可視アルファの最大値
```

- `max` を採用する理由: 「いずれかのデカールが見えている領域」= 和集合。重なりで 1 を超えない。
- もし「濃さの合計で MatCap を強める」挙動が欲しい場合は `saturate(sdexCoverage + sdexCol.a)` も選択肢。
  既定は `max`（領域マスクとして素直）。

`_SDEXMatCapEnable` が false のときは積算をスキップしてよい（分岐で軽量化）。ただし
`sdexCoverage` 変数自体は常に宣言する（後段が参照するため）。

### 4.2 MatCap 合成（BEFORE_EMISSION_1ST 側）

```hlsl
#define BEFORE_EMISSION_1ST \
    if(_SDEXMatCapEnable && sdexCoverage > 0.0) \
    { \
        float3 sdexMat = _SDEXMatCapColor.rgb * LIL_SAMPLE_2D(_SDEXMatCapTex, lil_sampler_linear_repeat, fd.uvMat).rgb; \
        /* メインカラー反映: 0=テクスチャ色そのまま, 1=表面色(albedo)で完全乗算 */ \
        sdexMat = lerp(sdexMat, sdexMat * fd.albedo, _SDEXMatCapMainStrength); \
        /* ライティング反映: 0=非依存オーバーレイ, 1=陰影(lightColor)追従 */ \
        sdexMat = lerp(sdexMat, sdexMat * fd.lightColor, _SDEXMatCapEnableLighting); \
        /* 影で減衰: shadowmix は影=0, 非影=1 */ \
        float sdexMatMask = sdexCoverage * _SDEXMatCapColor.a * _SDEXMatCapOpacity * lerp(1.0, fd.shadowmix, _SDEXMatCapShadowMask); \
        fd.col.rgb = lilBlendColor(fd.col.rgb, sdexMat, sdexMatMask, _SDEXMatCapBlendMode); \
    }
```

- `fd.uvMat`: lilToon が算出済みの MatCap UV（法線ベース、`lil_pass_forward_normal.hlsl` で無条件代入）。
- `fd.lightColor` / `fd.shadowmix`: SHADOW 段で確定済み（`BEFORE_EMISSION_1ST` 時点で利用可能）。
- 既定はオーバーレイ（`EnableLighting=0`）。`fd.col` へ `lilBlendColor`（Normal/Add/Screen/Multiply）で重ねる。
- 既定 Blend=Add は発光的マットキャップに向く。

### 4.3 ForwardAdd パスの扱い

- `BEFORE_EMISSION_1ST` は通常・lite パスとも `#ifndef LIL_PASS_FORWARDADD` の内側にあるため、
  **加算ライトパスでは実行されない**。よって MatCap の二重合成は構造的に発生せず、追加ガードは不要。
- `sdexCoverage` は全パスの `BEFORE_SHADOW` で宣言・蓄積されるが、ForwardAdd では読み出されないだけ。

---

## 5. インスペクタ実装方針（CustomInspector.cs）

デカール 6 枚のフォールドアウト群の後ろに「MatCap (All Decals)」セクションを 1 つ追加する。

- `MaterialProperty`: matEnable / matTex / matColor / matBlend / matShadowMask / matEnableLighting。
- 既存方針どおり enum（Blend Mode）は `DrawPopup`、トグルは `DrawToggle` で手動描画（lilEnum ドロワー不使用）。
- `matEnable` が false の間はテクスチャ以下を折り畳む（`if(matEnable.floatValue > 0.5f)`）。
- ローカライズは既存の `L(en, ja)` を流用。

UI 例:

```
▼ MatCap (All Decals)
   [x] Enable
   Texture / Color : (Tex) (Color)
   Blend Mode      : Add
   Shadow Mask     : 0.00   ... 影部分での減衰（1で影では非表示）
   Enable Lighting : 0.00   ... 0=ライティング非依存 / 1=陰影に追従
```

---

## 6. エッジケース / 留意点

1. **被覆マスクの共有スコープ**: `sdexCoverage` を `BEFORE_SHADOW` 先頭で宣言し `BEFORE_EMISSION_1ST` で読む。
   lilToon のインライン展開順（SHADOW < EMISSION）に依存するため、将来 lilToon の処理順が変わると破綻し得る。
   custom.hlsl のコメントで依存を明記済み。
2. **MatCap UV の有無**: `fd.uvMat` は `lil_pass_forward_normal.hlsl` で無条件代入、lite では v2f TEXCOORD2 として
   常に存在するため、`LIL_FEATURE_MATCAP` の有無に関わらず利用可能。
3. **Lite 版**: `fd.shadowmix` は lite でも `lilGetShading` 内で代入される。`_UseShadow` オフ時は既定値（≒1）のため
   Shadow Mask を上げても減衰しないが、これは「影が無いマテリアル」として自然な挙動。
4. **デカール UV モードの MatCap 廃止**: 旧 UV モードの「MatCap」選択肢は削除済み。本 MatCap 機能のみで提供する。
5. **透過マテリアル**: MatCap は `fd.col.rgb` のみ変更しアルファは触らない。デカール透過の見え方を変えない。

---

## 7. 作業手順（チェックリスト）

- [x] `lilCustomShaderProperties.lilblock` に MatCap プロパティ 6 種を追加。
- [x] `custom.hlsl` `LIL_CUSTOM_PROPERTIES` / `LIL_CUSTOM_TEXTURES` に変数・テクスチャ追加。
- [x] `LIL_SDEX_APPLY` 末尾に `sdexCoverage = max(...)` を追加。
- [x] `BEFORE_SHADOW` 先頭に `float sdexCoverage = 0.0;` を追加。
- [x] `BEFORE_EMISSION_1ST` を定義して MatCap を `fd.col` に合成（フックが自動で ForwardAdd 除外）。
- [x] `CustomInspector.cs` に MatCap セクション（手動描画）を追加。
- [ ] lilToon Refresh Shader 後、不透明 / 半透明 / Lite / アウトライン / ForwardAdd（追加ライト下）で
      表示・影減衰・ライティング反映・二重合成の有無を確認（Unity 上で要確認）。
