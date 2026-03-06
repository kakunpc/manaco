---
title: 使い方
sidebar_position: 4
slug: /usage
---

## 基本操作

### 1. コンポーネント追加
- Hierarchy上のアバターを右クリック
- `ちゃとらとりー/Manaco(まなこ)` を選択
- `Manaco` オブジェクトと `Manaco` コンポーネントが追加される

### 2. モード選択
- `EyeMaterialAssignment`  
  目領域にマテリアルを直接割り当てる通常モード
- `CopyEyeFromAvatar`  
  別アバターから目情報を抽出して反映するモード

### 3. プリセット適用
- `Apply Preset` で `ManacoPreset` を選択
- プリセット内のレンダラー名・マテリアルスロット情報に基づきEyeRegionが展開

### 4. マテリアル適用
- `Apply Material` で `Manaco Material Definition` を選択
- 左目/右目/瞳孔に対応するマテリアルが割り当てられる

## Advanced Settings

### EyeRegion編集
- EyeType（Left/Right/LeftPupil/RightPupil）
- 対象SkinnedMeshRenderer
- マテリアルスロット番号
- UV Island情報
- 個別マテリアル設定

### UVエディタ
- テクスチャ表示上で対象ポリゴンのUVを選択
- 選択結果はEyeRegionの `eyePolygonRegions` に保存

### NDMF Preview
- `useNdmfPreview` のON/OFFでプレビュー連携を切り替え

## アセット作成

### Avatar Preset作成
1. `Manaco` コンポーネント右上メニュー（コンテキスト）を開く
2. `Create Avatar Preset` を実行
3. `.asset` として保存し、配布物に同梱

### Material Definition作成
1. Projectビューで右クリック
2. `Create/ちゃとらとりー/Manaco Material Definition`
3. 表示名と各目マテリアルを設定して保存
