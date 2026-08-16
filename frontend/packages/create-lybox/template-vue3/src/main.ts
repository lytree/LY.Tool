import { createApp } from 'vue';
import '@lytree/sdk/css';
import '@lytree/sdk/components';
import { restoreTheme } from '@lytree/sdk';
import App from './App.vue';

// 恢复上次保存的主题
restoreTheme();

createApp(App).mount('#app');
