---
title: "マテリアル再割当て"
sidebar_label: "マテリアル再割当て"
sidebar_position: 3
slug: /tutorial/material-reassignment
---

## 目的
このチュートリアルでは、MANACOを使って「目マテリアルを適用してアップロード準備する」手順を説明します。

## 手順

1. Hierarchyで対象アバターを右クリック
2. `ちゃとらとりー/Manaco(まなこ)` を実行
3. 生成された `Manaco` オブジェクトを選択
4. `Mode` が `EyeMaterialAssignment` になっていることを確認
5. `Advanced Settings` を開き、左目・右目に使う `EyeRegion` を設定
6. `UV エディタを開く` から各目のUV Islandを選択
   - 左クリック: 追加
   - 右クリック: 削除
7. `Custom Material` で適用したい `Manaco Material Definition` を選択
8. 各目のマテリアル割り当てが想定どおりか確認
9. 必要なら `NDMF Preview` を有効化して見た目を確認
10. 問題なければVRChatへアップロード

## ポイント

- この手順では `Avatar Preset` を使わず、対象アバターごとに `EyeRegion` を手動設定します。
- 瞳孔も分けて設定したい場合は、必要に応じて `LeftPupil` / `RightPupil` も追加で調整します。
