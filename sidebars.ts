import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  tutorialSidebar: [
    'features',
    'install',
    {
      type: 'category',
      label: 'チュートリアル',
      link: {
        type: 'doc',
        id: 'tutorial',
      },
      collapsible: true,
      collapsed: false,
      items: [
        'tutorial-material-reassignment',
        'tutorial-copy-eye-assignment',
      ],
    },
    {
      type: 'category',
      label: 'アセット配布者向け',
      collapsible: true,
      collapsed: false,
      items: [
        'distributor-avatar-preset',
        'distributor-material-definition',
      ],
    },
    'cautions',
  ],
};

export default sidebars;
