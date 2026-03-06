---
title: "マテリアル再割当て"
sidebar_label: "マテリアル再割当て"
sidebar_position: 3
slug: /tutorial/material-reassignment
---

## 目的
このチュートリアルでは、MANACOを使って「目マテリアルを適用してアップロード準備する」手順を説明します。  
初めてMANACOを触る場合は、このページから始めてください。

> [こういう画像を配置: このチュートリアルで最終的にできる状態の完成イメージ]

## はじめる前に

- MANACOのインストールが終わっている
- 対象アバターをUnityで開いている
- 適用したい `Manaco Material Definition` を用意している

まだインストールが終わっていない場合は、先に [インストール方法](/install) を確認してください。

## 手順1: MANACOを追加する

1. Hierarchyで対象アバターを右クリック
2. `ちゃとらとりー/Manaco(まなこ)` を実行
3. 生成された `Manaco` オブジェクトを選択

> [こういう画像を配置: Hierarchy の右クリックから MANACO を追加する場所]

## 手順2: モードを確認する

1. `Mode` が `EyeMaterialAssignment` になっていることを確認
2. `Advanced Settings` を開く

> [こういう画像を配置: Inspector 上部の `Mode` と `Advanced Settings`]

## 手順3: 左右の目領域を設定する

1. 左目・右目に使う `EyeRegion` を設定
2. `UV エディタを開く` から各目のUV Islandを選択
   - 左クリック: 追加
   - 右クリック: 削除

> [こういう画像を配置: UV エディタ上で目の領域を選択している画面]

## 手順4: マテリアルを適用する

1. `Custom Material` で適用したい `Manaco Material Definition` を選択
2. 各目のマテリアル割り当てが想定どおりか確認

> [こういう画像を配置: `Custom Material` を選択して反映された Inspector]

## 手順5: 見た目を確認する

1. 必要なら `NDMF Preview` を有効化して見た目を確認
2. 問題なければVRChatへアップロード

> [こういう画像を配置: NDMF Preview で反映結果を確認している画面]

## 迷いやすいポイント

- この手順では `Avatar Preset` を使わず、対象アバターごとに `EyeRegion` を手動設定します。
- 瞳孔も分けて設定したい場合は、必要に応じて `LeftPupil` / `RightPupil` も追加で調整します。

## 次に読むページ

別アバターの目表現を参考にしたい場合は、[他アバターの目の割当](/tutorial/copy-eye-assignment) に進んでください。
