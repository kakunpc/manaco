---
title: インストール方法
sidebar_position: 2
slug: /install
---

## このページでやること

MANACOを使い始める前に、必要なものを確認して、Unityプロジェクトへ導入します。  
初めて触る場合は、まずこのページの手順どおりに進めてください。

> [こういう画像を配置: インストール完了までの流れがひと目でわかる全体図]

## 先に確認すること

- Unity `2022.3` 系
- VRChat SDK Avatars `>=3.7.0 <3.11.0`
- NDMF（`nadena.dev.ndmf`） `>=1.8.0 <2.0.0`

## おすすめの導入方法

初めて使う場合は、`VCC・ALCOM` での導入をおすすめします。  
依存関係をまとめて管理しやすく、更新もしやすいためです。

## 方法1: VCC・ALCOMで入れる（推奨）

### 1. VPMリポジトリを追加する

<a className="button button--primary button--lg" href="vcc://vpm/addRepo?url=https%3A%2F%2Fkakunpc.github.io%2Fkakunpc_vpm%2Findex.json">VCC・ALCOMに追加</a>

<div style={{ marginBottom: '1.25rem' }} />

1. 上の `VCC・ALCOMに追加` ボタンをクリック
2. `VCC` が起動した場合は `Add Repository` ダイアログの内容を確認し、`I Understand, Add Repository` をクリックする

![VCC の Add Repository ダイアログ](../Images/vcc-add_repository.png)

3. `ALCOM` が起動した場合は、表示された確認ダイアログでリポジトリ追加を実行する


> [こういう画像を配置: ALCOM 側でリポジトリ追加を確認する画面]

### 2. プロジェクトにMANACOを追加する

1. `VCC` または `ALCOM` で対象プロジェクトを開く
2. パッケージ一覧から `MANACO` を探す
3. `+` ボタンで追加する

> [こういう画像を配置: VCC・ALCOM で MANACO を追加する場所]

### 3. Unityで導入を確認する

1. Unityで対象プロジェクトを開く
2. Hierarchy上のアバターを右クリックする
3. `ちゃとらとりー/Manaco(まなこ)` が表示されるか確認する

> [こういう画像を配置: Hierarchy 右クリックメニューに `ちゃとらとりー/Manaco(まなこ)` が見えている状態]

## 方法2: UnityPackage

`VCC・ALCOM` を使わない場合だけ、この方法を使ってください。

1. リリースから `Manaco.unitypackage` を取得する
2. Unityプロジェクトへインポートする
3. インポート後、依存関係（VRChat SDK / NDMF）の解決状態を確認する

> [こういう画像を配置: `Manaco.unitypackage` を Unity にインポートしている画面]

## インストール確認

1. UnityでHierarchy上のアバターを選択
2. 右クリックメニューから `ちゃとらとりー/Manaco(まなこ)` が表示されるか確認
3. 追加後、`Manaco` コンポーネントのInspectorが表示されれば導入完了

> [こういう画像を配置: `Manaco` コンポーネントが Inspector に表示されている状態]

## 次に読むページ

インストールできたら、次は [チュートリアル](/tutorial) に進んでください。
