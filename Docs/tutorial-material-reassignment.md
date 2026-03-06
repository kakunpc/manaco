---
title: "対応マテリアルを割り当てる（マテリアル再割当て）"
sidebar_label: "対応マテリアルを割り当てる（マテリアル再割当て）"
sidebar_position: 3
slug: /tutorial/material-reassignment
---

## 目的
このチュートリアルでは、MANACO対応のマテリアルをアバターに適応する方法を説明します。  
初めてMANACOを触る場合は、このページから始めてください。

![このチュートリアルで最終的にできる状態の完成イメージ](../Images/set-material-sample.png)

## はじめる前に

- MANACOのインストールが終わっている
- MANACO対応のマテリアルデータを用意している
    - MANACO対応商品を探す - [booth](https://booth.pm/ja/search/MANACO%E5%AF%BE%E5%BF%9C)

まだインストールが終わっていない場合は、先に [インストール方法](/install) を確認してください。

## 手順1: MANACOを追加する

1. Hierarchyで対象アバターを右クリック
2. `ちゃとらとりー/Manaco(まなこ)` を実行
3. 生成された `Manaco` オブジェクトを選択

![Hierarchy 右クリックメニューに `ちゃとらとりー/Manaco(まなこ)` が見えている状態](../Images/install-manaco-check.png)

## 手順2: モードを確認する

1. `モード` が `マテリアル割当` になっていることを確認

![Inspector](../Images/manaco-inspector-material.png)

## 手順3: 左右の目領域を設定する

1. `アバタープリセット` を `【Other】その他のアバター` に設定
2. 未割り当ての箇所を割り当てるためのボタンが表示されるので、順番に設定する
![Inspectorの状態](../Images/set-uv-island.png)

3. 選ぶを押すとUVエディタが開くので、対象の目のUV Islandを選択する
   - 左クリック: 追加
   - 右クリック: 削除

![UVエディタで目のUV Islandを選択している状態](../Images/set-uv-island2.png)

## 手順4: マテリアルを適用する

1. `カスタムマテリアル` で適用したいものを選択
2. 各目のマテリアル割り当てが想定どおりか確認

## 手順5: 見た目を確認する

1. 必要なら `NDMF Preview` を有効化して見た目を確認
2. 問題なければVRChatへアップロード

![NDMF Preview](../Images/set-material-sample.png)

## 次に読むページ

別アバターの目表現を参考にしたい場合は、[他アバターの目の割当](/tutorial/copy-eye-assignment) に進んでください。
