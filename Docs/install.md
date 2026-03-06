---
title: インストール方法
sidebar_position: 2
slug: /install
---

## 前提環境
- Unity `2022.3` 系
- VRChat SDK Avatars `>=3.7.0 <3.11.0`
- NDMF（`nadena.dev.ndmf`） `>=1.8.0 <2.0.0`

## 方法1: VCC（推奨）

1. VPMリポジトリページを開く  
   [https://kakunpc.github.io/kakunpc_vpm/](https://kakunpc.github.io/kakunpc_vpm/)
2. `Add to VCC` をクリック
3. VCCの `Add Repository` ダイアログで内容を確認し、`I Understand, Add Repository` を実行
4. 対象プロジェクトの `Manage Project` を開く
5. パッケージ一覧から `MANACO` を追加（`+` ボタン）

## 方法2: UnityPackage

1. リリースから `Manaco.unitypackage` を取得
2. Unityプロジェクトへインポート
3. インポート後、依存関係（VRChat SDK / NDMF）の解決状態を確認

## インストール確認

1. UnityでHierarchy上のアバターを選択
2. 右クリックメニューから `ちゃとらとりー/Manaco(まなこ)` が表示されるか確認
3. 追加後、`Manaco` コンポーネントのInspectorが表示されれば導入完了
