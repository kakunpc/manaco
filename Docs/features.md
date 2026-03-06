---
title: 特徴
sidebar_position: 1
slug: /features
---

## MANACOとは
MANACO（Material Assign Non-destructive Assistant for Customization Operations）は、VRChatアバターの目のマテリアル再割り当てを非破壊で行うためのUnity Editor拡張です。  
NDMF（Modular Avatar）ビルドフローに統合され、元メッシュを直接書き換えずに目領域へマテリアル適用や目テクスチャ抽出を行えます。

## 主な機能

### 1. 目領域への非破壊マテリアル割り当て
- `EyeMaterialAssignment` モードで左目・右目（必要に応じて瞳孔）に個別マテリアルを割り当て
- UV Islandを使って目ポリゴンを指定し、ビルド時に専用SubMeshへ分離
- 元メッシュは複製して処理するため、編集時の破壊的変更を回避

### 2. 別アバターから目テクスチャをコピー
- `CopyEyeFromAvatar` モードでコピー元アバターを参照
- コピー元の目UV領域を抽出し、コピー先の目へ反映
- アバター改変の移植作業を効率化

### 3. プリセット運用
- `ManacoPreset` によりアバター別の目領域定義を再利用
- `Manaco Material Definition` でマテリアル定義をアセット化して配布可能
- アセット配布ベースなので、配布先で扱いやすい構成

### 4. UVエディタ
- テクスチャプレビュー上でUV Islandを選択
- 左クリックで追加、右クリックで削除
- 選択領域をSceneView側でも確認しながら調整可能

### 5. NDMF統合
- NDMFのGeneratingフェーズで処理を実行
- Modular Avatar処理後に適用される構成
- NDMF Preview連携（設定で有効化可能）

## 想定ユースケース
- 既存アバターに目シェーダーを導入したい
- 別アバターの目表現をコピーして再利用したい
