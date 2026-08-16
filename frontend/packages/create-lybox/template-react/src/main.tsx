import React from 'react';
import ReactDOM from 'react-dom/client';
import '@lytree/sdk/css';
import { restoreTheme } from '@lytree/sdk';
import App from './App';

// 恢复上次保存的主题
restoreTheme();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
