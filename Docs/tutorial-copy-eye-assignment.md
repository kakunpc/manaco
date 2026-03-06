---
title: "他アバターの目の割当"
sidebar_label: "他アバターの目の割当"
sidebar_position: 4
slug: /tutorial/copy-eye-assignment
---

## 目的
このチュートリアルでは、別アバターの目表現を参照しながら、コピー先アバターへ割り当ててアップロード準備するまでの流れを説明します。

## 手順

1. Hierarchyで対象アバターを右クリック
2. `ちゃとらとりー/Manaco(まなこ)` を実行
3. 生成された `Manaco` オブジェクトを選択
4. `Mode` を `CopyEyeFromAvatar` に変更
5. `Source Avatar` にコピー元アバターPrefabを指定
6. コピー先アバター側の `Advanced Settings` を開き、適用先の `EyeRegion` を確認または調整
7. 必要に応じて `UV エディタを開く` からコピー先の左右の目領域を調整
8. 反映結果を確認し、必要なら `Custom Material` や各 `EyeRegion` を微調整
9. `NDMF Preview` で見た目を確認
10. 問題なければVRChatへアップロード

## 注意

- コピー先の設定はプリセットなしでも始められますが、コピー元側の構成によっては追加設定が必要です。
- `CopyEyeFromAvatar` モードでは、コピー元アバターとコピー元プリセットの整合が必要になる場合があります。
