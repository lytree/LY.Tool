import { createApp } from 'vue';
import '@lybox/sdk/css';
import { restoreTheme } from '@lybox/sdk';
import App from './App.vue';

// 恢复上次保存的主题
restoreTheme();

createApp(App).mount('#app');
