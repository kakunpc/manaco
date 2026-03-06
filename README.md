# manaco-docs

MANACO 紹介サイトを Docusaurus で管理するリポジトリです。  
`main` へ push すると GitHub Pages に自動デプロイされます。

## 必要環境
- Node.js 20 以上
- npm

## ローカル起動
```bash
npm ci
npm run start
```

## ビルド確認
```bash
npm run build
```

## ドキュメント更新フロー
1. `Docs/*.md` を編集
2. ローカルで表示確認（`npm run start`）
3. ビルド確認（`npm run build`）
4. PR を作成
5. `doc` へマージ後、GitHub Pages に自動反映

## ページ構成
- `Docs/features.md`
- `Docs/install.md`
- `Docs/tutorial.md`
- `Docs/tutorial-material-reassignment.md`
- `Docs/tutorial-copy-eye-assignment.md`
- `Docs/distributor-avatar-preset.md`
- `Docs/distributor-material-definition.md`
- `Docs/cautions.md`
