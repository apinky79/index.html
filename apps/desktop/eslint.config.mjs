import config from '@marketdna/eslint-config';

export default [
  ...config,
  {
    ignores: ['dist/**', 'dist-electron/**'],
  },
];
