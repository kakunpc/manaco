import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import React from 'react';

import styles from './index.module.css';

const VCC_URL =
  'vcc://vpm/addRepo?url=https%3A%2F%2Fkakunpc.github.io%2Fkakunpc_vpm%2Findex.json';
const BOOTH_URL = 'https://booth.pm/ja/search/MANACO';

export default function Home(): JSX.Element {
  return (
    <Layout
      title="MANACO"
      description="MANACO: VRChat向けUnity Editor拡張"
    >
      <main className={styles.page}>
        <section className={styles.hero}>
          <div className={styles.glowA} aria-hidden="true" />
          <div className={styles.glowB} aria-hidden="true" />

          <p className={styles.kicker}>VRChat / Unity Editor Extension</p>
          <h1 className={styles.title}>MANACO</h1>
          <p className={styles.subtitle}>Material Assign Non-destructive Assistant for Customization Operations</p>
          <p className={styles.lead}>目のマテリアル再割り当てを、非破壊で。</p>

          <div className={styles.ctaRow}>
            <Link className={`${styles.cta} ${styles.ctaPrimary}`} to={VCC_URL}>
              VCC・ALCOMに追加
            </Link>
            <Link className={`${styles.cta} ${styles.ctaGhost}`} to={BOOTH_URL}>
              Boothからダウンロード
            </Link>
            <Link className={`${styles.cta} ${styles.ctaGhost}`} to="/tutorial">
              チュートリアル
            </Link>
          </div>
        </section>

        <section className={styles.featureSection}>
          <div className={styles.featureGrid}>
            <article className={styles.featureCard}>
              <p className={styles.featureTitle}>非破壊ワークフロー</p>
              <p className={styles.featureText}>
                Non-Destructive Modular Frameworkで、アバターの元データに影響なく使えます。
              </p>
            </article>
            <article className={styles.featureCard}>
              <p className={styles.featureTitle}>かんたん操作</p>
              <p className={styles.featureText}>
                専用エディターから目と瞳孔を選ぶだけ。マウス操作中心で設定できます。
              </p>
            </article>
            <article className={styles.featureCard}>
              <p className={styles.featureTitle}>プレビュー対応</p>
              <p className={styles.featureText}>
                プレビュー機能により、ビルド前でも見た目を確認できます。
              </p>
            </article>
            <article className={styles.featureCard}>
              <p className={styles.featureTitle}>目の移植機能</p>
              <p className={styles.featureText}>
                別アバターの目表現をコピーして、移植用途にも使えます。
              </p>
            </article>
          </div>
        </section>
      </main>
    </Layout>
  );
}
