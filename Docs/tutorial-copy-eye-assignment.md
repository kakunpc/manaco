---
title: "他アバターの目の割当"
sidebar_label: "他アバターの目の割当"
sidebar_position: 4
slug: /tutorial/copy-eye-assignment
---

## 目的
このチュートリアルでは、別アバターの目表現を参照しながら、コピー先アバターへ割り当ててアップロード準備するまでの流れを説明します。  
基本操作がまだ不安な場合は、先に [対応マテリアルを割り当てる（マテリアル再割当て）](/tutorial/material-reassignment) を読むのがおすすめです。


![このチュートリアルで最終的にできる状態の完成イメージ](../Images/assign-sample.png)

## はじめる前に

- MANACOのインストールが終わっている
- コピー先アバターをUnityで開いている
- コピー元として参照したいアバターPrefabを用意している

まだインストールが終わっていない場合は、先に [インストール方法](/install) を確認してください。

## 手順1: MANACOを追加する

1. Hierarchyで対象アバターを右クリック
2. `ちゃとらとりー/Manaco(まなこ)` を実行
3. 生成された `Manaco` オブジェクトを選択

> [こういう画像を配置: Hierarchy の右クリックから MANACO を追加する場所]

## 手順2: コピー元を指定する

1. `Mode` を `CopyEyeFromAvatar` に変更
2. `Source Avatar` にコピー元アバターPrefabを指定

> [こういう画像を配置: `Mode` を `CopyEyeFromAvatar` に切り替えて `Source Avatar` を設定する画面]

![このチュートリアルで最終的にできる状態の完成イメージ](../Images/assign-inspector.png)


## 手順3: コピー先の目領域を確認する

1. コピー先アバター側の `Advanced Settings` を開き、適用先の `EyeRegion` を確認または調整
2. 必要に応じて `UV エディタを開く` からコピー先の左右の目領域を調整

> [こういう画像を配置: コピー先側の `Advanced Settings` と UV エディタ]

## 手順4: 反映結果を確認する

1. 反映結果を確認し、必要なら `Custom Material` や各 `EyeRegion` を微調整
2. `NDMF Preview` で見た目を確認
3. 問題なければVRChatへアップロード

> [こういう画像を配置: コピー結果を Preview で確認している画面]

## 注意

- コピー先の設定はプリセットなしでも始められますが、コピー元側の構成によっては追加設定が必要です。
- `CopyEyeFromAvatar` モードでは、コピー元アバターとコピー元プリセットの整合が必要になる場合があります。

## 困ったとき

- まず基本的な設定の流れを確認したい場合は、[対応マテリアルを割り当てる（マテリアル再割当て）](/tutorial/material-reassignment) を参照してください。
