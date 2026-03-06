import type {Config} from '@docusaurus/types';
import {themes as prismThemes} from 'prism-react-renderer';

const config: Config = {
  title: 'MANACO',
  tagline: 'VRChat向けUnity Editor拡張',
  favicon: 'img/logo.svg',

  url: 'https://kakunpc.github.io',
  baseUrl: process.env.NODE_ENV === 'development' ? '/' : '/manaco/',

  organizationName: 'kakunpc',
  projectName: 'manaco',

  onBrokenLinks: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },

  i18n: {
    defaultLocale: 'ja',
    locales: ['ja'],
  },

  trailingSlash: false,

  presets: [
    [
      'classic',
      {
        docs: {
          path: 'Docs',
          sidebarPath: './sidebars.ts',
          routeBasePath: '/',
          include: [
            'features.md',
            'install.md',
            'tutorial.md',
            'tutorial-material-reassignment.md',
            'tutorial-copy-eye-assignment.md',
            'distributor-avatar-preset.md',
            'distributor-material-definition.md',
            'cautions.md',
          ],
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      },
    ],
  ],

  themeConfig: {
    navbar: {
      title: 'MANACO',
      items: [
        {
          to: '/features',
          label: 'ドキュメント',
          position: 'left',
        },
        {
          href: 'https://github.com/kakunpc/manaco',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [],
      copyright: `Copyright © ${new Date().getFullYear()} MANACO`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  },
};

export default config;
